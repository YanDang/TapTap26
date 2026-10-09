using System.IO;
using UnityEditor;
using UnityEngine;

// Play 模式下：让离主角最近的鱿鱼马上进入抓取前摇（愤怒脸 + 红光），用特写相机拍鱿鱼头部到 Temp/squid_face.png
public static class SquidFaceShot
{
    [MenuItem("GameJam/Squid/Shoot Angry Face (Play mode)")]
    public static void Shoot()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[SquidFace] 先进入 Play 模式"); return; }
        var gm = GameJam.GameManager.I;
        GameJam.EnemyAI e = null;
        foreach (var x in GameJam.EnemyAI.All) if (x && !x.Dead) { e = x; break; }
        if (!e) e = gm.SpawnEnemy(GameJam.EnemyKind.Beast, gm.player.Pos + new Vector3(2.5f, 0, 0));
        e.Teleport(gm.player.Pos + new Vector3(2.5f, 0, 0));
        e.speed = 0; e.forceGrab = true; e.forceAttackNow = true; e.attackRange = 99f; e.aggroRange = 99f;
        var go = new GameObject("SquidFaceShot");
        go.AddComponent<Shooter>().target = e;
    }

    class Shooter : MonoBehaviour
    {
        public GameJam.EnemyAI target;
        float t;
        void LateUpdate()
        {
            t += Time.unscaledDeltaTime;
            if (t < 0.9f || !target) return;
            var main = Camera.main;
            var cam = new GameObject("cam").AddComponent<Camera>();
            cam.CopyFrom(main);
            cam.orthographic = true; cam.orthographicSize = 1.4f;
            var head = target.squid ? target.squid.body : target.transform;
            cam.transform.position = head.position - main.transform.forward * 30f;
            var rt = new RenderTexture(600, 600, 24); cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(600, 600, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 600, 600), 0, 0); tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes("Temp/squid_face.png", tex.EncodeToPNG());
            Debug.Log("[SquidFace] Temp/squid_face.png");
            Destroy(cam.gameObject); Destroy(gameObject);
        }
    }
}
