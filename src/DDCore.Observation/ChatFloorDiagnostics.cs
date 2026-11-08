using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DDCore.Core.World;

namespace DDCore.Observation;

public enum ChatFloorEventSurface
{
    LogMessage,
    ChatMessage,
}

public enum ChatFloorCandidateQuality
{
    Missing,
    Unverified,
    ConsistentCandidate,
    Conflict,
}

public enum ChatFloorRecordingState
{
    Off,
    Recording,
    Stopped,
    LimitReached,
    Faulted,
}

public sealed record ChatFloorRawEvent(
    DateTimeOffset ObservedAtUtc,
    long Sequence,
    ChatFloorEventSurface Surface,
    uint? LogMessageId,
    ushort? LogKind,
    ImmutableArray<int> IntegerParameters,
    int? ParsedJapaneseFloor);

public sealed record ChatFloorContext(
    bool IsDeepDungeon,
    DeepDungeonKind DungeonKind,
    DeepDungeonFloorRange? FloorRange);

public sealed record ChatFloorDiagnosticEntry(
    DateTimeOffset ObservedAtUtc,
    long Sequence,
    ChatFloorEventSurface Surface,
    uint? LogMessageId,
    ushort? LogKind,
    ImmutableArray<int> IntegerParameters,
    int? CandidateFloor,
    ChatFloorCandidateQuality Quality,
    string ReasonCode,
    DeepDungeonKind DungeonKind,
    DeepDungeonFloorRange? FloorRange);

public sealed record ChatFloorRecordingLimits(int MaximumBytes, int MaximumEntries, TimeSpan MaximumDuration)
{
    public static ChatFloorRecordingLimits Default { get; } = new(256 * 1024, 256, TimeSpan.FromMinutes(15));

    public void Validate()
    {
        if (MaximumBytes <= 0 || MaximumBytes > 4 * 1024 * 1024 || MaximumEntries <= 0 || MaximumEntries > 4096 || MaximumDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ChatFloorRecordingLimits));
        }
    }
}

public sealed record ChatFloorRecordingStatus(
    ChatFloorRecordingState State,
    int EntryCount,
    long StoredBytes,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? LastObservedAtUtc,
    ChatFloorDiagnosticEntry? Latest,
    string? RecordingPath,
    string? ExportPath,
    string? StopReason,
    string? LatestError)
{
    public static ChatFloorRecordingStatus Off { get; } = new(
        ChatFloorRecordingState.Off, 0, 0, null, null, null, null, null, null, null);
}

public sealed record ChatFloorDiagnosticDocument(
    int SchemaVersion,
    string ProducerVersion,
    DateTimeOffset CreatedAtUtc,
    ImmutableArray<ChatFloorDiagnosticEntry> Entries);

public static partial class ChatFloorEventAnalyzer
{
    public const ushort SystemMessageLogKind = 57;
    public const uint TransferExecutedLogMessageId = 7248;
    public const uint DeepDungeonFloorLogMessageId = 7270;
    public const uint LayerLogMessageId = 9218;
    public const int MaximumIntegerParameters = 8;

    [GeneratedRegex("^地下([1-9][0-9]{0,2})階$", RegexOptions.CultureInvariant)]
    private static partial Regex JapaneseFloorRegex();

    public static bool IsCandidateLogMessage(uint id) =>
        id is TransferExecutedLogMessageId or DeepDungeonFloorLogMessageId or LayerLogMessageId;

    public static int? ParseJapaneseFloor(string text)
    {
        if (text is null)
        {
            return null;
        }

        var match = JapaneseFloorRegex().Match(text);
        return match.Success && int.TryParse(match.Groups[1].Value, out var floor) && floor <= 200
            ? floor
            : null;
    }

    public static ChatFloorDiagnosticEntry Analyze(ChatFloorRawEvent observed, ChatFloorContext context)
    {
        var candidate = observed.ParsedJapaneseFloor;
        var reason = "chat-floor.candidate-unresolved";
        if (observed.Surface == ChatFloorEventSurface.LogMessage)
        {
            if (observed.LogMessageId == TransferExecutedLogMessageId)
            {
                reason = "chat-floor.transition-marker-unverified";
            }
            else if (observed.LogMessageId is DeepDungeonFloorLogMessageId or LayerLogMessageId)
            {
                var plausible = observed.IntegerParameters.Where(static value => value is >= 1 and <= 200).Distinct().ToArray();
                if (plausible.Length == 1)
                {
                    candidate = plausible[0];
                    reason = "chat-floor.log-parameter-unverified";
                }
                else
                {
                    reason = plausible.Length == 0
                        ? "chat-floor.log-parameter-missing"
                        : "chat-floor.log-parameter-ambiguous";
                }
            }
        }

        var quality = candidate is null ? ChatFloorCandidateQuality.Missing : ChatFloorCandidateQuality.Unverified;
        if (candidate is not null && context.IsDeepDungeon && context.FloorRange is { } range)
        {
            if (candidate >= range.Start && candidate <= range.End)
            {
                quality = ChatFloorCandidateQuality.ConsistentCandidate;
                reason = "chat-floor.candidate-range-consistent-unverified";
            }
            else
            {
                quality = ChatFloorCandidateQuality.Conflict;
                reason = "chat-floor.candidate-range-conflict";
            }
        }
        else if (candidate is not null && !context.IsDeepDungeon)
        {
            quality = ChatFloorCandidateQuality.Conflict;
            reason = "chat-floor.candidate-outside-deep-dungeon";
        }

        return new ChatFloorDiagnosticEntry(
            observed.ObservedAtUtc,
            observed.Sequence,
            observed.Surface,
            observed.LogMessageId,
            observed.LogKind,
            observed.IntegerParameters.Take(MaximumIntegerParameters).ToImmutableArray(),
            candidate,
            quality,
            reason,
            context.DungeonKind,
            context.FloorRange);
    }
}

public sealed class ChatFloorDiagnosticRecorder : IDisposable
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
    private readonly ChatFloorRecordingLimits limits;
    private readonly IRecordingStorage storage;
    private readonly Func<DateTimeOffset> utcNow;
    private readonly List<ChatFloorDiagnosticEntry> entries = [];
    private ChatFloorRecordingStatus status = ChatFloorRecordingStatus.Off;
    private bool disposed;

    public ChatFloorDiagnosticRecorder(
        string recordingDirectory,
        string producerVersion,
        ChatFloorRecordingLimits? limits = null,
        IRecordingStorage? storage = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        this.recordingDirectory = Path.GetFullPath(recordingDirectory ?? throw new ArgumentNullException(nameof(recordingDirectory)));
        exportDirectory = Path.Combine(this.recordingDirectory, "exports");
        this.producerVersion = string.IsNullOrWhiteSpace(producerVersion) ? throw new ArgumentException("Producer version is required.", nameof(producerVersion)) : producerVersion;
        this.limits = limits ?? ChatFloorRecordingLimits.Default;
        this.limits.Validate();
        this.storage = storage ?? new FileRecordingStorage();
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public ChatFloorRecordingStatus Status
    {
        get { lock (gate) { return status; } }
    }

    public bool Start()
    {
        lock (gate)
        {
            if (disposed || status.State == ChatFloorRecordingState.Recording)
            {
                return false;
            }

            entries.Clear();
            var started = utcNow();
            status = new ChatFloorRecordingStatus(ChatFloorRecordingState.Recording, 0, 0, started, null, null, null, null, null, null);
            return true;
        }
    }

    public bool Record(ChatFloorRawEvent observed, ChatFloorContext context)
    {
        lock (gate)
        {
            if (disposed || status.State != ChatFloorRecordingState.Recording)
            {
                return false;
            }

            if (observed.ObservedAtUtc - status.StartedAtUtc >= limits.MaximumDuration)
            {
                StopInternal(ChatFloorRecordingState.LimitReached, "chat-floor.duration-limit");
                return false;
            }

            var entry = ChatFloorEventAnalyzer.Analyze(observed, context);
            entries.Add(entry);
            if (entries.Count > limits.MaximumEntries)
            {
                entries.RemoveAt(entries.Count - 1);
                StopInternal(ChatFloorRecordingState.LimitReached, "chat-floor.entry-limit");
                return false;
            }

            var bytes = Serialize();
            if (bytes.Length > limits.MaximumBytes)
            {
                entries.RemoveAt(entries.Count - 1);
                StopInternal(ChatFloorRecordingState.LimitReached, "chat-floor.byte-limit");
                return false;
            }

            status = status with { EntryCount = entries.Count, StoredBytes = bytes.Length, LastObservedAtUtc = entry.ObservedAtUtc, Latest = entry };
            return true;
        }
    }

    public bool Stop()
    {
        lock (gate)
        {
            if (status.State != ChatFloorRecordingState.Recording)
            {
                return false;
            }

            StopInternal(ChatFloorRecordingState.Stopped, "chat-floor.user-stopped");
            return status.State == ChatFloorRecordingState.Stopped;
        }
    }

    public bool Export(bool previewConfirmed)
    {
        lock (gate)
        {
            if (!previewConfirmed || entries.Count == 0 || status.State == ChatFloorRecordingState.Recording)
            {
                return false;
            }

            try
            {
                storage.EnsureDirectory(exportDirectory);
                var path = Path.Combine(exportDirectory, $"ddcore-chat-floor-{utcNow():yyyyMMdd-HHmmss}.diagnostic.json");
                storage.WriteAtomically(path, Serialize());
                status = status with { ExportPath = path, LatestError = null };
                return true;
            }
            catch (Exception exception)
            {
                Fault("chat-floor.export-failed", exception);
                return false;
            }
        }
    }

    public bool Delete(bool deletionConfirmed)
    {
        lock (gate)
        {
            if (!deletionConfirmed || status.State == ChatFloorRecordingState.Recording)
            {
                return false;
            }

            try
            {
                if (status.RecordingPath is { } recording && storage.FileExists(recording)) storage.DeleteFile(recording);
                if (status.ExportPath is { } export && storage.FileExists(export)) storage.DeleteFile(export);
                entries.Clear();
                status = ChatFloorRecordingStatus.Off;
                return true;
            }
            catch (Exception exception)
            {
                Fault("chat-floor.delete-failed", exception);
                return false;
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            if (status.State == ChatFloorRecordingState.Recording) StopInternal(ChatFloorRecordingState.Stopped, "chat-floor.plugin-disposed");
            disposed = true;
        }
    }

    private void StopInternal(ChatFloorRecordingState finalState, string reason)
    {
        try
        {
            storage.EnsureDirectory(recordingDirectory);
            var path = Path.Combine(recordingDirectory, $"ddcore-chat-floor-{status.StartedAtUtc:yyyyMMdd-HHmmss}.diagnostic.json");
            var bytes = Serialize();
            storage.WriteAtomically(path, bytes);
            status = status with { State = finalState, EntryCount = entries.Count, StoredBytes = bytes.Length, RecordingPath = path, StopReason = reason, LatestError = null };
        }
        catch (Exception exception)
        {
            Fault("chat-floor.save-failed", exception);
        }
    }

    private byte[] Serialize() => JsonSerializer.SerializeToUtf8Bytes(
        new ChatFloorDiagnosticDocument(1, producerVersion, status.StartedAtUtc ?? utcNow(), entries.ToImmutableArray()),
        JsonOptions);

    private void Fault(string reason, Exception exception) => status = status with
    {
        State = ChatFloorRecordingState.Faulted,
        StopReason = reason,
        LatestError = $"{reason}:{exception.GetType().Name}",
    };
}
