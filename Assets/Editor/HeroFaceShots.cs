using System.Collections;
using System.IO;
using GameJam;
using UnityEditor;
using UnityEngine;

// Play 模式下依次切到 无表情 / 严肃 / 愤怒 / 痛苦 / 高兴，拍主角脸部特写到 Temp/HeroFaces/（核对表情位置用）
public static class HeroFaceShots
{
    [MenuItem("GameJam/Hero2D/Shoot Faces (Play mode)")]
    static void Shoot()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[HeroFaces] 先进入 Play 模式"); return; }
        var face = Object.FindObjectOfType<HeroFace>();
        if (!face) return;
        new GameObject("HeroFaceShots").AddComponent<Runner>().face = face;
    }

    class Runner : MonoBehaviour
    {
        public HeroFace face;
        IEnumerator Start()
        {
            Directory.CreateDirectory("Temp/HeroFaces");
            foreach (var e in EnemyAI.All.ToArray()) if (e) Destroy(e.gameObject);
            if (GameManager.I) GameManager.I.autoWaves = false;
            var blink = face.GetComponent<HeroBlink>();
            foreach (HeroFace.Expr ex in System.Enum.GetValues(typeof(HeroFace.Expr)))
            {
                face.forceOn = true; face.forced = ex;
                yield return new WaitForSecondsRealtime(0.5f);
                var main = Camera.main;
                var cam = new GameObject("cam").AddComponent<Camera>();
                cam.CopyFrom(main);
                cam.orthographic = true; cam.orthographicSize = 0.32f;
                cam.transform.position = blink.lid.transform.position + main.transform.up * 0.05f - main.transform.forward * 30f;
                var rt = new RenderTexture(500, 500, 24); cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(500, 500, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 500, 500), 0, 0); tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes($"Temp/HeroFaces/{(int)ex}_{ex}.png", tex.EncodeToPNG());
                Destroy(cam.gameObject); rt.Release();
            }
            face.forceOn = false;
            Debug.Log("[HeroFaces] done → Temp/HeroFaces");
            Destroy(gameObject);
        }
    }
}
