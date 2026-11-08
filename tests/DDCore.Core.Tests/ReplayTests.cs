using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;
using DDCore.Core.Geometry;
using DDCore.Core.Replay;
using DDCore.Core.Schema;
using DDCore.Core.World;

namespace DDCore.Core.Tests;

public sealed class ReplayTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 7, 1, 0, 0, TimeSpan.Zero);
    private static readonly Guid Session = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void CaptureAndExportRequireExplicitUserIntent()
    {
        var snapshot = CreateSnapshot(1, Start);
        Assert.Throws<InvalidOperationException>(() =>
            ReplayDocument.Create(Start, Session, "0.2.0", [snapshot], new ReplayUserIntent(false, false)));

        var document = ReplayDocument.Create(Start, Session, "0.2.0", [snapshot], ReplayUserIntent.ExplicitCapture);
        Assert.Throws<InvalidOperationException>(() => new ReplayCodec().Serialize(document, ReplayUserIntent.ExplicitCapture));
    }

    [Fact]
    public void RoundTripIsDeterministic()
    {
        var document = ReplayDocument.Create(
            Start,
            Session,
            "0.2.0",
            [CreateSnapshot(1, Start), CreateSnapshot(2, Start.AddSeconds(1))],
            ReplayUserIntent.ExplicitCapture);
        var codec = new ReplayCodec();

        var first = codec.Serialize(document, ReplayUserIntent.ExplicitCaptureAndExport);
        var restored = codec.Deserialize(first);
        var second = codec.Serialize(restored, ReplayUserIntent.ExplicitCaptureAndExport);

        Assert.Equal(first, second);
        Assert.Equal([1L, 2L], restored.Snapshots.Select(snapshot => snapshot.Sequence));
    }

    [Fact]
    public void UnknownJsonPropertyIsRejected()
    {
        var document = ReplayDocument.Create(Start, Session, "0.2.0", [CreateSnapshot(1, Start)], ReplayUserIntent.ExplicitCapture);
        var json = Encoding.UTF8.GetString(new ReplayCodec().Serialize(document, ReplayUserIntent.ExplicitCaptureAndExport));
        json = json.Insert(json.LastIndexOf('}'), ",\"unexpected\":true");

        Assert.Throws<InvalidDataException>(() => new ReplayCodec().Deserialize(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void OversizedInputIsRejectedBeforeParsing()
    {
        var input = new byte[ReplayCodec.MaximumDocumentBytes + 1];
        Assert.Throws<InvalidDataException>(() => new ReplayCodec().Deserialize(input));
    }

    [Fact]
    public void NonIncreasingSequenceIsRejected()
    {
        var document = ReplayDocument.Create(
            Start,
            Session,
            "0.2.0",
            [CreateSnapshot(2, Start), CreateSnapshot(2, Start.AddSeconds(1))],
            ReplayUserIntent.ExplicitCapture);

        Assert.Throws<InvalidDataException>(() => new ReplayCodec().Validate(document));
    }

    [Fact]
    public void SessionMismatchIsRejected()
    {
        var snapshot = new MockWorldSnapshotBuilder()
            .InSession(Guid.Parse("33333333-3333-3333-3333-333333333333"))
            .At(Start)
            .WithSequence(1)
            .Build();
        var document = ReplayDocument.Create(Start, Session, "0.2.0", [snapshot], ReplayUserIntent.ExplicitCapture);

        Assert.Throws<InvalidDataException>(() => new ReplayCodec().Validate(document));
    }

    [Fact]
    public void ExcessiveActorCountIsRejected()
    {
        var actors = Enumerable.Range(1, ReplayCodec.MaximumActorsPerSnapshot + 1)
            .Select(index => CreateActor((ulong)index))
            .ToArray();
        var snapshot = new MockWorldSnapshotBuilder()
            .InSession(Session)
            .At(Start)
            .WithSequence(1);
        foreach (var actor in actors)
        {
            snapshot.AddActor(actor);
        }

        var document = ReplayDocument.Create(Start, Session, "0.2.0", [snapshot.Build()], ReplayUserIntent.ExplicitCapture);
        Assert.Throws<InvalidDataException>(() => new ReplayCodec().Validate(document));
    }

    [Fact]
    public void MockBuilderProducesRepeatableSnapshot()
    {
        var first = CreateSnapshot(7, Start);
        var second = CreateSnapshot(7, Start);

        Assert.Equal(first.Sequence, second.Sequence);
        Assert.Equal(first.CapturedAt, second.CapturedAt);
        Assert.Equal(first.SessionId, second.SessionId);
        Assert.Equal(first.Dungeon, second.Dungeon);
    }

    [Fact]
    public void LegacySchemaThreeReplayWithoutPlayerPoseRemainsReadable()
    {
        var codec = new ReplayCodec();
        var current = ReplayDocument.Create(
            Start,
            Session,
            "0.5.1.0",
            [CreateSnapshot(1, Start)],
            ReplayUserIntent.ExplicitCapture);
        var root = JsonNode.Parse(codec.Serialize(current, ReplayUserIntent.ExplicitCaptureAndExport))!.AsObject();
        root["schemaVersion"] = SchemaVersions.LegacyReplay;
        root["snapshots"]![0]!["schemaVersion"] = SchemaVersions.LegacyWorldSnapshot;

        var restored = codec.Deserialize(Encoding.UTF8.GetBytes(root.ToJsonString()));

        Assert.Equal(SchemaVersions.LegacyReplay, restored.SchemaVersion);
        var snapshot = Assert.Single(restored.Snapshots);
        Assert.Equal(SchemaVersions.LegacyWorldSnapshot, snapshot.SchemaVersion);
        Assert.Null(snapshot.PlayerPose);
    }

    [Fact]
    public void CurrentReplayPreservesAnonymousPoseMetadata()
    {
        var snapshot = new MockWorldSnapshotBuilder()
            .InSession(Session)
            .At(Start)
            .WithSequence(1)
            .WithAnonymousPlayerPose(
                new Coordinate2D(0, 0),
                0,
                new ObservationMetadata(ObservationValueSource.PublicDalamud, ObservationValueQuality.Direct, null))
            .Build();
        var document = ReplayDocument.Create(Start, Session, "0.5.1.0", [snapshot], ReplayUserIntent.ExplicitCapture);
        var codec = new ReplayCodec();

        var restored = codec.Deserialize(codec.Serialize(document, ReplayUserIntent.ExplicitCaptureAndExport));

        var pose = Assert.Single(restored.Snapshots).PlayerPose!;
        Assert.Equal(new Coordinate2D(0, 0), pose.Position);
        Assert.Equal(ObservationValueSource.PublicDalamud, pose.Metadata.Source);
        Assert.Equal(ObservationValueQuality.Direct, pose.Metadata.Quality);
    }

    [Theory]
    [InlineData(SchemaVersions.LegacyReplay, SchemaVersions.LegacyWorldSnapshot)]
    [InlineData(SchemaVersions.Replay, SchemaVersions.WorldSnapshot)]
    public void ExistingSchemaThreeAndFourWithoutB1FieldsRemainReadable(int replaySchema, int snapshotSchema)
    {
        var codec = new ReplayCodec();
        var document = ReplayDocument.Create(
            Start,
            Session,
            "0.5.1.0",
            [CreateSnapshot(1, Start)],
            ReplayUserIntent.ExplicitCapture);
        var root = JsonNode.Parse(codec.Serialize(document, ReplayUserIntent.ExplicitCaptureAndExport))!.AsObject();
        root["schemaVersion"] = replaySchema;
        var snapshotNode = root["snapshots"]![0]!.AsObject();
        snapshotNode["schemaVersion"] = snapshotSchema;
        var dungeon = snapshotNode["dungeon"]!.AsObject();
        dungeon.Remove("floorRange");
        dungeon.Remove("isDeepDungeonObserved");
        if (replaySchema == SchemaVersions.LegacyReplay)
        {
            snapshotNode.Remove("playerPose");
        }

        var restored = codec.Deserialize(Encoding.UTF8.GetBytes(root.ToJsonString()));

        var restoredDungeon = Assert.Single(restored.Snapshots).Dungeon;
        Assert.Null(restoredDungeon.FloorRange);
        Assert.Null(restoredDungeon.IsDeepDungeonObserved);
    }

    [Fact]
    public void CurrentSchemaRoundTripPreservesPublicBandWithoutInventingExactFloor()
    {
        var snapshot = new MockWorldSnapshotBuilder()
            .InSession(Session)
            .At(Start)
            .WithSequence(1)
            .InDungeon(new DeepDungeonContext(
                DeepDungeonKind.EurekaOrthos,
                null,
                1099,
                897,
                "eureka-orthos:1-10:public",
                null,
                new DeepDungeonFloorRange(1, 10),
                true))
            .Build();
        var codec = new ReplayCodec();
        var document = ReplayDocument.Create(Start, Session, "0.5.2.0", [snapshot], ReplayUserIntent.ExplicitCapture);

        var restored = codec.Deserialize(codec.Serialize(document, ReplayUserIntent.ExplicitCaptureAndExport));

        var dungeon = Assert.Single(restored.Snapshots).Dungeon;
        Assert.Equal(DeepDungeonKind.EurekaOrthos, dungeon.Kind);
        Assert.Equal(new DeepDungeonFloorRange(1, 10), dungeon.FloorRange);
        Assert.Null(dungeon.Floor);
        Assert.True(dungeon.IsDeepDungeonObserved);
    }

    private static WorldSnapshot CreateSnapshot(long sequence, DateTimeOffset at) =>
        new MockWorldSnapshotBuilder()
            .InSession(Session)
            .At(at)
            .WithSequence(sequence)
            .AddActor(CreateActor(1))
            .Build();

    private static ActorSnapshot CreateActor(ulong entityId) =>
        new(
            new ActorIdentity(entityId, 0),
            0x3D54,
            ActorKind.Enemy,
            new Coordinate2D(-300, -300),
            0,
            1.8,
            100,
            100,
            false,
            false,
            true,
            null,
            null,
            ImmutableArray<StatusSnapshot>.Empty);
}
