# Stage 5.6A — 環境オブジェクト診断

## 実装境界

Stage 5.6Aは、Dalamud API 15の公開`IObjectTable` / `IGameObject`から得られる汎用オブジェクト観測を、ユーザーの明示操作中だけローカルへ記録する診断機能である。転移装置、宝箱の内容、部屋、通路などの意味は判定しない。BFS、Phase 5戦略判断、`WorldSnapshot`、ゲーム操作には接続しない。

初期対象は公開`ObjectKind`の`EventObj`、`Treasure`、`Aetheryte`だけである。各フレームで値型へコピーし、`IGameObject`参照を保持しない。

## 保存フィールド

- `objectKind`
- `baseId`
- 記録セッション内だけで有効な`anonymousObjectId`
- nullableのX/Z座標と回転
- `usable` / `quarantined` / `missing`の座標品質
- `isTargetable`
- 観測時刻
- B1のDD種別・10階帯、取得済みの場合だけ正確な階層
- 匿名の観測セッションID、provenance、固定理由コード

生の`EntityId` / `GameObjectId`、名前、チャット、キャラクター・アカウント識別情報は保存しない。匿名IDは消滅、ランタイムID再利用、観測セッション変更、確認済み階層変更で再発行する。

## 座標品質

- `Usable`: finite、ライフサイクル有効、X/Zが原点でない。
- `Quarantined`: ライフサイクルが不確実、または独立した裏付けのない(0,0)。値は診断用に残すが判断には利用しない。
- `Missing`: 取得失敗、値なし、NaN/Infinity。

同一Territory内の階層移動を公開APIだけで常に検出できない。正確な階層がないサンプルは`floorEpochId: null`かつ`environment-object.floor-continuity-unverified`とし、同一フロア継続を主張しない。

## 独立JSON

ファイルは`environment-object-diagnostics`配下へ保存する独立schema 1である。通常リプレイschema 3/4/5と階層診断schema 2は変更しない。

既定上限は512 KiB、1,000サンプル、15分、最小250 ms間隔。上限、保存例外、取得例外では環境診断だけを停止し、`DisabledExecutionGateway`を含むプラグインの安全境界には触れない。エクスポートと削除は内容確認を伴う明示操作のみ許可する。

## 実機検証手順

1. ユーザーが開発プラグインとして0.5.6.0を読み込み、`/ddcore`で画面を開く。
2. 「環境オブジェクト診断」がOFFで起動することを確認する。
3. DD内の対象を画面に入れ、診断開始、短時間の観測、診断停止の順に操作する。
4. ObjectKind別件数、座標品質、保存容量、停止理由を確認する。
5. プレビューの匿名性を確認してからエクスポートする。
6. JSONに生ID、名前、チャットがなく、schema 1、DD種別・階層帯、匿名ID、座標品質が記録されることを確認する。
7. 階層移動、DD退出・再入場、対象消失を別々に記録し、古い対象や匿名IDが現在値として残らないか確認する。

実機検証前は、特定BaseIdの意味、対象ObjectKind、targetable状態、同一Territory階層移動時の公開観測挙動を確認済みと扱わない。

## Stage 5.6B 選択対象照合・状態遷移

schema 2では公開`ITargetManager.Target`からコピーしたObjectKind/BaseId/匿名化対象座標/targetableを、同一サンプルの`IObjectTable`値とRuntimeId+ObjectIndex+Kind+BaseIdの全一致で照合する。永続化するのは一致した匿名IDと品質・根拠コードであり、生IDは保存しない。不一致・複数一致・座標品質不良は別品質として記録し、候補を推定で選ばない。

対象ごとのtargetable変更、観測消失、ランタイムキーの再出現をtransitionとして記録する。消失理由は`disappeared-not-open-confirmed`、再出現理由は`runtime-key-returned-continuity-unknown`とし、再出現時は新しい匿名IDを割り当てる。いずれも開封済みとは判定しない。診断停止・再開始と観測セッション/正確な階層変更で照合状態を引き継がない。schema 1の旧JSONは新フィールドなしでも読込可能で、schema 2を新規出力する。

画面上で選択した対象とBaseIdの対応は未検証。Stage 5.6Bの再調査では、固定replay exports内にはBaseId 2007357/2007358がなかった一方、別の`recordings`配下にあるschema-1環境診断JSONで両IDを確認した。詳細なサンプル番号・時刻・連続観測の差分は[Stage 5.6B照合資料](phase5.6b-object-correlation.md)に記録した。targetable変化と観測消失は原本JSONから確認できたが、JSONに選択ターゲット情報や開封イベントはなく、画面上の各宝箱との対応・開封の因果関係は未確定のまま維持する。
