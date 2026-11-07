using DDCore.Core.Modes;

namespace DDCore.Core.Configuration;

public class DDCoreConfiguration
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public OperationMode Mode { get; set; } = OperationMode.Observer;
    public bool EmergencyStopLatched { get; set; }
    public bool ShowSafetyDetails { get; set; } = true;
    public bool LogModeChanges { get; set; } = true;
}

public sealed record ConfigurationMigrationResult(
    DDCoreConfiguration Configuration,
    bool Changed,
    IReadOnlyList<string> Changes);

public static class ConfigurationMigrator
{
    public static ConfigurationMigrationResult NormalizeForStartup(DDCoreConfiguration? source)
    {
        var configuration = source ?? new DDCoreConfiguration();
        var changes = new List<string>();

        if (source is null)
        {
            changes.Add("既定設定を作成しました。");
        }

        if (configuration.Version != DDCoreConfiguration.CurrentVersion)
        {
            configuration.Version = DDCoreConfiguration.CurrentVersion;
            changes.Add("設定バージョンを更新しました。");
        }

        if (!configuration.Mode.IsDefined() || configuration.Mode != OperationMode.Observer)
        {
            configuration.Mode = OperationMode.Observer;
            changes.Add("起動時の安全規則により観測モードへ戻しました。");
        }

        return new(configuration, changes.Count > 0, changes);
    }
}

public interface IConfigurationPersistence
{
    DDCoreConfiguration? Load();
    void Save(DDCoreConfiguration configuration);
}

public sealed class ConfigurationService
{
    private readonly IConfigurationPersistence persistence;

    public ConfigurationService(IConfigurationPersistence persistence)
    {
        this.persistence = persistence;
        var migration = ConfigurationMigrator.NormalizeForStartup(persistence.Load());
        Current = migration.Configuration;
        StartupChanges = migration.Changes;

        if (migration.Changed)
        {
            persistence.Save(Current);
        }
    }

    public DDCoreConfiguration Current { get; }
    public IReadOnlyList<string> StartupChanges { get; }

    public void Save() => persistence.Save(Current);
}
