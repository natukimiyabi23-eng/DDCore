using System.Collections.Immutable;
using DDCore.Core.Geometry;
using DDCore.Core.Model;
using DDCore.Core.Schema;
using DDCore.Core.World;

namespace DDCore.Core.Tests;

public sealed class WorldSnapshotTests
{
    private static readonly DateTimeOffset CapturedAt = new(2026, 11, 7, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CompleteSnapshotIsImmutableAndValid()
    {
        var actor = CreateActor();
        var snapshot = CreateSnapshot(actors: [actor]);

        Assert.Equal(10, snapshot.Dungeon.Floor);
        Assert.True(snapshot.IsDeepDungeon);
        Assert.Single(snapshot.Actors);
        Assert.Equal(ObservationQuality.Complete, snapshot.Quality);
    }

    [Fact]
    public void Phase2ConstructorRemainsObserverCompatible()
    {
        var snapshot = new WorldSnapshot(12, CapturedAt, true, "legacy-content");

        Assert.Equal(12, snapshot.Sequence);
        Assert.True(snapshot.IsDeepDungeon);
        Assert.Equal("legacy-content", snapshot.ContentKey);
        Assert.Empty(snapshot.Actors);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(201)]
    public void InvalidFloorIsRejected(int floor) =>
        Assert.Throws<InvalidDataException>(() => CreateSnapshot(floor: floor));

    [Fact]
    public void TenFloorBandIsSeparateFromExactFloor()
    {
        var context = new DeepDungeonContext(
            DeepDungeonKind.EurekaOrthos,
            null,
            1099,
            897,
            "eureka-orthos:1-10:public",
            null,
            new DeepDungeonFloorRange(1, 10),
            true);

        context.Validate();

        Assert.Null(context.Floor);
        Assert.Equal(1, context.FloorRange!.Start);
        Assert.Equal(10, context.FloorRange.End);
    }

    [Theory]
    [InlineData(0, 9)]
    [InlineData(1, 11)]
    [InlineData(2, 11)]
    [InlineData(191, 201)]
    public void InvalidTenFloorBandIsRejected(int start, int end) =>
        Assert.Throws<InvalidDataException>(() => new DeepDungeonFloorRange(start, end).Validate());

    [Fact]
    public void UnsupportedLegacyDutyIsNotDeepDungeonWithoutDirectConditionEvidence()
    {
        var legacy = new WorldSnapshot(
            SchemaVersions.WorldSnapshot,
            1,
            CapturedAt,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ObservationSource.Replay,
            ObservationQuality.Partial,
            new DeepDungeonContext(DeepDungeonKind.Unsupported, null, 1, 1, "legacy-unidentified-duty", null),
            [],
            []);

        Assert.False(legacy.IsDeepDungeon);
    }

    [Fact]
    public void DuplicateActorIdentityIsRejected()
    {
        var actor = CreateActor();
        Assert.Throws<InvalidDataException>(() => CreateSnapshot(actors: [actor, actor]));
    }

    [Fact]
    public void InvalidHpRangeIsRejected()
    {
        var actor = CreateActor() with { CurrentHp = 101, MaximumHp = 100 };
        Assert.Throws<InvalidDataException>(() => CreateSnapshot(actors: [actor]));
    }

    [Fact]
    public void InvalidCastTimeRangeIsRejected()
    {
        var actor = CreateActor() with
        {
            Cast = new CastSnapshot(1, CastLifecycle.Casting, CapturedAt, CapturedAt.AddSeconds(-1), 0, null, null),
        };

        Assert.Throws<InvalidDataException>(() => CreateSnapshot(actors: [actor]));
    }

    [Fact]
    public void NonFiniteActorDataIsRejected()
    {
        var actor = CreateActor() with
        {
            Spatial = ActorSpatialObservation.Usable(new Coordinate2D(double.NaN, 0), 0),
        };
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateSnapshot(actors: [actor]));
    }

    [Fact]
    public void NonFiniteCastRotationIsRejected()
    {
        var actor = CreateActor() with
        {
            Cast = new CastSnapshot(1, CastLifecycle.Casting, CapturedAt, CapturedAt.AddSeconds(1), double.NaN, null, null),
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateSnapshot(actors: [actor]));
    }

    [Fact]
    public void UnknownObservationQualityIsRepresentedExplicitly()
    {
        var snapshot = CreateSnapshot(quality: ObservationQuality.Unsupported, diagnostics: ["adapter.unsupported"]);
        Assert.Equal(ObservationQuality.Unsupported, snapshot.Quality);
        Assert.Equal("adapter.unsupported", Assert.Single(snapshot.Diagnostics));
    }

    [Fact]
    public void MissingPoseAndObservedOriginAreDistinct()
    {
        var missing = CreateSnapshot(playerPose: new AnonymousPlayerPose(
            null,
            null,
            new ObservationMetadata(
                ObservationValueSource.PublicDalamud,
                ObservationValueQuality.Missing,
                "dalamud.player.not-created")));
        var origin = CreateSnapshot(playerPose: new AnonymousPlayerPose(
            new Coordinate2D(0, 0),
            0,
            new ObservationMetadata(
                ObservationValueSource.PublicDalamud,
                ObservationValueQuality.Direct,
                null)));

        Assert.Null(missing.PlayerPose!.Position);
        Assert.Equal(ObservationValueQuality.Missing, missing.PlayerPose.Metadata.Quality);
        Assert.Equal(new Coordinate2D(0, 0), origin.PlayerPose!.Position);
        Assert.Equal(ObservationValueQuality.Direct, origin.PlayerPose.Metadata.Quality);
    }

    [Fact]
    public void DirectPoseRequiresCoordinatesAndNoMissingReason()
    {
        Assert.Throws<InvalidDataException>(() => CreateSnapshot(playerPose: new AnonymousPlayerPose(
            null,
            null,
            new ObservationMetadata(ObservationValueSource.PublicDalamud, ObservationValueQuality.Direct, null))));
        Assert.Throws<InvalidDataException>(() => CreateSnapshot(playerPose: new AnonymousPlayerPose(
            new Coordinate2D(1, 2),
            0,
            new ObservationMetadata(ObservationValueSource.PublicDalamud, ObservationValueQuality.Direct, "unexpected"))));
    }

    [Fact]
    public void ActorSpatialMissingAndOriginRemainDistinct()
    {
        var missing = ActorSpatialObservation.Missing(
            new ObservationMetadata(ObservationValueSource.PublicDalamud, ObservationValueQuality.Missing, "actor.spatial.not-observed"),
            "actor.spatial.not-observed");
        var origin = ActorSpatialObservation.Quarantined(
            new Coordinate2D(0, 0),
            0,
            new ObservationMetadata(ObservationValueSource.PublicDalamud, ObservationValueQuality.Direct, null),
            "actor.spatial.origin-unverified");

        missing.Validate();
        origin.Validate();

        Assert.Null(missing.Position);
        Assert.Equal(new Coordinate2D(0, 0), origin.Position);
        Assert.Equal(SpatialUsability.Missing, missing.Usability);
        Assert.Equal(SpatialUsability.Quarantined, origin.Usability);
    }

    [Fact]
    public void MissingSpatialRequiresMissingMetadataWithMatchingReason()
    {
        var direct = new ActorSpatialObservation(
            null,
            null,
            new ObservationMetadata(ObservationValueSource.PublicDalamud, ObservationValueQuality.Direct, null),
            SpatialUsability.Missing,
            "actor.spatial.not-observed");
        var mismatch = new ActorSpatialObservation(
            null,
            null,
            new ObservationMetadata(ObservationValueSource.PublicDalamud, ObservationValueQuality.Missing, "one"),
            SpatialUsability.Missing,
            "two");

        Assert.Throws<InvalidDataException>(direct.Validate);
        Assert.Throws<InvalidDataException>(mismatch.Validate);
    }

    private static WorldSnapshot CreateSnapshot(
        int floor = 10,
        ObservationQuality quality = ObservationQuality.Complete,
        ActorSnapshot[]? actors = null,
        string[]? diagnostics = null,
        AnonymousPlayerPose? playerPose = null) =>
        new(
            SchemaVersions.WorldSnapshot,
            1,
            CapturedAt,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ObservationSource.Mock,
            quality,
            new DeepDungeonContext(DeepDungeonKind.EurekaOrthos, floor, 0, 897, "eureka-orthos-10", null),
            (actors ?? []).ToImmutableArray(),
            (diagnostics ?? []).ToImmutableArray(),
            playerPose);

    private static ActorSnapshot CreateActor() =>
        new(
            new ActorIdentity(0x100, 0),
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
            new CastSnapshot(31479, CastLifecycle.Casting, CapturedAt, CapturedAt.AddSeconds(5), 0, null, null),
            ImmutableArray<StatusSnapshot>.Empty);
}
