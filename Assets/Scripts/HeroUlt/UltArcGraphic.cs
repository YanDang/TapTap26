using UnityEngine;
using UnityEngine.UI;

namespace GameJam.HeroUlt
{
    /// <summary>
    /// 满能量图标的放电效果（v2，Claude 2026-10-18 重写；类守望先锋“大招就绪”）：
    /// - 闪电：在圆环上两点之间“跳”，向外拱出，路径用中点位移法生成分形锯齿，带 1–2 根分叉；
    ///   每道只亮 0.06–0.15 s，灭掉后隔 0–0.08 s 在别处重新打出 → 噼啪乱闪，同时最多 4 道；
    /// - 火星：每次打出闪电时从两个落点向外迸几粒短火星；
    /// - 高光：一道亮弧沿圆环旋转，表示能量在流动。
    /// 白芯 + 青色 / 金色加法辉光；全部真实时间。对外接口（Active / ArcCount / BranchCount）与 v1 相同。
    /// </summary>
    public class UltArcGraphic : MaskableGraphic
    {
        public float radius = 46f, width = 2.4f, glowWidth = 8f, bulge = 17f, roughness = 0.42f;   // radius = 环形进度条（112 px、环心 0.82）的实际半径
        bool _active;

        public bool Active
        {
            get => _active;
            set
            {
                if (_active == value) return;
                _active = value;
                SetVerticesDirty();
            }
        }

        public int ArcCount { get { int n = 0; foreach (var b in _bolts) if (b.on) n++; return n; } }
        public int BranchCount { get { int n = 0; foreach (var b in _bolts) if (b.on && b.forks != null) n += b.forks.Length; return n; } }

        class Bolt
        {
            public bool on;
            public float life, wait;
            public Vector2[] path;
            public Vector2[][] forks;
            public Color glow;
        }

        struct Spark { public Vector2 p, v; public float life, max; public Color c; }

        const int MaxBolts = 4, MaxSparks = 24;
        readonly Bolt[] _bolts = new Bolt[MaxBolts];
        readonly Spark[] _sparks = new Spark[MaxSparks];
        float _sheen;
        static readonly Color Cyan = new Color(0.4f, 0.9f, 1f);
        static readonly Color Gold = new Color(1f, 0.82f, 0.3f);

        protected override void Awake()
        {
            base.Awake();
            
            for (int i = 0; i < MaxBolts; i++) _bolts[i] = new Bolt { wait = i * 0.03f };
        }

        protected override void OnEnable() { base.OnEnable(); }

        static Vector2 OnRing(float deg, float r) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad)) * r;

        void Strike(Bolt b)
        {
            float a0 = Random.value * 360f, span = Random.Range(40f, 95f) * (Random.value < 0.5f ? 1 : -1);
            Vector2 p0 = OnRing(a0, radius), p1 = OnRing(a0 + span, radius);
            Vector2 mid = OnRing(a0 + span * 0.5f, radius + bulge * Random.Range(0.6f, 1.3f));   // 向外拱
            // 中点位移：两段（起点→拱点→终点）各细分 4 层
            var a = Fractal(p0, mid, 4, (mid - p0).magnitude * roughness);
            var c = Fractal(mid, p1, 4, (p1 - mid).magnitude * roughness);
            b.path = new Vector2[a.Length + c.Length - 1];
            a.CopyTo(b.path, 0); System.Array.Copy(c, 1, b.path, a.Length, c.Length - 1);
            // 分叉：从路径中段 1–2 个点向外打出短闪电
            int nf = Random.value < 0.65f ? 1 : 2;
            b.forks = new Vector2[nf][];
            for (int f = 0; f < nf; f++)
            {
                var s = b.path[Random.Range(b.path.Length / 4, b.path.Length * 3 / 4)];
                var outDir = (s.normalized + Random.insideUnitCircle * 0.6f).normalized;
                b.forks[f] = Fractal(s, s + outDir * Random.Range(10f, 20f), 3, 5f);
            }
            b.glow = Random.value < 0.3f ? Gold : Cyan;
            b.life = Random.Range(0.06f, 0.15f);
            b.on = true;
            Burst(p0, b.glow); Burst(p1, b.glow);
        }

        static Vector2[] Fractal(Vector2 a, Vector2 b, int levels, float disp)
        {
            int n = (1 << levels) + 1;
            var p = new Vector2[n];
            p[0] = a; p[n - 1] = b;
            for (int step = n - 1; step > 1; step /= 2, disp *= 0.55f)
                for (int i = 0; i + step < n; i += step)
                {
                    var m = (p[i] + p[i + step]) * 0.5f;
                    var d = p[i + step] - p[i];
                    var nrm = new Vector2(-d.y, d.x).normalized;
                    p[i + step / 2] = m + nrm * Random.Range(-disp, disp);
                }
            return p;
        }

        void Burst(Vector2 at, Color c)
        {
            int k = Random.Range(2, 4);
            for (int i = 0; i < MaxSparks && k > 0; i++)
            {
                if (_sparks[i].life > 0f) continue;
                var dir = (at.normalized + Random.insideUnitCircle * 0.8f).normalized;
                float max = Random.Range(0.18f, 0.32f);
                _sparks[i] = new Spark { p = at, v = dir * Random.Range(60f, 130f), life = max, max = max, c = c };
                k--;
            }
        }

        void Update() => Step(Mathf.Min(Time.unscaledDeltaTime, 0.05f));

        /// <summary>推进一帧（编辑器预览录制也用它）。</summary>
        public void Step(float dt)
        {
            if (!Active || dt <= 0f) return;
            foreach (var b in _bolts)
            {
                if (b.on) { b.life -= dt; if (b.life <= 0f) { b.on = false; b.wait = Random.Range(0f, 0.08f); } }
                else { b.wait -= dt; if (b.wait <= 0f) Strike(b); }
            }
            for (int i = 0; i < MaxSparks; i++)
                if (_sparks[i].life > 0f)
                {
                    _sparks[i].life -= dt;
                    _sparks[i].p += _sparks[i].v * dt;
                    _sparks[i].v *= 1f - 4f * dt;
                }
            _sheen = (_sheen + 260f * dt) % 360f;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!Active) return;
            // 环上旋转高光（两层：宽淡 + 窄亮）
            var arc = new Vector2[13];
            for (int i = 0; i < arc.Length; i++) arc[i] = OnRing(_sheen + i * 4f, radius);
            Strip(vh, arc, 12f, new Color(1f, 0.85f, 0.4f, 0.18f));
            Strip(vh, arc, 4f, new Color(1f, 0.95f, 0.75f, 0.55f));
            foreach (var b in _bolts)
            {
                if (!b.on || b.path == null) continue;
                // 刚打出时最亮，随后快速衰减（闪一下的感觉）
                float k = Mathf.Clamp01(b.life / 0.06f);
                Color halo = b.glow; halo.a = 0.10f + 0.08f * k;
                Color glow = b.glow; glow.a = 0.30f + 0.25f * k;
                Color core = new Color(1f, 1f, 1f, 0.75f + 0.25f * k);
                Strip(vh, b.path, glowWidth * 2.4f, halo);
                Strip(vh, b.path, glowWidth, glow);
                Strip(vh, b.path, width, core);
                if (b.forks != null)
                    foreach (var f in b.forks)
                    {
                        Strip(vh, f, glowWidth * 0.7f, glow);
                        Strip(vh, f, width * 0.6f, core);
                    }
            }
            foreach (var s in _sparks)
            {
                if (s.life <= 0f) continue;
                float k = s.life / s.max;
                Color c = s.c; c.a = 0.9f * k;
                Strip(vh, new[] { s.p, s.p - s.v * 0.03f }, 2f, c);
            }
        }

        /// <summary>共享顶点的折线带：相邻段共用两侧顶点，避免接缝开裂。</summary>
        static void Strip(VertexHelper vh, Vector2[] p, float w, Color c)
        {
            if (p == null || p.Length < 2) return;
            int start = vh.currentVertCount;
            for (int i = 0; i < p.Length; i++)
            {
                Vector2 d = i == 0 ? p[1] - p[0] : i == p.Length - 1 ? p[i] - p[i - 1] : p[i + 1] - p[i - 1];
                if (d.sqrMagnitude < 1e-6f) d = Vector2.right;
                d.Normalize();
                var n = new Vector2(-d.y, d.x) * (w * 0.5f);
                vh.AddVert(new Vector3(p[i].x - n.x, p[i].y - n.y, 0f), c, Vector2.zero);
                vh.AddVert(new Vector3(p[i].x + n.x, p[i].y + n.y, 0f), c, Vector2.zero);
            }
            for (int i = 0; i < p.Length - 1; i++)
            {
                int a = start + i * 2;
                vh.AddTriangle(a, a + 1, a + 2);
                vh.AddTriangle(a + 1, a + 3, a + 2);
            }
        }
    }
}
