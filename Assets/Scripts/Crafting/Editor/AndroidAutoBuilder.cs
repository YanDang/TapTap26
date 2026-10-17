using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class AndroidAutoBuilder
{
    private const string TriggerFile = "Builds/build_trigger.txt";
    private const string StatusFile = "Builds/build_status.txt";
    private static bool isBuilding = false;

    static AndroidAutoBuilder()
    {
        EditorApplication.update += CheckBuildTrigger;
    }

    [MenuItem("Build/Android/Auto Build Release APK")]
    public static void TriggerBuildFromMenu()
    {
        PerformBuild();
    }

    private static void CheckBuildTrigger()
    {
        if (isBuilding || EditorApplication.isCompiling) return;

        if (File.Exists(TriggerFile))
        {
            AssetDatabase.Refresh();
            if (EditorApplication.isCompiling) return;

            try
            {
                File.Delete(TriggerFile);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AndroidAutoBuilder] Failed to delete trigger file: " + ex.Message);
            }

            PerformBuild();
        }
    }

    public static void PerformBuild()
    {
        if (isBuilding) return;
        isBuilding = true;

        try
        {
            Directory.CreateDirectory("Builds");
            Directory.CreateDirectory("Builds/Android");
            File.WriteAllText(StatusFile, "STATUS:BUILDING");

            Debug.Log("[AndroidAutoBuilder] Starting Android build...");

            // 1. Switch active build target if necessary
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[AndroidAutoBuilder] Switching active build target to Android...");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            // 2. Configure keystore
            string keystorePath = Path.Combine(Directory.GetCurrentDirectory(), "HermitStudio.keystore");
            if (File.Exists(keystorePath))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystorePath;
                PlayerSettings.Android.keystorePass = "hermitstudio";
                PlayerSettings.Android.keyaliasName = "hermit";
                PlayerSettings.Android.keyaliasPass = "hermitstudio";
                Debug.Log("[AndroidAutoBuilder] Keystore configured successfully.");
            }
            else
            {
                Debug.LogWarning("[AndroidAutoBuilder] HermitStudio.keystore not found at: " + keystorePath);
            }

            // 3. Build Settings
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && File.Exists(s.path))
                .Select(s => s.path)
                .ToArray();

            string apkPath = Path.GetFullPath("Builds/Android/ShellSeaJourney.apk");

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);

            if (report.summary.result == BuildResult.Succeeded)
            {
                string localizedApk = Path.GetFullPath("Builds/Android/螺壳海底游记.apk");
                File.Copy(apkPath, localizedApk, true);
                long bytes = new FileInfo(apkPath).Length;
                double seconds = report.summary.totalTime.TotalSeconds;

                string successMsg = $"STATUS:SUCCESS|TIME:{seconds:F1}s|SIZE:{bytes}";
                File.WriteAllText(StatusFile, successMsg);
                Debug.Log($"[AndroidAutoBuilder] Android build succeeded in {seconds:F1}s! Output size: {bytes} bytes. Copied to: {localizedApk}");
            }
            else
            {
                string failMsg = $"STATUS:FAILED|ERRORS:{report.summary.totalErrors}";
                File.WriteAllText(StatusFile, failMsg);
                Debug.LogError($"[AndroidAutoBuilder] Android build failed with {report.summary.totalErrors} errors.");
            }
        }
        catch (Exception ex)
        {
            File.WriteAllText(StatusFile, "STATUS:EXCEPTION|" + ex.Message);
            Debug.LogError("[AndroidAutoBuilder] Exception during build: " + ex);
        }
        finally
        {
            isBuilding = false;
        }
    }
}
