using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏模式管理器：
/// - 控制【探索模式】与【建造模式】的平滑切换
/// - 建造按钮点击后进入建造界面，并启用建造系统，禁止玩家移动
/// - 退出建造后返回探索模式，恢复玩家点击移动
/// </summary>
public class GameModeManager : MonoBehaviour
{
    [Header("Controllers (核心控制器)")]
    public BuildController buildController;
    public PlayerController playerController;
    public CameraController cameraController;

    [Header("UI Panels (UI 面板)")]
    [Tooltip("主界面 HUD (包含建造按钮等)")]
    public GameObject mainHUD;
    [Tooltip("建造模式专用界面 (顶部提示与退出按钮)")]
    public GameObject buildPanel;

    [Header("UI Buttons (交互按钮)")]
    [Tooltip("主界面【建造】按钮")]
    public Button openBuildButton;
    [Tooltip("建造界面【退出建造】按钮")]
    public Button exitBuildButton;

    [Header("UI Text (状态提示文本)")]
    public Text statusText;

    [Header("Category Selection (建造分类选择)")]
    [Tooltip("放置家具分类按钮")]
    public Button furnitureCategoryButton;
    [Tooltip("放置方块分类按钮")]
    public Button blockCategoryButton;
    [Tooltip("家具按钮背景 Image")]
    public Image furnitureBtnBg;
    [Tooltip("方块按钮背景 Image")]
    public Image blockBtnBg;

    // 当前是否在建造模式
    public bool IsBuildMode { get; private set; } = false;

    void Start()
    {
        // 自动查找引用
        if (buildController == null)
            buildController = FindObjectOfType<BuildController>();
        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>();
        if (cameraController == null)
            cameraController = FindObjectOfType<CameraController>();

        // 绑定按钮事件
        if (openBuildButton != null)
        {
            openBuildButton.onClick.RemoveAllListeners();
            openBuildButton.onClick.AddListener(EnterBuildMode);
        }

        if (exitBuildButton != null)
        {
            exitBuildButton.onClick.RemoveAllListeners();
            exitBuildButton.onClick.AddListener(ExitBuildMode);
        }

        if (furnitureCategoryButton != null)
        {
            furnitureCategoryButton.onClick.RemoveAllListeners();
            furnitureCategoryButton.onClick.AddListener(SelectFurnitureCategory);
        }

        if (blockCategoryButton != null)
        {
            blockCategoryButton.onClick.RemoveAllListeners();
            blockCategoryButton.onClick.AddListener(SelectBlockCategory);
        }

        // 默认进入探索/战斗模式
        ExitBuildMode();
    }

    /// <summary>
    /// 进入建造模式：允许自由平移拖拽视角，锁定角色与战斗
    /// </summary>
    public void EnterBuildMode()
    {
        // 机制落地（需求4）：消耗 30 点精力开启建造模式，避免无成本频繁开启
        if (playerController != null)
        {
            if (!playerController.ConsumeStamina(playerController.buildModeStaminaCost))
            {
                if (DamageTextManager.Instance != null)
                {
                    DamageTextManager.Instance.ShowText(
                        playerController.transform.position + Vector3.up * 0.8f,
                        $"⚠️ 精力不足，无法开启建造！(需 {playerController.buildModeStaminaCost:0} 精力)",
                        new Color(1f, 0.85f, 0.2f),
                        0.11f
                    );
                }
                return;
            }
        }

        IsBuildMode = true;

        // 机制落地（需求4 - 塞尔达式时停）：建造模式下怪物冻结停滞，给予安全布防环境！
        EnemyController.isAIPaused = true;

        // 切换摄像机为自由平移视角模式
        if (cameraController != null)
            cameraController.SetFollowPlayer(false);

        // 启用建造，禁用玩家点击移动与攻击
        if (buildController != null)
            buildController.SetBuildMode(true);

        if (playerController != null)
        {
            playerController.canMove = false;
            playerController.ClearAttackTarget();
            playerController.StopMovement();
        }

        // 切换 UI 面板
        if (mainHUD != null)
            mainHUD.SetActive(false);

        if (buildPanel != null)
            buildPanel.SetActive(true);

        // 默认进入家具放置品类
        SelectFurnitureCategory();

        Debug.Log("[GameModeManager] 已切换至：建造模式（塞尔达式时停生效，怪物已静止）");
    }

    /// <summary>
    /// 切换为家具建造分类（玄岩熔火餐桌）
    /// </summary>
    public void SelectFurnitureCategory()
    {
        if (buildController != null)
            buildController.SetCategory(BuildController.BuildModeCategory.Furniture);

        UpdateCategoryUI(BuildController.BuildModeCategory.Furniture);
    }

    /// <summary>
    /// 切换为方块建造分类（防御方块）
    /// </summary>
    public void SelectBlockCategory()
    {
        if (buildController != null)
            buildController.SetCategory(BuildController.BuildModeCategory.Block);

        UpdateCategoryUI(BuildController.BuildModeCategory.Block);
    }

    private void UpdateCategoryUI(BuildController.BuildModeCategory category)
    {
        bool isFurniture = category == BuildController.BuildModeCategory.Furniture;

        if (furnitureBtnBg != null)
            furnitureBtnBg.color = isFurniture ? new Color(0.95f, 0.55f, 0.15f, 0.95f) : new Color(0.2f, 0.22f, 0.28f, 0.8f);

        if (blockBtnBg != null)
            blockBtnBg.color = !isFurniture ? new Color(0.2f, 0.6f, 1f, 0.95f) : new Color(0.2f, 0.22f, 0.28f, 0.8f);

        if (statusText != null)
        {
            if (isFurniture)
                statusText.text = "【建造模式 · 家具】点击绿框放置玄岩熔火餐桌，点击红框回收家具";
            else
                statusText.text = "【建造模式 · 方块】点击绿框放置防御方块，点击红框拆除方块";
        }
    }

    /// <summary>
    /// 退出建造模式（返回探索/战斗模式）：锁定摄像机跟随角色，启用短滑翻滚与自动攻击
    /// </summary>
    public void ExitBuildMode()
    {
        IsBuildMode = false;

        // 恢复怪物行动
        EnemyController.isAIPaused = false;

        // 切换摄像机为跟随玩家模式
        if (cameraController != null)
        {
            cameraController.SetFollowPlayer(true);
            if (cameraController.targetToFollow == null && playerController != null)
                cameraController.targetToFollow = playerController.transform;
        }

        // 禁用建造，恢复玩家点击移动与战斗
        if (buildController != null)
            buildController.SetBuildMode(false);

        if (playerController != null)
            playerController.canMove = true;

        // 切换 UI 面板
        if (mainHUD != null)
            mainHUD.SetActive(true);

        if (buildPanel != null)
            buildPanel.SetActive(false);

        Debug.Log("[GameModeManager] 已切换至：探索/战斗模式（视角跟随角色）");
    }

    /// <summary>
    /// 切换模式
    /// </summary>
    public void ToggleBuildMode()
    {
        if (IsBuildMode)
            ExitBuildMode();
        else
            EnterBuildMode();
    }
}
