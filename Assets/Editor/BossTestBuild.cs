using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BossTestBuild
{
    public static void Build()
    {
        const string path="Assets/Scenes/BossTest.unity";
        if(!System.IO.File.Exists(path)) {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("BossTestEntry").AddComponent<BossEncounterController>();
            EditorSceneManager.SaveScene(scene,path);
        }
        var original=EditorBuildSettings.scenes;
        try {
            var list=new System.Collections.Generic.List<EditorBuildSettingsScene>(original);
            if(!list.Exists(s=>s.path==path)) list.Add(new EditorBuildSettingsScene(path,true));
            EditorBuildSettings.scenes=list.ToArray();
            TapWindowsBuild.Build();
        } finally { EditorBuildSettings.scenes=original; }
    }
}
