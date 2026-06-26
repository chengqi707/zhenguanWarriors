using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

using ZhenguanWarriors.Core.Level;

public class BuildScript
{
    [MenuItem("贞观勇士/导出全部关卡JSON")]
    public static void ExportLevels()
    {
        LevelLibrary.ExportAllToJson();
        Debug.Log("[编辑器] 全部关卡 JSON 导出完成");
    }

    public static void BuildAPK()
    {
        string[] scenes = new string[] { "Assets/Scenes/SampleScene.scene" };
        string outputPath = "zhenguanWarriors_v0.2.apk";

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = scenes;
        buildPlayerOptions.locationPathName = outputPath;
        buildPlayerOptions.target = BuildTarget.Android;
        buildPlayerOptions.options = BuildOptions.None;

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log("Build succeeded: " + summary.totalSize + " bytes");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError("Build failed: " + summary.result);
            EditorApplication.Exit(1);
        }
    }
}
