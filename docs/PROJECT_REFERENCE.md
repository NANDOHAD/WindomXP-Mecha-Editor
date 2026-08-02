# WindomXP Mecha Editor Project Reference

## 概要

このUnityプロジェクトは、「Ultimate Knight ウィンダムXP」の機体MODデータを読み込み、パーツ階層、HOD姿勢、ANIアニメーション、Script.spt設定を編集・確認するためのツールです。上流は `MugenAttack/WindomXP-Mecha-Editor`、このリポジトリは日本語ローカライズと追加移植作業を含む派生版です。

現在のUnityバージョンは `6000.5.0f1` です。旧 `2021.3.45f2` からUnity 6系へ更新済みであり、`ProjectSettings/ProjectVersion.txt` の値を現行環境として扱います。レンダリングはURP設定が含まれており、TextMesh Pro、UGUI、AssimpNet、UniGif、RuntimeTransformHandleを利用しています。

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

生成物である `Library/`, `Temp/`, `Logs/`, `obj/`, `.vs/` は通常の作業対象外です。

## 外部依存

- Unity `6000.5.0f1`
- `com.unity.textmeshpro` `3.0.9`
- `com.unity.ugui`
- `com.unity.test-framework`
- `com.kyub.emojisearch`
- AssimpNet DLLとnative assimp DLL
- RuntimeTransformHandle
- UniGif

Assimp DLLは `Assets/Assimp/Plugins/` とリポジトリ直下にあります。モデル読み込み経路を変える場合は、Editor/Player双方でDLL解決を確認してください。

## データモデル

### 機体フォルダ

通常は `Windom_Data\Robo\<機体名>` のようなフォルダ単位で扱います。`UI_SelectMech.folder` の既定値は `Windom_Data\Robo` です。フォルダ内には `.ani` / `.hod` / `Script.spt` / `.x` / `.png` などが入る想定です。

### 暗号化ファイル

`CypherTranscoder` が `.png` と `.x` の先頭4バイトシグネチャからXORキーを推定し、4バイト単位で変換します。

- 既定キー: `0x0B7E7759`
- 登録済みシグネチャ: `.png` = `1196314761`, `.x` = `543584120`
- `findCypher(path)` がフォルダ内ファイルからキーを検出します。
- `Transcode(path)` / `Transcode(byte[])` は復号にも再暗号化にも使える対称処理です。

`RoboStructure.buildStructure()` は機体フォルダ内を走査してキーを検出し、各パーツ名に対応するモデルファイルを `ImportModelEncrypted()` で復号してAssimpへ渡します。

### HOD形式

HODはパーツ階層とトランスフォームを持つ姿勢データです。

| クラス | 用途 |
| --- | --- |
| `hod1` | 旧形式。シグネチャ `HOD`。各パーツは4x4行列で姿勢を保持。 |
| `hod2v0` | 現行構造部。シグネチャ `HD2`、version `0`。パーツ名、階層、初期TRS、flag、未知Vector3を保持。 |
| `hod2v1` | アニメーションフレーム部。シグネチャ `HD2`、version `1`。パーツ名は `hod2v0` 側構造から引き継ぎ、フレームごとのTRSと未知Quaternion群を保持。 |

重要な注意点:

- パーツ階層は `treeDepth` と `childCount` の両方で表現されます。
- `HodHierarchyValidator` が両値の整合性を検査します。不整合時も読み込みと表示は継続しますが、警告を表示し、階層破損を防ぐためパーツの追加・削除を抑止します。
- `hod2v0` はパーツ名を256バイト固定長ASCIIで保存します。
- `hod2v1.saveToBinary()` は先頭にファイル名長 `short` とShift-JISファイル名を書き、その後に `HD2` version `1` を書きます。
- 予約領域は `Seek(82)` / `Seek(83)` で飛ばされます。互換性維持のため削除しないでください。
- 旧 `hod1` は `Utils.GetPosition/GetRotation/GetScale()` で行列からTRSへ変換されます。

### ANI / AN2形式

`ani2` は3種類を読み込めます。

- `AN2`: 現行保存形式。先頭に構造用 `hod2v0`、続いてアニメーション数と各 `animation`。
- `ANI`: 旧形式。旧 `hod1` 構造を読み、200個の旧アニメーションスロットを読み込む。
- `HOD`: 単体HODを1アニメーション1フレームとして読み込む互換経路。

保存は `ani2.save()` が `AN2` として行います。ファイル名やスクリプト文字列は `USEncoder.ToEncoding.ToSJIS()` / `ToUnicode()` を通して扱います。

`animation` は以下を保持します。

- `name`
- `squirrelInit`
- `List<hod2v1> frames`
- `List<script> scripts`

`script` は時間/フレーム進行用パラメータとANI内スクリプト本文を保持します。

### Script.spt

`UI_SPT.loadSPTField()` は `robo.folder\Script.spt` を `CypherTranscoder.Transcode()` で読み、`USEncoder` でUnicode文字列へ変換します。その後 `SptParser.Parse()` を実行し、`AniScriptRuntime.sptData` へ渡します。

`SptParser` が現在扱う主な要素:

- `BURNERSET(id, frameName, scale, direction)`
- `Name`, `NameEng`, `HP`, `Generator`, `Energy`, `LockDist`

`SptRuntimeData.BindTransforms()` は機体ルート以下から `frameName` または `frameName.x` に一致するTransformを探します。`BuildBurnerEffects()` は該当ボーンにParticleSystemを生成・接続します。

## 実行フロー

### 機体読み込み

1. `UI_SelectMech` が `Windom_Data\Robo` 以下の機体フォルダを列挙する。
2. 選択されたフォルダから `.ani` を読み込む。
3. `ani2.load()` が `AN2` / `ANI` / `HOD` を判定し、`hod2v0 structure` と `animations` を作る。
4. `RoboStructure.buildStructure()` が `hod2v0.parts` からGameObject階層を作る。
5. 各パーツのモデルを暗号化解除し、AssimpでUnity Mesh/Materialへ変換する。
6. `.ani` 読み込み後、`Script.spt` があれば `UI_SPT` 経由でランタイムへ反映する。

### パーツ編集

`UI_EditParts` がパーツ一覧、選択状態、RuntimeTransformHandle、Transform入力欄を管理します。選択中パーツのTRS変更は `RoboStructure.updatePart()` などを通じて、現在選択中のアニメーション/ HODフレームへ反映されます。

パーツ追加/削除は `ani2.addPart()` / `ani2.removePart()` が構造HODと全アニメーションフレームを同時更新します。階層整合性が壊れると保存データ全体に影響するため、この領域は特に慎重に扱ってください。

### アニメーション編集

`UI_EditAni` がアニメーション一覧、HODフレーム一覧、スクリプト欄、HOD追加/削除/リネーム、全HODへの値適用などを管理します。保存は最終的に `robo.ani.save()` を呼びます。

### アニメーション再生

`MechaAnimator` は `MA_Runner` を使い、Unityの `FixedUpdate()` 1回をANI 1 tickとしてスクリプトブロックとフレーム補間を進めます。既定の `Fixed Timestep` は0.02秒のため、ゲーム互換プレビューの標準は50Hzです。

- 単一アニメーション再生: `run(animation, loop)`
- 2つ以上のアニメーションブレンド: `run(animation[], blend, loop)`
- フレーム間補間: `animation.interpolatePart()` と `InterpolateTransform()`
- スクリプト発火: スクリプトインデックスが進んだタイミングで `HandleScriptText()`

`executeAniScripts` がfalseの場合でも、`scriptInterpreter` によるシンボル収集は行われます。未知命令レポートは `Application.persistentDataPath/ani_script_symbols_report.txt` に出力されます。

### ANIスクリプト移植

`scriptInterpreter` はANI内の簡易スクリプトを行単位で解析し、関数呼び出し、変数代入、簡易IFを処理します。`AniScriptRuntime.Register()` が多くの `Scr_` 系命令を登録し、Unity側の状態・イベント・BURNER制御へ橋渡しします。

この層は移植途中です。`AniScriptRuntime` 内のTODO命令は受け皿として登録されているものが多く、ゲーム本体挙動の完全再現ではありません。実装時は、未知命令を無視して落ちないこと、既存シンボル収集を壊さないことを優先してください。

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
| `CypherTranscoder.cs` | 暗号化 `.x` / `.png` のXORキー検出と変換。 |
| `USEncoder.cs` | Shift-JIS/Unicode変換テーブル。大きいが重要。 |
| `UI_SelectMech.cs` | 機体フォルダ選択とロード開始。 |
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

1. Unity `6000.5.0f1` でプロジェクトを開き、Consoleのコンパイルエラーがない。
2. `Assets/UI_MechaClean.unity` を開き、主要Inspector参照がMissingになっていない。
3. `Windom_Data\Robo` の既存機体を選択して `.ani` をロードできる。
4. パーツ一覧がHOD階層どおりに表示され、モデルが表示される。
5. パーツを移動/回転/拡縮し、選択中HODへ反映される。
6. アニメーションプレビューが進行し、フレーム補間で破綻しない。
7. `Script.spt` 読み込み時に `BURNERSET` が解析され、対象ボーンがあればエフェクトが生成される。
8. 保存した `.an2` を再読み込みして、パーツ数、階層、主要アニメーション、スクリプト文字列が維持される。

## 既知の注意点

- 一部ファイル名やクラス名に古い命名・タイポがありますが、Unityのシリアライズ参照を壊す可能性があるため、安易に修正しないでください。
- `scriptInterpreter` は簡易実装であり、完全なSquirrel互換ではありません。
- `AniScriptRuntime` はオリジナル実行ファイル由来の推定コメントを含みます。確定していない挙動はTODOまたは推定として扱ってください。
- `RoboStructure.ImportModelEncrypted()` は復号後の `.x` 文字列に変換処理をかけてからAssimpへ渡します。文字列変換を変更する場合は複数機体で確認してください。
- `debug.txt` やANIシンボルレポートなど、実行時に生成/更新される補助ファイルがあります。
