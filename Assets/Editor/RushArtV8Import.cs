using UnityEditor;
using UnityEngine;
using System;
using System.IO;

/// <summary>Only V8 bitmap imports; never alters legacy V7 settings or source PNG bytes.</summary>
public sealed class RushArtV8Import : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/V8Art/", StringComparison.Ordinal)) return;
        var importer = (TextureImporter)assetImporter;
        bool floor = assetPath.Contains("/floors-v3/") || assetPath.Contains("/legacy-floors/");
        importer.textureType = TextureImporterType.Default; // Runtime Sprite.Create owns the ground pivot.
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = !floor;
        importer.mipmapEnabled = floor;
        importer.isReadable = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = floor ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.anisoLevel = floor ? 2 : 1;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.crunchedCompression = false;
    }

    [MenuItem("Early Rush/V8/Validate Art Assets")]
    public static void Validate()
    {
        const string root = "Assets/Resources/V8Art";
        if (!Directory.Exists(root)) throw new Exception("V8 art directory missing");
        int count = 0;
        foreach (string file in Directory.GetFiles(root, "*.png", SearchOption.AllDirectories))
        {
            string path = file.Replace('\\', '/');
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture || texture.width <= 0 || texture.height <= 0) throw new Exception("Invalid V8 art: " + path);
            count++;
        }
        string[] required = { "floors-v3/asphalt", "floors-v3/ceramic", "floors-v3/carpet", "stalls/coffee-stall",
            "people/boy-up", "people/boy-down", "people/boy-left", "people/boy-right", "office/checkin",
            "tasks/backpack", "tasks/chair" };
        foreach (string key in required)
            if (!AssetDatabase.LoadAssetAtPath<Texture2D>(root + "/" + key + ".png")) throw new Exception("Required V8 art missing: " + key);
        Debug.Log("EARLY_RUSH_V8_ART_VALIDATED count=" + count);
    }
}
