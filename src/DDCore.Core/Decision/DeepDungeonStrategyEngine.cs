using System.Collections.Immutable;
using DDCore.Core.Navigation;
using DDCore.Core.World;

namespace DDCore.Core.Decision;

public enum StrategyAction
{
    Stop,
    AvoidDanger,
    EngageEnemy,
    PreserveResources,
    MoveToExit,
    ExploreRoom,
    SearchForExit,
}

public enum StrategyRisk
{
    Unknown,
    Low,
    Moderate,
    High,
    Critical,
}

public sealed record DeepDungeonStrategyPolicy(TimeSpan MinimumRemainingTime)
{
    public void Validate()
    {
        if (MinimumRemainingTime < TimeSpan.Zero)
        {
            throw new InvalidDataException("Minimum remaining time cannot be negative.");
        }
    }
}

public sealed record StrategyDecision(
    long SnapshotSequence,
    StrategyAction RecommendedAction,
    string Reason,
    ImmutableArray<string> Preconditions,
    StrategyRisk Risk,
    ImmutableArray<string> BlockingReasons,
    int? TargetRoom,
    ImmutableArray<int> Route)
{
    public bool CanProceed => RecommendedAction != StrategyAction.Stop && BlockingReasons.IsEmpty;
}

public interface IDeepDungeonStrategyEngine
{
    StrategyDecision Evaluate(
        DeepDungeonStrategyState current,
        DeepDungeonStrategyPolicy policy,
        DeepDungeonStrategyState? previous = null);
}

public sealed class DeepDungeonStrategyEngine : IDeepDungeonStrategyEngine
{
    private static readonly ImmutableArray<string> ObservationPreconditions =
        ["最新かつ完全な読み取り専用観測", "実行前の別系統安全再検証"];

    public StrategyDecision Evaluate(
        DeepDungeonStrategyState current,
        DeepDungeonStrategyPolicy policy,
        DeepDungeonStrategyState? previous = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();

        if (previous is not null
            && current.SessionId == previous.SessionId
            && (current.Sequence <= previous.Sequence || current.CapturedAt < previous.CapturedAt))
        {
            return Stop(current, "観測状態が逆行または重複しています。", "時系列が単調増加していません。", StrategyRisk.Unknown);
        }

        var missing = RequiredObservationGaps(current);
        if (!missing.IsEmpty)
        {
            return Stop(current, "攻略判断に必要な観測情報が不足しています。", missing, StrategyRisk.Unknown);
        }

        var kind = current.DungeonKind.RequireKnown(nameof(current.DungeonKind));
        if (kind is DeepDungeonKind.None or DeepDungeonKind.Unsupported)
        {
            return Stop(current, "対応するディープダンジョンを観測していません。", "対応コンテンツではありません。", StrategyRisk.Unknown);
        }

        var remaining = current.RemainingTime.RequireKnown(nameof(current.RemainingTime));
        if (remaining < policy.MinimumRemainingTime)
        {
            return Stop(
                current,
                "残り時間が明示された最低行動時間を下回っています。",
                $"残り時間 {remaining} / 必要時間 {policy.MinimumRemainingTime}",
                StrategyRisk.Critical);
        }

        var currentRoom = current.CurrentRoom.RequireKnown(nameof(current.CurrentRoom));
        var rooms = current.Rooms.RequireKnown(nameof(current.Rooms)).OrderBy(static room => room.Index).ToImmutableArray();
        var mapResult = BuildMap(current, rooms);
        if (mapResult.Error is not null)
        {
            return Stop(current, "部屋接続情報から安全な経路を構築できません。", mapResult.Error, StrategyRisk.Unknown);
        }

        var map = mapResult.Map!;
        var room = rooms[currentRoom];
        var currentTraps = current.Traps.RequireKnown(nameof(current.Traps))
            .Where(trap => trap.RoomIndex == currentRoom)
            .OrderBy(static trap => trap.Id, StringComparer.Ordinal)
            .ToImmutableArray();
        if (room.Danger == RoomDangerState.Unknown || currentTraps.Any(static trap => trap.Danger == TrapDangerState.Unknown))
        {
            return Stop(current, "現在地の危険状態を判定できません。", "部屋または罠の危険度がUnknownです。", StrategyRisk.Unknown);
        }

        if (room.Danger == RoomDangerState.Dangerous
            || currentTraps.Any(static trap => trap.Danger == TrapDangerState.Dangerous))
        {
            return AvoidDanger(
                current,
                rooms,
                map,
                currentRoom,
                current.Enemies.RequireKnown(nameof(current.Enemies)),
                current.Traps.RequireKnown(nameof(current.Traps)));
        }

        var enemies = current.Enemies.RequireKnown(nameof(current.Enemies))
            .Where(enemy => enemy.RoomIndex == currentRoom)
            .OrderByDescending(static enemy => enemy.Disposition == EnemyDisposition.Engaged)
            .ThenByDescending(static enemy => enemy.Threat)
            .ThenBy(static enemy => enemy.Id, StringComparer.Ordinal)
            .ToImmutableArray();
        if (enemies.Any(static enemy => enemy.Disposition == EnemyDisposition.Unknown || enemy.Threat == ThreatLevel.Unknown))
        {
            return Stop(current, "現在地の敵を評価できません。", "敵の状態または脅威度がUnknownです。", StrategyRisk.Unknown);
        }

        var engaged = enemies.FirstOrDefault(static enemy => enemy.Disposition == EnemyDisposition.Engaged);
        if (engaged is not null)
        {
            return Decide(
                current,
                StrategyAction.EngageEnemy,
                $"交戦中の敵 {engaged.Id} への対応を継続します。",
                StrategyRisk.High,
                currentRoom,
                []);
        }

        var resources = current.ResourceReadiness.RequireKnown(nameof(current.ResourceReadiness));
        if (resources is ResourceReadiness.Low or ResourceReadiness.Critical)
        {
            return Decide(
                current,
                StrategyAction.PreserveResources,
                "資源状態が不足しているため、新しい交戦や探索を開始しません。",
                resources == ResourceReadiness.Critical ? StrategyRisk.Critical : StrategyRisk.High,
                currentRoom,
                []);
        }

        var hostile = enemies.FirstOrDefault(static enemy => enemy.Disposition == EnemyDisposition.Hostile);
        if (hostile is not null)
        {
            return Decide(
                current,
                StrategyAction.EngageEnemy,
                $"現在地の敵 {hostile.Id} を交戦候補として推奨します。",
                ThreatToRisk(hostile.Threat),
                currentRoom,
                []);
        }

        if (current.Exit.Discovery == ExitDiscoveryState.Found)
        {
            if (!current.Exit.IsActive.IsKnown)
            {
                return Stop(current, "出口の利用可否を判定できません。", "出口の活性状態がUnknownです。", StrategyRisk.Unknown);
            }

            if (current.Exit.IsActive.RequireKnown(nameof(current.Exit.IsActive)))
            {
                return MoveToExit(
                    current,
                    rooms,
                    map,
                    currentRoom,
                    current.Exit.RoomIndex!.Value,
                    current.Enemies.RequireKnown(nameof(current.Enemies)),
                    current.Traps.RequireKnown(nameof(current.Traps)));
            }
        }

        return ExploreOrSearch(
            current,
            rooms,
            map,
            currentRoom,
            current.Enemies.RequireKnown(nameof(current.Enemies)),
            current.Traps.RequireKnown(nameof(current.Traps)));
    }

    private static ImmutableArray<string> RequiredObservationGaps(DeepDungeonStrategyState state)
    {
        var gaps = ImmutableArray.CreateBuilder<string>();
        AddGap(state.DungeonKind, "DD種別", gaps);
        AddGap(state.Floor, "階層", gaps);
        AddGap(state.CurrentRoom, "現在の部屋", gaps);
        AddGap(state.Rooms, "部屋一覧", gaps);
        AddGap(state.Enemies, "敵一覧", gaps);
        AddGap(state.Traps, "罠一覧", gaps);
        AddGap(state.Pomanders, "魔土器一覧", gaps);
        AddGap(state.ResourceReadiness, "資源充足度", gaps);
        AddGap(state.RemainingTime, "残り時間", gaps);
        if (state.ResourceReadiness.IsKnown
            && state.ResourceReadiness.RequireKnown(nameof(state.ResourceReadiness)) == ResourceReadiness.Unknown)
        {
            gaps.Add("資源充足度がUnknownです。");
        }

        if (state.Exit.Discovery == ExitDiscoveryState.Unknown)
        {
            gaps.Add("出口の発見状態がUnknownです。");
        }

        if (state.Pomanders.IsKnown)
        {
            foreach (var pomander in state.Pomanders.RequireKnown(nameof(state.Pomanders)).OrderBy(static value => value.Key, StringComparer.Ordinal))
            {
                if (!pomander.Count.IsKnown)
                {
                    gaps.Add($"魔土器 {pomander.Key} の所持数がUnknownです。");
                }

                if (!pomander.IsUsable.IsKnown)
                {
                    gaps.Add($"魔土器 {pomander.Key} の使用可否がUnknownです。");
                }
            }
        }

        return gaps.ToImmutable();
    }

    private static void AddGap<T>(ObservedValue<T> value, string name, ImmutableArray<string>.Builder gaps)
    {
        if (!value.IsKnown)
        {
            gaps.Add($"{name}がUnknownです。");
        }
    }

    private static (RoomConnectionFlags[]? Map, string? Error) BuildMap(
        DeepDungeonStrategyState current,
        ImmutableArray<StrategyRoom> rooms)
    {
        if (rooms.Any(static room => !room.Connections.IsKnown))
        {
            return (null, "1つ以上の部屋接続がUnknownです。");
        }

        var map = new RoomConnectionFlags[RoomPathfinder.RoomCount];
        foreach (var room in rooms)
        {
            map[room.Index] = room.Connections.RequireKnown(nameof(room.Connections));
        }

        try
        {
            _ = RoomPathfinder.FindPath(map, current.CurrentRoom.RequireKnown(nameof(current.CurrentRoom)), 0);
            return (map, null);
        }
        catch (Exception error) when (error is ArgumentException or InvalidDataException)
        {
            return (null, error.Message);
        }
    }

    private static StrategyDecision AvoidDanger(
        DeepDungeonStrategyState current,
        ImmutableArray<StrategyRoom> rooms,
        RoomConnectionFlags[] map,
        int currentRoom,
        ImmutableArray<StrategyEnemy> enemies,
        ImmutableArray<StrategyTrap> traps)
    {
        var safeMap = BuildSafeTraversalMap(rooms, map, currentRoom, enemies, traps);
        var route = CandidateRoutes(rooms, safeMap, currentRoom, room =>
                room.Exploration == RoomExplorationState.Explored && IsRoomSafeForTravel(room, enemies, traps))
            .FirstOrDefault();
        if (route.Route.IsDefaultOrEmpty)
        {
            return Stop(current, "現在地は危険ですが、安全と確認済みの退避経路がありません。", "退避先または経路を確認できません。", StrategyRisk.Critical);
        }

        return Decide(
            current,
            StrategyAction.AvoidDanger,
            $"危険な現在地から確認済みの安全な部屋 {route.Room} へ退避します。",
            StrategyRisk.Critical,
            route.Room,
            route.Route);
    }

    private static StrategyDecision MoveToExit(
        DeepDungeonStrategyState current,
        ImmutableArray<StrategyRoom> rooms,
        RoomConnectionFlags[] map,
        int currentRoom,
        int exitRoom,
        ImmutableArray<StrategyEnemy> enemies,
        ImmutableArray<StrategyTrap> traps)
    {
        var destination = rooms[exitRoom];
        if (!IsRoomSafeForTravel(destination, enemies, traps))
        {
            return Stop(current, "出口までの安全性を確認できません。", $"出口の部屋 {exitRoom} が安全と確認できません。", StrategyRisk.High);
        }

        if (currentRoom == exitRoom)
        {
            return Decide(current, StrategyAction.MoveToExit, "活性化した出口の部屋に到達済みです。", StrategyRisk.Low, exitRoom, []);
        }

        var safeMap = BuildSafeTraversalMap(rooms, map, currentRoom, enemies, traps);
        var route = RoomPathfinder.FindPath(safeMap, currentRoom, exitRoom).ToImmutableArray();
        if (route.IsEmpty)
        {
            return Stop(current, "活性化した出口への経路がありません。", $"部屋 {exitRoom} へ到達不能です。", StrategyRisk.High);
        }

        return Decide(current, StrategyAction.MoveToExit, $"活性化した出口の部屋 {exitRoom} へ向かいます。", StrategyRisk.Low, exitRoom, route);
    }

    private static StrategyDecision ExploreOrSearch(
        DeepDungeonStrategyState current,
        ImmutableArray<StrategyRoom> rooms,
        RoomConnectionFlags[] map,
        int currentRoom,
        ImmutableArray<StrategyEnemy> enemies,
        ImmutableArray<StrategyTrap> traps)
    {
        if (rooms.Any(static room => room.Exploration == RoomExplorationState.Unknown))
        {
            return Stop(current, "探索状況が不完全です。", "1つ以上の部屋の探索状態がUnknownです。", StrategyRisk.Unknown);
        }

        var safeMap = BuildSafeTraversalMap(rooms, map, currentRoom, enemies, traps);
        var route = CandidateRoutes(rooms, safeMap, currentRoom, room =>
                room.Exploration == RoomExplorationState.Unexplored && IsRoomSafeForTravel(room, enemies, traps))
            .FirstOrDefault();
        if (!route.Route.IsDefaultOrEmpty)
        {
            return Decide(
                current,
                StrategyAction.ExploreRoom,
                $"到達可能な未探索部屋 {route.Room} を探索します。",
                StrategyRisk.Moderate,
                route.Room,
                route.Route);
        }

        var hasUnexplored = rooms.Any(static room => room.Exploration == RoomExplorationState.Unexplored);
        if (hasUnexplored)
        {
            return Stop(current, "未探索部屋への安全な経路がありません。", "到達可能で安全な未探索部屋を確認できません。", StrategyRisk.High);
        }

        if (current.Exit.Discovery == ExitDiscoveryState.NotFound)
        {
            return Decide(
                current,
                StrategyAction.SearchForExit,
                "全ての既知部屋を探索済みですが、出口が未発見です。現在地を再確認します。",
                StrategyRisk.Moderate,
                currentRoom,
                []);
        }

        return Decide(
            current,
            StrategyAction.SearchForExit,
            "出口は発見済みですが未活性のため、現在地で追加条件を確認します。",
            StrategyRisk.Moderate,
            currentRoom,
            []);
    }

    private static IEnumerable<(int Room, ImmutableArray<int> Route)> CandidateRoutes(
        ImmutableArray<StrategyRoom> rooms,
        RoomConnectionFlags[] map,
        int currentRoom,
        Func<StrategyRoom, bool> predicate) =>
        rooms.Where(room => room.Index != currentRoom && predicate(room))
            .Select(room => (Room: room.Index, Route: RoomPathfinder.FindPath(map, currentRoom, room.Index).ToImmutableArray()))
            .Where(static candidate => !candidate.Route.IsEmpty)
            .OrderBy(static candidate => candidate.Route.Length)
            .ThenBy(static candidate => candidate.Room);

    private static RoomConnectionFlags[] BuildSafeTraversalMap(
        ImmutableArray<StrategyRoom> rooms,
        RoomConnectionFlags[] map,
        int currentRoom,
        ImmutableArray<StrategyEnemy> enemies,
        ImmutableArray<StrategyTrap> traps)
    {
        var traversable = rooms.ToDictionary(
            static room => room.Index,
            room => room.Index == currentRoom || IsRoomSafeForTravel(room, enemies, traps));
        var safeMap = new RoomConnectionFlags[map.Length];
        for (var room = 0; room < map.Length; room++)
        {
            if (!traversable[room])
            {
                continue;
            }

            var flags = map[room];
            safeMap[room] = KeepTraversableEdge(flags, RoomConnectionFlags.North, room - RoomPathfinder.GridWidth, traversable)
                | KeepTraversableEdge(flags, RoomConnectionFlags.South, room + RoomPathfinder.GridWidth, traversable)
                | KeepTraversableEdge(flags, RoomConnectionFlags.West, room - 1, traversable)
                | KeepTraversableEdge(flags, RoomConnectionFlags.East, room + 1, traversable);
        }

        return safeMap;
    }

    private static RoomConnectionFlags KeepTraversableEdge(
        RoomConnectionFlags flags,
        RoomConnectionFlags direction,
        int adjacent,
        IReadOnlyDictionary<int, bool> traversable) =>
        flags.HasFlag(direction) && traversable.GetValueOrDefault(adjacent) ? direction : RoomConnectionFlags.None;

    private static bool IsRoomSafeForTravel(
        StrategyRoom room,
        ImmutableArray<StrategyEnemy> enemies,
        ImmutableArray<StrategyTrap> traps) =>
        room.Danger == RoomDangerState.Safe
        && traps.Where(trap => trap.RoomIndex == room.Index).All(static trap => trap.Danger == TrapDangerState.Safe)
        && enemies.Where(enemy => enemy.RoomIndex == room.Index).All(static enemy => enemy.Disposition == EnemyDisposition.Defeated);

    private static StrategyRisk ThreatToRisk(ThreatLevel threat) => threat switch
    {
        ThreatLevel.Low => StrategyRisk.Low,
        ThreatLevel.Moderate => StrategyRisk.Moderate,
        ThreatLevel.High => StrategyRisk.High,
        ThreatLevel.Critical => StrategyRisk.Critical,
        _ => StrategyRisk.Unknown,
    };

    private static StrategyDecision Decide(
        DeepDungeonStrategyState state,
        StrategyAction action,
        string reason,
        StrategyRisk risk,
        int? targetRoom,
        ImmutableArray<int> route) =>
        new(
            state.Sequence,
            action,
            reason,
            ObservationPreconditions,
            risk,
            ImmutableArray<string>.Empty,
            targetRoom,
            route);

    private static StrategyDecision Stop(
        DeepDungeonStrategyState state,
        string reason,
        string blocker,
        StrategyRisk risk) => Stop(state, reason, [blocker], risk);

    private static StrategyDecision Stop(
        DeepDungeonStrategyState state,
        string reason,
        ImmutableArray<string> blockers,
        StrategyRisk risk) =>
        new(
            state.Sequence,
            StrategyAction.Stop,
            reason,
            ObservationPreconditions,
            risk,
            blockers,
            null,
            ImmutableArray<int>.Empty);
}
