using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Threading.Tasks;

public class UI_SelectMech : MonoBehaviour
{
    enum HierarchyRepairDecision
    {
        Repair,
        PreferTreeDepth,
        PreferChildCount,
        Prune,
        Manual,
        ReadOnly,
        Cancel
    }

    const string RepairAndLoadLabel = "修復して読み込む";
    const string PreferTreeDepthLabel = "treeDepthを正として修復";
    const string PreferChildCountLabel = "childCountを正として修復";
    const string PruneAndLoadLabel = "不整合パーツを除外して読み込む";
    const string ManualRepairLabel = "階層を手動修復";
    const string ApplyManualRepairLabel = "検証して適用";
    const string ClearParentLabel = "未接続に戻す";
    const string ReadOnlyLoadLabel = "読取専用で続行";


    public Dropdown RoboDD;
    List<string> list = new List<string>();
    public Image selectImage;
    public Material selectMaterial;
    public bool enableTool = true;
    public UI_MsgBox msgBox;
    public RoboStructure robo;
    public RoboStructure prevRobo;
    public UI_ViewControl vc;
    public GameObject saveAni;
    public GameObject saveHod;
    public UI_Tabs editTabs;
    public GameObject modeSelect;
    public UI_EditParts editParts;
    public UI_EditAni editAni;
    public UI_SPT uiSPT;           // Script.spt の自動初期化に使用
    public string folder = "Windom_Data\\Robo";
    public GameObject prefPanel;
    public GameObject loadingUI; // ローディングUIのGameObject
    public Text lodingPerTxt;
    public GameObject maskLoad;
    
    // Start is called before the first frame update
    void Start()
    {
        robo.transcoder = new CypherTranscoder();
        RoboDD.ClearOptions();
        if(maskLoad != null && !maskLoad.activeSelf)
        {
            maskLoad.SetActive(true);
        }
        if (Directory.Exists(folder))
        {
            DirectoryInfo directory = new DirectoryInfo(folder);

            if (directory.GetDirectories().Length > 0)
            {
                List<string> options = new List<string>();
                foreach (DirectoryInfo di in directory.GetDirectories())
                {
                    options.Add(di.Name);
                    list.Add(di.Name);
                }
                RoboDD.AddOptions(options);
                selectedMech(0);

                selectImage.material = selectMaterial;
                enableTool = true;
            }
            else
            {
                msgBox.Show("ディレクトリ内に機体データがみつかりません。");
                enableTool = true;
            }
        }
        else
        {
            Directory.CreateDirectory(folder);
            msgBox.Show("初期ディレクトリ（Windom_Data\\Robo）を作成しました。");
            enableTool = true;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void selectedMech(int value)
    {
        try
        {
            if (RoboDD == null)
            {
                return;
            }

            if (selectImage == null)
            {
                return;
            }

            if (robo == null || robo.transcoder == null)
            {
                return;
            }

            if (RoboDD.value < 0 || RoboDD.value >= list.Count)
            {
                return;
            }

            string filePath = Path.Combine(folder, list[RoboDD.value], "select.png");
            
            // ファイルの存在を確認
            if (File.Exists(filePath))
            {

                robo.transcoder.findCypher(filePath);

                Texture2D tex = Helper.LoadTextureEncrypted(filePath, ref robo.transcoder);
                if (tex == null)
                {
                    return;
                }


                Sprite st = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0, 0));
                selectImage.sprite = st;
            }
            else
            {
            }
        }
        catch (Exception ex)
        {
        }
    }

    public async void loadFile(string name)
    {
        if (enableTool)
        {
            // ローディング表示を開始 (例: ローディングUIをアクティブにする)
            loadingUI.SetActive(true);
            try
            {
                await LoadDataAsync(name);
            }
            catch (Exception ex)
            {
                Debug.LogError($"機体データの読み込みに失敗しました: {ex.Message}");
                if (msgBox != null)
                    msgBox.Show($"機体データの読み込みに失敗しました。\n{ex.Message}");
                return;
            }
            finally
            {
            // ローディング表示を終了 (例: ローディングUIを非アクティブにする)
            loadingUI.SetActive(false);
            }

        }
    }
    private async Task LoadDataAsync(string name)
    {
        var progress = new Progress<int>(value =>
        {
            if (value == 100)
                lodingPerTxt.text = "Loading Complete.";
            else
                lodingPerTxt.text = $"NowLoading...{value}%";
        });

        ani2 ani = new ani2();
        string selectedFolder = Path.Combine(folder, list[RoboDD.value]);
        bool loaded = await ani.load(Path.Combine(selectedFolder, name), progress);
        if (!loaded || ani.structure == null || ani.animations == null)
            throw new InvalidDataException($"'{name}' を読み込めませんでした。");

        HodHierarchyRepairPlan repairPlan = HodHierarchyRepair.CreatePlan(ani.structure.parts);
        if (repairPlan.Kind != HodHierarchyRepairKind.None)
        {
            HodHierarchyPrunePlan prunePlan = HodHierarchyPrune.CreatePlan(ani.structure.parts, repairPlan);

            string targetRepairError;
            bool canRepairLoadedData = repairPlan.CanApplyTo(ani, out targetRepairError);
            string targetPruneError;
            bool canPruneLoadedData = prunePlan.CanApplyTo(ani, out targetPruneError);

            HodHierarchyRepairPlan treeDepthPlan = null;
            HodHierarchyRepairPlan childCountPlan = null;
            bool canUseTreeDepth = false;
            bool canUseChildCount = false;
            string treeDepthUnavailableReason = "";
            string childCountUnavailableReason = "";
            if (repairPlan.Kind == HodHierarchyRepairKind.Ambiguous)
            {
                treeDepthPlan = HodHierarchyRepair.CreatePlanUsingTreeDepth(ani.structure.parts);
                childCountPlan = HodHierarchyRepair.CreatePlanUsingChildCount(ani.structure.parts);
                canUseTreeDepth = treeDepthPlan.CanApplyTo(ani, out treeDepthUnavailableReason);
                canUseChildCount = childCountPlan.CanApplyTo(ani, out childCountUnavailableReason);
            }

            HodHierarchyManualRepairSession manualSession = null;
            string manualUnavailableReason = "";
            bool canRepairManually = repairPlan.Kind == HodHierarchyRepairKind.Unrepairable
                && HodHierarchyManualRepairSession.TryCreate(
                    ani, out manualSession, out manualUnavailableReason);

            HierarchyRepairDecision decision = await AskHierarchyRepairAsync(
                repairPlan,
                prunePlan,
                treeDepthPlan,
                childCountPlan,
                ani.structure.parts,
                canRepairLoadedData,
                targetRepairError,
                canPruneLoadedData,
                targetPruneError,
                canUseTreeDepth,
                treeDepthUnavailableReason,
                canUseChildCount,
                childCountUnavailableReason,
                canRepairManually,
                manualUnavailableReason);
            if (decision == HierarchyRepairDecision.Cancel)
                return;

            HodHierarchyRepairPlan selectedRepairPlan = null;
            if (decision == HierarchyRepairDecision.Repair)
                selectedRepairPlan = repairPlan;
            else if (decision == HierarchyRepairDecision.PreferTreeDepth)
                selectedRepairPlan = treeDepthPlan;
            else if (decision == HierarchyRepairDecision.PreferChildCount)
                selectedRepairPlan = childCountPlan;

            if (selectedRepairPlan != null)
            {
                string repairError;
                if (!selectedRepairPlan.TryApply(ani, out repairError))
                {
                    Debug.LogWarning($"[UI_SelectMech] HOD階層の修復を中止しました: {repairError}");
                    msgBox?.Show("HOD階層を安全に修復できなかったため、読み込みを中止しました。\n" + repairError);
                    return;
                }

                Debug.LogWarning($"[UI_SelectMech] HOD階層をメモリ上で修復しました: {selectedRepairPlan.Summary}");
            }
            else if (decision == HierarchyRepairDecision.Prune)
            {
                string pruneError;
                if (!prunePlan.TryApply(ani, out pruneError))
                {
                    Debug.LogWarning($"[UI_SelectMech] 不整合パーツの除外を中止しました: {pruneError}");
                    msgBox?.Show("不整合パーツを安全に除外できなかったため、読み込みを中止しました。\n" + pruneError);
                    return;
                }

                Debug.LogWarning($"[UI_SelectMech] 不整合パーツをメモリ上で除外しました: {prunePlan.Summary}");
            }
            else if (decision == HierarchyRepairDecision.Manual)
            {
                if (manualSession == null || !await RunManualHierarchyRepairAsync(manualSession, ani))
                    return;

                Debug.LogWarning("[UI_SelectMech] HOD階層を親指定によりメモリ上で修復しました。");
            }
        }

        robo.folder = selectedFolder;
        robo.buildStructure(ani.structure);
        if (name.Contains(".ani"))
        {
            robo.ani = ani;
            robo.filename = name;
            if (prevRobo != null)
            {
                prevRobo.buildStructureFromLoaded(robo, ani.structure);
                foreach (GameObject prt in prevRobo.parts)
                    prt.layer = 7;
            }

            saveAni.SetActive(true);
            saveHod.SetActive(false);
            editAni.populateAnimationList();
            if (editTabs != null)
                editTabs.setTabActive(0, true);
            if (modeSelect != null)
                modeSelect.SetActive(true);

            // .ani 読み込み完了後、Script.spt を自動で復号・パースして
            // BURNER エフェクトを初期化する
            if (uiSPT != null)
            {
                try
                {
                    uiSPT.loadSPTField();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[UI_SelectMech] Script.spt の自動読み込みに失敗しました: {ex.Message}");
                }
            }
        }
        else
        {
            robo.filename = ani._filename;
            saveAni.SetActive(false);
            saveHod.SetActive(true);
            if (editTabs != null)
                editTabs.setTabActive(0, false);
            if (modeSelect != null)
                modeSelect.SetActive(false);
        }

        editParts.PopulatePartsList();
        this.gameObject.SetActive(false);
        vc.Menu.SetActive(true);
        vc.EditMode(true);
        prefPanel.SetActive(false);
    }


    async Task<HierarchyRepairDecision> AskHierarchyRepairAsync(
        HodHierarchyRepairPlan repairPlan,
        HodHierarchyPrunePlan prunePlan,
        HodHierarchyRepairPlan treeDepthPlan,
        HodHierarchyRepairPlan childCountPlan,
        IList<hod2v0_Part> parts,
        bool canRepairLoadedData,
        string repairUnavailableReason,
        bool canPruneLoadedData,
        string pruneUnavailableReason,
        bool canUseTreeDepth,
        string treeDepthUnavailableReason,
        bool canUseChildCount,
        string childCountUnavailableReason,
        bool canRepairManually,
        string manualUnavailableReason)
    {
        if (robo == null || robo.inputBox == null)
        {
            Debug.LogWarning("[UI_SelectMech] 修復確認ダイアログが未設定のため、読取専用で読み込みます。");
            return HierarchyRepairDecision.ReadOnly;
        }

        string message = "HODのパーツ階層に不整合があります。\n\n"
            + repairPlan.Summary;
        if (!string.IsNullOrEmpty(repairPlan.Details))
            message += "\n" + repairPlan.Details;

        string repairPreview = repairPlan.BuildPreview(parts);
        if (!string.IsNullOrEmpty(repairPreview))
            message += "\n\n修復予定:\n" + repairPreview;

        if (repairPlan.CanApply && !canRepairLoadedData && !string.IsNullOrEmpty(repairUnavailableReason))
            message += "\n\n自動修復を適用できません:\n" + repairUnavailableReason;

        if (treeDepthPlan != null)
        {
            if (canUseTreeDepth)
            {
                message += "\n\ntreeDepthを正とする場合:\n" + treeDepthPlan.Summary;
                string preview = treeDepthPlan.BuildPreview(parts);
                if (!string.IsNullOrEmpty(preview))
                    message += "\n" + preview;
            }
            else if (!string.IsNullOrEmpty(treeDepthUnavailableReason))
            {
                message += "\n\ntreeDepthを正とする修復を適用できません:\n"
                    + treeDepthUnavailableReason;
            }
        }

        if (childCountPlan != null)
        {
            if (canUseChildCount)
            {
                message += "\n\nchildCountを正とする場合:\n" + childCountPlan.Summary;
                string preview = childCountPlan.BuildPreview(parts);
                if (!string.IsNullOrEmpty(preview))
                    message += "\n" + preview;
            }
            else if (!string.IsNullOrEmpty(childCountUnavailableReason))
            {
                message += "\n\nchildCountを正とする修復を適用できません:\n"
                    + childCountUnavailableReason;
            }
        }

        if (canRepairManually)
        {
            message += "\n\n手動修復では各パーツの親を指定し、"
                + "構造HODと全アニメーションフレームを同じ順序へ再構築します。";
        }
        else if (repairPlan.Kind == HodHierarchyRepairKind.Unrepairable
            && !string.IsNullOrEmpty(manualUnavailableReason))
        {
            message += "\n\n手動修復を開始できません:\n" + manualUnavailableReason;
        }

        if (canPruneLoadedData)
        {
            message += "\n\n除外候補:\n" + prunePlan.Summary;
            if (!string.IsNullOrEmpty(prunePlan.Details))
                message += "\n" + prunePlan.Details;

            string prunePreview = prunePlan.BuildPreview(parts);
            if (!string.IsNullOrEmpty(prunePreview))
                message += "\n" + prunePreview;
        }
        else if (prunePlan.CanApply && !string.IsNullOrEmpty(pruneUnavailableReason))
        {
            message += "\n\n不整合パーツを除外できません:\n" + pruneUnavailableReason;
        }

        if (canRepairLoadedData || canUseTreeDepth || canUseChildCount
            || canRepairManually || canPruneLoadedData)
        {
            message += "\n\n変更はメモリ上だけで行い、元ファイルを自動上書きしません。";
        }
        else
        {
            message += "\n\n読取専用なら表示を継続できます。";
        }

        List<string> options = new List<string>();
        if (canRepairLoadedData)
            options.Add(RepairAndLoadLabel);
        if (canUseTreeDepth)
            options.Add(PreferTreeDepthLabel);
        if (canUseChildCount)
            options.Add(PreferChildCountLabel);
        if (canRepairManually)
            options.Add(ManualRepairLabel);
        if (canPruneLoadedData)
            options.Add(PruneAndLoadLabel);
        options.Add(ReadOnlyLoadLabel);

        string selected = await AskSelectionAsync(message, options);
        if (selected == null)
            return HierarchyRepairDecision.Cancel;
        if (selected == RepairAndLoadLabel)
            return HierarchyRepairDecision.Repair;
        if (selected == PreferTreeDepthLabel)
            return HierarchyRepairDecision.PreferTreeDepth;
        if (selected == PreferChildCountLabel)
            return HierarchyRepairDecision.PreferChildCount;
        if (selected == ManualRepairLabel)
            return HierarchyRepairDecision.Manual;
        if (selected == PruneAndLoadLabel)
            return HierarchyRepairDecision.Prune;
        return HierarchyRepairDecision.ReadOnly;
    }

    async Task<bool> RunManualHierarchyRepairAsync(
        HodHierarchyManualRepairSession session,
        ani2 ani)
    {
        while (true)
        {
            string message = "HODパーツ階層の手動修復\n\n"
                + "修正するパーツを選び、その親パーツを指定してください。"
                + "ルートはパーツ[0]に固定されます。\n"
                + $"未接続: {session.UnassignedCount} / {session.PartCount - 1}\n\n"
                + "現在の接続:";

            List<string> options = new List<string>();
            options.Add(ApplyManualRepairLabel);
            for (int partIndex = 1; partIndex < session.PartCount; partIndex++)
            {
                string assignment = session.GetAssignmentLabel(partIndex);
                message += "\n" + assignment;
                options.Add(assignment);
            }

            string selected = await AskSelectionAsync(message, options);
            if (selected == null)
                return false;

            if (selected == ApplyManualRepairLabel)
            {
                string applyError;
                if (session.TryApply(ani, out applyError))
                    return true;

                string ignored = await AskSelectionAsync(
                    "手動修復を適用できません。\n\n" + applyError,
                    new List<string> { "設定へ戻る" });
                if (ignored == null)
                    return false;
                continue;
            }

            int selectedOptionIndex = options.IndexOf(selected);
            int selectedPartIndex = selectedOptionIndex;
            if (selectedPartIndex <= 0 || selectedPartIndex >= session.PartCount)
                continue;

            List<string> parentOptions = new List<string>();
            parentOptions.Add(ClearParentLabel);
            for (int parentIndex = 0; parentIndex < selectedPartIndex; parentIndex++)
                parentOptions.Add(session.GetPartLabel(parentIndex));

            string parentSelection = await AskSelectionAsync(
                session.GetPartLabel(selectedPartIndex)
                    + " の親パーツを選択してください。\n"
                    + "循環を防ぐため、現在より前に並ぶパーツだけを選択できます。",
                parentOptions);
            if (parentSelection == null)
                continue;

            int selectedParentIndex = parentSelection == ClearParentLabel
                ? -1
                : parentOptions.IndexOf(parentSelection) - 1;
            string setError;
            if (!session.TrySetParent(selectedPartIndex, selectedParentIndex, out setError))
            {
                string ignored = await AskSelectionAsync(
                    "親パーツを設定できません。\n\n" + setError,
                    new List<string> { "設定へ戻る" });
                if (ignored == null)
                    return false;
            }
        }
    }

    async Task<string> AskSelectionAsync(string message, List<string> options)
    {
        if (robo == null || robo.inputBox == null)
            return null;

        TaskCompletionSource<string> completion = new TaskCompletionSource<string>();
        bool loadingWasActive = loadingUI != null && loadingUI.activeSelf;
        if (loadingUI != null)
            loadingUI.SetActive(false);

        robo.inputBox.openSelectDialog(
            message,
            options,
            selected => completion.TrySetResult(selected),
            ignored => completion.TrySetResult(null));

        string selectedOption = await completion.Task;
        if (loadingUI != null && loadingWasActive)
            loadingUI.SetActive(true);
        return selectedOption;
    }



        

    public void setFolder(string value)
    {
        if (Directory.Exists(value))
        {
            folder = value;
            RoboDD.ClearOptions();
            list.Clear();
            DirectoryInfo directory = new DirectoryInfo(folder);

            if (directory.GetDirectories().Length > 0)
            {
                List<string> options = new List<string>();
                foreach (DirectoryInfo di in directory.GetDirectories())
                {
                    options.Add(di.Name);
                    list.Add(di.Name);
                }
                RoboDD.AddOptions(options);
                selectedMech(0);

                selectImage.material = selectMaterial;
            }
            else
            {
                msgBox.Show("ディレクトリ内に機体データがみつかりませんでした。");
                enableTool = true;
            }
        }
        
    }

    public void ClearTextureCache()
    {
        Helper.TextureCache.Clear();
        //Debug.Log("Texture cache cleared.");
    }
}
