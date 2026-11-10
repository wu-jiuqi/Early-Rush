using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EarlyRush.V9;

/// <summary>Reproducible source-only build; optional bitmap art is never a build prerequisite.</summary>
public static class BuildEarlyRushV10
{
    const string Destination="Builds/EarlyRush-V10";
    [MenuItem("Early Rush/V10/Validate Design V3")]
    public static void Validate()
    {
        Directory.CreateDirectory(Destination+"/QA");
        int assertions=RushTestsV9.RunAll(100);
        int cameraChecks=RushCameraV10.Validate();
        var report=new StringBuilder("Design v3 / Unity model validation\n");
        report.AppendLine("Model assertions: "+assertions);
        report.AppendLine("Camera regression checks: "+cameraChecks);
        report.AppendLine("Map validation: 100 seeds x 3 stages. Default stage deadline: "+new ConfigV9().stageLimit+" seconds.");
        bool success=true;
        for(int stage=0;stage<3;stage++)
        {
            var route=RushTestsV9.ReplayMainline(stage,91026);
            string line="mainline stage="+stage+" success="+route.success+" seconds="+route.elapsed.ToString("F2")+" reason="+route.reason;
            Debug.Log("V10_VALIDATION "+line);report.AppendLine(line);success&=route.success;
            File.WriteAllText(Destination+"/QA/layout-stage-"+stage+".json",new RunV9(stage,91026).map.ToPreviewJson());
            var tasks=RushTestsV9.ReplayAllTasks(stage,91026);
            line="all-tasks stage="+stage+" success="+tasks.success+" completed="+tasks.completed+" seconds="+tasks.elapsed.ToString("F2")+" reason="+tasks.reason;
            Debug.Log("V10_VALIDATION "+line);report.AppendLine(line);success&=tasks.success;
            var trace=new StringBuilder("time,type,id,x,y\n");
            foreach(var t in tasks.trace)trace.AppendLine(string.Join(",",t.time.ToString(System.Globalization.CultureInfo.InvariantCulture),t.type,t.id,t.position.x.ToString(System.Globalization.CultureInfo.InvariantCulture),t.position.y.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            File.WriteAllText(Destination+"/QA/tasks-stage-"+stage+".csv",trace.ToString());
        }
        report.AppendLine("Machine input validates achievable routes at the default deadline. It is not human playtesting or a measured 5-7 minute experience.");
        File.WriteAllText(Destination+"/QA/validation.txt",report.ToString());
        if(!success)throw new Exception("V10 route/task validation failed. Inspect QA/validation.txt");
    }
    [MenuItem("Early Rush/V10/Build Windows")]
    public static void Build()
    {
        Validate();BuildPlayer();
    }
    public static void BuildPlayer()
    {
        foreach(string name in new[]{"Sprites/Default","Unlit/Texture","GUI/Text Shader"})
        {
            var shader=Shader.Find(name);if(!shader)throw new Exception("Missing shader "+name);
            string path="Assets/Resources/V10Keep_"+name.Replace('/','_')+".mat";
            if(!AssetDatabase.LoadAssetAtPath<Material>(path))AssetDatabase.CreateAsset(new Material(shader),path);
        }
        PlayerSettings.companyName="Morning Club";PlayerSettings.productName="Early Rush";PlayerSettings.bundleVersion="0.10.0";
        PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;
        PlayerSettings.runInBackground=false;PlayerSettings.colorSpace=ColorSpace.Gamma;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{UnityEngine.Rendering.GraphicsDeviceType.Direct3D11});
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Early Rush - Design V3").AddComponent<RushPlayV9>();
        Directory.CreateDirectory("Assets/Scenes");EditorSceneManager.SaveScene(scene,"Assets/Scenes/EarlyRushV10.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/EarlyRushV10.unity",true)};
        AssetDatabase.SaveAssets();
        string output=Path.GetFullPath(Destination+"/EarlyRush.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/EarlyRushV10.unity"},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        Debug.Log("V10_BUILD_RESULT "+report.summary.result);
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("V10 build failed");
    }
}
