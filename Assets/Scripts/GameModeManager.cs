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
            blockCategoryButton.gameObject.SetActive(false);
        }
        if (blockBtnBg != null)
        {
            blockBtnBg.gameObject.SetActive(false);
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
    /// 激活战术生体构装建造分类
    /// </summary>
    public void SelectFurnitureCategory()
    {
        if (buildController != null)
            buildController.SetCategory(BuildController.BuildModeCategory.Furniture);

        UpdateCategoryUI();
    }

    /// <summary>
    /// 旧方块建造已废弃，自动重定向至生体构装模式
    /// </summary>
    public void SelectBlockCategory()
    {
        SelectFurnitureCategory();
    }

    private void UpdateCategoryUI()
    {
        // 彻底移除旧版“重型家具”与“防御方块”分类按键，全面由底部生体构装选择栏接管
        if (furnitureCategoryButton != null)
            furnitureCategoryButton.gameObject.SetActive(false);
        if (furnitureBtnBg != null)
            furnitureBtnBg.gameObject.SetActive(false);
        if (blockCategoryButton != null)
            blockCategoryButton.gameObject.SetActive(false);
        if (blockBtnBg != null)
            blockBtnBg.gameObject.SetActive(false);

        var catBar = GameObject.Find("CategoryBar");
        if (catBar != null) catBar.SetActive(false);

        if (statusText != null)
        {
            statusText.text = "【建造模式 · 战术构装】从底部仓库选择构装/废料部署于绿框，点击红框回收入库";
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
