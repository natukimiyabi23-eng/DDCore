using Dalamud.Plugin.Services;
using DDCore.Core.Logging;

namespace DDCore.Plugin.Infrastructure;

internal sealed class DalamudLogger(IPluginLog log) : IDDLogger
{
    public void Debug(string message) => log.Debug(message);
    public void Information(string message) => log.Information(message);
    public void Warning(string message) => log.Warning(message);

    public void Error(string message, Exception? exception = null)
    {
        if (exception is null)
        {
            log.Error(message);
            return;
        }

        log.Error(exception, message);
    }
}
