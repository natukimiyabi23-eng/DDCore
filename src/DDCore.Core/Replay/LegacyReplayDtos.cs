using System.Collections.Immutable;
using DDCore.Core.Geometry;
using DDCore.Core.Model;
using DDCore.Core.World;

namespace DDCore.Core.Replay;

internal sealed record LegacyReplayDocument(
    int SchemaVersion,
    DateTimeOffset CreatedAtUtc,
    Guid SessionId,
    string ProducerVersion,
    ImmutableArray<LegacyWorldSnapshot> Snapshots);

internal sealed record LegacyWorldSnapshot(
    int SchemaVersion,
    long Sequence,
    DateTimeOffset CapturedAt,
    Guid SessionId,
    ObservationSource Source,
    ObservationQuality Quality,
    DeepDungeonContext Dungeon,
    ImmutableArray<LegacyActorSnapshot> Actors,
    ImmutableArray<string> Diagnostics,
    AnonymousPlayerPose? PlayerPose = null,
    bool IsDeepDungeon = false,
    string? ContentKey = null);

internal sealed record LegacyActorSnapshot(
    ActorIdentity Identity,
    uint ObjectId,
    ActorKind Kind,
    Coordinate2D Position,
    double Rotation,
    double HitboxRadius,
    uint CurrentHp,
    uint MaximumHp,
    bool IsDead,
    bool IsDestroyed,
    bool IsInCombat,
    ActorIdentity? Target,
    LegacyCastSnapshot? Cast,
    ImmutableArray<StatusSnapshot> Statuses);

internal sealed record LegacyCastSnapshot(
    uint ActionId,
    CastLifecycle Lifecycle,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishesAt,
    double Rotation,
    ActorIdentity? Target,
    Coordinate2D? TargetLocation);
