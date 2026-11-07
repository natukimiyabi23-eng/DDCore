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
        Assert.Equal(0x3D54u, GancanaghAnalyzer.BossObjectId);
        Assert.Equal(0x3D55u, GancanaghAnalyzer.PachypodiumMineObjectId);
        Assert.Equal(6499u, GancanaghAnalyzer.AttackActionId);
        Assert.Equal(31477u, GancanaghAnalyzer.AuthoritativeShriekActionId);
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
        Assert.Equal(hazard.ActivatesAt, hazard.ExpiresAt);
        Assert.Equal(HazardLifetimeKind.Instantaneous, hazard.Lifetime);
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
    public void ActorDisappearanceBeforeActivationRetainsHazardAndIsUnknown()
    {
        var analyzer = new GancanaghAnalyzer();
        var actor = Actor(1, GancanaghAnalyzer.PachypodiumMineObjectId, new(-295, -300), GancanaghAnalyzer.MandrashockSingleActionId, Start, Start.AddSeconds(5));
        var first = analyzer.Analyze(Snapshot(1, Start, actor), MechanicState.Empty);
        var second = analyzer.Analyze(Snapshot(2, Start.AddSeconds(1)), first.State);

        Assert.Single(first.Frame.Hazards);
        Assert.Single(second.Frame.Hazards);
        Assert.Equal(MechanicSafetyAssessment.UnknownOrUnsupported, second.Frame.Safety);
        Assert.Contains(second.Frame.Diagnostics, value => value.StartsWith("cast-hazard.source-missing", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingActorHazardExpiresAtKnownInstant()
    {
        var analyzer = new GancanaghAnalyzer();
        var actor = Actor(1, GancanaghAnalyzer.PachypodiumMineObjectId, new(-295, -300), GancanaghAnalyzer.MandrashockSingleActionId, Start, Start.AddSeconds(5));
        var first = analyzer.Analyze(Snapshot(1, Start, actor), MechanicState.Empty);
        var second = analyzer.Analyze(Snapshot(2, Start.AddSeconds(5)), first.State);

        Assert.Empty(second.Frame.Hazards);
        Assert.Equal(MechanicSafetyAssessment.UnknownOrUnsupported, second.Frame.Safety);
    }

    [Fact]
    public void ExplicitDestroyedActorRemovesHazard()
    {
        var analyzer = new GancanaghAnalyzer();
        var actor = Actor(1, GancanaghAnalyzer.PachypodiumMineObjectId, new(-295, -300), GancanaghAnalyzer.MandrashockSingleActionId, Start, Start.AddSeconds(5));
        var first = analyzer.Analyze(Snapshot(1, Start, actor), MechanicState.Empty);
        var destroyed = actor with { IsDestroyed = true, Cast = null };
        var second = analyzer.Analyze(Snapshot(2, Start.AddSeconds(1), destroyed), first.State);

        Assert.Empty(second.Frame.Hazards);
        Assert.Equal(MechanicSafetyAssessment.HazardsKnown, second.Frame.Safety);
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
    public void AuthoritativeShriekIsKnownNonHazardOnlyForBoss()
    {
        var boss = Actor(1, GancanaghAnalyzer.BossObjectId, new(-300, -300), GancanaghAnalyzer.AuthoritativeShriekActionId, Start, Start.AddSeconds(3));
        var mine = Actor(2, GancanaghAnalyzer.PachypodiumMineObjectId, new(-295, -300), GancanaghAnalyzer.AuthoritativeShriekActionId, Start, Start.AddSeconds(3));

        var bossFrame = Analyze(Snapshot(1, Start, boss)).Frame;
        var mineFrame = Analyze(Snapshot(1, Start, mine)).Frame;

        Assert.Empty(bossFrame.Unsupported);
        Assert.Equal(MechanicSafetyAssessment.HazardsKnown, bossFrame.Safety);
        Assert.Equal(GancanaghAnalyzer.AuthoritativeShriekActionId, Assert.Single(mineFrame.Unsupported).ActionId);
        Assert.Equal(MechanicSafetyAssessment.UnknownOrUnsupported, mineFrame.Safety);
    }

    [Fact]
    public void AutoAttackReportedAsCastIsUnsupported()
    {
        var boss = Actor(1, GancanaghAnalyzer.BossObjectId, new(-300, -300), GancanaghAnalyzer.AttackActionId, Start, Start.AddSeconds(1));
        var frame = Analyze(Snapshot(1, Start, boss)).Frame;

        Assert.Equal(GancanaghAnalyzer.AttackActionId, Assert.Single(frame.Unsupported).ActionId);
        Assert.Equal(MechanicSafetyAssessment.UnknownOrUnsupported, frame.Safety);
    }

    [Fact]
    public void CastLocationAndRotationComeFromCastObservation()
    {
        var actor = Actor(1, GancanaghAnalyzer.PachypodiumMineObjectId, new(-295, -300), GancanaghAnalyzer.MandrashockSingleActionId, Start, Start.AddSeconds(5));
        actor = actor with { Cast = actor.Cast! with { TargetLocation = new Coordinate2D(-290, -299), Rotation = 1.25 } };

        var hazard = Assert.Single(Analyze(Snapshot(1, Start, actor)).Frame.Hazards);
        Assert.Equal(new Coordinate2D(-290, -299), hazard.Origin);
        Assert.Equal(1.25, hazard.Rotation);
    }

    [Fact]
    public void TitanLimitPrefersEarlierCastStartsBeforeEntityId()
    {
        var actors = Enumerable.Range(1, 7)
            .Select(index => Actor(
                (ulong)index,
                GancanaghAnalyzer.PachypodiumMineObjectId,
                new(-300 + index, -300),
                GancanaghAnalyzer.MandrashockTitanActionId,
                index == 7 ? Start : Start.AddSeconds(1),
                Start.AddSeconds(8)))
            .ToArray();

        var frame = Analyze(Snapshot(1, Start, actors)).Frame;
        Assert.Contains(frame.Hazards, hazard => hazard.Evidence.Source.EntityId == 7);
        Assert.DoesNotContain(frame.Hazards, hazard => hazard.Evidence.Source.EntityId == 6);
    }

    [Fact]
    public void ConsecutiveCastOnSameActorReplacesOldPrediction()
    {
        var analyzer = new GancanaghAnalyzer();
        var firstActor = Actor(1, GancanaghAnalyzer.PachypodiumMineObjectId, new(-295, -300), GancanaghAnalyzer.MandrashockSingleActionId, Start, Start.AddSeconds(5));
        var first = analyzer.Analyze(Snapshot(1, Start, firstActor), MechanicState.Empty);
        var secondActor = Actor(1, GancanaghAnalyzer.PachypodiumMineObjectId, new(-295, -300), GancanaghAnalyzer.MandrashockSingleActionId, Start.AddSeconds(2), Start.AddSeconds(7));
        var second = analyzer.Analyze(Snapshot(2, Start.AddSeconds(2), secondActor), first.State);

        var hazard = Assert.Single(second.Frame.Hazards);
        Assert.NotEqual(Assert.Single(first.Frame.Hazards).HazardId, hazard.HazardId);
    }

    [Fact]
    public void ReusedEntityIdWithNewGenerationDoesNotAliasOldCast()
    {
        var analyzer = new GancanaghAnalyzer();
        var original = Actor(1, GancanaghAnalyzer.PachypodiumMineObjectId, new(-295, -300), GancanaghAnalyzer.MandrashockSingleActionId, Start, Start.AddSeconds(5));
        var first = analyzer.Analyze(Snapshot(1, Start, original), MechanicState.Empty);
        var replacement = Actor(1, GancanaghAnalyzer.PachypodiumMineObjectId, new(-294, -300), GancanaghAnalyzer.MandrashockSingleActionId, Start.AddSeconds(1), Start.AddSeconds(6), generation: 1);
        var second = analyzer.Analyze(Snapshot(2, Start.AddSeconds(1), replacement), first.State);

        Assert.Equal(2, second.Frame.Hazards.Length);
        Assert.Equal(MechanicSafetyAssessment.UnknownOrUnsupported, second.Frame.Safety);
    }

    [Fact]
    public void MissingEncounterActorsAreUnknownNotSafe()
    {
        var frame = Analyze(Snapshot(1, Start)).Frame;
        Assert.Equal(MechanicSafetyAssessment.UnknownOrUnsupported, frame.Safety);
        Assert.Contains("gancanagh.encounter-actors.missing", frame.Diagnostics);
    }

    [Fact]
    public void DirectAnalyzerRejectsTimeRegression()
    {
        var analyzer = new GancanaghAnalyzer();
        var actor = Actor(1, GancanaghAnalyzer.BossObjectId, new(-300, -300), GancanaghAnalyzer.MandrastormActionId, Start, Start.AddSeconds(5));
        var first = analyzer.Analyze(Snapshot(1, Start.AddSeconds(1), actor), MechanicState.Empty);
        var second = analyzer.Analyze(Snapshot(2, Start, actor), first.State);

        Assert.Equal(MechanicSafetyAssessment.InvalidObservation, second.Frame.Safety);
        Assert.Contains("gancanagh.time.regressed", second.Frame.Diagnostics);
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

    [Fact]
    public void ReplayGapRetainsPredictionAndNeverReportsGapAsSafe()
    {
        var mine = Actor(1, GancanaghAnalyzer.PachypodiumMineObjectId, new(-290, -300), GancanaghAnalyzer.MandrashockSingleActionId, Start, Start.AddSeconds(5));
        var document = ReplayDocument.Create(
            Start,
            Session,
            "0.2.0",
            [
                Snapshot(1, Start, mine),
                Snapshot(2, Start.AddSeconds(1)),
                Snapshot(3, Start.AddSeconds(2), mine),
            ],
            ReplayUserIntent.ExplicitCapture);
        var codec = new ReplayCodec();
        var runner = new ReplayAnalyzerRunner(codec, new AnalyzerRegistry([new GancanaghAnalyzer()]));

        var first = runner.Run(document);
        var second = runner.Run(document);

        Assert.Equal(new[] { 1, 1, 1 }, first.Select(frame => frame.Hazards.Length).ToArray());
        Assert.Equal(
            new[] { MechanicSafetyAssessment.HazardsKnown, MechanicSafetyAssessment.UnknownOrUnsupported, MechanicSafetyAssessment.HazardsKnown },
            first.Select(frame => frame.Safety).ToArray());
        Assert.Equal(first.Select(FrameSignature), second.Select(FrameSignature));
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
        DateTimeOffset finishesAt,
        int generation = 0) =>
        new(
            new ActorIdentity(entityId, generation),
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
            new CastSnapshot(actionId, CastLifecycle.Casting, startsAt, finishesAt, 0, null, null),
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
