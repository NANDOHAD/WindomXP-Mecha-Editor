using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;

public struct ctFileType
{
    public string fileExt;
    public uint signature;  
}
public class CypherTranscoder
{
    public uint cypher = 0x0B7E7759;
    public List<ctFileType> filetypes = new List<ctFileType>();

    public CypherTranscoder()
    {
        registerFileType(".png", 1196314761);
        registerFileType(".x", 543584120);
    }
        
    public void registerFileType(string fileExt, uint signature)
    {
        ctFileType fileType = new ctFileType();
        fileType.fileExt = fileExt;
        fileType.signature = signature;
        filetypes.Add(fileType);
    }

    public bool findCypher(string name)
    {
        FileInfo fi = new FileInfo(name);
        byte[] bytes = File.ReadAllBytes(name);
        //check if encrypted
        uint signature = BitConverter.ToUInt32(bytes, 0);
            
        for (int i = 0; i < filetypes.Count; i++)
        {
            if (filetypes[i].fileExt == fi.Extension)
            {
                uint cypherF = filetypes[i].signature ^ signature;
                signature ^= cypherF;
                if (signature == filetypes[i].signature)
                {
                    cypher = cypherF;
                    return true;
                }
                break;
            }
        }
        return false;
    }

    public byte[] Transcode(string name)
    {
        //Debug.log($"Starting transcoding for file: {name}");
        
        byte[] bytes = File.ReadAllBytes(name);
        //Debug.log($"Read {bytes.Length} bytes from file: {name}");

        for (int i = 0; i < bytes.Length; i += 4)
        {
            byte[] cypherBytes = BitConverter.GetBytes(cypher);
            for (int b = 0; b < cypherBytes.Length; b++)
            {
                if (i + 3 < bytes.Length)
                    bytes[i + b] ^= cypherBytes[b];
            }
        }

        //Debug.log($"Completed transcoding for file: {name}");
        return bytes;
    }

    public byte[] Transcode(byte[] bytes)
    {
        //Debug.log($"Starting transcoding for byte array of length: {bytes.Length}");
        
        byte[] cypherBytes = BitConverter.GetBytes(cypher);
        int fullBlocks = bytes.Length / 4; // 4バイトのブロック数

        // 4バイトのブロックを処理
        for (int i = 0; i < fullBlocks * 4; i += 4)
        {
            for (int b = 0; b < cypherBytes.Length; b++)
            {
                bytes[i + b] ^= cypherBytes[b];
            }
        }

        // 端数のバイトを処理
        int remainingBytes = bytes.Length % 4;
        if (remainingBytes > 0)
        {
            int start = fullBlocks * 4;
            for (int b = 0; b < remainingBytes; b++)
            {
                bytes[start + b] ^= cypherBytes[b];
            }
        }

        //Debug.log($"Completed transcoding for byte array of length: {bytes.Length}");
        return bytes;
    }
}

