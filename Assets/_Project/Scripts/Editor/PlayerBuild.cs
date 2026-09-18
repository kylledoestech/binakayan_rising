using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BinakayanRising.EditorTools
{
    /// <summary>
    /// Builds the Linux player the screenshot autopilot runs in.
    /// </summary>
    /// <remarks>
    /// Batchmode, with the editor closed:
    /// <c>Unity -batchmode -quit -projectPath . -executeMethod BinakayanRising.EditorTools.PlayerBuild.BuildLinux</c>.
    /// Exits non-zero when the build fails, so a script can stop on it.
    /// </remarks>
    public static class PlayerBuild
    {
        public const string LinuxPath = "Builds/Linux/BinakayanRising.x86_64";

        [MenuItem("Tools/Binakayan Rising/Build Linux Player", priority = 40)]
        public static void BuildLinux()
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = LinuxPath,
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log("[PlayerBuild] " + summary.result + " in " + summary.totalTime + ", " + summary.totalErrors + " errors, " + summary.outputPath);

            if (Application.isBatchMode && summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
