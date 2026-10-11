using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// 增生珊瑚管排墙（furn_coral_wall，木系正式家具）：一段 length 米长的掩体墙，有碰撞体，挡住怪物（和主角）。
    /// · 自愈：每 regenInterval 秒回 1 点；在水汽里（WaterTower 的喷淋范围内）自愈快一倍（原型：“在水汽中不断增生自愈”）。
    /// · 荆棘反伤：只有敌人攻击墙时才反伤——打墙的那只怪受 thorns 点伤害（用户 2026-10-11）。
    /// 怪物追玩家被墙挡住时会转头打墙（EnemyAI.BlockedBy）。玩家打不到它。坏掉后 respawnSeconds 秒原地长回来。
    /// 外观：沿墙排 3 棵珊瑚（贴图借原型材料图标「增生珊瑚骨板」）。
    /// </summary>
    public class CoralWall : CombatFurniture
    {
        public float length = 2.6f;
        public int thorns = 1;
        public float regenInterval = 4f;

        BoxCollider _col;
        float _regenT, _seed;
        readonly List<Transform> _extraHolders = new List<Transform>();
        readonly List<SpriteRenderer> _extra = new List<SpriteRenderer>();
        public int thornsDealt;
        public override bool Blocks => Alive;
        public bool Moist => WaterTower.Moist(Pos);

        public Vector3 Axis => transform.right;
        public float Yaw => transform.eulerAngles.y;
        /// <summary>墙的两头（s = ±1）。新墙可以接在这里（BuildController 吸附）。</summary>
        public Vector3 End(float s) => Pos + Axis * (s * length * 0.5f);

        /// <summary>墙上离 from 最近的点（怪物从侧面过来打的是离它近的那一段）。</summary>
        public override Vector3 AimPoint(Vector3 from)
        {
            var d = DirUtil.Flat(from) - Pos;
            float t = Mathf.Clamp(Vector3.Dot(d, Axis), -length * 0.5f, length * 0.5f);
            return Pos + Axis * t;
        }

        /// <summary>yaw = 墙的走向（0 = 沿世界 X 轴）。</summary>
        public static CoralWall Build(Vector3 pos, float yaw, Material mat)
        {
            var root = new GameObject("CoralWall");
            root.transform.SetPositionAndRotation(DirUtil.Flat(pos), Quaternion.Euler(0, yaw, 0));
            var w = root.AddComponent<CoralWall>();
            w.maxHp = w.hp = 20;
            w.respawnSeconds = 18f;
            w.hitRadius = 0.45f;
            w._col = root.AddComponent<BoxCollider>();
            w._col.size = new Vector3(w.length, 1.4f, 0.6f);
            w._col.center = new Vector3(0, 0.7f, 0);
            w.BuildVisual("coral_wall", 1.15f, mat, 0f, Color.clear);              // 中间那棵（受击闪红用它）
            foreach (float s in new[] { -1f, 1f })
            {   // 两头各一棵，矮一点
                var anchor = new GameObject("Seg").transform;
                anchor.SetParent(root.transform, false);
                anchor.localPosition = new Vector3(s * w.length * 0.4f, 0, 0);       // 靠近两头：几段墙接起来看着是连着的
                var holder = new GameObject("Visual").transform;
                holder.SetParent(root.transform, false);
                var bb = holder.gameObject.AddComponent<Billboard>();
                bb.anchor = anchor; bb.towardCamera = 1.5f;
                var r = new GameObject("Sprite").AddComponent<SpriteRenderer>();
                r.transform.SetParent(holder, false);
                r.sprite = w.sr ? w.sr.sprite : null;
                if (mat) r.sharedMaterial = mat;
                if (r.sprite)
                {
                    float h = 0.9f, k = h / r.sprite.bounds.size.y;
                    r.transform.localScale = new Vector3(s * k, k, 1f);              // 右边那棵镜像，不显得是复制的
                    r.transform.localPosition = new Vector3(0, h * 0.5f, 0);
                }
                w._extraHolders.Add(holder);
                w._extra.Add(r);
            }
            return w;
        }

        protected override void Awake() { base.Awake(); _seed = Random.value * 10f; }

        public override void HitByEnemy(EnemyAI e, int dmg)
        {
            if (!Alive) return;
            base.HitByEnemy(e, dmg);
            if (e && !e.Dead && thorns > 0)
            {   // 荆棘反伤：只反给打墙的这只
                thornsDealt++;
                e.TakeDamage(thorns, e.Pos - AimPoint(e.Pos), 0.15f, false);
                Vfx.Spawn("VFX_ParrySpark", AimPoint(e.Pos) + Vector3.up * 0.7f, Random.Range(0f, 360f), 0.9f, false, null, new Color(1f, 0.6f, 0.75f));
            }
        }

        protected override void Break()
        {
            base.Break();
            _col.enabled = false;
            foreach (var h in _extraHolders) h.gameObject.SetActive(false);
            Vfx.Spawn("VFX_ArrowHit", Pos + Vector3.up * 0.5f, 0, 1.6f, false, null, new Color(1f, 0.7f, 0.8f));
            Sfx.Play("sfx_table_land", 0.9f);
        }

        protected override void Respawn()
        {
            base.Respawn();
            _col.enabled = true;
            foreach (var h in _extraHolders) h.gameObject.SetActive(true);
        }

        protected override void Update()
        {
            base.Update();
            if (!Alive) return;
            float dt = Time.deltaTime, t = Time.time + _seed;
            // 血少时缩小一点，平时微微起伏（在长）；水汽里长得快
            float life = 0.8f + 0.2f * hp / (float)maxHp;
            float w = 1f + Mathf.Sin(t * 1.7f) * 0.025f;
            SetScale(life * w, life / w);
            foreach (var r in _extra) if (r && sr) r.color = sr.color;
            bool moist = Moist;
            _regenT += dt * (moist ? 2f : 1f);
            if (_regenT >= regenInterval) { _regenT = 0f; if (hp < maxHp) hp++; }
        }
    }
}
