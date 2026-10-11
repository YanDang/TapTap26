using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// 小型生体驯化培养箱（furn_bio_incubator，水系正式家具）：散发淡蓝荧光，安抚近旁的生体怪物。
    /// · lureRange 内的怪（没在攻击、没被控制时）每秒积累 pacifyRate 的“安抚值”，满 1 → 被安抚 pacifySeconds 秒：
    ///   不攻击任何东西，慢慢走到培养箱旁发呆（身上淡蓝光）。挨打会立刻醒（EnemyAI.TakeDamage）。
    /// · 安抚结束后这只怪 immuneSeconds 秒内不会再被安抚（不然会永远被锁住）。离开范围安抚值慢慢回落。
    /// 怪物会把它当目标——来打它的怪正好被安抚。血量 maxHp，坏掉后 respawnSeconds 秒重生。不影响主角。
    /// 贴图借原型材料图标「破损逻辑指令片」，地上的荧光圈是 lureRange。
    /// </summary>
    public class BioIncubator : CombatFurniture
    {
        public float lureRange = 4.5f;
        public float pacifyRate = 0.35f;
        public float pacifySeconds = 5f;
        public float immuneSeconds = 4f;

        readonly Dictionary<EnemyAI, float> _calm = new Dictionary<EnemyAI, float>();
        readonly Dictionary<EnemyAI, float> _immuneUntil = new Dictionary<EnemyAI, float>();
        float _seed;
        public int pacified;
        public static readonly Color Glow = new Color(0.45f, 0.85f, 1f);

        /// <summary>某只怪当前的安抚值（0–1，自测 / 调试用）。</summary>
        public float CalmOf(EnemyAI e) => _calm.TryGetValue(e, out var c) ? c : 0f;

        public static BioIncubator Build(Vector3 pos, Material mat)
        {
            var root = new GameObject("BioIncubator");
            root.transform.position = DirUtil.Flat(pos);
            var b = root.AddComponent<BioIncubator>();
            b.maxHp = b.hp = 18;
            b.respawnSeconds = 15f;
            b.hitRadius = 0.45f;
            b.BuildVisual("bio_incubator", 0.95f, mat, b.lureRange, new Color(Glow.r, Glow.g, Glow.b, 0.25f));
            return b;
        }

        protected override void Awake() { base.Awake(); _seed = Random.value * 10f; }

        protected override void Update()
        {
            base.Update();
            if (!Alive) { _calm.Clear(); return; }
            float dt = Time.deltaTime, t = Time.time + _seed;
            SetScale(1f, 1f + Mathf.Sin(t * 1.3f) * 0.03f);
            if (ring) ring.color = new Color(Glow.r, Glow.g, Glow.b, 0.18f + 0.1f * Mathf.Sin(t * 2.2f));

            var p = Pos;
            foreach (var e in EnemyAI.All.ToArray())
            {
                if (!e || e.Dead) { if (e) _calm.Remove(e); continue; }
                _calm.TryGetValue(e, out float c);
                bool inRange = DirUtil.Flat(e.Pos - p).magnitude <= lureRange;
                bool immune = _immuneUntil.TryGetValue(e, out var until) && Time.time < until;
                if (inRange && !immune && e.CanBePacified)
                {
                    c += pacifyRate * dt;
                    if (c >= 1f)
                    {
                        c = 0f;
                        pacified++;
                        e.Pacify(pacifySeconds, this);
                        _immuneUntil[e] = Time.time + pacifySeconds + immuneSeconds;
                        Vfx.Spawn("VFX_ParrySpark", e.Pos + Vector3.up * 1.6f, 0, 1.1f, false, null, Glow);
                        Sfx.Play("sfx_parry", 0.3f);
                    }
                }
                else if (!e.Pacified) c = Mathf.Max(0f, c - dt * 0.2f);
                _calm[e] = c;
            }
        }
    }
}
