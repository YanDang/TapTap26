using System.Collections;
using System.IO;
using GameJam;
using UnityEditor;
using UnityEngine;

// Play 模式下：把一只鱿鱼送进最近的气囊疙瘩，拍 平时 / 鼓胀 / 爆炸 三张到 Temp/GasCyst/
public static class GasCystShots
{
    [MenuItem("GameJam/Furniture/Shoot Gas Cyst (Play mode)")]
    static void Shoot()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[GasCyst] 先进入 Play 模式（场景里要有气囊疙瘩）"); return; }
        new GameObject("GasCystShots").AddComponent<Runner>();
    }

    class Runner : MonoBehaviour
    {
        IEnumerator Start()
        {
            Directory.CreateDirectory("Temp/GasCyst");
            var gm = GameManager.I; gm.autoWaves = false;
            foreach (var x in EnemyAI.All.ToArray()) if (x) Destroy(x.gameObject);
            GasCyst cyst = null;
            foreach (var f in CombatFurniture.All) if (f is GasCyst g) { cyst = g; break; }
            if (!cyst) { Destroy(gameObject); yield break; }
            var p = cyst.transform.position;
            gm.player.Teleport(p + new Vector3(-2.5f, 0, -1.5f));
            yield return new WaitForSecondsRealtime(0.4f);
            Shot(p, "0_idle");
            var e = gm.SpawnEnemy(EnemyKind.Beast, p + new Vector3(2.5f, 0, 0.6f));
            e.speed = 0;
            yield return new WaitForSecondsRealtime(0.6f);
            e.Teleport(p + new Vector3(0.4f, 0, 0.2f));
            yield return new WaitForSecondsRealtime(cyst.fuse * 0.75f);
            Shot(p, "1_fusing");
            yield return new WaitForSecondsRealtime(cyst.fuse * 0.25f + 0.08f);
            Shot(p, "2_explode");
            yield return new WaitForSecondsRealtime(0.25f);
            Shot(p, "3_after");
            Debug.Log("[GasCyst] shots → Temp/GasCyst");
            Destroy(gameObject);
        }

        static void Shot(Vector3 p, string name)
        {
            var main = Camera.main;
            var cam = new GameObject("cam").AddComponent<Camera>();
            cam.CopyFrom(main);
            cam.orthographic = true; cam.orthographicSize = 3.2f;
            cam.transform.position = p + Vector3.up * 1f - main.transform.forward * 40f;
            var rt = new RenderTexture(640, 640, 24); cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(640, 640, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes($"Temp/GasCyst/{name}.png", tex.EncodeToPNG());
            Object.Destroy(cam.gameObject); rt.Release();
        }
    }
}
