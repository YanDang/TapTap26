using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class AndroidDisplayAdapter : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (Application.platform != RuntimePlatform.Android) return;
        Application.targetFrameRate = 60;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.AutoRotation;
        var adapter = new GameObject("AndroidDisplayAdapter");
        DontDestroyOnLoad(adapter);
        adapter.AddComponent<AndroidDisplayAdapter>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(AdaptCanvases());
    }

    private IEnumerator AdaptCanvases()
    {
        yield return null;
        foreach (var scaler in FindObjectsOfType<CanvasScaler>(true))
        {
            var canvas = scaler.GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) continue;
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }
    }
}
