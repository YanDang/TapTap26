using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// 战斗家具基类（家具合成原型 Mix_v02 的废料 / 家具）：血量、被怪物攻击、坏掉后隐藏并在原地重生、
    /// 公告板贴图（底边贴地）+ 地上范围圈。
    /// 怪物的目标规则见 EnemyAI.ThinkTarget：检测范围内没有玩家时会去打 Targetable 的家具，锁定后打到坏为止；
    /// TauntRange &gt; 0 的家具（嘶鸣排气瓣）会强行把范围内的怪吸引过来。
    /// </summary>
    public abstract class CombatFurniture : MonoBehaviour
    {
        public static readonly List<CombatFurniture> All = new List<CombatFurniture>();

        public int maxHp = 10;
        public int hp;
        public float hitRadius = 0.6f;          // 怪物攻击它时，攻击距离额外加上这个（家具本身的半径）
        public float respawnSeconds = 12f;      // 坏掉后多久在原地重生（0 = 不重生）
        public int timesHit;

        public bool Alive { get; protected set; } = true;
        public Vector3 Pos => transform.position;
        /// <summary>怪物能不能把它当攻击目标。</summary>
        public virtual bool Targetable => Alive;
        /// <summary>正在嘲讽时的范围（0 = 不嘲讽）。</summary>
        public virtual float TauntRange => 0f;

        protected Transform holder, spriteT;
        protected SpriteRenderer sr, ring;
        protected float baseScale, height;
        float _respawnT, _flash;

        protected virtual void Awake() { All.Add(this); hp = maxHp; }
        protected virtual void OnDestroy() { All.Remove(this); }

        public static CombatFurniture NearestTargetable(Vector3 p, float range)
        {
            CombatFurniture best = null; float bd = range;
            foreach (var f in All)
            {
                if (!f || !f.Targetable) continue;
                float d = DirUtil.Flat(f.Pos - p).magnitude;
                if (d < bd) { bd = d; best = f; }
            }
            return best;
        }

        /// <summary>p 在哪个正在嘲讽的家具范围内（取最近的）。</summary>
        public static CombatFurniture TauntingNear(Vector3 p)
        {
            CombatFurniture best = null; float bd = float.MaxValue;
            foreach (var f in All)
            {
                if (!f || !f.Targetable || f.TauntRange <= 0f) continue;
                float d = DirUtil.Flat(f.Pos - p).magnitude;
                if (d <= f.TauntRange && d < bd) { bd = d; best = f; }
            }
            return best;
        }

        /// <summary>被怪物打了一下。</summary>
        public virtual void HitByEnemy(EnemyAI e, int dmg)
        {
            if (!Alive) return;
            hp -= dmg; timesHit++;
            _flash = 1f;
            Sfx.Play("sfx_hit", 0.6f);
            if (hp <= 0) Break();
        }

        protected virtual void Break()
        {
            Alive = false;
            if (holder) holder.gameObject.SetActive(false);
            if (ring) ring.enabled = false;
            _respawnT = respawnSeconds;
            CameraRig.I?.Shake(0.2f, 0.2f);
        }

        protected virtual void Respawn()
        {
            Alive = true; hp = maxHp;
            if (holder) holder.gameObject.SetActive(true);
            if (ring) ring.enabled = true;
        }

        protected virtual void Update()
        {
            if (!Alive && respawnSeconds > 0f)
            {
                _respawnT -= Time.deltaTime;
                if (_respawnT <= 0f) Respawn();
            }
            if (sr && _flash > 0f)
            {
                _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 5f);
                sr.color = Color.Lerp(BaseColor, new Color(1f, 0.55f, 0.55f), _flash);
            }
        }

        protected virtual Color BaseColor => Color.white;

        // ---------------------------------------------------------------- 外观
        /// <summary>公告板贴图（Resources/Furniture/sprite），高 h 世界单位，底边贴地；ringRadius &gt; 0 时地上画一圈范围。</summary>
        protected void BuildVisual(string sprite, float h, Material mat, float ringRadius, Color ringColor)
        {
            height = h;
            holder = new GameObject("Visual").transform;
            holder.SetParent(transform, false);
            var bb = holder.gameObject.AddComponent<Billboard>();
            bb.anchor = transform; bb.towardCamera = 1.5f;
            spriteT = new GameObject("Sprite").transform;
            spriteT.SetParent(holder, false);
            sr = spriteT.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = Resources.Load<Sprite>("Furniture/" + sprite);
            if (mat) sr.sharedMaterial = mat;
            if (sr.sprite) baseScale = h / sr.sprite.bounds.size.y;
            SetScale(1f, 1f);
            if (ringRadius > 0f) ring = GroundSprite("Ring", "UI/ui_ring", ringRadius * 2f, ringColor, mat);
        }

        /// <summary>平铺在地上的贴图（直径 size 世界单位）。</summary>
        protected SpriteRenderer GroundSprite(string name, string res, float size, Color c, Material mat, float y = 0.03f)
        {
            var g = new GameObject(name);
            g.transform.SetParent(transform, false);
            g.transform.localPosition = new Vector3(0, y, 0);
            g.transform.rotation = Quaternion.Euler(90, 0, 0);
            var r = g.AddComponent<SpriteRenderer>();
            r.sprite = Resources.Load<Sprite>(res);
            if (mat) r.sharedMaterial = mat;
            r.color = c;
            if (r.sprite) g.transform.localScale = Vector3.one * (size / r.sprite.bounds.size.x);
            return r;
        }

        /// <summary>横向 x、纵向 y 的缩放倍率，底边保持贴地。</summary>
        protected void SetScale(float x, float y)
        {
            if (!spriteT) return;
            spriteT.localScale = new Vector3(baseScale * x, baseScale * y, 1f);
            spriteT.localPosition = new Vector3(0, height * 0.5f * y, 0);
        }
    }
}
