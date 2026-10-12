using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    // 鱿鱼愤怒表情（EnemyAI 攻击 / 抓取期间 angry = true）：大眼睛被压下来的眼皮盖住一半，瞳孔和脸上的小眼点发红光，头上冒青筋。
    // 贴图在 Resources/Squid/Face（make_face_sprites.py 生成），按 PSB 文档坐标挂到「身体根」骨上；渐显渐隐。
    public class SquidFace : MonoBehaviour
    {
        public bool angry;
        public void SetAngry(bool isAngry) => angry = isAngry;
        public bool forceAngry;                 // 表情调整窗口用
        float w;
        readonly List<SpriteRenderer> items = new List<SpriteRenderer>();
        SpriteRenderer vein, halo, dotsHalo;
        SquidAnimator anim;

        void Start()
        {
            anim = GetComponent<SquidAnimator>();
            SpriteRenderer refLayer = null; Transform body = null;
            int maxOrder = int.MinValue;
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
            {
                maxOrder = Mathf.Max(maxOrder, r.sortingOrder);
                // 「图层 1」= 身体（PSB 文档里 x 905–2495、y 186–3202），导入时被裁边并缩小到 1/4
                if (r.name == "图层 1" && r.sprite) refLayer = r;
            }
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "身体根" && !t.GetComponent<UnityEngine.U2D.Animation.SpriteSkin>()) body = t;
            if (!refLayer || !body) { Debug.LogWarning("[SquidFace] 找不到「图层 1」或身体根"); enabled = false; return; }

            foreach (var e in FaceOverlay.Get().squid)
            {
                int order = maxOrder + (e.name == "sq_lid" ? 1 : e.name.EndsWith("halo") ? 2 : e.name == "sq_vein" ? 4 : 3);
                var sr = FaceOverlay.Create("squid", e, "Squid/Face", refLayer, new Vector2(905f, 186f), 1590f, body, order);
                if (!sr) continue;
                sr.sharedMaterial = refLayer.sharedMaterial;    // 同一个材质，受击闪白 / 红光预警才会一起变色
                sr.gameObject.SetActive(true);
                items.Add(sr);
                if (e.name == "sq_vein") vein = sr;
                if (e.name == "sq_glow_halo") halo = sr;
                if (e.name == "sq_dots_halo") dotsHalo = sr;
            }
            var enemy = GetComponentInParent<EnemyController>();
            if (enemy != null)
            {
                enemy.RegisterExtraRenderers(items);
            }
            Apply(0f, 0f);
        }

        void LateUpdate()
        {
            if (anim && anim.IsDead)
            {   // 死亡时 SquidAnimator 会淡出所有图层（含这些贴图），这里不再改颜色
                enabled = false;
                return;
            }
            w = Mathf.MoveTowards(w, angry || forceAngry ? 1f : 0f, Time.deltaTime / 0.15f);
            Apply(w, Time.time);
        }

        void Apply(float k, float now)
        {
            foreach (var r in items)
            {
                var c = r.color; c.a = k; r.color = c;
                if (r.gameObject.activeSelf != k > 0.001f) r.gameObject.SetActive(k > 0.001f);
            }
            if (vein && !FaceItem.Editing) vein.transform.localScale = vein.GetComponent<FaceItem>().RestScale * (1f + 0.2f * Mathf.Abs(Mathf.Sin(now * 8f)));
            float flick = 0.75f + 0.25f * Mathf.Sin(now * 13f);
            if (halo) { var c = halo.color; c.a = k * flick; halo.color = c; }
            if (dotsHalo) { var c = dotsHalo.color; c.a = k * flick; dotsHalo.color = c; }
        }
    }
}
