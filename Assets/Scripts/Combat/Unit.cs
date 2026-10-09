using UnityEngine;

namespace GameJam
{
    /// <summary>Shared character plumbing: HP, CharacterController movement on the XZ plane, sprite + flash.</summary>
    public abstract class Unit : MonoBehaviour
    {
        public int maxHp = 5;
        public int hp;
        public SpriteAnimator anim;
        public SpriteRenderer sr;
        public CharacterController cc;
        public Dir facing = Dir.Down;
        public Renderer[] flashRenderers;      // 骨骼角色（鱿鱼等）的各图层：受击闪白 / 红光预警同样作用到它们

        protected MaterialPropertyBlock _mpb;
        float _flashUntil = -1, _flashDur = 0.12f;
        Color _flashColor = Color.white;

        public bool Dead => hp <= 0;
        public Vector3 Pos => transform.position;

        protected virtual void Awake()
        {
            hp = maxHp;
            _mpb = new MaterialPropertyBlock();
        }

        public void Face(Vector3 worldDir)
        {
            worldDir.y = 0;
            if (worldDir.sqrMagnitude > 1e-4f) facing = DirUtil.FromWorld(worldDir);
        }

        protected void MoveBy(Vector3 delta)
        {
            delta.y = 0;
            if (cc && cc.enabled) cc.Move(delta);
            else transform.position += delta;
            var p = transform.position;
            if (Mathf.Abs(p.y) > 0.001f) { p.y = 0; Teleport(p); }
        }

        public void Teleport(Vector3 p)
        {
            p.y = 0;
            bool e = cc && cc.enabled;
            if (cc) cc.enabled = false;
            transform.position = p;
            if (cc) cc.enabled = e;
        }

        /// <summary>Short hit flash in real time.</summary>
        public void Flash(Color c, float dur = 0.12f)
        {
            _flashColor = c; _flashDur = dur; _flashUntil = Time.unscaledTime + dur;
        }

        /// <summary>Called each frame by subclasses with a persistent glow (e.g. red attack telegraph); 0 = none.</summary>
        protected void ApplyFlash(float persistent, Color persistentColor)
        {
            if (!sr) return;
            float hit = Mathf.Clamp01((_flashUntil - Time.unscaledTime) / Mathf.Max(_flashDur, 1e-3f));
            Color c = hit > persistent ? _flashColor : persistentColor;
            float amount = Mathf.Max(hit * 0.85f, persistent);
            Apply(sr, c, amount);
            if (flashRenderers != null)
                foreach (var r in flashRenderers) if (r) Apply(r, c, amount);
        }

        void Apply(Renderer r, Color c, float amount)
        {
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor("_FlashColor", c);
            _mpb.SetFloat("_Flash", amount);
            r.SetPropertyBlock(_mpb);
        }
    }
}
