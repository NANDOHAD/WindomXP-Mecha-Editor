# テストプレイ再設計 Phase 2 実装記録

## 結果

Phase 2では、`TestPlayController`に混在していたアクションID解決、Force速度積分、移動状態の分類と終了条件を、シーン非依存のCoreへ分離しました。

既存のpublicフィールド、MonoBehaviour名、`StartTestPlay()` / `StopTestPlay()` / `ChangeAnimation()`、Inspector参照、UnityEvent、シーン構造は維持しています。既存のprivate helperも回帰検証が参照しているためFacadeとして残し、内部からCoreへ委譲します。

Phase 0の根拠区分と再設計契約は [TEST_PLAY_PHASE0_REDESIGN_SPEC.md](TEST_PLAY_PHASE0_REDESIGN_SPEC.md)、Phase 1のANI中間表現とtrack schedulerは [TEST_PLAY_PHASE1_IMPLEMENTATION.md](TEST_PLAY_PHASE1_IMPLEMENTATION.md) を参照してください。

## 追加・変更した責務

| ファイル | 責務 |
| --- | --- |
| `TestPlayActionCore.cs` | 要求アクション、論理アクション、表示ANI、スクリプトANI、主／副チャンネルを分離して決定する。 |
| `TestPlayMotionCore.cs` | 上昇速度制限、Force、重力、水平減衰、`vF_Multi`、Unity変位合成を決定的な順序で計算する。 |
| `TestPlayLocomotionCore.cs` | 待機、歩行、ジャンプ開始、上昇、空中移動／停止、着地、ステップ、ブースト、防御を分類し、移動系遷移条件を判定する。 |
| `TestPlayPhase2TickTrace.cs` | アクション3種、移動状態、Force前後速度、変位、適用された制限をCulture非依存のJSON 1行へ出力する。 |
| `TestPlayController.cs` | 入力取得、アニメーション／シーン操作を維持するFacade。上記Coreをランタイム正本として呼び出す。 |
| `TestPlayPhase2Verification.cs` | アクション分離、演算順、移動状態と境界、trace決定性を検証する。 |

## Action Core

`ChangeAnimation()`は次のIDを別々に保持します。

| ID | 意味 | 例: サーベル歩行 |
| --- | --- | ---: |
| `requestedActionId` | 入力／Scriptが要求したID | 1 |
| `logicalActionId` | ゲーム状態遷移に使う基本ID | 1 |
| `poseActionId` | HOD姿勢を表示するANI | 51 |
| `scriptActionId` | Script blockと時間を供給するANI | 1または51 |

サーベル形態で0～49が要求され、利用可能な+50 ANIがあれば表示だけを+50側へ切り替えます。+50側にScriptがなければ基本ID側をScript sourceにし、+50側にScriptがあればそちらを優先します。100以上の射撃／格闘IDは+50変換しません。

基本論理アクション0～49は主チャンネル0、続いて副チャンネル1を使います。格闘130～155は主チャンネル0のみ、その他の非基本アクションは従来どおり副チャンネル1を初期値とします。

## Motion Core

1 tickの演算順を次で固定しました。

1. 上昇ID 7または空中移動ID 4なら、現在のY速度を上限0.15へ制限する。
2. `Force`を1 tick当たりの速度差分として加える。
3. `GvEnable`ならY速度へ0.013/tickの重力を適用し、-0.8を終端とする。
4. 水平X/Zへ空中0.95または接地0.9の保持率を掛ける。
5. 3軸へ`vF_Multi`を1回だけ掛ける。
6. ANI `Move`とForce速度を加え、`aniUnitsToUnityScale`を最後に1回だけ掛けてUnity変位を得る。

Coreは`Time.deltaTime`、Transform、CharacterController、入力APIを参照しません。同じ入力構造体から同じ中間値と最終速度を返します。Colliderによる実変位の制限と接地状態の反映はUnity Adapterとして引き続き`TestPlayController`が担当します。

## Locomotion Core

論理状態を次へ正規化しました。

```text
Idle -> Walk
Idle -> JumpStart -> Rise -> AirMove / AirIdle -> Landing -> Idle
AirMove / AirIdle / Rise -> Boost -> AirMove / AirIdle
Idle / Walk -> Step -> StepLanding -> Idle
                    \-> AirIdle  (既に空中の場合)
```

Coreへ移した終了・分岐条件は次のとおりです。

- Z解放は上昇5tick目以降に有効。Z保持とエネルギーが続く限り12tickを超えても上昇を維持する。
- 空中停止からの再上昇は11tick目から受け付ける。
- ブーストは30tickまで全解放を無視し、31tick以降にZと方向入力を両方離すと終了する。
- 方向入力があれば空中移動4、なければ空中停止8を選ぶ。
- ステップ終了時、既に空中なら8、接地分岐なら6を選ぶ。

攻撃受付、エネルギー消費、入力ラッチ、Transform操作はこのPhaseで移していません。これらは副作用を伴うためControllerのAdapter境界に残し、Coreが返す状態判断から既存処理を呼びます。

## tickトレースのPhase 2範囲

`TestPlayController.CapturePhase2TickTrace()`は次を決定的な順序で出力します。

- tick番号
- requested／logical／pose／script action ID
- weapon mode、主／副ANI channel
- locomotion state
- Force適用前速度、Force、適用後速度
- Script由来速度、要求Unity変位
- 上昇速度制限／重力の適用有無
- 水平保持率、`vF_Multi`

Unity Instance ID、現在Culture、`Time.deltaTime`は含みません。CharacterControllerが実際に許可した変位、リソース差分、combat、presentationは後続Phaseで同じtick契約へ追加します。

## 互換性

- `.ani` / `.an2` / `.hod` / `Script.spt`の読み書き処理は変更していない。
- `TestPlayController`の既存publicフィールド名とメソッド名は変更していない。
- シーン、Prefab、ProjectSettings、Package構成は変更していない。
- `riseMaximumTicks`は旧Inspector互換用として残し、原作準拠終了条件には使わない。
- `ResolveActionForWeaponMode`、`IntegrateOriginalForceVelocity`、上昇／ブースト／ステップ判定helperはFacadeとして残し、既存回帰検証を継続する。

## 検証

Unityメニュー`Tools > WindomXP > Test Play > Run Runtime Verification`へPhase 2検証を統合しました。

結果は278 assertions成功、Console Error 0です。Phase 1完了時の236件をすべて維持し、次の42件を追加しています。

- 銃／サーベルのlogical／pose／script action分離
- +50 Scriptの優先と基本ANIへのフォールバック
- 主／副チャンネルと格闘チャンネル
- 上昇制限→Force→重力→減衰→倍率→変位の演算順
- 空中／接地保持率、終端速度、入力解放後の重力遷移
- 待機、歩行、ジャンプ、空中、着地、ステップ、ブースト、防御の分類
- 上昇5tick、再上昇11tick、ブースト31tickの境界
- Phase 2 tick traceの決定性

## Phase 3への入口

Phase 3では、副作用を持つ戦闘処理をCombat CoreとUnity Adapterへ分けます。攻撃プロファイル、受付窓、クールダウン、格闘連携、命中結果をCoreの入力／出力として固定し、Projectile、Collider、Effect、Audio生成はAdapter側へ残します。Phase 2のaction selectionとmotion traceを戦闘traceへ接続し、同じtickでのアクション・移動・攻撃結果を比較可能にします。
