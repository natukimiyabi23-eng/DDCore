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

    [Theory]
    [InlineData("stale", "publicDalamud")]
    [InlineData("direct", "unknown")]
    public void PoseWithoutDirectPublicProvenanceIsNotScored(string quality, string source)
    {
        using var replay = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            schemaVersion = 5,
            snapshots = new[]
            {
                new
                {
                    dungeon = new { kind = "eurekaOrthos", floorRange = new { start = 1, end = 10 } },
                    playerPose = new
                    {
                        position = new { isFinite = true, x = 1d, y = 1d },
                        metadata = new { source, quality },
                    },
                },
            },
        }));

        var summary = ReplayRoomLabelEvaluator.Evaluate(
            replay.RootElement,
            ReplaySha256,
            PlanmapSha256,
            Labels(new ReplayRoomLabel(0, 2, "B", 4, "userScreenshot")),
            [new ReplayRoomCandidateBox("LVD_Room_B", 4, 0, 3, 0, 3)]);

        Assert.Equal(1, summary.UntrustedPoseCount);
        Assert.Equal(0, summary.MatchedExpectedCandidateCount);
        Assert.Null(summary.MatchRate);
    }

    [Fact]
    public void BoxTraceReportsContainmentCategoriesWithoutCoordinates()
    {
        using var replay = CreateReplay("eurekaOrthos", 1, 10, null, 1.25, 2.25, 6, 6, 20, 20);

        var trace = ReplayRoomLabelEvaluator.CreateBoxTrace(
            replay.RootElement,
            [
                new ReplayRoomCandidateBox("LVD_Room_A", 3, 0, 3, 0, 3),
                new ReplayRoomCandidateBox("LVD_Room_B", 7, 5, 8, 5, 8),
                new ReplayRoomCandidateBox("LVD_Room_A", 8, 5, 8, 5, 8),
            ]);

        Assert.Collection(
            trace,
            unique =>
            {
                Assert.Equal(0, unique.SnapshotIndex);
                Assert.Equal("uniqueBoxContainment", unique.Status);
                Assert.Equal("1-10", unique.FloorRange);
                var onlyCandidate = Assert.Single(unique.Candidates);
                Assert.Equal(new ReplayRoomBoxTraceCandidate("A", 3), onlyCandidate);
            },
            ambiguous =>
            {
                Assert.Equal(1, ambiguous.SnapshotIndex);
                Assert.Equal("ambiguousBoxContainment", ambiguous.Status);
                Assert.Equal(2, ambiguous.Candidates.Count);
            },
            unmatched => Assert.Equal("noBoxContainment", unmatched.Status));

        var json = JsonSerializer.Serialize(trace);
        Assert.DoesNotContain("1.25", json, StringComparison.Ordinal);
        Assert.DoesNotContain("coordinates", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("playerPose", json, StringComparison.Ordinal);
    }

    [Fact]
    public void BoxTraceKeepsContextAndPoseFailuresDistinct()
    {
        using var replay = JsonDocument.Parse("""
            {"schemaVersion":5,"snapshots":[
              {"dungeon":{"kind":"eurekaOrthos","floorRange":{"start":1,"end":10}},"playerPose":{"position":{"isFinite":true,"x":1,"y":1},"metadata":{"source":"publicDalamud","quality":"direct"}}},
              {"dungeon":{"kind":"eurekaOrthos","floorRange":{"start":1,"end":10}},"playerPose":{"position":{"isFinite":true,"x":1,"y":1},"metadata":{"source":"publicDalamud","quality":"stale"}}},
              {"dungeon":{"kind":"unsupported","floorRange":null},"playerPose":null},
              {"dungeon":{"kind":"eurekaOrthos","floorRange":{"start":1,"end":10}},"playerPose":null}
            ]}
            """);

        var trace = ReplayRoomLabelEvaluator.CreateBoxTrace(
            replay.RootElement,
            [new ReplayRoomCandidateBox("LVD_Room_B", 4, 0, 3, 0, 3)]);

        Assert.Equal(
            ["uniqueBoxContainment", "untrustedPose", "contextUnknown", "invalidPose"],
            trace.Select(static entry => entry.Status));
    }

    [Fact]
    public void BoxTraceRejectsInvalidSchemaAndCandidateBounds()
    {
        using var replay = CreateReplay("eurekaOrthos", 1, 10, null, 1, 1);
        Assert.Throws<InvalidDataException>(() => ReplayRoomLabelEvaluator.CreateBoxTrace(
            replay.RootElement,
            [new ReplayRoomCandidateBox("LVD_Room_C", 0, 0, 1, 0, 1)]));

        using var invalidReplay = JsonDocument.Parse("""{"schemaVersion":4,"snapshots":[]}""");
        Assert.Throws<InvalidDataException>(() => ReplayRoomLabelEvaluator.CreateBoxTrace(invalidReplay.RootElement, []));
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
            playerPose = new
            {
                position = new { isFinite = true, x = pair[0], y = pair[1] },
                metadata = new { source = "publicDalamud", quality = "direct", missingReason = (string?)null },
            },
        });
        return JsonDocument.Parse(JsonSerializer.Serialize(new { schemaVersion = 5, snapshots }));
    }
}
