using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

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
        if (!fi.Exists || fi.Length < 4)
            return false;

        ctFileType fileType = default(ctFileType);
        bool registered = false;
        for (int i = 0; i < filetypes.Count; i++)
        {
            if (string.Equals(filetypes[i].fileExt, fi.Extension, StringComparison.OrdinalIgnoreCase))
            {
                fileType = filetypes[i];
                registered = true;
                break;
            }
        }
        if (!registered)
            return false;

        byte[] bytes = new byte[4];
        using (FileStream fs = File.OpenRead(name))
        {
            if (fs.Read(bytes, 0, bytes.Length) != bytes.Length)
                return false;
        }

        uint rawSignature = BitConverter.ToUInt32(bytes, 0);
        // Plaintext assets must never set the cipher to zero. This matters in
        // mixed folders where one extracted .x file can coexist with encrypted
        // models.
        if (rawSignature == fileType.signature)
            return false;

        uint candidate = rawSignature ^ fileType.signature;
        byte[] decodedHeader = (byte[])bytes.Clone();
        byte[] keyBytes = BitConverter.GetBytes(candidate);
        for (int i = 0; i < decodedHeader.Length; i++)
            decodedHeader[i] ^= keyBytes[i];

        if (BitConverter.ToUInt32(decodedHeader, 0) != fileType.signature)
            return false;

        // Header XOR is mathematically self-validating, so validate the
        // decoded payload as well. For X files, require a supported text X
        // header; for PNG, require the fixed PNG signature bytes.
        if (fileType.fileExt.Equals(".x", StringComparison.OrdinalIgnoreCase))
        {
            byte[] decoded = Transcode(File.ReadAllBytes(name), candidate);
            string header = Encoding.UTF8.GetString(decoded, 0, Math.Min(decoded.Length, 64));
            if (!Regex.IsMatch(header, @"\A\uFEFF?\s*xof\s+\d{4}(?:txt|bin)\s+\d{4}", RegexOptions.IgnoreCase))
                return false;
        }
        else if (fileType.fileExt.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            byte[] decoded = Transcode(File.ReadAllBytes(name), candidate);
            if (decoded.Length < 8 || decoded[0] != 0x89 || decoded[1] != 0x50 ||
                decoded[2] != 0x4E || decoded[3] != 0x47 || decoded[4] != 0x0D ||
                decoded[5] != 0x0A || decoded[6] != 0x1A || decoded[7] != 0x0A)
                return false;
        }

        cypher = candidate;
        return true;
    }

    public byte[] Transcode(string name)
    {
        byte[] bytes = File.ReadAllBytes(name);
        if (IsPlaintextAsset(name, bytes))
            return bytes;
        return Transcode(bytes, cypher);
    }

    static bool IsPlaintextAsset(string name, byte[] bytes)
    {
        if (bytes == null || bytes.Length < 4)
            return false;

        string extension = Path.GetExtension(name);
        uint signature = BitConverter.ToUInt32(bytes, 0);
        if (extension.Equals(".x", StringComparison.OrdinalIgnoreCase))
            return signature == 543584120u;
        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            return signature == 1196314761u;
        return false;
    }

    public byte[] Transcode(byte[] bytes)
    {
        return Transcode(bytes, cypher);
    }

    public byte[] Transcode(byte[] bytes, uint key)
    {
        if (bytes == null)
            return null;

        byte[] cypherBytes = BitConverter.GetBytes(key);
        int fullBlocks = bytes.Length / 4;

        // 原作の暗号化仕様に合わせ、4バイト完全ブロックのみを処理する。
        // 端数バイト（bytes.Length % 4 != 0 の残り）は暗号化されていないため処理しない。
        for (int i = 0; i < fullBlocks * 4; i += 4)
        {
            for (int b = 0; b < cypherBytes.Length; b++)
                bytes[i + b] ^= cypherBytes[b];
        }

        return bytes;
    }

}
