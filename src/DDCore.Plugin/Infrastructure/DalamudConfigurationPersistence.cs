using Dalamud.Plugin;
using DDCore.Core.Configuration;

namespace DDCore.Plugin.Infrastructure;

internal sealed class DalamudConfigurationPersistence(IDalamudPluginInterface pluginInterface)
    : IConfigurationPersistence
{
    public DDCoreConfiguration? Load() =>
        pluginInterface.GetPluginConfig() as PluginConfiguration;

    public void Save(DDCoreConfiguration configuration) =>
        pluginInterface.SavePluginConfig(configuration as PluginConfiguration ?? PluginConfiguration.From(configuration));
}
