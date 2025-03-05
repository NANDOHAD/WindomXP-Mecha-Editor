using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using System.Threading.Tasks;
public class ani2
{
    public hod2v0 structure;
    public List<animation> animations;
    public string _filename;
    private BinaryReader br; // BinaryReaderをクラスのフィールドとして定義

    public async Task<bool> load(string filename, IProgress<int> progress = null) // IProgress<int>を追加
    {
        _filename = filename;
        using (br = new BinaryReader(File.Open(filename, FileMode.Open, FileAccess.Read)))
        {
            return await Task.Run(() => 
            {
                try
                {
                    string signature = new string(br.ReadChars(3));
                    if (signature == "AN2")
                    {
                        animations = new List<animation>();
                        string robohod = USEncoder.ToEncoding.ToUnicode(br.ReadBytes(256)).TrimEnd('\0');
                        structure = new hod2v0(robohod);
                        structure.loadFromBinary(ref br); // refを使用して渡す

                        int aCount = br.ReadInt32();
                        for (int i = 0; i < aCount; i++)
                        {
                            try
                            {
                                animation aData = new animation();
                                aData.loadFromAni(ref br, ref structure); // refを使用して渡す
                                animations.Add(aData);
                                progress?.Report((i + 1) * 100 / aCount); // 進捗を報告
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"Animation loading failed at index {i}: {ex.Message}");
                            }
                        }
                        return true; // 成功した場合は true を返す
                    }
                    else if (signature == "ANI")
                    {
                        using (StreamWriter debug = new StreamWriter("debug.txt"))
                        {
                            animations = new List<animation>();
                            string robohod = USEncoder.ToEncoding.ToUnicode(br.ReadBytes(256)).TrimEnd('\0');
                            hod1 oldStructure = new hod1(robohod);
                            oldStructure.loadFromBinary(ref br); // refを使用して渡す
                            structure = oldStructure.convertToHod2v0();
                            debug.WriteLine(br.BaseStream.Position.ToString());
                        }
                        for (int i = 0; i < 200; i++)
                        { 
                            try
                            {
                                animation aData = new animation();
                                aData.loadFromAniOld(ref br); // refを使用して渡す
                                animations.Add(aData);
                                progress?.Report((i + 1) * 100 / 200); // 進捗を報告
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"Old animation loading failed at index {i}: {ex.Message}");
                            }
                        }
                        return true; // 成功した場合は true を返す
                    }
                    else if (signature == "HOD")
                    {
                        br.BaseStream.Seek(0, SeekOrigin.Begin);
                        animations = new List<animation>();
                        hod1 hodfile = new hod1("HOD1 FILE");
                        hodfile.loadFromBinary(ref br); // refを使用して渡す
                        structure = hodfile.convertToHod2v0();
                        animation aData = new animation();
                        aData.frames = new List<hod2v1>();
                        aData.frames.Add(hodfile.convertToHod2v1());
                        aData.scripts = new List<script>();
                        animations.Add(aData);
                        return true; // 成功した場合は true を返す
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error loading file {filename}: {ex.Message}");
                }
                return false; // どの条件にも合致しない場合は false を返す
            });
        }
    }

    public void save(string filename = "")
    {
        if (filename == "")
            filename = _filename;
        //Encoding ShiftJis = Encoding.GetEncoding(932);
        if (File.Exists(filename))
            File.Delete(filename);
        BinaryWriter bw = new BinaryWriter(File.Open(filename, FileMode.OpenOrCreate, FileAccess.ReadWrite));
        bw.Write(ASCIIEncoding.ASCII.GetBytes("AN2"));
        byte[] shiftjistext = USEncoder.ToEncoding.ToSJIS(structure.filename);
        bw.Write(shiftjistext);
        bw.BaseStream.Seek(256 - shiftjistext.Length, SeekOrigin.Current);
        structure.saveToBinary(ref bw);

        bw.Write(animations.Count);
        for (int i = 0; i < animations.Count; i++)
        {
            animations[i].saveToAni(ref bw);
        }

        bw.Close();
    }

    public void addPart(string partName, int parent)
    {
        //Debug.Log(structure.parts.Count);
        int level = structure.parts[parent].treeDepth + 1;
        hod2v0_Part pHod = structure.parts[parent];
        pHod.childCount++;
        structure.parts[parent] = pHod;
        hod2v0_Part nPart = new hod2v0_Part();
        nPart.name = partName;
        nPart.treeDepth = level;
        nPart.flag = 1;
        nPart.unk = new Vector3(1, 1, 1);
        nPart.position = new Vector3(0, 0, 0);
        nPart.rotation = new Quaternion(0, 0, 0, 1);
        nPart.scale = new Vector3(1, 1, 1);
        int i = parent + 1;
        for (; i < structure.parts.Count; i++)
        {
            if (structure.parts[i].treeDepth  <= structure.parts[parent].treeDepth)
            {
                break;
            }
        }
        structure.parts.Insert(i, nPart);
        //Debug.Log(structure.parts.Count);
        //Debug.Log(partName);
        hod2v1_Part nPart1 = new hod2v1_Part();
        nPart1.name = partName;
        nPart1.treeDepth = level;
        nPart1.position = new Vector3(0,0,0);
        nPart1.rotation = new Quaternion(0, 0, 0, 1);
        nPart1.scale = new Vector3(1, 1, 1);
        nPart1.unk1 = new Quaternion();
        nPart1.unk2 = new Quaternion();
        nPart1.unk3 = new Quaternion();
        for (int j = 0; j < animations.Count;j++)
        {
            for (int k = 0; k < animations[j].frames.Count; k++)
            {
                hod2v1_Part pHod1 = animations[j].frames[k].parts[parent];
                pHod1.childCount++;
                animations[j].frames[k].parts[parent] = pHod1;
                animations[j].frames[k].parts.Insert(i, nPart1);
            }
        }
    }

    public bool removePart(int index)
    {
        if (structure.parts[index].childCount == 0)
        {
            int i = index;
            for (; i >= 0; i--)
            {
                if (structure.parts[i].treeDepth < structure.parts[index].treeDepth)
                {
                    hod2v0_Part pHod = structure.parts[i];
                    pHod.childCount--;
                    structure.parts[i] = pHod;
                    structure.parts.RemoveAt(index);
                    break;
                }
            }

            for (int j = 0; j < animations.Count; j++)
            {
                for (int k = 0; k < animations[j].frames.Count; k++)
                {
                    hod2v1_Part pHod1 = animations[j].frames[k].parts[i];
                    pHod1.childCount--;
                    animations[j].frames[k].parts[i] = pHod1;
                    animations[j].frames[k].parts.RemoveAt(index);
                }
            }

        }
        else
            return false;
        return true;
    }
}

