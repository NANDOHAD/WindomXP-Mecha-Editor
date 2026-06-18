using UnityEngine;
using System.Collections.Generic;
using System.IO;

public static class Helper
{
        public const Assimp.PostProcessSteps PostProcessStepflags = Assimp.PostProcessSteps.OptimizeMeshes |
        Assimp.PostProcessSteps.OptimizeGraph |
        Assimp.PostProcessSteps.RemoveRedundantMaterials |
        Assimp.PostProcessSteps.SortByPrimitiveType |
        Assimp.PostProcessSteps.SplitLargeMeshes |
        Assimp.PostProcessSteps.Triangulate |
        Assimp.PostProcessSteps.CalculateTangentSpace |
        Assimp.PostProcessSteps.GenerateUVCoords |
        Assimp.PostProcessSteps.GenerateSmoothNormals |
        Assimp.PostProcessSteps.RemoveComponent |
        Assimp.PostProcessSteps.JoinIdenticalVertices |
        Assimp.PostProcessSteps.JoinIdenticalVertices |
        Assimp.PostProcessSteps.MakeLeftHanded;

        public static Dictionary<string, Texture2D> TextureCache = new Dictionary<string, Texture2D>();

        public static Texture2D LoadTexture(string path)
        {
            Texture2D result;

            if (!TextureCache.TryGetValue(path, out result))
            {
                result = new Texture2D(0, 0);
                result.LoadImage(File.ReadAllBytes(path));
                result.name = Path.GetFileNameWithoutExtension(path);
                TextureCache[path] = result;
            }

            return result;
        }

        public static Texture2D LoadTextureEncrypted(string path, ref CypherTranscoder transcoder)
        {
            Texture2D result;
            bool logSelectPng = Path.GetFileName(path).ToLowerInvariant() == "select.png";

            if (!TextureCache.TryGetValue(path, out result))
            {
                result = new Texture2D(0, 0);
                byte[] imageData = transcoder.Transcode(path);
                
                if (imageData == null || imageData.Length == 0)
                {
                    Debug.LogError($"[Helper] 復号後データが空です。path='{path}'");
                    return null; // エラー処理
                }

                if (logSelectPng)
                    Debug.Log($"[Helper] 復号後画像データ: path='{path}', length={imageData.Length}, header={BytesToHex(imageData, 0, 16)}, tail={BytesToHex(imageData, Mathf.Max(0, imageData.Length - 16), 16)}");

                if (!result.LoadImage(imageData))
                {
                    Debug.LogError($"[Helper] Texture2D.LoadImage に失敗しました。path='{path}', length={imageData.Length}, header={BytesToHex(imageData, 0, 16)}");
                    return null; // エラー処理
                }

                result.name = Path.GetFileNameWithoutExtension(path);
                TextureCache[path] = result;
                if (logSelectPng)
                    Debug.Log($"[Helper] Texture2D.LoadImage 成功: path='{path}', width={result.width}, height={result.height}, format={result.format}");
            }
            else
            {
                if (logSelectPng)
                    Debug.Log($"[Helper] TextureCache 使用: path='{path}', width={result.width}, height={result.height}, format={result.format}");
            }

            return result;
        }

        private static string BytesToHex(byte[] bytes, int start, int maxLength)
        {
            if (bytes == null || bytes.Length == 0 || start >= bytes.Length || maxLength <= 0)
                return "";

            int end = Mathf.Min(bytes.Length, start + maxLength);
            System.Text.StringBuilder sb = new System.Text.StringBuilder((end - start) * 3);
            for (int i = start; i < end; i++)
            {
                if (i > start)
                    sb.Append(' ');
                sb.Append(bytes[i].ToString("X2"));
            }
            return sb.ToString();
        }
    
}
