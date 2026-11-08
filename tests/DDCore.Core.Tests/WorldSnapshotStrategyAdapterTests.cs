using System.Collections.Immutable;
using DDCore.Core.Decision;
using DDCore.Core.Geometry;
using DDCore.Core.Model;
using DDCore.Core.Schema;
using DDCore.Core.World;

namespace DDCore.Core.Tests;

public sealed class WorldSnapshotStrategyAdapterTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 7, 18, 9, 39, TimeSpan.FromHours(9));

    [Fact]
    public void ConvertsOnlyDirectlyObservedFields()
    {
        var conversion = WorldSnapshotStrategyAdapter.Convert(Snapshot(floor: 4));

        Assert.Equal(DeepDungeonKind.EurekaOrthos, conversion.State.DungeonKind.RequireKnown("kind"));
        Assert.Equal(4, conversion.State.Floor.RequireKnown("floor"));
        Assert.False(conversion.State.CurrentRoom.IsKnown);
        Assert.False(conversion.State.Rooms.IsKnown);
        Assert.False(conversion.State.Enemies.IsKnown);
        Assert.Equal(ExitDiscoveryState.Unknown, conversion.State.Exit.Discovery);
        Assert.Contains("enemyRoomMembership", conversion.Evidence.UnavailableStrategyFields);
        Assert.DoesNotContain("floor", conversion.Evidence.UnavailableStrategyFields);
    }

    [Fact]
    public void MissingFloorAndZeroCoordinateRemainAmbiguous()
    {
        var conversion = WorldSnapshotStrategyAdapter.Convert(Snapshot(
            floor: null,
            floorRange: new DeepDungeonFloorRange(1, 10)));

        Assert.False(conversion.State.Floor.IsKnown);
        Assert.Equal(new DeepDungeonFloorRange(1, 10), conversion.Evidence.ObservedFloorRange);
        Assert.Equal(1, conversion.Evidence.ZeroCoordinateActorCount);
        Assert.Contains("floor", conversion.Evidence.UnavailableStrategyFields);
        Assert.Contains("playerPosition", conversion.Evidence.UnavailableStrategyFields);
        Assert.Null(conversion.Evidence.AnonymousPlayerPosition);
    }

    [Fact]
    public void RetainsAggregateEnemyCastStatusAndDeathEvidenceWithoutInventingRooms()
    {
        var conversion = WorldSnapshotStrategyAdapter.Convert(Snapshot(floor: 4));

        Assert.Equal(1, conversion.Evidence.ActorCount);
        Assert.Equal(1, conversion.Evidence.EnemyCount);
        Assert.Equal(1, conversion.Evidence.CastingEnemyCount);
        Assert.Equal(1, conversion.Evidence.StatusCount);
        Assert.Equal(1, conversion.Evidence.DeadOrDestroyedEnemyCount);
        Assert.False(conversion.State.Enemies.IsKnown);
    }

    [Fact]
    public void ConvertedIncompleteSnapshotStopsFailClosed()
    {
        var conversion = WorldSnapshotStrategyAdapter.Convert(Snapshot(
            floor: null,
            floorRange: new DeepDungeonFloorRange(1, 10)));
        var decision = new DeepDungeonStrategyEngine().Evaluate(
            conversion.State,
            new DeepDungeonStrategyPolicy(TimeSpan.FromMinutes(1)));

        Assert.Equal(new DeepDungeonFloorRange(1, 10), conversion.Evidence.ObservedFloorRange);
        Assert.False(conversion.State.Floor.IsKnown);
        Assert.Equal(StrategyAction.Stop, decision.RecommendedAction);
        Assert.Equal(StrategyRisk.Unknown, decision.Risk);
        Assert.False(decision.CanProceed);
        Assert.Contains(decision.BlockingReasons, reason => reason.Contains("階層", StringComparison.Ordinal));
        Assert.Contains(decision.BlockingReasons, reason => reason.Contains("現在の部屋", StringComparison.Ordinal));
    }

    [Fact]
    public void ObservedOriginIsNotTreatedAsMissingPlayerPosition()
    {
        var pose = new AnonymousPlayerPose(
            new Coordinate2D(0, 0),
            0,
            new ObservationMetadata(ObservationValueSource.PublicDalamud, ObservationValueQuality.Direct, null));
        var conversion = WorldSnapshotStrategyAdapter.Convert(Snapshot(floor: 4, playerPose: pose));

        Assert.Equal(new Coordinate2D(0, 0), conversion.Evidence.AnonymousPlayerPosition);
        Assert.Equal(0, conversion.Evidence.AnonymousPlayerRotation);
        Assert.Equal(ObservationValueQuality.Direct, conversion.Evidence.PlayerPoseMetadata!.Quality);
        Assert.DoesNotContain("playerPosition", conversion.Evidence.UnavailableStrategyFields);
        Assert.Equal(1, conversion.Evidence.ZeroCoordinateActorCount);
    }

    [Fact]
    public void MissingPlayerPoseStillStopsFailClosed()
    {
        var pose = new AnonymousPlayerPose(
            null,
            null,
            new ObservationMetadata(ObservationValueSource.PublicDalamud, ObservationValueQuality.Missing, "dalamud.player.not-created"));
        var conversion = WorldSnapshotStrategyAdapter.Convert(Snapshot(floor: 4, playerPose: pose));
        var decision = new DeepDungeonStrategyEngine().Evaluate(
            conversion.State,
            new DeepDungeonStrategyPolicy(TimeSpan.FromMinutes(1)));

        Assert.Null(conversion.Evidence.AnonymousPlayerPosition);
        Assert.Equal("dalamud.player.not-created", conversion.Evidence.PlayerPoseMetadata!.MissingReason);
        Assert.Contains("playerPosition", conversion.Evidence.UnavailableStrategyFields);
        Assert.Equal(StrategyAction.Stop, decision.RecommendedAction);
        Assert.False(decision.CanProceed);
    }

    [Fact]
    public void ObservedPoseOutsideDeepDungeonDoesNotPermitStrategy()
    {
        var pose = new AnonymousPlayerPose(
            new Coordinate2D(5, 6),
            0.5,
            new ObservationMetadata(ObservationValueSource.PublicDalamud, ObservationValueQuality.Direct, null));
        var conversion = WorldSnapshotStrategyAdapter.Convert(Snapshot(
            floor: null,
            playerPose: pose,
            dungeonKind: DeepDungeonKind.None));
        var decision = new DeepDungeonStrategyEngine().Evaluate(
            conversion.State,
            new DeepDungeonStrategyPolicy(TimeSpan.FromMinutes(1)));

        Assert.Equal(StrategyAction.Stop, decision.RecommendedAction);
        Assert.False(decision.CanProceed);
        Assert.Equal(DeepDungeonKind.None, conversion.State.DungeonKind.RequireKnown("kind"));
    }

    private static WorldSnapshot Snapshot(
        int? floor,
        AnonymousPlayerPose? playerPose = null,
        DeepDungeonKind dungeonKind = DeepDungeonKind.EurekaOrthos,
        DeepDungeonFloorRange? floorRange = null)
    {
        var identity = new ActorIdentity(100, 0);
        var cast = new CastSnapshot(
            32381,
            CastLifecycle.Casting,
            At.AddSeconds(-1),
            At.AddSeconds(2),
            0,
            null,
            new Coordinate2D(10, 20));
        var actor = new ActorSnapshot(
            identity,
            500,
            ActorKind.Enemy,
            new Coordinate2D(0, 0),
            0,
            1,
            0,
            100,
            true,
            false,
            true,
            null,
            cast,
            [new StatusSnapshot(123, null, null, null)]);

        return new WorldSnapshot(
            SchemaVersions.WorldSnapshot,
            1,
            At,
            Guid.Parse("ce0e1027-8d2f-4ac4-9e70-27d806e370a9"),
            ObservationSource.Replay,
            ObservationQuality.Partial,
            new DeepDungeonContext(
                dungeonKind,
                floor,
                1099,
                null,
                dungeonKind == DeepDungeonKind.None ? "not-deep-dungeon" : "duty-unidentified",
                null,
                floorRange,
                dungeonKind == DeepDungeonKind.None ? false : true),
            ImmutableArray.Create(actor),
            ImmutableArray<string>.Empty,
            playerPose);
    }
}
