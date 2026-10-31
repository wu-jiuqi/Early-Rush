using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
public static class BuildEarlyRush
{
    public static void Build()
    {
        Directory.CreateDirectory("Assets/Resources");
        foreach (string shaderName in new [] { "Standard", "Sprites/Default", "GUI/Text Shader", "Unlit/Color" })
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new System.Exception("Required shader missing: " + shaderName);
            string materialPath = "Assets/Resources/Keep_" + shaderName.Replace('/', '_').Replace(' ', '_') + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) == null)
                AssetDatabase.CreateAsset(new Material(shader), materialPath);
        }
        AssetDatabase.SaveAssets();
        PlayerSettings.companyName = "Morning Club";
        PlayerSettings.productName = "Early Rush";
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new []{UnityEngine.Rendering.GraphicsDeviceType.Direct3D11});
        QualitySettings.vSyncCount = 0;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.Medium;
        QualitySettings.shadowDistance = 65;
        QualitySettings.antiAliasing = 2;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Early Rush").AddComponent<EarlyRush.RushGame>();
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/EarlyRush.unity");
        EditorBuildSettings.scenes = new []{new EditorBuildSettingsScene("Assets/Scenes/EarlyRush.unity", true)};
        string output = Path.GetFullPath("../artifacts/EarlyRush-Windows/EarlyRush.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new []{"Assets/Scenes/EarlyRush.unity"}, locationPathName=output, target=BuildTarget.StandaloneWindows64, options=BuildOptions.None });
        Debug.Log("EARLY_RUSH_BUILD: " + report.summary.result + " bytes=" + report.summary.totalSize);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception("Build failed");
    }
}
