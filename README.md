# DDCore

BossModの調査結果を参考にしつつ、実行時にBossModへ依存しないFFXIVディープダンジョン向けDalamudプラグインを構築するプロジェクトです。

## 現在の状態

Phase 2では、将来の攻略機能を安全に追加するための独立基盤を実装しています。

- C# / .NET 10 / Dalamud API 15
- BossModへの実行時依存なし
- 日本語のメイン画面と設定画面
- Observer / Advisor / Simulation / Assisted / Autonomousのモード定義
- 起動時は必ずObserver
- 緊急停止とモード変更時の安全チェック
- World、Mechanics、Navigation、Combat、Resources、Decision、Executionの契約分離
- 設定保存、設定バージョン移行、Dalamudログ
- 自動操作を行わない`DisabledExecutionGateway`

Phase 2には、移動、戦闘、魔土器、ターゲット、オブジェクト操作、ゲーム内部フックは含まれていません。AssistedとAutonomousは将来用の定義のみで、現在のビルドでは選択できません。

## プロジェクト構成

```text
src/DDCore.Core/          ゲーム非依存のモード、安全制御、設定、責務別契約
src/DDCore.Plugin/        Dalamudライフサイクル、日本語UI、設定・ログアダプター
tests/DDCore.Core.Tests/  安全制御、設定移行、無効実行経路の単体テスト
```

## ビルドとテスト

前提:

- .NET SDK 10
- XIVLauncher/Dalamud開発用アセンブリ
- 既定場所以外を使う場合は`DALAMUD_HOME`を設定

```powershell
dotnet restore DDCore.slnx
dotnet build DDCore.slnx -c Release --no-restore
dotnet test tests/DDCore.Core.Tests/DDCore.Core.Tests.csproj -c Release --no-build --no-restore
```

生成パッケージ:

```text
src/DDCore.Plugin/bin/x64/Release/DDCore/latest.zip
```

## コマンド

- `/ddcore`: メイン画面の表示切り替え
- `/ddcore config`: 設定画面の表示切り替え
- `/ddcore stop`: 緊急停止をラッチしてObserverへ戻す
- `/ddcore status`: 状態画面を表示

## 調査基準

- BossMod: <https://github.com/awgil/ffxiv_bossmod>
- 固定調査コミット: `6e410ef8bb912fc70e3c1f97eedfb77a7dc4dfc0`
- Dalamud: API 15 / .NET 10
- BossModライセンス: BSD-3-Clause

[マスタープラン](docs/DDCore_Codex_MasterPlan.md)、[調査報告](docs/research-report.md)、[Phase 2検証記録](docs/phase2-verification.md)を参照してください。

## 配布上の注意

公式Dalamudリポジトリの制限は、自動戦闘や自動ゲームプレイを許可していません。将来のAutonomous機能は公式配布対象とは仮定せず、実装前に配布方針とリスクを別途判断します。
