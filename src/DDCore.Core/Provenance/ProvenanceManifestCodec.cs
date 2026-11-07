using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using DDCore.Core.Schema;

namespace DDCore.Core.Provenance;

public sealed record ProvenanceManifest(int SchemaVersion, ImmutableArray<SourceProvenance> Entries)
{
    public void Validate()
    {
        if (SchemaVersion != SchemaVersions.Provenance || Entries.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Provenance manifest schema or entry collection is invalid.");
        }

        foreach (var entry in Entries)
        {
            entry.Validate();
        }

        if (Entries.GroupBy(entry => entry.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            throw new InvalidDataException("Provenance IDs must be unique.");
        }
    }
}

public sealed class ProvenanceManifestCodec
{
    public const int MaximumBytes = 256 * 1024;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public ProvenanceManifest Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.Length == 0 || utf8Json.Length > MaximumBytes)
        {
            throw new InvalidDataException("Provenance manifest is empty or too large.");
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<ProvenanceManifest>(utf8Json, Options)
                ?? throw new InvalidDataException("Provenance JSON produced no manifest.");
            manifest.Validate();
            return manifest;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Provenance JSON is malformed.", exception);
        }
    }
}
