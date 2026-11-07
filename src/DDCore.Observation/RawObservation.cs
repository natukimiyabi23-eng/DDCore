using System.Collections.Immutable;

namespace DDCore.Observation;

public enum RawActorKind
{
    Unknown,
    Player,
    PartyMember,
    Enemy,
}

public readonly record struct RawPosition(double X, double Y, double Z);

public sealed record RawStatusObservation(
    uint StatusId,
    uint? SourceEntityId,
    byte? Stacks,
    double? RemainingSeconds);

public sealed record RawCastObservation(
    uint ActionId,
    double CurrentSeconds,
    double TotalSeconds,
    ulong? TargetObjectId);

public sealed record RawActorObservation(
    uint EntityId,
    ulong GameObjectId,
    ushort ObjectIndex,
    uint BaseId,
    RawActorKind Kind,
    RawPosition Position,
    double Rotation,
    double HitboxRadius,
    uint CurrentHp,
    uint MaximumHp,
    bool IsDead,
    bool IsValid,
    bool IsInCombat,
    ulong? TargetObjectId,
    RawCastObservation? Cast,
    ImmutableArray<RawStatusObservation> Statuses);

public sealed record RawObservationFrame(
    DateTimeOffset CapturedAt,
    bool IsLoggedIn,
    uint? TerritoryId,
    uint? ContentId,
    bool IsDutyStarted,
    uint? LocalPlayerEntityId,
    ImmutableArray<RawActorObservation> Actors,
    ImmutableArray<string> Diagnostics);

public interface ILiveObservationSource
{
    RawObservationFrame Capture();
}
