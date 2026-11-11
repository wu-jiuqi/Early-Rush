using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EarlyRush.V9;

public static class BuildEarlyRushV14
{
    const string Destination="Builds/EarlyRush-V14";
    const int Seed=91026;
    static readonly CultureInfo Culture=CultureInfo.InvariantCulture;

    [MenuItem("Early Rush/V14/Validate Quick Mechanisms")]
    public static void ValidateQuick(){ValidateInternal(false);}
    [MenuItem("Early Rush/V14/Validate Complete")]
    public static void Validate(){ValidateInternal(true);}
    [MenuItem("Early Rush/V14/Validate and Build Windows")]
    public static void Build(){Validate();BuildPlayer();}
    [MenuItem("Early Rush/V14/Quick Checks and Preview Build")]
    public static void BuildPreview(){ValidateQuick();BuildPlayer();}

    static void ValidateInternal(bool complete)
    {
        string output=Destination+"/QA";Directory.CreateDirectory(output);
        var watch=System.Diagnostics.Stopwatch.StartNew();
        var report=new StringBuilder("Early Rush V14 validation\n");
        report.AppendLine("UTC: "+DateTime.UtcNow.ToString("o",Culture));
        report.AppendLine("Scope: "+(complete?"full model/mechanisms and six default-deadline input routes":"mechanisms only; no route completion claim"));
        report.AppendLine("Default stage deadline: "+new ConfigV9().stageLimit.ToString(Culture)+" seconds. Seed: "+Seed);
        bool success=true;
        Action<string,Func<int>> check=(name,test)=>
        {
            try{string line=name+": "+test()+" checks passed";report.AppendLine(line);Debug.Log("V14_VALIDATION "+line);}
            catch(Exception error){success=false;report.AppendLine(name+": FAILED "+error);Debug.LogWarning("V14_VALIDATION "+name+" FAILED "+error.Message);}
        };
        check("Authored street coordinate and geometry",()=>RushStreetArtV14Tests.Run());
        check("Model/map",()=>RushTestsV9.RunAll(complete?100:1));
        check("Action mechanisms",()=>RushActionTestsV11.RunAll());
        check("Dash cadence and input buffer",()=>RushDashFeelTestsV13.RunAll());
        check("Independent pedestrian mechanisms",()=>RushPedestrianTestsV12.RunAll());
        check("Route crowd coverage",()=>RushCrowdCoverageTestsV13.RunAll());
        check("Dash impact mechanisms",()=>RushDashImpactTestsV12.RunAll());
        check("Coffee mechanisms",()=>RushCoffeeAITestsV12.RunAll());
        check("Minimap projection",()=>RushMinimapTestsV13.RunAll());
        check("Camera numeric regression",()=>RushCameraV10.Validate());
        for(int stage=0;stage<3;stage++)
        {
            int chapter=stage;
            check("Map seed="+Seed+" stage="+chapter,()=>
            {
                var run=new RunV9(chapter,Seed);
                if(!run.map.Validate(out string reason))throw new Exception(reason);
                string density="stage="+chapter+" "+run.CrowdDensitySummary();
                report.AppendLine("Density "+density);Debug.Log("V14_DENSITY "+density);
                File.WriteAllText(output+"/layout-stage-"+chapter+".json",run.map.ToPreviewJson());
                return 1;
            });
            if(complete)success=AppendRoutes(chapter,report,output)&&success;
        }
        report.AppendLine("Assertions include repeated actor/map checks, not independent test-case counts. Replays use actual input and unchanged task rules; they are not human timing or FPS evidence.");
        report.AppendLine("Wall-clock seconds: "+watch.Elapsed.TotalSeconds.ToString("F2",Culture));
        report.AppendLine("Passed: "+success);
        string file=output+(complete?"/validation.txt":"/validation-quick.txt");File.WriteAllText(file,report.ToString());
        if(!success)throw new Exception("V14 validation failed. Inspect "+file);
    }

    static bool AppendRoutes(int stage,StringBuilder report,string output)
    {
        bool success=true;
        foreach(bool allTasks in new[]{false,true})
        {
            string kind=allTasks?"all-tasks":"mainline";
            try
            {
                var route=allTasks?RushTestsV9.ReplayAllTasks(stage,Seed):RushTestsV9.ReplayMainline(stage,Seed);
                bool passed=route.success&&route.elapsed<=new ConfigV9().stageLimit+.00001&&(!allTasks||route.completed==3);
                string line=kind+" stage="+stage+" seed="+Seed+" success="+passed+" seconds="+route.elapsed.ToString("F2",Culture)+
                    " contacts="+route.contacts+(allTasks?" completed="+route.completed+" states="+string.Join("|",route.taskStates??new string[0]):"")+
                    " stopped="+route.stoppedAt+" reason="+route.reason;
                success&=passed;report.AppendLine(line);Debug.Log("V14_VALIDATION "+line);
                var trace=new StringBuilder("time,type,id,x,y\n");
                foreach(var entry in route.trace)trace.AppendLine(string.Join(",",entry.time.ToString(Culture),entry.type,entry.id.ToString(Culture),entry.position.x.ToString(Culture),entry.position.y.ToString(Culture)));
                File.WriteAllText(output+"/"+kind+"-stage-"+stage+".csv",trace.ToString());
            }
            catch(Exception error){success=false;report.AppendLine(kind+" stage="+stage+" FAILED "+error);Debug.LogWarning("V14_VALIDATION "+kind+" stage="+stage+" FAILED "+error.Message);}
        }
        return success;
    }

    public static void ValidateStreet(){ValidateStage(0);}
    public static void ValidateMetro(){ValidateStage(1);}
    public static void ValidateOffice(){ValidateStage(2);}
    static void ValidateStage(int stage)
    {
        string output=Destination+"/QA";Directory.CreateDirectory(output);
        var report=new StringBuilder("Early Rush V14 focused stage="+stage+" input replay\n");
        report.AppendLine("UTC: "+DateTime.UtcNow.ToString("o",Culture));
        bool passed=AppendRoutes(stage,report,output);report.AppendLine("Passed: "+passed);
        File.WriteAllText(output+"/validation-stage-"+stage+".txt",report.ToString());
        if(!passed)throw new Exception("V14 stage "+stage+" replay failed");
    }

    public static void BuildPlayer()
    {
        // Reuse the existing shader keep materials rather than adding copies for every build.
        foreach(string name in new[]{"Sprites/Default","Unlit/Texture","GUI/Text Shader"})
        {
            string path="Assets/Resources/V12Keep_"+name.Replace('/','_')+".mat";
            if(!AssetDatabase.LoadAssetAtPath<Material>(path))
            {var shader=Shader.Find(name);if(!shader)throw new Exception("Missing shader "+name);AssetDatabase.CreateAsset(new Material(shader),path);}
        }
        PlayerSettings.companyName="Morning Club";PlayerSettings.productName="Early Rush";PlayerSettings.bundleVersion="0.14.0";
        PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;
        PlayerSettings.runInBackground=false;PlayerSettings.colorSpace=ColorSpace.Gamma;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{UnityEngine.Rendering.GraphicsDeviceType.Direct3D11});
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Early Rush - V14 Art Integration").AddComponent<RushPlayV9>();
        Directory.CreateDirectory("Assets/Scenes");EditorSceneManager.SaveScene(scene,"Assets/Scenes/EarlyRushV14.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/EarlyRushV14.unity",true)};
        AssetDatabase.SaveAssets();
        string executable=Path.GetFullPath(Destination+"/EarlyRush.exe");Directory.CreateDirectory(Path.GetDirectoryName(executable));
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/EarlyRushV14.unity"},locationPathName=executable,
            target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        Debug.Log("V14_BUILD_RESULT "+result.summary.result);
        if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("V13 build failed");
    }
}
