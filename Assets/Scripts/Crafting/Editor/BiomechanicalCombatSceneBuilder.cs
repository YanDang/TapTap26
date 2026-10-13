using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using BiomechanicalCrafting;

namespace BiomechanicalCrafting.Editor
{
    public static class BiomechanicalCombatSceneBuilder
    {
        [MenuItem("BiomechanicalCrafting/Build Biomechanical Combat Scene")]
        public static void BuildCombatScene()
        {
            string scenePath = "Assets/Scenes/BiomechanicalCombatScene.unity";

            // 确保文件存在（如未存在，从 TileTest 复制）
            if (!System.IO.File.Exists(scenePath))
            {
                FileUtil.CopyFileOrDirectory("Assets/Scenes/TileTest.unity", scenePath);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 1. 获取并配置基础组件
            var lavaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Furniture_LavaTable.prefab");
            var player = Object.FindObjectOfType<PlayerController>();

            // 2. 配置 BiomechanicalCombatAdapter
            var adapterObj = GameObject.Find("BiomechanicalCombatAdapter");
            if (adapterObj == null)
            {
                adapterObj = new GameObject("BiomechanicalCombatAdapter");
            }
            var adapter = adapterObj.GetComponent<BiomechanicalCombatAdapter>() ?? adapterObj.AddComponent<BiomechanicalCombatAdapter>();
            adapter.genericFurniturePrefab = lavaPrefab;
            adapter.autoSpawnShowcaseOnStart = false;
            foreach (var duplicate in Object.FindObjectsOfType<BiomechanicalCombatAdapter>())
                if (duplicate != adapter) Object.DestroyImmediate(duplicate);

            // 3. 配置 CombatSceneCraftingEntrance
            var entrance = Object.FindObjectOfType<CombatSceneCraftingEntrance>();
            if (entrance != null)
            {
                entrance.craftingSceneName = "Mix";
                entrance.shortcutKey = KeyCode.Tab;
            }

            // 5. 配置 BuildController 与 GameModeManager
            var buildCtrl = Object.FindObjectOfType<BuildController>();
            if (buildCtrl != null)
            {
                buildCtrl.furniturePrefab = lavaPrefab;
            }

            // 6. 清理旧的遗留测试家具
            var oldFurnitures = Object.FindObjectsOfType<FurnitureObject>();
            foreach (var f in oldFurnitures)
            {
                if (f != null && f.gameObject != null)
                {
                    Object.DestroyImmediate(f.gameObject);
                }
            }

            // 7. 战场开局纯净：移除所有预置展示家具，家具全部由玩家在背壳工坊中合成入库后部署！
            GameObject furnitureRoot = GameObject.Find("Preplaced_Biomechanical_Furnitures");
            if (furnitureRoot != null)
            {
                Object.DestroyImmediate(furnitureRoot);
            }

            // 8. 清理所有场景怪物：开局战场纯净无怪，怪兽仅由地图边缘裂隙门在开启后生成
            RemoveAllEnemies();

            // 9. 在地图北侧边缘部署异界裂隙之门
            PlacePortalDoorAtEdge(adapter, lavaPrefab);

            // 10. 更新 EditorBuildSettings
            EnsureSceneInBuildSettings(scenePath);

            // 11. 持久化保存新场景
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"<color=#38bdf8>[BiomechanicalCombatSceneBuilder]</color> 成功构建并保存家具战斗协同新场景: {scenePath}");
        }

        private static GameObject SpawnPreplacedConstruct(Transform parent, GameObject prefab, BiomechanicalCombatAdapter adapter,
            string id, string name, string category, bool isScrap, int p, int e, int g, int w, Color color, Vector3 pos)
        {
            if (prefab == null) return null;
            GameObject go = Object.Instantiate(prefab, pos, Quaternion.identity, parent);
            go.name = $"{id}_Showcase";

            var prod = BiomechanicalCombatAdapter.CreateSampleProduct(id, name, category, isScrap, p, e, g, w, color);
            var furn = go.GetComponent<FurnitureObject>() ?? go.AddComponent<FurnitureObject>();
            furn.instanceId = id;
            furn.furnitureName = name;
            furn.originalProduct = prod;

            float dura = g > 0 ? (120f + g * 1.5f) : 160f;
            furn.maxDurability = dura;
            furn.currentDurability = dura;
            furn.kickDamage = 60f + p * 0.8f;
            furn.kickGuardBreak = 80f + p * 0.5f;

            if (furn.furnitureRenderer != null)
            {
                furn.furnitureRenderer.color = color;
            }

            adapter.AttachTacticalAura(go, prod);
            furn.SnapToNearestGrid();
            return go;
        }

        private static void RemoveAllEnemies()
        {
            var demons = Object.FindObjectsOfType<EnemyController>();
            foreach (var d in demons)
            {
                if (d != null && d.gameObject != null)
                {
                    Object.DestroyImmediate(d.gameObject);
                }
            }

            var player = Object.FindObjectOfType<PlayerController>();
            if (player != null && player.currentTarget != null)
            {
                player.ClearAttackTarget();
            }
        }

        private static void PlacePortalDoorAtEdge(BiomechanicalCombatAdapter adapter, GameObject prefab)
        {
            var existingDoor = Object.FindObjectOfType<GatherDoorAuraBehavior>();
            if (existingDoor != null)
            {
                return;
            }

            if (prefab == null) return;
            Vector3 doorPos = new Vector3(0.0f, 5.0f, 0f);
            GameObject go = Object.Instantiate(prefab, doorPos, Quaternion.identity);
            go.name = "Preplaced_Gather_Door";

            var prod = BiomechanicalCombatAdapter.CreateSampleProduct(
                "furn_gather_door", "精制·异界裂隙之门", "空间设施", false, 25, 25, 25, 25, new Color(0.2f, 0.9f, 0.8f)
            );
            var furn = go.GetComponent<FurnitureObject>() ?? go.AddComponent<FurnitureObject>();
            furn.instanceId = "furn_gather_door";
            furn.furnitureName = "精制·异界裂隙之门";
            furn.originalProduct = prod;

            furn.maxDurability = 300f;
            furn.currentDurability = 300f;
            furn.kickDamage = 0f;
            furn.kickGuardBreak = 0f;

            if (furn.furnitureRenderer != null)
            {
                var realSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/FurnitureSprites/furn_gather_door.png");
                if (realSprite != null)
                {
                    furn.furnitureRenderer.sprite = realSprite;
                    furn.furnitureRenderer.color = Color.white;
                }
            }

            if (furn.furnitureCollider is CircleCollider2D col)
            {
                col.radius = 0.52f;
                col.offset = new Vector2(0f, 0.12f);
            }

            adapter.AttachTacticalAura(go, prod);
            furn.SnapToNearestGrid();
        }

        private static void EnsureSceneInBuildSettings(string scenePath)
        {
            var currentScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool exists = false;
            foreach (var s in currentScenes)
            {
                if (s.path == scenePath)
                {
                    s.enabled = true;
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                currentScenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = currentScenes.ToArray();
            }
        }
    }
}
