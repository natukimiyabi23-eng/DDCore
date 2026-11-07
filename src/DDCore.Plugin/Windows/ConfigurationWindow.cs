using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DDCore.Plugin.Windows;

internal sealed class ConfigurationWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public ConfigurationWindow(Plugin plugin)
        : base("DDCore 設定##Configuration", ImGuiWindowFlags.NoCollapse)
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(430, 220),
            MaximumSize = new Vector2(720, 520),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        ImGui.TextUnformatted("表示とログ");
        ImGui.Spacing();

        var showSafetyDetails = plugin.Configuration.ShowSafetyDetails;
        if (ImGui.Checkbox("安全状態の詳細を表示", ref showSafetyDetails))
        {
            plugin.Configuration.ShowSafetyDetails = showSafetyDetails;
            plugin.SaveConfiguration();
        }

        var logModeChanges = plugin.Configuration.LogModeChanges;
        if (ImGui.Checkbox("モード変更をDalamudログへ記録", ref logModeChanges))
        {
            plugin.Configuration.LogModeChanges = logModeChanges;
            plugin.SaveConfiguration();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextUnformatted("起動時の安全規則");
        ImGui.BulletText("毎回Observerモードで起動します。");
        ImGui.BulletText("緊急停止状態は保存されます。");
        ImGui.BulletText("実行機能とゲーム内部フックは含まれていません。");

        ImGui.Spacing();
        if (ImGui.Button("メイン画面を開く"))
        {
            plugin.ToggleMainUi();
        }
    }
}
