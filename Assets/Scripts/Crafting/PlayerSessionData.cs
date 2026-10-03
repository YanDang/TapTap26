using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局跨场景会话数据持久化管理器：
/// 使得玩家在【战斗场景】与【合成工坊场景】之间无缝切换时，
/// 保持当前手持武器、拥有材料以及返回场景路径的状态持久化。
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

    /// <summary>
    /// 初始化或获取共享材料库
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
