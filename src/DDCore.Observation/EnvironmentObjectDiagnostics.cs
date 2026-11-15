using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using DDCore.Core.Geometry;
using DDCore.Core.World;

namespace DDCore.Observation;

public enum EnvironmentObjectKind
{
    EventObject,
    Treasure,
    Aetheryte,
}

public enum EnvironmentObjectRecordingState
{
    Off,
    Recording,
    Stopped,
    LimitReached,
    Faulted,
}

public static class EnvironmentObjectDiagnosticPolicy
{
    public static bool Accepts(EnvironmentObjectKind kind) =>
        kind is EnvironmentObjectKind.EventObject or EnvironmentObjectKind.Treasure or EnvironmentObjectKind.Aetheryte;
}

public sealed record RawEnvironmentObjectObservation(
    ulong RuntimeId,
    ushort RuntimeIndex,
    EnvironmentObjectKind Kind,
    uint BaseId,
    RawPosition? Position,
    double? Rotation,
    bool IsTargetable,
    bool IsValid,
    string? MissingReason = null);

public sealed record RawSelectedObjectObservation(ulong RuntimeId, ushort RuntimeIndex, EnvironmentObjectKind Kind, uint BaseId, RawPosition? Position, double? Rotation, bool IsTargetable, bool IsValid);
public enum ObjectCorrelationQuality { Unknown, ExactRuntimeMatch, NoMatch, Ambiguous }
public enum EnvironmentObjectTransitionKind { TargetabilityChanged, Disappeared, Reappeared }
public sealed record EnvironmentObjectTransition(string AnonymousObjectId, EnvironmentObjectTransitionKind Kind, bool? PreviousTargetable, bool? CurrentTargetable, DateTimeOffset ObservedAtUtc, string ReasonCode);
public sealed record SelectedObjectCorrelation(ObjectCorrelationQuality Quality, string? AnonymousObjectId, EnvironmentObjectKind? ObjectKind, uint? BaseId, Coordinate2D? Position, double? Rotation, SpatialUsability CoordinateQuality, bool? IsTargetable, string ReasonCode);

public sealed record EnvironmentObjectContext(
    bool IsDeepDungeon,
    DeepDungeonKind DungeonKind,
    DeepDungeonFloorRange? FloorRange,
    int? ExactFloor,
    string ObservationSessionId);

public sealed record EnvironmentObjectDiagnosticEntry(
    string AnonymousObjectId,
    EnvironmentObjectKind ObjectKind,
    uint BaseId,
    Coordinate2D? Position,
    double? Rotation,
    SpatialUsability CoordinateQuality,
    bool IsTargetable,
    DateTimeOffset ObservedAtUtc,
    ObservationValueSource Provenance,
    string? ReasonCode);

public sealed record EnvironmentObjectDiagnosticSample(
    long Sequence,
    DateTimeOffset ObservedAtUtc,
    bool IsDeepDungeon,
    DeepDungeonKind DungeonKind,
    DeepDungeonFloorRange? FloorRange,
    int? ExactFloor,
    string ObservationSessionId,
    string? FloorEpochId,
    string ContinuityReason,
    ImmutableArray<EnvironmentObjectDiagnosticEntry> Objects,
    SelectedObjectCorrelation? SelectedObject = null,
    ImmutableArray<EnvironmentObjectTransition> Transitions = default);

public sealed record EnvironmentObjectDiagnosticDocument(
    int SchemaVersion,
    string ProducerVersion,
    string RecordingSessionId,
    DateTimeOffset CreatedAtUtc,
    ImmutableArray<EnvironmentObjectDiagnosticSample> Samples);

public sealed record EnvironmentObjectRecordingLimits(
    int MaximumBytes,
    int MaximumSamples,
    TimeSpan MaximumDuration,
    TimeSpan MinimumSampleInterval)
{
    public static EnvironmentObjectRecordingLimits Default { get; } =
        new(512 * 1024, 1_000, TimeSpan.FromMinutes(15), TimeSpan.FromMilliseconds(250));

    public void Validate()
    {
        if (MaximumBytes <= 0 || MaximumSamples <= 0 || MaximumDuration <= TimeSpan.Zero || MinimumSampleInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(EnvironmentObjectRecordingLimits));
        }
    }
}

public sealed record EnvironmentObjectRecordingPreview(
    int SampleCount,
    int ObjectCount,
    int EventObjectCount,
    int TreasureCount,
    int AetheryteCount,
    int UsableCount,
    int QuarantinedCount,
    int MissingCount,
    long StoredBytes,
    DateTimeOffset? LastObservedAtUtc,
    string? LatestReason,
    string? LatestSelection,
    int LatestTransitionCount)
{
    public static EnvironmentObjectRecordingPreview Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, null, null, null, 0);
}

public sealed record EnvironmentObjectRecordingStatus(
    EnvironmentObjectRecordingState State,
    EnvironmentObjectRecordingPreview Preview,
    DateTimeOffset? StartedAtUtc,
    string? RecordingPath,
    string? ExportPath,
    string? StopReason,
    string? LatestError)
{
    public static EnvironmentObjectRecordingStatus Off { get; } =
        new(EnvironmentObjectRecordingState.Off, EnvironmentObjectRecordingPreview.Empty, null, null, null, null, null);
}

public static class EnvironmentObjectSpatialQualifier
{
    public const string MissingReason = "environment-object.spatial.not-observed";
    public const string InvalidReason = "environment-object.spatial.invalid";
    public const string OriginReason = "environment-object.spatial.origin-unverified";
    public const string LifecycleReason = "environment-object.lifecycle-unreliable";

    public static ActorSpatialObservation Qualify(RawEnvironmentObjectObservation observed)
    {
        if (observed.Position is not { } rawPosition || observed.Rotation is not { } rawRotation)
        {
            var reason = string.IsNullOrWhiteSpace(observed.MissingReason) ? MissingReason : observed.MissingReason;
            return ActorSpatialObservation.Missing(
                new ObservationMetadata(ObservationValueSource.PublicDalamud, ObservationValueQuality.Missing, reason), reason);
        }

        if (!double.IsFinite(rawPosition.X) || !double.IsFinite(rawPosition.Y) || !double.IsFinite(rawPosition.Z) || !double.IsFinite(rawRotation))
        {
            return ActorSpatialObservation.Missing(
                new ObservationMetadata(ObservationValueSource.PublicDalamud, ObservationValueQuality.Invalid, InvalidReason), InvalidReason);
        }

        var position = new Coordinate2D(rawPosition.X, rawPosition.Z);
        var rotation = Coordinate2D.FromClockwisePositiveYRotation(rawRotation);
        if (!observed.IsValid)
        {
            return ActorSpatialObservation.Quarantined(
                position, rotation,
                new ObservationMetadata(ObservationValueSource.PublicDalamud, ObservationValueQuality.Direct, null), LifecycleReason);
        }

        if (position == new Coordinate2D(0, 0))
        {
            return ActorSpatialObservation.Quarantined(
                position, rotation,
                new ObservationMetadata(ObservationValueSource.PublicDalamud, ObservationValueQuality.Direct, null), OriginReason);
        }

        return ActorSpatialObservation.Usable(position, rotation, ObservationValueSource.PublicDalamud);
    }
}

public sealed class EnvironmentObjectDiagnosticRecorder : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly object gate = new();
    private readonly string recordingDirectory;
    private readonly string exportDirectory;
    private readonly string producerVersion;
    private readonly EnvironmentObjectRecordingLimits limits;
    private readonly IRecordingStorage storage;
    private readonly Func<DateTimeOffset> utcNow;
    private readonly List<EnvironmentObjectDiagnosticSample> samples = [];
    private readonly Dictionary<RuntimeKey, TrackedIdentity> activeIdentities = [];
    private readonly Dictionary<string, bool> lastTargetable = new(StringComparer.Ordinal);
    private readonly Dictionary<RuntimeKey, DateTimeOffset> recentlyAbsent = [];
    private EnvironmentObjectRecordingStatus status = EnvironmentObjectRecordingStatus.Off;
    private string recordingSessionId = string.Empty;
    private DateTimeOffset? lastSampleAt;
    private long sequence;
    private int nextAnonymousId;
    private string? lastObservationSessionId;
    private int? lastExactFloor;
    private bool disposed;

    public EnvironmentObjectDiagnosticRecorder(
        string recordingDirectory,
        string producerVersion,
        EnvironmentObjectRecordingLimits? limits = null,
        IRecordingStorage? storage = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        this.recordingDirectory = Path.GetFullPath(recordingDirectory ?? throw new ArgumentNullException(nameof(recordingDirectory)));
        exportDirectory = Path.Combine(this.recordingDirectory, "exports");
        this.producerVersion = string.IsNullOrWhiteSpace(producerVersion) ? throw new ArgumentException("Producer version is required.", nameof(producerVersion)) : producerVersion;
        this.limits = limits ?? EnvironmentObjectRecordingLimits.Default;
        this.limits.Validate();
        this.storage = storage ?? new FileRecordingStorage();
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public EnvironmentObjectRecordingStatus Status { get { lock (gate) { return status; } } }

    public static EnvironmentObjectDiagnosticDocument DeserializeDocument(ReadOnlySpan<byte> json)
    {
        var document = JsonSerializer.Deserialize<EnvironmentObjectDiagnosticDocument>(json, JsonOptions)
            ?? throw new InvalidDataException("Environment object diagnostic document could not be read.");
        if (document.SchemaVersion is not (1 or 2)) throw new InvalidDataException("Unsupported environment object diagnostic schema.");
        return document;
    }

    public bool Start()
    {
        lock (gate)
        {
            if (disposed || status.State == EnvironmentObjectRecordingState.Recording) return false;
            samples.Clear();
            activeIdentities.Clear();
            lastTargetable.Clear();
            recentlyAbsent.Clear();
            sequence = 0;
            nextAnonymousId = 0;
            lastSampleAt = null;
            lastObservationSessionId = null;
            lastExactFloor = null;
            recordingSessionId = Guid.NewGuid().ToString("N");
            status = new EnvironmentObjectRecordingStatus(
                EnvironmentObjectRecordingState.Recording, EnvironmentObjectRecordingPreview.Empty, utcNow(), null, null, null, null);
            return true;
        }
    }

    public bool Record(DateTimeOffset observedAtUtc, EnvironmentObjectContext context, IReadOnlyCollection<RawEnvironmentObjectObservation> objects, RawSelectedObjectObservation? selected = null)
    {
        lock (gate)
        {
            if (disposed || status.State != EnvironmentObjectRecordingState.Recording) return false;
            if (lastSampleAt is { } last && observedAtUtc - last < limits.MinimumSampleInterval) return false;
            if (observedAtUtc - status.StartedAtUtc >= limits.MaximumDuration)
            {
                StopInternal(EnvironmentObjectRecordingState.LimitReached, "environment-object.duration-limit");
                return false;
            }

            try
            {
                ArgumentNullException.ThrowIfNull(context);
                ArgumentNullException.ThrowIfNull(objects);
                var contextChanged = lastObservationSessionId is not null && lastObservationSessionId != context.ObservationSessionId;
                var floorChanged = lastExactFloor.HasValue && context.ExactFloor.HasValue && lastExactFloor != context.ExactFloor;
                if (contextChanged || floorChanged) { activeIdentities.Clear(); lastTargetable.Clear(); recentlyAbsent.Clear(); }

                var present = new HashSet<RuntimeKey>();
                var entries = ImmutableArray.CreateBuilder<EnvironmentObjectDiagnosticEntry>();
                var transitions = ImmutableArray.CreateBuilder<EnvironmentObjectTransition>();
                var currentAnonymous = new HashSet<string>(StringComparer.Ordinal);
                foreach (var observed in objects)
                {
                    present.Add(new RuntimeKey(observed.RuntimeId, observed.RuntimeIndex));
                    var identity = ResolveIdentity(observed);
                    currentAnonymous.Add(identity.AnonymousId);
                    if (lastTargetable.TryGetValue(identity.AnonymousId, out var previous) && previous != observed.IsTargetable)
                        transitions.Add(new(identity.AnonymousId, EnvironmentObjectTransitionKind.TargetabilityChanged, previous, observed.IsTargetable, observedAtUtc, "environment-object.targetability-changed"));
                    var runtimeKey = new RuntimeKey(observed.RuntimeId, observed.RuntimeIndex);
                    if (recentlyAbsent.Remove(runtimeKey))
                        transitions.Add(new(identity.AnonymousId, EnvironmentObjectTransitionKind.Reappeared, null, observed.IsTargetable, observedAtUtc, "environment-object.runtime-key-returned-continuity-unknown"));
                    lastTargetable[identity.AnonymousId] = observed.IsTargetable;
                    var spatial = EnvironmentObjectSpatialQualifier.Qualify(observed);
                    entries.Add(new EnvironmentObjectDiagnosticEntry(
                        identity.AnonymousId, observed.Kind, observed.BaseId, spatial.Position, spatial.Rotation,
                        spatial.Usability, observed.IsTargetable, observedAtUtc, ObservationValueSource.PublicDalamud, spatial.ReasonCode));
                }

                foreach (var removed in activeIdentities.Where(pair => !present.Contains(pair.Key)).ToArray())
                {
                    activeIdentities.Remove(removed.Key);
                    if (lastTargetable.TryGetValue(removed.Value.AnonymousId, out var previous))
                        transitions.Add(new(removed.Value.AnonymousId, EnvironmentObjectTransitionKind.Disappeared, previous, null, observedAtUtc, "environment-object.disappeared-not-open-confirmed"));
                    lastTargetable.Remove(removed.Value.AnonymousId);
                    recentlyAbsent[removed.Key] = observedAtUtc;
                }
                foreach (var missing in lastTargetable.Keys.Where(id => !currentAnonymous.Contains(id)).ToArray())
                {
                    lastTargetable.Remove(missing);
                }
                while (recentlyAbsent.Count > limits.MaximumSamples)
                {
                    var oldest = recentlyAbsent.OrderBy(pair => pair.Value).ThenBy(pair => pair.Key.RuntimeId).ThenBy(pair => pair.Key.RuntimeIndex).First();
                    recentlyAbsent.Remove(oldest.Key);
                }

                var selectedCorrelation = CorrelateSelected(selected, objects);

                var exactFloor = context.ExactFloor;
                var sample = new EnvironmentObjectDiagnosticSample(
                    ++sequence, observedAtUtc, context.IsDeepDungeon, context.DungeonKind, context.FloorRange, exactFloor,
                    context.ObservationSessionId,
                    exactFloor is { } floor ? $"floor-{floor}" : null,
                    exactFloor is not null ? "environment-object.floor-exact-observed" : "environment-object.floor-continuity-unverified",
                    entries.ToImmutable(), selectedCorrelation, transitions.ToImmutable());
                samples.Add(sample);
                if (samples.Count > limits.MaximumSamples)
                {
                    samples.RemoveAt(samples.Count - 1);
                    StopInternal(EnvironmentObjectRecordingState.LimitReached, "environment-object.sample-limit");
                    return false;
                }

                var bytes = Serialize();
                if (bytes.Length > limits.MaximumBytes)
                {
                    samples.RemoveAt(samples.Count - 1);
                    StopInternal(EnvironmentObjectRecordingState.LimitReached, "environment-object.byte-limit");
                    return false;
                }

                lastSampleAt = observedAtUtc;
                lastObservationSessionId = context.ObservationSessionId;
                lastExactFloor = context.ExactFloor;
                status = status with { Preview = BuildPreview(bytes.Length), LatestError = null };
                return true;
            }
            catch (Exception exception)
            {
                Fault("environment-object.record-failed", exception);
                return false;
            }
        }
    }

    private SelectedObjectCorrelation? CorrelateSelected(RawSelectedObjectObservation? selected, IReadOnlyCollection<RawEnvironmentObjectObservation> objects)
    {
        if (selected is null) return null;
        var candidates = objects.Where(x => x.RuntimeId == selected.RuntimeId && x.RuntimeIndex == selected.RuntimeIndex && x.Kind == selected.Kind && x.BaseId == selected.BaseId).ToArray();
        var spatialInput = new RawEnvironmentObjectObservation(selected.RuntimeId, selected.RuntimeIndex, selected.Kind, selected.BaseId, selected.Position, selected.Rotation, selected.IsTargetable, selected.IsValid);
        var spatial = EnvironmentObjectSpatialQualifier.Qualify(spatialInput);
        if (candidates.Length != 1) return new(candidates.Length > 1 ? ObjectCorrelationQuality.Ambiguous : ObjectCorrelationQuality.NoMatch, null, selected.Kind, selected.BaseId, spatial.Position, spatial.Rotation, spatial.Usability, selected.IsTargetable, candidates.Length > 1 ? "environment-object.selection.ambiguous" : "environment-object.selection.no-match");
        var runtimeKey = new RuntimeKey(selected.RuntimeId, selected.RuntimeIndex);
        if (!activeIdentities.TryGetValue(runtimeKey, out var identity)) return new(ObjectCorrelationQuality.NoMatch, null, selected.Kind, selected.BaseId, spatial.Position, spatial.Rotation, spatial.Usability, selected.IsTargetable, "environment-object.selection.identity-unavailable");
        return new(ObjectCorrelationQuality.ExactRuntimeMatch, identity.AnonymousId, selected.Kind, selected.BaseId, spatial.Position, spatial.Rotation, spatial.Usability, selected.IsTargetable, "environment-object.selection.exact-runtime-match");
    }

    public void ReportSourceFailure(string reason, Exception exception)
    {
        lock (gate)
        {
            if (!disposed && status.State == EnvironmentObjectRecordingState.Recording) Fault(reason, exception);
        }
    }

    public bool Stop()
    {
        lock (gate)
        {
            if (status.State != EnvironmentObjectRecordingState.Recording) return false;
            StopInternal(EnvironmentObjectRecordingState.Stopped, "environment-object.user-stopped");
            return status.State == EnvironmentObjectRecordingState.Stopped;
        }
    }

    public bool Export(bool previewConfirmed)
    {
        lock (gate)
        {
            if (!previewConfirmed || samples.Count == 0 || status.State == EnvironmentObjectRecordingState.Recording) return false;
            try
            {
                storage.EnsureDirectory(exportDirectory);
                var bytes = Serialize();
                _ = DeserializeDocument(bytes);
                var path = Path.Combine(exportDirectory, $"ddcore-environment-objects-{utcNow():yyyyMMdd-HHmmss}.diagnostic.json");
                storage.WriteAtomically(path, bytes);
                status = status with { ExportPath = path, LatestError = null };
                return true;
            }
            catch (Exception exception)
            {
                Fault("environment-object.export-failed", exception);
                return false;
            }
        }
    }

    public bool Delete(bool deletionConfirmed)
    {
        lock (gate)
        {
            if (!deletionConfirmed || status.State == EnvironmentObjectRecordingState.Recording) return false;
            try
            {
                if (status.RecordingPath is { } recording && storage.FileExists(recording)) storage.DeleteFile(recording);
                if (status.ExportPath is { } export && storage.FileExists(export)) storage.DeleteFile(export);
                samples.Clear();
                activeIdentities.Clear();
                status = EnvironmentObjectRecordingStatus.Off;
                return true;
            }
            catch (Exception exception)
            {
                Fault("environment-object.delete-failed", exception);
                return false;
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            if (status.State == EnvironmentObjectRecordingState.Recording)
                StopInternal(EnvironmentObjectRecordingState.Stopped, "environment-object.plugin-disposed");
            disposed = true;
        }
    }

    private TrackedIdentity ResolveIdentity(RawEnvironmentObjectObservation observed)
    {
        var key = new RuntimeKey(observed.RuntimeId, observed.RuntimeIndex);
        if (activeIdentities.TryGetValue(key, out var existing)
            && existing.Kind == observed.Kind && existing.BaseId == observed.BaseId)
        {
            return existing;
        }

        var created = new TrackedIdentity($"obj-{++nextAnonymousId:D6}", observed.Kind, observed.BaseId);
        activeIdentities[key] = created;
        return created;
    }

    private EnvironmentObjectRecordingPreview BuildPreview(int storedBytes)
    {
        var latest = samples.LastOrDefault();
        var objects = latest?.Objects ?? [];
        return new EnvironmentObjectRecordingPreview(
            samples.Count, objects.Length,
            objects.Count(x => x.ObjectKind == EnvironmentObjectKind.EventObject),
            objects.Count(x => x.ObjectKind == EnvironmentObjectKind.Treasure),
            objects.Count(x => x.ObjectKind == EnvironmentObjectKind.Aetheryte),
            objects.Count(x => x.CoordinateQuality == SpatialUsability.Usable),
            objects.Count(x => x.CoordinateQuality == SpatialUsability.Quarantined),
            objects.Count(x => x.CoordinateQuality == SpatialUsability.Missing),
            storedBytes,
            latest?.ObservedAtUtc,
            latest?.Objects.LastOrDefault(x => x.ReasonCode is not null)?.ReasonCode ?? latest?.ContinuityReason,
            latest?.SelectedObject is { } selected
                ? $"{selected.Quality}: {selected.ObjectKind} / BaseId {selected.BaseId?.ToString() ?? "不明"} / {selected.AnonymousObjectId ?? "匿名IDなし"} / 座標 {selected.CoordinateQuality} / {selected.ReasonCode}"
                : "選択対象なし",
            latest?.Transitions.Length ?? 0);
    }

    private void StopInternal(EnvironmentObjectRecordingState finalState, string reason)
    {
        try
        {
            storage.EnsureDirectory(recordingDirectory);
            var path = Path.Combine(recordingDirectory, $"ddcore-environment-objects-{status.StartedAtUtc:yyyyMMdd-HHmmss}.diagnostic.json");
            var bytes = Serialize();
            storage.WriteAtomically(path, bytes);
            status = status with
            {
                State = finalState,
                Preview = BuildPreview(bytes.Length),
                RecordingPath = path,
                StopReason = reason,
                LatestError = null,
            };
        }
        catch (Exception exception)
        {
            Fault("environment-object.save-failed", exception);
        }
    }

    private byte[] Serialize() => JsonSerializer.SerializeToUtf8Bytes(
        new EnvironmentObjectDiagnosticDocument(2, producerVersion, recordingSessionId, status.StartedAtUtc ?? utcNow(), samples.ToImmutableArray()),
        JsonOptions);

    private void Fault(string reason, Exception exception) => status = status with
    {
        State = EnvironmentObjectRecordingState.Faulted,
        StopReason = reason,
        LatestError = $"{reason}:{exception.GetType().Name}",
    };

    private sealed record TrackedIdentity(string AnonymousId, EnvironmentObjectKind Kind, uint BaseId);
    private readonly record struct RuntimeKey(ulong RuntimeId, ushort RuntimeIndex);
}
