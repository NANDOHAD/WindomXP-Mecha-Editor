# Script.ani 命令仕様メモ

この資料は、`Script.ani` 内スクリプト命令について、ゲーム本来の挙動を推測込みでまとめたものです。Unity側で現在登録されているか、再現されているかとは切り離して読んでください。

根拠は、オリジナル実行ファイルの最新逆コンパイル擬似コード [`WindomXP_orig_decompiled.c`](WindomXP_orig_decompiled.c)、ルート直下の `解析資料/*.txt`、同梱サンプル機体 `Windom_Data/Robo/ガンダムTR-1ヘイズル改/Script.ani` の命令出現状況、既存コード内の移植コメントです。ロード・パーサー・実行器の詳細は [SCRIPT_ANI_ORIGINAL_DECOMPILED_ANALYSIS.md](SCRIPT_ANI_ORIGINAL_DECOMPILED_ANALYSIS.md) にまとめています。未確定情報は「推定」と明記します。

## 確度の見方

| 確度 | 意味 |
| --- | --- |
| A | 逆コンパイル擬似コードで構文や書き込み先を直接追跡できる、または解析資料とサンプル実データの両方で確認できる。 |
| B | 解析資料またはサンプル実データで確認できるが、引数意味に推測が残る。 |
| C | 命令名、周辺文脈、移植コメントからの推測。実ゲームでの検証が必要。 |

## 基本文法

Script.ani の本文はSquirrel風の表記ですが、オリジナル実行ファイルでは固定トークンを順番に照合する専用パーサーで処理されます。汎用Squirrel処理系として扱わないでください。

| 要素 | 例 | 推定仕様 |
| --- | --- | --- |
| 文末 | `Snd(1);` | 原則セミコロンで区切る。セミコロン欠落でゲーム本体がエラーを出すという解析メモあり。 |
| コメント | `' comment` | 行頭 `'` はコメント。 |
| 関数命令 | `WeaponAttack(0,3,0,30);` | 命令名と引数列。文字列風の引数は `Voice(Damage);` のように裸シンボルの場合がある。 |
| 代入命令 | `AttackFlag = 3;` | 状態値や内部変数を変更する。 |
| ベクトル代入 | `Move=(0,STOP,0);` | 3成分指定。原作実行ハンドラーでは数値`0`が現在値を維持し、`STOP`が対象軸を0へ停止する。 |
| 内部変数 | `@int[155]=1;` | ゲーム側の整数/浮動小数変数テーブル参照。 |
| 演算代入 | `@float[100]-=100;` | 解析資料では `/=` `*=` `-=` `+=` `=` が使用可能とされる。 |
| 条件分岐 | `IF(@int[151],==,0);` | `IF(lhs, op, rhs);` 型。`==` / `>=` / `<=` / `!=` / `>` / `<` と `ELSE;` / `ENDIF;` を扱う。 |

サンプル `Script.ani` では、関数命令として `BURNER`、`IF`、`RunProc2`、`LockBodyDownTarget`、`WeaponAttack`、`ATTACK`、`MoveLock`、`Snd`、`Voice`、`ExecScriptEveryTime`、`AttackDelay`、`RunProc`、`ChangeWeapon` などが出現します。代入命令としては `GvEnable`、`Move`、`ShotTurnAng`、`Force`、`ShildGuard`、`SwordCancel`、`AttackFlag`、`CamEffect`、`vF_Multi`、`AnimeLoop`、`@int[99]`、`@int[155]` などが確認できます。

## 内部変数テーブル

解析資料 `Guide 2.txt` による `@int[]` / `@float[]` の用途候補です。確度は資料由来で、実ゲーム検証は別途必要です。

| 変数 | 確度 | 推定用途 |
| --- | --- | --- |
| `@float[99]` | B | 自機とロック中敵機の距離。例では `@int[99]=100` とも書かれており、型の扱いに曖昧さあり。 |
| `@float[100]` | B | エネルギー値。例: `@float[100]-=100`。 |
| `@int[100]` | B | 噴射/ブースト値。`@float[100]` と同様の使い方とされる。 |
| `@float[105]` | B | カメラ/座標系のX軸回転。 |
| `@float[106]` | B | カメラ/座標系のY軸回転。 |
| `@float[108]` | B | カメラ/座標系のZ軸回転。 |
| `@float[109]` | B | 左右位置。正方向が右、負方向が左。 |
| `@float[110]` | B | 上下位置。正方向が上、負方向が下。 |
| `@float[111]` | B | 前後位置。正方向が前、負方向が後ろ。 |
| `@int[150]` | A | 原作キャラクター状態`+0xBA8`。`0`が接地、`1`が空中。通常攻撃終了処理`FUN_004d8e30` / 格闘更新`FUN_004d9030`は0なら接地復帰6、1なら空中停止8へ分岐する。 |
| `@int[151]` | A | 現在実行中のANIチャンネル。`FUN_004b8250`の`param_4`が`+0xAAC`へ書かれ、内部変数表のint 151が同アドレスを参照する。主チャンネルは`0`、副チャンネルは`1`で、格闘ブロックは主に0、持替え18/68は1を条件にする。 |
| `@int[152]` | B | 武器保持状態。`0` = 銃、`1` = 剣。 |
| `@int[153]` | B | ヒット判定。 |
| `@int[154]` | B | 敵機状態。 |
| `@int[155]` | B | EXレベル。サンプルで `@int[155]=1` が出現。 |
| `@int[156]` | B | 自機状態。 |
| `@int[157]` | B | 防御中に攻撃された判定。 |
| `@int[158]` | B | 被弾時間、または無敵時間相当。 |
| `@int[180]` - `@int[188]` | C | チャージ解除系入力/状態。解析資料ではボタン対応とされるが詳細未整理。 |
| `@int[190]` | B | 方向入力。`2`=後、`4`=左、`6`=右、`8`=前。 |
| `@int[191]` | B | 噴射/ブーストキー。 |
| `@int[192]` | B | 射撃ボタン。 |
| `@int[193]` | B | 格闘ボタン。 |
| `@int[194]` | B | 防御ボタン。 |
| `@int[195]` | B | ロックボタン。 |
| `@int[196]` | B | 特殊武器1。 |
| `@int[197]` | B | 特殊武器2。 |
| `@int[198]` | B | 特殊武器3。 |

空きアドレスは一時変数として使えるという解析メモがあります。例として旧版では `@int[11]` が空きとして使われていたようです。

## フロー制御・アクション遷移

| 命令 | 確度 | 推定仕様 |
| --- | --- | --- |
| `IF(lhs,op,rhs);` | A | 条件分岐。演算子は少なくとも `==`、`>=`、`<=` が使われる。 |
| `ELSE;` / `ENDIF;` | A | `IF` ブロックの分岐/終了。 |
| `GoScriptIndex(index);` | C | 現在アニメーション内のスクリプトブロック番号へジャンプする命令と推定。ループや分岐復帰に使う可能性。 |
| `GoPoseIndex(index);` | C | HOD/ポーズ番号へジャンプする命令と推定。 |
| `ChangeAnime(id);` | B | 指定アクション/アニメーション番号へ遷移。番号はアクション番号表と対応すると推定。 |
| `AnimeLoop=1;` | B | 現在アニメーションをループさせるフラグと推定。 |
| `ExecScriptEveryTime(interval);` | A | 現在ブロックの再実行を有効化する。`0`は毎tick、`n`は概ね`n + 1` tick周期。 |
| `RunSubScript(...)` | C | サブスクリプト呼び出し。詳細未確認。 |
| `SwordCancel=actionId;` | B | 剣/格闘ヒット時などのキャンセル先アクションIDを設定すると推定。サンプルでは `132`、`133` など格闘コンボ番号が入る。 |
| `AttackDelay(slot, ticks);` | B | 武器/攻撃の再使用時間。解析資料では第2引数が再使用時間。 |

## アクション番号

解析資料および実機データからのアクション番号定義です。
ID `0`〜`49` は**通常（射撃系）状態**での基本アクション、ID `50`〜`99` は**抜刀（格闘系）状態**での基本アクションに対応しており、0インデックスで定義されます（ID `0` / ID `50` が「立ち」に対応。抜刀状態のIDは「通常ID + 50」となります）。

### 基本動作アクション（通常 / 抜刀対比）

| 通常(射撃) ID | 抜刀(格闘) ID | アクション内容 | 備考 |
| --- | --- | --- | --- |
| `0` | `50` | 立ち | |
| `1` | `51` | 歩き | |
| `2` | `52` | 未設定 | |
| `3` | `53` | ジャンプ開始 | |
| `4` | `54` | 空中移動 | |
| `5` | `55` | 着地 | |
| `6` | `56` | 着地（ステップ版） | |
| `7` | `57` | 上昇 | |
| `8` | `58` | 空中停止 | |
| `9` | `59` | 左ステップ | |
| `10` | `60` | 右ステップ | |
| `11` | `61` | 前ステップ | |
| `12` | `62` | 後ろステップ | |
| `13` | `63` | 被ダメージ | |
| `14` | `64` | 被ダメージ（空中版） | |
| `15` | `65` | 被ダメージ（ぶっ飛び） | |
| `16` | `66` | ダウン | |
| `17` | `67` | 起き上がり | |
| `18` | `68` | 武器切り替え | ID18: 格闘系へ切り替え / ID68: 射撃系へ切り替え |
| `19` | `69` | 防御 | |
| `20` | `70` | 受身 | |
| `21` | `71` | 防御ダッシュ | |
| `22` | `72` | ブーストダッシュ | |
| `23` | `73` | 変形 | |
| `24` | `74` | 変形解除 | |
| `27` | `77` | HIT回避 | |

※ 未記載のID（25-26, 28-49, 75-76, 78-99）は空き、機体固有、または未調査です。

### 攻撃・特殊アクション（ID 100以降）

| 番号 | 推定アクション | 備考 |
| --- | --- | --- |
| `100` | 通常射撃 | |
| `103` | ファンネル/浮遊砲射撃 | |
| `104` | 特殊武器1 | |
| `105` | 特殊武器2 | |
| `106` | 飛行射撃 | |
| `109` | 特殊武器3 | |
| `110` | 飛行中特殊武器1 | |
| `111` | 飛行中特殊武器2 | |
| `112` | 飛行中特殊武器3 | |
| `113` | 変形飛行中特殊武器 | |
| `116` | 変形飛行中特殊武器1 | |
| `117` | 変形飛行中特殊武器2 | |
| `118` | 変形飛行中特殊武器3 | |
| `130` | 格闘誘導開始 | |
| `131` - `135` | 正面格闘/直進格闘 1-5 | |
| `136` - `140` | 前格闘 1-5 | |
| `141` - `145` | 左格闘 1-5 | |
| `146` - `150` | 右格闘 1-5 | |
| `151` - `155` | 後格闘 1-5 | |
| `181` | 捕獲された状態 | |

## 移動・力・照準系

| 命令 | 確度 | 推定仕様 |
| --- | --- | --- |
| `Move=(x,y,z);` / `Move(x,y,z);` | A | 移動速度または入力方向を設定。サンプルでは `Move=(0,STOP,0);`、`Move=(0,0,0.08f);` が出現。解析資料では第3成分が前後移動速度のように扱われ、`0.99f` 付近が上限候補とされる。 |
| `Force=(x,y,z);` / `Force(x,y,z);` | B | 加速度/外力を設定。サンプルでは上方向らしき `Force=(0,0.04f,0);` が出現。 |
| `MoveLock();` / `MoveLock=...` | B | 移動入力をロックする命令と推定。 |
| `BoostDashMode(...)` | C | ブーストダッシュ状態へ移行、またはブースト移動補正。 |
| `ShotTurnAng=value;` | B | 射撃時の旋回可能角度。サンプルでは `ShotTurnAng = 20;`。 |
| `TurnMoveAng=value;` | C | 移動旋回角度または旋回中移動角。 |
| `vF_Multi=value;` | B | 速度/前進ベクトルの倍率と推定。サンプルでは `0.9`、`0.8`。 |
| `Sub_LRKey(...)` | C | 左右入力の補助判定/ゲート。 |
| `LockBodyUpTarget(a,b);` | A | 上半身/機体上側をロック対象へ向ける照準命令と推定。射撃系の条件として資料に出る。 |
| `LockBodyDownTarget(a,b);` | A | 下半身/機体下側をロック対象へ向ける照準命令と推定。サンプルで頻出。 |
| `LockBodyTarget(...)` | B | Body全体のロック照準。Up/Downの汎用形と推定。 |
| `LockArm1Target(a,b);` | A | 腕1をロック対象へ向ける。サンプルに `LockArm1Target(-1,-1);`。 |
| `LockArm2Target(a,b);` | A | 腕2をロック対象へ向ける。 |

`Lock...Target(-1,-1)` の `-1` は自動追尾、制限なし、またはデフォルト角度を意味する可能性があります。解析資料では一部攻撃タイプの条件として `LockBodyUpTarget` が挙がっています。

## 攻撃判定・格闘系

| 命令 | 確度 | 推定仕様 |
| --- | --- | --- |
| `ATTACK(power, down, force, forceY);` | A | 原作パーサーが `AttackPow`、`AttackDownF`、`AttackForce`、`AttackForceY` の4命令へ順番に展開する。第1・第2引数は整数、第3・第4引数は浮動小数。 |
| `AttackFlag=value;` | A | 攻撃判定の状態/属性フラグ。サンプルでは `0`、`2`、`8`。解析資料では `3` 例もあり。ビットフラグの可能性が高い。 |
| `AttackPow(value);` | A | 攻撃力を変更する。原作の `ATTACK` 第1引数から生成される。 |
| `AttackForce(value);` | A | 吹き飛ばし/押し出し力。原作の `ATTACK` 第3引数から生成される。 |
| `AttackForceY(value);` | A | 縦方向の吹き飛ばし力。原作では `ATTACK` 第4引数から内部生成され、独立した入力トークンは確認できない。 |
| `AttackDownF(value);` | A | ダウン値/ダウンフラグ。原作の `ATTACK` 第2引数から生成される。 |
| `ShildGuard=value;` | A | 機体`+0xB58`の1 byteへ格納する前方防御値。綴りは `Shield` ではない。type 1／11／57が別の前方dot閾値で参照する。 |
| `LaserReflect=value;` | A | `Scr_LaserReflect`が機体`+0xB68`の低1 byteへ格納する。type 11は符号付きcharで読み、0～99のrollと厳密な`<`で比較し、AttackFlag bit `0x02`時だけ反射する。実ELS_QTの各形式では`20`を12件、`00`を1件確認。 |
| `SwordEnable(...)` | C | 剣モデルまたは剣判定の表示/有効化。 |
| `ChangeWeapon(SWORD/GUN);` | A | 原作ANIコンパイラは`GUN`をtype 51、`SWORD`をtype 52へ変換する。保持武器`@int[152]`とSPT武器モデル表示を同時に切り替える。 |

## 射撃・武器生成系

解析資料では、引数が5個以下の武器は `WeaponAttack`、より細かいデータが必要な武器は `RunProc2` を使う、と説明されています。第1引数は「順番」「何個目の武器文か」を示すローカルスロット、第2引数は武器タイプ番号、第3引数は発射口/WeaponPoint、以降はタイプ別パラメータと読むのが自然です。

### `WeaponAttack`

```text
WeaponAttack(order, weaponType, weaponPoint, energy[, sptPosition]);
```

| 引数 | 確度 | 推定仕様 |
| --- | --- | --- |
| `order` | B | 同一スクリプト内の武器文番号/スロット。複数発射時の識別に使う可能性。 |
| `weaponType` | A | 武器タイプ番号。下の武器タイプ表を参照。 |
| `weaponPoint` | A | 発射口/WeaponPoint番号。`Script.spt` のWEAPONPOINT系設定と対応すると推定。 |
| `energy` | A | 消費エネルギー。 |
| `sptPosition` | B | SPT上の追加位置指定。資料では一部武器にのみ登場。 |

例:

```text
WeaponAttack(0,3,0,30);     // 小型ミサイル
WeaponAttack(0,10,0,30,1);  // 長距離ミサイル、SPT位置1
```

### `WeaponAttack2`

```text
WeaponAttack2(order, weaponType, weaponPoint, energy, p4, p5, ...);
```

`WeaponAttack` の拡張形と推定されます。資料では誘導ミサイル `19`、召喚火球/電気球 `30` などで使用例があります。第5引数以降はタイプ別です。

### `RunProc`

```text
RunProc(order, procType, weaponPoint);
```

短い補助プロシージャ呼び出しと推定されます。資料では `53` 飛行風圧、`54` 風圧の飛行時間に使われています。

### `RunProc2`

```text
RunProc2(order, procType, weaponPoint, p0, p1, p2, p3, p4, p5, p6, p7, p8);
```

武器/エフェクト生成の詳細版です。末尾がすべて0の場合は、省略できるという解析メモがあります。`procType` ごとに引数の意味が大きく変わります。

代表例:

```text
RunProc2(0,1,0,150,30,100,20,0,0,0,0,0);
RunProc2(1,55,1,200,12,13,0,0,0,0,0,0);
RunProc2(1,57,1,200,0,8,9,0,0,0,0,9);
RunProc2(0,62,28,3,300,300,0,0,0,0,0,0);
```

type 1は`FUN_004e8310`から生成される`LZ_Beam`です。引数は次のとおり確定しています。

| 引数 | 用途 |
| --- | --- |
| `weaponPoint` | 生成時に複製するWEAPONPOINT行列。 |
| `p0` | Script.spt `Energy`側の消費量。不足時は生成しない。Generator移動ゲージとは別。 |
| `p1` | trail位置履歴の最大点数。 |
| `p2` | `p2/100`が60 Hz 1 tick当たりの前進距離。 |
| `p3` | `p3/100`が表示幅。原作対象側形状の命中半径ではない。 |
| `p4` | 距離依存旋回角へ掛ける百分率補正。 |
| `p5` | texture ID。 |
| `p6` | low byteで保持するtrail／表示mode。厳密な表示意味は未確定。 |
| `p7`, `p8` | type 1 handlerでの利用を確認できない。 |

targetが発射方向20度以内なら生成時にtargetへ照準し、距離100未満では
`(1-distance/100)*0.4*(1+p4/100)`度/tickを最大旋回角にします。毎tickは前進してから旋回し、
有効期間は固定300 tickです。trail線分と対象側複合形状を照合し、同じbeamは同じ対象へ1回だけ命中します。
命中時もbeamは継続します。ATTACK関連値は生成時snapshotです。

## 武器/Procタイプ番号

`Guide.txt` と `GUIDE4.txt` を統合した一覧です。名称は資料の英語/日本語表記を機械的に寄せています。

| 番号 | 確度 | 推定内容 | 主な備考 |
| --- | --- | --- | --- |
| `1` | A | 通常レーザー / `LZ_Beam` | `RunProc2`: WEAPONPOINT、energy、trail点数、p2/100前進/tick、p3/100表示幅、距離依存旋回、texture、固定300 active tick、同一対象1回。 |
| `2` | A | 矢形レーザー長/低速 | `WeaponAttack`。`LockBodyUpTarget` 条件の資料あり。 |
| `3` | A | 小型ミサイル / 4 Scatter Missiles | `LoadXFile` 要求とされる資料あり。 |
| `4` | A | 多弾頭ミサイル / Pod Missile Scatter | `LoadXFile` 要求。 |
| `5` | A | ファンネル / DRAGOONS | `WeaponAttack` または `RunProc2`。 |
| `6` | A | バルカン / bullet | `ATTACK` 併用可能。 |
| `7` | A | 弱爆弾/グレネード | `ATTACK` 併用可能。 |
| `8` | A | クレイジーボール / Shotball | `ATTACK` 併用可能。 |
| `9` | A | レールカノン / Cannon Shell | `ATTACK`、`RunProc`、`WeaponAttack` 併用可能。 |
| `10` | A | 巡航/長距離ミサイル、Drop Bomb | `LoadXFile` 要求。 |
| `11` | B | 2 bullets / heat-seek vulcan候補 | サンプルに `WeaponAttack(0,11,5,30)`。 |
| `12` | A | 矢形レーザー短 | `LockBodyUpTarget` 条件。 |
| `13` | A | 矢形レーザー高速/誘導 | Heat Beam Missilesとも記載。 |
| `14` | A | サンダーライフル / 電磁砲 | `RunProc2`: thickness, speed, length, helicity等。 |
| `15` | A | トラップレーザー / 粘性レーザー | `WeaponAttack`。 |
| `16` | A | 爆撃ミサイル / Drop Bomb | `LoadXFile` 要求。 |
| `17` | C | 不明、Magician系候補 | crystalの `LoadXFile` が必要と推測されている。 |
| `18` | A | 貫通弾 / Missile | `LoadXFile` 要求。 |
| `19` | A | 誘導ミサイル | `WeaponAttack2` 使用例。 |
| `20` | A | ファイヤーボール | `WeaponAttack`。 |
| `21` | A | 雷/曲がるレーザー | 誘導小ビーム系。 |
| `22` | A | クリスタル / Crystal Drones | `LoadXFile` 要求。 |
| `23` | A | 魔方陣 / Laser Summoning | `RunProc` / `WeaponAttack` 併用可能。 |
| `24` | A | 大型レーザー、固定/消滅型 | 剣出力にも使えるとされる。 |
| `25` | A | 大型レーザー/鞭、発射口追従型 | `RunProc2`: vibration, smoke, electricity候補。 |
| `26` | A | 砲弾 / Cannon Shell | `RunProc2`。 |
| `27` | A | 近距離火炎/キャノン | `WeaponAttack`。 |
| `28` | A | 継続ビーム | `RunProc2`: thickness, length, beam time等。 |
| `29` | A | 飛行シールド / ダガー投げ | `LoadXFile` 要求資料あり。 |
| `30` | A | 火球/電気球召喚 | `WeaponAttack2`。 |
| `31` | A | 飛行剣 / ダガー投げ | `LockBodyUpTarget` 条件。 |
| `32` | A | 召喚剣/刺突剣 | `grab on tail.dat`、`LoadXFile` 要求資料あり。 |
| `33` | A | 召喚剣 | `LoadXFile` 要求。 |
| `34` | B | 羽根透明シールド | shield SPT位置、feather SPT位置、枚数、防御時間、防御率らしき引数。 |
| `35` | B | flame wave | 炎波。 |
| `36` | B | protective cover | 保護シールド。HP、時間らしき引数。 |
| `51` | A | GUN側表示・SWORD側非表示 | `ChangeWeapon(GUN)`のコンパイル結果。`FUN_004f97f0`。KD系では`Gun`＋`Sword_dammy`を表示。 |
| `52` | A | GUN側非表示・SWORD側表示 | `ChangeWeapon(SWORD)`のコンパイル結果。`FUN_004f9930`。KD系では`Sword`＋`Gun_dammy`を表示。 |
| `53` | B | flight airwave | `RunProc` 使用。 |
| `54` | B | pressure wave flight time | `RunProc` 使用。 |
| `55` | A | サーベル/ソードレーザー | `RunProc2`: length, texture, line texture等。 |
| `57` | A | 格闘判定 / Fighting judgment | `weaponPoint`から前方へ`p0/100`の線分。前tick線分との掃引、`p8` tick寿命、生成時ATTACK snapshot。`p2`は攻防双方hit-stop tick、`p3`は対象別命中履歴の存続tickで、削除後は同じ生成物から再命中できる。 |
| `58` | B | Bow | 時間引数あり。 |
| `59` | C | クリスタルを味方へ転送 | 詳細不明。 |
| `60` | A | 電気特殊効果 / `LZ_ThunderEffect` | `RunProc2`: WEAPONPOINT、`p0/100`幅、`p1`長さ、`p2/100`前進量/tick、`p3` texture、`p4/100`散布半径、`p5`有効tick。非攻撃。 |
| `61` | B | 光球攻撃 | `0`=消滅、`1`=攻撃という資料あり。 |
| `62` | A | 汎用特殊エフェクト | `FUN_004fa830`が第4引数のsubtype 0～11を明示dispatchする。subtypeごとに別factory／引数契約を持つ。 |

`LoadXFile` は資料上の「必要条件」として出ますが、ANI命令名として確認できているわけではありません。該当モデル/弾/剣などの `.x` リソースが機体フォルダまたはSPT設定側で必要、という意味に近いと考えます。

## `RunProc2` タイプ62 サブタイプ

`RunProc2(..., 62, weaponPoint, subtype, ...)` は`FUN_004fa830`がsubtype 0～11を明示dispatchする汎用エフェクト生成です。現行4 containerを公開loaderで走査するとraw 534件、ELS_QTのANI／AN2重複を除く3機体338件で、subtype 1=8、2=57、3=4、6=131、8=130、11=8件でした。0／4／5／7／9／10は現行データにありません。dispatchの存在と各subtypeの意味確定は分け、factory・更新・終了まで追跡した項目だけAとします。

| subtype | 確度 | 推定内容 |
| --- | --- | --- |
| `0` | B | 光輪/halo。時間、回転速度、サイズ、テクスチャ、前後距離、Z追加量候補。 |
| `1` | B | 振動。 |
| `2` | A | 非戦闘`BB_WindRing`。第3引数WEAPONPOINTのtranslationを生成時snapshotし、p4～p11は未使用。`WindRing.png`、初期size 1.0／alpha 255、更新ごとにsize +0.3後alpha -24、更新10は4.0／15で存続、更新11の候補alpha -9で終了する。RunProc type 54の`BB_WindRing2`とは別object。DX9頂点・UV・blendはUnity Adapter。 |
| `3` | A | 非戦闘`BB_Burner`＋`BB_BurnerBall`。p4/100はprimary size、p5/100はlength、p6 variant 0はtexture ID 8／9、variant 1は43／44。両objectはWEAPONPOINT行列へ追従し、primaryはsize×length・alpha 128・11更新目終了、ballはsize/2・local Z=length/8・XY基底0.95倍/update・5更新目終了。DX9頂点・UV・blendはUnity Adapter。 |
| `4` | B | テクスチャ呼び出し。厚み、黒縁有無、テクスチャ番号候補。 |
| `5` | B | 薬莢/殻。SPT位置、排出角度候補。 |
| `6` | A | 非戦闘`BB_Hinoko`火花。第3引数WEAPONPOINT、p4/100はsize、p5/10000はWEAPONPOINT Z基底へ加える符号付き方向、p6～p8/100は独立XYZ散布範囲、p9～p11は未使用。texture ID 40 `hinoko.png`、初期alpha 255、更新31から-4/update、更新94で0・終了、draw値+5/update。共有乱数列とDX9描画はUnity Adapter。 |
| `7` | B | テクスチャモデル回転エフェクト。SPT位置、テクスチャ、フレーム、回転時間候補。 |
| `8` | A | 非戦闘`LZ_MagicShieldEffect`。第3引数WEAPONPOINT、p4は原作内部model slot、p5低16bitはrelease gate、p6はWEAPONPOINT行列追従flag兼active countdown。初期scale 0.1／opacity 0、growは双方+0.1/tick、scale 0.9でactive、activeはcountdownを先に減算、fadeはopacity -0.1／scale +0.05、opacity 0.0001未満で終了。p7～p11はfactory経路で未使用。 |
| `9` | B | 3モデル回転エフェクト。 |
| `10` | B | 羽根エフェクト。サイズ、フレーム候補。 |
| `11` | B | 火花/sparks。数量、速度候補。 |

## BURNER / バーニア系

| 命令 | 確度 | 推定仕様 |
| --- | --- | --- |
| `BURNER(id, output);` | A | ID 0～19の有効フラグを1にし、別の浮動小数出力配列へ `output` を書く。通常機体の観測済み描画call siteは有効フラグだけをgateに使い、ANI output値を描画倍率へ使わない。内部クラス名は `Scr_BunerOut`。 |
| `BURNERSET(id, frameName, value, token)` | A | SPT側定義。ID 0～19、frameNameとfloat値を保存する。通常機体は有効IDのfloat値をそのまま描画へ渡す。別クラス`CShip`は`value * max(speed * 10, 0)`を使う。第4文字列tokenは構文上必須だが、このEXE buildでは一時bufferへ読むだけで保存・比較・参照しない。 |
| `BURNER2(...)` | A | 原作パーサーは名称を認識するが、エラーを表示して命令オブジェクトを生成しない。 |
| `BunerOut(...)` | A | 入力トークンではなく `BURNER` の内部クラス名。独立命令として扱わない。 |

現行プロジェクト3機体のANIはすべて `BURNER(id)` の1引数表記で、解析対象のオリジナル実行ファイルは2引数目の浮動小数を必須で読みます。Unityは既存データ互換として1引数時を`output=1`で読みますが、これは原作パーサー仕様とは区別します。通常描画では`output=0`でも要求フラグは立つため、値0を消灯命令として扱いません。ブースト噴射の見た目は `RunProc2` タイプ62サブタイプ3でも表現されるため、`BURNER` は機体固定の継続出力、`RunProc2(62, ..., 3, ...)` は瞬間的なエフェクト生成と見るのが自然です。

## 音声・カメラ・ゲージ・その他

| 命令 | 確度 | 推定仕様 |
| --- | --- | --- |
| `Snd(id);` | A | 効果音再生。サンプルでは `Snd(1);`。 |
| `Voice(name);` | A | ボイス再生。サンプルでは `Voice(Damage);`。 |
| `CamEffect=value;` / `CamEffect(id);` | B | カメラ効果。サンプルでは `CamEffect=0;`。 |
| `AddExGauge(value);` | C | EXゲージ増減。 |
| `AddEnergy(value);` | C | エネルギー増減。`@float[100]` 操作の命令版候補。 |
| `GvEnable=value;` | B | ガード/重力/ゲージ系の有効フラグ候補。サンプルでは `GvEnable=0;`。名称だけでは断定困難。 |
| `SetExtParam(index,value);` | C | 拡張パラメータ配列への書き込み。 |
| `Rnd(...)` | C | グローバル乱数。 |
| `LocalRnd(...)` | C | ローカル/アニメーション内乱数。 |
| `CatchLastChara(a,b,c)` | A | 原作パーサーで確認できる3引数命令。内部クラス名は `Scr_CatchChara`。直前に扱った相手キャラの捕獲/関連付け処理と推定。 |

## テクスチャ番号

`RunProc2` のテクスチャ引数で使われる番号候補です。`GUIDE4.txt` 由来で、実ファイル名やバージョンにより差がある可能性があります。これは擬似コード21410～21453行の「起動時ロード順」とは別の番号体系です。ロード順をそのままスクリプトIDとして扱わないでください。原作確定のロード順は`SCRIPT_ANI_ORIGINAL_DECOMPILED_ANALYSIS.md`に分離しています。

| 番号 | テクスチャ候補 |
| --- | --- |
| `0` | `beam.bmp` |
| `1` | `BlueBall` / `Beam8.png` |
| `2` | `explode2` |
| `3` | `burner_` |
| `4` | `beam2.bmp` |
| `5` | `smokeline.png` |
| `6` | `Gsmoke` |
| `7` | `laser 2` |
| `8` | `burner` |
| `9` | `burner2` |
| `10` | `beamHit3` |
| `11` | 不使用/不明 (`X`) |
| `12` | `sabel` |
| `13` | `sabel_line` |
| `14` | `beam2` |
| `15` | `beam7_` |
| `16` | `BlueRedBeam` |
| `17` | `smoke` |
| `18` | `smoke2` |
| `19` | `smokeline_b` |
| `20` | `WindRing` |
| `21` | `beam3` |
| `22` | `beam3Hit` |
| `23` | `summon` |
| `24` | `BlueRedBeam2` |
| `25` | `beam4` |
| `26` | `beam5` |
| `27` | `beam6` |
| `28` | `beam7` |
| `29` | `beam8` |
| `30` | `sabel_line2` |
| `31` | `Fire` |
| `32` | 不使用/不明 (`X`) |
| `33` | `magic` |
| `34` | `beamHit2` |
| `35` | `Wing` |
| `36` | `wind` |
| `37` | `Bomb` |
| `38` | `m_circle4` |
| `39` | `beamHit4` / `blueLight` |
| `40` | `hinoko` |
| `41` | `fireAnime2` |
| `42` | `sabel_line3` |

テストプレイ実装はこの表を`scriptTextureId`として保持し、原作確定のロード順は別の`loadSequence`として保持します。2026-08-12時点の`Assets/IMG_TX`ではロード表44件すべてを生成済みです。ID11と32は不明のまま割り当てず、ID14は`beam2.png`、ID39は`blueLight.bmp`を使います。

## 調査時の扱い

- 既存の未知命令や未知引数は削除せず、コメントアウトも最小限にしてください。ゲーム本体では有効な可能性があります。
- `RunProc2` はタイプごとに引数意味が変わるため、共通引数名を強く決めすぎないでください。
- `AttackFlag` や `GvEnable` はビットフラグの可能性が高く、値ごとの意味はサンプル比較で確認する必要があります。
- `@int` / `@float` はゲーム本体の状態テーブルに直結している可能性があります。Unity側に移植する場合は、読み取り専用状態と書き込み可能状態を分けて設計してください。
- サンプル `Script.ani` の出現命令は1機体分に過ぎません。オリジナル全機体やMODを横断してシンボル収集すると、命令表の確度を上げられます。
