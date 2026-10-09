using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// 膨胀的气囊疙瘩（家具合成原型 Mix_v02 的杂交废料 scrap_gas_cyst，用户 2026-10-08 改：敌人进入碰撞范围就触发）。
    /// 平时缓慢鼓动；敌人走进 triggerRadius（或怪物打它一下）→ 迅速鼓胀发红（fuse 秒）→ 爆炸，
    /// blastRadius 内的敌人受伤、击退、眩晕。不伤主角。炸完 respawnSeconds 秒后在原地重新长出来。
    /// 贴图暂借原型里的材料图标「紫斑弹性吸盘皮」（Resources/Furniture/gas_cyst.png）。
    /// </summary>
    public class GasCyst : CombatFurniture
    {
        public float triggerRadius = 0.9f;     // 敌人进入这个范围就触发（“碰撞体”）
        public float fuse = 0.35f;             // 触发后鼓胀多久才炸
        public float blastRadius = 2.6f;
        public int damage = 4;
        public float knock = 0.9f, stunSeconds = 1.2f;

        bool _fusing;
        float _t, _seed;
        public int explosions;
        public bool IsIdle => Alive && !_fusing;
        public override bool Targetable => IsIdle;

        public static GasCyst Build(Vector3 pos, Material spriteMat)
        {
            var root = new GameObject("GasCyst");
            root.transform.position = DirUtil.Flat(pos);
            var c = root.AddComponent<GasCyst>();
            c.respawnSeconds = 10f;
            c.hitRadius = 0.5f;
            c.BuildVisual("gas_cyst", 0.9f, spriteMat, c.triggerRadius, new Color(0.8f, 0.45f, 1f, 0.35f));
            return c;
        }

        protected override void Awake() { base.Awake(); _seed = Random.value * 10f; }

        /// <summary>立即触发（自测 / 被怪物打 / 以后被别的东西引爆用）。</summary>
        public void Trigger()
        {
            if (!IsIdle) return;
            _fusing = true; _t = 0f;
            Sfx.Play("sfx_enemy_windup", 0.7f);
        }

        // 怪物打它 = 直接引爆
        public override void HitByEnemy(EnemyAI e, int dmg) { timesHit++; Trigger(); }

        protected override void Update()
        {
            base.Update();
            if (!Alive) return;
            if (!_fusing)
            {
                float b = 1f + 0.05f * Mathf.Sin((Time.time + _seed) * 2.2f);   // 缓慢呼吸鼓动
                SetScale(b, 1f / Mathf.Sqrt(b));
                sr.color = Color.white;
                var p = Pos;
                foreach (var e in EnemyAI.All)
                    if (e && !e.Dead && DirUtil.Flat(e.Pos - p).magnitude <= triggerRadius) { Trigger(); break; }
                return;
            }
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / fuse);
            float swell = 1f + 0.45f * k + 0.08f * Mathf.Sin(_t * 60f) * k;   // 越鼓越大、快速颤抖
            SetScale(swell, swell);
            sr.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.4f), k);
            if (_t >= fuse) Explode();
        }

        void Explode()
        {
            explosions++;
            _fusing = false;
            var p = DirUtil.Flat(Pos);
            foreach (var e in EnemyAI.All.ToArray())
            {
                if (!e || e.Dead) continue;
                var v = DirUtil.Flat(e.Pos - p);
                if (v.magnitude <= blastRadius)
                {
                    e.TakeDamage(damage, v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward, knock, true);
                    if (!e.Dead) e.Stun(stunSeconds);
                }
            }
            CameraRig.I?.Shake(0.55f, 0.4f);
            Sfx.Play("sfx_explosion");
            var tint = new Color(0.85f, 0.55f, 1f);
            Vfx.Spawn("VFX_ArrowHit", p + Vector3.up * 0.4f, 0, blastRadius * 0.75f, false, null, tint);
            Vfx.Spawn("VFX_ArrowBurst", p + Vector3.up * 0.6f, 0, blastRadius * 0.5f, false, null, tint);
            Break();
        }
    }
}
