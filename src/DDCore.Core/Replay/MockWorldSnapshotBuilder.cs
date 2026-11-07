using System.Collections.Immutable;
using DDCore.Core.Model;
using DDCore.Core.Schema;
using DDCore.Core.World;

namespace DDCore.Core.Replay;

public sealed class MockWorldSnapshotBuilder
{
    private long _sequence;
    private DateTimeOffset _capturedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private Guid _sessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private ObservationQuality _quality = ObservationQuality.Complete;
    private DeepDungeonContext _dungeon = new(DeepDungeonKind.EurekaOrthos, 10, 0, 897, "eureka-orthos-10", null);
    private readonly List<ActorSnapshot> _actors = [];
    private readonly List<string> _diagnostics = [];

    public MockWorldSnapshotBuilder WithSequence(long sequence)
    {
        _sequence = sequence;
        return this;
    }

    public MockWorldSnapshotBuilder At(DateTimeOffset capturedAt)
    {
        _capturedAt = capturedAt;
        return this;
    }

    public MockWorldSnapshotBuilder InSession(Guid sessionId)
    {
        _sessionId = sessionId;
        return this;
    }

    public MockWorldSnapshotBuilder WithQuality(ObservationQuality quality, params string[] diagnostics)
    {
        _quality = quality;
        _diagnostics.Clear();
        _diagnostics.AddRange(diagnostics);
        return this;
    }

    public MockWorldSnapshotBuilder InDungeon(DeepDungeonContext dungeon)
    {
        _dungeon = dungeon;
        return this;
    }

    public MockWorldSnapshotBuilder AddActor(ActorSnapshot actor)
    {
        _actors.Add(actor);
        return this;
    }

    public WorldSnapshot Build() =>
        new(
            SchemaVersions.WorldSnapshot,
            _sequence,
            _capturedAt,
            _sessionId,
            ObservationSource.Mock,
            _quality,
            _dungeon,
            _actors.ToImmutableArray(),
            _diagnostics.ToImmutableArray());
}
