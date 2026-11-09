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
        BumpVersion();
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

    // Каждая сборка получает новый номер: последняя цифра +1 (0.1.0 → 0.1.1). Версия видна справа внизу главного меню.
    // Старую 0.0.x переводим на 0.1.0. Номер хранится в ProjectSettings (PlayerSettings.bundleVersion).
    public static string BumpVersion()
    {
        string[] parts = PlayerSettings.bundleVersion.Split('.');
        int major = 0, minor = 0, patch = 0;
        if (parts.Length > 0) int.TryParse(parts[0], out major);
        if (parts.Length > 1) int.TryParse(parts[1], out minor);
        if (parts.Length > 2) int.TryParse(parts[2], out patch);
        if (major == 0 && minor == 0) { minor = 1; patch = 0; }
        else patch++;
        PlayerSettings.bundleVersion = major + "." + minor + "." + patch;
        AssetDatabase.SaveAssets();
        return PlayerSettings.bundleVersion;
    }
}
