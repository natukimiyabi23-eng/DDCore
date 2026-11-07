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
        var conversion = WorldSnapshotStrategyAdapter.Convert(Snapshot(floor: null));

        Assert.False(conversion.State.Floor.IsKnown);
        Assert.Equal(1, conversion.Evidence.ZeroCoordinateActorCount);
        Assert.Contains("floor", conversion.Evidence.UnavailableStrategyFields);
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
        var conversion = WorldSnapshotStrategyAdapter.Convert(Snapshot(floor: null));
        var decision = new DeepDungeonStrategyEngine().Evaluate(
            conversion.State,
            new DeepDungeonStrategyPolicy(TimeSpan.FromMinutes(1)));

        Assert.Equal(StrategyAction.Stop, decision.RecommendedAction);
        Assert.Equal(StrategyRisk.Unknown, decision.Risk);
        Assert.False(decision.CanProceed);
        Assert.Contains(decision.BlockingReasons, reason => reason.Contains("階層", StringComparison.Ordinal));
        Assert.Contains(decision.BlockingReasons, reason => reason.Contains("現在の部屋", StringComparison.Ordinal));
    }

    private static WorldSnapshot Snapshot(int? floor)
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
            new DeepDungeonContext(DeepDungeonKind.EurekaOrthos, floor, 1099, null, "duty-unidentified", null),
            ImmutableArray.Create(actor),
            ImmutableArray<string>.Empty);
    }
}
