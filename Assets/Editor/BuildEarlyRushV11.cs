using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using EarlyRush.V9;

/// <summary>V11 checks use the shipped configuration. Mechanism fixtures and input routes are reported separately.</summary>
public static class BuildEarlyRushV11
{
    const string Destination = "Builds/EarlyRush-V11";
    const int RouteSeed = 91026;
    static readonly double[] V10Mainline = { 155.02, 164.98, 157.95 };
    static readonly double[] V10AllTasks = { 195.83, 224.90, 224.92 };
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [MenuItem("Early Rush/V11/Validate Quick Mechanisms")]
    public static void ValidateQuick() { ValidateInternal(false); }

    [MenuItem("Early Rush/V11/Validate Complete")]
    public static void Validate() { ValidateInternal(true); }

    static void ValidateInternal(bool complete)
    {
        string output = Destination + "/QA";
        Directory.CreateDirectory(output);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var report = new StringBuilder("Early Rush V11 validation\n");
        report.AppendLine("UTC: " + DateTime.UtcNow.ToString("o", Invariant));
        report.AppendLine("Scope: " + (complete ? "complete model, action, crowd, camera and default-deadline input routes" : "quick mechanism checks and route-seed map validation; no route completion claim"));
        report.AppendLine("Default stage deadline: " + new ConfigV9().stageLimit.ToString(Invariant) + " seconds.");
        bool success = true;
        Action<string, Func<int>> check = (name, run) =>
        {
            try
            {
                string line = name + ": " + run() + " checks passed";
                report.AppendLine(line); Debug.Log("V11_VALIDATION " + line);
            }
            catch (Exception error)
            {
                success = false;
                report.AppendLine(name + ": FAILED " + error);
                Debug.LogWarning("V11_VALIDATION " + name + " FAILED " + error.Message);
            }
        };
        check("Model/map", () => RushTestsV9.RunAll(complete ? 100 : 1));
        check("Action mechanisms (controlled fixtures)", () => RushActionTestsV11.RunAll());
        check("Crowd mechanisms", () => RushCrowdTestsV11.RunAll());
        check("Camera numeric regression", () => RushCameraV10.Validate());
        for (int stage = 0; stage < 3; stage++)
        {
            int chapter = stage;
            check("Map seed=" + RouteSeed + " stage=" + chapter, () =>
            {
                var run = new RunV9(chapter, RouteSeed);
                if (!run.map.Validate(out string reason)) throw new Exception(reason);
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
                    double baseline = (allTasks ? V10AllTasks : V10Mainline)[chapter];
                    bool passed = route.success && route.elapsed <= new ConfigV9().stageLimit + .00001 && (!allTasks || route.completed == 3);
                    string line = kind + " stage=" + chapter + " seed=" + RouteSeed + " success=" + passed +
                        " seconds=" + route.elapsed.ToString("F2", Invariant) + " v10Seconds=" + baseline.ToString("F2", Invariant) +
                        " deltaSeconds=" + (route.elapsed - baseline).ToString("+0.00;-0.00;0.00", Invariant) +
                        " contacts=" + route.contacts + (allTasks ? " completed=" + route.completed + " states=" + string.Join("|", route.taskStates ?? new string[0]) : "") +
                        " stopped=" + route.stoppedAt + " reason=" + route.reason;
                    success &= passed; report.AppendLine(line); Debug.Log("V11_VALIDATION " + line);
                    var trace = new StringBuilder("time,type,id,x,y\n");
                    foreach (var entry in route.trace)
                        trace.AppendLine(string.Join(",", entry.time.ToString(Invariant), entry.type, entry.id.ToString(Invariant), entry.position.x.ToString(Invariant), entry.position.y.ToString(Invariant)));
                    File.WriteAllText(output + "/" + kind + "-stage-" + chapter + ".csv", trace.ToString());
                }
                catch (Exception error)
                {
                    success = false; report.AppendLine(kind + " stage=" + chapter + " FAILED " + error);
                    Debug.LogWarning("V11_VALIDATION " + kind + " stage=" + chapter + " FAILED " + error.Message);
                }
            }
        }
        report.AppendLine("V10 comparison source: Docs/V10-验证结果.txt, published values rounded to 0.01 seconds.");
        report.AppendLine("Controlled fixtures verify individual mechanics. Input routes verify achievable machine-driven routes only; neither is human playtesting or observed natural attack frequency.");
        report.AppendLine("Wall-clock seconds: " + watch.Elapsed.TotalSeconds.ToString("F2", Invariant));
        report.AppendLine("Passed: " + success);
        string reportPath = output + (complete ? "/validation.txt" : "/validation-quick.txt");
        File.WriteAllText(reportPath, report.ToString());
        if (!success) throw new Exception("V11 validation failed. Inspect " + reportPath);
    }

    [MenuItem("Early Rush/V11/Validate and Build Windows")]
    public static void Build() { Validate(); BuildPlayer(); }

    public static void BuildPlayer()
    {
        Directory.CreateDirectory("Assets/Resources");
        foreach (string name in new[] { "Sprites/Default", "Unlit/Texture", "GUI/Text Shader" })
        {
            var shader = Shader.Find(name);
            if (!shader) throw new Exception("Missing shader " + name);
            string path = "Assets/Resources/V11Keep_" + name.Replace('/', '_') + ".mat";
            if (!AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.CreateAsset(new Material(shader), path);
        }
        PlayerSettings.companyName = "Morning Club"; PlayerSettings.productName = "Early Rush"; PlayerSettings.bundleVersion = "0.11.0";
        PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = false; PlayerSettings.colorSpace = ColorSpace.Gamma;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Early Rush - V11 Crowd and Actions").AddComponent<RushPlayV9>();
        Directory.CreateDirectory("Assets/Scenes"); EditorSceneManager.SaveScene(scene, "Assets/Scenes/EarlyRushV11.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/EarlyRushV11.unity", true) };
        AssetDatabase.SaveAssets();
        string output = Path.GetFullPath(Destination + "/EarlyRush.exe"); Directory.CreateDirectory(Path.GetDirectoryName(output));
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/Scenes/EarlyRushV11.unity" }, locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
        Debug.Log("V11_BUILD_RESULT " + result.summary.result);
        if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("V11 build failed");
    }
}
