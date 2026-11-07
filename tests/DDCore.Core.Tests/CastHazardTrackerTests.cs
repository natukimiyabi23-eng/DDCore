using System.Collections.Immutable;
using DDCore.Core.Geometry;
using DDCore.Core.Mechanics;
using DDCore.Core.Replay;
using DDCore.Core.World;

namespace DDCore.Core.Tests;

public sealed class CastHazardTrackerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 7, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PersistentHazardSurvivesCastCompletionUntilEvidenceBasedExpiry()
    {
        var definition = new CastHazardDefinition(
            100,
            new CircleAoe(5),
            HazardSeverity.Dangerous,
            "test.persistent",
            Lifetime: HazardLifetimeKind.Persistent,
            PersistentDuration: TimeSpan.FromSeconds(3));
        var definitions = new Dictionary<uint, CastHazardDefinition> { [100] = definition };
        var casting = Actor(CastLifecycle.Casting);
        var first = CastHazardTracker.Reconcile(Snapshot(1, Start, casting), "test", definitions, _ => true, ImmutableDictionary<string, HazardInstance>.Empty);
        var completed = casting with { Cast = casting.Cast! with { Lifecycle = CastLifecycle.Completed } };
        var second = CastHazardTracker.Reconcile(Snapshot(2, Start.AddSeconds(3), completed), "test", definitions, _ => true, first.Hazards);
        var third = CastHazardTracker.Reconcile(Snapshot(3, Start.AddSeconds(5), completed), "test", definitions, _ => true, second.Hazards);

        var hazard = Assert.Single(first.Hazards.Values);
        Assert.Equal(HazardLifetimeKind.Persistent, hazard.Lifetime);
        Assert.Equal(Start.AddSeconds(5), hazard.ExpiresAt);
        Assert.Single(second.Hazards);
        Assert.Empty(third.Hazards);
    }

    [Fact]
    public void LifetimeDefinitionRequiresEvidenceBasedDuration()
    {
        Assert.Throws<InvalidDataException>(() => new CastHazardDefinition(
            100,
            new CircleAoe(5),
            HazardSeverity.Dangerous,
            "test",
            Lifetime: HazardLifetimeKind.Persistent).Validate());
        Assert.Throws<InvalidDataException>(() => new CastHazardDefinition(
            100,
            new CircleAoe(5),
            HazardSeverity.Dangerous,
            "test",
            PersistentDuration: TimeSpan.FromSeconds(1)).Validate());
    }

    private static WorldSnapshot Snapshot(long sequence, DateTimeOffset at, ActorSnapshot actor) =>
        new MockWorldSnapshotBuilder()
            .WithSequence(sequence)
            .At(at)
            .AddActor(actor)
            .Build();

    private static ActorSnapshot Actor(CastLifecycle lifecycle) =>
        new(
            new ActorIdentity(1, 0),
            1,
            ActorKind.Enemy,
            new Coordinate2D(0, 0),
            0,
            1,
            100,
            100,
            false,
            false,
            true,
            null,
            new CastSnapshot(100, lifecycle, Start, Start.AddSeconds(2), 0, null, null),
            ImmutableArray<StatusSnapshot>.Empty);
}
