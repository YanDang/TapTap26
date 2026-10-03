using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 战斗场景【手机竖屏适配 - 顶部状态HUD + 底部紧凑摇杆按键面板 + 无缝工坊叠加】控制器：
/// 1. 竖屏空间全面利用（需求0）：
///    - 顶部空间：常驻玩家血条、精力条 (100/100)、当前手持武器徽章，锁定敌人时展示目标血条与瘫痪条，右上角【🏗️ 建造模式】(消耗30精力)；
///    - 底部空间收缩（比例缩至 ~0.22f）：留出 78% 的广阔视野给 2.5D 战场；
///    - 底部左侧：超顺滑 360° 移动虚拟摇杆 (Virtual Joystick)；
///    - 底部右侧：超大拇指按键【⚔️ 普攻出招】、【💨 翻滚闪避 (15精力)】、【🔨 合成工坊】与动态家具互动键；
/// 2. 彻底解决场景重置 Bug（需求0.1）：
///    - 采用 LoadSceneMode.Additive 叠加模式打开工坊场景，战斗场景状态 100% 冻结保留！
///    - 返回时卸载工坊场景，怪物的血量、站位、已建成的墙壁和掉落物全部丝毫不变！
/// </summary>
public class CombatSceneCraftingEntrance : MonoBehaviour
{
    [Header("Crafting Settings")]
    public string craftingSceneName = "CraftingScene";
    public KeyCode shortcutKey = KeyCode.B;

    [Header("Mobile Portrait Layout (竖屏空间适配)")]
    [Tooltip("底部功能底座占屏幕高度的比例（默认 0.16f，释放 84%+ 广阔空间留给战场）")]
    [Range(0.12f, 0.35f)]
    public float bottomDockRatio = 0.16f;

    public const float VIRTUAL_WIDTH = 1080f;

    private PlayerController player;
    private GameModeManager gameModeManager;

    // GUI 样式
    private GUIStyle headerStyle;
    private GUIStyle statLabelStyle;
    private GUIStyle hpLabelStyle;
    private GUIStyle bigBtnPrimaryStyle;
    private GUIStyle bigBtnDodgeStyle;
    private GUIStyle bigBtnAttackStyle;
    private GUIStyle bigBtnSubStyle;
    private GUIStyle centerHintStyle;
    private GUIStyle topBuildBtnStyle;
    private Texture2D whitePixelTex;
    private bool stylesInit = false;

    void Start()
    {
        player = FindObjectOfType<PlayerController>();
        gameModeManager = FindObjectOfType<GameModeManager>();

        // 从工坊返回时，若存在新锻造武器，自动装备并播放飘字
        if (PlayerSessionData.currentEquippedWeapon != null && player != null)
        {
            player.equippedWeapon = PlayerSessionData.currentEquippedWeapon;

            if (PlayerSessionData.hasJustReturnedFromCrafting)
            {
                PlayerSessionData.hasJustReturnedFromCrafting = false;
                if (DamageTextManager.Instance != null)
                {
                    DamageTextManager.Instance.ShowText(
                        player.transform.position + Vector3.up * 0.8f,
                        $"⚔️ 已装备新武器: {player.equippedWeapon.weaponName}",
                        new Color(0.2f, 1f, 0.5f),
                        0.10f
                    );
                }
            }
        }
    }

    void Update()
    {
        // 检查工坊是否刚刚关闭并返回
        if (PlayerSessionData.hasJustReturnedFromCrafting)
        {
            PlayerSessionData.hasJustReturnedFromCrafting = false;
            PlayerSessionData.isCraftingOpen = false;
            Time.timeScale = 1f;

            if (player == null) player = FindObjectOfType<PlayerController>();
            if (player != null && PlayerSessionData.currentEquippedWeapon != null)
            {
                player.equippedWeapon = PlayerSessionData.currentEquippedWeapon;
                if (DamageTextManager.Instance != null)
                {
                    DamageTextManager.Instance.ShowText(
                        player.transform.position + Vector3.up * 0.8f,
                        $"⚔️ 已装备新武器: {player.equippedWeapon.weaponName}",
                        new Color(0.2f, 1f, 0.5f),
                        0.10f
                    );
                }
            }
        }

        if (PlayerSessionData.isCraftingOpen) return;

        // PC 快捷键支持
        if (Input.GetKeyDown(shortcutKey) || Input.GetKeyDown(KeyCode.Tab))
        {
            EnterCraftingScene();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (player != null) player.TriggerDodgeRoll();
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            TriggerContextAction();
        }
    }

    private void InitStyles()
    {
        if (stylesInit) return;

        whitePixelTex = new Texture2D(1, 1);
        whitePixelTex.SetPixel(0, 0, Color.white);
        whitePixelTex.Apply();

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        headerStyle.normal.textColor = new Color(1f, 0.88f, 0.35f);

        statLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 19,
            alignment = TextAnchor.MiddleLeft
        };
        statLabelStyle.normal.textColor = new Color(0.85f, 0.9f, 0.98f);

        hpLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        hpLabelStyle.normal.textColor = Color.white;

        bigBtnAttackStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        bigBtnAttackStyle.normal.textColor = Color.white;

        bigBtnDodgeStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 23,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        bigBtnDodgeStyle.normal.textColor = Color.white;

        bigBtnPrimaryStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        bigBtnPrimaryStyle.normal.textColor = Color.white;

        bigBtnSubStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 23,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        bigBtnSubStyle.normal.textColor = Color.white;

        topBuildBtnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        topBuildBtnStyle.normal.textColor = Color.white;

        centerHintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter
        };
        centerHintStyle.normal.textColor = new Color(0.85f, 0.9f, 0.98f);

        stylesInit = true;
    }

    void OnGUI()
    {
        if (PlayerSessionData.isCraftingOpen) return;

        if (gameModeManager == null) gameModeManager = FindObjectOfType<GameModeManager>();
        if (gameModeManager != null && gameModeManager.IsBuildMode)
        {
            return; // 建造模式下隐藏底座与战斗面板，释放完整视野
        }

        InitStyles();

        // 移动端自适应矩阵缩放
        float scale = Screen.width / VIRTUAL_WIDTH;
        if (scale < 0.2f) scale = 0.2f;

        Matrix4x4 prevMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1.0f));

        float virtualH = Screen.height / scale;

        // 1. 绘制【屏幕上方空间】HUD：血条、精力条、目标怪状态、建造按钮
        DrawTopHUD();

        // 2. 绘制【屏幕下方紧凑底座】(~22%高度)：左侧摇杆 + 右侧紧凑大按键
        float dockHeight = virtualH * bottomDockRatio;
        float dockTopY = virtualH - dockHeight;
        DrawBottomDock(dockTopY, dockHeight);

        GUI.matrix = prevMatrix;
    }

    private void DrawTopHUD()
    {
        if (player == null) player = FindObjectOfType<PlayerController>();

        float padX = 20f;
        float curY = 16f;

        // 背景半透明黑条
        Color oldColor = GUI.color;
        GUI.color = new Color(0.06f, 0.08f, 0.14f, 0.88f);
        GUI.DrawTexture(new Rect(padX, curY, VIRTUAL_WIDTH - padX * 2, 88f), whitePixelTex);
        GUI.color = oldColor;

        // === 左侧：玩家生命条与精力条 ===
        float curHp = player != null ? player.currentHp : 100f;
        float maxHp = player != null ? player.maxHp : 100f;
        float hpRatio = Mathf.Clamp01(curHp / Mathf.Max(maxHp, 1f));

        float curStamina = player != null ? player.currentStamina : 100f;
        float maxStamina = player != null ? player.maxStamina : 100f;
        float staminaRatio = Mathf.Clamp01(curStamina / Mathf.Max(maxStamina, 1f));

        // 生命条
        GUI.Label(new Rect(padX + 16f, curY + 6f, 150f, 26f), $"❤️ {curHp:0}/{maxHp:0}", hpLabelStyle);
        GUI.color = new Color(0.2f, 0.25f, 0.35f, 1f);
        GUI.DrawTexture(new Rect(padX + 175f, curY + 9f, 200f, 18f), whitePixelTex);
        GUI.color = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(0.2f, 0.85f, 0.35f), hpRatio);
        GUI.DrawTexture(new Rect(padX + 175f, curY + 9f, 200f * hpRatio, 18f), whitePixelTex);
        GUI.color = oldColor;

        // 精力条
        GUI.Label(new Rect(padX + 16f, curY + 32f, 150f, 26f), $"⚡ 精力 {curStamina:0}/{maxStamina:0}", hpLabelStyle);
        GUI.color = new Color(0.2f, 0.25f, 0.35f, 1f);
        GUI.DrawTexture(new Rect(padX + 175f, curY + 36f, 200f, 18f), whitePixelTex);
        GUI.color = new Color(0.15f, 0.75f, 0.95f, 1f);
        GUI.DrawTexture(new Rect(padX + 175f, curY + 36f, 200f * staminaRatio, 18f), whitePixelTex);
        GUI.color = oldColor;

        // 武器信息简述
        if (player != null && player.equippedWeapon != null)
        {
            var w = player.equippedWeapon;
            string elemBadge = (w.primaryElement != "None" && !string.IsNullOrEmpty(w.primaryElement))
                ? $"<color=#38bdf8>[{w.primaryElement} {w.elementPotency:0}]</color>"
                : "";
            string wpnText = $"⚔️ <b>{w.weaponName}</b> {elemBadge} (伤:{w.damage:0} 攻速:{w.attackInterval:0.00}s)";
            GUI.Label(new Rect(padX + 16f, curY + 58f, 400f, 24f), wpnText, statLabelStyle);
        }

        // === 中间：当前锁定怪物目标状态卡片或复活状态 ===
        if (player != null && player.currentTarget != null && player.currentTarget.IsAlive)
        {
            var e = player.currentTarget;
            float eHpRatio = Mathf.Clamp01(e.currentHp / Mathf.Max(e.maxHp, 1f));
            float eGuardRatio = Mathf.Clamp01(e.currentGuardBreak / Mathf.Max(e.maxGuardBreak, 1f));

            float targetX = padX + 410f;
            string stagText = e.IsStaggered ? "<color=#f59e0b>[瘫痪中!]</color>" : "";
            GUI.Label(new Rect(targetX, curY + 6f, 300f, 26f), $"🎯 目标: <b>{e.name}</b> {stagText}", statLabelStyle);

            // 怪物血条
            GUI.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            GUI.DrawTexture(new Rect(targetX, curY + 34f, 230f, 16f), whitePixelTex);
            GUI.color = new Color(0.95f, 0.25f, 0.25f, 1f);
            GUI.DrawTexture(new Rect(targetX, curY + 34f, 230f * eHpRatio, 16f), whitePixelTex);

            // 怪物韧性条
            GUI.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            GUI.DrawTexture(new Rect(targetX, curY + 54f, 230f, 12f), whitePixelTex);
            GUI.color = new Color(0.2f, 0.85f, 1f, 1f);
            GUI.DrawTexture(new Rect(targetX, curY + 54f, 230f * eGuardRatio, 12f), whitePixelTex);
            GUI.color = oldColor;
        }
        else
        {
            var enemy = FindObjectOfType<EnemyController>();
            float targetX = padX + 410f;
            if (enemy != null && enemy.IsRespawning)
            {
                GUI.Label(new Rect(targetX, curY + 12f, 340f, 30f), $"👾 恶魔已击败: <color=#fbbf24><b>[复活刷新中 {enemy.respawnCountdown:F1}s]</b></color>", statLabelStyle);
                GUI.Label(new Rect(targetX, curY + 42f, 340f, 26f), "<color=#94a3b8>将在营地外初始点重新苏醒...</color>", statLabelStyle);
            }
            else if (enemy != null && enemy.IsAlive)
            {
                GUI.Label(new Rect(targetX, curY + 12f, 340f, 30f), $"👾 恶魔在营地外游荡中 (HP: {enemy.currentHp:0})", statLabelStyle);
                GUI.Label(new Rect(targetX, curY + 42f, 340f, 26f), "<color=#38bdf8>点击怪物或右下【索敌锁定】即可交战</color>", statLabelStyle);
            }
        }

        // === 右侧：建造模式切换按钮 (消耗30精力) ===
        float buildBtnW = 270f;
        float buildBtnH = 68f;
        float buildBtnX = VIRTUAL_WIDTH - padX - buildBtnW - 12f;

        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.92f, 0.58f, 0.16f, 1f);
        if (GUI.Button(new Rect(buildBtnX, curY + 10f, buildBtnW, buildBtnH), "🏗️ 建造模式 (30精力)", topBuildBtnStyle))
        {
            if (gameModeManager != null)
            {
                gameModeManager.EnterBuildMode();
            }
        }
        GUI.backgroundColor = prevBg;
    }

    private void DrawBottomDock(float dockTopY, float dockHeight)
    {
        float padX = 24f;

        // 顶边分割荧光蓝线
        Color oldColor = GUI.color;
        GUI.color = new Color(0.25f, 0.65f, 1f, 0.8f);
        GUI.DrawTexture(new Rect(0, dockTopY - 3f, VIRTUAL_WIDTH, 3f), whitePixelTex);

        // 紧凑半透明底座背景
        GUI.color = new Color(0.07f, 0.10f, 0.16f, 0.94f);
        GUI.DrawTexture(new Rect(0, dockTopY, VIRTUAL_WIDTH, dockHeight), whitePixelTex);
        GUI.color = oldColor;

        float btnW = 390f;
        float btnH = (dockHeight - 34f) * 0.5f;
        float row1Y = dockTopY + 11f;
        float row2Y = row1Y + btnH + 10f;

        Color prevBg = GUI.backgroundColor;

        // ==========================================
        // 【左手功能区】：辅助与工坊大按键 (X: 24 ~ 414)
        // ==========================================
        // 左上：进入独立合成工坊 (无缝 Additive 叠加模式)
        GUI.backgroundColor = new Color(0.24f, 0.55f, 0.96f, 1f);
        if (GUI.Button(new Rect(padX, row1Y, btnW, btnH), "🔨 合成工坊 (B)", bigBtnPrimaryStyle))
        {
            EnterCraftingScene();
        }

        // 左下：动态情境互动键 (放下家具 / 踢飞家具 / 索敌锁定)
        var nearestFurn = player != null ? FurnitureObject.GetNearestPlaced(player.transform.position, 1.6f) : null;
        if (player != null && player.heldFurniture != null)
        {
            GUI.backgroundColor = new Color(0.42f, 0.58f, 0.75f, 1f);
            if (GUI.Button(new Rect(padX, row2Y, btnW, btnH), $"📦 放下: {player.heldFurniture.furnitureName}", bigBtnSubStyle))
            {
                player.PutDownHeldFurniture();
            }
        }
        else if (nearestFurn != null)
        {
            GUI.backgroundColor = new Color(0.95f, 0.65f, 0.15f, 1f);
            if (GUI.Button(new Rect(padX, row2Y, btnW, btnH), $"🦶 踢飞碎裂: {nearestFurn.furnitureName}", bigBtnSubStyle))
            {
                player.KickNearbyFurniture();
            }
        }
        else
        {
            GUI.backgroundColor = new Color(0.35f, 0.45f, 0.65f, 1f);
            string lockText = (player != null && player.currentTarget != null && player.currentTarget.IsAlive) ? "🎯 目标锁定中" : "🎯 索敌锁定";
            if (GUI.Button(new Rect(padX, row2Y, btnW, btnH), lockText, bigBtnSubStyle))
            {
                if (player != null) player.PerformManualAttack();
            }
        }

        // ==========================================
        // 【中轴指南区】：全屏战场快捷手势说明 (X: 434 ~ 646)
        // ==========================================
        float centerW = 216f;
        float centerX = (VIRTUAL_WIDTH - centerW) * 0.5f;
        float hintY = dockTopY + (dockHeight - 78f) * 0.5f;

        GUI.Label(new Rect(centerX, hintY, centerW, 26f), "<color=#38bdf8>👆 点击地面</color> 寻路移动", centerHintStyle);
        GUI.Label(new Rect(centerX, hintY + 26f, centerW, 26f), "<color=#facc15>⚡ 快速划动</color> 翻滚闪避", centerHintStyle);
        GUI.Label(new Rect(centerX, hintY + 52f, centerW, 26f), "<color=#fb923c>💣 连点敌人</color> 掷出家具", centerHintStyle);

        // ==========================================
        // 【右手战术区】：核心战术大按键 (X: 666 ~ 1056)
        // ==========================================
        float rightX = VIRTUAL_WIDTH - padX - btnW;

        // 右上：普通攻击挥击 (核心绿色大键)
        GUI.backgroundColor = new Color(0.14f, 0.78f, 0.42f, 1f);
        if (GUI.Button(new Rect(rightX, row1Y, btnW, btnH), "⚔️ 普攻挥击", bigBtnAttackStyle))
        {
            if (player != null) player.PerformManualAttack();
        }

        // 右下：翻滚闪避 (需15精力)
        bool canRoll = player != null && player.CanRoll && player.currentStamina >= player.rollStaminaCost;
        GUI.backgroundColor = canRoll ? new Color(0.2f, 0.75f, 0.95f, 1f) : new Color(0.4f, 0.45f, 0.5f, 1f);
        string rollText = canRoll ? "💨 闪避 (15精力)" : "💨 冷却/缺精力";
        if (GUI.Button(new Rect(rightX, row2Y, btnW, btnH), rollText, bigBtnDodgeStyle))
        {
            if (player != null) player.TriggerDodgeRoll();
        }

        GUI.backgroundColor = prevBg;
    }

    public void EnterCraftingScene()
    {
        if (PlayerSessionData.isCraftingOpen) return;
        PlayerSessionData.isCraftingOpen = true;

        PlayerSessionData.returnCombatSceneName = SceneManager.GetActiveScene().name;

        if (player == null) player = FindObjectOfType<PlayerController>();
        if (player != null && player.equippedWeapon != null)
        {
            PlayerSessionData.currentEquippedWeapon = player.equippedWeapon;
        }

        Debug.Log($"<color=#38bdf8>[CombatScene] 采用 Additive 叠加模式打开工坊场景: {craftingSceneName}，战斗场景原样冻结不重置！</color>");
        Time.timeScale = 0f; // 冻结战斗
        SceneManager.LoadSceneAsync(craftingSceneName, LoadSceneMode.Additive);
    }

    private void TriggerContextAction()
    {
        if (player == null) return;
        if (player.heldFurniture != null)
        {
            player.PutDownHeldFurniture();
        }
        else
        {
            var furn = FurnitureObject.GetNearestPlaced(player.transform.position, 1.6f);
            if (furn != null)
            {
                player.PickUpNearbyFurniture();
            }
            else
            {
                player.PerformManualAttack();
            }
        }
    }
}
