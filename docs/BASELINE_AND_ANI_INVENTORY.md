# 承認baseline比較と非同期ANI棚卸し（P1-03）

更新: 2026-09-06。Unity 6000.6.0f1、Editor専用。ゲームCore、ANI/HOD/SPT形式、Inspector参照は変更しない。

## 実行入口

```csharp
// Runtime/HOD/Golden各2回＋承認baseline比較
string id = WindomVerificationRunner.StartVerification(WindomVerificationSelection.Regression);
// 上記に3機体の棚卸しを追加
string id = WindomVerificationRunner.StartVerification(
    WindomVerificationSelection.Regression | WindomVerificationSelection.AniInventory);
// 棚卸しだけ
string id = WindomVerificationRunner.StartVerification(WindomVerificationSelection.AniInventory);
```

状態取得と中止は [検証ジョブAPI](VERIFICATION_JOB_API.md) の `GetVerificationStatus(id)` / `CancelVerification(id)` を使う。`completedItems` / `totalItems` は棚卸しの完了ファイル数、`currentItem` は読込ファイルまたは走査actionを示す。開始返却は完了ではない。

`Tools/WindomXP/Verification/Run All` と既存Real-Mech Goldenメニューはbaseline比較を含む。追加メニューは `Inventory Three Mechs` と `Verify Baseline and Inventory`。既存enumの `All=7` は互換性維持のためRuntime/HOD/Goldenだけとし、新しい通常回帰は `Regression=23` を使う。`BaselineComparison=16` は同じrunに `Golden=4` が必要。`AniInventory=32` は単独実行できる。SelectedGoldenは個別機体の原作観測用入口として保持し、固定10シナリオのbaselineには混ぜない。

## 固定baselineと比較契約

基準は `Verification/Baselines/6000.6-20260905-p1-02/` の10 JSONLとmanifest。ユーザーが確認したP1-02のrun `20260905T124650315Z-6d1e66e249494195b7c00a95548730a0` をコピーして固定した。ゲームデータ本体は含めない。manifestには承認理由/UTC、source run、Unity版、ソース・データhash、シナリオ設定hash、traceファイルhashを保存する。ソースhashは来歴であり、ソース変更そのものを回帰失敗にはしない。

`.gitattributes`でbaseline配下の改行変換を無効化し、`core.autocrlf=true`のcheckoutでもhash対象のバイト列を保つ。baselineが移動/再構成されてもWindowsの改行変換だけで比較不能にしない。

比較は次を区別する。

1. Goldenの同一実装2回一致とfocused期待値: 既存検証を保持。
2. 比較前提: baselineファイルhash、シナリオ設定（setup、target、required action/command、全入力segment）、trace schema=1、source、scenario、機体、ANI/SPT hash、60Hz、確度、tick数、入力列を検査。不正JSON、欠落入力、非連続tickも拒否。
3. 挙動比較: 前提一致後に全tick文字列を厳密比較。最初の差分indexと双方のtickを `baseline-comparison.json` へ保存。

前提不一致は `ContractMismatch`、挙動差は `Mismatch`。どちらもjobはFailedになる。比較前の読込障害も `firstFailure` に残す。同じ誤挙動が2回出ても固定baselineとの差分で検出できる。JSONの表現差を数値許容誤差で吸収する比較ではない。

これは **RealAniObservedのUnity回帰基準**。原作EXE一致や描画・音響の受入へ昇格しない。

## 意図した差分の昇格

通常検証はbaselineへ書き込まない。新しい基準が必要な場合は、まず `All` または `Golden` で候補runを生成し、既存基準との差分・変更理由・影響範囲をレビューする。承認後だけ以下を呼ぶ。

```csharp
WindomGoldenBaseline.CreateFromReviewedRun(candidateRunId, "new-version-id", approvalReason);
```

成功した全Golden run、空でない理由、実行時と現在のシナリオcatalogソースhash一致が必要。同じIDの上書きやパスを含むIDは拒否する。全ファイルを一時ディレクトリで作成してから新規ディレクトリへrenameする。生成だけでは参照先は変わらない。新規基準をレビュー対象に含めて `ApprovedBaselineId` を更新し、Regressionを実行する。古い基準は履歴として保持する。

## ANI棚卸しの範囲と意味

対象は `Windom_Data/Robo/{ELS_QT, ガンダムTR-1ヘイズル改, ザクIIS型}/Script.ani`。公開 `ani2.load()` を1ファイルずつawaitし、各actionでEditorへ制御を戻す。拡張actionもloaderが返す全件を走査する。ANIを保存・変換せず、前後のhashで不変を検査する。

出力はrun内の `inventory/inventory.json` とUTF-8の `inventory.csv`。RunProc/RunProc2の静的な命令出現を、action、script block、source ordinal、分岐、rawText、全引数、WEAPONPOINT、type、type62 subtype、p4〜p11とともに記録する。命令実行回数ではない。

- Initial / Timed / Sentinelを分離。Initial/Sentinelの存在をTestPlayでの実行証拠としない。
- 条件の真偽は評価せずthen/else両方を記録。変数・式・欠落引数を0に置換しない。整数literalの解決可否を別フィールドに保存する。
- `occurrenceKey` はANI hash＋action＋block種別/index＋分岐＋source ordinal。別位置の同じ命令を消さず、同一内容ANIのコピー由来重複を識別する。
- `patternKey` はcommandと全引数のkind/正規化literal。出現数、位置重複除外数、引数pattern数を混同しない。
- compiler診断は `diagnostics`、該当行は `blockHasDiagnostics` に記録する。Succeededは読込・棚卸し完走であり、全scriptの意味解析成功や原作互換を意味しない。

20actionごととファイル完了時にcheckpointを保存する。中止時は現在のloaderが返ってから停止し、採取済み行・完了ファイル数・Cancelledを残す。parser途中の強制中断はしない。再実行は新runで最初から開始する。

途中結果を読むWindowsプロセスが置換を一時的に拒否する場合は50msずつ最大10回非同期待機し、旧ファイルを保ったまま再試行する。job結果JSONにも同じ保存処理を使う。終了保存中はFinalizingとし、保存前の成功を返さない。解消しないI/O障害はFailed。JSON/CSVは各ファイル単位で置換し、両方まとめたトランザクションではないため、結果の正本はJSONとする。診断とblockHasDiagnosticsはJSONを参照する。

## 検証結果

最終統合run `20260905T152910402Z-28067300157447d4a816c1f4dd5dc26a` は **Succeeded**、active=false。 [結果JSON](../Logs/VerificationRuns/20260905T152910402Z-28067300157447d4a816c1f4dd5dc26a/result.json)、[baseline比較](../Logs/VerificationRuns/20260905T152910402Z-28067300157447d4a816c1f4dd5dc26a/baseline-comparison.json)、[棚卸しJSON](../Logs/VerificationRuns/20260905T152910402Z-28067300157447d4a816c1f4dd5dc26a/inventory/inventory.json)、[CSV](../Logs/VerificationRuns/20260905T152910402Z-28067300157447d4a816c1f4dd5dc26a/inventory/inventory.csv)、[hash照合要約](../Logs/VerificationWork/p1-03-20260905/final-summary.json)を保存した。

- Unity compile成功、Runtime 672 / HOD 374 assertions、Golden 10シナリオ各2回、固定baseline 10件一致。
- P1-03 focused 31 assertions、既存job protocol＋保存完了待ち21 assertions成功。意図した誤挙動候補を実際にjobへ渡してFailed・最初の差分保存を確認。失敗/中止probeのselection=0はテスト用であり、実機体の不具合結果に混ぜない。
- 実ファイル1件完了後の中止、途中結果保持、欠落ファイル失敗、3件再実行、読取ロック中の旧結果保持と解除後の完了を確認。棚卸し中にpublic statusから進捗を取得できた。
- 最終検証開始前/終了後のConsole Error/Exception 0。ソース・設定・ANI/AN2/SPTの98 hashが実行前後不変、固定baselineのmanifest＋10 JSONLも不変。
- 新規Editorスクリプト4件をmetaと対で追跡候補へ露出し、別ディレクトリ再構成のhash一致を確認。全体のクリーンcheckoutとcommit/pushは未実施。

| 機体 | loader判定 | action数 | RunProc/RunProc2出現 | compiler診断 |
| --- | --- | ---: | ---: | ---: |
| ELS_QT | LegacyAni | 219 | 269 | 8 |
| ガンダムTR-1ヘイズル改 | An2（ファイル名Script.ani） | 200 | 208 | 0 |
| ザクIIS型 | An2（ファイル名Script.ani） | 200 | 27 | 1 |
| 合計 | | 619 | 504 | 9 |

U-005jはザクIIS型action105のTimed block2/5に各4箇所（0始まり）、ordinal8〜11、branch=`root/if0/then`、全件 `RunProc2(0,62,0,11,30,30,20,0,0,0,0,0)`。8出現位置・1pattern、WEAPONPOINT=0、p4〜p11=`30,30,20,0,0,0,0,0`。block5は `UnexpectedEndIf ENDIF`（ordinal15）の診断付きで、該当4行もblockHasDiagnostics=true。条件の実行可否を確定したとは扱わない。

途中で発生したファイル置換エラーは、inventoryの失敗run `20260905T150834390Z-332d630e60ee45feb3045431c0371620`、旧job保存の失敗run `20260905T151918056Z-e1bc6f67b4ba4c52b6ad462d7d6e5447` / `20260905T151947076Z-6c08ac7619774f6683b818931672b3e5` に保持した。共通保存へ修正後の上記最終runと区別する。

## 次の範囲

P1-04は独立ゲームの起動・機体・session・入力・命中・資産のAPI/所有関係を確定する設計単位。U-005jの個数/lifecycle/texture/戦闘性の原作解析は別の未完了境界として保持する。棚卸しだけで推定AdapterをProjectileへ変えない。
