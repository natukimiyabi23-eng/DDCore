using Dalamud.Plugin;
using DDCore.Core.Configuration;

namespace DDCore.Plugin.Infrastructure;

internal sealed class DalamudConfigurationPersistence(IDalamudPluginInterface pluginInterface)
    : IConfigurationPersistence
{
    public DDCoreConfiguration? Load()
    {
        var stored = pluginInterface.GetPluginConfig();
        return stored switch
        {
            null => null,
            PluginConfiguration configuration => configuration,
            _ => throw new InvalidDataException(
                $"DDCoreの設定型が不正です: {stored.GetType().FullName}"),
        };
    }

    public void Save(DDCoreConfiguration configuration) =>
        pluginInterface.SavePluginConfig(configuration as PluginConfiguration ?? PluginConfiguration.From(configuration));
}
