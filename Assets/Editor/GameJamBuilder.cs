using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameJam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// GameJam/Build All: slice sprite sheets from Assets/Art/import_manifest.json, build SpriteSet assets, materials,
/// UI sprite settings, and the Main scene (Hermes' Ecostation + GameRoot). Everything here is regenerated — edit the
/// manifest / scripts, not the generated assets.
/// </summary>
public static class GameJamBuilder
{
    [Serializable] class ClipInfo { public string name, file; public int frames, cellW, cellH; public float fps; public bool loop; public string[] rows; }
    [Serializable] class SetInfo { public string name; public float ppu, pivotX, pivotY; public ClipInfo[] clips; }
    [Serializable] class Manifest { public SetInfo[] sets; }

    const string MainScene = "Assets/Scenes/Main.unity";
    const string HermesScene = "Assets/SpotlightScene/Scenes/Nirun_Ecostation.unity";

    [MenuItem("GameJam/Build All")]
    public static void BuildAll()
    {
        AssetDatabase.Refresh();
        ImportSprites();
        ImportUI();
        BuildMaterials();
        BuildScene();
        ConfigurePlayer();
        AssetDatabase.SaveAssets();
        Debug.Log("[GameJamBuilder] BuildAll done");
    }

    static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    // ------------------------------------------------------------------ sprites
    static void ImportSprites()
    {
        var m = JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/Art/import_manifest.json"));
        EnsureFolder("Assets/Resources/SpriteSets");
        foreach (var set in m.sets)
        {
            var so = ScriptableObject.CreateInstance<SpriteSet>();
            foreach (var c in set.clips)
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(c.file);
                if (ti == null) { Debug.LogError("missing sheet " + c.file); continue; }
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Multiple;
                ti.spritePixelsPerUnit = set.ppu;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.filterMode = FilterMode.Bilinear;
                ti.maxTextureSize = 16384;
                ti.textureCompression = TextureImporterCompression.Compressed;
                ti.wrapMode = TextureWrapMode.Clamp;
                int rows = c.rows.Length;
                var metas = new List<SpriteMetaData>();
                for (int r = 0; r < rows; r++)
                    for (int f = 0; f < c.frames; f++)
                        metas.Add(new SpriteMetaData
                        {
                            name = $"{c.name}_{c.rows[r]}_{f:00}",
                            rect = new Rect(f * c.cellW, (rows - 1 - r) * c.cellH, c.cellW, c.cellH),
                            alignment = (int)SpriteAlignment.Custom,
                            pivot = new Vector2(set.pivotX, set.pivotY)
                        });
#pragma warning disable 618
                ti.spritesheet = metas.ToArray();
#pragma warning restore 618
                ti.SaveAndReimport();

                var sprites = AssetDatabase.LoadAllAssetsAtPath(c.file).OfType<Sprite>().ToDictionary(s => s.name);
                var arr = new Sprite[rows * c.frames];
                for (int r = 0; r < rows; r++)
                    for (int f = 0; f < c.frames; f++)
                        sprites.TryGetValue($"{c.name}_{c.rows[r]}_{f:00}", out arr[r * c.frames + f]);
                int missing = arr.Count(s => s == null);
                if (missing > 0) Debug.LogError($"[GameJamBuilder] {c.file}: {missing} sprites missing");
                so.clips.Add(new SpriteSet.Clip { name = c.name, fps = c.fps, loop = c.loop, dirs = c.rows, frames = c.frames, sprites = arr });
            }
            string path = $"Assets/Resources/SpriteSets/{set.name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(so, path);
            Debug.Log($"[GameJamBuilder] SpriteSet {set.name}: {so.clips.Count} clips");
        }
    }

    static void ImportUI()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/UI" }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var ti = (TextureImporter)AssetImporter.GetAtPath(p);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.spritePixelsPerUnit = 100;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
    }

    static void BuildMaterials()
    {
        EnsureFolder("Assets/Resources/Materials");
        Mat("SpriteBillboard", "GameJam/SpriteBillboard");
        Mat("SpriteAdditive", "GameJam/SpriteAdditive");
        Mat("ScreenFX", "GameJam/ScreenFX");
    }

    static void Mat(string name, string shader)
    {
        var path = $"Assets/Resources/Materials/{name}.mat";
        var sh = Shader.Find(shader);
        if (!sh) { Debug.LogError("shader not found " + shader); return; }
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) { m.shader = sh; EditorUtility.SetDirty(m); return; }
        AssetDatabase.CreateAsset(new Material(sh), path);
    }

    // ------------------------------------------------------------------ scene
    static void BuildScene()
    {
        EnsureFolder("Assets/Scenes");
        AssetDatabase.DeleteAsset(MainScene);
        if (!AssetDatabase.CopyAsset(HermesScene, MainScene)) { Debug.LogError("copy scene failed"); return; }
        var scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
        RemoveClutter();
        var root = new GameObject("GameRoot");
        root.AddComponent<GameManager>();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        // 程序同学的采集 / 合成场景（CraftingBridge 叠加加载，场景名 Mix / Collect 在对方代码里写死）
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MainScene, true),
            new EditorBuildSettingsScene("Assets/Crafting/Scenes/Mix.unity", true),
            new EditorBuildSettingsScene("Assets/Crafting/Scenes/Collect.unity", true),
        };
    }

    // 用户 2026-10-11：测试场景里的杂物全部删掉，只留地面、水渠、中央建筑（怪物出生点）和地图边缘的石头 / 废墟墙。
    // 只改拷贝出来的 Main.unity，Hermes 的原场景不动。
    static readonly string[] ClutterGroups =
    {
        "Algae_Water_Resource_Garden",   // 左边的藻类水池 + 3 个培养台
        "Lightning_Jellyfish_Nursery",   // 右边的水母育苗台
        "Open_Building_Plaza",           // 中间的建造网格圈
    };
    static readonly string[] ClutterPrefixes = { "Moss_Islet", "Foreground_Coral_", "Harvest_" };

    static void RemoveClutter()
    {
        int n = 0;
        foreach (var name in ClutterGroups)
        {
            var g = GameObject.Find(name);
            if (g) { UnityEngine.Object.DestroyImmediate(g); n++; }
        }
        foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>(true))
        {
            if (!t) continue;
            foreach (var pre in ClutterPrefixes)
                if (t.name.StartsWith(pre)) { UnityEngine.Object.DestroyImmediate(t.gameObject); n++; break; }
        }
        Debug.Log($"[GameJamBuilder] removed {n} clutter objects");
    }

    static void ConfigurePlayer()
    {
        PlayerSettings.productName = "gamejam";
        PlayerSettings.companyName = "Spotlight";
        PlayerSettings.runInBackground = true;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;
    }

    // ------------------------------------------------------------------ builds
    [MenuItem("GameJam/Build Windows Player")]
    public static void BuildPlayer()
    {
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { MainScene, "Assets/Crafting/Scenes/Mix.unity", "Assets/Crafting/Scenes/Collect.unity" },   // 采集 / 合成场景由 CraftingBridge 叠加加载
            locationPathName = "Builds/Windows/gamejam.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log("[GameJamBuilder] build result: " + report.summary.result + " size=" + report.summary.totalSize);
        if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    public static void BuildAllAndPlayer()
    {
        BuildAll();
        BuildPlayer();
    }
}
