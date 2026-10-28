// Builds the LAN test scenes so multiplayer can also be checked outside the Unity Editor.

using UnityEditor;
using UnityEditor.Build.Reporting;

//Builds a separate development player for real multi-process LAN checks.
public static class LanBuildChecks
{
    public static void Build()
    {
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Race_Gamemode.unity" },
            locationPathName = "Logs/LanPlayer/Escape4Now.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
