using DDCore.Core.Mechanics;
using DDCore.Core.World;

namespace DDCore.Core.Combat;

public sealed record CombatAdvice(
    long SnapshotSequence,
    string Summary,
    bool HasRecommendation);

public interface ICombatAdvisor
{
    CombatAdvice Evaluate(WorldSnapshot snapshot, MechanicsReport mechanics);
}
