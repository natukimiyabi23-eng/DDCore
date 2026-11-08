using System.Collections.Immutable;
using DDCore.Core.Geometry;

namespace DDCore.Core.Navigation;

public enum DeepDungeonEntryDestination
{
    EurekaOrthos,
    PalaceOfTheDead,
    HeavenOnHigh,
}

public enum EntryTravelArea
{
    Unknown,
    Other,
    MorDhonaRevenantsToll,
    MorDhonaEightSentinels,
}

public enum EntryAccessState
{
    Unknown,
    Locked,
    Unlocked,
}

public enum TeleportAvailability
{
    Unknown,
    Unavailable,
    Available,
}

public enum EntryTravelStepKind
{
    TeleportToHub,
    NavigateToLandmark,
    UseAreaTransition,
    VerifyArrival,
}

public readonly record struct MapCoordinate(decimal X, decimal Y)
{
    public void Validate()
    {
        if (X is < 0 or > 100 || Y is < 0 or > 100)
        {
            throw new InvalidDataException("Map coordinates must be in the displayed 0-100 range.");
        }
    }
}

public sealed record EntryTravelLandmark(
    string Key,
    string JapaneseName,
    string AreaName,
    MapCoordinate MapPosition)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Key)
            || string.IsNullOrWhiteSpace(JapaneseName)
            || string.IsNullOrWhiteSpace(AreaName))
        {
            throw new InvalidDataException("An entry-travel landmark requires stable and display names.");
        }

        MapPosition.Validate();
    }
}

public sealed record EntryTravelContext(
    EntryTravelArea CurrentArea,
    EntryAccessState Access,
    TeleportAvailability Teleport);

public sealed record EntryTravelStep(
    EntryTravelStepKind Kind,
    string Description,
    EntryTravelLandmark? Landmark,
    bool RequiresExternalSideEffect)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Kind) || string.IsNullOrWhiteSpace(Description))
        {
            throw new InvalidDataException("An entry-travel step has an invalid kind or description.");
        }

        Landmark?.Validate();
    }
}

public sealed record EntryTravelPlan(
    DeepDungeonEntryDestination Destination,
    ImmutableArray<EntryTravelStep> Steps,
    bool IsExecutable,
    string StatusReason)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Destination) || Steps.IsDefault || string.IsNullOrWhiteSpace(StatusReason))
        {
            throw new InvalidDataException("An entry-travel plan has an invalid destination, steps, or status reason.");
        }

        foreach (var step in Steps)
        {
            step.Validate();
        }

        if (IsExecutable && Steps.Length == 0)
        {
            throw new InvalidDataException("An executable entry-travel plan cannot be empty.");
        }
    }
}

public enum EntryTravelRunState
{
    Idle,
    Ready,
    Running,
    Arrived,
    Stopped,
    Blocked,
    Faulted,
}

public sealed record EntryTravelStartAuthorization(Guid ConfirmationId, DateTimeOffset ConfirmedAt)
{
    public void Validate()
    {
        if (ConfirmationId == Guid.Empty || ConfirmedAt == default)
        {
            throw new InvalidDataException("Entry travel requires an explicit, timestamped user authorization.");
        }
    }
}

public sealed record EntryTravelProgressObservation(
    DateTimeOffset ObservedAt,
    int ActiveStepIndex,
    Coordinate2D? Position,
    bool StepCompleted,
    bool DestinationReached,
    string? FailureReason);

public sealed record EntryTravelStatus(
    EntryTravelRunState State,
    int CompletedSteps,
    int TotalSteps,
    DateTimeOffset? LastProgressAt,
    string Reason)
{
    public static EntryTravelStatus Idle { get; } = new(
        EntryTravelRunState.Idle,
        0,
        0,
        null,
        "移動は開始されていません。");
}
