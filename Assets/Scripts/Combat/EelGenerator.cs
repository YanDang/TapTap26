using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// 电鳗电机发电机组（furn_eel_generator，电系正式家具）：砗磲壳里的放电肌不停充电，充满后只要 range 内有敌人
    /// 就放一次电涌——范围内所有敌人受 damage 点伤害、麻痹 paralyzeSeconds 秒；身上有水渍（WaterTower 淋湿）的敌人
    /// 多受 wetBonus 点。放完电重新充能 chargeSeconds 秒。
    /// 怪物会把它当目标（打它的怪正好在放电范围里）。血量 maxHp，坏掉后 respawnSeconds 秒重生。不伤主角。
    /// 贴图借原型材料图标「砗磲绝缘厚壳」，电弧借烂泥珊瑚的程序电弧（染黄）。
    /// </summary>
    public class EelGenerator : CombatFurniture
    {
        public float range = 2.2f;
        public int damage = 1;
        public int wetBonus = 1;
        public float paralyzeSeconds = 1f;
        public float chargeSeconds = 3f;

        float _charge, _flashT, _seed;
        SpriteRenderer _arc;
        public int discharges;
        public bool Charged => _charge >= chargeSeconds;
        static readonly Color ArcColor = new Color(1f, 0.92f, 0.35f);

        public static EelGenerator Build(Vector3 pos, Material mat)
        {
            var root = new GameObject("EelGenerator");
            root.transform.position = DirUtil.Flat(pos);
            var g = root.AddComponent<EelGenerator>();
            g.maxHp = g.hp = 15;
            g.respawnSeconds = 15f;
            g.hitRadius = 0.55f;
            g.BuildVisual("eel_generator", 1.0f, mat, g.range, new Color(1f, 0.9f, 0.3f, 0.3f));
            g._arc = g.GroundSprite("Arc", "Furniture/mud_spark", g.range * 2f, ArcColor, mat, 0.05f);
            g._arc.enabled = false;
            return g;
        }

        protected override void Awake() { base.Awake(); _seed = Random.value * 10f; }

        /// <summary>立刻充满（自测用）。</summary>
        public void ChargeNow() { _charge = chargeSeconds; }

        protected override void Respawn() { base.Respawn(); _charge = 0f; }

        protected override void Update()
        {
            base.Update();
            if (!Alive) { if (_arc) _arc.enabled = false; return; }
            float dt = Time.deltaTime, t = Time.time + _seed;
            _charge = Mathf.Min(chargeSeconds, _charge + dt);
            _flashT = Mathf.MoveTowards(_flashT, 0f, dt * 4f);

            // 充能越满抖得越厉害；放电瞬间地上的电弧闪一下
            float k = _charge / chargeSeconds;
            float buzz = Mathf.Sin(t * 60f) * 0.03f * k * k;
            SetScale(1f + buzz + _flashT * 0.15f, 1f - buzz + _flashT * 0.1f);
            if (ring) ring.color = new Color(1f, 0.9f, 0.3f, Charged ? 0.35f + 0.1f * Mathf.Sin(t * 8f) : 0.12f);
            if (_arc)
            {
                _arc.enabled = _flashT > 0f && Mathf.PerlinNoise(t * 20f, 1.7f) > 0.3f;
                _arc.transform.rotation = Quaternion.Euler(90, Mathf.Floor(t * 12f) * 53f, 0);
                _arc.color = new Color(ArcColor.r, ArcColor.g, ArcColor.b, _flashT);
            }

            if (!Charged) return;
            var p = Pos;
            bool any = false;
            foreach (var e in EnemyAI.All) if (e && !e.Dead && DirUtil.Flat(e.Pos - p).magnitude <= range) { any = true; break; }
            if (any) Discharge();
        }

        void Discharge()
        {
            _charge = 0f; _flashT = 1f;
            discharges++;
            var p = Pos;
            foreach (var e in EnemyAI.All.ToArray())
            {
                if (!e || e.Dead) continue;
                var v = DirUtil.Flat(e.Pos - p);
                if (v.magnitude > range) continue;
                e.TakeDamage(damage + (e.IsWet ? wetBonus : 0), v, 0f, true);
                if (!e.Dead) e.Stun(paralyzeSeconds);
                Vfx.Spawn("VFX_ParrySpark", e.Pos + Vector3.up * 0.8f, Random.Range(0f, 360f), 1.1f, false, null, ArcColor);
            }
            CameraRig.I?.Shake(0.15f, 0.15f);
            Sfx.Play("sfx_parry", 0.8f);
        }
    }
}
