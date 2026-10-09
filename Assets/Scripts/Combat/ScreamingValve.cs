using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// 嘶鸣的过载排气瓣（scrap_screaming_valve）：【诱敌嘲讽】间歇性喷带电蒸汽并尖啸——尖啸 screamSeconds 秒、
    /// 歇 restSeconds 秒循环。尖啸期间 tauntRange 内的怪全部被强行吸引过来打它（就算在追玩家也会转头），
    /// 锁定后打到它坏为止（EnemyAI.ThinkTarget）。血量 maxHp，坏掉后 respawnSeconds 秒重生。
    /// 尖啸时地上有一圈圈往外扩的声波环。贴图借原型材料图标「过载泄电阀瓣」。
    /// </summary>
    public class ScreamingValve : CombatFurniture
    {
        public float tauntRange = 7f;
        public float screamSeconds = 12f, restSeconds = 6f;
        public float startDelay = 1f;            // 放下后多久第一次尖啸

        float _cycle;
        float _pulse;
        SpriteRenderer[] _waves;
        public bool Screaming { get; private set; }
        public override float TauntRange => Screaming ? tauntRange : 0f;

        public static ScreamingValve Build(Vector3 pos, Material mat)
        {
            var root = new GameObject("ScreamingValve");
            root.transform.position = DirUtil.Flat(pos);
            var v = root.AddComponent<ScreamingValve>();
            v.maxHp = v.hp = 10;
            v.respawnSeconds = 15f;
            v.BuildVisual("screaming_valve", 0.8f, mat, 0f, Color.clear);
            v._waves = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++) v._waves[i] = v.GroundSprite("Wave" + i, "UI/ui_ring", 1f, Color.clear, mat, 0.04f + i * 0.002f);
            v._cycle = -v.startDelay;
            return v;
        }

        /// <summary>立刻开始尖啸（自测用）。</summary>
        public void ScreamNow() { _cycle = 0f; }

        protected override void Respawn() { base.Respawn(); _cycle = -startDelay; }

        protected override void Break()
        {
            base.Break();
            Screaming = false;
            foreach (var w in _waves) if (w) w.enabled = false;
            Vfx.Spawn("VFX_ArrowHit", Pos + Vector3.up * 0.4f, 0, 1.2f, false, null, new Color(0.8f, 0.9f, 1f));
            Sfx.Play("sfx_explosion", 0.5f);
        }

        protected override void Update()
        {
            base.Update();
            if (!Alive) return;
            float dt = Time.deltaTime;
            _cycle += dt;
            if (_cycle >= screamSeconds + restSeconds) _cycle -= screamSeconds + restSeconds;
            bool was = Screaming;
            Screaming = _cycle >= 0f && _cycle < screamSeconds;
            if (Screaming && !was) Sfx.Play("sfx_sword_draw", 1f);

            float t = Time.time;
            if (Screaming)
            {
                // 尖啸：阀瓣快速抖动，每 1.2 秒喷一口蒸汽、发一次声
                SetScale(1f + Mathf.Sin(t * 45f) * 0.05f, 1f - Mathf.Sin(t * 45f) * 0.05f);
                _pulse += dt;
                if (_pulse >= 1.2f)
                {
                    _pulse = 0f;
                    Vfx.Spawn("VFX_ParrySpark", Pos + Vector3.up * 0.9f, Random.Range(0f, 360f), 1f, false, null, new Color(0.75f, 0.9f, 1f));
                    Sfx.Play("sfx_enemy_windup", 0.45f);
                }
                // 3 圈声波环依次往外扩，扩到嘲讽范围
                for (int i = 0; i < _waves.Length; i++)
                {
                    float k = Mathf.Repeat(t * 0.6f + i / 3f, 1f);
                    var w = _waves[i];
                    w.enabled = true;
                    float d = Mathf.Lerp(0.6f, tauntRange * 2f, k);
                    if (w.sprite) w.transform.localScale = Vector3.one * (d / w.sprite.bounds.size.x);
                    w.color = new Color(0.8f, 0.9f, 1f, 0.45f * (1f - k));
                }
            }
            else
            {
                SetScale(1f, 1f);
                foreach (var w in _waves) w.enabled = false;
            }
        }
    }
}
