using System.Collections.Immutable;
using DDCore.Core.Mechanics;
using DDCore.Core.Replay;
using DDCore.Core.World;

namespace DDCore.Observation;

public enum ObservationRecordingState
{
    Off,
    Recording,
    Stopped,
    LimitReached,
    Faulted,
}

public sealed record ObservationRecordingLimits(
    int MaximumBytes,
    int MaximumSnapshots,
    TimeSpan MaximumDuration,
    TimeSpan MinimumSampleInterval,
    TimeSpan FlushInterval)
{
    public static ObservationRecordingLimits Default { get; } = new(
        2 * 1024 * 1024,
        1_800,
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(5));

    public void Validate()
    {
        if (MaximumBytes <= 0
            || MaximumBytes > ReplayCodec.MaximumDocumentBytes
            || MaximumSnapshots <= 0
            || MaximumSnapshots > ReplayCodec.MaximumSnapshots
            || MaximumDuration <= TimeSpan.Zero
            || MinimumSampleInterval < TimeSpan.Zero
            || FlushInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ObservationRecordingLimits), "Recording limits are invalid.");
        }
    }
}

public sealed record ObservationRecordingPreview(
    Guid? SessionId,
    DateTimeOffset? StartedAt,
    DateTimeOffset? LastCapturedAt,
    int SnapshotCount,
    long StoredBytes,
    int EnemyCount,
    int ActiveCastCount,
    int HazardCount,
    string? LatestUnsupportedReason);

public sealed record ObservationRecordingStatus(
    ObservationRecordingState State,
    ObservationRecordingPreview Preview,
    string? RecordingPath,
    string? ExportPath,
    string? StopReason,
    string? LatestError)
{
    public static ObservationRecordingStatus Off { get; } = new(
        ObservationRecordingState.Off,
        new ObservationRecordingPreview(null, null, null, 0, 0, 0, 0, 0, null),
        null,
        null,
        null,
        null);
}

public interface IRecordingStorage
{
    void EnsureDirectory(string directory);
    void WriteAtomically(string path, byte[] contents);
    byte[] ReadAllBytes(string path);
    bool FileExists(string path);
    void DeleteFile(string path);
}

public sealed class FileRecordingStorage : IRecordingStorage
{
    public void EnsureDirectory(string directory) => Directory.CreateDirectory(directory);

    public void WriteAtomically(string path, byte[] contents)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Recording path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, contents);
            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, null);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public bool FileExists(string path) => File.Exists(path);

    public void DeleteFile(string path) => File.Delete(path);
}

public interface IObservationRecorder : IDisposable
{
    ObservationRecordingStatus Status { get; }
    bool Start();
    void Record(WorldSnapshot snapshot, MechanicFrame frame);
    bool Stop(string reason = "recording.user-stopped");
    bool Export(bool previewConfirmed);
    bool Delete(bool deletionConfirmed);
}

public sealed class ObservationRecordingService : IObservationRecorder
{
    private readonly object gate = new();
    private readonly string recordingDirectory;
    private readonly string exportDirectory;
    private readonly string producerVersion;
    private readonly ObservationRecordingLimits limits;
    private readonly IRecordingStorage storage;
    private readonly ReplayCodec codec;
    private readonly Func<DateTimeOffset> utcNow;
    private readonly List<WorldSnapshot> snapshots = [];
    private ObservationRecordingStatus status = ObservationRecordingStatus.Off;
    private DateTimeOffset? lastSampleAt;
    private DateTimeOffset? lastFlushAt;
    private MechanicFrame? lastFrame;
    private bool disposed;

    public ObservationRecordingService(
        string recordingDirectory,
        string producerVersion,
        ObservationRecordingLimits? limits = null,
        IRecordingStorage? storage = null,
        ReplayCodec? codec = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(recordingDirectory))
        {
            throw new ArgumentException("Recording directory is required.", nameof(recordingDirectory));
        }

        if (string.IsNullOrWhiteSpace(producerVersion) || producerVersion.Length > 128)
        {
            throw new ArgumentException("Producer version is invalid.", nameof(producerVersion));
        }

        this.recordingDirectory = Path.GetFullPath(recordingDirectory);
        exportDirectory = Path.Combine(this.recordingDirectory, "exports");
        this.producerVersion = producerVersion;
        this.limits = limits ?? ObservationRecordingLimits.Default;
        this.limits.Validate();
        this.storage = storage ?? new FileRecordingStorage();
        this.codec = codec ?? new ReplayCodec();
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public ObservationRecordingStatus Status
    {
        get
        {
            lock (gate)
            {
                return status;
            }
        }
    }

    public bool Start()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (status.State == ObservationRecordingState.Recording)
            {
                return false;
            }

            try
            {
                storage.EnsureDirectory(recordingDirectory);
                snapshots.Clear();
                lastSampleAt = null;
                lastFlushAt = null;
                lastFrame = null;
                var startedAt = utcNow();
                var fileName = $"ddcore-observation-{startedAt:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.replay.json";
                status = new ObservationRecordingStatus(
                    ObservationRecordingState.Recording,
                    new ObservationRecordingPreview(null, startedAt, null, 0, 0, 0, 0, 0, null),
                    Path.Combine(recordingDirectory, fileName),
                    null,
                    null,
                    null);
                return true;
            }
            catch (Exception exception)
            {
                Fault("recording.start-failed", exception);
                return false;
            }
        }
    }

    public void Record(WorldSnapshot snapshot, MechanicFrame frame)
    {
        lock (gate)
        {
            if (disposed || status.State != ObservationRecordingState.Recording)
            {
                return;
            }

            try
            {
                snapshot.Validate();
                if (status.Preview.SessionId is { } sessionId && sessionId != snapshot.SessionId)
                {
                    StopInternal("recording.session-changed", ObservationRecordingState.Stopped);
                    return;
                }

                var startedAt = status.Preview.StartedAt ?? snapshot.CapturedAt;
                if (snapshot.CapturedAt - startedAt > limits.MaximumDuration)
                {
                    StopInternal("recording.duration-limit", ObservationRecordingState.LimitReached);
                    return;
                }

                if (lastSampleAt is { } previous && snapshot.CapturedAt - previous < limits.MinimumSampleInterval)
                {
                    return;
                }

                if (snapshots.Count >= limits.MaximumSnapshots)
                {
                    StopInternal("recording.snapshot-limit", ObservationRecordingState.LimitReached);
                    return;
                }

                var sanitized = SnapshotPrivacyFilter.Sanitize(snapshot);
                snapshots.Add(sanitized);
                lastSampleAt = sanitized.CapturedAt;
                lastFrame = frame;
                status = status with { Preview = BuildPreview(status.Preview.StoredBytes) };

                if (lastFlushAt is null || sanitized.CapturedAt - lastFlushAt >= limits.FlushInterval)
                {
                    Flush();
                }
            }
            catch (Exception exception)
            {
                Fault("recording.capture-failed", exception);
            }
        }
    }

    public bool Stop(string reason = "recording.user-stopped")
    {
        lock (gate)
        {
            if (status.State != ObservationRecordingState.Recording)
            {
                return false;
            }

            try
            {
                StopInternal(reason, ObservationRecordingState.Stopped);
                return status.State == ObservationRecordingState.Stopped;
            }
            catch (Exception exception)
            {
                Fault("recording.stop-save-failed", exception);
                return false;
            }
        }
    }

    public bool Export(bool previewConfirmed)
    {
        lock (gate)
        {
            if (!previewConfirmed
                || status.State == ObservationRecordingState.Recording
                || string.IsNullOrWhiteSpace(status.RecordingPath)
                || !storage.FileExists(status.RecordingPath))
            {
                return false;
            }

            try
            {
                var bytes = storage.ReadAllBytes(status.RecordingPath);
                _ = codec.Deserialize(bytes);
                storage.EnsureDirectory(exportDirectory);
                var exportPath = status.ExportPath
                    ?? Path.Combine(exportDirectory, $"ddcore-export-{utcNow():yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.replay.json");
                storage.WriteAtomically(exportPath, bytes);
                status = status with { ExportPath = exportPath, LatestError = null };
                return true;
            }
            catch (Exception exception)
            {
                status = status with { LatestError = $"recording.export-failed:{exception.GetType().Name}" };
                return false;
            }
        }
    }

    public bool Delete(bool deletionConfirmed)
    {
        lock (gate)
        {
            if (!deletionConfirmed || status.State == ObservationRecordingState.Recording)
            {
                return false;
            }

            try
            {
                if (status.RecordingPath is { } recordingPath && storage.FileExists(recordingPath))
                {
                    storage.DeleteFile(recordingPath);
                }

                if (status.ExportPath is { } exportPath && storage.FileExists(exportPath))
                {
                    storage.DeleteFile(exportPath);
                }

                snapshots.Clear();
                lastSampleAt = null;
                lastFlushAt = null;
                lastFrame = null;
                status = ObservationRecordingStatus.Off;
                return true;
            }
            catch (Exception exception)
            {
                status = status with { LatestError = $"recording.delete-failed:{exception.GetType().Name}" };
                return false;
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            if (status.State == ObservationRecordingState.Recording)
            {
                try
                {
                    StopInternal("recording.plugin-disposed", ObservationRecordingState.Stopped);
                }
                catch (Exception exception)
                {
                    Fault("recording.dispose-save-failed", exception);
                }
            }

            disposed = true;
        }
    }

    private void StopInternal(string reason, ObservationRecordingState finalState)
    {
        if (snapshots.Count > 0)
        {
            Flush();
            if (status.State == ObservationRecordingState.LimitReached)
            {
                return;
            }
        }

        status = status with { State = finalState, StopReason = reason };
    }

    private void Flush()
    {
        if (snapshots.Count == 0 || status.RecordingPath is null)
        {
            return;
        }

        var document = ReplayDocument.Create(
            status.Preview.StartedAt ?? snapshots[0].CapturedAt,
            snapshots[0].SessionId,
            producerVersion,
            snapshots,
            ReplayUserIntent.ExplicitCapture);
        var bytes = codec.Serialize(document, ReplayUserIntent.ExplicitCaptureAndExport);
        if (bytes.Length > limits.MaximumBytes)
        {
            snapshots.RemoveAt(snapshots.Count - 1);
            if (snapshots.Count > 0)
            {
                Flush();
            }

            status = status with
            {
                State = ObservationRecordingState.LimitReached,
                StopReason = "recording.byte-limit",
                Preview = BuildPreview(status.Preview.StoredBytes),
            };
            return;
        }

        storage.WriteAtomically(status.RecordingPath, bytes);
        lastFlushAt = snapshots[^1].CapturedAt;
        status = status with { Preview = BuildPreview(bytes.Length), LatestError = null };
    }

    private ObservationRecordingPreview BuildPreview(long storedBytes)
    {
        var latest = snapshots.Count == 0 ? null : snapshots[^1];
        return new ObservationRecordingPreview(
            latest?.SessionId,
            status.Preview.StartedAt,
            latest?.CapturedAt,
            snapshots.Count,
            storedBytes,
            latest?.Actors.Count(actor => actor.Kind == ActorKind.Enemy) ?? 0,
            latest?.Actors.Count(actor => actor.Kind == ActorKind.Enemy && actor.Cast?.Lifecycle == CastLifecycle.Casting) ?? 0,
            lastFrame?.Hazards.Length ?? 0,
            lastFrame?.Unsupported.FirstOrDefault()?.ReasonCode);
    }

    private void Fault(string code, Exception exception)
    {
        status = status with
        {
            State = ObservationRecordingState.Faulted,
            StopReason = code,
            LatestError = $"{code}:{exception.GetType().Name}",
        };
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}

public static class SnapshotPrivacyFilter
{
    public static WorldSnapshot Sanitize(WorldSnapshot snapshot)
    {
        snapshot.Validate();
        var keptActors = snapshot.Actors
            .Where(actor => actor.Kind is ActorKind.Enemy or ActorKind.EventObject)
            .ToImmutableArray();
        var keptIdentities = keptActors.Select(actor => actor.Identity).ToHashSet();
        var sanitizedActors = keptActors.Select(actor => actor with
        {
            Target = actor.Target is { } target && keptIdentities.Contains(target) ? target : null,
            Cast = actor.Cast is null
                ? null
                : actor.Cast with
                {
                    Target = actor.Cast.Target is { } castTarget && keptIdentities.Contains(castTarget) ? castTarget : null,
                },
            Statuses = actor.Statuses.Select(status => status with
            {
                Source = status.Source is { } source && keptIdentities.Contains(source) ? source : null,
            }).ToImmutableArray(),
        }).ToImmutableArray();

        return new WorldSnapshot(
            snapshot.SchemaVersion,
            snapshot.Sequence,
            snapshot.CapturedAt,
            snapshot.SessionId,
            snapshot.Source,
            snapshot.Quality,
            snapshot.Dungeon,
            sanitizedActors,
            snapshot.Diagnostics);
    }
}
