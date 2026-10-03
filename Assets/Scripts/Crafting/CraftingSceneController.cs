using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 独立的【合成工坊场景】主控制器 (Crafting Scene Controller) - 手机竖屏高精适配版：
/// 1. 采用 VIRTUAL_WIDTH = 1080 自适应 GUI 矩阵缩放，高 DPI 手机屏幕文字大而锐利，杜绝微小文字；
/// 2. 竖屏流式层级布局：顶部导航 -> 蓝图切换 -> 3 部位槽位工作台 -> 属性实时推演与大锻造键 -> 底部材料背包大卡片；
/// 3. 超大触控热区（按钮高度 85~105px），专为手机大拇指交互优化；
/// 4. 严格校验蓝图部位（如剑至少包含【块】+【棒】），元素强度结合累加取最高值；
/// 5. 锻造完成一键无缝携装返回战斗场景。
/// </summary>
public class CraftingSceneController : MonoBehaviour
{
    [Header("Crafting Setup")]
    public List<CraftingBlueprint> blueprints = new List<CraftingBlueprint>();
    public int currentBlueprintIndex = 0;
    public List<MaterialItem> selectedMaterials = new List<MaterialItem>();
    public const int MAX_SLOTS = 3;

    [Header("Preview Result")]
    public CraftingResult currentPreview = new CraftingResult();

    [Header("Status Feedback")]
    public string notificationMessage = "";
    public float notificationTimer = 0f;

    public const float VIRTUAL_WIDTH = 1080f;

    private Vector2 materialsScrollPos = Vector2.zero;
    private Rect materialsViewRect = new Rect(24f, 500f, 1032f, 400f);
    private float maxScrollY = 0f;
    private bool isTouchDragging = false;
    private Vector2 touchLastPos = Vector2.zero;
    private float scrollVelocity = 0f;
    private bool isMouseDragging = false;
    private Vector2 mouseLastPos = Vector2.zero;

    private GUIStyle titleStyle;
    private GUIStyle headerStyle;
    private GUIStyle subHeaderStyle;
    private GUIStyle statLabelStyle;
    private GUIStyle statValueStyle;
    private GUIStyle validBoxStyle;
    private GUIStyle invalidBoxStyle;
    private GUIStyle notifStyle;
    private GUIStyle bigBtnPrimaryStyle;
    private GUIStyle bigBtnSuccessStyle;
    private GUIStyle tabBtnStyle;
    private GUIStyle tabBtnSelectedStyle;
    private GUIStyle slotBoxStyle;
    private GUIStyle slotBoxEmptyStyle;
    private Texture2D whitePixelTex;
    private bool stylesInit = false;

    void Awake()
    {
        PlayerSessionData.isCraftingOpen = true;
        PlayerSessionData.hasJustReturnedFromCrafting = false;

        if (blueprints.Count == 0)
        {
            blueprints = CraftingBlueprint.CreateDefaultBlueprints();
        }

        PlayerSessionData.GetOrCreateMaterials();
        UpdatePreview();
    }

    void OnEnable()
    {
        PlayerSessionData.isCraftingOpen = true;
        PlayerSessionData.hasJustReturnedFromCrafting = false;
    }

    void Update()
    {
        if (!PlayerSessionData.isCraftingOpen) return;

        if (notificationTimer > 0f)
        {
            notificationTimer -= Time.unscaledDeltaTime;
            if (notificationTimer <= 0f) notificationMessage = "";
        }

        // 按 Esc 或 B 键快捷返回战斗 (PC测试兼容)
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.B))
        {
            ReturnToCombatScene();
            return;
        }

        // 手机端原生触摸流畅下滑/上滑浏览材料 (Direct Touch Swipe / Dragging)
        float scale = Screen.width / VIRTUAL_WIDTH;
        if (scale < 0.2f) scale = 0.2f;

        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            Vector2 virtTouchPos = new Vector2(t.position.x / scale, (Screen.height - t.position.y) / scale);

            if (t.phase == TouchPhase.Began)
            {
                if (materialsViewRect.Contains(virtTouchPos))
                {
                    isTouchDragging = true;
                    touchLastPos = virtTouchPos;
                    scrollVelocity = 0f;
                }
            }
            else if (t.phase == TouchPhase.Moved && isTouchDragging)
            {
                float deltaY = virtTouchPos.y - touchLastPos.y;
                materialsScrollPos.y -= deltaY;
                scrollVelocity = -deltaY / Mathf.Max(0.001f, Time.unscaledDeltaTime);
                touchLastPos = virtTouchPos;
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                isTouchDragging = false;
            }
        }
        else
        {
            // 电脑端鼠标在材料列表区域直接按住拖拽模拟
            if (Input.GetMouseButton(0))
            {
                Vector2 virtMousePos = new Vector2(Input.mousePosition.x / scale, (Screen.height - Input.mousePosition.y) / scale);
                if (Input.GetMouseButtonDown(0))
                {
                    if (materialsViewRect.Contains(virtMousePos))
                    {
                        isMouseDragging = true;
                        mouseLastPos = virtMousePos;
                        scrollVelocity = 0f;
                    }
                }
                else if (isMouseDragging)
                {
                    float deltaY = virtMousePos.y - mouseLastPos.y;
                    materialsScrollPos.y -= deltaY;
                    scrollVelocity = -deltaY / Mathf.Max(0.001f, Time.unscaledDeltaTime);
                    mouseLastPos = virtMousePos;
                }
            }
            else if (isMouseDragging)
            {
                isMouseDragging = false;
            }
        }

        // 惯性平滑减速 (Inertia Damping)
        if (!isTouchDragging && !isMouseDragging && Mathf.Abs(scrollVelocity) > 2f)
        {
            materialsScrollPos.y += scrollVelocity * Time.unscaledDeltaTime;
            scrollVelocity = Mathf.Lerp(scrollVelocity, 0f, Time.unscaledDeltaTime * 6f);
        }

        materialsScrollPos.y = Mathf.Clamp(materialsScrollPos.y, 0f, maxScrollY);
    }

    public CraftingBlueprint CurrentBlueprint
    {
        get
        {
            if (blueprints == null || blueprints.Count == 0) return null;
            if (currentBlueprintIndex < 0 || currentBlueprintIndex >= blueprints.Count)
                currentBlueprintIndex = 0;
            return blueprints[currentBlueprintIndex];
        }
    }

    public void SelectBlueprint(int index)
    {
        if (index >= 0 && index < blueprints.Count)
        {
            currentBlueprintIndex = index;
            UpdatePreview();
        }
    }

    public void AddMaterial(MaterialItem mat)
    {
        if (mat == null) return;
        if (selectedMaterials.Count >= MAX_SLOTS)
        {
            SetNotification($"⚠️ 最多只能够选择 {MAX_SLOTS} 个材料！", 2.5f);
            return;
        }

        selectedMaterials.Add(mat);
        UpdatePreview();
    }

    public void RemoveMaterialAt(int index)
    {
        if (index >= 0 && index < selectedMaterials.Count)
        {
            selectedMaterials.RemoveAt(index);
            UpdatePreview();
        }
    }

    public void ClearMaterials()
    {
        selectedMaterials.Clear();
        UpdatePreview();
    }

    public void UpdatePreview()
    {
        var bp = CurrentBlueprint;
        if (bp != null)
        {
            currentPreview = bp.Evaluate(selectedMaterials);
        }
        else
        {
            currentPreview = new CraftingResult
            {
                isValid = false,
                statusMessage = "未选择有效蓝图"
            };
        }
    }

    public void ExecuteCraft()
    {
        UpdatePreview();
        if (!currentPreview.isValid)
        {
            SetNotification($"❌ 无法合成：{currentPreview.statusMessage}", 3f);
            return;
        }

        // 保存并设为当前手持武器
        if (currentPreview.category == "weapon" && currentPreview.generatedWeaponData != null)
        {
            PlayerSessionData.currentEquippedWeapon = currentPreview.generatedWeaponData;
            Debug.Log($"<color=#10b981>[CraftingScene] 成功锻造装备：{currentPreview.productName} (伤害:{currentPreview.damage} 元素:{currentPreview.primaryElement})，携装返回战斗！</color>");
        }
        else
        {
            Debug.Log($"<color=#10b981>[CraftingScene] 成功制作家具：{currentPreview.productName}，携装返回战斗！</color>");
        }

        // 合成完成后立即携装关闭工坊并无缝返回战斗场景
        ReturnToCombatScene();
    }

    public void ReturnToCombatScene()
    {
        PlayerSessionData.isCraftingOpen = false;
        PlayerSessionData.hasJustReturnedFromCrafting = true;
        Time.timeScale = 1f;

        // 立即关闭本组件 OnGUI，杜绝与战斗界面重叠残留
        this.enabled = false;

        // 若是通过 Additive 叠加模式打开的工坊，直接卸载当前工坊场景，战斗场景状态 100% 保持！
        if (SceneManager.sceneCount > 1)
        {
            Debug.Log("<color=#38bdf8>[CraftingScene] 叠加模式：卸载工坊场景，战斗场景原样无缝恢复！</color>");
            SceneManager.UnloadSceneAsync(gameObject.scene);
        }
        else
        {
            string targetScene = string.IsNullOrEmpty(PlayerSessionData.returnCombatSceneName)
                ? "TileTest"
                : PlayerSessionData.returnCombatSceneName;

            Debug.Log($"<color=#38bdf8>[CraftingScene] 单场景回退: {targetScene}</color>");
            SceneManager.LoadScene(targetScene);
        }
    }

    private void SetNotification(string msg, float duration)
    {
        notificationMessage = msg;
        notificationTimer = duration;
    }

    private void InitStyles()
    {
        if (stylesInit) return;

        whitePixelTex = new Texture2D(1, 1);
        whitePixelTex.SetPixel(0, 0, Color.white);
        whitePixelTex.Apply();

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(1f, 0.88f, 0.35f);

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        headerStyle.normal.textColor = new Color(0.35f, 0.85f, 1f);

        subHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        subHeaderStyle.normal.textColor = new Color(0.92f, 0.94f, 0.98f);

        statLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 21,
            alignment = TextAnchor.MiddleLeft
        };
        statLabelStyle.normal.textColor = new Color(0.85f, 0.88f, 0.95f);

        statValueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        statValueStyle.normal.textColor = Color.white;

        validBoxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 23,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        validBoxStyle.normal.textColor = new Color(0.2f, 1f, 0.5f);

        invalidBoxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 23,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        invalidBoxStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);

        notifStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        notifStyle.normal.textColor = new Color(1f, 0.95f, 0.3f);

        bigBtnPrimaryStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        bigBtnPrimaryStyle.normal.textColor = Color.white;

        bigBtnSuccessStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        bigBtnSuccessStyle.normal.textColor = Color.white;

        tabBtnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        tabBtnStyle.normal.textColor = new Color(0.85f, 0.9f, 0.95f);

        tabBtnSelectedStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        tabBtnSelectedStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);

        slotBoxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft
        };

        slotBoxEmptyStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter
        };
        slotBoxEmptyStyle.normal.textColor = new Color(0.55f, 0.62f, 0.72f);

        stylesInit = true;
    }

    void OnGUI()
    {
        if (!PlayerSessionData.isCraftingOpen || !this.enabled) return;

        InitStyles();

        // 1. 移动端高 DPI / 竖屏矩阵自适应
        float scale = Screen.width / VIRTUAL_WIDTH;
        if (scale < 0.2f) scale = 0.2f;

        Matrix4x4 prevMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1.0f));

        // 电脑端鼠标滚轮直接在材料列表区域滑行
        if (Event.current.type == EventType.ScrollWheel && materialsViewRect.Contains(Event.current.mousePosition))
        {
            materialsScrollPos.y += Event.current.delta.y * 45f;
            materialsScrollPos.y = Mathf.Clamp(materialsScrollPos.y, 0f, maxScrollY);
        }

        float virtualH = Screen.height / scale;
        float pad = 24f;
        float curY = 16f;

        // 2. 顶部导航与当前武器栏
        curY = DrawTopBar(pad, curY);

        // 3. 提示横幅
        if (!string.IsNullOrEmpty(notificationMessage))
        {
            GUI.Box(new Rect(pad, curY, VIRTUAL_WIDTH - pad * 2, 70f), notificationMessage, notifStyle);
            curY += 78f;
        }

        // 4. 蓝图选择 Tabs (横向大按钮)
        curY = DrawBlueprintTabs(pad, curY);

        // 5. 工作台槽位与推演预览区 (占屏中间核心区域)
        curY = DrawWorkbenchAndPreview(pad, curY);

        // 6. 拥有材料库列表 (填充至屏幕底部)
        float inventoryHeight = Mathf.Max(300f, virtualH - curY - 16f);
        DrawMaterialsList(pad, curY, inventoryHeight);

        // 恢复 GUI 矩阵
        GUI.matrix = prevMatrix;
    }

    private float DrawTopBar(float pad, float curY)
    {
        float barH = 88f;

        // 返回战斗按钮 (大拇指快速点击)
        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.18f, 0.62f, 0.95f, 1f);
        if (GUI.Button(new Rect(pad, curY, 320f, barH), "🔙 返回战斗 (B / Esc)", bigBtnPrimaryStyle))
        {
            ReturnToCombatScene();
        }

        // 标题
        GUI.Label(new Rect(pad + 335f, curY, 410f, barH), "🔨 独立合成工坊", titleStyle);

        // 补满材料按钮
        GUI.backgroundColor = new Color(0.28f, 0.42f, 0.65f, 1f);
        if (GUI.Button(new Rect(VIRTUAL_WIDTH - pad - 230f, curY, 230f, barH), "✨ 补满测试材料", bigBtnPrimaryStyle))
        {
            PlayerSessionData.playerMaterials = MaterialItem.CreateDefaultMaterialLibrary();
            UpdatePreview();
            SetNotification("已注满全套测试材料库！", 2f);
        }
        GUI.backgroundColor = prevBg;

        curY += barH + 10f;

        // 当前手持展示条
        var curWpn = PlayerSessionData.currentEquippedWeapon;
        string wpnName = curWpn != null ? curWpn.weaponName : "铁制佩剑 (默认)";
        float wpnDmg = curWpn != null ? curWpn.damage : 25f;
        string elemStr = (curWpn != null && curWpn.primaryElement != "None" && !string.IsNullOrEmpty(curWpn.primaryElement))
            ? $"[{curWpn.primaryElement} {curWpn.elementPotency:0}]" : "";

        Color oldColor = GUI.color;
        GUI.color = new Color(0.12f, 0.16f, 0.25f, 0.9f);
        GUI.DrawTexture(new Rect(pad, curY, VIRTUAL_WIDTH - pad * 2, 44f), whitePixelTex);
        GUI.color = oldColor;

        string currentWpnInfo = $"当前手持装备: <color=#fbbf24><b>{wpnName}</b></color> <color=#38bdf8>{elemStr}</color>  |  伤害: <b>{wpnDmg:0}</b>";
        GUI.Label(new Rect(pad + 16f, curY + 6f, VIRTUAL_WIDTH - pad * 2 - 32f, 32f), currentWpnInfo, statLabelStyle);

        return curY + 54f;
    }

    private float DrawBlueprintTabs(float pad, float curY)
    {
        GUI.Label(new Rect(pad, curY, 400f, 34f), "<b>选择锻造蓝图:</b>", headerStyle);
        curY += 40f;

        float totalW = VIRTUAL_WIDTH - pad * 2;
        int bpCount = blueprints.Count;
        float btnW = (totalW - (bpCount - 1) * 12f) / Mathf.Max(1, bpCount);
        float btnH = 80f;

        Color prevBg = GUI.backgroundColor;
        for (int i = 0; i < bpCount; i++)
        {
            var bp = blueprints[i];
            bool isSelected = (i == currentBlueprintIndex);
            float btnX = pad + i * (btnW + 12f);

            GUI.backgroundColor = isSelected ? new Color(0.95f, 0.65f, 0.15f, 1f) : new Color(0.2f, 0.26f, 0.38f, 1f);
            var style = isSelected ? tabBtnSelectedStyle : tabBtnStyle;
            string prefix = isSelected ? "▶ " : "";

            if (GUI.Button(new Rect(btnX, curY, btnW, btnH), prefix + bp.blueprintName, style))
            {
                SelectBlueprint(i);
            }
        }
        GUI.backgroundColor = prevBg;

        return curY + btnH + 14f;
    }

    private float DrawWorkbenchAndPreview(float pad, float curY)
    {
        var bp = CurrentBlueprint;
        string reqStr = bp != null ? string.Join(" + ", bp.requiredTags) : "无";

        GUI.Label(new Rect(pad, curY, 600f, 34f), $"<b>🛠️ 组装工作台 ({selectedMaterials.Count}/{MAX_SLOTS})</b>", headerStyle);
        GUI.Label(new Rect(pad + 400f, curY + 4f, 600f, 30f), $"<color=#94a3b8>蓝图配方约束: 必须包含【{reqStr}】</color>", statLabelStyle);
        curY += 40f;

        // 3 个水平大槽位
        float totalW = VIRTUAL_WIDTH - pad * 2;
        float slotW = (totalW - 24f) / 3f; // 约 344px 每个
        float slotH = 175f;

        for (int i = 0; i < MAX_SLOTS; i++)
        {
            float slotX = pad + i * (slotW + 12f);
            Rect slotRect = new Rect(slotX, curY, slotW, slotH);

            bool hasMat = (i < selectedMaterials.Count);
            MaterialItem mat = hasMat ? selectedMaterials[i] : null;

            Color oldColor = GUI.color;
            GUI.color = hasMat ? new Color(0.14f, 0.20f, 0.32f, 0.95f) : new Color(0.10f, 0.13f, 0.20f, 0.85f);
            GUI.DrawTexture(slotRect, whitePixelTex);
            GUI.color = oldColor;

            if (hasMat)
            {
                string tags = string.Join("/", mat.adaptableParts);
                GUI.Label(new Rect(slotX + 10f, curY + 8f, slotW - 20f, 30f), $"<b>槽位{i + 1}: {mat.materialName}</b>", subHeaderStyle);
                GUI.Label(new Rect(slotX + 10f, curY + 40f, slotW - 20f, 26f), $"部位: <color=#38bdf8>[{tags}]</color>  重: <b>{mat.weight:F1}kg</b>", statLabelStyle);

                string elem = mat.element != "None" ? $"<color=#fbbf24>{mat.element}:{mat.elementPotency}</color>" : "<color=#94a3b8>无元素</color>";
                GUI.Label(new Rect(slotX + 10f, curY + 68f, slotW - 20f, 26f), $"硬度:{mat.hardness:0} 韧性:{mat.toughness:0} | {elem}", statLabelStyle);

                Color prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f, 1f);
                if (GUI.Button(new Rect(slotX + 10f, curY + 108f, slotW - 20f, 54f), "❌ 移出插槽", bigBtnPrimaryStyle))
                {
                    RemoveMaterialAt(i);
                }
                GUI.backgroundColor = prevBg;
            }
            else
            {
                GUI.Label(new Rect(slotX + 10f, curY + 40f, slotW - 20f, 32f), $"【 槽位 {i + 1} : 空置 】", slotBoxEmptyStyle);
                GUI.Label(new Rect(slotX + 10f, curY + 80f, slotW - 20f, 30f), "点击下方材料装配", slotBoxEmptyStyle);
            }
        }

        curY += slotH + 14f;

        // 实时属性推演与状态横条
        if (currentPreview.isValid)
        {
            GUI.Box(new Rect(pad, curY, totalW, 52f), $"✅ {currentPreview.statusMessage}", validBoxStyle);
        }
        else
        {
            GUI.Box(new Rect(pad, curY, totalW, 52f), $"❌ {currentPreview.statusMessage}", invalidBoxStyle);
        }
        curY += 60f;

        // 属性推演大卡片
        Rect previewCardRect = new Rect(pad, curY, totalW, 170f);
        Color prevC = GUI.color;
        GUI.color = new Color(0.12f, 0.17f, 0.28f, 0.96f);
        GUI.DrawTexture(previewCardRect, whitePixelTex);
        GUI.color = prevC;

        if (currentPreview.isValid)
        {
            // 产物名称
            string prodTitle = $"锻造产物: <color=#fbbf24><b>{currentPreview.productName}</b></color>";
            GUI.Label(new Rect(pad + 16f, curY + 10f, totalW - 32f, 36f), prodTitle, titleStyle);

            // 4 核心指标
            string line1 = $"💥 预估伤害: <b>{currentPreview.damage:0}</b>   |   ❄️ 最终元素: <b><color=#38bdf8>{currentPreview.primaryElement} ({currentPreview.elementPotency:0})</color></b>";
            GUI.Label(new Rect(pad + 20f, curY + 54f, totalW - 40f, 30f), line1, subHeaderStyle);

            string line2 = $"⚡ 攻击间隔: <b>{currentPreview.attackInterval:0.00}s</b>   |   🛡️ 耐久上限: <b>{currentPreview.maxDurability:0}</b>   |   破防削韧: <b>{currentPreview.guardBreakPower:0}</b>";
            GUI.Label(new Rect(pad + 20f, curY + 88f, totalW - 40f, 30f), line2, statLabelStyle);

            // 元素计算说明
            string elemRuleDesc = $"🔥 元素最高值推演: 各材料元素叠加累加，取最高项为武器附魔！";
            GUI.Label(new Rect(pad + 20f, curY + 124f, totalW - 40f, 28f), elemRuleDesc, statLabelStyle);
        }
        else
        {
            GUI.Label(new Rect(pad + 16f, curY + 50f, totalW - 32f, 40f), "⚠️ 尚未满足蓝图配方要求，请放入符合条件的材料以激活属性推演", titleStyle);
        }

        curY += 180f;

        // 巨大锻造按钮 (大拇指核心按键)
        Color prevBgColor = GUI.backgroundColor;
        bool canCraft = currentPreview.isValid;
        GUI.backgroundColor = canCraft ? new Color(0.15f, 0.85f, 0.42f, 1f) : new Color(0.35f, 0.42f, 0.48f, 1f);
        GUI.enabled = canCraft;

        string craftBtnText = canCraft
            ? $"🔥 确认锻造【{currentPreview.productName}】并携装返回战斗 (CRAFT & BATTLE)"
            : "⚠️ 材料未满足蓝图要求，无法锻造";

        if (GUI.Button(new Rect(pad, curY, totalW, 102f), craftBtnText, bigBtnSuccessStyle))
        {
            ExecuteCraft();
        }

        GUI.enabled = true;
        GUI.backgroundColor = prevBgColor;

        return curY + 116f;
    }

    private void DrawMaterialsList(float pad, float curY, float listHeight)
    {
        float totalW = VIRTUAL_WIDTH - pad * 2;
        GUI.Label(new Rect(pad, curY, 700f, 34f), "<b>📦 背包材料库 (支持直接屏幕触控滑动浏览)</b>", headerStyle);
        curY += 40f;

        float contentH = listHeight - 40f;
        Rect viewRect = new Rect(pad, curY, totalW, contentH);
        materialsViewRect = viewRect;

        Color oldColor = GUI.color;
        GUI.color = new Color(0.09f, 0.12f, 0.19f, 0.95f);
        GUI.DrawTexture(viewRect, whitePixelTex);
        GUI.color = oldColor;

        var materials = PlayerSessionData.GetOrCreateMaterials();
        float cardH = 155f;
        float totalScrollH = materials.Count * (cardH + 12f) + 20f;
        maxScrollY = Mathf.Max(0f, totalScrollH - contentH);

        materialsScrollPos = GUI.BeginScrollView(viewRect, materialsScrollPos, new Rect(0, 0, totalW - 24f, totalScrollH));

        for (int i = 0; i < materials.Count; i++)
        {
            var mat = materials[i];
            float itemY = i * (cardH + 12f) + 8f;
            Rect itemRect = new Rect(8f, itemY, totalW - 40f, cardH);

            oldColor = GUI.color;
            GUI.color = new Color(0.14f, 0.19f, 0.28f, 0.95f);
            GUI.DrawTexture(itemRect, whitePixelTex);
            GUI.color = oldColor;

            // 材料名称与部位标签
            string tags = string.Join(" / ", mat.adaptableParts);
            string elemBadge = mat.element != "None" ? $"<color=#fbbf24>[{mat.element} {mat.elementPotency:0}]</color>" : "<color=#94a3b8>[无元素]</color>";

            GUI.Label(new Rect(24f, itemY + 10f, totalW - 260f, 32f), $"<b>{mat.materialName}</b>  <color=#38bdf8>[{tags}]</color>  {elemBadge}", subHeaderStyle);

            // 物理数值
            string stats = $"硬度: <b>{mat.hardness:0}</b>   |   韧性: <b>{mat.toughness:0}</b>   |   重量: <b>{mat.weight:F1}kg</b>";
            GUI.Label(new Rect(24f, itemY + 46f, totalW - 260f, 28f), stats, statLabelStyle);

            // 描述
            GUI.Label(new Rect(24f, itemY + 78f, totalW - 260f, 56f), mat.description, statLabelStyle);

            // 放入按钮 (超大触控按键)
            bool canAdd = selectedMaterials.Count < MAX_SLOTS;
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = canAdd ? new Color(0.18f, 0.75f, 0.95f, 1f) : new Color(0.35f, 0.4f, 0.45f, 1f);
            GUI.enabled = canAdd;

            if (GUI.Button(new Rect(totalW - 230f, itemY + 28f, 180f, 96f), "➕ 放入", bigBtnPrimaryStyle))
            {
                AddMaterial(mat);
            }

            GUI.enabled = true;
            GUI.backgroundColor = prevBg;
        }

        GUI.EndScrollView();
    }
}
