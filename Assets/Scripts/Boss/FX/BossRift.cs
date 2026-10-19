using System.Collections.Generic;
using UnityEngine;
using Random = BossVisualRandom;

namespace GameJam.Boss
{
    /// <summary>编辑器预览会同步调用 Step(dt)；运行时组件自己在 Update 里调。</summary>
    public interface IBossPreviewFx { void Step(float dt); }

    /// <summary>
    /// 夜王的“裂隙”（Claude 2026-10-18）：按原画配色——藏青 / 紫的深渊内腔、银白裂纹边（同独眼的白色血丝）、
    /// 橙色余烬从边缘往外飘。open 0..1 控制张开；可在内部放一只独眼（盯着 lookAt）。
    /// 形状是一条两头尖、边缘锯齿的“撕口”，不是圆。所有动画用真实时间。
    /// </summary>
    public sealed class BossRift : MonoBehaviour, IBossPreviewFx
    {
        public float height = 3.4f, width = 1.15f;
        public float open, targetOpen, openSpeed = 4.5f;
        public bool horizontal;          // true = 横向（天上的“眼缝”）
        public bool withEye;
        public Vector3? lookAt;
        public float squint;             // 0..1 眯眼（锁定时）
        public float flash;              // 0..1 边缘闪白
        public bool faceCamera = true;
        public bool autoDestroyWhenClosed;
        /// <summary>排序基准：地上的裂隙要画在 Boss 身后（导演设成 Boss 排序 - 20），天上的用默认 74。</summary>
        public int baseOrder = 74;
        public Color GlowColor = new Color(.62f, .28f, 1f, 1f);
        public Color InnerColor = new Color(.20f, .05f, .32f, 1f);

        const int N = 22;                // 每侧边缘点数
        static readonly Color Abyss = new Color(.02f, .01f, .06f, 1f), Inner = new Color(.20f, .05f, .32f, 1f),
                              Violet = new Color(.62f, .28f, 1f, 1f), Vein = new Color(.93f, .9f, 1f, 1f), EmberC = new Color(1f, .55f, .16f, 1f);

        Mesh _body, _glow, _eyeMesh;
        MeshRenderer _bodyR, _glowR, _eyeR;
        LineRenderer _rimA, _rimB, _rimGlow;
        readonly List<LineRenderer> _veins = new List<LineRenderer>();
        readonly List<Vector3[]> _veinPaths = new List<Vector3[]>();
        Transform _pupil, _iris;
        readonly float[] _jag = new float[N * 2];
        float _jitterT, _emberT, _time;
        bool _built;
        readonly List<Ember> _embers = new List<Ember>();
        struct Ember { public Transform t; public SpriteRenderer sr; public Vector3 v; public float life, age; }

        public static BossRift Spawn(Vector3 pos, float h, float w, bool horizontal = false, bool eye = false, int order = 74)
        {
            var go = new GameObject("BossRift");
            go.transform.position = pos;
            var r = go.AddComponent<BossRift>();
            r.height = h; r.width = w; r.horizontal = horizontal; r.withEye = eye; r.baseOrder = order;
            r.Build();
            
            return r;
        }

        void Build()
        {
            if (_built) return; _built = true;
            for (int i = 0; i < _jag.Length; i++) _jag[i] = Random.Range(-1f, 1f);
            var sprite = BossFxAssets.Shader;
            var add = BossFxAssets.Material;
            _glowR = MeshChild("Glow", add ? new Material(add) : new Material(sprite), baseOrder, out _glow);
            _bodyR = MeshChild("Abyss", new Material(sprite), baseOrder + 1, out _body);
            if (_glowR.sharedMaterial.HasProperty("_MainTex")) _glowR.sharedMaterial.mainTexture = Texture2D.whiteTexture;
            _rimGlow = Line("RimGlow", 0.22f, baseOrder + 2, true);
            _rimA = Line("RimA", 0.05f, baseOrder + 4, true); _rimB = Line("RimB", 0.05f, baseOrder + 4, true);
            for (int i = 0; i < 9; i++) { _veins.Add(Line("Vein" + i, 0.025f, baseOrder + 3, true)); _veinPaths.Add(null); }
            if (withEye) BuildEye(sprite);
            Rebuild();
        }

        MeshRenderer MeshChild(string n, Material m, int order, out Mesh mesh)
        {
            var go = new GameObject(n); go.transform.SetParent(transform, false);
            mesh = new Mesh { name = n }; mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.sortingOrder = order;
            return r;
        }

        LineRenderer Line(string n, float w, int order, bool additive)
        {
            var go = new GameObject(n); go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>(); lr.useWorldSpace = false; lr.widthMultiplier = w;
            lr.numCapVertices = 2; lr.sortingOrder = order;
            var mat = additive ? BossFxAssets.Material : null;
            lr.sharedMaterial = mat ? new Material(mat) : new Material(BossFxAssets.Shader);
            if (lr.sharedMaterial.HasProperty("_MainTex")) lr.sharedMaterial.mainTexture = Texture2D.whiteTexture;
            return lr;
        }

        void BuildEye(Shader sprite)
        {
            _eyeR = MeshChild("Eye", new Material(sprite), baseOrder + 5, out _eyeMesh);
            _iris = Disc("Iris", new Color(.55f, .55f, .62f), baseOrder + 6, .42f);
            _pupil = Disc("Pupil", new Color(.03f, .02f, .05f), baseOrder + 7, .2f);
            _pupil.SetParent(_iris, true);
        }

        Transform Disc(string n, Color c, int order, float r)
        {
            var go = new GameObject(n); go.transform.SetParent(transform, false);
            var mesh = new Mesh(); int seg = 24; var v = new Vector3[seg + 1]; var tri = new int[seg * 3]; var col = new Color[seg + 1];
            v[0] = Vector3.zero; col[0] = c;
            for (int i = 0; i < seg; i++) { float a = i * Mathf.PI * 2f / seg; v[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * r; col[i + 1] = c * .8f; col[i + 1].a = 1f; tri[i * 3] = 0; tri[i * 3 + 1] = (i + 1) % seg + 1; tri[i * 3 + 2] = i + 1; }
            mesh.vertices = v; mesh.triangles = tri; mesh.colors = col;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = new Material(BossFxAssets.Shader); mr.sortingOrder = order;
            return go.transform;
        }

        // ------------------------------------------------------------------ 形状
        /// <summary>撕口边缘：u 0..1 沿长轴，side ±1。返回局部坐标（长轴 = Y，horizontal 时换成 X）。</summary>
        Vector3 Edge(int i, float side, float o, float jagAmp)
        {
            float u = i / (float)(N - 1);
            float bulge = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)), 0.75f);
            float j = _jag[i + (side > 0 ? 0 : N)];
            float halfW = width * .5f * o * bulge * (1f + jagAmp * j * .35f);
            float along = (u - .5f) * height * Mathf.Lerp(.35f, 1f, Mathf.Sqrt(o));
            // 撕口有一点 S 形弯，像布被扯开
            float bend = Mathf.Sin(u * Mathf.PI * 2f) * width * .12f * o;
            var p = new Vector3(bend + side * halfW, along, 0f);
            return horizontal ? new Vector3(p.y, p.x, 0f) : p;
        }

        void Rebuild()
        {
            float o = Mathf.Clamp01(open);
            var verts = new List<Vector3>(); var cols = new List<Color>(); var tris = new List<int>();
            // 内腔：中心线到两侧边缘的扇面，中心最黑，边缘偏紫
            for (int i = 0; i < N; i++)
            {
                var c = (Edge(i, 1, o, 1) + Edge(i, -1, o, 1)) * .5f;
                verts.Add(c); cols.Add(Abyss);
                verts.Add(Edge(i, 1, o, 1)); cols.Add(InnerColor);
                verts.Add(Edge(i, -1, o, 1)); cols.Add(InnerColor);
            }
            for (int i = 0; i < N - 1; i++)
            {
                int a = i * 3, b = (i + 1) * 3;
                tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1, a, a + 2, b, a + 2, b + 2, b });
            }
            _body.Clear(); _body.SetVertices(verts); _body.SetColors(cols); _body.SetTriangles(tris, 0);

            // 外辉光：边缘一圈往外淡出
            verts.Clear(); cols.Clear(); tris.Clear();
            float glowW = Mathf.Lerp(.25f, .55f, o) * (1f + flash * .8f);
            var gc = Color.Lerp(GlowColor, Vein, flash * .6f); gc.a = .55f * Mathf.Clamp01(o * 3f);
            var ge = gc; ge.a = 0f;
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? 1f : -1f; int baseI = verts.Count;
                for (int i = 0; i < N; i++)
                {
                    var e = Edge(i, side, o, 1);
                    var n = Edge(i, side, Mathf.Max(o, .05f) + .3f, 0) - Edge(i, -side, Mathf.Max(o, .05f) + .3f, 0);
                    n = n.sqrMagnitude > 1e-6f ? n.normalized : (horizontal ? Vector3.up : Vector3.right) * side;
                    float tip = Mathf.Sin(i / (float)(N - 1) * Mathf.PI);
                    verts.Add(e); cols.Add(gc);
                    verts.Add(e + n * glowW * (.4f + .6f * tip)); cols.Add(ge);
                }
                for (int i = 0; i < N - 1; i++) { int a = baseI + i * 2; tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 }); }
            }
            _glow.Clear(); _glow.SetVertices(verts); _glow.SetColors(cols); _glow.SetTriangles(tris, 0);

            // 边缘：两条银白细线 + 一条紫色粗辉光
            SetRim(_rimA, 1, o); SetRim(_rimB, -1, o);
            _rimGlow.positionCount = N * 2;
            for (int i = 0; i < N; i++) _rimGlow.SetPosition(i, Edge(i, 1, o, 1));
            for (int i = 0; i < N; i++) _rimGlow.SetPosition(N + i, Edge(N - 1 - i, -1, o, 1));
            float rimA = Mathf.Clamp01(o * 4f);
            Col(_rimA, Color.Lerp(Vein, Color.white, flash), rimA); Col(_rimB, Color.Lerp(Vein, Color.white, flash), rimA);
            Col(_rimGlow, GlowColor, .35f * rimA + flash * .3f);

            // 白色裂纹：从边缘往外分叉，像独眼周围的血丝
            for (int k = 0; k < _veins.Count; k++)
            {
                var lr = _veins[k];
                if (_veinPaths[k] == null) _veinPaths[k] = VeinPath(k);
                var path = _veinPaths[k];
                float grow = Mathf.Clamp01(o * 1.6f - k * .05f);
                int cnt = Mathf.Max(2, Mathf.RoundToInt(path.Length * grow));
                lr.positionCount = cnt;
                int edgeI = Mathf.Clamp(2 + k * (N - 4) / _veins.Count, 1, N - 2);
                var start = Edge(edgeI, (k & 1) == 0 ? 1 : -1, o, 1);
                for (int i = 0; i < cnt; i++) lr.SetPosition(i, start + path[i] * Mathf.Lerp(.6f, 1f, o));
                lr.widthMultiplier = .028f * (1f + flash);
                Col(lr, Vein, .8f * grow);
            }

            if (_eyeR)
            {
                // 眼：杏仁形白眼球 + 灰虹膜 + 黑瞳，跟着 lookAt 转
                float eo = Mathf.Clamp01((o - .35f) / .5f) * (1f - squint * .55f);
                verts.Clear(); cols.Clear(); tris.Clear();
                float ew = (horizontal ? height : width) * .28f, eh = (horizontal ? width : height) * .2f * eo;
                verts.Add(Vector3.zero); cols.Add(new Color(.92f, .9f, .95f));
                int seg = 28;
                for (int i = 0; i < seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    verts.Add(new Vector3(Mathf.Cos(a) * ew, Mathf.Sin(a) * eh * (1f - .25f * Mathf.Abs(Mathf.Cos(a))), 0f)); cols.Add(new Color(.75f, .72f, .82f));
                    tris.AddRange(new[] { 0, (i + 1) % seg + 1, i + 1 });
                }
                _eyeMesh.Clear(); _eyeMesh.SetVertices(verts); _eyeMesh.SetColors(cols); _eyeMesh.SetTriangles(tris, 0);
                Vector2 look = Vector2.zero;
                if (lookAt.HasValue)
                {
                    var d = transform.InverseTransformPoint(lookAt.Value);
                    look = new Vector2(d.x, d.y).normalized;
                }
                // Leave sclera around the tracking iris, including the squint pose.
                float irisScale = Mathf.Min(ew * .4f, eh * .72f) / .42f;
                _iris.localScale = Vector3.one * Mathf.Max(.001f, irisScale);
                _iris.localPosition = new Vector3(look.x * ew * .3f, look.y * eh * .18f, -.01f);
                _pupil.localScale = Vector3.one * Mathf.Lerp(1f, .8f, squint);
                bool vis = withEye && eo > .02f; _eyeR.enabled = vis; _iris.gameObject.SetActive(vis);
            }
        }

        Vector3[] VeinPath(int k)
        {
            var dir = horizontal ? new Vector3(Random.Range(-.6f, .6f), (k & 1) == 0 ? 1f : -1f, 0f) : new Vector3((k & 1) == 0 ? 1f : -1f, Random.Range(-.6f, .6f), 0f);
            dir.Normalize();
            int n = Random.Range(4, 7); var p = new Vector3[n]; var cur = Vector3.zero; float len = Random.Range(.35f, .8f) * Mathf.Max(width, .8f);
            for (int i = 0; i < n; i++)
            {
                p[i] = cur;
                dir = Quaternion.Euler(0, 0, Random.Range(-35f, 35f)) * dir;
                cur += dir * len / n;
            }
            return p;
        }

        void SetRim(LineRenderer lr, float side, float o)
        {
            lr.positionCount = N;
            for (int i = 0; i < N; i++) lr.SetPosition(i, Edge(i, side, o, 1));
        }

        static void Col(LineRenderer lr, Color c, float a) { c.a = a; lr.startColor = lr.endColor = c; }

        // ------------------------------------------------------------------ 更新
        void Update() { if (Application.isPlaying && !EnemyController.CombatWorldPaused) Step(Time.deltaTime); }
        void OnDestroy() { foreach(var f in GetComponentsInChildren<MeshFilter>()) if(f.sharedMesh) Destroy(f.sharedMesh); foreach(var r in GetComponentsInChildren<Renderer>()) if(r.sharedMaterial && r.sharedMaterial!=BossFxAssets.Material) Destroy(r.sharedMaterial); }

        public void Step(float dt)
        {
            if (EnemyController.CombatWorldPaused) return;
            if (!_built) Build();
            _time += dt;
            open = Mathf.MoveTowards(open, targetOpen, openSpeed * dt);
            flash = Mathf.MoveTowards(flash, 0f, dt * 3f);
            _jitterT -= dt;
            if (_jitterT <= 0f)
            {
                _jitterT = .06f;
                for (int i = 0; i < _jag.Length; i++) _jag[i] = Mathf.Lerp(_jag[i], Random.Range(-1f, 1f), .35f);
            }
            if (faceCamera && Application.isPlaying) { var c = Camera.main; if (c) transform.rotation = c.transform.rotation; }
            Rebuild();
            Embers(dt);
            if (autoDestroyWhenClosed && targetOpen <= 0f && open <= 0.001f && _embers.Count == 0) Destroy(gameObject);
        }

        void Embers(float dt)
        {
            _emberT -= dt;
            if (open > .1f && _emberT <= 0f)
            {
                _emberT = .045f;
                int i = Random.Range(1, N - 1); float side = Random.value < .5f ? 1f : -1f;
                SpawnEmber(Edge(i, side, open, 1), (horizontal ? Vector3.up * side : Vector3.right * side) * Random.Range(.2f, .6f) + Vector3.up * Random.Range(.3f, .8f), Random.Range(.5f, 1f), Random.value < .75f ? EmberC : Violet);
            }
            for (int k = _embers.Count - 1; k >= 0; k--)
            {
                var e = _embers[k]; e.age += dt;
                if (!e.t || e.age >= e.life) { if (e.t) Destroy(e.t.gameObject); _embers.RemoveAt(k); continue; }
                e.t.localPosition += e.v * dt; e.v *= 1f - dt * .8f;
                var c = e.sr.color; c.a = 1f - e.age / e.life; e.sr.color = c;
                _embers[k] = e;
            }
        }

        void SpawnEmber(Vector3 local, Vector3 vel, float life, Color c)
        {
            var go = new GameObject("Ember"); go.transform.SetParent(transform, false); go.transform.localPosition = local;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = BossFxAssets.Dot; sr.color = c; sr.sortingOrder = baseOrder + 5;
            var add = BossFxAssets.Material; if (add) sr.sharedMaterial = add;
            float s = Random.Range(.05f, .11f) / Mathf.Max(.01f, sr.sprite.bounds.size.x); go.transform.localScale = Vector3.one * s;
            _embers.Add(new Ember { t = go.transform, sr = sr, v = vel, life = life });
        }

        /// <summary>啪地合上：闪白 + 一圈余烬炸开。</summary>
        public void Slam()
        {
            
            flash = 1f; targetOpen = 0f; openSpeed = 7f;
            for (int i = 0; i < 26; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                SpawnEmber(Vector3.zero, new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(1.5f, 3.5f), Random.Range(.3f, .6f), i % 3 == 0 ? Vein : EmberC);
            }
        }
    }
}
