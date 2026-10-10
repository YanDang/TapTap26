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

            // 3. 配置 CombatSynergyHUD
            var synergyHudObj = GameObject.Find("CombatSynergyHUD");
            if (synergyHudObj == null)
            {
                synergyHudObj = new GameObject("CombatSynergyHUD");
            }
            if (synergyHudObj.GetComponent<CombatSynergyHUD>() == null)
            {
                synergyHudObj.AddComponent<CombatSynergyHUD>();
            }

            // 4. 配置 CombatSceneCraftingEntrance
            var entrance = Object.FindObjectOfType<CombatSceneCraftingEntrance>();
            if (entrance != null)
            {
                entrance.craftingSceneName = "Mix";
                entrance.shortcutKey = KeyCode.B;
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

            // 8. 配置多只恶魔敌人以供完整战斗协同测试
            ConfigureDemonEnemies();

            // 9. 更新 EditorBuildSettings
            EnsureSceneInBuildSettings(scenePath);

            // 10. 持久化保存新场景
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

        private static void ConfigureDemonEnemies()
        {
            var demons = Object.FindObjectsOfType<EnemyController>();
            GameObject baseDemonGo = demons.Length > 0 ? demons[0].gameObject : null;

            if (baseDemonGo != null)
            {
                // 恶魔 1: 位于水电回路区域附近
                baseDemonGo.name = "Enemy_Demon_WaterElec";
                baseDemonGo.transform.position = new Vector3(3.8f, 2.8f, 0f);
                demons[0].autoRespawn = true;
                demons[0].respawnDelay = 4.5f;

                // 若只有1只，再克隆出另外两只部署在防线和驯化区
                if (demons.Length < 2)
                {
                    // 恶魔 2: 位于珊瑚墙与弹力跳板前方
                    GameObject demon2 = Object.Instantiate(baseDemonGo, new Vector3(-4.5f, 2.2f, 0f), Quaternion.identity);
                    demon2.name = "Enemy_Demon_Chokepoint";
                    var ctrl2 = demon2.GetComponent<EnemyController>();
                    if (ctrl2 != null)
                    {
                        ctrl2.autoRespawn = true;
                        ctrl2.respawnDelay = 5.0f;
                    }

                    // 恶魔 3: 位于培养箱附近巡逻
                    GameObject demon3 = Object.Instantiate(baseDemonGo, new Vector3(1.4f, 4.2f, 0f), Quaternion.identity);
                    demon3.name = "Enemy_Demon_Incubator";
                    var ctrl3 = demon3.GetComponent<EnemyController>();
                    if (ctrl3 != null)
                    {
                        ctrl3.autoRespawn = true;
                        ctrl3.respawnDelay = 5.5f;
                    }
                }
            }
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
