using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Runtime-only P0 alignment. No scene/prefab regeneration. Camera input, pinch and time ownership remain unchanged.</summary>
public sealed class TapCombatScale : MonoBehaviour
{
    // Source 00_overview.png: visible Hero2D silhouette ~235 / 1080 (40_boss_idle clips player).
    public const float SourceScreenFraction = 235f / 1080f;
    // The guide's 2m was approximate: screenshot at ortho 5.5 measures ~2.394m.
    // Identical Hero2D prefab SHA + target ancestor scale explain the original size difference.
    public const float SourceWorldHeight = SourceScreenFraction * (2f * 5.5f);
    public const float SourceHeightsPerSecond = 4.5f / SourceWorldHeight;
    public float VisualHeight { get; private set; }
    public float SpeedFactor { get; private set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install() { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; }
    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if(scene.name != "BiomechanicalCombatScene") return;
        if(Array.IndexOf(Environment.GetCommandLineArgs(), "-tapbaseline") >= 0) return;
        var go = new GameObject("TapCombatScale");
        SceneManager.MoveGameObjectToScene(go,scene);
        go.AddComponent<TapCombatScale>();
    }
    public static Bounds HeroBounds(PlayerController player)
    {
        var renderers = player.hero.GetComponentsInChildren<SpriteRenderer>();
        bool found=false; var bounds=new Bounds();
        foreach(var r in renderers) if(r.enabled && r.gameObject.activeInHierarchy && r.sprite != null) {
            if(!found) { bounds=r.bounds; found=true; } else bounds.Encapsulate(r.bounds);
        }
        if(!found) throw new InvalidOperationException("No visible Hero2D renderers");
        return bounds;
    }
    IEnumerator Start()
    {
        // All scene Awake and Start handlers have completed; initialize once, never recapture animated sizes.
        yield return null;
        var player=PlayerController.Instance;
        var control=CameraController.Instance;
        if(player==null || player.hero==null || control==null) { Debug.LogError("[TapScale] Missing combat dependencies"); yield break; }
        VisualHeight=HeroBounds(player).size.y;
        float oldSpeed=player.baseMoveSpeed;
        player.baseMoveSpeed=VisualHeight*SourceHeightsPerSecond;
        SpeedFactor=player.baseMoveSpeed/oldSpeed;
        player.rollSpeed*=SpeedFactor;
        var camera=control.GetComponent<Camera>();
        float size=VisualHeight/(2f*SourceScreenFraction);
        float zoomFactor=size/camera.orthographicSize;
        control.minZoom*=zoomFactor; control.maxZoom*=zoomFactor;
        control.FocusOn(player.transform.position,size);
        Debug.Log($"[TapScale] height={VisualHeight:R} speed={player.baseMoveSpeed:R} roll={player.rollSpeed:R} size={size:R} min={control.minZoom:R} max={control.maxZoom:R}");
        // Ordinary enemy speeds remain unchanged: 1.35 u/s is already slower than aligned player.
    }
}
