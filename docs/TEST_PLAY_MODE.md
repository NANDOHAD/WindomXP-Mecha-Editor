# テストプレイモード実装メモ

このモードは既存の `MechaAnimator` / `AniScriptRuntime` / 編集UIには手を入れず、`Assets/Scripts/TestPlay/` 以下の新規スクリプトだけで動く実験用ランタイムです。

## 追加したコンポーネント

| ファイル | 役割 |
| --- | --- |
| `TestPlayController.cs` | 入力、ANI tick、HOD補間、Script命令、簡易武器生成をまとめるテストプレイ本体。 |
| `TestPlayScriptVM.cs` | `IF`、関数呼び出し、代入、`@int/@float`、`STOP` を扱う簡易Script VM。 |
| `TestPlayStateTable.cs` | ゲーム本体の `@int[]` / `@float[]` 風の状態テーブル。 |
| `TestPlayScriptValue.cs` | Script引数の数値、シンボル、`STOP` 表現。 |
| `TestPlayTargetDummy.cs` | ロック対象/被弾対象のダミー。 |
| `TestPlayProjectile.cs` | `WeaponAttack` / `RunProc2` 用の簡易弾。 |

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
| 矢印キー | `@int[190]` 方向入力。通常移動は `moveAction`、既定ID `1` を押下中ループ再生する。 |
| 同じ方向の矢印キーを2回入力 | 各方向のステップ。上/下/左/右で `11` / `12` / `9` / `10` を再生する。ステップ中は `airborneFlag` をオンにし、Script内の `Move` から算出した規定移動量に達したら空中アイドルID `4` へ移行する。Script距離が取れない場合はステップIDの方向からフォールバック移動を生成する。 |
| `Z` | 押下中のみ上昇/ブースト。上昇開始は `riseStartAction`、既定ID `3`。上昇は `riseAction`、既定ID `7`。 |
| 空中で `Z` を押し直す | `@int[191]` ブースト入力。`boostAction`、既定ID `22` を押下中のみ再生する。 |
| `X` | `@int[192]` 射撃。 |
| `C` | `@int[193]` 格闘。 |
| `V` | `@int[194]` 防御。 |
| `S` | `@int[195]` ターゲット変更。 |
| `A` / `D` / `F` | `@int[196]` - `@int[198]` サブ攻撃。 |

## 現在の実装範囲

- `Move` / `Force` / `STOP`。`Move=(...)` / `Force=(...)` の代入形式と `Move(...)` / `Force(...)` の関数形式を扱う。
- `@int[]` / `@float[]` の代入、`+=`、`-=`、`*=`、`/=`
- `IF` / `ELSE` / `ENDIF`
- `ChangeAnime` / `GoScriptIndex` / `GoPoseIndex` / `AnimeLoop`
- `WeaponAttack` / `WeaponAttack2` / `RunProc` / `RunProc2` の簡易弾または簡易エフェクト
- `ATTACK` の近距離ヒット判定
- `LockBody...` / `LockArm...` の簡易ターゲット方向旋回
- `BURNER(id)` と既存 `UI_SPT.LastSptData` の連携。標準では `BURNERSET` のボーン、方向、scaleに従うコーン状メッシュを表示する。
- 移動は `idleAction` とは別の `moveAction` を使い、`@int[190]` を更新しながら方向キー押下中は既定ID `1` をループ再生する。Script内で `Move` が設定されない通常移動では、`@int[190]` の方向から `inputMoveMagnitude` 分のローカル移動を生成する。方向キー2回入力時のみステップアクションへ入る。
- `airborneFlag` がオンで移動/ブースト/防御などの入力がない場合は、空中アイドルとして `airMoveAction`、既定ID `4` をループ再生する。空中アイドル中に `airborneFlag` がオフになった時点で `landingAction`、既定ID `3` を再生し、完了後に通常アイドルの `idleAction`、既定ID `0` へ戻る。
- ブースト中およびステップ中は `airborneFlag` を必ずオンにする。ブースト状態は `Z` 押下中のみ維持し、既定では `boostAction` ID `22` を再生する。
- `Z` が押されていない状態で `boostAction` がANIスクリプト等から再要求された場合は、空中フラグONなら空中アイドルID `4` へリダイレクトする。
- ステップは入力継続に依存しない有限アクションとして扱い、ANIスクリプト側がループしてもScript内の `Move` 指定から算出した規定移動量に達した時点で空中アイドルID `4` へ移行する。`Move=(0,STOP,0.42f)` のような代入形式と `f` サフィックス付き数値も距離計算対象。ステップ中はScriptブロックの異常に大きいtick長を `maxStepScriptTicksPerBlock`、既定 `5` tickに丸めて、各フレームの `Move` を順に反映する。`Move` が取得できない場合は、ステップIDの前後左右から `stepFallbackMoveMagnitude` のローカル移動を生成し、`stepFallbackDistance` 到達で空中アイドルID `4` へ移行する。
- 上昇、ブースト、防御は押下維持型。上昇/ブーストは `Z` 押下中のみ再生し、`Z` を離した後も `airborneFlag` がオンなら空中アイドルへ移行する。
- `useColliderGrounding` がオンの場合、テストプレイ開始時に機体ルートへランタイム用 `CharacterController` を追加または取得し、Script由来の移動に重力を加えて `CharacterController.Move()` で移動する。接地先はUnity Colliderで、接地中は `airborneFlag` をオフ、非接地中はオンにする。上昇/ブースト/ステップ中は従来仕様どおり一時的に空中扱いを強制し、終了後にColliderの接地状態で着地へ遷移する。
- `characterControllerRadius` / `characterControllerHeight` / `characterControllerCenter` は機体サイズに合わせて調整する。足元が床に埋まる、または接地しない場合は、まず `characterControllerCenter.y` と `characterControllerHeight` を見直す。
- 射撃、格闘、特殊武器は単発型。アニメーション/スクリプト終端まで進めてからIdleへ戻る。
- アクション切替時は現在表示中のパーツ姿勢から次アクションの姿勢へ短時間ブレンドする。`blendActionTransitions`、`actionTransitionSeconds`、`heldReleaseTransitionSeconds` で調整する。

## BURNER表示

`TestPlayController.useConeBurnerEffects` がオンの場合、テストプレイ中の `BURNER(id)` はParticleSystemを再生せず、`TestPlayBurnerCone` を対象ボーンへ生成して表示する。長さは `Script.spt` の `BURNERSET(id, frameName, scale, direction)` の `scale` に `burnerLengthMultiplier` を掛けた値、太さは長さに `burnerRadiusRatio` を掛けた値になる。`direction` は `DOWN` をローカルZプラス方向、`UP` をローカルZマイナス方向として扱う。見た目の点火/消灯速度は `burnerFadeSpeed`、色は `burnerConeColor` で調整する。

## 移動デバッグ

`TestPlayController.logMotionDebug` をオンにすると、Unity Consoleへ移動系ログを出力する。`logMotionAssignments` がオンの場合は `Move` / `Force` / `vF_Multi` / `MoveLock` の代入時に `[TestPlay][MotionAssign]` が出る。`[TestPlay][RootMotion]` は `motionDebugIntervalTicks` ごと、または移動成分があるtickで出る。

確認する主な項目:

- `MotionAssign` が出ない場合: そのアクションのScriptで `Move` が実行されていない。
- `action=1` の歩きで `inputMove=True`: Script `Move` ではなく、方向入力から生成した移動量で動いている。
- ステップで `stepFallbackMove=True`: Script `Move` が取れないため、ステップIDから推定した方向のフォールバック移動で動いている。`Move=(...)` があるステップでこれが出る場合はScriptブロックの取得または条件評価を確認する。
- `move=(0,0,0)` または `vF=0`: Script値または速度倍率が移動なしになっている。
- `moveLocked=True`: `MoveLock` により `Move` が無効化されている。
- `scriptedMove=(0,0,0)`: `Move` / `moveScale` / `vF_Multi` / ルート向きのいずれかで移動量が0になっている。
- `delta` が0以外で見た目が動かない場合: `path` に表示された `robo.root` が想定している表示ルートか確認する。
- ステップ時は `step=True` と `stepDist=現在値/規定値` を見て、規定移動量またはフォールバック距離まで進んでいるか確認する。
- `logGroundingDebug` をオンにすると `[TestPlay][Grounding]` が出る。`grounded=True` ならCharacterControllerが床Colliderに接地しており、`airborne=False` なら着地遷移可能な状態。`forceAir=True` は上昇/ブースト/ステップ中の強制空中扱いを示す。

未対応命令は `TestPlayController.logUnhandledCommands` がオンならConsoleへ出ます。
