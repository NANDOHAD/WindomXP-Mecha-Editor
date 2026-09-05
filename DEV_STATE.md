# 開発状態

最終確認日: 2026-09-06（P1-04独立起動・セッション境界設計）

## 現在の目標

- 機体MOD編集（ANI/AN2/HOD/SPT、階層、モデル、アニメーション、保存）の後方互換性を維持する。
- Test Playをベースに、将来、原作をUnity上で再現したクローンゲームとして独立させる。Core/ANI実行を再利用し、ゲーム起動・戦闘主体・入力・資産読込を編集UIから段階的に分離する。
- 対象Unityは **6000.6.0f1**。これは2026-09-05の決定であり、下記の保存済み設定・実行環境・受入状況とは区別する。
- Phase 6B/6Cの既存の境界付き受入は維持する。独立ゲーム完成や原作EXE全tick一致を意味しない。

## 現行環境と移行状態

| 項目 | 2026-09-05移行後確認結果 |
| --- | --- |
| 更新先 | 6000.6.0f1、導入済みEXEのProductVersion `6000.6.0f1_f7f8ed4d1e24` を確認 |
| 保存済みProjectVersion | `6000.6.0f1 (f7f8ed4d1e24)` |
| 実行中Editor | `A:/ProgramFile/UnityEditor/6000.6.0f1/Editor/Unity.exe`。確認開始時Play=false、IsCompiling=false、IsUpdating=false |
| Package | tetgen=1.0.0、timeline=6.6.0、ugui=2.6.0をBuiltInとして登録確認。以前の解決Errorは今回なし |
| 移行受入 | **P0完了**。Editor実測に加え、Windows Player・通常操作・音声は2026-09-05のユーザー確認で受入 |

ユーザーが6000.6で開き直し、承認済みプランに基づき確認した。今回エージェントはProjectVersion/Packages/ゲームコードを変更していない。旧監査の6000.5結果は履歴として保持する。詳細は [6000.6確認結果と次タスク](docs/UNITY6000_6_ACCEPTANCE_AND_NEXT_TASKS.md)。

## 参照順と正本

1. 本書で現在の目標・環境・次タスクを確認する。
2. [PROJECT_REFERENCE](docs/PROJECT_REFERENCE.md)から必要な分野へ進む。
3. 横断課題は[2026-09-05監査報告](docs/PROJECT_AUDIT_2026-09-05.md)、具体的な実行順・依存・完了条件は[6000.6確認結果と次タスク](docs/UNITY6000_6_ACCEPTANCE_AND_NEXT_TASKS.md)。原作差分のU番号は[Phase 6C台帳](docs/TEST_PLAY_PHASE6C_DIFFERENCE_LEDGER.md)。
4. 原作の実装判断は[原作挙動索引](docs/WINDOMXP_ORIGINAL_BEHAVIOR_REFERENCE.md)から対象一次関数へ進む。
5. 以前の詳細な検証・PID/BP・失敗経過は[監査前DEV_STATE完全版](docs/history/DEV_STATE_2026-09-05_PRE_AUDIT.md)。これは履歴であり、旧「次のタスク」や旧PIDを現行指示として使わない。

## 維持する主要な決定

- ANI/AN2/HOD/SPT/暗号化x/pngの形式、読み書き順、Shift-JISのUSEncoder、public/Inspector参照を維持する。未コミット変更やbackupを巻き戻さない。
- AN2/単体HODは有効なtreeDepthを優先しchildCountを修復する。depth無効時だけchildCountから再構成し、全フレームを安全に同期できなければ中止する。
- 旧ANIは本読込前にAN2変換を確認し、変換コピーのみを新規保存する。キャンセルは旧ANIのまま。旧ANI読込時の無修復保持、構造編集時の限定正規化、未知IKDATAの保護、未編集行列/文字列/末尾の原バイト保持を維持する。旧ANI保存は一時書込・同期再読込検証後に置換する。詳細な条件はPROJECT_REFERENCEを正とする。
- 旧HODに保存欄のないunk1〜unk3回転制約がrotationと異なっても、表現可能なrotationはTRS行列へ保存する。失われる制約は警告し、制約保持にはAN2を使う。
- 原作確定/原作高確度/原作推定/RealAniObserved/Unity代替を区別する。Goldenの2回一致は決定性であり原作EXE parityではない。
- GT-001接地Move保持は一次擬似コード、原作アンカー、schema v1の22tick一致を根拠に境界付き受入済み。schema v2全22tick未取得は非ブロッキング。同形式での原作一致が受入条件になった時だけ観測を再開する。
- 原作観測は疑問・段階・observedFields・hash・成功/停止条件をチケット化し、通常起動後attach、対象ECX限定で最小窓を取得する。未観測値は0補完しない。同一経路は既定2回、2セッション連続の運用失敗で止める。
- action4のraw ForceとappliedForceを分け、正YだけをUnity物理へ適用しない。action7/22のゲージ境界を維持する。
- 通常BURNERの要求byte/SPT第3float/ANI outputを分離し、第4tokenは編集互換用に保持する。CShip速度倍率を通常機体へ接続しない。burner開始音＋burner_f15ループ/fadeはUnity Audio Adapter。
- AN2初期scriptはtimed blockとは別vectorであり、TestPlayでは保存・診断compileのみ。action entryで実行しない。GUNFILENAME/SWORDFILENAMEに外部model loader consumerなしという一次解析境界を維持する。
- UI言語はSettings.txtの2行目Language=ja/enへ保存し、既存1行形式・機体フォルダ・データ名を維持する。

## 直近の確認結果

- **P1-04設計完了**: 現行loader/SPT/Controller依存と60Hz順序をソースで確認し、機体別データ・資産所有、sessionの時計/ID/入力/命中/終了/再戦、既存Facadeを保持するP1-05a〜dと受入条件を[独立起動・セッション設計](docs/STANDALONE_SESSION_DESIGN.md)へ確定。Projectile独自UpdateとDummyタイマー二重更新の移行リスクを明記。文書のみで、新API実装・Unity/Player実行は未実施。
- **P1-03実装**: 受入済みP1-02 runを10件の固定baselineへ保存。通常回帰は `Regression` で同一実装2回一致、focused期待値、承認baseline比較を区別する。3機体のANI棚卸しと進捗/途中結果/中止をjobへ接続。[比較契約と受入結果](docs/BASELINE_AND_ANI_INVENTORY.md)。
- **focused検証**: P1-03 31 assertions、job protocol 21 assertions成功。誤挙動2回一致でもbaseline差分を検出、入力/schema/hash不一致拒否、実ANI1ファイル後の中止・結果保持、3ファイル再実行を確認。Windowsの結果置換エラーを共通保存の非同期再試行で修正し、保存待ちはFinalizingとして成功と区別する。
- **棚卸し**: ELS_QT=219、ヘイズル改=200、ザクIIS型=200 action、RunProc/RunProc2は計504出現。compiler診断9件（ELS_QT 8、ザク1）を別記録。U-005jはザクaction105のblock2/5各4出現、WEAPONPOINT=0、p4〜p11=`30,30,20,0,0,0,0,0`、1pattern。block5のUnexpectedEndIfを含むため実行条件の確定ではない。
- **P1-03受入完了**: 最終run `20260905T152910402Z-28067300157447d4a816c1f4dd5dc26a` はSucceeded。Runtime 672 / HOD 374 / Golden 10×2 / 固定baseline 10一致 / 棚卸し3件完走。Console Error/Exception 0、ソース・設定・ANI/AN2/SPTの98 hashと固定baseline不変。新規4組のsource/meta再構成を確認。ゲームCore・シーン・Packages変更、commit/pushは未実施。

### 前回のP0/P1-02確認記録

- **P0受入完了**: Windows Player・通常操作・音声はユーザー確認済み。下記の旧「未検証」「状態再取得不可」は前回エージェント確認時点の境界であり、現在の阻害要因ではない。
- **P1-02実装**: public job APIへRuntime/HOD/Golden/Selected-Mechメニューを統合。非同期locale初期化、開始/状態取得/中止、最初の失敗、期待警告、run別JSON/trace保存を追加。[使い方と受入結果](docs/VERIFICATION_JOB_API.md)。Runtime 672、HOD 374、Golden 10＋選択機体1シナリオ成功。job protocol 19 assertions、domain reload後の完了結果取得、Missing Script 0を確認。
- **P1-01対象修正**: 既存meta 8件のGUIDを保持して除外解除、新規runner/検証2件もsource/meta対で可視化。10対の別ディレクトリ再構成でhash一致。全体のクリーンcheckoutはcommit候補確定後の残条件とし、完了を過大申告しない。
- **最終確認**: run `20260905T124650315Z-6d1e66e249494195b7c00a95548730a0` はSucceeded、active=false。Console Error/Exception 0。ソース・設定・ANI/AN2/SPTの実行前hash 94件すべて不変、既存Golden 10件とも一致。今回のコード変更はEditor検証入口に限定し、ゲームCore/シーン/Packagesは変更していない。

### 前回の6000.6移行確認記録

- **6000.6実測**: Runtime 672 assertions、HOD 374 assertions、Golden GT-001〜010各2回全tick一致。HODは今回、手動Play初期化前に成功。Runtimeの想定警告でMCPは失敗扱いだが本体は完走。最後に成功したConsole Error/Exception取得はError 0。
- **保存データ**: 実行前に退避したGolden全10ファイルと今回の出力がhash一致。ANI/AN2/SPTの7ファイルもhash不変。成果物は `Logs/UpgradeVerification/6000.6-080905-111212/`。旧参照にEditor版情報がないため厳密な版間比較実験とはしない。
- **実機体Play確認**: ガンダムTR-1ヘイズル改を通常UI loaderで読み、74パーツ/200アニメーション/27メッシュ/SPT、Missing Script 0、モデル/プレビュー/HUD描画、public APIからのTestPlay開始、tick 1169、終了を確認。日英の代表文字列も取得できた。通常ModeSelect入力、射撃/格闘/音声、Windows Playerは未検証。
- **終了**: TestPlay停止とEditor Play終了は成功応答取得。その後のGetStateは自動承認レビューの利用上限到達で拒否され、最終状態の再取得は未実施。別経路で迂回しない。
- **ユーザー受入済み**: 2026-09-05、静止X後の論理100→6→0/action6の34tickを保ち、表示だけ立ちへ戻すAdapterと同区間Snd(2)実再生抑止を目視・聴感確認済み。
- **実装済み・手動受入待ち**: type1弾の通過位置ribbon、方向C/連携Cの単一TargetDummy接触面での移動制限。
- **方針・資料**: 監査A01〜A16のプランはユーザー承認済み。旧DEV全文と監査時点の結果は保持。今回P0-02/P1-01〜05の具体計画を追加した。ゲームコード・シーン・Packagesは編集していない。

## 未完了と再開条件

- A01/P0-02完了: Editor実測とWindows Player・通常操作・音声のユーザー確認を合わせて受入。Playerの個別ログ/ビルド条件をエージェントが取得したとは扱わない。
- A04: 対象metaの除外解除は完了。全体のクリーンcheckoutと必要資産の再現確認はcommit候補確定後に残る。
- A08の検証入口/locale初期化はP1-02、A09の固定baselineとA13の非同期棚卸しはP1-03で実装・検証済み。条件別の実描画/音/Collider検証、独立起動、複数機体/sessionは継続。
- U-005j: type62 subtype11の静的8出現・1patternを非同期で再確認済み。次はFUN_004fa830 / FUN_00489590の個数・散布・vtable/lifecycle/owner/textureと、ザクaction105 block5の構文診断境界を確定する。現在のtexture40/root基準/0.4秒は推定Adapter。詳細は6C台帳。
- 残りtype、共有乱数、実機体同士の反射/複合当たり形状、Camera/DX9/音響の厳密再現は未完了。独立ゲームの必要性に応じて小区分で優先する。
- 通常編集プレビューのAniScriptRuntime TODOはTestPlay実装の二重化を避け、必要な共有経路だけ段階接続する。
- 性能はモデルimport/階層探索/演出更新を候補とするが、Profiler計測前に速度改善を断定しない。

## 次のタスク（新目的に基づく順序）

1. **P0-02完了**: Windows Player・通常操作・音声をユーザー確認で受入。新しい障害がない限り同じ受入を要求し直さない。
2. **P1-01対象修正済み・全体再現確認は残る**: 文書と10組のsource/metaを追跡候補へ露出。commit候補の確定後に別checkoutでMissing Script/参照切れを確認する。commit/pushは未実施。
3. **P1-02完了**: public job APIで非同期開始/状態取得/中止、期待ログ・失敗・cleanupと結果保存を検証済み。強制終了時の限界はAPI文書を参照。
4. **P1-03完了**: 固定baseline比較、明示的な新ID昇格、3機体棚卸し、中止/再実行とI/O競合対策を実装・検証済み。最終統合runの結果はP1-03文書を参照。
5. **P1-04設計完了**: [独立起動・セッション設計](docs/STANDALONE_SESSION_DESIGN.md)をP1-05のAPI/所有権/更新順/受入条件の正本とする。現行挙動と新しいsession-v1のUnity契約を区別する。
6. **次はP1-05a**: 共通loader/builderと明示context、編集UIなしの1機体起動を実装する。以降P1-05bの2機体、cのtype1/type57・被弾、dの終了/再戦・Player受入へ進む。既存Facade/形式/承認baselineを保持し、U-005jや表示受入、計測に基づくP2はその後とする。

各変更の検証は[AGENTS](AGENTS.md)と[変更種別表](docs/PROJECT_AUDIT_2026-09-05.md#verification)に従う。Runtime内包Phaseの二重実行や文書のみでの全回帰は不要。新たな決定・進捗・受入結果が変わった時に本書を更新する。
