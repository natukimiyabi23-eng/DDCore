using DDCore.Core.Modes;
using DDCore.Core.Logging;

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
    private readonly IDDLogger logger;

    public ConfigurationService(IConfigurationPersistence persistence, IDDLogger? logger = null)
    {
        this.persistence = persistence;
        this.logger = logger ?? NullDDLogger.Instance;

        DDCoreConfiguration? loaded;
        var startupChanges = new List<string>();

        try
        {
            loaded = persistence.Load();
        }
        catch (Exception exception)
        {
            loaded = new DDCoreConfiguration
            {
                Mode = OperationMode.Observer,
                EmergencyStopLatched = true,
            };
            RecoveredFromLoadFailure = true;
            LastPersistenceError = "設定の読み込みに失敗したため、安全設定へ復旧しました。";
            startupChanges.Add(LastPersistenceError);
            this.logger.Error(LastPersistenceError, exception);
        }

        var migration = ConfigurationMigrator.NormalizeForStartup(loaded);
        Current = migration.Configuration;
        startupChanges.AddRange(migration.Changes);
        StartupChanges = startupChanges;

        if (migration.Changed || RecoveredFromLoadFailure)
        {
            TrySave("起動時の安全設定を保存できませんでした。");
        }
    }

    public DDCoreConfiguration Current { get; }
    public IReadOnlyList<string> StartupChanges { get; }
    public bool RecoveredFromLoadFailure { get; }
    public bool IsPersistenceHealthy { get; private set; } = true;
    public string? LastPersistenceError { get; private set; }

    public bool Save() => TrySave("設定を保存できませんでした。変更は現在のセッションだけに保持されます。");

    private bool TrySave(string failureMessage)
    {
        try
        {
            persistence.Save(Current);
            IsPersistenceHealthy = true;
            LastPersistenceError = null;
            return true;
        }
        catch (Exception exception)
        {
            IsPersistenceHealthy = false;
            LastPersistenceError = failureMessage;
            logger.Error(failureMessage, exception);
            return false;
        }
    }
}
