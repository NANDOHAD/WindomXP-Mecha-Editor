# Script.ani 取り扱い資料

この資料は、本Unityツールで読み書きしている `Script.ani` について、現行実装から確認できる構造と編集時の注意点をまとめたものです。ここでの `Script.ani` は、機体フォルダ内の `.ani` ファイル名として扱われるアニメーションコンテナを指します。実体は「機体パーツ構造」「HOD姿勢フレーム」「ANI内スクリプトブロック」を含むバイナリファイルです。ゲーム本来の命令仕様寄りのメモは [SCRIPT_ANI_COMMANDS_ORIGINAL_SPEC.md](SCRIPT_ANI_COMMANDS_ORIGINAL_SPEC.md) を参照してください。

本ツールでは `Script.ani` を `ani2` クラスでロードします。入力は `AN2` / 旧 `ANI` / 単体 `HOD` を受け付けますが、保存時は常に `AN2` 形式で書き出します。旧 `ANI` を開いて保存すると、同じファイル名でも中身は `AN2` へ変換されます。

## 関連ファイル

| ファイル | 役割 |
| --- | --- |
| `Assets/Scripts/ani2.cs` | `Script.ani` 全体の判定、ロード、保存。 |
| `Assets/Scripts/animation.cs` | アニメーション1件分の名前、初期スクリプト、HOD列、スクリプト列の読み書き。 |
| `Assets/Scripts/hod2v0.cs` | `AN2` の構造HOD。パーツ名、階層、初期TRS。 |
| `Assets/Scripts/hod2v1.cs` | アニメーション中のHODフレーム。パーツごとのTRS。 |
| `Assets/Scripts/UI_EditAni.cs` | Unity UI上のアニメーション、HOD、スクリプト編集。 |
| `Assets/Scripts/MechaAnimator.cs` | HOD補間、スクリプトブロック進行、発火タイミング。 |
| `Assets/Scripts/scriptInterpret.cs` | ANI内スクリプトの簡易パーサ。 |
| `Assets/Scripts/AniScriptRuntime.cs` | ANI命令をUnity側の状態、イベント、BURNERへ橋渡しする層。 |
| `Assets/Scripts/SptParser.cs` / `UI_SPT.cs` | `Script.spt` の `BURNERSET` 等を読み、`BURNER(id)` と接続する。 |
| `解析資料/*.txt` | 既存MOD向けの攻撃番号、テクスチャ番号、アクション番号、`@int` / `@float` などの解析メモ。 |

## ファイル形式の概要

### `AN2` 形式

`Script.ani` を本ツールで保存した場合の現行形式です。数値はリトルエンディアン、文字列は原則 `USEncoder.ToEncoding` 経由のShift-JIS互換バイト列です。

| 順序 | 型 | 内容 |
| --- | --- | --- |
| 1 | `char[3]` | シグネチャ。`AN2`。 |
| 2 | `byte[256]` | 構造HOD名。Shift-JIS、NULL詰め。 |
| 3 | `hod2v0` | 機体パーツ構造。`HD2` version `0`。 |
| 4 | `int32` | アニメーション数。 |
| 5 | `animation[]` | アニメーションレコードを数分繰り返す。 |

### 旧 `ANI` 形式

旧形式は読み込み専用の互換経路として扱われています。

| 順序 | 型 | 内容 |
| --- | --- | --- |
| 1 | `char[3]` | シグネチャ。`ANI`。 |
| 2 | `byte[256]` | 構造HOD名。 |
| 3 | `hod1` | 旧構造HOD。読み込み後 `hod2v0` へ変換。 |
| 4 | `animation[200]` | 旧アニメーションレコードを200件固定で読む。 |

旧 `ANI` の各HODフレーム名は30バイト固定です。ロード後の保存は `AN2` になります。

### 単体 `HOD`

シグネチャが `HOD` の場合、単体HODを「1アニメーション、1フレーム、スクリプトなし」として読みます。`Script.ani` 編集用途というより、HOD互換ロード用です。

## アニメーションレコード

`AN2` 内の1アニメーションは次の順で保存されます。

| 順序 | 型 | 内容 |
| --- | --- | --- |
| 1 | `byte[256]` | アニメーション名。Shift-JIS、NULL詰め。 |
| 2 | `int32` | 初期スクリプト本文のバイト長。0なら本文なし。 |
| 3 | `byte[]` | 初期スクリプト本文。長さは前項。 |
| 4 | `int32` | HODフレーム数。 |
| 5 | `hod2v1[]` | HODフレームを数分繰り返す。各フレームはファイル名長、ファイル名、`HD2` version `1`本体を持つ。 |
| 6 | `int32` | スクリプトブロック数。 |
| 7 | `script[]` | スクリプトブロックを数分繰り返す。 |

### `script` ブロック

`animation.cs` の `script` 構造体に対応します。

| 順序 | 型 | UI名 | 実装上の意味 |
| --- | --- | --- | --- |
| 1 | `int32` | `fLength` | `unk`。現在のスクリプトインデックスを維持するtick数として使われる。 |
| 2 | `float32` | `hDuration` | `time`。1tickごとにHOD補間値 `frameTime` へ加算される量。 |
| 3 | `int32` | なし | スクリプト本文のバイト長。 |
| 4 | `byte[]` | `scriptText` | スクリプト本文。Shift-JIS互換。 |

保存時、スクリプト本文と初期スクリプトは `\n` の直前に `\r` がない場合、自動で `\r\n` に正規化されます。読み込み時は本文をそのままUnicode文字列化します。

## HODフレームとの関係

スクリプトブロック列は、HODフレーム列を時間方向に進めるためのタイムラインでもあります。

- `MechaAnimator` はUnityの `FixedUpdate()` 1回をANI 1tickとして扱います。既定のUnity設定では0.02秒、つまり50Hzです。
- 毎tick `scriptTime` が増え、`scriptTime >= unk` になると次のスクリプトブロックへ進みます。
- `frameTime += time` で現在HODと次HODの補間率を進めます。
- `frameTime >= 1` になると `frameIndex` が1つ進み、`frameTime` から1を引きます。
- `time` が大きすぎると1tickで複数フレーム分進む設計にはなっていません。互換性確認が取れていない値は避けてください。
- HOD補間はパーツごとの `position` / `rotation` / `scale` に対して `Vector3.Lerp` / `Quaternion.Lerp` を使います。

実装上、スクリプト発火は `runner.Update()` の後に行われます。そのため、`unk` が極端に小さいブロック、特に先頭ブロックの `unk=1` は、プレビュー上では同tick内で次ブロックへ進んだ後のインデックスが発火対象になる点に注意してください。

## スクリプト発火

`MechaAnimator` はアニメーション再生開始ごとに次を行います。

1. 初回tickで `squirrelInit` を1回だけ処理する。
2. `scriptIndex` が初回または前回から変わったとき、そのブロックの本文を処理する。
3. `executeAniScripts` が無効なら、本文ログやシンボル収集以外の実行は行わない。
4. `dumpSymbolsToFile` が有効なら、未知命令などを `Application.persistentDataPath/ani_script_symbols_report.txt` へ出力する。

スクリプト本文は「ブロックに入った時に1回」処理されます。ブロック滞在中に毎tick実行する完全なゲーム本体挙動は、現状まだ再現途中です。

## スクリプト本文の構文

現行の `scriptInterpreter` は完全なSquirrel処理系ではなく、ANI命令を拾うための簡易パーサです。

### コメント

行頭が `'` の行はコメントとして無視されます。行途中の `'` 以降をコメントとして切り捨てる処理は、ANIスクリプト側にはありません。

```text
'これはコメント
```

### 関数呼び出し

`Name(arg1,arg2,...)` 形式を処理します。引数は単純にカンマで分割されるため、文字列内カンマや入れ子式は扱えません。

```text
WeaponAttack(0,3,0,30);
RunProc2(0,62,0,3,350,350,0,0,0,0,0,0);
BURNER(1);
```

`AniScriptRuntime` は `Scr_` 付きと無しの両方を登録します。例えば `Scr_Move(...)` と `Move(...)` は同じ命令として受けます。

### 代入

`name=value;` 形式を処理します。ただし、現在の実行系で有効に処理されるのは登録済みsetterだけです。解析資料にある `+=` / `-=` / `*=` / `/=` や `@int[...]` / `@float[...]` のようなゲーム本体変数アクセスは、現行パーサではまだ完全には実行されません。シンボル収集上の未知変数として扱われる可能性があります。

```text
AttackFlag=3;
LaserReflect=20;
```

解析資料には `Move=(0,STOP,0.21f)` のような表記例もありますが、現行実装の登録命令は `Move(...)` / `Scr_Move(...)` 形式です。`f` サフィックス付き数値も `float.TryParse` では数値化されない環境があるため、ツール上で実行確認する場合は `0.21` のように書く方が安全です。

### IF

簡易的に `IF(lhs,op,rhs)` 形式を処理します。対応演算子は `==` / `>=` / `<=` です。`ELSE;` で条件を反転し、`ENDIF;` でブロックを終了します。

```text
IF(1,==,1);
  BURNER(1);
ELSE;
  BURNER(2);
ENDIF;
```

一般的な `IF(a == b)` 形式や複雑な論理式は、現行実装では対象外です。

## 現在登録されている主な命令

`AniScriptRuntime` で登録される命令は、`Scr_` 付き別名も受け付けます。以下は「Unity側で何らかの受け皿がある」命令であり、ゲーム本体と同じ効果が完全再現されているという意味ではありません。

| 命令 | 現行ツールでの扱い |
| --- | --- |
| `Move` / `Force` | `Vector3` 状態へ保存。挙動反映は移植途中。 |
| `AttackPow` / `AttackForce` / `AttackForceY` / `AttackDownF` | 攻撃関連の状態値へ保存。 |
| `LockArmTarget` / `LockBodyTarget` | スロット、enable、角度候補を状態へ保存。 |
| `ShildGuard` | 防御フラグ候補を保存。スペルは既存表記に合わせて `Shild`。 |
| `ShotTurnAng` / `TurnMoveAng` | 角度値を状態へ保存。 |
| `AddExGauge` / `AddEnergy` | ゲージ/エネルギー増減候補を保存。 |
| `Snd` / `Voice` / `CamEffect` | イベントを発行。購読側がなければ実効果なし。 |
| `RunProc` / `RunSubScript` / `CatchChara` | イベントを発行。 |
| `SetExtParam` | `extParams[index] = value` として保存し、イベントを発行。 |
| `GvEnable` | フラグを保存。 |
| `GoScriptIndex` / `GoPoseIndex` / `ChangeAnime` | 値を保存し、イベントを発行。ジャンプ実処理は購読側次第。 |
| `BURNER` | `Script.spt` の `BURNERSET` と対応し、該当ParticleSystemの点火要求に使う。 |
| `WeaponAttack` / `WeaponAttack2` / `RunProc2` / `ATTACK` | 受け皿はあるがTODO。解析資料の番号表を参照して今後移植する領域。 |
| `AttackFlag` / `LaserReflect` / `SwordCancel` / `BoostDashMode` / `AttackDelay` / `AnimeLoop` / `SwordEnable` / `ChangeWeapon` / `MoveLock` | 受け皿はあるがTODO。 |
| `Rnd` / `LocalRnd` / `ExecScriptEveryTime` / `Sub_LRKey` / `vF_Multi` / `BunerOut` | 受け皿はあるがTODO。 |

未登録命令は、`logUnknownScriptSymbols` や `dumpSymbolsToFile` を有効にして調査してください。未知命令を削除してしまうと、後からゲーム本体挙動を移植できなくなるため、編集時は温存を基本にしてください。

## `Script.spt` との連携

`Script.ani` と `Script.spt` は別ファイルです。

- `Script.ani` は通常のバイナリとして直接読みます。`CypherTranscoder` で復号しません。
- `Script.spt` は `CypherTranscoder.Transcode()` で復号/再暗号化し、テキストとして編集します。
- `Script.ani` 側の `BURNER(id)` は、`Script.spt` 側の `BURNERSET(id, frameName, scale, direction)` とIDで対応します。
- `BURNERSET` の `frameName` はUnity上のTransform名、または `frameName + ".x"` と照合されます。
- `scale <= 0` の `BURNERSET` はエフェクト生成対象外です。
- 方向は `UP` / `DOWN` / `FORWARD` / `BACK` / `LEFT` / `RIGHT` を受けます。

例:

```text
// Script.spt
BURNERSET(1, B3, 1.0, UP)

// Script.ani
BURNER(1);
```

`executeAniScripts` 有効時、スクリプトブロック切り替わりごとにBURNER要求セットをリセットし、そのブロックで要求されたIDだけを点火、要求されなかったIDは停止します。

## 解析資料の読み方

ルート直下の `解析資料` フォルダには、既存コミュニティ由来の調査メモがあります。現行ツールでの確定仕様ではなく、移植時の参考資料として扱ってください。

| 資料 | 主な内容 |
| --- | --- |
| `解析資料/Guide.txt` | `WeaponAttack` / `WeaponAttack2` / `RunProc` / `RunProc2` の武器・エフェクト番号と引数例。 |
| `解析資料/Guide 2.txt` | `@float[...]` / `@int[...]` の用途候補、入力状態、演算記号のメモ。 |
| `解析資料/Guide 3.txt` | アニメーション番号の共通的な割り当て例。0-99前後が基本動作、100以降が射撃/特殊武器系。 |
| `解析資料/GUIDE4.txt` | 攻撃タイプ番号、テクスチャ番号の一覧。 |
| `解析資料/GUIDE日本語.txt` | `ATTACK`、`AttackFlag`、`LaserReflect`、`RunProc2`、`Move` などの日本語メモと例。 |

解析資料の引数説明は、ゲーム本体での挙動推定を含みます。Unityツール側では未実装/TODOの命令が多いため、資料の例を書けば必ずプレビューで再現されるわけではありません。

## 編集時の注意点

- 文字コード変換は必ず `USEncoder.ToEncoding.ToSJIS()` / `ToUnicode()` 経由にしてください。`Encoding.GetEncoding(932)` への置換は避けます。
- 既存の未知命令、未知変数、`@int` / `@float` 参照は削除しないでください。現行プレビューで無効でも、ゲーム本体や将来の移植で意味を持つ可能性があります。
- スクリプトブロックの順序を変えると、発火タイミングとHOD補間タイミングが同時に変わります。
- HODフレームの追加/削除とスクリプトブロックの `time` / `unk` はセットで確認してください。片方だけ変更すると、ポーズ進行と命令発火がずれます。
- `Script.ani` 保存は `AN2` 形式への上書きです。オリジナルの旧 `ANI` を保持したい場合は、保存前にバックアップを取って比較してください。
- `Script.spt` の `BURNERSET` を変更した場合は、`BURNER(id)` のID、ボーン名、エフェクト方向を合わせて確認してください。

## 最低限の確認手順

1. 対象機体の `Script.ani` をロードできることを確認する。
2. アニメーション一覧、HOD一覧、スクリプト一覧が表示されることを確認する。
3. 編集対象アニメーションをプレビューし、HOD補間が破綻しないことを確認する。
4. `executeAniScripts` を使う場合は、Consoleで未知命令ログと実行エラーを確認する。
5. `BURNER(id)` を扱う場合は、同じ機体フォルダの `Script.spt` をロードし、`BURNERSET` のボーンにParticleSystemが付くことを確認する。
6. 保存後に再ロードし、アニメーション数、HOD数、スクリプト本文、主要プレビューが維持されることを確認する。
