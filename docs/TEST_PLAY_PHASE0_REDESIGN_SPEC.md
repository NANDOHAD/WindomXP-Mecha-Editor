# テストプレイ再設計 Phase 0 差分仕様書

> Phase 0時点の仕様・基準スナップショットです。2026-09-05からTest Playの目的は将来の独立クローンゲーム基盤、Unity対象は6000.6.0f1へ変更しました。現在の状態とタスク順は [DEV_STATE](../DEV_STATE.md) と [監査報告](PROJECT_AUDIT_2026-09-05.md) を参照してください。以下の旧Unity・198 assertionsは当時の記録として保持します。

## 目的

この資料は、原作 `WindomXP_orig.exe` の擬似コード、実際の ANI / AN2 / HOD / `Script.spt`、現行Unity実装の3者を比較し、テストプレイ再設計で採用する仕様と未確定事項を一元管理する台帳です。

Phase 0ではランタイム挙動を変更しません。後続Phaseで、原作確定事項をUnity側の推定や演出上の代替と混同せずに実装・検証できる状態を完了条件とします。

基準スナップショット:

| 項目 | 基準 |
| --- | --- |
| Unity | `6000.5.0f1` |
| 原作実行ファイル | `WindomXP_orig.exe` |
| 原作SHA-256 | `EC5A09973CD00C1BAB7AD7FE284293C06A415C65378410E31E4534327CCC20C1` |
| デコンパイル結果 | 4,291関数、失敗0 |
| 現行テストプレイ検証 | `TestPlayRuntimeVerification` 198 assertions |

一次資料への入口は [WINDOMXP_ORIGINAL_BEHAVIOR_REFERENCE.md](WINDOMXP_ORIGINAL_BEHAVIOR_REFERENCE.md)、命令一覧は [SCRIPT_ANI_COMMANDS_ORIGINAL_SPEC.md](SCRIPT_ANI_COMMANDS_ORIGINAL_SPEC.md)、関数単位の解析は [SCRIPT_ANI_ORIGINAL_DECOMPILED_ANALYSIS.md](SCRIPT_ANI_ORIGINAL_DECOMPILED_ANALYSIS.md) を正とします。この資料はそれらの解析内容を再掲するのではなく、Unity実装との差分と実装判断を管理します。

## 判定記号

### 原作根拠

| 記号 | 区分 | 採用方針 |
| --- | --- | --- |
| `C` | 原作確定 | 擬似コードの分岐、固定値、書き込み先、呼び出し順を直接追跡できる。Coreの回帰期待値にできる。 |
| `H` | 原作高確度 | 擬似コードと実データが一致し、用途がほぼ一意。反例データを継続確認する。 |
| `I` | 原作推定 | 周辺コード、名称、実データからの推定。調整可能かつ差し替え可能にする。 |
| `U` | Unity代替 | DirectX9、Win32入力、原作内部オブジェクトのUnity置換。原作値として固定しない。 |

### 現行実装範囲

| 記号 | 区分 | 意味 |
| --- | --- | --- |
| `E` | 実装済み | 現行テストプレイで明示的な処理と回帰検証がある。 |
| `P` | 部分実装 | 入口または代表ケースのみ。タイプ別引数や実行順などに不足がある。 |
| `S` | 受け皿 | 登録、警告、no-op、簡易イベントだけで原作効果を再現しない。 |
| `N` | 未実装 | 現行テストプレイのハンドラーがない。 |
| `X` | 原作側無効 | トークンは認識するが、解析対象ビルドでは命令オブジェクトを生成しない。 |

`AniScriptRuntime.Register()` に登録されているだけの命令は `E` と数えません。同クラスは編集プレビューを壊さず未知命令を受ける移植レイヤーであり、登録済みでもハンドラーの厳密な原作互換性は保証されないためです。

## 現行アーキテクチャ差分

| 領域 | 原作モデル | 現行Unity | 判定 | 再設計判断 |
| --- | --- | --- | --- | --- |
| ANI解析 | ロード時に命令オブジェクト化 | `TestPlayScriptVM`が文字列を事前分割し、文字列名で分配 | `C/P` | Phase 1で型付き中間表現へ統合する。 |
| 実行トラック | 主トラック、副トラック、サブトラックを別状態で更新 | 単一の現在アニメーション状態を中心に、基本アクションのチャンネルを逐次補完 | `C/P` | トラックごとにblock、remaining tick、channel、repeat状態を保持する。 |
| 命令意味論 | 命令ID別ハンドラー | `TestPlayController`と`AniScriptRuntime`へ分散 | `C/P` | 解析・命令定義を共通化し、実行アダプターだけを分ける。 |
| 時刻 | 16.666666ms基準 | 本体は独立60Hz、弾・一部演出は`Update` / `LateUpdate` | `C/P` | ゲーム結果に影響する処理を60Hz Coreへ移す。表示補間だけをrender frameで行う。 |
| 移動積分 | Force加算、水平減衰、重力、倍率、位置加算 | 主要式は再現。接地は`CharacterController`へ変換 | `C/E+U` | Coreの要求移動とUnity接触結果を明示的に分離する。 |
| 攻撃 | 武器／Procタイプ別オブジェクト、判定、対象選択 | 汎用弾、距離ベース近接、単純HPダミー | `H/P+U` | Phase 3で武器プロファイルとtick基準判定へ置換する。 |
| SPT | 機体、武器ポイント、攻撃腕、各種ファイル、ゲージ、ロック等 | BURNERSET、名称、HP、Generator、Energy、Score、RestBody、LockDist、SubLockDist | `H/P` | 原作根拠のあるフィールドだけを不変セッションプロファイルへ追加する。 |
| カメラ | FOV 60は確定、配置式は未確定 | 追従、ロック構図、障害物回避、揺れをUnity実装 | `C+I/U` | Coreは論理注視情報とイベントだけを出し、構図は調整可能なAdapterに残す。 |
| 演出 | ID／タイプ別の原作処理 | 音、テクスチャ、BURNER、簡易エフェクトをUnity表示 | `H/P+U` | 発生tickと原作IDをCoreイベントへ残し、表示方式と分離する。 |
| 検証 | 原作実行結果との比較が必要 | 198 assertions。主にヘルパーと状態遷移 | `P` | tickトレース、実機体E2E、決定性検証を追加する。 |

### 現行60Hz処理順

`TestPlayController.SimulateOriginalTick()` の現在順は次のとおりです。

```text
tick更新
  -> 攻撃クールダウン
  -> 入力状態
  -> ロック対象
  -> 対象状態
  -> Generator回復
  -> 入力からアクション選択
  -> ANIブロックとHOD進行
  -> 照準命令反映
  -> Move / Force / CharacterController移動
  -> 入力ラッチ消費
```

この順序は現行回帰の基準として保存します。ただし、攻撃オブジェクト、命中、接触結果、演出寿命まで含めた原作の完全な順序と確定したものではありません。Phase 1以降では各段階をトレースし、原作関数の呼び出し順が確認できた範囲から置き換えます。

## ANI命令差分台帳

### フロー・状態・移動

| 命令 | 根拠 | TestPlay | 現状と次の判断 |
| --- | --- | --- | --- |
| `IF` / `ELSE` / `ENDIF` | `C` | `E` | 6比較演算子と入れ子を実装済み。Phase 1で型付き条件命令へ移す。 |
| `@int[0..199]` / `@float[0..199]`、複合代入 | `C` | `E` | 範囲と演算は実装済み。全インデックスの意味は確定しない。 |
| `GoScriptIndex` | `I` | `P` | block indexを書き換える。原作のtrack別停止／yield戻り値との一致を再検証する。 |
| `GoPoseIndex` | `I` | `P` | HOD frameを直接変更する。補間時間と境界処理は未確定。 |
| `ChangeAnime` | `H` | `E` | アクション遷移を実装。Phase 2で論理アクション、姿勢ソース、スクリプトソースを分離する。 |
| `AnimeLoop` | `H` | `E` | フラグを保持。主／副／サブtrackごとの終了処理へ移す。 |
| `ExecScriptEveryTime` | `C` | `E` | `n + 1` tick周期を検証済み。trackごとに独立保持する。 |
| `RunSubScript` | `I` | `N` | 編集プレビュー側は受け皿のみ。呼び出し対象、戻り、共有状態を原作で追跡する。 |
| `Move` | `C` | `E` | 数値0は維持、`STOP`は軸と速度を停止。Coreへ移す。 |
| `Force` | `C` | `E` | tick単位の速度差分として実装。Coreへ移す。 |
| `MoveLock` | `H` | `E` | boolとして保持。入力抑止範囲は原作側を追加追跡する。 |
| `BoostDashMode` | `I` | `N` | ブーストアクション22のハードコードとは別。命令の書き込み先を特定する。 |
| `ShotTurnAng` | `H` | `E` | 射撃中Yawへ反映。角度適用順をトレース対象にする。 |
| `TurnMoveAng` | `I` | `P` | 値を保持するが用途全体は未確定。 |
| `vF_Multi` | `H` | `E` | 共通速度積分へ一度適用。 |
| `Sub_LRKey` | `I` | `N` | 編集プレビュー側は受け皿のみ。入力ゲートの書き込み先を追跡する。 |
| `GvEnable` | `H` | `E` | 現行では重力有効フラグとして使用。名称の意味ではなく原作書き込み先で再確認する。 |
| `Rnd` / `LocalRnd` | `I` | `N` | 決定性のためRNG状態とseedをCore状態へ含める。原作乱数範囲を追跡する。 |
| `SetExtParam` | `I` | `N` | 配列範囲、型、利用先が未確定。 |

### 照準・攻撃・武器

| 命令 | 根拠 | TestPlay | 現状と次の判断 |
| --- | --- | --- | --- |
| `LockBodyUpTarget` / `LockBodyDownTarget` / `LockBodyTarget` | `H` | `P` | Transform割り当て時だけUnity `LookAt`系で近似。角度引数とボーン回転式は未確定。 |
| `LockArm1Target` / `LockArm2Target` | `H` | `P` | 腕TransformへのUnity近似。機体別SPT割り当てが不足。 |
| `ATTACK` | `C` | `E` | 4値プロファイルへ展開し、単独では命中させない。 |
| `AttackFlag` | `C` | `P` | 値を保持。全ビット意味と命中側の利用が未確定。 |
| `AttackPow` / `AttackDownF` / `AttackForce` | `C` | `E` | 攻撃プロファイルへ反映。 |
| `AttackForceY` | `C` | `E` | `ATTACK`から内部生成される値として反映。独立入力トークンとは扱わない。 |
| `ShildGuard` | `H` | `P` | boolを保持。被弾軽減、ガード硬直、方向条件は未実装。 |
| `LaserReflect` | `H` | `N` | 反射対象と値20の意味を追跡する。 |
| `SwordEnable` | `I` | `N` | type 55表示および武器形態との関係を追跡する。 |
| `ChangeWeapon` | `H` | `E` | `@int[152]`と銃／剣状態を同期し、原作コンパイル結果GUN＝type 51／SWORD＝type 52と同じSPTモデル表示を適用。 |
| `SwordCancel` | `H` | `E` | C押下保留と格闘遷移を実装。ヒット時限定か受付時点かは実データ別に記録する。 |
| `AttackDelay` | `H` | `E` | 5スロットを60Hzで減算。 |
| `WeaponAttack` / `WeaponAttack2` | `H` | `P` | 汎用弾を生成。タイプ別引数、発射口、消費ゲージ、寿命、誘導は未再現。 |
| `RunProc` / `RunProc2` | `H` | `P` | 一部タイプを簡易弾／演出／近接へ分岐。完全なタイプ別処理ではない。 |
| `CatchLastChara` | `C` | `S` | 名称と3引数は認識するがno-op。対象参照の保持方法を追跡する。 |

### 演出・ゲージ

| 命令 | 根拠 | TestPlay | 現状と次の判断 |
| --- | --- | --- | --- |
| `BURNER(id, output)` | `C` | `E/U` | ID0～19とoutputを受け、Unityエフェクトへ変換。原作表示式とは区別する。 |
| `BURNER2` | `C` | `X` | 原作同様、認識して警告するが実行しない。 |
| `BunerOut` | `C` | `X` | `BURNER`の内部クラス名。独立入力命令にしない。 |
| `Snd` | `C` | `P/U` | ID0～20、97～99の既知対応をUnity AudioClipで再生。21～96は割り当てない。 |
| `Voice` | `C` | `P/U` | イベントとUnity再生経路はあるが、機体別ボイス対応が不足。 |
| `CamEffect` | `H` | `P/U` | 値をUnityカメラ揺れへ近似。原作式は未確定。 |
| `AddExGauge` | `I` | `P` | `@int[155]`へ加算。原作ゲージ本体との接続は未確定。 |
| `AddEnergy` | `I` | `P` | 現行Generatorへ加算。Generator／Energyどちらかは書き込み先の追加確認が必要。 |

## アクション差分台帳

| アクション | 原作根拠 | 現行範囲 | Phase 0判断 |
| --- | --- | --- | --- |
| 0/50 立ち、1/51 歩き | `H` | `E` | +50姿勢と元IDスクリプトの分離をPhase 2のモデル要件とする。 |
| 3/53 ジャンプ開始、7/57 上昇 | `C` | `E` | 最低5tick、Z保持、ゲージ5/tick、非ループ最終ポーズ保持を固定回帰とする。 |
| 4/54 空中移動、8/58 空中停止 | `H` | `E` | Force上限、入力解放、重力移行を固定回帰とする。 |
| 5/55 通常着地、6/56 ステップ着地 | `H` | `E+U` | 原作の立ち復帰結果を固定し、異常ANI監視はUnity代替として残す。 |
| 9～12 / 59～62 ステップ | `C/H` | `E+U` | ID、最短16、最長61、角度上限、ゲージ4を固定。CharacterController接触はUnity代替。 |
| 18/68 武器切替 | `H` | `E` | `@int[151]`チャンネルと`@int[152]`形態を固定回帰とする。 |
| 22/72 ブースト | `C/H` | `E+I` | ゲージ消費と主要旋回は維持。二度押し0.3秒は推定値として調整可能にする。 |
| 100 通常射撃、106 飛行射撃 | `H` | `P` | 開始・復帰は再現。弾タイプ、発射口、ヒット処理はPhase 3対象。 |
| 104/105/109等 サブ攻撃 | `H` | `P` | 入力とアクション開始はあるが、機体固有武器処理は未再現。 |
| 130～155 格闘 | `H` | `P` | 方向選択、誘導130、SwordCancel、復帰、type 57の掃引・寿命・hit-stop・対象別再命中intervalを実装。対象側複合形状はUnity Adapter。 |
| 13～17 被弾・ダウン、20受身、21防御ダッシュ | `H/I` | `N` | Phase 3で被弾状態機械と同時に設計する。 |
| 19/69 防御 | `H` | `P` | 入力／フラグ中心。防御方向、軽減、硬直、反射は未再現。 |
| 23/73 変形、24/74 変形解除、27/77 HIT回避 | `I/H` | `N` | 実ANIと原作分岐が揃うまでCoreへ入れない。 |
| その他機体固有ID | `I` | `N/P` | IDの存在だけで意味を固定しない。実データ単位のプロファイルで追加する。 |

## 状態・入力差分台帳

| 状態 | 根拠 | 現行 | 判断 |
| --- | --- | --- | --- |
| `@int/@float[0..199]` | `C` | 256要素のserialized配列を持ち、Scriptからは0～199だけ許可 | 配列互換は保持し、Coreでは変更slotをトレースする。 |
| `@int[100]` / `@int[101]` | `H` | Generator現在／最大を別フィールドと同期 | 直接配列とフィールドの二重正本を解消する。 |
| `@int[102]` / `@int[103]` | `H` | Energy現在／最大を別フィールドと同期 | Generatorとは別ゲージとして保持する。 |
| `@int[150]` | `H` | 0接地、1空中として同期 | 接触判定の前後どちらを同tickへ公開するかをtraceに残す。 |
| `@int[151]` | `C` | ANIチャンネル0/1を実行時に設定 | track状態の一部へ移し、命令実行直前の値を公開する。 |
| `@int[152]` | `H` | 0銃、1剣 | 形態の正本として維持する。 |
| `@int[153]`～`@int[158]` | `I/H` | 一部のみ利用 | ヒット、敵状態、自機状態、ガード被弾、無敵時間をPhase 3で追跡する。 |
| `@int[190]`～`@int[198]` | `H` | 方向、Z/X/C/V/S/A/D/Fへ同期 | raw、held、pressed、releasedを別々にtraceする。 |

## SPT・武器・演出差分

| 領域 | 現行 | 不足 | 後続Phase |
| --- | --- | --- | --- |
| 基本値 | Name、NameEng、HP、Generator、Energy、Score、RestBody、LockDist、SubLockDist | 原作での全利用箇所 | Phase 1/2 |
| BURNERSET | ID、frameName、第3float、第4token原文、Transform接続。第4tokenは原作で破棄、Unity既知方向だけ互換表示へ投影 | 第3floatとANI出力値から表示への経路 | Phase 4 |
| 武器発射口 | `WEAPONPOINT` ID 0～49、フレーム、UP/DOWN、Transformを保持 | 他の武器typeでの参照式 | Phase 3 |
| 攻撃腕 | `ATTACKARMSET` ID 0～1、フレーム、Transform接続。Inspector照準参照が未設定の場合だけfallback | 原作の軸別制限・補間式。type 57経路からは参照されない | Phase 3 |
| 武器表示ノード | `GUNFILENAME`／`SWORDFILENAME` ID 0～19、読込済みHODフレーム、Transform接続。外部loader参照なし。GUN＝type 51でGUN側表示、SWORD＝type 52でSWORD側表示。source `ChangeWeapon`も同じRenderer再帰切替へ接続 | 原作の腕回転制限式 | Phase 3/4 |
| 効果音 | 既知Snd IDをUnity AudioClipへ割り当て | ID21～96、音量、距離、同時発音規則 | Phase 4 |
| 原作テクスチャ | 44ロード順と既知script IDを分離保持 | UV、寿命、blend、billboardのタイプ別式 | Phase 4 |
| カメラ | FOV 60、追従・ロック・障害物回避・揺れ | 原作位置、注視点、CamEffect値別式 | Phase 4 |

## 未解決事項台帳

| ID | 未解決事項 | 現状の扱い | 確定に必要な証拠 | 主に止めるPhase |
| --- | --- | --- | --- | --- |
| `U-001` | 主／副／サブtrackの完全な生成・停止・yield順 | 現行逐次実行を維持 | `FUN_004b8030`、`FUN_004b8250`、`FUN_004b84d0`の呼び出し元と実ANI trace | 1 |
| `U-002` | 初期スクリプトの全生成経路での発火時刻 | 通常blockと分離保持 | キャラクター生成2経路、AN2初期リストの呼び出し追跡 | 1 |
| `U-003` | HOD補間の単位、丸め、最終frame境界 | Unity補間 | `FUN_0056d600` / `FUN_0056dbe0`の値追跡と実frame比較 | 2 |
| `U-004` | 移動・接触・重力の完全なtick順 | 現行順を基準 | `FUN_004cd840`前後の接地更新呼び出しと原作実プレイtrace | 2 |
| `U-005` | `RunProc*` / `WeaponAttack*`全タイプの引数 | type 53／54／55／60は一次コードへ対応付けて用途別Presentationへ分離済み。次の代表小区分はGT-007で使う`RunProc2` type 1 | type 1は`FUN_004e8310`のhandler・生成・更新・衝突と複数機体実データを先に追跡。全type一括実装はしない | 3/4（継続） |
| `U-006` | type 57のボーン、形状、持続tick、多段、対象 | 一次証拠化・Core実装済み。WEAPONPOINT掃引、`p8`寿命、ATTACK snapshot、`p2` hit-stop、`p3`対象別再命中interval。対象形状は球Adapter | `FUN_004b74a0`、`FUN_004fa150`、`FUN_00502e60`、`FUN_00498260`、`FUN_004b27a0`、`FUN_00495750` | 完了（境界あり） |
| `U-007` | AttackFlag全ビット、防御、反射、無敵 | bit `0x01`／`0x04`／`0x08`／`0x10`／`0x20`／`0x40`、ShildGuard値1／2、type 1／11／57前方閾値、`@int[157]`／`@int[158]`、type 57 hit-stopをCore実装済み | type 11 bit `0x02`の防御側確率`+0xB68`設定元、`LaserReflect`読取り、実機体被弾Adapter | 完了（境界あり） |
| `U-008` | SPTのWEAPONPOINT／ATTACKARMSET／武器表示定義 | U-008a／bとして原作範囲・Transform接続、ChangeWeapon GUN＝type 51／SWORD＝type 52を実装。FILENAME名でも読込済みHODノードの再帰検索で、外部loader consumerはない。UnityはGameObject停止ではなくRenderer再帰切替 | 原作の腕回転制限式 | 完了（境界あり） |
| `U-009` | BURNERSETとBURNER出力の描画式 | U-009aで第4token破棄を確定し、任意tokenの原文保持・未知値無回転fallbackを実装。表示はUnity Particle／Cone近似 | U-009bで第3float、ANI output、通常機体／飛行機体系の描画値経路を分離追跡 | 4 |
| `U-010` | 原作カメラ位置・注視点・CamEffect式 | Inspector調整可能なUnity代替 | `FUN_0044f170`周辺の座標式と値別分岐、原作映像比較 | 4 |
| `U-011` | RNGアルゴリズム、seed、Rnd/LocalRnd範囲 | 未実装 | 乱数関数呼び出し先、初期化、命令ハンドラー | 1/3 |
| `U-012` | `Script.ani`内部HODの暗号・変換境界 | 現行ローダー互換を維持 | loaderのread/decode境界と既存ファイル再読み込み比較 | 1 |
| `U-013` | 全命令文字列と命令ID／ハンドラー対応 | 部分一覧 | 文字列テーブル、parser分岐、handler tableの全照合 | 1～4 |

未解決事項は推定実装を禁止する一覧ではありません。推定またはUnity代替で進める場合は、対応ID、理由、差し替え境界、Inspectorまたはデータ上の調整点を残します。

## tickトレース契約

Phase 1以降のCoreは、同じデータ・初期状態・入力列から同じトレースを生成できることを必須とします。トレースはUnityのInstance IDや`Time.deltaTime`を含めず、1行1tickのJSON Linesを想定します。

### セッションヘッダー

| フィールド | 内容 |
| --- | --- |
| `schemaVersion` | トレース形式の版。最初は`1`。 |
| `source` | `unity-core`、将来の`original-observation`等。 |
| `unityVersion` | Unity側記録時のみ。 |
| `mechId` | 機体フォルダの相対識別子。絶対パスは保存しない。 |
| `aniHash` / `sptHash` | 読み込んだ入力データのハッシュ。 |
| `tickRate` | `60`。 |
| `initialRngState` | RNG利用時の初期状態。 |
| `evidenceProfile` | 採用した推定／Unity代替設定の版。 |

### tickレコード

| グループ | 必須内容 |
| --- | --- |
| `tick` | 0始まりの整数tick。 |
| `input` | 方向コード、各キーのheld／pressed／released、二度押し判定、入力ラッチ消費。 |
| `action` | 論理action ID、表示pose ID、script source ID、開始からのtick、遷移理由。 |
| `tracks` | 主／副／サブごとのchannel、block index、remaining tick、frame、repeat counter、finished。 |
| `stateDelta` | 変更された`@int` / `@float` indexと変更前後。 |
| `motion` | Move、Force、積分前後velocity、vF_Multi、要求変位、向き、接地状態の前後。 |
| `resources` | HP、Generator、Energy、各AttackDelay slotの前後。 |
| `combat` | 攻撃プロファイル、spawn、hit、damage、down、impact、cancelイベント。 |
| `presentation` | Snd、Voice、BURNER、Proc、texture、CamEffectの原作IDと引数。 |
| `diagnostics` | 未対応命令、引数不足、fallback、Unity代替を使用した理由。 |

浮動小数は書式依存で丸めず、ラウンドトリップ可能なInvariant Culture表現で保存します。比較側では項目ごとに完全一致と許容誤差を分け、位置・回転のUnity変換値だけに明示した許容誤差を設定します。

### 最初のgolden traceシナリオ

| ID | シナリオ | 固定する観測点 |
| --- | --- | --- |
| `GT-001` | 待機から前進して解放 | 1/51、Move、旋回、解放後停止、Generator回復。 |
| `GT-002` | Z短押し | 3/53完了、7/57最低5tick、8/58、重力移行。 |
| `GT-003` | Z長押し | 7/57継続、5/tick消費、HOD最終ポーズ保持、解放後8/58。 |
| `GT-004` | 空中方向入力から解放 | action 4/54、Y速度上限、Force停止、下降開始。 |
| `GT-005` | 方向二度押しステップ | 9～12、開始方位、最短16、最長61、ゲージ4、6/56復帰。 |
| `GT-006` | Z二度押しブースト | 22/72、初回と継続ゲージ消費、旋回、解放条件。 |
| `GT-007` | X射撃 | 押下エッジ、AttackDelay 0、100、ATTACK、spawn、6/8復帰。 |
| `GT-008` | C持替えから方向格闘 | 18/68、`@int[151]`、`@int[152]`、130/131/141/146/151選択。 |
| `GT-009` | 格闘連携 | SwordCancel受付、type 57発生、接地6/56、0/50復帰。 |
| `GT-010` | ロックと射撃旋回 | `@int[195]`、対象、ShotTurnAng、照準イベント、カメラ論理方向。 |

## Phase 0完了条件

- 既知のANI命令群が本資料の差分台帳へ分類されている。
- 現行テストプレイで扱う主要アクションと、未再現の被弾・変形系が分離されている。
- 状態、入力、SPT、武器、演出の正本と不足が明示されている。
- 未確定事項がID付きで管理され、何を確認すれば確定できるかが記録されている。
- Phase 1以降で使うtickトレースの必須項目と最初のgoldenシナリオが定義されている。
- 既存のpublicフィールド、MonoBehaviour名、UnityEvent、シーン参照、ANI / AN2 / HOD / SPTの読み書きに変更がない。
- 現行198 assertionsをPhase 1開始時の回帰基準として維持する。

## Phase 1開始時の実装境界

Phase 1では、まず型付きのANI中間表現とtrack schedulerを純粋C#として追加します。`TestPlayController`は既存Inspector／UnityEvent互換のFacadeとして残し、次の順で移行します。

1. 現行`TestPlayScriptVM`の入力を、型付きcommand／assignment／conditionへcompileする。
2. 主／副／サブtrackの状態を独立させる。
3. 現行VMと新Coreへ同じ入力スクリプトを与え、Phase 0のtrace項目で差分を比較する。
4. 198 assertionsとGT-001～GT-010を新Coreへ移してから、現行の個別分岐を段階的にFacade外へ出す。

この段階では、type 57、全武器タイプ、原作カメラの未確定値を推測で完成扱いにしません。未知命令は無言で無視せず、命令名、引数、action、track、block、tickを診断イベントへ記録します。
