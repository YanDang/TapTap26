using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// 气泡囊（原料「气虫储气泡囊」）：存气，越充越鼓。
    /// · 扩大负重：充到 fullThreshold 以上的气泡囊，每个让物品栏负重上限 +Inventory.capacityPerSac。
    /// · 吸引怪物：储气超过 tauntThreshold 后开始嘲讽，储气越多范围越大（地上有一圈淡紫色范围提示）。
    /// · 没有伤害；被打破时气爆，储气越多弹得越远。
    /// </summary>
    public class GasSac : GasNode
    {
        public float fullThreshold = 0.8f;
        public float tauntThreshold = 0.3f;
        public float tauntMin = 2.5f, tauntMax = 7f;

        float _seed;

        public bool IsFull => Alive && Fill >= fullThreshold;
        public override float TauntRange => Alive && Fill >= tauntThreshold ? Mathf.Lerp(tauntMin, tauntMax, Fill) : 0f;
        protected override float BurstRadius => 1.4f + 2f * Fill;
        protected override float BurstDistance => 1.5f + 2.5f * Fill;

        /// <summary>场景里充满的气泡囊个数（物品栏负重用）。</summary>
        public static int FullCount
        {
            get { int n = 0; foreach (var f in All) if (f is GasSac s && s.IsFull) n++; return n; }
        }

        public static GasSac Build(Vector3 pos, Material mat)
        {
            var root = new GameObject("GasSac");
            root.transform.position = DirUtil.Flat(pos);
            var s = root.AddComponent<GasSac>();
            s.maxHp = s.hp = 12;
            s.capacity = 20f;
            s.hitRadius = 0.6f;
            s.BuildVisual("gas_sac", 0.85f, mat, 1f, new Color(0.8f, 0.6f, 1f, 0f));
            GasNetwork.Dirty();
            return s;
        }

        protected override void Awake() { base.Awake(); _seed = Random.value * 10f; }

        protected override void Update()
        {
            base.Update();
            if (!Alive) return;
            float t = Time.time + _seed;
            // 越充越鼓，满了以后微微发颤
            float swell = 0.75f + 0.55f * Fill;
            float breathe = 0.03f * Mathf.Sin(t * 2.5f) + (Fill >= fullThreshold ? 0.02f * Mathf.Sin(t * 30f) : 0f);
            SetScale(swell + breathe, swell - breathe * 0.5f);
            // 嘲讽范围圈
            if (ring)
            {
                float r = TauntRange;
                ring.enabled = r > 0f;
                if (r > 0f && ring.sprite)
                {
                    ring.transform.localScale = Vector3.one * (r * 2f / ring.sprite.bounds.size.x);
                    ring.color = new Color(0.8f, 0.6f, 1f, 0.18f + 0.1f * Mathf.Sin(t * 4f));
                }
            }
        }
    }
}
