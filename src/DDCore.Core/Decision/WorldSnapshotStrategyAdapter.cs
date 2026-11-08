using System.Collections.Immutable;
using DDCore.Core.Geometry;
using DDCore.Core.World;

namespace DDCore.Core.Decision;

public sealed record StrategyObservationEvidence(
    int ActorCount,
    int EnemyCount,
    int CastingEnemyCount,
    int StatusCount,
    int DeadOrDestroyedEnemyCount,
    int ZeroCoordinateActorCount,
    Coordinate2D? AnonymousPlayerPosition,
    double? AnonymousPlayerRotation,
    ObservationMetadata? PlayerPoseMetadata,
    ImmutableArray<string> UnavailableStrategyFields);

public sealed record StrategyInputConversion(
    DeepDungeonStrategyState State,
    StrategyObservationEvidence Evidence);

/// <summary>
/// Converts only directly observed WorldSnapshot fields. Room membership, exits, resources, and other
/// strategy inputs are deliberately left unknown until an observation source provides them explicitly.
/// </summary>
public static class WorldSnapshotStrategyAdapter
{
    private static readonly Coordinate2D Zero = new(0, 0);

    private static readonly ImmutableArray<string> AlwaysUnavailableFields =
    [
        "currentRoom",
        "rooms",
        "exit",
        "enemyRoomMembership",
        "traps",
        "pomanders",
        "resourceReadiness",
        "remainingTime",
    ];

    public static StrategyInputConversion Convert(WorldSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.Validate();

        var enemies = snapshot.Actors.Where(static actor => actor.Kind == ActorKind.Enemy).ToImmutableArray();
        var unavailable = AlwaysUnavailableFields.ToBuilder();
        if (snapshot.Dungeon.Floor is null)
        {
            unavailable.Add("floor");
        }

        if (snapshot.PlayerPose?.Position is null)
        {
            unavailable.Add("playerPosition");
        }

        var state = new DeepDungeonStrategyState(
            snapshot.SessionId,
            snapshot.Sequence,
            snapshot.CapturedAt,
            ObservedValue<DeepDungeonKind>.Known(snapshot.Dungeon.Kind),
            snapshot.Dungeon.Floor is { } floor
                ? ObservedValue<int>.Known(floor)
                : ObservedValue<int>.Unknown(),
            ObservedValue<int>.Unknown(),
            ObservedValue<ImmutableArray<StrategyRoom>>.Unknown(),
            ExitObservation.Unknown(),
            ObservedValue<ImmutableArray<StrategyEnemy>>.Unknown(),
            ObservedValue<ImmutableArray<StrategyTrap>>.Unknown(),
            ObservedValue<ImmutableArray<PomanderState>>.Unknown(),
            ObservedValue<ResourceReadiness>.Unknown(),
            ObservedValue<TimeSpan>.Unknown());

        var evidence = new StrategyObservationEvidence(
            snapshot.Actors.Length,
            enemies.Length,
            enemies.Count(static actor => actor.Cast?.Lifecycle == CastLifecycle.Casting),
            enemies.Sum(static actor => actor.Statuses.Length),
            enemies.Count(static actor => actor.IsDead || actor.IsDestroyed),
            snapshot.Actors.Count(actor => actor.Position == Zero),
            snapshot.PlayerPose?.Position,
            snapshot.PlayerPose?.Rotation,
            snapshot.PlayerPose?.Metadata,
            unavailable.ToImmutable());
        return new StrategyInputConversion(state, evidence);
    }
}
