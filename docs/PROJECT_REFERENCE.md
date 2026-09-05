# WindomXP Mecha Editor Project Reference

## 概要

このUnityプロジェクトは、「Ultimate Knight ウィンダムXP」の機体MODデータを読み込み、パーツ階層、HOD姿勢、ANIアニメーション、Script.spt設定を編集・確認するためのツールです。上流は `MugenAttack/WindomXP-Mecha-Editor`、このリポジトリは日本語ローカライズと追加移植作業を含む派生版です。

2026-09-05の決定により対象Unityは `6000.6.0f1` です。保存済みProjectVersionと実行中Editorの一致・Editor回帰を確認し、Windows Player・通常操作・音声のユーザー確認によりP0移行受入は完了しました。根拠は [6000.6確認結果と次タスク](UNITY6000_6_ACCEPTANCE_AND_NEXT_TASKS.md)、最新環境・受入結果は [DEV_STATE](../DEV_STATE.md) を確認してください。レンダリングはURP設定を含み、TextMesh Pro、UGUI、AssimpNet、UniGif、RuntimeTransformHandleを利用しています。

Test Playは、将来的に原作をUnity上で再現した独立クローンゲームへ発展させる基盤として開発します。編集機能とデータ互換を継続し、既存Coreを再利用して起動・入力・機体/戦闘状態・資産読込を段階的に分離します。原作根拠の確度区分は維持します。

## 資料の入口と適用範囲

| 資料 | 用途 |
| --- | --- |
| [AGENTS](../AGENTS.md) / [DEV_STATE](../DEV_STATE.md) | 恒常ルール / 現在の目標・環境・直近結果・次タスク |
| [2026-09-05監査](PROJECT_AUDIT_2026-09-05.md) | 横断課題A01〜A16、独立化の実装順、変更種別の検証 |
| [検証ジョブAPI](VERIFICATION_JOB_API.md) | Runtime/HOD/Goldenの開始・状態取得・中止、結果JSON、P1-01/02の実装と受入境界 |
| [固定baseline・ANI棚卸し](BASELINE_AND_ANI_INVENTORY.md) | P1-03の比較前提・差分検出・明示的昇格、3機体の非同期棚卸しと中止 |
| [独立起動・セッション設計](STANDALONE_SESSION_DESIGN.md) | P1-04の依存・API・所有権・60Hz順序、P1-05の段階差分と受入条件（未実装） |
| [TEST_PLAY_MODE](TEST_PLAY_MODE.md) | 現行Test Play機能の入口 |
| [Phase 6C台帳](TEST_PLAY_PHASE6C_DIFFERENCE_LEDGER.md) | U番号別の原作差分・根拠・受入境界 |
| Phase 0〜6 / [旧DEV全文](history/DEV_STATE_2026-09-05_PRE_AUDIT.md) | 段階別仕様・過去の検証経過。現在の環境やタスク順はDEV_STATEを優先 |
| [原作挙動索引](WINDOMXP_ORIGINAL_BEHAVIOR_REFERENCE.md) | 原作一次関数への入口。履歴の行番号は対象関数名で再確認 |

## ディレクトリ構成

| パス | 役割 |
| --- | --- |
| `Assets/Scripts/` | ツール本体のC#スクリプト。ファイル形式、機体構造、UI、アニメーション、SPT/ANIスクリプト処理を含む。 |
| `Assets/Assimp/` | AssimpNet連携。Assimp型からUnity型への変換、テクスチャ読み込み補助。 |
| `Assets/Plugins/RuntimeTransformHandle/` | シーン上でパーツを移動/回転/拡縮するランタイムギズモ。 |
| `Assets/UniGif/` | GIFデコードライブラリ。 |
| `Assets/TextMesh Pro/` | TextMesh Pro標準リソース。 |
| `Assets/Materials/` | UI/プレビュー/グリッド/方向表示などの素材。 |
| `Assets/UI_MechaClean.unity` | メインUIシーンと見られるシーン。 |
| `Assets/Scenes/TestScene.unity` | テスト用シーン。 |
| `Packages/` | Unity Package Manager設定。 |
| `ProjectSettings/` | Unityプロジェクト設定。 |
| `Windom_Data/` | ゲーム側データ配置用ディレクトリ。既定UIは `Windom_Data\Robo` を参照する。 |

生成物である `Library/`, `Temp/`, `Logs/`, `obj/`, `.vs/` は通常のソース修正対象外です。Consoleや観測traceが根拠となる場合はLogs内の対象だけを読み、既存観測成果物は保持します。

## 外部依存

- Unity対象 `6000.6.0f1`（移行受入状況はDEV_STATE）
- TextMesh Pro（利用中。現行manifestに独立した `com.unity.textmeshpro` の直接指定はない）
- `com.unity.ugui`
- `com.unity.test-framework`
- `com.kyub.emojisearch`
- AssimpNet DLLとnative assimp DLL
- RuntimeTransformHandle
- UniGif

Assimp DLLは `Assets/Assimp/Plugins/` とリポジトリ直下にあります。モデル読み込み経路を変える場合は、Editor/Player双方でDLL解決を確認してください。

Packageの直接指定は `Packages/manifest.json`、解決結果は `packages-lock.json` とEditorで確認します。固定の旧バージョン一覧を正本にしません。旧6000.5監査で残っていたtetgen/timelineの解決Errorは、6000.6再確認では発生せず、両PackageのBuiltIn登録を確認しました。

## データモデル

### 機体フォルダ

通常は `Windom_Data\Robo\<機体名>` のようなフォルダ単位で扱います。`UI_SelectMech.folder` の既定値は `Windom_Data\Robo` です。フォルダ内には `.ani` / `.hod` / `Script.spt` / `.x` / `.png` などが入る想定です。

### 暗号化ファイル

`CypherTranscoder` が `.png` と `.x` の先頭4バイトシグネチャからXORキーを推定し、4バイト単位で変換します。

- 既定キー: `0x0B7E7759`
- 登録済みシグネチャ: `.png` = `1196314761`, `.x` = `543584120`
- `findCypher(path)` がフォルダ内ファイルからキーを検出します。
- `Transcode(path)` / `Transcode(byte[])` は復号にも再暗号化にも使える対称処理です。

`RoboStructure.buildStructure()` は機体フォルダ内を走査してキーを検出し、HODパーツ名と同名の実ファイルが存在する場合だけ `ImportModelEncrypted()` で復号してAssimpへ渡します。HODパーツ名はボーンや接続ポイントとしても使われるため、同名の `.x` が存在しないノードも正常な非描画ノードとして階層に残します。

### HOD形式

HODはパーツ階層とトランスフォームを持つ姿勢データです。

| クラス | 用途 |
| --- | --- |
| `hod1` | 旧形式。シグネチャ `HOD`。各パーツは4x4行列で姿勢を保持。 |
| `hod2v0` | 現行構造部。シグネチャ `HD2`、version `0`。パーツ名、階層、初期TRS、flag、未知Vector3を保持。 |
| `hod2v1` | アニメーションフレーム部。シグネチャ `HD2`、version `1`。パーツ名は `hod2v0` 側構造から引き継ぎ、フレームごとのTRSと未知Quaternion群を保持。 |

重要な注意点:

- パーツ階層は `treeDepth` と `childCount` の両方で表現されます。
- AN2または単体HODの機体読み込み時は `treeDepth` を第一の正本とします。深度列が有効なら、記録済み `childCount` の内容にかかわらず深度列から全パーツの `childCount` を再計算します。両方が有効でも異なる階層を表す場合は `treeDepth` を優先します。
- AN2または単体HODでは、`treeDepth` 自体が無効な場合だけ `childCount` を検証し、有効ならpreorderの子数列から `treeDepth` を一意に再構築します。
- AN2または単体HODの自動修復は構造HODと全アニメーションフレームのパーツ数・順序を事前検査し、両方へ同じ階層値をメモリ上で一括適用します。フレームの階層列が構造HODと異なる場合は、構造側とフレーム側の名前がすべて非空・一意で同じ集合を持つ場合だけ、パーツのTRSと未知Quaternion群を名前に付随させたまま構造順へ並べ替えます。
- 旧ANIは読込時にHOD階層の自動修復、階層列同期、フレームパーツ順の自動並べ替えを行わず、記録されている`treeDepth`／`childCount`／index順をそのまま維持します。不整合があっても表示と非構造編集は継続します。パーツ追加・削除を開始した時だけ、構造HODと全フレームのパーツ数およびindexごとの階層列が一致し、旧ANI末尾を安全に書き直せることを事前検査します。`treeDepth`または`childCount`の一方だけが有効ならその列を正本とする正規化確認を表示し、両方が有効だが異なる階層なら両方の変更プレビューを表示して正本の明示選択を要求します。両方が無効、フレーム不一致、未知末尾の場合は構造編集を抑止したままにします。
- AN2または単体HODで`treeDepth`と`childCount`の両方が無効、または構造HODと全フレームを安全に同期できない場合は、理由を表示して読み込みを中止します。全フレームの事前検査が完了するまで変更せず、元ファイルは明示的に保存するまで上書きしません。
- `RoboStructure` の編集前検証は、読み込み後に予期しない不整合が発生した場合の最終安全策として維持します。通常の読み込みでは自動修復後の有効な階層が渡されるため、構造編集は抑止されません。
- `hod2v0` はパーツ名を256バイト固定長ASCIIで保存します。
- `hod2v1.saveToBinary()` は先頭にファイル名長 `short` とShift-JISファイル名を書き、その後に `HD2` version `1` を書きます。
- 予約領域は `Seek(82)` / `Seek(83)` で飛ばされます。互換性維持のため削除しないでください。
- 旧 `hod1` は `Utils.GetPosition/GetRotation/GetScale()` で行列からTRSへ変換されます。

### ANI / AN2形式

`ani2` は3種類を読み込めます。

- `AN2`: 現行保存形式。先頭に構造用 `hod2v0`、続いてアニメーション数と各 `animation`。
- `ANI`: 旧形式。旧 `hod1` 構造と200個の基本アニメーションスロットを読み込む。基本200枠の後から`IKDATA`までが旧アニメーションブロックとして完全に解釈できる場合は、拡張スロットとして追加読込みする。既知の`IKDATA`は`IKDATA`署名、レコード数、各レコードの`int32 length + 13 byte payload`（`byte flag + Vector3 unk`）として検証し、先頭から同じindexの構造パーツ属性へ適用する。レコードがパーツ数より少ない場合、残りは旧HOD変換時の既定値`flag=1`／`unk=(1,1,1)`を維持する。
- `HOD`: 単体HODを1アニメーション1フレームとして読み込む互換経路。

`ani2.save()` は読込元形式を維持します。旧`ANI`由来なら旧`ANI`として、`AN2`／単体`HOD`由来なら`AN2`として保存します。旧`ANI`を明示的に変換する場合は`ani2.saveAsAn2(filename)`を使います。旧`ANI`では未編集の固定長文字列、スクリプト本文、HOD行列、未知の末尾データを原バイトのまま再利用し、編集された値だけを書き直します。旧フレームHODのパーツ名は構造HOD名と独立したラベルとしてindex順のまま保持し、構造パーツ名の変更には追従させません。

`UI_SelectMech`は機体ANIを本読込みする前に先頭3バイトだけを検査します。`ANI`シグネチャなら、旧ANIのまま読み込むかAN2へ変換して読み込むかを確認します。変換を選ぶと、元の旧ANIは上書きせず、同じフォルダの空いている`<元名>.an2`（既存時は`<元名>.converted*.an2`）へ書き出し、公開ローダーで再読込みできたAN2コピーを編集対象にします。変換時はAN2の通常方針どおり`treeDepth`を優先し、無効な場合だけ`childCount`へフォールバックして構造と全フレームを同期します。既存変換先および元ANIと同じパスは拒否します。旧ANI固有の未解析末尾はAN2に含まれないため確認文で明示し、変換失敗時は生成途中の新規コピーを除去して元ANIを変更せず読込みを中止します。変換しない場合は従来どおり旧ANIを無修復で読み込みます。`AN2`／`HOD`シグネチャではこの確認を表示しません。

旧`ANI`に表現できない状態（`squirrelInit`、フレームと構造のパーツ数不一致など）は、破損を避けるため保存前検証で拒否します。旧HODにはTRS行列しかなく、現行HOD2の`unk1`〜`unk3`回転制約は保存欄がないため、これらが現在の`rotation`と異なっていても保存を拒否せず、旧ANI保存時は`rotation`を行列へ書き出して制約値を破棄します。この場合は警告を出します。制約値も保持する必要がある場合は`AN2`として保存してください。上記の既知13バイト形式として末尾まで厳密に解析できた`IKDATA`では、構造パーツの数・順序・名前を変更した旧`ANI`保存を許可します。追加・削除は、正規化済み構造と全フレームを複製して編集・再検証した後にだけ一括反映するため、途中のフレーム不一致で構造だけが変更されることはありません。パーツ属性列が元の明示レコード列＋既定値接尾部と同値なら`IKDATA`原バイトを再利用し、挿入・削除・並べ替え・属性変更があれば現在の全構造パーツ分へ再構築します。未知長レコード、余剰バイト、不正件数などがある未解析末尾では、参照関係を推測せず構造変更を拒否し、明示的な`AN2`保存を選びます。旧ANI保存は同一ディレクトリの一時ファイルへ書き、構造・全フレーム・スクリプト・IKDATAを同期再読込検証してから保存先を置換します。ファイル名やスクリプト文字列は `USEncoder.ToEncoding.ToSJIS()` / `ToUnicode()` を通して扱います。

`animation` は以下を保持します。

- `name`
- `squirrelInit`
- `List<hod2v1> frames`
- `List<script> scripts`

`script` は時間/フレーム進行用パラメータとANI内スクリプト本文を保持します。

原作実行ファイルの擬似コードをテストプレイ実装の入口から参照する場合は、まず
[WINDOMXP_ORIGINAL_BEHAVIOR_REFERENCE.md](WINDOMXP_ORIGINAL_BEHAVIOR_REFERENCE.md) を確認してください。
関数索引、原作確定値とUnity代替の区別、`Script.ani` のロードから命令実行・移動積分までの正規化した処理順をまとめています。
オリジナル版実行ファイルの最新逆コンパイル解析は `docs/SCRIPT_ANI_ORIGINAL_DECOMPILED_ANALYSIS.md` に分離しています。原作は `ANI` の200スロット固定読み込みと `AN2` の可変アニメーション数を同じローダーで処理し、本文を専用パーサーで命令オブジェクトへ変換してから実行します。

### Script.spt

`UI_SPT.loadSPTField()` は `robo.folder\Script.spt` を `CypherTranscoder.Transcode()` で読み、`USEncoder` でUnicode文字列へ変換します。その後 `SptParser.Parse()` を実行し、`AniScriptRuntime.sptData` へ渡します。

`SptParser` が現在扱う主な要素:

- `BURNERSET(id, frameName, value, fourthToken)`（原作EXEは第4tokenを必須として読むが破棄。Unityは原文を保持し、既知方向だけ既存表示互換へ投影）
- `Name`, `NameEng`, `HP`, `Generator`, `Energy`, `Score`, `RestBody`, `LockDist`, `SubLockDist`

`SptRuntimeData.BindTransforms()` は機体ルート以下から `frameName` または `frameName.x` に一致するTransformを探します。`BuildBurnerEffects()` は該当ボーンにParticleSystemを生成・接続します。

## 実行フロー

### ツール設定と言語の復元

実行ディレクトリの`Settings.txt`は、1行目に機体フォルダ、2行目にUI言語を保持します。

```text
Windom_Data\Robo
Language=ja
```

- UI言語は`ja`または`en`。設定画面で言語を変更した時点で保存され、次回起動時はシステム言語や`PlayerPrefs`より先にこの値を使います。
- `Language=`行がない旧1行形式も読み込めます。言語変更時は1行目の機体フォルダを、フォルダ保存時は2行目の言語を保持します。
- 旧バージョンからの移行用として、`Settings.txt`に言語がない場合だけ既存の`PlayerPrefs`値、その次にシステム言語へフォールバックします。

### 機体読み込み

1. `UI_SelectMech` が `Windom_Data\Robo` 以下の機体フォルダを列挙する。
2. 選択されたファイルの先頭シグネチャを検査し、旧`ANI`ならAN2変換コピーを作るか確認する。変換しない場合は元の旧ANI、変換する場合は非上書きで作成・再読込みしたAN2コピーを以降の編集対象とする。
3. `ani2.load()` が `AN2` / `ANI` / `HOD` を判定し、`hod2v0 structure` と `animations` を作る。
4. AN2／単体HODでは全フレームのパーツ対応を事前検査し、必要なら一意名集合から構造順へ並べ替える。その後、HOD階層に不整合があれば、まず `treeDepth` を正として `childCount` を自動再計算し、`treeDepth` 自体が無効な場合だけ有効な `childCount` から `treeDepth` を再構築する。構造HODと全フレームへ同時適用できない場合は読み込みを中止する。旧ANIはこの読込時処理を行わず、構造編集開始時にだけ限定的な正規化確認へ進む。
5. `RoboStructure.buildStructure()` が `hod2v0.parts` からGameObject階層を作る。
6. 各パーツのモデルを暗号化解除し、AssimpでUnity Mesh/Materialへ変換する。
7. `.ani` 読み込み後、`Script.spt` があれば `UI_SPT` 経由でランタイムへ反映する。

### パーツ編集

`UI_EditParts` がパーツ一覧、選択状態、RuntimeTransformHandle、Transform入力欄を管理します。選択中パーツのTRS変更は `RoboStructure.updatePart()` などを通じて、現在選択中のアニメーション/ HODフレームへ反映されます。

パーツ追加/削除は `ani2.TryAddPart()` / `ani2.TryRemovePart()` が構造HODと全アニメーションフレームの編集コピーを作り、階層検証後に同時更新します。旧ANIの階層不整合は`LegacyAniStructureEditPlan`による正本確認・選択を通過した時だけこの経路へ進みます。階層整合性が壊れると保存データ全体に影響するため、この領域は特に慎重に扱ってください。

### アニメーション編集

`UI_EditAni` がアニメーション一覧、HODフレーム一覧、スクリプト欄、HOD追加/削除/リネーム、全HODへの値適用などを管理します。保存は最終的に `robo.ani.save()` を呼びます。

### アニメーション再生

通常の編集プレビューでは、`MechaAnimator` が `MA_Runner` を使い、Unityの `FixedUpdate()` 1回をANI 1 tickとしてスクリプトブロックとフレーム補間を進めます。既定の `Fixed Timestep` は0.02秒なので、この編集プレビューは50Hzです。一方、原作挙動比較用の `TestPlayController` はUnity物理周期から独立した60Hz（16.666ms）のシミュレーションtickを持ちます。両者は用途が異なるため、Project SettingsのFixed Timestepを変更して揃えないでください。

- 単一アニメーション再生: `run(animation, loop)`
- 2つ以上のアニメーションブレンド: `run(animation[], blend, loop)`
- フレーム間補間: `animation.interpolatePart()` と `InterpolateTransform()`
- スクリプト発火: スクリプトインデックスが進んだタイミングで `HandleScriptText()`

`executeAniScripts` がfalseの場合でも、`scriptInterpreter` によるシンボル収集は行われます。未知命令レポートは `Application.persistentDataPath/ani_script_symbols_report.txt` に出力されます。

### ANIスクリプト移植

`scriptInterpreter` はANI内の簡易スクリプトを行単位で解析し、関数呼び出し、変数代入、簡易IFを処理します。`AniScriptRuntime.Register()` が多くの `Scr_` 系命令を登録し、Unity側の状態・イベント・BURNER制御へ橋渡しします。

この層は移植途中です。`AniScriptRuntime` 内のTODO命令は受け皿として登録されているものが多く、ゲーム本体挙動の完全再現ではありません。実装時は、未知命令を無視して落ちないこと、既存シンボル収集を壊さないことを優先してください。

AN2のアニメーション別初期本文`squirrelInit`は、原作`FUN_0049cd50`／`FUN_0049d8e0`でscript index `-1`としてアニメーションレコード`+0x0C`の別vectorへ格納されます。通常action実行器`FUN_004b8250`とruntime tickが消費するのはtimed block列であり、初回action、同一action再入場、遷移、武器形態`+50` fallbackから初期vectorを実行する経路はありません。TestPlayはデータ保存と診断compileを維持しますがaction entryでは実行せず、init-only animationを実行可能actionに数えません。`MechaAnimator`の再生開始時1回実行は通常編集プレビューの既存Unity互換として分離し、原作runtimeの根拠にはしません。

原作比較用の`TestPlayController` / `TestPlayScriptVM`には、6比較演算子、0～199のint/float内部変数と複合代入、`ATTACK`の4値、2引数`BURNER(id, output)`、`ExecScriptEveryTime(n)`、`CatchLastChara`互換名を反映済みです。通常攻撃もX/Cの押下エッジ、銃／サーベル形態18/68、基本アクション+50、方向別格闘130/131/141/146/151、`SwordCancel`、`AttackDelay`、攻撃後6/8復帰まで原作擬似コードに合わせています。Phase 6Aでは`Logs/WindomXP`の原作EXEからGT-001を3回観測し、`observedFields`単位で同一ANI/SPTのUnity基準traceと比較済みです。待機directionとfloat32非正規化残留値をrawのまま保持して比較値だけ正規化した結果、意味のある最初の不一致は入力解放index tick 14の`scriptedVelocity`（原作は0.08を保持後0.8倍減衰、Unityは即時0）です。ただし原作値は`FUN_004cd840`入口の保持率適用前Move、Unity側は要求変位用の値です。一次擬似コードと原作アンカー観測で待機actionの保持率0.8、同tick内のMove乗算、解放tickの`0.08 × 0.8 = 0.064`を確認し、Phase 6BはCore反映、schema v1 Run 1～3の22tick一致、Runtime、Real-Mech、KD-03手動停止確認で受入済みです。Phase 6Cは全GTの順次観測ではなく、証拠レベルと挙動クラスによる代表観測方式です。GT-002／003には実ANI意味検証を追加し、action 7の単一入場、最終HODフレーム保持、解放後action 8を固定しました。GT-005／006には二度押し開始、ステップ16～61tick／ブースト31tick以降の解放境界、4／5 per tickとGenerator/5、実ANI水平移動のfocused verificationを追加しました。これらは全10 GTの2回完全一致で受入済みですが、実ANI再生結果は`RealAniObserved`であり原作EXE観測一致とは区別します。一次資料で一意な項目は追加EXE観測を必須にせず、共有Coreの未確定境界や実装後の実機差が残る場合だけ代表ケースを観測します。通常編集プレビュー側の`AniScriptRuntime`は移植途中です。Test Play側のtype 57はWEAPONPOINT前後tick掃引、ATTACK snapshot、`p8`寿命、第6引数`p2`のhit-stop、第7引数`p3`の対象別再命中intervalまでCore実装済みです。命中対象記録は`p3` tick後に失効し、`p3=0`は次tick連続命中を許可します。原作対象側複合形状と実機体同士の被弾Adapterは未確定です。追加実装では、推定コメントではなく解析資料の関数アドレスと実データを根拠にしてください。

GT-004の空中移動解放は、`FUN_004d68e0`のaction 8呼出引数`0x3f7d70a4`をANI Move保持率`0.99`として反映し、`c38 < 31`・移動ゲージ正・Y速度`< 0.05`のとき共通`FUN_004cd840`前でY速度へ`+0.012`する空中停止補助も実装済みです。実ANI focused verificationはaction 4の`Move Z=0.06`、raw Force Y `0.04`→`0.02`、解放後action 8のMove減衰、Force 0、31tick境界後の下降を固定します。実プレイでは、Unityがaction 4を全身物理actionとして扱ってrawの正Y Forceを毎tick積分した結果、移動入力を押し続けるだけでゲージを消費せず上昇を継続する不整合が確認されました。action 4では姿勢・水平Move・BURNER・非正Y Forceを維持しつつ、正Y Forceだけを物理上昇へ適用しないUnity Adapterへ修正しています。正の上昇力はゲージ消費境界を持つaction 7／22に限定します。raw ANI命令の観測は`RealAniObserved`、action 4の正Y Force除外は実プレイ不整合を解消するUnity Adapterであり、原作EXEの全tick観測一致を主張するものではありません。

GT-007の通常射撃は、`FUN_004d3e60`のX押下エッジとcooldown slot 0、`FUN_004b66e0`の`AttackDelay`、`FUN_004d8e30`の接地復帰をfocused verificationへ結合済みです。実機体traceではX押下1tickからaction 100へ1回だけ入り、実ANI 39tick、`AttackDelay(0,100)`、`ATTACK(100,200,0.4,0)`、`AttackFlag=2`、`RunProc2` type 1発射、cooldown 100→0、action 6→0を固定します。入力・action・cooldownは一次擬似コードに基づく`原作高確度`、命令値とtickは`RealAniObserved`です。type 1本体はU-005eの静的解析により専用Core化しました。表示は生成時から`p1`長のquadを置かず、実際の通過位置だけを結ぶworld-space camera-facing ribbonとするUnity Adapterです。原作対象側複合形状、DX9頂点・UV・fadeは未確定で、原作EXE全tick一致は主張しません。`OBS-GT007-20260902-GROUNDED-SHOT-RECOVERY`の原作画面手動観測では、静止action 100射撃後に着地姿勢を挟まず立ちへ移行しました。そのため論理action 100→6→0、action 6の34tick、script実行とtrace上の`Snd(2)` eventは保持し、接地action 100後のaction 6区間だけ立ちaction先頭HODを表示して、Audio Adapterで`Snd(2)=tyakuti.wav`の実再生だけを抑止します。移動中のaction 100、方向射撃、空中射撃、格闘、通常／ステップ着地には適用しません。2026-09-05の実機体Test Playで、立ち姿勢への直接復帰と同区間の無音をユーザーが目視・聴感受入しました。

GT-008／009の持替え・格闘連携は、`FUN_004d3280`／`FUN_004d40b0`のC押下と方向別選択、`FUN_004d9dd0`の誘導130→136、`FUN_004d9030`の入力queue・`SwordCancel`・接地復帰をfocused verificationへ結合済みです。GT-008はaction 18の21tick後にSword idle、方向8＋Cから130を6tick・5/tick消費し136へ遷移、6→0復帰を固定します。GT-009はC入力をqueueし、実ANIが`SwordCancel=132/133`を公開した次tickに131→132→133へ進み、各actionの攻撃プロファイルと6→0復帰を固定します。type 55／57の命令発火tickは`RealAniObserved`です。type 57の命中は別の一次静的解析とCore回帰で、WEAPONPOINT前後tick掃引、生成時ATTACK／AttackFlag snapshot、`p8`寿命、`p2` hit-stop、`p3`対象別再命中intervalまで確認します。単一TargetDummyではANIの長いMoveが対象を貫通しないよう、格闘130～155の適用水平移動だけをtarget球入口で制限します。raw Move、保持状態、action期間、ゲージ消費は変更しません。この接触制限と対象側球形状はUnity Adapterであり、原作の実機体同士の押し戻しや原作EXE全tick一致は主張しません。

U-007ではAttackFlag bit `0x01`／`0x04`／`0x08`／`0x10`／`0x20`／`0x40`、`ShildGuard`値1／2、type 1／11／57の前方dot閾値、`@int[157]`／`@int[158]`、type 57 hit-stopをCombat Coreとtraceへ接続済みです。U-007bでは`Scr_LaserReflect`が機体`+0xB68`の1 byteへ書き、type 11がこれをsigned charとして0～99 rollと厳密な`<`で比較し、AttackFlag bit `0x02`時だけ反射する経路を確定しました。`ShildGuard`の対応位置は`+0xB58`です。TestPlayはANI値の保持とCore判定まで反映しますが、実ANIのtype 11例がなく防御側も単一ダミーのため、実機体同士の反射は未接続です。

U-008a／bではSPT `ATTACKARMSET` ID 0～1、`GUNFILENAME`／`SWORDFILENAME` ID 0～19を原作パーサー範囲で保持し、機体階層Transformへ接続します。`LockArm1Target`／`LockArm2Target`は既存Inspector参照を優先し、未設定時だけSPT攻撃腕へfallbackします。`GUNFILENAME`／`SWORDFILENAME`は外部モデル読込指定ではありません。`FUN_00499d50`は`FUN_00571230`／`FUN_00571250`で読込済みHODノードを再帰名前検索し、両配列から外部loaderへ至る参照はありません。原作ANIコンパイラは`ChangeWeapon(GUN)`をtype 51、`ChangeWeapon(SWORD)`をtype 52へ変換し、`FUN_004b74a0`から`FUN_004f97f0`／`FUN_004f9930`へ入ります。type 51はGUN側表示・SWORD側非表示、type 52は逆です。`FUN_00572c50`はノード表示byteだけを子階層へ再帰設定するため、UnityもGameObjectを停止せず対象以下のRendererだけを切り替えます。実データではザクIIS型の`Output07.x`／`gun_dammy.x`が独立ファイルなしでHODノード名として使われることを確認しています。原作の腕回転制限式は未移植で、既存のUnity照準Adapterを維持します。

U-005aでは実データで使われる`RunProc` type 53／54を非戦闘Presentationへ分離します。原作type tableは53を`FUN_004f99f0`、54を`FUN_004f9ba0`へdispatchし、前者は`BB_WindLine`を7個（散布範囲±1.5、幅0.07、長さ3.0）、後者は`BB_WindRing2`を1個生成します。両handlerはproc引数objectを受け取らないため、ANI第3引数を表示パラメータに使いません。Unityは`RunProc:53`／`RunProc:54`のmapped prefabを優先し、未登録時はprocedural meshへfallbackします。原作乱数列、texture、blend、寿命は未確定なので、散布と寿命は決定的Unity Adapterです。GT-006ではtype 53=1回、type 54=4回、Visual 7／4件、両type由来Projectile 0件を固定します。

U-005bでは`RunProc2` type 55を`BB_SwordBeam`の非戦闘表示として実装します。`FUN_004f9d00`は第3引数をWEAPONPOINT ID、第4／第7引数を100で割った目標／初期長、第5／第6引数を主・線texture、第11引数を管理slot置換、第12引数を寿命tickとして`FUN_00502b00`へ渡します。寿命0は`999999999`へ置換され、`FUN_00497cd0`で有限寿命だけを1/tick減算し、現在長を0.2/tickで目標長まで伸ばします。線幅0.075も初期化で固定されます。実機体ではWEAPONPOINTのUP／DOWNラベルにかかわらずHODボーン姿勢へ外向き基準が含まれるため、追加180度回転を掛けずローカルZ+へ伸ばします。Primaryは縦長textureのローカルYを両交差面とも刀身方向へ合わせます。Beam_Lineは常時の刀身層ではなく、前後フレームで根元または先端が動いた時だけ両姿勢を結ぶ2面ブラーとして表示し、静止時は非表示にします。Projectile／ATTACK判定は生成しません。ブラー閾値・厚み、原作頂点・UV・blendは実機確認に基づくUnity Adapterです。

U-005dでは、実ANIの`BURNER(id, output)`集合を推進音の開始／停止境界として使います。非稼働→稼働時に`burner.wav`を非ループ開始音、`burner_f15.wav`を同時開始する持続ループとして別AudioSourceで再生します。出力消失時は開始音を自然終了させ、持続音だけを0.05秒fade後に停止します。これは明示的なUnity Adapterであり、固定`Snd(5)=burner_f15.wav`は独立して維持し、未確認の`engine.wav`は接続しません。

U-009aでは原作SPT parser `0x004A6D6F`以降と`+0x26AC`／`+0x26B4`の全参照を逆引きし、`BURNERSET`第4文字列が一時bufferへ読まれた後に保存・比較・参照されず破棄されることを確定しました。現行実SPT 31件は`UP` 19件／`DOWN` 12件ですが、scale 0／正値やANI点火IDとの一意な相関はありません。Unity parserは任意tokenを受理して原文保持し、未知tokenを無回転にfallbackします。既存MODの`FORWARD`等はUnity表示互換として維持し、原作挙動とは区別します。

U-009bでは`Scr_BunerOut`実行器`FUN_004aa140`と通常機体`FUN_00499d50`／`FUN_004ba1c0`／`FUN_004d39d0`を接続し、ANI `output`の格納先`+0xB7C`と表示要求byte`+0xB74`、SPT第3floatの設定値配列`+0xE38`を分離しました。通常描画は要求byteでgateし、SPT第3floatを変更せず渡すため、ANI outputを表示倍率へ使わず、`BURNER(id, 0)`も表示要求を解除しません。U-009cで速度倍率側のfactory／constructorを追い、`FUN_0049c790`が`CShip::vftable`を設定する別クラス経路だと確定しました。現行データは`Windom_Data/Robo`だけなので、この式は明示的な`CShip` Coreとして未接続を維持します。`BB_Burner`／`BB_BurnerBall`はfield layoutとの一致から更新関数を高確度対応付けし、11回目／5回目の終了、`-(value/2)/15`引数、0.95行列倍率を純粋Coreとfocused回帰へ固定しました。既存Inspector倍率、持続Cone、推進音の正output境界はUnity Adapterとして維持します。

U-005cでは`RunProc2` type 60を非戦闘`LZ_ThunderEffect`として実装します。type tableは`FUN_004fa690`へdispatchし、生成`FUN_00489b40`、初期化`FUN_0048c470`はWEAPONPOINT行列を生成時に複製し、`p0/100`初期幅、`p1`表示長、`p2/100`前進量/tick、`p3` texture、`p4/100`散布半径、`p5`有効tickを設定します。更新`FUN_0047c380`は移動、tick減算、初期幅の1/10減衰の順で処理し、幅0.001未満で終了します。実ANI 25機体の走査では959件・17機体・10形式を確認しました。Unityは生成位置から独立移動するtexture交差quadを使い、Projectile／ATTACK判定を生成しません。原作共有乱数の列、DirectX9頂点・UV・blendは未確定のUnity Adapterです。

U-005eでは`RunProc2` type 1を原作`LZ_Beam`として専用Coreへ接続します。`FUN_004e8310`はWEAPONPOINT行列とATTACK値を生成時に複製し、p0をScript.spt `Energy`側（`+0xD28`、上限`+0xD2C`）から消費し、p1 trail点数、p2/100前進距離/tick、p3/100表示幅、p4旋回補正、p5 texture、p6 low-byte modeを初期化`FUN_004600c0`へ渡します。Generator移動ゲージは消費しません。20度以内のtargetへ初期照準し、距離100未満では`(1-distance/100)*0.4*(1+p4/100)`度/tickを最大旋回角にします。`FUN_00460200`は前進後に旋回し、固定300 active tickを減算します。衝突`FUN_0045e610`はtrail線分と対象側複合形状を照合し、同一targetを1回だけ命中させ、beamは命中後も継続します。Unityはtrailと重複抑止をCoreへ移しますが、対象は単一球Adapter、DX9 trail描画とactive終了後fadeは表示Adapterです。Runtime 584 assertions、GT-001～GT-010各2回完全一致、Console Error 0を確認済みです。

U-005hでは`RunProc2` type 62 subtype 2を非戦闘`BB_WindRing`として専用Coreへ接続します。`FUN_004fa830`は第3引数のWEAPONPOINTからtranslationだけを生成時に複製し、p4～p11を読まず、`FUN_0048bef0`へsize 1.0、draw mode 1、`WindRing.png`、alpha 255、growth 0.3、signed alpha delta -24を固定で渡します。更新`FUN_00491af0`はsize加算後にalphaを減らし、更新10はsize 4.0／alpha 15で存続、更新11は候補alpha -9で終了します。owner／ATTACK／衝突処理を持たず、RunProc type 54の派生`BB_WindRing2`とは別です。Unityは位置snapshotの非billboard quadを使い、DX9頂点・UV・blendと座標系への面対応を表示Adapterとして分離します。

U-005iでは`RunProc2` type 62 subtype 3を非戦闘の`BB_Burner`＋`BB_BurnerBall`へ置換します。`FUN_004fa830`はp4／100をprimary size、p5／100をlength、p6をvariantとして読み、0ならtexture ID 8／9、1なら43／44を選びます。両objectはWEAPONPOINT行列へ追従します。primary vtable `0x005B74F0`の更新`FUN_00491580`は11更新目、ball vtable `0x005B7BA8`の更新`FUN_004902c0`はXY基底を0.95倍しながら5更新目に独立終了します。Unityは二枚のquadで表現し、ATTACK／Projectileを生成しません。DX9頂点・UV・blendは表示Adapter境界です。

GT-010のロック／射撃旋回は、ロック対象から見た前後左右へ通常射撃100を100／103／102／101、飛行射撃106を106／103／108／107へ解決します。左右actionが実データにない場合はaction 103、103もない場合は元の100／106へ戻します。実機体focused verificationでは後方targetをtick 1でロックし、tick 4の左＋Xでもtarget相対後方としてaction 103をpose/script 103・channel 0／1で49tick実行します。実ANIの`ShotTurnAng=20`はaction 103中だけ各tick -20度、tick 53からaction 6を34tick、tick 87でidleへ復帰します。action期間と命令値は`RealAniObserved`であり、Camera構図・注視点・追従感はUnity代替のままです。

## 主要スクリプト一覧

| ファイル | 役割 |
| --- | --- |
| `RoboStructure.cs` | 機体階層構築、モデル読み込み、姿勢反映、HOD読み書き。 |
| `ani2.cs` | `.an2` / 旧 `.ani` / 単体 `.hod` のロード、`.an2` 保存、パーツ追加削除。 |
| `animation.cs` | animation単位のHODフレーム、スクリプト、補間、読み書き。 |
| `hod1.cs` | 旧HODの読み書きと現行HODへの変換。 |
| `hod2v0.cs` | 現行構造HODの読み書き。 |
| `hod2v1.cs` | 現行アニメーションフレームHODの読み書き。 |
| `HodHierarchyValidator.cs` | `treeDepth`、親候補、ルート数、`childCount` の整合性検査。 |
| `HodHierarchyRepair.cs` | 階層列一致による旧ANIのindex維持、一意名によるフレーム順修復、`treeDepth`優先・無効時のみ`childCount`へフォールバックする階層修復、旧ANI構造編集開始時の限定正規化計画、全フレーム事前検査とメモリ上の一括適用。 |
| `HodHierarchyPrune.cs` | 親へ接続不能な孤立パーツ範囲の判定と一括除外。現行の自動読み込み経路では使用しない補助実装。 |
| `HodHierarchyManualRepair.cs` | 親指定によるpreorder再構築。現行の自動読み込み経路では使用しない補助実装。 |
| `HodHierarchyPriorityVerification.cs` | 階層正本の優先順位、旧ANI名差の許容、一意名順序修復、旧ANI構造編集時の一意／曖昧／修復不能判定、追加削除の非破壊失敗、IKDATA、ELS_QT実ANIの正規化保存再読込と修復後AN2の公開ロード回帰。 |
| `CypherTranscoder.cs` | 暗号化 `.x` / `.png` のXORキー検出と変換。 |
| `USEncoder.cs` | Shift-JIS/Unicode変換テーブル。大きいが重要。 |
| `UI_SelectMech.cs` | 機体フォルダ選択、旧ANIの事前変換確認、非上書きAN2変換・再読込み、通常ロード開始。 |
| `UI_EditParts.cs` | パーツ選択、Transform編集、追加削除UI。 |
| `UI_EditAni.cs` | アニメーション/HODフレーム/スクリプト編集UI。 |
| `UI_SPT.cs` | `Script.spt` 読み書きとランタイム反映。 |
| `SptParser.cs` | `Script.spt` の解析とBURNERエフェクト初期化。 |
| `MechaAnimator.cs` | アニメーション再生、補間、ANIスクリプト発火。 |
| `AniScriptRuntime.cs` | ANI命令をUnity状態・イベント・ParticleSystemへ接続。 |
| `scriptInterpret.cs` | ANI内スクリプトの簡易パーサとシンボル収集。 |
| `FreeCam.cs` | プレビュー用カメラ操作。 |
| `UI_InputBox.cs`, `UI_MsgBox.cs` | ダイアログUI。 |

## 変更時のリスク

### 高リスク

- `hod*`, `ani2`, `animation` の読み書き順変更
- `USEncoder` の変換テーブルや文字コード処理変更
- `RoboStructure.buildStructure()` の階層構築ロジック変更
- `ani2.addPart()` / `removePart()` の階層更新ロジック変更
- Unityシーン上のpublic参照名やGameObject構成変更

### 中リスク

- `MechaAnimator` のフレーム進行、補間、スクリプト発火タイミング変更
- `SptParser` の正規表現や方向定義変更
- `CypherTranscoder` の端数バイト処理、キー検出処理変更
- Assimp post-process flags変更

### 低リスク

- UI文言の調整
- ログ文言の改善
- 未知ANI命令のスタブ追加
- 既存挙動を変えないドキュメント整備

## 推奨検証チェックリスト

以下は編集機能の受入一覧です。毎回全項目を要求するものではなく、変更範囲は [監査の検証表](PROJECT_AUDIT_2026-09-05.md#verification) に従います。

1. 対象Unity `6000.6.0f1` でプロジェクトを開き、Package解決とコンパイルを確認する。移行受入前の6000.5結果は区別する。
2. `Assets/UI_MechaClean.unity` を開き、主要Inspector参照がMissingになっていない。
3. `Windom_Data\Robo` の既存機体を選択して `.ani` をロードできる。
4. パーツ一覧がHOD階層どおりに表示され、モデルが表示される。
5. パーツを移動/回転/拡縮し、選択中HODへ反映される。
6. アニメーションプレビューが進行し、フレーム補間で破綻しない。
7. `Script.spt` 読み込み時に `BURNERSET` が解析され、対象ボーンがあればエフェクトが生成される。
8. 保存した `.an2` を再読み込みして、パーツ数、階層、主要アニメーション、スクリプト文字列が維持される。
9. `Tools > WindomXP > HOD > Run Hierarchy Priority Verification` が成功し、Console Errorがない。

## 既知の注意点

- 一部ファイル名やクラス名に古い命名・タイポがありますが、Unityのシリアライズ参照を壊す可能性があるため、安易に修正しないでください。
- `scriptInterpreter` は簡易実装であり、完全なSquirrel互換ではありません。
- `AniScriptRuntime` はオリジナル実行ファイル由来の推定コメントを含みます。確定していない挙動はTODOまたは推定として扱ってください。
- `RoboStructure.ImportModelEncrypted()` は復号後の `.x` 文字列に変換処理をかけてからAssimpへ渡します。文字列変換を変更する場合は複数機体で確認してください。
- `debug.txt` やANIシンボルレポートなど、実行時に生成/更新される補助ファイルがあります。
