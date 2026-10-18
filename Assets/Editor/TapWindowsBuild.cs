using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class TapWindowsBuild
{
    [MenuItem("TapTap/Build/Windows Development")]
    public static void Build()
    {
        var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
        const string bossTest="Assets/Scenes/BossTest.unity";
        if(File.Exists(bossTest) && !scenes.Contains(bossTest))scenes=scenes.Concat(new[]{bossTest}).ToArray();
        if(!scenes.Contains("Assets/Scenes/BiomechanicalCombatScene.unity")) throw new InvalidOperationException("Formal combat scene must be enabled");
        var args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"-tapbuildpath");
        var path=index>=0 && index+1<args.Length ? args[index+1] : "Builds/Windows/TapTap26.exe";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=scenes, locationPathName=path, target=BuildTarget.StandaloneWindows64,
            options=BuildOptions.Development
        });
        Debug.Log($"[TapWindowsBuild] {report.summary.result} errors={report.summary.totalErrors} warnings={report.summary.totalWarnings} bytes={report.summary.totalSize}");
        if(report.summary.result!=BuildResult.Succeeded) throw new InvalidOperationException("Windows build failed");
    }
}
