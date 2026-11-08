using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using DDCore.Core.Schema;
using DDCore.Core.World;

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
            using var parsed = JsonDocument.Parse(utf8Json.ToArray(), new JsonDocumentOptions { MaxDepth = Options.MaxDepth });
            RejectDuplicateProperties(parsed.RootElement);
            if (!parsed.RootElement.TryGetProperty("schemaVersion", out var schemaElement)
                || !schemaElement.TryGetInt32(out var schemaVersion))
            {
                throw new InvalidDataException("Replay schema version is missing or invalid.");
            }

            ReplayDocument document;
            if (schemaVersion is SchemaVersions.LegacyReplayV3 or SchemaVersions.LegacyReplayV4)
            {
                var legacy = JsonSerializer.Deserialize<LegacyReplayDocument>(utf8Json, Options)
                    ?? throw new InvalidDataException("Legacy replay JSON produced no document.");
                document = MigrateLegacy(legacy);
            }
            else if (schemaVersion == SchemaVersions.Replay)
            {
                document = JsonSerializer.Deserialize<ReplayDocument>(utf8Json, Options)
                    ?? throw new InvalidDataException("Replay JSON produced no document.");
            }
            else
            {
                throw new InvalidDataException($"Replay schema {schemaVersion} is unsupported.");
            }

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
        if (document.SchemaVersion != SchemaVersions.Replay
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
                || snapshot.SchemaVersion != SchemaVersions.WorldSnapshot
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

    private static ReplayDocument MigrateLegacy(LegacyReplayDocument legacy)
    {
        ValidateLegacy(legacy);
        var snapshots = legacy.Snapshots.Select(snapshot =>
        {
            var actors = snapshot.Actors.Select(actor =>
            {
                const string reason = "actor.spatial.legacy-unqualified";
                var metadata = new ObservationMetadata(
                    ObservationValueSource.Replay,
                    ObservationValueQuality.LegacyUnqualified,
                    reason);
                var spatial = ActorSpatialObservation.Quarantined(actor.Position, actor.Rotation, metadata, reason);
                CastSnapshot? cast = null;
                if (actor.Cast is { } legacyCast)
                {
                    var targetMetadata = legacyCast.TargetLocation is null
                        ? null
                        : new ObservationMetadata(
                            ObservationValueSource.Replay,
                            ObservationValueQuality.LegacyUnqualified,
                            "cast.target-location.legacy-unqualified");
                    cast = new CastSnapshot(
                        legacyCast.ActionId,
                        legacyCast.Lifecycle,
                        legacyCast.StartedAt,
                        legacyCast.FinishesAt,
                        legacyCast.Rotation,
                        legacyCast.Target,
                        legacyCast.TargetLocation,
                        targetMetadata,
                        legacyCast.TargetLocation is null ? SpatialUsability.Missing : SpatialUsability.Quarantined,
                        legacyCast.TargetLocation is null ? null : "cast.target-location.legacy-unqualified",
                        SpatialUsability.Quarantined,
                        "cast.rotation.legacy-unqualified");
                }

                return new ActorSnapshot(
                    actor.Identity,
                    actor.ObjectId,
                    actor.Kind,
                    spatial,
                    actor.HitboxRadius,
                    actor.CurrentHp,
                    actor.MaximumHp,
                    actor.IsDead,
                    actor.IsDestroyed,
                    actor.IsInCombat,
                    actor.Target,
                    cast,
                    actor.Statuses);
            }).ToImmutableArray();

            return new WorldSnapshot(
                SchemaVersions.WorldSnapshot,
                snapshot.Sequence,
                snapshot.CapturedAt,
                snapshot.SessionId,
                snapshot.Source,
                snapshot.Quality,
                snapshot.Dungeon,
                actors,
                snapshot.Diagnostics,
                snapshot.PlayerPose);
        }).ToImmutableArray();

        return new ReplayDocument(
            SchemaVersions.Replay,
            legacy.CreatedAtUtc,
            legacy.SessionId,
            legacy.ProducerVersion,
            snapshots);
    }

    private static void ValidateLegacy(LegacyReplayDocument document)
    {
        var expectedSnapshotSchema = document.SchemaVersion switch
        {
            SchemaVersions.LegacyReplayV3 => SchemaVersions.LegacyWorldSnapshotV3,
            SchemaVersions.LegacyReplayV4 => SchemaVersions.LegacyWorldSnapshotV4,
            _ => throw new InvalidDataException("Legacy replay schema is unsupported."),
        };
        if (document.CreatedAtUtc == default
            || document.SessionId == Guid.Empty
            || string.IsNullOrWhiteSpace(document.ProducerVersion)
            || document.ProducerVersion.Length > 128
            || document.Snapshots.IsDefault
            || document.Snapshots.Length is 0 or > MaximumSnapshots)
        {
            throw new InvalidDataException("Legacy replay header or snapshot count is invalid.");
        }

        long? previousSequence = null;
        DateTimeOffset? previousTime = null;
        foreach (var snapshot in document.Snapshots)
        {
            if (snapshot.SchemaVersion != expectedSnapshotSchema
                || snapshot.Sequence < 0
                || snapshot.CapturedAt == default
                || snapshot.SessionId != document.SessionId
                || snapshot.Actors.IsDefault
                || snapshot.Diagnostics.IsDefault
                || snapshot.Actors.Length > MaximumActorsPerSnapshot
                || snapshot.Diagnostics.Length > MaximumDiagnosticsPerSnapshot
                || snapshot.Diagnostics.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > MaximumDiagnosticLength)
                || snapshot.Actors.Any(actor => actor.Statuses.IsDefault || actor.Statuses.Length > MaximumStatusesPerActor)
                || (previousSequence is not null && snapshot.Sequence <= previousSequence)
                || (previousTime is not null && snapshot.CapturedAt < previousTime))
            {
                throw new InvalidDataException("Legacy replay snapshot order, session, schema, or collection limits are invalid.");
            }

            snapshot.Dungeon.Validate();
            previousSequence = snapshot.Sequence;
            previousTime = snapshot.CapturedAt;
        }
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException($"Replay JSON contains duplicate property '{property.Name}'.");
                }

                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                RejectDuplicateProperties(item);
            }
        }
    }
}
