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

    public async Task<bool> load(string filename, IProgress<int> progress = null)
    {
        _filename = filename;
        return await Task.Run(() =>
        {
            try
            {
                BinaryReader br = new BinaryReader(File.Open(filename, FileMode.Open, FileAccess.Read));
                using (br)
                {
                    return LoadFromReader(ref br, filename, progress);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading file {filename}: {ex.Message}");
                return false;
            }
        });
    }

    bool LoadFromReader(ref BinaryReader br, string filename, IProgress<int> progress)
    {
        string signature = new string(br.ReadChars(3));
        if (signature == "AN2")
        {
            animations = new List<animation>();
            string robohod = USEncoder.ToEncoding.ToUnicode(br.ReadBytes(256)).TrimEnd('\0');
            structure = new hod2v0(robohod);
            if (!structure.loadFromBinary(ref br))
                return false;

            int aCount = br.ReadInt32();
            for (int i = 0; i < aCount; i++)
            {
                try
                {
                    animation aData = new animation();
                    aData.loadFromAni(ref br, ref structure);
                    animations.Add(aData);
                    if (aCount > 0)
                        progress?.Report((i + 1) * 100 / aCount);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Animation loading failed at index {i}: {ex.Message}");
                    return false;
                }
            }
            return true;
        }

        if (signature == "ANI")
        {
            using (StreamWriter debug = new StreamWriter("debug.txt"))
            {
                animations = new List<animation>();
                string robohod = USEncoder.ToEncoding.ToUnicode(br.ReadBytes(256)).TrimEnd('\0');
                hod1 oldStructure = new hod1(robohod);
                if (!oldStructure.loadFromBinary(ref br))
                    return false;
                structure = oldStructure.convertToHod2v0();
                debug.WriteLine(br.BaseStream.Position.ToString());
            }

            for (int i = 0; i < 200; i++)
            {
                try
                {
                    animation aData = new animation();
                    aData.loadFromAniOld(ref br);
                    animations.Add(aData);
                    progress?.Report((i + 1) * 100 / 200);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Old animation loading failed at index {i}: {ex.Message}");
                    return false;
                }
            }
            return true;
        }

        if (signature == "HOD")
        {
            br.BaseStream.Seek(0, SeekOrigin.Begin);
            animations = new List<animation>();
            hod1 hodfile = new hod1("HOD1 FILE");
            if (!hodfile.loadFromBinary(ref br))
                return false;
            structure = hodfile.convertToHod2v0();
            animation aData = new animation();
            aData.frames = new List<hod2v1>();
            aData.frames.Add(hodfile.convertToHod2v1());
            aData.scripts = new List<script>();
            animations.Add(aData);
            return true;
        }

        Debug.LogError($"Unsupported file signature '{signature}' in {filename}");
        return false;
    }

    public void save(string filename = "")
    {
        if (filename == "")
            filename = _filename;

        string directory = Path.GetDirectoryName(Path.GetFullPath(filename));
        string tempFilename = Path.Combine(directory, Path.GetFileName(filename) + ".tmp");

        try
        {
            BinaryWriter bw = new BinaryWriter(File.Open(tempFilename, FileMode.Create, FileAccess.ReadWrite));
            try
            {
                bw.Write(ASCIIEncoding.ASCII.GetBytes("AN2"));
                WriteFixedSJIS(bw, structure.filename, 256);
                structure.saveToBinary(ref bw);

                bw.Write(animations.Count);
                for (int i = 0; i < animations.Count; i++)
                {
                    animations[i].saveToAni(ref bw);
                }
            }
            finally
            {
                bw.Close();
            }

            ReplaceFile(tempFilename, filename);
        }
        catch
        {
            if (File.Exists(tempFilename))
                File.Delete(tempFilename);
            throw;
        }
    }

    static void WriteFixedSJIS(BinaryWriter bw, string value, int byteLength)
    {
        byte[] text = USEncoder.ToEncoding.ToSJIS(value ?? "");
        if (text.Length > byteLength)
            throw new InvalidDataException($"Fixed string is too long: {text.Length}/{byteLength} bytes.");

        bw.Write(text);
        for (int i = text.Length; i < byteLength; i++)
            bw.Write((byte)0);
    }

    static void ReplaceFile(string tempFilename, string filename)
    {
        if (File.Exists(filename))
            File.Replace(tempFilename, filename, null);
        else
            File.Move(tempFilename, filename);
    }

    public void addPart(string partName, int parent)
    {
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
            if (structure.parts[i].treeDepth <= structure.parts[parent].treeDepth)
            {
                break;
            }
        }
        structure.parts.Insert(i, nPart);

        hod2v1_Part nPart1 = new hod2v1_Part();
        nPart1.name = partName;
        nPart1.treeDepth = level;
        nPart1.position = new Vector3(0, 0, 0);
        nPart1.rotation = new Quaternion(0, 0, 0, 1);
        nPart1.scale = new Vector3(1, 1, 1);
        nPart1.unk1 = new Quaternion();
        nPart1.unk2 = new Quaternion();
        nPart1.unk3 = new Quaternion();
        for (int j = 0; j < animations.Count; j++)
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
        if (structure == null || structure.parts == null || index <= 0 || index >= structure.parts.Count)
            return false;

        int parentIndex = FindParentIndex(structure.parts, index);
        if (parentIndex < 0)
            return false;

        int removeCount = GetSubtreeEndIndex(structure.parts, index) - index;
        if (!CanRemovePartRangeFromAllFrames(structure.parts, index, removeCount))
        {
            Debug.LogWarning("[ani2] HOD構造とアニメーションフレームのパーツ階層が一致しないため、パーツを削除できません。");
            return false;
        }

        hod2v0_Part parentPart = structure.parts[parentIndex];
        parentPart.childCount--;
        structure.parts[parentIndex] = parentPart;

        if (animations != null)
        {
            for (int animationIndex = 0; animationIndex < animations.Count; animationIndex++)
            {
                for (int frameIndex = 0; frameIndex < animations[animationIndex].frames.Count; frameIndex++)
                {
                    List<hod2v1_Part> frameParts = animations[animationIndex].frames[frameIndex].parts;
                    hod2v1_Part frameParentPart = frameParts[parentIndex];
                    frameParentPart.childCount = parentPart.childCount;
                    frameParts[parentIndex] = frameParentPart;
                }
            }
        }

        structure.parts.RemoveRange(index, removeCount);

        if (animations != null)
        {
            for (int animationIndex = 0; animationIndex < animations.Count; animationIndex++)
            {
                for (int frameIndex = 0; frameIndex < animations[animationIndex].frames.Count; frameIndex++)
                {
                    animations[animationIndex].frames[frameIndex].parts.RemoveRange(index, removeCount);
                }
            }
        }

        return true;
    }


    bool CanRemovePartRangeFromAllFrames(IList<hod2v0_Part> structureParts, int index, int removeCount)
    {
        if (removeCount <= 0 || animations == null)
            return removeCount > 0;

        for (int animationIndex = 0; animationIndex < animations.Count; animationIndex++)
        {
            animation animationData = animations[animationIndex];
            if (animationData == null || animationData.frames == null)
                return false;

            for (int frameIndex = 0; frameIndex < animationData.frames.Count; frameIndex++)
            {
                hod2v1 frame = animationData.frames[frameIndex];
                if (frame == null || frame.parts == null || frame.parts.Count != structureParts.Count)
                    return false;

                for (int partIndex = 0; partIndex < structureParts.Count; partIndex++)
                {
                    if (frame.parts[partIndex].treeDepth != structureParts[partIndex].treeDepth ||
                        frame.parts[partIndex].childCount != structureParts[partIndex].childCount)
                    {
                        return false;
                    }
                }
            }
        }

        return index + removeCount <= structureParts.Count;
    }


    static int GetSubtreeEndIndex(IList<hod2v0_Part> parts, int index)
    {
        int subtreeDepth = parts[index].treeDepth;
        int endIndex = index + 1;
        while (endIndex < parts.Count && parts[endIndex].treeDepth > subtreeDepth)
            endIndex++;
        return endIndex;
    }


    static int FindParentIndex(IList<hod2v0_Part> parts, int index)
    {
        int parentDepth = parts[index].treeDepth - 1;
        for (int i = index - 1; i >= 0; i--)
        {
            if (parts[i].treeDepth == parentDepth)
                return i;
        }
        return -1;
    }
}
