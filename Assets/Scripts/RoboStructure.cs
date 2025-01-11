using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;
using System.IO;
using Assets;
using System.Linq;
public delegate void Update_Event();
public class RoboStructure : MonoBehaviour
{
    public GameObject root;
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
                    //Debug.LogWarning($"Failed to import model: {file}. Attempting partial import.");
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

                    materials[index] = mat;
                }

                GO.AddComponent<MeshFilter>().mesh = mesh;
                //part.AddComponent<MeshCollider>().sharedMesh = mesh; 
                GO.AddComponent<MeshRenderer>().materials = materials;
            }
            catch (Exception e)
            {
                //Debug.Log($"Error importing model {file}: {e.Message}. Attempting partial import.");
                // ここで部分的なデータを取得するための処理を追加
            }
        }
    }

    void ImportModelEncrypted(GameObject GO, string file)
    {
        //Debug.Log($"Attempting to import encrypted model: {file}");
        
        //if (File.Exists(file))
        //{
            try
            {
                string Modelpath = Path.GetDirectoryName(file);
                //Debug.Log("パス取得:" + file);
                byte[] data = transcoder.Transcode(file);
                byte[] data3;
                if (data != null && data.Length > 0)
                {
                    string data1 = System.Text.Encoding.GetEncoding("utf-8").GetString(data);
                    //Debug.Log("未処理の" + file + "のデータ:" + data1);
                    data1 = XfileStringConverter(data1);
                    //Debug.Log("処理済みの" + file + "のデータ:" + data1);
                    data3 = System.Text.Encoding.GetEncoding("utf-8").GetBytes(data1);
                }
                else
                {
                    //Debug.Log($"Failed to decrypt file: {file}. Attempting partial import.");
                    return;
                }
                //Debug.Log("MemoryStream取得開始:" + file);
                MemoryStream ms = new MemoryStream(data3);
                //Debug.Log("MemoryStream取得完了:" + ms + data3);
                //Debug.Log("SCEN取得開始:" + file);
                Assimp.Scene scen = null;
                try
                {
                    scen = Importer.ImportFileFromStream(ms, Helper.PostProcessStepflags, "x");
                }
                catch (System.Exception e)
                {
                    //Debug.Log($"なんかアカン {file}: {e.Message}");
                }
                //Debug.Log("SCEN取得完了:" + file);
                if (scen == null)
                {
                    //Debug.Log($"Failed to import model from stream: {file}. Attempting partial import.");
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
                    //Debug.Log($"Failed to combine meshes for {file}: {e.Message}");
                    return;
                }
                
                Material[] materials = new Material[scen.Meshes.Length];

                for (int index = 0; index < materials.Length; index++)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));

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
                                mat.mainTexture = Helper.LoadTextureEncrypted(Path.Combine(Modelpath, textures[0].FilePath), ref transcoder);
                            }
                            catch (System.Exception e)
                            {
                                //Debug.LogWarning($"Failed to load texture for {file}: {e.Message}");
                            }
                        }
                    }

                    materials[index] = mat;
                }
                GO.AddComponent<MeshFilter>().mesh = mesh;
                //part.AddComponent<MeshCollider>().sharedMesh = mesh; 
                GO.AddComponent<MeshRenderer>().materials = materials;
                //Debug.Log($"Successfully imported model: {file}");

            }
            catch (Exception e)
            {
                //Debug.Log($"Error processing model {file}: {e.Message}. Attempting partial import.");
                
                // ここで部分的なデータを取得するための処理を追加
            }
        //}
        //else
        //{
        //    Debug.Log($"File not found: {file}");  
        //}
    }


    public string XfileStringConverter(string data)
    {
        if (!data.Trim().EndsWith("}"))
        {
            //Debug.Log("文字化けを確認しました");
            int lastBraceIndex = data.LastIndexOf('}');
            if (lastBraceIndex != -1)
            {
                data = data.Substring(0, lastBraceIndex + 1); // 最後の波括弧を残す
            }
            data += "}"; // 新たに波括弧
            //Debug.Log("文字化けを対応しました。");
        }
        // FrameTransformMatrixの部分を探す正規表現        
        string pattern = @"FrameTransformMatrix\s*{([^}]*)}";
        MatchCollection matches = Regex.Matches(data, pattern, RegexOptions.Singleline);

        if (matches.Count >= 2) // 2回目のマッチが存在するか確認
        {
            
            string matrixContent = matches[1].Groups[1].Value;
            //Debug.Log("マッチしました:" + matrixContent);
            // 4行目の数値を処理
            string[] lines = matrixContent.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
            //Debug.Log("要素数は" + lines.Length);
            if (lines.Length >= 16) // 4行目の数値があるか確認
            {
                //Debug.Log("4行目を確認しました。");
                for (int i = 13; i < 16; i++) // 4行目の数値（3つ）を処理
                {
                    lines[i] = lines[i].TrimStart('-'); // マイナス符号を取り除く
                    //Debug.Log("符号排除処理をしました");
                }

                // 新しい内容を生成
                string newMatrixContent = string.Join(",", lines);
                string output = data.Replace(matrixContent, newMatrixContent);
                //Debug.Log($"書き換えました: {output.Substring(output.Length - 10)}");
                return output;
            }
            else
            {
                //Debug.Log("4行目の数値が見つかりませんでした。");
            }
        }
        else
        {
            //Debug.Log("FrameTransformMatrixが見つかりませんでした。");
        }



        return data;
    }


    void OutputFrameTransformMatrix(Assimp.Node node, string fileName)
    {
        //Debug.Log($"ファイル: {fileName} ");
        var transform = node.Transform;
        try
        {
            if (transform.A1 < 0 || transform.A2 < 0 || transform.A3 < 0 ||
            transform.B1 < 0 || transform.B2 < 0 || transform.B3 < 0 ||
            transform.C1 < 0 || transform.C2 < 0 || transform.C3 < 0)
            {
                
                //Debug.Log($"マイナス発見: {fileName} has negative Transform values: {transform}");
            }else{
                //Debug.Log($"マイナスなし: {fileName} has negative Transform values: {transform}");
            }    
        }catch (System.Exception e)
                {
                    //Debug.Log($"発見 {fileName}: {e.Message}");
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
    
}
