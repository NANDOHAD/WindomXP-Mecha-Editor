using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using UnityEngine;
public struct script
{
    public int unk;
    public float time;
    public string squirrel;
}
public class animation
{
    public string name;
    public string squirrelInit = "";
    public List<hod2v1> frames;
    public List<script> scripts;
    public void loadFromAni(ref BinaryReader br, ref hod2v0 structure)
    {
        frames = new List<hod2v1>();
        scripts = new List<script>();

        //load Name
        //Encoding ShiftJis = Encoding.GetEncoding(932);
        long position = br.BaseStream.Position;
        try
        {
            name = USEncoder.ToEncoding.ToUnicode(br.ReadBytes(256)).TrimEnd('\0');
            //Debug.Log(name);
        }
        catch
        {
            br.BaseStream.Seek(position + 256, SeekOrigin.Begin);
        }

        //Read Initial Script
        int textLength = br.ReadInt32();
        if (textLength != 0)
        {
            squirrelInit = USEncoder.ToEncoding.ToUnicode(br.ReadBytes(textLength));
        }

        //Read Hod Files
        int hodCount = br.ReadInt32();
        for (int i = 0; i < hodCount; i++)
        {
            short nameLength = br.ReadInt16();
            hod2v1 nHod = new hod2v1(USEncoder.ToEncoding.ToUnicode(br.ReadBytes(nameLength)));
            nHod.loadFromBinary(ref br, ref structure);
            frames.Add(nHod);
        }

        //Read Script Files
        int scriptCount = br.ReadInt32();
        for (int i = 0; i < scriptCount; i++)
        {
            script ns = new script();
            ns.unk = br.ReadInt32();
            ns.time = br.ReadSingle();
            textLength = br.ReadInt32();
            ns.squirrel = USEncoder.ToEncoding.ToUnicode(br.ReadBytes(textLength));
            scripts.Add(ns);
        }
    }

    public void loadFromAniOld(ref BinaryReader br)
    {
        LegacyAnimationSource ignored;
        loadFromAniOld(ref br, out ignored);
    }

    internal void loadFromAniOld(
        ref BinaryReader br,
        out LegacyAnimationSource legacySource)
    {
        frames = new List<hod2v1>();
        scripts = new List<script>();
        legacySource = new LegacyAnimationSource();

        //load Name
        //Encoding ShiftJis = Encoding.GetEncoding(932);
        legacySource.name = LegacyAniSourceValues.ReadFixedSjis(
            br, 256, "legacy animation name");
        name = legacySource.name.modelValue;

        //Read Hod Files
        int hodCount = br.ReadInt32();
        ValidateCount(hodCount, 100000, "legacy animation HOD count");
        for (int i = 0; i < hodCount; i++)
        {
            LegacyFixedStringSource frameName = LegacyAniSourceValues.ReadFixedSjis(
                br, 30, $"legacy animation HOD {i} filename");
            hod1 nHod = new hod1(frameName.modelValue);
            LegacyHodSource hodSource;
            if (!nHod.loadFromBinary(ref br, out hodSource))
                throw new InvalidDataException($"Legacy animation HOD {i} is invalid.");
            frames.Add(nHod.convertToHod2v1());
            legacySource.frameNames.Add(frameName);
            legacySource.frames.Add(hodSource);
        }

        //Read Script Files
        int scriptCount = br.ReadInt32();
        ValidateCount(scriptCount, 100000, "legacy animation script count");
        for (int i = 0; i < scriptCount; i++)
        {
            script ns = new script();
            ns.unk = br.ReadInt32();
            ns.time = br.ReadSingle();
            int textLength = br.ReadInt32();
            if (textLength < 0 || textLength > br.BaseStream.Length - br.BaseStream.Position)
                throw new InvalidDataException($"Legacy animation script {i} has an invalid text length: {textLength}.");
            byte[] textBytes = LegacyAniSourceValues.ReadRequiredBytes(
                br, textLength, $"legacy animation script {i} text");
            ns.squirrel = USEncoder.ToEncoding.ToUnicode(textBytes);
            scripts.Add(ns);
            legacySource.scripts.Add(new LegacyScriptSource
            {
                unk = ns.unk,
                time = ns.time,
                modelText = ns.squirrel,
                textBytes = textBytes
            });
        }
    }

    static void ValidateCount(int count, int maximum, string field)
    {
        if (count < 0 || count > maximum)
            throw new InvalidDataException($"Invalid {field}: {count}.");
    }

    public void saveToAni(ref BinaryWriter bw)
    {
        //Encoding ShiftJis = Encoding.GetEncoding(932);
        WriteFixedSJIS(bw, name, 256);
        byte[] shiftjistext = USEncoder.ToEncoding.ToSJIS(squirrelInit);
        bw.Write(shiftjistext.Length);
        if (shiftjistext.Length > 0)
            bw.Write(shiftjistext);
        bw.Write(frames.Count);

        for (int i = 0; i < frames.Count; i++)
        {
            frames[i].saveToBinary(ref bw);
        }

        bw.Write(scripts.Count);
        for (int i = 0; i < scripts.Count; i++)
        {
            bw.Write(scripts[i].unk);
            bw.Write(scripts[i].time);
            //shiftjistext = USEncoder.ToEncoding.ToSJIS(scripts[i].squirrel);
            //bw.Write(shiftjistext.Length);
            //if (shiftjistext.Length > 0)
            //    bw.Write(shiftjistext);
            List<byte> list = new List<byte>();
            list.AddRange(USEncoder.ToEncoding.ToSJIS(scripts[i].squirrel));
            for (int j = 0; j < list.Count; j++)
            {
                if (list[j] == 0x0A && (j == 0 || list[j - 1] != 0x0D))
                {
                    list.Insert(j, 0x0D);
                    j++;
                }
            }
            bw.Write(list.Count);
            bw.Write(list.ToArray());
        }
    }

    internal void saveToAniOld(
        ref BinaryWriter bw,
        LegacyAnimationSource legacySource)
    {
        LegacyAniSourceValues.WriteFixedSjis(
            bw, name, 256, legacySource != null ? legacySource.name : null);
        bw.Write(frames.Count);

        for (int i = 0; i < frames.Count; i++)
        {
            LegacyFixedStringSource frameNameSource = legacySource != null
                && i < legacySource.frameNames.Count
                ? legacySource.frameNames[i]
                : null;
            LegacyHodSource hodSource = legacySource != null
                && i < legacySource.frames.Count
                ? legacySource.frames[i]
                : null;
            LegacyAniSourceValues.WriteFixedSjis(
                bw, frames[i].filename, 30, frameNameSource);
            hod1.SaveFromHod2v1(ref bw, frames[i], hodSource);
        }

        bw.Write(scripts.Count);
        for (int i = 0; i < scripts.Count; i++)
        {
            script current = scripts[i];
            bw.Write(current.unk);
            bw.Write(current.time);

            LegacyScriptSource scriptSource = legacySource != null
                && i < legacySource.scripts.Count
                ? legacySource.scripts[i]
                : null;
            byte[] textBytes;
            if (scriptSource != null
                && current.unk == scriptSource.unk
                && current.time.Equals(scriptSource.time)
                && current.squirrel == scriptSource.modelText
                && scriptSource.textBytes != null)
            {
                textBytes = scriptSource.textBytes;
            }
            else
            {
                textBytes = LegacyAniSourceValues.EncodeScriptText(current.squirrel);
            }

            bw.Write(textBytes.Length);
            if (textBytes.Length > 0)
                bw.Write(textBytes);
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

    public hod2v1_Part interpolatePart(int frame, int part, float time)
    {
        hod2v1_Part iPart = new hod2v1_Part();
        if (frame < frames.Count - 1)
        {
            iPart.position = Vector3.Lerp(frames[frame].parts[part].position, frames[frame + 1].parts[part].position, time);
            iPart.rotation = Quaternion.Lerp(frames[frame].parts[part].rotation, frames[frame + 1].parts[part].rotation, time);
            iPart.scale = Vector3.Lerp(frames[frame].parts[part].scale, frames[frame + 1].parts[part].scale, time);
        }
        else
        {
            iPart.position = frames[frames.Count - 1].parts[part].position;
            iPart.rotation = frames[frames.Count - 1].parts[part].rotation;
            iPart.scale = frames[frames.Count - 1].parts[part].scale;
        }
        return iPart;
    }
}

