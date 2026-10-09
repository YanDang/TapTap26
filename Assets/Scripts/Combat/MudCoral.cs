using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// 漏电的烂泥珊瑚块（scrap_mud_coral）：【导电陷阱】地上铺开一片带电泥潭（半径 poolRadius），
    /// 踩进去的敌人持续减速（slowFactor），每 shockInterval 秒被电一下（shockDamage，受击硬直 + 青色电火花）。
    /// 是地面陷阱，怪物不会把它当攻击目标，也不会坏。不影响主角。
    /// 中间那块珊瑚借原型材料图标「多孔空心珊瑚」，泥潭和电弧是程序画的（Resources/Furniture/mud_*.png）。
    /// </summary>
    public class MudCoral : CombatFurniture
    {
        public float poolRadius = 2.4f;
        [Range(0.1f, 1f)] public float slowFactor = 0.55f;   // 速度乘以它（0.55 = 减速 45%）
        public float shockInterval = 1f;
        public int shockDamage = 1;

        SpriteRenderer _pool, _spark;
        readonly Dictionary<EnemyAI, float> _nextShock = new Dictionary<EnemyAI, float>();
        public int shocks;
        public override bool Targetable => false;

        public static MudCoral Build(Vector3 pos, Material mat)
        {
            var root = new GameObject("MudCoral");
            root.transform.position = DirUtil.Flat(pos);
            var m = root.AddComponent<MudCoral>();
            m.respawnSeconds = 0f;
            m._pool = m.GroundSprite("Pool", "Furniture/mud_pool", m.poolRadius * 2f, Color.white, mat, 0.035f);
            m._spark = m.GroundSprite("Spark", "Furniture/mud_spark", m.poolRadius * 1.9f, Color.white, mat, 0.05f);
            m.BuildVisual("mud_coral", 0.6f, mat, 0f, Color.clear);
            return m;
        }

        protected override void Update()
        {
            base.Update();
            float t = Time.time;
            // 电弧随机闪烁、转动
            if (_spark)
            {
                bool on = Mathf.PerlinNoise(t * 7f, 3.3f) > 0.45f;
                _spark.enabled = on;
                _spark.transform.rotation = Quaternion.Euler(90, Mathf.Floor(t * 6f) * 47f, 0);
            }
            if (_pool) _pool.color = Color.Lerp(Color.white, new Color(0.75f, 0.95f, 1f), 0.5f + 0.5f * Mathf.Sin(t * 5f));

            var p = Pos;
            foreach (var e in EnemyAI.All.ToArray())
            {
                if (!e || e.Dead) continue;
                if (DirUtil.Flat(e.Pos - p).magnitude > poolRadius) { _nextShock.Remove(e); continue; }
                e.Slow(slowFactor, 0.2f);
                if (!_nextShock.TryGetValue(e, out var next)) { next = t + 0.25f; _nextShock[e] = next; }
                if (t >= next)
                {
                    _nextShock[e] = t + shockInterval;
                    shocks++;
                    e.TakeDamage(shockDamage, Vector3.zero, 0f, false);
                    Vfx.Spawn("VFX_ParrySpark", e.Pos + Vector3.up * 0.8f, Random.Range(0f, 360f), 0.9f, false, null, new Color(0.55f, 0.9f, 1f));
                    Sfx.Play("sfx_parry", 0.35f);
                }
            }
        }
    }
}
