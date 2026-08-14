# テストプレイ再設計 Phase 5 実装記録

## 結果

Phase 5では、Phase 0で定義したGT-001～GT-010を、実機体`ガンダムTR-1ヘイズル改`の`Script.ani` / `Script.spt`へ接続するgolden trace基盤を追加しました。同じ初期状態と入力列を2回実行し、Action、Motion、Combat、Presentation、入力、リソース、ANI進行、ルート姿勢を含む全tickがbyte単位で一致するか検査します。

このtraceは原作実行ファイルから直接採取した観測値ではありません。実ANI／SPTを現在のUnity Coreで実行した`RealAniObserved`基準値であり、原作確定値、原作推定値、Unity表示代替の区分はPhase 0～4のまま維持します。

ANI / AN2 / HOD / SPTの読み書き、既存public Inspectorフィールド、MonoBehaviour名、UnityEvent、シーン／Prefab参照は変更していません。

## 決定的な入力注入

`TestPlayController`へ、通常の`StartTestPlay()`とは独立した検証用入口を追加しました。

- `BeginDeterministicTraceSession()`: 入力、Core、SPT状態、武器、接地／空中状態を初期化する。HUD、カメラ、ハードウェア入力は生成しない。
- `SimulateDeterministicTraceTick()`: 明示した1tick分の入力を注入し、原作基準の60Hz simulationを正確に1tick進める。
- `EndDeterministicTraceSession()`: BURNERや発射体などの一時表示物を破棄してセッションを閉じる。

描画フレーム時間、Unity `Input`、Instance ID、生成GameObjectの自動suffixはtraceへ含めません。

## Phase 5 tick trace

Phase 4 JSONへ次を追加します。

| 区分 | 主なフィールド |
| --- | --- |
| 入力 | direction、rise、boost、shot、melee、guard、lock |
| リソース | HP、移動エネルギー、補助エネルギー |
| ANI進行 | frame、script、script tick、action tick、終端姿勢保持 |
| 状態 | airborne、grounded、target locked |
| Unity境界 | root position、root rotation |

セッション先頭にはschema、scenario、機体ID、ANI／SPTのSHA-256、tick rate、根拠区分をJSONL headerとして保存します。各tickはCulture非依存のround-trip数値で直列化し、セッション全体にもSHA-256を付けられます。

## GT-001～GT-010

| ID | 入力列 | 主な確認境界 |
| --- | --- | --- |
| GT-001 | 待機→前進→解放 | idle／move復帰、Move |
| GT-002 | Zを1tick押下 | 上昇開始、空中復帰、Force |
| GT-003 | Zを80tick保持 | 非ループ上昇姿勢、解放後復帰 |
| GT-004 | 空中前進→解放 | air move／air idle、Force |
| GT-005 | 方向を二度押し | step開始、接地復帰、Move |
| GT-006 | 空中でZを二度押し | boost開始／終了、Move |
| GT-007 | Xを1tick押下 | 射撃、AttackDelay、復帰 |
| GT-008 | Cで持替え→方向+C | ChangeWeapon、方向格闘 |
| GT-009 | 剣状態でCを3回 | SwordCancel、RunProc2、連携復帰 |
| GT-010 | ロック→左+X | target lock、ShotTurnAng、射撃旋回 |

各シナリオは、必要なANI action slotと命令文字列が実データに存在することを先に検査します。その後、実際に観測したaction列も検査し、単に同じ誤動作を2回繰り返しただけの成功を避けます。

## 検証と出力

通常の統合検証は次のメニューです。

`Tools > WindomXP > Test Play > Run Runtime Verification`

Phase 5は82 assertionsに、非ロック移動基準／BURNER方向・原作テクスチャ・実描画・Particleシェーダーの回帰検査10件を追加し、Phase 4までの373件と合わせて465件を対象にします。scenario catalog、JSONL／SHA-256、最初の不一致tick、ハードウェア非依存replay、実ANI／SPTのmanifestを検査します。

2026-08-15にUnity `6000.5.0f1`で実測し、スクリプトコンパイル成功後に465 assertionsが全件通過しました。実機体manifestは、拡張子`Script.ani`の先頭signatureが対象データでは`AN2`であることを確認し、ローダーが正式対応する`ANI`／`AN2`のいずれかを要求します。

実機体E2Eは次の専用メニューです。

`Tools > WindomXP > Test Play > Run Real-Mech Golden Traces`

読み込みをUnityメインスレッドで同期待機せず、非同期にANIを1回ロードします。各GTを新規ランタイムで2回実行し、全tickとhashの完全一致を確認します。基準traceは`Logs/TestPlayGolden/GT-xxx.unity-reference.jsonl`へ生成します。`Logs`以下は検証生成物であり、ゲームデータやUnity assetではありません。

同日の実測ではGT-001～GT-010が10/10成功し、各シナリオ2回の全tickとSHA-256が完全一致しました。実ANI hashは`ca1d7a9cf134ecc305dc646bab7ea8cf9e2421acf9ed3b2108d5591df97c9c6a`、実SPT hashは`4414870017376ace0deaae8d64ee6237d02005758b43cf4c62d6eec9be324a3d`です。出力された10個のJSONLはすべて非空で、session headerの`baseline`は`RealAniObserved`です。

## 未解決境界

- `RealAniObserved` traceは原作EXEの観測traceではない。原作との数値差を確定するには、原作側の同一入力・同一初期状態traceが必要。
- RunProc、BURNER、CamEffectの個別表示式はUnity代替／未確定のまま。BURNERの素材は原作`burner.png`へ置換した。実SPT/HODと目視受入ではOutputボーン自身が`UP`／`DOWN`双方の外向き姿勢を持つため、Unity表示Adapterは両者とも追加回転なしのローカルZプラスとしてプレビュー系で統一した。`output`から長さ・太さ・アルファへの原作式は未確定。
- Collider、物理衝突、AudioSource、Camera描画はgolden core replayから除外している。表示Adapterは別系統とし、BURNERについてはactive Render PipelineでRenderTextureへ可視画素が出ることと、Particleフォールバックがエラーシェーダーを使わないことを検査する。
- 効果音・ボイスは命令イベント、原作素材割当、`AudioSource.PlayOneShot`まで自動確認できるが、2026-08-15の受入環境では聴感確認を保留している。

## 次フェーズへの入口

次フェーズでは、golden traceの最初の不一致tickを診断単位として使い、原作観測traceを取得できるシナリオからAction、Motion、Combat、Presentationの差分を縮めます。原作traceがない項目は`RealAniObserved`基準値のまま固定し、原作一致とは表記しません。
