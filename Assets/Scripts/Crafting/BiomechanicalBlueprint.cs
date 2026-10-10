using System;
using System.Collections.Generic;
using UnityEngine;

namespace BiomechanicalCrafting
{
    /// <summary>
    /// 生体机械配方/蓝图实体 (Biomechanical Blueprint)
    /// 承载图鉴展示数据、关键构件需求与 P/E/G/W 数值换算规则
    /// </summary>
    [Serializable]
    public class BiomechanicalBlueprint
    {
        public string id = "bp_default";
        public string name = "未知生体蓝图";
        public string category = "家具设施"; // 电网设施, 增生防线, 气压设施, 潮润设施, 活性杂交废料
        public bool isAberrant = false;
        public string primaryElement = "None";
        public List<string> requiredCoreIds = new List<string>();
        public List<string> requiredAnyIds = new List<string>();
        public int minNodes = 2;
        public string flavor = "";
        public string formulaDescription = "";

        [NonSerialized]
        private Sprite cachedIcon = null;

        public Sprite GetIconSprite()
        {
            if (cachedIcon != null) return cachedIcon;

            // 优先检查是否有配套的真实家具/废料高清精灵图片
            if (!string.IsNullOrEmpty(id))
            {
                var directFurnSprite = Resources.Load<Sprite>("FurnitureSprites/" + id);
                if (directFurnSprite != null)
                {
                    cachedIcon = directFurnSprite;
                    return cachedIcon;
                }
            }

            // 优先采用首个核心材料的图标
            if (requiredCoreIds != null && requiredCoreIds.Count > 0)
            {
                var coreMat = BiomechanicalMaterialDatabase.GetById(requiredCoreIds[0]);
                if (coreMat != null)
                {
                    cachedIcon = coreMat.GetOrLoadSprite();
                    return cachedIcon;
                }
            }

            // 备用根据元素寻找一个代表性图标
            var all = BiomechanicalMaterialDatabase.GetAllMaterials();
            foreach (var m in all)
            {
                if (m.element == primaryElement)
                {
                    cachedIcon = m.GetOrLoadSprite();
                    break;
                }
            }

            return cachedIcon;
        }

        public Color ThemeColor
        {
            get
            {
                switch (primaryElement)
                {
                    case "Gas": return new Color(0.75f, 0.52f, 0.99f);
                    case "Electric": return new Color(0.98f, 0.82f, 0.18f);
                    case "Wood": return new Color(0.29f, 0.85f, 0.44f);
                    case "Water": return new Color(0.22f, 0.74f, 0.97f);
                    default: return isAberrant ? new Color(0.95f, 0.45f, 0.85f) : Color.white;
                }
            }
        }
    }

    [Serializable]
    public class BlueprintListWrapper
    {
        public List<BiomechanicalBlueprint> blueprints;
    }

    /// <summary>
    /// 全局蓝图/图鉴数据库管理器
    /// </summary>
    public static class BiomechanicalBlueprintDatabase
    {
        private static List<BiomechanicalBlueprint> _cachedList = null;
        private static Dictionary<string, BiomechanicalBlueprint> _dictById = null;

        public static List<BiomechanicalBlueprint> GetAllBlueprints()
        {
            if (_cachedList == null)
            {
                LoadFromJson();
            }
            return _cachedList;
        }

        public static BiomechanicalBlueprint GetById(string id)
        {
            GetAllBlueprints();
            if (_dictById != null && _dictById.TryGetValue(id, out var bp))
            {
                return bp;
            }
            return null;
        }

        public static void Reload()
        {
            _cachedList = null;
            _dictById = null;
            LoadFromJson();
        }

        private static void LoadFromJson()
        {
            _cachedList = new List<BiomechanicalBlueprint>();
            _dictById = new Dictionary<string, BiomechanicalBlueprint>();

            TextAsset jsonAsset = Resources.Load<TextAsset>("BiomechanicalBlueprints");
            if (jsonAsset != null && !string.IsNullOrEmpty(jsonAsset.text))
            {
                try
                {
                    BlueprintListWrapper wrapper = JsonUtility.FromJson<BlueprintListWrapper>(jsonAsset.text);
                    if (wrapper != null && wrapper.blueprints != null)
                    {
                        foreach (var b in wrapper.blueprints)
                        {
                            if (b != null && !string.IsNullOrEmpty(b.id))
                            {
                                _cachedList.Add(b);
                                _dictById[b.id] = b;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BiomechanicalBlueprintDatabase] JSON 解析失败: {ex.Message}");
                }
            }
            else
            {
                Debug.LogError("[BiomechanicalBlueprintDatabase] 未能加载 Resources/BiomechanicalBlueprints.json！");
            }
        }
    }
}
