using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// 所有家具共用的部署动画（2026-10-11）：
    /// 1. 飞行：主角从壳里掏出的“家具胶囊”（物品图标缩小版）沿抛物线从钳子飞到落点，边飞边转；
    /// 2. 落地：一声闷响 + 尘土 + 地上一圈冲击波；
    /// 3. 长出来：家具从地里弹出来（先拉长再压扁、回弹两下），地上的范围圈 / 水渍从中心铺开。
    /// 整个过程家具不起作用（脚本、碰撞体关掉，CombatFurniture.Deploying = true，怪物不会把它当目标），播完才启用。
    /// 只认通用结构：带 Billboard 的公告板贴图、平铺在地上的 SpriteRenderer、名叫 Visual 的子物体（桌子），所以任何家具都能用。
    /// </summary>
    public class FurnitureDeploy : MonoBehaviour
    {
        public const float GrowTime = 0.45f;
        public static int finished;                       // 自测用

        readonly List<(Transform t, Vector3 s)> _pop = new List<(Transform, Vector3)>();      // 弹出来的部分（公告板 / 桌子模型）
        readonly List<(Transform t, Vector3 s)> _ground = new List<(Transform, Vector3)>();   // 铺开的部分（地上的圈、水渍）
        readonly List<Behaviour> _scripts = new List<Behaviour>();
        readonly List<Collider> _cols = new List<Collider>();
        CombatFurniture _furn;
        KickableTable _table;
        Transform _capsule, _capsuleAnchor;
        SpriteRenderer _wave;
        Billboard _capsuleBb;
        Vector3 _from, _to;
        float _t, _flight;
        bool _landed;
        System.Action _done;

        public float Total => _flight + GrowTime;

        /// <summary>
        /// 给刚生成的家具播部署动画。from = 胶囊从哪飞出来（主角钳子的位置，null = 不飞，直接落地长出来）；
        /// icon = 胶囊的样子；done = 播完回调。
        /// </summary>
        public static FurnitureDeploy Begin(GameObject root, Sprite icon, Vector3? from, System.Action done = null)
        {
            var d = root.AddComponent<FurnitureDeploy>();
            d._done = done;
            d._to = DirUtil.Flat(root.transform.position);
            d._furn = root.GetComponent<CombatFurniture>();
            d._table = root.GetComponent<KickableTable>();
            if (d._furn) d._furn.Deploying = true;
            if (d._table) d._table.Deploying = true;
            foreach (var b in root.GetComponents<Behaviour>())
                if (b != d && b.enabled && (b is CombatFurniture || b is KickableTable)) { b.enabled = false; d._scripts.Add(b); }
            foreach (var c in root.GetComponentsInChildren<Collider>()) if (c.enabled) { c.enabled = false; d._cols.Add(c); }

            var bbs = root.GetComponentsInChildren<Billboard>();
            foreach (var bb in bbs) d._pop.Add((bb.transform, bb.transform.localScale));
            if (bbs.Length == 0)
            {
                var v = root.transform.Find("Visual");
                if (v) d._pop.Add((v, v.localScale));
            }
            foreach (var r in root.GetComponentsInChildren<SpriteRenderer>())
            {
                if (r.GetComponentInParent<Billboard>()) continue;                                 // 公告板里的贴图跟着公告板弹
                if (Vector3.Angle(r.transform.up, Vector3.up) < 80f) continue;                     // 不是平铺在地上的
                d._ground.Add((r.transform, r.transform.localScale));
            }
            d.Apply(0f, 0f);

            if (from.HasValue)
            {
                d._from = from.Value;
                float dist = DirUtil.Flat(d._to - d._from).magnitude;
                d._flight = 0.22f + dist * 0.035f;
                d.MakeCapsule(icon);
            }
            else d.Land();
            return d;
        }

        void MakeCapsule(Sprite icon)
        {
            _capsuleAnchor = new GameObject("DeployCapsuleAnchor").transform;
            _capsuleAnchor.position = _from;
            _capsule = new GameObject("DeployCapsule").transform;
            _capsuleBb = _capsule.gameObject.AddComponent<Billboard>();
            _capsuleBb.anchor = _capsuleAnchor; _capsuleBb.towardCamera = 2.2f;
            var r = new GameObject("Sprite").AddComponent<SpriteRenderer>();
            r.transform.SetParent(_capsule, false);
            r.sprite = icon;
            r.sharedMaterial = Resources.Load<Material>("Materials/SpriteBillboard");
            if (icon) r.transform.localScale = Vector3.one * (0.5f / Mathf.Max(icon.bounds.size.x, icon.bounds.size.y));
        }

        void Land()
        {
            _landed = true;
            if (_capsule) Destroy(_capsule.gameObject);
            if (_capsuleAnchor) Destroy(_capsuleAnchor.gameObject);
            Vfx.Spawn("VFX_ArrowHit", _to + Vector3.up * 0.3f, 0, 1.1f, false, null, new Color(0.85f, 0.95f, 1f));
            Sfx.Play("sfx_table_land", 0.55f);
            CameraRig.I?.Shake(0.08f, 0.1f);
            // 地上一圈冲击波
            var g = new GameObject("DeployWave");
            g.transform.position = _to + Vector3.up * 0.05f;
            g.transform.rotation = Quaternion.Euler(90, 0, 0);
            _wave = g.AddComponent<SpriteRenderer>();
            _wave.sprite = Resources.Load<Sprite>("UI/ui_ring");
            _wave.sharedMaterial = Resources.Load<Material>("Materials/SpriteBillboard");
        }

        /// <summary>u = 弹出进度、g = 铺开进度（0–1）。</summary>
        void Apply(float u, float g)
        {
            // 先冒出来再回弹两下：纵向过冲、横向反着压
            float baseS = 1f - Mathf.Pow(1f - u, 3f);
            float wob = Mathf.Sin(u * Mathf.PI * 2.5f) * (1f - u) * 0.35f;
            var k = u <= 0f ? Vector3.zero : new Vector3(baseS - wob * 0.6f, baseS + wob, 1f);
            foreach (var (t, s) in _pop) if (t) t.localScale = Vector3.Scale(s, new Vector3(k.x, k.y, k.x));
            float gk = 1f - Mathf.Pow(1f - g, 3f);
            foreach (var (t, s) in _ground) if (t) t.localScale = s * gk;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            if (!_landed)
            {
                float u = Mathf.Clamp01(_t / Mathf.Max(_flight, 1e-3f));
                var p = Vector3.Lerp(_from, _to, u);
                float h = 1.2f + DirUtil.Flat(_to - _from).magnitude * 0.12f;
                p.y = Mathf.Lerp(_from.y, 0.25f, u) + h * 4f * u * (1f - u);
                if (_capsuleAnchor) _capsuleAnchor.position = p;
                if (_capsuleBb) _capsuleBb.roll = -_t * 720f;
                if (u >= 1f) Land();
                return;
            }
            float tg = _t - _flight;
            Apply(Mathf.Clamp01(tg / GrowTime), Mathf.Clamp01((tg - 0.08f) / GrowTime));
            if (_wave)
            {
                float w = Mathf.Clamp01(tg / 0.35f);
                if (_wave.sprite) _wave.transform.localScale = Vector3.one * (Mathf.Lerp(0.3f, 2.6f, w) / _wave.sprite.bounds.size.x);
                _wave.color = new Color(0.85f, 0.95f, 1f, 0.8f * (1f - w));
                if (w >= 1f) { Destroy(_wave.gameObject); _wave = null; }
            }
            if (tg >= GrowTime + 0.08f) Finish();
        }

        void Finish()
        {
            foreach (var (t, s) in _pop) if (t) t.localScale = s;
            foreach (var (t, s) in _ground) if (t) t.localScale = s;
            foreach (var b in _scripts) if (b) b.enabled = true;
            foreach (var c in _cols) if (c) c.enabled = true;
            if (_furn) _furn.Deploying = false;
            if (_table) _table.Deploying = false;
            if (_wave) Destroy(_wave.gameObject);
            finished++;
            _done?.Invoke();
            Destroy(this);
        }

        void OnDestroy()
        {
            if (_capsule) Destroy(_capsule.gameObject);
            if (_capsuleAnchor) Destroy(_capsuleAnchor.gameObject);
            if (_wave) Destroy(_wave.gameObject);
        }
    }
}
