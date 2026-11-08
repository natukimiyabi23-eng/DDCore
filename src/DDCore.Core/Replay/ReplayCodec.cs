using System.Text.Json;
using System.Text.Json.Serialization;
using DDCore.Core.Schema;

namespace DDCore.Core.Replay;

public sealed class ReplayCodec
{
    public const int MaximumDocumentBytes = 4 * 1024 * 1024;
    public const int MaximumSnapshots = 10_000;
    public const int MaximumActorsPerSnapshot = 512;
    public const int MaximumStatusesPerActor = 128;
    public const int MaximumDiagnosticsPerSnapshot = 64;
    public const int MaximumDiagnosticLength = 512;

    private static readonly JsonSerializerOptions Options = new()
    {
        MaxDepth = 64,
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public byte[] Serialize(ReplayDocument document, ReplayUserIntent intent)
    {
        if (!intent.ExportRequested)
        {
            throw new InvalidOperationException("Replay export requires an explicit user request.");
        }

        Validate(document);
        var result = JsonSerializer.SerializeToUtf8Bytes(document, Options);
        if (result.Length > MaximumDocumentBytes)
        {
            throw new InvalidDataException($"Replay exceeds the {MaximumDocumentBytes}-byte limit.");
        }

        return result;
    }

    public ReplayDocument Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.Length == 0 || utf8Json.Length > MaximumDocumentBytes)
        {
            throw new InvalidDataException("Replay is empty or exceeds the document size limit.");
        }

        try
        {
            var document = JsonSerializer.Deserialize<ReplayDocument>(utf8Json, Options)
                ?? throw new InvalidDataException("Replay JSON produced no document.");
            Validate(document);
            return document;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Replay JSON is malformed or contains unsupported fields.", exception);
        }
    }

    public void Validate(ReplayDocument document)
    {
        if (document.SchemaVersion is not (SchemaVersions.LegacyReplay or SchemaVersions.Replay)
            || document.CreatedAtUtc == default
            || document.SessionId == Guid.Empty
            || string.IsNullOrWhiteSpace(document.ProducerVersion)
            || document.ProducerVersion.Length > 128
            || document.Snapshots.IsDefault
            || document.Snapshots.Length is 0 or > MaximumSnapshots)
        {
            throw new InvalidDataException("Replay header, version, or snapshot count is invalid.");
        }

        long? previousSequence = null;
        DateTimeOffset? previousTime = null;
        foreach (var snapshot in document.Snapshots)
        {
            snapshot.Validate();
            if (snapshot.SessionId != document.SessionId
                || (document.SchemaVersion == SchemaVersions.LegacyReplay && snapshot.SchemaVersion != SchemaVersions.LegacyWorldSnapshot)
                || (document.SchemaVersion == SchemaVersions.Replay && snapshot.SchemaVersion != SchemaVersions.WorldSnapshot)
                || (previousSequence is not null && snapshot.Sequence <= previousSequence)
                || (previousTime is not null && snapshot.CapturedAt < previousTime)
                || snapshot.Actors.Length > MaximumActorsPerSnapshot
                || snapshot.Diagnostics.Length > MaximumDiagnosticsPerSnapshot
                || snapshot.Diagnostics.Any(value => value.Length > MaximumDiagnosticLength)
                || snapshot.Actors.Any(actor => actor.Statuses.Length > MaximumStatusesPerActor))
            {
                throw new InvalidDataException("Replay snapshot order, session, or collection limits are invalid.");
            }

            previousSequence = snapshot.Sequence;
            previousTime = snapshot.CapturedAt;
        }
    }
}
