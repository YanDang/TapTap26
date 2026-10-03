using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 合成管理器：
/// 控制当前选中的 1~3 个材料、当前激活的蓝图，并提供实时的配方校验与属性预览。
/// 支持一键将锻造出的武器直接装备给场景中的玩家角色。
/// </summary>
public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance { get; private set; }

    [Header("Inventory & Blueprints")]
    public List<MaterialItem> availableMaterials = new List<MaterialItem>();
    public List<CraftingBlueprint> availableBlueprints = new List<CraftingBlueprint>();

    [Header("Current Crafting Workspace (当前工作台状态)")]
    public int currentBlueprintIndex = 0;
    public List<MaterialItem> selectedMaterials = new List<MaterialItem>();
    public const int MAX_SELECTED_MATERIALS = 3;

    [Header("Real-time Output Preview")]
    public CraftingResult currentPreviewResult = new CraftingResult();

    [Header("Player Target (若存在，合成后可直接装备)")]
    public PlayerController playerController;

    // 事件通知
    public event Action OnWorkspaceChanged;
    public event Action<CraftingResult> OnCraftSuccess;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        if (availableMaterials.Count == 0)
        {
            availableMaterials = MaterialItem.CreateDefaultMaterialLibrary();
        }

        if (availableBlueprints.Count == 0)
        {
            availableBlueprints = CraftingBlueprint.CreateDefaultBlueprints();
        }

        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
        }

        UpdatePreview();
    }

    /// <summary>
    /// 获取当前选中的蓝图
    /// </summary>
    public CraftingBlueprint CurrentBlueprint
    {
        get
        {
            if (availableBlueprints == null || availableBlueprints.Count == 0) return null;
            if (currentBlueprintIndex < 0 || currentBlueprintIndex >= availableBlueprints.Count)
                currentBlueprintIndex = 0;
            return availableBlueprints[currentBlueprintIndex];
        }
    }

    /// <summary>
    /// 切换蓝图
    /// </summary>
    public void SelectBlueprint(int index)
    {
        if (index >= 0 && index < availableBlueprints.Count)
        {
            currentBlueprintIndex = index;
            UpdatePreview();
            OnWorkspaceChanged?.Invoke();
        }
    }

    /// <summary>
    /// 向工作台放入材料（最多 3 个）
    /// </summary>
    public bool AddMaterial(MaterialItem material)
    {
        if (material == null) return false;

        if (selectedMaterials.Count >= MAX_SELECTED_MATERIALS)
        {
            Debug.LogWarning($"[CraftingManager] 最多只能够选择 {MAX_SELECTED_MATERIALS} 个材料！");
            return false;
        }

        selectedMaterials.Add(material);
        UpdatePreview();
        OnWorkspaceChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 从工作台槽位移除材料
    /// </summary>
    public void RemoveMaterialAt(int index)
    {
        if (index >= 0 && index < selectedMaterials.Count)
        {
            selectedMaterials.RemoveAt(index);
            UpdatePreview();
            OnWorkspaceChanged?.Invoke();
        }
    }

    /// <summary>
    /// 清空工作台槽位
    /// </summary>
    public void ClearSelectedMaterials()
    {
        selectedMaterials.Clear();
        UpdatePreview();
        OnWorkspaceChanged?.Invoke();
    }

    /// <summary>
    /// 实时重新推演预览属性
    /// </summary>
    public void UpdatePreview()
    {
        var bp = CurrentBlueprint;
        if (bp != null)
        {
            currentPreviewResult = bp.Evaluate(selectedMaterials);
        }
        else
        {
            currentPreviewResult = new CraftingResult
            {
                isValid = false,
                statusMessage = "未选择有效蓝图"
            };
        }
    }

    /// <summary>
    /// 执行合成：生成成品并尝试直接装备给场景中的玩家
    /// </summary>
    public bool ExecuteCraft(out CraftingResult result)
    {
        UpdatePreview();
        result = currentPreviewResult;

        if (!result.isValid)
        {
            Debug.LogWarning($"[CraftingManager] 合成失败：{result.statusMessage}");
            return false;
        }

        // 若生成的是武器且存在玩家，立即装备
        if (result.category == "weapon" && result.generatedWeaponData != null)
        {
            if (playerController == null)
                playerController = FindObjectOfType<PlayerController>();

            if (playerController != null)
            {
                playerController.equippedWeapon = result.generatedWeaponData;
                Debug.Log($"<color=#10b981>[CraftingManager] 成功为玩家装备新锻造武器：{result.productName}！伤害: {result.damage}，攻速: {result.attackInterval}s，元素: {result.primaryElement} ({result.elementPotency})</color>");
                
                // 浮动文字通知
                if (DamageTextManager.Instance != null)
                {
                    DamageTextManager.Instance.ShowText(playerController.transform.position + Vector3.up * 0.8f, $"EQUIPPED: {result.productName}!", Color.green, 0.09f);
                }
            }
        }

        OnCraftSuccess?.Invoke(result);
        return true;
    }
}
