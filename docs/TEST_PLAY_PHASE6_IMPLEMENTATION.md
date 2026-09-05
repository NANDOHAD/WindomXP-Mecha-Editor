# Test Play Phase 6A/6B 原作EXE部分観測trace比較

## 目的

Phase 6Aでは、Phase 5の`RealAniObserved`決定性traceと、原作EXEから実際に取得できた観測値を混同せずに比較する基盤を追加しました。原作側で観測できない値を0として補わず、セッションヘッダーの`observedFields`に列挙された項目だけを比較します。Phase 6Bでは、ANI `Move`の処理段階を入口値・保持率・適用後値へ分離し、2026-08-25に証拠境界を明示したうえで接地Move保持を受け入れました。

`Logs/WindomXP`の原作EXEからGT-001を3回観測し、承認された`Logs/WindomXP/ROBO/KD-03`と同一ANI/SPTのUnity基準へ接続しました。比較表現の正規化とColliderを使わない決定性traceの論理接地面だけを追加し、実シーンのCollider接地経路およびAction／Motion／Combat／Presentation Coreの実挙動はまだ変更していません。したがって、確認した一致範囲と最初の不一致を記録しますが、GT-001全体の原作一致は示しません。

## 追加ファイル

| ファイル | 役割 |
| --- | --- |
| `Assets/Scripts/TestPlay/TestPlayOriginalTrace.cs` | 原作観測JSONL契約、部分フィールド比較、最初の不一致診断。 |
| `Assets/Editor/TestPlayOriginalTraceComparison.cs` | 原作観測JSONLと現在のUnity基準JSONLを比較するメニュー。 |
| `Assets/Editor/TestPlayPhase6Verification.cs` | 観測範囲、欠落値、最初の不一致、許容誤差、hashの回帰検証。 |
| `Tools/OriginalTrace/convert_gt001_probe.py` | GT-001デバッガーCSVのhash／入力境界検査とJSONL変換。 |
| `Tools/OriginalTrace/README.md` | 原作関数、x86レジスター、構造体offset、取得手順と制限。 |

## original-observation契約

先頭行は次の情報を持ちます。

- `schemaVersion=1`（Phase 6A既存成果物）または`schemaVersion=2`（Phase 6B Move段階フィールド付き）
- `source=original-observation`
- `scenario` / `mechId`
- `exeHash` / `aniHash` / `sptHash`
- `tickRate=60`
- `tickOrigin`
- `normalizationProfile`
- `observedFields`

`observedFields`が空のtraceは拒否します。宣言したフィールドがtickレコードに存在しない場合も、既定値0として比較せず`MissingField`診断にします。未登録パスは`UnsupportedField`です。

schema v2のMove段階フィールドは次の3つです。

- `scriptedVelocityBeforeRetention`: `FUN_004cd840`入口のMove。原作側は`+0xAD0/+0xAD4/+0xAD8`、Unity側は同tickのANI Moveを方向変換した値。
- `moveRetention`: action開始時に設定される`+0xA88`保持率。GT-001では前進action 1が`1.0`、入力解放後の待機action 0が`0.8`。
- `scriptedVelocityAfterRetention`: 入口Moveへ保持率を乗算した値。原作側は乗算後のMove、Unity側はroot motionへ渡す前の同値。

既存の`scriptedVelocity`はschema v1との互換性のため残します。schema v2の`observedFields`では処理段階を明示した3フィールドを比較し、v1成果物は従来どおり入口Move相当の`scriptedVelocity`を読み込みます。

Phase 0の契約はtickを0始まりとしていましたが、既存Phase 5 JSONLは最初のsimulation結果を`tick=1`として記録しています。既存基準を変更せず、Phase 6Aでは取得CSVの0始まりindexとJSONLの`tickOrigin=1`を分離して記録します。tickレコードは開始値以降が連番であることを検査します。

## 比較規則

- action、channel、入力、boolean、tick、文字列は完全一致。
- Force、Move由来`scriptedVelocity`、velocity、resource値は正規化後に比較する。Focused verificationの既定比較は完全一致を維持し、実原作trace比較メニューだけは、原作デバッガーの十進float32表記とUnityのround-trip float32表記の差を吸収する明示的な`1e-7`許容誤差を使う。
- 原作待機direction `5`は比較用`input.direction=0`へ変換し、`input.rawDirection=5`へ保持。
- float32最小正規値未満の非0 Move残留値だけ比較用`scriptedVelocity=0`へ変換し、`rawScriptedVelocity`とraw CSVへ保持。
- `runtime.rootPosition` / `rootRotation`だけ既定`0.00001`の明示的な許容誤差。
- ANI/SPT hash、機体ID、シナリオ、tick rateが異なるセッションはtick比較前に拒否。
- 最初の不一致だけを診断単位とし、既定で前後2tickの原作／Unityレコードを保存。

比較メニュー:

`Tools > WindomXP > Test Play > Compare Original Observation Trace`

結果:

`Logs/TestPlayOriginalCompare/<GT-ID>.first-mismatch.txt`

原作観測ヘッダーのANI/SPT hashに一致するUnity基準を`Logs/TestPlayGolden/ByDataHash/<short-hash>/`から先に解決します。通常の基準traceは完全hashが一致するときだけ後方互換経路として利用します。短縮hashはWindowsの深い作業パス対策で、最終比較ではJSONLヘッダーの完全hashを検査します。

## GT-001取得スパイク

一次コードから次の観測候補を固定しました。

- `FUN_004d2030` (`0x004d2030`): action開始。x86 thiscallのECXと`[ESP+4]`。
- `FUN_004cd840` (`0x004cd840`): 60Hz Motion積分。
- direction: `[ECX+0x30]`
- velocity: `[ECX+0xA7C/0xA80/0xA84]`
- Force: `[ECX+0xAE8/0xAEC/0xAF0]`
- Move: `[ECX+0xAD0/0xAD4/0xAD8]`
- airborne: `[ECX+0xBA8]`

位置行列のtranslationは`[ECX+0x78C/0x790/0x794]`ですが、Unity座標とscaleの正規化が未確定なので既定比較から除外しています。

x32dbg 2026.05.27を`Logs/OriginalTraceTools/`へ配置し、`Logs/WindomXP/WindomXP_orig.exe`の通常起動と、実行中プロセスへのAttachを確認しました。デバッガーからEXEを直接開始すると起動設定段階で失敗するため、通常起動後のAttachを採用します。操作可能な戦闘中の`0x004CD840`では`ECX=0x028EC550`と`ECX=0x171A05B0`が交互に観測され、両者の待機時`[ECX+0x30]`は`5`でした。これは一次擬似コード`FUN_004d4f60`の待機分岐と一致します。入力試行時の`0x004D2030`停止は`ECX=0x028EC550`、要求action `[ESP+4]=1`だったため、自機を`0x028EC550`と確定しました。完全停止後もMove Zは厳密な0ではなく`2.8026e-45`を保持します。Run 1～3は待機5の2tick、前進8の12tick、解放後待機5の8tickを採取し、`Logs/TestPlayOriginal/GT-001.run1.csv`～`run3.csv`へ転記しました。解放後のMove Zは`0.08`から`0.0167772`までtickごとに減衰し、3回のraw CSVと変換後JSONLはそれぞれSHA-256まで完全一致しました。

変換profile `original-direction5-idle-to0-subnormal-to0-v1`で、待機direction `5`と停止時Move Z `2.8026e-45`をrawフィールドへ保持しつつ比較値だけ正規化しました。また、`GroundedGun`決定性traceがCollider無効時に初期接地を失って重力を蓄積していたため、決定性sessionに限る論理接地面を追加しました。実シーンの`useColliderGrounding=true`経路は変更していません。

正規化後のRun 3は、待機2tickと前進12tickについて`input.direction`、`logicalAction`、`runtime.grounded`、`velocityBefore`、`force`、`scriptedVelocity`がUnity基準と一致しました。意味のある最初の不一致は取得index tick 14（JSONL tick 15）の入力解放直後にあり、原作`scriptedVelocity Z=0.08`に対してUnityは`0`です。以後、原作は`0.08, 0.064, 0.0512, ...`と0.8倍で減衰しますが、Unityは待機actionへの遷移時に即時0としています。レポートは`Logs/TestPlayOriginalCompare/GT-001.first-mismatch.txt`です。

## 2026-08-23 再評価

### Phaseごとの現在地

| Phase | 判定 | 現在の意味 |
| --- | --- | --- |
| Phase 0～4 | 完了 | 差分台帳、型付きANI、Action／Motion／Locomotion、Combat、Presentationの段階移行と回帰検証を実装済み。 |
| Phase 5 | 完了 | GT-001～GT-010を実ANI/SPTへ接続し、`RealAniObserved`として決定性を確認済み。原作一致の証拠ではない。 |
| Phase 6A | 基盤完了 | 部分観測契約、hash一致、3回再現、最初の不一致診断まで完了。Core挙動は未変更。 |
| Phase 6B | 完了（証拠境界付き） | Move保持率と観測段階を分離するCore、schema v2受け入れ、既存v1比較を実装。機構は一次擬似コードと原作アンカー観測で確定し、schema v1は22tick一致。原作schema v2全tick一致は未取得のため主張しないが、非ブロッキングとする。 |

### 原作のMove減衰経路

入力解放後の0.8倍減衰は未特定のままではなく、一次擬似コード上で次の経路まで確認できます。

1. 前進action 1開始は`FUN_004d2030(..., param_6=1.0, ...)`を呼ぶ（`WindomXP_orig_decompiled.c` 94414～94418行）。
2. 入力解放を処理する`FUN_004d5420`は待機action 0を`param_6=0.8`で開始する（同94517～94524行）。
3. `FUN_004d2030`は`param_6`を機体状態`+0xA88`へ保存する（同93546～93549行）。
4. `FUN_004cd840`は各tickでMove `+0xAD0/+0xAD4/+0xAD8`へ`+0xA88`を乗算し、その後に位置へ加算する（同92378～92416行）。

したがって、原作観測の入口Moveは解放tickで`0.08`、同tick内の乗算後Moveは`0.064`、次tick入口は`0.064`です。

### 2026-08-23 x32dbg追加観測

通常起動後にAttachした`Logs/WindomXP/WindomXP_orig.exe`で、上矢印の前進・解放を追試した。`FUN_004D2030`の条件付きログは`EIP=004D2030 ECX=1AD22010 ARG=0 OBJ30=5 RET=1065353216`であり、解放要求`ARG=0`と待機方向`5`を確認した。続く`FUN_004CD840`入口は`ECX=1AD22010 DIR=5 M0=1 M1=0 M2=1034147594 RET=1061997773`で、入口Move Zはfloat32 `0.08`、保持率は`0.8`だった。

同じ機体を対象にした`0x004CDFC4`の追加ログは`M2=1032000111`（float32 `0.064`）、`RET=1061997773`となり、解放tick内の保持率適用後Moveを実値で確認した。これは一次擬似コードの「`+0xA88`をMoveへ乗算してから位置へ加える」経路と一致する。観測BPは解除し、原作は実行状態へ復元した。既存の原作schema v1 Run 1～3と原作フォルダ内データは変更していない。この時点ではschema v2の22tick JSONL化と全tick比較を最終条件として保留したが、この条件は後述する2026-08-25の受入境界で非ブロッキングへ置き換えた。

前進入口の条件式が別機体へ流れる問題を切り分けるため、`FUN_004CD840`のブレーク条件／ログ条件を`ecx==0x1AD22010 && dword:[ecx+0x30]==8`として再設定した。上矢印を短く1回押した時、x32dbgの停止ログは`V2_FORWARD ECX=1AD22010 DIR=8 M0=0 M1=0 M2=1034147594 RET=1065353216`だった。対象機の前進入口で`DIR=8`、入口Move Z=`0.08`、保持率=`1.0`を一次実値で確認できた。これは前進1tickの追加証拠であり、22tick全列のschema v2 JSONLではない。採取後は`bc 004CD840`でBPを削除し、Breakpoints欄が空、x32dbgが実行中、ゲーム画面が表示されることを確認した。既存schema v1 Run 1～3、JSONL、原作フォルダ内データは変更していない。

同じ対象条件で`0x004CDFC4`の適用後Moveを一回採取し、停止ログ`V2_FORWARD_AFTER ECX=1AD22010 DIR=8 M0=0 M1=0 M2=1034147594 RET=1065353216`を得た。前進時は入口Move Z=`0.08`、適用後Move Z=`0.08`、保持率=`1.0`であり、保持率1.0の前進tickではMoveが変化しないことを一次実値で確認した。採取後に`004CDFC4`の停止状態を整理し、命令バイトが元の`D9 9A D0 0A 00 00`であることをx32dbgのバイナリエディタで確認してから、ファイルメニューの「デタッチ」を実行した。x32dbgはプロセスなしの停止状態、原作`WindomXP_orig.exe`は実行中のまま残った。schema v2全列の採取ではないため、既存schema v1 Run 1～3、JSONL、原作フォルダ内データは変更していない。

### 2026-08-24 再起動後の前進入口再観測

再起動後に通常起動した`Logs/WindomXP/WindomXP_orig.exe`（PID`62516`）へAttachし、`FUN_004D2030`を`[ESP+4]==1`で条件化した。前進要求時の自機オブジェクトは`ECX=0x02820988`、`[ECX+0x30]=8`、`[ECX+0xA88]=1063675494`だった。同じ対象条件を`FUN_004CD840`入口へ設定し、上矢印を短く1回押したとき、`V2_FORWARD_NEW ECX=02820988 DIR=8 M0=0 M1=0 M2=1034147594 RET=1065353216`を取得した。新プロセスでも前進入口は`DIR=8`、入口Move Z=`0.08`、保持率=`1.0`である。ワンショットBPは発火後に消え、x32dbgを再開して原作を実行状態へ復元した。前進1tickの追加証拠であり、schema v2の22tick全列ではないため、既存schema v1 Run 1～3、JSONL、原作フォルダ内データは変更していない。

同じPID`62516`で`FUN_004CDFC4`を自機`ECX=0x02820988`かつ`DIR=8`に限定して観測し、`V2_FORWARD_AFTER_NEW ECX=02820988 DIR=8 M0=0 M1=0 M2=1034147594 RET=1065353216`を取得した。前進時は適用前後Move Z=`0.08`、保持率=`1.0`である。ワンショットBP発火後に一覧は空になったが、`004CDFC4`の「リストにないブレークポイント」停止が残ったため、x32dbgのファイルメニュー「デタッチ」で復旧した。デタッチ後はx32dbgがプロセスなし、原作`WindomXP_orig.exe`が実行中であることを確認した。schema v2 JSONLの22tick全列ではないため、既存schema v1 Run 1～3、JSONL、原作フォルダ内データは変更していない。

### 2026-08-24 ジャンプ開始入口の単発観測

再起動後に通常起動した`Logs/WindomXP/WindomXP_orig.exe`（PID`69968`）へAttachし、`FUN_004D2030`を`[ESP+4]==3`に限定した。Zキーを一度押した時の停止ログは`JUMP_REQ ECX=02878118 ARG=3 DIR=261 M0=0 M1=0 M2=0 RET=1063675494`だった。したがって、原作のジャンプ開始要求`ARG=3`、要求時方向値`DIR=261`、入口時点のMove各軸0、`+0xA88`の保持率ビット列`1063675494`を一次実値で確認した。取得直後にx32dbgのファイルメニュー「デタッチ」を実行し、x32dbgはプロセスなし、原作は通常タイトルで実行中のまま残った。

これはジャンプ開始入口の単発証拠であり、ジャンプ上昇中の22tick、重力遷移、ANIモーション再生回数を示すものではない。したがって既存の原作schema v1 Run 1～3およびschema v2成果物へ値を追加せず、ジャンプ実装の一次確認資料として保持する。

同じPID`69968`・同じ機体`ECX=0x02878118`で、ジャンプ要求停止中に`FUN_004CD840`へ対象機体限定のワンショットBPを設定した。再開直後の物理積分入口ログは`JUMP_TICK2 ECX=02878118 DIR=261 M0=0 M1=0 M2=0 RET=1063675494`であり、`ARG=3`のジャンプ要求直後も入口Move各軸0、保持率0.9（ビット列`1063675494`）だった。これはジャンプ開始直後の積分入口の追加一次実値であり、上昇中の連続tick、垂直Force適用後値、重力遷移、ANIモーション再生回数の証拠ではない。取得直後にx32dbgをデタッチし、原作は通常タイトルで実行中のまま残った。既存schema v1 Run 1～3およびschema v2成果物へは追加していない。

続く物理値追試ではジャンプ終了後の待機状態を捕捉し、`DIR=5 / V0=V1=V2=0 / F0=F1=F2=0 / M0=M1=M2=0 / AIR=0`だったため、ジャンプ証拠から除外した。対象機体の停止型BPおよびサイレントログは原作の応答停止を招いたため中止し、x32dbgデタッチ後に原作が通常実行へ復旧した。新しいジャンプ値およびschema v2成果物へは追加していない。

再起動後の戦闘画面（原作PID`28592`）では、ゲームウィンドウをフォーカスしてこちらからZキーを送信でき、送信後も原作は実行状態を維持した。ただしAttach時に前回の高頻度`FUN_004CD840` BPが残っており、短時間でhit count`5178`まで増えていたため、Z入力が`ARG=3`として受理されたとは判定しない。残留BPを削除してx32dbgをデタッチし、原作は通常タイトル・実行中へ復元した。今後はAttach後に既存BPを全削除してから、低頻度のジャンプ要求入口だけを観測する。

その後、デタッチ済みPID`28592`へゲーム窓ハンドル経由のZ入力を試したが、入力APIがタイムアウトし、画面が一時的に「応答なし」表示となった。約7.5秒後のアプリ状態は通常タイトル・実行中へ戻ったものの、`INJECT_JUMP_REQ`ログは得られず、Z入力の受理は未確認として扱う。追加入力は重ねていない。

再起動後の戦闘画面（原作PID`41592`）へAttachする際、残留`004D2030` BPを先に削除し、`dword:[esp+4]==3`、ジャンプ要求ログ、サイレント設定をx32dbgの編集画面で確認した。こちらからZを単発送信したが、この再起動後試行では画面上のジャンプおよび`JUMP_REQ`ログを取得できなかったため、入力受理の一次証拠には追加しない。一方、ユーザーは前回のこちらからの1回目のZ入力で機体がジャンプしたと目視確認しているため、入力APIが実操作として成立する可能性は維持する。BPを削除してデタッチし、原作PID`41592`は通常タイトル・実行中の戦闘画面へ復元した。

### 2026-08-23 Phase 6B実装結果

Unity側は、ANI `Move`の保持状態を`Force`およびジャンプ用pending慣性から分離し、各60Hz tickで`moveRetention`を適用した値を次tickのANI Moveへ戻すよう更新しました。`STOP`は従来どおり指定軸だけを停止し、数値0はその軸を上書きしません。GT-001のUnity traceでは、入力解放tickの入口`0.08`、保持率`0.8`、適用後`0.064`、次tick入口`0.064`を記録します。

既存の原作schema v1 Run1〜3は、KD-03のhash別Unity基準と各22tick・154観測値が一致しました。比較メニューにはfloat32の十進表記差だけを吸収する`1e-7`の明示許容を設定しています。原作側の入口／保持率／乗算後を22tick全列で採取したschema v2 JSONLは存在しないため、schema v2形式での原作観測一致は主張しません。

### 2026-08-25 Phase 6B受入境界

Phase 6Bは、次の証拠を組み合わせて接地Move保持の実装を受け入れます。

| 項目 | 判定 | 根拠と表記境界 |
| --- | --- | --- |
| Move保持機構 | 原作確定 | `FUN_004d2030`が保持率を`+0xA88`へ保存し、`FUN_004cd840`が同tick内でMoveへ乗算する処理順を一次擬似コードで一意に確認。 |
| 代表実値 | アンカー観測済み | 前進`0.08 × 1.0`、解放`0.08 × 0.8 = 0.064`を原作EXEで確認。単発値を22tick適合traceとは表記しない。 |
| 既存原作trace | 適合trace一致 | schema v1 Run 1～3が各22tick・154観測値でUnity基準と一致。 |
| Unity実装 | 受入 | 保持Move、Force速度、ジャンプ用pending慣性を分離し、Runtime VerificationとReal-Mech Golden Traceで回帰確認済み。 |
| schema v2全tick | 未取得・非ブロッキング | 同形式での原作観測一致は主張しない。これを明示的な受入条件とする別作業が発生した場合だけ再開する。 |

したがって、新しい原作取得補助ツールは現時点で作成しません。実機体KD-03の前進解放については、数値証明とは別の操作感確認として2026-08-25に手動Test Playを実施し、短い減速と最終停止を確認しました。

### 現行比較の境界

現在のCSV `scripted_velocity_*`は`FUN_004cd840`入口で取得した「保持率適用前Move」です。Phase 6B用CSVではさらに`move_retention`と`scripted_velocity_after_retention_*`を取得し、変換器がschema v2として別フィールドへ出力します。既存Run 1～3のCSV／schema v1 JSONLは変更しません。

Unity traceのschema v1 `scriptedVelocity`は既存互換のため入口Moveを残し、追加した`scriptedVelocityBeforeRetention`／`moveRetention`／`scriptedVelocityAfterRetention`を同じtickへ記録します。`requestedDisplacement`と実際のroot motionには適用後Moveを使います。

Unityの`CaptureDrivenHorizontalVelocity`／`DecayPendingDrivenHorizontalVelocity`／`ApplyPendingDrivenHorizontalInertia`は、直前Moveをジャンプ／空中Forceへ引き継ぐUnity近似です。接地中の待機actionで原作Move状態を保持・減衰する責務ではなく、`ChangeAnimation(idleAction)`ではこのpending状態も消去されます。GT-001修正でこれらを流用せず、既存ジャンプ回帰への影響確認対象として扱います。

## 検証結果

2026-08-29:

- U-005b RunProc2 type 55: `FUN_004f9d00`／`FUN_00502b00`／`FUN_00502cb0`／`FUN_00497cd0`から、WEAPONPOINT ID、目標／初期長、主・線texture、管理slot置換、寿命tick、幅0.075、0.2/tick伸長を固定。Unityは実`sabel.png`／`sabel_line.png`を2層表示し、Projectile／ATTACK判定を生成しない。主層幅、頂点・UV・blendはUnity Adapterとして分離する。
- Unity 6000.5.0f1 Runtime Verification 562 assertions成功。Real-Mech Golden TraceはGT-001～GT-010を各2回実行して完全一致。Console Error 0。

2026-08-25:

- Runtime Verification: 513 assertions成功。action 8のMove保持率`0.99`、空中停止補助`+0.012`の適用条件と31tick境界、共通重力前の処理順に加え、target相対の通常／飛行射撃action選択、左右／後方action欠損時fallback、pose/script/channel同一性を含む。隔離Unity 6000.5.0f1でコンパイルError 0。
- Real-Mech Golden Traces: GT-001～GT-010の10シナリオを各2回実行し、全tick列が完全一致。Console Error 0。
- Phase 6Cジャンプfocused verification: GT-002でaction 7への入場1回とZ解放後action 8を確認。GT-003は実ANIの`5tick × 0.1frame + 180tick × 0.01frame`を完了できる220tick保持とし、最終HODフレーム保持からZ解放後action 8まで確認。
- Phase 6C空中移動解放focused verification: GT-004でaction 4を15tick実行し、実ANIの`Move Z=0.06`とForce Y `0.04`→`0.02`を確認。解放後はaction 8を35tick実行し、`FUN_004d68e0`由来のMove保持率`0.99`、Y速度`< 0.05`時の`+0.012`、31tick境界後の下降を確認。
- Phase 6C射撃focused verification: GT-007でX押下1tickからaction 100への単一入場と39tick継続、tick 18の`AttackDelay(0,100)`／`ATTACK(100,200,0.4,0)`／`AttackFlag=2`／`RunProc2` type 1発射、cooldownのtick 118での0到達、tick 42のaction 6、tick 76のaction 0復帰を確認。発射体の攻撃値は`OriginalScriptProfile`を確認し、type 1の速度・形状・描画式は合否外とした。
- Phase 6C持替え・格闘focused verification: GT-008でaction 18を21tick実行し、tick 22のSword idle、tick 62の方向8＋Cからaction 130へ単一入場、6tick・5/tick消費後のtick 68でaction 136、tick 127のaction 6、tick 161のaction 0を確認。GT-009でtick 12／33のC入力queue、tick 21／37の`SwordCancel=132/133`、tick 22／38の131→132→133遷移、各actionの`ATTACK`／`AttackFlag`値、tick 92のaction 6、tick 126のaction 0を確認。type 55／57命令の発火tickは`RealAniObserved`として固定し、type 57の`Hit`成否・形状・多段条件は合否外とした。
- Phase 6Cロック／射撃旋回focused verification: GT-010でtick 1の後方target lock、tick 4の左＋Xから要求100→action 103、pose/script 103とchannel 0／1、49 action tickすべての`ShotTurnAng=20`による-20度旋回、tick 53のaction 6、34tick後のtick 87でaction 0復帰を確認。Camera構図・注視点・追従感はUnity代替として合否外とした。
- U-005a RunProc type 53／54: `FUN_004f99f0`の`BB_WindLine` 7個・0.07／3.0と`FUN_004f9ba0`の`BB_WindRing2` 1個を非戦闘Presentationへ分離。GT-006でtype 53=1回、type 54=4回、Visual 7／4件、両type由来Projectile 0件を確認。Runtime 554 assertions、全10 Golden Trace×各2回を独立2セット完全一致、Console Error 0。原作乱数列、texture、blend、寿命はUnity表示Adapterとして合否を分離する。
- U-005h RunProc2 type 62 subtype 2: `FUN_0048bdf0`／`FUN_0048bef0`／`FUN_00491af0`／`FUN_0048d5d0`から`BB_WindRing`の位置snapshot、固定`WindRing.png`、初期size 1.0／alpha 255、size +0.3→alpha -24/update、11更新目終了、非戦闘性をCore化した。p4～p11は未使用。Runtime 662 assertions、全10 Golden Trace各2回完全一致、Console Error 0。RunProc type 54の`BB_WindRing2`とDX9描画は分離する。
- U-005i RunProc2 type 62 subtype 3: `FUN_0048abd0`／`FUN_0048ada0`とprimary vtable `0x005B74F0`／`0x005B7BA8`を直接対応付け、variant別texture ID 8／9または43／44、WEAPONPOINT行列追従、`BB_Burner` 11更新／`BB_BurnerBall` 5更新の独立終了、非戦闘性をCore化した。Unityは二枚のquadへ置換し、DX9頂点・UV・blendをAdapterに残す。Runtime 672 assertions、全10 Golden Trace各2回完全一致、検証開始後のテスト／プロジェクト由来Error 0。起動時Package Resolver Error 1件は別件。
- 実機体KD-03手動Test Play: 上入力を短く押して解放後、RootMotion traceでZ位置の前進と、保持Move Zの`0.0018 → 0.0014 → 0.0012 → 0`への減衰を確認。tick 819で停止し、前進解放後の短い減速と最終停止を満たした。Console Error 0。
- Play Mode終了後、Editorは`UI_MechaClean`の編集状態へ復帰した。

2026-08-21:

- Unity C#標準検証: `TestPlayPhase5Verification.cs`は診断0。`TestPlayController.cs`は既知のUpdate内検索／文字列連結の性能警告2件のみ。
- Runtime Verification: 491 assertions成功。決定性Grounded sessionの接地保持と負のY速度非蓄積を追加検査。
- Unity基準GT-001～GT-010: 10個すべて再生成、非空。GT-001は22tickとして新parserで再読込成功。
- Python変換器: `py_compile`成功、`--help`起動成功。
- Run 1 raw CSV変換: 22tick、EXE/ANI/SPT hash、direction `5/8/5`の境界検査に成功。部分観測JSONLは`velocityBefore`、`force`、`scriptedVelocity`を含み、未観測の`velocityAfter`を含まない。
- Run 2 raw CSV変換: Run 1と同じ検査に成功。raw CSVと`mechId=KD-03`の部分観測JSONLはいずれもRun 1とbyte単位で一致。
- Run 3 raw CSV変換: Run 1～2と同じ検査に成功。3回のraw CSVと`mechId=KD-03`の部分観測JSONLはいずれもbyte単位で一致。
- Unity比較: Console Error 0。正規化後の最初の不一致は入力解放index tick 14の`scriptedVelocity`（original `0.08`、Unity `0`）。
- Console Error: Runtime Verification／GT再生成に起因するErrorなし。
- 原作ゲーム環境`Logs/WindomXP`: EXE hashは逆コンパイル対象と一致。`ROBO/KD-03`のANI `ca1d7a...c9c6a`、SPT `441487...24a3d`はプロジェクト基準と完全一致。
- `ROBO/KD-03`をUnity Coreへ読み込んだGT-001: 22tickを2回実行し、byte単位で完全一致。hash別Unity基準を生成済み。

## 再評価後の工程

### Phase 6B-1: GT-001観測契約を同じ処理段階へ揃える

1. 既存Run 1～3のCSV／JSONLはPhase 6A証拠として変更せず保存する。
2. 原作側は、関数入口のMove、`+0xA88`保持率、乗算後Moveを別フィールドとして取得する。乗算後の正確なブレーク位置は逆コンパイル行だけで決めず、x32dbgの実命令列で確定する。
3. Unity側も、保持率適用前Move、保持率、適用後Moveを同じ60Hz tickへ記録する。既存schema v1の`scriptedVelocity`の意味は変更せず、後方互換を維持した追加schemaまたは明示フィールドで拡張する。
4. 原作の位置はraw値を保存してよいが、Unity座標／scaleが確定するまで`runtime.rootPosition`や要求変位との合否判定には使わない。

この旧完了条件は2026-08-25の受入境界で置き換えました。schema v2の22tick完全取得は、同形式での原作観測一致を明示的に要求する場合だけの追加条件です。通常のPhase 6B受入では、一次擬似コードで一意な処理順、原作アンカー観測、schema v1適合trace、Unity回帰検証を使用します。

### Phase 6B-2: Move保持をMotion Coreへ実装する

1. action開始時の保持率と、ANI `Move`／`STOP`が更新する保持Moveを、Force速度およびジャンプ用pending慣性から分離したCore状態として設計する。
2. 数値Moveは原作どおり保持率を毎tick適用し、`STOP`は対象軸を明示的に停止する。待機遷移だけの個別ハードコードにはしない。
3. action 1の保持率1.0、action 0の保持率0.8、乗算後Moveを位置更新へ使う順序をfocused verificationへ追加する。Unity側は`idleMoveRetention`／`moveActionRetention`／`defaultMoveRetention`をInspector互換の調整点として保持する。
4. GT-001を同一ANI/SPT・同一hashで再生成し、原作観測の全宣言フィールドが22tick一致するか、座標変換などUnity代替が残る場合は根拠と比較除外範囲を記録する。

完了条件は、Runtime Verification、Phase 6比較、GT-001の2回決定性、GT-001～GT-010のReal-Mech Golden Traces、Console Errorがすべて通り、既存ジャンプ／ブースト慣性回帰を壊さないことです。2026-08-25に実機体`KD-03`の手動Test Playでも前進解放後の短い減速と最終停止を確認し、この条件を満たしました。

### Phase 6C: 残件を証拠レベルで分類し、代表ケースだけ観測する

現在の挙動クラス別の実装範囲、残る差分、追加観測条件は
[`TEST_PLAY_PHASE6C_DIFFERENCE_LEDGER.md`](TEST_PLAY_PHASE6C_DIFFERENCE_LEDGER.md)を正とします。

GT-002～GT-010を順番に全件原作観測する工程は採用しません。まず各差分を次の3段階へ分類し、追加観測の費用を共有Coreの不確実性が高い箇所へ集中します。

| 証拠レベル | 判定条件 | 既定の進め方 |
| --- | --- | --- |
| 原作確定 | 一次擬似コードで状態、代入値、処理順、分岐条件まで一意に追え、実ANI/SPTと矛盾しない。 | EXE追加観測を必須にせず、共通Core実装、focused verification、実機体Golden Traceで確認する。 |
| 原作高確度 | 一次擬似コードと実ANI/SPTは一致するが、tick境界、対象offset、呼出順の一部が未確認。 | その未確認点が共通Coreの合否を左右する場合だけ、挙動クラスから1つの代表ケースを観測する。 |
| 原作推定／Unity代替 | 原作の値や描画式を一意に確定できない、またはCollider、Camera、Audio、描画などUnity適応層に属する。 | 調整可能な代替として実装・記録し、手動実機体確認と回帰検証を受入に使う。原作一致とは表記しない。 |

追加の原作EXE観測は、次のいずれかに該当するときだけ実施します。

1. 複数アクションへ波及する共有Coreの処理順または状態の意味を、一次擬似コードだけでは一意に決められない。
2. 原作確定／高確度の実装後も、実機体の見た目・操作・Golden Traceに説明できない差が残る。
3. 複数の実装候補が同じ一次資料と整合し、選択に実行時の値が必要である。
4. 対象項目を「原作観測一致」として受け入れる必要がある。
5. 既存trace同士で、同名フィールドの処理段階や単位が一致していない。

代表観測は、共有関数、固定値、処理順が確認でき、同じ実ANI/SPTに反証がなければ終了します。差がUnity適応層だけに残った場合や、安定した観測点を確定できない場合は全体工程を止めず、`原作推定`または`Unity代替`へ降格して未確定範囲を明記します。

#### 挙動クラスごとの軽量化

| 挙動クラス | 対応GT | 改訂後の扱い |
| --- | --- | --- |
| 接地Move保持 | GT-001、GT-004の一部 | GT-001を代表観測としてPhase 6Bを完了する。GT-004は空中回帰で差が残る場合だけ追加観測する。 |
| ジャンプ、上昇、入力解放 | GT-002、GT-003、GT-004 | 一次擬似コードと実ANIを先に使う。GT-004は`FUN_004d68e0`のaction 8保持率`0.99`、`c38 < 31`・移動ゲージ正・Y速度`< 0.05`時の`+0.012`を共通重力前へ実装し、focused verificationを完了した。実機操作に説明できない差が出た場合だけGT-002～004から一組を代表観測する。 |
| ステップ、ブースト | GT-005、GT-006 | 一次擬似コードの固定値と実ANI/SPTを先に反映し、両GTのReal-Mech回帰で確認する。式、キャンセル順、Force境界が一意でない場合だけ片方を代表観測する。 |
| 射撃、持替え、格闘 | GT-007～GT-009 | GT-007は射撃100、攻撃設定、cooldown、接地復帰をfocused verification済み。GT-008／009は持替え18、方向格闘130→136、C入力queue、`SwordCancel` 131→132→133、攻撃プロファイル、接地復帰をfocused verification済み。type 55／57の発火tickは実ANI事実として固定するが、type 1発射体の速度・形状・描画式とtype 57の`Hit`成否・形状・持続・多段条件は未確定のまま分離する。 |
| ロック、旋回、カメラ | GT-010 | target相対の100／106射撃分岐、後方103と欠損fallback、ロック保持、実ANI `ShotTurnAng`、6→0復帰をfocused verification済み。画角、注視点、追従感はUnity代替として受け入れ、画面構図の全tick観測は行わない。 |
| Presentation | RunProc、BURNER、CamEffect等 | ID、発火tick、Coreイベントへ影響する項目だけ必要時に観測する。色、寿命、合成、聴感の厳密一致はPhase 6完了の必須条件にしない。 |

#### ツール整備も必要時に限定する

1. `convert_gt001_probe.py`、22tick検査、Selected-Mech GT-001基準生成は、Phase 6Bの証拠を保つため現状のまま維持する。
2. 次の代表観測が実際に選ばれるまでは、全GT対応の取得・変換フレームワークを先行実装しない。
3. 2つ目のシナリオを追加する時点で、hash、ヘッダー、`observedFields`検証、scenario catalog入力のうち実際に共通する部分だけを抽出する。GT-001 schema v1と既存成果物の読み込み互換は維持する。
4. GT-001で計測経路の初回妥当性を示した3回一致は保持する。同じ機体ECX、offset、ブレーク位置を再利用する後続観測は2回一致を既定とし、値の不一致、新規ポインター／offset、非決定性の疑いがある場合だけ3回目を追加する。

Phase 6Cの完了条件は、全GTの原作traceが揃うことではありません。各変更に証拠レベル、一次資料、影響する挙動クラス、観測を追加する条件が記録され、Runtime Verification、対象focused verification、GT-001～GT-010 Real-Mech Golden Traces、Console Error確認が通ることです。原作EXEを観測していない項目は`RealAniObserved`、`原作推定`、`Unity代替`のいずれかを維持し、原作一致へ昇格させません。

RunProc／type57／BURNER／CamEffectなど観測点が未確定の領域は、traceに0値を補わず`observedFields`から除外し、実装対象になった項目だけ一次擬似コードと実ANI/SPTの根拠調査へ戻します。

### 原作EXE観測の運用契約

追加観測を開始する前に[`Tools/OriginalTrace/OBSERVATION_TICKET_TEMPLATE.md`](../Tools/OriginalTrace/OBSERVATION_TICKET_TEMPLATE.md)を複製し、疑問を1つだけ固定します。

1. 一次擬似コード、実ANI/SPT、既存first-mismatchを先に確認し、複数候補が残る場合だけ観測する。
2. 観測を`アンカー観測`、`適合trace`、`診断ログ`のいずれかへ事前分類する。
3. 通常起動後にAttachし、残留BPが空であることを確認する。低頻度action入口でPIDごとの自機ECXと入力受理を確定してから、高頻度Motion点を対象限定する。
4. 停止型BPを常設せず、ワンショットまたは非停止ログで必要最小窓だけ取得する。値は可能ならfloat32の生ビットと解釈値を併記する。
5. 同じoffset・同じ経路は2回一致で終了する。不一致、新規ポインター、非決定性の疑いがある場合だけ3回目を行う。
6. 2セッション連続で対象特定、入力受理、BP運用に失敗した場合は観測を止め、一次資料へ戻るか`原作高確度`／`Unity代替`へ降格する。
7. 観測中に見つかった別の疑問はその場で追わず、別チケットへ送る。停止中にデタッチせず、実行再開とBP撤去を確認してから終了する。

## 2026-08-24 観測履歴（2026-08-25受入境界より前）

以下は観測工程の診断履歴です。各節にある「schema v2未完了」「次回追試」などの当時の状態は、2026-08-25のPhase 6B受入境界と停止規則で置き換えられています。新しい観測チケットが承認されない限り、ここから追試を再開しません。

### ジャンプ直後の物理入口追試

再起動後に通常起動した`Logs/WindomXP/WindomXP_orig.exe`（PID`41592`）へAttachし、`FUN_004D2030`を`[ESP+4]==3`に限定した状態で、ユーザーのZキー1回の操作を観測した。ジャンプ要求`JUMP_REQ_DETAIL ECX=028D0988 ARG=3 DIR=261 M0=0 M1=0 M2=0 RET=1063675494 AIR=0`の取得後、同じ機体`ECX=0x028D0988`に限定した`FUN_004CD840`ワンショットBPを再開直後へ設定した。

物理積分入口の追加ログは`JUMP_PHYS_ENTRY ECX=028D0988 DIR=261 V0=0 V1=0 V2=0 F0=0 F1=0 F2=0 M0=0 M1=0 M2=0 RET=1063675494 AIR=0`だった。既存のPID`69968`で得た`JUMP_TICK2`と同じく、ジャンプ要求直後の入口Move各軸0、速度各軸0、Force各軸0、空中フラグ値0、保持率ビット列`1063675494`（float32 `0.9`）を確認した。別PIDで同じ入口値を再確認できたが、これはジャンプ開始直後の1回の一次実値であり、上昇中の連続tick、Force設定元、空中フラグの遷移、重力遷移、ANIモーション再生回数を示さない。

採取後は`004CD840`／`004D2030`を全解除してx32dbgをデタッチした。x32dbgはプロセスなし、原作は戦闘画面・通常タイトル・`isRunning=true`へ復元した。schema v2 JSONL、既存schema v1 Run 1～3、原作フォルダ内データには追加・変更していないため、Phase 6Bの22tick全列比較と最終原作一致判定は引き続き未完了である。

## 2026-08-24 再起動後ジャンプ入口追試（PID 72796）

再起動後に通常起動した`Logs/WindomXP/WindomXP_orig.exe`（PID`72796`）へAttachし、残留BPがないことを確認して`FUN_004D2030`を`[ESP+4]==3`のワンショットへ設定した。ユーザーのZキー1回で`JUMP_REQ_NEW ECX=02950988 ARG=3 DIR=261 M0=0 M1=0 M2=0 RET=1063675494 AIR=0`を取得した。同じ`ECX=0x02950988`へ`FUN_004CD840`のワンショットを設定した直後のログは`JUMP_RISE_ENTRY_NEW ECX=02950988 DIR=261 V0=0 V1=0 V2=0 F0=0 F1=0 F2=0 M0=0 M1=0 M2=0 RET=1063675494 AIR=0`だった。別PIDでジャンプ要求と直後入口の対応を再確認できたが、今回も上昇中の非0速度・Force・Move、空中フラグ遷移の証拠ではない。

その後、`ecx==02950988`と物理値非0を組み合わせた条件を試したが、x32dbg上で対象機体を正しく絞れず、`ECX=17162328 DIR=5`など別機体の待機ログを大量に取得したため無効な観測として破棄した。該当BPを削除してデタッチし、x32dbgはプロセスなし、原作は戦闘画面・通常タイトルへ復元した。schema v2 JSONL、既存schema v1 Run 1～3、原作フォルダ内データは変更していない。

## 2026-08-24 ジャンプコールバック追試（PID 72796）

再起動後の原作PID`72796`へAttachし、`FUN_004D59F0`に対象機体`ecx==02950988`限定・一回限りのログBPを設定した。ユーザーが戦闘画面でZキーを1回押下したところ、BPが1回ヒットし、ログは次の通りだった。

`JUMP_CALLBACK_NEW ECX=02950988 DIR=261 FLAG31=1 STATE=0 V0=0 V1=0 V2=0 F0=0 F1=0 F2=0 M0=0 M1=0 M2=0 RET=1063675494 AIR=0`

これはジャンプ要求後に`FUN_004D59F0`が呼ばれた時点の一次実値で、方向`261`、`FLAG31=1`、状態値`0`を確認した。一方、速度・Force・Moveは全軸0、空中フラグ値`0`であるため、上昇中の非0値、重力遷移、ANIモーション再生回数を示す証拠ではない。`FUN_004D59F0`の単発コールバック到達は確定したが、後続の上昇状態は未確定のままである。

取得後はBPを削除してx32dbgをデタッチし、原作は戦闘画面・通常タイトルで実行状態へ復元した。schema v2 JSONL、既存schema v1 Run 1～3、原作フォルダ内データは変更していない。

## 2026-08-24 後段ジャンプコールバック追試（PID 73640）

再起動後の原作PID`73640`へAttachし、`FUN_004D2030`を`dword:[esp+4]==3`の一回限りBPとして設定した。ユーザーのZキー1回で次のジャンプ要求を取得した。

`JUMP_REQ_RESTART ECX=028E0988 ARG=3 DIR=261 RET=1063675494 AIR=0`

要求停止位置から続行し、同じ機体`ecx==028E0988`に限定した`FUN_004D5B60`の一回限りBPを命中させた。ログは次の通りだった。

`JUMP_ANIM_CB_NEW ECX=028E0988 DIR=5 FLAG31=0 STATE=0 V0=0 V1=1021128474 V2=0 F0=0 F1=1025758962 F2=0 M0=0 M1=0 M2=0 RET=1065353216 AIR=1`

`V1`（`+0xA80`）はfloat32で約`0.027`、`F1`（`+0xAEC`）は約`0.04`、`RET`（`+0xA88`）は`1.0`、`AIR`（`+0xBA8`）は`1`である。開始直後に取得済みの`FUN_004CD840`入口（Move・速度・Force全軸0、`AIR=0`）と比較すると、同じジャンプ処理の後段で空中状態と垂直系の非0値へ遷移したことが確認できる。`DIR=5`、`FLAG31=0`、`STATE=0`も同時に確認した。

これは後段コールバック到達と1点の空中状態を示す一次実値であり、上昇全tick、着地、重力遷移、ANIモーション再生回数を示すものではない。取得後はBPを削除してx32dbgをデタッチし、原作は戦闘画面・通常タイトルで実行状態へ復元した。schema v2 JSONL、既存schema v1 Run 1～3、原作フォルダ内データは変更していない。

## 2026-08-24 次段ジャンプ遷移コールバック追試（PID 73640）

同じPID`73640`・同じ機体`ecx==028E0988`で`FUN_004D5EC0`の一回限りBPを設定し、ユーザーのZキー1回後の次段コールバックを取得した。ログは次の通りだった。

`JUMP_TRANSITION_CB_NEW ECX=028E0988 DIR=5 FLAG31=0 STATE=0 V0=0 V1=1042334876 V2=0 F0=0 F1=1017370378 F2=0 M0=0 M1=0 M2=0 RET=1065353216 AIR=1`

`V1`（`+0xA80`）はfloat32`0.157`、`F1`（`+0xAEC`）は`0.02`、`RET`（`+0xA88`）は`1.0`、`AIR`（`+0xBA8`）は`1`である。直前の`FUN_004D5B60`では`V1≈0.027`、`F1≈0.04`だったため、同じジャンプ処理の次段で垂直系の値が更新されたことを確認した。`DIR=5`、`FLAG31=0`、`STATE=0`も同時に確認した。

これは各コールバック入口の単発一次実値であり、上昇全tick、着地、重力遷移、ANIモーション再生回数を示すものではない。取得後はBPを削除してx32dbgをデタッチし、原作は戦闘画面・通常タイトルで実行状態へ復元した。schema v2 JSONL、既存schema v1 Run 1～3、原作フォルダ内データは変更していない。

## 2026-08-24 同一関数の別遷移入口追試（PID 73640）

続く同PID`73640`・同じ機体`ecx==028E0988`の追試では、`FUN_004D5EC0`に対象機体限定のログBPを設定し、ユーザーのZキー1回後に次のログを取得した。

`JUMP_TRANSITION_NEXT ECX=028E0988 DIR=261 FLAG31=1 STATE=0 V0=0 V1=1042334876 V2=0 F0=0 F1=1017370378 F2=0 M0=0 M1=0 M2=0 RET=1065353216 AIR=0`

`V1`（`+0xA80`）はfloat32`0.157`、`F1`（`+0xAEC`）は`0.02`、`RET`（`+0xA88`）は`1.0`だった。前回の同関数観測と垂直系の値は同じだが、今回は`DIR=261`、`FLAG31=1`、`AIR=0`であり、前回の`DIR=5`、`FLAG31=0`、`AIR=1`とは異なる入口状態だった。同一関数内に異なる呼出し文脈があることは確認できるが、単発入口値だけから初期・後段の意味を一般化しない。

これはコールバック入口の単発一次実値であり、上昇全tick、着地、重力遷移、ANIモーション再生回数を示すものではない。取得後はBPを削除して原作を再開し、x32dbgをデタッチした。原作は戦闘画面で実行状態へ復元し、schema v2 JSONL、既存schema v1 Run 1～3、原作フォルダ内データは変更していない。

## 2026-08-24 AIR=1物理入口追試（PID 73640）

同じPID`73640`で`FUN_004CD840`のブレーク条件とログ条件を`ecx==028E0988&&dword:[ecx+BA8]==1`へ明示設定し、ユーザーのZキー1回後に条件成立の物理入口を取得した。採用するログは次の通りだった。

`JUMP_AIR_ENTRY_NEXT ECX=028E0988 DIR=261 FLAG31=1 STATE=0 V0=0 V1=1021128474 V2=0 F0=0 F1=1025758962 F2=0 M0=0 M1=0 M2=0 RET=1065353216 AIR=1`

`V1`（`+0xA80`）はfloat32で約`0.027`、`F1`（`+0xAEC`）は約`0.04`、`RET`（`+0xA88`）は`1.0`、`AIR`（`+0xBA8`）は`1`である。これは`AIR=0`入口から空中フラグ値`1`で呼ばれた`FUN_004CD840`入口の単発実値であり、上昇全tick、着地、重力遷移の完了を示すものではない。条件設定前に生成された対象外・未限定のログ行は証拠として採用していない。

取得後はBPを削除して原作を再開し、x32dbgをデタッチした。原作は戦闘画面で実行状態へ復元し、schema v2 JSONL、既存schema v1 Run 1～3、原作フォルダ内データは変更していない。

## 2026-08-24 DIR条件式の再確認（未採用）

同じPID`73640`・同じ機体`ecx==028E0988`で、`FUN_004CD840`の次ティック観測を試した。`ecx==028E0988&&dword:[ecx+BA8]==1&&byte:[ecx+30]==5`を条件にしたところ、ログは`JUMP_AIR_TICK_NEXT ECX=028E0988 DIR=261 FLAG31=1 STATE=0 V0=0 V1=1021128474 V2=0 F0=0 F1=1025758962 F2=0 M0=0 M1=0 M2=0 RET=1065353216 AIR=1`だった。

この条件は`DIR`の下位バイトだけを比較するため、`DIR=261 (0x105)`も一致する。したがって`DIR=5`の証拠としては採用せず、`AIR=1`の既確認入口値を更新しない。厳密な4バイト条件への修正追試は、再アタッチ後に原作ゲームが応答なしとなったため未完了である。x32dbgは手動デタッチ済みで、次回は原作を再起動してから再開する。

## 2026-08-24 厳密DIR=5物理入口追試（該当なし、PID 4416）

原作再起動後のPID`4416`で、`FUN_004CD840`のブレーク条件・ログ条件を`ecx==028E0988&&dword:[ecx+BA8]==1&&dword:[ecx+30]==5`へ設定した。ユーザー入力相当のZキー1回を送ったが、条件成立ログは発生しなかった。`DIR=261`の下位バイト一致を除外した厳密条件で、今回の入力窓には`AIR=1 / DIR=5`の物理入口がなかったことを確認した。ただし、これは当該入力窓での非成立であり、全状況での不在を示すものではない。BP削除・デタッチ後、原作は通常タイトルの戦闘画面へ復元した。

## 2026-08-24 AIR=1物理入口の再追試（未取得、PID 4416）

同じPID`4416`・同じ対象候補`ECX=0x028E0988`で、`FUN_004CD840`の条件を`ecx==028E0988&&dword:[ecx+BA8]==1`へ緩和し、`DIR`を限定せず`AIR=1`の物理入口を再取得しようとした。ユーザーのZキー1回後も`JUMP_AIR_TICK_AIR1`の条件成立ログは発生しなかった。続けて対象機体だけを`ecx==028E0988`で記録する条件、および短時間の無条件`PHYS_ANY`サンプルへ切り替えたが、新しい物理入口ログは得られなかった。画面上でもジャンプ状態は確認できなかったため、今回の入力窓から`DIR`・垂直速度・Forceの新しい値は確定できない。これは対象アドレスまたは入力がこの実行状態で物理入口へ到達しなかった記録であり、`AIR=1`の全状況での不在を示さない。

観測BPは一時停止後に`bc 004CD840`で解除し、x32dbgをデタッチした。ただし一度停止中にデタッチしたため原作が「応答なし」となり、PID`4416`へ再アタッチして実行再開後に再度デタッチしても応答状態へ復帰しなかった。x32dbgは最終的にプロセスなしへ戻したが、原作は再起動が必要な状態である。今回の未取得結果はschema v2、既存schema v1 Run 1～3、原作フォルダ内データへ追加していない。

## 2026-08-24 ジャンプ要求からAIR=1物理入口までの連続追試（PID 79628）

原作再起動後のPID`79628`へAttachし、Breakpoints欄が空であることを確認した。`FUN_004D2030`の無条件入口からユーザーのZキーを受け、次のログを取得した。

`JUMP_REQ_ANY_RESTART2 ECX=028A0988 ARG=3 DIR=261 RET=1063675494 AIR=0`

同じ機体`ECX=0x028A0988`を対象に`FUN_004D5B60`、続けて`FUN_004D5EC0`を観測した。

`JUMP_ANIM_CB_RESTART2 ECX=028A0988 DIR=5 FLAG31=0 STATE=0 V0=0 V1=1021128474 V2=0 F0=0 F1=1025758962 F2=0 M0=0 M1=0 M2=0 RET=1065353216 AIR=1`

`JUMP_TRANSITION_CB_RESTART2 ECX=028A0988 DIR=5 FLAG31=0 STATE=0 V0=0 V1=1042334876 V2=0 F0=0 F1=1017370378 F2=0 M0=0 M1=0 M2=0 RET=1065353216 AIR=1`

後段コールバックでは`DIR=5`、`AIR=1`、保持率`1.0`を確認し、`V1`は約`0.027`から約`0.157`、`F1`は約`0.04`から約`0.02`へ変化した。

さらに`FUN_004CD840`を`ecx==028A0988&&dword:[ecx+BA8]==1`で観測し、次を取得した。

`JUMP_AIR_ENTRY_RESTART2 ECX=028A0988 DIR=5 FLAG31=0 STATE=0 V0=0 V1=1041865114 V2=0 F0=0 F1=0 F2=0 M0=0 M1=0 M2=0 RET=1065353216 AIR=1`

`V1=1041865114`はfloat32約`0.15`、`RET=1065353216`は`1.0`である。今回の要求→後段コールバック→AIR=1入口の対応は一次実値で確認できたが、上昇全tick、着地、重力遷移、ANIモーション再生回数の証拠ではない。BPはワンショット発火後に消失し、F9で実行状態へ戻してからデタッチした。schema v2、既存schema v1 Run 1～3、原作フォルダ内データには追加・変更していない。

## 2026-08-24 自動前進入力の未取得確認（PID 79628）

同じPID`79628`へ再Attachし、Breakpoints欄が空であることを確認して`FUN_004D2030`の前進要求観測を準備した。`dword:[esp+4]==1`の条件とログを設定した後、自動送信した上矢印を試したが、要求入口BPには命中しなかった。これは自動入力が原作へ受理されたことを確認できない結果であり、前進要求が存在しない証拠とは扱わない。

新しい前進Move・保持率・適用後Moveは取得できなかったため、schema v2、既存schema v1 Run 1～3、原作フォルダ内データは変更していない。BPは一覧から削除し、実行中にデタッチして原作を通常タイトルの戦闘画面へ復元した。次回はユーザーの実操作で上矢印を押下してもらい、要求入口から連続tick観測を再開する。

## 2026-08-24 ユーザー操作による前進要求再取得（PID 79628）

自動入力ではなく、ユーザーが戦闘画面で上矢印を1回押下した。`FUN_004D2030`へ`dword:[esp+4]==1`の条件付きワンショットBPを設定し、次のログを取得した。

`V2_REQ ECX=028A0988 ARG=1 DIR=8 RET=3F4CCCCD`

前進要求`ARG=1`、対象機体`ECX=0x028A0988`、要求方向`DIR=8`、保持率ビット列`RET=0x3F4CCCCD`を一次実値で確認した。これは自動入力未取得記録を置き換えるものではなく、ユーザーの実操作が要求入口へ到達した追加証拠である。

続けて同じ機体を`FUN_004CD840`で観測し、次を記録した。

`JUMP_AIR_ENTRY_V2 ECX=028A0988 DIR=8 AIR=0 V0=0 V1=0 V2=442AC2AA RET=3F800000`

この行は前進要求直後の物理入口として記録する。ただし、設定した`ecx==028A0988&&dword:[ecx+BA8]==1`条件とログの`AIR=0`が整合しないため、`AIR=1`成立の証拠や空中状態の遷移値としては採用しない。`DIR=8`の前進入口・保持率`1.0`をschema v2へ追加するには、条件式の再確認と`004CDFC4`の適用後Move観測が必要である。既存schema v1 Run 1～3、schema v2 JSONL、原作フォルダ内データは変更していない。

## 2026-08-24 前進要求から適用後Moveまでの連続追跡（PID 79628）

ユーザーの上矢印操作に対して`FUN_004D2030`を`dword:[esp+4]==1`で停止し、次の要求ログを取得した。

`V2_REQ_CHAIN ECX=028A0988 ARG=1 DIR=8 RET=3F4CCCCD`

停止中に要求BPを解除し、同じ機体を対象に`004CDFC4`を設定して再開した。同一系列のログは次の通りだった。

`V2_FORWARD_AFTER_CHAIN ECX=028A0988 EDX=028A0988 DIR=8 M0=0 M1=0 M2=3DA3D70A RET=3F800000`

`M2=0x3DA3D70A`はfloat32約`0.08`、`RET=0x3F800000`は`1.0`である。前進要求`DIR=8`に続く適用後Moveの代表一次実値として確認したが、22tick全列ではないためschema v2 JSONLへ追加していない。

## 2026-08-24 入力解放要求から適用後Moveまでの追跡（PID 79628）

続けて`FUN_004D2030`を`dword:[esp+4]==0`で観測し、入力解放要求を取得した。

`V2_REL_CHAIN ECX=028A0988 ARG=0 DIR=5 RET=3F800000`

要求停止後に同じ機体の`004CDFC4`を再設定して再開したログは次の通りだった。

`V2_REL_AFTER_CHAIN ECX=028A0988 EDX=028A0988 DIR=5 M0=0 M1=0 M2=3D51B718 RET=3F4CCCCD`

`M2=0x3D51B718`はfloat32約`0.0512`、`RET=0x3F4CCCCD`は約`0.8`である。解放経路と保持率遷移の追加一次実値として保持するが、停止・BP再設定を挟む単発観測であり、同一tickの22列または一般則としての適用順を確定する値には採用しない。観測BPを解除し、実行中デタッチ後に原作通常画面、x32dbgプロセスなし、BP一覧空を確認した。既存schema v1 Run 1～3、schema v2 JSONL、原作フォルダ内データは変更していない。

## 2026-08-24 対象機体限定の004CDFC4連続ログ再確認（PID 79628）

`004CDFC4`のログ条件を`ecx==028A0988`へ設定し、ブレーク条件は空欄、コマンドは`go`として自動送信した上矢印列を観測した。これにより、前回のように他機体のECXを混在させず、対象機体の`V2_MOVE_INPUT`だけを抽出できた。

前進方向`DIR=8`のログは2行で、両方とも次の同一値だった。

`V2_MOVE_INPUT ECX=028A0988 DIR=8 V0=BC54FDF4 V1=0 V2=3F800000 F0=0 F1=0 F2=0 M0=0 M1=0 M2=3DA3D70A RET=3F800000`

`M2=0x3DA3D70A`はfloat32約`0.08`、`RET=0x3F800000`は`1.0`であり、前回の`V2_FORWARD_AFTER_CHAIN`と同じ適用後Move値を連続ログでも再確認した。解放後の終端付近には次の対象機体行も含まれていた。

`V2_MOVE_INPUT ECX=028A0988 DIR=5 V0=BC54FDF4 V1=0 V2=3F4CCCCD F0=0 F1=0 F2=0 M0=0 M1=0 M2=3D51B718 RET=3F4CCCCD`

ただし、自動送信した上矢印列は保持キーを1回押し続けた手動入力と同一ではなく、`004CDFC4`の全ログも22tickの同一tick列へ整列していない。したがって、今回の値は対象限定・実行中の再確認として資料に残すが、schema v2 JSONLや原作一致の最終判定には追加しない。観測後はBP一覧を空にし、対象PIDから実行中にデタッチして、原作を戦闘画面の実行状態へ戻した。

## 2026-08-24 新PIDでの要求・適用後Move再確認（PID 67932、未採用）

再起動後PID`67932`へAttachし、`FUN_004D2030`に無条件ログを設定して自動上入力列を送った。対象機体は`ECX=028B0988`で、要求ログは10行だった。

`V3_REQ ECX=028B0988 ARG=1 DIR=8 RET=3F666666`

続く4回の押下要求は`ARG=1 DIR=8 RET=3F4CCCCD`、解放要求5回は`ARG=0 DIR=5 RET=3F800000`だった。したがって今回の自動入力列では、上入力要求が対象機体へ到達したこと、要求方向が`8`、解放方向が`5`であることを再確認した。ただし、これはキー保持を1回連続押下した手動操作とは同一でない。

その後`004CDFC4`へ対象条件`ecx==028B0988`、ログ継続コマンド`go`を設定し、`V3_MOVE_STREAM`を1,587行取得した。内訳は`DIR=5`が1,585行、`DIR=8`が2行だった。2行の前進値は同一で、次の通りである。

`V3_MOVE_STREAM ECX=028B0988 DIR=8 V0=BC54FDF4 V1=0 V2=3F800000 F0=0 F1=0 F2=0 M0=0 M1=0 M2=3DA3D70A RET=3F800000`

`M2=0x3DA3D70A`はfloat32約`0.08`、`V2`および`RET=0x3F800000`は`1.0`で、前回PID`79628`の対象限定前進値と一致する。一方、`DIR=5`の長い連続ログは`M2`が単一の22tick列へ整列しておらず、解放終端の一般値として採用しない。今回もschema v2 JSONL、既存schema v1 Run 1～3には追加しない。

採取末尾で`F8C00043`への`EXCEPTION_ACCESS_VIOLATION`（`C0000005`、DEP違反）のファーストチャンス例外が発生した。BPを撤去してデタッチした後、ゲームPID`67932`は終了しており、原作ゲームは再起動が必要な状態である。これは今回の観測環境の終了結果であり、原作の恒常的クラッシュ原因とは断定しない。
