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

    private static readonly ImmutableHashSet<uint> KnownNonHazardActions =
        ImmutableHashSet.Create(AttackActionId, AuthoritativeShriekActionId);

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

        var active = CastHazardTracker.Reconcile(snapshot, AnalyzerId, Definitions, IsEncounterActor);
        var unsupported = snapshot.Actors
            .Where(IsEncounterActor)
            .Where(actor => !actor.IsDead && !actor.IsDestroyed && actor.Cast is not null)
            .Where(actor => actor.Cast!.Lifecycle == CastLifecycle.Casting)
            .Where(actor => !Definitions.ContainsKey(actor.Cast!.ActionId) && !KnownNonHazardActions.Contains(actor.Cast.ActionId))
            .Select(actor => new UnsupportedMechanic(
                AnalyzerId,
                actor.Cast!.ActionId,
                actor.Identity,
                "gancanagh.action.unsupported",
                SupportState.Unsupported))
            .OrderBy(item => item.ActionId)
            .ThenBy(item => item.Source.EntityId)
            .ToImmutableArray();
        var hazards = active.Values.OrderBy(hazard => hazard.HazardId, StringComparer.Ordinal).ToImmutableArray();
        var safety = unsupported.Length == 0
            ? MechanicSafetyAssessment.HazardsKnown
            : MechanicSafetyAssessment.UnknownOrUnsupported;
        var frame = new MechanicFrame(
            SchemaVersions.MechanicFrame,
            snapshot.Sequence,
            snapshot.CapturedAt,
            hazards,
            unsupported,
            ImmutableArray<string>.Empty,
            safety);
        return new(new MechanicState(snapshot.Sequence, snapshot.CapturedAt, active), frame);
    }

    private static bool IsEncounterActor(ActorSnapshot actor) =>
        actor.ObjectId is BossObjectId or PachypodiumMineObjectId;
}
