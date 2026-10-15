using UnityEditor;
using UnityEditor.SceneManagement;

public static class StartMenuEditor
{
    [MenuItem("Game/Play From Start Menu")]
    public static void PlayFromStartMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Scenes/StartMenu.unity");
        EditorApplication.isPlaying = true;
    }
}
