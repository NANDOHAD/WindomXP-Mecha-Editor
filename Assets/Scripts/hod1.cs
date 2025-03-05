using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public struct hod1_Part
{
    public int treeDepth;
    public int childCount;
    public string name;
    public Matrix4x4 transform;
}

public class hod1
{
    string filename;
    public List<hod1_Part> parts;
    public hod1(string name)
    {
        filename = name;
    }
    public bool loadFromBinary(ref BinaryReader br)
    {
        string signature = new string(br.ReadChars(3));
        if (signature != "HOD")
        {
            Debug.LogWarning("Warning: 署名が 'HOD' ではありません。強制的に 'HOD' として処理を続行します。");
            // ここで署名を「HOD」として扱う
            // 必要に応じて、他の処理を追加することもできます
        }
        
        parts = new List<hod1_Part>();
        int partCount = br.ReadInt32();
        
        if (partCount <= 0)
        {
            Debug.LogError("Error: パーツ数が0または負の値です。");
            return false;
        }

        for (int i = 0; i < partCount; i++)
        {
            hod1_Part nPart = new hod1_Part();
            nPart.treeDepth = br.ReadInt32();
            nPart.childCount = br.ReadInt32();
            nPart.name = ASCIIEncoding.ASCII.GetString(br.ReadBytes(256)).TrimEnd('\0');

            if (string.IsNullOrEmpty(nPart.name))
            {
                Debug.LogWarning($"Warning: パーツ {i} の名前が空です。仮の名前 'NoNamedParts' を設定します。");
                nPart.name = "NoNamedParts";
            }

            nPart.transform = new Matrix4x4();
            nPart.transform.m00 = br.ReadSingle();
            nPart.transform.m10 = br.ReadSingle();
            nPart.transform.m20 = br.ReadSingle(); 
            nPart.transform.m30 = br.ReadSingle();
            nPart.transform.m01 = br.ReadSingle();
            nPart.transform.m11 = br.ReadSingle();
            nPart.transform.m21 = br.ReadSingle();
            nPart.transform.m31 = br.ReadSingle();
            nPart.transform.m02 = br.ReadSingle();
            nPart.transform.m12 = br.ReadSingle();
            nPart.transform.m22 = br.ReadSingle();
            nPart.transform.m32 = br.ReadSingle();
            nPart.transform.m03 = br.ReadSingle();
            nPart.transform.m13 = br.ReadSingle();
            nPart.transform.m23 = br.ReadSingle();
            nPart.transform.m33 = br.ReadSingle();
            parts.Add(nPart);
        }

        return true;
    }

    public void saveToBinary(ref BinaryWriter bw)
    {
        bw.Write(ASCIIEncoding.ASCII.GetBytes("HOD"));
        bw.Write(parts.Count);
        for (int i = 0; i < parts.Count; i++)
        {
            bw.Write(parts[i].treeDepth);
            bw.Write(parts[i].childCount);
            byte[] text = ASCIIEncoding.ASCII.GetBytes(parts[i].name);
            bw.Write(text);
            bw.BaseStream.Seek(256 - text.Length, SeekOrigin.Current);
            bw.Write(parts[i].transform.m00);
            bw.Write(parts[i].transform.m10);
            bw.Write(parts[i].transform.m20);
            bw.Write(parts[i].transform.m30);
            bw.Write(parts[i].transform.m01);
            bw.Write(parts[i].transform.m11);
            bw.Write(parts[i].transform.m21);
            bw.Write(parts[i].transform.m31);
            bw.Write(parts[i].transform.m02);
            bw.Write(parts[i].transform.m12);
            bw.Write(parts[i].transform.m22);
            bw.Write(parts[i].transform.m32);
            bw.Write(parts[i].transform.m03);
            bw.Write(parts[i].transform.m13);
            bw.Write(parts[i].transform.m23);
            bw.Write(parts[i].transform.m33);
        }
    }

    public hod2v0 convertToHod2v0()
    {
        hod2v0 hod = new hod2v0(filename);
        hod.parts = new List<hod2v0_Part>();
        for (int i = 0; i < parts.Count; i++)
        {
            hod2v0_Part nPart = new hod2v0_Part();
            nPart.treeDepth = parts[i].treeDepth;
            nPart.childCount = parts[i].childCount;
            nPart.name = parts[i].name;
            nPart.rotation = Utils.GetRotation(parts[i].transform);
            nPart.scale = Utils.GetScale(parts[i].transform);
            nPart.position = Utils.GetPosition(parts[i].transform);
            nPart.flag = 1;
            nPart.unk = new Vector3(1.0f, 1.0f, 1.0f);
            hod.parts.Add(nPart);  
        }
        return hod;
    }

    public hod2v1 convertToHod2v1()
    {
        hod2v1 hod = new hod2v1(filename);
        hod.parts = new List<hod2v1_Part>();
        for (int i = 0; i < parts.Count; i++)
        {
            hod2v1_Part nPart = new hod2v1_Part();
            nPart.treeDepth = parts[i].treeDepth;
            nPart.childCount = parts[i].childCount;
            nPart.name = parts[i].name;
            nPart.rotation = Utils.GetRotation(parts[i].transform);
            nPart.scale = Utils.GetScale(parts[i].transform);
            nPart.position = Utils.GetPosition(parts[i].transform);
            nPart.unk1 = Utils.GetRotation(parts[i].transform);
            nPart.unk2 = Utils.GetRotation(parts[i].transform);
            nPart.unk3 = Utils.GetRotation(parts[i].transform);
            hod.parts.Add(nPart);
        }
        return hod;
    }

    public void createFromHod2v0(hod2v0 hod)
    {
        filename = hod.filename;
        parts = new List<hod1_Part>();
        for (int i = 0; i < hod.parts.Count; i++)
        {
            hod1_Part nPart = new hod1_Part();
            nPart.name = hod.parts[i].name;
            nPart.treeDepth = hod.parts[i].treeDepth;
            nPart.childCount = hod.parts[i].childCount;
            nPart.transform.SetTRS(hod.parts[i].position, hod.parts[i].rotation, hod.parts[i].scale);
            parts.Add(nPart);
            
        }
    }
    public void createFromHod2v1(hod2v1 hod)
    {
        filename = hod.filename;
        parts = new List<hod1_Part>();
        for (int i = 0; i < hod.parts.Count; i++)
        {
            hod1_Part nPart = new hod1_Part();
            nPart.name = hod.parts[i].name;
            nPart.treeDepth = hod.parts[i].treeDepth;
            nPart.childCount = hod.parts[i].childCount;
            nPart.transform.SetTRS(hod.parts[i].position,hod.parts[i].rotation,hod.parts[i].scale);
            parts.Add(nPart);
        }
    }
}

