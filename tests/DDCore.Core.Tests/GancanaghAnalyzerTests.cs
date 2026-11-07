using System.Collections.Immutable;
using DDCore.Core.Geometry;
using DDCore.Core.Mechanics;
using DDCore.Core.Mechanics.Analyzers;
using DDCore.Core.Replay;
using DDCore.Core.World;

namespace DDCore.Core.Tests;

public sealed class GancanaghAnalyzerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 7, 2, 0, 0, TimeSpan.Zero);
    private static readonly Guid Session = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void UpstreamActionAndArenaConstantsArePinned()
    {
        Assert.Equal(31478u, GancanaghAnalyzer.MandrashockSingleActionId);
        Assert.Equal(32700u, GancanaghAnalyzer.MandrashockTitanActionId);
        Assert.Equal(31479u, GancanaghAnalyzer.MandrastormActionId);
        Assert.Equal(19.5, GancanaghAnalyzer.ArenaHalfSize);
    }

    [Fact]
    public void SingleMandrashockProducesCircleAtMine()
    {
        var actor = Actor(1, GancanaghAnalyzer.PachypodiumMineObjectId, new(-295, -300), GancanaghAnalyzer.MandrashockSingleActionId, Start, Start.AddSeconds(5));
        var result = Analyze(Snapshot(1, Start, actor));

        var hazard = Assert.Single(result.Frame.Hazards);
        var circle = Assert.IsType<CircleAoe>(hazard.Shape);
        Assert.Equal(10, circle.Radius);
        Assert.Equal(new Coordinate2D(-295, -300), hazard.Origin);
        Assert.Equal(Start.AddSeconds(5), hazard.ActivatesAt);
        Assert.Equal(MechanicSafetyAssessment.HazardsKnown, result.Frame.Safety);
    }

    [Fact]
    public void MandrastormUsesBossCenteredRadius()
    {
        var actor = Actor(1, GancanaghAnalyzer.BossObjectId, new(-300, -300), GancanaghAnalyzer.MandrastormActionId, Start, Start.AddSeconds(5));
        var hazard = Assert.Single(Analyze(Snapshot(1, Start, actor)).Frame.Hazards);
        Assert.Equal(18.5, Assert.IsType<CircleAoe>(hazard.Shape).Radius);
    }

    [Fact]
    public void TitanPatternIsDeterministicallyLimitedToSixInstances()
    {
        var actors = Enumerable.Range(1, 7)
            .Select(index => Actor((ulong)index, GancanaghAnalyzer.PachypodiumMineObjectId, new(-300 + index, -300), GancanaghAnalyzer.MandrashockTitanActionId, Start, Start.AddSeconds(8)))
            .ToArray();
        var frame = Analyze(Snapshot(1, Start, actors)).Frame;

        Assert.Equal(6, frame.Hazards.Length);
        Assert.DoesNotContain(frame.Hazards, hazard => hazard.Evidence.Source.EntityId == 7);
    }

    [Fact]
    public void MultipleAoeKindsRemainIndependent()
    {
        var mine = Actor(1, GancanaghAnalyzer.PachypodiumMineObjectId, new(-290, -300), GancanaghAnalyzer.MandrashockSingleActionId, Start, Start.AddSeconds(5));
        var boss = Actor(2, GancanaghAnalyzer.BossObjectId, new(-300, -300), GancanaghAnalyzer.MandrastormActionId, Start, Start.AddSeconds(5));
        var frame = Analyze(Snapshot(1, Start, mine, boss)).Frame;

        Assert.Equal(2, frame.Hazards.Length);
        Assert.Equal(new[] { 31478u, 31479u }, frame.Hazards.Select(hazard => hazard.Evidence.ActionId).Order().ToArray());
    }

    [Theory]
    [InlineData(CastLifecycle.Interrupted)]
    [InlineData(CastLifecycle.Completed)]
    public void InterruptedOrCompletedCastRemovesHazard(CastLifecycle lifecycle)
    {
        var analyzer = new GancanaghAnalyzer();
        var casting = Actor(1, GancanaghAnalyzer.PachypodiumMineObjectId, new(-295, -300), GancanaghAnalyzer.MandrashockSingleActionId, Start, Start.AddSeconds(5));
        var first = analyzer.Analyze(Snapshot(1, Start, casting), MechanicState.Empty);
        var ended = casting with { Cast = casting.Cast! with { Lifecycle = lifecycle } };
        var second = analyzer.Analyze(Snapshot(2, Start.AddSeconds(1), ended), first.State);

        Assert.Single(first.Frame.Hazards);
        Assert.Empty(second.Frame.Hazards);
        Assert.Empty(second.State.ActiveHazards);
    }

    [Fact]
    public void ActorDisappearanceRemovesHazard()
    {
        var analyzer = new GancanaghAnalyzer();
        var actor = Actor(1, GancanaghAnalyzer.PachypodiumMineObjectId, new(-295, -300), GancanaghAnalyzer.MandrashockSingleActionId, Start, Start.AddSeconds(5));
        var first = analyzer.Analyze(Snapshot(1, Start, actor), MechanicState.Empty);
        var second = analyzer.Analyze(Snapshot(2, Start.AddSeconds(1)), first.State);

        Assert.Single(first.Frame.Hazards);
        Assert.Empty(second.Frame.Hazards);
    }

    [Fact]
    public void ElapsedCastIsRemovedEvenIfAdapterStillMarksCasting()
    {
        var actor = Actor(1, GancanaghAnalyzer.PachypodiumMineObjectId, new(-295, -300), GancanaghAnalyzer.MandrashockSingleActionId, Start, Start.AddSeconds(5));
        var frame = Analyze(Snapshot(1, Start.AddSeconds(6), actor)).Frame;
        Assert.Empty(frame.Hazards);
    }

    [Fact]
    public void UnknownEncounterActionIsUnsupportedNotSafe()
    {
        var actor = Actor(1, GancanaghAnalyzer.BossObjectId, new(-300, -300), 999999, Start, Start.AddSeconds(5));
        var frame = Analyze(Snapshot(1, Start, actor)).Frame;

        Assert.Empty(frame.Hazards);
        Assert.Equal(999999u, Assert.Single(frame.Unsupported).ActionId);
        Assert.Equal(MechanicSafetyAssessment.UnknownOrUnsupported, frame.Safety);
    }

    [Fact]
    public void SyntheticReplayMatchesGoldenFramesAndIsDeterministic()
    {
        var mine = Actor(1, GancanaghAnalyzer.PachypodiumMineObjectId, new(-290, -300), GancanaghAnalyzer.MandrashockSingleActionId, Start, Start.AddSeconds(5));
        var boss = Actor(2, GancanaghAnalyzer.BossObjectId, new(-300, -300), GancanaghAnalyzer.MandrastormActionId, Start, Start.AddSeconds(5));
        var completedMine = mine with { Cast = mine.Cast! with { Lifecycle = CastLifecycle.Completed } };
        var completedBoss = boss with { Cast = boss.Cast! with { Lifecycle = CastLifecycle.Completed } };
        var document = ReplayDocument.Create(
            Start,
            Session,
            "0.2.0",
            [
                Snapshot(1, Start, mine, boss),
                Snapshot(2, Start.AddSeconds(5), completedMine, completedBoss),
                Snapshot(3, Start.AddSeconds(6), Actor(3, GancanaghAnalyzer.BossObjectId, new(-300, -300), 999999, Start.AddSeconds(6), Start.AddSeconds(7))),
            ],
            ReplayUserIntent.ExplicitCapture);
        var codec = new ReplayCodec();
        var json = codec.Serialize(document, ReplayUserIntent.ExplicitCaptureAndExport);
        var restored = codec.Deserialize(json);
        var runner = new ReplayAnalyzerRunner(codec, new AnalyzerRegistry([new GancanaghAnalyzer()]));

        var first = runner.Run(restored);
        var second = runner.Run(restored);

        Assert.Equal(new[] { 2, 0, 0 }, first.Select(frame => frame.Hazards.Length).ToArray());
        Assert.Equal(
            new[] { MechanicSafetyAssessment.HazardsKnown, MechanicSafetyAssessment.HazardsKnown, MechanicSafetyAssessment.UnknownOrUnsupported },
            first.Select(frame => frame.Safety).ToArray());
        Assert.Equal(
            first.Select(FrameSignature).ToArray(),
            second.Select(FrameSignature).ToArray());
    }

    private static AnalysisResult Analyze(WorldSnapshot snapshot) => new GancanaghAnalyzer().Analyze(snapshot, MechanicState.Empty);

    private static string FrameSignature(MechanicFrame frame) =>
        $"{frame.SnapshotSequence}:{frame.Safety}:{string.Join(',', frame.Hazards.Select(hazard => hazard.HazardId))}:{string.Join(',', frame.Unsupported.Select(item => item.ActionId))}";

    private static WorldSnapshot Snapshot(long sequence, DateTimeOffset at, params ActorSnapshot[] actors) =>
        new MockWorldSnapshotBuilder()
            .InSession(Session)
            .WithSequence(sequence)
            .At(at)
            .InDungeon(new DeepDungeonContext(DeepDungeonKind.EurekaOrthos, 10, 0, 897, "eureka-orthos-10", null))
            .AddActors(actors)
            .Build();

    private static ActorSnapshot Actor(
        ulong entityId,
        uint objectId,
        Coordinate2D position,
        uint actionId,
        DateTimeOffset startsAt,
        DateTimeOffset finishesAt) =>
        new(
            new ActorIdentity(entityId, 0),
            objectId,
            ActorKind.Enemy,
            position,
            0,
            objectId == GancanaghAnalyzer.BossObjectId ? 1.8 : 1.5,
            100,
            100,
            false,
            false,
            true,
            null,
            new CastSnapshot(actionId, CastLifecycle.Casting, startsAt, finishesAt, null, null),
            ImmutableArray<StatusSnapshot>.Empty);
}

internal static class MockWorldSnapshotBuilderExtensions
{
    public static MockWorldSnapshotBuilder AddActors(this MockWorldSnapshotBuilder builder, IEnumerable<ActorSnapshot> actors)
    {
        foreach (var actor in actors)
        {
            builder.AddActor(actor);
        }

        return builder;
    }
}
