using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class AndroidReleaseBuild : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    [MenuItem("Build/Android/Build Release APK")]
    public static void BuildApk()
    {
        Build(false);
    }

    [MenuItem("Build/Android/Build Release AAB")]
    public static void BuildBundle()
    {
        Build(true);
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android) return;
        Validate((report.summary.options & BuildOptions.Development) == 0);
    }

    private static void Validate(bool release)
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            throw new BuildFailedException("请先为当前 Unity 安装 Android Build Support、SDK & NDK Tools 和 OpenJDK。");
        if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) != ScriptingImplementation.IL2CPP ||
            (PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
            throw new BuildFailedException("Android 构建必须启用 IL2CPP 和 ARM64。");

        var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
        string[] requiredScenes = { "StartMenu", "BiomechanicalCombatScene", "Mix", "Collect" };
        if (scenes.Length == 0 || Path.GetFileNameWithoutExtension(scenes[0].path) != requiredScenes[0] ||
            requiredScenes.Any(required => !scenes.Any(scene => Path.GetFileNameWithoutExtension(scene.path) == required)) ||
            scenes.Any(scene => !File.Exists(scene.path)))
            throw new BuildFailedException("构建场景必须以 StartMenu 开始，并包含 BiomechanicalCombatScene、Mix 和 Collect。");

        if (!release) return;
        string identifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
        if (string.IsNullOrWhiteSpace(identifier) || identifier.IndexOf("DefaultCompany", StringComparison.OrdinalIgnoreCase) >= 0)
            throw new BuildFailedException("请在 Player Settings 中填写正式 Android Package Name，不要使用 DefaultCompany。");
        if (string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion) || PlayerSettings.Android.bundleVersionCode < 1)
            throw new BuildFailedException("请填写版本号和大于零的 Android Bundle Version Code。");
        if (!PlayerSettings.Android.useCustomKeystore || string.IsNullOrWhiteSpace(PlayerSettings.Android.keystoreName) ||
            !File.Exists(PlayerSettings.Android.keystoreName) || string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName) ||
            string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) || string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
            throw new BuildFailedException("请在 Publishing Settings 配置正式 keystore、alias 及密码；请勿上传调试签名包。");
    }

    private static void Build(bool bundle)
    {
        Validate(true);
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            throw new BuildFailedException("请先在 Build Settings 切换至 Android，等待资源导入及脚本编译完成后再打包。");
        string extension = bundle ? "aab" : "apk";
        string path = EditorUtility.SaveFilePanel("Build Android Release", "Builds", "TapTap2026-" + PlayerSettings.bundleVersion, extension);
        if (string.IsNullOrEmpty(path)) return;

        bool previousBundle = EditorUserBuildSettings.buildAppBundle;
        bool previousExport = EditorUserBuildSettings.exportAsGoogleAndroidProject;
        try
        {
            EditorUserBuildSettings.buildAppBundle = bundle;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Android 构建失败，请检查 Console 和 Editor.log。");
            Debug.Log("Android 发布包已生成：" + path);
        }
        finally
        {
            EditorUserBuildSettings.buildAppBundle = previousBundle;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = previousExport;
        }
    }
}
