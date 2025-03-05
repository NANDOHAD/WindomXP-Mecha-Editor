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
        // キャッシュをクリア
        ClearTextureCache();

        try
        {
            string filePath = Path.Combine(folder, list[RoboDD.value], "select.png");
            
            Debug.Log($"Checking file path: {filePath}");
            
            // ファイルの存在を確認
            if (File.Exists(filePath))
            {
                //Debug.Log("select.pngファイルが見つかりました。");
                robo.transcoder.findCypher(filePath);
                Texture2D tex = Helper.LoadTextureEncrypted(filePath, ref robo.transcoder);
                Sprite st = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0, 0));
                selectImage.sprite = st;
            }
            else
            {
                //Debug.LogError("select.pngファイルが見つかりません。");
            }
        }
        catch (Exception ex)
        {
            //Debug.LogError($"select.pngの読み込み中にエラーが発生しました: {ex.Message}\n{ex.StackTrace}");
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
            finally
            {
            // ローディング表示を終了 (例: ローディングUIを非アクティブにする)
            loadingUI.SetActive(false);
            }
            editParts.PopulatePartsList();
            this.gameObject.SetActive(false);
            vc.Menu.SetActive(true);
            vc.EditMode(true);
            prefPanel.SetActive(false);
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
        await Task.Run(() => ani.load(Path.Combine(folder, list[RoboDD.value], name), progress)); // 非同期でロード
        robo.folder = Path.Combine(folder, list[RoboDD.value]);
        robo.buildStructure(ani.structure);
        if (name.Contains(".ani"))
        {
            robo.ani = ani;
            robo.filename = name;
            prevRobo.folder = robo.folder;
            prevRobo.buildStructure(ani.structure);
            foreach (GameObject prt in prevRobo.parts)
            {
                prt.layer = 7;
            }
            saveAni.SetActive(true);
            saveHod.SetActive(false);
            editAni.populateAnimationList();
            if (editTabs != null)
                editTabs.setTabActive(0, true);
            if (modeSelect != null)
                modeSelect.SetActive(true);
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
