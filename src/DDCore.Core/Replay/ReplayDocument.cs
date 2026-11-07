using System.Collections.Immutable;
using DDCore.Core.Schema;
using DDCore.Core.World;

namespace DDCore.Core.Replay;

public sealed record ReplayUserIntent(bool CaptureRequested, bool ExportRequested)
{
    public static ReplayUserIntent ExplicitCapture => new(true, false);

    public static ReplayUserIntent ExplicitCaptureAndExport => new(true, true);
}

public sealed record ReplayDocument(
    int SchemaVersion,
    DateTimeOffset CreatedAtUtc,
    Guid SessionId,
    string ProducerVersion,
    ImmutableArray<WorldSnapshot> Snapshots)
{
    public static ReplayDocument Create(
        DateTimeOffset createdAtUtc,
        Guid sessionId,
        string producerVersion,
        IEnumerable<WorldSnapshot> snapshots,
        ReplayUserIntent intent)
    {
        if (!intent.CaptureRequested)
        {
            throw new InvalidOperationException("Replay capture requires an explicit user request.");
        }

        return new ReplayDocument(
            SchemaVersions.Replay,
            createdAtUtc,
            sessionId,
            producerVersion,
            snapshots.ToImmutableArray());
    }
}
