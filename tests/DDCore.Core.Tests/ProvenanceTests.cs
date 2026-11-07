using DDCore.Core.Provenance;
using DDCore.Core.Schema;

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
}
