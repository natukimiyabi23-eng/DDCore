using DDCore.Core.World;

namespace DDCore.Core.Resources;

public sealed record ResourceAdvice(
    long SnapshotSequence,
    string Summary,
    bool HasRecommendation);

public interface IResourceAdvisor
{
    ResourceAdvice Evaluate(WorldSnapshot snapshot);
}
