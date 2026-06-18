using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Script.spt の BURNERSET / WEAPONPOINT などを解析してランタイムデータを構築するパーサー。
/// エフェクト方向は「エフェクトが原点からZ軸方向に吹き出す」仕様に基づき、
/// UP = ローカルZ+, DOWN = ローカルZ- (= XZ平面上で180度回転) として扱う。
/// </summary>
public enum SptDirection { UP, DOWN, FORWARD, BACK, LEFT, RIGHT }

/// <summary>BURNERSET 1エントリ分のデータ。</summary>
public class BurnerSetInfo
{
    /// <summary>Script.ani の BURNER(id) で指定されるID。</summary>
    public int Id;

    /// <summary>Script.spt に記述されたフレーム名（拡張子除く）。例: "PENSHE1"。</summary>
    public string FrameName;

    /// <summary>エフェクトの大きさ（スケール）。0 の場合は非表示扱い。</summary>
    public float Scale;

    /// <summary>吹き出し方向。UP=ローカルZ+, DOWN=ローカルZ-。</summary>
    public SptDirection Direction;

    /// <summary>
    /// Unity 上のボーン Transform。
    /// キャラクター初期化後に <see cref="SptRuntimeData.BindTransforms"/> で設定される。
    /// </summary>
    public Transform BoneTr;

    /// <summary>
    /// このバーナー用の ParticleSystem。
    /// <see cref="SptRuntimeData.BuildBurnerEffects"/> で生成・アタッチされる。
    /// </summary>
    public ParticleSystem Ps;

    /// <summary>現在フレームで BURNER 命令から要求されているか。</summary>
    [HideInInspector] public bool RequestedThisFrame;
}

/// <summary>
/// Script.spt 全体のランタイムデータを保持するコンテナ。
/// パース後は BindTransforms / BuildBurnerEffects を呼び出して初期化を完了させること。
/// </summary>
public class SptRuntimeData
{
    // ---- BURNERSET ----
    public readonly Dictionary<int, BurnerSetInfo> BurnerSets = new Dictionary<int, BurnerSetInfo>();

    // ---- 今後追加予定 ----
    // WEAPONPOINT, ATTACKARMSET, GUNFILENAME, SWORDFILENAME ... など

    // ---- 基本パラメータ ----
    public string Name;
    public string NameEng;
    public int HP;
    public int Generator;
    public int Energy;
    public float LockDist;

    // ---- エフェクト設定 ----
    /// <summary>バーナーエフェクトの Prefab。BurnerSetInfo.Scale をスケールに掛け合わせて使う。</summary>
    public ParticleSystem BurnerEffectPrefab;
}

/// <summary>Script.spt テキストを解析して SptRuntimeData を返す静的パーサー。</summary>
public static class SptParser
{
    // BURNERSET(id, frameName, scale, direction)
    static readonly Regex RxBurnerSet = new Regex(
        @"BURNERSET\s*\(\s*(\d+)\s*,\s*([^,]+?)\s*,\s*([+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)\s*,\s*(UP|DOWN|FORWARD|BACK|LEFT|RIGHT)\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static readonly Regex RxSimpleKV = new Regex(
        @"^([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.+?)\s*;",
        RegexOptions.Compiled);

    /// <summary>Script.spt のテキスト全体を受け取り、SptRuntimeData を返す。</summary>
    public static SptRuntimeData Parse(string sptText)
    {
        var data = new SptRuntimeData();
        if (string.IsNullOrEmpty(sptText)) return data;

        foreach (var rawLine in sptText.Split('\n'))
        {
            // コメント除去（ ' 以降）
            var line = rawLine;
            var commentIdx = line.IndexOf('\'');
            if (commentIdx >= 0) line = line.Substring(0, commentIdx);
            line = line.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // --- BURNERSET ---
            var mBurner = RxBurnerSet.Match(line);
            if (mBurner.Success)
            {
                var info = new BurnerSetInfo
                {
                    Id        = int.Parse(mBurner.Groups[1].Value),
                    FrameName = mBurner.Groups[2].Value.Trim().Replace(".x", "").Replace(".X", ""),
                    Scale     = float.Parse(mBurner.Groups[3].Value,
                                    System.Globalization.CultureInfo.InvariantCulture),
                    Direction = ParseDirection(mBurner.Groups[4].Value)
                };
                data.BurnerSets[info.Id] = info;
                continue;
            }

            // --- シンプルな Key=Value 設定 ---
            var mKV = RxSimpleKV.Match(line);
            if (mKV.Success)
            {
                var key = mKV.Groups[1].Value;
                var val = mKV.Groups[2].Value.Trim();
                switch (key)
                {
                    case "Name":        data.Name      = val; break;
                    case "NameEng":     data.NameEng   = val; break;
                    case "HP":          int.TryParse(val, out data.HP); break;
                    case "Generator":   int.TryParse(val, out data.Generator); break;
                    case "Energy":      int.TryParse(val, out data.Energy); break;
                    case "LockDist":    float.TryParse(val, System.Globalization.NumberStyles.Float,
                                            System.Globalization.CultureInfo.InvariantCulture,
                                            out data.LockDist);
                        break;
                }
            }
            // 他の命令 (WEAPONPOINT, ATTACKARMSET ...) は今後ここへ追加
        }

        return data;
    }

    /// <summary>
    /// キャラクターのルート Transform 以下を再帰的に検索し、
    /// BurnerSetInfo.FrameName と一致するボーンをキャッシュする。
    /// </summary>
    public static void BindTransforms(Transform root, SptRuntimeData data)
    {
        if (root == null || data == null) return;
        var all = root.GetComponentsInChildren<Transform>(includeInactive: true);
        foreach (var info in data.BurnerSets.Values)
        {
            info.BoneTr = null;
            // Unity の GameObject 名は ".x" 付き（例: "B3.x"）の場合があるため、
            // FrameName そのものと FrameName+".x" の両方でマッチを試みる
            string nameWithExt = info.FrameName + ".x";
            foreach (var t in all)
            {
                if (string.Equals(t.name, info.FrameName,  StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t.name, nameWithExt,     StringComparison.OrdinalIgnoreCase))
                {
                    info.BoneTr = t;
                    break;
                }
            }
            if (info.BoneTr == null)
            {
                // Scale=0 はエフェクトを生成しないので警告不要（無視）
                if (info.Scale <= 0f) continue;

                // Scale>0 のエントリはボーンが見つからないと実際にエフェクトが出ないが、
                // ゲームデータ由来のダミーボーン欠落は想定内のため Log レベルに留める
                Debug.Log($"[SptParser] BURNERSET id={info.Id}: bone '{info.FrameName}' は" +
                          $" '{root.name}' 内に見つかりませんでした（エフェクトはスキップされます）。");
            }
        }
    }

    /// <summary>
    /// 各 BurnerSetInfo のボーンに ParticleSystem を子として生成する。
    /// prefab が null の場合はデフォルトの ParticleSystem を新規作成する。
    /// Scale=0 のエントリはエフェクトを生成しない。
    /// </summary>
    public static void BuildBurnerEffects(SptRuntimeData data, ParticleSystem prefab = null)
    {
        if (data == null) return;
        foreach (var info in data.BurnerSets.Values)
        {
            if (info.BoneTr == null) continue;
            if (info.Scale <= 0f)   continue;  // Scale=0 はオリジナルで無効扱い

            // 既存のエフェクトがあれば再利用
            if (info.Ps != null) continue;

            GameObject goEffect;
            if (prefab != null)
            {
                goEffect = UnityEngine.Object.Instantiate(prefab.gameObject, info.BoneTr);
            }
            else
            {
                goEffect = new GameObject($"Burner_{info.Id}_{info.FrameName}");
                goEffect.transform.SetParent(info.BoneTr, worldPositionStays: false);
                goEffect.AddComponent<ParticleSystem>();
            }

            goEffect.transform.localPosition = Vector3.zero;
            goEffect.transform.localRotation = DirectionToRotation(info.Direction);
            goEffect.transform.localScale    = Vector3.one * info.Scale;

            var ps = goEffect.GetComponent<ParticleSystem>();
            ps.Stop(withChildren: true, stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);
            info.Ps = ps;
        }
    }

    // ---- ヘルパー ----

    static SptDirection ParseDirection(string s) =>
        Enum.TryParse<SptDirection>(s.Trim(), ignoreCase: true, out var d) ? d : SptDirection.UP;

    /// <summary>
    /// エフェクトは「ローカルZ軸の正方向」に吹き出す。
    /// UP   = そのまま (ローカルZ+)
    /// DOWN = Z軸を反転 (ローカルZ-)
    /// </summary>
    static Quaternion DirectionToRotation(SptDirection dir)
    {
        switch (dir)
        {
            case SptDirection.DOWN:    return Quaternion.Euler(180f, 0f, 0f);
            case SptDirection.FORWARD: return Quaternion.Euler(-90f,  0f, 0f);
            case SptDirection.BACK:    return Quaternion.Euler( 90f,  0f, 0f);
            case SptDirection.LEFT:    return Quaternion.Euler(0f,  90f, 0f);
            case SptDirection.RIGHT:   return Quaternion.Euler(0f, -90f, 0f);
            default:                   return Quaternion.identity; // UP
        }
    }
}
