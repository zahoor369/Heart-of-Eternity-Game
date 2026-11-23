using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NED2024.EditorTools
{
    /// <summary>Builds the Windows x86-64 player.</summary>
    public static class BuildTools
    {
        [MenuItem("NED2024/Build Windows Player")]
        public static void BuildWindows()
        {
            PlayerSettings.productName = "Heart of Eternity";
            PlayerSettings.companyName = "Muhammad Zahoor Ul Haq CT-24148 - NED University";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outDir = Path.GetFullPath(Path.Combine(projectRoot, "..", "Windows_Build"));
            Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { GameBuilder.MenuScenePath, GameBuilder.GameScenePath },
                locationPathName = Path.Combine(outDir, "HeartOfEternity.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("BUILD_RESULT " + report.summary.result + " size=" + (report.summary.totalSize / (1024 * 1024)) + "MB errors=" + report.summary.totalErrors + " warnings=" + report.summary.totalWarnings);
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
