using System;
using System.Collections.Generic;
using UnityEngine;
using BiomechanicalCrafting;

/// <summary>
/// 全局跨场景会话数据持久化管理器：
/// 使得玩家在【战斗场景】、【采集温室场景】与【合成工坊场景】之间无缝切换时，
/// 保持当前手持武器、背壳背包材料、拥有材料以及返回场景路径的状态持久化。
/// </summary>
public static class PlayerSessionData
{
    // 当前玩家手持/装备的武器
    public static WeaponData currentEquippedWeapon = null;

    // 玩家背包材料库（在两个场景间共享，工坊合成消耗或采集获得）
    public static List<MaterialItem> playerMaterials = null;

    // 战斗场景名称（以便工坊合成完毕后精准返回）
    public static string returnCombatSceneName = "TileTest";

    // 状态标记：刚从工坊合成归来
    public static bool hasJustReturnedFromCrafting = false;

    // 当前是否正处于工坊开启状态（用于无缝叠加模式）
    public static bool isCraftingOpen = false;

    // 玩家在温室采集到的生体机械材料库历史记录
    public static List<BiomechanicalMaterial> gatheredBiomechanicalMaterials = new List<BiomechanicalMaterial>();

    // =========================================================================
    // 玩家已合成生体家具与废料构装仓库 (Crafted Furniture & Scrap Inventory)
    // 存放工坊合成出的成品，不占用背壳材料背包，供战斗场景部署使用！
    // =========================================================================
    public static List<CraftedProduct> craftedProducts = new List<CraftedProduct>();
    private const string PrefKey_CraftedProducts = "TAP_CRAFTED_PRODUCTS_DATA_V2";

    /// <summary>
    /// 清空仓库中的已合成构装成品（保证开局纯净）
    /// </summary>
    public static void ClearCraftedProducts()
    {
        craftedProducts.Clear();
        PlayerPrefs.DeleteKey(PrefKey_CraftedProducts);
        PlayerPrefs.Save();
    }

    [Serializable]
    public class SavedProductListWrapper
    {
        public List<CraftedProduct> products = new List<CraftedProduct>();
    }

    /// <summary>
    /// 获取当前仓库中拥有的已合成构装家具与战术废料列表
    /// </summary>
    public static List<CraftedProduct> GetCraftedProducts()
    {
        EnsureInitialized();
        return craftedProducts;
    }

    /// <summary>
    /// 将新合成的构装成品存入仓库
    /// </summary>
    public static void AddCraftedProduct(CraftedProduct product)
    {
        if (product == null) return;
        EnsureInitialized();
        craftedProducts.Add(product);
        SaveCraftedProductsToStorage();
    }

    /// <summary>
    /// 从仓库中消耗/部署指定构装成品
    /// </summary>
    public static bool RemoveCraftedProduct(CraftedProduct product)
    {
        EnsureInitialized();
        bool removed = craftedProducts.Remove(product);
        if (removed) SaveCraftedProductsToStorage();
        return removed;
    }

    /// <summary>
    /// 按 ID 消耗一件构装成品
    /// </summary>
    public static bool RemoveCraftedProductById(string productId)
    {
        EnsureInitialized();
        int idx = craftedProducts.FindIndex(p => p != null && p.id == productId);
        if (idx >= 0)
        {
            craftedProducts.RemoveAt(idx);
            SaveCraftedProductsToStorage();
            return true;
        }
        return false;
    }

    public static void SaveCraftedProductsToStorage()
    {
        try
        {
            SavedProductListWrapper wrapper = new SavedProductListWrapper();
            wrapper.products = new List<CraftedProduct>(craftedProducts);
            string json = JsonUtility.ToJson(wrapper);
            PlayerPrefs.SetString(PrefKey_CraftedProducts, json);
            PlayerPrefs.Save();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PlayerSessionData] 保存家具仓库失败: {ex}");
        }
    }

    public static void LoadCraftedProductsFromStorage()
    {
        try
        {
            craftedProducts.Clear();
            if (PlayerPrefs.HasKey(PrefKey_CraftedProducts))
            {
                string json = PlayerPrefs.GetString(PrefKey_CraftedProducts, "");
                if (!string.IsNullOrEmpty(json))
                {
                    SavedProductListWrapper wrapper = JsonUtility.FromJson<SavedProductListWrapper>(json);
                    if (wrapper != null && wrapper.products != null)
                    {
                        craftedProducts = wrapper.products;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PlayerSessionData] 加载家具仓库失败: {ex}");
        }
    }

    // =========================================================================
    // 背壳背包跨场景共享数据 (Shell Backpack Shared Slots)
    // =========================================================================
    public static ShellType currentShellType = ShellType.ConchShell;

    // 当前背壳背包各槽位的材料数据 (长度 32，初始全部为空 null)
    public static BiomechanicalMaterial[] shellSlotItems = new BiomechanicalMaterial[32];

    private const string PrefKey_ShellBackpack = "TAP_SHELL_BACKPACK_DATA";
    private static bool isInitialized = false;

    [Serializable]
    public class SavedSlotEntry
    {
        public int slotId;
        public string materialId;
        public int rootSlotId;
        public int[] occupiedSlots;
    }

    [Serializable]
    public class SavedBackpackData
    {
        public int shellType;
        public List<SavedSlotEntry> entries = new List<SavedSlotEntry>();
    }

    /// <summary>
    /// 确保背壳数据已从本地存储加载（首次或跨场景/跨运行保持持久）
    /// </summary>
    public static void EnsureInitialized()
    {
        if (isInitialized) return;
        isInitialized = true;
        LoadBackpackFromStorage();
        LoadCraftedProductsFromStorage();
    }

    /// <summary>
    /// 将工坊背壳中的最新槽位数据持久化保存（同时写入静态数组与本地持久化 PlayerPrefs）
    /// </summary>
    public static void SaveBackpackSlots(BiomechanicalMaterial[] items)
    {
        if (items != null)
        {
            for (int i = 0; i < shellSlotItems.Length; i++)
            {
                shellSlotItems[i] = (i < items.Length) ? items[i] : null;
            }
        }
        SaveBackpackToStorage();
    }

    public static void SaveBackpackToStorage()
    {
        try
        {
            SavedBackpackData data = new SavedBackpackData();
            data.shellType = (int)currentShellType;

            for (int i = 0; i < shellSlotItems.Length; i++)
            {
                var mat = shellSlotItems[i];
                if (mat != null && !string.IsNullOrEmpty(mat.id))
                {
                    data.entries.Add(new SavedSlotEntry
                    {
                        slotId = i,
                        materialId = mat.id,
                        rootSlotId = mat.rootSlotId,
                        occupiedSlots = mat.occupiedSlotIds != null ? mat.occupiedSlotIds.ToArray() : new int[] { i }
                    });
                }
            }

            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(PrefKey_ShellBackpack, json);
            PlayerPrefs.Save();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PlayerSessionData] 保存背包至本地存储失败: {ex}");
        }
    }

    public static void LoadBackpackFromStorage()
    {
        try
        {
            for (int i = 0; i < shellSlotItems.Length; i++)
            {
                shellSlotItems[i] = null;
            }

            if (!PlayerPrefs.HasKey(PrefKey_ShellBackpack))
            {
                // 首次进入游戏，背包初始全空！
                return;
            }

            string json = PlayerPrefs.GetString(PrefKey_ShellBackpack, "");
            if (string.IsNullOrEmpty(json)) return;

            SavedBackpackData data = JsonUtility.FromJson<SavedBackpackData>(json);
            if (data == null) return;

            currentShellType = (ShellType)data.shellType;

            // 建立已恢复的 2x2 引用字典，防止 2x2 的 4 个格子被实例化成 4 个独立对象
            Dictionary<int, BiomechanicalMaterial> restoredRootMats = new Dictionary<int, BiomechanicalMaterial>();

            foreach (var entry in data.entries)
            {
                if (entry.slotId < 0 || entry.slotId >= shellSlotItems.Length) continue;

                if (restoredRootMats.TryGetValue(entry.rootSlotId, out var existingMat))
                {
                    shellSlotItems[entry.slotId] = existingMat;
                }
                else
                {
                    var baseMat = BiomechanicalMaterialDatabase.GetMaterial(entry.materialId);
                    if (baseMat != null)
                    {
                        var cloned = baseMat.Clone();
                        cloned.rootSlotId = entry.rootSlotId;
                        if (entry.occupiedSlots != null)
                        {
                            cloned.occupiedSlotIds = new List<int>(entry.occupiedSlots);
                        }
                        else
                        {
                            cloned.occupiedSlotIds = new List<int> { entry.slotId };
                        }

                        shellSlotItems[entry.slotId] = cloned;
                        if (cloned.isMajor)
                        {
                            restoredRootMats[cloned.rootSlotId] = cloned;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PlayerSessionData] 从本地存储加载背包失败: {ex}");
        }
    }

    /// <summary>
    /// 获取当前背壳总槽位数
    /// </summary>
    public static int GetTotalSlotCount()
    {
        EnsureInitialized();
        var config = ShellConfig.CreateConfig(currentShellType);
        return config != null ? config.slots.Count : 24;
    }

    /// <summary>
    /// 获取当前已占用槽位数
    /// </summary>
    public static int GetOccupiedSlotCount()
    {
        EnsureInitialized();
        int count = 0;
        int total = GetTotalSlotCount();
        for (int i = 0; i < total && i < shellSlotItems.Length; i++)
        {
            if (shellSlotItems[i] != null) count++;
        }
        return count;
    }

    /// <summary>
    /// 采集系统核心接口：
    /// 尝试按顺序将采集到的生体材料放入合成背包。
    /// - 1x1 辅料：按顺序寻找第一个空白槽位放入；
    /// - 2x2 骨架：按顺序寻找第一个能完整容纳 4 格的连续拓扑空间；
    /// - 若背包已满或无空间，返回 false，材料无法进入！
    /// </summary>
    public static bool TryAddMaterialToShellBackpack(BiomechanicalMaterial rawMat, out int targetSlotId, out string failReason)
    {
        EnsureInitialized();
        targetSlotId = -1;
        failReason = "";
        if (rawMat == null)
        {
            failReason = "材料数据无效";
            return false;
        }

        var config = ShellConfig.CreateConfig(currentShellType);
        if (config == null || config.slots.Count == 0)
        {
            failReason = "背壳拓扑配置未就绪";
            return false;
        }

        // 1. 如果是 2x2 骨架核心，按顺序寻找第一个能够容纳 2x2 的空位 (4 格全部为空且有效)
        if (rawMat.isMajor)
        {
            foreach (var slot in config.slots)
            {
                var ids = config.Get2x2SlotIds(slot.gridCoord.x, slot.gridCoord.y);
                if (ids != null && ids.Count == 4)
                {
                    bool allFree = true;
                    foreach (int id in ids)
                    {
                        if (id >= shellSlotItems.Length || shellSlotItems[id] != null)
                        {
                            allFree = false;
                            break;
                        }
                    }

                    if (allFree)
                    {
                        // 找到按顺序的第一个合法 2x2 空间！
                        var cloned = rawMat.Clone();
                        cloned.rootSlotId = ids[0];
                        cloned.occupiedSlotIds = new List<int>(ids);

                        foreach (int id in ids)
                        {
                            shellSlotItems[id] = cloned;
                        }

                        targetSlotId = ids[0];
                        SaveBackpackToStorage(); // 立即持久化存盘
                        return true;
                    }
                }
            }

            failReason = "背壳空间不足！无法容纳 2x2 核心骨架";
            return false;
        }
        // 2. 如果是 1x1 生体辅料，按顺序寻找第一个为空的槽位
        else
        {
            for (int i = 0; i < config.slots.Count; i++)
            {
                if (i < shellSlotItems.Length && shellSlotItems[i] == null)
                {
                    // 找到按顺序的第一个空槽位！
                    var cloned = rawMat.Clone();
                    cloned.rootSlotId = i;
                    cloned.occupiedSlotIds = new List<int> { i };

                    shellSlotItems[i] = cloned;
                    targetSlotId = i;
                    SaveBackpackToStorage(); // 立即持久化存盘
                    return true;
                }
            }

            failReason = "背壳背包已满！所有槽位均已占满";
            return false;
        }
    }

    /// <summary>
    /// 初始化或获取共享武器材料库
    /// </summary>
    public static List<MaterialItem> GetOrCreateMaterials()
    {
        if (playerMaterials == null || playerMaterials.Count == 0)
        {
            playerMaterials = MaterialItem.CreateDefaultMaterialLibrary();
        }
        return playerMaterials;
    }
}
