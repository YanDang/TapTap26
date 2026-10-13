using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BiomechanicalCrafting;

/// <summary>
/// 现代化 UGUI 建造模式交互坞：
/// 1. 左下角建造锤子大图标，点击平滑横向展开/收起家具构装库存；
/// 2. 横向抽屉面板（Shelf），支持大量家具卡片水平滑动浏览与横向滚动条联动；
/// 3. 点击卡片即时选中并呼出等距网格全息放置预览（Ghost Preview）；
/// 4. 自动检测库存：若初始为空，注入代表性构装样品供即时测试。
/// </summary>
public class BuildDockUI : MonoBehaviour
{
    public static BuildDockUI Instance { get; private set; }

    [Header("Core References")]
    public BuildController buildController;
    public GameModeManager gameModeManager;

    [Header("Hammer Button (建造锤子按键)")]
    public Button hammerButton;
    public Image hammerButtonBg;
    public Image hammerIcon;

    [Header("Shelf & Drawer (横向构装抽屉)")]
    public RectTransform shelfPanel;
    public CanvasGroup shelfCanvasGroup;
    public Text titleText;
    public Button closeButton;

    [Header("Scroll Area (横向滑动视图与滚动条)")]
    public ScrollRect scrollRect;
    public RectTransform contentRect;
    public Scrollbar horizontalScrollbar;

    [Header("Animation & Layout Settings (横向抽屉动画与自适应布局)")]
    [Tooltip("抽屉动画类型：\n- SlideFromLeft: 从左向右平滑滑出（内容不挤压变形，推荐）\n- ExpandWidth: 从左向右横向展开（卷轴拉伸）\n- SlideAndScale: 平移滑入与微弹性展开结合\n- FadeOnly: 原地淡入淡出")]
    public DrawerAnimStyle animationStyle = DrawerAnimStyle.SlideFromLeft;

    [Tooltip("展开/收起动画时长（秒），可在 Inspector 中自由拖动调节")]
    [Range(0.05f, 1.0f)]
    public float animationDuration = 0.25f;

    [Tooltip("展开动画缓动曲线（点击可选择 EaseInOut / EaseOutBack / 自由拖动贝塞尔曲线）")]
    public AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("收起动画缓动曲线")]
    public AnimationCurve closeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Drawer Screen Margins (屏幕边距与横向覆盖)")]
    [Tooltip("抽屉距离屏幕左侧的边距（避开左侧锤子按钮）")]
    public float leftMargin = 125f;

    [Tooltip("抽屉距离屏幕右侧的边距（设为 0 即完全横向覆盖延伸贴满屏幕最右边缘；可设置 10~20 留出呼吸边距）")]
    public float rightMargin = 0f;

    [Tooltip("抽屉距离屏幕底部的边距")]
    public float bottomMargin = 10f;

    [Tooltip("抽屉的高度")]
    public float drawerHeight = 135f;

    [Tooltip("横向滑入时的起始位移偏置量（像素）")]
    public float slideDistanceOffset = 350f;

    public enum DrawerAnimStyle
    {
        SlideFromLeft,   // 从左侧向右平滑滑出（推荐）
        ExpandWidth,     // 水平展开
        SlideAndScale,   // 滑入与微缩放结合
        FadeOnly         // 仅淡入淡出
    }

    [Header("Card Templates & Sprites")]
    public GameObject cardPrefab;
    public Sprite cardBgNormal;
    public Sprite cardBgSelected;

    [Header("State")]
    public bool isDrawerOpen = false;
    private List<FurnitureCardItem> activeCards = new List<FurnitureCardItem>();
    private CraftedProduct currentSelectedProduct = null;
    private Coroutine animCoroutine = null;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        if (buildController == null) buildController = FindObjectOfType<BuildController>();
        if (gameModeManager == null) gameModeManager = FindObjectOfType<GameModeManager>();

        ApplyShelfLayout();
    }

    private void OnEnable()
    {
        PlayerSessionData.OnCraftedProductsChanged += HandleCraftedProductsChanged;
    }

    private void OnDisable()
    {
        PlayerSessionData.OnCraftedProductsChanged -= HandleCraftedProductsChanged;
    }

    private void HandleCraftedProductsChanged()
    {
        if (isDrawerOpen)
        {
            PopulateCards();
        }
    }

    private void OnValidate()
    {
        // 编辑器面板参数修改时即时刷新预览布局
        if (shelfPanel != null)
        {
            ApplyShelfLayout();
        }
    }

    /// <summary>
    /// 将抽屉面板锚点设置为水平拉伸自适应覆盖屏幕宽度
    /// </summary>
    [ContextMenu("Apply Shelf Layout Now")]
    public void ApplyShelfLayout()
    {
        if (shelfPanel == null) return;

        // 设置水平拉伸：左锚点 0，右锚点 1，下锚点 0，上锚点 0
        shelfPanel.anchorMin = new Vector2(0f, 0f);
        shelfPanel.anchorMax = new Vector2(1f, 0f);
        shelfPanel.pivot = new Vector2(0f, 0f);

        shelfPanel.offsetMin = new Vector2(leftMargin, bottomMargin);
        shelfPanel.offsetMax = new Vector2(-rightMargin, bottomMargin + drawerHeight);
    }
    /// <summary>
    /// 判断屏幕坐标是否位于抽屉面板或锤子按钮区域内（用于阻断场景点击/拖拽穿透）
    /// </summary>
    public bool IsPointerOverDrawer(Vector2 screenPos)
    {
        // 检查锤子按钮
        if (hammerButton != null && hammerButton.gameObject.activeInHierarchy)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(hammerButton.transform as RectTransform, screenPos, null))
            {
                return true;
            }
        }

        // 检查抽屉面板（展开时有效）
        if (isDrawerOpen && shelfPanel != null && shelfPanel.gameObject.activeInHierarchy)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(shelfPanel, screenPos, null))
            {
                return true;
            }
        }

        return false;
    }

    private void Start()
    {
        // 绑定锤子按键
        if (hammerButton != null)
        {
            hammerButton.onClick.RemoveAllListeners();
            hammerButton.onClick.AddListener(ToggleDrawer);
        }

        // 绑定收起按键
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseDrawer);
        }

        // 默认收起抽屉
        SetDrawerStateImmediate(false);
    }

    private void Update()
    {
        if (PlayerSessionData.IsCombatInputBlocked) return;
        // 支持快捷键 [B] 展开/收起建造坞
        if (Input.GetKeyDown(KeyCode.B))
        {
            ToggleDrawer();
        }

        // 如果在建造模式下外部取消了建造（例如右键退出），同步关闭抽屉
        if (isDrawerOpen && buildController != null && !buildController.isBuildMode)
        {
            CloseDrawer();
        }
    }

    /// <summary>
    /// 切换横向抽屉展开/收起状态
    /// </summary>
    public void ToggleDrawer()
    {
        if (PlayerSessionData.IsCombatInputBlocked) return;
        if (isDrawerOpen)
        {
            CloseDrawer();
        }
        else
        {
            OpenDrawer();
        }
    }

    /// <summary>
    /// 横向展开构装库存抽屉并进入建造模式
    /// </summary>
    public void OpenDrawer()
    {
        if (PlayerSessionData.IsCombatInputBlocked) return;
        isDrawerOpen = true;

        // 真实读取工坊合成产物并刷新卡片列表
        PopulateCards();

        // 触发建造模式
        if (gameModeManager != null)
        {
            if (!gameModeManager.IsBuildMode)
            {
                gameModeManager.EnterBuildMode(true);
            }
        }
        else if (buildController != null)
        {
            buildController.SetBuildMode(true);
        }

        // 锤子按键高亮反馈
        if (hammerButtonBg != null)
        {
            hammerButtonBg.color = new Color(0.2f, 0.95f, 0.5f, 1f);
        }

        // 播放横向展开平滑动画
        if (Application.isPlaying)
        {
            if (animCoroutine != null) StopCoroutine(animCoroutine);
            animCoroutine = StartCoroutine(AnimateDrawer(true));
        }
        else
        {
            SetDrawerStateImmediate(true);
        }
    }

    /// <summary>
    /// 横向收起抽屉并退出建造模式
    /// </summary>
    public void CloseDrawer()
    {
        isDrawerOpen = false;

        // 退出建造模式
        if (gameModeManager != null && gameModeManager.IsBuildMode)
        {
            gameModeManager.ExitBuildMode();
        }
        else if (buildController != null && buildController.isBuildMode)
        {
            buildController.SetBuildMode(false);
        }

        // 锤子按键恢复基础颜色
        if (hammerButtonBg != null)
        {
            hammerButtonBg.color = Color.white;
        }

        // 播放收起动画
        if (Application.isPlaying)
        {
            if (animCoroutine != null) StopCoroutine(animCoroutine);
            animCoroutine = StartCoroutine(AnimateDrawer(false));
        }
        else
        {
            SetDrawerStateImmediate(false);
        }
    }

    private void SetDrawerStateImmediate(bool open)
    {
        isDrawerOpen = open;
        if (shelfPanel != null)
        {
            shelfPanel.gameObject.SetActive(open);
            ApplyShelfLayout();
            ApplyAnimFrame(open ? 1f : 0f);
        }
        if (shelfCanvasGroup != null)
        {
            shelfCanvasGroup.alpha = open ? 1f : 0f;
            shelfCanvasGroup.interactable = open;
            shelfCanvasGroup.blocksRaycasts = open;
        }
    }

    private IEnumerator AnimateDrawer(bool open)
    {
        if (shelfPanel == null) yield break;

        ApplyShelfLayout();

        if (open)
        {
            shelfPanel.gameObject.SetActive(true);
        }

        float duration = Mathf.Max(0.01f, animationDuration);
        float elapsed = 0f;

        AnimationCurve curve = open ? openCurve : closeCurve;
        if (curve == null || curve.length == 0)
        {
            curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curveVal = curve.Evaluate(t);
            float progress = open ? curveVal : (1f - curveVal);

            ApplyAnimFrame(progress);

            yield return null;
        }

        ApplyAnimFrame(open ? 1f : 0f);

        if (shelfCanvasGroup != null)
        {
            shelfCanvasGroup.interactable = open;
            shelfCanvasGroup.blocksRaycasts = open;
        }

        if (!open)
        {
            shelfPanel.gameObject.SetActive(false);
        }
    }

    private void ApplyAnimFrame(float progress)
    {
        if (shelfPanel == null) return;

        if (shelfCanvasGroup != null)
        {
            shelfCanvasGroup.alpha = Mathf.Clamp01(progress);
        }

        switch (animationStyle)
        {
            case DrawerAnimStyle.SlideFromLeft:
                float xOffset = (1f - progress) * (-slideDistanceOffset);
                shelfPanel.offsetMin = new Vector2(leftMargin + xOffset, bottomMargin);
                shelfPanel.offsetMax = new Vector2(-rightMargin + xOffset, bottomMargin + drawerHeight);
                shelfPanel.localScale = Vector3.one;
                break;

            case DrawerAnimStyle.ExpandWidth:
                shelfPanel.offsetMin = new Vector2(leftMargin, bottomMargin);
                shelfPanel.offsetMax = new Vector2(-rightMargin, bottomMargin + drawerHeight);
                shelfPanel.localScale = new Vector3(Mathf.Max(0.001f, progress), 1f, 1f);
                break;

            case DrawerAnimStyle.SlideAndScale:
                float slideX = (1f - progress) * (-slideDistanceOffset * 0.5f);
                shelfPanel.offsetMin = new Vector2(leftMargin + slideX, bottomMargin);
                shelfPanel.offsetMax = new Vector2(-rightMargin + slideX, bottomMargin + drawerHeight);
                float scaleX = Mathf.Lerp(0.85f, 1f, progress);
                float scaleY = Mathf.Lerp(0.9f, 1f, progress);
                shelfPanel.localScale = new Vector3(scaleX, scaleY, 1f);
                break;

            case DrawerAnimStyle.FadeOnly:
                shelfPanel.offsetMin = new Vector2(leftMargin, bottomMargin);
                shelfPanel.offsetMax = new Vector2(-rightMargin, bottomMargin + drawerHeight);
                shelfPanel.localScale = Vector3.one;
                break;
        }
    }

    /// <summary>
    /// 构筑卡片数据并动态加载到横向列表中（与真实工坊仓库深度绑定）
    /// </summary>
    public void PopulateCards(CraftedProduct preferredSelected = null)
    {
        if (contentRect == null) return;

        // 清理所有旧卡片与旧提示
        for (int i = contentRect.childCount - 1; i >= 0; i--)
        {
            Destroy(contentRect.GetChild(i).gameObject);
        }
        activeCards.Clear();

        var products = PlayerSessionData.GetCraftedProducts();

        if (products.Count == 0)
        {
            if (titleText != null)
            {
                titleText.text = "📦 战术构装库存 (0 件)  ·  仓库为空，请前往生体工坊进行背壳合成";
            }
            currentSelectedProduct = null;
            if (buildController != null)
            {
                buildController.selectedProduct = null;
                buildController.ClearSelection();
            }
            CreateEmptyWarehouseNotice(contentRect);
            return;
        }

        if (titleText != null)
        {
            titleText.text = $"📦 战术构装库存 ({products.Count} 件)  ·  点击构装选中，在地面绿色菱形框上部署";
        }

        // 维护当前选中项
        if (preferredSelected != null && products.Contains(preferredSelected))
        {
            currentSelectedProduct = preferredSelected;
        }
        else if (currentSelectedProduct == null || !products.Contains(currentSelectedProduct))
        {
            currentSelectedProduct = products[0];
        }

        if (buildController != null)
        {
            buildController.selectedProduct = currentSelectedProduct;
        }

        // 生成卡片
        for (int i = 0; i < products.Count; i++)
        {
            var p = products[i];
            if (p == null) continue;

            GameObject cardGo = null;
            if (cardPrefab != null)
            {
                cardGo = Instantiate(cardPrefab, contentRect);
            }
            else
            {
                cardGo = CreateDefaultCardObject(contentRect);
            }

            cardGo.name = $"Card_{i}_{p.id}";
            var cardComp = cardGo.GetComponent<FurnitureCardItem>() ?? cardGo.AddComponent<FurnitureCardItem>();
            cardComp.Setup(this, p, p == currentSelectedProduct);
            activeCards.Add(cardComp);
        }

        // 重置滑动条到最左侧
        if (scrollRect != null)
        {
            scrollRect.horizontalNormalizedPosition = 0f;
        }
    }

    /// <summary>
    /// 当场景中成功放置一件构装时触发：实时从横向抽屉列表扣除卡片并更新建造选中
    /// </summary>
    public void OnProductPlaced(CraftedProduct product)
    {
        var products = PlayerSessionData.GetCraftedProducts();

        // 寻找并实时销毁已放置的卡片
        int cardIdx = activeCards.FindIndex(c => c != null && c.Product == product);
        if (cardIdx >= 0)
        {
            if (activeCards[cardIdx] != null && activeCards[cardIdx].gameObject != null)
            {
                Destroy(activeCards[cardIdx].gameObject);
            }
            activeCards.RemoveAt(cardIdx);
        }

        if (products.Count == 0)
        {
            PopulateCards();
            return;
        }

        if (titleText != null)
        {
            titleText.text = $"📦 战术构装库存 ({products.Count} 件)  ·  点击构装选中，在地面绿色菱形框上部署";
        }

        // 如果刚刚消耗的卡片正是当前选中的项目，自动切换选中下一件
        if (currentSelectedProduct == product)
        {
            OnCardSelected(products[0]);
        }
    }

    /// <summary>
    /// 当拆除或回收一件家具回仓库时触发：实时重新加入抽屉列表
    /// </summary>
    public void OnProductRecycled(CraftedProduct product)
    {
        PopulateCards(preferredSelected: product);
    }

    /// <summary>
    /// 玩家点击卡片选中某件家具
    /// </summary>
    public void OnCardSelected(CraftedProduct product)
    {
        currentSelectedProduct = product;

        if (buildController != null)
        {
            buildController.selectedProduct = product;
            buildController.ClearSelection();
        }

        // 刷新所有卡片的高亮状态
        foreach (var card in activeCards)
        {
            if (card != null)
            {
                card.SetSelected(card.Product == product);
            }
        }
    }

    /// <summary>
    /// 仓库为空时展示的空状态提示标签
    /// </summary>
    private void CreateEmptyWarehouseNotice(Transform parent)
    {
        GameObject noticeGo = new GameObject("EmptyWarehouseNotice", typeof(RectTransform), typeof(Text));
        noticeGo.transform.SetParent(parent, false);

        RectTransform rt = noticeGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(580f, 75f);

        Text txt = noticeGo.GetComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = 14;
        txt.lineSpacing = 1.25f;
        txt.fontStyle = FontStyle.Normal;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = new Color(0.95f, 0.85f, 0.45f, 0.9f);
        txt.text = "【战术构装仓库当前为空】\n请前往生体工坊进行背壳合成，产出设施或战术废料后再来部署！";
    }

    /// <summary>
    /// 代码动态兜底创建卡片 GameObject
    /// </summary>
    private GameObject CreateDefaultCardObject(Transform parent)
    {
        GameObject go = new GameObject("FurnitureCard", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(120f, 88f);

        Image bg = go.GetComponent<Image>();
        bg.sprite = cardBgNormal;
        bg.type = Image.Type.Sliced;

        // 图标
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(go.transform, false);
        RectTransform iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 0.62f);
        iconRt.anchorMax = new Vector2(0.5f, 0.62f);
        iconRt.sizeDelta = new Vector2(46f, 46f);
        Image iconImg = iconGo.GetComponent<Image>();
        iconImg.preserveAspect = true;

        // 标题
        GameObject nameGo = new GameObject("NameText", typeof(RectTransform), typeof(Text));
        nameGo.transform.SetParent(go.transform, false);
        RectTransform nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 0.08f);
        nameRt.anchorMax = new Vector2(1f, 0.35f);
        nameRt.offsetMin = new Vector2(4f, 0f);
        nameRt.offsetMax = new Vector2(-4f, 0f);
        Text nameText = nameGo.GetComponent<Text>();
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        nameText.fontSize = 12;
        nameText.fontStyle = FontStyle.Bold;
        nameText.alignment = TextAnchor.MiddleCenter;
        nameText.color = Color.white;

        return go;
    }
}
