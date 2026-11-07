using System.Collections.Immutable;
using DDCore.Core.Geometry;
using DDCore.Core.Model;
using DDCore.Core.Schema;
using DDCore.Core.World;

namespace DDCore.Core.Mechanics;

public enum HazardSeverity
{
    Informational,
    Caution,
    Dangerous,
    Lethal,
}

public enum MechanicSafetyAssessment
{
    HazardsKnown,
    UnknownOrUnsupported,
    InvalidObservation,
}

public enum HazardLifetimeKind
{
    Instantaneous,
    Persistent,
}

public sealed record HazardEvidence(uint ActionId, ActorIdentity Source, string ReasonCode);

public sealed record HazardInstance(
    string HazardId,
    string AnalyzerId,
    AoeShape Shape,
    Coordinate2D Origin,
    double Rotation,
    DateTimeOffset ObservedAt,
    DateTimeOffset? ActivatesAt,
    DateTimeOffset? ExpiresAt,
    HazardLifetimeKind Lifetime,
    HazardSeverity Severity,
    SupportState Support,
    HazardEvidence Evidence)
{
    public void Validate()
    {
        Coordinate2D.EnsureFinite(Origin, nameof(Origin));
        Coordinate2D.EnsureFinite(Rotation, nameof(Rotation));
        Evidence.Source.Validate();
        if (string.IsNullOrWhiteSpace(HazardId)
            || string.IsNullOrWhiteSpace(AnalyzerId)
            || string.IsNullOrWhiteSpace(Evidence.ReasonCode)
            || ObservedAt == default
            || !Enum.IsDefined(Severity)
            || !Enum.IsDefined(Support)
            || !Enum.IsDefined(Lifetime)
            || (ActivatesAt is not null && ActivatesAt < ObservedAt)
            || (ExpiresAt is not null && ActivatesAt is not null && ExpiresAt < ActivatesAt))
        {
            throw new InvalidDataException("Hazard metadata or time range is invalid.");
        }

        if (ActivatesAt is null
            || (Lifetime == HazardLifetimeKind.Instantaneous && ExpiresAt != ActivatesAt)
            || (Lifetime == HazardLifetimeKind.Persistent && (ExpiresAt is null || ExpiresAt <= ActivatesAt)))
        {
            throw new InvalidDataException("Hazard lifetime does not match its activation and expiry timestamps.");
        }
    }
}

public sealed record UnsupportedMechanic(
    string AnalyzerId,
    uint ActionId,
    ActorIdentity Source,
    string ReasonCode,
    SupportState Support);

public sealed record MechanicFrame(
    int SchemaVersion,
    long SnapshotSequence,
    DateTimeOffset CapturedAt,
    ImmutableArray<HazardInstance> Hazards,
    ImmutableArray<UnsupportedMechanic> Unsupported,
    ImmutableArray<string> Diagnostics,
    MechanicSafetyAssessment Safety)
{
    public static MechanicFrame Unknown(WorldSnapshot snapshot, string reason, bool invalid = false) =>
        new(
            SchemaVersions.MechanicFrame,
            snapshot.Sequence,
            snapshot.CapturedAt,
            ImmutableArray<HazardInstance>.Empty,
            ImmutableArray<UnsupportedMechanic>.Empty,
            [reason],
            invalid ? MechanicSafetyAssessment.InvalidObservation : MechanicSafetyAssessment.UnknownOrUnsupported);
}

public sealed record MechanicState(
    long SnapshotSequence,
    DateTimeOffset CapturedAt,
    ImmutableDictionary<string, HazardInstance> ActiveHazards)
{
    public static MechanicState Empty { get; } = new(-1, DateTimeOffset.MinValue, ImmutableDictionary<string, HazardInstance>.Empty);
}

public sealed record AnalysisResult(MechanicState State, MechanicFrame Frame);

public interface IMechanicAnalyzer
{
    string AnalyzerId { get; }

    Version AnalyzerVersion { get; }

    bool CanAnalyze(WorldSnapshot snapshot);

    AnalysisResult Analyze(WorldSnapshot snapshot, MechanicState previousState);
}

public sealed record MechanicAnalysisSession(
    long LastSnapshotSequence,
    DateTimeOffset LastCapturedAt,
    ImmutableDictionary<string, MechanicState> AnalyzerStates)
{
    public static MechanicAnalysisSession Empty { get; } = new(-1, DateTimeOffset.MinValue, ImmutableDictionary<string, MechanicState>.Empty);
}

public sealed record RegistryAnalysisResult(MechanicAnalysisSession Session, MechanicFrame Frame);

public sealed class AnalyzerRegistry
{
    private readonly ImmutableArray<IMechanicAnalyzer> _analyzers;

    public AnalyzerRegistry(IEnumerable<IMechanicAnalyzer> analyzers)
    {
        _analyzers = analyzers.OrderBy(analyzer => analyzer.AnalyzerId, StringComparer.Ordinal).ToImmutableArray();
        if (_analyzers.Any(analyzer => string.IsNullOrWhiteSpace(analyzer.AnalyzerId))
            || _analyzers.GroupBy(analyzer => analyzer.AnalyzerId, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Analyzer IDs must be non-empty and unique.", nameof(analyzers));
        }
    }

    public RegistryAnalysisResult Analyze(WorldSnapshot snapshot, MechanicAnalysisSession previousSession)
    {
        snapshot.Validate();
        if (snapshot.Sequence <= previousSession.LastSnapshotSequence)
        {
            return new(previousSession, MechanicFrame.Unknown(snapshot, "analysis.sequence.not-increasing", invalid: true));
        }

        if (previousSession.LastCapturedAt != DateTimeOffset.MinValue && snapshot.CapturedAt < previousSession.LastCapturedAt)
        {
            return new(previousSession, MechanicFrame.Unknown(snapshot, "analysis.time.regressed", invalid: true));
        }

        if (snapshot.Quality is ObservationQuality.Stale or ObservationQuality.Unsupported or ObservationQuality.Invalid)
        {
            return new(
                previousSession with { LastSnapshotSequence = snapshot.Sequence, LastCapturedAt = snapshot.CapturedAt },
                MechanicFrame.Unknown(snapshot, $"analysis.observation.{snapshot.Quality.ToString().ToLowerInvariant()}", invalid: snapshot.Quality == ObservationQuality.Invalid));
        }

        var inputUncertain = snapshot.Quality == ObservationQuality.Partial;
        var applicable = _analyzers.Where(analyzer => analyzer.CanAnalyze(snapshot)).ToArray();
        if (applicable.Length == 0)
        {
            return new(
                previousSession with { LastSnapshotSequence = snapshot.Sequence, LastCapturedAt = snapshot.CapturedAt },
                MechanicFrame.Unknown(snapshot, "analysis.no-analyzer"));
        }

        var states = previousSession.AnalyzerStates.ToBuilder();
        var hazards = ImmutableArray.CreateBuilder<HazardInstance>();
        var unsupported = ImmutableArray.CreateBuilder<UnsupportedMechanic>();
        var diagnostics = ImmutableArray.CreateBuilder<string>();
        if (inputUncertain)
        {
            diagnostics.Add("analysis.observation.partial");
        }
        var invalid = false;
        var unknown = false;

        foreach (var analyzer in applicable)
        {
            try
            {
                var previous = previousSession.AnalyzerStates.GetValueOrDefault(analyzer.AnalyzerId, MechanicState.Empty);
                var result = analyzer.Analyze(snapshot, previous);
                states[analyzer.AnalyzerId] = result.State;
                hazards.AddRange(result.Frame.Hazards);
                unsupported.AddRange(result.Frame.Unsupported);
                diagnostics.AddRange(result.Frame.Diagnostics);
                invalid |= result.Frame.Safety == MechanicSafetyAssessment.InvalidObservation;
                unknown |= result.Frame.Safety == MechanicSafetyAssessment.UnknownOrUnsupported;
            }
            catch (Exception exception)
            {
                invalid = true;
                diagnostics.Add($"analysis.analyzer-failed:{analyzer.AnalyzerId}:{exception.GetType().Name}");
            }
        }

        var safety = invalid
            ? MechanicSafetyAssessment.InvalidObservation
            : inputUncertain || unknown || unsupported.Count > 0
                ? MechanicSafetyAssessment.UnknownOrUnsupported
                : MechanicSafetyAssessment.HazardsKnown;
        var frame = new MechanicFrame(
            SchemaVersions.MechanicFrame,
            snapshot.Sequence,
            snapshot.CapturedAt,
            hazards.OrderBy(hazard => hazard.HazardId, StringComparer.Ordinal).ToImmutableArray(),
            unsupported.OrderBy(item => item.ActionId).ThenBy(item => item.Source.EntityId).ToImmutableArray(),
            diagnostics.ToImmutable(),
            safety);
        return new(new MechanicAnalysisSession(snapshot.Sequence, snapshot.CapturedAt, states.ToImmutable()), frame);
    }
}
