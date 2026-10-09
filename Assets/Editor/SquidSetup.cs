using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 鱿鱼怪：生成带 SquidAnimator 的 Resources/Squid/Squid.prefab，以及测试场景。
// 骨骼名来自 E:\GameJamArt\squid\bone_names.json（rename_psb_bones.py 写进 PSB meta）。
public static class SquidSetup
{
    const string PsbPath = "Assets/Art/Characters/Squid/鱿鱼.psb";
    const string PrefabPath = "Assets/Resources/Squid/Squid.prefab";
    const string TestScenePath = "Assets/Scenes/SquidTest.unity";

    // 触手名、骨名前缀、爪指前缀（无则 null）、走路相位
    static readonly (string name, string prefix, string claw, float phase)[] Tentacles =
    {
        ("左长臂", "左长臂_", null, 0f),
        ("左爪臂", "左爪臂_", "左爪_指", 0.4f),
        ("中爪臂", "中爪臂_", "中爪_指", 0.8f),
        ("右爪臂", "右爪臂_", "右爪_指", 0.2f),
        ("右长臂", "右长臂_", null, 0.6f),
    };

    [MenuItem("GameJam/Squid/Build Squid Prefab")]
    public static void BuildPrefab()
    {
        AssetDatabase.Refresh();
        var psb = AssetDatabase.LoadAssetAtPath<GameObject>(PsbPath);
        if (!psb) { Debug.LogError("[Squid] 找不到 " + PsbPath); return; }
        var root = (GameObject)PrefabUtility.InstantiatePrefab(psb);
        root.name = "Squid";
        var skinned = new HashSet<Transform>(root.GetComponentsInChildren<UnityEngine.U2D.Animation.SpriteSkin>(true).Select(s => s.transform));
        var bones = new Dictionary<string, Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (!skinned.Contains(t)) bones[t.name] = t;

        var anim = root.AddComponent<GameJam.SquidAnimator>();
        anim.body = Get(bones, "身体根");
        anim.mantle1 = Get(bones, "外套膜_1"); anim.mantle2 = Get(bones, "外套膜_2");
        anim.finL = Get(bones, "鳍_左"); anim.finR = Get(bones, "鳍_右");
        var list = new List<GameJam.SquidAnimator.Tentacle>();
        foreach (var (n, prefix, claw, phase) in Tentacles)
        {
            var chain = bones.Where(kv => kv.Key.StartsWith(prefix)).OrderBy(kv => kv.Key).Select(kv => kv.Value).ToArray();
            var claws = claw == null ? new Transform[0]
                : bones.Where(kv => kv.Key.StartsWith(claw)).OrderBy(kv => kv.Key).Select(kv => kv.Value).ToArray();
            list.Add(new GameJam.SquidAnimator.Tentacle { name = n, bones = chain, claw = claws, phase = phase });
            Debug.Log($"[Squid] {n}: {chain.Length} 节，爪指 {claws.Length} 节");
        }
        anim.tentacles = list.ToArray();

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
        Object.DestroyImmediate(root);
        Debug.Log("[Squid] 已保存 " + PrefabPath + " = " + (prefab != null));
    }

    static Transform Get(Dictionary<string, Transform> b, string n)
    {
        if (b.TryGetValue(n, out var t)) return t;
        Debug.LogWarning("[Squid] 缺骨 " + n); return null;
    }

    [MenuItem("GameJam/Squid/Build Test Scene (Auto Demo)")]
    public static void BuildTestScene() => BuildTestScene(false);

    // 用户明确同意丢弃当前场景未保存改动时才用
    [MenuItem("GameJam/Squid/Build Test Scene (Discard Current Scene Changes)")]
    public static void BuildTestSceneDiscard() => BuildTestScene(true);

    static void BuildTestScene(bool discardCurrent)
    {
        // 当前场景有未保存改动时不替换，免得丢掉用户的修改
        var active = EditorSceneManager.GetActiveScene();
        if (active.isDirty && !discardCurrent) { Debug.LogError("[Squid] 当前场景 " + active.name + " 有未保存的改动，先保存（Ctrl+S）再生成测试场景"); return; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!prefab) { Debug.LogError("[Squid] 先运行 Build Squid Prefab"); return; }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.86f, 0.9f, 0.93f);

        var squid = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        // 用骨骼定位（编辑模式下蒙皮包围盒不可靠）：身体根水平居中，最低的触手梢贴 y=0
        float minY = float.MaxValue, maxY = float.MinValue, bodyX = 0f;
        foreach (var t in squid.GetComponentsInChildren<Transform>(true))
        {
            minY = Mathf.Min(minY, t.position.y); maxY = Mathf.Max(maxY, t.position.y);
            if (t.name == "身体根") bodyX = t.position.x;
        }
        squid.transform.position = new Vector3(-bodyX, -minY, 0f);
        float h = maxY - minY;
        camGo.transform.position = new Vector3(0, h * 0.5f, -10);
        cam.orthographicSize = h * 0.6f;
        squid.AddComponent<GameJam.SquidTestDriver>();
        var rec = camGo.AddComponent<GameJam.Hero2DTestRecorder>();
        rec.follow = squid.transform; rec.record = true; rec.everyN = 2; rec.frames = 90;   // 6 秒
        EditorSceneManager.SaveScene(scene, TestScenePath);
        Debug.Log("[Squid] 测试场景已生成 " + TestScenePath);
    }
}
