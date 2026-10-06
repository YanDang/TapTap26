using System;
using System.Collections.Generic;
using UnityEngine;

// Biomechanical Crafting System - P/E/G/W Dynamics Database

namespace BiomechanicalCrafting
{
    /// <summary>
    /// 生体机械四大核心元素分类
    /// </summary>
    public enum BiomechanicalElement
    {
        None = 0,
        Gas = 1,        // 【气 / 压力 P】：充气、浮力、喷射、泄压
        Electric = 2,   // 【电 / 脉冲 E】：放电、神经、电解、磁极
        Wood = 3,       // 【木 / 增生 G】：珊瑚、菌群、齿轮生根、甲壳
        Water = 4       // 【水 / 潮润 W】：心室水压、海绵、虹吸、藻丝、复眼
    }

    /// <summary>
    /// 生体机械材料实体 (Biomechanical Material)
    /// 属性均从 Resources/BiomechanicalMaterials.json 动态配置加载，彻底去除硬编码
    /// </summary>
    [Serializable]
    public class BiomechanicalMaterial
    {
        public string id = "mat_default";
        public string materialName = "未知生体组织";
        public string element = "None"; // 字符串形式方便 JSON 兼容
        
        [Tooltip("是否为大格骨架物品（决定占用 2x2 格子还是 1x1 格子）")]
        public bool isMajor = false;

        public int width = 1;
        public int height = 1;

        public int Width => isMajor ? 2 : 1;
        public int Height => isMajor ? 2 : 1;
        public string SizeLabel => isMajor ? "2x2 骨架核心" : "1x1 生体辅料";
        public string CategoryName => isMajor ? "核心骨架 (2x2)" : "生体辅料 (1x1)";

        [NonSerialized] public int rootSlotId = -1;
        [NonSerialized] public List<int> occupiedSlotIds = new List<int>();

        public string shortDescription = "";

        [Header("四大元素核心参数")]
        public int p = 0; // 【压力值 P】
        public int e = 0; // 【电荷律 E】
        public int g = 0; // 【生物活性 / 增生力 G】
        public int w = 0; // 【流体通量 W】

        [NonSerialized]
        public Sprite iconSprite = null;

        /// <summary>
        /// 获取元素枚举
        /// </summary>
        public BiomechanicalElement ElementEnum
        {
            get
            {
                if (Enum.TryParse<BiomechanicalElement>(element, true, out var result))
                    return result;
                return BiomechanicalElement.None;
            }
        }

        /// <summary>
        /// 获取元素对应的纯净生体主题色
        /// </summary>
        public Color ElementColor
        {
            get
            {
                switch (ElementEnum)
                {
                    case BiomechanicalElement.Gas:
                        return new Color(0.75f, 0.52f, 0.99f); // 荧光紫
                    case BiomechanicalElement.Electric:
                        return new Color(0.98f, 0.82f, 0.18f); // 暖黄弧光
                    case BiomechanicalElement.Wood:
                        return new Color(0.29f, 0.85f, 0.44f); // 翠绿增生
                    case BiomechanicalElement.Water:
                        return new Color(0.22f, 0.74f, 0.97f); // 潮润青蓝
                    default:
                        return new Color(0.85f, 0.88f, 0.92f); // 浅灰合金
                }
            }
        }

        /// <summary>
        /// 元素中文标识
        /// </summary>
        public string ElementName
        {
            get
            {
                switch (ElementEnum)
                {
                    case BiomechanicalElement.Gas: return "气 / 压力 (P)";
                    case BiomechanicalElement.Electric: return "电 / 脉冲 (E)";
                    case BiomechanicalElement.Wood: return "木 / 增生 (G)";
                    case BiomechanicalElement.Water: return "水 / 潮润 (W)";
                    default: return "通用";
                }
            }
        }

        /// <summary>
        /// 加载图标精灵（优先从 Unity 内置切片的精灵图集 BiomechanicalMaterialsSheet 中索引加载）
        /// </summary>
        public Sprite GetOrLoadSprite()
        {
            if (iconSprite != null) return iconSprite;

            var sheetSprite = BiomechanicalMaterialDatabase.GetSpriteFromSheet(this.id);
            if (sheetSprite != null)
            {
                iconSprite = sheetSprite;
                return iconSprite;
            }
            return iconSprite;
        }

        /// <summary>
        /// 生成简明增量属性文本 (用于槽位浮动角标，如: P+10 E+65)
        /// </summary>
        public string GetCompactDeltaString()
        {
            List<string> parts = new List<string>();
            if (p > 0) parts.Add($"<color=#C085FF>P{p}</color>");
            if (e > 0) parts.Add($"<color=#FBD12E>E{e}</color>");
            if (g > 0) parts.Add($"<color=#4AE070>G{g}</color>");
            if (w > 0) parts.Add($"<color=#38BDF8>W{w}</color>");
            return parts.Count > 0 ? string.Join(" ", parts) : "<color=#AAAAAA>+0</color>";
        }

        /// <summary>
        /// 生成标准四维要素增量文本 (用于主面板 HUD 实时显示)
        /// </summary>
        public string GetStatsSummary()
        {
            return $"<color=#C085FF>+💨 P:{p}</color>  <color=#FBD12E>+⚡ E:{e}</color>  <color=#4AE070>+🌿 G:{g}</color>  <color=#38BDF8>+💧 W:{w}</color>";
        }

        /// <summary>
        /// 复制实例
        /// </summary>
        public BiomechanicalMaterial Clone()
        {
            return new BiomechanicalMaterial
            {
                id = this.id,
                materialName = this.materialName,
                element = this.element,
                isMajor = this.isMajor,
                width = this.Width,
                height = this.Height,
                shortDescription = this.shortDescription,
                p = this.p,
                e = this.e,
                g = this.g,
                w = this.w,
                iconSprite = this.iconSprite
            };
        }
    }

    [Serializable]
    public class MaterialListWrapper
    {
        public List<BiomechanicalMaterial> materials;
    }

    /// <summary>
    /// 全局材料数据库查询与缓存工具（从 JSON 统一加载与持久化）
    /// </summary>
    public static class BiomechanicalMaterialDatabase
    {
        private static List<BiomechanicalMaterial> _cachedList = null;
        private static Dictionary<string, BiomechanicalMaterial> _dictById = null;
        private static Dictionary<string, Sprite> _sheetSpriteCache = null;

        public static Sprite GetSpriteFromSheet(string spriteName)
        {
            if (_sheetSpriteCache == null)
            {
                _sheetSpriteCache = new Dictionary<string, Sprite>();

                // 优先从 Resources 加载已切片的图集子精灵 (在 Editor 及 Standalone 打包客户端中均完美支持)
                Sprite[] resSprites = Resources.LoadAll<Sprite>("BiomechanicalMaterialsSheet");
                if (resSprites != null && resSprites.Length > 0)
                {
                    foreach (var s in resSprites)
                    {
                        if (s != null && !string.IsNullOrEmpty(s.name))
                        {
                            _sheetSpriteCache[s.name] = s;
                        }
                    }
                }

#if UNITY_EDITOR
                if (_sheetSpriteCache.Count == 0)
                {
                    string sheetPath = "Assets/Resources/BiomechanicalMaterialsSheet.png";
                    var allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(sheetPath);
                    if (allAssets == null || allAssets.Length == 0)
                    {
                        sheetPath = "Assets/Art/Materials/BiomechanicalMaterialsSheet.png";
                        allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(sheetPath);
                    }
                    if (allAssets != null)
                    {
                        foreach (var a in allAssets)
                        {
                            if (a is Sprite s)
                            {
                                _sheetSpriteCache[s.name] = s;
                            }
                        }
                    }
                }
#endif
            }

            if (_sheetSpriteCache.TryGetValue(spriteName, out var sprite))
            {
                return sprite;
            }
            return null;
        }

        public static List<BiomechanicalMaterial> GetAllMaterials()
        {
            if (_cachedList == null)
            {
                LoadFromJson();
            }
            return _cachedList;
        }

        public static BiomechanicalMaterial GetById(string id)
        {
            GetAllMaterials();
            if (_dictById != null && _dictById.TryGetValue(id, out var mat))
            {
                return mat;
            }
            return null;
        }

        public static void Reload()
        {
            _cachedList = null;
            _dictById = null;
            _sheetSpriteCache = null;
            LoadFromJson();
        }

        private static void LoadFromJson()
        {
            _cachedList = new List<BiomechanicalMaterial>();
            _dictById = new Dictionary<string, BiomechanicalMaterial>();

            TextAsset jsonAsset = Resources.Load<TextAsset>("BiomechanicalMaterials");
            if (jsonAsset != null && !string.IsNullOrEmpty(jsonAsset.text))
            {
                try
                {
                    MaterialListWrapper wrapper = JsonUtility.FromJson<MaterialListWrapper>(jsonAsset.text);
                    if (wrapper != null && wrapper.materials != null)
                    {
                        foreach (var m in wrapper.materials)
                        {
                            if (m != null && !string.IsNullOrEmpty(m.id))
                            {
                                m.GetOrLoadSprite();
                                _cachedList.Add(m);
                                _dictById[m.id] = m;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BiomechanicalMaterialDatabase] JSON 解析失败: {ex.Message}");
                }
            }
            else
            {
                Debug.LogError("[BiomechanicalMaterialDatabase] 未能加载 Resources/BiomechanicalMaterials.json！");
            }
        }
    }
}
