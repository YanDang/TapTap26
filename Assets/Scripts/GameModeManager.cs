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

    // 当前是否在建造模式
    public bool IsBuildMode { get; private set; } = false;

    void Start()
    {
        // 自动查找引用
        if (buildController == null)
            buildController = FindObjectOfType<BuildController>();
        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>();

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

        // 默认进入探索模式
        ExitBuildMode();
    }

    /// <summary>
    /// 进入建造模式
    /// </summary>
    public void EnterBuildMode()
    {
        IsBuildMode = true;

        // 启用建造，禁用玩家点击移动
        if (buildController != null)
            buildController.SetBuildMode(true);

        if (playerController != null)
            playerController.canMove = false;

        // 切换 UI 面板
        if (mainHUD != null)
            mainHUD.SetActive(false);

        if (buildPanel != null)
            buildPanel.SetActive(true);

        if (statusText != null)
            statusText.text = "【建造模式】点击空地放置，点击物品拆除";

        Debug.Log("[GameModeManager] 已切换至：建造模式");
    }

    /// <summary>
    /// 退出建造模式（返回探索模式）
    /// </summary>
    public void ExitBuildMode()
    {
        IsBuildMode = false;

        // 禁用建造，恢复玩家点击移动
        if (buildController != null)
            buildController.SetBuildMode(false);

        if (playerController != null)
            playerController.canMove = true;

        // 切换 UI 面板
        if (mainHUD != null)
            mainHUD.SetActive(true);

        if (buildPanel != null)
            buildPanel.SetActive(false);

        Debug.Log("[GameModeManager] 已切换至：探索模式");
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
