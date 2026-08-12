# テストプレイモード実装メモ

このモードは、Unity上でオリジナル版の実行結果を可能な限り再現するための独立ランタイムです。既存の `MechaAnimator` / `AniScriptRuntime` / 編集UIの互換性を保ちながら、確定した原作仕様を `Assets/Scripts/TestPlay/` 以下へ段階的に実装します。挙動だけでなく、最終的には武器、エフェクト、効果音、ボイス、カメラ演出も再現対象とします。

実装は、擬似コードと実データから確認できた「原作確定」、動作から補う「原作推定」、DirectX9固有処理を置き換える「Unity代替」を区別します。

## 追加したコンポーネント

| ファイル | 役割 |
| --- | --- |
| `TestPlayController.cs` | 入力、ANI tick、HOD補間、Script命令、簡易武器生成をまとめるテストプレイ本体。 |
| `TestPlayScriptVM.cs` | `IF`、関数呼び出し、代入、`@int/@float`、`STOP` を扱う簡易Script VM。 |
| `TestPlayStateTable.cs` | ゲーム本体の `@int[]` / `@float[]` 風の状態テーブル。 |
| `TestPlayScriptValue.cs` | Script引数の数値、シンボル、`STOP` 表現。 |
| `TestPlayTargetDummy.cs` | ロック対象/被弾対象のダミー。 |
| `TestPlayProjectile.cs` | `WeaponAttack` / `RunProc2` 用の簡易弾。 |
| `TestPlayCameraController.cs` | テストプレイ中だけ編集用`FreeCam`を置き換え、機体後方追従、ロック対象フレーミング、障害物回避、`CamEffect`近似を担当する。 |
| `TestPlayRuntimeEvent.cs` | スクリプト、戦闘、エフェクト、音声を接続する型付きイベントと攻撃プロファイル。 |
| `TestPlayPresentationRuntime.cs` | `Snd` / `Voice`、原作テクスチャID、Unity上の演出生成を接続する演出層。 |
| `TestPlayOriginalEffect.cs` | 原作テクスチャを加算合成・ビルボード表示し、寿命とフェードを管理するランタイム。 |
| `Assets/Editor/TestPlayOriginalSoundSetup.cs` | 原作の固定Snd ID表から`Assets/SND_SE`のAudioClip参照をシーンへ再生成する。 |
| `Assets/Editor/TestPlayOriginalTextureSetup.cs` | `Assets/IMG_TX`を原本のまま保持し、テスト用復号コピーと原作ロード順/スクリプトID対応を再生成する。 |
| `Assets/TestPlayOriginalEffect.shader` | URP用の両面・Z書き込みなし・加算合成シェーダー。 |
| `Assets/Editor/TestPlayRuntimeVerification.cs` | 原作確定仕様のエディタ回帰検証。 |

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
| `Z` | 地上ではジャンプ開始ID `3`を完了して上昇ID `7`へ進む。短押しでもID `3`を中断せず、ID `7`を最低5tick再生する。上昇ID `7`は原作の12コールバックで空中移動／停止へ遷移し、保持だけで無限に上昇しない。空中停止中の保持／単押しは、空中停止を11tick待ってからエネルギー80を使って再上昇する。 |
| `Z` を短時間内に2回入力 | `@int[191]` ブースト開始入力。既定の判定間隔は`doubleTapBoostSeconds=0.3`秒で、空中からブーストダッシュID `22`へ移る。 |
| `X` | `@int[192]` 射撃。 |
| `C` | `@int[193]` 格闘。 |
| `V` | `@int[194]` 防御。 |
| `S` | `@int[195]` ロック取得／解除。現在は単一の`target`候補をトグルする。 |
| `A` / `D` / `F` | `@int[196]` - `@int[198]` サブ攻撃。 |

`S`は現在、単一の`target`候補に対するロック取得／解除トグルとして動作する。候補が`Script.spt`の`LockDist`より遠い、非アクティブ、またはHP 0の場合は取得せず、ロック中に条件を外れた場合も解除する。SPT未読時は`fallbackLockDistance`を使う。

## 現在の実装範囲

- テストプレイ本体はUnityの`FixedUpdate`から独立した60Hz（16.666ms）の蓄積型シミュレーションで進む。低フレーム時は`maximumCatchUpTicks`まで追いつき、余った時間は捨てず次フレームへ持ち越す。キー状態は毎描画フレームで取得し、短い押下を次のシミュレーションtickまでラッチするため、60Hz境界間の単発入力を失わない。二度押し判定も実時間ではなく60Hz tick差で行う。

- `Move` / `Force` / `STOP`。`Move=(...)` / `Force=(...)` の代入形式と `Move(...)` / `Force(...)` の関数形式を扱う。原作の`Move`では数値`0`が現在値を維持し、`STOP`が対象軸を明示的に0へ停止する。
- `@int[0..199]` / `@float[0..199]` の代入、`+=`、`-=`、`*=`、`/=`。シーン互換のため配列自体は従来サイズを保つが、Script.aniから200以上は参照できない。
- `IF` / `ELSE` / `ENDIF`。`==` / `>=` / `<=` / `!=` / `>` / `<`とネストを扱う。
- セミコロン区切り、CRLF/LF、アポストロフィから行末までのコメント、同一行の複数命令。
- スクリプト文字列を命令列へ事前解析してキャッシュし、制御遷移命令で残り命令の走査を中断する。
- `ChangeAnime` / `GoScriptIndex` / `GoPoseIndex` / `AnimeLoop`
- `ExecScriptEveryTime(0)`は毎tick、`ExecScriptEveryTime(n)`は概ね`n + 1` tick周期で現在ブロックを再実行する。
- `unk == 999999999` のスクリプトブロックを原作終端番兵として扱う。
- 原作のブロック選択処理に合わせ、ブロック入場時に`Move`、`Force`、攻撃状態、ガード、`CamEffect`、`vF_Multi`、BURNER要求を既定値へ戻してから命令列を実行する。
- `WeaponAttack` / `WeaponAttack2` / `RunProc` / `RunProc2` の簡易弾または簡易エフェクト。`RunProc2`タイプ1/24/25/28/60、タイプ55、タイプ62サブタイプ3/4/7/10/11は、対応する原作テクスチャがあれば加算合成表示へ接続する。引数位置は`GUIDE4.txt`由来のため原作推定を含む。
- `ATTACK(power, down, force, forceY)`を4つの攻撃状態へ展開し、テスト用近距離判定ではダメージ、ダウン値、水平/垂直衝撃値を記録する。
- `LockBody...` / `LockArm...` はブロック内の照準要求として保持し、`bodyUpAimRoot` / `bodyDownAimRoot` / `arm1AimRoot` / `arm2AimRoot`が設定されている場合だけ、その表示Transformを対象へ向ける。ボーン割り当ては機体依存で、角度引数の厳密な意味も未確定なためUnity近似である。未設定時に機体ルートを回す旧フォールバックは廃止し、移動方位とカメラ基準を照準命令が書き換えないようにした。
- `BURNER(id, output)` と既存 `UI_SPT.LastSptData` の連携。IDは原作どおり0～19。既存MODの1引数形式は`output=1`として扱う。`BURNER2`は認識するが命令化しない。
- `CatchLastChara`を原作トークンとして認識する。旧Unity実装向けの`CatchChara`も互換別名として残す。
- `Snd` / `Voice`は型付きイベントを発行し、`TestPlayPresentationRuntime`の対応表にAudioClipがあれば機体位置から再生する。原作擬似コードで確認できる距離40の判定に合わせ、既定の最大距離は40。未登録素材は一度だけ警告する。短い固定効果音は開始時に音声データをプリロードする。
- `TestPlayPresentationRuntime.effects`へ`WeaponAttack:種別`や`RunProc2:種別:サブ種別`をキーとしてPrefabを登録すると、簡易球の代わりに対応エフェクトを生成する。
- `TestPlayPresentationRuntime.originalTextures`は原作の起動時ロード順と、別資料である`GUIDE4.txt`のスクリプトテクスチャIDを別フィールドで保持する。両者は同じ番号体系ではない。
- 移動は `idleAction` とは別の `moveAction` を使い、`@int[190]` を更新しながら方向キー押下中は既定ID `1` をループ再生する。原作の通常移動と同様に、Script内の`Move`が持つ水平速度を方向入力で作った移動方位へ割り当てるため、前進値だけを持つアニメーションでも後・左・右・斜めへ移動する。Scriptに水平`Move`がない場合だけ`inputMoveMagnitude`を使う。方向キー2回入力時のみステップアクションへ入る。
- 移動基準は切替可能。`useTargetRelativeMovement`がオンで、Sキーにより取得済みのロック対象が有効なら、毎tick更新する自機→対象方向を優先し、前=対象方向、後=対象から離れる方向、左右=対象方向に直交する方向とする。単に`target`がInspector設定されただけでは対象基準へ切り替わらない。対象基準が無効または利用不能で`useCameraRelativeMovement`がオンなら、`CamEffect`や視覚補間を適用する前の論理カメラ水平Forward/Rightを使う。両方をオフにすると、入力開始時の機体正面をキー解放まで保持する。通常・空中・上昇・ブースト・ステップ開始判定は同じ優先順位を使い、開始済みステップの移動方位だけは途中のカメラ移動で曲がらないよう固定する。
- 通常移動の旋回量は`inputTurnDegreesPerTick`で調整する。既定値は通常移動更新`FUN_004d5420`で確認した12度/tick。`0`にすると即時に入力方向へ向く。ブースト開始時の15度/tickと、その後の内積式旋回は別設定として維持する。
- `airborneFlag` がオンの場合、方向入力中は空中移動`airMoveAction`、既定ID `4`、中立時は空中停止`airIdleAction`、既定ID `8`をループ再生する。接地した時点で着地`landingAction`、既定ID `5`を再生し、完了後に通常アイドルID `0`へ戻る。旧シーンで着地IDがジャンプ開始と同じ`3`の場合は起動時に`5`へ正規化する。
- 上昇ID `7`は原作`FUN_004d5b60`→`FUN_004d5ec0`に合わせて毎tickエネルギー5を消費する。`c38=0～11`の12コールバックで上昇を終え、Z保持だけでANIブロックを無限ループさせない。Z保持中の方向入力へは最大14度/tickで姿勢を向ける。地上の短押しでもジャンプ開始ID `3`を完了後、ID `7`を最低5tick維持してから空中移動／停止へ移る。
- `Move`と`Force`はANI座標/tickのまま一度だけ積分し、最後に`aniUnitsToUnityScale`、既定`0.7`でUnity座標へ変換する。旧`moveScale=35` / `forceScale=35`は既存シーンとInspector互換のため残すが、原作準拠経路では使用しない。
- `Force`は原作`FUN_004cd840`どおり1 tick当たりの速度差分として積分する。上昇ID `7`と、上向きForceを持つ方向入力中の空中移動ID `4`は、Y速度をANI座標の`0.15`へ制限してから`0.04` / `0.02`を加える。`GvEnable`時は`0.013/tick`をY速度から引き、`-0.8/tick`を終端とする。その後、水平Force速度へ空中`0.95`または接地`0.9`を掛け、3軸速度へ`vF_Multi`を一度だけ適用する。`Force(...STOP...)`は対象軸の現在速度も停止する。
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
- 射撃、格闘、特殊武器は単発型。アニメーション/スクリプト終端まで進めてからIdleへ戻る。
- アクション切替時は現在表示中のパーツ姿勢から次アクションの姿勢へ短時間ブレンドする。`blendActionTransitions`、`actionTransitionSeconds`、`heldReleaseTransitionSeconds` で調整する。

## 原作寄せテストプレイカメラ

`UI_MechaClean.unity`のMain Cameraには、編集用`FreeCam`と`TestPlayCameraController`を併設する。テストプレイ開始時はカメラ位置・回転・FOV・`FreeCam`状態を保存して`FreeCam`を無効化し、終了時にすべて復元する。編集モードの右ドラッグ、中ドラッグ、ホイール操作は変更しない。

テストプレイ中はマウス入力を使わず、次の2状態を`LateUpdate`で追従する。投影行列生成へ渡される値から原作FOVは60度と確認できたため、`UI_MechaClean.unity`ではテストプレイ中だけFOV 60を適用し、終了時に編集カメラのFOVへ復元する。

- Sキーで取得済みのロック対象があり、`useLockTargetWhenAvailable`がオン: 通常は機体から対象への水平方向を後方基準にし、機体と対象の間を`targetFraming`で注視する。左右または斜め入力中は`followLateralMovementHeading`により配置基準を機体の移動方位へ寄せ、左入力では左、右入力では右へカメラYawが追従する。後退または後ろ斜め入力中は`followBackwardMovement`によりカメラを追加で後方・上方へ引き、対象を画面前方に保ったまま自機との間隔を広げる。対象距離に応じて`targetDistanceScale`の範囲でもカメラを引く。
- 対象がない、または同設定がオフ: 機体の正面を基準に`followDistance`、`followHeight`の後方視点へ戻す。

`playerPivotHeight`、`targetPivotHeight`、追従距離、高さ、補間速度は原作の厳密値が未確定なのでInspector調整項目とする。FOV 60だけは原作確定値。左右移動方位の反映量は`lateralMovementHeadingWeight`、ロック中の最大回り込み角は`maxLockedOrbitAngle`で調整する。対象基準の移動と画面方向の乖離を抑える既定値として、前者は`0.5`、後者は`60`度とする。後退時の引き量は`backwardDistanceBonus`、持ち上げ量は`backwardHeightBonus`で調整し、後ろ斜めは約0.707倍を適用する。後退方位をそのまま180度反映するとカメラが自機とロック対象の間へ回り込むため、ロック中は設定角以内に制限して対象をカメラ前方へ維持する。急な位置変更は`teleportSnapDistance`以上で即時追従する。`avoidObstacles`はUnity Colliderに対するSphereCastでカメラの壁抜けを抑える「Unity代替」であり、機体ルートとロック対象自身のColliderは無視する。

`CamEffect`は原作側でbyte状態へ保存されることまでは確認済みだが、値ごとの演出内容は未確定である。現在の`approximateCameraEffects`は、非0値を短い位置・回転揺れへ変換する明示的な「Unity代替」。厳密なID対応が判明した段階で置き換える。

## BURNER表示

`TestPlayController.useConeBurnerEffects` がオンの場合、テストプレイ中の `BURNER(id, output)` はParticleSystemを再生せず、`TestPlayBurnerCone` を対象ボーンへ生成して表示する。長さは `Script.spt` の `BURNERSET(id, frameName, scale, direction)` の `scale`、`burnerLengthMultiplier`、`output`を掛けた値になる。太さは長さに `burnerRadiusRatio` を掛ける。`direction` は `DOWN` をローカルZプラス方向、`UP` をローカルZマイナス方向として扱う。見た目の点火/消灯速度は `burnerFadeSpeed`、色は `burnerConeColor` で調整する。`output`を表示長とアルファへ割り当てる処理は、原作の共有出力値をUnityで可視化するための暫定的な「Unity代替」である。

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

ID 21～96は登録表に根拠がないため割り当てない。`burner.wav`、`engine.wav`はANIの`Snd`固定表とは別の持続音候補、`title_btn_decide.wav`、`title_btn_shift.wav`はタイトルUI用として保留し、誤ったIDへ推定割り当てしない。`Voice`は機体別OGGを参照する別経路なので今回のWAV表には含めない。

## 原作エフェクトテクスチャ

`Assets/IMG_TX`には原作の暗号化画像を置く。原本は編集せず、`Tools > WindomXP > Test Play > Rebuild Original IMG_TX Mappings`でテストプレイ用コピーを`Assets/Generated/TestPlay/OriginalTextures`へ生成する。復号後のDX9時代のPNGはUnity 6のアセットインポーターが直接拒否する場合があるため、ランタイムデコーダーで一度読み、生成コピーだけをPNGへ再エンコードする。

2026-08-12時点では、擬似コードで確認した起動時ロード表44件すべてをシーンへ設定済み。`beam2.png`と、`Beam.bmp`、`Beam2.bmp`、`laser2.bmp`、`Beam3.bmp`、`blueLight.bmp`を含むBMP素材も取り込めている。

番号には次の2種類があるため混同しない。

- `loadSequence`: `WindomXP_orig_decompiled.c` 21410～21453行で確認した起動時ロード順。原作確定。
- `scriptTextureId`: `解析資料/GUIDE4.txt`が示す`RunProc2`等のテクスチャ番号。資料由来で、ロード順とは途中から一致しない。

表示は`WindomXP/TestPlayOriginalEffect`シェーダーを使う。加算合成、カメラ正対、寿命フェードはDirectX9描画をUnityで再現するための「Unity代替」であり、個別エフェクトのUVアニメーション、色、拡大率、寿命の厳密値は今後の実機比較対象である。

## 回帰検証

Unityメニューの `Tools > WindomXP > Test Play > Run Runtime Verification` から原作確定仕様とUnity操作補正の検証を実行できます。現在は、セミコロン分割、コメント、内部変数範囲、複合代入、ネストIF、6比較演算子、命令走査中断、`ExecScriptEveryTime`周期、`ATTACK`の4引数、`BURNER`の出力値とID範囲、固定`Snd` ID表、原作テクスチャロード順と`GUIDE4.txt`由来IDの分離、60Hz tick、入力ラッチ、tick二度押し窓、`LockDist`取得／解除、照準Transform分離、FOV 60、テストカメラの開始/終了、左右旋回方向、後退時の距離・高さ・対象維持、後ろ斜め時の前方回り込み防止、対象／カメラ／入力開始時自機正面の基準切替、保持入力中の論理カメラ方位更新、方向コード1～9、Script前進量の方向変換、実際のRootMotion座標、入力方向と開始姿勢からのステップID 9～12選択、ローカル方向と進行方向を一致させる姿勢、前方0.3補正、16/61tick境界、ステップ4エネルギー消費と枯渇終了、接地ステップのID 6復帰、空中ステップ後のID 8入力保持、方向変更時の回復解除、ANIブロック長維持、空中アクションID、12度／14度／15度／内積式旋回、30/31tick境界、上昇12tick・空中停止11tickの再上昇境界、空中ブースト開始時のGenerator/5、Forceのtick速度積分・上昇上限・水平慣性、重力0.013／終端-0.8、vF適用順、空中移動Forceの蓄積上限と解放後の下降、エネルギー消費・接地回復を146項目で検査します。

## 移動デバッグ

`TestPlayController.logMotionDebug` をオンにすると、Unity Consoleへ移動系ログを出力する。`logMotionAssignments` がオンの場合は `Move` / `Force` / `vF_Multi` / `MoveLock` の代入時に `[TestPlay][MotionAssign]` が出る。`[TestPlay][RootMotion]` は `motionDebugIntervalTicks` ごと、または移動成分があるtickで出る。

確認する主な項目:

- `MotionAssign` が出ない場合: そのアクションのScriptで `Move` が実行されていない。
- `action=1` の歩きで `inputMove=True`: Scriptの水平`Move`量を方向入力の方位へ変換している。Scriptに水平移動量がない場合は`inputMoveMagnitude`を使う。
- ステップで `stepFallbackMove=True`: Script `Move` が取れないため、保存したステップ入力方向へ`stepFallbackMoveMagnitude`で動いている。`Move=(...)` があるステップでこれが出る場合はScriptブロックの取得または条件評価を確認する。
- `move=(0,0,0)` または `vF=0`: Script値または速度倍率が移動なしになっている。
- `moveLocked=True`: `MoveLock` により `Move` が無効化されている。
- `scriptedMove=(0,0,0)`: `Move` / `aniUnitsToUnityScale` / ルート向きのいずれかで移動量が0になっている。
- `delta` が0以外で見た目が動かない場合: `path` に表示された `robo.root` が想定している表示ルートか確認する。
- ステップ時は `step=True` と`action`、`tick`、`worldMove`を見て、アニメーションIDと独立して入力方向へ進んでいるか確認する。`stepDist`の右辺は原作準拠モードでは`0`で、左辺だけが診断用の累積移動量になる。
- `logGroundingDebug` をオンにすると `[TestPlay][Grounding]` が出る。`grounded=True` ならCharacterControllerが床Colliderに接地しており、`airborne=False` なら接地復帰可能な状態。`forceAir=True` は上昇/ブースト中の強制空中扱いを示し、ステップだけではオンにならない。

未対応命令は `TestPlayController.logUnhandledCommands` がオンならConsoleへ出ます。
