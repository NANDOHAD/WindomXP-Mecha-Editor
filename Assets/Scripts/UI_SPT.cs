using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class UI_SPT : MonoBehaviour
{
    public InputField SPTField;
    public RoboStructure robo;

    [Header("BURNER初期化")]
    [Tooltip("バーナーエフェクトのPrefab（nullの場合はデフォルトのParticleSystemを生成）")]
    public ParticleSystem burnerEffectPrefab;

    [Tooltip("SPT読み込み後に自動でSptParserを実行する対象のMechaAnimator")]
    public MechaAnimator mechaAnimator;

    // 最後にパースした SptRuntimeData（外部から参照可能）
    public SptRuntimeData LastSptData { get; private set; }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void loadSPTField()
    {
        string sptPath = Path.Combine(robo.folder, "Script.spt");
        if (!File.Exists(sptPath))
        {
            SPTField.text = "";
            LastSptData = null;
            Debug.Log($"[UI_SPT] Script.spt が見つかりません: {sptPath}");
            return;
        }

        byte[] file = robo.transcoder.Transcode(sptPath);
        SPTField.text = USEncoder.ToEncoding.ToUnicode(file);

        // SPT読み込み後、自動でSptParserを実行してBURNERエフェクトを初期化
        ApplySptToRuntime(SPTField.text);
    }

    /// <summary>
    /// SPTテキストをパースし、MechaAnimatorのAniScriptRuntimeへSptRuntimeDataを渡す。
    /// UIから手動で呼ぶことも可能。
    /// </summary>
    public void ApplySptToRuntime(string sptText)
    {
        if (string.IsNullOrEmpty(sptText)) return;

        // 1. Script.spt をパース
        var data = SptParser.Parse(sptText);
        LastSptData = data;

        // 2. ボーン（Transform）をキャラクターのルートに紐づける
        if (robo != null && robo.root != null)
            SptParser.BindTransforms(robo.root.transform, data);

        // 3. 各バーナーボーンにParticleSystemを生成・アタッチ
        SptParser.BuildBurnerEffects(data, burnerEffectPrefab);

        // 4. AniScriptRuntime に渡す
        //    mechaAnimator が Inspector で未設定の場合はシーン内から自動検索する
        AniScriptRuntime runtime = null;

        if (mechaAnimator != null)
        {
            runtime = mechaAnimator.GetComponent<AniScriptRuntime>();
        }

        if (runtime == null)
        {
            // フォールバック: シーン内の AniScriptRuntime を自動検索
            runtime = FindObjectOfType<AniScriptRuntime>();
            if (runtime != null)
                Debug.Log("[UI_SPT] AniScriptRuntime をシーンから自動検索で見つけました。" +
                          " Inspector の mechaAnimator フィールドに設定すると検索コストを省けます。");
        }

        if (runtime != null)
        {
            runtime.sptData = data;
            Debug.Log($"[UI_SPT] SptRuntimeData を AniScriptRuntime へ渡しました。" +
                      $" BURNERSETエントリ数: {data.BurnerSets.Count}");
        }
        else
        {
            Debug.LogWarning("[UI_SPT] AniScriptRuntime が見つかりませんでした。" +
                             " MechaAnimator と同じ GameObject に AniScriptRuntime を追加してください。");
        }
    }

    public void saveSPT()
    {
        List<byte> list = new List<byte>();
        list.AddRange(USEncoder.ToEncoding.ToSJIS(SPTField.text));
        for (int j = 0; j < list.Count; j++)
        {
            if (list[j] == 0x0A && (j == 0 || list[j - 1] != 0x0D))
            {
                list.Insert(j, 0x0D);
                j++;
            }
        }
        byte[] tFile = robo.transcoder.Transcode(list.ToArray());
        File.WriteAllBytes(Path.Combine(robo.folder, "Script.spt"), tFile);
    }
}
