using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;

/// <summary>
/// 智能等距建造与拆除系统（带立方体顶面高程对齐）：
/// 1. 立方体顶面对齐：
///    - 识别地面方块为带有 0.5 单位高度的立体方块；
///    - 玩家点击方块最上表面时，精准命中对应方块坐标；
///    - 绿色/红色高亮棱形框精准贴合在方块【最顶面】上；
///    - 放置的物品（如栅栏）自动站在方块【最顶面】上。
/// 2. 地面检测：
///    - 有地面且空格子：显示【绿框】，再次点击放置物品。
///    - 无地面（虚空）：显示【红框】，无法放置，再次点击会有抖动警告。
/// 3. 物品拆除：
///    - 点击已放置的物品：显示【红框】（删除标记），再次点击即可拆除/删除该物品。
/// 4. 手势过滤：
///    - 拖拽平移或双指缩放时不触发点击判定。
/// </summary>
public class BuildController : MonoBehaviour
{
    private enum CellAction
    {
        None,
        Place,    // 处于地面上，可正常放置（绿框）
        Delete,   // 已有放置物品，再次点击删除（红框）
        Invalid   // 无地面（悬空/虚空），无法放置（红框）
    }

    [Header("Tilemaps (网格与瓦片图层)")]
    public Grid grid;
    [Tooltip("地面参考层：基础地面方块所在的图层")]
    public Tilemap groundTilemap;
    [Tooltip("建造放置层：玩家放置物品的图层 (如栅栏、道具等)")]
    public Tilemap buildTilemap;

    // 兼容旧属性命名
    [HideInInspector]
    public Tilemap targetTilemap;

    [Header("Block Surface Alignment (顶面高度对齐)")]
    [Tooltip("地面方块的厚度/顶面高度 (128x128 像素且侧面厚度 64px 的方块对应世界单位 0.5)")]
    public float groundHeightOffset = 0.5f;

    public enum BuildModeCategory
    {
        Furniture,  // 放置重型战斗家具 (如玄岩熔火餐桌)
        Block       // 放置地形防御方块 (如栅栏)
    }

    [Header("Build Category (建造品类设置)")]
    [Tooltip("当前建造品类：家具 (Furniture) 或 防御方块 (Block)")]
    public BuildModeCategory currentCategory = BuildModeCategory.Furniture;
    [Tooltip("放置的家具预制体 (如玄岩熔火重型餐桌)")]
    public GameObject furniturePrefab;

    [Header("Building Tile (用于放置的物品/方块资源)")]
    [Tooltip("放置的方块资源 (可拖拽替换)")]
    public TileBase buildTile;

    [Header("Biomechanical Products Selection (生体构装选择)")]
    [Tooltip("当前选中的待放置构装成品")]
    public BiomechanicalCrafting.CraftedProduct selectedProduct = null;

    [Header("Cursor Colors (高亮边框颜色设置)")]
    [Tooltip("可放置时的绿框颜色 (有地面)")]
    public Color validColor = new Color(0.2f, 1f, 0.4f, 1f);
    [Tooltip("不可放置时的红框颜色 (无地面)")]
    public Color invalidColor = new Color(1f, 0.25f, 0.25f, 1f);
    [Tooltip("点击已放置物品时的删除红框颜色")]
    public Color deleteColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Header("Cursor Settings (光标设置)")]
    [Tooltip("绿色棱形光标精灵")]
    public Sprite highlightSprite;
    [Tooltip("高亮光标微调额外偏移")]
    public float cursorYOffset = 0f;
    [Tooltip("高亮图层排序 (确保悬浮在所有地块之上)")]
    public int highlightSortingOrder = 100;
    [Tooltip("是否启用呼吸脉冲微动画")]
    public bool enablePulseAnimation = true;

    [Header("Click Sensitivity (点击判定灵敏度)")]
    [Tooltip("最大允许手指/鼠标移动距离 (像素)，超过则判定为拖动地图而非点击")]
    public float clickMaxDistance = 15f;
    [Tooltip("最长点击判定时间 (秒)")]
    public float clickMaxDuration = 0.35f;

    [Header("Build Mode State (建造模式开关)")]
    [Tooltip("是否正处于建造模式（由 UI 建造按钮控制，为 false 时不响应建造点击）")]
    public bool isBuildMode = false;

    // 当前选中的格子与对应操作
    private Vector3Int? selectedCell = null;
    private CellAction currentAction = CellAction.None;

    // 光标物体与组件
    private GameObject highlightObject;
    private SpriteRenderer highlightRenderer;
    private GameObject ghostPreviewObject;
    private SpriteRenderer ghostPreviewRenderer;

    // 抖动动画状态
    private float shakeTimer = 0f;
    private Vector3 originalCursorPos;

    // 点击判定跟踪
    private Vector2 pointerDownPos;
    private float pointerDownTime;
    private bool isPointerDown = false;
    private bool hasMovedBeyondThreshold = false;

    private Camera cam;

    void Awake()
    {
        cam = Camera.main;

        if (grid == null)
            grid = FindObjectOfType<Grid>();

        if (grid != null)
        {
            var tms = grid.GetComponentsInChildren<Tilemap>();
            if (groundTilemap == null)
            {
                groundTilemap = System.Array.Find(tms, t => t.name == "Tilemap") ?? (tms.Length > 0 ? tms[0] : null);
            }
            if (buildTilemap == null)
            {
                buildTilemap = targetTilemap ?? System.Array.Find(tms, t => t.name != "Tilemap") ?? groundTilemap;
            }
            targetTilemap = buildTilemap;

            // 自动将建造层抬高到方块顶面高度，使放置的物品自然站在方块顶面上！
            if (buildTilemap != null && buildTilemap != groundTilemap)
            {
                buildTilemap.transform.localPosition = new Vector3(0f, groundHeightOffset, 0f);
            }
        }

        CreateHighlightCursor();
    }

    private void CreateHighlightCursor()
    {
        highlightObject = new GameObject("Build_Highlight_Cursor");
        highlightObject.transform.SetParent(transform);
        highlightRenderer = highlightObject.AddComponent<SpriteRenderer>();

        if (highlightSprite != null)
        {
            highlightRenderer.sprite = highlightSprite;
        }

        // 使用 URP Unlit 材质，保证边框不受场景光照阴影影响，始终极度鲜亮醒目
        var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (unlitShader != null)
        {
            highlightRenderer.material = new Material(unlitShader);
        }

        highlightRenderer.color = validColor;
        highlightRenderer.sortingOrder = highlightSortingOrder;

        // 创建生体构装待放置半透明投影幻影预览
        ghostPreviewObject = new GameObject("Ghost_Preview");
        ghostPreviewObject.transform.SetParent(highlightObject.transform, false);
        ghostPreviewObject.transform.localPosition = Vector3.zero;
        ghostPreviewRenderer = ghostPreviewObject.AddComponent<SpriteRenderer>();
        var ghostShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
        if (ghostShader != null)
        {
            ghostPreviewRenderer.material = new Material(ghostShader);
        }
        ghostPreviewRenderer.sortingOrder = highlightSortingOrder + 1;
        ghostPreviewObject.SetActive(false);

        highlightObject.SetActive(false);
    }

    void Update()
    {
        HandleInput();
        UpdatePulseAnimation();
        UpdateShakeAnimation();
    }

    private void HandleInput()
    {
        // 若未开启建造模式，不接收建造点击
        if (!isBuildMode)
        {
            isPointerDown = false;
            return;
        }

        // 手机触控检测
        if (Input.touchCount > 0)
        {
            if (Input.touchCount > 1)
            {
                isPointerDown = false;
                return;
            }

            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                if (IsPointerOverUI(touch.fingerId))
                {
                    isPointerDown = false;
                    return;
                }
                StartPointer(touch.position);
            }
            else if (touch.phase == TouchPhase.Moved)
            {
                CheckPointerMove(touch.position);
            }
            else if (touch.phase == TouchPhase.Ended)
            {
                if (isPointerDown && !hasMovedBeyondThreshold && (Time.time - pointerDownTime) <= clickMaxDuration)
                {
                    OnCellClicked(touch.position);
                }
                isPointerDown = false;
            }
            else if (touch.phase == TouchPhase.Canceled)
            {
                isPointerDown = false;
            }
        }
        else
        {
            // 电脑鼠标（仅鼠标左键用于选择与建造/删除）
            if (Input.GetMouseButtonDown(0))
            {
                if (IsPointerOverUI(-1))
                {
                    isPointerDown = false;
                    return;
                }
                StartPointer(Input.mousePosition);
            }
            else if (Input.GetMouseButton(0) && isPointerDown)
            {
                CheckPointerMove(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                if (isPointerDown && !hasMovedBeyondThreshold && (Time.time - pointerDownTime) <= clickMaxDuration)
                {
                    OnCellClicked(Input.mousePosition);
                }
                isPointerDown = false;
            }
        }
    }

    private void StartPointer(Vector2 screenPos)
    {
        pointerDownPos = screenPos;
        pointerDownTime = Time.time;
        isPointerDown = true;
        hasMovedBeyondThreshold = false;
    }

    private void CheckPointerMove(Vector2 currentScreenPos)
    {
        if (Vector2.Distance(pointerDownPos, currentScreenPos) > clickMaxDistance)
        {
            hasMovedBeyondThreshold = true;
        }
    }

    private bool IsPointerOverUI(int fingerId)
    {
        if (EventSystem.current == null) return false;
        if (fingerId >= 0)
            return EventSystem.current.IsPointerOverGameObject(fingerId);
        return EventSystem.current.IsPointerOverGameObject();
    }

    private void OnCellClicked(Vector2 screenPosition)
    {
        if (cam == null || grid == null) return;

        Vector3 worldPos = cam.ScreenToWorldPoint(screenPosition);
        worldPos.z = 0f;

        // 核心对齐：玩家在屏幕上看见并点击的是方块的【最顶面】。
        // 减去方块的物理厚度 (groundHeightOffset)，才能精准映射到该方块实际底层的网格坐标！
        Vector3 groundRayPos = new Vector3(worldPos.x, worldPos.y - groundHeightOffset, 0f);
        Vector3Int cellPos = grid.WorldToCell(groundRayPos);

        // 评估当前格子的状态（建造 / 删除 / 无效）
        CellAction action = EvaluateCellAction(cellPos);

        // 如果点击的正是当前已高亮的格子 -> 触发对应动作！
        if (selectedCell.HasValue && selectedCell.Value == cellPos)
        {
            switch (currentAction)
            {
                case CellAction.Place:
                    PlaceObject(cellPos);
                    break;
                case CellAction.Delete:
                    DeleteObject(cellPos);
                    break;
                case CellAction.Invalid:
                    TriggerShakeFeedback();
                    Debug.LogWarning($"[BuildSystem] 无法放置：格子 {cellPos} 没有地面！");
                    break;
            }
        }
        else
        {
            // 第一次点击该格子 -> 显示对应颜色高亮边框
            SelectCell(cellPos, action);
        }
    }

    /// <summary>
    /// 评估格子状态：
    /// 1. 如果已有家具或建筑层物品 -> Delete (红框待删/回收)
    /// 2. 如果无物品但有地面 -> Place (绿框可建)
    /// 3. 如果无地面 -> Invalid (红框不可建)
    /// </summary>
    private CellAction EvaluateCellAction(Vector3Int cellPos)
    {
        // 1. 检查网格上是否已有放置状态的家具（可拆除回收）
        if (FurnitureObject.GetFurnitureAtCell(cellPos) != null)
        {
            return CellAction.Delete;
        }

        // 2. 检查建筑层上是否已有物品/栅栏（可拆除）
        var bTilemap = buildTilemap != null ? buildTilemap : targetTilemap;
        if (bTilemap != null && bTilemap.HasTile(cellPos))
        {
            return CellAction.Delete;
        }

        // 3. 检查下方地面层是否有地块
        if (groundTilemap != null && groundTilemap.HasTile(cellPos))
        {
            return CellAction.Place;
        }

        // 4. 无地面
        return CellAction.Invalid;
    }

    private void SelectCell(Vector3Int cellPos, CellAction action)
    {
        selectedCell = cellPos;
        currentAction = action;

        var activeTilemap = groundTilemap != null ? groundTilemap : (buildTilemap ?? targetTilemap);
        Vector3 cellCenter = activeTilemap != null ? activeTilemap.GetCellCenterWorld(cellPos) : grid.GetCellCenterWorld(cellPos);

        // 高亮边框精确显示在方块的【最顶面】上！
        highlightObject.transform.position = new Vector3(cellCenter.x, cellCenter.y + groundHeightOffset + cursorYOffset, 0f);
        originalCursorPos = highlightObject.transform.position;

        // 设置对应的高亮颜色与幻影预览
        switch (action)
        {
            case CellAction.Place:
                highlightRenderer.color = validColor;
                UpdateGhostPreview();
                break;
            case CellAction.Delete:
                highlightRenderer.color = deleteColor;
                if (ghostPreviewObject != null) ghostPreviewObject.SetActive(false);
                break;
            case CellAction.Invalid:
                highlightRenderer.color = invalidColor;
                if (ghostPreviewObject != null) ghostPreviewObject.SetActive(false);
                break;
        }

        highlightObject.SetActive(true);
    }

    private void PlaceObject(Vector3Int cellPos)
    {
        // 彻底移除旧方块放置逻辑，全面拥抱工坊联动生体构装与战术废料建造体系
        PlaceFurniture(cellPos);
    }

    private void PlaceFurniture(Vector3Int cellPos)
    {
        var activeTilemap = groundTilemap != null ? groundTilemap : (buildTilemap ?? targetTilemap);
        Vector3 cellCenter = activeTilemap != null ? activeTilemap.GetCellCenterWorld(cellPos) : grid.GetCellCenterWorld(cellPos);
        Vector3 spawnPos = new Vector3(cellCenter.x, cellCenter.y + groundHeightOffset, 0f);

        // 若当前未选定构装，自动从 PlayerSessionData 仓库中选择第一件
        if (selectedProduct == null)
        {
            var products = PlayerSessionData.GetCraftedProducts();
            if (products.Count > 0)
            {
                selectedProduct = products[0];
            }
        }

        // 放置工坊联动生体构装
        if (selectedProduct != null && BiomechanicalCombatAdapter.Instance != null)
        {
            var spawned = BiomechanicalCombatAdapter.Instance.SpawnProductAtCell(selectedProduct, cellPos, spawnPos);
            if (spawned != null)
            {
                Debug.Log($"[BuildSystem] 成功放置生体构装: {selectedProduct.productName} 于格子 {cellPos}");
                // 若该物品已全部放置，更新或清空当前选中项以便重新从仓库选择
                var products = PlayerSessionData.GetCraftedProducts();
                if (!products.Contains(selectedProduct))
                {
                    selectedProduct = products.Count > 0 ? products[0] : null;
                }
                ClearSelection();
                return;
            }
        }

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(spawnPos + Vector3.up * 0.6f, "⚠️ 仓库中暂无可用构装，请按 [B] 前往工坊合成！", new Color(1f, 0.4f, 0.4f), 0.12f);
        }
        ClearSelection();
    }

    private void DeleteObject(Vector3Int cellPos)
    {
        // 1. 优先检查并删除家具
        var furn = FurnitureObject.GetFurnitureAtCell(cellPos);
        if (furn != null)
        {
            string fName = furn.furnitureName;
            Vector3 fPos = furn.transform.position;

            // 若属于生体构装，回收返回仓库
            if (furn.originalProduct != null)
            {
                PlayerSessionData.AddCraftedProduct(furn.originalProduct);
                if (DamageTextManager.Instance != null)
                {
                    DamageTextManager.Instance.ShowText(fPos + Vector3.up * 0.6f, $"♻️ 回收入库: {furn.originalProduct.productName}", new Color(0.3f, 0.9f, 1f), 0.12f);
                }
            }
            else
            {
                if (DamageTextManager.Instance != null)
                {
                    DamageTextManager.Instance.ShowText(fPos + Vector3.up * 0.5f, "RECYCLED!", new Color(1f, 0.4f, 0.4f), 0.1f);
                }
            }

            Destroy(furn.gameObject);
            Debug.Log($"[BuildSystem] 成功拆除并回收了格子 {cellPos} 上的家具: {fName}");
            ClearSelection();
            return;
        }

        // 2. 检查并删除建筑层方块
        var bTilemap = buildTilemap != null ? buildTilemap : targetTilemap;
        if (bTilemap != null && bTilemap.HasTile(cellPos))
        {
            var oldTile = bTilemap.GetTile(cellPos);
            string tileName = oldTile != null ? oldTile.name : "物品";
            bTilemap.SetTile(cellPos, null);

            Debug.Log($"[BuildSystem] 成功拆除删除了格子 {cellPos} 上的方块: {tileName}");
            ClearSelection();
        }
    }

    public void SetCategory(BuildModeCategory category)
    {
        currentCategory = category;
        ClearSelection();
    }

    public void SetBuildMode(bool active)
    {
        isBuildMode = active;
        if (!active)
        {
            ClearSelection();
        }
    }

    private void UpdateGhostPreview()
    {
        if (ghostPreviewObject == null) return;
        if (selectedProduct == null)
        {
            var products = PlayerSessionData.GetCraftedProducts();
            if (products.Count > 0)
            {
                selectedProduct = products[0];
            }
        }

        if (selectedProduct != null)
        {
            var sp = Resources.Load<Sprite>("FurnitureSprites/" + selectedProduct.id);
            if (sp != null)
            {
                ghostPreviewRenderer.sprite = sp;
                ghostPreviewRenderer.color = new Color(1f, 1f, 1f, 0.6f);
                ghostPreviewObject.SetActive(true);
                return;
            }
        }
        ghostPreviewObject.SetActive(false);
    }

    public void ClearSelection()
    {
        selectedCell = null;
        currentAction = CellAction.None;
        if (ghostPreviewObject != null)
        {
            ghostPreviewObject.SetActive(false);
        }
        if (highlightObject != null)
        {
            highlightObject.SetActive(false);
        }
    }

    private void TriggerShakeFeedback()
    {
        shakeTimer = 0.2f;
    }

    private void UpdateShakeAnimation()
    {
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            float offset = Mathf.Sin(shakeTimer * 50f) * 0.08f;
            if (highlightObject != null)
            {
                highlightObject.transform.position = originalCursorPos + new Vector3(offset, 0f, 0f);
            }
        }
    }

    private void UpdatePulseAnimation()
    {
        if (!enablePulseAnimation || highlightObject == null || !highlightObject.activeSelf || shakeTimer > 0f) return;

        // 呼吸效果：温和的尺寸轻微脉冲
        float scale = 1f + Mathf.Sin(Time.time * 6f) * 0.04f;
        highlightObject.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private Vector2 warehouseScrollPos = Vector2.zero;

    void OnGUI()
    {
        if (!isBuildMode) return;

        float dockHeight = 125f;
        float dockY = Screen.height - dockHeight;

        // 半透明暗底
        GUI.Box(new Rect(10f, dockY, Screen.width - 20f, dockHeight), "");

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        titleStyle.normal.textColor = new Color(0.95f, 0.85f, 0.3f);

        string selName = selectedProduct != null ? selectedProduct.productName : "未选定（点击下方卡片）";
        GUI.Label(new Rect(20f, dockY + 6f, Screen.width - 200f, 22f), $"📦 战术构装仓库选择 (点击卡片选中，点击地面绿框部署，点击红框回收入库) | 当前选中: <color=#38bdf8><b>{selName}</b></color>", titleStyle);

        // 快捷前往生体工坊按钮
        if (GUI.Button(new Rect(Screen.width - 200f, dockY + 6f, 185f, 24f), "🔨 前往生体工坊 (B)"))
        {
            var entrance = FindObjectOfType<CombatSceneCraftingEntrance>();
            if (entrance != null)
            {
                entrance.EnterCraftingScene();
            }
            else
            {
                PlayerSessionData.isCraftingOpen = true;
                PlayerSessionData.returnCombatSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                UnityEngine.SceneManagement.SceneManager.LoadScene("Mix", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            }
        }

        var products = PlayerSessionData.GetCraftedProducts();
        if (products.Count == 0)
        {
            GUI.Label(new Rect(25f, dockY + 40f, 750f, 30f), "<color=#94A3B8>📦 战术仓库开局为空。请点击右上角【前往生体工坊 (B)】连线合成构装，合成后即可在此选取并部署到战场！</color>");
            return;
        }

        // 横向滚动构装卡片列表
        float scrollWidth = Screen.width - 40f;
        float contentWidth = Mathf.Max(scrollWidth, products.Count * 220f + 20f);
        warehouseScrollPos = GUI.BeginScrollView(new Rect(20f, dockY + 32f, scrollWidth, 80f), warehouseScrollPos, new Rect(0, 0, contentWidth, 65f), true, false);

        for (int i = 0; i < products.Count; i++)
        {
            var p = products[i];
            if (p == null) continue;

            float itemX = i * 220f;
            bool isSelected = selectedProduct == p;

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = isSelected ? new Color(0.2f, 0.95f, 0.45f, 1f) : (p.isAberrantScrap ? new Color(0.9f, 0.45f, 0.65f, 0.9f) : new Color(0.2f, 0.45f, 0.75f, 0.9f));

            string tag = p.isAberrantScrap ? "[废料]" : $"[{p.category}]";
            string btnText = $"{tag} {p.productName}\nP:{p.totalP} E:{p.totalE} G:{p.totalG} W:{p.totalW}";

            Sprite furnSprite = Resources.Load<Sprite>("FurnitureSprites/" + p.id);
            GUIContent btnContent = furnSprite != null && furnSprite.texture != null
                ? new GUIContent(btnText, furnSprite.texture)
                : new GUIContent(btnText);

            if (GUI.Button(new Rect(itemX, 0, 210f, 55f), btnContent))
            {
                selectedProduct = p;
                ClearSelection();
            }

            GUI.backgroundColor = oldBg;
        }

        GUI.EndScrollView();
    }
}
