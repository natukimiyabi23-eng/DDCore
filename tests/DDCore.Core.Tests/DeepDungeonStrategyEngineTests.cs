using System.Collections.Immutable;
using DDCore.Core.Decision;
using DDCore.Core.Navigation;
using DDCore.Core.World;

namespace DDCore.Core.Tests;

public sealed class DeepDungeonStrategyEngineTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 8, 5, 30, 0, TimeSpan.FromHours(9));
    private static readonly Guid Session = Guid.Parse("bdbac1e2-f4b8-4e98-9726-742a669113e2");
    private static readonly DeepDungeonStrategyPolicy Policy = new(TimeSpan.FromMinutes(1));

    [Fact]
    public void UnknownRequiredObservationStopsFailClosed()
    {
        var state = State(rooms: ObservedValue<ImmutableArray<StrategyRoom>>.Unknown());

        var decision = Evaluate(state);

        Assert.Equal(StrategyAction.Stop, decision.RecommendedAction);
        Assert.Equal(StrategyRisk.Unknown, decision.Risk);
        Assert.Contains(decision.BlockingReasons, reason => reason.Contains("部屋一覧", StringComparison.Ordinal));
        Assert.False(decision.CanProceed);
    }

    [Theory]
    [InlineData(9, 10, 1)]
    [InlineData(10, 10, -1)]
    public void SequenceOrTimeRegressionStops(long currentSequence, long previousSequence, int minuteOffset)
    {
        var previous = State(sequence: previousSequence, capturedAt: At);
        var current = State(sequence: currentSequence, capturedAt: At.AddMinutes(minuteOffset));

        var decision = Evaluate(current, previous);

        Assert.Equal(StrategyAction.Stop, decision.RecommendedAction);
        Assert.Contains("逆行", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void NewSessionDoesNotCompareSequenceWithPreviousSession()
    {
        var previous = State(sequence: 100, sessionId: Guid.NewGuid());
        var current = State(sequence: 1, sessionId: Guid.NewGuid());

        var decision = Evaluate(current, previous);

        Assert.NotEqual(StrategyAction.Stop, decision.RecommendedAction);
    }

    [Fact]
    public void RemainingTimeBelowExplicitPolicyStops()
    {
        var state = State(remainingTime: ObservedValue<TimeSpan>.Known(TimeSpan.FromSeconds(59)));

        var decision = Evaluate(state);

        Assert.Equal(StrategyAction.Stop, decision.RecommendedAction);
        Assert.Equal(StrategyRisk.Critical, decision.Risk);
        Assert.Contains("残り時間", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void DangerousRoomUsesShortestKnownSafeRetreat()
    {
        var map = Map((0, 1), (0, 5), (1, 2));
        var rooms = Rooms(map, explored: [0, 1, 2, 5], dangerous: [0]);
        var state = State(currentRoom: 0, rooms: Known(rooms));

        var decision = Evaluate(state);

        Assert.Equal(StrategyAction.AvoidDanger, decision.RecommendedAction);
        Assert.Equal(1, decision.TargetRoom);
        Assert.Equal<int>([1], decision.Route);
        Assert.Equal(StrategyRisk.Critical, decision.Risk);
    }

    [Fact]
    public void DangerousTrapUsesSameRetreatPolicy()
    {
        var map = Map((0, 1));
        var state = State(
            rooms: Known(Rooms(map, explored: [0, 1])),
            traps: KnownArray(new StrategyTrap("trap-a", 0, TrapDangerState.Dangerous)));

        var decision = Evaluate(state);

        Assert.Equal(StrategyAction.AvoidDanger, decision.RecommendedAction);
        Assert.Equal<int>([1], decision.Route);
    }

    [Fact]
    public void DangerWithoutSafeRouteStops()
    {
        var state = State(rooms: Known(Rooms(Map(), explored: [0], dangerous: [0])));

        var decision = Evaluate(state);

        Assert.Equal(StrategyAction.Stop, decision.RecommendedAction);
        Assert.Equal(StrategyRisk.Critical, decision.Risk);
        Assert.Contains("退避", decision.BlockingReasons[0], StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownCurrentDangerStops()
    {
        var state = State(rooms: Known(Rooms(Map(), explored: [0], unknownDanger: [0])));

        var decision = Evaluate(state);

        Assert.Equal(StrategyAction.Stop, decision.RecommendedAction);
        Assert.Contains("危険状態", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void EngagedEnemyTakesPriorityOverResourceConservation()
    {
        var enemy = new StrategyEnemy("enemy-a", 0, EnemyDisposition.Engaged, ThreatLevel.High);
        var state = State(enemies: KnownArray(enemy), resourceReadiness: Known(ResourceReadiness.Critical));

        var decision = Evaluate(state);

        Assert.Equal(StrategyAction.EngageEnemy, decision.RecommendedAction);
        Assert.Equal(StrategyRisk.High, decision.Risk);
        Assert.Contains("継続", decision.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ResourceReadiness.Low, StrategyRisk.High)]
    [InlineData(ResourceReadiness.Critical, StrategyRisk.Critical)]
    public void ResourceShortagePreventsNewCombat(ResourceReadiness readiness, StrategyRisk expectedRisk)
    {
        var enemy = new StrategyEnemy("enemy-a", 0, EnemyDisposition.Hostile, ThreatLevel.Low);
        var state = State(enemies: KnownArray(enemy), resourceReadiness: Known(readiness));

        var decision = Evaluate(state);

        Assert.Equal(StrategyAction.PreserveResources, decision.RecommendedAction);
        Assert.Equal(expectedRisk, decision.Risk);
    }

    [Fact]
    public void SufficientResourcesRecommendCurrentRoomEnemy()
    {
        var enemy = new StrategyEnemy("enemy-a", 0, EnemyDisposition.Hostile, ThreatLevel.Moderate);
        var state = State(enemies: KnownArray(enemy));

        var decision = Evaluate(state);

        Assert.Equal(StrategyAction.EngageEnemy, decision.RecommendedAction);
        Assert.Equal(StrategyRisk.Moderate, decision.Risk);
        Assert.Equal(0, decision.TargetRoom);
    }

    [Fact]
    public void UnknownEnemyStateStops()
    {
        var enemy = new StrategyEnemy("enemy-a", 0, EnemyDisposition.Unknown, ThreatLevel.Unknown);

        var decision = Evaluate(State(enemies: KnownArray(enemy)));

        Assert.Equal(StrategyAction.Stop, decision.RecommendedAction);
        Assert.Contains("敵", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveExitUsesRoomPathfinderRoute()
    {
        var map = Map((0, 1), (1, 2));
        var state = State(
            rooms: Known(Rooms(map, explored: [0, 1, 2])),
            exit: ExitObservation.Found(2, ObservedValue<bool>.Known(true)));

        var decision = Evaluate(state);

        Assert.Equal(StrategyAction.MoveToExit, decision.RecommendedAction);
        Assert.Equal(2, decision.TargetRoom);
        Assert.Equal<int>([1, 2], decision.Route);
    }

    [Fact]
    public void AlreadyAtActiveExitProducesEmptyRoute()
    {
        var state = State(exit: ExitObservation.Found(0, ObservedValue<bool>.Known(true)));

        var decision = Evaluate(state);

        Assert.Equal(StrategyAction.MoveToExit, decision.RecommendedAction);
        Assert.Empty(decision.Route);
        Assert.True(decision.CanProceed);
    }

    [Fact]
    public void DisconnectedActiveExitStops()
    {
        var state = State(exit: ExitObservation.Found(2, ObservedValue<bool>.Known(true)));

        var decision = Evaluate(state);

        Assert.Equal(StrategyAction.Stop, decision.RecommendedAction);
        Assert.Contains("到達不能", decision.BlockingReasons[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ExitRouteAvoidsKnownDangerousIntermediateRoom()
    {
        var map = Map((0, 1), (1, 2), (0, 5), (5, 6), (6, 7), (7, 2));
        var rooms = Rooms(map, explored: [0, 1, 2, 5, 6, 7], dangerous: [1]);
        var state = State(rooms: Known(rooms), exit: ExitObservation.Found(2, Known(true)));

        var decision = Evaluate(state);

        Assert.Equal(StrategyAction.MoveToExit, decision.RecommendedAction);
        Assert.Equal<int>([5, 6, 7, 2], decision.Route);
    }

    [Fact]
    public void UnknownExitActivationStops()
    {
        var state = State(exit: ExitObservation.Found(0, ObservedValue<bool>.Unknown()));

        var decision = Evaluate(state);

        Assert.Equal(StrategyAction.Stop, decision.RecommendedAction);
        Assert.Contains("利用可否", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ChoosesShortestThenLowestIndexUnexploredRoom()
    {
        var map = Map((6, 1), (6, 5), (1, 2), (5, 10));
        var rooms = Rooms(map, explored: [6], unexplored: [1, 2, 5, 10]);
        var state = State(currentRoom: 6, rooms: Known(rooms));

        var decision = Evaluate(state);

        Assert.Equal(StrategyAction.ExploreRoom, decision.RecommendedAction);
        Assert.Equal(1, decision.TargetRoom);
        Assert.Equal<int>([1], decision.Route);
    }

    [Fact]
    public void UnreachableUnexploredRoomStops()
    {
        var state = State(rooms: Known(Rooms(Map(), explored: [0], unexplored: [2])));

        var decision = Evaluate(state);

        Assert.Equal(StrategyAction.Stop, decision.RecommendedAction);
        Assert.Contains("経路", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void FullyExploredFloorWithoutExitSearchesCurrentRoom()
    {
        var state = State();

        var decision = Evaluate(state);

        Assert.Equal(StrategyAction.SearchForExit, decision.RecommendedAction);
        Assert.Equal(0, decision.TargetRoom);
        Assert.Equal(StrategyRisk.Moderate, decision.Risk);
    }

    [Fact]
    public void UnknownRoomExplorationStopsInsteadOfAssumingExplored()
    {
        var rooms = Rooms(Map(), explored: [0]).ToBuilder();
        rooms[2] = rooms[2] with { Exploration = RoomExplorationState.Unknown };

        var decision = Evaluate(State(rooms: Known(rooms.ToImmutable())));

        Assert.Equal(StrategyAction.Stop, decision.RecommendedAction);
        Assert.Contains("探索状況", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownConnectionStopsInsteadOfAssumingNoPassage()
    {
        var rooms = Rooms(Map(), explored: [0]).ToBuilder();
        rooms[2] = rooms[2] with { Connections = ObservedValue<RoomConnectionFlags>.Unknown() };

        var decision = Evaluate(State(rooms: Known(rooms.ToImmutable())));

        Assert.Equal(StrategyAction.Stop, decision.RecommendedAction);
        Assert.Contains("Unknown", decision.BlockingReasons[0], StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownPomanderDetailStopsInsteadOfAssumingResources()
    {
        var pomander = new PomanderState("safety", ObservedValue<int>.Unknown(), Known(true));

        var decision = Evaluate(State(pomanders: KnownArray(pomander)));

        Assert.Equal(StrategyAction.Stop, decision.RecommendedAction);
        Assert.Contains(decision.BlockingReasons, reason => reason.Contains("所持数", StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownAggregateResourceReadinessStops()
    {
        var decision = Evaluate(State(resourceReadiness: Known(ResourceReadiness.Unknown)));

        Assert.Equal(StrategyAction.Stop, decision.RecommendedAction);
        Assert.Contains(decision.BlockingReasons, reason => reason.Contains("資源充足度", StringComparison.Ordinal));
    }

    [Fact]
    public void IdenticalInputsProduceEqualDecisions()
    {
        var map = Map((0, 1));
        var state = State(rooms: Known(Rooms(map, explored: [0], unexplored: [1])));
        var engine = new DeepDungeonStrategyEngine();

        var first = engine.Evaluate(state, Policy);
        var second = engine.Evaluate(state, Policy);

        Assert.Equal(first.SnapshotSequence, second.SnapshotSequence);
        Assert.Equal(first.RecommendedAction, second.RecommendedAction);
        Assert.Equal(first.Reason, second.Reason);
        Assert.Equal(first.Risk, second.Risk);
        Assert.Equal(first.TargetRoom, second.TargetRoom);
        Assert.Equal<int>(first.Route, second.Route);
        Assert.Equal<string>(first.Preconditions, second.Preconditions);
        Assert.Equal<string>(first.BlockingReasons, second.BlockingReasons);
    }

    [Fact]
    public void DuplicateRoomIndexIsRejected()
    {
        var rooms = Rooms(Map()).ToBuilder();
        rooms[1] = rooms[1] with { Index = 0 };

        Assert.Throws<InvalidDataException>(() => State(rooms: Known(rooms.ToImmutable())));
    }

    [Fact]
    public void DuplicateEnemyIdentityIsRejected()
    {
        var enemies = KnownArray(
            new StrategyEnemy("same", 0, EnemyDisposition.Hostile, ThreatLevel.Low),
            new StrategyEnemy("same", 1, EnemyDisposition.Defeated, ThreatLevel.Low));

        Assert.Throws<InvalidDataException>(() => State(enemies: enemies));
    }

    [Fact]
    public void NegativePolicyThresholdIsRejected()
    {
        var engine = new DeepDungeonStrategyEngine();

        Assert.Throws<InvalidDataException>(() =>
            engine.Evaluate(State(), new DeepDungeonStrategyPolicy(TimeSpan.FromSeconds(-1))));
    }

    private static StrategyDecision Evaluate(DeepDungeonStrategyState current, DeepDungeonStrategyState? previous = null) =>
        new DeepDungeonStrategyEngine().Evaluate(current, Policy, previous);

    private static DeepDungeonStrategyState State(
        Guid? sessionId = null,
        long sequence = 10,
        DateTimeOffset? capturedAt = null,
        int currentRoom = 0,
        ObservedValue<ImmutableArray<StrategyRoom>>? rooms = null,
        ExitObservation? exit = null,
        ObservedValue<ImmutableArray<StrategyEnemy>>? enemies = null,
        ObservedValue<ImmutableArray<StrategyTrap>>? traps = null,
        ObservedValue<ImmutableArray<PomanderState>>? pomanders = null,
        ObservedValue<ResourceReadiness>? resourceReadiness = null,
        ObservedValue<TimeSpan>? remainingTime = null) =>
        new(
            sessionId ?? Session,
            sequence,
            capturedAt ?? At,
            Known(DeepDungeonKind.EurekaOrthos),
            Known(10),
            Known(currentRoom),
            rooms ?? Known(Rooms(Map(), explored: Enumerable.Range(0, RoomPathfinder.RoomCount).ToArray())),
            exit ?? ExitObservation.NotFound(),
            enemies ?? Known(ImmutableArray<StrategyEnemy>.Empty),
            traps ?? Known(ImmutableArray<StrategyTrap>.Empty),
            pomanders ?? Known(ImmutableArray<PomanderState>.Empty),
            resourceReadiness ?? Known(ResourceReadiness.Sufficient),
            remainingTime ?? Known(TimeSpan.FromMinutes(10)));

    private static ObservedValue<T> Known<T>(T value) => ObservedValue<T>.Known(value);

    private static ObservedValue<ImmutableArray<T>> KnownArray<T>(params T[] values) =>
        Known(values.ToImmutableArray());

    private static ImmutableArray<StrategyRoom> Rooms(
        RoomConnectionFlags[] map,
        int[]? explored = null,
        int[]? unexplored = null,
        int[]? dangerous = null,
        int[]? unknownDanger = null)
    {
        explored ??= [];
        unexplored ??= [];
        dangerous ??= [];
        unknownDanger ??= [];
        return Enumerable.Range(0, RoomPathfinder.RoomCount)
            .Select(index => new StrategyRoom(
                index,
                Known(map[index]),
                explored.Contains(index)
                    ? RoomExplorationState.Explored
                    : unexplored.Contains(index)
                        ? RoomExplorationState.Unexplored
                        : RoomExplorationState.Explored,
                unknownDanger.Contains(index)
                    ? RoomDangerState.Unknown
                    : dangerous.Contains(index)
                        ? RoomDangerState.Dangerous
                        : RoomDangerState.Safe))
            .ToImmutableArray();
    }

    private static RoomConnectionFlags[] Map(params (int First, int Second)[] connections)
    {
        var map = new RoomConnectionFlags[RoomPathfinder.RoomCount];
        foreach (var (first, second) in connections)
        {
            var delta = second - first;
            var (forward, reverse) = delta switch
            {
                -RoomPathfinder.GridWidth => (RoomConnectionFlags.North, RoomConnectionFlags.South),
                RoomPathfinder.GridWidth => (RoomConnectionFlags.South, RoomConnectionFlags.North),
                -1 when first / RoomPathfinder.GridWidth == second / RoomPathfinder.GridWidth =>
                    (RoomConnectionFlags.West, RoomConnectionFlags.East),
                1 when first / RoomPathfinder.GridWidth == second / RoomPathfinder.GridWidth =>
                    (RoomConnectionFlags.East, RoomConnectionFlags.West),
                _ => throw new ArgumentException("Rooms must be cardinally adjacent."),
            };
            map[first] |= forward;
            map[second] |= reverse;
        }

        return map;
    }
}
