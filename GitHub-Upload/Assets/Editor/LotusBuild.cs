using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor helpers: creates the (empty) main scene and builds a Windows player.
// The game itself is assembled at runtime by Game.Boot, so the scene needs no content.
public static class LotusBuild
{
    const string ScenePath = "Assets/Scenes/Lotus.unity";

    [MenuItem("Lotus/Open Main Scene")]
    public static void EnsureScene()
    {
        if (!File.Exists(ScenePath))
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        else if (!Application.isBatchMode)
        {
            EditorSceneManager.OpenScene(ScenePath);
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    [MenuItem("Lotus/Build Windows")]
    public static void BuildWindows()
    {
        EnsureScene();
        PlayerSettings.productName = "Lotus";
        PlayerSettings.companyName = "Vibecode";
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultIsNativeResolution = true;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = false;
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/Windows/Lotus.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log("[LotusBuild] " + report.summary.result + " errors=" + report.summary.totalErrors);
        if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
