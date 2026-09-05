# テストプレイモード実装メモ

このモードは、2026-09-05の決定により、将来的に原作をUnity上で再現したクローンゲームとして独立させるための基盤です。既存の `MechaAnimator` / `AniScriptRuntime` / 編集UIの互換性を保ちながら、確定した原作仕様を `Assets/Scripts/TestPlay/` 以下へ段階的に実装します。武器、エフェクト、効果音、ボイス、カメラ演出に加え、編集UIを経ない起動と実機体同士の戦闘を後続の受入対象にします。

対象Unityは6000.6.0f1。Runtime 672、HOD 374、Golden 10件各2回一致とWindows Player・通常操作・音声のユーザー確認によりP0移行受入は完了しました。具体計画は [6000.6確認結果と次タスク](UNITY6000_6_ACCEPTANCE_AND_NEXT_TASKS.md)、自動検証の入口は [検証ジョブAPI](VERIFICATION_JOB_API.md) を参照してください。最新環境・結果は [DEV_STATE](../DEV_STATE.md)、独立化の優先順と検証範囲は [監査報告](PROJECT_AUDIT_2026-09-05.md) を正とします。本書の過去日付の検証件数は履歴であり、6000.6の合格実績ではありません。

実装は、擬似コードと実データから確認できた「原作確定」、動作から補う「原作推定」、DirectX9固有処理を置き換える「Unity代替」を区別します。

再設計Phase 0で確定した原作との差分、未解決事項、tickトレース契約、後続Phaseの実装境界は [TEST_PLAY_PHASE0_REDESIGN_SPEC.md](TEST_PLAY_PHASE0_REDESIGN_SPEC.md)、Phase 1の共通ANI中間表現とtrack schedulerは [TEST_PLAY_PHASE1_IMPLEMENTATION.md](TEST_PLAY_PHASE1_IMPLEMENTATION.md)、Phase 2のAction／Motion／Locomotion Coreは [TEST_PLAY_PHASE2_IMPLEMENTATION.md](TEST_PLAY_PHASE2_IMPLEMENTATION.md)、Phase 3のCombat Coreと60Hz発射体は [TEST_PLAY_PHASE3_IMPLEMENTATION.md](TEST_PLAY_PHASE3_IMPLEMENTATION.md)、Phase 4のPresentation EventとUnity表示Adapterは [TEST_PLAY_PHASE4_IMPLEMENTATION.md](TEST_PLAY_PHASE4_IMPLEMENTATION.md)、Phase 5の実機体golden traceと反復決定性検証は [TEST_PLAY_PHASE5_IMPLEMENTATION.md](TEST_PLAY_PHASE5_IMPLEMENTATION.md)、Phase 6Aの原作EXE部分観測trace契約と比較器は [TEST_PLAY_PHASE6_IMPLEMENTATION.md](TEST_PLAY_PHASE6_IMPLEMENTATION.md)、Phase 6Cの挙動クラス別完了境界と次タスクは [TEST_PLAY_PHASE6C_DIFFERENCE_LEDGER.md](TEST_PLAY_PHASE6C_DIFFERENCE_LEDGER.md) を参照してください。

## 追加したコンポーネント

| ファイル | 役割 |
| --- | --- |
| `TestPlayController.cs` | 入力、ANI tick、HOD補間、Script命令、簡易武器生成をまとめるテストプレイ本体。 |
| `TestPlayScriptVM.cs` | `IF`、関数呼び出し、代入、`@int/@float`、`STOP` を扱う簡易Script VM。 |
| `TestPlayAniProgram.cs` | ANIをcommand、assignment、condition、operandへ変換する共通中間表現・コンパイラ・実行器。 |
| `TestPlayAniAnimationProgram.cs` | `animation` / `script`を初期プログラム、block時間、HOD進行量、終端へ変換する。 |
| `TestPlayAniTrackScheduler.cs` | Main / Secondary / Sub trackのblock進行、repeat、loop、jump、tick traceを管理する。 |
| `TestPlayActionCore.cs` | 要求、論理、表示、Scriptの各アクションIDとANI channelを分離する。 |
| `TestPlayMotionCore.cs` | Force、重力、水平減衰、速度倍率、Unity変位を決定的に積分する。 |
| `TestPlayLocomotionCore.cs` | 移動状態を分類し、上昇、空中、着地、ステップ、ブーストの遷移条件を判定する。 |
| `TestPlayPhase2TickTrace.cs` | Action／Locomotion／Motion Coreの1 tick結果をCulture非依存JSONへ変換する。 |
| `TestPlayCombatCore.cs` | 攻撃受付、AttackDelay、格闘連携、payload、hit result、発射体1 tickを決定する。 |
| `TestPlayCombatTrace.cs` | 攻撃プロファイル、クールダウン、spawn／hit／cancelをPhase 2 traceへ追加する。 |
| `TestPlayPresentationCore.cs` | Snd、Voice、BURNER、Proc、texture、CamEffectの原作識別情報・根拠・Unity Adapterを分離する。 |
| `TestPlayPresentationTrace.cs` | 型付き演出イベントと全引数をPhase 3 traceへ決定的に追加する。 |
| `TestPlayGoldenTrace.cs` | GT-001～GT-010の入力列、Phase 5 tick snapshot、JSONL session、SHA-256比較を定義する。 |
| `TestPlayOriginalTrace.cs` | 原作観測traceの部分フィールド契約、欠落診断、最初の不一致比較を定義する。 |
| `TestPlayStateTable.cs` | ゲーム本体の `@int[]` / `@float[]` 風の状態テーブル。 |
| `TestPlayScriptValue.cs` | Script引数の数値、シンボル、`STOP` 表現。 |
| `TestPlayTargetDummy.cs` | ロック対象/被弾対象のダミー。 |
| `TestPlayProjectile.cs` | `WeaponAttack` / `RunProc2`のUnity表示Adapter。移動、寿命、homing、命中は60Hz Combat Coreへ委譲する。 |
| `TestPlayCameraController.cs` | テストプレイ中だけ編集用`FreeCam`を置き換え、機体後方追従、ロック対象フレーミング、障害物回避、`CamEffect`近似を担当する。 |
| `TestPlayRuntimeEvent.cs` | スクリプト、戦闘、エフェクト、音声を接続する型付きイベントと攻撃プロファイル。 |
| `TestPlayPresentationRuntime.cs` | `Snd` / `Voice`、原作テクスチャID、Unity上の演出生成を接続する演出層。 |
| `TestPlayOriginalEffect.cs` | 原作テクスチャを加算合成・ビルボード表示し、寿命とフェードを管理するランタイム。 |
| `TestPlayThunderEffect.cs` | `RunProc2` type 60の生成時WEAPONPOINT行列、移動、散布、寿命、減衰を表示へ接続する非戦闘Adapter。 |
| `Assets/Editor/TestPlayOriginalSoundSetup.cs` | 原作の固定Snd ID表から`Assets/SND_SE`のAudioClip参照をシーンへ再生成する。 |
| `Assets/Editor/TestPlayOriginalTextureSetup.cs` | `Assets/IMG_TX`を原本のまま保持し、テスト用復号コピーと原作ロード順/スクリプトID対応を再生成する。 |
| `Assets/TestPlayOriginalEffect.shader` | URP用の両面・Z書き込みなし・加算合成シェーダー。 |
| `Assets/Editor/TestPlayRuntimeVerification.cs` | 原作確定仕様のエディタ回帰検証。 |
| `Assets/Editor/TestPlayPhase1Verification.cs` | 共通ANI中間表現、編集プレビュー共有、track scheduler、traceの回帰検証。 |
| `Assets/Editor/TestPlayPhase2Verification.cs` | action source分離、Force演算順、移動状態と境界、Phase 2 traceの回帰検証。 |
| `Assets/Editor/TestPlayPhase3Verification.cs` | 攻撃受付、連携、値の根拠区分、命中結果、60Hz発射体、Phase 3 traceの回帰検証。 |
| `Assets/Editor/TestPlayPhase4Verification.cs` | 演出ID、根拠区分、Unity Adapter、診断、Phase 4 traceの回帰検証。 |
| `Assets/Editor/TestPlayPhase5Verification.cs` | golden scenario、決定的replay、JSONL／hash、実機体manifestの回帰検証。 |
| `Assets/Editor/TestPlayGoldenTraceVerification.cs` | 実ANI／SPT上で全GTを2回実行し、全tickの完全一致とaction／command anchorを検証する。 |
| `Assets/Editor/TestPlayOriginalTraceComparison.cs` | 原作観測JSONLをUnity基準traceと比較し、最初の不一致レポートを保存する。 |
| `Assets/Editor/TestPlayPhase6Verification.cs` | 部分観測、欠落値、比較許容誤差、原作EXE hashの回帰検証。 |

## 手動セットアップ

1. 機体を通常どおりロードする。
2. 空のGameObjectを作り、`TestPlayController` を追加する。
3. `Robo` に現在の `RoboStructure` を割り当てる。未設定でも開始時に自動検索する。
4. ターゲット用GameObjectを作り、機体の前方に置いて `TestPlayTargetDummy` を追加する。
5. `TestPlayController.target` にそのダミーを割り当てる。
6. 接地/落下を確認する場合は、床や地形に `BoxCollider` / `MeshCollider` などのUnity Colliderを設定する。表示用グリッドだけでは接地判定されない。
7. `Start On Play` をオンにするか、UI/Button/Eventから `StartTestPlay()` を呼ぶ。

## 入力

| 入力 | 状態 |
| --- | --- |
| 矢印キー | `@int[190]` 方向入力。上下左右は原作コード`8` / `2` / `4` / `6`、同時押しの斜めは`7` / `9` / `1` / `3`。通常移動は `moveAction`、既定ID `1` を押下中ループ再生する。 |
| 同じ方向の矢印キーを2回入力 | 各方向のステップ。入力方向を開始時の機体正面から見た前/後/左/右へ分類し、`11` / `12` / `9` / `10`を選ぶ。ステップだけを理由に空中扱いにはせず、原作の最短16tick・最長61tick条件で終了する。接地中はステップ着地ID `6`、既に空中なら空中停止ID `8`へ移る。 |
| `Z` | 地上ではジャンプ開始ID `3`を完了して上昇ID `7`へ進む。短押しでもID `3`を中断せず、ID `7`を最低5tick再生する。Z保持・空中・エネルギーありの間は上昇を継続するが、モーションは1回だけ再生して最終ポーズを保持する。空中停止中の保持／単押しは、空中停止を11tick待ってからエネルギー80を使って再上昇する。 |
| `Z` を短時間内に2回入力 | `@int[191]` ブースト開始入力。既定の判定間隔は`doubleTapBoostSeconds=0.3`秒で、空中からブーストダッシュID `22`へ移る。 |
| `X` | `@int[192]` 射撃。押下エッジで銃形態ならID `100`、サーベル形態なら持替えID `68`を選ぶ。ブーストID `22`の6tick目以降は形態より優先して飛行射撃ID `106`。保持中は状態値を維持するが自動再入力しない。 |
| `C` | `@int[193]` 格闘。銃形態では持替えID `18`、サーベル形態では前`130`、中立`131`、左`141`、右`146`、後`151`を選ぶ。格闘中の再押下はScriptの`SwordCancel`先へ連携する。 |
| `V` | `@int[194]` 防御。 |
| `S` | `@int[195]` ロック取得／解除。現在は単一の`target`候補をトグルする。 |
| `A` / `D` / `F` | `@int[196]` - `@int[198]` サブ攻撃。 |

`S`は現在、単一の`target`候補に対するロック取得／解除トグルとして動作する。候補が`Script.spt`の`LockDist`より遠い、非アクティブ、またはHP 0の場合は取得せず、ロック中に条件を外れた場合も解除する。SPT未読時は`fallbackLockDistance`を使う。

## 現在の実装範囲

- テストプレイ本体はUnityの`FixedUpdate`から独立した60Hz（16.666ms）の蓄積型シミュレーションで進む。低フレーム時は`maximumCatchUpTicks`まで追いつき、余った時間は捨てず次フレームへ持ち越す。キー状態は毎描画フレームで取得し、短い押下を次のシミュレーションtickまでラッチするため、60Hz境界間の単発入力を失わない。二度押し判定も実時間ではなく60Hz tick差で行う。

- `Move` / `Force` / `STOP`。`Move=(...)` / `Force=(...)` の代入形式と `Move(...)` / `Force(...)` の関数形式を扱う。原作の`Move`では数値`0`が現在値を維持し、`STOP`が対象軸を明示的に0へ停止する。ANI MoveはForce速度・ジャンプ用pending慣性とは別の保持状態として持ち、action開始時の保持率（GT-001では通常移動1.0、待機0.8）を毎tick適用する。
- `@int[0..199]` / `@float[0..199]` の代入、`+=`、`-=`、`*=`、`/=`。シーン互換のため配列自体は従来サイズを保つが、Script.aniから200以上は参照できない。
- `IF` / `ELSE` / `ENDIF`。`==` / `>=` / `<=` / `!=` / `>` / `<`とネストを扱う。
- セミコロン区切り、CRLF/LF、アポストロフィから行末までのコメント、同一行の複数命令。
- スクリプト文字列を命令列へ事前解析してキャッシュし、制御遷移命令で残り命令の走査を中断する。
- `ChangeAnime` / `GoScriptIndex` / `GoPoseIndex` / `AnimeLoop`
- `ExecScriptEveryTime(0)`は毎tick、`ExecScriptEveryTime(n)`は概ね`n + 1` tick周期で現在ブロックを再実行する。
- `unk == 999999999` のスクリプトブロックを原作終端番兵として扱う。
- 原作のブロック選択処理に合わせ、ブロック入場時に`Force`、攻撃状態、ガード、`CamEffect`、`vF_Multi`、BURNER要求を既定値へ戻してから命令列を実行する。ANI `Move`は保持状態として残り、数値代入または`STOP`でだけ更新され、action保持率を毎tick適用する。
- `WeaponAttack` / `WeaponAttack2` / `RunProc` / `RunProc2` の弾またはエフェクト。type 1はWEAPONPOINT生成、Script.spt `Energy`側からのp0消費（Generator非消費）、p1 trail点数、p2/100前進/tick、p3/100表示幅、20度初期照準、距離依存旋回、固定300 active tick、生成時ATTACK／AttackFlag、同一対象1回を専用Coreで扱う。原作はtrail対対象側複合形状だが、Unityは単一球target Adapter。type 55は追従サーベル表示だけ、type 57はWEAPONPOINT線分の前tick掃引判定、type 60は生成時WEAPONPOINT行列を複製して独立移動する非戦闘`LZ_ThunderEffect`表示。その他typeの引数位置には`GUIDE4.txt`由来の推定を含む。
- type 60は`p0/100`を初期幅、`p1`を表示長、`p2/100`を1 tick前進量、`p3`をtexture ID、`p4/100`を散布半径、`p5`を有効tickとして使う。有効期間後は初期幅の1/10ずつ減衰し、0.001未満で消える。原作の共有乱数列、DirectX9頂点・UV・blendは未確定で、Unityでは同じ±散布範囲を決定的hashと交差quadで表示する。
- SPT `ATTACKARMSET`は原作範囲0～1をTransformへ接続し、`LockArm1Target`／`LockArm2Target`のInspector参照が未設定の場合だけ照準対象階層として使う。`GUNFILENAME`／`SWORDFILENAME`は原作範囲0～19の読込済みHODノード名であり、外部`.x`を追加ロードしない。原作ANIコンパイラの`ChangeWeapon(GUN)`＝type 51、`ChangeWeapon(SWORD)`＝type 52に従い、51はGUN側表示・SWORD側非表示、52は逆とする。原作の再帰表示byte切替に合わせ、UnityではGameObjectを非active化せず、source命令と直接typeの両経路から対象ノード以下のRendererだけを再帰切替する。
- `ATTACK(power, down, force, forceY)`を威力、ダウン値、水平/垂直衝撃値へ展開する。`ATTACK`単独では命中せず、type 57格闘判定または発射体衝突時に4値を適用する。type 57と発射体はいずれも生成時の値を保持する。
- type 57は第3引数のWEAPONPOINT、第4引数÷100の長さ、第12引数`p8`の寿命tickを使う。第6引数は命中時に攻撃側・防御側へ同値を設定するhit-stop tick、第7引数は対象別命中履歴を保持する再命中interval tickである。同じ生成物でも履歴nodeが削除された後は同じ対象へ再命中でき、第7引数0なら次tickに再命中できる。原作の対象側複合当たり形状は、Test Playでは`TestPlayTargetDummy.hitRadius`の球へ適合する。旧公開`meleeRange`はInspector互換のため残すが、type 57判定には使わない。
- `AttackFlag`は発射体・type 57判定の生成時payloadへ複製する。Combat Coreではbit `0x01`／`0x08`のreaction 2／3、`0x04`のowner/self命中許可、`0x10`のtype 1 guard貫通、`0x20`のtarget/link解除と向き補正、`0x40`の通常reaction抑止を評価する。
- `ShildGuard`はboolへ畳まず値0～255を保持する。非0時の前方guard閾値はtype 1がdot `0.1736`、type 11が`0.766`、type 57が`0.5`。type 57かつ値1は攻撃側reaction 2・防御側`@int[157]=20`・防御側forward×`0.05`の攻撃側反動を返し、値2はguardだけを行う。
- `@int[157]`（guard-hit timer）と`@int[158]`（非0中の被弾受付抑止）、type 57 hit-stopは60 Hz tickで正値を1ずつ減算する。Combat TraceのHit eventにはAttackFlag、`Damaged`／`Guarded`／`Reflected`／`Invulnerable`、reaction、guard timer、hit-stopを記録する。
- type 11のAttackFlag bit `0x02`確率反射は、`LaserReflect`が機体`+0xB68`の低1 byteへ書いた値をsigned charで読み、0～99 rollと厳密な`<`で比較する。TestPlayはANI代入値を保持してブロック進入時に0へ戻し、`AniScriptRuntime`も編集プレビュー用に低1 byte・符号付き値を保持する。Combat Coreは同じ読取りを再現する。ただし現行シーンの被弾側は単一`TestPlayTargetDummy`で、実ANIのtype 11例もないため、実機体同士の入射反射と共有乱数列は未接続とする。
- ANIの攻撃プロファイルは`OriginalScriptProfile`、威力未設定時の既存タイプ別推定値は`UnityFallback`として区別する。発射体はrender frameの可変時間で直接移動せず、60Hz tickで寿命、homing、移動後の半径命中判定を更新する。
- X/Cは原作の押下済みbyteに合わせて押下エッジだけで開始し、`AttackDelay(slot,ticks)`の0～4スロットを60Hzで減算する。スロット0/1がX/Cに対応する。
- 銃／サーベル形態は`ChangeWeapon`と`@int[152]`を同期する。`@int[151]`は原作`FUN_004b8250`がANI実行直前に書くアニメーションチャンネル値であり、基本アクション0～49は原作`FUN_004d2030`どおり主0→副1の順に同じブロックを実行する。これによりチャンネル0側の移動・ジャンプ`Force`と、チャンネル1側の持替え18/68を両立する。サーベル形態では対応ANIが存在する場合に+50側のHOD姿勢を再生し、+50側にスクリプトがない場合は元の0～49側をスクリプトとフレーム時間の供給元にする。通常攻撃完了時は接地ならアクション6、空中なら8へ戻る。
- 格闘は方向別ID `130` / `131` / `141` / `146` / `151`を選び、格闘テーブル130～155の実行中は原作の主チャンネル値`@int[151]=0`を使う。C再押下を保留し、実データの`SwordCancel=132`や`133`が設定される受付ブロックへ到達した時点で連携する。誘導格闘130は5エネルギー/tickを消費し、6tick目以降に対象との中心間距離が`meleeApproachDistance`、既定3.5未満、またはゲージ枯渇になると136へ進む。開始15tick後のブースト／ステップキャンセルも受け付ける。
- MOD側の最終段にHODフレームだけがありスクリプト時間がない場合は、Unity側の明示的なフォールバックとして1フレームを60Hzの1tickで進める。最終フレーム後は接地なら6/56を経由して立ち0/50へ戻し、最後の攻撃姿勢を保持し続けない。
- 接地復帰6/56には、開始時に確定した有限時間を別カウンターで監視する。`ChangeAnime(56)`のような自己遷移で通常のスクリプト末尾判定が毎tick中断されても、その有限時間を1tick超えた時点で接地状態を解除し、立ち0/50へ必ず戻す。これは原作の接地コールバックが完了後に立ち0へ明示遷移することを守るUnity側の異常ANIフォールバックである。
- サーベル側の接地56と立ち50のようなスクリプトなしアクションへ移る際は、直前の格闘ブロックが残した照準要求、`Move`、`Force`、攻撃状態を明示的に消す。接地完了時には遷移ブレンドも終えて立ちHODの先頭フレームを即時適用し、接地姿勢へ格闘段ごとの一時状態が重ならないようにする。
- 原作`+0xBA8`に対応する`@int[150]`は0=接地、1=空中として`airborneFlag`と同期する。接地5/6は原作の接地分岐からだけ入るため、その再生中と立ち0/50へ切り替えた同じ物理tickでは接地を優先し、ANI更新後に行われるCharacterController更新が空中状態を再設定しないようにする。
- 射撃中の左右入力4/6は、現在ブロックの`ShotTurnAng`を機体Yawへ反映する。
- `LockBody...` / `LockArm...` はブロック内の照準要求として保持し、`bodyUpAimRoot` / `bodyDownAimRoot` / `arm1AimRoot` / `arm2AimRoot`が設定されている場合だけ、その表示Transformを対象へ向ける。ボーン割り当ては機体依存で、角度引数の厳密な意味も未確定なためUnity近似である。未設定時に機体ルートを回す旧フォールバックは廃止し、移動方位とカメラ基準を照準命令が書き換えないようにした。
- `BURNER(id, output)` と既存 `UI_SPT.LastSptData` の連携。IDは原作どおり0～19。通常表示は命令の存在で点火要求を立て、ANI outputの正負や大きさを表示倍率に使わない。既存MODの1引数形式は`output=1`として扱う。`BURNER2`は認識するが命令化しない。
- `CatchLastChara`を原作トークンとして認識する。旧Unity実装向けの`CatchChara`も互換別名として残す。
- `Snd` / `Voice`はPhase 4の型付きPresentation Eventを発行し、原作ID／シンボル、全引数、根拠区分、選択Adapterをtick traceへ残す。`TestPlayPresentationRuntime`の対応表にAudioClipがあれば機体位置から再生する。原作擬似コードで確認できる距離40の判定に合わせ、既定の最大距離は40。未登録素材は一度だけ警告する。短い固定効果音は開始時に音声データをプリロードする。
- `TestPlayPresentationRuntime.effects`へ`WeaponAttack:種別`や`RunProc2:種別:サブ種別`をキーとしてPrefabを登録すると、簡易球の代わりに対応エフェクトを生成する。
- `TestPlayPresentationRuntime.originalTextures`は原作の起動時ロード順と、別資料である`GUIDE4.txt`のスクリプトテクスチャIDを別フィールドで保持する。両者は同じ番号体系ではない。
- 移動は `idleAction` とは別の `moveAction` を使い、`@int[190]` を更新しながら方向キー押下中は既定ID `1` をループ再生する。原作の通常移動と同様に、Script内の`Move`が持つ水平速度を方向入力で作った移動方位へ割り当てるため、前進値だけを持つアニメーションでも後・左・右・斜めへ移動する。Scriptに水平`Move`がない場合だけ`inputMoveMagnitude`を使う。方向キー2回入力時のみステップアクションへ入る。
- 移動基準は切替可能。`useTargetRelativeMovement`がオンで、Sキーにより取得済みのロック対象が有効なら、毎tick更新する自機→対象方向を優先し、前=対象方向、後=対象から離れる方向、左右=対象方向に直交する方向とする。単に`target`がInspector設定されただけでは対象基準へ切り替わらない。対象基準が無効または利用不能で`useCameraRelativeMovement`がオンなら、`CamEffect`や視覚補間を適用する前の論理カメラ水平Forward/Rightを方向入力開始時に取得し、キー解放まで保持する。これにより非ロック時の後退で、旋回後の機体正面を次tickのカメラ基準として再読込して方位が反転する循環を防ぐ。両方をオフにすると、入力開始時の機体正面を同様に保持する。通常・空中・上昇・ブースト・ステップ開始判定は同じ優先順位を使い、開始済みステップの移動方位だけは途中のカメラ移動で曲がらないよう固定する。
- 通常移動の旋回量は`inputTurnDegreesPerTick`で調整する。既定値は通常移動更新`FUN_004d5420`で確認した12度/tick。`0`にすると即時に入力方向へ向く。ブースト開始時の15度/tickと、その後の内積式旋回は別設定として維持する。
- `airborneFlag` がオンの場合、方向入力中は空中移動`airMoveAction`、既定ID `4`、中立時は空中停止`airIdleAction`、既定ID `8`をループ再生する。ID `4`は下半身／表示側ANIとして姿勢、水平`Move`、BURNERを使うが、Unityで単一の物理actionとして正Y `Force`まで積分すると方向入力だけで無制限上昇できるため、正Y成分は物理上昇へ適用しない。接地した時点で着地`landingAction`、既定ID `5`を再生し、完了後に通常アイドルID `0`へ戻る。旧シーンで着地IDがジャンプ開始と同じ`3`の場合は起動時に`5`へ正規化する。
- 上昇ID `7`は原作`FUN_004d5b60`→`FUN_004d5ec0`に合わせて毎tickエネルギー5を消費する。Z保持・空中・エネルギーありの間は継続し、Z解放は5tick目以降、エネルギー枯渇は即座に空中移動／停止へ送る。HODモーションは`FUN_0056d600`→`FUN_0056dbe0`の非ループ指定に合わせて1回だけ進め、ANIスクリプト終端後は最終ポーズと最後の`Force`状態を保持する。Z保持中の方向入力へは最大14度/tickで姿勢を向ける。
- `Move`と`Force`はANI座標/tickのまま一度だけ積分し、最後に`aniUnitsToUnityScale`、既定`0.7`でUnity座標へ変換する。旧`moveScale=35` / `forceScale=35`は既存シーンとInspector互換のため残すが、原作準拠経路では使用しない。
- `Force`は原作`FUN_004cd840`どおり1 tick当たりの速度差分として積分する。上昇ID `7`はY速度をANI座標の`0.15`へ制限してから`0.04` / `0.02`を加える。方向入力中の空中移動ID `4`は実ANIの正Y `Force`命令自体を解析・表示用に維持するが、Unity単一action化で生じる無消費上昇を防ぐため物理積分には渡さない。0以下のY `Force`は維持する。`GvEnable`時は`0.013/tick`をY速度から引き、`-0.8/tick`を終端とする。その後、水平Force速度へ空中`0.95`または接地`0.9`を掛け、3軸速度へ`vF_Multi`を一度だけ適用する。`Force(...STOP...)`は対象軸の現在速度も停止する。
- 移動・空中移動・ステップ・ブーストから上昇へ移る場合は、直前のScript水平速度を上昇中の慣性へ引き継ぐ。この引き渡し箇所は擬似コード上で未特定のため、実プレイ挙動を再現する明示的な推定実装である。
- ブーストID `22`は実ANIの最初の5tickを停止区間として扱い、その後の`Move=(0,0,0.25f)`を入力方向へ変換する。開始10tick未満は最大15度/tick、その後は原作式`(dot - 1) * -2 + 0.5`度/tick（0.5～4.5度）で旋回する。
- ブースト開始時は原作`FUN_004d5ec0`／`FUN_004d68e0`の`-Generator/5`を一度だけ消費し、その後毎tickエネルギー5を消費する。入力を全解放しても30tickまでは継続し、31tick以降はZと方向入力の両方を離した時、またはエネルギーが0になった時に終了する。Zを離しても方向キーを保持していれば継続する。
- テストプレイ開始時、`UI_SPT.LastSptData`が未読なら現在の機体フォルダから`Script.spt`を復号・解析する。SPT `Generator`をブースト・上昇・ステップ用ゲージ`maximumEnergy/currentEnergy`へ、SPT `Energy`を別の補助ゲージ`maximumAuxiliaryEnergy/currentAuxiliaryEnergy`へ割り当てる。原作整数スロットは前者が`@int[100]/@int[101]`、後者が`@int[102]/@int[103]`で、既存Unity側MODとの互換用に同番号の`@float`も同期する。接地回復量は両SPT値とは別の`+0xD24=24`に合わせ、`groundedEnergyRecoveryPerTick=24`を既定値とする。SPT未読時は各fallback値を使う。
- SPT `HP`は`maximumHP/currentHP`を初期化し、被ダメージ・回復用APIもこの上限でクランプする。`Score`と`RestBody`はテスト状態へ保持し、`LockDist`は主ロック距離、20スロットの`SubLockDist`はサブ対象機能から取得できる。現テストモードにスコア集計・残機消費・複数サブ対象UIはまだないため、これら3項目は読込とランタイム公開までを確定範囲とする。
- `Z`が押されておらず、ブースト開始状態でもない時に`boostAction`がANIスクリプト等から再要求された場合は、空中フラグONなら現在の方向入力に応じた空中移動ID `4`または空中停止ID `8`へリダイレクトする。
- ステップ開始時は原作`FUN_004d0b70`と同様に、二度押しした世界方向を開始時の機体正面から見た前後左右へ分類してアニメーションID `11` / `12` / `9` / `10`を選ぶ。実際の移動方向は二度押しした方向コードをロック対象／論理カメラ基準の世界方向へ変換し、対象前方の`stepTargetForwardBias`、既定`0.3`を加えて正規化する。`useOriginalStepFacing`がオンなら、選択したANIのローカル前後左右がその世界進行方向へ一致する姿勢を求め、`stepTurnDegreesPerTick`、既定3度/tick以内で追う。横ステップなら機体正面を保ったまま横へ進み、後ステップなら正面を保ったまま後退する。オフ時だけ`preserveStepReferenceFacing`による旧近似を使用できる。
- ステップは原作`FUN_004d77e0`の継続条件に合わせ、開始後`stepMinimumTicks`、既定16tickまでは入力を離しても継続し、それ以降は入力解放・方向変更で終了する。押し続けても`stepMaximumTicks`、既定61tickで終了する。毎tickのエネルギー消費は`stepEnergyPerTick`、既定4で、0になった場合は16tickを待たず終了する。接地状態はステップ開始で変更せず、接地中の終了は専用`stepLandingAction`、既定ID `6`、既に空中なら空中停止ID `8`へ移行する。空中ステップ後に同じ方向を押し続けている間はID `8`を維持し、上向き`Force`を持つID `4`へ直結させない。方向解放または変更後の新規空中移動ではID `4`を再利用できる。ANIブロックの`unk`は丸めず、実データどおり8tickの初速区間から120tickの減速区間へ進む。`Move`がない場合だけ`stepFallbackMoveMagnitude`を同じ世界方向へ適用する。
- `useColliderGrounding` がオンの場合、テストプレイ開始時に機体ルートへランタイム用 `CharacterController` を追加または取得する。重力を含む原作tick速度は先に一度だけ計算し、`CharacterController.Move()`は衝突解決と接地判定にだけ使う。天井／床衝突時は該当するY速度を止める。接地先はUnity Colliderで、接地中は `airborneFlag` をオフ、非接地中はオンにする。上昇/ブースト中は一時的に空中扱いを強制するが、ステップは実際の接地状態を保持する。`gravity` / `terminalFallSpeed` / `groundedVerticalSpeed` / `applyGravityDuringForcedAirborneActions`は旧シーン互換用フィールドであり、原作準拠経路では使用しない。
- `characterControllerRadius` / `characterControllerHeight` / `characterControllerCenter` は機体サイズに合わせて調整する。足元が床に埋まる、または接地しない場合は、まず `characterControllerCenter.y` と `characterControllerHeight` を見直す。
- A/D/Fの特殊武器は従来どおり単発型。X/C通常攻撃は上記の形態切替、方向分岐、連携、6/8復帰を使う。
- アクション切替時は現在表示中のパーツ姿勢から次アクションの姿勢へ短時間ブレンドする。`blendActionTransitions`、`actionTransitionSeconds`、`heldReleaseTransitionSeconds` で調整する。

## 原作寄せテストプレイカメラ

`UI_MechaClean.unity`のMain Cameraには、編集用`FreeCam`と`TestPlayCameraController`を併設する。テストプレイ開始時はカメラ位置・回転・FOV・`FreeCam`状態を保存して`FreeCam`を無効化し、終了時にすべて復元する。編集モードの右ドラッグ、中ドラッグ、ホイール操作は変更しない。

テストプレイ中はマウス入力を使わず、次の2状態を`LateUpdate`で追従する。投影行列生成へ渡される値から原作FOVは60度と確認できたため、`UI_MechaClean.unity`ではテストプレイ中だけFOV 60を適用し、終了時に編集カメラのFOVへ復元する。

- Sキーで取得済みのロック対象があり、`useLockTargetWhenAvailable`がオン: 通常は機体から対象への水平方向を後方基準にし、機体と対象の間を`targetFraming`で注視する。左右または斜め入力中は`followLateralMovementHeading`により配置基準を機体の移動方位へ寄せ、左入力では左、右入力では右へカメラYawが追従する。後退または後ろ斜め入力中は`followBackwardMovement`によりカメラを追加で後方・上方へ引き、対象を画面前方に保ったまま自機との間隔を広げる。対象距離に応じて`targetDistanceScale`の範囲でもカメラを引く。
- 対象がない、または同設定がオフ: 機体の正面を基準に`followDistance`、`followHeight`の後方視点へ戻す。

`playerPivotHeight`、`targetPivotHeight`、追従距離、高さ、補間速度は原作の厳密値が未確定なのでInspector調整項目とする。FOV 60だけは原作確定値。左右移動方位の反映量は`lateralMovementHeadingWeight`、ロック中の最大回り込み角は`maxLockedOrbitAngle`で調整する。対象基準の移動と画面方向の乖離を抑える既定値として、前者は`0.5`、後者は`60`度とする。後退時の引き量は`backwardDistanceBonus`、持ち上げ量は`backwardHeightBonus`で調整し、後ろ斜めは約0.707倍を適用する。後退方位をそのまま180度反映するとカメラが自機とロック対象の間へ回り込むため、ロック中は設定角以内に制限して対象をカメラ前方へ維持する。急な位置変更は`teleportSnapDistance`以上で即時追従する。`avoidObstacles`はUnity Colliderに対するSphereCastでカメラの壁抜けを抑える「Unity代替」であり、機体ルートとロック対象自身のColliderは無視する。

`CamEffect`は原作側でbyte状態へ保存されることまでは確認済みだが、値ごとの演出内容は未確定である。現在の`approximateCameraEffects`は、非0値を短い位置・回転揺れへ変換する明示的な「Unity代替」。厳密なID対応が判明した段階で置き換える。

## BURNER表示

`TestPlayController.useConeBurnerEffects` がオンの場合、テストプレイ中の `BURNER(id, output)` はParticleSystemを再生せず、互換クラス名`TestPlayBurnerCone`の表示Adapterを対象ボーンへ生成する。`useOriginalBurnerTexture`がオンでGUIDEテクスチャID `8`の`burner.png`が登録済みなら、原作から復号した青白い噴射テクスチャを加算合成の交差板2枚へ貼り、どのゲームプレイ視点からも噴射形状が見えるようにする。未登録時だけ従来の単色Coneへ戻る。通常機体の原作描画値は `Script.spt` の `BURNERSET(id, frameName, value, fourthToken)` 第3floatそのもので、ANI `output`を乗算しない。Unityではこれへ既存Inspectorの`burnerLengthMultiplier`を掛け、太さを`burnerRadiusRatio`、点火／消灯遷移を`burnerFadeSpeed`、色を`burnerConeColor`または原作textureで構成する。命令が存在し第3floatが正なら表示し、`output=0`や負値でも通常表示要求は消えない。原作EXEは第4tokenを必須として読みますが保存・参照せず、方向指定として扱いません。Unityは原文を保持し、実データの`UP`／`DOWN`と未知tokenは追加回転なし、既存MODの`FORWARD`等だけを表示互換拡張として扱います。交差板／ConeはいずれもボーンのローカルZプラスへ伸びる。`WindomXP/TestPlayOriginalEffect`はURPとBuilt-in Render Pipelineの両方に描画Passを持ちます。速度倍率`value * max(speed * 10, 0)`は別constructorで生成される`CShip`専用で、現行`Windom_Data/Robo`のTest Playには接続しません。Presentation Coreは通常ロボット／`CShip`を型で区別し、`BB_Burner`の11回目終了、`BB_BurnerBall`の0.95行列倍率と5回目終了をfocused回帰へ固定しています。ただし既存Coneは持続表示Adapterなので、一時object生成周期と寿命を見た目へ混在させていません。交差板／Coneの太さ、alpha、fade、厳密なDX9頂点・UV・blendはUnity表示Adapterです。推進音は従来どおり正のANI output集合を開始／停止境界に使う独立Audio Adapterで、表示gateとは分離します。

## 原作効果音

`Assets/SND_SE`には原作のWAV素材を置く。`Tools > WindomXP > Test Play > Rebuild Original SND_SE Mappings`を実行すると、擬似コードの起動時登録表から確認できた次の24件を、現在のシーンにある`TestPlayPresentationRuntime.sounds`へ設定して保存する。ファイル名照合は大文字小文字を区別しない。

| Snd ID | ファイル | Snd ID | ファイル |
| ---: | --- | ---: | --- |
| 0 | `shot.wav` | 1 | `asioto.wav` |
| 2 | `tyakuti.wav` | 3 | `explode_m.wav` |
| 4 | `explode_l.wav` | 5 | `burner_f15.wav` |
| 6 | `shot2.wav` | 7 | `BeamHit.wav` |
| 8 | `Fannel.wav` | 9 | `sword.wav` |
| 10 | `sword2.wav` | 11 | `BeamHit2.wav` |
| 12 | `guard.wav` | 13 | `Bullet.wav` |
| 14 | `burst01.wav` | 15 | `BulletHit.wav` |
| 16 | `BeamReflect.wav` | 17 | `Henkei.wav` |
| 18 | `oc.wav` | 19 | `magic01.wav` |
| 20 | `magic02.wav` | 97 | `PI2.wav` |
| 98 | `PI.wav` | 99 | `cursor27.wav` |

ID 21～96は登録表に根拠がないため割り当てない。`burner.wav`はANIの`Snd`固定表へ追加せず、正の`BURNER(id, output)`が0件から1件以上へ変化した時の非ループ開始音に使う。同時に`burner_f15.wav`を専用AudioSourceで持続ループし、出力消失時は持続音だけを0.05秒fade後に停止する。固定`Snd(5)=burner_f15.wav`は別経路として維持する。この処理は実ANIの出力境界を利用したUnity Adapterであり、原作の持続音再生関数・音量・fade式の一致は主張しない。`engine.wav`は用途未確認のため未接続、`title_btn_decide.wav`、`title_btn_shift.wav`はタイトルUI用として保留する。`Voice`は機体別OGGを参照する別経路なので今回のWAV表には含めない。

## 原作エフェクトテクスチャ

`Assets/IMG_TX`には原作の暗号化画像を置く。原本は編集せず、`Tools > WindomXP > Test Play > Rebuild Original IMG_TX Mappings`でテストプレイ用コピーを`Assets/Generated/TestPlay/OriginalTextures`へ生成する。復号後のDX9時代のPNGはUnity 6のアセットインポーターが直接拒否する場合があるため、ランタイムデコーダーで一度読み、生成コピーだけをPNGへ再エンコードする。

2026-08-12時点では、擬似コードで確認した起動時ロード表44件すべてをシーンへ設定済み。`beam2.png`と、`Beam.bmp`、`Beam2.bmp`、`laser2.bmp`、`Beam3.bmp`、`blueLight.bmp`を含むBMP素材も取り込めている。

番号には次の2種類があるため混同しない。

- `loadSequence`: `WindomXP_orig_decompiled.c` 21410～21453行で確認した起動時ロード順。原作確定。
- `scriptTextureId`: `解析資料/GUIDE4.txt`が示す`RunProc2`等のテクスチャ番号。資料由来で、ロード順とは途中から一致しない。

表示はURP／Built-in両対応の`WindomXP/TestPlayOriginalEffect`シェーダーを使う。加算合成、カメラ正対、寿命フェードはDirectX9描画をUnityで再現するための「Unity代替」であり、個別エフェクトのUVアニメーション、色、拡大率、寿命の厳密値は今後の実機比較対象である。

## 回帰検証

Unityメニューの `Tools > WindomXP > Test Play > Run Runtime Verification` から原作確定仕様とUnity操作補正の統合回帰を実行できます。現在はPhase 6のMove保持、ジャンプ／Force、action 4の正Y Force除外、type 1を含むCombat、Presentation、Golden Trace関連に加え、type 62 subtype 2の`BB_WindRing`とsubtype 3の`BB_Burner`／`BB_BurnerBall` lifecycleを含む672項目を検査します。

2026-08-31のUnity `6000.5.0f1`実測では580項目が全件通過しました。

`Tools > WindomXP > Test Play > Run Real-Mech Golden Traces`では、`ガンダムTR-1ヘイズル改`の実`Script.ani` / `Script.spt`を読み込み、GT-001～GT-010をそれぞれ2回実行します。全tickがbyte単位で一致した場合だけ`Logs/TestPlayGolden`へUnity基準traceを出力します。GT-002はaction 7への単一入場と解放後action 8、GT-003は実ANIの約185tick再生が完了するまで220tick保持し、最終HODフレーム保持と解放後action 8を意味検証します。これは`RealAniObserved`であり、原作EXEから採取した観測値ではありません。

`Tools > WindomXP > Test Play > Compare Original Observation Trace`は、原作EXE観測JSONLの`observedFields`に宣言された項目だけを現在のUnity基準traceと比較します。取得不能な値は0で補わず、宣言済み値の欠落、未対応パス、hash／シナリオ不一致を診断します。最初の不一致と前後tickは`Logs/TestPlayOriginalCompare`へ保存します。GT-001取得契約と現状の制限は`Tools/OriginalTrace/README.md`を参照してください。

GT-001の比較用正規化では、原作の待機direction `5`をUnity規約の`0`へ変換し、float32最小正規値未満のMove残留値だけを停止値`0`として扱います。原作値は`input.rawDirection`、`rawScriptedVelocity`、raw CSVへ保持されます。また、Colliderを使わない決定性traceだけは開始setupに対応する論理接地面を持ち、実シーンのCollider接地経路には影響しません。

原作`scriptedVelocity`は`FUN_004cd840`入口の保持率適用前Move、Unity側はそのtickの要求変位へ合成する値です。一次擬似コードと原作アンカー観測で、待機actionの保持率`0.8`、同tick内のMove乗算、解放tickの`0.08 × 0.8 = 0.064`を確認しました。Phase 6BはCore反映、schema v1 Run 1～3の22tick一致、Runtime、Real-Mech、KD-03手動停止確認で受入済みです。schema v2完全traceは同形式の原作観測一致が明示的な受入条件になった場合だけ再開します。

Phase 6CではGT-002～GT-010を順番に全件原作観測せず、残件を接地Move、ジャンプ、ステップ／ブースト、Combat、ロック／Camera、Presentationへ分類しました。2026-08-31の再評価で各クラスの実装済み・未確定・Unity代替、追加観測条件、回帰が追跡できるため、Phase 6Cは境界付き完了です。U-005eの`RunProc2` type 1、U-005f～iのtype 62 subtype 8／6／2／3、U-006bのtype 57対象別再命中interval、U-007bの`LaserReflect`値経路は静的解析で一意になった範囲をCoreへ移し、追加原作EXE観測を要しませんでした。subtype 2は位置snapshotの`BB_WindRing`、subtype 3はWEAPONPOINTへ追従し11／5更新で独立終了する`BB_Burner`＋`BB_BurnerBall`です。いずれも非戦闘quadで、DX9描画はAdapter境界です。U-009aは`BURNERSET`第4tokenが原作で破棄されることを確定し、Unity parserの任意token保持と無回転fallbackを追加しました。U-009bは通常機体の表示gateとSPT第3float、ANI outputの分離を確定して通常表示からoutput倍率を除去しました。U-009cは速度倍率側を`CShip`専用と分類し、現行Roboデータへ未接続のまま維持します。

原作実行環境の機体データがプロジェクト内基準と異なる場合は、`Run Selected-Mech GT-001 Reference Trace`で原作側と同じ機体フォルダーから基準を作ります。結果はANI/SPT hash別に保存され、比較メニューが完全hashの一致する基準だけを選択します。最後に選択したフォルダーはEditor設定へ記憶され、再取得時は`Run Last Selected-Mech GT-001 Reference Trace`を使用できます。

同日の実測では10/10シナリオが成功し、各2回の全tickが完全一致しました。

## 移動デバッグ

`TestPlayController.logMotionDebug` をオンにすると、Unity Consoleへ移動系ログを出力する。`logMotionAssignments` がオンの場合は `Move` / `Force` / `vF_Multi` / `MoveLock` の代入時に `[TestPlay][MotionAssign]` が出る。`[TestPlay][RootMotion]` は `motionDebugIntervalTicks` ごと、または移動成分があるtickで出る。

確認する主な項目:

- `MotionAssign` が出ない場合: そのアクションのScriptで `Move` が実行されていない。
- `action=1` の歩きで `inputMove=True`: Scriptの水平`Move`量を方向入力の方位へ変換している。Scriptに水平移動量がない場合は`inputMoveMagnitude`を使う。
- ステップで `stepFallbackMove=True`: Script `Move` が取れないため、保存したステップ入力方向へ`stepFallbackMoveMagnitude`で動いている。`Move=(...)` があるステップでこれが出る場合はScriptブロックの取得または条件評価を確認する。
- `move=(0,0,0)` または `vF=0`: Script値または速度倍率が移動なしになっている。
- `moveLocked=True`: `MoveLock` により `Move` が無効化されている。
- `force` はANIが要求したraw値、`appliedForce`は物理積分へ渡した値。空中移動ID `4`の正Yでは、無消費上昇を防ぐAdapterにより`force.y > 0`でも`appliedForce.y = 0`になる。
- `scriptedMove=(0,0,0)`: `Move` / `aniUnitsToUnityScale` / ルート向きのいずれかで移動量が0になっている。
- `delta` が0以外で見た目が動かない場合: `path` に表示された `robo.root` が想定している表示ルートか確認する。
- ステップ時は `step=True` と`action`、`tick`、`worldMove`を見て、アニメーションIDと独立して入力方向へ進んでいるか確認する。`stepDist`の右辺は原作準拠モードでは`0`で、左辺だけが診断用の累積移動量になる。
- `logGroundingDebug` をオンにすると `[TestPlay][Grounding]` が出る。`grounded=True` ならCharacterControllerが床Colliderに接地しており、`airborne=False` なら接地復帰可能な状態。`forceAir=True` は上昇/ブースト中の強制空中扱いを示し、ステップだけではオンにならない。

未対応命令は `TestPlayController.logUnhandledCommands` がオンならConsoleへ出ます。
