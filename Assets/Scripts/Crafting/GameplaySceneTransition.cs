using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameplaySceneTransition : MonoBehaviour
{
    private static GameplaySceneTransition instance;
    public static bool IsBusy { get; private set; }
    private CameraController[] cameras = new CameraController[0];
    private bool[] cameraStates = new bool[0];
    private static GameplaySceneTransition GetRunner()
    {
        if (instance != null) return instance;
        var runner = new GameObject("GameplaySceneTransition");
        instance = runner.AddComponent<GameplaySceneTransition>();
        DontDestroyOnLoad(runner);
        return instance;
    }

    public static bool TryOpen(string sceneName, PlayerController player, Action onFailure = null)
    {
        if (IsBusy || PlayerSessionData.IsCombatInputBlocked || (player != null && !player.IsAlive)) return false;
        if (!Application.CanStreamedLevelBeLoaded(sceneName)) return false;
        var runner = GetRunner();
        IsBusy = true;
        runner.CaptureCombat();
        PlayerSessionData.returnCombatSceneName = SceneManager.GetActiveScene().name;
        if (player != null) { player.CancelPlace(); PlayerSessionData.currentEquippedWeapon = player.equippedWeapon; }
        foreach (var build in FindObjectsOfType<BuildController>()) build.CancelPendingPlacement();
        if (BuildDockUI.Instance != null) BuildDockUI.Instance.CloseDrawer();
        PlayerSessionData.isCraftingOpen = true;
        GameJam.TimeManager.I.Refresh();
        foreach (var camera in runner.cameras) if (camera != null) camera.enabled = false;
        if (GameJam.HUD.I != null) GameJam.HUD.I.SetVisible(false);
        runner.StartCoroutine(runner.OpenRoutine(sceneName, onFailure));
        return true;
    }

    public static bool TryReturn(Scene overlay, Action save)
    {
        if (IsBusy || !overlay.IsValid() || !overlay.isLoaded) return false;
        var runner = GetRunner();
        bool additive = SceneManager.GetSceneByName(PlayerSessionData.returnCombatSceneName).isLoaded && overlay.name != PlayerSessionData.returnCombatSceneName;
        string target = string.IsNullOrEmpty(PlayerSessionData.returnCombatSceneName) ? "BiomechanicalCombatScene" : PlayerSessionData.returnCombatSceneName;
        if (!additive && !Application.CanStreamedLevelBeLoaded(target)) return false;
        IsBusy = true;
        try { save?.Invoke(); }
        catch (Exception exception) { IsBusy = false; Debug.LogException(exception); return false; }
        runner.StartCoroutine(runner.ReturnRoutine(overlay, additive, target));
        return true;
    }

    private void CaptureCombat()
    {
        cameras = FindObjectsOfType<CameraController>();
        cameraStates = new bool[cameras.Length];
        for (int index = 0; index < cameras.Length; index++) cameraStates[index] = cameras[index].enabled;
    }

    private IEnumerator OpenRoutine(string sceneName, Action onFailure)
    {
        AsyncOperation operation = null;
        try { operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive); }
        catch (Exception exception) { Debug.LogException(exception); }
        if (operation != null) yield return operation;
        if (!SceneManager.GetSceneByName(sceneName).isLoaded)
        {
            RestoreCombat();
            try { onFailure?.Invoke(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        yield return null;
        IsBusy = false;
        GameJam.TimeManager.I.Refresh();
    }

    private IEnumerator ReturnRoutine(Scene overlay, bool additive, string target)
    {
        string overlayName = overlay.name;
        AsyncOperation operation = null;
        try { operation = additive ? SceneManager.UnloadSceneAsync(overlay) : SceneManager.LoadSceneAsync(target, LoadSceneMode.Single); }
        catch (Exception exception) { Debug.LogException(exception); }
        if (operation != null) yield return operation;
        if (SceneManager.GetSceneByName(overlayName).isLoaded)
        {
            IsBusy = false;
            yield break;
        }
        RestoreCombat();
        PlayerSessionData.hasJustReturnedFromCrafting = true;
        yield return null;
        IsBusy = false;
        GameJam.TimeManager.I.Refresh();
    }

    private void RestoreCombat()
    {
        for (int index = 0; index < cameras.Length; index++) if (cameras[index] != null) cameras[index].enabled = cameraStates[index];
        PlayerSessionData.isCraftingOpen = false;
        if (GameJam.HUD.I != null) GameJam.HUD.I.SetVisible(true);
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        IsBusy = false;
    }
}
