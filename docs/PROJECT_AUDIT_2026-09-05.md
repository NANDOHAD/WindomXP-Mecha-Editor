# プロジェクト監査 — 2026-09-05

> 承認後の更新: 6000.6でEditor/ProjectVersion一致・BuiltIn Package解決・回帰・実機体起動を確認し、Windows Player・通常操作・音声のユーザー確認によりP0完了。P1-01の対象meta除外解除とP1-02の非同期検証入口を実装・検証した。全体のクリーンcheckoutは未確認。現在の順序は [6000.6確認結果と次タスク](UNITY6000_6_ACCEPTANCE_AND_NEXT_TASKS.md)、実装結果は [検証ジョブAPI](VERIFICATION_JOB_API.md) を参照。以下の監査実測表・課題状態欄は移行前の監査記録として保持する。

> 2026-09-06更新: P1-03でA09の承認baseline比較とA13の非同期ANI棚卸しを追加。U-005jの静的8出現・1patternを再確認し、compiler診断を分離した。実測とI/O競合修正は [P1-03結果](BASELINE_AND_ANI_INVENTORY.md) を参照。次はP1-04の独立起動/session境界設計。

## 決定と監査の結論

> 2026-09-06 P1-04設計完了: A11/A12の依存・API・所有権・更新順と段階差分を[独立起動・セッション設計](STANDALONE_SESSION_DESIGN.md)に確定。独立起動・2機体戦闘の実装と受入はP1-05に残る。以下の監査時点の状態は保持する。

対象Unityを **6000.6.0f1** に変更する。Test Playの開発目的を **「テストモードをベースに将来的に原作をUnity上で再現したクローンゲームとして独立させる」** に変更する。機体MOD編集とANI/AN2/HOD/SPT互換性は継続する。

既存のCore、60Hz実行、原作根拠区分、focused検証は再利用できる。一方、単一ダミーを前提としたControllerとEditor中心の検証だけでは独立ゲームの受入を満たさない。次の順序を推奨する: **移行・再現可能な作業環境 → 独立起動と戦闘主体の設計 → 最小の実機体同士の戦闘 → 未対応type・描画・性能の拡充**。Phase 6Cの境界付き完了は取り消さず、新目的の完成とは区別する。

今回実施したのは監査と資料・資料検索設定の更新。Unity再起動、プロジェクト変換、Packages変更、ゲームコードの改善、Playerビルドは実施していない。これらは以下の受入条件を持つ後続作業として管理する。

## 調査範囲と確度

- 現行作業ツリーを対象にAGENTS、DEV_STATE、README、PROJECT_REFERENCE、Test Play Phase 0/5/6/6C、主要Controller/Core/検証/ローダー、manifest、Build Settings、Git除外設定を確認した。開始時点の変更済み26ファイルと未追跡2ファイルは既存変更として保持する。
- 過去作業はDEV_STATEの記録と関連メモリ（階層保持、ジャンプ・移動、BURNER、原作観測）で照合した。会話全件の頻度集計ではない。「繰り返し」は複数の記録が示すパターンであり、依頼回数の推計は行っていない。
- 全ソースの逐行監査、Profiler計測、全機体棚卸しは未実施。性能項目は実測済み障害と静的な候補を分ける。資料の利用履歴がないため「長期間未使用」とは断定しない。
- GitHub connectorで `repo:NANDOHAD/WindomXP-Mecha-Editor is:issue is:open` を検索し0件。CLIはローカルproxy接続拒否で失敗したがconnectorで補完した。確認できたopen Issueに放置案件はなく、ローカル台帳には未完了がある。他のtrackerや非公開タスクの不存在は主張しない。

## 今回の環境・検証実測

| 項目 | 結果 | 解釈 |
| --- | --- | --- |
| ProjectVersion | `6000.5.0f1 (88b47c5e7076)` | 保存済み設定は移行前 |
| 実行中Editor | `A:/ProgramFile/UnityEditor/6000.5.0f1/Editor/Unity.exe`、Play=false、Compiling=false、Updating=false | 6000.6での動作確認ではない |
| 導入済み更新先 | `A:/ProgramFile/UnityEditor/6000.6.0f1/Editor/Unity.exe` のProductVersion=`6000.6.0f1_f7f8ed4d1e24` | 実ファイル存在・バージョン確認。移行完了を意味しない |
| Console開始前 | Package Resolver Error 1件 | `com.unity.modules.tetgen@1.0.0` と `com.unity.timeline@6.6.0` を解決できない |
| Runtime再実行 | `TestPlayRuntimeVerification.RunAll()` が672 assertionsを返した | 現行6000.5の回帰完走を確認 |
| 実行ツール | 想定内のWEAPONPOINT 49未結合警告によりMCPは `success=false` | 完走ログとツール結果を分離する必要がある |
| Console実行後 | 既存Error 1件、上記Warning 1件 | 新規Errorはこの取得範囲で増加なし。環境全体のError 0とはしない |
| Golden/HOD/実画面/音声/Player | 今回再実行なし | 過去のGolden 10/10×2、HOD 374等は履歴であり6000.6の保証ではない |

## 優先順位の基準

- **P0**: 移行完了や受入可否を誤判定させる環境課題。新環境の基準確定前に対処。
- **P1**: 独立ゲームの最小構成、作業再現性、回帰検出を妨げる課題。次の開発単位で設計・実装。
- **P2**: 局所的な重複、性能候補、保守効率。計測または実需要を条件に対応。

<a id="findings"></a>
## 問題・根拠・改善案・確認方法

| ID / 優先度 | 問題と根拠 | 改善案 | 確認方法・完了条件 | 今回の状態 |
| --- | --- | --- | --- | --- |
| A01 / P0 | 目標6000.6に対しProjectVersion・実行Editorは6000.5。manifestにtetgen/timelineがありConsole解決Error 1件。 | 更新先で再importと依存解決を行い、必要な差分のみ採用。6000.5のエラーを理由に依存を即削除しない。 | ProjectVersionと実行Editorが6000.6、再起動後のPackage解決成功、コンパイル、下記移行検証とWindows Player起動。 | 方針・未検証状態を明記。移行作業は未実施。 |
| A02 / P1 | READMEは日本語化のみ、旧DEV冒頭は比較用Test Play。Phase 6Cの完了がゲーム完成に読める。 | 編集製品と独立ゲーム基盤の目的を明示。既存Phaseの完了範囲を維持し、新しい受入段階を追加。 | README/AGENTS/DEV/PROJECT_REFERENCE/Test Play入口の目的が一致。旧snapshotは履歴表示。 | 更新済み。 |
| A03 / P1 | `.gitignore`が `docs/`、AGENTS、DEVを除外。`git ls-files`でもこれらは未追跡。通常rgでdocsが消える。 | 文書Markdownと指定履歴だけを除外解除。一次C全文や生ログは一括追加しない。 | 通常 `rg --files docs` と `git status --untracked-files=all` で更新資料を検出。次のcommitで必要資料を選択する。 | 除外解除済み。commit/pushは未実施。 |
| A04 / P1 | `.gitignore`の `*.meta` で新規type1 trailのmetaが隠れる。既存TestPlay metaは一部追跡済み。 | 別の変更単位でAssets内のmeta方針を整理し、必要な新規asset/metaを対で追加する。 | 対象GUID参照とmetaの追跡を検査し、クリーンcheckoutでMissing Script/参照切れなし。 | 課題化のみ。既存ignore全体は変更しない。 |
| A05 / P1 | 旧DEV末尾は全変更に全Runtime/Golden/一次コード更新を要求。AGENTSはLogsを全面対象外とするがDEVは観測根拠としてLogsを指す。 | 変更種別ごとの検証表へ統合。Logsは必要な根拠の限定読取り可、既存証拠は保持。無変更の一次コードを書き換えない。 | 文書のみの変更にUnity全回帰を要求しない。挙動変更・移行では必要な検証が残る。 | 指示修正済み。 |
| A06 / P1 | 旧DEVは193行に長大なPID/BP記録と旧assertion数が混在。Phase 0の198、MODEの580、6C冒頭の611と後段の672が同居。メモリにもschema v2を受入必須とした旧記録が残る。 | DEVを現行状態へ要約、旧版を丸ごと保存。各資料を「現行仕様」「段階別履歴」「原作根拠」に分類し、現行値の入口をDEVに固定。 | 履歴から旧作業を再開しない。旧版の完全保存、参照リンク、最新結果の確認日/Unity/範囲を確認。 | 文書整理済み。外部メモリは編集しない。 |
| A07 / P1 | Unity MCP skillは `mcpforunity://editor/state` / `batch_execute` を例示するが本接続は `Unity_ManageEditor` 等。メモリskillは絶対cwd、旧GT-001不一致、全ジャンプチェックを含む。 | 実スキーマを先に確認し、skillは適用対象だけ使用。旧GT-001診断やジャンプ全項目を文書/音声等の小変更へ持ち込まない。 | 最初の状態確認が成功。存在しないtool/URIを反復しない。現在の受入条件が旧skillで上書きされない。 | ローカル指示に適用境界を追加。グローバルskillは未変更。 |
| A08 / P1 | RuntimeはEditorメニュー主体、Goldenの `RunAllAsync` はprivate。今回672完走でもnegative-path警告でMCP失敗。HODは過去にSelectedLocale未初期化で停止し手動Play後に解消。 | 非同期完了・結果・想定ログを構造化する検証入口、locale初期化を明示するfixture、時間上限とcleanupを用意。 | 警告想定ケースは合格、新規Errorと意図的assertion失敗は不合格。冷起動Edit ModeでもHODを開始でき、手動Playを前提にしない。 | 課題化。既存回帰結果とMCP判定のずれを実測。 |
| A09 / P1 | Golden `RunAllAsync` は同一実装の2回比較とfocused判定を行い、固定パスにreferenceを書出す。過去版との自動比較ではない。 | 現在の意味検証を維持し、承認済みbaselineを別保存。意図した変更の差分と承認理由を追跡する。 | 同じ誤挙動を2回出す意図的変更でもbaseline/独立期待値が検出。新旧ファイルと最初の差分が残る。 | 課題化。「テストが無意味」とは判定しない。 |
| A10 / P1 | Golden `RunScenario` はrootだけのRoboStructureと合成WEAPONPOINTを作り、`useColliderGrounding=false`。単一ダミー。旧DEVも描画/Audio/Colliderを対象外と明示。 | 実HOD/モデルのPlay Mode検証を追加。入力列、Camera、接触、AudioSourceの状態、スクリーンショット/必要時録音を保存。 | 静止X、方向C/連携C、ジャンプ/boost、再入場で姿勢・移動・SE・接触を確認。人工的な音声/Collider不具合を該当検証が検出。 | 未実装。現在の自動検証の境界を明文化。 |
| A11 / P1 | Controllerは6069行、`RoboStructure`、`UI_SPT`、`TestPlayTargetDummy`を直接参照。Build Settingsは編集シーン1件、対象ディレクトリにasmdefなし。 | まず対戦セッション、機体状態、入力、資産読込、標的/命中の境界を設計。既存Facadeを保ちゲーム起動入口を追加。asmdefは依存検証後に導入。 | 編集UI操作を経ずにデータ読込→2機体→入力→攻撃/被弾→終了/再戦をPlayerで実行。Editor参照なし、既存編集シーンも回帰合格。 | 新目的のP1課題。行数だけで分割せず段階移行。 |
| A12 / P1 | 複数機体・対象別判定・共有乱数が未接続。通常対象は単一球形Dummy。ローカル散布関数は原作の共有乱数列ではない。 | 機体ID、更新順、target列挙、HP/被弾/撃破、セッションseedを明示。type57 interval等の既存Coreを再利用。 | 2機体以上で命中先・対象別interval・所有者除外・消滅後参照を検証。同seed/入力で再現し、原作乱数一致は別判定。 | 独立化設計と最小戦闘に組み込む。ネットワーク/AI対戦を今回の必須仕様として追加しない。 |
| A13 / P1 | U-005i記録では全ANI走査でmain thread同期待ちによりEditor停止。`ani2.load` は `await Task.Run`、Goldenはawait使用。 | loader自体の破棄ではなく棚卸し呼出側を非同期化し、進捗、取消、ファイル単位タイムアウト/結果保存を設ける。 | Editorが応答しながら実3機体を走査し、取消後に再実行可能。元ANI hash不変、action/WEAPONPOINT/p4～p11と重複定義を出力。 | ガードをAGENTSへ追加。停止の再現実験はしない。 |
| A14 / P2 | `RoboStructure.buildStructure` と `buildStructureFromLoaded` に後方親探索が重複。前者のj==0/elseは同じTRS適用。Controllerの複数RemoveActive…に同じ破棄処理がある。 | 純粋な親index解決と破棄の小さな共通処理を候補にする。RemoveActiveSwordBeamはindex探索を担うため無条件に不要とはしない。 | 旧ANI不整合/複数root/全フレーム保持と既存Hierarchy回帰、owner削除、二層効果の独立終了、Play/Edit cleanupを確認。 | 改善候補のみ。形式保持と型別lifecycleを優先。 |
| A15 / P2 | 親探索は最悪O(n²)、モデルimportはパーツ別同期処理。trailは点ごとSetPosition、生成ごとMaterial確保。遅さの実測値は未取得。 | 固定機体/パーツ/弾数でProfilerと読込時間を測る。親stack、キャッシュ、一括転送、poolは上位コストに限って採用。 | 同一条件の前後でframe時間p95、GC allocation、読込時間、メモリを記録。見た目/形式の回帰なし。 | 性能候補。目標値は基準計測後に設定。 |
| A16 / P2 | `AniScriptRuntime`にWeaponAttack、RunProc2、Rnd等のTODO。TestPlay側には既存のANI compiler/VM/Coreがある。 | 編集プレビューの未対応表示を明確にし、必要な命令だけ共通実行結果/イベントへ接続する。ゲーム機能の二重実装を避ける。 | 同一ANIの命令解釈を比較し、プレビュー50HzとTestPlay60Hzの用途差を保持。未知本文・保存結果を変更しない。 | 新目的下の優先順位を整理。TODO一括実装はしない。 |

## チェックリストとの対応

| チェック | 確認結果・参照 |
| --- | --- |
| ① AIに何度も伝える指示 | 原作確度の区別、Inspector/原データ保持、現物での受入が過去記録に反復。AGENTSへ固定（A02/A05/A10）。 |
| ① 毎回の手操作 | メニュー実行、locale用Play初期化、見た目/聴感確認、デバッガattach/BP/入力受理確認（A08/A10/A13）。 |
| ① 同じ修正を頼む箇所 | action4の上昇、BURNER向き/音、type1長さ、静止射撃の姿勢/SE。Core合格だけではAdapterを覆えない（A10）。 |
| ① よく行き詰まる作業 | 原作入力/ECX/tick観測とmain thread同期待ち。停止条件を維持（A13、後述保留表）。 |
| ② 古い前提 | Unity、ローカライズのみのREADME、比較用Test Play、段階別の旧検証件数（A01/A02/A06）。 |
| ② 矛盾・重複 | Logs参照禁止と観測運用、全変更への全検証、複数資料の現行状態重複（A05/A06）。 |
| ② 小作業への必須手順 | 文書にも全Runtime/Goldenを要求する旧DEV末尾を廃止し変更種別表へ統合（A05）。 |
| ② 実態に合わないSkills | 実際のMCPと例示APIの違い、メモリskillの旧診断/絶対path/広いチェック（A07）。 |
| ③ 古い/新しい情報の混在 | DEV要約と完全履歴保存、Phase 0〜6の履歴位置付け（A06）。 |
| ③ 保存したまま使われない資料 | backup/巨大解析資料の未使用は断定不可。削除せず、入口資料から対象関数へ絞る。全解析資料の参照到達性監査は未実施。 |
| ③ 検索で見つからない情報 | Git除外を実測。文書だけ通常検索に復帰（A03/A04）。 |
| ④ 前のモデルが解けなかった問題 | schema v2完全trace・共有乱数・残りtype等が未完。モデル能力が原因とは断定せず、観測条件/仕様未確定/統合未実装を分類。 |
| ④ 修正を繰り返すコード | Controllerの演出/移動と検証境界。局所共通化とPlay Mode受入（A10/A14）。 |
| ④ 放置Issue | GitHub open Issue検索0件。ローカルU台帳・本報告を後続作業として整理。 |
| ④ 複雑すぎる業務 | GT全件の原作観測を標準化せず、必要な代表観測と非同期棚卸しへ限定（A13）。 |
| ⑤ 重複・中継 | 階層探索/効果cleanupに具体例。Facadeや意味のあるindex探索を削除候補と混同しない（A14/A16）。 |
| ⑤ 検出できないテスト | 同一実装2回と固定baselineの違い、合成root/Collider無効、実音声/映像の範囲外（A09/A10）。 |
| ⑤ 遅い箇所 | 同期待ち停止は記録あり。親探索/import/trailは未計測候補（A13/A15）。 |
| ⑤ AIが確認できない工程 | 原作の入力受理、最終的な視覚/聴感受入、Editorを使わないPlayer起動の自動入口不足（A08/A10/A11）。画像取得やAudioSource確認で補い、人の最終受入と分離。 |

## 止まっている仕事の再分類

| 対象 | 現在の判断 | 再開条件・確認方法 |
| --- | --- | --- |
| U-005j type62 subtype11 | 未実装。旧「次の第一候補」から移行/独立化基盤の後に置く。詳細は[6C台帳](TEST_PLAY_PHASE6C_DIFFERENCE_LEDGER.md)。 | A13で非同期棚卸しを整え、既存8件を再確認。factory/vtable/lifecycle/owner/textureを一意にした範囲だけ実装。 |
| type1 ribbon・方向C/連携C接触 | 実装済み、実機体の手動受入が残る。 | 同じ機体/入力/視点で描画と接触を記録。静止Xの立ち復帰/SE抑止は2026-09-05受入済みとして保持。 |
| GT-001 schema v2全22tick | 未取得・非ブロッキング。Phase 6Bは境界付き受入済み。 | 同形式の原作一致が明示的な受入条件となった時だけ観測チケットで再開。 |
| GUNFILENAME/SWORDFILENAME外部loader、AN2初期scriptのaction entry実行 | 未解決扱いで再開しない。現在の一次解析ではconsumerなし/別vectorで境界確定。 | 新しい一次根拠が既存結論を覆す場合に限り再評価。 |
| 実機体同士の戦闘、共有乱数、type11実データ/反射 | 旧Test Playでは範囲外だったが独立化の課題へ昇格。 | 最小戦闘はA11/A12。原作乱数列やtype11の再現は根拠確定を別途要する。 |
| 複数対象、残り武器/効果、Camera/DX9/音響厳密式 | 未確定またはAdapter。 | 最小戦闘で必要なtypeから選定し、Coreと表示/音声の受入を分離。 |

<a id="verification"></a>
## 変更種別ごとの検証

| 変更 | 必須範囲 | 合格としないもの |
| --- | --- | --- |
| 文書・指示のみ | リンク、差分、現行/履歴表示、検索可否 | 古い実測数を今回の合格に転記 |
| UI/文言 | 対象日英表示、既存値/Inspector参照。C#ならコンパイル/Console | 他領域の全Goldenを毎回必須化 |
| ANI/HOD形式・階層 | 公開loader、保存/再読込、原hash/未編集行列、全フレーム階層、プレビュー、HOD回帰 | assertions数だけで原バイト保持を推測 |
| Test Play挙動 | focused＋Runtime（Phase1〜6を内包）＋全10Golden各2回＋Console | 決定性だけを原作一致と表記 |
| 描画/音/Collider | 上記に加え実機体Play Modeの報告された条件 | 合成rootのGoldenやAudioSource状態だけで聴感一致と表記 |
| Unity/Packages移行 | 解決成功、再import/compile、HOD、Runtime、Golden、日英UI、実機体Play、Assimp/音/描画を含むWindows Player起動 | 導入済みEXE、ProjectVersion書換え、Compiling=falseだけで移行完了 |
| 独立ゲーム基盤 | 編集UIなしで起動/読込/戦闘/終了/再戦、2機体以上、既存編集機能の回帰 | 編集シーンだけの起動を独立ゲーム完成とする |

Unity移行では現在の未コミット変更と無視された必要データも含む復元可能なsnapshotを先に確保し、旧Editorを通常終了してから対象版で開く。必要な依存差分を確認し、履歴の数値は変更せず新しい受入結果を追記する。この順序は[Unity公式アップグレード手順](https://docs.unity3d.com/kr/6000.0/Manual/upgrade-project.html)のバックアップ・依存互換性・影響範囲テストに沿う。更新先の変更点は[6000.6.0f1公式リリースノート](https://activation.unity3d.com/releases/editor/whats-new/6000.6.0f1)を参照する。

## 新目的に対する実装順案

1. **環境確定**: A01を完了。A03/A04の追跡漏れを解消し、6000.6での結果を保存する。
2. **検証入口と設計**: A08/A09/A13を具体化。A11/A12の機体・セッション・入力・資産・命中境界を設計し、既存public参照とデータ形式を保つ受入条件を確定する。
3. **最小ゲーム実行**: 編集UIなしの起動、2機体の移動/通常射撃/格闘/被弾/終了/再戦をPlayerで検証する。ネットワーク、AI、モード一覧は別途仕様化する。
4. **再現範囲拡大**: U-005jと必要な未対応type、実機体の描画/音声受入を進める。共有乱数や原作複合当たり形状は別の根拠チケットで扱う。
5. **計測に基づく整理**: A14/A15/A16を需要順に実施。全面的なController書換えを先行させない。

## 文書の正本と今回の変更

- `AGENTS.md`: 恒常ルール、証拠区分、変更種別検証、非同期/skill適用ガード。
- `DEV_STATE.md`: 現在の目標・実環境・直近結果・次のタスク。詳細調査ログは保持しない。
- `PROJECT_REFERENCE.md`: 分野別の索引と互換仕様。manifestにない古い直接依存表記を修正。
- `TEST_PLAY_MODE.md`: 現行機能の入口。Phase 0〜6はその段階の仕様/履歴として読む。
- 本報告: A01〜A16の横断改善台帳。U番号の原作差分は6C台帳で管理する。
- [監査前DEV_STATE完全版](history/DEV_STATE_2026-09-05_PRE_AUDIT.md): 更新前の全文をそのまま保存した履歴。PID、検証数、旧「次のタスク」は当時の情報。
- READMEは日英で目的と更新先を反映。.gitignoreは文書だけ可視化。C#・シーン・Packages・ProjectVersionおよび外部skill/メモリは変更していない。

## 根拠への直接参照

- 環境: [ProjectVersion](../ProjectSettings/ProjectVersion.txt)、[manifest](../Packages/manifest.json)、[Build Settings](../ProjectSettings/EditorBuildSettings.asset)。Console/MCPの今回結果は本報告の実測表を参照。
- A08/A09/A10: [Runtime検証](../Assets/Editor/TestPlayRuntimeVerification.cs)の `RunAll` / `VerifyOriginalNormalAttackFlow`、[Golden検証](../Assets/Editor/TestPlayGoldenTraceVerification.cs)の `RunAllAsync` / `RunScenario`。
- A11/A12/A14: [Controller](../Assets/Scripts/TestPlay/TestPlayController.cs)のフィールド、`SampleHinokoSigned`、`RemoveActiveMagicShieldEffectAt` / `RemoveActiveThunderEffectAt` / `RemoveActiveSwordBeam`。
- A13/A14/A15: [ani2](../Assets/Scripts/ani2.cs)の `load`、[RoboStructure](../Assets/Scripts/RoboStructure.cs)の `buildStructure` / `buildStructureFromLoaded` / `ImportModelEncrypted`、[type1 trail](../Assets/Scripts/TestPlay/TestPlayType1TrailEffect.cs)の `Initialize` / `SetTrail`。
- A16: [AniScriptRuntime](../Assets/Scripts/AniScriptRuntime.cs)のTODOと [TestPlayAniProgram](../Assets/Scripts/TestPlay/TestPlayAniProgram.cs)の共通実行器。
- A06/A07: メモリの旧条件は現行DEV/6C台帳で上書き判断した。監査対象skillは `C:/Users/Hibiki/.codex/skills/unity-mcp-skill/SKILL.md` と `C:/Users/Hibiki/.codex/memories/skills/windomxp-testplay-original-verify/SKILL.md`。外部ファイルのため本リポジトリの移行対象には含めない。

## 文書更新の確認

- 更新した現行Markdown 8件のローカルファイルリンク先が存在することを確認した。履歴完全版内の相対パスは保存当時のリポジトリroot基準で読み、履歴本文は改変しない。
- 通常rgでdocsのMarkdownが列挙され、Git statusでAGENTS/DEV/監査報告を確認できる。一次C全文と新規metaは既存どおり除外対象である。
- `git diff --check -- README.md` は成功。AGENTS/DEV/新規監査報告の末尾空白検査と、更新した現行Markdown 8件のリンク検査も成功。全作業ツリーへの `git diff --check` は既存シーン・Sprite/ギズモmeta等の末尾空白で失敗したため、全体成功とはしない。既存変更の空白は本監査では修正していない。改行正規化のGit警告もある。
- 履歴完全版のSHA-256: `ACC27F18941E3E82AC1BEB84C634FAE8CCC6FDC4D286082F6DC03EB2DC0E1A29`。
