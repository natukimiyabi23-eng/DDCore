using System.Collections.Immutable;
using DDCore.Core.Logging;
using DDCore.Core.Mechanics;
using DDCore.Core.Model;
using DDCore.Core.World;

namespace DDCore.Observation;

public interface IObservationTickSource : IDisposable
{
    event Action? Tick;
}

public enum LiveObservationState
{
    Waiting,
    Observing,
    Partial,
    Unsupported,
    Invalid,
}

public sealed record LiveObservationDiagnostics(
    LiveObservationState State,
    DeepDungeonKind DungeonKind,
    int? Floor,
    int EnemyCount,
    int CastingEnemyCount,
    int HazardCount,
    MechanicSafetyAssessment Safety,
    ImmutableArray<string> Reasons,
    DateTimeOffset? LastObservedAt)
{
    public static LiveObservationDiagnostics Waiting { get; } = new(
        LiveObservationState.Waiting,
        DeepDungeonKind.None,
        null,
        0,
        0,
        0,
        MechanicSafetyAssessment.UnknownOrUnsupported,
        ["observation.waiting"],
        null);
}

public sealed class ObservationRuntime : IDisposable
{
    private readonly IObservationTickSource tickSource;
    private readonly IWorldSnapshotProvider provider;
    private readonly AnalyzerRegistry registry;
    private readonly IObservationRecorder? recorder;
    private readonly ObservationDiagnosticLog diagnosticLog;
    private readonly IDDLogger logger;
    private MechanicAnalysisSession analysisSession = MechanicAnalysisSession.Empty;
    private Guid? sessionId;
    private LiveObservationDiagnostics current = LiveObservationDiagnostics.Waiting;
    private string? lastDiagnosticSignature;
    private bool disposed;

    public ObservationRuntime(
        IObservationTickSource tickSource,
        IWorldSnapshotProvider provider,
        AnalyzerRegistry registry,
        IObservationRecorder? recorder = null,
        ObservationDiagnosticLog? diagnosticLog = null,
        IDDLogger? logger = null)
    {
        this.tickSource = tickSource;
        this.provider = provider;
        this.registry = registry;
        this.recorder = recorder;
        this.diagnosticLog = diagnosticLog ?? new ObservationDiagnosticLog();
        this.logger = logger ?? NullDDLogger.Instance;
        tickSource.Tick += OnTick;
    }

    public LiveObservationDiagnostics Current => Volatile.Read(ref current);

    public ObservationDiagnosticLog DiagnosticLog => diagnosticLog;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        tickSource.Tick -= OnTick;
        tickSource.Dispose();
    }

    private void OnTick()
    {
        try
        {
            var snapshot = provider.Capture();
            if (sessionId != snapshot.SessionId)
            {
                var previousSession = sessionId;
                sessionId = snapshot.SessionId;
                analysisSession = MechanicAnalysisSession.Empty;
                AppendDiagnostic(
                    snapshot.CapturedAt,
                    DiagnosticSeverity.Information,
                    "observation.session.reset",
                    previousSession is null
                        ? $"観測セッションを開始しました。session={snapshot.SessionId:N}"
                        : $"観測セッションが切り替わりました。old={previousSession:N}, new={snapshot.SessionId:N}",
                    snapshot.SessionId,
                    snapshot.Sequence);
            }

            var analysis = registry.Analyze(snapshot, analysisSession);
            analysisSession = analysis.Session;
            recorder?.Record(snapshot, analysis.Frame);
            var reasons = snapshot.Diagnostics
                .Concat(analysis.Frame.Diagnostics)
                .Concat(analysis.Frame.Unsupported.Select(item => $"{item.ReasonCode}:{item.ActionId}"))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();
            var updated = new LiveObservationDiagnostics(
                ConvertState(snapshot.Quality),
                snapshot.Dungeon.Kind,
                snapshot.Dungeon.Floor,
                snapshot.Actors.Count(actor => actor.Kind == ActorKind.Enemy),
                snapshot.Actors.Count(actor => actor.Kind == ActorKind.Enemy && actor.Cast?.Lifecycle == CastLifecycle.Casting),
                analysis.Frame.Hazards.Length,
                analysis.Frame.Safety,
                reasons,
                snapshot.CapturedAt);
            Volatile.Write(ref current, updated);
            RecordStateChange(snapshot, analysis.Frame, updated);
        }
        catch (Exception exception)
        {
            var at = DateTimeOffset.UtcNow;
            Volatile.Write(ref current, new LiveObservationDiagnostics(
                LiveObservationState.Invalid,
                DeepDungeonKind.Unsupported,
                null,
                0,
                0,
                0,
                MechanicSafetyAssessment.InvalidObservation,
                [$"observation.capture-failed:{exception.GetType().Name}"],
                at));
            AppendDiagnostic(
                at,
                DiagnosticSeverity.Error,
                "observation.capture-failed",
                $"観測または解析に失敗しました。type={exception.GetType().Name}",
                sessionId,
                null,
                exception);
        }
    }

    private void RecordStateChange(
        WorldSnapshot snapshot,
        MechanicFrame frame,
        LiveObservationDiagnostics diagnostics)
    {
        var hazardSignature = string.Join(
            ';',
            frame.Hazards.Select(hazard =>
                $"{hazard.Evidence.ActionId}:{hazard.Evidence.Source.EntityId}:{hazard.Shape}:{hazard.Origin.X:F3}:{hazard.Origin.Y:F3}:{hazard.ActivatesAt:O}"));
        var signature = string.Join(
            '|',
            diagnostics.State,
            diagnostics.DungeonKind,
            diagnostics.Floor,
            diagnostics.EnemyCount,
            diagnostics.CastingEnemyCount,
            diagnostics.HazardCount,
            diagnostics.Safety,
            hazardSignature,
            string.Join(';', diagnostics.Reasons));
        if (string.Equals(signature, lastDiagnosticSignature, StringComparison.Ordinal))
        {
            return;
        }

        lastDiagnosticSignature = signature;
        var severity = diagnostics.State == LiveObservationState.Invalid
            ? DiagnosticSeverity.Error
            : diagnostics.State is LiveObservationState.Partial or LiveObservationState.Unsupported
                ? DiagnosticSeverity.Warning
                : DiagnosticSeverity.Information;
        var message = $"state={diagnostics.State}, dungeon={diagnostics.DungeonKind}, floor={diagnostics.Floor?.ToString() ?? "unknown"}, "
            + $"enemies={diagnostics.EnemyCount}, casts={diagnostics.CastingEnemyCount}, hazards={diagnostics.HazardCount}, safety={diagnostics.Safety}";
        if (frame.Hazards.Length > 0)
        {
            var hazards = string.Join(
                "; ",
                frame.Hazards.Take(12).Select(hazard =>
                    $"action={hazard.Evidence.ActionId}, source={hazard.Evidence.Source.EntityId}, shape={hazard.Shape}, "
                    + $"origin=({hazard.Origin.X:F3},{hazard.Origin.Y:F3}), rotation={hazard.Rotation:F6}, activates={hazard.ActivatesAt:O}"));
            message = $"{message}; {hazards}";
        }

        AppendDiagnostic(
            snapshot.CapturedAt,
            severity,
            "observation.state.changed",
            message,
            snapshot.SessionId,
            snapshot.Sequence);
    }

    private void AppendDiagnostic(
        DateTimeOffset at,
        DiagnosticSeverity severity,
        string code,
        string message,
        Guid? diagnosticSessionId,
        long? sequence,
        Exception? exception = null)
    {
        diagnosticLog.Append(new ObservationDiagnosticEntry(
            at,
            severity,
            code,
            message,
            diagnosticSessionId,
            sequence));
        switch (severity)
        {
            case DiagnosticSeverity.Error:
                logger.Error(message, exception);
                break;
            case DiagnosticSeverity.Warning:
                logger.Warning(message);
                break;
            default:
                logger.Information(message);
                break;
        }
    }

    private static LiveObservationState ConvertState(ObservationQuality quality) => quality switch
    {
        ObservationQuality.Complete => LiveObservationState.Observing,
        ObservationQuality.Partial => LiveObservationState.Partial,
        ObservationQuality.Invalid => LiveObservationState.Invalid,
        _ => LiveObservationState.Unsupported,
    };
}
