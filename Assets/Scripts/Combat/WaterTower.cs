using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// 脉动造水循环塔（furn_water_tower，水系正式家具）：心室有节律地泵水，每 pulseSeconds 秒喷淋一次，
    /// 周围 radius 米铺着一片水渍：
    /// · 水渍里的敌人减速（slowFactor），并被淋湿 wetSeconds 秒（湿身的敌人挨电系家具的电多受伤害，见 EelGenerator）。
    /// · 水汽范围内的增生珊瑚墙自愈快一倍（CoralWall.Moist）。
    /// 怪物会把它当目标。血量 maxHp，坏掉后水渍干掉、respawnSeconds 秒重生。不影响主角。
    /// 贴图借原型材料图标「脉动造水心室」，水渍是程序画的（Resources/Furniture/water_puddle.png）。
    /// </summary>
    public class WaterTower : CombatFurniture
    {
        public float radius = 3.2f;
        [Range(0.1f, 1f)] public float slowFactor = 0.55f;   // 速度乘以它（0.55 = 减速 45%）
        public float wetSeconds = 3f;
        public float pulseSeconds = 1.6f;

        SpriteRenderer _puddle;
        SpriteRenderer[] _waves;
        float _pulse, _beat, _seed;
        public int pulses;

        /// <summary>p 在不在某个水塔的水汽范围内。</summary>
        public static bool Moist(Vector3 p)
        {
            foreach (var f in All)
                if (f is WaterTower w && w.Alive && DirUtil.Flat(w.Pos - p).magnitude <= w.radius) return true;
            return false;
        }

        public static WaterTower Build(Vector3 pos, Material mat)
        {
            var root = new GameObject("WaterTower");
            root.transform.position = DirUtil.Flat(pos);
            var w = root.AddComponent<WaterTower>();
            w.maxHp = w.hp = 14;
            w.respawnSeconds = 15f;
            w.hitRadius = 0.5f;
            w._puddle = w.GroundSprite("Puddle", "Furniture/water_puddle", w.radius * 2f, Color.white, mat, 0.035f);
            w._waves = new SpriteRenderer[2];
            for (int i = 0; i < 2; i++) w._waves[i] = w.GroundSprite("Wave" + i, "UI/ui_ring", 1f, Color.clear, mat, 0.045f + i * 0.002f);
            w.BuildVisual("water_tower", 1.1f, mat, 0f, Color.clear);
            return w;
        }

        protected override void Awake() { base.Awake(); _seed = Random.value * 10f; }

        protected override void Break()
        {
            base.Break();
            if (_puddle) _puddle.enabled = false;
            foreach (var r in _waves) if (r) r.enabled = false;
            Vfx.Spawn("VFX_ArrowBurst", Pos + Vector3.up * 0.6f, 0, 1.2f, false, null, new Color(0.6f, 0.85f, 1f));
            Sfx.Play("sfx_explosion", 0.4f);
        }

        protected override void Respawn()
        {
            base.Respawn();
            if (_puddle) _puddle.enabled = true;
        }

        protected override void Update()
        {
            base.Update();
            if (!Alive) return;
            float dt = Time.deltaTime, t = Time.time + _seed;

            // 心跳：每次泵水前猛地一缩、再鼓起来；泵水时喷一口水花、地上扩两圈涟漪
            _pulse += dt;
            if (_pulse >= pulseSeconds)
            {
                _pulse -= pulseSeconds;
                _beat = 1f;
                pulses++;
                Vfx.Spawn("VFX_ParrySpark", Pos + Vector3.up * 1.1f, Random.Range(0f, 360f), 1f, false, null, new Color(0.6f, 0.85f, 1f));
                Sfx.Play("sfx_dodge", 0.25f);
            }
            _beat = Mathf.MoveTowards(_beat, 0f, dt * 2.5f);
            float squeeze = Mathf.Sin(_beat * Mathf.PI) * 0.12f;
            SetScale(1f + squeeze, 1f - squeeze * 0.6f);
            for (int i = 0; i < _waves.Length; i++)
            {
                float k = Mathf.Repeat(_pulse / pulseSeconds + i * 0.5f, 1f);
                var w = _waves[i];
                w.enabled = true;
                float d = Mathf.Lerp(0.8f, radius * 2f, k);
                if (w.sprite) w.transform.localScale = Vector3.one * (d / w.sprite.bounds.size.x);
                w.color = new Color(0.75f, 0.92f, 1f, 0.5f * (1f - k));
            }
            if (_puddle) _puddle.color = Color.Lerp(Color.white, new Color(0.85f, 0.95f, 1f), 0.5f + 0.5f * Mathf.Sin(t * 2f));

            var p = Pos;
            foreach (var e in EnemyAI.All)
            {
                if (!e || e.Dead || DirUtil.Flat(e.Pos - p).magnitude > radius) continue;
                e.Slow(slowFactor, 0.2f);
                e.Wet(wetSeconds);
            }
        }
    }
}
