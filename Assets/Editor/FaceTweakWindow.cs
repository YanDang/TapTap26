using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameJam;
using UnityEditor;
using UnityEngine;

// 表情调整窗口（菜单 GameJam/表情调整）。Play 模式下用：
//   1. 选一个表情（主角会一直保持它），需要时勾“腰上触手”“鱿鱼愤怒”
//   2. 点「对准主角的脸」/「对准鱿鱼的脸」，Scene 视图会切到和游戏镜头同一个角度并贴近
//   3. 在列表里点部件（眉毛、眼睛、嘴…），用下面的按钮按像素微调；也可以直接在 Scene 里用移动 / 旋转 / 缩放工具拖
//   4. 点「保存」写进 Assets/Resources/Hero2D/Face/face_tweaks.json，以后每次运行都按这个摆
// 编辑期间各部件不播小动画（眼泪流、青筋跳等），停在“原位 + 手调”上。
public class FaceTweakWindow : EditorWindow
{
    static readonly string[] ExprNames = { "无表情", "愤怒（蓄力 / 砸 / 处决）", "痛苦（受击 / 被缠 / 被丢）", "高兴（打倒敌人）" };
    static readonly Dictionary<string, string> PartNames = new Dictionary<string, string>
    {
        { "eye_angry_L", "生气眼线 左" }, { "eye_angry_R", "生气眼线 右" }, { "eye_happy_L", "高兴眼 左（^）" }, { "eye_happy_R", "高兴眼 右（^）" },
        { "eye_pain_L", "痛苦眼 左（>）" }, { "eye_pain_R", "痛苦眼 右（<）" },
        { "mouth_pain", "嘴：哭" }, { "mouth_happy", "嘴：笑" }, { "mouth_grit", "嘴：咬牙" }, { "patch_mouth", "盖住原嘴的肤色块" }, { "patch_lash_L", "盖住下睫毛 左" }, { "patch_lash_R", "盖住下睫毛 右" }, { "patch_blue", "盖住眼白下的蓝色涂抹" },
        { "tear_L", "眼泪 左" }, { "tear_R", "眼泪 右" }, { "sweat", "汗滴" }, { "vein", "青筋" },
        { "sparkle_1", "星星 1" }, { "sparkle_2", "星星 2" }, { "coil", "腰上触手" },
        { "sq_lid", "鱿鱼：压下的眼皮" }, { "sq_pupil", "鱿鱼：红瞳" }, { "sq_glow_halo", "鱿鱼：红瞳光晕" },
        { "sq_dots", "鱿鱼：红眼点" }, { "sq_dots_halo", "鱿鱼：眼点光晕" }, { "sq_vein", "鱿鱼：青筋" },
    };

    int expr = 1;
    bool wrapped, squidAngry;
    float step = 1f;
    Vector2 scroll;
    string status = "";

    [MenuItem("GameJam/表情调整")]
    static void Open() => GetWindow<FaceTweakWindow>("表情调整");

    void OnEnable() { EditorApplication.playModeStateChanged += OnPlayMode; }
    void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayMode;
        StopEditing();
    }

    void OnPlayMode(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.ExitingPlayMode) StopEditing();
    }

    static HeroFace Hero => Object.FindObjectOfType<HeroFace>();

    void StopEditing()
    {
        FaceItem.Editing = false;
        if (!Application.isPlaying) return;
        var h = Hero; if (h) h.forceOn = false;
        foreach (var sf in Object.FindObjectsOfType<SquidFace>()) sf.forceAngry = false;
    }

    void Update() { if (Application.isPlaying && FaceItem.Editing) Repaint(); }

    void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("先进入 Play 模式再调整（表情是运行时贴到角色身上的）。\n调好点「保存」，退出 Play 模式也不会丢。", MessageType.Info);
            return;
        }
        var hero = Hero;
        if (!hero) { EditorGUILayout.HelpBox("场景里没有寄居蟹主角（HeroFace）。", MessageType.Warning); return; }

        // ---------------------------------------------------------- 1. 表情
        EditorGUILayout.LabelField("1. 选表情", EditorStyles.boldLabel);
        bool editing = EditorGUILayout.Toggle("开始调整（锁定表情）", FaceItem.Editing);
        if (editing != FaceItem.Editing)
        {
            FaceItem.Editing = editing;
            if (editing) foreach (var it in Object.FindObjectsOfType<FaceItem>(true)) it.ApplyRest();
            else StopEditing();
        }
        using (new EditorGUI.DisabledScope(!FaceItem.Editing))
        {
            expr = GUILayout.SelectionGrid(expr, ExprNames, 1);
            wrapped = EditorGUILayout.ToggleLeft("显示腰上缠的触手", wrapped);
            squidAngry = EditorGUILayout.ToggleLeft("所有鱿鱼显示愤怒脸", squidAngry);
        }
        if (FaceItem.Editing)
        {
            hero.forceOn = true; hero.forced = (HeroFace.Expr)expr; hero.forceWrapped = wrapped;
            foreach (var sf in Object.FindObjectsOfType<SquidFace>()) sf.forceAngry = squidAngry;
        }

        var tw = FaceOverlay.Tweaks;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("眯眼程度（0 睁开 – 1 闭上）", EditorStyles.boldLabel);
        tw.angrySquint = EditorGUILayout.Slider("愤怒", tw.angrySquint, 0f, 0.9f);

        // ---------------------------------------------------------- 2. 镜头
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. Scene 视图对准", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("对准主角的脸")) FrameOn(FindItem("hero/eye_angry_L"), 0.35f);
            if (GUILayout.Button("对准最近的鱿鱼脸")) FrameOn(NearestSquidItem(hero), 0.9f);
        }
        bool paused = EditorGUILayout.ToggleLeft("暂停游戏（怪物不会打扰；拖动也更稳）", EditorApplication.isPaused);
        if (paused != EditorApplication.isPaused) EditorApplication.isPaused = paused;

        // ---------------------------------------------------------- 3. 部件
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3. 选部件，再微调（也可以在 Scene 里直接拖）", EditorStyles.boldLabel);
        var items = Object.FindObjectsOfType<FaceItem>().Where(i => i.gameObject.activeInHierarchy)
                          .GroupBy(i => i.key).Select(g => g.OrderBy(i => (i.transform.position - hero.transform.position).sqrMagnitude).First())
                          .OrderBy(i => i.key).ToList();
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(170));
        foreach (var it in items)
        {
            string part = it.key.Substring(it.key.IndexOf('/') + 1);
            string label = PartNames.TryGetValue(part, out var n) ? n : part;
            bool sel = Selection.activeGameObject == it.gameObject;
            bool changed = it.tweak.offset != Vector3.zero || Mathf.Abs(it.tweak.rotation) > 0.01f || Mathf.Abs(it.tweak.scale - 1f) > 0.001f;
            if (GUILayout.Toggle(sel, (changed ? "● " : "    ") + label, "Button") && !sel)
            {
                Selection.activeGameObject = it.gameObject;
                SceneView.lastActiveSceneView?.Repaint();
            }
        }
        if (items.Count == 0) GUILayout.Label("（当前表情没有显示任何部件，先在上面选一个表情）");
        EditorGUILayout.EndScrollView();

        var cur = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<FaceItem>() : null;
        if (cur)
        {
            if (FaceItem.Editing) cur.Capture();   // 先把 Scene 里拖动的结果记下来，按钮在此基础上改
            step = EditorGUILayout.Slider("每次移动（文档像素）", step, 0.5f, 10f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("↑", GUILayout.Width(40))) Nudge(cur, 0, -step);
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("←", GUILayout.Width(40))) Nudge(cur, -step, 0);
                if (GUILayout.Button("↓", GUILayout.Width(40))) Nudge(cur, 0, step);
                if (GUILayout.Button("→", GUILayout.Width(40))) Nudge(cur, step, 0);
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("逆时针 2°")) { cur.tweak.rotation += 2f; cur.ApplyRest(); }
                if (GUILayout.Button("顺时针 2°")) { cur.tweak.rotation -= 2f; cur.ApplyRest(); }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("缩小 5%")) { cur.tweak.scale *= 0.95f; cur.ApplyRest(); }
                if (GUILayout.Button("放大 5%")) { cur.tweak.scale *= 1.05f; cur.ApplyRest(); }
            }
            var off = OffsetInPx(cur);
            EditorGUILayout.LabelField($"当前：右移 {off.x:F1} px，下移 {off.y:F1} px，旋转 {cur.tweak.rotation:F1}°，缩放 ×{cur.tweak.scale:F2}");
            if (GUILayout.Button("这个部件恢复原位")) cur.ResetTweak();
            SyncSameKey(cur);
        }

        // ---------------------------------------------------------- 4. 保存
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("保存", GUILayout.Height(30))) Save();
            if (GUILayout.Button("全部恢复原位", GUILayout.Height(30)) &&
                EditorUtility.DisplayDialog("全部恢复原位", "把所有表情部件的手调都清掉？（保存后才写入文件）", "清掉", "取消"))
                foreach (var it in Object.FindObjectsOfType<FaceItem>(true)) it.ResetTweak();
        }
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.None);
    }

    static FaceItem FindItem(string key) => Object.FindObjectsOfType<FaceItem>(true).FirstOrDefault(i => i.key == key);

    static FaceItem NearestSquidItem(HeroFace hero) => Object.FindObjectsOfType<FaceItem>(true)
        .Where(i => i.key == "squid/sq_lid").OrderBy(i => (i.transform.position - hero.transform.position).sqrMagnitude).FirstOrDefault();

    static void FrameOn(FaceItem it, float size)
    {
        var sv = SceneView.lastActiveSceneView;
        if (!sv || !it || !Camera.main) return;
        sv.orthographic = true;
        sv.LookAt(it.transform.position, Camera.main.transform.rotation, size);
        Selection.activeGameObject = it.gameObject;
        Tools.pivotRotation = PivotRotation.Local;    // 移动工具沿部件自己的方向，拖起来是“屏幕上的上下左右”
    }

    static void Nudge(FaceItem it, float right, float down)
    {
        it.tweak.offset += it.pxRight * right + it.pxDown * down;
        it.ApplyRest();
    }

    static Vector2 OffsetInPx(FaceItem it)
    {
        var o = it.tweak.offset;
        float r = it.pxRight.sqrMagnitude > 0 ? Vector3.Dot(o, it.pxRight) / it.pxRight.sqrMagnitude : 0f;
        float d = it.pxDown.sqrMagnitude > 0 ? Vector3.Dot(o, it.pxDown) / it.pxDown.sqrMagnitude : 0f;
        return new Vector2(r, d);
    }

    // 多只鱿鱼共用同一组手调：改了一只，其他的同名部件立刻跟着变
    static void SyncSameKey(FaceItem cur)
    {
        foreach (var it in Object.FindObjectsOfType<FaceItem>(true))
            if (it != cur && it.key == cur.key) it.ApplyRest();
    }

    void Save()
    {
        // 场景里被拖过的部件（偏离了“原位 + 手调”）先记下来
        if (FaceItem.Editing)
            foreach (var it in Object.FindObjectsOfType<FaceItem>(true))
            {
                var t = it.transform;
                if ((t.localPosition - it.RestPos).sqrMagnitude > 1e-10f || Quaternion.Angle(t.localRotation, it.RestRot) > 0.01f ||
                    (t.localScale - it.RestScale).sqrMagnitude > 1e-10f)
                    it.Capture();
            }
        // 只写有改动的部件（内存里的列表保持完整，部件还引用着它们）
        var tw = FaceOverlay.Tweaks;
        var file = new FaceOverlay.TweakFile
        {
            angrySquint = tw.angrySquint,
            items = tw.items.Where(t => t.offset != Vector3.zero || Mathf.Abs(t.rotation) > 0.01f || Mathf.Abs(t.scale - 1f) > 0.001f).ToList(),
        };
        File.WriteAllText(FaceOverlay.TweakAssetPath, JsonUtility.ToJson(file, true), new System.Text.UTF8Encoding(false));
        AssetDatabase.ImportAsset(FaceOverlay.TweakAssetPath);
        status = $"已保存 {file.items.Count} 个部件的调整 + 眯眼程度 → {FaceOverlay.TweakAssetPath}（{System.DateTime.Now:HH:mm:ss}）";
        Debug.Log("[表情调整] " + status);
    }
}
