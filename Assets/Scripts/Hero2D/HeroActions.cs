using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.IK;

namespace GameJam
{
    // 寄居蟹主角的战斗动作层：在步态（-10）之后、弹簧骨（-5）和 IK（-2）之前，把动作姿势叠加到身体 / 上身 / IK 目标上。
    // 普通动作 = 关键帧表（角色本地坐标，单位同 PSB：100 像素 = 1），关键帧之间平滑插值，切换动作时 0.06 s 交叉淡入。
    // 缩壳翻滚 + 伸臂重拳是单独的程序化段落：其余图层隐藏只留壳，整只壳绕壳心滚动；随后机械臂顺着伸缩杆从壳口打出去。
    // 约定：角色本地 +X = 美术原图的“前方”（HeroFacing.Facing = +1 时是屏幕右侧）。
    [DefaultExecutionOrder(-8)]
    public class HeroActions : MonoBehaviour
    {
        public struct Pose
        {
            public Vector2 body; public float bodyRot, chest, squash, shrink;   // shrink = 上身（头、手臂）整体缩小，钻出壳用
            public Vector2 arm, hand, fwdLegs, backLegs;   // 机械臂 / 左手 IK 目标、前侧两腿 / 后侧两腿 IK 目标的偏移

            public static Pose Lerp(Pose a, Pose b, float t) => new Pose
            {
                body = Vector2.LerpUnclamped(a.body, b.body, t), bodyRot = Mathf.LerpUnclamped(a.bodyRot, b.bodyRot, t),
                chest = Mathf.LerpUnclamped(a.chest, b.chest, t), squash = Mathf.LerpUnclamped(a.squash, b.squash, t),
                shrink = Mathf.LerpUnclamped(a.shrink, b.shrink, t),
                arm = Vector2.LerpUnclamped(a.arm, b.arm, t), hand = Vector2.LerpUnclamped(a.hand, b.hand, t),
                fwdLegs = Vector2.LerpUnclamped(a.fwdLegs, b.fwdLegs, t), backLegs = Vector2.LerpUnclamped(a.backLegs, b.backLegs, t),
            };
        }

        public class Clip
        {
            public float hit = -1f;                // 判定时刻（秒），-1 = 无
            public readonly List<(float t, Pose p)> keys = new List<(float, Pose)>();
            public float Length => keys.Count > 0 ? keys[keys.Count - 1].t : 0f;
            public Clip K(float t, Pose p) { keys.Add((t, p)); return this; }
            public Pose Sample(float t)
            {
                if (t <= keys[0].t) return keys[0].p;
                for (int i = 1; i < keys.Count; i++)
                    if (t <= keys[i].t)
                    {
                        float u = (t - keys[i - 1].t) / Mathf.Max(keys[i].t - keys[i - 1].t, 1e-4f);
                        return Pose.Lerp(keys[i - 1].p, keys[i].p, u * u * (3f - 2f * u));
                    }
                return keys[keys.Count - 1].p;
            }
        }

        // 机械臂 IK 目标原位（钳子），左手原位 —— 写关键帧时用绝对坐标更直观，这里换成偏移
        static readonly Vector2 ArmRest = new Vector2(1.27f, 3.00f), HandRest = new Vector2(2.28f, 3.10f);
        static Vector2 Arm(float x, float y) => new Vector2(x, y) - ArmRest;
        static Vector2 Hand(float x, float y) => new Vector2(x, y) - HandRest;

        public static readonly Dictionary<string, Clip> Clips = BuildClips();

        static Dictionary<string, Clip> BuildClips()
        {
            var c = new Dictionary<string, Clip>();
            var zero = new Pose();
            // 普攻 1：钳子从身后上方抡下来横扫
            c["Sweep"] = new Clip { hit = 0.2f }
                .K(0f, zero)
                .K(0.12f, new Pose { body = new Vector2(-0.2f, 0.05f), bodyRot = 4, chest = 14, arm = Arm(-0.7f, 5.4f), hand = Hand(2.0f, 3.6f), squash = -0.03f })
                .K(0.20f, new Pose { body = new Vector2(0.4f, -0.1f), bodyRot = -4, chest = -10, arm = Arm(2.9f, 4.0f), hand = Hand(1.9f, 3.3f), fwdLegs = new Vector2(0.25f, 0f), squash = 0.05f })
                .K(0.30f, new Pose { body = new Vector2(0.35f, -0.18f), bodyRot = -5, chest = -14, arm = Arm(2.2f, 2.3f), hand = Hand(1.9f, 3.3f), fwdLegs = new Vector2(0.25f, 0f), squash = 0.04f })
                .K(0.58f, zero);
            // 普攻 2：钳子收到腰间，前腿一蹬直戳出去
            c["Thrust"] = new Clip { hit = 0.22f }
                .K(0f, zero)
                .K(0.14f, new Pose { body = new Vector2(-0.4f, -0.25f), bodyRot = 5, chest = 8, arm = Arm(0.1f, 3.9f), hand = Hand(2.4f, 3.4f), squash = 0.07f })
                .K(0.22f, new Pose { body = new Vector2(0.7f, 0.05f), bodyRot = -6, chest = -8, arm = Arm(3.1f, 3.9f), hand = Hand(1.7f, 3.2f), backLegs = new Vector2(-0.3f, 0f), squash = -0.05f })
                .K(0.34f, new Pose { body = new Vector2(0.55f, 0f), bodyRot = -4, chest = -6, arm = Arm(2.9f, 3.8f), hand = Hand(1.7f, 3.2f), backLegs = new Vector2(-0.3f, 0f) })
                .K(0.62f, zero);
            // 普攻 3（连招收尾）：身体后仰，前侧两条蟹腿抬起来砸地
            c["Stomp"] = new Clip { hit = 0.27f }
                .K(0f, zero)
                .K(0.18f, new Pose { body = new Vector2(-0.3f, 0.5f), bodyRot = 10, chest = 6, arm = Arm(0.4f, 4.4f), hand = Hand(2.0f, 4.2f), fwdLegs = new Vector2(0.6f, 1.5f), squash = -0.04f })
                .K(0.27f, new Pose { body = new Vector2(0.45f, -0.4f), bodyRot = -7, chest = -8, arm = Arm(2.0f, 2.6f), hand = Hand(2.4f, 2.9f), fwdLegs = new Vector2(1.2f, 0f), squash = 0.11f })
                .K(0.38f, new Pose { body = new Vector2(0.4f, -0.3f), bodyRot = -5, chest = -6, arm = Arm(2.0f, 2.7f), hand = Hand(2.4f, 2.9f), fwdLegs = new Vector2(1.2f, 0f), squash = 0.06f })
                .K(0.7f, zero);
            // 蓄力姿势（按住时由蓄力程度混入）：四腿压低、身体后缩、钳子高举
            c["ChargeHold"] = new Clip()
                .K(0f, new Pose { body = new Vector2(-0.45f, -0.7f), bodyRot = 9, chest = 12, arm = Arm(0.5f, 7.4f), hand = Hand(2.6f, 3.6f), squash = 0.09f });
            // 蓄力释放：弹起前扑，钳子砸下（PlayerController 同时把角色往前送）
            var hold = c["ChargeHold"].keys[0].p;
            c["Smash"] = new Clip { hit = 0.22f }
                .K(0f, hold)
                .K(0.10f, new Pose { body = new Vector2(0.8f, 1.4f), bodyRot = -10, chest = 4, arm = Arm(2.2f, 7.4f), hand = Hand(2.2f, 4.4f), fwdLegs = new Vector2(0.5f, 1.0f), backLegs = new Vector2(0.4f, 0.9f), squash = -0.1f })
                .K(0.22f, new Pose { body = new Vector2(1.3f, -0.45f), bodyRot = -12, chest = -16, arm = Arm(3.0f, 1.4f), hand = Hand(2.5f, 2.8f), fwdLegs = new Vector2(0.9f, 0f), backLegs = new Vector2(0.6f, 0f), squash = 0.13f })
                .K(0.40f, new Pose { body = new Vector2(1.1f, -0.35f), bodyRot = -9, chest = -12, arm = Arm(3.0f, 1.5f), hand = Hand(2.5f, 2.8f), fwdLegs = new Vector2(0.9f, 0f), backLegs = new Vector2(0.6f, 0f), squash = 0.06f })
                .K(0.78f, zero);
            // 格挡：（PlayerController 先让角色背对敌人）缩成一团，用背上的贝壳去挡
            c["ShellGuard"] = new Clip()
                .K(0f, zero)
                .K(0.07f, new Pose { body = new Vector2(-0.25f, -0.45f), bodyRot = 8, chest = 16, arm = Arm(0.7f, 3.6f), hand = Hand(1.6f, 3.6f), squash = 0.08f })
                .K(0.30f, new Pose { body = new Vector2(-0.2f, -0.4f), bodyRot = 7, chest = 14, arm = Arm(0.7f, 3.6f), hand = Hand(1.6f, 3.6f), squash = 0.06f })
                .K(0.48f, zero);
            // 处决：钳子夹住 → 举过头顶 → 甩砸下去
            c["Execute"] = new Clip { hit = 0.28f }
                .K(0f, zero)
                .K(0.12f, new Pose { body = new Vector2(0.45f, -0.2f), bodyRot = -5, chest = -6, arm = Arm(3.0f, 3.4f), fwdLegs = new Vector2(0.3f, 0f), squash = 0.03f })
                .K(0.22f, new Pose { body = new Vector2(-0.2f, 0.25f), bodyRot = 6, chest = 14, arm = Arm(1.6f, 7.3f), hand = Hand(2.4f, 4.2f), squash = -0.06f })
                .K(0.30f, new Pose { body = new Vector2(0.55f, -0.35f), bodyRot = -8, chest = -16, arm = Arm(3.0f, 1.2f), hand = Hand(2.4f, 2.9f), fwdLegs = new Vector2(0.4f, 0f), squash = 0.12f })
                .K(0.45f, new Pose { body = new Vector2(0.5f, -0.3f), bodyRot = -6, chest = -12, arm = Arm(3.0f, 1.3f), hand = Hand(2.4f, 2.9f), fwdLegs = new Vector2(0.4f, 0f), squash = 0.05f })
                .K(0.8f, zero);
            // 放置家具（约 1 s）：钳子伸到背后壳里掏 → 掏出“家具胶囊”举过头顶 → 身体前倾往前一抛（hit = 抛出的瞬间）→ 站稳
            c["Place"] = new Clip { hit = 0.56f }
                .K(0f, zero)
                .K(0.18f, new Pose { body = new Vector2(-0.15f, -0.25f), bodyRot = 6, chest = 14, arm = Arm(-0.9f, 4.6f), hand = Hand(1.6f, 3.8f), squash = 0.05f })
                .K(0.34f, new Pose { body = new Vector2(-0.25f, -0.15f), bodyRot = 9, chest = 18, arm = Arm(-1.2f, 4.9f), hand = Hand(1.5f, 3.9f), squash = 0.06f })
                .K(0.48f, new Pose { body = new Vector2(-0.2f, 0.25f), bodyRot = 4, chest = 6, arm = Arm(0.6f, 6.6f), hand = Hand(2.2f, 4.4f), backLegs = new Vector2(-0.15f, 0f), squash = -0.05f })
                .K(0.56f, new Pose { body = new Vector2(0.45f, -0.2f), bodyRot = -7, chest = -12, arm = Arm(3.3f, 4.4f), hand = Hand(2.4f, 3.2f), fwdLegs = new Vector2(0.3f, 0f), squash = 0.06f })
                .K(0.70f, new Pose { body = new Vector2(0.35f, -0.15f), bodyRot = -5, chest = -8, arm = Arm(3.0f, 4.0f), hand = Hand(2.4f, 3.2f), fwdLegs = new Vector2(0.3f, 0f), squash = 0.03f })
                .K(1.0f, zero);
            // 受击：往后一仰
            c["Hurt"] = new Clip()
                .K(0f, zero)
                .K(0.06f, new Pose { body = new Vector2(-0.45f, 0.1f), bodyRot = 11, chest = 13, arm = Arm(0.9f, 3.6f), hand = Hand(2.6f, 3.8f), squash = -0.05f })
                .K(0.32f, zero);
            // 伸臂重拳后从壳里钻出来（0.3 s，期间不能操作）：缩成一团 → 往上一弹 → 晃一下站稳
            c["Emerge"] = new Clip()
                .K(0f, new Pose { body = new Vector2(-0.1f, -0.9f), shrink = 0.55f, squash = 0.16f, arm = Arm(1.0f, 3.6f), hand = Hand(1.9f, 3.6f), fwdLegs = new Vector2(-0.7f, 0.9f), backLegs = new Vector2(0.7f, 0.9f) })
                .K(0.12f, new Pose { body = new Vector2(0f, 0.35f), shrink = -0.06f, squash = -0.1f, chest = -6, arm = Arm(1.6f, 3.4f), hand = Hand(2.6f, 3.6f), fwdLegs = new Vector2(-0.1f, 0.3f), backLegs = new Vector2(0.1f, 0.3f) })
                .K(0.20f, new Pose { body = new Vector2(0f, -0.12f), bodyRot = -3, chest = 5, squash = 0.05f })
                .K(0.30f, zero);
            // 被鱿鱼丢出去：空中往后翻仰、腿乱蹬 → 0.45 s 摔地（一压）→ 躺着蟹腿摊开 → 0.85–1.35 s 缓慢起身
            var lie = new Pose { body = new Vector2(-0.1f, -1.05f), bodyRot = 10, chest = 14, arm = Arm(2.6f, 2.4f), hand = Hand(3.2f, 2.5f),
                                 fwdLegs = new Vector2(1.4f, -0.05f), backLegs = new Vector2(-1.4f, -0.05f), squash = 0.06f };
            var landed = lie; landed.body = new Vector2(-0.1f, -1.15f); landed.squash = 0.16f;
            var lie2 = lie; lie2.chest = 10; lie2.bodyRot = 8;
            c["Thrown"] = new Clip()
                .K(0f, new Pose { body = new Vector2(0f, 1.0f), bodyRot = 6, chest = 8, arm = Arm(2.4f, 5.6f), hand = Hand(2.8f, 5.0f), fwdLegs = new Vector2(0.4f, 0.7f), backLegs = new Vector2(-0.4f, 0.7f) })
                .K(0.20f, new Pose { body = new Vector2(-0.4f, 2.0f), bodyRot = 28, chest = 18, arm = Arm(2.8f, 6.5f), hand = Hand(3.2f, 6.0f), fwdLegs = new Vector2(0.9f, 1.8f), backLegs = new Vector2(-0.6f, 1.9f), squash = -0.08f })
                .K(0.45f, landed)
                .K(0.56f, lie)
                .K(0.85f, lie2)
                .K(1.35f, zero);
            return c;
        }

        [Header("缩壳翻滚 / 伸臂重拳")]
        public float rollTurns = 1f;            // 翻滚整圈数（整数：落地时壳口正好朝前）
        public float rollHop = 0.35f;           // 翻滚时的弹跳高度（角色单位）
        public float punchMaxReach = 18f;       // 伸缩杆最长（角色单位）
        public Color rodDark = new Color(0.17f, 0.15f, 0.3f), rodLight = new Color(0.42f, 0.36f, 0.62f);

        public bool useUnscaledTime = true;
        public float intensity = 1.35f;         // 关键帧动作的整体幅度（身体位移 / 旋转 / 压扁）

        // ---- 状态
        Clip clip; float clipT = -1f;
        Pose fromPose; float fadeT = 1f;
        Pose lastPose;
        bool charging; float chargeLevel, chargeBlend, chargeT;
        bool struggling; float struggleT, struggleBlend, squeezeKick;
        HeroFace face;
        public HeroFace Face => face ? face : (face = GetComponent<HeroFace>());

        enum Shell { None, Roll, Hold, Punch }   // Hold = 滚完在壳里等出拳（避免身体闪现一帧）
        Shell shell;
        float shellT, shellLen; int rollDir;
        Vector3 punchTargetWorld; float punchHitT, punchLen;
        public bool PunchFired { get; private set; }

        // ---- 骨骼
        Transform body, chest, armUp, armLow, claw;
        Transform armTarget, handTarget;
        readonly List<Transform> fwdLegs = new List<Transform>(), backLegs = new List<Transform>();
        Vector3 armTargetRest, handTargetRest;
        Vector3 heroBasePos;
        SpriteRenderer shellLayer, armLayer, clawLayer;
        Vector3 shellCenterBody, openingBody;          // 壳心、壳口（身体根本地坐标，绑定姿势下量的）
        Vector3 chestRestPos;
        Solver2D armSolver;
        HeroFacing facing;
        HeroGait gait;
        LineRenderer rodDarkLine, rodLightLine;
        readonly List<SpriteRenderer> bodyRenderers = new List<SpriteRenderer>();
        bool ready;

        public float T => clipT;
        public bool Playing => clip != null && clipT >= 0f && clipT < clip.Length;
        public float Hit => clip != null ? clip.hit : -1f;
        public float Length => clip != null ? clip.Length : 0f;
        public bool InShell => shell != Shell.None;
        public bool IsInShell => shell != Shell.None;
        public float PunchHitTime => punchHitT;
        public float ShellTime => shellT;
        public bool ShellDone => shell == Shell.None;

        // 出拳时机械臂三节骨被直接摆过（肩部还被平移），IK 只管两节的旋转、不会复原肩部位置和钳子角度，收拳后要手动还原
        Vector3 armUpRestPos;
        Quaternion armUpRestRot, armLowRestRot, clawRestRot;

        /// <summary>机械臂肩部离原位多远（角色单位）+ 钳子角度偏差（度）：自测用，收拳后应≈0。</summary>
        public Vector2 ArmRestError => new Vector2(Vector3.Distance(armUp.localPosition, armUpRestPos),
                                                   claw ? Quaternion.Angle(claw.localRotation, clawRestRot) : 0f);

        void RestoreArm()
        {
            armUp.localPosition = armUpRestPos;
            armUp.localRotation = armUpRestRot;
            if (armLow) armLow.localRotation = armLowRestRot;
            if (claw) claw.localRotation = clawRestRot;
        }

        void Start()
        {
            facing = GetComponent<HeroFacing>();
            gait = GetComponent<HeroGait>();
            heroBasePos = transform.localPosition;
            var skinned = new HashSet<Transform>();
            var layers = new Dictionary<string, SpriteRenderer>();
            foreach (var sk in GetComponentsInChildren<UnityEngine.U2D.Animation.SpriteSkin>(true))
            {
                skinned.Add(sk.transform);
                var r = sk.GetComponent<SpriteRenderer>();
                if (r) layers[sk.name] = r;
            }
            var bones = new Dictionary<string, Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (!skinned.Contains(t) && !bones.ContainsKey(t.name)) bones[t.name] = t;
            Transform B(string n) => bones.TryGetValue(n, out var t) ? t : null;

            body = B("身体根"); chest = B("上身");
            armUp = B("机械臂_上"); armLow = B("机械臂_下"); claw = B("钳子");
            armTarget = B("机械臂_目标"); handTarget = B("左臂_目标");
            foreach (var n in new[] { "左前腿_目标", "左后腿_目标" }) if (B(n)) fwdLegs.Add(B(n));
            foreach (var n in new[] { "右前腿_目标", "右后腿_目标" }) if (B(n)) backLegs.Add(B(n));
            if (!body || !chest || !armUp || !armTarget) { Debug.LogWarning("[HeroActions] 缺骨，动作层停用"); enabled = false; return; }
            armTargetRest = armTarget.localPosition;
            armUpRestPos = armUp.localPosition; armUpRestRot = armUp.localRotation;
            armLowRestRot = armLow ? armLow.localRotation : Quaternion.identity;
            clawRestRot = claw ? claw.localRotation : Quaternion.identity;
            handTargetRest = handTarget ? handTarget.localPosition : Vector3.zero;
            chestRestPos = chest.localPosition;
            foreach (var s in GetComponentsInChildren<Solver2D>(true)) if (s.name == "IK_机械臂") armSolver = s;

            layers.TryGetValue("壳", out shellLayer);
            layers.TryGetValue("机械臂", out armLayer);
            layers.TryGetValue("手", out clawLayer);

            bodyRenderers.Clear();
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (r != shellLayer) bodyRenderers.Add(r);
            }

            if (shellLayer && shellLayer.sprite)
            {
                // 壳图：壳口在图的右侧（约 71% 宽、离底 40% 高），壳心取整张图的中心偏右一点
                var b = shellLayer.sprite.bounds;
                Vector3 P(float u, float v) => body.InverseTransformPoint(shellLayer.transform.TransformPoint(
                    new Vector3(Mathf.Lerp(b.min.x, b.max.x, u), Mathf.Lerp(b.min.y, b.max.y, v), 0f)));
                shellCenterBody = P(0.55f, 0.45f);
                openingBody = P(0.71f, 0.40f);
            }
            BuildRod();
            ready = true;
        }

        void BuildRod()
        {
            // 伸缩杆：两条 LineRenderer（深色杆身 + 浅色高光），贴图是程序生成的横向条纹，像一节节的金属套管
            var tex = new Texture2D(16, 4) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (int x = 0; x < 16; x++)
                for (int y = 0; y < 4; y++)
                    tex.SetPixel(x, y, x < 2 ? new Color(0.55f, 0.55f, 0.6f) : x < 13 ? Color.white : new Color(0.7f, 0.7f, 0.75f));
            tex.Apply();
            var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
            LineRenderer Make(string n, float w, Color col, int orderOffset)
            {
                var go = new GameObject(n);
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = false; lr.positionCount = 2;
                lr.widthMultiplier = w; lr.sharedMaterial = mat;
                lr.textureMode = LineTextureMode.Tile;
                lr.startColor = lr.endColor = col;
                lr.numCapVertices = 2;
                lr.alignment = LineAlignment.TransformZ;
                if (armLayer) { lr.sortingLayerID = armLayer.sortingLayerID; lr.sortingOrder = armLayer.sortingOrder - 2 + orderOffset; }
                lr.enabled = false;
                return lr;
            }
            rodDarkLine = Make("伸缩杆", 0.55f, rodDark, 0);
            rodLightLine = Make("伸缩杆_高光", 0.2f, rodLight, 1);
            if (shellLayer && armLayer && armLayer.sortingOrder - 2 <= shellLayer.sortingOrder)
            {   // 杆必须画在壳上面
                rodDarkLine.sortingOrder = shellLayer.sortingOrder + 1;
                rodLightLine.sortingOrder = shellLayer.sortingOrder + 2;
            }
        }

        // ------------------------------------------------------------------ API
        /// <summary>朝向：+1 = 屏幕右，-1 = 屏幕左。lockIt = 在 Unlock 之前不随移动自动转身。</summary>
        public void FaceScreen(int side, bool lockIt = false)
        {
            if (!facing) return;
            facing.lockSide = lockIt ? (side < 0 ? -1 : 1) : 0;
            facing.Face(side);
        }
        public void UnlockFacing() { if (facing) facing.lockSide = 0; }

        public void Play(string name, bool crossfade = true)
        {
            if (!ready || !Clips.TryGetValue(name, out var c)) return;
            if (name != "Emerge" && shell != Shell.None)
            {
                CancelShell();
            }
            charging = false; struggling = false;
            fromPose = lastPose; fadeT = crossfade ? 0f : 1f;
            clip = c; clipT = 0f;
        }

        public void Stop() { if (Playing || charging) { fromPose = lastPose; fadeT = 0f; } clip = null; clipT = -1f; charging = false; }

        /// <summary>被鱿鱼缠住：身体被提起来，四条腿乱蹬、手臂挣扎，直到 EndStruggle。</summary>
        public void BeginStruggle() { if (!ready) return; CancelShell(); Stop(); struggling = true; struggleT = 0f; struggleBlend = 0f; }
        public void EndStruggle() { if (struggling) { struggling = false; fromPose = lastPose; fadeT = 0f; } }
        /// <summary>每次被勒（扣血）时身体一缩。</summary>
        public void SqueezeKick() { squeezeKick = 1f; if (Face) Face.Squeeze(); }

        /// <summary>立即结束缩壳（被打断时），身体回来。</summary>
        public void CancelShell()
        {
            if (shell == Shell.None) return;
            shell = Shell.None;
            transform.localScale = Vector3.one; transform.localPosition = heroBasePos;
            HideBody(false);
        }

        public void BeginCharge() { if (!ready) return; Stop(); charging = true; chargeBlend = 0f; chargeT = 0f; }
        public void SetCharge(float level) => chargeLevel = Mathf.Clamp01(level);
        public void ReleaseCharge() { charging = false; Play("Smash"); }

        /// <summary>缩壳翻滚：dir = 屏幕上的滚动方向（+1 右 / -1 左），duration 与 PlayerController 的翻滚时长一致。</summary>
        public void ShellRoll(int screenDir, float duration)
        {
            if (!ready) return;
            Stop();
            shell = Shell.Roll; shellT = 0f; shellLen = duration; rollDir = screenDir < 0 ? -1 : 1;
            HideBody(true);
        }

        /// <summary>翻滚后从壳口伸出机械臂重拳。返回整段时长；PunchHitTime 时刻打中。</summary>
        public float ShellPunch(Vector3 targetWorld)
        {
            if (!ready) return 0f;
            if (shell == Shell.None) HideBody(true);
            shell = Shell.Punch; shellT = 0f;
            punchTargetWorld = targetWorld;
            punchHitT = 0.42f; punchLen = 1.0f;
            PunchFired = false;
            return punchLen;
        }

        public Vector3 PunchTipWorld => claw ? claw.position : transform.position;

        void HideBody(bool hide)
        {
            if (hide)
            {
                for (int i = 0; i < bodyRenderers.Count; i++)
                {
                    if (bodyRenderers[i] != null) bodyRenderers[i].enabled = false;
                }
            }
            else
            {
                for (int i = 0; i < bodyRenderers.Count; i++)
                {
                    if (bodyRenderers[i] != null) bodyRenderers[i].enabled = true;
                }
                if (rodDarkLine) rodDarkLine.enabled = rodLightLine.enabled = false;
                if (armSolver) armSolver.weight = 1f;
                chest.localPosition = chestRestPos;
                chest.localScale = Vector3.one;
                RestoreArm();
            }
        }

        // ------------------------------------------------------------------ update
        void LateUpdate()
        {
            if (!ready) return;
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (dt <= 0f) return;

            if (shell != Shell.None) { UpdateShell(dt); return; }

            Pose p = new Pose();
            if (struggling)
            {
                struggleT += dt;
                struggleBlend = Mathf.MoveTowards(struggleBlend, 1f, dt / 0.15f);
                squeezeKick = Mathf.MoveTowards(squeezeKick, 0f, dt * 4f);
                float t = struggleT;
                var st = new Pose
                {
                    body = new Vector2(Noise(t * 6f, 7) * 0.15f, 1.0f + 0.1f * Mathf.Sin(t * 9f) - 0.25f * squeezeKick),
                    bodyRot = Mathf.Sin(t * 7f) * 8f, chest = Mathf.Sin(t * 5.3f) * 10f + 6f * squeezeKick,
                    arm = Arm(2.4f + Mathf.Sin(t * 11f) * 0.6f, 5.8f + Mathf.Cos(t * 9f) * 0.8f),
                    hand = Hand(2.8f + Mathf.Sin(t * 10f + 1f) * 0.5f, 5.0f + Mathf.Cos(t * 12f) * 0.7f),
                    fwdLegs = new Vector2(0.4f + Mathf.Sin(t * 13f) * 0.5f, 0.6f + Mathf.Cos(t * 12f) * 0.4f),
                    backLegs = new Vector2(-0.4f + Mathf.Sin(t * 12f + 1f) * 0.5f, 0.6f + Mathf.Cos(t * 14f + 2f) * 0.4f),
                    squash = 0.12f * squeezeKick,
                };
                p = Pose.Lerp(new Pose(), st, struggleBlend);
            }
            else if (charging)
            {
                chargeT += dt;
                chargeBlend = Mathf.MoveTowards(chargeBlend, 1f, dt / 0.22f);
                var hold = Clips["ChargeHold"].keys[0].p;
                float e = chargeBlend * chargeBlend * (3f - 2f * chargeBlend);
                p = Pose.Lerp(new Pose(), hold, e * (0.55f + 0.45f * chargeLevel));
                // 越蓄越抖
                float amp = chargeLevel * chargeLevel;
                p.body += new Vector2(Noise(chargeT * 31f, 1), Noise(chargeT * 29f, 2)) * 0.09f * amp;
                p.arm += new Vector2(Noise(chargeT * 23f, 3), Noise(chargeT * 27f, 4)) * 0.16f * amp;
                p.chest += Noise(chargeT * 19f, 5) * 3f * amp;
            }
            else if (clip != null && clipT >= 0f)
            {
                clipT += dt;
                p = clip.Sample(clipT);
                if (clipT >= clip.Length) { clip = null; clipT = -1f; }
            }
            if (fadeT < 1f)
            {
                fadeT = Mathf.Min(1f, fadeT + dt / 0.06f);
                p = Pose.Lerp(fromPose, p, fadeT);
            }
            lastPose = p;
            Apply(p);
        }

        static float Noise(float t, int seed) => Mathf.PerlinNoise(t, seed * 13.7f) * 2f - 1f;

        void Apply(Pose p)
        {
            body.localPosition += (Vector3)p.body * intensity;
            body.localRotation *= Quaternion.Euler(0, 0, p.bodyRot * intensity);
            chest.localRotation *= Quaternion.Euler(0, 0, p.chest * intensity);
            chest.localScale = Vector3.one * (1f - p.shrink);   // 只有关键帧动作会写；缩壳时由 HideBody 复位
            // 压扁 / 拉伸：缩放整个角色实例，保持脚底不动（实例原点在 heroBasePos，脚底在原点的 -heroBasePos 处）
            float sq = p.squash * intensity;
            var sc = new Vector3(1f + sq * 0.6f, 1f - sq, 1f);
            transform.localScale = sc;
            transform.localPosition = new Vector3(heroBasePos.x * sc.x, heroBasePos.y * sc.y, heroBasePos.z);
            armTarget.localPosition = armTargetRest + (Vector3)p.arm;
            if (handTarget) handTarget.localPosition = handTargetRest + (Vector3)p.hand;
            foreach (var l in fwdLegs) l.localPosition += (Vector3)p.fwdLegs;
            foreach (var l in backLegs) l.localPosition += (Vector3)p.backLegs;
        }

        // ------------------------------------------------------------------ 缩壳 / 重拳
        void UpdateShell(float dt)
        {
            shellT += dt;
            if (shellT > 2.5f)
            {
                CancelShell();
                return;
            }
            transform.localScale = Vector3.one;
            transform.localPosition = heroBasePos;
            // 壳心的“原位”（角色本地坐标）
            var restRot = body.localRotation;            // 步态这一帧写的旋转（静止时≈原始）
            Vector3 centerRest = body.localPosition + restRot * shellCenterBody;

            if (shell == Shell.Roll)
            {
                float k = Mathf.Clamp01(shellT / Mathf.Max(shellLen, 0.01f));
                float e = 1f - (1f - k) * (1f - k);
                // 在角色本地坐标里，屏幕方向要乘以朝向（镜像时本地 +X 指向屏幕左）
                int localDir = rollDir * (facing ? facing.Facing : 1);
                float ang = -localDir * 360f * Mathf.Max(1f, Mathf.Round(rollTurns)) * e;
                float hop = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f)) * rollHop * (1f - k * 0.5f);
                // 缩壳瞬间往下一压
                float squash = k < 0.15f ? Mathf.Sin(k / 0.15f * Mathf.PI) * 0.12f : 0f;
                SpinBodyAround(centerRest, ang, new Vector3(0f, hop - squash * 1.5f, 0f));
                if (shellT >= shellLen) { SpinBodyAround(centerRest, 0f, Vector3.zero); shell = Shell.Hold; shellT = 0f; }
                return;
            }
            if (shell == Shell.Hold)
            {
                SpinBodyAround(centerRest, 0f, Vector3.zero);
                if (shellT > 0.25f) { shell = Shell.None; HideBody(false); }   // 没人叫出拳（被打断等）就自己钻出来
                return;
            }

            // ---- Punch
            float t = shellT;
            // 壳整体：出拳前往后缩，出拳瞬间往前顶
            float lean = t < 0.3f ? Mathf.SmoothStep(0, 8f, t / 0.3f)
                       : t < punchHitT ? Mathf.Lerp(8f, -10f, (t - 0.3f) / (punchHitT - 0.3f))
                       : Mathf.Lerp(-10f, 0f, Mathf.Clamp01((t - punchHitT) / 0.3f));
            float shove = t < punchHitT ? 0f : Mathf.Sin(Mathf.Clamp01((t - punchHitT) / 0.25f) * Mathf.PI) * 0.25f;
            SpinBodyAround(centerRest, lean, new Vector3(shove, 0f, 0f));

            // 伸出长度：出壳（0.15 s）→ 稍微收回蓄劲 → 0.1 s 打到敌人 → 停顿 → 收回
            Vector3 openW = body.TransformPoint(openingBody);
            Vector3 tgtLocal = transform.InverseTransformPoint(punchTargetWorld);
            Vector3 openLocal = transform.InverseTransformPoint(openW);
            Vector3 dirLocal = tgtLocal - openLocal; dirLocal.z = 0f;
            float dist = dirLocal.magnitude;
            if (dist < 1e-3f) { dirLocal = Vector3.right; dist = 1f; }
            dirLocal /= dist;
            const float armLen = 2.75f;      // 机械臂上下两节 + 钳子的长度（角色单位）
            float reach = Mathf.Clamp(dist - armLen, 0f, punchMaxReach);
            float ext;                        // 肩部离壳口的距离；负数 = 还在壳里
            if (t < 0.15f) ext = Mathf.Lerp(-armLen, 0.2f, Smooth(t / 0.15f));
            else if (t < 0.32f) ext = Mathf.Lerp(0.2f, -0.6f, Smooth((t - 0.15f) / 0.17f));
            else if (t < punchHitT) ext = Mathf.Lerp(-0.6f, reach, (t - 0.32f) / (punchHitT - 0.32f));   // 线性 = 最快
            else if (t < punchHitT + 0.16f) ext = reach + Mathf.Sin((t - punchHitT) / 0.16f * Mathf.PI) * 0.3f;
            else if (t < punchLen - 0.15f) ext = Mathf.Lerp(reach, -armLen, Smooth((t - punchHitT - 0.16f) / (punchLen - 0.15f - punchHitT - 0.16f)));
            else ext = -armLen;
            if (!PunchFired && t >= punchHitT) PunchFired = true;

            bool showArm = t < punchLen - 0.15f;
            if (armLayer) armLayer.enabled = showArm;
            if (clawLayer) clawLayer.enabled = showArm;
            if (armSolver) armSolver.weight = 0f;

            // 机械臂整条拉直、对准目标，肩部沿伸缩杆推出
            Vector3 dirW = transform.TransformDirection(dirLocal).normalized;
            Vector3 axis = transform.forward;
            Align(armUp, dirW, axis); Align(armLow, dirW, axis); Align(claw, dirW, axis);
            armUp.position = openW + dirW * ext * transform.lossyScale.y;
            // 伸缩杆：从壳口稍里面一点到肩部
            bool rod = showArm && ext > 0.05f;
            rodDarkLine.enabled = rodLightLine.enabled = rod;
            if (rod)
            {
                var a = openLocal - dirLocal * 0.3f; var b = openLocal + dirLocal * (ext + 0.3f);
                a.z = b.z = -0.01f;
                rodDarkLine.SetPosition(0, a); rodDarkLine.SetPosition(1, b);
                rodLightLine.SetPosition(0, a + Vector3.up * 0.08f); rodLightLine.SetPosition(1, b + Vector3.up * 0.08f);
            }

            if (t >= punchLen)
            {
                SpinBodyAround(centerRest, 0f, Vector3.zero);
                shell = Shell.None;
                HideBody(false);
                Play("Emerge", false);            // 直接从缩成一团的姿势开始，不做交叉淡入
                Apply(Clips["Emerge"].Sample(0f)); lastPose = Clips["Emerge"].Sample(0f);
            }
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        static void Align(Transform bone, Vector3 dirW, Vector3 axis)
        {
            if (!bone) return;
            float a = Vector3.SignedAngle(bone.right, dirW, axis);
            bone.rotation = Quaternion.AngleAxis(a, axis) * bone.rotation;
        }

        // 身体根绕壳心转 ang 度（角色本地），再整体平移 offset
        void SpinBodyAround(Vector3 centerRest, float ang, Vector3 offset)
        {
            var rot = body.localRotation * Quaternion.Euler(0, 0, ang);
            body.localRotation = rot;
            body.localPosition = centerRest - rot * shellCenterBody + offset;
        }
    }
}
