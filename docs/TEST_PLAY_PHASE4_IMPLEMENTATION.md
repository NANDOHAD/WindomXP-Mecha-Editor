# テストプレイ再設計 Phase 4 実装記録

## 結果

Phase 4では、`Snd`、`Voice`、`BURNER`、`RunProc` / `RunProc2`、原作テクスチャ、`CamEffect`を、原作命令を表すPresentation EventとUnity表示Adapterへ分離しました。

ANI / AN2 / HOD / SPTの読み書き、既存public Inspectorフィールド、MonoBehaviour名、UnityEvent、シーン／Prefab参照は変更していません。従来の`RuntimeEventRaised`も外部互換用に残し、音声とカメラの既存ランタイムだけを新しい型付きイベントへ接続しました。

## Presentation Core

`TestPlayPresentationCore.cs`はUnity表示物を生成せず、次を不変イベントへ変換します。

- `Snd`: 固定効果音ID、元引数、発生tick。
- `Voice`: 機体別シンボル、元引数、発生tick。
- `BURNER`: ID 0～19、出力値、元引数。1引数MOD互換の`output=1`は診断付き。
- `BURNER2`: 原作パーサーが命令オブジェクトを生成しない診断イベント。
- `RunProc` / `RunProc2`: order、proc type、type 62 subtype、全引数。
- 原作テクスチャ: script texture IDとファイル名を別フィールドで保持。
- `CamEffect`: 元の値を保持し、Unityカメラ揺れは別Adapterとして記録。

イベントは次の3軸を混同しません。

| 軸 | 例 |
| --- | --- |
| 原作側の識別情報 | `originalId`、`procType`、`subtype`、`textureId`、`arguments` |
| 根拠 | `OriginalExecutableConfirmed`、`OriginalDataObserved`、`IncompleteInference`、`Unknown` |
| Unity表示Adapter | `AudioClip`、`BurnerCone`、`ParticleSystem`、`MappedPrefab`、`OriginalTextureQuad`、`PrimitiveFallback`、`CameraShakeApproximation`、`CombatOnly` |

これにより、例えば「RunProc2 type 57を原作データで確認している」ことと、「UnityではCombat Coreだけが処理し表示Adapterを生成しない」ことを同時に記録できます。

## Controller FacadeとAdapter

`TestPlayController`は既存APIを維持したまま`PresentationEventRaised`を追加しました。

- 音声: `TestPlayPresentationRuntime`が型付き`Sound` / `Voice`だけを受け、対応AudioClipを再生する。
- カメラ: `TestPlayCameraController`が型付き`CameraEffect`だけを受け、Inspector調整可能な揺れへ変換する。
- BURNER: 原作要求はイベントへ記録し、SPT対応がある場合だけConeまたは既存ParticleSystemへ渡す。
- Proc: 命令イベントを先に確定し、type 57はCombat Core、テクスチャ／Prefab／簡易Primitiveは表示Adapterとして別イベントへ記録する。
- 原作テクスチャ: スクリプトID／ファイル名と、Quad生成成功・未対応を分離する。

表示寿命、ビルボード、フェード、カメラ補間、BURNERの見た目はrender frame側に残します。これらは攻撃payload、命中tick、damage、down、forceを変更しません。

## 原作確定と未確定の境界

確定扱い:

- `Snd(id)`の命令ハンドラーと既知固定ID。
- `BURNER(id, output)`の2引数、ID 0～19。
- `BURNER2`を認識しても命令オブジェクトを生成しない原作パーサー挙動。
- `CamEffect`値が原作状態へ保存されること。

原作実データで観測済みだが、全パラメータ式は未確定:

- `Voice(name)`。
- `RunProc2` type 55のサーベル表示、type 57の格闘判定。
- `RunProc*`のテクスチャIDとtype 62 subtype。

Unity代替のまま残すもの:

- AudioSourceの距離減衰、同時発音。
- BURNER outputからCone長・太さ・アルファへの式。
- Quadの加算合成、billboard、寿命、fade。
- 未登録表示のPrefab／Sphere fallback。
- `CamEffect`非0値から短い位置・回転揺れへの式。

したがって`U-005`、`U-009`、`U-010`は解決済みにしていません。型付きイベントと診断により、追加解析結果を表示側だけ差し替えられる境界を作った段階です。

## Phase 4 tick trace

`CapturePhase4TickTrace()`はPhase 3 JSONへ`presentation.events`を追加します。各イベントに次を保存します。

- tick、論理action、script index。
- command、source、symbol、resource name。
- original ID、proc type、subtype、texture ID、output。
- 根拠区分と選択Adapter。
- 数値／シンボル／STOPの型を保持した全引数。
- 互換fallback、未対応、原作式未確定の診断。

浮動小数はInvariant Cultureのround-trip形式、文字列はJSON用に決定的にescapeします。Unity Instance ID、`Time.deltaTime`、GameObject名の自動suffixは記録しません。

## 回帰検証

`Assets/Editor/TestPlayPhase4Verification.cs`で46 assertionsを追加しました。

- Snd／VoiceのID・シンボル・引数snapshot。
- BURNER 0／19境界、不正ID、引数不足、1引数互換。
- BURNER2診断。
- RunProc2 type 55／57／62とsubtype。
- texture／visual Adapterと原作IDの分離。
- Phase 4 traceの決定性、escape、根拠区分。
- Controllerから型付きイベント、旧RuntimeEvent、Phase 4 traceへの接続。

統合検証はPhase 3までの327件を維持し、合計373 assertionsを対象とします。

## 次フェーズへの入口

次フェーズではGT-001～GT-010のgolden traceを実機体データへ接続し、同一入力列の複数回実行でAction、Motion、Combat、Presentationの決定性を確認します。原作観測traceを取得できない項目はUnity基準値と明記し、未確定のRunProc、BURNER、CamEffect式を完成扱いにはしません。
