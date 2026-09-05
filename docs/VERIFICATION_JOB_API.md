# 検証ジョブAPI（P1-01/P1-02）

更新: 2026-09-06。対象Unity 6000.6.0f1。P0はEditor実測とWindows Player・通常操作・音声のユーザー確認により受入済み。P1-03の固定baseline比較・ANI棚卸しは [運用と比較契約](BASELINE_AND_ANI_INVENTORY.md) を参照。

## 使い方

Editorのメインスレッドから呼ぶ。`StartVerification`は実行を予約してrun IDを即返す。Taskの同期待ちは不要。

```csharp
string id = WindomVerificationRunner.StartVerification(WindomVerificationSelection.Regression);
WindomVerificationResult result = WindomVerificationRunner.GetVerificationStatus(id);
bool accepted = WindomVerificationRunner.CancelVerification(id);
```

選択は `Runtime`、`Hod`、`Golden`、`SelectedGolden`、Runtime/Hod/Goldenを含む `All=7`、`BaselineComparison=16`、`AniInventory=32`。通常回帰は `Regression=23`（All＋BaselineComparison）。フラグの組合せも可能。BaselineComparisonにはGoldenが必要。`SelectedGolden`には第2引数で機体フォルダを指定する。

```csharp
string id = WindomVerificationRunner.StartVerification(
    WindomVerificationSelection.SelectedGolden, selectedMechFolder);
```

既存のRuntime/HOD/Real-Mech Golden/Selected-Mechメニューはこの入口を使用する。Consoleには開始IDと終了状態・結果JSONの保存先を表示し、実jobのFailedはErrorで通知する。新しいメニューは次の3つ。

- `Tools/WindomXP/Verification/Run All`
- `Tools/WindomXP/Verification/Cancel Active Run`
- `Tools/WindomXP/Verification/Verify Job Protocol`

公開済みのRuntime `RunAll()` とSelected-Mech `RunSelectedMechGt001Async(string)` は互換性のため保持する。これらをjob実行中に直接呼ばない。自動運用は上のjob APIへ統一する。従来の一引数Selected-Mech APIは以前のhash別出力先を維持するため、保存成果物を保持する新規運用ではjob APIを使う。

## 状態と終了判断

| state | 意味 |
| --- | --- |
| Queued | 開始APIは返ったが処理は未開始 |
| Running | 初期化・hash採取・suite実行中 |
| CancellationRequested | 中止要求済み。処理境界まで待機中 |
| Finalizing | suite処理は終了し、結果の保存・cleanupを完了するまで待機中。まだ成功ではなく中止対象でもない |
| Succeeded | すべての選択suiteが完走し実行中のError/Exception/Assertなし |
| Failed | assertion例外、初期化失敗、実行中Error等。firstFailureを参照 |
| Cancelled | 中止を受理し現在処理のcleanupが終了。全suite成功とはしない |
| Interrupted | Editor終了/domain中断を検知。cleanup/完走は保証しない |

Warningは記録し、Error/Exception/Assertと区別する。想定外WarningがあってもそれだけではFailedにしない。実行中のErrorを中止で隠さず、最初の失敗を保持する。終了前からConsoleに存在するErrorはこのjobのlistenerでは取得できないため、環境確認は別途Consoleで行う。

`GetVerificationStatus`は内部状態の複製を返す。完了後やdomain reload後は保存JSONから読む。中止要求はメモリ上の状態へ即時反映し、次のcheckpointで永続化する。終了通知は結果保存とcleanup完了後。結果置換に失敗した場合は新規 `persistence-failed.json`、終了/domain中断時は新規 `interrupted.json` を別に保存し、状態取得ではこれらを優先する。ストレージ全体の書込不能やOS強制終了は保存を保証できない。run IDのパス記号を拒否し、未存在IDはFileNotFoundExceptionになる。外部から結果を変更してもjobには影響しない。

## 出力契約

`Logs/VerificationRuns/<UTC日時-GUID>/result.json` と同じrunフォルダ内の `golden/` / `selected-golden/` へ保存する。UTCはInvariantCultureを使用し、和暦設定でもrun IDを変えない。既存 `Logs/TestPlayGolden/` はjob経由の実行では上書きしない。

schemaVersion=1。結果にはrun ID、state、Unity版、開始/完了UTC、選択suite、実行中suite、suite別assertions/scenarios、firstFailure、expected/unexpectedログ、ソース/データhash、outputFolderが入る。P1-03で棚卸し用のcompletedItems/totalItems/currentItemを追加した。旧JSONではこれらは0/空となる。JSONは一時ファイルから置換する。statusの取得にprivateメンバーへのReflectionは不要。

hashは `Assets/Scripts/**/*.cs`、`Assets/Editor/**/*.cs`、ProjectVersion、manifest/lock、機体フォルダ内ANI/AN2/SPT、選択された機体のANI/SPTを実行前に取得する。画像や全モデルassetのhashではなく、原作EXEの観測証拠でもない。

## 初期化・中止・cleanup

- idle Edit Modeだけで開始し、Play/コンパイル/import中は拒否する。既存シーンやユーザーのPlay状態を自動変更しない。
- Localization初期化をawaitし、60秒で完了しなければ失敗。localeを明示的に切り替えず、既存設定を保存し直さない。
- 同時jobは1件。hash採取は.NETのworkerで実施し、Unity APIとsuiteの実行はEditorメインスレッドへ戻る。
- queued状態では処理開始前に中止。実行中はsuite境界、Goldenのscenario境界、初期化/hash処理で中止を確認する。
- Runtimeの同期suiteやHODのファイル操作の途中を強制中断しない。中止要求後も現在の処理が返るまで `CancellationRequested` となる。全処理に強制timeoutがあるわけではない。
- HODの一時ファイル、Goldenの一時GameObjectは既存suiteのfinallyで破棄する。runnerはfinallyでlistener、CancellationTokenSource、実行ロックを解放する。
- 実行中はassembly reloadを遅延させ、cleanup後に解除する。Editor終了/domain中断通知はInterruptedを保存する。OS強制終了・電源断で通知が来ない場合は記録がRunningで残る可能性があり、成功に読み替えない。

## 期待ログ

`ExpectWarning(message, action)`は同期action内の**完全一致するWarningをちょうど1件**要求する。該当WarningだけをConsoleへ流さず結果にexpected=trueで保存する。他のWarning/Error/Exceptionは通常handlerへ渡す。0件/複数件は失敗し、actionが例外でもhandlerを復元する。

RuntimeのWEAPONPOINT 49未結合テストへ適用した。HODの制約破棄・未知IKDATA保持のWarningは通常ログとして残す。ゲーム本体の警告を一律無効化しない。

## 検証と追跡対象

- Unity compile後にjob protocol 19 assertions成功。queued即返却、結果複製、多重開始拒否、期待Warning件数、例外/新規Errorの失敗、firstFailure保持、中止/cleanup、再実行、Interrupted永続化、IDのパス拒否を確認した。
- job API経由でRuntime 672 / HOD 374 / Golden 10シナリオ各2回とSelectedGolden 1シナリオがSucceeded。過去の10 Golden出力ともhash一致。
- Interruptedはhandlerへ合成結果を渡す回帰で検証した。Editorを実際に強制終了した実験とは区別する。実際の再コンパイル/domain reload後にも完了結果のSucceededとactive=falseを取得した。
- TestPlay/Editorの既存meta 8件だけをGit除外から解除し、GUIDを維持した。新規runner/検証2件もsource/metaの対で可視化した。
- 10対を別ディレクトリへ再構成しsource/meta hash一致を確認。対象フォルダの残るignoredファイル0。これは該当対の再構成検査であり、全プロジェクトのクリーンcheckout/Playerビルドの保証ではない。
- 既存未コミット変更を保持。commit/pushは未実施。追跡対象のmanifestと再構成コピーは `Logs/VerificationWork/p1-20260905/` に保存。

最終コードの検証runは `20260905T124650315Z-6d1e66e249494195b7c00a95548730a0`。 [結果JSON](../Logs/VerificationRuns/20260905T124650315Z-6d1e66e249494195b7c00a95548730a0/result.json) はSucceeded、Runtime 672 / HOD 374 / Golden 10 / SelectedGolden 1。protocolは19 assertions。開始前・終了後のConsole Error/Exceptionは0、現在シーンのMissing Scriptは0。実行前hash 94件は終了後もすべて一致し、ANI/AN2/SPT 7ファイルを含む。既存Golden 10ファイルと今回の出力も一致した。 [照合要約](../Logs/VerificationWork/p1-20260905/final-summary.json) と `source-meta-manifest-final.json` / `reconstructed-pairs-final/` を保存した。

## 次の範囲

P1-03で [承認baseline比較・昇格手順と非同期ANI棚卸し](BASELINE_AND_ANI_INVENTORY.md) を追加した。次はP1-04の独立起動・session境界設計。選択source/metaの除外解除は完了したが、全体のクリーンcheckout確認は必要な資産を含むcommit候補の確定後に行う。ゲームCore/ファイル形式/Inspector参照は今回変更していない。
