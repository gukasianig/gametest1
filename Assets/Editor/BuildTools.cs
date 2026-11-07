using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Windows build of the game. Menu: Tools > Build Windows. Batch:
//   Unity.exe -batchmode -quit -projectPath . -executeMethod BuildTools.BuildWindows -logFile Logs/build.log
// Output: Builds/Windows/ArenaSurvival.exe (ignored by Git). Saves live in %USERPROFILE%\AppData\LocalLow, not in the build
// folder, so rebuilding into the same folder keeps the player's progress.
public static class BuildTools
{
    private const string OutputFolder = "Builds/Windows";
    private const string ExeName = "ArenaSurvival.exe";

    [MenuItem("Tools/Build Windows")]
    public static void BuildWindows()
    {
        string[] scenes = Array.ConvertAll(Array.FindAll(EditorBuildSettings.scenes, s => s.enabled), s => s.path);
        if (scenes.Length == 0) throw new Exception("No enabled scenes in Build Settings.");
        Directory.CreateDirectory(OutputFolder);
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(OutputFolder, ExeName),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        string text = summary.result + " | version " + PlayerSettings.bundleVersion + " | " + summary.totalErrors +
            " errors | " + (summary.totalSize / (1024 * 1024)) + " MB | " + summary.totalTime;
        Debug.Log("Windows build: " + text);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/build-result.txt", text);
        if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
