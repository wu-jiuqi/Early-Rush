using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EarlyRush.ArtV4;
public static class ArtV4ReviewBuild {
 const string Scene="Assets/ArtV4Review/ArtV4Review.unity";
 [MenuItem("Early Rush/Art V4/Create Review Scene")]
 public static void Create(){Directory.CreateDirectory("Assets/ArtV4Review");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("Art V4 Review - not gameplay").AddComponent<RushArtV4Review>();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Scene);AssetDatabase.SaveAssets();}
 [MenuItem("Early Rush/Art V4/Build Windows Review")]
 public static void Build(){Create();Directory.CreateDirectory("Builds/EarlyRush-ArtV4-Review");var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName="Builds/EarlyRush-ArtV4-Review/EarlyRush-ArtV4-Review.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new System.Exception("Art review build failed: "+result.summary.result);}
}
public sealed class ArtV4TextureImport:AssetPostprocessor {
 void OnPreprocessTexture(){if(!assetPath.StartsWith("Assets/Resources/ArtV4/"))return;var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Default;t.alphaSource=TextureImporterAlphaSource.FromInput;t.alphaIsTransparency=true;t.mipmapEnabled=false;t.isReadable=false;t.npotScale=TextureImporterNPOTScale.None;t.maxTextureSize=4096;t.textureCompression=TextureImporterCompression.Uncompressed;t.filterMode=FilterMode.Bilinear;t.wrapMode=assetPath.Contains("/tiles/")||assetPath.Contains("/materials/")?TextureWrapMode.Repeat:TextureWrapMode.Clamp;}
}
