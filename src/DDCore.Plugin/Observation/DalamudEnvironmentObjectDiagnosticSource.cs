using System.Collections.Immutable;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using DDCore.Observation;

namespace DDCore.Plugin.Observation;

internal sealed class DalamudEnvironmentObjectDiagnosticSource : IDisposable
{
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly ITargetManager targetManager;
    private readonly EnvironmentObjectDiagnosticRecorder recorder;
    private readonly Func<LiveObservationDiagnostics> diagnostics;
    private string observationSessionId = Guid.NewGuid().ToString("N");
    private uint? lastTerritory;
    private bool wasLoggedIn;
    private bool disposed;

    public DalamudEnvironmentObjectDiagnosticSource(
        IFramework framework,
        IClientState clientState,
        IObjectTable objectTable,
        ITargetManager targetManager,
        EnvironmentObjectDiagnosticRecorder recorder,
        Func<LiveObservationDiagnostics> diagnostics)
    {
        this.framework = framework;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.targetManager = targetManager;
        this.recorder = recorder;
        this.diagnostics = diagnostics;
        framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        framework.Update -= OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (recorder.Status.State != EnvironmentObjectRecordingState.Recording) return;

        try
        {
            UpdateSessionBoundary();
            var captured = ImmutableArray.CreateBuilder<RawEnvironmentObjectObservation>();
            foreach (var gameObject in objectTable)
            {
                if (!TryMapKind(gameObject.ObjectKind, out var kind)) continue;

                RawPosition? position = null;
                double? rotation = null;
                string? missingReason = null;
                try
                {
                    var value = gameObject.Position;
                    position = new RawPosition(value.X, value.Y, value.Z);
                    rotation = gameObject.Rotation;
                }
                catch (Exception exception)
                {
                    missingReason = $"environment-object.spatial-read-failed:{exception.GetType().Name}";
                }

                captured.Add(new RawEnvironmentObjectObservation(
                    gameObject.GameObjectId,
                    gameObject.ObjectIndex,
                    kind,
                    gameObject.BaseId,
                    position,
                    rotation,
                    gameObject.IsTargetable,
                    gameObject.IsValid(),
                    missingReason));
            }

            var current = diagnostics();
            RawSelectedObjectObservation? selected = null;
            try
            {
                if (targetManager.Target is { } target && TryMapKind(target.ObjectKind, out var targetKind))
                {
                    RawPosition? position = null;
                    double? rotation = null;
                    try { var p = target.Position; position = new RawPosition(p.X, p.Y, p.Z); rotation = target.Rotation; } catch { }
                    selected = new RawSelectedObjectObservation(target.GameObjectId, target.ObjectIndex, targetKind, target.BaseId, position, rotation, target.IsTargetable, target.IsValid());
                }
            }
            catch (Exception exception) { recorder.ReportSourceFailure("environment-object.selection-read-failed", exception); return; }
            recorder.Record(
                DateTimeOffset.UtcNow,
                new EnvironmentObjectContext(
                    current.DungeonKind is not (DDCore.Core.World.DeepDungeonKind.None or DDCore.Core.World.DeepDungeonKind.Unsupported),
                    current.DungeonKind,
                    current.FloorRange,
                    current.Floor,
                    observationSessionId),
                captured.ToImmutable(), selected);
        }
        catch (Exception exception)
        {
            recorder.ReportSourceFailure("environment-object.source-callback-failed", exception);
        }
    }

    private void UpdateSessionBoundary()
    {
        var loggedIn = clientState.IsLoggedIn;
        uint? territory = loggedIn ? clientState.TerritoryType : null;
        if (loggedIn != wasLoggedIn || territory != lastTerritory)
        {
            observationSessionId = Guid.NewGuid().ToString("N");
            wasLoggedIn = loggedIn;
            lastTerritory = territory;
        }
    }

    private static bool TryMapKind(ObjectKind kind, out EnvironmentObjectKind mapped)
    {
        mapped = kind switch
        {
            ObjectKind.EventObj => EnvironmentObjectKind.EventObject,
            ObjectKind.Treasure => EnvironmentObjectKind.Treasure,
            ObjectKind.Aetheryte => EnvironmentObjectKind.Aetheryte,
            _ => default,
        };
        return kind is ObjectKind.EventObj or ObjectKind.Treasure or ObjectKind.Aetheryte;
    }
}
