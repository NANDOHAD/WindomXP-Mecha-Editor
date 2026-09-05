# WindomXP Mecha Editor Agent Rules

このリポジトリは、DirectX9 時代のゲーム「Ultimate Knight ウィンダムXP」の機体MOD編集ツールです。2026-09-05の決定により、Test Playは将来、原作をUnity上で再現した独立クローンゲームへ発展させるための基盤として開発します。編集機能と既存データ互換性を維持し、ゲーム実行の入口・状態・入出力を段階的に分離します。

作業開始時は最初に [DEV_STATE.md](DEV_STATE.md) の現在の目標・環境・次のタスクを確認し、その後 [docs/PROJECT_REFERENCE.md](docs/PROJECT_REFERENCE.md) から対象資料だけを参照してください。進捗・決定・受入結果が変わった場合にDEV_STATEを更新し、調査ログの逐次追記や全資料の毎回読破は不要です。

## 基本方針

- 対象Unityバージョンは `6000.6.0f1`（2026-09-05決定）。`ProjectSettings/ProjectVersion.txt` は保存済み設定、MCPのEditor状態は実行環境の正本として別々に確認する。2026-09-05に両方の一致・Package解決・Editor回帰を確認し、Windows Player・通常操作・音声のユーザー確認によりP0移行受入は完了。根拠は `docs/UNITY6000_6_ACCEPTANCE_AND_NEXT_TASKS.md` を参照。過去の6000.5検証を6000.6の実績へ書き換えない。無関係なURP／Packages更新・ダウングレードは行わない。
- 既存のゲームデータ互換性を最優先する。`.ani` / `.an2` / `.hod` / `Script.spt` / 暗号化 `.x` / `.png` の読み書き仕様を変更する場合は、必ず後方互換性と保存結果を確認する。
- `Assets/Scripts/USEncoder.cs` のShift-JIS変換を避けて通常の `Encoding.GetEncoding(932)` へ置き換えない。Unity/実行環境差分を避けるため、既存の `USEncoder.ToEncoding` を使う。
- `Assets/Scripts.bak*` や `*.bak` は調査用に存在する。ユーザーが明示しない限り削除・同期・復元しない。
- `Library/`, `Temp/`, `obj/`, `.vs/` は生成物で通常の編集・レビュー対象外。`Logs/`もソース修正対象外だが、Console・実機観測・traceの根拠を読む場合は対象を限定して参照できる。既存観測成果物は削除・上書きせず、新しい実行結果を分離する。
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

- 文書のみはリンク・現行/履歴の区別・差分を確認し、Unity起動や全回帰を必須にしない。文言/UIのみは対象言語・画面の確認を中心にする。C#変更時のコンパイル確認は省略しない。
- C#のみの変更でもUnityコンパイルを前提に確認する。可能ならUnity Editorで開き、Consoleエラーがないことを確認する。
- Test Playの挙動変更は対象focused検証・Runtime Verification・Real-Mech Golden Traceを実施する。描画・音声・Collider変更には実機体の該当Play条件を追加する。Runtimeに含まれるPhase検証を重複実行する必要はない。Editor/Package移行や共有境界変更は [監査報告の検証表](docs/PROJECT_AUDIT_2026-09-05.md#verification) に従う。
- `IsCompiling=false`／`IsUpdating=false`だけで合格にしない。検証の完走、期待値、開始前後のConsole、環境Errorを分けて記録する。期待したnegative-path警告によるMCP失敗はログとassertion結果で切り分け、盲目的に再実行しない。
- Runtime/HOD/Goldenの自動検証は [検証ジョブAPI](docs/VERIFICATION_JOB_API.md) のpublic開始・状態取得・中止を使う。開始応答だけで成功とせず、保存結果の終端状態とsuite件数を確認する。既存の同期/Task入口をjob実行中に重ねない。生成traceはrun別に保存し、既存baselineを上書きしない。
- 通常回帰は `WindomVerificationSelection.Regression`（Runtime/HOD/Golden＋承認baseline比較）。`All=7` は従来どおり候補生成用に保持する。baselineの前提不一致を一致として無視せず、意図した差分を承認後に新IDへ昇格する。棚卸しの出現数・pattern数・compiler診断と意味解析成功を区別する。詳細は [P1-03運用](docs/BASELINE_AND_ANI_INVENTORY.md)。
- ファイルフォーマット変更時は、最低限「既存 `.ani` / `.hod` 読み込み」「保存」「再読み込み」「パーツ数と階層」「主要アニメーションプレビュー」を確認する。
- `Script.spt` / `BURNER` 関連変更時は、`UI_SPT.loadSPTField()` から `SptParser.Parse()`、`AniScriptRuntime.sptData`、`BURNER(id)` の点火/停止まで確認する。
- シーン編集を行った場合は `Assets/UI_MechaClean.unity` の参照切れ、Inspector未設定、Prefab参照切れを確認する。

## 原作根拠・独立化・資料の運用

- `原作確定`、`原作高確度`、`原作推定`、`RealAniObserved`、`Unity代替`を区別する。2回同じUnity traceが出ることは決定性の検証であり、原作EXE一致や描画・音響の受入を意味しない。
- TestPlay Core／ANI実行の再利用を優先し、編集プレビューのTODOを別ゲーム実装として二重に埋めない。Facade/public/Inspector参照は保持し、独立化のための大規模分割は依存と受入条件を示して段階的に行う。
- 原作EXE観測は一意に決められない境界だけに限定し、[観測チケット](Tools/OriginalTrace/OBSERVATION_TICKET_TEMPLATE.md)に処理段階・最小フィールド・成功/停止条件を記録する。GT-001 schema v2の全22tick未取得は現在の接地Move保持の受入を妨げない。受入条件が変わった場合だけ再開する。
- Unityメインスレッドで `ani2.load()` を `.Result`／`.Wait()`／`GetAwaiter().GetResult()` により同期待ちしない。非同期完了を待てる入口を使用する。
- 現在のタスク順・環境はDEV_STATE、横断課題は監査報告、各U番号の原作差分はPhase 6C台帳、古い経過は `docs/history/` を参照する。履歴やメモリの古い「未完了」「次回」を現行指示として復活させない。
- Skillの例示ツール名・URIは接続先の実スキーマで確認する。本環境の `Unity_ManageEditor` 等と `mcpforunity://` テンプレートを混同しない。ユーザーの最新決定と本リポジトリの現行指示を優先する。
- 通常検索で資料が見つからない場合は `git check-ignore -v <path>`、`rg --no-ignore` を対象ディレクトリに限定して使う。バックアップ・デコンパイル全文・生成物を無差別検索しない。

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
