using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using DDCore.Core.Modes;
using DDCore.Observation;

namespace DDCore.Plugin.Windows;

internal sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private bool exportPreviewConfirmed;
    private bool deletionConfirmed;

    public MainWindow(Plugin plugin)
        : base("DDCore##Main")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 720),
            MaximumSize = new Vector2(980, 980),
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
        DrawObservation();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawBossModIntegration();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawRecording();
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
        ImGui.TextDisabled("Phase 3.7: 読み取り専用観測・診断・明示操作によるローカル記録");
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

    private void DrawObservation()
    {
        var diagnostics = plugin.ObservationDiagnostics;
        ImGui.TextUnformatted("観測診断（読み取り専用）");
        ImGui.Spacing();
        DrawValue("観測状態", ObservationStateText(diagnostics.State));
        DrawValue("DD種別・階層", DungeonText(diagnostics));
        DrawValue("観測した敵", $"{diagnostics.EnemyCount}体");
        DrawValue("詠唱中の敵", $"{diagnostics.CastingEnemyCount}体");
        DrawValue("検出したAoE", $"{diagnostics.HazardCount}件");
        DrawValue("安全判定", diagnostics.Safety switch
        {
            DDCore.Core.Mechanics.MechanicSafetyAssessment.HazardsKnown => "既知の危険を解析済み",
            DDCore.Core.Mechanics.MechanicSafetyAssessment.InvalidObservation => "観測データが無効",
            _ => "不明・未対応（安全とは判定しません）",
        });
        DrawValue("最終観測", diagnostics.LastObservedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "未観測");

        if (diagnostics.Reasons.Length > 0 && ImGui.TreeNode("解析不能・未対応の理由"))
        {
            foreach (var reason in diagnostics.Reasons.Take(12))
            {
                ImGui.BulletText(reason);
            }

            if (diagnostics.Reasons.Length > 12)
            {
                ImGui.TextDisabled($"ほか {diagnostics.Reasons.Length - 12} 件");
            }

            ImGui.TreePop();
        }
    }

    private static void DrawValue(string label, string value)
    {
        ImGui.TextUnformatted(label);
        ImGui.SameLine(170);
        ImGui.TextWrapped(value);
    }

    private void DrawBossModIntegration()
    {
        var diagnostics = plugin.BossModIntegration;
        ImGui.TextUnformatted("BossMod連携診断（読み取り専用）");
        ImGui.TextDisabled("接続可否とDD攻略データの提供可否は別々に判定します。");
        ImGui.Spacing();
        DrawValue("IPC接続", diagnostics.Connection switch
        {
            BossModConnectionState.NotChecked => "未確認",
            BossModConnectionState.Unavailable => "未導入・未起動・既知IPCなし",
            BossModConnectionState.ReachableUnversioned => "到達可能（バージョン契約なし）",
            BossModConnectionState.IncompatibleOrFaulted => "署名不一致または呼び出し失敗",
            _ => "不明",
        });
        DrawValue("ガンカナグ定義", diagnostics.HasGancanaghModule switch
        {
            true => "既知IPCで登録を確認",
            false => "既知IPCには未登録",
            null => "確認不能",
        });
        DrawValue("DD攻略データ", "取得不可（AoE・安全位置・部屋・資源・優先度の公開契約なし）");
        DrawValue("プロトコル版", diagnostics.ProtocolVersion ?? "提供なし・互換性を保証できません");
        DrawValue("診断コード", diagnostics.ReasonCode);
    }

    private static string ObservationStateText(LiveObservationState state) => state switch
    {
        LiveObservationState.Waiting => "待機中",
        LiveObservationState.Observing => "観測中",
        LiveObservationState.Partial => "部分観測（推定・未取得あり）",
        LiveObservationState.Unsupported => "未対応",
        LiveObservationState.Invalid => "観測エラー",
        _ => "不明",
    };

    private static string DungeonText(LiveObservationDiagnostics diagnostics)
    {
        var kind = diagnostics.DungeonKind switch
        {
            DDCore.Core.World.DeepDungeonKind.PalaceOfTheDead => "死者の宮殿",
            DDCore.Core.World.DeepDungeonKind.HeavenOnHigh => "アメノミハシラ",
            DDCore.Core.World.DeepDungeonKind.EurekaOrthos => "オルト・エウレカ",
            DDCore.Core.World.DeepDungeonKind.None => "DD外",
            _ => "未対応・識別不能",
        };
        return diagnostics.Floor is { } floor ? $"{kind} {floor}階（推定を含む）" : kind;
    }

    private void DrawRecording()
    {
        var status = plugin.RecordingStatus;
        ImGui.TextUnformatted("実機検証用の観測記録");
        ImGui.TextDisabled("初期状態はOFFです。敵とギミック再現に必要な情報だけをローカル保存し、外部送信しません。");
        ImGui.Spacing();
        DrawValue("記録状態", RecordingStateText(status.State));
        DrawValue("記録件数", $"{status.Preview.SnapshotCount}件");
        DrawValue("記録容量", FormatBytes(status.Preview.StoredBytes));
        DrawValue("保存先", status.RecordingPath ?? "未作成");
        DrawValue("最新解析エラー", plugin.LatestObservationError?.Message ?? status.LatestError ?? "なし");

        ImGui.Spacing();
        using (ImRaii.Disabled(status.State == ObservationRecordingState.Recording))
        {
            if (ImGui.Button("記録開始", new Vector2(120, 32)))
            {
                exportPreviewConfirmed = false;
                deletionConfirmed = false;
                plugin.StartObservationRecording();
            }
        }

        ImGui.SameLine();
        using (ImRaii.Disabled(status.State != ObservationRecordingState.Recording))
        {
            if (ImGui.Button("記録停止", new Vector2(120, 32)))
            {
                plugin.StopObservationRecording();
            }
        }

        if (status.Preview.SnapshotCount > 0 && ImGui.TreeNode("保存内容を確認"))
        {
            DrawValue("セッション", status.Preview.SessionId?.ToString("N") ?? "未確定");
            DrawValue("記録開始", status.Preview.StartedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "未確定");
            DrawValue("最終観測", status.Preview.LastCapturedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "未確定");
            DrawValue("敵・詠唱・AoE", $"{status.Preview.EnemyCount}体 / {status.Preview.ActiveCastCount}件 / {status.Preview.HazardCount}件");
            DrawValue("未対応理由", status.Preview.LatestUnsupportedReason ?? "なし");
            ImGui.BulletText("他プレイヤー名、チャット本文、アカウント情報は収集しません。");
            ImGui.BulletText("プレイヤーとパーティメンバーの識別子は保存前に除外します。");
            ImGui.Checkbox("この保存内容を確認しました", ref exportPreviewConfirmed);
            ImGui.TreePop();
        }

        using (ImRaii.Disabled(
            status.State == ObservationRecordingState.Recording
            || status.Preview.SnapshotCount == 0
            || !exportPreviewConfirmed))
        {
            if (ImGui.Button("エクスポート", new Vector2(140, 32)))
            {
                plugin.ExportObservationRecording(exportPreviewConfirmed);
            }
        }

        ImGui.SameLine();
        ImGui.Checkbox("削除を確認", ref deletionConfirmed);
        ImGui.SameLine();
        using (ImRaii.Disabled(
            status.State == ObservationRecordingState.Recording
            || status.Preview.SnapshotCount == 0
            || !deletionConfirmed))
        {
            if (ImGui.Button("記録データ削除", new Vector2(150, 32)))
            {
                plugin.DeleteObservationRecording(deletionConfirmed);
                deletionConfirmed = false;
                exportPreviewConfirmed = false;
            }
        }

        if (!string.IsNullOrWhiteSpace(status.ExportPath))
        {
            DrawValue("出力先", status.ExportPath);
        }

        if (!string.IsNullOrWhiteSpace(status.StopReason))
        {
            DrawValue("停止理由", status.StopReason);
        }
    }

    private static string RecordingStateText(ObservationRecordingState state) => state switch
    {
        ObservationRecordingState.Off => "OFF",
        ObservationRecordingState.Recording => "記録中",
        ObservationRecordingState.Stopped => "停止・保存済み",
        ObservationRecordingState.LimitReached => "上限到達・停止",
        ObservationRecordingState.Faulted => "保存エラー・停止",
        _ => "不明",
    };

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / (1024d * 1024d):F2} MiB",
        >= 1024 => $"{bytes / 1024d:F1} KiB",
        _ => $"{bytes} bytes",
    };

    private void DrawModes()
    {
        ImGui.TextUnformatted("動作モード");
        ImGui.TextDisabled("AssistedとAutonomousは将来用の定義です。現在のビルドでは選択できません。");
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
            ImGui.SetTooltip("現在のビルドには実行機能が含まれていません。");
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
