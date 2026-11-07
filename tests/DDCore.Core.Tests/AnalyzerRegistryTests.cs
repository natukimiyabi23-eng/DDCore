using System.Collections.Immutable;
using DDCore.Core.Mechanics;
using DDCore.Core.Model;
using DDCore.Core.Replay;
using DDCore.Core.Schema;
using DDCore.Core.World;

namespace DDCore.Core.Tests;

public sealed class AnalyzerRegistryTests
{
    [Fact]
    public void RegistryProducesDeterministicOrder()
    {
        var snapshot = Snapshot(1);
        var registry = new AnalyzerRegistry([new StubAnalyzer("z"), new StubAnalyzer("a")]);

        var first = registry.Analyze(snapshot, MechanicAnalysisSession.Empty);
        var second = registry.Analyze(snapshot, MechanicAnalysisSession.Empty);

        Assert.Equal(first.Frame.Diagnostics.ToArray(), second.Frame.Diagnostics.ToArray());
        Assert.Equal(new[] { "a", "z" }, first.Frame.Diagnostics.ToArray());
        Assert.Equal(MechanicSafetyAssessment.HazardsKnown, first.Frame.Safety);
    }

    [Fact]
    public void DuplicateAnalyzerIdIsRejected() =>
        Assert.Throws<ArgumentException>(() => new AnalyzerRegistry([new StubAnalyzer("same"), new StubAnalyzer("same")]));

    [Fact]
    public void MissingAnalyzerIsUnknownNotSafe()
    {
        var result = new AnalyzerRegistry([]).Analyze(Snapshot(1), MechanicAnalysisSession.Empty);
        Assert.Equal(MechanicSafetyAssessment.UnknownOrUnsupported, result.Frame.Safety);
        Assert.Contains("analysis.no-analyzer", result.Frame.Diagnostics);
    }

    [Fact]
    public void PartialObservationIsUnknownNotSafe()
    {
        var snapshot = new MockWorldSnapshotBuilder()
            .WithSequence(1)
            .WithQuality(ObservationQuality.Partial, "missing.actors")
            .Build();
        var result = new AnalyzerRegistry([new StubAnalyzer("a")]).Analyze(snapshot, MechanicAnalysisSession.Empty);

        Assert.Equal(MechanicSafetyAssessment.UnknownOrUnsupported, result.Frame.Safety);
        Assert.Empty(result.Frame.Hazards);
    }

    [Fact]
    public void NonIncreasingSequenceIsInvalid()
    {
        var registry = new AnalyzerRegistry([new StubAnalyzer("a")]);
        var first = registry.Analyze(Snapshot(2), MechanicAnalysisSession.Empty);
        var second = registry.Analyze(Snapshot(2), first.Session);

        Assert.Equal(MechanicSafetyAssessment.InvalidObservation, second.Frame.Safety);
    }

    [Fact]
    public void AnalyzerExceptionBecomesInvalidFrame()
    {
        var result = new AnalyzerRegistry([new ThrowingAnalyzer()]).Analyze(Snapshot(1), MechanicAnalysisSession.Empty);

        Assert.Equal(MechanicSafetyAssessment.InvalidObservation, result.Frame.Safety);
        Assert.Contains(result.Frame.Diagnostics, value => value.Contains("analysis.analyzer-failed", StringComparison.Ordinal));
    }

    [Fact]
    public void UnsupportedMechanicPreventsKnownAssessment()
    {
        var result = new AnalyzerRegistry([new StubAnalyzer("a", unsupported: true)]).Analyze(Snapshot(1), MechanicAnalysisSession.Empty);
        Assert.Equal(MechanicSafetyAssessment.UnknownOrUnsupported, result.Frame.Safety);
    }

    private static WorldSnapshot Snapshot(long sequence) => new MockWorldSnapshotBuilder().WithSequence(sequence).Build();

    private sealed class StubAnalyzer(string id, bool unsupported = false) : IMechanicAnalyzer
    {
        public string AnalyzerId => id;

        public Version AnalyzerVersion => new(1, 0);

        public bool CanAnalyze(WorldSnapshot snapshot) => true;

        public AnalysisResult Analyze(WorldSnapshot snapshot, MechanicState previousState)
        {
            var unsupportedItems = unsupported
                ? ImmutableArray.Create(new UnsupportedMechanic(id, 123, new ActorIdentity(1, 0), "unknown", SupportState.Unsupported))
                : ImmutableArray<UnsupportedMechanic>.Empty;
            var frame = new MechanicFrame(
                SchemaVersions.MechanicFrame,
                snapshot.Sequence,
                snapshot.CapturedAt,
                ImmutableArray<HazardInstance>.Empty,
                unsupportedItems,
                [id],
                unsupported ? MechanicSafetyAssessment.UnknownOrUnsupported : MechanicSafetyAssessment.HazardsKnown);
            return new(new MechanicState(snapshot.Sequence, snapshot.CapturedAt, ImmutableDictionary<string, HazardInstance>.Empty), frame);
        }
    }

    private sealed class ThrowingAnalyzer : IMechanicAnalyzer
    {
        public string AnalyzerId => "throwing";

        public Version AnalyzerVersion => new(1, 0);

        public bool CanAnalyze(WorldSnapshot snapshot) => true;

        public AnalysisResult Analyze(WorldSnapshot snapshot, MechanicState previousState) => throw new InvalidOperationException("test");
    }
}
