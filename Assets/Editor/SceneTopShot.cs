using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 调试用：俯视渲染 Main 场景（Logs/scene_top.png），并列出场景里有渲染器的物体（Logs/scene_objects.txt）
public static class SceneTopShot
{
    [MenuItem("GameJam/Debug/Scene Top Shot")]
    public static void Shoot()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
        var go = new GameObject("TopCam");
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 14.5f;
        cam.aspect = 34f / 29f;
        go.transform.SetPositionAndRotation(new Vector3(0, 40, 0.5f), Quaternion.Euler(90, 0, 0));
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        const int W = 1360, H = 1160;
        var rt = new RenderTexture(W, H, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        File.WriteAllBytes("Logs/scene_top.png", tex.EncodeToPNG());

        var sb = new StringBuilder();
        foreach (var t in Object.FindObjectsOfType<Transform>())
        {
            if (t.parent == null) continue;
            var p = t.parent;
            if (p.parent == null || p.parent.parent != null) continue;   // 只列根下面第二层（各个组里的直接子物体）
            sb.AppendLine($"{p.name}/{t.name}\t{t.position.x:F1}\t{t.position.z:F1}");
        }
        File.WriteAllText("Logs/scene_objects.txt", sb.ToString());
        Debug.Log("[SceneTopShot] done");
    }
}
