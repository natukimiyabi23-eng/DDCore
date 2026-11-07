using DDCore.Core.Combat;
using DDCore.Core.Mechanics;
using DDCore.Core.Modes;
using DDCore.Core.Navigation;
using DDCore.Core.Resources;
using DDCore.Core.World;

namespace DDCore.Core.Decision;

public sealed record DecisionContext(
    WorldSnapshot World,
    MechanicsReport Mechanics,
    NavigationAdvice Navigation,
    CombatAdvice Combat,
    ResourceAdvice Resources,
    OperationMode Mode);

public sealed record DecisionFrame(
    long SnapshotSequence,
    string Summary,
    bool RequestsExecution,
    IReadOnlyList<string> Reasons);

public interface IDecisionEngine
{
    DecisionFrame Evaluate(DecisionContext context);
}
