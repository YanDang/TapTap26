using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// 充气导管：连接两个气系节点（点对点，中间略微下垂的软管）。连通的节点共享气体。
    /// 管道里的条纹随流量滚动（流量 = GasNetwork 每帧写的 flow）。
    /// 也是家具：玩家和怪物都能打断它（打断时在中点小气爆、网络被切开）。不重生。
    /// </summary>
    public class GasPipe : CombatFurniture
    {
        public GasNode a, b;
        public float flow;                      // 每秒流量（GasNetwork 写，用于画面）
        public const float MaxLength = 6f;      // 最长多长
        public const float MetersPerDuct = 2f;  // 每根“充气导管”能铺多长

        LineRenderer _line, _shine;
        float _scroll;
        static Material _mat;

        public override bool PlayerCanHit => Alive;

        public override Vector3 AimPoint(Vector3 from)
        {
            if (!a || !b) return Pos;
            Vector3 p0 = DirUtil.Flat(a.Pos), p1 = DirUtil.Flat(b.Pos), d = p1 - p0;
            float t = d.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector3.Dot(DirUtil.Flat(from) - p0, d) / d.sqrMagnitude) : 0.5f;
            return p0 + d * t;
        }
        public float Length => a && b ? DirUtil.Flat(b.Pos - a.Pos).magnitude : 0f;

        /// <summary>铺这么长的管道要几根导管。</summary>
        public static int DuctCost(float length) => Mathf.Max(1, Mathf.CeilToInt(length / MetersPerDuct));

        public static GasPipe Connect(GasNode a, GasNode b)
        {
            var go = new GameObject("GasPipe");
            go.transform.position = (a.Pos + b.Pos) * 0.5f;
            var p = go.AddComponent<GasPipe>();
            p.a = a; p.b = b;
            p.maxHp = p.hp = 6;
            p.hitRadius = 0.35f;                                     // 怪物打的是管子上离它最近的点（AimPoint）
            p.respawnSeconds = 0f;
            a.pipes.Add(p); b.pipes.Add(p);
            p._line = p.MakeLine("Hose", 0.2f, new Color(0.95f, 0.55f, 0.75f), 0);
            p._shine = p.MakeLine("Shine", 0.07f, new Color(1f, 0.85f, 0.95f), 1);
            p.UpdateShape();
            GasNetwork.Dirty();
            return p;
        }

        LineRenderer MakeLine(string name, float width, Color c, int order)
        {
            if (!_mat)
            {
                // 横向条纹贴图：一节一节的软管环
                var tex = new Texture2D(16, 4) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
                for (int x = 0; x < 16; x++)
                    for (int y = 0; y < 4; y++)
                        tex.SetPixel(x, y, x < 3 ? new Color(0.6f, 0.6f, 0.65f) : Color.white);
                tex.Apply();
                _mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
            }
            var g = new GameObject(name);
            g.transform.SetParent(transform, false);
            var lr = g.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 12;
            lr.widthMultiplier = width;
            lr.material = _mat;                          // 每根管道一份材质实例，各自滚动
            lr.textureMode = LineTextureMode.Tile;
            lr.startColor = lr.endColor = c;
            lr.numCapVertices = 3;
            lr.alignment = LineAlignment.View;
            lr.sortingOrder = order;
            return lr;
        }

        void UpdateShape()
        {
            if (!a || !b) return;
            Vector3 p0 = a.Pos + Vector3.up * 0.3f, p1 = b.Pos + Vector3.up * 0.3f;
            float sag = Mathf.Min(0.25f, Length * 0.05f);
            for (int i = 0; i < 12; i++)
            {
                float t = i / 11f;
                var p = Vector3.Lerp(p0, p1, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * sag;
                _line.SetPosition(i, p);
                _shine.SetPosition(i, p + Vector3.up * 0.04f);
            }
            transform.position = (a.Pos + b.Pos) * 0.5f;
        }

        /// <summary>两头的节点没了：管道跟着断（不再气爆一次）。</summary>
        public void Detach()
        {
            if (!Alive) return;
            Alive = false;
            Cleanup();
        }

        protected override void Break()
        {
            base.Break();
            GasBurst.Do(Pos, 1.2f, 1.6f);           // 断口漏气，小气爆
            Cleanup();
        }

        void Cleanup()
        {
            if (a) a.pipes.Remove(this);
            if (b) b.pipes.Remove(this);
            if (_line) _line.enabled = false;
            if (_shine) _shine.enabled = false;
            GasNetwork.Dirty();
            Destroy(gameObject, 0.3f);
        }

        protected override void Update()
        {
            base.Update();
            if (!Alive) return;
            if (!a || !b || !a.Alive || !b.Alive) { Detach(); return; }
            UpdateShape();
            // 有气在流就滚动条纹，流量越大越快；被打时变红
            _scroll -= Time.deltaTime * (0.4f + flow * 0.8f) * (flow > 0.01f ? 1f : 0f);
            _line.material.mainTextureOffset = new Vector2(_scroll, 0);
            float hurt = 1f - hp / (float)maxHp;
            _line.startColor = _line.endColor = Color.Lerp(new Color(0.95f, 0.55f, 0.75f), new Color(1f, 0.3f, 0.3f), hurt * 0.7f);
        }
    }
}
