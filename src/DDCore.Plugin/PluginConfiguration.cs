using Dalamud.Configuration;
using DDCore.Core.Configuration;

namespace DDCore.Plugin;

[Serializable]
public sealed class PluginConfiguration : DDCoreConfiguration, IPluginConfiguration
{
    public static PluginConfiguration From(DDCoreConfiguration source) => new()
    {
        Version = source.Version,
        Mode = source.Mode,
        EmergencyStopLatched = source.EmergencyStopLatched,
        ShowSafetyDetails = source.ShowSafetyDetails,
        LogModeChanges = source.LogModeChanges,
    };
}
