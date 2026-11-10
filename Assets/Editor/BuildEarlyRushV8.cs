using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System;
using System.IO;
public static class BuildEarlyRushV8
{
    [MenuItem("Early Rush/V8/Validate and Build Windows")]
    public static void Build()
    {
        RushArtV8Import.Validate();
        int maps=EarlyRush.V8.RushMapV8Tests.RunAll(100);
        int logic=EarlyRush.V8.RushV8Tests.RunAll();
        foreach(string name in new[]{"Sprites/Default","Unlit/Texture","GUI/Text Shader"})
        {
            var shader=Shader.Find(name);if(!shader)throw new Exception("Missing shader "+name);
            string file="Assets/Resources/V8Keep_"+name.Replace('/','_')+".mat";
            if(!AssetDatabase.LoadAssetAtPath<Material>(file))AssetDatabase.CreateAsset(new Material(shader),file);
        }
        PlayerSettings.companyName="Morning Club";PlayerSettings.productName="Early Rush";PlayerSettings.bundleVersion="0.8.0";
        PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=false;PlayerSettings.colorSpace=ColorSpace.Gamma;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{UnityEngine.Rendering.GraphicsDeviceType.Direct3D11});
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("Early Rush V8").AddComponent<EarlyRush.V8.RushPlayV8>();
        Directory.CreateDirectory("Assets/Scenes");EditorSceneManager.SaveScene(scene,"Assets/Scenes/EarlyRushV8.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/EarlyRushV8.unity",true)};AssetDatabase.SaveAssets();
        string output=Path.GetFullPath("Builds/EarlyRush-V8-Windows/EarlyRush.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/EarlyRushV8.unity"},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        Debug.Log("V8_BUILD_RESULT "+result.summary.result+" mapChecks="+maps+" logicChecks="+logic);
        if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Build failed");
    }
}
