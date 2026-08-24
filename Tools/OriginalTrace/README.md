# GT-001 原作EXE観測スパイク

このフォルダは、原作EXEの観測値をUnity基準値と混同せず、再検査可能なJSONLへ変換するためのPhase 6A成果物です。原作プロセスへの自動注入は行いません。ブレークポイントで得た値をCSVへ保存し、`convert_gt001_probe.py`でhash検査と入力境界検査を行います。

## 現在の扱い

- 既存Run 1～3とschema v1 JSONLは、GT-001の取得経路と22tick適合traceの証拠として維持します。
- Phase 6Bの接地Move保持は、一次擬似コード、原作EXEのアンカー観測、schema v1一致、Unity回帰検証を組み合わせて受入済みです。
- schema v2の22tick完全取得は既定工程ではありません。同形式での原作観測一致を明示的な受入条件とする場合だけ再開します。
- 新しい観測は[`OBSERVATION_TICKET_TEMPLATE.md`](OBSERVATION_TICKET_TEMPLATE.md)で疑問、処理段階、最小フィールド、成功条件、停止条件を固定してから開始します。

## 固定対象

- 原作ゲーム環境: `Logs/WindomXP`
- EXE: `Logs/WindomXP/WindomXP_orig.exe`
- EXE SHA-256: `EC5A09973CD00C1BAB7AD7FE284293C06A415C65378410E31E4534327CCC20C1`
- 機体: `Logs/WindomXP/ROBO/KD-03`
- ANI SHA-256: `CA1D7A9CF134ECC305DC646BAB7EA8CF9E2421ACF9ED3B2108D5591DF97C9C6A`
- SPT SHA-256: `4414870017376ACE0DEAAE8D64EE6237D02005758B43CF4C62D6EEC9BE324A3D`
- x86 / image base `0x00400000`
- 既存証拠シナリオ: GT-001、全22tick
- 原作direction: tick 0～1は待機5、2～13は前進8、14～21は待機5
- 論理入力: tick 0～1は0、2～13は前進action 1、14～21は0

CSVの`tick`は取得indexとして0～21です。既存Phase 5 JSONLは最初のsimulation結果を`tick=1`として記録するため、変換後JSONLは`tickOrigin=1`、tick 1～22になります。

CSVの`direction`は原作ランタイムのrawコードを保持します。変換後JSONLでは、比較対象の`input.direction`だけ待機`5`をUnity入力規約の`0`へ正規化し、原作rawコードは`input.rawDirection`へ残します。`input.rawDirection`は取得証拠であり、Unity基準との`observedFields`比較には含めません。前進`8`は両規約で同じため変換しません。

完全停止時のMove Zに残る`2.8026e-45`はfloat32の非正規化残留値です。比較用`scriptedVelocity`ではfloat32最小正規値未満の非0値だけを0へ正規化し、取得値は`rawScriptedVelocity`とraw CSVへ保持します。通常の移動値と解放後の減衰値にはこの正規化を適用しません。既定profileは`original-direction5-idle-to0-subnormal-to0-v1`です。

ASLRや別実行ファイルを使う場合、絶対アドレスをそのまま流用しません。最初にmodule baseとEXE hashを確認してください。

## 一次コード上の観測点

### Action遷移

`FUN_004d2030`、VA `0x004d2030`。

- x86 `thiscall`の`ECX`: 対象機体ランタイム
- `[ESP+4]`: 要求action ID

GT-001では初期値0から、前進開始時の1、解放後の0を保持値としてCSVの`logical_action`へ記録します。別経路のaction開始が見つかった場合は、取得を続けず根拠関数を追加確認します。

### 60Hz Motion tick

`FUN_004cd840`、VA `0x004cd840`。この関数への対象機体の呼び出し1回を1tickとして採取します。

入口時の`ECX`を対象機体ポインターとし、次を読む候補にします。

| CSV | 原作ランタイム | 根拠 |
| --- | --- | --- |
| `direction` | byte `[ECX+0x30]` | `FUN_004d0480`の方向コード読取 |
| `velocity_before_*` | float `[ECX+0xA7C/0xA80/0xA84]` | `FUN_004cd840`の積分開始速度 |
| `force_*` | float `[ECX+0xAE8/0xAEC/0xAF0]` | 同関数冒頭で速度へ加算 |
| `scripted_velocity_*` | float `[ECX+0xAD0/0xAD4/0xAD8]` | Move値。`0xA88`適用前の値 |
| `airborne` | int `[ECX+0xBA8]` | 0=接地、1=空中 |

`scripted_velocity_*`は関数入口の保持率適用前Moveです。Phase 6B用CSVでは、さらに`move_retention`（`+0xA88`）と`scripted_velocity_after_retention_*`（乗算後Move）を追加できます。変換器はこの4列が揃ったときschema v2を生成し、`scriptedVelocityBeforeRetention`／`moveRetention`／`scriptedVelocityAfterRetention`として`observedFields`へ列挙します。この対応はschema v2を受け入れるための互換機能であり、新しい22tick取得を必須にするものではありません。既存Run 1～3とschema v1 JSONLは取得証拠として変更しません。

`velocity_after_*`は同じ呼び出しから戻った直後の`[ECX+0xA7C/0xA80/0xA84]`です。呼出元のreturn addressへ一時ブレークポイントを置く場合、別スレッド・別機体の停止と混同しないよう入口時のECXを照合します。

位置は行列`[ECX+0x78C/0x790/0x794]`ですが、Unity座標・scaleとの正規化が未確定なためPhase 6Aの既定CSVには含めません。未確定変換を適用して`runtime.rootPosition`と比較しないでください。

## 取得手順

以下は既存GT-001の再現、または観測チケットで22tick適合traceが必要と判断された場合の手順です。アンカー観測ではチケットに書いた最小窓だけを取得し、未観測tickを推定値で補いません。

1. `Logs/WindomXP/WindomXP_orig.exe`を通常起動し、起動設定で`Launch`を選ぶ。x32dbgからEXEを直接開始すると起動設定段階で失敗したため、実行中プロセスへのAttachを使う。
2. x32dbgの環境設定で`ユーザー TLS コールバック`と`システム TLS コールバック`の自動停止を無効にする。この設定変更後はDetach／Attachし直す。
3. 実行中の`WindomXP_orig.exe`へAttachし、残留BPが空であることを確認する。まず低頻度の`bp 0x004D2030`でactionと現在PIDの自機ECXを確定し、その後だけ対象ECX限定の`bp 0x004CD840`を使う。`0x`を省略すると10進値として解釈されるため省略しない。
4. `gt001_probe_template.csv`を作業用ファイルへコピーする。
5. `KD-03`の対象機体ECXを固定し、GT-001開始前の2 idle tickから22tickを採取する。
6. GT-001は計測経路の初回妥当性確認を兼ねるため、同じ初期状態・入力で最低3回取得し、CSV値が一致することを先に確認する。
7. 次の形式でJSONLへ変換する。

```powershell
python Tools/OriginalTrace/convert_gt001_probe.py `
  path/to/gt001.csv Logs/TestPlayOriginal/GT-001.original-observation.jsonl `
  --exe "Logs/WindomXP/WindomXP_orig.exe" `
  --ani "Logs/WindomXP/ROBO/KD-03/Script.ani" `
  --spt "Logs/WindomXP/ROBO/KD-03/Script.spt" `
  --mech-id KD-03
```

8. Unityで`Tools > WindomXP > Test Play > Run Selected-Mech GT-001 Reference Trace`を実行し、`Logs/WindomXP/ROBO/KD-03`を選ぶ。再実行時は`Run Last Selected-Mech GT-001 Reference Trace`を使える。
9. `Tools > WindomXP > Test Play > Compare Original Observation Trace`から生成JSONLを選ぶ。

比較結果は`Logs/TestPlayOriginalCompare/GT-001.first-mismatch.txt`へ保存されます。観測できなかったフィールドは`observedFields`へ含まれず、ゼロ値として一致扱いされません。

## スパイクの制限

- x32dbg 2026.05.27を`Logs/OriginalTraceTools/`へ配置し、通常起動した原作プロセスへのAttachを確認済みです。物理キー入力と対象機体ECX条件付きブレークポイントを組み合わせ、Run 1～3の22tickを採取済みです。3回のraw CSVと変換後JSONLはそれぞれbyte単位で一致しています。
- `Logs/WindomXP/ROBO/KD-03`のANI/SPT hashはプロジェクト基準と完全一致します。hash別Unity基準は`Logs/TestPlayGolden/ByDataHash/ca1d7a9cf134ecc3_4414870017376ace/GT-001.unity-reference.jsonl`へ生成済みです。
- Actionは`FUN_004d2030`を通るGT-001範囲だけを最初の対象とします。
- 現在の変換器、22tick入力境界検査、Selected-Mech基準生成はGT-001専用です。全GT対応へ先行共通化せず、追加観測の条件を満たす2つ目の代表シナリオが実際に選ばれた時点で、hash、ヘッダー、`observedFields`、scenario catalog入力の実共通部分だけを分離します。
- GT-001の3回一致は既存証拠として維持します。同じ機体ECX、offset、ブレーク位置を再利用する後続の代表観測は2回一致を既定とし、不一致、新規ポインター／offset、非決定性の疑いがある場合だけ3回目を追加します。
- GT番号ごとの全件観測は行いません。共有Coreの解釈が一意でない、実装後に説明できない実機差が残る、複数候補から選べない、原作観測一致を主張する、またはtraceの処理段階が揃っていない場合だけ、挙動クラスから代表1ケースを追加します。
- 接触補正後の位置、HOD frame、Generatorの原作offsetはまだ観測契約へ入れていません。
- CSVが作れない場合は、共有Coreの確定に観測が必須かを先に再判定します。必須ならGhidra側で対象ポインターとoffsetの根拠を追加し、必須でなければ`原作推定`または`Unity代替`として範囲を明記し、全体工程を停止しません。

## 停止規則

- 同じ機体ECX取得方法、offset、ブレーク位置を再利用する観測は2回一致で終了します。値の不一致、新規ポインター／offset、非決定性の疑いがある場合だけ3回目を追加します。
- 2セッション連続で対象特定、入力受理、BP運用に失敗した場合は、その観測を停止します。入力初期化や別アクションへ調査範囲を広げず、一次資料へ戻るか証拠レベルを降格します。
- 自動入力は`FUN_004d2030`などの低頻度入口で要求actionが記録された場合だけ、受理済み入力として扱います。画面変化やキー送信成功だけではtraceへ採用しません。
- 高頻度BPは対象ECX限定のワンショットまたは非停止ログにします。取得後はBP撤去、実行再開、実行中デタッチの順で終了します。
- 観測中に見つかった別の疑問は同じセッションで追跡せず、新しい観測チケットへ分離します。
