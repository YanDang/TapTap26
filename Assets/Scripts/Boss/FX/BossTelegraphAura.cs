using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace GameJam.Boss
{
    /// <summary>
    /// 预警时“身上透光”的外层表现（返工 1）：
    /// 1) 每一层原画在身后复制一份放大 1.04 的纯色剪影（只采样 sprite alpha），蒙皮图层带同骨 SpriteSkin；
    /// 2) Boss 根下贴地一个柔光圆。
    /// 由 BossAnimator.SetTelegraph 驱动；只在运行时创建，不改预制体、不改共享材质。原画层的 _Flash 由
    /// BossAnimator 压到 0.35（受击白闪仍可到 0.85），所以细节清楚，外圈仍一眼分得清黄/红/紫。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossTelegraphAura : MonoBehaviour
    {
        const float GhostScale = 1.07f;   // Claude：原画不再整体染色后，外圈加宽一点保证黄 / 红一眼可辨

        sealed class Ghost
        {
            public SpriteRenderer source, renderer;
        }

        static Material _silhouette, _glow;
        static readonly FieldInfo RootField = typeof(SpriteSkin).GetField("m_RootBone", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo BonesField = typeof(SpriteSkin).GetField("m_BoneTransforms", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo AlwaysField = typeof(SpriteSkin).GetField("m_AlwaysUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

        BossAnimator _boss;
        readonly List<Ghost> _ghosts = new List<Ghost>();
        MeshRenderer _ground;
        MaterialPropertyBlock _mpb;
        Color _color = Color.yellow;
        float _amount;
        bool _visible, _built;

        public static BossTelegraphAura Attach(BossAnimator boss)
        {
            var aura = boss.GetComponent<BossTelegraphAura>();
            if (!aura) aura = boss.gameObject.AddComponent<BossTelegraphAura>();
            aura._boss = boss;
            return aura;
        }

        public void Show(float amount, Color color)
        {
            _amount = Mathf.Clamp01(amount);
            color.a = 1f;
            _color = color;
            _visible = true;
            if (!_built) Build();
            Apply();
        }

        public void Hide()
        {
            _visible = false;
            if (_built) Apply();
        }

        void LateUpdate()
        {
            // 动画器被停用 / Boss 死亡时自动收掉，不依赖 BossAI 额外调用。
            if (!_boss || !_boss.gameObject.activeInHierarchy || _boss.IsDead || PlayerSessionData.IsCombatInputBlocked) { if (_visible) { _visible = false; if (_built) Apply(); } return; }
            if (_visible) Apply();
        }

        void Build()
        {
            _built = true;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            var sources = new List<SpriteRenderer>();
            var assigned = _boss.layers;
            if (assigned != null) foreach (var r in assigned) if (r) sources.Add(r);
            if (_boss.eyeRenderer && !sources.Contains(_boss.eyeRenderer)) sources.Add(_boss.eyeRenderer);
            // 没有显式图层表时宁可不做剪影：避免把运行时副本误当成动画器图层收进去。
            if (sources.Count == 0) { BuildGround(0); return; }

            int minOrder = int.MaxValue;
            foreach (var r in sources) if (r) minOrder = Mathf.Min(minOrder, r.sortingOrder);
            if (minOrder == int.MaxValue) minOrder = 0;

            foreach (var src in sources) AddGhost(src, minOrder - 5);
            BuildGround(minOrder);
        }

        void BuildGround(int minOrder)
        {
            var go = new GameObject("BossTelegraphAura_Ground");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.045f, 0f);
            float radius = Mathf.Max(1.6f, _boss.Height * 0.5f);
            go.transform.localScale = new Vector3(radius, radius, 1f);
            var mf = go.AddComponent<MeshFilter>();
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0) };
            mesh.uv = new[] { Vector2.zero,Vector2.right,Vector2.one,Vector2.up };
            mesh.triangles = new[] { 0,1,2,0,2,3 }; mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            _ground = go.AddComponent<MeshRenderer>();
            _ground.sharedMaterial = GlowMaterial();
            _ground.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ground.receiveShadows = false;
            _ground.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _ground.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _ground.sortingOrder = minOrder - 6;
            _ground.enabled = false;
        }

        void AddGhost(SpriteRenderer src, int order)
        {
            if (!src || !src.sprite) return;
            var go = new GameObject(src.name + "_Aura");
            go.SetActive(false);                     // 先关掉，配好 SpriteSkin 再启用，避免空骨绑定
            go.transform.SetParent(src.transform.parent, false);
            go.transform.localPosition = src.transform.localPosition;
            go.transform.localRotation = src.transform.localRotation;
            go.transform.localScale = src.transform.localScale * GhostScale;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = src.sprite;
            renderer.sortingLayerID = src.sortingLayerID;
            renderer.sortingOrder = order;
            renderer.flipX = src.flipX;
            renderer.flipY = src.flipY;
            renderer.sharedMaterial = SilhouetteMaterial();
            renderer.enabled = false;

            var skinSource = src.GetComponent<SpriteSkin>();
            if (skinSource)
            {
                var skin = go.AddComponent<SpriteSkin>();
                if (RootField != null) RootField.SetValue(skin, skinSource.rootBone);
                if (BonesField != null) BonesField.SetValue(skin, skinSource.boneTransforms);
                if (AlwaysField != null) AlwaysField.SetValue(skin, true);
            }
            go.SetActive(true);
            _ghosts.Add(new Ghost { source = src, renderer = renderer });
        }

        void Apply()
        {
            if (!_built) return;
            Color c = _color;
            c.a = 0.20f + 0.42f * _amount;
            _mpb.SetColor("_Color", c);
            for (int i = 0; i < _ghosts.Count; i++)
            {
                var g = _ghosts[i];
                if (g.renderer == null || g.source == null) continue;
                bool on = _visible && g.source.enabled && g.source.gameObject.activeInHierarchy;
                if (g.renderer.enabled != on) g.renderer.enabled = on;
                if (on)
                {
                    // 必须先取回渲染器现有的 property block：SpriteRenderer 把 sprite 贴图放在
                    // block 的 _MainTex 里，直接 Set 一个空 block 会让剪影丢掉贴图 alpha 变成整块矩形。
                    g.renderer.GetPropertyBlock(_mpb);
                    _mpb.SetColor("_Color", c);
                    g.renderer.SetPropertyBlock(_mpb);
                }
            }
            if (_ground)
            {
                bool on = _visible;
                if (_ground.enabled != on) _ground.enabled = on;
                if (on)
                {
                    Color gc = _color;
                    gc.a = 0.10f + 0.30f * _amount;
                    _mpb.SetColor("_Color", gc);
                    _ground.SetPropertyBlock(_mpb);
                }
            }
        }

        static Material SilhouetteMaterial()
        {
            if (_silhouette) return _silhouette;
            _silhouette = Resources.Load<Material>("Materials/BossAuraSilhouette");
            if (!_silhouette)
            {
                var sh = Shader.Find("GameJam/BossAuraSilhouette");
                if (sh) _silhouette = new Material(sh) { name = "BossAuraSilhouette(runtime)" };
            }
            return _silhouette;
        }

        static Material GlowMaterial()
        {
            if (_glow) return _glow;
            _glow = Resources.Load<Material>("Materials/BossAuraGlow");
            if (!_glow)
            {
                var sh = Shader.Find("GameJam/BossAuraGlow");
                if (sh) _glow = new Material(sh) { name = "BossAuraGlow(runtime)" };
            }
            return _glow;
        }
    }
}
