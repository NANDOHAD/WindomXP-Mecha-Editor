# Test Play Phase 6C 挙動クラス差分台帳

最終更新: 2026-09-05

2026-09-05からTest Playは将来の独立クローンゲーム基盤として開発し、Unity対象を6000.6.0f1とする。本台帳のPhase 6C完了は既存の境界付き受入であり、独立ゲーム完成を意味しない。全体のタスク順は [DEV_STATE](../DEV_STATE.md) と [監査報告](PROJECT_AUDIT_2026-09-05.md) を優先する。以下の過去検証は当時の環境の記録であり6000.6の受入ではない。

## 目的

Phase 6Cの残件を挙動クラス単位で管理し、原作EXEの全GT観測を完了条件にしないための現在台帳です。証拠区分は`原作確定`、`原作高確度`、`原作推定`、`Unity代替`を使い、実ANI/SPTをUnity Coreで再生しただけの結果は`RealAniObserved`として分離します。

追加の原作EXE観測は、一次擬似コードと実データだけでは共有Coreの候補を一意に選べない場合、または実装後に説明できない実機差が残った場合だけ行います。開始前に`Tools/OriginalTrace/OBSERVATION_TICKET_TEMPLATE.md`を作成します。

## 2026-08-31 再評価

Phase 6Cは**境界付きで完了**と判定します。これは原作ゲーム全体の再現完了ではなく、代表挙動クラスを、実装済み・未確定・Unity代替へ分離して回帰可能にしたという意味です。

- Core／Facade: 60 HzのAction、Motion、Locomotion、Combat、Presentationを既存`TestPlayController`のInspector／UnityEvent互換を保って接続済み。移動、ジャンプ、ステップ、ブースト、通常射撃、持替え、格闘連携、ロック射撃の代表経路は実ANI/SPTへ接続されている。
- 検証: 2026-09-01にUnity 6000.5.0f1でRuntime Verification 611 assertions、GT-001～GT-010の各2回tick列完全一致、Console Error 0を再確認した。
- 原作一致境界: Golden Traceは`RealAniObserved`であり、原作EXE全tick一致ではない。原作schema v2の完全trace、CharacterController接触、HOD補間、カメラ、描画、聴感の厳密一致は未完了のまま分離する。
- 対話実装境界: 現在の手動Test Playは単一の`TestPlayTargetDummy`をロック／被弾対象にする構成で、複数候補選択や実機体同士の戦闘ループではない。
- Presentation境界: type 53／54／55／60とBURNER通常表示／音は用途別に接続済み。速度倍率側は別クラス`CShip`と分類済みだが現行Roboデータには接続しない。厳密なDX9描画、その他の`RunProc*`／`WeaponAttack*`は未完了であり、一括で完成扱いにしない。

## 現在の差分台帳

| 挙動クラス | 対応GT | 証拠レベル | Unity実装と現在の受入 | 残る差分 | 次に観測する条件 |
| --- | --- | --- | --- | --- | --- |
| 接地Move保持 | GT-001、GT-004の一部 | 原作確定 | `FUN_004d2030`のaction保持率と`FUN_004cd840`の同tick乗算をCoreへ反映。原作アンカー値`0.08 × 1.0`、解放`0.08 × 0.8 = 0.064`、schema v1 22tick一致、Runtime、Real-Mech、KD-03手動停止確認を組み合わせてPhase 6B受入済み。 | 原作schema v2の22tick完全traceは未取得。UnityのCharacterController接触結果は比較外。 | 同形式での原作観測一致が明示的な受入条件になった場合だけ再開する。 |
| ジャンプ・上昇・入力解放 | GT-002、GT-003、GT-004 | 原作確定＋RealAniObserved＋Unity代替 | action 3→7→8、最低5tick、Force Y `0.04`／`0.02`、上昇前上限`0.15`、重力`-0.013`、終端`-0.8`を実装。`FUN_004d68e0`のaction 8呼出値からANI Move保持率`0.99`、`c38 < 31`・移動ゲージ正・Y速度`< 0.05`時に共通`FUN_004cd840`前でY速度へ`+0.012`する空中停止補助も反映した。GT-002はaction 7の単一入場と解放後8、GT-003は実ANI最終HODフレーム保持と解放後8、GT-004はaction 4を15tick実行して`Move Z=0.06`とForce Y `0.04`→`0.02`を確認後、action 8を35tick実行してMoveの`0.99`減衰、空中停止補助、31tick境界後の下降をfocused verificationする。 | CharacterController接地tickとHOD補間の厳密境界はUnity代替。実ANIのframe進行とroot変位は原作EXE観測一致ではない。 | 一次擬似コードと実ANIで値・条件・順序が一意で、全10 GT回帰にも説明不能差がないため追加観測条件は未発生。実機操作で上昇／空中停止の境界に説明できない差が残る場合だけ、GT-002～004から一組を代表観測する。 |
| ステップ／ブースト | GT-005、GT-006 | 原作確定＋原作高確度＋RealAniObserved＋Unity代替 | ステップ方向ID、最短16／最大61tick、最大3度/tick、ゲージ4/tick、ブースト開始時Generator/5、継続5/tick、30tick保持と31tick以降の解放終了を実装。Runtime固定値回帰に加え、GT-005は1回目の方向入力で非開始、2回目でaction 11へ1回入場、45tick保持後の解放でaction 6、水平移動、4/tickを意味検証する。GT-006は1回目のZ入力で非開始、2回目でaction 22へ1回入場、50tick保持後の解放でaction 8、開始時Generator/5、以後5/tick、実ANIの先頭5 action tick静止と6tick目以降の水平移動を意味検証する。接触移動はUnity代替。 | 二度押し受付`0.3`秒は調整可能な原作推定。Collider接触後の移動量は原作物理と同一ではない。実ANIの移動開始tickは原作EXE観測一致ではない。 | focused verificationと全10 GTの2回完全一致で説明不能差は発生していない。式、キャンセル順、Force境界に実機差が残る場合だけGT-005または006の一方を代表観測する。 |
| Combat | GT-007～GT-009 | 原作高確度＋原作確定type 1／11防御／51／52／57＋RealAniObserved＋Unity Adapter | X/C押下エッジ、通常攻撃、持替え、格闘連携をCoreへ反映。type 1はWEAPONPOINT snapshot、Script.spt `Energy`側のp0消費、trail点数・移動・照準・300 active tick・ATTACK/AttackFlag snapshot・同一対象1回を実装。type 57はWEAPONPOINT前後tick掃引、`p8`寿命、`p2` hit-stop、`p3`対象別再命中intervalを実装し、`p3=0`の次tick連続命中も回帰固定した。U-007bで`LaserReflect` → 機体`+0xB68`の低1 byte格納、signed char読取り、0～99 rollと厳密な`<`、AttackFlag bit `0x02`、ブロック進入時0リセットをCore／runtime状態へ反映。 | type 1の原作対象側複合形状、DX9 trail描画とactive終了後fadeはUnity Adapter。type 11実ANI生成例、実機体同士の入射／反射、共有乱数列は未確認。現在の被弾対象は単一の球形`TestPlayTargetDummy`。 | type 57の引数意味は一意に確定した。実機体同士の被弾や原作対象側複合形状が必要になった場合だけ別タスクで観測・実装する。 |
| ロック／Camera | GT-010 | 原作確定＋原作高確度＋RealAniObserved＋Unity代替 | 60Hz入力、SPT `LockDist`、ロック対象状態、target相対の通常射撃100→100／102／101／103・飛行射撃106→106／108／107／103、後方action欠損時fallback、`ShotTurnAng`、垂直FOV 60をCore／Adapterへ反映。GT-010で後方targetのlock保持、左＋Xから103、pose/script 103とchannel 0／1、49tickの各-20度旋回、6→0復帰を意味検証済み。追従、注視点、障害物回避、揺れはInspector調整可能なUnity代替。 | action 103期間と`ShotTurnAng=20`は実ANI事実であり原作EXE全tick一致ではない。原作カメラ配置・注視点の全式、`CamEffect`値別演出は未確定。 | focused verificationと全10 GTの2回完全一致で説明不能差はない。Coreの対象選択や旋回状態に実機差が残る場合だけ観測し、画面構図の全tick一致目的では観測しない。 |
| Presentation | GT-002～GT-010横断 | 原作確定＋原作高確度＋Unity代替 | `Snd`既知ID、`BURNER(id, output)`、`BURNER2`無効、`CamEffect`状態、Procイベントを型付きtraceへ記録。通常BURNER表示は命令存在でgateし、SPT第3floatを表示値へそのまま使ってANI output倍率を掛けない。推進音だけは正output集合を境界に`burner.wav`開始音と`burner_f15.wav`持続ループを制御する独立Adapter。速度倍率側は`CShip`専用Coreとして保持し、現行Robo機体へ接続しない。`BB_Burner`／`BB_BurnerBall`の11／5 update寿命と行列引数／倍率をfocused回帰へ固定。RunProc type 53／54／55／60は非戦闘表示へ分離する。 | BURNERのDX9頂点・UV・blend、原作推進音の登録・音量・fade、その他`RunProc*` type、Audioの距離・聴感は未確定またはUnity代替。 | CoreイベントのIDまたは発火tickが不明で実装を選べない場合だけ観測する。色、描画、聴感の厳密一致には使わない。 |

## Phase 6Cの到達状況

1. GT-002／003の短押し・長押し遷移、最終ポーズ保持、解放後action 8を実ANI focused verificationで固定済み。
2. GT-005／006の二度押し、ステップ／ブーストtick境界、ゲージ消費、実ANI水平移動を固定済み。
3. GT-004のMove保持率`0.99`、空中停止補助`+0.012`、31tick境界、action 4の正Y Force除外Adapterを固定済み。
4. GT-007～010の攻撃入力、持替え、格闘連携、ロック相対射撃、復帰をfocused verification済み。
5. U-006／U-006b type 57格闘判定・hit-stop・対象別再命中interval、U-007 AttackFlag／防御／被弾timer、U-007b `LaserReflect`値経路をCombat Coreへ接続済み。
6. U-008a／b SPT攻撃腕・銃／剣HODノード定義、type 51／52のRenderer再帰表示切替を接続済み。外部モデルloader consumerがないことも全参照で確認済み。
7. U-005a／b／cとしてtype 53／54／55／60の生成・更新意味を一次コードへ対応付け、非戦闘Presentationへ分離済み。
8. U-005dとしてBURNER開始音・持続音・停止境界をUnity Adapterへ接続済み。
9. Runtime Verification、対象focused verification、GT-001～GT-010 Real-Mech Golden Traces、Console Error確認を各変更後に維持した。

## 完了: U-005e RunProc2 type 1 通常射撃

### 選定理由

- GT-007の通常射撃で実際に発火する中核経路だが、現在は`PrimitiveFallback`と汎用Projectile推定値であり、action／cooldownよりも弾本体の原作差が大きい。
- 現在のプロジェクト内3機体の`Script.ani`生文字列走査ではtype 1を6件確認した。見た目だけの微調整より、発射位置、移動、寿命、命中を含む接続済みCombat経路を先に強化できる。
- 原作type tableはtype 1を`FUN_004e8310`へdispatchしており、GT-007実データと対応する静的解析入口が既に特定できる。

### 調査・実装範囲

1. `FUN_004b74a0`の引数配列構築とtype table、`FUN_004e8310`のhandler、生成factory、更新、衝突、描画呼び出しを追跡し、type 1の各引数を処理段階と単位つきで表にする。
2. プロジェクト内実ANIのtype 1全6件を走査し、共通形式と差分形式を分ける。GT-007の`RunProc2(0,1,0,200,30,100,10,0,0,0,0,0)`を最初の代表ケースにする。
3. 一次コードと複数実データから一意に決められた値だけをCombat Coreへ移し、ATTACK／AttackFlagの生成時snapshot、WEAPONPOINT、60 Hz移動、寿命、命中の順を固定する。既存publicフィールドとPrefab keyは維持する。
4. DirectX9固有のmesh、UV、blend、共有乱数はPresentation Adapterへ残し、Core合否と分離する。
5. GT-007 focused verificationへ、1回だけのspawn、発射口、tick移動、寿命／hit、damage／down／force／AttackFlag、cooldown 100→0、6→0復帰を追加する。

### 完了条件

- type 1のhandler・factory・update・collision根拠と実データ引数表が資料へ保存される。
- 推定の汎用speed／radius／lifetimeを原作値として扱わず、確定値とUnity Adapterがコード・traceで識別できる。
- Runtime Verification、GT-007 focused verification、GT-001～GT-010各2回の完全一致、Unityコンパイル、Console Error 0、`git diff --check`が成功する。
- 原作EXE観測は通常の完了条件にしない。複数のCore解釈が残り実装を選べない場合だけ、先に観測チケットを作成する。

### 対象外

- `RunProc*`／`WeaponAttack*`全typeの一括実装。
- 複数ロック候補、実機体同士の戦闘、敵AI。
- DirectX9描画および聴感の厳密一致。

### 2026-08-31実施結果

- type tableから`FUN_004e8310`、factory`FUN_0048b280`、初期化`FUN_004600c0`、更新`FUN_00460200`、対象衝突`FUN_0045e610`、地形衝突`FUN_0045f5a0`、trail追加`FUN_005119a0`まで追跡した。
- 実ANI 6件ではp0（Script.spt `Energy`側の消費量）=200/300、p1 trail点数=15/30、p2=100/200、p3=10/15/20/40、p4=-10/0/10を確認した。GT-007代表値は`RunProc2(0,1,0,200,30,100,10,0,0,0,0,0)`。
- CoreへWEAPONPOINT行列snapshot、Script.spt `Energy`側のp0消費（Generator非消費）、p1 trail点数、p2/100距離/tick、p3/100表示幅、20度以内の初期照準、距離100未満の旋回、固定300 active tick、ATTACK／AttackFlag snapshot、同一対象1回を接続した。
- 原作はtrail線分を対象側複合形状へ照合し、命中後も弾を継続する。Unityは同じtrail／重複抑止を使うが、対象形状は単一球Adapter。DX9頂点・UV・blendとactive終了後fadeは表示Adapterとして残した。
- Runtime Verification 584 assertions、GT-001～GT-010各2回の完全一致、Unityコンパイル、Console Error 0、`git diff --check`を確認した。Core値が静的解析で一意になったため追加原作EXE観測は実施していない。

## 完了: U-007b type 11反射設定の根拠追跡

- `FUN_004a9a10`の状態基点`+0xCC`書込みと`FUN_004b8250`の機体`+0xA9C`引渡しから`+0xB68`へ一意に対応付けた。
- type 11のguard後分岐で`FUN_00576390() % 100 < (char)*(target+0xB68)`とAttackFlag bit `0x02`を確認した。ShildGuardは`+0xB58`。
- ELS_QTの各ANI/AN2形式で`20`×12、`00`×1、type 11生成例0件を確認した。コード経路と実データ同時使用の証拠レベルを分ける。
- Runtime 589 assertions、Real-Mech 10/10各シナリオ内2回完全一致、Unityコンパイル、Console Error 0、`git diff --check`で回帰を確認した。

## 完了: U-006b type 57第7引数の根拠追跡

- `FUN_004fa150`から`FUN_00502e60`へ渡る第6引数`p2`=`+0x198`、第7引数`p3`=`+0x100`、`p8`寿命=`+0x110`を固定した。
- `FUN_004b27a0`は命中時に`p2`を攻撃側・防御側hit-stopへ書き、`p3`を対象pointerと共に`BBHITNODE+0x14`へ保存する。`FUN_00495750`が毎更新1減算し、1未満でnodeを削除するため、`p3`は対象別再命中interval tickである。
- 現行の実ANI 92件は`p3`が0×2、1×31、2×23、5×35、12×1。ELS_QTの`Script.an2` 23件は同機体ANIと一致した。
- Coreの永久重複抑止を対象別cooldownへ置換し、`p3=5`の期限内抑止・5 tick後再命中と`p3=0`の次tick連続命中を固定した。Runtime Verificationは592 assertions、Real-Mech Golden Traceは10/10を各シナリオ2回完全一致、Console Error 0。
- 原作対象側複合形状は引き続き球形Unity Adapter境界とし、type 57引数意味の確定と混同しない。

## 完了: U-009a BURNERSET第4引数の根拠追跡

- 原作parser `0x004A6D6F`以降はframe名を`+0x26AC`、第3floatを`+0x26B4`へ保存する。第4文字列は`0x004A6E5C`で一時bufferへ読むだけで、その後は空白／閉じ括弧検証へ進む。全即値参照にも第4引数の格納先・比較・生成・描画参照は存在しないため、このbuildでは構文上必須だが破棄される原作確定事項とした。
- 現行実SPT 3機体31行は`UP` 19件、`DOWN` 12件、正scale 17件、0 scale 14件。UP／DOWNは正値・0値の双方に現れ、方向・有効無効の一意な意味を持たない。現行ANI／AN2のASCII script走査は計2,066件すべて1引数`BURNER(id)`で、比較可能な第2output実値は0件だった。
- `SptParser`は原作同様に任意の第4tokenを受理し、編集互換のため`BurnerSetInfo.FourthToken`へ原文保持する。未知tokenは無回転へfallbackし、既存`FORWARD`等の方向回転はUnity表示互換として維持する。実SPTのUP／DOWNは従来どおりOutputボーンのローカルZ+を使う。
- 第4tokenはPresentation Core、`burner.wav`／`burner_f15.wav`、長さ・太さ・alpha・fadeへ接続しない。これらは引き続き独立したUnity Adapter境界とする。
- Unity 6000.5.0f1で`IsCompiling=false`／`IsUpdating=false`を確認後、Runtime Verification 594 assertions、Real-Mech Golden Trace 10/10シナリオ各2回完全一致、同一Console消去後範囲のError 0を確認した。

## 完了: U-009b BURNER描画値経路の分離追跡

- `Scr_BunerOut`実行context`+0xD8`／`+0xE0`を通常機体`+0xB74`の要求byte配列／`+0xB7C`のANI output配列へ接続した。ブロック進入時は要求byte20件だけが0へ戻る。
- 通常機体は要求byteでgateし、SPT第3float配列`+0xE38`の値を変更せず`FUN_004d39d0`へ渡す。観測済みcall siteはANI outputを読まないため、`output=0`でも通常表示要求は残る。
- 飛行機体系だけはSPT設定値へ`max(speed * 10, 0)`を掛ける別経路。描画関数の引数は主効果`value/2`と`value`、副効果`value/3`とvector Z `value/8`まで確定した。
- Unityは通常表示からANI output倍率とalpha倍率を除去し、命令存在と正のSPT第3floatを表示gateにした。既存1引数`output=1`、Inspector倍率、正output集合を使う音声Adapterは維持した。飛行機体系式はCoreへ分離しただけで全機体へ接続していない。
- Unity 6000.5.0f1で`IsCompiling=false`／`IsUpdating=false`を確認し、Runtime Verification 598 assertions、Real-Mech Golden Trace 10/10シナリオ各2回完全一致、Console Error 0を確認した。

## 完了: U-009c BURNER機体系分類と描画ライフサイクル境界

- 通常機体はfactory`FUN_0049c5f0`、速度倍率側はfactory`FUN_0049c6c0`／constructor`FUN_0049c790`を通り、後者が`CShip::vftable`を設定する。SPT／モデル条件による切替ではなく別クラス生成である。
- 現行`Windom_Data`は`Robo`ディレクトリだけで、3件の`Script.spt`もすべてRobo配下。よってTest Playは通常ロボット経路を明示使用し、`CShip`式を未接続Coreとして保持する。
- field layoutとの一致から`BB_Burner`更新を`FUN_00491580`、`BB_BurnerBall`更新を`FUN_004902c0`へ`原作高確度`で対応付けた。前者は11回目、後者は初期counter 4が-1になる5回目で終了し、owner削除状態では即終了する。
- 主効果の`-(value/2)/15`呼出引数、副効果の行列成分`0.95`倍率を純粋Presentation Coreへ追加した。既存Coneへ一時object周期を混在させず、DX9視覚はUnity Adapter境界を維持する。
- Unity 6000.5.0f1でRuntime Verification 604 assertions、Real-Mech Golden Trace 10/10シナリオ各2回完全一致、Console Error 0、`git diff --check`成功を確認した。

## 完了: U-008b 武器表示ノード消費経路

- `GUNFILENAME`格納`+0x38FC`と`SWORDFILENAME`格納`+0x40CC`の全参照を追跡した。`FUN_00499d50`は各20件を`FUN_00571230`／`FUN_00571250`へ渡して読込済みHOD階層を名前検索し、機体側pointer配列へ保存する。両格納域から外部モデルloaderへ至る参照はない。
- type 51／52の`FUN_004f97f0`／`FUN_004f9930`は、解決済みpointerを`FUN_00572c50`へ渡し、ノード`+0x30`の表示byteを子階層へ再帰設定する。名前に`FILENAME`を含むことを根拠に、別`.x`の追加ロードや装着処理を実装しない。
- 現行3機体を照合し、ガンダムTR-1ヘイズル改は`Gun`／`Sword_dammy`対`Sword`／`Gun_dammy`、ザクIIS型は`gun`／`Output07`対`sword`／`gun_dammy`を使用する。ザクIIS型の`Output07.x`／`gun_dammy.x`は`Script.ani`内HODノード名として存在するが、独立ファイルは存在しない。
- Unityの従来`GameObject.SetActive`は描画以外のcomponentまで停止するため、対象Transform以下の`Renderer.enabled`だけを再帰切替するよう修正した。GameObject／子階層をactiveのまま維持する集中回帰も追加した。
- Unity 6000.5.0f1でRuntime Verification 605 assertions、Real-Mech Golden Trace 10/10シナリオ各2回完全一致、Console Error 0、`git diff --check`成功を確認した。Golden Traceは`RealAniObserved`であり原作EXE parityではない。

## 完了: U-002a AN2初期スクリプト実行境界

- `FUN_0049cd50`はAN2のアニメーション別初期本文を`FUN_0049d8e0(text, animationIndex, -1)`へ渡し、script index `-1`の命令をアニメーションレコード`+0x0C`の別vectorへ格納する。timed block命令は各block record `+0x08`へ格納される。
- 通常機体／`CShip`の生成は共通初期化`FUN_00499d50`へ入り、初回action、同一action再入場、action遷移、武器形態`+50` fallbackはいずれも`FUN_004b8250`からtimed block列だけを開始する。runtime tickも`FUN_004b8030`へtimed block命令だけを渡し、`+0x0C` vectorのconsumerはない。この範囲は一次コードから**原作確定**とする。
- TestPlayは`squirrelInit`を保存し、未知命令を含め診断用compileを維持する一方、action初回tickでは実行せず、init-only animationを実行可能action／`+50` fallback候補に数えない。通常編集プレビューの再生開始時1回実行は既存Unity編集互換として残し、原作runtimeの根拠にはしない。
- 現行実データではAN2は`ELS_QT/Script.an2`の1件、219 animationで、非空`squirrelInit`は0件。旧ANIには同fieldがないため、初回／再入場／遷移／`+50` fallbackは合成fixtureで直接固定し、実機体Golden Traceは回帰確認として分離した。
- Unity 6000.5.0f1の一時検証コピーでRuntime Verification 611 assertions、Real-Mech Golden Trace 10/10シナリオ各2回完全一致、検証開始後Console Error 0を確認した。Golden Traceは`RealAniObserved`であり原作EXE parityではない。

## 完了: U-005f RunProc2 type 62 subtype／handler境界

- 公開`ani2.load`経由で4 containerを走査し、type 62はraw 534件、ELS_QTのANI／AN2同一内容を除く3機体338件だった。重複除外後はsubtype 1=8、2=57、3=4、6=131、8=130、11=8件で、0／4／5／7／9／10は0件。
- `FUN_004b74a0`のtype tableから`FUN_004fa830`へ入り、subtype 0～11の明示dispatchを確認した。範囲外subtypeは従来どおり未確定eventに残し、type 62全体をProjectileへ接続しない。
- 実データ最多級で引数とlifecycleが一意なsubtype 8を選び、factory `FUN_005028f0`、初期化`FUN_00479120`、grow `FUN_00479370`、active `FUN_00479560`、fade／終了 `FUN_00479740`をCore化した。第3引数はWEAPONPOINT、p4は原作内部model slot、p5低16bitはrelease gate、p6はWEAPONPOINT追従flag兼active countdown。初期scale 0.1／opacity 0、各grow tick +0.1、scale 0.9でactive、activeはcountdownを先に減算、fadeはopacity -0.1かつ残存中scale +0.05、opacity 0.0001未満で終了する。
- owner／WEAPONPOINTを失った場合は最後の行列snapshotでlifecycleを継続する。非戦闘`LZ_MagicShieldEffect`なのでProjectile／ATTACK判定を生成しない。原作model slotと同等のasset対応は自動解決できないため、`RunProc2:62:8:ModelSlot{p4}`の設定済みPrefabだけを表示し、未設定時は論理状態のみ進める。汎用弾・推測primitiveは使わない。
- Unity 6000.5.0f1の一時検証コピーでRuntime Verification 632 assertions、Real-Mech Golden Trace GT-001～GT-010各2回完全一致、検証開始後Console Error 0を確認した。Golden Traceは`RealAniObserved`であり原作EXE parityではない。

## 完了: U-005g RunProc2 type 62 subtype 6 `BB_Hinoko`

- 重複除外後131件は2 patternだった。TR-1 action 135 `ソードラスト`の`RunProc2(0,62,20,6,5,300,15,15,15,0,0,0)`が130件、ELS_QT action 199 `掴み１`の`RunProc2(0,62,13,6,5,-300,15,15,15,0,0,0)`が1件である。ELS_QTの`Script.an2`は同じ旧ANI由来内容なので重複へ数えない。
- factory `FUN_00503600`のRTTIは`BB_Hinoko`、初期化は`FUN_005036e0`、更新はvtable slot 1の`FUN_0048e0b0`、描画はslot 2の`FUN_0048e3a0`と確認した。p4/100はsize、p5/10000はWEAPONPOINT Z基底へ加える符号付き方向、p6～p8/100は独立したXYZ散布範囲、p9～p11は未使用。texture global `+0xB40`はロード順39／script texture ID 40の`hinoko.png`へ対応する。
- 初期alphaは255、elapsedとdraw角は0。各更新でコピー済み行列のlocal Zへ前進し、draw値を+5、elapsedを+1する。更新31からalphaを4ずつ減らし、更新93はalpha 3で存続、更新94は0へclampして終了する。owner pointer、ATTACK snapshot、衝突呼出し、damage fieldはなく非戦闘表示である。
- Unityは`hinoko.png`の非billboard quadをsize×size/2で生成し、Projectile／ATTACK判定を作らない。原作の共有乱数列、前後位置差fieldとUnity速度の段階差、DX9頂点・UV・blendはAdapter境界である。散布と行列回転には再現可能な決定的sampleを使い、trace診断も`DeterministicUnitySharedRngAdapter`として原作乱数一致と区別する。
- 現行Unity 6000.5.0f1でRuntime Verification 639 assertions、Real-Mech Golden Trace GT-001～GT-010各2回完全一致、検証開始後Console Error 0を確認した。Golden Traceは`RealAniObserved`であり原作EXE parityではない。現行エディタは応答を維持したため再起動していない。

## 完了: U-005h RunProc2 type 62 subtype 2 `BB_WindRing`

- 重複除外後57件はELS_QT action 109／150／152～158に集中し、`RunProc2(0,62,WEAPONPOINT,2,200,0,0,0,0,0,0,0)`の固定patternだった。WEAPONPOINT 28～43だけが変化し、p4～p11はhandlerから未読である。
- factory `FUN_0048bdf0`はprimary vtable `0x005B7960`の`BB_WindRing`を生成する。初期化slot `+0x24`の`FUN_0048bef0`はWEAPONPOINT translation、size 1.0、draw mode 1、texture global `+0xA20`、色`0xFFFFFFFF`、growth 0.3、signed alpha delta -24を保持する。`+0xA20`は起動時texture表の`WindRing.png`に対応する。
- 更新slot `+0x04`の`FUN_00491af0`はquad sizeを先に0.3増やし、その後alphaへ-24を適用する。初期size 1.0／alpha 255、更新1で1.3／231、更新10で4.0／15、更新11はsize 4.3へ進んだ後の候補alpha -9で終了する。描画slot `+0x08`の`FUN_0048d5d0`は保持行列・texture・quadを描画し、owner／ATTACK／衝突callback／damage処理を呼ばない。factoryが追従pointerを0にしsubtype 2 handlerも設定しないため、位置は生成時snapshotである。
- Unityは`WindRing.png`の非billboard quadを専用状態へ接続し、Projectile／ATTACK判定を生成しない。RunProc type 54の派生`BB_WindRing2`は変更していない。Unity座標系へのquad面対応、DX9頂点・UV・blendはAdapter境界である。
- focused回帰は異なるp4～p11でも固定初期値になること、WEAPONPOINT移動後もsnapshot位置を保つこと、2個同時生成、更新10／11境界、Projectile／damageなし、traceの証拠区分を確認した。Unity 6000.5.0f1でRuntime Verification 662 assertions、Real-Mech Golden Trace GT-001～GT-010各2回完全一致、Console Error 0。Golden Traceは`RealAniObserved`であり原作EXE parityではない。

## 完了: U-005i RunProc2 type 62 subtype 3 `BB_Burner`＋`BB_BurnerBall`

- 既存の公開loader棚卸しでは重複除外後4件であり、既知例は`RunProc2(0,62,0,3,350,350,0,...)`と`RunProc2(0,62,28,3,300,300,0,...)`である。今回の全container再走査は`ani2.load`をmain threadで同期待ちしてエディタ停止を招いたため中止し、件数・action対応を推測で更新していない。
- `FUN_004fa830`はp4／100をprimary size、p5／100をlength、p6をvariantとして読む。variant 0はtexture ID 8／9 (`burner.png`／`burner2.png`)、variant 1は43／44 (`burner3.png`／`burner4.png`)を選び、未知variantは生成しない。
- factory `FUN_0048abd0`の`BB_Burner`はprimary vtable `0x005B74F0`、初期化`FUN_0048acb0`、更新`FUN_00491580`、描画`FUN_00491720`である。WEAPONPOINT行列へ追従し、size×length、alpha 128、mode 1ではX 90度で開始する。更新ごとに両側へ`-size/15`を渡し、11更新目で終了する。
- factory `FUN_0048ada0`の`BB_BurnerBall`はprimary vtable `0x005B7BA8`、初期化`FUN_0048cea0`、更新`FUN_004902c0`、描画`FUN_0048d5d0`である。WEAPONPOINT local Z=`length/8`、size/2、白で開始し、XY基底を0.95倍しながら5更新目で終了する。両objectはowner削除時も独立終了し、ATTACK／衝突／damageを持たない。
- UnityはWEAPONPOINT追従の二枚の非戦闘quadへ置換した。DX9頂点・UV・blendと、primary幅調整のUnity座標系への見た目投影はAdapter境界である。Unity 6000.5.0f1でRuntime Verification 672 assertions、Real-Mech Golden Trace GT-001～GT-010各2回完全一致、検証開始後のテスト／プロジェクト由来Error 0を確認した。起動時の既存Package Resolver Error 1件は別件として残る。

## 後続タスク: U-005j RunProc2 type 62 subtype 11

全体優先順は6000.6環境受入、検証入口・独立化基盤、必要なtypeの順とする。U-005jはこの台帳内の次のtype候補として保持する。

1. **2026-09-06 P1-03で再棚卸し済み**: 3機体の公開loaderを非同期で走査し、type62 subtype11はザクIIS型action 105のTimed block 2/5（0始まり）に各4箇所、source ordinal 8〜11、branch `root/if0/then` の計8出現位置を確認した。全件 `RunProc2(0,62,0,11,30,30,20,0,0,0,0,0)`、WEAPONPOINT=0、p4〜p11=`30,30,20,0,0,0,0,0`、引数patternは1種類。block 5には `UnexpectedEndIf` 診断があるため、8は静的出現位置数であり実行回数/原作の条件解釈の確定ではない。[棚卸し仕様と実行結果](BASELINE_AND_ANI_INVENTORY.md)を参照。
2. `FUN_004fa830`の個数loop、factory `FUN_00489590`、固定値0.01／0.01／0.1、各軸±0.3の局所散布、texture global `+0x750`からRTTI／vtable、初期化、更新、描画、終了、owner／戦闘field、texture名を追う。
3. 現行Unityのtexture ID 40、機体root基準、固定0.4秒寿命は推定Adapterである。Coreが一意になった場合だけ専用Presentationへ置換し、汎用Projectileへ接続しない。
4. 共有乱数列とDX9頂点・UV・blendはAdapterに残す。focused回帰、全Runtime、Real-Mech Golden Trace各2回、Console Error 0を完了条件とする。

## 完了境界

Phase 6Cは、全GTの原作EXE traceではなく、各挙動クラスについて次を満たした時点で完了とします。

- 実装済み範囲、未確定範囲、Unity代替境界がこの台帳で追跡できる。
- 原作確定値には一次関数、実データ、focused verificationが対応する。
- `RealAniObserved`を原作観測一致と表記していない。
- 追加観測の開始条件と停止条件が明示されている。
- Runtime、Real-Mech Golden Trace、Console Error確認が成功する。

2026-08-31の再評価では上記を満たしたため、Phase 6Cは境界付き完了とする。以後のU-005e以降は、この完了済み基準を回帰させずに未確定範囲を小区分で縮める。
