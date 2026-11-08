using DDCore.Core.Mechanics;
using DDCore.Core.Mechanics.Analyzers;
using DDCore.Core.World;
using DDCore.Observation;

namespace DDCore.Observation.Tests;

public sealed class ObservationRuntimeTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ObservationAssemblyHasNoDalamudOrBossModReference()
    {
        var references = typeof(LiveWorldSnapshotProvider).Assembly.GetReferencedAssemblies().Select(name => name.Name).ToArray();

        Assert.DoesNotContain("Dalamud", references);
        Assert.DoesNotContain("BossMod", references);
    }

    [Fact]
    public void TenFloorBandDoesNotPretendExactBossFloorOrClaimSafety()
    {
        var frame = LiveWorldSnapshotProviderTests.Frame(
            At,
            [
                LiveWorldSnapshotProviderTests.Player(),
                LiveWorldSnapshotProviderTests.Enemy(
                    1,
                    GancanaghAnalyzer.BossObjectId,
                    cast: new RawCastObservation(GancanaghAnalyzer.MandrashockSingleActionId, 1, 5, null)),
            ]);
        var tick = new FakeTickSource();
        using var runtime = new ObservationRuntime(
            tick,
            new LiveWorldSnapshotProvider(new LiveWorldSnapshotProviderTests.QueueSource(frame)),
            new AnalyzerRegistry([new GancanaghAnalyzer()]));

        tick.Raise();

        Assert.Equal(LiveObservationState.Partial, runtime.Current.State);
        Assert.Equal(0, runtime.Current.HazardCount);
        Assert.Equal(MechanicSafetyAssessment.UnknownOrUnsupported, runtime.Current.Safety);
        Assert.Equal(DeepDungeonKind.EurekaOrthos, runtime.Current.DungeonKind);
        Assert.Null(runtime.Current.Floor);
        Assert.Equal(new DeepDungeonFloorRange(1, 10), runtime.Current.FloorRange);
    }

    [Fact]
    public void ConvertsCaptureExceptionToInvalidDiagnostics()
    {
        var tick = new FakeTickSource();
        var diagnosticLog = new ObservationDiagnosticLog();
        using var runtime = new ObservationRuntime(
            tick,
            new ThrowingProvider(),
            new AnalyzerRegistry([]),
            diagnosticLog: diagnosticLog);

        tick.Raise();

        Assert.Equal(LiveObservationState.Invalid, runtime.Current.State);
        Assert.Equal(MechanicSafetyAssessment.InvalidObservation, runtime.Current.Safety);
        Assert.Contains("observation.capture-failed:InvalidOperationException", runtime.Current.Reasons);
        Assert.Equal("observation.capture-failed", diagnosticLog.LatestError?.Code);
    }

    [Fact]
    public void DiagnosticLogRecordsChangesWithoutRepeatingIdenticalState()
    {
        var source = new LiveWorldSnapshotProviderTests.QueueSource(
            LiveWorldSnapshotProviderTests.Frame(At, [LiveWorldSnapshotProviderTests.Player()], dutyStarted: false),
            LiveWorldSnapshotProviderTests.Frame(At.AddSeconds(1), [LiveWorldSnapshotProviderTests.Player()], dutyStarted: false));
        var tick = new FakeTickSource();
        var diagnosticLog = new ObservationDiagnosticLog();
        using var runtime = new ObservationRuntime(
            tick,
            new LiveWorldSnapshotProvider(source),
            new AnalyzerRegistry([]),
            diagnosticLog: diagnosticLog);

        tick.Raise();
        var afterFirst = diagnosticLog.Snapshot().Length;
        tick.Raise();

        Assert.Equal(2, afterFirst);
        Assert.Equal(afterFirst, diagnosticLog.Snapshot().Length);
        Assert.Equal(["observation.session.reset", "observation.state.changed"], diagnosticLog.Snapshot().Select(entry => entry.Code));
    }

    [Fact]
    public void TerritoryChangeStopsActiveRecordingBeforeSessionsMix()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ddcore-runtime-{Guid.NewGuid():N}");
        try
        {
            using var recorder = new ObservationRecordingService(directory, "0.3.1.0", utcNow: () => At);
            Assert.True(recorder.Start());
            var source = new LiveWorldSnapshotProviderTests.QueueSource(
                LiveWorldSnapshotProviderTests.Frame(At, [LiveWorldSnapshotProviderTests.Player()], territoryId: 777, dutyStarted: false),
                LiveWorldSnapshotProviderTests.Frame(At.AddSeconds(1), [LiveWorldSnapshotProviderTests.Player()], territoryId: 778, dutyStarted: false));
            var tick = new FakeTickSource();
            using var runtime = new ObservationRuntime(
                tick,
                new LiveWorldSnapshotProvider(source),
                new AnalyzerRegistry([]),
                recorder);

            tick.Raise();
            tick.Raise();

            Assert.Equal(ObservationRecordingState.Stopped, recorder.Status.State);
            Assert.Equal("recording.session-changed", recorder.Status.StopReason);
            Assert.Equal(1, recorder.Status.Preview.SnapshotCount);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void LogoutStopsActiveRecordingBeforeSessionsMix()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ddcore-runtime-{Guid.NewGuid():N}");
        try
        {
            using var recorder = new ObservationRecordingService(directory, "0.3.1.0", utcNow: () => At);
            Assert.True(recorder.Start());
            var source = new LiveWorldSnapshotProviderTests.QueueSource(
                LiveWorldSnapshotProviderTests.Frame(At, [LiveWorldSnapshotProviderTests.Player()], dutyStarted: false),
                new RawObservationFrame(At.AddSeconds(1), false, null, null, false, null, [], []));
            var tick = new FakeTickSource();
            using var runtime = new ObservationRuntime(
                tick,
                new LiveWorldSnapshotProvider(source),
                new AnalyzerRegistry([]),
                recorder);

            tick.Raise();
            tick.Raise();

            Assert.Equal(ObservationRecordingState.Stopped, recorder.Status.State);
            Assert.Equal("recording.session-changed", recorder.Status.StopReason);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void DisposeUnsubscribesAndDisposesTickSource()
    {
        var tick = new FakeTickSource();
        var provider = new CountingProvider();
        var runtime = new ObservationRuntime(tick, provider, new AnalyzerRegistry([]));

        tick.Raise();
        runtime.Dispose();
        tick.Raise();

        Assert.Equal(1, provider.Captures);
        Assert.True(tick.Disposed);
        Assert.Equal(0, tick.SubscriberCount);
    }

    private sealed class FakeTickSource : IObservationTickSource
    {
        private Action? tick;

        public bool Disposed { get; private set; }

        public int SubscriberCount => tick?.GetInvocationList().Length ?? 0;

        public event Action? Tick
        {
            add => tick += value;
            remove => tick -= value;
        }

        public void Raise() => tick?.Invoke();

        public void Dispose() => Disposed = true;
    }

    private sealed class ThrowingProvider : IWorldSnapshotProvider
    {
        public WorldSnapshot Capture() => throw new InvalidOperationException("test");
    }

    private sealed class CountingProvider : IWorldSnapshotProvider
    {
        public int Captures { get; private set; }

        public WorldSnapshot Capture()
        {
            Captures++;
            return new LiveWorldSnapshotProvider(new LiveWorldSnapshotProviderTests.QueueSource(
                LiveWorldSnapshotProviderTests.Frame(At, [LiveWorldSnapshotProviderTests.Player()], dutyStarted: false))).Capture();
        }
    }
}
