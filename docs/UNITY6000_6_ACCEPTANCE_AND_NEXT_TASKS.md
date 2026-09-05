# Unity 6000.6確認結果と次の実行計画

確認日: 2026-09-05。2026-09-05監査プランのユーザー承認後、ユーザーが6000.6で開き直したプロジェクトを確認した。

## 判定

**P0完了。Editor実測に加え、2026-09-05にWindows Player・通常操作・音声のユーザー確認が得られたため移行受入とする。**

以下の実測表はエージェント確認時点の記録。Playerの個別ケース結果やBuildReportをエージェントが取得したとは主張しない。P1実装結果は [検証ジョブAPI](VERIFICATION_JOB_API.md) を参照。

対象と実行環境は `6000.6.0f1 (f7f8ed4d1e24)` で一致した。旧環境で発生していたtetgen/timelineのPackage解決エラーは今回再現せず、両方ともBuiltInとして登録されている。Packagesの削除やダウングレードは必要ない。

## 今回確認した範囲

| 項目 | 実測結果 | 境界 |
| --- | --- | --- |
| Editor/ProjectVersion | `A:/ProgramFile/UnityEditor/6000.6.0f1/Editor/Unity.exe`、保存設定6000.6.0f1 | 開始時Play=false、Compiling=false、Updating=false |
| Package登録 | timeline=6.6.0、tetgen=1.0.0、ugui=2.6.0、いずれもBuiltIn | manifest/lockだけでなくEditorの登録を照会 |
| Runtime | 672 assertions完走 | WEAPONPOINT 49未結合の想定警告でMCPは失敗扱い。検証本体は戻り値672 |
| HOD | `isRunning=false`、`lastResult=374 assertions passed.` | 今回は手動Play初期化前に完走。過去のlocale障害は今回は再現しない |
| Golden | GT-001〜010すべて各2回、全tick一致 | focused判定も通過。原作EXE parityではない |
| 保存済みtraceとの差分 | 実行前に退避した10ファイルと実行後のSHA-256がすべて一致 | 退避ファイルにEditor版の履歴情報がないため、厳密な6000.5対6000.6比較実験とは呼ばない |
| 実機体読込 | 通常 `UI_SelectMech.loadFile("Script.ani")` でガンダムTR-1ヘイズル改を読込。74パーツ、200アニメーション、27 MeshRenderer、SPTあり | 元データの形式変更なし、公開UI経路を使用 |
| シーン | 読込後の全root子孫でMissing Script 0 | 全SerializedObject参照の完全監査ではない |
| Play Mode | `StartTestPlay()`、logicalAction=0、tick=1169のIdle trace、`StopTestPlay()`後active=false | 通常のModeSelect UIではなくpublic起動APIを使用。射撃/格闘入力・全UI遷移は未確認 |
| 画面 | 実モデル、階層、プレビュー、HP/ゲージの描画を画像で確認 | 編集UI上からの直接起動画像。ゲーム画面レイアウトやtype1/接触の最終受入ではない |
| 日英リソース | en=`Position`、ja=`位置`をStringDatabaseから取得 | 全画面切替/永続化の再試験ではない |
| 元データ | Golden前に採取したANI/AN2/SPTの7ファイルは終了後hash不変 | HODテスト中の原本保持はHOD検証自身のassertionに依拠 |
| Windows準備 | ActiveBuildTarget=StandaloneWindows64、IsBuildTargetSupported=true | Playerビルド/実行は未実施 |
| Console | 開始時と最後に成功したError/Exception取得でError 0 | 全警告0の主張ではない。診断用一時コードの失敗は下記に分離 |
| 終了状態 | TestPlay停止、Editor Play終了の成功応答取得 | 続くGetStateは利用上限による自動承認レビュー拒否で取得不可。別経路で迂回しない |

### 検証用一時コードで判明した制約

- `System.Reflection` のimportはUnity RunCommand側で拒否された。private `running` の照会を行わず、Golden完走ログで判定した。次の検証APIはpublic入口を用意する。
- 一時コードの `Object.GetInstanceID()` は6000.6でCS0619。画像はScreenCaptureで取得した。Camera toolのinstance ID例示をそのまま使わない。既存プロジェクトのコンパイル失敗とは区別する。
- 通常読込後に選択UIが非表示となり、active-only検索では一時コードがNullReferenceExceptionとなった。`FindObjectsInactive.Include`とnull確認で実体を確認した。ロード不具合として扱わない。
- これらのためゲームコードを変更していない。想定ログ/MCP結果/プロジェクトErrorを別に記録する必要性はA08に残る。

### 成果物

`Logs/UpgradeVerification/6000.6-080905-111212/` に今回の退避・比較結果を保存した。フォルダ日時はOSの和暦書式由来で、確認日は上記の西暦日付を正とする。今後のrun IDはInvariantCultureのUTCで生成する。

- `golden-before/`、`golden-after/`: 旧/新JSONLと診断ファイル。
- `golden-comparison.json`: 10シナリオのhash一致。
- `input-hashes-before.json`、`input-hashes-comparison.json`: 7ファイルの原データ保持。
- `testplay.png`: [実機体表示画像](../Logs/UpgradeVerification/6000.6-080905-111212/testplay.png)。

## 次タスクの順序と依存

**P0-02 → P1-01 → P1-02 → P1-03 → P1-04 → P1-05** を基本順とする。P0-02はユーザー受入で完了、P1-01は対象meta除外解除と10対の再構成確認済み（全体checkoutは残る）、P1-02は実装・回帰済み。P1-03の実装と受入結果は [固定baseline・ANI棚卸し](BASELINE_AND_ANI_INVENTORY.md)、P1-04設計は完了し、次はP1-05aの共通ロードと独立1機体起動。以下は承認済み計画の要件であり、個別の実施証拠は各実装文書に記録する。P2整理やU-005jの演出拡張はこの基盤の後に置く。

### P0-02: Windows Playerと通常操作の移行受入を閉じる

状態: **ユーザー確認により受入完了**。以下の提案ケースすべての個別ログを取得したとは扱わず、確認済み事項の再実施は要求しない。

- 対象: 現行編集シーン `Assets/UI_MechaClean.unity`、Build Settings、Assimp/native DLL、Localization、TestPlay描画/音声。
- 作業: 現在の未コミット変更も含めて復元可能なsnapshotを確保する。現在の保存済みシーンを専用の未使用出力フォルダへWindows64 Development Buildし、BuildReportとPlayer.logを保存する。既存Build Settingsや公開出力を上書きしない。
- 実行: Playerを通常の画面付き環境で起動し、機体フォルダ選択→実ANI/HOD/SPT/モデル読込→アニメプレビュー→ModeSelectからTest Play→移動/短押し・長押しジャンプ/boost/静止X/方向C・連携C→終了/再入場を確認する。元ANI保存は実施しない。
- 日英: 両言語へ実際に切替し、固定候補とHUD、再起動後の言語/機体フォルダ保持を確認。既存設定は試験前に退避して復元する。
- 完了: Build成功、PlayerでDLL/Shader/locale例外なし、モデル/カメラ/接触/AudioSourceが動作、静止XのSE抑止とboost音の開始/停止を確認。最終的な視覚・聴感評価は操作条件付きで記録する。
- 停止条件: DLL不足/Shader失敗/未保存シーン/新規例外があれば最初の失敗を保存してその範囲を修正する。Editor回帰の成功でPlayer失敗を相殺しない。
- 出力: `docs/`へ結果要約、`Logs/UpgradeVerification/<UTC-run-id>/`へBuildReport要約・Player.log・画像・入力条件。P0完了はこの受入後。

### P1-01: 文書・asset/metaの再現可能な追跡範囲（A03/A04）

- 対象: `.gitignore`、今回の文書、新規TestPlayスクリプトと対応meta、シーンGUID参照。
- 作業: `git ls-files`と `git check-ignore`で必要なソース/文書/metaのペアを棚卸し。Assetsのmetaを一律追加せず、新規依存と参照される資産から対象集合を確定する。ゲームデータ、Logs、backupは一括追加しない。
- 完了: 新規スクリプト/assetの必要metaが追跡対象に入り、依存集合を別checkoutへ再現した際のMissing Script/参照切れ0。commit/pushは独立の操作として扱う。
- 注意: 既存シーン・metaの末尾空白は今回の機能修正に混ぜない。

### P1-02: 構造化された非同期検証入口（A08、実装・回帰済み）

- 対象: `Assets/Editor/TestPlayRuntimeVerification.cs`、`HodHierarchyPriorityVerification.cs`、`TestPlayGoldenTraceVerification.cs`。Editor専用runner/resultを小さく追加する。
- API案: public `StartVerification(selection)` がrun IDを即返し、public `GetVerificationStatus(runId)` で待機/実行中/成功/失敗/中止要求中と結果を取得する。内部は `Task` をawaitし、Unityメインスレッドを同期待ちしない。既存MenuItemは同じ入口へ委譲する。
- 結果: schemaVersion、UTC run ID、Unity版、ソース/データhash、対象suite、assertion数、開始/完了時刻、最初の失敗、expected/unexpectedログ、生成物パスを保存する。
- ログ: WEAPONPOINT 49等の期待警告はケース内で種類/内容/件数を限定して検証する。全Warning抑止やMCPの `success=false` の無条件無視はしない。
- 初期化: locale初期化を非同期で明示し、ユーザーのlocale/scene/Play状態を保持する。今回のHOD成功を理由に初期化依存が常にないと断定しない。
- 中止: 同時実行は1件。suite境界で中止要求を受け、cleanupして結果を確定する。未対応parser処理を強制中断したと偽らず、実行中操作の完了待ちを表示する。
- 完了: Runtime/HOD/Golden成功、意図的な失敗1件を検出、想定警告成功、新規Error失敗、多重開始拒否、中止後の再実行、domain reload後に実行中のまま残らないことを確認する。元ANIと既存baselineのhashを保持する。
- 依存: P1-01。検証入口の変更はゲームtick/原作根拠/ファイル形式を変更しない。

### P1-03: 承認baselineと非同期ANI棚卸し（A09/A13）

- baseline: 生成したtraceは毎回新しいrunフォルダへ保存し、承認済み参照は別ディレクトリに固定する。比較はデータhash/シナリオ/60Hz/schemaを先に検査し、同一実装2回比較＋承認baseline比較＋focused期待値を分ける。差分が意図的な場合だけ理由付きでbaselineを昇格する。
- 棚卸し: 3機体をファイル単位でawaitし、action、script block、WEAPONPOINT、type/subtype、p4〜p11、重複判定キーをCSV/JSONへ出力。進捗・中止要求・途中結果をP1-02と同じjob契約へ接続する。
- 完了: 同じ誤挙動を2回生成しても承認baselineとの差分を検出。入力契約不一致は比較拒否。Editor応答を維持し棚卸しの中止後も再実行できる。原データ不変。U-005jの既存8件を実データで再確認するまで件数を確定値として更新しない。
- 依存: P1-02。baselineの自動上書きや全ANIのメインスレッド同期待ちは禁止。

### P1-04: 独立ゲームの起動・セッション境界を確定（A11/A12、設計完了）

2026-09-06: [独立起動・セッション設計](STANDALONE_SESSION_DESIGN.md)に現行依存、API/所有権、現行tick順とsession-v1契約、P1-05a〜dの差分・受入条件を確定した。文書のみで、新APIの実装やUnity/Player受入は含まない。次はP1-05a。

- 現在の結合: `UI_SelectMech.LoadDataAsync` がANI/階層/モデル/UI更新をまとめる。`UI_SPT.LastSptData` はprivate setter。`TestPlayController.StartTestPlay` がRoboStructure/UI_SPT/単一Dummyを探して起動する。`UI_ViewControl.ModeSelect` が編集/試験表示を切り替える。
- 設計成果物: 読込済み機体データの契約、実行時機体ID/状態の所有者、sessionの生成/終了/再戦、入力snapshot、target検索/命中通知、資産の所有と破棄、60Hz更新順を図とAPI表にする。
- 境界案: 共通ANI compiler/track scheduler/Action/Motion/Combat Coreを維持。UI付きloaderとゲーム起動loaderが同じデータ読込処理を使い、ControllerへSPT/機体/入力/対象providerを明示的に渡す。既存public/Inspector参照を互換Facadeとして残す。
- 対象IDと乱数: session内でIDと更新順を安定化し、seed/乱数使用箇所をtraceへ出す。決定性のためのUnity方式と原作共有乱数列一致を別に扱う。
- 完了: 編集UIを使う既存経路と独立入口の依存図が明示され、どの処理を共通化し、何をAdapterへ残すか決定できる。形式/API破壊や一括asmdef移動を含まない段階別差分を用意する。
- 依存: P0-02の実行上の制約とP1-02/03の検証基盤。大規模Controller分割を最初の実装にしない。

### P1-05: 最小の独立起動と2機体戦闘（A10/A11/A12、段階実装）

1. 編集UIを生成しない専用起動入口で、同じ実機体を1体読込・表示・入力・終了できるようにする。編集シーンのBuild Settingsを置き換えず専用build構成で確認する。
2. 2体目を独立したID/HP/状態で生成し、target providerを単一Dummyから機体集合へ接続する。Dummyは検証fixtureとして残す。
3. 通常射撃と既対応type57格闘だけで、所有者除外、対象別再命中interval、HP/撃破、消滅後参照、終了/再戦時cleanupを成立させる。
4. 入力列replayとPlay Mode画像/接触/AudioSourceログを保存し、EditorとWindows Player双方で検証する。

完了条件: UI_SelectMechやUI_SPTのGameObjectなしで起動でき、2体の状態が混ざらず、同seed/入力で再現し、終了/再戦後に弾・音・対象参照が残らない。既存編集シーン、HOD回帰、Runtime、Goldenは維持する。ネットワーク・CPU AI・全武器・原作EXE全tick一致はこの最小段階の必須要件にはしない。

## 作業の境界

全体プランの承認は継続して有効。既に承認された範囲で同じ確認を要求し直さない。未定のゲームルールを確定仕様として追加せず、実装対象の受入条件が変わる場合にだけ具体案を示す。

前回の移行確認ではEditor状態再取得が自動承認レビューの利用上限到達で拒否された。今回P1開始時にはUnity操作が成功し、6000.6 idle Edit ModeとConsole Error 0を再確認して検証を実施した。過去の拒否を現在の阻害要因として扱わない。
