using System;
using System.IO;
using System.Text;
using EarlyRush.V9;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Independent V15 entry; never invokes a V13/V14 builder or overwrites their scenes.</summary>
public static class BuildEarlyRushV15
{
    public const string Destination = "Builds/EarlyRush-V15";
    const string ScenePath = "Assets/Scenes/EarlyRushV15.unity";

    [MenuItem("Early Rush/V15/Prepare Scene and UI Art")]
    public static void Prepare()
    {
        AssetDatabase.Refresh();
        RushUIV15Import.Apply();
        foreach (string name in new[] { "Sprites/Default", "UI/Default", "GUI/Text Shader", "Unlit/Texture" })
        {
            string path = "Assets/Resources/V15Keep_" + name.Replace('/', '_') + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path)) continue;
            Shader shader = Shader.Find(name);
            if (!shader) throw new InvalidOperationException("Missing V15 shader " + name);
            AssetDatabase.CreateAsset(new Material(shader), path);
        }
        PlayerSettings.companyName = "Morning Club";
        PlayerSettings.productName = "Early Rush";
        PlayerSettings.bundleVersion = "0.15.0";
        PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = false; PlayerSettings.colorSpace = ColorSpace.Gamma;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Early Rush - V15 Icon UI").AddComponent<RushPlayV9>();
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("V15_PREPARED " + ScenePath);
    }

    [MenuItem("Early Rush/V15/Validate UI and Focused Mechanisms")]
    public static void Validate()
    {
        Directory.CreateDirectory(Destination + "/QA");
        var report = new StringBuilder("V15 UI integration focused validation\n");
        report.AppendLine("UTC: " + DateTime.UtcNow.ToString("o"));
        report.AppendLine("Scope: exact UI assets, existing dash/minimap/coffee/action mechanisms. No full routes, full seed sweep or human playtest claim.");
        bool passed = true;
        Action<string, Func<int>> check = (label, action) =>
        {
            try { int count = action(); report.AppendLine(label + ": " + count + " assertions passed"); Debug.Log("V15_VALIDATION " + label + " PASS " + count); }
            catch (Exception error) { passed = false; report.AppendLine(label + ": FAILED " + error); Debug.LogWarning("V15_VALIDATION " + label + " FAILED " + error.Message); }
        };
        check("UI sprites", RushUIV15Import.ValidateAssets);
        check("Basic actions", RushActionTestsV11.RunAll);
        check("Dash cadence and input buffer", RushDashFeelTestsV13.RunAll);
        check("Minimap projection", RushMinimapTestsV13.RunAll);
        check("Coffee mechanisms", RushCoffeeAITestsV12.RunAll);
        report.AppendLine("Passed: " + passed);
        File.WriteAllText(Destination + "/QA/validation-focused.txt", report.ToString());
        if (!passed) throw new InvalidOperationException("V15 focused validation failed; see " + Destination + "/QA/validation-focused.txt");
    }

    [MenuItem("Early Rush/V15/Prepare Validate and Build Windows")]
    public static void Build()
    {
        Prepare();
        Validate();
        string executable = Path.GetFullPath(Destination + "/EarlyRush.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(executable));
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath },
            locationPathName = executable, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
        Directory.CreateDirectory(Destination + "/QA");
        string summary = "V15_BUILD_RESULT " + result.summary.result + " errors=" + result.summary.totalErrors
            + " warnings=" + result.summary.totalWarnings + " bytes=" + result.summary.totalSize;
        File.WriteAllText(Destination + "/QA/build-summary.txt", summary + "\n");
        Debug.Log(summary);
        if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("V15 build failed");
    }
}
