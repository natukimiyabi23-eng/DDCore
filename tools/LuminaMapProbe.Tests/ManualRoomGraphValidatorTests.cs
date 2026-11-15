using System.Text.Json;
using Xunit;

public sealed class ManualRoomGraphValidatorTests
{
    private const string ReplayHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string PlanmapHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string EvidenceHash = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    [Fact]
    public void CompleteUserLabeledGraphProducesOfflineOnlyRoomRoute()
    {
        var labels = Labels();
        using var replay = Replay();

        var result = ManualRoomGraphValidator.Evaluate(
            labels,
            ReplayHash,
            PlanmapHash,
            replay.RootElement,
            [new ReplayRoomCandidateBox("LVD_Room_A", 0, 0, 2, 0, 2)],
            new HashSet<string>([EvidenceHash], StringComparer.OrdinalIgnoreCase),
            destinationRoom: 1);

        Assert.Equal("offline-route", result.Status);
        Assert.Equal([1], result.Route);
        Assert.Equal(25, result.PresentRoomCount);
        Assert.Equal(0, result.UnknownRoomCount);
        Assert.Equal(1, result.OpenPassageCount);
        Assert.Equal(0, result.UnknownPassageCount);
        Assert.True(result.GraphComplete);
        Assert.True(result.IsOfflineOnly);
        Assert.Contains("image meaning is user-asserted", result.EvidenceTrust, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownLabelsOutsideKnownOpenPathAllowOnlyAnOfflinePartialRoute()
    {
        using var replay = Replay();
        var unknownRoom = Labels();
        unknownRoom.Rooms[24] = new(24, "unknown", "notObserved");
        var unknownPassage = Labels();
        unknownPassage.Passages[1] = unknownPassage.Passages[1] with { State = "unknown", Evidence = "notObserved" };
        var pathUnknown = Labels();
        pathUnknown.Passages[0] = new(0, 1, "unknown", "notObserved");

        var first = Evaluate(unknownRoom, replay.RootElement);
        var second = Evaluate(unknownPassage, replay.RootElement);
        var third = Evaluate(pathUnknown, replay.RootElement);

        Assert.Equal("offline-partial-route", first.Status);
        Assert.Equal("route.uses-only-explicitly-open-labeled-passages", first.ReasonCode);
        Assert.Equal([1], first.Route);
        Assert.False(first.GraphComplete);
        Assert.Equal(1, first.UnknownRoomCount);
        Assert.Equal(0, first.UnknownPassageCount);
        Assert.Equal("offline-partial-route", second.Status);
        Assert.Equal([1], second.Route);
        Assert.Equal(1, second.UnknownPassageCount);
        Assert.Equal("incomplete", third.Status);
        Assert.Equal("route.no-known-open-path-in-incomplete-graph", third.ReasonCode);
        Assert.Empty(third.Route);
    }

    [Fact]
    public void OpenPassageCannotUseAnUnknownEndpointRoom()
    {
        using var replay = Replay();
        var labels = Labels();
        labels.Rooms[1] = new(1, "unknown", "notObserved");

        Assert.Throws<InvalidDataException>(() => Evaluate(labels, replay.RootElement));
    }

    [Fact]
    public void RouteIsUnavailableWhenPlayerPoseDoesNotUniquelyMatchLabeledStartRoom()
    {
        using var replay = Replay();
        var result = ManualRoomGraphValidator.Evaluate(
            Labels(), ReplayHash, PlanmapHash, replay.RootElement,
            [new ReplayRoomCandidateBox("LVD_Room_A", 8, 0, 2, 0, 2)],
            new HashSet<string>([EvidenceHash], StringComparer.OrdinalIgnoreCase),
            destinationRoom: 1);

        Assert.Equal("unavailable", result.Status);
        Assert.Equal("route.current-room-candidate-mismatch", result.ReasonCode);
        Assert.Empty(result.Route);
        Assert.True(result.GraphComplete);
    }

    [Fact]
    public void AmbiguousPlayerPoseCandidatePreventsRouteOutput()
    {
        using var replay = Replay();
        Assert.Throws<InvalidDataException>(() => ManualRoomGraphValidator.Evaluate(
                Labels(), ReplayHash, PlanmapHash, replay.RootElement,
                [
                    new ReplayRoomCandidateBox("LVD_Room_A", 0, 0, 2, 0, 2),
                    new ReplayRoomCandidateBox("LVD_Room_B", 0, 0, 2, 0, 2),
                ],
                new HashSet<string>([EvidenceHash], StringComparer.OrdinalIgnoreCase),
                destinationRoom: 1));
    }

    [Fact]
    public void OpenPassageCannotTouchAbsentRoom()
    {
        using var replay = Replay();
        var labels = Labels();
        labels.Rooms[1] = new(1, "absent", "userConfirmed");

        Assert.Throws<InvalidDataException>(() => Evaluate(labels, replay.RootElement));
    }

    [Fact]
    public void CompleteDisconnectedGraphReportsNoRoute()
    {
        using var replay = Replay();
        var labels = Labels();
        labels.Passages[0] = new(0, 1, "closed", "userConfirmed");
        var result = Evaluate(labels, replay.RootElement);

        Assert.Equal("no-route", result.Status);
        Assert.Equal("route.no-open-path", result.ReasonCode);
        Assert.True(result.GraphComplete);
        Assert.Empty(result.Route);
    }

    [Fact]
    public void MissingOrDuplicatePotentialPassageIsRejected()
    {
        using var replay = Replay();
        var missing = Labels();
        missing.Passages.RemoveAt(0);
        var duplicate = Labels();
        duplicate.Passages[1] = duplicate.Passages[0];

        Assert.Throws<InvalidDataException>(() => Evaluate(missing, replay.RootElement));
        Assert.Throws<InvalidDataException>(() => Evaluate(duplicate, replay.RootElement));
    }

    [Fact]
    public void StaleHashesAndIncompatibleReplaySnapshotAreRejected()
    {
        using var replay = Replay();
        var labels = Labels() with { ReplaySha256 = new string('d', 64) };
        Assert.Throws<InvalidDataException>(() => Evaluate(labels, replay.RootElement));

        using var unsupported = JsonDocument.Parse("""
            {"schemaVersion":5,"snapshots":[{"dungeon":{"kind":"eurekaOrthos","floorRange":{"start":11,"end":20}},"playerPose":{"position":{"isFinite":true,"x":1,"y":1},"metadata":{"source":"publicDalamud","quality":"direct"}}}]}
            """);
        Assert.Throws<InvalidDataException>(() => Evaluate(Labels(), unsupported.RootElement));
    }

    [Fact]
    public void SchemaRejectsUnknownFieldsAndInvalidEvidenceHash()
    {
        const string json = """
            {"schemaVersion":3,"replaySha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","planmapSha256":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","evidenceSha256":"cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc","snapshotIndex":0,"exactFloor":2,"variant":"A","currentRoom":0,"rooms":[],"passages":[],"characterName":"not-allowed"}
            """;
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ManualRoomGraphLabelSet>(json, ManualRoomGraphValidator.JsonOptions));

        using var replay = Replay();
        var badHash = Labels() with { EvidenceSha256 = "not-a-hash" };
        Assert.Throws<InvalidDataException>(() => Evaluate(badHash, replay.RootElement));
    }

    [Fact]
    public void UnknownTemplateContainsNoRouteReadyRoomOrPassageLabels()
    {
        using var replay = Replay();
        var template = ManualRoomGraphValidator.CreateUnknownTemplate(ReplayHash, PlanmapHash, replay.RootElement, snapshotIndex: 0);

        Assert.Null(template.ExactFloor);
        Assert.Null(template.Variant);
        Assert.Null(template.CurrentRoom);
        Assert.Null(template.EvidenceSha256);
        Assert.Equal(3, template.SchemaVersion);
        Assert.Equal(25, template.Rooms.Count);
        Assert.Equal(40, template.Passages.Count);
        Assert.All(template.Rooms, static row => Assert.Equal("unknown", row.State));
        Assert.All(template.Passages, static row => Assert.Equal("unknown", row.State));

        var result = Evaluate(template, replay.RootElement);
        Assert.Equal("incomplete", result.Status);
        Assert.Empty(result.Route);
    }

    [Fact]
    public void FullyLabeledGraphStillRequiresKnownFloorVariantStartAndEvidence()
    {
        using var replay = Replay();
        var labels = Labels() with { ExactFloor = null, Variant = null, CurrentRoom = null, EvidenceSha256 = null };

        var result = Evaluate(labels, replay.RootElement);

        Assert.Equal("incomplete", result.Status);
        Assert.Equal("graph.context-unknown", result.ReasonCode);
        Assert.Empty(result.Route);
    }

    [Fact]
    public void ScreenshotLabelsRequireTheirImageHashInTheLocalEvidenceCatalog()
    {
        using var replay = Replay();
        var labels = Labels();
        labels.Rooms[0] = labels.Rooms[0] with { Evidence = "userScreenshot", EvidenceSha256 = new string('d', 64), EvidenceSnapshotIndex = 0 };

        Assert.Throws<InvalidDataException>(() => Evaluate(labels, replay.RootElement));
    }

    [Fact]
    public void ScreenshotLabelRequiresACompatibleReplaySnapshotIndex()
    {
        using var replay = Replay();
        var missingSnapshot = Labels();
        missingSnapshot.Passages[0] = missingSnapshot.Passages[0] with { EvidenceSnapshotIndex = null };
        var outOfRangeSnapshot = Labels();
        outOfRangeSnapshot.Passages[0] = outOfRangeSnapshot.Passages[0] with { EvidenceSnapshotIndex = 1 };
        var wrongLayoutFamily = Labels();

        Assert.Throws<InvalidDataException>(() => Evaluate(missingSnapshot, replay.RootElement));
        Assert.Throws<InvalidDataException>(() => Evaluate(outOfRangeSnapshot, replay.RootElement));
        Assert.Throws<InvalidDataException>(() => ManualRoomGraphValidator.Evaluate(
            wrongLayoutFamily, ReplayHash, PlanmapHash, replay.RootElement,
            [new ReplayRoomCandidateBox("LVD_Room_B", 0, 0, 2, 0, 2)],
            new HashSet<string>([EvidenceHash], StringComparer.OrdinalIgnoreCase),
            destinationRoom: 1));
    }

    [Fact]
    public void ContextScreenshotHashMustMatchAFileInTheEvidenceCatalog()
    {
        using var replay = Replay();
        var labels = Labels() with { EvidenceSha256 = new string('d', 64) };
        var result = ManualRoomGraphValidator.Evaluate(
            labels, ReplayHash, PlanmapHash, replay.RootElement,
            [new ReplayRoomCandidateBox("LVD_Room_A", 0, 0, 2, 0, 2)],
            new HashSet<string>([EvidenceHash], StringComparer.OrdinalIgnoreCase),
            destinationRoom: 1);

        Assert.Equal("unavailable", result.Status);
        Assert.Equal("evidence.context-image-not-found", result.ReasonCode);
        Assert.Empty(result.Route);
    }

    [Fact]
    public void UnknownTemplateRequiresEOContextAndDirectPose()
    {
        using var wrongDungeon = JsonDocument.Parse("""
            {"schemaVersion":5,"snapshots":[{"dungeon":{"kind":"unsupported","floorRange":null},"playerPose":{"position":{"isFinite":true,"x":1,"y":1},"metadata":{"source":"publicDalamud","quality":"direct"}}}]}
            """);
        using var untrustedPose = JsonDocument.Parse("""
            {"schemaVersion":5,"snapshots":[{"dungeon":{"kind":"eurekaOrthos","floorRange":{"start":1,"end":10}},"playerPose":{"position":{"isFinite":true,"x":1,"y":1},"metadata":{"source":"unknown","quality":"direct"}}}]}
            """);

        Assert.Throws<InvalidDataException>(() => ManualRoomGraphValidator.CreateUnknownTemplate(ReplayHash, PlanmapHash, wrongDungeon.RootElement, 0));
        Assert.Throws<InvalidDataException>(() => ManualRoomGraphValidator.CreateUnknownTemplate(ReplayHash, PlanmapHash, untrustedPose.RootElement, 0));
    }

    private static ManualRoomGraphValidation Evaluate(ManualRoomGraphLabelSet labels, JsonElement replay) =>
        ManualRoomGraphValidator.Evaluate(
            labels, ReplayHash, PlanmapHash, replay,
            [new ReplayRoomCandidateBox("LVD_Room_A", 0, 0, 2, 0, 2)],
            new HashSet<string>([EvidenceHash], StringComparer.OrdinalIgnoreCase),
            destinationRoom: 1);

    private static ManualRoomGraphLabelSet Labels()
    {
        var rooms = Enumerable.Range(0, 25)
            .Select(static room => new ManualRoomPresenceLabel(room, "present", "userConfirmed"))
            .ToList();
        var passages = new List<ManualPassageLabel>();
        for (var room = 0; room < 25; room++)
        {
            if (room % 5 < 4)
                passages.Add(new(room, room + 1, "closed", "userConfirmed"));
            if (room / 5 < 4)
                passages.Add(new(room, room + 5, "closed", "userConfirmed"));
        }
        passages[0] = new(0, 1, "open", "userScreenshot", EvidenceHash, 0);
        return new(3, ReplayHash, PlanmapHash, EvidenceHash, 0, 2, "A", 0, rooms, passages);
    }

    private static JsonDocument Replay() => JsonDocument.Parse("""
        {"schemaVersion":5,"snapshots":[{"dungeon":{"kind":"eurekaOrthos","floor":null,"floorRange":{"start":1,"end":10}},"playerPose":{"position":{"isFinite":true,"x":1,"y":1},"metadata":{"source":"publicDalamud","quality":"direct"}}}]}
        """);
}
