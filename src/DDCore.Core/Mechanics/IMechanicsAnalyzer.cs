using DDCore.Core.World;

namespace DDCore.Core.Mechanics;

public sealed record MechanicsReport(
    long SnapshotSequence,
    IReadOnlyList<string> Notices,
    bool HasUnsupportedState);

public interface IMechanicsAnalyzer
{
    MechanicsReport Analyze(WorldSnapshot snapshot);
}
