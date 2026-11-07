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

    private readonly IDDLogger logger;
    private readonly ConfigurationService configurationService;
    private readonly MainWindow mainWindow;
    private readonly ConfigurationWindow configurationWindow;

    public Plugin()
    {
        logger = new DalamudLogger(PluginLog);
        configurationService = new ConfigurationService(new DalamudConfigurationPersistence(PluginInterface));
        Safety = new SafetyController(executionAvailable: false);
        Safety.RestoreEmergencyStop(configurationService.Current.EmergencyStopLatched);
        Execution = new DisabledExecutionGateway();

        WindowSystem = new WindowSystem("DDCore");
        mainWindow = new MainWindow(this);
        configurationWindow = new ConfigurationWindow(this);
        WindowSystem.AddWindow(mainWindow);
        WindowSystem.AddWindow(configurationWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "DDCoreを開きます。config: 設定、stop: 緊急停止、status: 状態表示",
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        foreach (var change in configurationService.StartupChanges)
        {
            logger.Information(change);
        }

        logger.Information("DDCore Phase 2をObserverモードで起動しました。ゲーム操作機能は含まれていません。");
    }

    public WindowSystem WindowSystem { get; }
    public ISafetyController Safety { get; }
    public IExecutionGateway Execution { get; }
    public DDCoreConfiguration Configuration => configurationService.Current;
    public string LastStatusMessage { get; private set; } = "Observerモードで安全に起動しました。";

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        CommandManager.RemoveHandler(CommandName);
        WindowSystem.RemoveAllWindows();
        mainWindow.Dispose();
        configurationWindow.Dispose();
    }

    public void ToggleMainUi() => mainWindow.Toggle();
    public void ToggleConfigUi() => configurationWindow.Toggle();

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
        configurationService.Save();
        if (Configuration.LogModeChanges)
        {
            logger.Information(result.Reason);
        }
    }

    public void EmergencyStop(string reason)
    {
        Safety.EmergencyStop(reason);
        Configuration.Mode = OperationMode.Observer;
        Configuration.EmergencyStopLatched = true;
        configurationService.Save();
        LastStatusMessage = "緊急停止しました。Observerモードへ戻しました。";
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
        configurationService.Save();
        LastStatusMessage = reason;
        logger.Information(reason);
    }

    public void SaveConfiguration() => configurationService.Save();

    private void OnCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "config":
                ToggleConfigUi();
                break;
            case "stop":
                EmergencyStop("/ddcore stop が実行されました。");
                mainWindow.IsOpen = true;
                break;
            case "status":
                mainWindow.IsOpen = true;
                break;
            default:
                ToggleMainUi();
                break;
        }
    }
}
