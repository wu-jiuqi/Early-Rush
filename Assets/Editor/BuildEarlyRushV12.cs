using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EarlyRush.V9;

/// <summary>V12 checks use the shipped configuration. Mechanism fixtures and input routes are reported separately.</summary>
public static class BuildEarlyRushV12
{
    const string Destination = "Builds/EarlyRush-V12";
    const int RouteSeed = 91026;
    static readonly double[] V11Mainline = { 166.72, 172.35, 168.28 };
    static readonly double[] V11AllTasks = { 207.38, 231.97, 228.15 };
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [MenuItem("Early Rush/V12/Validate Quick Mechanisms")]
    public static void ValidateQuick() { ValidateInternal(false); }

    [MenuItem("Early Rush/V12/Validate Complete")]
    public static void Validate() { ValidateInternal(true); }

    static void ValidateInternal(bool complete)
    {
        string output = Destination + "/QA";
        Directory.CreateDirectory(output);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var report = new StringBuilder("Early Rush V12 validation\n");
        report.AppendLine("UTC: " + DateTime.UtcNow.ToString("o", Invariant));
        report.AppendLine("Scope: " + (complete ? "complete model, actions, independent pedestrian AI, camera and default-deadline input routes" : "quick mechanism checks and route-seed map validation; no route completion claim"));
        report.AppendLine("Default stage deadline: " + new ConfigV9().stageLimit.ToString(Invariant) + " seconds.");
        bool success = true;
        Action<string, Func<int>> check = (name, run) =>
        {
            try
            {
                string line = name + ": " + run() + " checks passed";
                report.AppendLine(line); Debug.Log("V12_VALIDATION " + line);
            }
            catch (Exception error)
            {
                success = false;
                report.AppendLine(name + ": FAILED " + error);
                Debug.LogWarning("V12_VALIDATION " + name + " FAILED " + error.Message);
            }
        };
        check("Model/map", () => RushTestsV9.RunAll(complete ? 100 : 1));
        check("Action mechanisms (controlled fixtures)", () => RushActionTestsV11.RunAll());
        check("Independent pedestrian mechanisms", () => RushPedestrianTestsV12.RunAll());
        check("Player dash impact mechanisms", () => RushDashImpactTestsV12.RunAll());
        check("Coffee pedestrian mechanisms", () => RushCoffeeAITestsV12.RunAll());
        check("Camera numeric regression", () => RushCameraV10.Validate());
        for (int stage = 0; stage < 3; stage++)
        {
            int chapter = stage;
            check("Map seed=" + RouteSeed + " stage=" + chapter, () =>
            {
                var run = new RunV9(chapter, RouteSeed);
                if (!run.map.Validate(out string reason)) throw new Exception(reason);
                string density="stage="+chapter+" "+run.CrowdDensitySummary();
                report.AppendLine("Density "+density);Debug.Log("V12_DENSITY "+density);
                File.WriteAllText(output + "/layout-stage-" + chapter + ".json", run.map.ToPreviewJson());
                return 1;
            });
            if (!complete) continue;
            foreach (bool allTasks in new[] { false, true })
            {
                string kind = allTasks ? "all-tasks" : "mainline";
                try
                {
                    var route = allTasks ? RushTestsV9.ReplayAllTasks(chapter, RouteSeed) : RushTestsV9.ReplayMainline(chapter, RouteSeed);
                    double baseline = (allTasks ? V11AllTasks : V11Mainline)[chapter];
                    bool passed = route.success && route.elapsed <= new ConfigV9().stageLimit + .00001 && (!allTasks || route.completed == 3);
                    string line = kind + " stage=" + chapter + " seed=" + RouteSeed + " success=" + passed +
                        " seconds=" + route.elapsed.ToString("F2", Invariant) + " v11Seconds=" + baseline.ToString("F2", Invariant) +
                        " deltaSeconds=" + (route.elapsed - baseline).ToString("+0.00;-0.00;0.00", Invariant) +
                        " contacts=" + route.contacts + " npcWindups="+CountEvents(route,"npc-windup")+" npcCharges="+CountEvents(route,"npc-charge")+
                        " braceBlocks="+CountEvents(route,"brace-block")+" cancelled="+CountEvents(route,"npc-charge-cancelled")+ (allTasks ? " completed=" + route.completed + " states=" + string.Join("|", route.taskStates ?? new string[0]) : "") +
                        " stopped=" + route.stoppedAt + " reason=" + route.reason;
                    success &= passed; report.AppendLine(line); Debug.Log("V12_VALIDATION " + line);
                    var trace = new StringBuilder("time,type,id,x,y\n");
                    foreach (var entry in route.trace)
                        trace.AppendLine(string.Join(",", entry.time.ToString(Invariant), entry.type, entry.id.ToString(Invariant), entry.position.x.ToString(Invariant), entry.position.y.ToString(Invariant)));
                    File.WriteAllText(output + "/" + kind + "-stage-" + chapter + ".csv", trace.ToString());
                }
                catch (Exception error)
                {
                    success = false; report.AppendLine(kind + " stage=" + chapter + " FAILED " + error);
                    Debug.LogWarning("V12_VALIDATION " + kind + " stage=" + chapter + " FAILED " + error.Message);
                }
            }
        }
        report.AppendLine("V11 comparison source: Docs/V11-验证结果.txt, published values rounded to 0.01 seconds.");
        report.AppendLine("Controlled fixtures verify individual mechanics. Input routes verify achievable machine-driven routes only; neither is human playtesting or a general human encounter-rate estimate. Reported action counts are observed within these machine-driven routes.");
        report.AppendLine("Wall-clock seconds: " + watch.Elapsed.TotalSeconds.ToString("F2", Invariant));
        report.AppendLine("Passed: " + success);
        string reportPath = output + (complete ? "/validation.txt" : "/validation-quick.txt");
        File.WriteAllText(reportPath, report.ToString());
        if (!success) throw new Exception("V12 validation failed. Inspect " + reportPath);
    }

    static int CountEvents(RushTestsV9.ReplayResult route,string name)
    {
        int count=0;foreach(var entry in route.trace)if(entry.type==name)count++;return count;
    }
    [MenuItem("Early Rush/V12/Validate and Build Windows")]
    public static void Build() { Validate(); BuildPlayer(); }

    // Useful while tuning crowd presentation; release evidence still requires Validate().
    [MenuItem("Early Rush/V12/Quick Checks and Preview Build")]
    public static void BuildPreview() { ValidateQuick(); BuildPlayer(); }

    // Focused follow-up after coffee-only logic fixes; this does not replace full validation.
    [MenuItem("Early Rush/V12/Validate Coffee and Office Follow-up")]
    public static void ValidateOfficeFollowup()
    {
        string output = Destination + "/QA";
        Directory.CreateDirectory(output);
        var report = new StringBuilder("Early Rush V12 coffee/office follow-up\n");
        report.AppendLine("UTC: " + DateTime.UtcNow.ToString("o", Invariant));
        report.AppendLine("Scope: final coffee-state changes, default population and unchanged 420-second office input routes. Other-stage evidence remains in validation.txt.");
        int checks = RushCoffeeAITestsV12.RunAll();
        report.AppendLine("Coffee mechanisms: " + checks + " checks passed");
        bool success = true;
        foreach (bool allTasks in new[] { false, true })
        {
            var route = allTasks ? RushTestsV9.ReplayAllTasks(2, RouteSeed) : RushTestsV9.ReplayMainline(2, RouteSeed);
            bool passed = route.success && route.elapsed <= new ConfigV9().stageLimit + .00001 && (!allTasks || route.completed == 3);
            string kind = allTasks ? "all-tasks" : "mainline";
            string line = kind + " stage=2 seed=" + RouteSeed + " success=" + passed + " seconds=" + route.elapsed.ToString("F2", Invariant) +
                " contacts=" + route.contacts + (allTasks ? " completed=" + route.completed + " states=" + string.Join("|", route.taskStates ?? new string[0]) : "") +
                " reason=" + route.reason;
            success &= passed; report.AppendLine(line); Debug.Log("V12_OFFICE_FOLLOWUP " + line);
            var trace = new StringBuilder("time,type,id,x,y\n");
            foreach (var entry in route.trace)
                trace.AppendLine(string.Join(",", entry.time.ToString(Invariant), entry.type, entry.id.ToString(Invariant), entry.position.x.ToString(Invariant), entry.position.y.ToString(Invariant)));
            File.WriteAllText(output + "/followup-" + kind + "-stage-2.csv", trace.ToString());
        }
        report.AppendLine("Passed: " + success);
        File.WriteAllText(output + "/validation-office-followup.txt", report.ToString());
        if (!success) throw new Exception("V12 office follow-up failed");
    }

    public static void BuildAfterOfficeFollowup() { ValidateOfficeFollowup(); BuildPlayer(); }

    public static void BuildPlayer()
    {
        Directory.CreateDirectory("Assets/Resources");
        foreach (string name in new[] { "Sprites/Default", "Unlit/Texture", "GUI/Text Shader" })
        {
            var shader = Shader.Find(name);
            if (!shader) throw new Exception("Missing shader " + name);
            string path = "Assets/Resources/V12Keep_" + name.Replace('/', '_') + ".mat";
            if (!AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.CreateAsset(new Material(shader), path);
        }
        PlayerSettings.companyName = "Morning Club"; PlayerSettings.productName = "Early Rush"; PlayerSettings.bundleVersion = "0.12.0";
        PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = false; PlayerSettings.colorSpace = ColorSpace.Gamma;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Early Rush - V12 Independent Pedestrians").AddComponent<RushPlayV9>();
        Directory.CreateDirectory("Assets/Scenes"); EditorSceneManager.SaveScene(scene, "Assets/Scenes/EarlyRushV12.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/EarlyRushV12.unity", true) };
        AssetDatabase.SaveAssets();
        string output = Path.GetFullPath(Destination + "/EarlyRush.exe"); Directory.CreateDirectory(Path.GetDirectoryName(output));
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/Scenes/EarlyRushV12.unity" }, locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
        Debug.Log("V12_BUILD_RESULT " + result.summary.result);
        if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("V12 build failed");
    }
}

