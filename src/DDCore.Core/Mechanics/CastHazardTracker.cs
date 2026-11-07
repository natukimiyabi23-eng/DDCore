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
    int MaximumInstances = int.MaxValue,
    HazardLifetimeKind Lifetime = HazardLifetimeKind.Instantaneous,
    TimeSpan? PersistentDuration = null)
{
    public void Validate()
    {
        if (ActionId == 0
            || Shape is null
            || string.IsNullOrWhiteSpace(ReasonCode)
            || MaximumInstances <= 0
            || !Enum.IsDefined(Lifetime)
            || (Lifetime == HazardLifetimeKind.Instantaneous && PersistentDuration is not null)
            || (Lifetime == HazardLifetimeKind.Persistent && PersistentDuration is null)
            || (PersistentDuration is { } configuredDuration && configuredDuration <= TimeSpan.Zero))
        {
            throw new InvalidDataException("Cast hazard definition is invalid.");
        }
    }
}

public sealed record CastHazardReconciliation(
    ImmutableDictionary<string, HazardInstance> Hazards,
    ImmutableArray<string> Diagnostics,
    bool ObservationUncertain);

public static class CastHazardTracker
{
    public static CastHazardReconciliation Reconcile(
        WorldSnapshot snapshot,
        string analyzerId,
        IReadOnlyDictionary<uint, CastHazardDefinition> definitions,
        Func<ActorSnapshot, bool> actorFilter,
        IReadOnlyDictionary<string, HazardInstance> previousHazards)
    {
        var candidates = snapshot.Actors
            .Where(actor => !actor.IsDead && !actor.IsDestroyed && actorFilter(actor) && actor.Cast is not null)
            .Where(actor => actor.Cast!.Lifecycle == CastLifecycle.Casting && actor.Cast.FinishesAt > snapshot.CapturedAt)
            .Where(actor => definitions.ContainsKey(actor.Cast!.ActionId))
            .ToArray();

        var result = ImmutableDictionary.CreateBuilder<string, HazardInstance>(StringComparer.Ordinal);
        var diagnostics = ImmutableArray.CreateBuilder<string>();
        var observationUncertain = false;
        foreach (var actionGroup in candidates.GroupBy(actor => actor.Cast!.ActionId))
        {
            var definition = definitions[actionGroup.Key];
            definition.Validate();
            foreach (var actor in actionGroup
                .OrderBy(candidate => candidate.Cast!.StartedAt)
                .ThenBy(candidate => candidate.Cast!.FinishesAt)
                .ThenBy(candidate => candidate.Identity.EntityId)
                .ThenBy(candidate => candidate.Identity.Generation)
                .Take(definition.MaximumInstances))
            {
                var cast = actor.Cast!;
                var hazardId = $"{analyzerId}:{actor.Identity.EntityId:X}:{actor.Identity.Generation}:{cast.ActionId}:{cast.StartedAt.UtcTicks}";
                var expiresAt = definition.Lifetime == HazardLifetimeKind.Instantaneous
                    ? cast.FinishesAt
                    : cast.FinishesAt + definition.PersistentDuration!.Value;
                var hazard = new HazardInstance(
                    hazardId,
                    analyzerId,
                    definition.Shape,
                    cast.TargetLocation ?? actor.Position,
                    cast.Rotation,
                    snapshot.CapturedAt,
                    cast.FinishesAt,
                    expiresAt,
                    definition.Lifetime,
                    definition.Severity,
                    SupportState.Supported,
                    new HazardEvidence(cast.ActionId, actor.Identity, definition.ReasonCode));
                hazard.Validate();
                result.Add(hazardId, hazard);
            }
        }

        var actorsByIdentity = snapshot.Actors.ToDictionary(actor => actor.Identity);
        foreach (var previous in previousHazards.Values.OrderBy(hazard => hazard.HazardId, StringComparer.Ordinal))
        {
            if (result.ContainsKey(previous.HazardId)
                || previous.ExpiresAt is { } expiry && snapshot.CapturedAt >= expiry)
            {
                continue;
            }

            if (previous.Lifetime == HazardLifetimeKind.Persistent)
            {
                result.Add(previous.HazardId, previous);
                continue;
            }

            if (actorsByIdentity.ContainsKey(previous.Evidence.Source))
            {
                continue;
            }

            result.Add(previous.HazardId, previous);
            observationUncertain = true;
            diagnostics.Add($"cast-hazard.source-missing:{previous.Evidence.Source.EntityId:X}:{previous.Evidence.Source.Generation}");
        }

        return new(
            result.ToImmutable(),
            diagnostics.Order(StringComparer.Ordinal).ToImmutableArray(),
            observationUncertain);
    }
}
