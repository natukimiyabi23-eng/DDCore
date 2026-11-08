using System.Collections.Immutable;
using DDCore.Core.Geometry;
using DDCore.Core.Model;
using DDCore.Core.Schema;
using DDCore.Core.World;

namespace DDCore.Observation;

public sealed class LiveWorldSnapshotProvider(ILiveObservationSource source) : IWorldSnapshotProvider
{
    private static readonly TimeSpan CompletionTolerance = TimeSpan.FromMilliseconds(100);
    private readonly EntityGenerationTracker generations = new();
    private readonly Dictionary<ActorIdentity, CastObservationState> activeCasts = [];
    private Guid sessionId = Guid.NewGuid();
    private long sequence;
    private SessionKey? sessionKey;
    private DateTimeOffset lastCapturedAt;

    public WorldSnapshot Capture()
    {
        var raw = source.Capture();
        ValidateRawFrame(raw);
        var currentKey = new SessionKey(raw.IsLoggedIn, raw.TerritoryId, raw.ContentId);
        if (sessionKey is null || sessionKey != currentKey)
        {
            ResetSession(currentKey);
        }

        if (lastCapturedAt != default && raw.CapturedAt < lastCapturedAt)
        {
            var staleDiagnostics = raw.Diagnostics.ToBuilder();
            staleDiagnostics.Add("observation.time.regressed");
            var staleQuality = ObservationQuality.Partial;
            var staleDungeon = ResolveDungeon(raw, raw.Actors, staleDiagnostics, ref staleQuality);
            return Build(
                raw,
                ObservationQuality.Stale,
                staleDungeon,
                [],
                staleDiagnostics,
                ConvertPlayerPose(raw, ObservationValueQuality.Stale, "observation.time.regressed"));
        }

        lastCapturedAt = raw.CapturedAt;

        var diagnostics = raw.Diagnostics.ToBuilder();
        var quality = ObservationQuality.Complete;
        if (!raw.IsLoggedIn)
        {
            diagnostics.Add("observation.client.logged-out");
            return Build(raw, ObservationQuality.Unsupported, NotInDungeon(raw), [], diagnostics, ConvertPlayerPose(raw));
        }

        var playerPose = ConvertPlayerPose(raw);
        if (playerPose.Metadata.Quality != ObservationValueQuality.Direct)
        {
            quality = ObservationQuality.Partial;
        }

        var identities = new Dictionary<uint, ActorIdentity>();
        foreach (var actor in raw.Actors)
        {
            if (actor.EntityId == 0 || !actor.IsValid)
            {
                quality = ObservationQuality.Partial;
                diagnostics.Add("observation.actor.invalid-or-missing-entity-id");
                continue;
            }

            identities[actor.EntityId] = new ActorIdentity(actor.EntityId, generations.Observe(actor));
        }

        generations.FinishFrame(identities.Keys);
        if (raw.LocalPlayerEntityId is null || !identities.ContainsKey(raw.LocalPlayerEntityId.Value))
        {
            quality = ObservationQuality.Partial;
            diagnostics.Add("observation.player.unavailable");
        }

        var byGameObjectId = raw.Actors
            .Where(actor => actor.GameObjectId != 0 && identities.ContainsKey(actor.EntityId))
            .GroupBy(actor => actor.GameObjectId)
            .ToDictionary(group => group.Key, group => group.First());
        var actors = ImmutableArray.CreateBuilder<ActorSnapshot>();
        foreach (var rawActor in raw.Actors.Where(actor => identities.ContainsKey(actor.EntityId)))
        {
            var identity = identities[rawActor.EntityId];
            var cast = ConvertCast(raw, rawActor, identity, identities, byGameObjectId, diagnostics, ref quality);
            var statuses = rawActor.Statuses.Select(status => ConvertStatus(raw, status, identities)).ToImmutableArray();
            actors.Add(new ActorSnapshot(
                identity,
                rawActor.BaseId,
                ConvertKind(rawActor.Kind, raw.LocalPlayerEntityId, rawActor.EntityId),
                new Coordinate2D(rawActor.Position.X, rawActor.Position.Z),
                Coordinate2D.FromClockwisePositiveYRotation(rawActor.Rotation),
                rawActor.HitboxRadius,
                rawActor.CurrentHp,
                rawActor.MaximumHp,
                rawActor.IsDead,
                !rawActor.IsValid,
                rawActor.IsInCombat,
                ResolveTarget(rawActor.TargetObjectId, identities, byGameObjectId),
                cast,
                statuses));
        }

        foreach (var missingIdentity in activeCasts.Keys.Where(identity => !identities.Values.Contains(identity)).ToArray())
        {
            activeCasts.Remove(missingIdentity);
        }

        var dungeon = ResolveDungeon(raw, raw.Actors, diagnostics, ref quality);
        return Build(raw, quality, dungeon, actors.ToImmutable(), diagnostics, playerPose);
    }

    private static void ValidateRawFrame(RawObservationFrame raw)
    {
        if (raw.CapturedAt == default || raw.Actors.IsDefault || raw.Diagnostics.IsDefault)
        {
            throw new InvalidDataException("Raw observation frame is incomplete.");
        }
    }

    private static AnonymousPlayerPose ConvertPlayerPose(
        RawObservationFrame frame,
        ObservationValueQuality? qualityOverride = null,
        string? reasonOverride = null)
    {
        if (frame.PlayerPose is { Position: { } rawPosition, Rotation: { } rawRotation })
        {
            if (!double.IsFinite(rawPosition.X)
                || !double.IsFinite(rawPosition.Y)
                || !double.IsFinite(rawPosition.Z)
                || !double.IsFinite(rawRotation))
            {
                return new AnonymousPlayerPose(
                    null,
                    null,
                    new ObservationMetadata(
                        ObservationValueSource.PublicDalamud,
                        ObservationValueQuality.Invalid,
                        "observation.player.pose-invalid"));
            }

            var quality = qualityOverride ?? ObservationValueQuality.Direct;
            var reason = quality is ObservationValueQuality.Direct ? null : reasonOverride ?? "observation.player.not-current";
            return new AnonymousPlayerPose(
                new Coordinate2D(rawPosition.X, rawPosition.Z),
                Coordinate2D.FromClockwisePositiveYRotation(rawRotation),
                new ObservationMetadata(ObservationValueSource.PublicDalamud, quality, reason));
        }

        var missingReason = reasonOverride
            ?? frame.PlayerPose?.MissingReason
            ?? "observation.player.pose-not-observed";
        return new AnonymousPlayerPose(
            null,
            null,
            new ObservationMetadata(
                ObservationValueSource.PublicDalamud,
                qualityOverride ?? ObservationValueQuality.Missing,
                missingReason));
    }

    private CastSnapshot? ConvertCast(
        RawObservationFrame frame,
        RawActorObservation actor,
        ActorIdentity identity,
        IReadOnlyDictionary<uint, ActorIdentity> identities,
        IReadOnlyDictionary<ulong, RawActorObservation> byGameObjectId,
        ImmutableArray<string>.Builder diagnostics,
        ref ObservationQuality quality)
    {
        if (actor.Cast is { } rawCast)
        {
            if (rawCast.ActionId == 0
                || !double.IsFinite(rawCast.CurrentSeconds)
                || !double.IsFinite(rawCast.TotalSeconds)
                || rawCast.CurrentSeconds < 0
                || rawCast.TotalSeconds <= 0)
            {
                quality = ObservationQuality.Partial;
                diagnostics.Add($"observation.cast.invalid:{actor.EntityId:X8}");
                activeCasts.Remove(identity);
                return null;
            }

            var elapsed = Math.Min(rawCast.CurrentSeconds, rawCast.TotalSeconds);
            var startedAt = frame.CapturedAt - TimeSpan.FromSeconds(elapsed);
            var finishesAt = startedAt + TimeSpan.FromSeconds(rawCast.TotalSeconds);
            var target = ResolveTarget(rawCast.TargetObjectId, identities, byGameObjectId);
            Coordinate2D? targetLocation = null;
            if (rawCast.TargetObjectId is { } targetId && byGameObjectId.TryGetValue(targetId, out var targetActor))
            {
                targetLocation = new Coordinate2D(targetActor.Position.X, targetActor.Position.Z);
                diagnostics.Add($"observation.cast.target-location.derived-from-target:{actor.EntityId:X8}");
            }

            diagnostics.Add($"observation.cast.timing.derived:{actor.EntityId:X8}");
            diagnostics.Add($"observation.cast.rotation.estimated-from-actor:{actor.EntityId:X8}");
            quality = ObservationQuality.Partial;
            CastSnapshot result;
            if (activeCasts.TryGetValue(identity, out var active)
                && active.Cast.ActionId == rawCast.ActionId
                && rawCast.CurrentSeconds + 0.1 >= active.LastCurrentSeconds)
            {
                result = active.Cast with
                {
                    Rotation = Coordinate2D.FromClockwisePositiveYRotation(actor.Rotation),
                    Target = target,
                    TargetLocation = targetLocation,
                };
            }
            else
            {
                result = new CastSnapshot(
                    rawCast.ActionId,
                    CastLifecycle.Casting,
                    startedAt,
                    finishesAt,
                    Coordinate2D.FromClockwisePositiveYRotation(actor.Rotation),
                    target,
                    targetLocation);
            }

            activeCasts[identity] = new CastObservationState(result, rawCast.CurrentSeconds);
            return result;
        }

        if (!activeCasts.Remove(identity, out var previousState))
        {
            return null;
        }

        var previous = previousState.Cast;
        var lifecycle = frame.CapturedAt + CompletionTolerance >= previous.FinishesAt
            ? CastLifecycle.Completed
            : CastLifecycle.Interrupted;
        return previous with { Lifecycle = lifecycle };
    }

    private static StatusSnapshot ConvertStatus(
        RawObservationFrame frame,
        RawStatusObservation status,
        IReadOnlyDictionary<uint, ActorIdentity> identities)
    {
        ActorIdentity? source = status.SourceEntityId is { } sourceId && identities.TryGetValue(sourceId, out var identity)
            ? identity
            : null;
        DateTimeOffset? expiresAt = status.RemainingSeconds is > 0 and var remaining && double.IsFinite(remaining)
            ? frame.CapturedAt + TimeSpan.FromSeconds(remaining)
            : null;
        return new StatusSnapshot(status.StatusId, source, status.Stacks, expiresAt);
    }

    private static ActorIdentity? ResolveTarget(
        ulong? targetObjectId,
        IReadOnlyDictionary<uint, ActorIdentity> identities,
        IReadOnlyDictionary<ulong, RawActorObservation> byGameObjectId) =>
        targetObjectId is { } targetId
        && byGameObjectId.TryGetValue(targetId, out var target)
        && identities.TryGetValue(target.EntityId, out var identity)
            ? identity
            : null;

    private static ActorKind ConvertKind(RawActorKind kind, uint? localPlayer, uint entityId) => kind switch
    {
        RawActorKind.Player when localPlayer == entityId => ActorKind.Player,
        RawActorKind.PartyMember => ActorKind.PartyMember,
        RawActorKind.Enemy => ActorKind.Enemy,
        _ => ActorKind.Unknown,
    };

    private static DeepDungeonContext ResolveDungeon(
        RawObservationFrame frame,
        ImmutableArray<RawActorObservation> actors,
        ImmutableArray<string>.Builder diagnostics,
        ref ObservationQuality quality)
    {
        if (actors.Any(actor => actor.BaseId is 0x3D54 or 0x3D55))
        {
            quality = ObservationQuality.Partial;
            diagnostics.Add("observation.dungeon.eureka-orthos-floor-10.estimated-from-encounter-actor");
            return new DeepDungeonContext(
                DeepDungeonKind.EurekaOrthos,
                10,
                frame.TerritoryId,
                frame.ContentId,
                "eureka-orthos-10:estimated",
                null);
        }

        if (frame.IsDutyStarted)
        {
            diagnostics.Add("observation.dungeon.unidentified");
            return new DeepDungeonContext(
                DeepDungeonKind.Unsupported,
                null,
                frame.TerritoryId,
                frame.ContentId,
                "duty-unidentified",
                null);
        }

        return NotInDungeon(frame);
    }

    private static DeepDungeonContext NotInDungeon(RawObservationFrame frame) =>
        new(DeepDungeonKind.None, null, frame.TerritoryId, frame.ContentId, "not-deep-dungeon", null);

    private WorldSnapshot Build(
        RawObservationFrame raw,
        ObservationQuality quality,
        DeepDungeonContext dungeon,
        ImmutableArray<ActorSnapshot> actors,
        ImmutableArray<string>.Builder diagnostics,
        AnonymousPlayerPose playerPose) =>
        new(
            SchemaVersions.WorldSnapshot,
            sequence++,
            raw.CapturedAt,
            sessionId,
            ObservationSource.LiveAdapter,
            quality,
            dungeon,
            actors,
            diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray(),
            playerPose);

    private void ResetSession(SessionKey key)
    {
        sessionKey = key;
        sessionId = Guid.NewGuid();
        sequence = 0;
        lastCapturedAt = default;
        generations.Reset();
        activeCasts.Clear();
    }

    private readonly record struct SessionKey(bool IsLoggedIn, uint? TerritoryId, uint? ContentId);

    private readonly record struct CastObservationState(CastSnapshot Cast, double LastCurrentSeconds);
}
