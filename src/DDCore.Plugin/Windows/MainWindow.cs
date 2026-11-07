using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using DDCore.Core.Modes;

namespace DDCore.Plugin.Windows;

internal sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin)
        : base("DDCore##Main", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 390),
            MaximumSize = new Vector2(900, 720),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        DrawStatus();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawModes();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawSafetyControls();
    }

    private void DrawStatus()
    {
        ImGui.TextUnformatted("ディープダンジョン攻略基盤");
        ImGui.TextDisabled("Phase 2: 独立基盤・観測・安全制御");
        ImGui.Spacing();

        ImGui.TextUnformatted("現在のモード");
        ImGui.SameLine(170);
        ImGui.TextColored(new Vector4(0.35f, 0.85f, 0.55f, 1f), plugin.Safety.CurrentMode.JapaneseName());

        ImGui.TextUnformatted("ゲーム操作機能");
        ImGui.SameLine(170);
        ImGui.TextColored(new Vector4(0.85f, 0.75f, 0.35f, 1f), "未実装・無効");

        ImGui.TextUnformatted("緊急停止");
        ImGui.SameLine(170);
        var stopColor = plugin.Safety.EmergencyStopLatched
            ? new Vector4(1f, 0.35f, 0.35f, 1f)
            : new Vector4(0.6f, 0.85f, 0.65f, 1f);
        ImGui.TextColored(stopColor, plugin.Safety.EmergencyStopLatched ? "有効" : "待機");

        if (plugin.Configuration.ShowSafetyDetails)
        {
            ImGui.Spacing();
            ImGui.TextWrapped(plugin.LastStatusMessage);
        }
    }

    private void DrawModes()
    {
        ImGui.TextUnformatted("動作モード");
        ImGui.TextDisabled("AssistedとAutonomousは将来用の定義です。Phase 2では選択できません。");
        ImGui.Spacing();

        DrawMode(OperationMode.Observer, "状態表示のみ");
        DrawMode(OperationMode.Advisor, "将来の助言表示用。現在は基盤のみ");
        DrawMode(OperationMode.Simulation, "将来のリプレイ検証用。ゲーム操作なし");
        DrawMode(OperationMode.Assisted, "ユーザー承認操作用。未実装");
        DrawMode(OperationMode.Autonomous, "完全自動用。未実装");
    }

    private void DrawMode(OperationMode mode, string description)
    {
        var blocked = mode.RequiresExecution();
        using (ImRaii.Disabled(blocked))
        {
            if (ImGui.RadioButton($"{mode.JapaneseName()}##Mode{mode}", plugin.Safety.CurrentMode == mode))
            {
                plugin.RequestMode(mode);
            }
        }

        if (blocked && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip("Phase 2には実行機能が含まれていません。");
        }

        ImGui.SameLine(180);
        ImGui.TextDisabled(description);
    }

    private void DrawSafetyControls()
    {
        ImGui.TextUnformatted("安全制御");
        ImGui.Spacing();

        if (ImGui.Button("緊急停止", new Vector2(150, 36)))
        {
            plugin.EmergencyStop("メイン画面の緊急停止ボタンが押されました。");
        }

        ImGui.SameLine();
        using (ImRaii.Disabled(!plugin.Safety.EmergencyStopLatched))
        {
            if (ImGui.Button("停止状態を解除", new Vector2(150, 36)))
            {
                plugin.ResetEmergencyStop();
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("設定", new Vector2(100, 36)))
        {
            plugin.ToggleConfigUi();
        }

        ImGui.Spacing();
        ImGui.TextDisabled("緊急停止はObserverへ戻し、セッション承認を破棄します。解除してもゲーム操作は有効になりません。");
    }
}
