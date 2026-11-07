namespace DDCore.Core.World;

public sealed record WorldSnapshot(
    long Sequence,
    DateTimeOffset CapturedAt,
    bool IsDeepDungeon,
    string ContentKey);

public interface IWorldSnapshotProvider
{
    WorldSnapshot Capture();
}
