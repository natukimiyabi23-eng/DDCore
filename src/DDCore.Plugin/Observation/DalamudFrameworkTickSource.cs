using Dalamud.Plugin.Services;
using DDCore.Observation;

namespace DDCore.Plugin.Observation;

internal sealed class DalamudFrameworkTickSource : IObservationTickSource
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(100);
    private readonly IFramework framework;
    private DateTime lastRaisedUtc;
    private bool disposed;

    public DalamudFrameworkTickSource(IFramework framework)
    {
        this.framework = framework;
        framework.Update += OnFrameworkUpdate;
    }

    public event Action? Tick;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        framework.Update -= OnFrameworkUpdate;
        Tick = null;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        var now = framework.LastUpdateUTC;
        if (lastRaisedUtc != default && now - lastRaisedUtc < MinimumInterval)
        {
            return;
        }

        lastRaisedUtc = now;
        Tick?.Invoke();
    }
}
