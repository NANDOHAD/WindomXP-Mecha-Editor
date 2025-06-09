using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;
using System.IO;
using Assets;
using System.Linq;
using UnityEngine.UI;
public delegate void Update_Event();
public class RoboStructure : MonoBehaviour
{
    public GameObject root;
    public UI_InputBox inputBox;
    public Text statusMessege;
    public List<GameObject> parts = new List<GameObject>();
    public List<bool> isTop = new List<bool>();
    public hod2v0 hod;
    public ani2 ani;
    public Assimp.AssimpImporter Importer = new Assimp.AssimpImporter();
    public string folder;
    public string filename;
    public List<Update_Event> updates = new List<Update_Event>();
    public CypherTranscoder transcoder;
    
    // Start is called before the first frame update
    void Start()
    {
        transcoder = new CypherTranscoder();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void buildStructure(hod2v0 Robo)
    {

        //find cypher
        string[] files = Directory.GetFiles(folder);
        foreach (string file in files)
        {
            if (transcoder.findCypher(file))
                break;
        }




        hod = Robo;
        if (root != null)
            GameObject.Destroy(root);

        //build Ani

        parts.Clear();

        for (int i = 0; i < Robo.parts.Count; i++)
        {
            int depth = Robo.parts[i].treeDepth;
            string offset = "";
            for (int j = 0; j < depth; j++)
                offset += "   ";

            var part = new GameObject(Robo.parts[i].name);
            if (Robo.parts[i].treeDepth == 0)
                root = part;
            parts.Add(part);
            if (i == 0)
            {
                parts[i].transform.localPosition = Robo.parts[i].position;
                parts[i].transform.localRotation = Robo.parts[i].rotation;
                parts[i].transform.localScale = Robo.parts[i].scale;
            }
            else
            {
                //find next level higher in tree.
                for (int j = i - 1; j >= 0; j--)
                {
                    if (Robo.parts[i].treeDepth - 1 == Robo.parts[j].treeDepth)
                    {
                        if (j == 0)
                        {
                            parts[i].transform.SetParent(parts[0].transform);
                            parts[i].transform.localPosition = Robo.parts[i].position;
                            parts[i].transform.localRotation = Robo.parts[i].rotation;
                            parts[i].transform.localScale = Robo.parts[i].scale;
                        }
                        else
                        {
                            parts[i].transform.SetParent(parts[j].transform);
                            parts[i].transform.localPosition = Robo.parts[i].position;
                            parts[i].transform.localRotation = Robo.parts[i].rotation;
                            parts[i].transform.localScale = Robo.parts[i].scale;
                        }
                        break;
                    }
                }
            }
        }

        for (int i = 0; i < Robo.parts.Count; i++)
        {
            try
            {
                if (i != 0)
                    ImportModelEncrypted(parts[i], Path.Combine(folder, Robo.parts[i].name));

            }
            catch { }
        }
    }

    void ImportModel(GameObject GO, string file)
    {
        if (File.Exists(file))
        {
            try
            {
                string Modelpath = Path.GetDirectoryName(file);
                var scen = Importer.ImportFile(file, Helper.PostProcessStepflags);
                if (scen == null)
                {
                    //Debug.logWarning($"Failed to import model: {file}. Assimp could not load the file.");
                    return;
                }

                Mesh mesh = new Mesh();
                mesh.CombineMeshes(scen.Meshes.Select(x => new CombineInstance()
                {
                    mesh = x.ToUnityMesh(),
                    transform = scen.RootNode.Transform.ToUnityMatrix()
                }).ToArray(), false);

                Material[] materials = new Material[scen.Meshes.Length];

                for (int index = 0; index < materials.Length; index++)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));

                    if (scen.Meshes[index].MaterialIndex < scen.Materials.Length)
                    {
                        if (scen.Materials[scen.Meshes[index].MaterialIndex] != null)
                        {
                            mat.name = scen.Materials[scen.Meshes[index].MaterialIndex].Name;
                            var textures = scen.Materials[scen.Meshes[index].MaterialIndex].GetAllTextures();
                            var color = scen.Materials[scen.Meshes[index].MaterialIndex].ColorDiffuse;
                            mat.color = new Color(color.R, color.G, color.B, color.A);
                            mat.SetFloat("_Glossiness", scen.Materials[scen.Meshes[index].MaterialIndex].ShininessStrength);


                            if (textures.Length > 0 && File.Exists(Path.Combine(Modelpath, textures[0].FilePath)))
                            {
                                try
                                {
                                    mat.mainTexture = Helper.LoadTexture(Path.Combine(Modelpath, textures[0].FilePath));
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                    else
                    {
                        //Debug.logWarning($"Invalid material index for mesh {index}: {scen.Meshes[index].MaterialIndex}");
                    }

                    materials[index] = mat;
                }

                GO.AddComponent<MeshFilter>().mesh = mesh;
                //part.AddComponent<MeshCollider>().sharedMesh = mesh; 
                GO.AddComponent<MeshRenderer>().materials = materials;
            }
            catch (Exception e)
            {
                //Debug.logError($"Error importing model {file}: {e.Message}. Assimp could not load the file.");
            }
        }
    }

    void ImportModelEncrypted(GameObject GO, string file)
    {
        try
        {
            string Modelpath = Path.GetDirectoryName(file);
            byte[] data = transcoder.Transcode(file);
            if (data == null)
            {
                //Debug.logError($"Failed to transcode file: {file}. The result is null.");
                return;
            }
            if (data.Length == 0)
            {
                //Debug.logError($"The transcoded data for file {file} is empty.");
                return;
            }
            if (data.Length > 0)
            {
                string data1 = System.Text.Encoding.GetEncoding("utf-8").GetString(data);
                data1 = XfileStringConverter(data1);
                byte[] data3 = System.Text.Encoding.GetEncoding("utf-8").GetBytes(data1);
                MemoryStream ms = new MemoryStream(data3);
                Assimp.Scene scen = null;
                try
                {
                    scen = Importer.ImportFileFromStream(ms, Helper.PostProcessStepflags, "x");
                }
                catch (System.Exception e)
                {
                    //Debug.logError($"Error importing encrypted model {file}: {e.Message}. Assimp could not load the file.");
                }
                if (scen == null)
                {
                    //Debug.logWarning($"Failed to import encrypted model from stream: {file}. Assimp could not load the file.");
                    return;
                }



                Mesh mesh = new Mesh();
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                
                try
                {
                    mesh.CombineMeshes(scen.Meshes.Select(x => {
                        var transform = scen.RootNode.Transform;
                        // 変換行列から負の値をチェックし、必要に応じて調整
                        if (transform.A1 < 0 || transform.A2 < 0 || transform.A3 < 0 ||
                            transform.B1 < 0 || transform.B2 < 0 || transform.B3 < 0 ||
                            transform.C1 < 0 || transform.C2 < 0 || transform.C3 < 0)
                        {
                            // 負の値が含まれている場合は単位行列を使用
                            transform = Assimp.Matrix4x4.Identity;
                        }

                        return new CombineInstance()
                        {
                            mesh = x.ToUnityMesh(),
                            transform = transform.ToUnityMatrix()
                        };
                    }).ToArray(), false);
                }
                catch (System.Exception e)
                {
                    //Debug.logError($"Failed to combine meshes for {file}: {e.Message}");
                    return;
                }
                
                Material[] materials = new Material[scen.Meshes.Length];

                for (int index = 0; index < materials.Length; index++)
                {
                    var mat = new Material(Shader.Find("Standard"));
                    if (mat == null)
                    {
                        //Debug.logError($"Shader not found for material: {file}");
                        return;
                    }

                    if (scen.Meshes[index].MaterialIndex < scen.Materials.Length)
                    {
                        if (scen.Materials[scen.Meshes[index].MaterialIndex] != null)
                        {
                            mat.name = scen.Materials[scen.Meshes[index].MaterialIndex].Name;
                            var textures = scen.Materials[scen.Meshes[index].MaterialIndex].GetAllTextures();
                            var color = scen.Materials[scen.Meshes[index].MaterialIndex].ColorDiffuse;
                            mat.color = new Color(color.R, color.G, color.B, color.A);
                            mat.SetFloat("_Glossiness", scen.Materials[scen.Meshes[index].MaterialIndex].ShininessStrength);

                            // シェーダーが設定されていない場合、デフォルトのシェーダーを割り当てる
                            if (string.IsNullOrEmpty(mat.shader.name) || mat.shader == null)
                            {
                                //Debug.logWarning($"Shader not set for material: {mat.name}. Assigning default shader.");
                                mat.shader = Shader.Find("Standard"); // デフォルトのシェーダーを設定
                            }

                            if (textures.Length > 0 && File.Exists(Path.Combine(Modelpath, textures[0].FilePath)))
                            {
                                try
                                {
                                    mat.mainTexture = Helper.LoadTextureEncrypted(Path.Combine(Modelpath, textures[0].FilePath), ref transcoder);
                                }
                                catch (System.Exception e)
                                {
                                    //Debug.logWarning($"Failed to load texture for {file}: {e.Message}");
                                }
                            }
                            else
                            {
                                //Debug.logWarning($"No textures found for material index {scen.Meshes[index].MaterialIndex}");
                            }
                        }
                    }
                    else
                    {
                        //Debug.logWarning($"Invalid material index for mesh {index}: {scen.Meshes[index].MaterialIndex}");
                    }

                    materials[index] = mat;
                }
                GO.AddComponent<MeshFilter>().mesh = mesh;
                //part.AddComponent<MeshCollider>().sharedMesh = mesh; 
                GO.AddComponent<MeshRenderer>().materials = materials;
            }
            else
            {
                //Debug.logWarning($"Failed to decrypt file: {file}. Assimp could not load the file.");
                return;
            }
        }
        catch (System.Exception e)
        {
            //Debug.logError($"Error processing encrypted model {file}: {e.Message}. Assimp could not load the file.");
        }
    }


    public string XfileStringConverter(string data)
    {
        if (!data.Trim().EndsWith("}"))
        {
            //Debug.log("文字化けを確認しました");
            int lastBraceIndex = data.LastIndexOf('}');
            if (lastBraceIndex != -1)
            {
                data = data.Substring(0, lastBraceIndex + 1); // 最後の波括弧を残す
            }
            data += "}"; // 新たに波括弧
            //Debug.log("文字化けを対応しました。");
        }
        // FrameTransformMatrixの部分を探す正規表現        
        string pattern = @"FrameTransformMatrix\s*{([^}]*)}";
        MatchCollection matches = Regex.Matches(data, pattern, RegexOptions.Singleline);

        if (matches.Count >= 2) // 2回目のマッチが存在するか確認
        {
            
            string matrixContent = matches[1].Groups[1].Value;
            //Debug.log("マッチしました:" + matrixContent);
            // 4行目の数値を処理
            string[] lines = matrixContent.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
            //Debug.log("要素数は" + lines.Length);
            if (lines.Length >= 16) // 4行目の数値があるか確認
            {
                //Debug.log("4行目を確認しました。");
                for (int i = 13; i < 16; i++) // 4行目の数値（3つ）を処理
                {
                    lines[i] = lines[i].TrimStart('-'); // マイナス符号を取り除く
                    //Debug.log("符号排除処理をしました");
                }

                // 新しい内容を生成
                string newMatrixContent = string.Join(",", lines);
                string output = data.Replace(matrixContent, newMatrixContent);
                //Debug.log($"書き換えました: {output.Substring(output.Length - 10)}");
                return output;
            }
            else
            {
                //Debug.log("4行目の数値が見つかりませんでした。");
            }
        }
        else
        {
            //Debug.log("FrameTransformMatrixが見つかりませんでした。");
        }



        return data;
    }


    void OutputFrameTransformMatrix(Assimp.Node node, string fileName)
    {
        //Debug.log($"ファイル: {fileName} ");
        var transform = node.Transform;
        try
        {
            if (transform.A1 < 0 || transform.A2 < 0 || transform.A3 < 0 ||
            transform.B1 < 0 || transform.B2 < 0 || transform.B3 < 0 ||
            transform.C1 < 0 || transform.C2 < 0 || transform.C3 < 0)
            {
                
                //Debug.log($"マイナス発見: {fileName} has negative Transform values: {transform}");
            }else{
                //Debug.log($"マイナスなし: {fileName} has negative Transform values: {transform}");
            }    
        }catch (System.Exception e)
                {
                    //Debug.log($"発見 {fileName}: {e.Message}");
                    return;
                }

    }
    public void setPose(hod2v0 pose)
    {
        if (pose.parts.Count == parts.Count)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                parts[i].transform.localPosition = pose.parts[i].position;
                parts[i].transform.localRotation = pose.parts[i].rotation;
                parts[i].transform.localScale = pose.parts[i].scale;
            }
        }
    }

    public void setPose(hod2v1 pose)
    {
        if (pose.parts.Count == parts.Count)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                parts[i].transform.localPosition = pose.parts[i].position;
                parts[i].transform.localRotation = pose.parts[i].rotation;
                parts[i].transform.localScale = pose.parts[i].scale;
            }
        }
    }

    public void setPose(int AnimID, int HodID)
    {
        if (ani != null)
        {
            setPose(ani.animations[AnimID].frames[HodID]);
        }
    }
    public void updateHod(int AnimID, int HodID)
    {
        for (int i = 0; i < parts.Count; i++)
        {
            updatePart(AnimID, HodID, i);
        }
    }
    public void updatePart(int AnimID, int HodID, int prtID, bool syncRotCont = true)
    {
        if (ani != null)
        {
            hod2v1_Part prt = ani.animations[AnimID].frames[HodID].parts[prtID];
            prt.position = parts[prtID].transform.localPosition;
            prt.rotation = parts[prtID].transform.localRotation;
            prt.scale = parts[prtID].transform.localScale;
            if (syncRotCont)
            {
                prt.unk1 = parts[prtID].transform.localRotation;
                prt.unk2 = parts[prtID].transform.localRotation;
                prt.unk3 = parts[prtID].transform.localRotation;
            }
            ani.animations[AnimID].frames[HodID].parts[prtID] = prt;
        }
    }
    public void updatePart(int prtID, hod2v1_Part prt, Space space = Space.Self)
    {
        if (space == Space.Self)
        {
            parts[prtID].transform.localPosition = prt.position;
            parts[prtID].transform.localRotation = prt.rotation;
            parts[prtID].transform.localScale = prt.scale;
        }
        else
        {
            parts[prtID].transform.position = prt.position;
            parts[prtID].transform.rotation = prt.rotation;
            parts[prtID].transform.localScale = prt.scale;
        }
    }    
    public void updatePart(int AnimID, int HodID, int prtID, hod2v1_Part prt, Space space = Space.Self)
    {
        if (space == Space.Self)
        {
            parts[prtID].transform.localPosition = prt.position;
            parts[prtID].transform.localRotation = prt.rotation;
            parts[prtID].transform.localScale = prt.scale;
        }
        else
        {
            parts[prtID].transform.position = prt.position;
            parts[prtID].transform.rotation = prt.rotation;
            parts[prtID].transform.localScale = prt.scale;
        }
        updatePart(AnimID, HodID, prtID);
        
    }

    public void updateConstraints(int AnimID, int HodID, int prtID, Quaternion c1, Quaternion c2, Quaternion c3)
    {
        if (ani != null)
        {
            hod2v1_Part prt = ani.animations[AnimID].frames[HodID].parts[prtID];
            prt.unk1 = c1;
            prt.unk2 = c2;
            prt.unk3 = c3;
            ani.animations[AnimID].frames[HodID].parts[prtID] = prt;
        }
    }
    public void addPart(string partName, int parent)
    {
        if (ani != null)
        {
            ani.addPart(partName, parent);
            buildStructure(ani.structure);
        }
        else
        {
            for (int i = 0; i < hod.parts.Count; i++)
            {
                hod2v0_Part prt = hod.parts[i];
                prt.position = parts[parent].transform.localPosition;
                prt.rotation = parts[parent].transform.localRotation;
                prt.scale = parts[parent].transform.localScale;
            }
            int level = hod.parts[parent].treeDepth + 1;
            hod2v0_Part pHod = hod.parts[parent];
            pHod.childCount++;
            hod.parts[parent] = pHod;
            hod2v0_Part nPart = new hod2v0_Part();
            nPart.name = partName;
            nPart.treeDepth = level;
            nPart.flag = 1;
            nPart.unk = new Vector3(1, 1, 1);
            nPart.position = new Vector3(0, 0, 0);
            nPart.rotation = new Quaternion();
            nPart.scale = new Vector3(1, 1, 1);
            int j = parent + 1;
            for (; j < hod.parts.Count; j++)
            {
                if (hod.parts[j].treeDepth <= hod.parts[parent].treeDepth)
                {
                    break;
                }
            }
            hod.parts.Insert(j, nPart);

            buildStructure(hod);
        }

    }

    public bool removePart(int index)
    {
        if (ani != null)
        {
            if (ani.removePart(index))
                buildStructure(ani.structure);
            else
                return false;

            return true;
        }
        else if (hod.parts[index].childCount == 0)
        {
            for (int i = 0; i < hod.parts.Count; i++)
            {
                hod2v0_Part prt = hod.parts[i];
                prt.position = parts[index].transform.localPosition;
                prt.rotation = parts[index].transform.localRotation;
                prt.scale = parts[index].transform.localScale;
            }

            int j = index;
            for (; j >= 0; j--)
            {
                if (hod.parts[j].treeDepth < hod.parts[index].treeDepth)
                {
                    hod2v0_Part pHod = hod.parts[j];
                    pHod.childCount--;
                    hod.parts[j] = pHod;
                    hod.parts.RemoveAt(index);
                    break;
                }
            }
            buildStructure(hod);
            return true;
        }
        else
            return false;
    }

    public void renamePart(int index, string name)
    {
        
        if (ani != null)
        {
            hod2v0_Part prt = ani.structure.parts[index];
            prt.name = name;
            ani.structure.parts[index] = prt;
            buildStructure(ani.structure);
        }
        else
        {
            hod2v0_Part prt = hod.parts[index];
            prt.name = name;
            hod.parts[index] = prt;
            buildStructure(hod);
        }
        
    }

    public hod2v1 createHod2v1()
    {
        hod2v1 Pose = new hod2v1("Copy");
        Pose.parts = new List<hod2v1_Part>();
        for (int i = 0; i < parts.Count; i++)
        {
            hod2v1_Part prt = new hod2v1_Part();
            prt.position = parts[i].transform.localPosition;
            prt.rotation = parts[i].transform.localRotation;
            prt.scale = parts[i].transform.localScale;
            prt.unk1 = parts[i].transform.localRotation;
            prt.unk2 = parts[i].transform.localRotation;
            prt.unk3 = parts[i].transform.localRotation;
            Pose.parts.Add(prt);

        }
        return Pose;
    }


    public void saveAni()
    {
        ani.save();
    }

    public void saveHOD1()
    {
        hod1 sHOD = new hod1("");
        for (int i = 0; i < parts.Count; i++)
        {
            hod2v0_Part prt = hod.parts[i];
            prt.position = parts[i].transform.localPosition;
            prt.rotation = parts[i].transform.localRotation;
            prt.scale = parts[i].transform.localScale;
            hod.parts[i] = prt;
        }
        sHOD.createFromHod2v0(hod);
        BinaryWriter bw = new BinaryWriter(File.Open(filename, FileMode.OpenOrCreate, FileAccess.ReadWrite));
        sHOD.saveToBinary(ref bw);
        bw.Close();
    }

    public void saveHOD1(string customFilename)
    {
        string dText = "HODファイルを書き出す";
        dText = customFilename;
        inputBox.openDialog("新しいHODファイルの名前を入力してください（拡張子.hodは除く）。", dText , (string rText) =>
        {
            hod1 sHOD = new hod1("");
            for (int i = 0; i < parts.Count; i++)
            {
                hod2v0_Part prt = hod.parts[i];
                prt.position = parts[i].transform.localPosition;
                prt.rotation = parts[i].transform.localRotation;
                prt.scale = parts[i].transform.localScale;
                hod.parts[i] = prt;
            }
            sHOD.createFromHod2v0(hod);
            rText = rText + ".hod";
            string savePath = Path.Combine(folder, rText);
            BinaryWriter bw = new BinaryWriter(File.Open(savePath, FileMode.OpenOrCreate, FileAccess.ReadWrite));
            sHOD.saveToBinary(ref bw);
            bw.Close();
         });
    }

    public void LoadHOD()
    {
        // folder内の.hodファイルを取得
        string[] hodFiles = Directory.GetFiles(folder, "*.hod");
        if (hodFiles.Length == 0)
        {
            Debug.LogError($"フォルダ内にHODファイルが見つかりません: {folder}");
            statusMessege.text = $"フォルダ内にHODファイルが見つかりません: {folder}";
            return;
        }

        // ファイル選択ダイアログを表示
        List<string> fileNames = hodFiles.Select(f => Path.GetFileName(f)).ToList();
        inputBox.openSelectDialog("読み込むHODファイルを選択してください", 
            fileNames,
            (string selectedFile) =>
            {
                string hodFilePath = Path.Combine(folder, selectedFile);
                try
                {
                    BinaryReader br = new BinaryReader(File.Open(hodFilePath, FileMode.Open, FileAccess.Read));
                    try
                    {
                        // シグネチャを確認
                        string signature = new string(br.ReadChars(3));
                        br.BaseStream.Seek(0, SeekOrigin.Begin); // ストリームを先頭に戻す

                        hod2v1 nFrame = new hod2v1(selectedFile);
                        nFrame.parts = new List<hod2v1_Part>();

                        if (signature == "HOD")
                        {
                            // hod1形式の場合
                            hod1 oldStructure = new hod1(selectedFile);
                            if (oldStructure.loadFromBinary(ref br))
                            {
                                // hod2v0形式に変換
                                hod2v0 convertedHOD = oldStructure.convertToHod2v0();
                                // パーツ情報をコピー
                                for (int i = 0; i < convertedHOD.parts.Count; i++)
                                {
                                    hod2v1_Part nPart = new hod2v1_Part();
                                    nPart.name = convertedHOD.parts[i].name;
                                    nPart.treeDepth = convertedHOD.parts[i].treeDepth;
                                    nPart.childCount = convertedHOD.parts[i].childCount;
                                    nPart.position = convertedHOD.parts[i].position;
                                    nPart.rotation = convertedHOD.parts[i].rotation;
                                    nPart.scale = convertedHOD.parts[i].scale;
                                    nPart.unk1 = nPart.rotation;
                                    nPart.unk2 = nPart.rotation;
                                    nPart.unk3 = nPart.rotation;
                                    nFrame.parts.Add(nPart);
                                }
                            }
                            else
                            {
                                Debug.LogError($"HODファイルの読み込みに失敗しました: {selectedFile}");
                                statusMessege.text = $"HODファイルの読み込みに失敗しました: {selectedFile}";
                                return;
                            }
                        }
                        else
                        {
                            // hod2v0形式の場合
                            hod2v0 loadedHOD = new hod2v0(selectedFile);
                            if (loadedHOD.loadFromBinary(ref br))
                            {
                                // パーツ情報をコピー
                                for (int i = 0; i < loadedHOD.parts.Count; i++)
                                {
                                    hod2v1_Part nPart = new hod2v1_Part();
                                    nPart.name = loadedHOD.parts[i].name;
                                    nPart.treeDepth = loadedHOD.parts[i].treeDepth;
                                    nPart.childCount = loadedHOD.parts[i].childCount;
                                    nPart.position = loadedHOD.parts[i].position;
                                    nPart.rotation = loadedHOD.parts[i].rotation;
                                    nPart.scale = loadedHOD.parts[i].scale;
                                    nPart.unk1 = nPart.rotation;
                                    nPart.unk2 = nPart.rotation;
                                    nPart.unk3 = nPart.rotation;
                                    nFrame.parts.Add(nPart);
                                }
                            }
                            else
                            {
                                Debug.LogError($"HODファイルの読み込みに失敗しました: {selectedFile}");
                                statusMessege.text = $"HODファイルの読み込みに失敗しました: {selectedFile}";
                                return;
                            }
                        }

                        // パーツ数の比較
                        if (nFrame.parts.Count != parts.Count)
                        {
                            Debug.LogWarning($"読み込んだHODファイルのパーツ数({nFrame.parts.Count})が現在のパーツ数({parts.Count})と異なります。読み込みを中止します。");
                            statusMessege.text = $"読み込んだHODファイルのパーツ数({nFrame.parts.Count})が現在のパーツ数({parts.Count})と異なります。読み込みを中止します。";
                            return;
                        }
                        
                        // UI_EditAniから現在の選択位置を取得
                        UI_EditAni editAni = FindObjectOfType<UI_EditAni>();
                        if (editAni != null)
                        {
                            int currentAnimIndex = 0; // 現在選択されているアニメーションのインデックス
                            int currentHodIndex = 0; // 現在選択されているHODのインデックス
                            
                            currentAnimIndex = editAni.animDD.value;
                            currentHodIndex = editAni.hodDD.value;
                            
                            // 現在のアニメーションのフレームリストに新しいフレームを追加
                            if (currentAnimIndex < ani.animations.Count)
                            {
                                if (ani.animations[currentAnimIndex].frames.Count > 0)
                                {
                                    ani.animations[currentAnimIndex].frames.Insert(currentHodIndex + 1, nFrame);
                                }
                                else
                                {
                                    ani.animations[currentAnimIndex].frames.Add(nFrame);
                                }

                                // HODリストを更新
                                editAni.populateHODList();
                            }
                        }
                    }
                    finally
                    {
                        br.Close();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"HODファイルの読み込み中にエラーが発生しました: {e.Message}");
                    statusMessege.text = $"HODファイルの読み込み中にエラーが発生しました: {e.Message}";
                }
            });
    }
    
}
