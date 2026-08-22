# GT-001 原作EXE観測スパイク

このフォルダは、原作EXEの観測値をUnity基準値と混同せず、再検査可能なJSONLへ変換するためのPhase 6A成果物です。原作プロセスへの自動注入は行いません。ブレークポイントで得た値をCSVへ保存し、`convert_gt001_probe.py`でhash検査と入力境界検査を行います。

## 固定対象

- 原作ゲーム環境: `Logs/WindomXP`
- EXE: `Logs/WindomXP/WindomXP_orig.exe`
- EXE SHA-256: `EC5A09973CD00C1BAB7AD7FE284293C06A415C65378410E31E4534327CCC20C1`
- 機体: `Logs/WindomXP/ROBO/KD-03`
- ANI SHA-256: `CA1D7A9CF134ECC305DC646BAB7EA8CF9E2421ACF9ED3B2108D5591DF97C9C6A`
- SPT SHA-256: `4414870017376ACE0DEAAE8D64EE6237D02005758B43CF4C62D6EEC9BE324A3D`
- x86 / image base `0x00400000`
- シナリオ: GT-001、全22tick
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

`velocity_after_*`は同じ呼び出しから戻った直後の`[ECX+0xA7C/0xA80/0xA84]`です。呼出元のreturn addressへ一時ブレークポイントを置く場合、別スレッド・別機体の停止と混同しないよう入口時のECXを照合します。

位置は行列`[ECX+0x78C/0x790/0x794]`ですが、Unity座標・scaleとの正規化が未確定なためPhase 6Aの既定CSVには含めません。未確定変換を適用して`runtime.rootPosition`と比較しないでください。

## 取得手順

1. `Logs/WindomXP/WindomXP_orig.exe`を通常起動し、起動設定で`Launch`を選ぶ。x32dbgからEXEを直接開始すると起動設定段階で失敗したため、実行中プロセスへのAttachを使う。
2. x32dbgの環境設定で`ユーザー TLS コールバック`と`システム TLS コールバック`の自動停止を無効にする。この設定変更後はDetach／Attachし直す。
3. 実行中の`WindomXP_orig.exe`へAttachし、`bp 0x004CD840`と`bp 0x004D2030`を16進表記で設定する。`0x`を省略すると10進値として解釈されるため省略しない。
4. `gt001_probe_template.csv`を作業用ファイルへコピーする。
5. `KD-03`の対象機体ECXを固定し、GT-001開始前の2 idle tickから22tickを採取する。
6. 同じ初期状態・入力で最低3回取得し、CSV値が一致することを先に確認する。
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
- 接触補正後の位置、HOD frame、Generatorの原作offsetはまだ観測契約へ入れていません。
- CSVが作れない場合もUnity実装を推測修正せず、Ghidra側で対象ポインターとoffsetの根拠を追加します。
