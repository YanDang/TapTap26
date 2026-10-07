using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BiomechanicalCrafting.Editor
{
    [InitializeOnLoad]
    public static class AutoMixSceneBuilder
    {
        private const string TriggerFile = "Temp/TriggerMixBuild.txt";
        private const string TriggerCollectFile = "Temp/TriggerCollectBuild.txt";
        private const string TriggerPlayerFile = "Temp/TriggerPlayerBuild.txt";
        private const string BuildResultFile = "Temp/BuildResult.txt";

        static AutoMixSceneBuilder()
        {
            EditorApplication.delayCall += CheckAndBuild;
            EditorApplication.update += CheckAndBuild;
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            EditorApplication.delayCall += CheckAndBuild;
        }

        public static void CheckAndBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (File.Exists(TriggerFile))
            {
                try
                {
                    File.Delete(TriggerFile);
                    Debug.Log("<color=#00FFFF>[AutoMixSceneBuilder]</color> 检测到场景构建触发指令，开始构建 Mix 背包场景...");
                    ShellInventorySceneBuilder.BuildMixScene();
                    Debug.Log("<color=#00FFFF>[AutoMixSceneBuilder]</color> Mix 背包场景已由自动脚本成功构建并持久化保存！");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AutoMixSceneBuilder] 构建 Mix 场景异常: {ex}");
                }
            }

            if (File.Exists(TriggerCollectFile))
            {
                try
                {
                    File.Delete(TriggerCollectFile);
                    Debug.Log("<color=#00FFFF>[AutoMixSceneBuilder]</color> 检测到采集场景构建触发指令，开始构建 Collect 场景...");
                    CollectSceneBuilder.BuildCollectScene();
                    Debug.Log("<color=#00FFFF>[AutoMixSceneBuilder]</color> Collect 场景已成功构建并持久化保存！");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AutoMixSceneBuilder] 构建 Collect 场景异常: {ex}");
                }
            }

            if (File.Exists(TriggerPlayerFile))
            {
                try
                {
                    File.Delete(TriggerPlayerFile);
                    Debug.Log("<color=#00FFFF>[AutoMixSceneBuilder]</color> 检测到独立客户端构建触发指令，开始构建 Windows 客户端...");
                    ShellInventorySceneBuilder.BuildMixScene();
                    
                    string buildPath = "C:/Users/YiLog/Desktop/test/Mix/TapTap2026.exe";
                    string[] scenes = new string[] { "Assets/Scenes/Mix.unity" };
                    var report = BuildPipeline.BuildPlayer(scenes, buildPath, BuildTarget.StandaloneWindows64, BuildOptions.None);
                    
                    string res = $"Result={report.summary.result}|Errors={report.summary.totalErrors}|Time={report.summary.totalTime.TotalSeconds:F1}s|Size={report.summary.totalSize}";
                    File.WriteAllText(BuildResultFile, res);
                    Debug.Log($"<color=#00FFFF>[AutoMixSceneBuilder]</color> Windows 客户端构建完成: {res}");
                }
                catch (Exception ex)
                {
                    File.WriteAllText(BuildResultFile, $"Result=Failed|Exception={ex.Message}");
                    Debug.LogError($"[AutoMixSceneBuilder] 构建独立客户端异常: {ex}");
                }
            }
        }
    }
}
