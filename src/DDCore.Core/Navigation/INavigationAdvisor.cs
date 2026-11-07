using DDCore.Core.Mechanics;
using DDCore.Core.World;

namespace DDCore.Core.Navigation;

public sealed record NavigationAdvice(
    long SnapshotSequence,
    string Summary,
    bool HasCandidateRoute);

public interface INavigationAdvisor
{
    NavigationAdvice Evaluate(WorldSnapshot snapshot, MechanicsReport mechanics);
}
