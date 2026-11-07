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
        var actor = CreateActor() with { Position = new Coordinate2D(double.NaN, 0) };
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

    private static WorldSnapshot CreateSnapshot(
        int floor = 10,
        ObservationQuality quality = ObservationQuality.Complete,
        ActorSnapshot[]? actors = null,
        string[]? diagnostics = null) =>
        new(
            SchemaVersions.WorldSnapshot,
            1,
            CapturedAt,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ObservationSource.Mock,
            quality,
            new DeepDungeonContext(DeepDungeonKind.EurekaOrthos, floor, 0, 897, "eureka-orthos-10", null),
            (actors ?? []).ToImmutableArray(),
            (diagnostics ?? []).ToImmutableArray());

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
