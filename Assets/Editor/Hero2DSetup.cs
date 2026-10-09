using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.IK;

// 新主角（寄居蟹女孩，2D 骨骼 PSB）：生成带 IK 的 Hero2D.prefab，并渲染 IK 测试图。
// 菜单 GameJam/Hero2D/Build Hero Prefab；batchmode：-executeMethod Hero2DSetup.BuildAndTest
// 骨骼名来自 E:\GameJamArt\hero2d\bone_names.json（rename_psb_bones.py 写进 PSB meta）。
public static class Hero2DSetup
{
    const string PsbPath = "Assets/Art/Characters/Hero2D/主角.psb";
    const string PrefabPath = "Assets/Resources/Hero2D/Hero2D.prefab";   // 放进 Resources，GameManager 运行时加载
    const string ShotDir = @"E:\GameJamArt\hero2d\shots";
    const float PixelsPerUnit = 100f;

    // 名称、上段骨、下段骨、末端（null = 在下段骨末端新建一个“尖”作为末端）、下段骨长度（像素）
    static readonly (string name, string upper, string lower, string end, float lowerLen)[] Limbs =
    {
        ("右前腿", "右前腿_上", "右前腿_下", null, 154.20035f),
        ("左前腿", "左前腿_上", "左前腿_下", null, 141.0275f),
        ("右后腿", "右后腿_上", "右后腿_下", null, 194.48001f),
        ("左后腿", "左后腿_上", "左后腿_下", null, 173.89172f),
        ("机械臂", "机械臂_上", "机械臂_下", "钳子", 0f),
        ("左臂", "左臂_上", "左臂_下", "左手掌", 0f),
    };

    [MenuItem("GameJam/Hero2D/Build Hero Prefab")]
    public static void BuildMenu() => Build(true);

    public static void BuildAndTest()
    {
        Build(true);
        EditorApplication.Exit(0);
    }

    static void Build(bool renderShots)
    {
        AssetDatabase.Refresh();
        var psb = AssetDatabase.LoadAssetAtPath<GameObject>(PsbPath);
        if (psb == null) { Debug.LogError("[Hero2D] 找不到 " + PsbPath); return; }

        var root = (GameObject)PrefabUtility.InstantiatePrefab(psb);
        root.name = "Hero2D";
        var bones = new Dictionary<string, Transform>();
        var skinned = new HashSet<Transform>();
        foreach (var sk in root.GetComponentsInChildren<UnityEngine.U2D.Animation.SpriteSkin>(true)) skinned.Add(sk.transform);
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (!skinned.Contains(t)) bones[t.name] = t;      // 图层物体与骨骼可能同名，只收骨骼
        Debug.Log($"[Hero2D] 层级节点 {bones.Count} 个；示例骨：身体根={bones.ContainsKey("身体根")} 钳子={bones.ContainsKey("钳子")}");

        var mgr = root.AddComponent<IKManager2D>();
        var ikRoot = new GameObject("IK").transform;
        ikRoot.SetParent(root.transform, false);

        var report = new List<string>();
        var targets = new Dictionary<string, Transform>();
        foreach (var l in Limbs)
        {
            if (!bones.TryGetValue(l.upper, out var up) || !bones.TryGetValue(l.lower, out var low))
            { Debug.LogError("[Hero2D] 缺骨 " + l.upper + "/" + l.lower); continue; }

            Transform effector;
            if (l.end != null) effector = bones[l.end];
            else
            {
                effector = new GameObject(l.name + "_尖").transform;
                effector.SetParent(low, false);
                effector.localPosition = new Vector3(l.lowerLen / PixelsPerUnit, 0, 0);   // 2D Animation 的骨沿本地 +X
            }

            var solverGo = new GameObject("IK_" + l.name);
            solverGo.transform.SetParent(ikRoot, false);
            var solver = solverGo.AddComponent<LimbSolver2D>();
            var target = new GameObject(l.name + "_目标").transform;
            target.SetParent(solverGo.transform, false);
            target.position = effector.position;
            target.rotation = effector.rotation;
            targets[l.name] = target;

            var chain = solver.GetChain(0);
            chain.effector = effector;
            chain.target = target;
            chain.transformCount = 3;
            solver.Initialize();
            mgr.AddSolver(solver);

            // 选膝盖/肘弯曲方向：目标放在原位时解算结果应保持原姿势
            var kneeBefore = low.position;
            var rots = new[] { up.localRotation, low.localRotation };
            solver.UpdateIK(1f);
            float err = Vector3.Distance(low.position, kneeBefore);
            if (err > 0.01f)
            {
                up.localRotation = rots[0]; low.localRotation = rots[1];
                solver.flip = !solver.flip;
                solver.UpdateIK(1f);
                err = Vector3.Distance(low.position, kneeBefore);
            }
            up.localRotation = rots[0]; low.localRotation = rots[1];
            report.Add($"{l.name}: flip={solver.flip} 原位解算误差={err * PixelsPerUnit:F2}px 有效={solver.isValid}");
        }
        foreach (var r in report) Debug.Log("[Hero2D] " + r);
        AddMotion(root, bones, targets);

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
        const string oldPath = "Assets/Art/Characters/Hero2D/Hero2D.prefab";             // 旧位置，避免两份同名预制体混淆
        if (AssetDatabase.LoadAssetAtPath<GameObject>(oldPath)) AssetDatabase.DeleteAsset(oldPath);
        Debug.Log("[Hero2D] 已保存 " + PrefabPath + " = " + (prefab != null));

        if (renderShots) RenderTests(root, mgr);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
    }

    // 程序化步态 + 弹簧骨 + 眨眼
    static void AddMotion(GameObject root, Dictionary<string, Transform> bones, Dictionary<string, Transform> targets)
    {
        root.AddComponent<GameJam.HeroFacing>();
        var gait = root.AddComponent<GameJam.HeroGait>();
        gait.body = bones["身体根"];
        gait.chest = bones["上身"];
        // 对角成对：右前 + 左后 一组，左前 + 右后 一组
        gait.legs = new[]
        {
            new GameJam.HeroGait.Leg { name = "右前腿", target = targets["右前腿"], phaseOffset = 0f },
            new GameJam.HeroGait.Leg { name = "左后腿", target = targets["左后腿"], phaseOffset = 0.02f },
            new GameJam.HeroGait.Leg { name = "左前腿", target = targets["左前腿"], phaseOffset = 0.5f },
            new GameJam.HeroGait.Leg { name = "右后腿", target = targets["右后腿"], phaseOffset = 0.52f },
        };

        // 名字、刚度、阻尼、惯性、转动滞后、最大角度、轻摆幅度（度）、轻摆快慢、踩步冲击（度/秒）
        var specs = new List<(string, float, float, float, float, float, float, float, float)>();
        // 用户圈出的部位（2026-10-07）：头顶右侧大片刘海、脸左侧垂下的发缕、两条辫子 —— 调软，并加轻摆和踩步弹跳
        foreach (var n in new[] { "刘海_4", "刘海_5", "刘海_6" })
            specs.Add((n, 60f, 5.5f, 0.07f, 0.65f, 16f, 4.5f, 0.6f, 100f));   // 大片刘海：上一版只摆 ±2.5–5°，太小
        foreach (var n in new[] { "刘海_1", "侧刘海_1" })
            specs.Add((n, 75f, 6.5f, 0.06f, 0.65f, 18f, 3f, 0.6f, 70f));
        foreach (var n in new[] { "刘海_1尖", "侧刘海_2" })
            specs.Add((n, 60f, 5.5f, 0.07f, 0.7f, 22f, 3.5f, 0.75f, 80f));
        foreach (var n in new[] { "右辫子_1", "左辫子_1" })
            specs.Add((n, 55f, 5f, 0.09f, 0.7f, 30f, 4f, 0.5f, 140f));
        foreach (var n in new[] { "右辫子_2", "左辫子_2" })
            specs.Add((n, 45f, 4.2f, 0.1f, 0.75f, 35f, 5f, 0.65f, 120f));
        // 其余
        specs.Add(("刘海_2", 140f, 11f, 0.035f, 0.5f, 12f, 1.2f, 0.5f, 25f));
        specs.Add(("刘海_3", 65f, 6f, 0.065f, 0.6f, 14f, 4f, 0.55f, 90f));   // 用户要求（2026-10-07）：比原来 ±1.6° 再大一些
        foreach (var n in new[] { "右发髻", "左发髻" })
            specs.Add((n, 220f, 16f, 0.02f, 0.3f, 8f, 0.8f, 0.4f, 30f));
        foreach (var n in new[] { "流苏_1", "流苏_2", "流苏_3" })
            specs.Add((n, 80f, 6f, 0.07f, 0.7f, 30f, 2f, 0.7f, 80f));
        specs.Add(("项链_骨", 160f, 13f, 0.03f, 0.5f, 12f, 0f, 0f, 35f));
        specs.Add(("贝壳", 90f, 13f, 0.012f, 0.4f, 5f, 0f, 0f, 20f));
        // 不给「头」加弹簧：脸和皮肤图层下巴处的顶点绑在「上身」，头相对上身转动会拉扯脸
        var springs = new List<GameJam.HeroSpringBones.Spring>();
        // 父在前、子在后（层级深度排序），链条才能逐级传递
        specs.Sort((a, b) => Depth(bones, a.Item1).CompareTo(Depth(bones, b.Item1)));
        foreach (var (n, k, c, inertia, lag, max, sway, rate, kick) in specs)
        {
            if (!bones.TryGetValue(n, out var b)) { Debug.LogWarning("[Hero2D] 弹簧骨缺失 " + n); continue; }
            springs.Add(new GameJam.HeroSpringBones.Spring
            {
                boneName = n, bone = b, stiffness = k, damping = c, inertia = inertia, rotationLag = lag, maxAngle = max,
                sway = sway, swayRate = rate, stepKick = kick
            });
        }
        root.AddComponent<GameJam.HeroSpringBones>().springs = springs.ToArray();

        var blink = root.AddComponent<GameJam.HeroBlink>();
        blink.head = bones["头"];
        SpriteRenderer R(string n)
        {
            foreach (var r in root.GetComponentsInChildren<SpriteRenderer>(true)) if (r.name == n) return r;
            Debug.LogWarning("[Hero2D] 找不到图层 " + n); return null;
        }
        blink.eyeParts = new[] { R("眼白"), R("眼珠") };
        blink.lid = R("眼眶");                    // 图层里还有下睫毛、雀斑和嘴巴，运行时会被切成两份
        blink.clipMaterial = EyeClipMaterial();
        Debug.Log($"[Hero2D] 步态 4 腿，弹簧骨 {springs.Count} 根，眨眼图层 3 个");
    }

    const string EyeClipMatPath = "Assets/Art/Characters/Hero2D/EyeClip.mat";

    static Material EyeClipMaterial()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(EyeClipMatPath);
        if (m) return m;
        m = new Material(Shader.Find("GameJam/SpriteClipPlane"));
        AssetDatabase.CreateAsset(m, EyeClipMatPath);
        return m;
    }

    static int Depth(Dictionary<string, Transform> bones, string n)
    {
        if (!bones.TryGetValue(n, out var t)) return 999;
        int d = 0; for (; t != null; t = t.parent) d++;
        return d;
    }

    const string TestScenePath = "Assets/Scenes/Hero2DTest.unity";

    [MenuItem("GameJam/Hero2D/Build Test Scene")]
    public static void BuildTestScene() => BuildTestScene(false);

    [MenuItem("GameJam/Hero2D/Build Test Scene (Auto Demo)")]
    public static void BuildTestSceneDemo() => BuildTestScene(true);

    static void BuildTestScene(bool autoDemo)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!prefab) { Debug.LogError("[Hero2D] 先运行 Build Hero Prefab"); return; }
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.86f, 0.9f, 0.93f);

        var hero = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        hero.transform.position = Vector3.zero;
        // 编辑模式下 SpriteSkin 的包围盒不可靠，用四只脚的 IK 目标定位：脚底贴 y=0，身体根居中
        float footY = float.MaxValue; float bodyX = 0f;
        foreach (var t in hero.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.EndsWith("腿_目标")) footY = Mathf.Min(footY, t.position.y);
            if (t.name == "身体根") bodyX = t.position.x;
        }
        hero.transform.position = new Vector3(-bodyX, -footY, 0);
        var mover = hero.AddComponent<GameJam.Hero2DTestMover>();
        mover.autoDemo = autoDemo;
        camGo.transform.position = new Vector3(0, 3.9f, -10);
        cam.orthographicSize = 4.6f;
        var rec = camGo.AddComponent<GameJam.Hero2DTestRecorder>();
        rec.follow = hero.transform;
        rec.record = autoDemo;
        rec.everyN = 2; rec.frames = 72;   // 4.8 秒：右走 2.5 秒 → 停 1.5 秒 → 开始左走

        var ground = new GameObject("地面线").AddComponent<LineRenderer>();
        ground.positionCount = 2; ground.widthMultiplier = 0.03f;
        ground.SetPositions(new[] { new Vector3(-40, 0.05f, 1), new Vector3(40, 0.05f, 1) });
        ground.material = new Material(Shader.Find("Sprites/Default")); ground.startColor = ground.endColor = new Color(0.4f, 0.45f, 0.5f);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, TestScenePath);
        Debug.Log("[Hero2D] 测试场景已生成 " + TestScenePath + "：Play 后用 WASD / 方向键移动，Shift 加速");
    }

    static Transform FindDeep(Transform t, string n)
    {
        foreach (var c in t.GetComponentsInChildren<Transform>(true)) if (c.name == n) return c;
        return null;
    }

    static void RenderTests(GameObject root, IKManager2D mgr)
    {
        Directory.CreateDirectory(ShotDir);
        var camGo = new GameObject("ShotCam");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.93f, 0.93f, 0.93f);
        var b = new Bounds(root.transform.position, Vector3.zero);
        foreach (var r in root.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        cam.transform.position = new Vector3(b.center.x, b.center.y, -10);
        cam.orthographicSize = Mathf.Max(b.extents.y, b.extents.x) * 1.35f;
        var rt = new RenderTexture(900, 900, 24);
        cam.targetTexture = rt;

        Transform T(string n) => root.transform.Find("IK/IK_" + n + "/" + n + "_目标");
        var poses = new (string file, System.Action apply)[]
        {
            ("0_绑定姿势", () => { }),
            ("1_抬右前腿_钳子前伸", () =>
            {
                T("右前腿").position += new Vector3(0.6f, 0.9f, 0);
                T("机械臂").position += new Vector3(1.0f, 0.6f, 0);
            }),
            ("2_蹲低_左手上举", () =>
            {
                FindDeep(root.transform, "身体根")?.Translate(0, -0.5f, 0, Space.World);
                T("左臂").position += new Vector3(0.3f, 1.2f, 0);
            }),
        };
        foreach (var p in poses)
        {
            p.apply();
            mgr.UpdateManager();
            var deform = typeof(UnityEngine.U2D.Animation.SpriteSkin).GetMethod("Deform",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            foreach (var sk in root.GetComponentsInChildren<UnityEngine.U2D.Animation.SpriteSkin>())
            { sk.alwaysUpdate = true; deform?.Invoke(sk, null); }
            foreach (var sv in mgr.solvers)
            {
                var ch = sv.GetChain(0);
                Debug.Log($"[Hero2D] {p.file} {sv.name}: 末端离目标 {Vector3.Distance(ch.effector.position, ch.target.position) * PixelsPerUnit:F1}px");
            }
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(ShotDir, p.file + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo);
        Debug.Log("[Hero2D] 测试图已写到 " + ShotDir);
    }
}
