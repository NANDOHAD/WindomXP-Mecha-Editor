# WindomXP Mecha Editor Agent Rules

このリポジトリは、DirectX9 時代のゲーム「Ultimate Knight ウィンダムXP」の機体MOD編集をUnity上で行うためのツールです。作業前に必ず [docs/PROJECT_REFERENCE.md](docs/PROJECT_REFERENCE.md) を確認してください。

## 基本方針

- 対象Unityバージョンは `6000.5.0f1`。旧 `2021.3.45f2` から更新済みであり、現在の `ProjectSettings/ProjectVersion.txt` を正とする。不用意にProjectVersion、URP、Packagesを更新・ダウングレードしない。
- 既存のゲームデータ互換性を最優先する。`.ani` / `.an2` / `.hod` / `Script.spt` / 暗号化 `.x` / `.png` の読み書き仕様を変更する場合は、必ず後方互換性と保存結果を確認する。
- `Assets/Scripts/USEncoder.cs` のShift-JIS変換を避けて通常の `Encoding.GetEncoding(932)` へ置き換えない。Unity/実行環境差分を避けるため、既存の `USEncoder.ToEncoding` を使う。
- `Assets/Scripts.bak*` や `*.bak` は調査用に存在する。ユーザーが明示しない限り削除・同期・復元しない。
- `Library/`, `Temp/`, `Logs/`, `obj/`, `.vs/` は生成物。編集やレビュー対象にしない。
- ユーザーの未コミット変更を巻き戻さない。Unityシーンやアセットは差分が大きくなりやすいので、必要な時だけ触る。

## コード作業ルール

- 主要スクリプトはグローバル名前空間にある。既存コードに合わせ、局所的な修正では大きな名前空間移動やファイル分割をしない。
- Unity Inspector 参照が多いため、publicフィールド名・クラス名・MonoBehaviour名の変更は破壊的変更として扱う。
- バイナリフォーマット処理では、読み書き順、固定長文字列、予約領域の `Seek`、シグネチャ、バージョン番号を維持する。
- パーツ階層は `treeDepth` と `childCount` の整合性で表現される。パーツ追加・削除時は `hod2v0.structure` と全アニメーションフレームの `hod2v1.parts` を同時に更新する。
- モデル読み込みは `CypherTranscoder` で復号してからAssimpで `.x` とテクスチャを扱う。暗号キー検出のため、機体フォルダ内ファイルの走査順や登録シグネチャの変更に注意する。
- ANIスクリプト実行はまだ移植途上。`AniScriptRuntime` のTODO命令は、推定実装と確定実装をコメントで区別し、未知命令を壊さない。
- UIテキストは日本語ローカライズ済み部分がある。新規UI文言は原則日本語で、既存表現に合わせる。

## 検証ルール

- C#のみの変更でもUnityコンパイルを前提に確認する。可能ならUnity Editorで開き、Consoleエラーがないことを確認する。
- ファイルフォーマット変更時は、最低限「既存 `.ani` / `.hod` 読み込み」「保存」「再読み込み」「パーツ数と階層」「主要アニメーションプレビュー」を確認する。
- `Script.spt` / `BURNER` 関連変更時は、`UI_SPT.loadSPTField()` から `SptParser.Parse()`、`AniScriptRuntime.sptData`、`BURNER(id)` の点火/停止まで確認する。
- シーン編集を行った場合は `Assets/UI_MechaClean.unity` の参照切れ、Inspector未設定、Prefab参照切れを確認する。

## 主要ファイル

- `Assets/Scripts/RoboStructure.cs`: 機体パーツ階層、Assimpモデル読み込み、HOD保存/読み込みの中心。
- `Assets/Scripts/ani2.cs`: `.an2` / 旧 `.ani` / 単体 `.hod` のロードと `.an2` 保存。
- `Assets/Scripts/hod1.cs`, `hod2v0.cs`, `hod2v1.cs`: HODバイナリ構造。
- `Assets/Scripts/animation.cs`: アニメーション内のフレーム列とスクリプトブロック。
- `Assets/Scripts/MechaAnimator.cs`: アニメーション再生、補間、ANIスクリプト発火。
- `Assets/Scripts/scriptInterpret.cs`: ANI内スクリプトの簡易パーサ/シンボル収集。
- `Assets/Scripts/AniScriptRuntime.cs`: ANI命令をUnity状態・イベントへ橋渡しする移植レイヤー。
- `Assets/Scripts/SptParser.cs`, `UI_SPT.cs`: `Script.spt` 解析とランタイム反映。
- `Assets/Scripts/UI_SelectMech.cs`, `UI_EditAni.cs`, `UI_EditParts.cs`: 主要UI操作。
