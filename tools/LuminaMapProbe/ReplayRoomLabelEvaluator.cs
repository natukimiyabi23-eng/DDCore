using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed record ReplayRoomLabelSet(int SchemaVersion, string ReplaySha256, string PlanmapSha256, List<ReplayRoomLabel> Labels);

public sealed record ReplayRoomLabel(int SnapshotIndex, int ExactFloor, string Variant, int RoomIndex, string Evidence);

public readonly record struct ReplayRoomCandidateBox(string Layer, int Index, double MinX, double MaxX, double MinZ, double MaxZ);

public sealed record ReplayRoomBoxTraceCandidate(string Variant, int RoomIndex);

public sealed record ReplayRoomBoxTraceEntry(int SnapshotIndex, string Status, string? FloorRange, IReadOnlyList<ReplayRoomBoxTraceCandidate> Candidates);

public sealed record ReplayRoomLabelSummary(
    int LabelCount,
    int MatchedExpectedCandidateCount,
    int AmbiguousCandidateCount,
    int OtherCandidateCount,
    int UnmatchedCount,
    int InvalidPoseCount,
    int UntrustedPoseCount,
    int ContextConflictCount,
    int ScreenshotEvidenceCount,
    int UserConfirmedEvidenceCount,
    double? MatchRate);

public static class ReplayRoomLabelEvaluator
{
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static ReplayRoomLabelSummary Evaluate(
        JsonElement replay,
        string actualReplaySha256,
        string actualPlanmapSha256,
        ReplayRoomLabelSet labelSet,
        IReadOnlyList<ReplayRoomCandidateBox> candidateBoxes)
    {
        if (labelSet.SchemaVersion != 1
            || labelSet.Labels is null
            || labelSet.Labels.Count is 0 or > 1000
            || labelSet.Labels.Any(static label => label is null))
            throw new InvalidDataException("Labels must use schema 1 and contain 1 to 1000 rows.");

        if (!string.Equals(labelSet.ReplaySha256, actualReplaySha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The labels replaySha256 does not match the replay bytes.");

        if (!string.Equals(labelSet.PlanmapSha256, actualPlanmapSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The labels planmapSha256 does not match the extracted planmap bytes.");

        if (candidateBoxes.Any(static box => box.Index is < 0 or >= 25
            || box.Layer is not ("LVD_Room_A" or "LVD_Room_B")
            || !double.IsFinite(box.MinX)
            || !double.IsFinite(box.MaxX)
            || !double.IsFinite(box.MinZ)
            || !double.IsFinite(box.MaxZ)
            || box.MinX > box.MaxX
            || box.MinZ > box.MaxZ))
            throw new InvalidDataException("Candidate bounds are invalid.");

        if (replay.ValueKind != JsonValueKind.Object
            || !replay.TryGetProperty("schemaVersion", out var schema)
            || schema.ValueKind != JsonValueKind.Number
            || !schema.TryGetInt32(out var schemaVersion)
            || schemaVersion != 5
            || !replay.TryGetProperty("snapshots", out var snapshots)
            || snapshots.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Only schema-5 replay data with a snapshots array is supported.");

        var duplicate = labelSet.Labels.GroupBy(static label => label.SnapshotIndex).Any(static group => group.Count() > 1);
        if (duplicate)
            throw new InvalidDataException("Each snapshotIndex may be labeled only once.");

        var matched = 0;
        var ambiguous = 0;
        var other = 0;
        var unmatched = 0;
        var invalidPose = 0;
        var untrustedPose = 0;
        var contextConflict = 0;
        var screenshotEvidence = 0;
        var userConfirmedEvidence = 0;

        foreach (var label in labelSet.Labels)
        {
            if (label.SnapshotIndex < 0 || label.SnapshotIndex >= snapshots.GetArrayLength())
                throw new InvalidDataException("A label snapshotIndex is outside the replay.");
            if (label.ExactFloor < 1 || label.ExactFloor > 100 || label.RoomIndex is < 0 or >= 25)
                throw new InvalidDataException("A label floor or room index is outside the supported range.");
            if (label.Variant is not ("A" or "B"))
                throw new InvalidDataException("A label variant must be A or B.");
            if (label.Evidence == "userScreenshot")
                screenshotEvidence++;
            else if (label.Evidence == "userConfirmed")
                userConfirmedEvidence++;
            else
                throw new InvalidDataException("Evidence must be userScreenshot or userConfirmed.");

            var expectedLayer = $"LVD_Room_{label.Variant}";
            if (!candidateBoxes.Any(box => box.Layer == expectedLayer && box.Index == label.RoomIndex))
                throw new InvalidDataException("A label points to a room index absent from the extracted static candidate data.");

            var snapshot = snapshots[label.SnapshotIndex];
            if (!HasCompatibleContext(snapshot, label.ExactFloor))
            {
                contextConflict++;
                continue;
            }

            if (!TryReadPose(snapshot, out var x, out var z))
            {
                invalidPose++;
                continue;
            }

            if (!HasTrustedPoseMetadata(snapshot))
            {
                untrustedPose++;
                continue;
            }

            var hits = candidateBoxes.Where(box => x >= box.MinX && x <= box.MaxX && z >= box.MinZ && z <= box.MaxZ).ToArray();
            var expectedHit = hits.Any(box => box.Layer == expectedLayer && box.Index == label.RoomIndex);
            if (expectedHit && hits.Length == 1)
                matched++;
            else if (expectedHit)
                ambiguous++;
            else if (hits.Length > 0)
                other++;
            else
                unmatched++;
        }

        var evaluableCount = labelSet.Labels.Count - invalidPose - untrustedPose - contextConflict;
        return new ReplayRoomLabelSummary(
            labelSet.Labels.Count,
            matched,
            ambiguous,
            other,
            unmatched,
            invalidPose,
            untrustedPose,
            contextConflict,
            screenshotEvidence,
            userConfirmedEvidence,
            evaluableCount == 0 ? null : (double)matched / evaluableCount);
    }

    public static IReadOnlyList<ReplayRoomBoxTraceEntry> CreateBoxTrace(JsonElement replay, IReadOnlyList<ReplayRoomCandidateBox> candidateBoxes)
    {
        ValidateReplayAndCandidates(replay, candidateBoxes);
        var snapshots = replay.GetProperty("snapshots");
        var trace = new List<ReplayRoomBoxTraceEntry>(snapshots.GetArrayLength());

        for (var index = 0; index < snapshots.GetArrayLength(); index++)
        {
            var snapshot = snapshots[index];
            if (!TryReadFloorRange(snapshot, out var floorRange))
            {
                trace.Add(new(index, "contextUnknown", null, []));
                continue;
            }

            if (!TryReadPose(snapshot, out var x, out var z))
            {
                trace.Add(new(index, "invalidPose", floorRange, []));
                continue;
            }

            if (!HasTrustedPoseMetadata(snapshot))
            {
                trace.Add(new(index, "untrustedPose", floorRange, []));
                continue;
            }

            var candidates = candidateBoxes
                .Where(box => x >= box.MinX && x <= box.MaxX && z >= box.MinZ && z <= box.MaxZ)
                .Select(static box => new ReplayRoomBoxTraceCandidate(box.Layer[^1].ToString(), box.Index))
                .Distinct()
                .OrderBy(static candidate => candidate.Variant, StringComparer.Ordinal)
                .ThenBy(static candidate => candidate.RoomIndex)
                .ToArray();
            var status = candidates.Length switch
            {
                0 => "noBoxContainment",
                1 => "uniqueBoxContainment",
                _ => "ambiguousBoxContainment",
            };
            trace.Add(new(index, status, floorRange, candidates));
        }

        return trace;
    }

    private static void ValidateReplayAndCandidates(JsonElement replay, IReadOnlyList<ReplayRoomCandidateBox> candidateBoxes)
    {
        if (candidateBoxes.Count == 0
            || candidateBoxes.Any(static box => box.Index is < 0 or >= 25
            || box.Layer is not ("LVD_Room_A" or "LVD_Room_B")
            || !double.IsFinite(box.MinX)
            || !double.IsFinite(box.MaxX)
            || !double.IsFinite(box.MinZ)
            || !double.IsFinite(box.MaxZ)
            || box.MinX > box.MaxX
            || box.MinZ > box.MaxZ))
            throw new InvalidDataException("Candidate bounds are invalid.");

        if (replay.ValueKind != JsonValueKind.Object
            || !replay.TryGetProperty("schemaVersion", out var schema)
            || schema.ValueKind != JsonValueKind.Number
            || !schema.TryGetInt32(out var schemaVersion)
            || schemaVersion != 5
            || !replay.TryGetProperty("snapshots", out var snapshots)
            || snapshots.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Only schema-5 replay data with a snapshots array is supported.");
    }

    private static bool TryReadFloorRange(JsonElement snapshot, out string floorRange)
    {
        floorRange = string.Empty;
        if (snapshot.ValueKind != JsonValueKind.Object
            || !snapshot.TryGetProperty("dungeon", out var dungeon)
            || dungeon.ValueKind != JsonValueKind.Object
            || !dungeon.TryGetProperty("kind", out var kind)
            || kind.ValueKind != JsonValueKind.String
            || kind.GetString() != "eurekaOrthos"
            || !dungeon.TryGetProperty("floorRange", out var range)
            || range.ValueKind != JsonValueKind.Object
            || !range.TryGetProperty("start", out var start)
            || !range.TryGetProperty("end", out var end)
            || !start.TryGetInt32(out var first)
            || !end.TryGetInt32(out var last)
            || first < 1
            || last < first)
            return false;

        floorRange = $"{first}-{last}";
        return true;
    }

    private static bool HasCompatibleContext(JsonElement snapshot, int exactFloor)
    {
        if (snapshot.ValueKind != JsonValueKind.Object
            || !snapshot.TryGetProperty("dungeon", out var dungeon)
            || dungeon.ValueKind != JsonValueKind.Object
            || !dungeon.TryGetProperty("kind", out var kind)
            || kind.ValueKind != JsonValueKind.String
            || kind.GetString() != "eurekaOrthos"
            || !dungeon.TryGetProperty("floorRange", out var range)
            || range.ValueKind != JsonValueKind.Object
            || !range.TryGetProperty("start", out var start)
            || !range.TryGetProperty("end", out var end)
            || !start.TryGetInt32(out var floorStart)
            || !end.TryGetInt32(out var floorEnd)
            || exactFloor < floorStart
            || exactFloor > floorEnd)
            return false;

        return !dungeon.TryGetProperty("floor", out var currentFloor)
            || currentFloor.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            || (currentFloor.ValueKind == JsonValueKind.Number && currentFloor.TryGetInt32(out var recordedFloor) && recordedFloor == exactFloor);
    }

    private static bool TryReadPose(JsonElement snapshot, out double x, out double z)
    {
        x = 0;
        z = 0;
        if (snapshot.ValueKind != JsonValueKind.Object
            || !snapshot.TryGetProperty("playerPose", out var pose)
            || pose.ValueKind != JsonValueKind.Object
            || !pose.TryGetProperty("position", out var position)
            || position.ValueKind != JsonValueKind.Object
            || !position.TryGetProperty("isFinite", out var isFinite)
            || isFinite.ValueKind != JsonValueKind.True
            || !position.TryGetProperty("x", out var xElement)
            || !position.TryGetProperty("y", out var zElement)
            || !xElement.TryGetDouble(out x)
            || !zElement.TryGetDouble(out z)
            || !double.IsFinite(x)
            || !double.IsFinite(z))
            return false;

        return true;
    }

    private static bool HasTrustedPoseMetadata(JsonElement snapshot) =>
        snapshot.TryGetProperty("playerPose", out var pose)
        && pose.ValueKind == JsonValueKind.Object
        && pose.TryGetProperty("metadata", out var metadata)
        && metadata.ValueKind == JsonValueKind.Object
        && metadata.TryGetProperty("source", out var source)
        && source.ValueKind == JsonValueKind.String
        && source.GetString() == "publicDalamud"
        && metadata.TryGetProperty("quality", out var quality)
        && quality.ValueKind == JsonValueKind.String
        && quality.GetString() == "direct";
}
