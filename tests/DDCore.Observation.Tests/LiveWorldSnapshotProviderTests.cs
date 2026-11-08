using System.Collections.Immutable;
using DDCore.Core.Mechanics.Analyzers;
using DDCore.Core.Model;
using DDCore.Core.World;
using DDCore.Observation;

namespace DDCore.Observation.Tests;

public sealed class LiveWorldSnapshotProviderTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConvertsPublicObservationAndMarksEstimatesPartial()
    {
        var target = Enemy(2, 0x9999, gameObjectId: 200, position: new(4, 0, 8));
        var boss = Enemy(
            1,
            GancanaghAnalyzer.BossObjectId,
            gameObjectId: 100,
            position: new(-300, 3, -300),
            rotation: 0,
            cast: new RawCastObservation(GancanaghAnalyzer.MandrastormActionId, 1, 4, 200),
            statuses: [new RawStatusObservation(123, 2, 2, 5)]);
        var provider = Provider(Frame(At, [Player(), boss, target]));

        var snapshot = provider.Capture();

        Assert.Equal(ObservationQuality.Partial, snapshot.Quality);
        Assert.Equal(DeepDungeonKind.EurekaOrthos, snapshot.Dungeon.Kind);
        Assert.Null(snapshot.Dungeon.Floor);
        Assert.Equal(new DeepDungeonFloorRange(1, 10), snapshot.Dungeon.FloorRange);
        Assert.Equal((uint?)1099, snapshot.Dungeon.TerritoryId);
        Assert.True(snapshot.Dungeon.IsDeepDungeonObserved);
        var actor = Assert.Single(snapshot.Actors, item => item.ObjectId == GancanaghAnalyzer.BossObjectId);
        Assert.Equal(-300, actor.Position.X);
        Assert.Equal(-300, actor.Position.Y);
        Assert.Equal(Math.PI / 2, actor.Rotation, 10);
        Assert.NotNull(actor.Cast);
        Assert.Equal(At - TimeSpan.FromSeconds(1), actor.Cast!.StartedAt);
        Assert.Equal(At + TimeSpan.FromSeconds(3), actor.Cast.FinishesAt);
        Assert.Equal(new DDCore.Core.Geometry.Coordinate2D(4, 8), actor.Cast.TargetLocation);
        Assert.Equal((ulong)2, actor.Cast.Target?.EntityId);
        var status = Assert.Single(actor.Statuses);
        Assert.Equal(At + TimeSpan.FromSeconds(5), status.ExpiresAt);
        Assert.Contains("observation.dungeon.public-identified:EurekaOrthos:1-10", snapshot.Diagnostics);
        Assert.Contains("observation.dungeon.exact-floor-unavailable-public-api", snapshot.Diagnostics);
        Assert.Equal(new DDCore.Core.Geometry.Coordinate2D(0, 0), snapshot.PlayerPose!.Position);
        Assert.Equal(ObservationValueQuality.Direct, snapshot.PlayerPose.Metadata.Quality);
        Assert.Equal(ObservationValueSource.PublicDalamud, snapshot.PlayerPose.Metadata.Source);
    }

    [Fact]
    public void EmitsInterruptedLifecycleWhenCastStopsEarly()
    {
        var source = new QueueSource(
            Frame(At, [Player(), Enemy(1, GancanaghAnalyzer.BossObjectId, cast: Cast(1, 5))]),
            Frame(At.AddSeconds(1), [Player(), Enemy(1, GancanaghAnalyzer.BossObjectId)]));
        var provider = new LiveWorldSnapshotProvider(source);

        provider.Capture();
        var stopped = provider.Capture();

        Assert.Equal(CastLifecycle.Interrupted, stopped.Actors.Single(actor => actor.ObjectId == GancanaghAnalyzer.BossObjectId).Cast?.Lifecycle);
    }

    [Fact]
    public void EmitsCompletedLifecycleAtPredictedFinish()
    {
        var source = new QueueSource(
            Frame(At, [Player(), Enemy(1, GancanaghAnalyzer.BossObjectId, cast: Cast(1, 2))]),
            Frame(At.AddSeconds(1), [Player(), Enemy(1, GancanaghAnalyzer.BossObjectId)]));
        var provider = new LiveWorldSnapshotProvider(source);

        provider.Capture();
        var stopped = provider.Capture();

        Assert.Equal(CastLifecycle.Completed, stopped.Actors.Single(actor => actor.ObjectId == GancanaghAnalyzer.BossObjectId).Cast?.Lifecycle);
    }

    [Fact]
    public void IncrementsGenerationAfterActorDisappearsAndEntityIdIsReused()
    {
        var source = new QueueSource(
            Frame(At, [Player(), Enemy(1, 0x1111, gameObjectId: 100)]),
            Frame(At.AddSeconds(1), [Player()]),
            Frame(At.AddSeconds(2), [Player(), Enemy(1, 0x2222, gameObjectId: 300)]));
        var provider = new LiveWorldSnapshotProvider(source);

        var first = provider.Capture();
        provider.Capture();
        var reused = provider.Capture();

        Assert.Equal(0, first.Actors.Single(actor => actor.Identity.EntityId == 1).Identity.Generation);
        Assert.Equal(1, reused.Actors.Single(actor => actor.Identity.EntityId == 1).Identity.Generation);
    }

    [Fact]
    public void ResetsSessionAndSequenceOnTerritoryChange()
    {
        var source = new QueueSource(
            Frame(At, [Player()], territoryId: 777),
            Frame(At.AddSeconds(1), [Player()], territoryId: 778));
        var provider = new LiveWorldSnapshotProvider(source);

        var first = provider.Capture();
        var changed = provider.Capture();

        Assert.NotEqual(first.SessionId, changed.SessionId);
        Assert.Equal(0, first.Sequence);
        Assert.Equal(0, changed.Sequence);
    }

    [Fact]
    public void MissingPlayerIsPartialAndDoesNotUseZeroPlaceholder()
    {
        var provider = Provider(Frame(
            At,
            [Enemy(1, 0x1111)],
            localPlayerEntityId: 99,
            playerPose: RawPlayerPoseObservation.Missing("dalamud.player.not-created")));

        var snapshot = provider.Capture();

        Assert.Equal(ObservationQuality.Partial, snapshot.Quality);
        Assert.DoesNotContain(snapshot.Actors, actor => actor.Identity.EntityId == 0);
        Assert.Contains("observation.player.unavailable", snapshot.Diagnostics);
        Assert.Null(snapshot.PlayerPose!.Position);
        Assert.Null(snapshot.PlayerPose.Rotation);
        Assert.Equal(ObservationValueQuality.Missing, snapshot.PlayerPose.Metadata.Quality);
        Assert.Equal("dalamud.player.not-created", snapshot.PlayerPose.Metadata.MissingReason);
    }

    [Fact]
    public void PublicApiReadFailurePreservesMissingReason()
    {
        var provider = Provider(Frame(
            At,
            [Player()],
            playerPose: RawPlayerPoseObservation.Missing("dalamud.player.read-failed:InvalidOperationException")));

        var snapshot = provider.Capture();

        Assert.Equal(ObservationQuality.Partial, snapshot.Quality);
        Assert.Null(snapshot.PlayerPose!.Position);
        Assert.Equal(ObservationValueSource.PublicDalamud, snapshot.PlayerPose.Metadata.Source);
        Assert.Equal(ObservationValueQuality.Missing, snapshot.PlayerPose.Metadata.Quality);
        Assert.Equal("dalamud.player.read-failed:InvalidOperationException", snapshot.PlayerPose.Metadata.MissingReason);
    }

    [Fact]
    public void NonFinitePublicPoseIsInvalidWithoutUsingOriginFallback()
    {
        var provider = Provider(Frame(
            At,
            [Player()],
            playerPose: RawPlayerPoseObservation.Direct(new RawPosition(double.NaN, 0, 0), 0)));

        var snapshot = provider.Capture();

        Assert.Null(snapshot.PlayerPose!.Position);
        Assert.Equal(ObservationValueQuality.Invalid, snapshot.PlayerPose.Metadata.Quality);
        Assert.Equal("observation.player.pose-invalid", snapshot.PlayerPose.Metadata.MissingReason);
    }

    [Fact]
    public void LoggedOutFrameHasUnavailableIdentifiers()
    {
        var frame = new RawObservationFrame(At, false, null, null, false, null, [], []);

        var snapshot = Provider(frame).Capture();

        Assert.Equal(ObservationQuality.Unsupported, snapshot.Quality);
        Assert.Null(snapshot.Dungeon.TerritoryId);
        Assert.Null(snapshot.Dungeon.ContentId);
        Assert.Empty(snapshot.Actors);
        Assert.Null(snapshot.PlayerPose!.Position);
        Assert.Equal(ObservationValueQuality.Missing, snapshot.PlayerPose.Metadata.Quality);
        Assert.Equal("observation.player.pose-not-observed", snapshot.PlayerPose.Metadata.MissingReason);
    }

    [Fact]
    public void RegressedCaptureTimeIsMarkedStale()
    {
        var source = new QueueSource(
            Frame(At.AddSeconds(1), [Player()]),
            Frame(At, [Player()]));
        var provider = new LiveWorldSnapshotProvider(source);

        provider.Capture();
        var stale = provider.Capture();

        Assert.Equal(ObservationQuality.Stale, stale.Quality);
        Assert.Contains("observation.time.regressed", stale.Diagnostics);
        Assert.Empty(stale.Actors);
        Assert.Equal(ObservationValueQuality.Stale, stale.PlayerPose!.Metadata.Quality);
        Assert.Equal(new DDCore.Core.Geometry.Coordinate2D(0, 0), stale.PlayerPose.Position);
    }

    [Fact]
    public void StableCastKeepsDerivedStartAndFinishAcrossFrames()
    {
        var source = new QueueSource(
            Frame(At, [Player(), Enemy(1, GancanaghAnalyzer.BossObjectId, cast: Cast(1, 5))]),
            Frame(At.AddSeconds(1), [Player(), Enemy(1, GancanaghAnalyzer.BossObjectId, cast: Cast(2.02, 5))]));
        var provider = new LiveWorldSnapshotProvider(source);

        var first = provider.Capture().Actors.Single(actor => actor.ObjectId == GancanaghAnalyzer.BossObjectId).Cast!;
        var second = provider.Capture().Actors.Single(actor => actor.ObjectId == GancanaghAnalyzer.BossObjectId).Cast!;

        Assert.Equal(first.StartedAt, second.StartedAt);
        Assert.Equal(first.FinishesAt, second.FinishesAt);
    }

    [Fact]
    public void DecreasedCastProgressStartsNewSameActionCast()
    {
        var source = new QueueSource(
            Frame(At, [Player(), Enemy(1, GancanaghAnalyzer.BossObjectId, cast: Cast(4, 5))]),
            Frame(At.AddSeconds(1), [Player(), Enemy(1, GancanaghAnalyzer.BossObjectId, cast: Cast(0.2, 5))]));
        var provider = new LiveWorldSnapshotProvider(source);

        var first = provider.Capture().Actors.Single(actor => actor.ObjectId == GancanaghAnalyzer.BossObjectId).Cast!;
        var second = provider.Capture().Actors.Single(actor => actor.ObjectId == GancanaghAnalyzer.BossObjectId).Cast!;

        Assert.NotEqual(first.StartedAt, second.StartedAt);
        Assert.Equal(At.AddSeconds(0.8), second.StartedAt);
    }

    [Fact]
    public void OrdinaryStartedDutyIsNotTreatedAsDeepDungeon()
    {
        var snapshot = Provider(Frame(At, [Player()], dutyStarted: true, isInDeepDungeon: false)).Capture();

        Assert.Equal(DeepDungeonKind.None, snapshot.Dungeon.Kind);
        Assert.Null(snapshot.Dungeon.Floor);
        Assert.Null(snapshot.Dungeon.FloorRange);
        Assert.False(snapshot.IsDeepDungeon);
    }

    [Fact]
    public void UnknownDeepDungeonContentFailsClosed()
    {
        var snapshot = Provider(Frame(
            At,
            [Player()],
            territoryId: 9999,
            contentId: 9999,
            isInDeepDungeon: true,
            contentFinderCondition: new RawContentFinderConditionObservation(9999, 9999, 9999, "unknown"))).Capture();

        Assert.Equal(DeepDungeonKind.Unsupported, snapshot.Dungeon.Kind);
        Assert.Null(snapshot.Dungeon.Floor);
        Assert.Null(snapshot.Dungeon.FloorRange);
        Assert.True(snapshot.IsDeepDungeon);
        Assert.Contains("observation.dungeon.content-finder-condition-unknown", snapshot.Diagnostics);
        Assert.Contains("observation.dungeon.unidentified", snapshot.Diagnostics);
    }

    [Fact]
    public void TerritoryMismatchFailsClosedWithoutKeepingBand()
    {
        var snapshot = Provider(Frame(At, [Player()], territoryId: 1100)).Capture();

        Assert.Equal(DeepDungeonKind.Unsupported, snapshot.Dungeon.Kind);
        Assert.Null(snapshot.Dungeon.FloorRange);
        Assert.Contains("observation.dungeon.territory-mismatch", snapshot.Diagnostics);
    }

    [Fact]
    public void DeepDungeonConditionWithoutContentRowFailsClosed()
    {
        var snapshot = Provider(Frame(
            At,
            [Player()],
            contentId: null,
            isInDeepDungeon: true,
            contentFinderCondition: null)).Capture();

        Assert.Equal(DeepDungeonKind.Unsupported, snapshot.Dungeon.Kind);
        Assert.Null(snapshot.Dungeon.FloorRange);
        Assert.Contains("observation.dungeon.content-finder-condition-missing", snapshot.Diagnostics);
    }

    [Fact]
    public void DutyStartFlagDoesNotEraseAvailablePublicDeepDungeonIdentity()
    {
        var snapshot = Provider(Frame(At, [Player()], dutyStarted: false, isInDeepDungeon: true)).Capture();

        Assert.Equal(DeepDungeonKind.EurekaOrthos, snapshot.Dungeon.Kind);
        Assert.Equal(new DeepDungeonFloorRange(1, 10), snapshot.Dungeon.FloorRange);
        Assert.Null(snapshot.Dungeon.Floor);
    }

    [Fact]
    public void DeepDungeonConditionChangeStartsNewSession()
    {
        var source = new QueueSource(
            Frame(At, [Player()], isInDeepDungeon: false),
            Frame(At.AddSeconds(1), [Player()], isInDeepDungeon: true));
        var provider = new LiveWorldSnapshotProvider(source);

        var outside = provider.Capture();
        var inside = provider.Capture();

        Assert.NotEqual(outside.SessionId, inside.SessionId);
        Assert.Equal(0, inside.Sequence);
        Assert.Equal(DeepDungeonKind.EurekaOrthos, inside.Dungeon.Kind);
    }

    [Fact]
    public void ContentExitKeepsPublicPoseButIsNotADeepDungeon()
    {
        var snapshot = Provider(Frame(At, [Player()], dutyStarted: false, isInDeepDungeon: false)).Capture();

        Assert.Equal(DeepDungeonKind.None, snapshot.Dungeon.Kind);
        Assert.Equal(new DDCore.Core.Geometry.Coordinate2D(0, 0), snapshot.PlayerPose!.Position);
        Assert.Equal(ObservationValueQuality.Direct, snapshot.PlayerPose.Metadata.Quality);
    }

    private static LiveWorldSnapshotProvider Provider(RawObservationFrame frame) => new(new QueueSource(frame));

    internal static RawObservationFrame Frame(
        DateTimeOffset at,
        ImmutableArray<RawActorObservation> actors,
        uint? territoryId = 1099,
        uint? contentId = 897,
        bool dutyStarted = true,
        uint? localPlayerEntityId = 10,
        RawPlayerPoseObservation? playerPose = null,
        bool isInDeepDungeon = true,
        RawContentFinderConditionObservation? contentFinderCondition = null) =>
        new(
            at,
            true,
            territoryId,
            contentId,
            dutyStarted,
            localPlayerEntityId,
            actors,
            [],
            playerPose ?? RawPlayerPoseObservation.Direct(new RawPosition(0, 0, 0), 0),
            isInDeepDungeon,
            contentFinderCondition ?? (contentId == 897
                ? new RawContentFinderConditionObservation(897, 1099, 60031, "dd003_001")
                : null));

    internal static RawActorObservation Player() => new(
        10,
        10,
        0,
        0x1000,
        RawActorKind.Player,
        new RawPosition(0, 0, 0),
        0,
        0.5,
        100,
        100,
        false,
        true,
        false,
        null,
        null,
        []);

    internal static RawActorObservation Enemy(
        uint entityId,
        uint baseId,
        ulong? gameObjectId = null,
        RawPosition? position = null,
        double rotation = 0,
        RawCastObservation? cast = null,
        ImmutableArray<RawStatusObservation>? statuses = null) =>
        new(
            entityId,
            gameObjectId ?? entityId,
            (ushort)entityId,
            baseId,
            RawActorKind.Enemy,
            position ?? new RawPosition(-300, 0, -300),
            rotation,
            1,
            100,
            100,
            false,
            true,
            true,
            null,
            cast,
            statuses ?? []);

    private static RawCastObservation Cast(double current, double total) =>
        new(GancanaghAnalyzer.MandrashockSingleActionId, current, total, null);

    internal sealed class QueueSource(params RawObservationFrame[] frames) : ILiveObservationSource
    {
        private readonly Queue<RawObservationFrame> frames = new(frames);

        public RawObservationFrame Capture() => frames.Dequeue();
    }
}
