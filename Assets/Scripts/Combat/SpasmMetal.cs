using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// 抽搐的痉挛金属团（scrap_spasm_metal）：【强力弹簧反冲】敌人踩进触发范围 → 被反向弹飞 bounceDistance 米、
    /// 带电麻痹 paralyzeSeconds 秒（眩晕星星）、受 1 点伤害；弹完金属团剧烈痉挛，rechargeSeconds 秒后才能再弹。
    /// 怪物也可以打它（血量 maxHp），打坏后 respawnSeconds 秒重生。不影响主角。
    /// 贴图暂借原型材料图标（Resources/Furniture/spasm_metal.png）。
    /// </summary>
    public class SpasmMetal : CombatFurniture
    {
        public float triggerRadius = 0.8f;
        public float bounceDistance = 3f;
        public float paralyzeSeconds = 1.2f;
        public int damage = 1;
        public float rechargeSeconds = 1.2f;

        float _cool, _spasm, _seed;
        public int bounces;

        public static SpasmMetal Build(Vector3 pos, Material mat)
        {
            var root = new GameObject("SpasmMetal");
            root.transform.position = DirUtil.Flat(pos);
            var m = root.AddComponent<SpasmMetal>();
            m.maxHp = m.hp = 10;
            m.respawnSeconds = 12f;
            m.BuildVisual("spasm_metal", 0.85f, mat, m.triggerRadius, new Color(1f, 0.9f, 0.3f, 0.35f));
            return m;
        }

        protected override void Awake() { base.Awake(); _seed = Random.value * 10f; }

        protected override void Update()
        {
            base.Update();
            if (!Alive) return;
            float dt = Time.deltaTime;
            _cool -= dt;
            _spasm = Mathf.MoveTowards(_spasm, 0f, dt * 2.5f);

            // 平时也在轻微抽搐；弹人之后剧烈痉挛
            float t = Time.time + _seed;
            float twitch = (Mathf.PerlinNoise(t * 9f, 0f) - 0.5f) * 0.06f + Mathf.Sin(t * 70f) * 0.12f * _spasm;
            SetScale(1f + twitch, 1f - twitch);
            if (spriteT) spriteT.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 55f) * 10f * _spasm);
            if (ring) ring.color = new Color(1f, 0.9f, 0.3f, _cool > 0f ? 0.12f : 0.35f);   // 充能中圈变淡

            if (_cool > 0f) return;
            var p = Pos;
            foreach (var e in EnemyAI.All.ToArray())
            {
                if (!e || e.Dead) continue;
                var v = DirUtil.Flat(e.Pos - p);
                if (v.magnitude > triggerRadius) continue;
                var dir = v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.right;
                e.TakeDamage(damage, dir, 0f, true);
                if (!e.Dead)
                {
                    e.Knockback(dir, bounceDistance);
                    e.Stun(paralyzeSeconds);
                }
                bounces++;
                _cool = rechargeSeconds; _spasm = 1f;
                CameraRig.I?.Shake(0.25f, 0.2f);
                Sfx.Play("sfx_kick", 0.9f);
                Vfx.Spawn("VFX_ParrySpark", p + Vector3.up * 0.5f, 0, 1.2f, false, null, new Color(1f, 0.95f, 0.4f));
                break;
            }
        }
    }
}
