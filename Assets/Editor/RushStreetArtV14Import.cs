using UnityEditor;
using UnityEngine;
public sealed class RushStreetArtV14Import:AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Resources/StreetV14/"))return;
        var t=(TextureImporter)assetImporter;
        t.textureType=TextureImporterType.Default;t.sRGBTexture=true;t.alphaSource=TextureImporterAlphaSource.FromInput;
        t.alphaIsTransparency=!assetPath.Contains("/tiles/");t.mipmapEnabled=false;t.isReadable=false;
        t.npotScale=TextureImporterNPOTScale.None;t.maxTextureSize=4096;t.filterMode=FilterMode.Bilinear;t.wrapMode=TextureWrapMode.Clamp;
        t.textureCompression=TextureImporterCompression.Uncompressed;t.crunchedCompression=false;
    }
}
