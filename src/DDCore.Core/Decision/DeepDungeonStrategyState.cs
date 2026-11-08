using System.Collections.Immutable;
using DDCore.Core.Navigation;
using DDCore.Core.World;

namespace DDCore.Core.Decision;

public enum ObservationKnowledge
{
    Unknown,
    Known,
}

public sealed record ObservedValue<T>
{
    private ObservedValue(ObservationKnowledge knowledge, T? value)
    {
        Knowledge = knowledge;
        Value = value;
        if (!Enum.IsDefined(knowledge) || (knowledge == ObservationKnowledge.Known && value is null))
        {
            throw new InvalidDataException("An observed value must be either unknown or contain a known value.");
        }
    }

    public ObservationKnowledge Knowledge { get; }

    public T? Value { get; }

    public bool IsKnown => Knowledge == ObservationKnowledge.Known;

    public static ObservedValue<T> Unknown() => new(ObservationKnowledge.Unknown, default);

    public static ObservedValue<T> Known(T value) => new(ObservationKnowledge.Known, value);

    public T RequireKnown(string name) =>
        IsKnown ? Value! : throw new InvalidOperationException($"{name} is unknown.");
}

public enum RoomExplorationState
{
    Unknown,
    Unexplored,
    Explored,
}

public enum RoomDangerState
{
    Unknown,
    Safe,
    Dangerous,
}

public sealed record StrategyRoom(
    int Index,
    ObservedValue<RoomConnectionFlags> Connections,
    RoomExplorationState Exploration,
    RoomDangerState Danger)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Connections);
        if (Index is < 0 or >= RoomPathfinder.RoomCount
            || !Enum.IsDefined(Exploration)
            || !Enum.IsDefined(Danger))
        {
            throw new InvalidDataException("A strategy room contains an invalid index or state.");
        }
    }
}

public enum ExitDiscoveryState
{
    Unknown,
    NotFound,
    Found,
}

public sealed record ExitObservation(
    ExitDiscoveryState Discovery,
    int? RoomIndex,
    ObservedValue<bool> IsActive)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(IsActive);
        if (!Enum.IsDefined(Discovery)
            || RoomIndex is < 0 or >= RoomPathfinder.RoomCount
            || (Discovery == ExitDiscoveryState.Found) != RoomIndex.HasValue
            || (Discovery != ExitDiscoveryState.Found && IsActive.IsKnown))
        {
            throw new InvalidDataException("Exit observation is inconsistent with its discovery state.");
        }
    }

    public static ExitObservation Unknown() =>
        new(ExitDiscoveryState.Unknown, null, ObservedValue<bool>.Unknown());

    public static ExitObservation NotFound() =>
        new(ExitDiscoveryState.NotFound, null, ObservedValue<bool>.Unknown());

    public static ExitObservation Found(int roomIndex, ObservedValue<bool> isActive) =>
        new(ExitDiscoveryState.Found, roomIndex, isActive);
}

public enum EnemyDisposition
{
    Unknown,
    Hostile,
    Engaged,
    Defeated,
}

public enum ThreatLevel
{
    Unknown,
    Low,
    Moderate,
    High,
    Critical,
}

public sealed record StrategyEnemy(
    string Id,
    int RoomIndex,
    EnemyDisposition Disposition,
    ThreatLevel Threat)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id)
            || RoomIndex is < 0 or >= RoomPathfinder.RoomCount
            || !Enum.IsDefined(Disposition)
            || !Enum.IsDefined(Threat))
        {
            throw new InvalidDataException("A strategy enemy contains invalid identity, room, or state data.");
        }
    }
}

public enum TrapDangerState
{
    Unknown,
    Safe,
    Dangerous,
}

public sealed record StrategyTrap(string Id, int RoomIndex, TrapDangerState Danger)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id)
            || RoomIndex is < 0 or >= RoomPathfinder.RoomCount
            || !Enum.IsDefined(Danger))
        {
            throw new InvalidDataException("A strategy trap contains invalid identity, room, or danger data.");
        }
    }
}

public sealed record PomanderState(
    string Key,
    ObservedValue<int> Count,
    ObservedValue<bool> IsUsable)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Count);
        ArgumentNullException.ThrowIfNull(IsUsable);
        if (string.IsNullOrWhiteSpace(Key) || (Count.IsKnown && Count.RequireKnown(nameof(Count)) < 0))
        {
            throw new InvalidDataException("A pomander has an invalid key or count.");
        }
    }
}

public enum ResourceReadiness
{
    Unknown,
    Sufficient,
    Low,
    Critical,
}

public sealed record DeepDungeonStrategyState
{
    public DeepDungeonStrategyState(
        Guid sessionId,
        long sequence,
        DateTimeOffset capturedAt,
        ObservedValue<DeepDungeonKind> dungeonKind,
        ObservedValue<int> floor,
        ObservedValue<int> currentRoom,
        ObservedValue<ImmutableArray<StrategyRoom>> rooms,
        ExitObservation exit,
        ObservedValue<ImmutableArray<StrategyEnemy>> enemies,
        ObservedValue<ImmutableArray<StrategyTrap>> traps,
        ObservedValue<ImmutableArray<PomanderState>> pomanders,
        ObservedValue<ResourceReadiness> resourceReadiness,
        ObservedValue<TimeSpan> remainingTime)
    {
        SessionId = sessionId;
        Sequence = sequence;
        CapturedAt = capturedAt;
        DungeonKind = dungeonKind;
        Floor = floor;
        CurrentRoom = currentRoom;
        Rooms = rooms;
        Exit = exit;
        Enemies = enemies;
        Traps = traps;
        Pomanders = pomanders;
        ResourceReadiness = resourceReadiness;
        RemainingTime = remainingTime;
        Validate();
    }

    public Guid SessionId { get; }

    public long Sequence { get; }

    public DateTimeOffset CapturedAt { get; }

    public ObservedValue<DeepDungeonKind> DungeonKind { get; }

    public ObservedValue<int> Floor { get; }

    public ObservedValue<int> CurrentRoom { get; }

    public ObservedValue<ImmutableArray<StrategyRoom>> Rooms { get; }

    public ExitObservation Exit { get; }

    public ObservedValue<ImmutableArray<StrategyEnemy>> Enemies { get; }

    public ObservedValue<ImmutableArray<StrategyTrap>> Traps { get; }

    public ObservedValue<ImmutableArray<PomanderState>> Pomanders { get; }

    public ObservedValue<ResourceReadiness> ResourceReadiness { get; }

    public ObservedValue<TimeSpan> RemainingTime { get; }

    private void Validate()
    {
        ArgumentNullException.ThrowIfNull(DungeonKind);
        ArgumentNullException.ThrowIfNull(Floor);
        ArgumentNullException.ThrowIfNull(CurrentRoom);
        ArgumentNullException.ThrowIfNull(Rooms);
        ArgumentNullException.ThrowIfNull(Exit);
        ArgumentNullException.ThrowIfNull(Enemies);
        ArgumentNullException.ThrowIfNull(Traps);
        ArgumentNullException.ThrowIfNull(Pomanders);
        ArgumentNullException.ThrowIfNull(ResourceReadiness);
        ArgumentNullException.ThrowIfNull(RemainingTime);

        if (SessionId == Guid.Empty || Sequence < 0 || CapturedAt == default)
        {
            throw new InvalidDataException("Strategy state requires a session, non-negative sequence, and timestamp.");
        }

        if (DungeonKind.IsKnown && !Enum.IsDefined(DungeonKind.RequireKnown(nameof(DungeonKind))))
        {
            throw new InvalidDataException("Dungeon kind is invalid.");
        }

        if (Floor.IsKnown && Floor.RequireKnown(nameof(Floor)) is < 1 or > 200)
        {
            throw new InvalidDataException("Floor is outside the accepted Deep Dungeon range.");
        }

        if (CurrentRoom.IsKnown && CurrentRoom.RequireKnown(nameof(CurrentRoom)) is < 0 or >= RoomPathfinder.RoomCount)
        {
            throw new InvalidDataException("Current room is outside the 5x5 room grid.");
        }

        if (RemainingTime.IsKnown && RemainingTime.RequireKnown(nameof(RemainingTime)) < TimeSpan.Zero)
        {
            throw new InvalidDataException("Remaining time cannot be negative.");
        }

        Exit.Validate();
        ValidateRooms();
        ValidateCollection(Enemies, static enemy => enemy.Validate(), nameof(Enemies));
        ValidateCollection(Traps, static trap => trap.Validate(), nameof(Traps));
        ValidateCollection(Pomanders, static pomander => pomander.Validate(), nameof(Pomanders));
        ValidateUnique(Enemies, static enemy => enemy.Id, nameof(Enemies));
        ValidateUnique(Traps, static trap => trap.Id, nameof(Traps));
        ValidateUnique(Pomanders, static pomander => pomander.Key, nameof(Pomanders));

        if (ResourceReadiness.IsKnown && !Enum.IsDefined(ResourceReadiness.RequireKnown(nameof(ResourceReadiness))))
        {
            throw new InvalidDataException("Resource readiness is invalid.");
        }
    }

    private void ValidateRooms()
    {
        if (!Rooms.IsKnown)
        {
            return;
        }

        var rooms = Rooms.RequireKnown(nameof(Rooms));
        if (rooms.IsDefault || rooms.Length != RoomPathfinder.RoomCount)
        {
            throw new InvalidDataException("Known room data must contain the complete 25-room grid.");
        }

        var indexes = new HashSet<int>();
        foreach (var room in rooms)
        {
            room.Validate();
            if (!indexes.Add(room.Index))
            {
                throw new InvalidDataException($"Duplicate strategy room index: {room.Index}.");
            }
        }
    }

    private static void ValidateCollection<T>(ObservedValue<ImmutableArray<T>> observed, Action<T> validate, string name)
    {
        if (!observed.IsKnown)
        {
            return;
        }

        var values = observed.RequireKnown(name);
        if (values.IsDefault)
        {
            throw new InvalidDataException($"{name} cannot be a default immutable array.");
        }

        foreach (var value in values)
        {
            validate(value);
        }
    }

    private static void ValidateUnique<T>(
        ObservedValue<ImmutableArray<T>> observed,
        Func<T, string> keySelector,
        string name)
    {
        if (!observed.IsKnown)
        {
            return;
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (observed.RequireKnown(name).Any(value => !keys.Add(keySelector(value))))
        {
            throw new InvalidDataException($"{name} contains duplicate identities.");
        }
    }
}
