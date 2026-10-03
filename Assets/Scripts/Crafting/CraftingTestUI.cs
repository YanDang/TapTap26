using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 合成系统交互测试面板 (Crafting Test UI)：
/// 提供可视化材料选择、最多3个材料槽位装配、蓝图配方部位限制校验、
/// 元素属性最高值推演、即时属性预览，以及一键合成并装备给玩家实战打靶。
/// </summary>
public class CraftingTestUI : MonoBehaviour
{
    [Header("GUI Styling")]
    public bool showUI = true;

    private Vector2 materialsScrollPos = Vector2.zero;
    private CraftingManager craftingManager;
    private GUIStyle headerStyle;
    private GUIStyle boxStyle;
    private GUIStyle titleStyle;
    private GUIStyle statLabelStyle;
    private GUIStyle statValueStyle;
    private GUIStyle validStyle;
    private GUIStyle invalidStyle;
    private GUIStyle buttonStyle;
    private GUIStyle activeTabStyle;

    private bool stylesInitialized = false;

    void Start()
    {
        craftingManager = CraftingManager.Instance;
        if (craftingManager == null)
            craftingManager = FindObjectOfType<CraftingManager>();
    }

    void Update()
    {
        // 按 C 键或 Tab 键可切换界面显隐
        if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.Tab))
        {
            showUI = !showUI;
        }
    }

    private void InitStyles()
    {
        if (stylesInitialized) return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold
        };
        headerStyle.normal.textColor = new Color(0.3f, 0.85f, 1f);

        statLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12
        };
        statLabelStyle.normal.textColor = new Color(0.8f, 0.8f, 0.85f);

        statValueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight
        };
        statValueStyle.normal.textColor = Color.white;

        validStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        validStyle.normal.textColor = new Color(0.2f, 1f, 0.5f);

        invalidStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        invalidStyle.normal.textColor = new Color(1f, 0.35f, 0.35f);

        stylesInitialized = true;
    }

    void OnGUI()
    {
        InitStyles();

        // 顶部浮动开关按钮
        if (GUI.Button(new Rect(10, 10, 170, 36), showUI ? "❌ 收起合成工作台 (C)" : "🔨 展开合成工作台 (C)"))
        {
            showUI = !showUI;
        }

        // 快捷实战打靶状态显示
        DrawCombatHUD();

        if (!showUI) return;
        if (craftingManager == null)
        {
            craftingManager = CraftingManager.Instance;
            if (craftingManager == null) return;
        }

        // 主合成面板
        float panelWidth = Mathf.Min(980, Screen.width - 20);
        float panelHeight = Mathf.Min(600, Screen.height - 70);
        Rect mainRect = new Rect(10, 55, panelWidth, panelHeight);
        GUI.Box(mainRect, "");

        GUILayout.BeginArea(mainRect);
        GUILayout.Space(8);

        // 标题栏
        GUILayout.Label("🔨 模块化材料组装与合成验证系统 (Crafting System Prototype)", titleStyle);
        GUILayout.Space(6);

        // 蓝图选择栏
        DrawBlueprintTabs();
        GUILayout.Space(8);

        // 三栏工作布局：左侧备选材料 | 中间已选槽位 (最多3个) | 右侧属性推演与合成
        GUILayout.BeginHorizontal();

        // 1. 左侧：材料库
        DrawMaterialsPanel(panelWidth * 0.36f);

        GUILayout.Space(10);

        // 2. 中间：合成工作台插槽
        DrawWorkspaceSlots(panelWidth * 0.30f);

        GUILayout.Space(10);

        // 3. 右侧：属性推演与合成执行
        DrawOutputPreview(panelWidth * 0.30f);

        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    /// <summary>
    /// 顶部快捷战斗状态与打靶助手
    /// </summary>
    private void DrawCombatHUD()
    {
        var player = craftingManager != null ? craftingManager.playerController : FindObjectOfType<PlayerController>();
        Rect hudRect = new Rect(190, 10, Screen.width - 200, 36);
        GUI.Box(hudRect, "");
        GUILayout.BeginArea(hudRect);
        GUILayout.BeginHorizontal();
        GUILayout.Space(10);

        if (player != null)
        {
            string wpnName = player.equippedWeapon != null ? player.equippedWeapon.weaponName : "空手/默认";
            float wpnDmg = player.equippedWeapon != null ? player.equippedWeapon.damage : 25f;
            float wpnInterval = player.equippedWeapon != null ? player.equippedWeapon.attackInterval : 0.75f;
            string elem = player.equippedWeapon != null ? player.equippedWeapon.primaryElement : "None";

            GUILayout.Label($"<b>当前玩家武器:</b> <color=#f59e0b>{wpnName}</color> | 伤害: <b>{wpnDmg}</b> | 攻速间隔: <b>{wpnInterval}s</b> | 元素: <b>{elem}</b>", statLabelStyle, GUILayout.Width(450));
        }

        if (GUILayout.Button("🔄 重置恶魔生命", GUILayout.Width(110)))
        {
            var enemy = FindObjectOfType<EnemyController>();
            if (enemy != null)
            {
                enemy.currentHp = enemy.maxHp;
                enemy.currentGuardBreak = enemy.maxGuardBreak;
                if (DamageTextManager.Instance != null)
                    DamageTextManager.Instance.ShowText(enemy.transform.position + Vector3.up * 0.5f, "RESET ENEMY", Color.cyan);
            }
        }

        if (GUILayout.Button("✨ 补满全套材料", GUILayout.Width(110)))
        {
            if (craftingManager != null)
            {
                craftingManager.availableMaterials = MaterialItem.CreateDefaultMaterialLibrary();
                craftingManager.UpdatePreview();
            }
        }

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    /// <summary>
    /// 蓝图切换 Tabs
    /// </summary>
    private void DrawBlueprintTabs()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("<b>选择配方蓝图:</b>", GUILayout.Width(100));

        for (int i = 0; i < craftingManager.availableBlueprints.Count; i++)
        {
            var bp = craftingManager.availableBlueprints[i];
            bool isSelected = (i == craftingManager.currentBlueprintIndex);

            string btnText = isSelected ? $"<b><color=#f59e0b>▶ {bp.blueprintName}</color></b>" : bp.blueprintName;
            if (GUILayout.Button(btnText, GUILayout.Height(28), GUILayout.MinWidth(110)))
            {
                craftingManager.SelectBlueprint(i);
            }
        }
        GUILayout.EndHorizontal();
    }

    /// <summary>
    /// 左侧：材料库列表面板
    /// </summary>
    private void DrawMaterialsPanel(float width)
    {
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(width));
        GUILayout.Label($"<b>📦 拥有材料库 (点击加号放入槽位)</b>", headerStyle);
        GUILayout.Space(4);

        materialsScrollPos = GUILayout.BeginScrollView(materialsScrollPos);

        for (int i = 0; i < craftingManager.availableMaterials.Count; i++)
        {
            var mat = craftingManager.availableMaterials[i];
            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.BeginHorizontal();
            // 部位标签徽章
            string tagsStr = string.Join(" / ", mat.adaptableParts);
            GUILayout.Label($"<b>{mat.materialName}</b> <color=#38bdf8>[{tagsStr}]</color>", GUILayout.Width(width - 90));

            // 添加按钮 (若槽位未满)
            bool canAdd = craftingManager.selectedMaterials.Count < CraftingManager.MAX_SELECTED_MATERIALS;
            GUI.enabled = canAdd;
            if (GUILayout.Button("➕ 放入", GUILayout.Width(60), GUILayout.Height(22)))
            {
                craftingManager.AddMaterial(mat);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            // 属性参数
            string elemStr = mat.element != "None" ? $"<color=#fbbf24>{GetElemIcon(mat.element)}{mat.elementPotency}</color>" : "<color=#94a3b8>无元素</color>";
            GUILayout.Label($"硬度:<b>{mat.hardness}</b> 韧性:<b>{mat.toughness}</b> 重:<b>{mat.weight}kg</b> | 元素: {elemStr}", statLabelStyle);

            GUILayout.EndVertical();
            GUILayout.Space(2);
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    /// <summary>
    /// 中间：当前工作台已选材料槽位 (最多3个)
    /// </summary>
    private void DrawWorkspaceSlots(float width)
    {
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(width));
        GUILayout.Label($"<b>🔨 已装入材料 ({craftingManager.selectedMaterials.Count}/3)</b>", headerStyle);

        var bp = craftingManager.CurrentBlueprint;
        string reqStr = bp != null ? string.Join(" 和 ", bp.requiredTags) : "无";
        GUILayout.Label($"<color=#94a3b8>当前蓝图需求: 至少包含【{reqStr}】</color>", statLabelStyle);
        GUILayout.Space(6);

        // 渲染 3 个固定槽位
        for (int i = 0; i < CraftingManager.MAX_SELECTED_MATERIALS; i++)
        {
            bool hasMaterial = (i < craftingManager.selectedMaterials.Count);
            MaterialItem mat = hasMaterial ? craftingManager.selectedMaterials[i] : null;

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(76));
            if (hasMaterial)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>槽位 {i + 1}: {mat.materialName}</b>", GUILayout.Width(width - 80));
                if (GUILayout.Button("❌ 移出", GUILayout.Width(55), GUILayout.Height(20)))
                {
                    craftingManager.RemoveMaterialAt(i);
                }
                GUILayout.EndHorizontal();

                string tags = string.Join("/", mat.adaptableParts);
                GUILayout.Label($"部位: <color=#38bdf8>[{tags}]</color> | 重: {mat.weight}kg", statLabelStyle);
                GUILayout.Label($"硬度: {mat.hardness} | 韧性: {mat.toughness} | 元素: {GetElemIcon(mat.element)}{mat.elementPotency}", statLabelStyle);
            }
            else
            {
                GUILayout.Label($"<b>槽位 {i + 1}: 【空置】</b>", statLabelStyle);
                GUILayout.Label("<color=#64748b>从左侧点击材料放入</color>", statLabelStyle);
            }
            GUILayout.EndVertical();
            GUILayout.Space(4);
        }

        if (craftingManager.selectedMaterials.Count > 0)
        {
            if (GUILayout.Button("清空所有槽位", GUILayout.Height(24)))
            {
                craftingManager.ClearSelectedMaterials();
            }
        }

        GUILayout.EndVertical();
    }

    /// <summary>
    /// 右侧：实时属性推演与合成按钮
    /// </summary>
    private void DrawOutputPreview(float width)
    {
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(width));
        GUILayout.Label("<b>📊 最终产物属性推演</b>", headerStyle);

        var result = craftingManager.currentPreviewResult;

        // 1. 配方校验状态
        if (result.isValid)
        {
            GUILayout.Box($"✅ {result.statusMessage}", validStyle, GUILayout.Height(30));
        }
        else
        {
            GUILayout.Box($"❌ {result.statusMessage}", invalidStyle, GUILayout.Height(30));
        }
        GUILayout.Space(6);

        // 2. 元素最高值推演详情展示（核心需求）
        GUILayout.Label("<b>🔥 元素最高值结合机制:</b>", statLabelStyle);
        if (result.elementBreakdown != null && result.elementBreakdown.Count > 0)
        {
            foreach (var kvp in result.elementBreakdown)
            {
                string icon = GetElemIcon(kvp.Key);
                string highlight = (kvp.Key.Equals(result.primaryElement, System.StringComparison.OrdinalIgnoreCase))
                    ? "<b><color=#f59e0b>★ (最高选定)</color></b>" : "";
                GUILayout.Label($"• {icon} {kvp.Key} 总和: {kvp.Value} {highlight}", statLabelStyle);
            }
            GUILayout.Label($"👉 最终元素结果: <b><color=#f59e0b>{GetElemIcon(result.primaryElement)} {result.primaryElement} ({result.elementPotency})</color></b>", statLabelStyle);
        }
        else
        {
            GUILayout.Label("<color=#94a3b8>• 无元素反应 (纯物理特质)</color>", statLabelStyle);
        }

        GUILayout.Space(8);

        // 3. 产物属性推演数值
        if (result.isValid)
        {
            GUILayout.Label($"产物命名: <b><color=#38bdf8>{result.productName}</color></b>", headerStyle);
            GUILayout.Label($"⚔️ 基础伤害 (Damage): <b>{result.damage}</b> (硬度贡献)", statLabelStyle);
            GUILayout.Label($"🛡️ 耐久上限 (Durability): <b>{result.maxDurability}</b> (韧性贡献)", statLabelStyle);
            GUILayout.Label($"⚡ 攻击间隔 (Interval): <b>{result.attackInterval}s</b> (重量/韧性平衡)", statLabelStyle);
            GUILayout.Label($"💥 削韧破防 (GuardBreak): <b>{result.guardBreakPower}</b>", statLabelStyle);
            GUILayout.Label($"🌀 挥砍耐力消耗: <b>{result.staminaCost}</b>", statLabelStyle);
            GUILayout.Label($"⚖️ 物理总重量: <b>{result.totalWeight:F1} kg</b>", statLabelStyle);

            if (result.category == "furniture")
            {
                GUILayout.Label($"🪑 滑动速度: <b>{result.slideSpeed}</b>", statLabelStyle);
                GUILayout.Label($"🔨 举起伤害: <b>{result.heldDamage}</b> | 间隔: <b>{result.heldInterval}s</b>", statLabelStyle);
            }
        }
        else
        {
            GUILayout.Label("<color=#64748b>放入符合蓝图要求的材料后即刻展示实时推演数据</color>", statLabelStyle);
        }

        GUILayout.FlexibleSpace();

        // 4. 合成执行按钮
        GUI.enabled = result.isValid;
        Color oldColor = GUI.backgroundColor;
        if (result.isValid) GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);

        if (GUILayout.Button("🔨 开始合成并装备武器", GUILayout.Height(42)))
        {
            craftingManager.ExecuteCraft(out var finalCraft);
        }

        GUI.backgroundColor = oldColor;
        GUI.enabled = true;

        GUILayout.EndVertical();
    }

    private string GetElemIcon(string elem)
    {
        if (string.IsNullOrEmpty(elem)) return "";
        switch (elem.ToLower())
        {
            case "fire": return "🔥 ";
            case "ice": return "❄️ ";
            case "thunder": return "⚡ ";
            case "poison": return "🌿 ";
            default: return "";
        }
    }
}
