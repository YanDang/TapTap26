using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// 活性共生杂交残渣（scrap_symbiotic_sludge）：【活体路障】有碰撞体，会挡住怪物（和主角）的路；
    /// 每 regenInterval 秒自愈 1 点；被打坏后分裂成 3 块小碎块散在周围，碎块范围内的敌人减速 slowFactor，
    /// chunkSeconds 秒后碎块干掉消失。respawnSeconds 秒后原地长回来。贴图借原型材料图标「荧光共生菌群」。
    /// </summary>
    public class SymbioticSludge : CombatFurniture
    {
        public float regenInterval = 2f;
        [Range(0.1f, 1f)] public float slowFactor = 0.6f;    // 碎块减速：速度乘以它（0.6 = 减速 40%）
        public float chunkRadius = 0.8f, chunkSeconds = 10f;

        CapsuleCollider _col;
        float _regenT, _seed;
        readonly List<Chunk> _chunks = new List<Chunk>();
        public int ChunkCount => _chunks.Count;
        public override bool Blocks => Alive;

        class Chunk { public Transform t; public SpriteRenderer sr; public float life; }

        public static SymbioticSludge Build(Vector3 pos, Material mat)
        {
            var root = new GameObject("SymbioticSludge");
            root.transform.position = DirUtil.Flat(pos);
            var s = root.AddComponent<SymbioticSludge>();
            s.maxHp = s.hp = 16;
            s.respawnSeconds = 15f;
            s.hitRadius = 0.7f;
            s._col = root.AddComponent<CapsuleCollider>();
            s._col.radius = 0.55f; s._col.height = 1.2f; s._col.center = new Vector3(0, 0.6f, 0);
            s.BuildVisual("symbiotic_sludge", 1.0f, mat, 0f, Color.clear);
            return s;
        }

        protected override void Awake() { base.Awake(); _seed = Random.value * 10f; }

        protected override void Break()
        {
            base.Break();
            _col.enabled = false;
            // 分裂成 3 块减速碎块
            for (int i = 0; i < 3; i++)
            {
                float a = (i / 3f + Random.Range(-0.08f, 0.08f)) * Mathf.PI * 2f;
                var c = new GameObject("SludgeChunk").transform;
                c.position = Pos + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 1.1f;
                var bb = new GameObject("Visual");
                bb.transform.SetParent(c, false);
                var b = bb.AddComponent<Billboard>(); b.anchor = c; b.towardCamera = 1.2f;
                var spr = new GameObject("Sprite").AddComponent<SpriteRenderer>();
                spr.transform.SetParent(bb.transform, false);
                spr.sprite = sr ? sr.sprite : null;
                spr.sharedMaterial = sr ? sr.sharedMaterial : null;
                if (spr.sprite)
                {
                    float k = 0.42f / spr.sprite.bounds.size.y;
                    spr.transform.localScale = Vector3.one * k;
                    spr.transform.localPosition = new Vector3(0, 0.21f, 0);
                }
                _chunks.Add(new Chunk { t = c, sr = spr, life = chunkSeconds });
            }
            Vfx.Spawn("VFX_ArrowHit", Pos + Vector3.up * 0.4f, 0, 1.4f, false, null, new Color(0.6f, 1f, 0.5f));
            Sfx.Play("sfx_table_land", 0.8f);
        }

        protected override void Respawn() { base.Respawn(); _col.enabled = true; }

        protected override void Update()
        {
            base.Update();
            float dt = Time.deltaTime, t = Time.time + _seed;
            if (Alive)
            {
                // 蠕动；血少时缩小一点
                float life = 0.75f + 0.25f * hp / (float)maxHp;
                float w = 1f + Mathf.Sin(t * 3.1f) * 0.05f;
                SetScale(life * w, life / w);
                _regenT += dt;
                if (_regenT >= regenInterval) { _regenT = 0f; if (hp < maxHp) hp++; }
            }
            // 碎块：减速范围内的敌人，到时间缩小消失
            for (int i = _chunks.Count - 1; i >= 0; i--)
            {
                var c = _chunks[i];
                c.life -= dt;
                if (c.life <= 0f) { Destroy(c.t.gameObject); _chunks.RemoveAt(i); continue; }
                float k = Mathf.Clamp01(c.life / 0.5f);
                c.t.localScale = Vector3.one * k;
                foreach (var e in EnemyAI.All)
                    if (e && !e.Dead && DirUtil.Flat(e.Pos - c.t.position).magnitude <= chunkRadius) e.Slow(slowFactor, 0.2f);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            foreach (var c in _chunks) if (c.t) Destroy(c.t.gameObject);
        }
    }
}
