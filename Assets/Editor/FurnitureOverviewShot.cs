using System.IO;
using GameJam;
using UnityEditor;
using UnityEngine;

// Play 模式下：从游戏镜头的角度拉远，拍出生点周围所有战斗家具的摆放 → Temp/furniture_overview.png
public static class FurnitureOverviewShot
{
    [MenuItem("GameJam/Furniture/Shoot Overview (Play mode)")]
    static void Shoot()
    {
        if (!Application.isPlaying || !GameManager.I) { Debug.LogWarning("[Furniture] 先进入 Play 模式"); return; }
        var main = Camera.main;
        var cam = new GameObject("cam").AddComponent<Camera>();
        cam.CopyFrom(main);
        cam.orthographic = true; cam.orthographicSize = 9f;
        var center = GameManager.I.player.Pos;
        if (Selection.activeGameObject) center = Selection.activeGameObject.transform.position;   // 选中了物体就对准它
        cam.transform.position = center - main.transform.forward * 60f;
        var rt = new RenderTexture(1280, 900, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0); tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes("Temp/furniture_overview.png", tex.EncodeToPNG());
        Object.DestroyImmediate(cam.gameObject); rt.Release();
        Debug.Log("[Furniture] Temp/furniture_overview.png");
    }
}
