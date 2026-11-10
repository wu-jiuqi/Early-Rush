using UnityEditor;
using UnityEngine;

// Runtime slicing uses the atlas's actual dimensions, never a guessed 1024/2048 size.
public sealed class RushArtImport:AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Resources/RushArt/")||!assetPath.EndsWith(".png"))return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Default;
        importer.npotScale=TextureImporterNPOTScale.None;
        importer.maxTextureSize=4096;
        importer.mipmapEnabled=false;
        importer.alphaSource=TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency=true;
        importer.isReadable=false;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.filterMode=FilterMode.Bilinear;
        importer.wrapMode=TextureWrapMode.Clamp;
        importer.sRGBTexture=true;
    }
    [MenuItem("Early Rush/Validate v7 Art")]
    public static void Validate()
    {
        foreach(string filename in new[]{"props","people","icons","extras","floors"})
        {
            var tex=Resources.Load<Texture2D>("RushArt/"+filename);
            if(!tex||tex.width<700||tex.height<700)throw new System.Exception("Missing or undersized v7 art: "+filename);
        }
        Debug.Log("EARLY_RUSH_ART: five generated atlases found. Visual scene verification still required.");
    }
}
