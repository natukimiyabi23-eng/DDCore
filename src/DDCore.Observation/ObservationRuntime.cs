using System.Collections.Immutable;
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
    private MechanicAnalysisSession analysisSession = MechanicAnalysisSession.Empty;
    private Guid? sessionId;
    private LiveObservationDiagnostics current = LiveObservationDiagnostics.Waiting;
    private bool disposed;

    public ObservationRuntime(
        IObservationTickSource tickSource,
        IWorldSnapshotProvider provider,
        AnalyzerRegistry registry)
    {
        this.tickSource = tickSource;
        this.provider = provider;
        this.registry = registry;
        tickSource.Tick += OnTick;
    }

    public LiveObservationDiagnostics Current => Volatile.Read(ref current);

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
                sessionId = snapshot.SessionId;
                analysisSession = MechanicAnalysisSession.Empty;
            }

            var analysis = registry.Analyze(snapshot, analysisSession);
            analysisSession = analysis.Session;
            var reasons = snapshot.Diagnostics
                .Concat(analysis.Frame.Diagnostics)
                .Concat(analysis.Frame.Unsupported.Select(item => $"{item.ReasonCode}:{item.ActionId}"))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();
            Volatile.Write(ref current, new LiveObservationDiagnostics(
                ConvertState(snapshot.Quality),
                snapshot.Dungeon.Kind,
                snapshot.Dungeon.Floor,
                snapshot.Actors.Count(actor => actor.Kind == ActorKind.Enemy),
                snapshot.Actors.Count(actor => actor.Kind == ActorKind.Enemy && actor.Cast?.Lifecycle == CastLifecycle.Casting),
                analysis.Frame.Hazards.Length,
                analysis.Frame.Safety,
                reasons,
                snapshot.CapturedAt));
        }
        catch (Exception exception)
        {
            Volatile.Write(ref current, new LiveObservationDiagnostics(
                LiveObservationState.Invalid,
                DeepDungeonKind.Unsupported,
                null,
                0,
                0,
                0,
                MechanicSafetyAssessment.InvalidObservation,
                [$"observation.capture-failed:{exception.GetType().Name}"],
                DateTimeOffset.UtcNow));
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
