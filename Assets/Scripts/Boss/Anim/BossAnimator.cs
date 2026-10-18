using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;
using UnityEngine.Rendering;

namespace GameJam.Boss
{
    /// <summary>
    /// 无名夜王：刚性肢体关键姿势 + 披风蒙皮骨链的确定性弹簧。
    /// 美术本地 +X 朝前，原画长臂朝下；所有角度均为绑定姿势的 Z 轴增量。
    /// 不移动 AI 根节点，不创建骨、不改骨名、不依赖 Animator / ExecuteAlways。
    /// 编辑器录制：赋引用，在绑定姿势调用 Initialize，再用 Tick(dt) 推进。
    ///
    /// 选择与限制：impacts 非空时直接作为发力关键帧的时间锚；空数组仅播放设计节奏，
    /// 不产生判定事件。null、未知招式、负数/非有限/非严格递增时间抛出参数异常。
    /// 可传任意判定数（光束可多段判定），事件绝不凭空补发。前摇随相邻时间锚拉伸。
    /// 打断不发旧 OnMoveEnd；自然结束只发一次。回调可 Play / Stagger / Die，旧 Tick
    /// 剩余时间丢弃，绝不推进新招；回调中的递归 Tick 被忽略。事件逐订阅者派发并检查版本。
    /// 非伤害动作的视觉时长采用设计值；外部给出更晚 impacts 时自动延长。
    /// 升空结束保留高空姿势直到下一招；力竭结束保留跪姿直到下一招。
    /// MPB 仅合成 _Flash/_FlashColor，外部闪白优先；同一属性同值的外部写入无法区分，
    /// 推荐 Unit 在 LateUpdate 最后写闪白。死亡只修改颜色 alpha，不克隆或修改共享材质。
    /// 这里不创建伤害、光束、位移、场地特效或修改碰撞器；这些归 AI / 特效后端。
    /// SpriteSkin 的绑定、Deform 与最终画面验收归 Builder，本类只驱动中文骨骼。
    /// </summary>
    [DefaultExecutionOrder(-8)]
    [DisallowMultipleComponent]
    public sealed class BossAnimator : MonoBehaviour, IBossAnimator
    {
        [Header("绑定骨（中文骨名；本地 XY 为角色画面平面）")]
        public Transform visualRoot, pelvis, torso, neck, head;
        public Transform upperArm, forearm, palm, thigh, shin, foot, otherFoot;
        [Tooltip("爪1_1、爪1_2……；可平铺传入 3–5 根手指的全部节")]
        public Transform[] fingers = new Transform[0];
        [Header("二阶段后层骨（Builder 直接赋骨_引用；镜像代理父节点 scale.x=-1）")]
        public Transform rearUpperArm, rearForearm, rearPalm, rearThigh, rearShin, rearFoot;
        public Transform[] rearFingers = new Transform[0];
        [Tooltip("二阶段镜像肢体及补全躯干；颜色由 Builder 预先压暗，本类只控制显隐")]
        public SpriteRenderer[] phaseTwoLayers = new SpriteRenderer[0];
        [Tooltip("撕裂后的肩后披风；完整斗篷另填 intactCloakRenderers，显隐不移动 Renderer")]
        public SpriteRenderer[] tornCloakRenderers = new SpriteRenderer[0];
        public SpriteRenderer[] intactCloakRenderers = new SpriteRenderer[0];
        public BossCloakChain frontChain, backChain;
        [Tooltip("骨_斗篷_前_1…8，根到梢；由 Builder 绑定 SpriteSkin")]
        public Transform[] cloakFront = new Transform[8];
        [Tooltip("骨_斗篷_后_1…8，根到梢；二阶段均向身后垂落而不是横伸")]
        public Transform[] cloakBack = new Transform[8];
        public Transform[] spikes = new Transform[0];
        public Transform clawTip, eye, cloakCenter;
        public SpriteRenderer[] layers = new SpriteRenderer[0];
        public SpriteRenderer eyeRenderer;
        [Min(0.01f)] public float height = 5f;

        public event Action<string, int> OnImpact;
        /// <summary>表现层（BossMoveFx）用：出招时带上判定时间表。</summary>
        public event Action<string, float[]> OnPlayMove;
        /// <summary>走路落脚（0 = 前腿，1 = 后腿 / 斗篷下的脚）。BossMoveFx 用来放尘土和脚步声。</summary>
        public event Action<int> OnFootfall;
        public event Action<string> OnMoveEnd;
        public string CurrentMove { get; private set; }
        public float Elapsed { get; private set; }
        public float Duration { get; private set; }
        public bool IsDead { get; private set; }
        public Renderer[] FlashRenderers { get { Initialize(); return flashRenderers; } }
        public Transform ClawTip { get { Initialize(); return clawTip ? clawTip : palm; } }
        public Transform Eye { get { Initialize(); return eye ? eye : head; } }
        public Transform CloakCenter { get { Initialize(); return cloakCenter ? cloakCenter : torso; } }
        public float Height => Finite(height) && height > 0f ? height : 5f;

        struct Rest
        {
            public Vector3 position, scale;
            public Quaternion rotation;
        }
        struct Pose
        {
            public float x, y, body, chest, neck, head, shoulder, elbow, wrist;
            public float hip, knee, ankle, other, spread, curl, cloak, turn, eye;
            public float rearShoulder, rearElbow, rearWrist, rearHip, rearKnee, rearAnkle, rearSpread, rearCurl;
            public static Pose Lerp(Pose a, Pose b, float t)
            {
                return new Pose {
                    x = Mathf.LerpUnclamped(a.x, b.x, t), y = Mathf.LerpUnclamped(a.y, b.y, t),
                    body = Mathf.LerpUnclamped(a.body, b.body, t), chest = Mathf.LerpUnclamped(a.chest, b.chest, t),
                    neck = Mathf.LerpUnclamped(a.neck, b.neck, t), head = Mathf.LerpUnclamped(a.head, b.head, t),
                    shoulder = Mathf.LerpUnclamped(a.shoulder, b.shoulder, t), elbow = Mathf.LerpUnclamped(a.elbow, b.elbow, t),
                    wrist = Mathf.LerpUnclamped(a.wrist, b.wrist, t), hip = Mathf.LerpUnclamped(a.hip, b.hip, t),
                    knee = Mathf.LerpUnclamped(a.knee, b.knee, t), ankle = Mathf.LerpUnclamped(a.ankle, b.ankle, t),
                    other = Mathf.LerpUnclamped(a.other, b.other, t), spread = Mathf.LerpUnclamped(a.spread, b.spread, t),
                    curl = Mathf.LerpUnclamped(a.curl, b.curl, t), cloak = Mathf.LerpUnclamped(a.cloak, b.cloak, t),
                    turn = Mathf.LerpUnclamped(a.turn, b.turn, t), eye = Mathf.LerpUnclamped(a.eye, b.eye, t),
                    rearShoulder = Mathf.LerpUnclamped(a.rearShoulder, b.rearShoulder, t),
                    rearElbow = Mathf.LerpUnclamped(a.rearElbow, b.rearElbow, t),
                    rearWrist = Mathf.LerpUnclamped(a.rearWrist, b.rearWrist, t),
                    rearHip = Mathf.LerpUnclamped(a.rearHip, b.rearHip, t),
                    rearKnee = Mathf.LerpUnclamped(a.rearKnee, b.rearKnee, t),
                    rearAnkle = Mathf.LerpUnclamped(a.rearAnkle, b.rearAnkle, t),
                    rearSpread = Mathf.LerpUnclamped(a.rearSpread, b.rearSpread, t),
                    rearCurl = Mathf.LerpUnclamped(a.rearCurl, b.rearCurl, t)
                };
            }
        }
        struct PoseKey { public float time; public Pose pose; public int curve; }
        sealed class Spring
        {
            public Transform bone;
            public float angle, velocity, parentAngle, seed;
            public bool primed;
        }
        sealed class LayerState
        {
            public SpriteRenderer renderer;
            public Color baseColor, deathColor, previousColor, writtenColor;
            public float previousFlash, writtenFlash;
            public bool ownsFlash;
            public bool baseEnabled;
        }

        // Cached production mesh data: shoulder ceiling is measured on actual indexed
        // skin vertices, not an AABB or a made-up bone radius. No mesh asset is changed.
        sealed class CloakSurface
        {
            public SpriteRenderer renderer;
            public Transform[] bones;
            public Matrix4x4[] bind, matrices;
            public Vector3[] vertices;
            public BoneWeight[] weights;
            public ushort[] triangles;
        }
        float cloakShoulderDrop;
        readonly List<CloakSurface> cloakSurfaces = new List<CloakSurface>();
        void CacheCloakSurfaces(SpriteRenderer[] renderers)
        {
            if (renderers == null) return;
            foreach (var r in renderers)
            {
                if (!r || !r.sprite) continue;
                bool cached=false; foreach(var s in cloakSurfaces) if(s.renderer==r) cached=true;
                var skin=r.GetComponent<SpriteSkin>(); if(cached || !skin) continue;
                cloakSurfaces.Add(new CloakSurface {renderer=r, bones=skin.boneTransforms,
                    bind=r.sprite.GetBindPoses().ToArray(), matrices=new Matrix4x4[skin.boneTransforms.Length],
                    vertices=r.sprite.GetVertexAttribute<Vector3>(VertexAttribute.Position).ToArray(),
                    weights=r.sprite.GetVertexAttribute<BoneWeight>(VertexAttribute.BlendWeight).ToArray(),
                    triangles=r.sprite.triangles});
            }
        }
        void ConstrainCloakSurface()
        {
            if (!upperArm) return;
            float shoulder=transform.InverseTransformPoint(upperArm.position).y;
            float top=float.NegativeInfinity;
            foreach(var s in cloakSurfaces)
            {
                if(!s.renderer || !s.renderer.enabled) continue;
                for(int i=0;i<s.bones.Length;i++) s.matrices[i]=transform.worldToLocalMatrix*s.bones[i].localToWorldMatrix*s.bind[i];
                foreach(int i in s.triangles)
                {
                    var w=s.weights[i]; var v=s.vertices[i];
                    Vector3 p=s.matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0
                        +s.matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1
                        +s.matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2
                        +s.matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
                    top=Mathf.Max(top,p.y);
                }
            }
            // Restore this offset BEFORE the next solver tick, otherwise restoring the
            // bind pose would repeatedly lift the root and inject energy into the cloth.
            float nextDrop=Mathf.Max(0f,cloakShoulderDrop+top-shoulder+.002f);
            float lower=nextDrop-cloakShoulderDrop; cloakShoulderDrop=nextDrop;
            if(Mathf.Abs(lower)>.000001f) { frontChain?.Translate(Vector3.down*lower); backChain?.Translate(Vector3.down*lower); }
        }

        readonly Dictionary<Transform, Rest> rest = new Dictionary<Transform, Rest>();
        readonly List<PoseKey> keys = new List<PoseKey>();
        readonly List<Spring> springs = new List<Spring>();
        readonly List<LayerState> layerStates = new List<LayerState>();
        Renderer[] flashRenderers = new Renderer[0];
        MaterialPropertyBlock block;
        int FlashId, FlashColorId;
        float[] impactTimes = new float[0];
        int impactIndex, revision, facing = 1, phase = 1;
        bool initialized, ticking, heldKneel, runtimeTick;
        /// <summary>0..1：被裂隙吸入 / 吐出（横向压扁 + 染成深紫 + 淡出）。BossUltDirector 写。</summary>
        public float Vanish;
        double moveTime;
        float clock, gait, moveWeight, localSpeed, phaseOpen, staggerLeft, staggerLength;
        float deathElapsed, deathLength, telegraph;
        BossTelegraphAura aura;
        Color telegraphColor = Color.yellow;
        Vector3 worldVelocity;
        Pose entryPose, lastPose, heldPose, deathPose;

        void Awake() { Initialize(); }
        void Update() { runtimeTick = true; try { Tick(Time.deltaTime); } finally { runtimeTick = false; } }

        /// <summary>
        /// 可重复调用：已有骨的绑定姿势不会被当前动画污染；新增引用会被补缓存。
        /// 调整绑定姿势后请重建组件/预制体，而非在动画中重新捕获绑定姿势。
        /// </summary>
        public void Initialize()
        {
            if (!initialized)
            {
                FlashId = Shader.PropertyToID("_Flash");
                FlashColorId = Shader.PropertyToID("_FlashColor");
                block = new MaterialPropertyBlock();
                initialized = true;
            }
            // Builder 可在 AddComponent(Awake) 后赋字段，再次 Initialize，避免过早初始化锁死。
            Find(ref visualRoot, "视觉根"); Find(ref pelvis, "盆骨"); Find(ref torso, "躯干");
            Find(ref neck, "颈"); Find(ref head, "头"); Find(ref upperArm, "上臂");
            Find(ref forearm, "前臂"); Find(ref palm, "手掌"); Find(ref thigh, "大腿");
            Find(ref shin, "小腿"); Find(ref foot, "脚"); Find(ref otherFoot, "另一只脚");
            Find(ref clawTip, "爪尖"); Find(ref eye, "独眼"); Find(ref cloakCenter, "斗篷中心");
            Cache(visualRoot); Cache(pelvis); Cache(torso); Cache(neck); Cache(head);
            Cache(upperArm); Cache(forearm); Cache(palm); Cache(thigh); Cache(shin); Cache(foot); Cache(otherFoot);
            Cache(rearUpperArm); Cache(rearForearm); Cache(rearPalm);
            Cache(rearThigh); Cache(rearShin); Cache(rearFoot); Cache(rearFingers);
            Cache(fingers); Cache(cloakFront); Cache(cloakBack); Cache(spikes); Cache(eye);
            BindChain(ref frontChain, cloakFront); BindChain(ref backChain, cloakBack);
            AddSprings(spikes); AddSpring(head);
            if (layers == null || layers.Length == 0) layers = GetComponentsInChildren<SpriteRenderer>(true);
            if (!eyeRenderer && eye) eyeRenderer = eye.GetComponent<SpriteRenderer>();
            if (layers != null) foreach (var r in layers) AddLayer(r);
            CacheCloakSurfaces(intactCloakRenderers); CacheCloakSurfaces(tornCloakRenderers);
            AddLayer(eyeRenderer);
            if (phaseTwoLayers != null) foreach (var r in phaseTwoLayers) AddLayer(r);
            if (tornCloakRenderers != null) foreach (var r in tornCloakRenderers) AddLayer(r);
            if (intactCloakRenderers != null) foreach (var r in intactCloakRenderers) AddLayer(r);
            if (flashRenderers.Length != layerStates.Count)
            {
                flashRenderers = new Renderer[layerStates.Count];
                for (int i = 0; i < layerStates.Count; i++) flashRenderers[i] = layerStates[i].renderer;
            }
        }

        void Find(ref Transform bone, string name)
        {
            if (bone) return;
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t != transform && t.name == name) { bone = t; return; }
        }
        bool Owned(Transform t) => t && t != transform && t.IsChildOf(transform);
        void Cache(Transform t)
        {
            if (Owned(t) && !rest.ContainsKey(t)) rest.Add(t, new Rest {
                position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
        }
        void Cache(Transform[] bones) { if (bones != null) foreach (var b in bones) Cache(b); }
        void AddSprings(Transform[] bones) { if (bones != null) foreach (var b in bones) AddSpring(b); }
        void AddSpring(Transform bone)
        {
            if (!Owned(bone)) return;
            foreach (var s in springs) if (s.bone == bone) return;
            springs.Add(new Spring { bone = bone, seed = springs.Count * 1.731f });
            springs.Sort((a, b) => Depth(a.bone).CompareTo(Depth(b.bone)));
        }
        static int Depth(Transform t) { int n = 0; while (t) { n++; t = t.parent; } return n; }
        void AddLayer(SpriteRenderer renderer)
        {
            if (!renderer || !renderer.transform.IsChildOf(transform)) return;
            foreach (var s in layerStates) if (s.renderer == renderer) return;
            layerStates.Add(new LayerState { renderer = renderer, baseColor = renderer.color, baseEnabled = renderer.enabled });
        }
        static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        public void Play(string move, float[] impacts)
        {
            if (!KnownMove(move)) throw new ArgumentException("未知的 Boss 招式。", nameof(move));
            if (impacts == null) throw new ArgumentNullException(nameof(impacts));
            // 完整校验先于任何状态改变；拒绝无法表达后摇的极大时间，避免浮点溢出。
            for (int i = 0; i < impacts.Length; i++)
                if (!Finite(impacts[i]) || impacts[i] < 0f || impacts[i] > 86400f ||
                    (i > 0 && impacts[i] <= impacts[i - 1]))
                    throw new ArgumentException("判定时间必须有限、非负、严格递增，且不超过 86400 秒。", nameof(impacts));
            Initialize();
            if (IsDead) return;
            revision++;
            entryPose = lastPose;
            heldPose = new Pose(); heldKneel = false;
            staggerLeft = 0f;
            CurrentMove = move; Elapsed = 0f; moveTime = 0d; impactIndex = 0;
            impactTimes = (float[])impacts.Clone();
            BuildKeys(move, impacts.Length > 0 ? impactTimes : DefaultImpacts(move));
            Render(0f);
            OnPlayMove?.Invoke(move, (float[])impactTimes.Clone());
        }

        static bool KnownMove(string m)
        {
            switch (m)
            {
                case BossMoves.P1_Claw3: case BossMoves.P1_Lunge: case BossMoves.P1_LeapSlam:
                case BossMoves.P1_CloakSweep: case BossMoves.P1_Wrap: case BossMoves.Transform:
                case BossMoves.P2_SpinThrust: case BossMoves.P2_Claw5: case BossMoves.P2_EyeBeam:
                case BossMoves.Ult_Rise: case BossMoves.Ult_Cleave: case BossMoves.Ult_Exhausted: return true;
                default: return false;
            }
        }
        static float[] DefaultImpacts(string m)
        {
            switch (m)
            {
                case BossMoves.P1_Claw3: return new[] { .55f, 1.05f, 1.9f };
                case BossMoves.P1_Lunge: return new[] { .85f };
                case BossMoves.P1_LeapSlam: return new[] { 1.4f };
                case BossMoves.P1_CloakSweep: return new[] { 1f };
                case BossMoves.P1_Wrap: return new[] { 1.2f };
                case BossMoves.P2_SpinThrust: return new[] { .6f, 1.3f };
                case BossMoves.P2_Claw5: return new[] { .4f, .75f, 1f, 1.45f, 2.2f };
                case BossMoves.P2_EyeBeam: return new[] { .8f, 2.3f };
                case BossMoves.Ult_Cleave: return new[] { .6f };
                default: return new float[0];
            }
        }

        void Key(float t, Pose p, int curve = 0)
        {
            // Dense/retimed anchors can interleave anticipation keys. Equal-time impact wins.
            int index = keys.Count;
            while (index > 0 && keys[index-1].time > t) index--;
            keys.Insert(index, new PoseKey { time = t, pose = p, curve = curve });
        }
        void BuildKeys(string m, float[] anchors)
        {
            keys.Clear();
            Key(0f, entryPose);
            if (m == BossMoves.Transform || m == BossMoves.Ult_Rise || m == BossMoves.Ult_Exhausted)
            {
                // P2 owns combat timing; P3 may observe but must not advance the combat clock.
                float length = m == BossMoves.Transform ? 2.5f : m == BossMoves.Ult_Rise ? 1f : 3f;
                if (anchors.Length > 0) length = Mathf.Max(length, anchors[anchors.Length - 1] + .3f);
                if (m == BossMoves.Transform)
                {
                    Key(.2f, new Pose { y = -.035f, body = 10f, chest = 12f, shoulder = 25f, elbow = 45f, cloak = -12f });
                    Key(length * .58f, new Pose { y = .045f, body = -10f, chest = -16f, head = -12f, shoulder = 110f, elbow = 25f, rearShoulder = 95f, rearElbow = 28f, rearHip = -18f, rearKnee = 20f, cloak = 30f, eye = 1f, spread = 30f });
                    Key(length, new Pose { rearShoulder = 25f, rearElbow = 20f, rearHip = -8f, cloak = 0f, eye = 1f });
                }
                else if (m == BossMoves.Ult_Rise)
                {
                    // Claude 2026-10-18：捂住半边脸狂笑 → 越笑越后仰 → 被身后的裂隙吸进去（压扁由 Vanish 做，BossUltDirector 同步）
                    Pose cover = new Pose { body = 2f, chest = 4f, neck = 2f, head = -6f, shoulder = 140f, elbow = 95f, wrist = 38f,
                                            curl = 28f, spread = 30f, cloak = 10f, eye = 1f };
                    Key(.18f, new Pose { y = -.02f, body = 4f, chest = 6f, head = 8f, shoulder = 120f, elbow = 80f, wrist = 10f, curl = 10f, eye = .5f });
                    Key(.32f, cover);
                    // Claude 2026-10-18 v2：用户把笑声改成“比较绝望的叹息”——吸气 0.32→0.62 s 肩胸抬起、头微仰；
                    // 0.62→1.1 s 长长呼出：胸口塌下去、肩落、头慢慢往后仰着垂下，捂脸的手指收紧
                    Pose inhale = cover; inhale.chest += 9f; inhale.neck += 3f; inhale.head += 6f; inhale.y += .012f; inhale.shoulder += 6f; inhale.curl += 6f; inhale.cloak = 18f;
                    Key(.62f, inhale, 2);
                    Pose exhale = cover; exhale.chest -= 6f; exhale.body -= 3f; exhale.neck += 6f; exhale.head += 12f; exhale.y -= .015f; exhale.shoulder -= 4f; exhale.curl += 14f; exhale.cloak = 30f;
                    Key(1.02f, exhale, 2);
                    Pose lean = cover;
                    lean.x = -.03f; lean.body = 6f; lean.chest = 6f; lean.neck = 8f; lean.head = 16f; lean.cloak = 50f; lean.spread = 42f;
                    Key(1.15f, lean); Key(length, lean);
                }
                else
                {
                    Pose kneel = Kneel();
                    Key(length * .08f, kneel);
                    Pose breath = kneel; breath.chest += 8f; breath.head -= 8f; breath.y += .018f;
                    Key(length * .45f, breath); Key(length * .7f, kneel); Key(length, kneel);
                }
                Duration = length;
                return;
            }
            float previous = 0f;
            for (int i = 0; i < anchors.Length; i++)
            {
                float hit = anchors[i], gap = hit - previous;
                Pose wind, strike;
                StrikePoses(m, i, anchors.Length, out wind, out strike);
                if (gap > 0f)
                {
                    // 延迟刀的前 72% 保持抬臂蓄势；最后 28% 才发力，终点就是判定点。
                    float prep = Mathf.Max(previous, hit - Mathf.Clamp(gap * .28f, .1f, .3f));
                    Key(previous + Mathf.Min(.22f, (prep-previous)*.5f), wind);
                    if (m == BossMoves.P1_LeapSlam || (m == BossMoves.P2_Claw5 && i == anchors.Length - 1))
                    {
                        Key(previous + gap * .22f, new Pose { y = -.1f, shoulder = 70f, elbow = 50f, hip = -25f, knee = 45f });
                        Key(previous + gap * .48f, wind);
                    }
                    if (m == BossMoves.Ult_Cleave && i == 0)
                        Key(previous + gap * .08f, new Pose { y = .18f, body = 10f, shoulder = 170f, elbow = 20f, cloak = 38f });
                    Key(prep, wind);
                }
                Key(hit, strike, 1);
                float nextGap = i + 1 < anchors.Length ? anchors[i + 1] - hit : Recovery(m);
                Pose overshoot = strike;
                overshoot.body += strike.body < 0f ? -6f : 6f;
                overshoot.chest += strike.chest < 0f ? -5f : 5f;
                overshoot.head -= 5f; overshoot.y -= .012f;
                Key(hit + Mathf.Min(.08f, nextGap * .16f), overshoot, 2);
                previous = hit + Mathf.Min(.08f, nextGap * .16f);
            }
            float last = anchors.Length > 0 ? anchors[anchors.Length - 1] : 0f;
            float tail = Recovery(m);
            if (m == BossMoves.P1_Wrap)
            {
                // 抓住后包裹两秒，随后甩出；只有显式 impacts 会发游戏判定。
                Pose grip = new Pose { body = -9f, shoulder = 80f, elbow = 55f, curl = 35f, cloak = -60f };
                Key(last + .18f, grip); Key(last + 1.8f, grip);
                Key(last + 2f, new Pose { body = -18f, shoulder = 105f, elbow = 8f, cloak = 55f, spread = 32f });
            }
            float end = last + tail;
            if (m == BossMoves.P2_EyeBeam)
            {
                // 横扫 120 度：颈与头分别承担 35/25 度，避免单个关节过折。
                float sweepEnd = Mathf.Max(last + .12f, anchors[0] + 1.5f);
                Key(sweepEnd, new Pose { neck = 35f, head = 25f, eye = 1f, shoulder = 82f, elbow = 20f, rearShoulder = 82f, rearElbow = 20f, rearSpread = 25f });
                end = Mathf.Max(end, sweepEnd + .4f);
            }
            Duration = end;
            Key(Duration, new Pose());
        }
        static float Recovery(string m)
        {
            switch (m)
            {
                case BossMoves.P1_Wrap: return 2.55f;
                case BossMoves.P1_LeapSlam: case BossMoves.Ult_Cleave: return .85f;
                case BossMoves.P2_EyeBeam: return .5f;
                default: return .55f;
            }
        }
        static Pose Kneel() => new Pose { y = -.19f, body = -12f, chest = -18f, head = 14f,
            shoulder = 22f, elbow = 35f, hip = -48f, knee = 88f, ankle = -30f, other = 25f, cloak = -12f };

        static void StrikePoses(string m, int index, int count, out Pose wind, out Pose strike)
        {
            bool down = (m == BossMoves.P1_Claw3 || m == BossMoves.P2_Claw5) && index == count - 1;
            bool reverse = (index & 1) != 0;
            wind = new Pose { x = -.025f, body = 7f, chest = 10f, shoulder = reverse ? 125f : 155f,
                elbow = 55f, wrist = -12f, spread = 20f, cloak = -10f };
            strike = new Pose { x = .045f, body = -12f, chest = -15f, shoulder = reverse ? 140f : 65f,
                elbow = reverse ? 22f : 12f, wrist = 8f, spread = 35f, cloak = 20f };
            if (reverse) { wind.shoulder = 45f; wind.elbow = 25f; wind.chest = -8f; strike.chest = 12f; }
            if (down)
            {
                wind = new Pose { body = 9f, chest = 10f, shoulder = 178f, elbow = 48f, wrist = -18f, spread = 25f, cloak = 22f };
                strike = new Pose { y = -.08f, body = -19f, chest = -18f, shoulder = 28f, elbow = 8f, wrist = 10f, hip = -18f, knee = 32f, spread = 38f, cloak = 32f };
                if (m == BossMoves.P2_Claw5) { wind.y = .25f; Rear(ref wind); Rear(ref strike); }
                return;
            }
            switch (m)
            {
                case BossMoves.P1_Lunge:
                    wind = new Pose { y = -.17f, body = -18f, chest = 12f, shoulder = 18f, elbow = 78f, hip = -28f, knee = 42f, cloak = -22f };
                    strike = new Pose { x = .075f, y = -.045f, body = -18f, chest = -8f, shoulder = 100f, elbow = 5f, wrist = -6f, hip = 18f, knee = 12f, other = -25f, spread = 24f, cloak = 32f };
                    break;
                case BossMoves.P1_LeapSlam:
                case BossMoves.Ult_Cleave:
                    wind = new Pose { y = m == BossMoves.Ult_Cleave ? .18f : .36f, body = 6f, shoulder = 175f, elbow = 40f, hip = 30f, knee = 50f, cloak = 35f, spread = 30f };
                    strike = new Pose { y = -.13f, body = -22f, chest = -14f, shoulder = 20f, elbow = 8f, hip = -32f, knee = 60f, ankle = -22f, cloak = 45f, spread = 40f };
                    break;
                case BossMoves.P1_CloakSweep:
                    wind = new Pose { body = 15f, chest = 18f, shoulder = 110f, elbow = 40f, cloak = -28f, turn = -.8f };
                    strike = new Pose { body = -8f, chest = -18f, shoulder = 100f, elbow = 12f, cloak = 100f, spread = 30f, turn = 1f };
                    break;
                case BossMoves.P1_Wrap:
                    wind = new Pose { y = -.07f, body = 6f, shoulder = 125f, elbow = 60f, cloak = 65f, spread = 30f };
                    strike = new Pose { body = -12f, shoulder = 90f, elbow = 45f, cloak = -65f, curl = 32f };
                    break;
                case BossMoves.P2_SpinThrust:
                    if (index == 0)
                    {
                        wind.turn = -.9f; wind.cloak = -28f;
                        strike.turn = 1f; strike.cloak = 85f; strike.shoulder = 95f;
                    }
                    else
                    {
                        wind = new Pose { body = 10f, chest = 12f, shoulder = 25f, elbow = 80f, cloak = 22f };
                        strike = new Pose { x = .07f, body = -18f, chest = -15f, shoulder = 105f, elbow = 4f, spread = 30f, cloak = 45f };
                    }
                    break;
                case BossMoves.P2_EyeBeam:
                    // 每个分段 impacts 对应扫到的确切方向，不重置蓄光。
                    float u = count <= 1 ? 0f : (float)index / (count - 1);
                    wind = new Pose { neck = Mathf.Lerp(-35f, 35f, u), head = Mathf.Lerp(-25f, 25f, u), eye = 1f, shoulder = 35f, elbow = 30f, cloak = 25f };
                    wind.shoulder = 82f; wind.elbow = 18f;
                    strike = wind; strike.chest = -5f; strike.spread = 25f;
                    break;
            }
            if (m == BossMoves.P2_Claw5)
            {
                if (reverse)
                {
                    Rear(ref wind); Rear(ref strike);
                    wind.shoulder = 18f; wind.elbow = 60f;
                    strike.shoulder = 20f; strike.elbow = 55f;
                }
                else { wind.rearShoulder = 20f; wind.rearElbow = 55f; strike.rearShoulder = 18f; strike.rearElbow = 60f; }
            }
            if (m == BossMoves.P2_SpinThrust || m == BossMoves.P2_EyeBeam) { Rear(ref wind); Rear(ref strike); }
        }

        // Rear arm shares the pose/interpolation system; no second animator or event schedule.
        static void Rear(ref Pose p)
        {
            p.rearShoulder = p.shoulder; p.rearElbow = p.elbow; p.rearWrist = p.wrist;
            p.rearSpread = p.spread; p.rearCurl = p.curl;
            p.rearHip = -p.hip * .55f; p.rearKnee = p.knee * .65f; p.rearAnkle = p.ankle;
        }
        public void SetLocomotion(Vector3 velocity)
        {
            if (!Finite(velocity.x) || !Finite(velocity.y) || !Finite(velocity.z))
                throw new ArgumentException("移动速度必须有限。", nameof(velocity));
            worldVelocity = new Vector3(velocity.x, 0f, velocity.z);
        }
        public void SetFacing(int side)
        {
            if (side != -1 && side != 1) throw new ArgumentOutOfRangeException(nameof(side), "朝向只能为 -1 或 +1。");
            Initialize();
            if (facing == side) return;
            facing = side;
            if (frontChain) frontChain.MirrorAt(visualRoot ? visualRoot.position : transform.position);
            if (backChain) backChain.MirrorAt(visualRoot ? visualRoot.position : transform.position);
            foreach (var s in springs) { s.primed = false; s.velocity = 0f; }
            Render(0f);
        }
        public void SetPhase(int value)
        {
            if (value != 1 && value != 2) throw new ArgumentOutOfRangeException(nameof(value));
            Initialize(); phase = value; Render(0f);
        }
        public void SetTelegraph(float amount, Color color)
        {
            if (!Finite(amount) || !Finite(color.r) || !Finite(color.g) || !Finite(color.b) || !Finite(color.a))
                throw new ArgumentException("预警强度与颜色必须有限。");
            Initialize(); telegraph = Mathf.Clamp01(amount); telegraphColor = color;
            // 预警不再把整只 Boss 涂成纯色：原画只吃 0.35 的 Flash，外圈交给剪影副本 + 贴地柔光。
            // 运行时才建表现对象；编辑器里的骨骼 / MPB 校验保持无副作用。
            if (Application.isPlaying)
            {
                if (telegraph > 0.001f)
                {
                    if (!aura) aura = BossTelegraphAura.Attach(this);
                    aura.Show(telegraph, telegraphColor);
                }
                else if (aura) aura.Hide();
            }
            ApplyLayers();
        }
        public void Stagger(float seconds)
        {
            if (!Finite(seconds) || seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            Initialize(); if (IsDead) return;
            revision++; CurrentMove = null; impactTimes = new float[0];
            entryPose = lastPose; heldPose = new Pose(); heldKneel = false;
            staggerLength = seconds; staggerLeft = seconds;
            Elapsed = 0f; Duration = seconds;
            Render(0f);
        }
        public void Die(float seconds)
        {
            if (!Finite(seconds) || seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            Initialize(); if (IsDead) return;
            revision++; IsDead = true; CurrentMove = null; impactTimes = new float[0];
            deathPose = lastPose; deathElapsed = 0f; deathLength = seconds;
            Elapsed = 0f; Duration = seconds; staggerLeft = 0f; telegraph = 0f;
            foreach (var s in layerStates) if (s.renderer) s.deathColor = s.renderer.color;
            Render(0f);
        }

        /// <summary>
        /// 不依赖 Time.time / Random；同一调用序列确定性。零 dt 仍采样并派发 t=0 判定。
        /// 大 dt 不漏事件：先精确推进到每个判定点，摆好骨骼，再调用订阅者。
        /// 弹簧以不超过 1/120s 的子步积分，不把长帧 dt 截断。
        /// </summary>
        public void Tick(float dt)
        {
            if (!Finite(dt) || dt < 0f || dt > 86400f) throw new ArgumentOutOfRangeException(nameof(dt), "单次推进必须在 0 到 86400 秒之间。");
            if (ticking) return;
            if (!initialized) Initialize();
            ticking = true;
            int version = revision;
            try
            {
                double remaining = dt;
                while (true)
                {
                    if (version != revision) return;
                    if (CurrentMove != null && impactIndex < impactTimes.Length && impactTimes[impactIndex] <= moveTime)
                    {
                        string move = CurrentMove;
                        int index = impactIndex++;
                        Render(0f);
                        if (!EmitImpact(move, index, version)) return;
                        continue;
                    }
                    if (CurrentMove != null && moveTime >= Duration)
                    {
                        string ended = CurrentMove;
                        heldKneel = ended == BossMoves.Ult_Exhausted;
                        heldPose = ended == BossMoves.Ult_Rise || ended == BossMoves.Ult_Exhausted ? lastPose : new Pose();
                        if (ended == BossMoves.Transform) { phase = 2; phaseOpen = 1f; }
                        CurrentMove = null;
                        if (!EmitEnd(ended, version)) return;
                        continue;
                    }
                    if (remaining <= 0f) { Render(0f); return; }
                    double step = Math.Min(remaining, 1d / 120d);
                    if (CurrentMove != null)
                    {
                        step = Math.Min(step, Duration - moveTime);
                        if (impactIndex < impactTimes.Length) step = Math.Min(step, impactTimes[impactIndex] - moveTime);
                    }
                    Advance(step);
                    remaining = Math.Max(0d, remaining - step);
                    Render((float)step);
                }
            }
            finally { ticking = false; }
        }
        bool EmitImpact(string move, int index, int version)
        {
            var handlers = OnImpact;
            if (handlers == null) return revision == version;
            foreach (Action<string, int> handler in handlers.GetInvocationList())
            {
                if (revision != version) return false;
                handler(move, index);
            }
            return revision == version;
        }
        bool EmitEnd(string move, int version)
        {
            var handlers = OnMoveEnd;
            if (handlers == null) return revision == version;
            foreach (Action<string> handler in handlers.GetInvocationList())
            {
                if (revision != version) return false;
                handler(move);
            }
            return revision == version;
        }
        void Advance(double seconds)
        {
            float dt = (float)seconds;
            clock += dt;
            phaseOpen = Mathf.MoveTowards(phaseOpen, phase == 2 ? 1f : 0f, dt / .65f);
            float speed = new Vector2(worldVelocity.x, worldVelocity.z).magnitude;
            moveWeight = Mathf.MoveTowards(moveWeight, Mathf.Clamp01(speed / 3f), dt * 4f);
            localSpeed = Mathf.Lerp(localSpeed, Mathf.Clamp(worldVelocity.x * facing / 3f, -1f, 1f), 1f - Mathf.Exp(-8f * dt));
            // Claude 2026-10-18：步频由真实速度决定（一步 ≈ 身高 × 0.4，二阶段 0.42），脚不打滑；原来是固定的 2–8 rad/s 正弦，像小碎步
            float stepLen = Mathf.Max(.5f, Height * (phase == 2 ? .42f : .4f));
            float prevGait = gait;
            gait += dt * Mathf.Max(.8f, Mathf.PI * speed / stepLen) * (moveWeight > .02f ? 1f : 0f);
            if (moveWeight > .25f && CurrentMove == null && !IsDead && Mathf.Floor(gait / Mathf.PI) != Mathf.Floor(prevGait / Mathf.PI))
                OnFootfall?.Invoke(Mathf.FloorToInt(prevGait / Mathf.PI) & 1);   // 刚放下的那只：偶数段抬的是前腿
            if (IsDead) { deathElapsed = Mathf.Min(deathLength, deathElapsed + dt); Elapsed = deathElapsed; }
            else if (CurrentMove != null)
            {
                moveTime = Math.Min(Duration, moveTime + seconds);
                Elapsed = (float)moveTime;
            }
            else if (staggerLeft > 0f)
            {
                staggerLeft = Mathf.Max(0f, staggerLeft - dt);
                Elapsed = Mathf.Min(Duration, Elapsed + dt);
            }
        }
        Pose Sample()
        {
            if (IsDead)
            {
                float u = deathLength <= 0f ? 1f : Smooth(deathElapsed / deathLength);
                return Pose.Lerp(deathPose, new Pose { y = -.45f, body = -85f, chest = 0f, head = 8f,
                    shoulder = 12f, elbow = 65f, hip = 0f, knee = -30f, cloak = -55f, curl = 30f }, u);
            }
            if (staggerLeft > 0f)
            {
                float t = staggerLength - staggerLeft;
                float weight = Smooth(t / Mathf.Max(.000001f, Mathf.Min(.065f, staggerLength * .25f))) *
                    Smooth(staggerLeft / Mathf.Max(.000001f, Mathf.Min(.18f, staggerLength * .4f)));
                Pose recoil = new Pose { x = -.03f, y = -.04f, body = 14f, chest = 14f, head = 18f, shoulder = 32f, elbow = 55f, knee = 25f, cloak = 12f };
                return Pose.Lerp(Pose.Lerp(entryPose, new Pose(), Smooth(t / .12f)), recoil, weight);
            }
            if (CurrentMove == null) return heldPose;
            // 顺序扫描也正确处理 t=0 的发力帧：最后一个同时间关键帧优先。
            Pose p = keys[0].pose;
            float previous = keys[0].time;
            for (int i = 1; i < keys.Count; i++)
            {
                if (Elapsed < keys[i].time)
                {
                    float u = Mathf.Clamp01((Elapsed - previous) / Mathf.Max(.000001f, keys[i].time - previous));
                    float eased = keys[i].curve == 1 ? u*u*u : keys[i].curve == 2 ? 1f + 2.70158f*(u-1f)*(u-1f)*(u-1f) + 1.70158f*(u-1f)*(u-1f) : Smooth(u);
                    return Pose.Lerp(p, keys[i].pose, eased);
                }
                p = keys[i].pose; previous = keys[i].time;
            }
            return p;
        }

        void Render(float dt)
        {
            Pose p = Sample();
            if (!IsDead && (heldKneel || CurrentMove == BossMoves.Ult_Exhausted))
            { float breathing = Mathf.Sin(clock * 4.2f); p.chest += breathing * 3.5f; p.head -= breathing * 2f; p.y += breathing * .009f; p.eye = .2f + .8f * (.5f + .5f * Mathf.Sin(clock * 6.5f)); }
            lastPose = p;
            foreach (var pair in rest)
            {
                if (!pair.Key) continue;
                pair.Key.localPosition = pair.Value.position;
                pair.Key.localRotation = pair.Value.rotation;
                pair.Key.localScale = pair.Value.scale;
            }
            float idle = !IsDead && CurrentMove == null && staggerLeft <= 0f && heldPose.y == 0f ? 1f : 0f;
            float breath = Mathf.Sin(clock * 2.1f);
            float walk = Mathf.Sin(gait) * moveWeight * idle;
            // ---- 步态 v2（Claude 2026-10-18）。用户：“走路是究极外八，膝盖往关节外面撇，不符合人体结构”。
            // 原因：Boss 是正对镜头的纸片，在纸面里转大腿 / 小腿 = 把腿往身体两侧甩，不是往前迈；屈膝也就朝外撇了。
            // 正面走路的做法：腿几乎不转，两只脚轮流“抬起”——大腿绕髋竖直压缩（像膝盖朝镜头抬过来），
            // 膝盖微微内收、脚始终在膝盖正下方；重心在支撑腿上：骨盆往支撑侧平移 + 侧滚，胸反向滚让头保持水平；
            // 双脚着地时身体最低。手臂沉沉地垂着，只随重心轻晃，不在纸面里甩。
            float wk = moveWeight * idle, s1 = Mathf.Sin(gait);
            bool p2 = phase == 2;
            float liftA = wk * Mathf.Pow(Mathf.Max(0f, s1), 1.3f);      // 前腿（看得见的那条）
            float liftB = wk * Mathf.Pow(Mathf.Max(0f, -s1), 1.3f);     // 另一条（一阶段 = 斗篷下露出的脚）
            float stance = liftB - liftA;                                // + = 重心压在前腿
            float contact = wk * (1f - Mathf.Abs(s1));                   // 两脚都着地 → 1
            if (wk > 0f)
            {
                walk = 0f;                                               // 旧的纸面摆腿不用了
                p.body += stance * 2.4f;
                p.chest -= stance * 3.4f; p.head -= stance * 1.2f;
                p.chest += wk * (p2 ? -2.5f : 1.5f); p.head += wk * (p2 ? 2.5f : -3.5f);   // 姿态只给一点点：纸面里的“前倾”其实是侧倾
                p.shoulder += wk * (p2 ? 6f : 4f) - 2.5f * stance;
                p.elbow += wk * 16f; p.curl += wk * 22f; p.spread += wk * 8f;
                if (p2) { p.rearShoulder += wk * 4f + 2.5f * stance; p.rearElbow += wk * 18f; p.rearCurl += wk * 20f; }
            }
            if (visualRoot && rest.TryGetValue(visualRoot, out var root))
            {
                float bob = wk > 0f ? -.011f * contact + .004f * (1f - contact) : 0f;
                visualRoot.localPosition = root.position + new Vector3(p.x, p.y + idle * .006f * breath * (1f - wk) + bob, 0f) * Height;
                // 回旋用纸片宽度变化 + 躯干/披风拧转，不把站立人物在 XY 平面转成风车。
                float paper = 1f - .68f * Mathf.Abs(Mathf.Sin(p.turn * Mathf.PI));
                float squeeze = Mathf.Lerp(1f, .05f, Smooth(Vanish)), stretch = 1f + .1f * Smooth(Vanish);
                visualRoot.localScale = new Vector3(Mathf.Abs(root.scale.x) * facing * paper * squeeze, root.scale.y * stretch, root.scale.z);
            }
            Rotate(pelvis, p.body - idle * localSpeed * 5f);
            Rotate(torso, p.chest + idle * breath * 1.6f);
            Rotate(neck, p.neck); Rotate(head, p.head + idle * breath * 1.2f);
            Rotate(upperArm, p.shoulder + idle * (2f * breath - walk * 5f));
            // 肘始终向同一侧弯，直接线性角度插值，避免 Euler 最短路径突然翻折。
            Rotate(forearm, Mathf.Clamp(p.elbow, 0f, 95f)); Rotate(palm, p.wrist);
            // 大步：髋 ±26°，摆腿时（向前那半）膝盖抬起再踩下，脚掌落地前勾起
            float swingKnee = Mathf.Max(0f, -walk) * 26f;
            // 一阶段后半步会被斗篷挡住：摆幅往前偏（-12°..+28°），腿始终看得见
            // 抬脚：大腿微内收（膝盖往身体中线）、小腿反向把脚放回膝下；竖直压缩在下面 LiftLeg 里做
            // 二阶段两腿原画是 A 字大开（后腿尤其外撇）→ 往里收，让脚落在髋下面（随化形渐入，不跳变）
            float narrow = Reveal;
            Rotate(thigh, p.hip + walk * 20f - 4f * liftA - 4f * narrow); Rotate(shin, p.knee + swingKnee + 6f * liftA + 3f * narrow);
            Rotate(foot, p.ankle - walk * (wk > 0f ? 14f : 9f)); Rotate(otherFoot, p.other - walk * 16f);
            if (fingers != null)
                for (int i = 0; i < fingers.Length; i++)
                {
                    var finger = fingers[i];
                    if (!finger) continue;
                    // 以父骨关系识别第二节，不假定每根手指恰好两节。
                    bool joint = Array.IndexOf(fingers, finger.parent) >= 0;
                    float fan = fingers.Length <= 1 ? 0f : (float)i / (fingers.Length - 1) * 2f - 1f;
                    Rotate(finger, p.curl * (joint ? .75f : .4f) + p.spread * fan * (joint ? .25f : 1f));
                }
            float reveal = Reveal;
            ApplySprings(dt, IsDead ? .2f : 1f);
            // Mirrored root has a reflected translation, but its own Z rotation is not
            // inherited through that local reflection: negate the shoulder/hip root channel.
            Rotate(rearUpperArm, -(p.rearShoulder + idle * (20f - breath * 2f)));
            Rotate(rearForearm, Mathf.Clamp(p.rearElbow + idle * 22f, 0f, 95f)); Rotate(rearPalm, p.rearWrist);
            float rearKnee = Mathf.Max(0f, walk) * 22f;
            // 后腿骨 scale.x = -1（镜像），往里收 / 膝盖内收的角度符号与前腿相反
            Rotate(rearThigh, -p.rearHip + walk * 18f + 4f * liftB + 11f * narrow); Rotate(rearShin, p.rearKnee + rearKnee - 6f * liftB - 6f * narrow); Rotate(rearFoot, p.rearAnkle + walk * 8f + 4f * narrow);
            if (wk > 0f)
            {
                LiftLeg(thigh, liftA);
                if (p2) LiftLeg(rearThigh, liftB);
                else if (otherFoot && rest.TryGetValue(otherFoot, out var of)) otherFoot.localPosition = of.position + new Vector3(.03f * liftB, .22f * liftB, 0f);
                // 重心：骨盆往支撑腿那边挪一点
                if (pelvis && rest.TryGetValue(pelvis, out var pr)) pelvis.localPosition = pr.position + new Vector3(.045f * stance, 0f, 0f);
            }
            RearFingers(p);
            if (rearUpperArm && rest.TryGetValue(rearUpperArm, out var ra)) rearUpperArm.localScale = ra.scale * Mathf.Lerp(.6f, 1f, reveal);
            // Contact is visual-only: never move the AI root. Flatten the leading shin,
            // then lower the visual skeleton until that knee reaches the ground plane.
            float kneelWeight = heldKneel ? 1f : CurrentMove == BossMoves.Ult_Exhausted ? Smooth((Elapsed-.12f)/.3f) : 0f;
            if (kneelWeight > 0f && shin && foot && visualRoot)
            {
                // 全部在 Boss 根的局部坐标里算（套公告板后纸片不在世界 XY 平面；Claude 2026-10-18）
                Vector3 lower = transform.InverseTransformVector(foot.position-shin.position);
                float correction = Vector3.SignedAngle(lower,Vector3.right*facing,Vector3.forward);
                shin.rotation = Quaternion.AngleAxis(correction*kneelWeight,transform.forward)*shin.rotation;
                Vector3 pos = transform.InverseTransformPoint(visualRoot.position);
                pos.y += (.09f-transform.InverseTransformPoint(shin.position).y)*kneelWeight;
                visualRoot.position=transform.TransformPoint(pos);
                if(otherFoot) { var support=transform.InverseTransformPoint(otherFoot.position); support.y=Mathf.Lerp(support.y,.07f,kneelWeight);otherFoot.position=transform.TransformPoint(support); }
            }
            if(cloakFront.Length>0 && cloakFront[0]) cloakFront[0].position-=transform.TransformVector(Vector3.up*cloakShoulderDrop);
            if(cloakBack.Length>0 && cloakBack[0]) cloakBack[0].position-=transform.TransformVector(Vector3.up*cloakShoulderDrop);
            DriveCloak(cloakFront, frontChain, 1f, dt);
            DriveCloak(cloakBack, backChain, -1f, dt);
            if (eye && rest.TryGetValue(eye, out var eyeRest))
            {
                float glow = IsDead ? 0f : (heldKneel || CurrentMove == BossMoves.Ult_Exhausted ? p.eye : Mathf.Max(phaseOpen, p.eye));
                float blink = idle > 0f && clock % 4.7f > 4.56f ? .18f : 1f;
                eye.localScale = Vector3.Scale(eyeRest.scale, new Vector3(1f + glow * .3f, (1f + glow * .25f) * blink, 1f));
            }
            ApplyLayers();
            ConstrainCloakSurface();
        }
        /// <summary>抬脚：整条腿绕髋竖直压缩（骨骼都是轴对齐的，压 Y = 沿腿方向缩短，看起来像膝盖朝镜头抬起）。</summary>
        void LiftLeg(Transform hipBone, float lift)
        {
            if (!hipBone || lift <= 0f || !rest.TryGetValue(hipBone, out var r)) return;
            hipBone.localScale = new Vector3(r.scale.x, r.scale.y * (1f - .24f * lift), r.scale.z);
        }
        void Rotate(Transform bone, float angle)
        {
            if (bone && rest.TryGetValue(bone, out var r)) bone.localRotation = r.rotation * Quaternion.Euler(0f, 0f, angle);
        }
        float Reveal => CurrentMove == BossMoves.Transform ? Mathf.Max(phaseOpen, Smooth((Elapsed - .35f) / .6f)) : phaseOpen;
        void BindChain(ref BossCloakChain cloth, Transform[] bones)
        {
            if (bones == null || bones.Length < 2 || !bones[0]) return;
            if (!cloth)
            {
                foreach (var candidate in bones[0].GetComponents<BossCloakChain>())
                    if (candidate.bones != null && candidate.bones.Length > 0 && candidate.bones[0] == bones[0]) { cloth = candidate; break; }
                if (!cloth) cloth = bones[0].gameObject.AddComponent<BossCloakChain>();
            }
            // Saved prefabs already contain the component, so assigning space only when
            // adding a new chain silently left existing rigs simulating in world XY.
            cloth.space = transform;
            cloth.bones = bones; cloth.automatic = false; cloth.Initialize();
        }
        public void ResetCloakChains()
        {
            if (frontChain) frontChain.ResetChain();
            if (backChain) backChain.ResetChain();
        }
        void RearFingers(Pose p)
        {
            if (rearFingers == null) return;
            for (int i = 0; i < rearFingers.Length; i++)
            {
                var b = rearFingers[i]; if (!b) continue;
                bool joint = Array.IndexOf(rearFingers, b.parent) >= 0;
                float fan = rearFingers.Length <= 1 ? 0f : 2f*i/(rearFingers.Length-1)-1f;
                Rotate(b, p.rearCurl*(joint ? .75f : .4f)+p.rearSpread*fan*(joint ? .25f : 1f));
            }
        }
        void DriveCloak(Transform[] chain, BossCloakChain cloth, float sign, float dt)
        {
            if (chain == null || chain.Length < 2 || !chain[0] || !chain[1] || !cloth) return;
            float wrap = 0f;
            if (CurrentMove == BossMoves.P1_Wrap)
            {
                float hit = impactTimes.Length > 0 ? impactTimes[0] : 1.2f;
                wrap = Smooth((Elapsed-(hit-.25f))/.25f) * (1f-Smooth((Elapsed-hit-1.8f)/.3f));
            }
            // Only the shoulder and next segment are keyed. The six distal joints are simulated.
            // Keep the original drape at rest: the previous -77/-93 degree targets
            // rotated both panels ~20 degrees away from the source silhouette even in idle.
            bool resting = !IsDead && Reveal < .001f && CurrentMove == null;
            float bindAngle = -90f;
            if (rest.TryGetValue(chain[1], out var cr) && rest.TryGetValue(chain[0], out var rr)) {
                Vector3 axis = rr.rotation * cr.position;
                bindAngle = Mathf.Atan2(axis.y, axis.x)*Mathf.Rad2Deg;
            }
            float angle = Mathf.Lerp(bindAngle, -145f + sign*8f, Reveal);
            angle = Mathf.Lerp(angle, -22f + sign*15f, wrap);
            if (CurrentMove == BossMoves.P1_CloakSweep)
            {
                float hit = impactTimes.Length > 0 ? impactTimes[0] : 1f;
                // Sideways throw, then 100ms hold and a gravity-led return. The
                // existing p.turn compresses the paper; exchange sides at its narrow
                // +0.5 crossing, never rotate the cape through the overhead hemisphere.
                float throwWeight = Smooth((Elapsed-(hit-.35f))/.25f) * (1f-Smooth((Elapsed-hit-.1f)/.45f));
                float sideAngle = lastPose.turn < .5f ? -158f : -22f;
                angle = Mathf.Lerp(angle, sideAngle, throwWeight);
            }
            if (IsDead) angle = Mathf.Lerp(angle, -95f, deathLength <= 0f ? 1f : Smooth(deathElapsed/deathLength));
            if (rest.TryGetValue(chain[1], out var childRest) && rest.TryGetValue(chain[0], out var rootRest))
            {
                Vector3 axis = rootRest.rotation * childRest.position;
                Rotate(chain[0], angle - Mathf.Atan2(axis.y, axis.x)*Mathf.Rad2Deg);
                Rotate(chain[1], sign * wrap * 8f);
            }
            // Preserve the cut's transverse offsets in idle, but retain Verlet inertia
            // within a 2-degree target-shape envelope. Actions keep the free 35-degree chain.
            cloth.targetAngleLimit = resting ? 2f : 180f;
            cloth.gravity = 9f;
            // Pose.cloak used to be a dead channel. It now only modulates secondary
            // softness (including Wrap/SpinThrust); it is NOT an unbounded root angle.
            cloth.stiffness = resting ? .8f : IsDead ? .3f : Mathf.Lerp(.18f, .05f, Mathf.Clamp01(Mathf.Abs(lastPose.cloak)/65f));
            cloth.wrap = wrap; cloth.facing = facing; cloth.collapse = IsDead ? (deathLength <= 0f ? 1f : Smooth(deathElapsed/deathLength)) : 0f;
            // Animator owns runtime and manual integration; standalone chains can still use LateUpdate.
            cloth.Step(dt); // single owner, including runtime; no second LateUpdate integration
        }
        void ApplySprings(float dt, float weight)
        {
            foreach (var s in springs)
            {
                if (!s.bone || !rest.ContainsKey(s.bone)) continue;
                Vector3 direction = s.bone.parent ? transform.InverseTransformVector(s.bone.parent.TransformVector(Vector3.right)) : Vector3.right;
                float parent = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                if (!s.primed) { s.parentAngle = parent; s.primed = true; }
                if (dt > 0f)
                {
                    float delta = Mathf.Clamp(Mathf.DeltaAngle(s.parentAngle, parent), -45f, 45f);
                    s.angle -= delta * .32f;
                    float target = Mathf.Sin(clock * 2.3f + s.seed) * 2.2f * weight - localSpeed * moveWeight * 3f;
                    s.velocity += (-90f * (s.angle - target) - 14f * s.velocity) * dt;
                    s.angle = Mathf.Clamp(s.angle + s.velocity * dt, -18f, 18f);
                }
                s.parentAngle = parent;
                s.bone.localRotation *= Quaternion.Euler(0f, 0f, s.angle * weight);
            }
        }
        void ApplyLayers()
        {
            float fade = !IsDead ? 1f : deathLength <= 0f ? 0f : 1f - Smooth((deathElapsed / deathLength-.72f)/.28f);
            foreach (var state in layerStates)
            {
                var r = state.renderer;
                if (!r) continue;
                bool rear = phaseTwoLayers != null && Array.IndexOf(phaseTwoLayers, r) >= 0;
                bool torn = tornCloakRenderers != null && Array.IndexOf(tornCloakRenderers, r) >= 0;
                bool intact = intactCloakRenderers != null && Array.IndexOf(intactCloakRenderers, r) >= 0;
                if(IsDead && (torn || intact) && r.name != "另一只脚") r.sortingOrder=70;
                if (rear) r.enabled = Reveal > .001f;
                else if (torn) r.enabled = Reveal >= .5f;
                else if (intact) r.enabled = Reveal < .5f;
                if (IsDead)
                {
                    var c = state.deathColor; c.a *= fade;
                    if (r == eyeRenderer) c.a *= deathLength <= 0f ? 0f : 1f-Smooth(deathElapsed/(deathLength*.35f));
                    r.color = c;
                }
                else if (r == eyeRenderer)
                {
                    // 颜色保持原画；独眼通过尺寸与 MPB 提亮，不新增共享材质。
                    float glow = heldKneel || CurrentMove == BossMoves.Ult_Exhausted ? lastPose.eye : Mathf.Max(phaseOpen, lastPose.eye);
                    Color c = state.baseColor; c.r *= 1f + glow * .4f; c.g *= 1f + glow * .4f; c.b *= 1f + glow * .4f;
                    r.color = c;
                }
                if (!IsDead)
                {
                    Color c = r == eyeRenderer ? r.color : state.baseColor;
                    float v = Smooth(Vanish);
                    if (v > 0f) { var dark = new Color(.14f, .05f, .26f, c.a); c = Color.Lerp(c, dark, v * .85f); c.a *= 1f - Smooth((Vanish - .55f) / .45f); }
                    r.color = c;
                }
                r.GetPropertyBlock(block);
                var material = r.sharedMaterial;
                float existing = block.HasFloat(FlashId) ? block.GetFloat(FlashId) : material && material.HasProperty(FlashId) ? material.GetFloat(FlashId) : 0f;
                Color existingColor = block.HasColor(FlashColorId) ? block.GetColor(FlashColorId) : material && material.HasProperty(FlashColorId) ? material.GetColor(FlashColorId) : Color.white;
                // 仅当当前值仍是我们上次写的，才撤销旧叠加；否则把它视为外部的新值。
                if (state.ownsFlash && Mathf.Approximately(existing, state.writtenFlash) && existingColor == state.writtenColor)
                { existing = state.previousFlash; existingColor = state.previousColor; }
                state.previousFlash = existing; state.previousColor = existingColor;
                // 预警写进 _Flash 的上限：原画保留细节；Unit 的受击白闪（最高 0.85）仍优先。
                float amount = IsDead ? 0f : Mathf.Min(telegraph, 1f) * 0.15f;   // Claude：0.35 时整只 Boss 被染成黄/红，看不清原画
                Color color = telegraphColor;
                bool white = existing > 0f && existingColor.r >= .9f && existingColor.g >= .9f && existingColor.b >= .9f;
                float eyeGlow = !IsDead && r == eyeRenderer ? (heldKneel || CurrentMove == BossMoves.Ult_Exhausted ? lastPose.eye : Mathf.Max(phaseOpen, lastPose.eye)) * .35f : 0f;
                if (eyeGlow > amount) { amount = eyeGlow; color = new Color(.55f, .8f, 1f); }
                if (white || existing >= amount) { amount = existing; color = existingColor; }
                block.SetFloat(FlashId, amount); block.SetColor(FlashColorId, color);
                r.SetPropertyBlock(block);
                state.writtenFlash = amount; state.writtenColor = color; state.ownsFlash = true;
            }
        }
        void OnDisable()
        {
            // 释放本组件仍拥有的预警值，不 Clear() 整个 MPB，保留 Unit 的闪白和其他属性。
            if (block == null) return;
            foreach (var s in layerStates)
            {
                if (!s.renderer || !s.ownsFlash) continue;
                s.renderer.GetPropertyBlock(block);
                if (Mathf.Approximately(block.GetFloat(FlashId), s.writtenFlash) && block.GetColor(FlashColorId) == s.writtenColor)
                {
                    block.SetFloat(FlashId, s.previousFlash); block.SetColor(FlashColorId, s.previousColor);
                    s.renderer.SetPropertyBlock(block);
                }
                s.ownsFlash = false;
            }
        }
    }
}
