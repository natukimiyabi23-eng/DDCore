using System.Collections.Immutable;
using DDCore.Core.Geometry;
using DDCore.Core.Model;
using DDCore.Core.Schema;
using DDCore.Core.World;

namespace DDCore.Core.Mechanics.Analyzers;

/// <summary>
/// Reimplements the observed Gancanagh cast-to-AoE mapping from BossMod commit
/// 6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0. See provenance/bossmod-phase3.json.
/// This analyzer only describes observations and never performs game operations.
/// </summary>
public sealed class GancanaghAnalyzer : IMechanicAnalyzer
{
    public const string Id = "eureka-orthos.d10.gancanagh";
    public const uint BossObjectId = 0x3D54;
    public const uint PachypodiumMineObjectId = 0x3D55;
    public const uint AttackActionId = 6499;
    public const uint AuthoritativeShriekActionId = 31477;
    public const uint MandrashockSingleActionId = 31478;
    public const uint MandrashockTitanActionId = 32700;
    public const uint MandrastormActionId = 31479;
    public const double ArenaCenterX = -300;
    public const double ArenaCenterY = -300;
    public const double ArenaHalfSize = 19.5;

    private static readonly ImmutableDictionary<uint, CastHazardDefinition> Definitions =
        new Dictionary<uint, CastHazardDefinition>
        {
            [MandrashockSingleActionId] = new(
                MandrashockSingleActionId,
                new CircleAoe(10),
                HazardSeverity.Dangerous,
                "gancanagh.mandrashock-single"),
            [MandrashockTitanActionId] = new(
                MandrashockTitanActionId,
                new CircleAoe(10),
                HazardSeverity.Dangerous,
                "gancanagh.mandrashock-titan",
                MaximumInstances: 6),
            [MandrastormActionId] = new(
                MandrastormActionId,
                new CircleAoe(18.5),
                HazardSeverity.Dangerous,
                "gancanagh.mandrastorm"),
        }.ToImmutableDictionary();

    public string AnalyzerId => Id;

    public Version AnalyzerVersion { get; } = new(1, 0, 0);

    public bool CanAnalyze(WorldSnapshot snapshot) =>
        snapshot.Dungeon.Kind == DeepDungeonKind.EurekaOrthos && snapshot.Dungeon.Floor == 10;

    public AnalysisResult Analyze(WorldSnapshot snapshot, MechanicState previousState)
    {
        snapshot.Validate();
        if (!CanAnalyze(snapshot))
        {
            return new(previousState, MechanicFrame.Unknown(snapshot, "gancanagh.context.unsupported"));
        }

        if (snapshot.Sequence <= previousState.SnapshotSequence)
        {
            return new(previousState, MechanicFrame.Unknown(snapshot, "gancanagh.sequence.not-increasing", invalid: true));
        }

        if (previousState.CapturedAt != DateTimeOffset.MinValue && snapshot.CapturedAt < previousState.CapturedAt)
        {
            return new(previousState, MechanicFrame.Unknown(snapshot, "gancanagh.time.regressed", invalid: true));
        }

        var encounterActors = snapshot.Actors.Where(IsEncounterActor).ToArray();
        var encounterMissing = encounterActors.Length == 0;
        var reconciliation = CastHazardTracker.Reconcile(
            snapshot,
            AnalyzerId,
            Definitions,
            IsEncounterActor,
            previousState.ActiveHazards);
        var unsupported = encounterActors
            .Where(actor => !actor.IsDead && !actor.IsDestroyed && actor.Cast is not null)
            .Where(actor => actor.Cast!.Lifecycle == CastLifecycle.Casting)
            .Where(actor => !Definitions.ContainsKey(actor.Cast!.ActionId) && !IsKnownNonHazardCast(actor, actor.Cast.ActionId))
            .Select(actor => new UnsupportedMechanic(
                AnalyzerId,
                actor.Cast!.ActionId,
                actor.Identity,
                "gancanagh.action.unsupported",
                SupportState.Unsupported))
            .OrderBy(item => item.ActionId)
            .ThenBy(item => item.Source.EntityId)
            .ToImmutableArray();
        var hazards = reconciliation.Hazards.Values.OrderBy(hazard => hazard.HazardId, StringComparer.Ordinal).ToImmutableArray();
        var diagnostics = reconciliation.Diagnostics.ToBuilder();
        if (encounterMissing)
        {
            diagnostics.Add("gancanagh.encounter-actors.missing");
        }

        var safety = unsupported.Length > 0 || reconciliation.ObservationUncertain || encounterMissing
            ? MechanicSafetyAssessment.UnknownOrUnsupported
            : MechanicSafetyAssessment.HazardsKnown;
        var frame = new MechanicFrame(
            SchemaVersions.MechanicFrame,
            snapshot.Sequence,
            snapshot.CapturedAt,
            hazards,
            unsupported,
            diagnostics.ToImmutable(),
            safety);
        return new(new MechanicState(snapshot.Sequence, snapshot.CapturedAt, reconciliation.Hazards), frame);
    }

    private static bool IsEncounterActor(ActorSnapshot actor) =>
        actor.Kind == ActorKind.Enemy && actor.ObjectId is BossObjectId or PachypodiumMineObjectId;

    private static bool IsKnownNonHazardCast(ActorSnapshot actor, uint actionId) =>
        actor.ObjectId == BossObjectId && actionId == AuthoritativeShriekActionId;
}
