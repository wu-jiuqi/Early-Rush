using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System;
using System.IO;
using EarlyRush.V9;
public static class BuildEarlyRushV9
{
    [MenuItem("Early Rush/V9/Validate Model and Routes")]
    public static void Validate()
    {
        RushArtV8Import.Validate();
        int assertions=RushTestsV9.RunAll(10);
        string output=Path.GetFullPath("Builds/EarlyRush-V9-Review/QA");Directory.CreateDirectory(output);
        var summary=new System.Text.StringBuilder("V9 review validation\nModel assertions: "+assertions+"\n");bool all=true;
        for(int stage=0;stage<3;stage++)
        {
            File.WriteAllText(Path.Combine(output,"layout-stage-"+stage+".json"),new RunV9(stage,91026).map.ToPreviewJson());
            var result=RushTestsV9.ReplayMainline(stage,91026);
            string line="stage="+stage+" mainline="+result.success+" elapsed="+result.elapsed+" contacts="+result.contacts+" stopped="+result.stoppedAt+" reason="+result.reason;
            summary.AppendLine(line);Debug.Log("V9_INPUT_REPLAY "+line);all&=result.success;
            var trace=new System.Text.StringBuilder("time,type,id,x,y\n");foreach(var t in result.trace)trace.AppendLine(string.Join(",",t.time.ToString(System.Globalization.CultureInfo.InvariantCulture),t.type,t.id,t.position.x.ToString(System.Globalization.CultureInfo.InvariantCulture),t.position.y.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            File.WriteAllText(Path.Combine(output,"mainline-stage-"+stage+".csv"),trace.ToString());
        }
        summary.AppendLine("Automated input mainline only. Not human playability approval, not nine-task end-to-end certification, not measured 7-10 minute experience.");
        File.WriteAllText(Path.Combine(output,"model-route-validation.txt"),summary.ToString());
        if(!all)throw new Exception("V9 mainline input replay failed; inspect QA trace before build.");
    }
    [MenuItem("Early Rush/V9/Replay All Tasks (Diagnostic)")]
    public static void ReplayAllTasks()
    {
        string output=Path.GetFullPath("Builds/EarlyRush-V9-Review/QA/all-tasks");Directory.CreateDirectory(output);
        var summary=new System.Text.StringBuilder("Pure input diagnostic replay. Relaxed stage deadline; not timing/balance acceptance.\n");bool all=true;
        for(int stage=0;stage<3;stage++)
        {
            var result=RushTestsV9.ReplayAllTasks(stage,91026);
            string line="stage="+stage+" success="+result.success+" completed="+result.completed+" elapsed="+result.elapsed+" stopped="+result.stoppedAt+" reason="+result.reason+" states="+string.Join("|",result.taskStates)+" scope="+result.validationScope;
            summary.AppendLine(line);Debug.Log("V9_ALL_TASKS_REPLAY "+line);all&=result.success;
            var trace=new System.Text.StringBuilder("time,type,id,x,y\n");foreach(var t in result.trace)trace.AppendLine(string.Join(",",t.time.ToString(System.Globalization.CultureInfo.InvariantCulture),t.type,t.id,t.position.x.ToString(System.Globalization.CultureInfo.InvariantCulture),t.position.y.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            File.WriteAllText(Path.Combine(output,"stage-"+stage+".csv"),trace.ToString());
        }
        File.WriteAllText(Path.Combine(output,"results.txt"),summary.ToString());
        if(!all)throw new Exception("V9 all-task diagnostic failed: inspect per-stage results and actual input trace.");
    }
    [MenuItem("Early Rush/V9/Validate and Build Windows Review")]
    public static void Build(){BuildTo("Builds/EarlyRush-V9-Review");}
    [MenuItem("Early Rush/V9/Build Separate Panorama Review")]
    public static void BuildPanorama(){BuildTo("Builds/EarlyRush-V9-PanoramaReview");}
    static void BuildTo(string destination)
    {
        Validate();
        foreach(string name in new[]{"Sprites/Default","Unlit/Texture","GUI/Text Shader"})
        {
            var shader=Shader.Find(name);if(!shader)throw new Exception("Missing shader "+name);
            string path="Assets/Resources/V9Keep_"+name.Replace('/','_')+".mat";
            if(!AssetDatabase.LoadAssetAtPath<Material>(path))AssetDatabase.CreateAsset(new Material(shader),path);
        }
        PlayerSettings.companyName="Morning Club";PlayerSettings.productName="Early Rush V9 Review";PlayerSettings.bundleVersion="0.9.0-review";
        PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=false;PlayerSettings.colorSpace=ColorSpace.Gamma;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{UnityEngine.Rendering.GraphicsDeviceType.Direct3D11});
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("Early Rush V9 Review").AddComponent<RushPlayV9>();
        Directory.CreateDirectory("Assets/Scenes");EditorSceneManager.SaveScene(scene,"Assets/Scenes/EarlyRushV9.unity");AssetDatabase.SaveAssets();
        string output=Path.GetFullPath(Path.Combine(destination,"EarlyRush.exe"));Directory.CreateDirectory(Path.GetDirectoryName(output));
        // Do not change the legacy V8 build-scene list. This invocation owns only its explicit V9 scene.
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/EarlyRushV9.unity"},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        Debug.Log("V9_BUILD_RESULT "+result.summary.result);
        if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("V9 build failed");
    }
}
