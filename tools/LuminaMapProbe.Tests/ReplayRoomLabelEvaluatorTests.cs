using System.Text.Json;
using Xunit;

public sealed class ReplayRoomLabelEvaluatorTests
{
    private const string ReplaySha256 = "abc";
    private const string PlanmapSha256 = "def";

    [Fact]
    public void ExpectedCandidateContainingPoseIsMatched()
    {
        using var replay = CreateReplay("eurekaOrthos", 1, 10, null, 1.5, 1.5);

        var summary = ReplayRoomLabelEvaluator.Evaluate(
            replay.RootElement,
            ReplaySha256,
            PlanmapSha256,
            Labels(new ReplayRoomLabel(0, 2, "B", 4, "userScreenshot")),
            [new ReplayRoomCandidateBox("LVD_Room_B", 4, 0, 3, 0, 3)]);

        Assert.Equal(1, summary.MatchedExpectedCandidateCount);
        Assert.Equal(1d, summary.MatchRate);
    }

    [Fact]
    public void OverlappingCandidatesAreAmbiguous()
    {
        using var replay = CreateReplay("eurekaOrthos", 1, 10, null, 1, 1);

        var summary = ReplayRoomLabelEvaluator.Evaluate(
            replay.RootElement,
            ReplaySha256,
            PlanmapSha256,
            Labels(new ReplayRoomLabel(0, 2, "B", 4, "userConfirmed")),
            [
                new ReplayRoomCandidateBox("LVD_Room_B", 4, 0, 3, 0, 3),
                new ReplayRoomCandidateBox("LVD_Room_A", 4, 0, 3, 0, 3),
            ]);

        Assert.Equal(1, summary.AmbiguousCandidateCount);
        Assert.Equal(0, summary.MatchedExpectedCandidateCount);
    }

    [Fact]
    public void OtherCandidateAndNoCandidateRemainDistinct()
    {
        using var replay = CreateReplay("eurekaOrthos", 1, 10, null, 1, 1, 20, 20);

        var summary = ReplayRoomLabelEvaluator.Evaluate(
            replay.RootElement,
            ReplaySha256,
            PlanmapSha256,
            Labels(
                new ReplayRoomLabel(0, 1, "B", 4, "userScreenshot"),
                new ReplayRoomLabel(1, 1, "B", 4, "userScreenshot")),
            [
                new ReplayRoomCandidateBox("LVD_Room_B", 7, 0, 3, 0, 3),
                new ReplayRoomCandidateBox("LVD_Room_B", 4, 5, 8, 5, 8),
            ]);

        Assert.Equal(1, summary.OtherCandidateCount);
        Assert.Equal(1, summary.UnmatchedCount);
    }

    [Fact]
    public void FloorBandConflictIsExcludedFromMatchRate()
    {
        using var replay = CreateReplay("eurekaOrthos", 1, 10, null, 1, 1);

        var summary = ReplayRoomLabelEvaluator.Evaluate(
            replay.RootElement,
            ReplaySha256,
            PlanmapSha256,
            Labels(new ReplayRoomLabel(0, 11, "B", 4, "userConfirmed")),
            [new ReplayRoomCandidateBox("LVD_Room_B", 4, 0, 3, 0, 3)]);

        Assert.Equal(1, summary.ContextConflictCount);
        Assert.Null(summary.MatchRate);
    }

    [Fact]
    public void WrongReplayHashAndDuplicateIndicesAreRejected()
    {
        using var replay = CreateReplay("eurekaOrthos", 1, 10, null, 1, 1);
        var box = new[] { new ReplayRoomCandidateBox("LVD_Room_B", 4, 0, 3, 0, 3) };

        Assert.Throws<InvalidDataException>(() => ReplayRoomLabelEvaluator.Evaluate(
            replay.RootElement,
            "different",
            PlanmapSha256,
            Labels(new ReplayRoomLabel(0, 2, "B", 4, "userScreenshot")),
            box));
        Assert.Throws<InvalidDataException>(() => ReplayRoomLabelEvaluator.Evaluate(
            replay.RootElement,
            ReplaySha256,
            "different",
            Labels(new ReplayRoomLabel(0, 2, "B", 4, "userScreenshot")),
            box));
        Assert.Throws<InvalidDataException>(() => ReplayRoomLabelEvaluator.Evaluate(
            replay.RootElement,
            ReplaySha256,
            PlanmapSha256,
            Labels(new ReplayRoomLabel(0, 2, "B", 4, "userScreenshot"), new ReplayRoomLabel(0, 2, "B", 4, "userConfirmed")),
            box));
    }

    [Fact]
    public void UnknownLabelFieldsAreRejected()
    {
        const string json = """
            {"schemaVersion":1,"replaySha256":"abc","labels":[{"snapshotIndex":0,"exactFloor":2,"variant":"B","roomIndex":4,"evidence":"userScreenshot","characterName":"not-allowed"}]}
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ReplayRoomLabelSet>(json, ReplayRoomLabelEvaluator.JsonOptions));
    }

    [Fact]
    public void VersionedCamelCaseLabelFileIsAccepted()
    {
        const string json = """
            {"schemaVersion":1,"replaySha256":"abc","planmapSha256":"def","labels":[{"snapshotIndex":0,"exactFloor":2,"variant":"B","roomIndex":4,"evidence":"userScreenshot"}]}
            """;

        var labels = JsonSerializer.Deserialize<ReplayRoomLabelSet>(json, ReplayRoomLabelEvaluator.JsonOptions);

        Assert.NotNull(labels);
        Assert.Equal(1, labels!.SchemaVersion);
        Assert.Equal(PlanmapSha256, labels.PlanmapSha256);
        Assert.Single(labels.Labels);
    }

    [Fact]
    public void MissingPoseIsNotTreatedAsUnmatchedGeometry()
    {
        using var replay = JsonDocument.Parse("""
            {"schemaVersion":5,"snapshots":[{"dungeon":{"kind":"eurekaOrthos","floorRange":{"start":1,"end":10}},"playerPose":null}]}
            """);

        var summary = ReplayRoomLabelEvaluator.Evaluate(
            replay.RootElement,
            ReplaySha256,
            PlanmapSha256,
            Labels(new ReplayRoomLabel(0, 2, "B", 4, "userScreenshot")),
            [new ReplayRoomCandidateBox("LVD_Room_B", 4, 0, 3, 0, 3)]);

        Assert.Equal(1, summary.InvalidPoseCount);
        Assert.Null(summary.MatchRate);
    }

    private static ReplayRoomLabelSet Labels(params ReplayRoomLabel[] labels) => new(1, ReplaySha256, PlanmapSha256, [.. labels]);

    private static JsonDocument CreateReplay(string kind, int floorStart, int floorEnd, int? exactFloor, params double[] coordinates)
    {
        var snapshots = coordinates.Chunk(2).Select((pair, index) => new
        {
            sample = index,
            dungeon = new
            {
                kind,
                floor = exactFloor,
                floorRange = new { start = floorStart, end = floorEnd },
            },
            playerPose = new { position = new { isFinite = true, x = pair[0], y = pair[1] } },
        });
        return JsonDocument.Parse(JsonSerializer.Serialize(new { schemaVersion = 5, snapshots }));
    }
}
