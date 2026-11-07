using DDCore.Core.Schema;

namespace DDCore.Core.Provenance;

public enum DerivationKind
{
    ReferenceOnly,
    ReimplementedFromBehavior,
    Adapted,
    Copied,
}

public sealed record SourceProvenance(
    int SchemaVersion,
    string Id,
    Uri Repository,
    string Commit,
    string SourcePath,
    string License,
    string Copyright,
    DerivationKind Derivation,
    string Destination,
    string Notes)
{
    public void Validate()
    {
        if (SchemaVersion != SchemaVersions.Provenance)
        {
            throw new InvalidDataException($"Unsupported provenance schema version: {SchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(Id)
            || !Repository.IsAbsoluteUri
            || string.IsNullOrWhiteSpace(Commit)
            || string.IsNullOrWhiteSpace(SourcePath)
            || string.IsNullOrWhiteSpace(License)
            || string.IsNullOrWhiteSpace(Copyright)
            || string.IsNullOrWhiteSpace(Destination))
        {
            throw new InvalidDataException("Provenance entries require an ID, absolute repository URI, commit, source, license, copyright, and destination.");
        }
    }
}
