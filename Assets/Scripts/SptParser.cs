using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Script.spt の BURNERSET / WEAPONPOINT などを解析してランタイムデータを構築するパーサー。
/// エフェクト方向は「エフェクトが原点からZ軸方向に吹き出す」仕様に基づく。
/// 実SPT/HODではUP/DOWN双方のOutputボーン姿勢に外向き基準が含まれるため、
/// Unity表示Adapterではどちらも追加回転なしのローカルZ+として扱う。
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

    /// <summary>SPTの方向指定。UP/DOWNはOutputボーンのローカルZ+をそのまま使う。</summary>
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

/// <summary>WEAPONPOINT 1エントリ分のデータ。</summary>
public class WeaponPointInfo
{
    /// <summary>RunProc2 / WeaponAttack の第3引数で参照されるID。原作範囲は0..49。</summary>
    public int Id;

    /// <summary>Script.spt に記述されたフレーム名（拡張子除く）。</summary>
    public string FrameName;

    /// <summary>原作SPTが受け付けるUP/DOWN方向。</summary>
    public SptDirection Direction;

    /// <summary>機体ロード後に紐づけたUnity上のボーン。</summary>
    public Transform BoneTr;

    /// <summary>原作の発射・判定方向。UPはローカルZ+、DOWNはローカルZ-。</summary>
    public Vector3 WorldForward => BoneTr == null
        ? Vector3.forward
        : (Direction == SptDirection.DOWN ? -BoneTr.forward : BoneTr.forward);
}

/// <summary>
/// Script.spt 全体のランタイムデータを保持するコンテナ。
/// パース後は BindTransforms / BuildBurnerEffects を呼び出して初期化を完了させること。
/// </summary>
public class SptRuntimeData
{
    public const int OriginalSubLockDistanceCount = 20;
    public const int OriginalWeaponPointCount = 50;

    // ---- BURNERSET ----
    public readonly Dictionary<int, BurnerSetInfo> BurnerSets = new Dictionary<int, BurnerSetInfo>();

    // ---- WEAPONPOINT ----
    public readonly Dictionary<int, WeaponPointInfo> WeaponPoints = new Dictionary<int, WeaponPointInfo>();

    // ---- 今後追加予定 ----
    // ATTACKARMSET, GUNFILENAME, SWORDFILENAME ... など

    // ---- 基本パラメータ ----
    public string Name;
    public string NameEng;
    public int HP;
    public int Generator;
    public int Energy;
    public int Score;
    public int RestBody;
    public float LockDist;
    public readonly Dictionary<int, int> SubLockDistances = new Dictionary<int, int>();

    // A value of zero and a missing statement have different meanings in the
    // original parser. Keep presence information so callers can apply their own
    // compatibility fallback without losing that distinction.
    public bool HasHP;
    public bool HasGenerator;
    public bool HasEnergy;
    public bool HasScore;
    public bool HasRestBody;
    public bool HasLockDist;

    // ---- エフェクト設定 ----
    /// <summary>バーナーエフェクトの Prefab。BurnerSetInfo.Scale をスケールに掛け合わせて使う。</summary>
    public ParticleSystem BurnerEffectPrefab;
}

/// <summary>Script.spt テキストを解析して SptRuntimeData を返す静的パーサー。</summary>
public static class SptParser
{
    static Material defaultBurnerParticleMaterial;
    static Texture2D defaultBurnerParticleTexture;

    // BURNERSET(id, frameName, scale, direction)
    static readonly Regex RxBurnerSet = new Regex(
        @"BURNERSET\s*\(\s*(\d+)\s*,\s*([^,]+?)\s*,\s*([+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)\s*,\s*(UP|DOWN|FORWARD|BACK|LEFT|RIGHT)\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // WEAPONPOINT(id, frameName, UP|DOWN)
    static readonly Regex RxWeaponPoint = new Regex(
        @"WEAPONPOINT\s*\(\s*([+-]?\d+)\s*,\s*([^,]+?)\s*,\s*(UP|DOWN)\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static readonly Regex RxSimpleKV = new Regex(
        @"^([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.+?)\s*;",
        RegexOptions.Compiled);

    // Original data uses the command-style SubLockDist(index, distance) form.
    // Accept '=' as well because older MOD tools have emitted both spellings.
    static readonly Regex RxSubLockDist = new Regex(
        @"^SubLockDist\s*(?:\(|=)\s*([+-]?\d+)\s*,\s*([+-]?\d+)\s*\)?\s*;",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

            // --- WEAPONPOINT ---
            var mWeaponPoint = RxWeaponPoint.Match(line);
            if (mWeaponPoint.Success &&
                int.TryParse(mWeaponPoint.Groups[1].Value, out int weaponPointId) &&
                weaponPointId >= 0 && weaponPointId < SptRuntimeData.OriginalWeaponPointCount)
            {
                data.WeaponPoints[weaponPointId] = new WeaponPointInfo
                {
                    Id = weaponPointId,
                    FrameName = NormalizeFrameName(mWeaponPoint.Groups[2].Value),
                    Direction = ParseDirection(mWeaponPoint.Groups[3].Value)
                };
                continue;
            }

            // --- SubLockDist(index, distance) ---
            var mSubLock = RxSubLockDist.Match(line);
            if (mSubLock.Success &&
                int.TryParse(mSubLock.Groups[1].Value, out int subLockIndex) &&
                int.TryParse(mSubLock.Groups[2].Value, out int subLockDistance) &&
                subLockIndex >= 0 && subLockIndex < SptRuntimeData.OriginalSubLockDistanceCount)
            {
                data.SubLockDistances[subLockIndex] = subLockDistance;
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
                    case "HP":          data.HasHP = TryParseInt(val, out data.HP); break;
                    case "Generator":   data.HasGenerator = TryParseInt(val, out data.Generator); break;
                    case "Energy":      data.HasEnergy = TryParseInt(val, out data.Energy); break;
                    case "Score":       data.HasScore = TryParseInt(val, out data.Score); break;
                    case "RestBody":    data.HasRestBody = TryParseInt(val, out data.RestBody); break;
                    case "LockDist":
                        data.HasLockDist = TryParseInt(val, out int lockDistance);
                        if (data.HasLockDist)
                            data.LockDist = lockDistance;
                        break;
                }
            }
            // 他の命令 (ATTACKARMSET ...) は今後ここへ追加
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
            info.BoneTr = FindFrame(all, info.FrameName);
        }
        foreach (var info in data.WeaponPoints.Values)
        {
            info.BoneTr = FindFrame(all, info.FrameName);
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
                ParticleSystem defaultParticle = goEffect.AddComponent<ParticleSystem>();
                ConfigureDefaultBurnerParticle(defaultParticle);
            }

            goEffect.transform.localPosition = Vector3.zero;
            goEffect.transform.localRotation = DirectionToRotation(info.Direction);
            goEffect.transform.localScale    = Vector3.one * info.Scale;

            var ps = goEffect.GetComponent<ParticleSystem>();
            ps.Stop(withChildren: true, stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);
            info.Ps = ps;
        }
    }

    static void ConfigureDefaultBurnerParticle(ParticleSystem particleSystem)
    {
        if (particleSystem == null)
            return;

        ParticleSystem.MainModule main = particleSystem.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 0.25f;
        main.startSpeed = 1.5f;
        main.startSize = 0.14f;
        main.startColor = new Color(0.45f, 0.85f, 1f, 0.85f);
        main.maxParticles = 64;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = 40f;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.03f;

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        Material compatibleMaterial = GetDefaultBurnerParticleMaterial();
        if (renderer != null && compatibleMaterial != null)
            renderer.sharedMaterial = compatibleMaterial;
    }

    static Material GetDefaultBurnerParticleMaterial()
    {
        if (defaultBurnerParticleMaterial != null)
            return defaultBurnerParticleMaterial;

        Shader shader = null;
        if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return null;

        defaultBurnerParticleMaterial = new Material(shader)
        {
            name = "TestPlayDefaultBurnerParticleMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };
        Texture2D particleTexture = GetDefaultBurnerParticleTexture();
        if (defaultBurnerParticleMaterial.HasProperty("_MainTex"))
            defaultBurnerParticleMaterial.SetTexture("_MainTex", particleTexture);
        if (defaultBurnerParticleMaterial.HasProperty("_BaseMap"))
            defaultBurnerParticleMaterial.SetTexture("_BaseMap", particleTexture);
        return defaultBurnerParticleMaterial;
    }

    static Texture2D GetDefaultBurnerParticleTexture()
    {
        if (defaultBurnerParticleTexture != null)
            return defaultBurnerParticleTexture;

        const int Size = 32;
        defaultBurnerParticleTexture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true)
        {
            name = "TestPlayDefaultBurnerParticleTexture",
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float nx = ((x + 0.5f) / Size) * 2f - 1f;
                float ny = ((y + 0.5f) / Size) * 2f - 1f;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny)), 2f);
                pixels[y * Size + x] = new Color(0.65f, 0.9f, 1f, alpha);
            }
        }
        defaultBurnerParticleTexture.SetPixels(pixels);
        defaultBurnerParticleTexture.Apply(false, true);
        return defaultBurnerParticleTexture;
    }

    // ---- ヘルパー ----

    static SptDirection ParseDirection(string s) =>
        Enum.TryParse<SptDirection>(s.Trim(), ignoreCase: true, out var d) ? d : SptDirection.UP;

    static string NormalizeFrameName(string value)
    {
        string frameName = value == null ? "" : value.Trim();
        return frameName.EndsWith(".x", StringComparison.OrdinalIgnoreCase)
            ? frameName.Substring(0, frameName.Length - 2)
            : frameName;
    }

    static Transform FindFrame(Transform[] all, string frameName)
    {
        if (all == null || string.IsNullOrEmpty(frameName))
            return null;

        // Unity の GameObject 名は ".x" 付きの場合があるため両方を受け付ける。
        string nameWithExt = frameName + ".x";
        foreach (var t in all)
        {
            if (string.Equals(t.name, frameName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.name, nameWithExt, StringComparison.OrdinalIgnoreCase))
                return t;
        }
        return null;
    }

    static bool TryParseInt(string value, out int result)
    {
        return int.TryParse(
            value,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out result);
    }

    /// <summary>
    /// エフェクトは「ローカルZ軸の正方向」に吹き出す。
    /// UP/DOWN = Outputボーン姿勢をそのまま使用 (ローカルZ+)
    /// </summary>
    static Quaternion DirectionToRotation(SptDirection dir)
    {
        switch (dir)
        {
            case SptDirection.DOWN:    return Quaternion.identity;
            case SptDirection.FORWARD: return Quaternion.Euler(-90f,  0f, 0f);
            case SptDirection.BACK:    return Quaternion.Euler( 90f,  0f, 0f);
            case SptDirection.LEFT:    return Quaternion.Euler(0f,  90f, 0f);
            case SptDirection.RIGHT:   return Quaternion.Euler(0f, -90f, 0f);
            default:                   return Quaternion.identity; // UP
        }
    }
}
