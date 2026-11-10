using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using EarlyRush.V10;
public static class BuildEarlyRushV10
{
    [MenuItem("Early Rush/V10/Validate Assembly Geometry")]
    public static void Validate()
    {
        string output=Path.GetFullPath("Builds/EarlyRush-V10-AssemblyReview/QA");Directory.CreateDirectory(output);
        for(int stage=0;stage<3;stage++)
        {
            var map=RushMapV10.Create(stage,91026);if(!map.Validate(out var reason))throw new Exception("V10 geometry "+stage+": "+reason);
            File.WriteAllText(Path.Combine(output,"chapter-"+stage+".json"),map.ToPreviewJson());
            Debug.Log("V10_GEOMETRY stage="+stage+" cells="+map.cells.Count+" length="+map.PathLength+" not timing verification");
        }
    }
    [MenuItem("Early Rush/V10/Build Independent Assembly Review")]
    public static void Build()
    {
        Validate();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("V10 Component Assembly").AddComponent<RushAssemblyV10>();
        Directory.CreateDirectory("Assets/Scenes");EditorSceneManager.SaveScene(scene,"Assets/Scenes/EarlyRushV10Assembly.unity");
        PlayerSettings.productName="Early Rush V10 Assembly Review";PlayerSettings.bundleVersion="0.10.0-assembly";
        PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.colorSpace=ColorSpace.Gamma;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{UnityEngine.Rendering.GraphicsDeviceType.Direct3D11});
        string output=Path.GetFullPath("Builds/EarlyRush-V10-AssemblyReview/EarlyRush.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/EarlyRushV10Assembly.unity"},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        Debug.Log("V10_ASSEMBLY_BUILD "+result.summary.result);if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("V10 assembly build failed");
    }
}
