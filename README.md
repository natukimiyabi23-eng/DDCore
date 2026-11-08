# DDCore

BossModの調査結果を参考にしつつ、実行時にBossModへ依存しないFFXIVディープダンジョン向けDalamudプラグインを構築するプロジェクトです。

## 現在の状態

ユーザー定義のPhase 5では、Phase 2〜4.5の安全・観測・解析基盤を維持しながら、ゲーム非依存のDD状態モデルと読み取り専用の決定論的攻略戦略エンジンを追加します。この番号は旧マスタープランの「Phase 4 — Strategy decision engine」を完成させる作業をPhase 5と呼ぶもので、旧「Phase 5 — Operation foundation」は実装していません。

- C# / .NET 10 / Dalamud API 15
- BossModへの実行時依存なし
- 日本語のメイン画面と設定画面
- Observer / Advisor / Simulation / Assisted / Autonomousのモード定義
- 起動時は必ずObserver
- 緊急停止とモード変更時の安全チェック
- World、Mechanics、Navigation、Combat、Resources、Decision、Executionの契約分離
- 設定保存、設定バージョン移行、Dalamudログ
- 自動操作を行わない`DisabledExecutionGateway`
- 純粋C#の円・扇・矩形・ドーナツ・ドーナツ扇・十字AoE計算
- イミュータブルな`WorldSnapshot`と未知・不完全観測の明示
- 上限・入力検証・明示的ユーザー操作契約を持つローカルJSONリプレイ
- 決定論的な解析Registry、Hazard、MechanicFrame
- エウレカオルト10階・ガンカナグの3種の円形AoE解析
- 公開Dalamud API 15だけを使う読み取り専用観測アダプター
- EntityId世代、詠唱ライフサイクル、ゾーン/コンテンツ切替の追跡
- 部分観測でも安全扱いしない日本語の観測診断
- 変化時だけを保持する有界な通常診断ログ
- 明示開始・停止、2 MiB/1,800件/15分上限を持つローカル観測記録
- プレイヤー/パーティ識別子を除外するプライバシーフィルター
- 内容確認後のローカルエクスポートと確認付き削除
- 5x5部屋グラフを対象とする純粋C#の決定論的BFS経路探索
- BossMod固定コミットとの出典・比較テスト・再利用率記録
- BossMod未導入でも起動できる、公開Dalamud IPCだけを使った任意の接続診断
- 接続できてもDD攻略データは取得不可と明示する安全側の能力判定
- Unknownを保持する階層・25部屋・出口・敵・罠・魔土器・残り時間の不変戦略状態
- 理由・前提・危険度・阻害理由・対象部屋・経路を返す決定論的戦略エンジン
- 危険回避、交戦継続、資源保全、活性出口、未探索部屋、出口再確認の安全優先判断
- 取得元・品質・欠損理由を持つ匿名プレイヤー姿勢
- 欠損と有効な原点を区別するnullable座標とschema 3後方互換

Phase 5にも、移動、戦闘、魔土器、ターゲット、オブジェクト操作、入力注入、ゲーム状態を書き換えるフックは含まれていません。BossModバイナリや内部メモリにも依存しません。戦略エンジンは合成または将来の正規化済み入力を計算するだけで、現在の公開API観測から部屋・出口・罠・魔土器状態を生成しません。AssistedとAutonomousは将来用の定義のみで、現在のビルドでは選択できません。

## プロジェクト構成

```text
src/DDCore.Core/          ゲーム非依存の安全制御、World、幾何、解析、リプレイ
src/DDCore.Observation/   Dalamud非依存の観測正規化、世代・詠唱・セッション追跡
src/DDCore.Plugin/        Dalamudライフサイクル、日本語UI、設定・ログアダプター
tests/DDCore.Core.Tests/  安全制御、幾何、World、リプレイ、解析の単体テスト
tests/DDCore.Observation.Tests/ 観測変換、解析接続、終了処理の単体テスト
```

## ビルドとテスト

前提:

- .NET SDK 10
- XIVLauncher/Dalamud開発用アセンブリ
- 既定場所以外を使う場合は`DALAMUD_HOME`を設定

```powershell
dotnet restore DDCore.slnx
dotnet build DDCore.slnx -c Release --no-restore
dotnet test DDCore.slnx -c Release --no-build --no-restore
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

[マスタープラン](docs/DDCore_Codex_MasterPlan.md)、[Phase 3設計](docs/phase3-mechanics-design.md)、[Phase 3精度レビュー](docs/phase3-accuracy-review.md)、[Phase 3.6観測アダプター](docs/phase3-live-observation.md)、[Phase 3.7診断・記録仕様](docs/phase3-diagnostics-recording.md)、[Phase 4再利用分析](docs/phase4-reuse-analysis.md)、[Phase 4検証記録](docs/phase4-verification.md)、[Phase 4.5 BossMod連携実証](docs/phase4.5-bossmod-integration.md)、[Phase 4.5検証記録](docs/phase4.5-verification.md)、[Phase 5戦略設計](docs/phase5-strategy-engine.md)、[Phase 5検証記録](docs/phase5-verification.md)、[Phase 5実データ検証](docs/phase5-real-data-validation.md)、[Phase 5.5観測情報取得調査](docs/phase5.5-observation-acquisition.md)、[Phase 5.5A観測基盤](docs/phase5.5a-observation-foundation.md)、[Phase 5.5A検証記録](docs/phase5.5a-verification.md)、[実機テスト手順](docs/phase3-live-test-procedure.md)を参照してください。

## 配布上の注意

公式Dalamudリポジトリの制限は、自動戦闘や自動ゲームプレイを許可していません。将来のAutonomous機能は公式配布対象とは仮定せず、実装前に配布方針とリスクを別途判断します。
