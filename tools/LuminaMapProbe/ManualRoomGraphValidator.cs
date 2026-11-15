using System.Text.Json;
using System.Text.Json.Serialization;
using DDCore.Core.Navigation;

public sealed record ManualRoomGraphLabelSet(
    int SchemaVersion,
    string ReplaySha256,
    string PlanmapSha256,
    string? EvidenceSha256,
    int SnapshotIndex,
    int? ExactFloor,
    string? Variant,
    int? CurrentRoom,
    List<ManualRoomPresenceLabel> Rooms,
    List<ManualPassageLabel> Passages);

public sealed record ManualRoomPresenceLabel(int Room, string State, string Evidence, string? EvidenceSha256 = null, int? EvidenceSnapshotIndex = null);

public sealed record ManualPassageLabel(
    int RoomA,
    int RoomB,
    string State,
    string Evidence,
    string? EvidenceSha256 = null,
    int? EvidenceSnapshotIndex = null);

public sealed record ManualRoomGraphValidation(
    string Status,
    string ReasonCode,
    int PresentRoomCount,
    int UnknownRoomCount,
    int OpenPassageCount,
    int UnknownPassageCount,
    IReadOnlyList<int> Route,
    bool GraphComplete,
    bool IsOfflineOnly,
    string EvidenceTrust);

public static class ManualRoomGraphValidator
{
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static ManualRoomGraphValidation Evaluate(
        ManualRoomGraphLabelSet labels,
        string actualReplaySha256,
        string actualPlanmapSha256,
        JsonElement replay,
        IReadOnlyList<ReplayRoomCandidateBox> candidateBoxes,
        IReadOnlySet<string> availableEvidenceSha256,
        int destinationRoom)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(candidateBoxes);
        ArgumentNullException.ThrowIfNull(availableEvidenceSha256);
        ValidateHash(labels.ReplaySha256, nameof(labels.ReplaySha256));
        ValidateHash(labels.PlanmapSha256, nameof(labels.PlanmapSha256));
        if (!string.Equals(labels.ReplaySha256, actualReplaySha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(labels.PlanmapSha256, actualPlanmapSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Graph labels do not match the exact replay and planmap inputs.");
        if (labels.SchemaVersion != 3)
            throw new InvalidDataException("Graph labels have an unsupported schema.");
        if (candidateBoxes.Any(static box => box.Index is < 0 or >= RoomPathfinder.RoomCount
            || box.Layer is not ("LVD_Room_A" or "LVD_Room_B")
            || !double.IsFinite(box.MinX) || !double.IsFinite(box.MaxX)
            || !double.IsFinite(box.MinZ) || !double.IsFinite(box.MaxZ)
            || box.MinX > box.MaxX || box.MinZ > box.MaxZ))
            throw new InvalidDataException("Static room candidate bounds are invalid.");
        ValidateReplaySnapshot(replay, labels);
        ValidateRoomLabels(labels.Rooms, availableEvidenceSha256);
        ValidatePassages(labels.Passages, labels.Rooms, availableEvidenceSha256);
        var roomStates = labels.Rooms.ToDictionary(static room => room.Room, static room => room.State);
        var hasUnknownLabels = labels.Rooms.Any(static room => room.State == "unknown")
            || labels.Passages.Any(static passage => passage.State == "unknown");

        if (labels.ExactFloor is null || labels.Variant is null or not ("A" or "B") || labels.CurrentRoom is null || labels.EvidenceSha256 is null)
            return Result("incomplete", "graph.context-unknown", labels, [], false);
        if (labels.ExactFloor is < 1 or > 100 || labels.CurrentRoom is < 0 or >= RoomPathfinder.RoomCount || destinationRoom is < 0 or >= RoomPathfinder.RoomCount)
            throw new InvalidDataException("The exact floor or route endpoint is outside the supported range.");
        ValidateHash(labels.EvidenceSha256, nameof(labels.EvidenceSha256));
        if (!availableEvidenceSha256.Contains(labels.EvidenceSha256))
            return Result("unavailable", "evidence.context-image-not-found", labels, [], true);
        ValidateScreenshotContexts(labels, replay, candidateBoxes);

        if (roomStates[labels.CurrentRoom.Value] != "present" || roomStates[destinationRoom] != "present")
            return Result("unavailable", "route.endpoint-not-present", labels, [], true);

        if (!IsCurrentRoomCandidateSupported(labels, candidateBoxes, replay, out var currentRoomReason))
            return Result("unavailable", currentRoomReason, labels, [], true);

        var map = new RoomConnectionFlags[RoomPathfinder.RoomCount];
        foreach (var passage in labels.Passages.Where(static passage => passage.State == "open"))
        {
            if (roomStates[passage.RoomA] != "present" || roomStates[passage.RoomB] != "present")
                throw new InvalidDataException("An open passage requires both endpoint rooms to be explicitly labeled present.");
            var (flagA, flagB) = DirectionFlags(passage.RoomA, passage.RoomB);
            map[passage.RoomA] |= flagA;
            map[passage.RoomB] |= flagB;
        }

        var path = RoomPathfinder.FindPath(map, labels.CurrentRoom.Value, destinationRoom);
        if (labels.CurrentRoom == destinationRoom)
            return Result("same-room", "route.start-equals-destination", labels, [], !hasUnknownLabels);
        if (path.Count == 0)
            return hasUnknownLabels
                ? Result("incomplete", "route.no-known-open-path-in-incomplete-graph", labels, [], false)
                : Result("no-route", "route.no-open-path", labels, [], true);
        return hasUnknownLabels
            ? Result("offline-partial-route", "route.uses-only-explicitly-open-labeled-passages", labels, path, false)
            : Result("offline-route", "route.validated-from-user-labels", labels, path, true);
    }

    private static void ValidateReplaySnapshot(JsonElement replay, ManualRoomGraphLabelSet labels)
    {
        if (replay.ValueKind != JsonValueKind.Object
            || !replay.TryGetProperty("schemaVersion", out var schema)
            || !schema.TryGetInt32(out var schemaVersion)
            || schemaVersion != 5
            || !replay.TryGetProperty("snapshots", out var snapshots)
            || snapshots.ValueKind != JsonValueKind.Array
            || labels.SnapshotIndex < 0
            || labels.SnapshotIndex >= snapshots.GetArrayLength())
            throw new InvalidDataException("The replay schema or labeled snapshot index is invalid.");

        var snapshot = snapshots[labels.SnapshotIndex];
        var contextCompatible = labels.ExactFloor is { } floor
            ? ReplayRoomLabelEvaluator.HasCompatibleContext(snapshot, floor)
            : ReplayRoomLabelEvaluator.TryReadFloorRange(snapshot, out _);
        if (!contextCompatible
            || !ReplayRoomLabelEvaluator.TryReadPose(snapshot, out _, out _)
            || !ReplayRoomLabelEvaluator.HasTrustedPoseMetadata(snapshot))
            throw new InvalidDataException("The labeled replay snapshot lacks compatible EO floor context or a direct public pose.");
    }

    private static void ValidateRoomLabels(List<ManualRoomPresenceLabel> rooms, IReadOnlySet<string> availableEvidenceSha256)
    {
        if (rooms is null || rooms.Count != RoomPathfinder.RoomCount
            || rooms.Any(static room => room is null)
            || rooms.Select(static room => room.Room).Distinct().Count() != RoomPathfinder.RoomCount
            || rooms.Any(room => room.Room is < 0 or >= RoomPathfinder.RoomCount
                || room.State is not ("present" or "absent" or "unknown")
                || !HasValidEvidence(room.State, room.Evidence, room.EvidenceSha256, room.EvidenceSnapshotIndex, availableEvidenceSha256)))
            throw new InvalidDataException("Room labels must cover each of 25 cells exactly once with valid state/evidence.");
    }

    private static void ValidatePassages(
        List<ManualPassageLabel> passages,
        List<ManualRoomPresenceLabel> rooms,
        IReadOnlySet<string> availableEvidenceSha256)
    {
        if (passages is null)
            throw new InvalidDataException("Passage labels are required.");

        var expected = new HashSet<(int A, int B)>();
        for (var room = 0; room < RoomPathfinder.RoomCount; room++)
        {
            if (room % RoomPathfinder.GridWidth < RoomPathfinder.GridWidth - 1)
                expected.Add((room, room + 1));
            if (room / RoomPathfinder.GridWidth < RoomPathfinder.GridWidth - 1)
                expected.Add((room, room + RoomPathfinder.GridWidth));
        }

        if (passages.Count != expected.Count
            || passages.Any(static passage => passage is null)
            || passages.Any(passage => passage.RoomA < 0 || passage.RoomB >= RoomPathfinder.RoomCount
                || passage.RoomA >= passage.RoomB
                || !IsAdjacent(passage.RoomA, passage.RoomB)
                || passage.State is not ("open" or "closed" or "unknown")
                || !HasValidEvidence(passage.State, passage.Evidence, passage.EvidenceSha256, passage.EvidenceSnapshotIndex, availableEvidenceSha256))
            || passages.Select(static passage => (passage.RoomA, passage.RoomB)).Distinct().Count() != expected.Count
            || passages.Any(passage => !expected.Contains((passage.RoomA, passage.RoomB))))
            throw new InvalidDataException("Passage labels must cover each of the 40 orthogonal cell pairs exactly once.");

        var roomStates = rooms.ToDictionary(static room => room.Room, static room => room.State);
        if (passages.Any(passage => passage.State == "open"
            && (roomStates[passage.RoomA] != "present" || roomStates[passage.RoomB] != "present")))
            throw new InvalidDataException("An open passage requires both endpoint rooms to be explicitly present.");
    }

    private static void ValidateScreenshotContexts(
        ManualRoomGraphLabelSet labels,
        JsonElement replay,
        IReadOnlyList<ReplayRoomCandidateBox> candidateBoxes)
    {
        var snapshots = replay.GetProperty("snapshots");
        var screenshotIndexes = labels.Rooms
            .Where(static room => room.Evidence == "userScreenshot")
            .Select(static room => room.EvidenceSnapshotIndex!.Value)
            .Concat(labels.Passages
                .Where(static passage => passage.Evidence == "userScreenshot")
                .Select(static passage => passage.EvidenceSnapshotIndex!.Value));
        var expectedLayer = $"LVD_Room_{labels.Variant}";

        foreach (var index in screenshotIndexes.Distinct())
        {
            if (index >= snapshots.GetArrayLength())
                throw new InvalidDataException("A screenshot-backed label references a missing replay snapshot.");

            var snapshot = snapshots[index];
            if (!ReplayRoomLabelEvaluator.HasCompatibleContext(snapshot, labels.ExactFloor!.Value)
                || !ReplayRoomLabelEvaluator.TryReadPose(snapshot, out var x, out var z)
                || !ReplayRoomLabelEvaluator.HasTrustedPoseMetadata(snapshot))
                throw new InvalidDataException("A screenshot-backed label snapshot lacks matching floor context or a trusted direct pose.");

            var candidates = candidateBoxes
                .Where(box => x >= box.MinX && x <= box.MaxX && z >= box.MinZ && z <= box.MaxZ)
                .Select(static box => (box.Layer, box.Index))
                .Distinct()
                .ToArray();
            if (candidates.Length != 1 || candidates[0].Layer != expectedLayer)
                throw new InvalidDataException("A screenshot-backed label snapshot does not uniquely support the graph layout family.");
        }
    }

    private static bool IsCurrentRoomCandidateSupported(
        ManualRoomGraphLabelSet labels,
        IReadOnlyList<ReplayRoomCandidateBox> candidateBoxes,
        JsonElement replay,
        out string reasonCode)
    {
        reasonCode = "route.current-room-candidate-mismatch";
        var snapshot = replay.GetProperty("snapshots")[labels.SnapshotIndex];
        if (!ReplayRoomLabelEvaluator.TryReadPose(snapshot, out var x, out var z))
        {
            reasonCode = "route.current-pose-invalid";
            return false;
        }

        var layer = $"LVD_Room_{labels.Variant}";
        var hits = candidateBoxes
            .Where(box => x >= box.MinX && x <= box.MaxX && z >= box.MinZ && z <= box.MaxZ)
            .Select(static box => (box.Layer, box.Index))
            .Distinct()
            .ToArray();
        if (hits.Length != 1)
        {
            reasonCode = hits.Length == 0 ? "route.current-pose-outside-candidates" : "route.current-pose-ambiguous";
            return false;
        }

        return hits[0] == (layer, labels.CurrentRoom!.Value);
    }

    private static (RoomConnectionFlags A, RoomConnectionFlags B) DirectionFlags(int a, int b)
    {
        if (b == a + RoomPathfinder.GridWidth)
            return (RoomConnectionFlags.South, RoomConnectionFlags.North);
        if (b == a + 1 && a / RoomPathfinder.GridWidth == b / RoomPathfinder.GridWidth)
            return (RoomConnectionFlags.East, RoomConnectionFlags.West);
        throw new InvalidDataException("Passage rooms are not orthogonally adjacent.");
    }

    private static bool IsAdjacent(int a, int b) =>
        b == a + RoomPathfinder.GridWidth
        || (b == a + 1 && a / RoomPathfinder.GridWidth == b / RoomPathfinder.GridWidth);

    private static bool HasValidEvidence(
        string state,
        string evidence,
        string? evidenceSha256,
        int? evidenceSnapshotIndex,
        IReadOnlySet<string> availableEvidenceSha256)
    {
        if (state == "unknown")
            return evidence == "notObserved" && evidenceSha256 is null && evidenceSnapshotIndex is null;

        if (evidence == "userConfirmed")
            return evidenceSha256 is null && evidenceSnapshotIndex is null;

        if (evidence != "userScreenshot" || evidenceSha256 is null || evidenceSnapshotIndex is null || evidenceSnapshotIndex < 0)
            return false;

        try
        {
            ValidateHash(evidenceSha256, nameof(evidenceSha256));
            return availableEvidenceSha256.Contains(evidenceSha256);
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static void ValidateHash(string value, string name)
    {
        if (value is null || value.Length != 64 || value.Any(static character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException($"{name} must be a 64-character SHA-256 hex value.");
    }

    public static ManualRoomGraphLabelSet CreateUnknownTemplate(string replaySha256, string planmapSha256, JsonElement replay, int snapshotIndex)
    {
        ValidateHash(replaySha256, nameof(replaySha256));
        ValidateHash(planmapSha256, nameof(planmapSha256));
        if (replay.ValueKind != JsonValueKind.Object
            || !replay.TryGetProperty("schemaVersion", out var schema)
            || !schema.TryGetInt32(out var schemaVersion)
            || schemaVersion != 5
            || !replay.TryGetProperty("snapshots", out var snapshots)
            || snapshots.ValueKind != JsonValueKind.Array
            || snapshotIndex < 0
            || snapshotIndex >= snapshots.GetArrayLength())
            throw new InvalidDataException("A schema-5 replay and valid snapshot index are required.");

        var snapshot = snapshots[snapshotIndex];
        if (!ReplayRoomLabelEvaluator.TryReadFloorRange(snapshot, out _)
            || !ReplayRoomLabelEvaluator.TryReadPose(snapshot, out _, out _)
            || !ReplayRoomLabelEvaluator.HasTrustedPoseMetadata(snapshot))
            throw new InvalidDataException("The selected snapshot must have an EO floor band and a direct public pose.");

        var rooms = Enumerable.Range(0, RoomPathfinder.RoomCount)
            .Select(static room => new ManualRoomPresenceLabel(room, "unknown", "notObserved"))
            .ToList();
        var passages = new List<ManualPassageLabel>(40);
        for (var room = 0; room < RoomPathfinder.RoomCount; room++)
        {
            if (room % RoomPathfinder.GridWidth < RoomPathfinder.GridWidth - 1)
                passages.Add(new(room, room + 1, "unknown", "notObserved"));
            if (room / RoomPathfinder.GridWidth < RoomPathfinder.GridWidth - 1)
                passages.Add(new(room, room + RoomPathfinder.GridWidth, "unknown", "notObserved"));
        }

        return new(3, replaySha256, planmapSha256, null, snapshotIndex, null, null, null, rooms, passages);
    }

    private static ManualRoomGraphValidation Result(
        string status,
        string reason,
        ManualRoomGraphLabelSet labels,
        IReadOnlyList<int> route,
        bool graphComplete) => new(
            status,
            reason,
            labels.Rooms.Count(static room => room.State == "present"),
            labels.Rooms.Count(static room => room.State == "unknown"),
            labels.Passages.Count(static passage => passage.State == "open"),
            labels.Passages.Count(static passage => passage.State == "unknown"),
            route,
            graphComplete,
            true,
            "local image hashes are verified; image meaning is user-asserted; offline only");
}
