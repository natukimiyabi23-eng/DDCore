using System.Collections.Immutable;
using DDCore.Core.Geometry;
using DDCore.Core.Model;
using DDCore.Core.World;

namespace DDCore.Core.Mechanics;

public sealed record CastHazardDefinition(
    uint ActionId,
    AoeShape Shape,
    HazardSeverity Severity,
    string ReasonCode,
    int MaximumInstances = int.MaxValue)
{
    public void Validate()
    {
        if (ActionId == 0 || string.IsNullOrWhiteSpace(ReasonCode) || MaximumInstances <= 0)
        {
            throw new InvalidDataException("Cast hazard definition is invalid.");
        }
    }
}

public static class CastHazardTracker
{
    public static ImmutableDictionary<string, HazardInstance> Reconcile(
        WorldSnapshot snapshot,
        string analyzerId,
        IReadOnlyDictionary<uint, CastHazardDefinition> definitions,
        Func<ActorSnapshot, bool> actorFilter)
    {
        var candidates = snapshot.Actors
            .Where(actor => !actor.IsDead && !actor.IsDestroyed && actorFilter(actor) && actor.Cast is not null)
            .Where(actor => actor.Cast!.Lifecycle == CastLifecycle.Casting && actor.Cast.FinishesAt > snapshot.CapturedAt)
            .Where(actor => definitions.ContainsKey(actor.Cast!.ActionId))
            .OrderBy(actor => actor.Cast!.ActionId)
            .ThenBy(actor => actor.Identity.EntityId)
            .ThenBy(actor => actor.Identity.Generation)
            .ToArray();

        var result = ImmutableDictionary.CreateBuilder<string, HazardInstance>(StringComparer.Ordinal);
        foreach (var actionGroup in candidates.GroupBy(actor => actor.Cast!.ActionId))
        {
            var definition = definitions[actionGroup.Key];
            definition.Validate();
            foreach (var actor in actionGroup.Take(definition.MaximumInstances))
            {
                var cast = actor.Cast!;
                var hazardId = $"{analyzerId}:{actor.Identity.EntityId:X}:{actor.Identity.Generation}:{cast.ActionId}:{cast.StartedAt.UtcTicks}";
                var hazard = new HazardInstance(
                    hazardId,
                    analyzerId,
                    definition.Shape,
                    actor.Position,
                    actor.Rotation,
                    snapshot.CapturedAt,
                    cast.FinishesAt,
                    cast.FinishesAt,
                    definition.Severity,
                    SupportState.Supported,
                    new HazardEvidence(cast.ActionId, actor.Identity, definition.ReasonCode));
                hazard.Validate();
                result.Add(hazardId, hazard);
            }
        }

        return result.ToImmutable();
    }
}
