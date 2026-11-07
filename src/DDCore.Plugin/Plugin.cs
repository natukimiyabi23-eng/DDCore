using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DDCore.Core.Configuration;
using DDCore.Core.Execution;
using DDCore.Core.Logging;
using DDCore.Core.Modes;
using DDCore.Core.Safety;
using DDCore.Plugin.Infrastructure;
using DDCore.Plugin.Windows;

namespace DDCore.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/ddcore";

    [PluginService] private static IDalamudPluginInterface PluginInterface { get; set; } = null!;
    [PluginService] private static ICommandManager CommandManager { get; set; } = null!;
    [PluginService] private static IPluginLog PluginLog { get; set; } = null!;

    private readonly Stack<Action> cleanupActions = new();
    private IDDLogger logger = NullDDLogger.Instance;
    private ConfigurationService configurationService = null!;
    private WindowSystem? windowSystem;
    private MainWindow? mainWindow;
    private ConfigurationWindow? configurationWindow;
    private bool resourcesReleased;

    public Plugin()
    {
        try
        {
            logger = new DalamudLogger(PluginLog);
            configurationService = new ConfigurationService(
                new DalamudConfigurationPersistence(PluginInterface),
                logger);
            Safety = new SafetyController(executionAvailable: false);
            Safety.RestoreEmergencyStop(configurationService.Current.EmergencyStopLatched);
            Execution = new DisabledExecutionGateway();

            windowSystem = new WindowSystem("DDCore");
            mainWindow = new MainWindow(this);
            cleanupActions.Push(mainWindow.Dispose);
            configurationWindow = new ConfigurationWindow(this);
            cleanupActions.Push(configurationWindow.Dispose);
            windowSystem.AddWindow(mainWindow);
            windowSystem.AddWindow(configurationWindow);
            cleanupActions.Push(windowSystem.RemoveAllWindows);

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "DDCoreを開きます。config: 設定、stop: 緊急停止、status: 状態表示",
            });
            cleanupActions.Push(() => CommandManager.RemoveHandler(CommandName));

            PluginInterface.UiBuilder.Draw += windowSystem.Draw;
            cleanupActions.Push(() => PluginInterface.UiBuilder.Draw -= windowSystem.Draw);
            PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
            cleanupActions.Push(() => PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi);
            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
            cleanupActions.Push(() => PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi);

            foreach (var change in configurationService.StartupChanges)
            {
                logger.Warning(change);
            }

            LastStatusMessage = configurationService.RecoveredFromLoadFailure
                ? "設定を読み込めなかったため、Observer＋緊急停止状態で復旧しました。"
                : "Observerモードで安全に起動しました。";

            if (!configurationService.IsPersistenceHealthy)
            {
                LastStatusMessage = configurationService.LastPersistenceError
                    ?? "設定の永続化に失敗しました。安全状態は現在のセッションで維持されます。";
            }

            logger.Information("DDCore Phase 2をObserverモードで起動しました。ゲーム操作機能は含まれていません。");
        }
        catch (Exception exception)
        {
            SafeLogError("DDCoreの初期化に失敗しました。登録済みリソースを解放します。", exception);
            ReleaseResources();
            throw;
        }
    }

    public WindowSystem WindowSystem => windowSystem
        ?? throw new InvalidOperationException("DDCoreのウィンドウシステムは初期化されていません。");
    public ISafetyController Safety { get; private set; } = null!;
    public IExecutionGateway Execution { get; private set; } = null!;
    public DDCoreConfiguration Configuration => configurationService.Current;
    public string LastStatusMessage { get; private set; } = "Observerモードで安全に起動しました。";

    public void Dispose() => ReleaseResources();

    public void ToggleMainUi() => mainWindow?.Toggle();
    public void ToggleConfigUi() => configurationWindow?.Toggle();

    public void RequestMode(OperationMode mode)
    {
        var result = Safety.RequestMode(mode);
        LastStatusMessage = result.Reason;

        if (!result.Accepted)
        {
            logger.Warning(result.Reason);
            return;
        }

        Configuration.Mode = result.CurrentMode;
        var saved = SaveConfiguration();
        if (Configuration.LogModeChanges)
        {
            logger.Information(result.Reason);
        }

        if (saved)
        {
            LastStatusMessage = result.Reason;
        }
    }

    public void EmergencyStop(string reason)
    {
        Safety.EmergencyStop(reason);
        Configuration.Mode = OperationMode.Observer;
        Configuration.EmergencyStopLatched = true;
        var saved = SaveConfiguration();
        if (saved)
        {
            LastStatusMessage = "緊急停止しました。Observerモードへ戻しました。";
        }
        logger.Warning($"緊急停止: {reason}");
    }

    public void ResetEmergencyStop()
    {
        if (!Safety.TryResetEmergencyStop(out var reason))
        {
            LastStatusMessage = reason;
            logger.Warning(reason);
            return;
        }

        Configuration.EmergencyStopLatched = false;
        if (SaveConfiguration())
        {
            LastStatusMessage = reason;
        }
        logger.Information(reason);
    }

    public bool SaveConfiguration()
    {
        if (configurationService.Save())
        {
            return true;
        }

        LastStatusMessage = configurationService.LastPersistenceError
            ?? "設定保存に失敗しました。変更は現在のセッションだけに保持されます。";
        return false;
    }

    private void OnCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "config":
                ToggleConfigUi();
                break;
            case "stop":
                EmergencyStop("/ddcore stop が実行されました。");
                if (mainWindow is not null)
                {
                    mainWindow.IsOpen = true;
                }
                break;
            case "status":
                if (mainWindow is not null)
                {
                    mainWindow.IsOpen = true;
                }
                break;
            default:
                ToggleMainUi();
                break;
        }
    }

    private void ReleaseResources()
    {
        if (resourcesReleased)
        {
            return;
        }

        resourcesReleased = true;
        while (cleanupActions.TryPop(out var cleanup))
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                SafeLogError("DDCoreのリソース解放中にエラーが発生しました。", exception);
            }
        }
    }

    private void SafeLogError(string message, Exception exception)
    {
        try
        {
            logger.Error(message, exception);
        }
        catch
        {
            // Logging must never prevent initialization rollback or disposal.
        }
    }
}
