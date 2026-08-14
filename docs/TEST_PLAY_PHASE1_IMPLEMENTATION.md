# テストプレイ再設計 Phase 1 実装記録

## 結果

Phase 1では、ANIテキストを型付き命令へ変換する共通コンパイラ、実ANIデータからのblock変換、主／副／サブtrack scheduler、決定的なtickトレース基盤を追加しました。

`TestPlayController`のpublicフィールド、`StartTestPlay()` / `StopTestPlay()`、Inspector参照、UnityEvent、シーン構造は変更していません。現行テストプレイの実行入口は`TestPlayScriptVM`のまま維持し、同クラス内部を型付きプログラムのキャッシュと実行へ置き換えました。

Phase 0で定義した根拠区分、未解決事項、トレース契約は [TEST_PLAY_PHASE0_REDESIGN_SPEC.md](TEST_PLAY_PHASE0_REDESIGN_SPEC.md) を参照してください。

## 追加・変更した責務

| ファイル | 責務 |
| --- | --- |
| `TestPlayAniProgram.cs` | command、assignment、condition、operand、diagnosticの型付き中間表現、共通コンパイラ、実行器。 |
| `TestPlayAniAnimationProgram.cs` | 既存`animation` / `script`から初期プログラム、block時間、HOD進行量、終端を変換する。 |
| `TestPlayAniTrackScheduler.cs` | 主／副／サブtrackのblock、channel、残りtick、repeat、loop、jump、終了、traceを独立管理する。 |
| `TestPlayScriptVM.cs` | 既存公開APIを維持し、文字列配列ではなく型付きプログラムをキャッシュ・実行するFacade。 |
| `scriptInterpret.cs` | 編集プレビューの登録済みハンドラーとシンボル収集を、同じ型付きコンパイラへ接続する。 |
| `TestPlayPhase1Verification.cs` | 中間表現、実行時参照、診断、実ANI変換、プレビュー共有、track、repeat、loop、traceを検証する。 |

## 型付きANI中間表現

コンパイル時に次を分類します。

| 種類 | 保持内容 |
| --- | --- |
| Command | 命令名、型付き引数、元テキスト、出現順。未知命令も削除しない。 |
| Assignment | 左辺、`=` / `+=` / `-=` / `*=` / `/=`、型付き値。 |
| Conditional | 左辺、6比較演算子、右辺、then／elseの子命令。 |
| Operand | 数値、シンボル、`STOP`、`@int`参照、`@float`参照。 |
| Diagnostic | 対応しない`ELSE` / `ENDIF`、不足した`ENDIF`、括弧不整合。 |

`@int` / `@float`はコンパイル時の数値へ固定せず、実行時に`TestPlayStateTable`から解決します。同じコンパイル結果を異なるtick・状態で安全に再利用できます。

未知のcommand／assignmentは中間表現に残り、TestPlay側では既存ハンドラーのdefault診断、編集プレビュー側ではunknown symbol台帳へ渡されます。

## 編集プレビューとの共有

`MechaAnimator`が利用する`scriptInterpreter.runScript()`も`TestPlayAniCompiler`を使います。

- `AniScriptRuntime.Register()`が登録する既存関数・変数コールバックは維持する。
- 実行時は型付きIFの選択された分岐だけを呼び出す。
- シンボル収集時は現在状態に依存せずthen／else両方を走査する。
- 6比較演算子を共通化する。
- 同一スクリプト文字列のコンパイル結果をキャッシュする。
- 既存の`InterpretLine()`公開入口は互換用として維持する。

これにより、通常プレビューとTestPlayが別々の文分割・IF構文を持つ状態を解消しました。命令の効果そのものは引き続き`AniScriptRuntime`と`TestPlayController`の各アダプターが担当するため、未確定命令を誤って同一挙動とみなすものではありません。

## track scheduler

trackは次の独立状態を持ちます。

```text
kind: Main / Secondary / Sub
channel
blockIndex
remainingTicks
repeatInterval / repeatCounter
active / finished / pendingEntry
executionCount
```

更新順は`Main -> Secondary -> Sub`で固定しています。各trackは別々のblockと残りtickを持ち、一方の進行・終了・jumpが他方を変更しません。

block entryでプログラムを1回実行し、`ExecScriptEveryTime(n)`相当のrepeatは`n + 1` tick周期になります。`999999999`は実行可能命令ではなく明示的なsentinelとして保持し、非loop時は終了、loop時は先頭の実行可能blockへ戻ります。

`TestPlayAniAnimationCompiler`は既存`script.unk`を最低1tickのblock時間、`script.time`を1tickあたりのHOD進行量として保持します。終端blockは時間0、programなしで変換します。

## tickトレースのPhase 1範囲

`TestPlayAniTickTrace.ToJsonLine()`は、現時点で次を決定的な順序で出力します。

- tick番号
- 各trackのkind、channel、block、remaining、repeat、active、finished
- block entry、execute、repeat、jump、loop、finish、diagnosticイベント

Unity Instance ID、`Time.deltaTime`、現在Cultureには依存しません。Phase 0で定義したinput、stateDelta、motion、resources、combat、presentationは、各責務をCoreへ移すPhase 2～4で同じtraceレコードへ追加します。

## 互換性と段階移行

現在の`TestPlayController`は、既存の`scriptIndex` / `scriptTick`と個別アクション処理を引き続きゲームプレイの正本としています。新schedulerは検証済みCoreですが、まだ実機体の移動結果を直接変更しません。

これは二重実行を避け、次の順で移行するための境界です。

1. `animation`を`TestPlayAniAnimationProgram`へ一度だけ変換する。
2. 現行block進行とschedulerのtrack traceを同じ入力で比較する。
3. 主／副チャンネルのblock進行が一致したアクション群からschedulerを正本へ切り替える。
4. `TestPlayController`のpublic Facadeを維持したまま、action／motion Coreへ接続する。

## 検証

Unityメニュー`Tools > WindomXP > Test Play > Run Runtime Verification`へPhase 1検証を統合しました。

現在の結果は236 assertions成功、Console Error 0です。Phase 0開始時の198件をすべて維持し、次の38件を追加しています。

- 型付きcommand／assignment／IF／operand
- 実行時の状態参照とコンパイルキャッシュ
- flow interruption
- 構文診断と未知命令保持
- 実`animation` / `script`からのblock／sentinel変換
- 編集プレビューの共通コンパイラ利用と全分岐シンボル収集
- Main／Secondary／Subの独立進行
- `ExecScriptEveryTime(n)`の`n + 1`周期
- sentinel終了、loop、jump診断
- tick traceの決定性とイベント順

## Phase 2への入口

Phase 2では、現行`TestPlayController`のアクション・移動状態を一度に置き換えず、最初に待機／歩行、次にジャンプ／空中、ステップ、ブーストの順でCoreへ移します。

各移行単位では、Phase 0の`GT-001`～`GT-006`に対応する入力列を固定し、旧経路と新Coreのaction、track、Move、Force、velocity、接地、Generator差分をtickごとに比較してから正本を切り替えます。
