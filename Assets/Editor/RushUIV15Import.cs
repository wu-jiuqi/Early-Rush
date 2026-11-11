using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

/// <summary>Imports only the 110 manifest-listed UIV15 PNGs. Original source bytes remain unchanged.</summary>
public static class RushUIV15Import
{
    public const string Root = "Assets/Resources/UIV15";
    public const int ExpectedAssetCount = 110;
    [Serializable] public sealed class Manifest { public int schemaVersion; public Entry[] assets; }
    [Serializable] public sealed class Entry
    {
        public string id, path, sha256, category, kind;
        public int width, height;
        public float[] pivot, border;
    }
    [Serializable] public sealed class AssetCheck { public string id, path; public bool passed; public string[] errors; }
    [Serializable] public sealed class Validation
    {
        public string unity, utc, scope = "UI sprite import and exact source-hash validation; not gameplay or visual acceptance.";
        public bool passed;
        public int expected = ExpectedAssetCount, actual, checkedCount;
        public List<AssetCheck> assets = new List<AssetCheck>();
    }

    [MenuItem("Early Rush/V15/Import UI Art")]
    public static void Apply()
    {
        Manifest manifest = ReadAndValidateInput();
        foreach (Entry entry in manifest.assets)
        {
            string path = AssetPath(entry);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidDataException("PNG has not imported: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.maxTextureSize = Mathf.Max(entry.width, entry.height) > 2048 ? 4096 : 2048;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(entry.pivot[0], entry.pivot[1]);
            settings.spriteBorder = new Vector4(entry.border[0], entry.border[1], entry.border[2], entry.border[3]);
            importer.SetTextureSettings(settings);
            importer.ClearPlatformTextureSettings("Standalone");
            importer.SaveAndReimport();
        }
        int count = ValidateAssets();
        Debug.Log("V15_UI_IMPORT " + count + " sprites imported and verified.");
    }

    [MenuItem("Early Rush/V15/Validate UI Art")]
    public static void ValidateMenu() { ValidateAssets(); }

    public static int ValidateAssets()
    {
        Manifest manifest = ReadAndValidateInput();
        var report = new Validation { utc = DateTime.UtcNow.ToString("o"), unity = Application.unityVersion,
            actual = Directory.GetFiles(DiskRoot, "*.png", SearchOption.AllDirectories).Length, passed = true };
        foreach (Entry entry in manifest.assets)
        {
            string path = AssetPath(entry);
            var errors = new List<string>();
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!importer || !sprite || !texture) errors.Add("Missing importer, Sprite or Texture2D");
            else
            {
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single) errors.Add("Sprite/Single");
                if (settings.spriteMeshType != SpriteMeshType.FullRect || settings.spriteAlignment != (int)SpriteAlignment.Custom) errors.Add("FullRect/custom pivot");
                if (Mathf.Abs(sprite.pixelsPerUnit - 100) > .001f) errors.Add("PPU");
                if (texture.width != entry.width || texture.height != entry.height || sprite.rect.width != entry.width || sprite.rect.height != entry.height) errors.Add("Dimensions");
                if (Vector2.Distance(sprite.pivot, new Vector2(entry.pivot[0] * entry.width, entry.pivot[1] * entry.height)) > .01f) errors.Add("Pivot");
                if (Vector4.Distance(sprite.border, new Vector4(entry.border[0], entry.border[1], entry.border[2], entry.border[3])) > .001f) errors.Add("Border");
                if (importer.mipmapEnabled || texture.mipmapCount != 1) errors.Add("Mipmaps");
                if (!importer.alphaIsTransparency || importer.alphaSource != TextureImporterAlphaSource.FromInput) errors.Add("Alpha");
                if (!importer.sRGBTexture || importer.isReadable) errors.Add("sRGB/readability");
                if (importer.filterMode != FilterMode.Bilinear || importer.wrapMode != TextureWrapMode.Clamp) errors.Add("Filter/wrap");
                if (importer.npotScale != TextureImporterNPOTScale.None || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.crunchedCompression) errors.Add("Rescale/compression");
                if (importer.GetPlatformTextureSettings("Standalone").overridden) errors.Add("Standalone override");
            }
            report.assets.Add(new AssetCheck { id = entry.id, path = path, passed = errors.Count == 0, errors = errors.ToArray() });
            report.passed &= errors.Count == 0;
            report.checkedCount++;
        }
        report.passed &= report.actual == ExpectedAssetCount;
        Directory.CreateDirectory("Builds/EarlyRush-V15/QA");
        File.WriteAllText("Builds/EarlyRush-V15/QA/ui-assets.json", JsonUtility.ToJson(report, true));
        if (!report.passed) throw new InvalidDataException("V15 UI import validation failed; see Builds/EarlyRush-V15/QA/ui-assets.json");
        Debug.Log("V15_UI_ASSETS_PASS " + report.checkedCount);
        return report.checkedCount;
    }

    static string DiskRoot => Path.Combine(Application.dataPath, "Resources/UIV15");
    static string AssetPath(Entry entry) => Root + "/" + entry.path.Substring("Sprites/".Length);

    static Manifest ReadAndValidateInput()
    {
        string manifestPath = Path.Combine(DiskRoot, "manifest.json");
        if (!File.Exists(manifestPath)) throw new FileNotFoundException("V15 UI manifest missing", manifestPath);
        Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
        if (manifest == null || manifest.schemaVersion != 1 || manifest.assets == null || manifest.assets.Length != ExpectedAssetCount)
            throw new InvalidDataException("Expected schemaVersion 1 and exactly 110 V15 UI assets.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string diskRoot = Path.GetFullPath(DiskRoot) + Path.DirectorySeparatorChar;
        foreach (Entry entry in manifest.assets)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id) || !ids.Add(entry.id)) throw new InvalidDataException("Duplicate/empty UI asset ID");
            if (string.IsNullOrEmpty(entry.path) || !entry.path.StartsWith("Sprites/", StringComparison.Ordinal) || !entry.path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || !paths.Add(entry.path))
                throw new InvalidDataException("Invalid/duplicate UI path: " + entry.id);
            string full = Path.GetFullPath(Path.Combine(DiskRoot, entry.path.Substring("Sprites/".Length)));
            if (!full.StartsWith(diskRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("UI path escapes root: " + entry.id);
            if (!File.Exists(full)) throw new FileNotFoundException(entry.id, full);
            if (entry.width <= 0 || entry.height <= 0 || entry.width > 4096 || entry.height > 4096) throw new InvalidDataException("UI dimensions: " + entry.id);
            if (entry.pivot == null || entry.pivot.Length != 2 || !Between(entry.pivot[0], 0, 1) || !Between(entry.pivot[1], 0, 1)) throw new InvalidDataException("UI pivot: " + entry.id);
            if (entry.border == null || entry.border.Length != 4) throw new InvalidDataException("UI border: " + entry.id);
            foreach (float value in entry.border) if (!Between(value, 0, 4096)) throw new InvalidDataException("UI border: " + entry.id);
            if (entry.border[0] + entry.border[2] >= entry.width || entry.border[1] + entry.border[3] >= entry.height) throw new InvalidDataException("UI border center: " + entry.id);
            using (var sha = SHA256.Create())
            {
                string actual = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(full))).Replace("-", "").ToLowerInvariant();
                if (!string.Equals(actual, entry.sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("UI source hash mismatch: " + entry.id);
            }
        }
        return manifest;
    }
    static bool Between(float value, float min, float max) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
}
