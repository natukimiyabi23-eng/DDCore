using DDCore.Core.Provenance;
using DDCore.Core.Schema;
using System.Text;

namespace DDCore.Core.Tests;

public sealed class ProvenanceTests
{
    [Fact]
    public void CompleteEntryIsAccepted()
    {
        var entry = new SourceProvenance(
            SchemaVersions.Provenance,
            "source-id",
            new Uri("https://example.invalid/repository"),
            new string('a', 40),
            "source.cs",
            "BSD-3-Clause",
            "Copyright holder",
            DerivationKind.ReimplementedFromBehavior,
            "destination.cs",
            "Behavioral reimplementation.");

        entry.Validate();
    }

    [Fact]
    public void UnknownSchemaIsRejected()
    {
        var entry = new SourceProvenance(
            999,
            "source-id",
            new Uri("https://example.invalid/repository"),
            "commit",
            "source.cs",
            "BSD-3-Clause",
            "Copyright holder",
            DerivationKind.ReferenceOnly,
            "destination.cs",
            string.Empty);

        Assert.Throws<InvalidDataException>(entry.Validate);
    }

    [Fact]
    public void MissingRequiredFieldIsRejected()
    {
        var entry = new SourceProvenance(
            SchemaVersions.Provenance,
            string.Empty,
            new Uri("https://example.invalid/repository"),
            "commit",
            "source.cs",
            "BSD-3-Clause",
            "Copyright holder",
            DerivationKind.ReferenceOnly,
            "destination.cs",
            string.Empty);

        Assert.Throws<InvalidDataException>(entry.Validate);
    }

    [Fact]
    public void ManifestCodecValidatesActualSchemaAndEnumNames()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "entries": [{
                "schemaVersion": 1,
                "id": "bossmod-source",
                "repository": "https://github.com/awgil/ffxiv_bossmod",
                "commit": "6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0",
                "sourcePath": "source.cs",
                "license": "BSD-3-Clause",
                "copyright": "Copyright holder",
                "derivation": "reimplementedFromBehavior",
                "destination": "destination.cs",
                "notes": "test"
              }]
            }
            """;

        var manifest = new ProvenanceManifestCodec().Deserialize(Encoding.UTF8.GetBytes(json));
        Assert.Equal(DerivationKind.ReimplementedFromBehavior, Assert.Single(manifest.Entries).Derivation);
    }

    [Fact]
    public void DuplicateProvenanceIdsAreRejected()
    {
        var entry = new SourceProvenance(
            SchemaVersions.Provenance,
            "duplicate",
            new Uri("https://example.invalid/repository"),
            "commit",
            "source.cs",
            "BSD-3-Clause",
            "Copyright holder",
            DerivationKind.ReferenceOnly,
            "destination.cs",
            string.Empty);
        var manifest = new ProvenanceManifest(SchemaVersions.Provenance, [entry, entry]);

        Assert.Throws<InvalidDataException>(manifest.Validate);
    }

    [Fact]
    public void RepositoryPhase3ManifestIsValid()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "bossmod-phase3.json");
        var manifest = new ProvenanceManifestCodec().Deserialize(File.ReadAllBytes(path));
        Assert.Equal(3, manifest.Entries.Length);
        var entry = Assert.Single(manifest.Entries, item => item.Id == "bossmod-eo-d10-gancanagh");

        Assert.Equal("bossmod-eo-d10-gancanagh", entry.Id);
        Assert.Equal("6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0", entry.Commit);
        Assert.Equal("BSD-3-Clause", entry.License);
        Assert.All(manifest.Entries, item => Assert.Equal("6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0", item.Commit));
    }
}
