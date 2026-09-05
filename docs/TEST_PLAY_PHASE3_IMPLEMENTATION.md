# テストプレイ再設計 Phase 3 実装記録

## 結果

Phase 3では、`TestPlayController`と`TestPlayProjectile`に分散していた攻撃受付、クールダウン、格闘連携、攻撃プロファイル、命中結果、発射体tickをCombat Coreへ分離しました。

Projectile、Transform、TargetDummy、Effect生成、Audio生成はUnity Adapter側に残しています。`.ani` / `.an2` / `.hod` / `Script.spt`の形式、`TestPlayController`の既存publicフィールド、Inspector参照、UnityEvent、シーン構造は変更していません。

Phase 0の再設計契約は [TEST_PLAY_PHASE0_REDESIGN_SPEC.md](TEST_PLAY_PHASE0_REDESIGN_SPEC.md)、Phase 1のANI Coreは [TEST_PLAY_PHASE1_IMPLEMENTATION.md](TEST_PLAY_PHASE1_IMPLEMENTATION.md)、Phase 2のAction／Motion／Locomotion Coreは [TEST_PLAY_PHASE2_IMPLEMENTATION.md](TEST_PLAY_PHASE2_IMPLEMENTATION.md) を参照してください。

## 追加・変更した責務

| ファイル | 責務 |
| --- | --- |
| `TestPlayCombatCore.cs` | 5スロットのAttackDelay、X/Cアクション選択、攻撃受付、格闘連携、攻撃完了、攻撃payload、命中結果、発射体1 tickを計算する。 |
| `TestPlayCombatTrace.cs` | 攻撃プロファイル、クールダウン、連携状態、spawn／hit／cancelイベントをPhase 2 JSONへ追加する。 |
| `TestPlayController.cs` | Scriptと入力をCore入力へ変換し、返された遷移、TargetDummy、Projectile、RuntimeEventへ接続するFacade／Adapter。 |
| `TestPlayProjectile.cs` | `Time.deltaTime`を60Hzへ蓄積し、Coreの発射体tick結果をTransformとTargetDummyへ適用する。 |
| `TestPlayPhase3Verification.cs` | 受付境界、格闘連携、攻撃値の根拠区分、命中結果、60Hz発射体、trace、Controller接続を検証する。 |

## Combat Coreの確定範囲

### ATTACKプロファイル

原作パーサーが生成する4値をそのまま保持します。

```text
ATTACK(power, down, force, forceY)
```

- `power`: 威力
- `down`: ダウン値
- `force`: 水平衝撃値
- `forceY`: 垂直衝撃値

`ATTACK`単独では命中しません。type 57の格闘判定または発射体衝突時に、その時点の4値をsnapshotして適用します。

### 根拠区分

攻撃payloadとhit resultは値の出所を保持します。

| 値 | 意味 |
| --- | --- |
| `OriginalScriptProfile` | ANIの`ATTACK` / `AttackPow`等から得た値。 |
| `UnityFallback` | `ATTACK`威力がない場合の既存タイプ別推定値。原作確定値とは扱わない。 |
| `Unknown` | プロファイル値を伴わない状態遷移イベント。 |

タイプ1/3/4/9/19/24/25/28/55/57の既存推定ダメージは削除せず、`UnityFallback`として可視化します。type 57のボーン別形状、持続tick、多段ヒット条件は未確定のままで、距離判定はUnity近似です。

## 攻撃受付と連携

Coreへ移した境界は次のとおりです。

- AttackDelayは0～4の5スロットだけを受け付け、60Hz tickごとに1減算する。
- Xは銃形態で100、剣形態では利用可能なら持替え68、ブースト中は5tickを超えてから106を選ぶ。
- Cは銃形態では利用可能なら持替え18。剣形態では前7/8/9→130、中立→131、左4→141、右6→146、後2→151を選ぶ。
- C再押下は保留し、現在blockの`SwordCancel`先が利用可能になったtickで遷移する。
- 誘導格闘130は6tick目以降、対象距離3.5未満またはGenerator枯渇で136へ進む。
- 格闘は15tickを超えた後、ブーストまたは方向ステップへキャンセルできる。
- 攻撃完了は接地なら6、空中なら8へ戻る。誘導130の完了だけは136を優先する。

入力ラッチ、Generator消費、アニメーション変更そのものはControllerに残し、Coreは副作用のないdecisionを返します。

## 命中結果とUnity Adapter

Combat Coreは次を含むpayload／hit resultを返します。

- source command
- damage / down
- horizontal / vertical impact force
- projectile speed / homing
- value source

type 57はControllerが対象との距離をUnity近似で確認し、Coreが水平方向を正規化して衝撃ベクトルを作り、TargetDummyへ適用します。

発射体は生成時にpayloadをsnapshotします。後続ANI blockで攻撃値が変わっても、飛行中の発射体へ遡及しません。TargetDummy、GameObject生成、原作テクスチャ表示、Prefab置換は引き続きAdapterです。

## 発射体60Hz化

旧`TestPlayProjectile.Update()`はrender frameの`Time.deltaTime`で直接移動・寿命・命中を更新していました。Phase 3では時間を蓄積し、1/60秒単位で`TestPlayCombatCore.TickProjectile()`を呼びます。

1 tickの順序は次です。

1. 寿命を1/60秒進め、期限到達なら終了する。
2. 生存対象がありhoming指定なら、tick上限角度で回転する。
3. 前方へ`speed / 60`進める。
4. 移動後位置と自弾＋対象半径で命中を判定する。
5. hit resultをTargetDummyへ適用し、Controllerへhitイベントを通知する。

低フレーム時は`maximumCatchUpTicks`まで処理し、未処理時間を捨てず次frameへ保持します。Transformと対象位置の読取はUnity Adapterに残るため、完全な原作物理形状の再現ではありません。

## Phase 3 tick trace

`TestPlayController.CapturePhase3TickTrace()`はPhase 2のaction／motion JSONを維持し、同じroot recordへ`combat`を追加します。

- ATTACK power / down / force / forceY / AttackFlag / SwordCancel
- AttackDelay 5スロット
- attack sequence / melee approach / combo pending
- profile change / cooldown set / attack start
- combo queue / SwordCancel / approach followup / locomotion cancel / finish
- projectile spawn / hit
- source、damage、down、force、value source

Projectile hitがControllerのtick外で発生した場合はpending eventとして保持し、次の60Hz tick traceへ取り込みます。浮動小数はInvariant Cultureのラウンドトリップ表現です。

## 互換性

- `TestPlayAttackProfile`と既存publicフィールドを維持した。
- `ResolveShotInputAction`、`ResolveMeleeInputAction`、`TryUpdateNormalAttackSequence`、`SpawnProjectile`等の既存helper名を維持した。
- `TestPlayProjectile`の既存public設定を維持し、owner、根拠区分、tick設定だけを追加した。
- `RuntimeEventRaised`と既存`AttackProfileChanged` / `WeaponSpawned` / `AttackHit`を維持した。
- Scene、Prefab、ProjectSettings、Package構成は変更していない。

## 検証

Unityメニュー`Tools > WindomXP > Test Play > Run Runtime Verification`へPhase 3検証を統合しました。

結果は327 assertions成功、Console Error 0です。Phase 2完了時の278件をすべて維持し、次の49件を追加しています。

- ATTACK 4値とAttackDelay 5スロット
- X/C形態・方向・ブースト境界
- SwordCancel保留、誘導130→136、16tick目の移動キャンセル
- 接地6／空中8への攻撃完了
- Script profileとUnity fallbackの区別
- 発射体payload snapshotと衝撃ベクトル
- 発射体の寿命、移動、homing、命中の60Hz演算
- Phase 2 traceへのcombat record追加
- Controller command AdapterからCore／traceへの接続

## Phase 4への入口

Phase 4では、Snd、Voice、BURNER、RunProc、原作テクスチャ、CamEffectをPresentation EventとUnity表示Adapterへ整理します。Combat Coreが確定したspawn／hit tickと原作IDをPresentation traceへ渡し、表示寿命や補間がゲーム結果へ影響しない境界を固定します。
