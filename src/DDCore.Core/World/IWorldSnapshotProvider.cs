namespace DDCore.Core.World;

public interface IWorldSnapshotProvider
{
    WorldSnapshot Capture();
}
