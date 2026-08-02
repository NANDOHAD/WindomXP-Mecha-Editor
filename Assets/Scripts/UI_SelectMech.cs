using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Threading.Tasks;
public class UI_SelectMech : MonoBehaviour
{
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
            if(value == 100)
            {
                lodingPerTxt.text = "Loading Complete.";
            }
            else
            {
                // 進捗を受け取ったときの処理をここに記述
                lodingPerTxt.text = $"NowLoading...{value}%"; // 進捗をテキストに設定
            }
        });
        ani2 ani = new ani2();
        string selectedFolder = Path.Combine(folder, list[RoboDD.value]);
        bool loaded = await ani.load(Path.Combine(selectedFolder, name), progress);
        if (!loaded || ani.structure == null || ani.animations == null)
            throw new InvalidDataException($"'{name}' を読み込めませんでした。");

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
