using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace BiomechanicalCrafting
{
    public enum InteractionMode
    {
        LineCrafting = 0,   // ⚡ 连线合成模式：在相邻格子间滑动连线，松开立即推演转化
        Reorder = 1         // ✋ 拖拽重排模式：长按拖动物品交换位置，自由整理背壳
    }

    /// <summary>
    /// 背壳背包系统主控制器 (Shell Inventory Controller)
    /// 实现“背壳即背包”、“大小宫格”、“8向一笔画连线合成”、“拖拽重排”、
    /// “去文字化/低数值感”、“无废品涌现规则”以及“背包装满警告”。
    /// </summary>
    public class ShellInventoryController : MonoBehaviour
    {
        public static ShellInventoryController Instance { get; private set; }

        [Header("Shell Configuration")]
        public ShellType currentShellType = ShellType.ConchShell;
        private ShellConfig activeConfig;

        [Header("Interaction State")]
        public InteractionMode currentMode = InteractionMode.LineCrafting;

        [Header("UI Containers")]
        public RectTransform shellContainer;
        public RectTransform overlayContainer;
        public Image shellPlateImage;
        public Text shellTitleText;
        public Text shellFlavorText;
        public UILineRenderer strokeLineRenderer;

        [Header("Backpack Warning Banner")]
        public GameObject fullWarningBanner;
        public Text fullWarningText;

        [Header("Floating Detail Popup")]
        public GameObject detailPopupPanel;
        public Image detailIcon;
        public Text detailName;
        public Text detailElementBadge;
        public Text detailTypeBadge;
        public Text detailDescription;
        public Button detailCloseBtn;

        [Header("Emergence Crafting Result Modal")]
        public GameObject craftResultModal;
        public Image craftResultIcon;
        public Text craftResultName;
        public Text craftResultQualityBadge;
        public Text craftResultCategoryBadge;
        public Text craftResultEffect;
        public Text craftResultFlavor;
        public RectTransform consumedMaterialsContainer;
        public Button craftConfirmBtn;
        public Button craftCancelBtn;

        [Header("Drag Ghost Object")]
        public RectTransform dragGhost;
        public Image dragGhostIcon;

        [Header("Elemental Stats Fields")]
        public Text detailStatsText;
        public Text craftResultStatsText;

        [Header("Live Stroke Synthesis HUD")]
        public GameObject liveStrokeHUD;
        public Image liveStrokeBg;
        public Text liveStrokeNodeText;
        public Text liveStrokeDeltaText;
        public Text liveStrokeTotalText;
        public Text liveStrokePredictText;

        [Header("Codex Illustrated Handbook")]
        public Button btnBlueprintCodex;
        public BlueprintCodexModal codexModal;

        [Header("Sidebar Blueprint Dock")]
        public BlueprintSidebarDock blueprintDock;

        [Header("Mode & Tool Buttons")]
        public Button modeLineBtn;
        public Button modeReorderBtn;
        public Text modeLineBtnText;
        public Text modeReorderBtnText;

        public Button switchConchBtn;
        public Button switchClamBtn;
        public Button switchCanBtn;

        public Button btnAddRandom;
        public Button btnFillAll;
        public Button btnClearAll;
        public Button btnDemoStroke;

        // Runtime Data
        private List<ShellSlotView> slotViews = new List<ShellSlotView>();
        private BiomechanicalMaterial[] slotItems = new BiomechanicalMaterial[32];

        // Line-drawing stroke tracking
        private List<ShellSlotView> currentStrokeSlots = new List<ShellSlotView>();
        private bool isDrawingStroke = false;

        // Dragging tracking
        private ShellSlotView dragSourceSlot = null;
        private bool isDraggingItem = false;

        // Pending crafted product
        private CraftedProduct pendingProduct = null;
        private List<int> pendingConsumedSlotIndices = new List<int>();

        // Procedural Sprite Cache
        private Sprite roundSlotBgSprite;
        private Sprite majorBorderSprite;
        private Sprite minorBorderSprite;
        private Sprite badgeBgSprite;
        private Sprite plateBgSprite;
        private Sprite buttonBgSprite;

        void Awake()
        {
            Instance = this;
            GenerateProceduralSprites();
        }

        void Start()
        {
            InitUIEvents();
            SwitchShell(currentShellType);

            // 填充一组初始特色材料，展现海螺壳生体风貌
            SeedInitialMaterials();
            UpdateLiveStrokeHUD();
        }

        void Update()
        {
            // 键盘快捷测试键
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Tab))
            {
                SetMode(currentMode == InteractionMode.LineCrafting ? InteractionMode.Reorder : InteractionMode.LineCrafting);
            }
            if (Input.GetKeyDown(KeyCode.R)) AddRandomMaterial();
            if (Input.GetKeyDown(KeyCode.F)) FillAllSlotsRandomly();
            if (Input.GetKeyDown(KeyCode.C)) ClearAllSlots();
            if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchShell(ShellType.ConchShell);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchShell(ShellType.ClamShell);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchShell(ShellType.RustedCanShell);
            if (Input.GetKeyDown(KeyCode.B)) { if (codexModal != null) codexModal.Toggle(); }

            // 满载警报静态稳定呈现（去除高频缩放闪烁，提升视觉舒适度）
            if (fullWarningBanner != null && fullWarningBanner.activeSelf)
            {
                fullWarningBanner.transform.localScale = Vector3.one;
            }
        }

        #region Procedural Sprites Generation

        private void GenerateProceduralSprites()
        {
            roundSlotBgSprite = CreateRoundedBoxSprite(96, 96, 18, new Color(0.08f, 0.12f, 0.16f, 0.85f), new Color(0.2f, 0.3f, 0.4f, 0.5f), 2);
            majorBorderSprite = CreateRoundedBoxSprite(110, 110, 24, new Color(0f, 0f, 0f, 0f), new Color(0.96f, 0.82f, 0.28f, 0.95f), 5);
            minorBorderSprite = CreateRoundedBoxSprite(80, 80, 16, new Color(0f, 0f, 0f, 0f), new Color(0.35f, 0.48f, 0.62f, 0.7f), 3);
            badgeBgSprite = CreateCircleSprite(36, new Color(0.98f, 0.82f, 0.18f, 1f), new Color(0.1f, 0.1f, 0.1f, 1f));
            plateBgSprite = CreateRoundedBoxSprite(256, 256, 36, new Color(0.06f, 0.08f, 0.12f, 0.9f), new Color(0.25f, 0.35f, 0.48f, 0.6f), 4);
            buttonBgSprite = CreateRoundedBoxSprite(128, 48, 12, new Color(0.15f, 0.22f, 0.32f, 0.95f), new Color(0.4f, 0.6f, 0.8f, 0.8f), 2);
        }

        private Sprite CreateRoundedBoxSprite(int width, int height, int radius, Color fillColor, Color borderColor, int borderWidth)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            Color transparent = new Color(0, 0, 0, 0);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 计算到4个圆角的距离
                    int dx = Mathf.Min(x, width - 1 - x);
                    int dy = Mathf.Min(y, height - 1 - y);

                    bool insideCorner = true;
                    float distFromCorner = 0f;

                    if (dx < radius && dy < radius)
                    {
                        distFromCorner = Mathf.Sqrt(Mathf.Pow(radius - dx, 2) + Mathf.Pow(radius - dy, 2));
                        if (distFromCorner > radius)
                        {
                            insideCorner = false;
                        }
                    }

                    if (!insideCorner)
                    {
                        tex.SetPixel(x, y, transparent);
                    }
                    else
                    {
                        // 判定是否在边框上
                        bool isBorder = false;
                        if (dx < borderWidth || dy < borderWidth) isBorder = true;
                        if (dx < radius && dy < radius && distFromCorner >= radius - borderWidth) isBorder = true;

                        tex.SetPixel(x, y, isBorder ? borderColor : fillColor);
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }

        private Sprite CreateCircleSprite(int size, Color fillColor, Color outlineColor)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = size * 0.5f;
            float radius = center - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (dist > radius)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else if (dist >= radius - 3f)
                    {
                        tex.SetPixel(x, y, outlineColor);
                    }
                    else
                    {
                        tex.SetPixel(x, y, fillColor);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        #endregion

        #region Shell Switching & Initialization

        public void SwitchShell(ShellType newType)
        {
            currentShellType = newType;
            activeConfig = ShellConfig.CreateConfig(newType);

            // 更新标题与韵味文本
            if (shellTitleText != null) shellTitleText.text = activeConfig.shellName;
            if (shellFlavorText != null) shellFlavorText.text = activeConfig.flavorText;

            // 清理旧宫格
            if (shellContainer != null)
            {
                for (int i = shellContainer.childCount - 1; i >= 0; i--)
                {
                    var child = shellContainer.GetChild(i).gameObject;
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(child);
                    else
                        Destroy(child);
#else
                    Destroy(child);
#endif
                }
            }
            slotViews.Clear();

            // 重新根据配置构建宫格
            for (int i = 0; i < activeConfig.slots.Count; i++)
            {
                ShellSlotLayout layout = activeConfig.slots[i];
                ShellSlotView view = CreateSlotViewGameObject(layout);
                slotViews.Add(view);
            }

            // 创建专用的 2x2 骨架顶层容器，确保其在所有网格槽位之后渲染（置于最顶层，杜绝下层网格背景遮挡）
            GameObject overlayContainerGO = new GameObject("Overlay2x2Container");
            overlayContainerGO.transform.SetParent(shellContainer, false);
            overlayContainer = overlayContainerGO.AddComponent<RectTransform>();
            overlayContainer.anchorMin = new Vector2(0.5f, 0.5f);
            overlayContainer.anchorMax = new Vector2(0.5f, 0.5f);
            overlayContainer.pivot = new Vector2(0.5f, 0.5f);
            overlayContainer.anchoredPosition = Vector2.zero;
            overlayContainer.sizeDelta = Vector2.zero;

            float step = 76f;
            float spanSize = 146f;
            for (int i = 0; i < slotViews.Count; i++)
            {
                Create2x2OverlayForSlot(slotViews[i], overlayContainer, step, spanSize);
            }

            // 统一刷新所有槽位视图（包含 2x2 覆盖层与 1x1 状态）
            RefreshSlotVisuals();

            // 更新背包装满警告
            UpdateBackpackFullStatus();

            // 确保连线渲染器清空并置于顶层
            if (strokeLineRenderer != null)
            {
                strokeLineRenderer.Clear();
                strokeLineRenderer.transform.SetAsLastSibling();
            }
            currentStrokeSlots.Clear();

            // 刷新蓝图侧边栏
            RefreshBlueprintDock();

            // 更新 Shell Switcher 按钮选中高亮
            UpdateShellButtonHighlights();
        }

        private ShellSlotView CreateSlotViewGameObject(ShellSlotLayout layout)
        {
            GameObject slotGO = new GameObject($"Slot_{layout.slotId}_{(layout.isMajor ? "Major" : "Minor")}");
            slotGO.transform.SetParent(shellContainer, false);

            RectTransform rt = slotGO.AddComponent<RectTransform>();
            rt.anchoredPosition = layout.relativePos;
            rt.sizeDelta = new Vector2(layout.size, layout.size);

            // 1. 背景底板
            Image bgImg = slotGO.AddComponent<Image>();
            bgImg.sprite = roundSlotBgSprite;
            bgImg.color = Color.white;

            // 2. 边框 (大小宫格不同)
            GameObject borderGO = new GameObject("Border");
            borderGO.transform.SetParent(slotGO.transform, false);
            RectTransform borderRT = borderGO.AddComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.sizeDelta = Vector2.zero;
            Image borderImg = borderGO.AddComponent<Image>();
            borderImg.sprite = layout.isMajor ? majorBorderSprite : minorBorderSprite;
            borderImg.raycastTarget = false;

            // 3. 元素光环
            GameObject glowGO = new GameObject("ElementGlow");
            glowGO.transform.SetParent(slotGO.transform, false);
            RectTransform glowRT = glowGO.AddComponent<RectTransform>();
            glowRT.anchorMin = Vector2.zero;
            glowRT.anchorMax = Vector2.one;
            glowRT.sizeDelta = Vector2.zero;
            Image glowImg = glowGO.AddComponent<Image>();
            glowImg.sprite = roundSlotBgSprite;
            glowImg.raycastTarget = false;
            glowGO.SetActive(false);

            // 4. 材料图元
            GameObject iconGO = new GameObject("ItemIcon");
            iconGO.transform.SetParent(slotGO.transform, false);
            RectTransform iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.1f, 0.1f);
            iconRT.anchorMax = new Vector2(0.9f, 0.9f);
            iconRT.sizeDelta = Vector2.zero;
            Image iconImg = iconGO.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconGO.SetActive(false);

            // 5. 大格核心标记 (金色小角标)
            GameObject majorBadgeGO = new GameObject("MajorBadge");
            majorBadgeGO.transform.SetParent(slotGO.transform, false);
            RectTransform majorRT = majorBadgeGO.AddComponent<RectTransform>();
            majorRT.anchorMin = new Vector2(1, 1);
            majorRT.anchorMax = new Vector2(1, 1);
            majorRT.pivot = new Vector2(1, 1);
            majorRT.anchoredPosition = new Vector2(-4, -4);
            majorRT.sizeDelta = new Vector2(18, 18);
            Image majorBadgeImg = majorBadgeGO.AddComponent<Image>();
            majorBadgeImg.sprite = badgeBgSprite;
            majorBadgeImg.color = new Color(0.98f, 0.82f, 0.18f, 0.9f);
            majorBadgeImg.raycastTarget = false;
            majorBadgeGO.SetActive(layout.isMajor);

            // 6. 序号气泡 (1, 2, 3...)
            GameObject seqGO = new GameObject("SeqBadge");
            seqGO.transform.SetParent(slotGO.transform, false);
            RectTransform seqRT = seqGO.AddComponent<RectTransform>();
            seqRT.anchorMin = new Vector2(0, 1);
            seqRT.anchorMax = new Vector2(0, 1);
            seqRT.pivot = new Vector2(0, 1);
            seqRT.anchoredPosition = new Vector2(4, -4);
            seqRT.sizeDelta = new Vector2(24, 24);
            Image seqBg = seqGO.AddComponent<Image>();
            seqBg.sprite = badgeBgSprite;
            seqBg.color = new Color(0.98f, 0.82f, 0.18f, 1f);
            seqBg.raycastTarget = false;

            GameObject seqTextGO = new GameObject("SeqText");
            seqTextGO.transform.SetParent(seqGO.transform, false);
            RectTransform stRT = seqTextGO.AddComponent<RectTransform>();
            stRT.anchorMin = Vector2.zero;
            stRT.anchorMax = Vector2.one;
            stRT.sizeDelta = Vector2.zero;
            Text seqTxt = seqTextGO.AddComponent<Text>();
            seqTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            seqTxt.fontSize = 15;
            seqTxt.fontStyle = FontStyle.Bold;
            seqTxt.alignment = TextAnchor.MiddleCenter;
            seqTxt.color = new Color(0.08f, 0.08f, 0.12f, 1f);
            seqTxt.raycastTarget = false;
            seqGO.SetActive(false);

            // 7. 槽位浮动增量角标 (DeltaBadge)
            GameObject deltaGO = new GameObject("DeltaBadge");
            deltaGO.transform.SetParent(slotGO.transform, false);
            RectTransform dtRT = deltaGO.AddComponent<RectTransform>();
            dtRT.anchorMin = new Vector2(0, 0);
            dtRT.anchorMax = new Vector2(1, 0);
            dtRT.pivot = new Vector2(0.5f, 0);
            dtRT.anchoredPosition = new Vector2(0, 3);
            dtRT.sizeDelta = new Vector2(-6, 17);
            Image deltaBg = deltaGO.AddComponent<Image>();
            deltaBg.sprite = roundSlotBgSprite;
            deltaBg.color = new Color(0.04f, 0.07f, 0.12f, 0.92f);
            deltaBg.raycastTarget = false;

            GameObject deltaTxtGO = new GameObject("DeltaText");
            deltaTxtGO.transform.SetParent(deltaGO.transform, false);
            RectTransform dttRT = deltaTxtGO.AddComponent<RectTransform>();
            dttRT.anchorMin = Vector2.zero;
            dttRT.anchorMax = Vector2.one;
            dttRT.sizeDelta = Vector2.zero;
            Text deltaTxt = deltaTxtGO.AddComponent<Text>();
            deltaTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            deltaTxt.fontSize = 10;
            deltaTxt.fontStyle = FontStyle.Bold;
            deltaTxt.alignment = TextAnchor.MiddleCenter;
            deltaTxt.color = Color.white;
            deltaTxt.raycastTarget = false;
            deltaGO.SetActive(false);

            // 挂载控制器组件
            ShellSlotView view = slotGO.AddComponent<ShellSlotView>();
            view.bgImage = bgImg;
            view.borderImage = borderImg;
            view.itemIconImage = iconImg;
            view.elementGlowImage = glowImg;
            view.majorBadgeImage = majorBadgeImg;
            view.sequenceBadge = seqGO;
            view.sequenceText = seqTxt;
            view.deltaBadge = deltaGO;
            view.deltaText = deltaTxt;

            view.Setup(layout);

            // 绑定交互回调
            view.onSlotPointerDown += HandleSlotPointerDown;
            view.onSlotPointerEnter += HandleSlotPointerEnter;
            view.onSlotPointerUp += HandleSlotPointerUp;
            view.onSlotBeginDrag += HandleSlotBeginDrag;
            view.onSlotDrag += HandleSlotDrag;
            view.onSlotEndDrag += HandleSlotEndDrag;
            view.onSlotClick += HandleSlotClick;

            return view;
        }

        private void Create2x2OverlayForSlot(ShellSlotView view, RectTransform container, float step, float spanSize)
        {
            if (view == null || container == null || view.layout == null) return;

            GameObject ovRoot = new GameObject($"Overlay2x2_Slot_{view.slotId}");
            ovRoot.transform.SetParent(container, false);
            RectTransform ovRT = ovRoot.AddComponent<RectTransform>();
            ovRT.anchorMin = new Vector2(0.5f, 0.5f);
            ovRT.anchorMax = new Vector2(0.5f, 0.5f);
            ovRT.pivot = new Vector2(0.5f, 0.5f);
            // 精确对齐 2x2 区域的几何中心点
            ovRT.anchoredPosition = view.layout.relativePos + new Vector2(step * 0.5f, -step * 0.5f);
            ovRT.sizeDelta = new Vector2(spanSize, spanSize);

            CanvasGroup cg = ovRoot.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            Image ovBg = ovRoot.AddComponent<Image>();
            ovBg.sprite = roundSlotBgSprite;
            ovBg.color = new Color(0.06f, 0.10f, 0.16f, 0.98f);
            ovBg.raycastTarget = true; // 启用全域射线拦截，覆盖 2x2 整个 146x146 区域（含中心缝隙）

            // 挂载交互转发器，将点击、划线进入、拖拽直接映射到根槽位
            Shell2x2OverlayView forwarder = ovRoot.AddComponent<Shell2x2OverlayView>();
            forwarder.rootSlot = view;

            GameObject ovBorderGO = new GameObject("Border");
            ovBorderGO.transform.SetParent(ovRoot.transform, false);
            RectTransform ovbRT = ovBorderGO.AddComponent<RectTransform>();
            ovbRT.anchorMin = Vector2.zero;
            ovbRT.anchorMax = Vector2.one;
            ovbRT.sizeDelta = Vector2.zero;
            Image ovBorder = ovBorderGO.AddComponent<Image>();
            ovBorder.sprite = majorBorderSprite;
            ovBorder.color = new Color(0.98f, 0.82f, 0.18f, 1f);
            ovBorder.raycastTarget = false;

            GameObject ovIconGO = new GameObject("Icon");
            ovIconGO.transform.SetParent(ovRoot.transform, false);
            RectTransform oviRT = ovIconGO.AddComponent<RectTransform>();
            oviRT.anchorMin = new Vector2(0.5f, 0.5f);
            oviRT.anchorMax = new Vector2(0.5f, 0.5f);
            oviRT.anchoredPosition = new Vector2(0, 14);
            oviRT.sizeDelta = new Vector2(74, 74);
            Image ovIcon = ovIconGO.AddComponent<Image>();
            ovIcon.preserveAspect = true;
            ovIcon.raycastTarget = false;

            GameObject ovBadgeGO = new GameObject("Badge");
            ovBadgeGO.transform.SetParent(ovRoot.transform, false);
            RectTransform ovbdgRT = ovBadgeGO.AddComponent<RectTransform>();
            ovbdgRT.anchorMin = new Vector2(0, 1);
            ovbdgRT.anchorMax = new Vector2(1, 1);
            ovbdgRT.pivot = new Vector2(0.5f, 1);
            ovbdgRT.anchoredPosition = new Vector2(0, -5);
            ovbdgRT.sizeDelta = new Vector2(-10, 18);
            Text ovBadge = ovBadgeGO.AddComponent<Text>();
            ovBadge.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ovBadge.fontSize = 11;
            ovBadge.fontStyle = FontStyle.Bold;
            ovBadge.alignment = TextAnchor.MiddleCenter;
            ovBadge.color = new Color(0.98f, 0.82f, 0.18f);
            ovBadge.raycastTarget = false;

            GameObject ovNameGO = new GameObject("Name");
            ovNameGO.transform.SetParent(ovRoot.transform, false);
            RectTransform ovnRT = ovNameGO.AddComponent<RectTransform>();
            ovnRT.anchorMin = new Vector2(0, 0);
            ovnRT.anchorMax = new Vector2(1, 0);
            ovnRT.pivot = new Vector2(0.5f, 0);
            ovnRT.anchoredPosition = new Vector2(0, 22);
            ovnRT.sizeDelta = new Vector2(-10, 18);
            Text ovName = ovNameGO.AddComponent<Text>();
            ovName.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ovName.fontSize = 12;
            ovName.fontStyle = FontStyle.Bold;
            ovName.alignment = TextAnchor.MiddleCenter;
            ovName.color = Color.white;
            ovName.raycastTarget = false;

            GameObject ovStatsGO = new GameObject("Stats");
            ovStatsGO.transform.SetParent(ovRoot.transform, false);
            RectTransform ovsRT = ovStatsGO.AddComponent<RectTransform>();
            ovsRT.anchorMin = new Vector2(0, 0);
            ovsRT.anchorMax = new Vector2(1, 0);
            ovsRT.pivot = new Vector2(0.5f, 0);
            ovsRT.anchoredPosition = new Vector2(0, 4);
            ovsRT.sizeDelta = new Vector2(-10, 16);
            Text ovStats = ovStatsGO.AddComponent<Text>();
            ovStats.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ovStats.fontSize = 10;
            ovStats.alignment = TextAnchor.MiddleCenter;
            ovStats.color = new Color(0.7f, 0.85f, 1f);
            ovStats.raycastTarget = false;

            ovRoot.SetActive(false);

            view.overlay2x2Root = ovRoot;
            view.overlay2x2CanvasGroup = cg;
            view.overlay2x2Border = ovBorder;
            view.overlay2x2Icon = ovIcon;
            view.overlay2x2Badge = ovBadge;
            view.overlay2x2Name = ovName;
            view.overlay2x2Stats = ovStats;
        }

        #endregion

        #region Interaction Handling (8-Way Line-Drawing & Drag Reorder)

        public void SetMode(InteractionMode mode)
        {
            currentMode = mode;
            if (modeLineBtnText != null) modeLineBtnText.color = mode == InteractionMode.LineCrafting ? Color.white : new Color(0.7f, 0.7f, 0.7f);
            if (modeReorderBtnText != null) modeReorderBtnText.color = mode == InteractionMode.Reorder ? Color.white : new Color(0.7f, 0.7f, 0.7f);

            // 模式高亮色彩反馈
            if (modeLineBtn != null) modeLineBtn.image.color = mode == InteractionMode.LineCrafting ? new Color(0.98f, 0.82f, 0.18f, 0.85f) : new Color(0.2f, 0.3f, 0.4f, 0.6f);
            if (modeReorderBtn != null) modeReorderBtn.image.color = mode == InteractionMode.Reorder ? new Color(0.29f, 0.85f, 0.44f, 0.85f) : new Color(0.2f, 0.3f, 0.4f, 0.6f);

            ResetStroke();
            ResetDrag();
        }

        private void HandleSlotPointerDown(ShellSlotView slot, PointerEventData eventData)
        {
            if (currentMode == InteractionMode.LineCrafting)
            {
                if (slot.currentMaterial != null)
                {
                    isDrawingStroke = true;
                    currentStrokeSlots.Clear();
                    currentStrokeSlots.Add(slot);
                    UpdateStrokeVisuals();
                    UpdateLiveStrokeHUD();
                }
            }
        }

        private void HandleSlotPointerEnter(ShellSlotView slot, PointerEventData eventData)
        {
            if (currentMode == InteractionMode.LineCrafting && isDrawingStroke)
            {
                if (slot.currentMaterial == null) return;

                int lastIdx = currentStrokeSlots.Count - 1;
                ShellSlotView lastSlot = currentStrokeSlots[lastIdx];

                // 1. 同一 2x2 材料内部子格子间移动，不重复添加节点
                if (slot.currentMaterial == lastSlot.currentMaterial)
                {
                    return;
                }

                // 2. 如果手指回退到上一个节点材料，支持撤回上一步 (Backtrack Undo)
                if (currentStrokeSlots.Count >= 2 && slot.currentMaterial == currentStrokeSlots[lastIdx - 1].currentMaterial)
                {
                    currentStrokeSlots.RemoveAt(lastIdx);
                    UpdateStrokeVisuals();
                    UpdateLiveStrokeHUD();
                    return;
                }

                // 3. 检查材料是否已经在连线中 (禁止重复连接同一物理材料)
                bool alreadyInStroke = false;
                foreach (var s in currentStrokeSlots)
                {
                    if (s.currentMaterial == slot.currentMaterial)
                    {
                        alreadyInStroke = true;
                        break;
                    }
                }
                if (alreadyInStroke) return;

                // 4. 8向相邻逻辑判定（支持 2x2 跨格材料任意子格子与相邻格子的连接）
                if (CheckMaterialAdjacency(lastSlot.currentMaterial, slot.currentMaterial))
                {
                    currentStrokeSlots.Add(slot);
                    UpdateStrokeVisuals();
                    UpdateLiveStrokeHUD();
                }
            }
        }

        private bool CheckMaterialAdjacency(BiomechanicalMaterial matA, BiomechanicalMaterial matB)
        {
            if (matA == null || matB == null || activeConfig == null) return false;

            List<int> slotsA = (matA.occupiedSlotIds != null && matA.occupiedSlotIds.Count > 0)
                ? matA.occupiedSlotIds
                : new List<int> { matA.rootSlotId };

            List<int> slotsB = (matB.occupiedSlotIds != null && matB.occupiedSlotIds.Count > 0)
                ? matB.occupiedSlotIds
                : new List<int> { matB.rootSlotId };

            foreach (int idA in slotsA)
            {
                if (idA < 0 || idA >= activeConfig.slots.Count) continue;
                var layoutA = activeConfig.slots[idA];

                foreach (int idB in slotsB)
                {
                    if (idB < 0 || idB >= activeConfig.slots.Count) continue;
                    var layoutB = activeConfig.slots[idB];

                    if (layoutA.IsAdjacent8Way(layoutB))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void HandleSlotPointerUp(ShellSlotView slot, PointerEventData eventData)
        {
            if (currentMode == InteractionMode.LineCrafting && isDrawingStroke)
            {
                isDrawingStroke = false;

                if (currentStrokeSlots.Count >= 2)
                {
                    // 触发涌现合成推演！
                    ResolveCraftingFromStroke();
                }
                else
                {
                    // 仅点击单格，重置连线
                    ResetStroke();
                }
            }
        }

        private void HandleSlotClick(ShellSlotView slot, PointerEventData eventData)
        {
            if (!isDrawingStroke && !isDraggingItem)
            {
                if (slot.currentMaterial != null)
                {
                    ShowDetailPopup(slot.currentMaterial, slot.layout.isMajor);
                }
                else
                {
                    // 点击空格子，取消蓝图选中与高亮
                    if (blueprintDock != null) blueprintDock.ClearSelection();
                }
            }
        }

        // ================= 拖拽重排 (Reorder Mode) =================
        private void HandleSlotBeginDrag(ShellSlotView slot, PointerEventData eventData)
        {
            if (currentMode == InteractionMode.Reorder && slot.currentMaterial != null)
            {
                isDraggingItem = true;
                dragSourceSlot = slot;
                var mat = slot.currentMaterial;

                if (dragGhost != null && dragGhostIcon != null)
                {
                    dragGhost.gameObject.SetActive(true);
                    dragGhostIcon.sprite = mat.GetOrLoadSprite();
                    dragGhost.position = eventData.position;

                    // 2x2 骨架与 1x1 辅料拖拽幽灵尺寸自适应
                    if (mat.isMajor)
                    {
                        dragGhost.sizeDelta = new Vector2(146, 146);
                    }
                    else
                    {
                        dragGhost.sizeDelta = new Vector2(70, 70);
                    }
                }

                // 虚化正在被拖拽的格位
                if (mat.isMajor && mat.occupiedSlotIds != null)
                {
                    foreach (int id in mat.occupiedSlotIds)
                    {
                        if (id < slotViews.Count) slotViews[id].SetGhostDimmed(true);
                    }
                }
                else
                {
                    slot.SetGhostDimmed(true);
                }
            }
        }

        private void HandleSlotDrag(ShellSlotView slot, PointerEventData eventData)
        {
            if (isDraggingItem && dragGhost != null)
            {
                dragGhost.position = eventData.position;
            }
        }

        private void HandleSlotEndDrag(ShellSlotView slot, PointerEventData eventData)
        {
            if (isDraggingItem)
            {
                isDraggingItem = false;
                if (dragGhost != null) dragGhost.gameObject.SetActive(false);

                // 解除所有幽灵半透明
                foreach (var sv in slotViews)
                {
                    sv.SetGhostDimmed(false);
                }

                ShellSlotView targetSlot = FindSlotUnderPointer(eventData);
                if (dragSourceSlot != null && targetSlot != null && targetSlot != dragSourceSlot)
                {
                    ExecuteReorderDrop(dragSourceSlot, targetSlot);
                }

                dragSourceSlot = null;
                RefreshSlotVisuals();
                UpdateBackpackFullStatus();
                RefreshBlueprintDock();
            }
        }

        private void ExecuteReorderDrop(ShellSlotView srcView, ShellSlotView dstView)
        {
            if (srcView == null || dstView == null || activeConfig == null) return;
            var draggedMat = srcView.currentMaterial;
            if (draggedMat == null) return;

            int srcId = srcView.slotId;
            int dstId = dstView.slotId;

            // -------------------------------------------------------------
            // 情况 A：拖拽的是 2x2 骨架核心 (draggedMat.isMajor)
            // -------------------------------------------------------------
            if (draggedMat.isMajor)
            {
                var targetSlotLayout = dstView.layout;
                List<int> candidate2x2Ids = activeConfig.Get2x2SlotIds(targetSlotLayout.gridCoord.x, targetSlotLayout.gridCoord.y);

                if (candidate2x2Ids == null)
                {
                    candidate2x2Ids = activeConfig.Get2x2SlotIds(targetSlotLayout.gridCoord.x - 1, targetSlotLayout.gridCoord.y);
                }
                if (candidate2x2Ids == null)
                {
                    candidate2x2Ids = activeConfig.Get2x2SlotIds(targetSlotLayout.gridCoord.x, targetSlotLayout.gridCoord.y - 1);
                }
                if (candidate2x2Ids == null)
                {
                    candidate2x2Ids = activeConfig.Get2x2SlotIds(targetSlotLayout.gridCoord.x - 1, targetSlotLayout.gridCoord.y - 1);
                }

                if (candidate2x2Ids == null || candidate2x2Ids.Count != 4)
                {
                    Debug.Log("<color=#FF9070>[无法重排]</color> 目标区域不足以容纳 2x2 骨架核心。");
                    return;
                }

                bool canPlaceClean = true;
                BiomechanicalMaterial other2x2Mat = null;
                bool isClean2x2Swap = true;

                foreach (int tid in candidate2x2Ids)
                {
                    var itemAtT = (tid < slotItems.Length) ? slotItems[tid] : null;
                    if (itemAtT != null && itemAtT != draggedMat)
                    {
                        canPlaceClean = false;
                        if (itemAtT.isMajor)
                        {
                            if (other2x2Mat == null) other2x2Mat = itemAtT;
                            else if (other2x2Mat != itemAtT) isClean2x2Swap = false;
                        }
                        else
                        {
                            isClean2x2Swap = false;
                        }
                    }
                }

                List<int> originalSlots = new List<int>(draggedMat.occupiedSlotIds != null ? draggedMat.occupiedSlotIds : new List<int> { srcId });

                if (canPlaceClean)
                {
                    // 目标 4 格为空或被自己原位置重叠：平移搬迁
                    foreach (int oid in originalSlots) if (oid < slotItems.Length) slotItems[oid] = null;
                    Place2x2MaterialAt(draggedMat, candidate2x2Ids);
                    Debug.Log($"<color=#70D2FF>[骨架重排成功]</color> 将 2x2 {draggedMat.materialName} 移至新区域。");
                }
                else if (isClean2x2Swap && other2x2Mat != null && other2x2Mat != draggedMat)
                {
                    // 两个 2x2 核心完美对调位置
                    List<int> otherSlots = new List<int>(other2x2Mat.occupiedSlotIds);
                    foreach (int oid in originalSlots) if (oid < slotItems.Length) slotItems[oid] = null;
                    foreach (int oid in otherSlots) if (oid < slotItems.Length) slotItems[oid] = null;

                    Place2x2MaterialAt(draggedMat, otherSlots);
                    Place2x2MaterialAt(other2x2Mat, originalSlots);
                    Debug.Log($"<color=#70D2FF>[核心对调成功]</color> 交换 {draggedMat.materialName} 与 {other2x2Mat.materialName} 的 2x2 骨架位置。");
                }
                else
                {
                    Debug.Log("<color=#FF9070>[无法放置]</color> 目标区域含有其他辅料构件，无法直接放下 2x2 骨架。");
                }
            }
            // -------------------------------------------------------------
            // 情况 B：拖拽的是 1x1 生体辅料 (!draggedMat.isMajor)
            // -------------------------------------------------------------
            else
            {
                var targetMat = (dstId < slotItems.Length) ? slotItems[dstId] : null;

                if (targetMat == null)
                {
                    slotItems[srcId] = null;
                    Place1x1MaterialAt(draggedMat, dstId);
                    Debug.Log($"<color=#70D2FF>[辅料移动成功]</color> {draggedMat.materialName} 移至槽位 [{dstId}]。");
                }
                else if (!targetMat.isMajor)
                {
                    Place1x1MaterialAt(targetMat, srcId);
                    Place1x1MaterialAt(draggedMat, dstId);
                    Debug.Log($"<color=#70D2FF>[辅料对调成功]</color> 交换 {draggedMat.materialName} 与 {targetMat.materialName}。");
                }
                else
                {
                    Debug.Log("<color=#FF9070>[无法放置]</color> 不能将 1x1 辅料拖入 2x2 骨架核心占用的空间。");
                }
            }
        }

        private ShellSlotView FindSlotUnderPointer(PointerEventData eventData)
        {
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            foreach (var r in results)
            {
                // 优先检查是否命中了 2x2 顶层覆盖层
                var fwd = r.gameObject.GetComponentInParent<Shell2x2OverlayView>();
                if (fwd != null && fwd.rootSlot != null) return fwd.rootSlot;

                ShellSlotView s = r.gameObject.GetComponentInParent<ShellSlotView>();
                if (s != null) return s;
            }
            return null;
        }

        private void ResetDrag()
        {
            if (dragGhost != null) dragGhost.gameObject.SetActive(false);
            foreach (var sv in slotViews)
            {
                sv.SetGhostDimmed(false);
            }
            dragSourceSlot = null;
            isDraggingItem = false;
        }

        #endregion

        #region Stroke Rendering & Visuals

        private void UpdateStrokeVisuals()
        {
            // 1. 取消所有格子的连线高亮
            foreach (var sv in slotViews)
            {
                sv.SetHighlighted(false, -1);
            }

            if (currentStrokeSlots.Count == 0)
            {
                if (strokeLineRenderer != null) strokeLineRenderer.Clear();
                return;
            }

            // 2. 计算当前连线所连材料的主导元素颜色
            Color pulseColor = new Color(0.98f, 0.82f, 0.18f); // 默认金色
            if (currentStrokeSlots[0].currentMaterial != null)
            {
                pulseColor = currentStrokeSlots[0].currentMaterial.ElementColor;
            }

            // 3. 为路径上的每个格子加上序号徽标 (1 -> 2 -> 3)
            List<Vector2> linePoints = new List<Vector2>();
            for (int i = 0; i < currentStrokeSlots.Count; i++)
            {
                ShellSlotView sv = currentStrokeSlots[i];
                var mat = sv.currentMaterial;
                int stepNum = i + 1;

                sv.SetHighlighted(true, stepNum, pulseColor);

                // 若为 2x2 骨架核心，同时高亮根槽位的 2x2 覆盖层边框，连线对齐到 2x2 中心
                if (mat != null && mat.isMajor && mat.rootSlotId >= 0 && mat.rootSlotId < slotViews.Count)
                {
                    var rootSlot = slotViews[mat.rootSlotId];
                    rootSlot.SetHighlighted(true, stepNum, pulseColor);
                    if (rootSlot.overlay2x2Border != null)
                    {
                        rootSlot.overlay2x2Border.color = pulseColor;
                    }
                    if (activeConfig != null && mat.rootSlotId < activeConfig.slots.Count)
                    {
                        var rootLayout = activeConfig.slots[mat.rootSlotId];
                        linePoints.Add(rootLayout.relativePos + new Vector2(38f, -38f));
                        continue;
                    }
                }

                linePoints.Add(sv.layout.relativePos);
            }

            // 4. 将点阵传入 UILineRenderer
            if (strokeLineRenderer != null)
            {
                strokeLineRenderer.SetColor(pulseColor);
                strokeLineRenderer.SetPoints(linePoints);
            }
        }

        public void ResetStroke()
        {
            isDrawingStroke = false;
            currentStrokeSlots.Clear();
            if (strokeLineRenderer != null) strokeLineRenderer.Clear();
            foreach (var sv in slotViews)
            {
                sv.SetHighlighted(false, -1);
            }
            UpdateLiveStrokeHUD();
        }

        public void UpdateLiveStrokeHUD()
        {
            if (liveStrokeHUD == null) return;

            // 1. 如果当前没有选中的槽位，恢复待机指引状态
            if (currentStrokeSlots.Count == 0)
            {
                if (liveStrokeNodeText != null)
                    liveStrokeNodeText.text = "<color=#94A3B8>[生体回路待机]</color>";
                if (liveStrokeDeltaText != null)
                    liveStrokeDeltaText.text = "<color=#94A3B8>按住滑动连接相邻材料，实时聚合推演 P/E/G/W 动力学与战术构装</color>";
                if (liveStrokeTotalText != null)
                    liveStrokeTotalText.text = "<b>聚合能量:</b> <color=#64748B>💨 P: 0 | ⚡ E: 0 | 🌿 G: 0 | 💧 W: 0</color>";
                if (liveStrokePredictText != null)
                    liveStrokePredictText.text = "<color=#64748B>✦ 预演构装: 等待回路连线...</color>";
                if (liveStrokeBg != null)
                    liveStrokeBg.color = new Color(0.04f, 0.07f, 0.12f, 0.94f);
                return;
            }

            // 2. 收集当前路径上的所有有效材料
            List<BiomechanicalMaterial> lineMats = new List<BiomechanicalMaterial>();
            foreach (var s in currentStrokeSlots)
            {
                if (s.currentMaterial != null) lineMats.Add(s.currentMaterial);
            }

            if (lineMats.Count == 0) return;

            int nodeCount = lineMats.Count;
            var latestMat = lineMats[lineMats.Count - 1];

            // 3. 回路阻抗转化率与骨架共鸣状态
            float efficiency = 1.0f;
            if (nodeCount == 3) efficiency = 0.85f;
            else if (nodeCount == 4) efficiency = 0.70f;
            else if (nodeCount >= 5) efficiency = 0.50f;

            string effTag = efficiency < 1.0f 
                ? $" <color=#F59E0B>(阻抗转化率 {Mathf.RoundToInt(efficiency * 100)}%)</color>" 
                : " <color=#10B981>(转化率 100%)</color>";

            bool hasResonance = lineMats[0] != null && lineMats[0].isMajor;
            string resTag = hasResonance ? " <color=#FBBF24>[★大骨架共鸣+20%]</color>" : "";

            if (liveStrokeNodeText != null)
            {
                liveStrokeNodeText.text = $"<color=#FFE050>【回路 {nodeCount} 节点】</color>{effTag}{resTag}";
            }

            // 4. 最新接入材料增量 (Delta)
            if (liveStrokeDeltaText != null)
            {
                liveStrokeDeltaText.text = $"<b>最新连入:</b> <color=#FFFFFF>[{latestMat.materialName}]</color> -> " +
                    $"<color=#C085FF>+💨 P:{latestMat.p}</color>  " +
                    $"<color=#FBD12E>+⚡ E:{latestMat.e}</color>  " +
                    $"<color=#4AE070>+🌿 G:{latestMat.g}</color>  " +
                    $"<color=#38BDF8>+💧 W:{latestMat.w}</color>";
            }

            // 5. 当前聚合累计能量池 (含阻抗衰减与共鸣加权)
            var stats = ShellCraftingRecipe.CalculateAccumulatedStats(lineMats);
            if (liveStrokeTotalText != null)
            {
                liveStrokeTotalText.text = $"<b>聚合能量:</b>  " +
                    $"<color=#C085FF>💨 P: {stats.p}</color>  |  " +
                    $"<color=#FBD12E>⚡ E: {stats.e}</color>  |  " +
                    $"<color=#4AE070>🌿 G: {stats.g}</color>  |  " +
                    $"<color=#38BDF8>💧 W: {stats.w}</color>";
            }

            // 6. 实时涌现产物预演 (>=2 节点立即推演蓝图或废料)
            if (liveStrokePredictText != null)
            {
                if (lineMats.Count < 2)
                {
                    liveStrokePredictText.text = "<color=#94A3B8>✦ 继续滑动连接相邻槽位，聚合回路以达成生体共振...</color>";
                }
                else
                {
                    CraftedProduct previewProd = ShellCraftingRecipe.Evaluate(lineMats);
                    if (previewProd != null)
                    {
                        string prodCol = previewProd.isAberrantScrap ? "#F472B6" : "#38BDF8";
                        string catTag = previewProd.isAberrantScrap ? "【活性杂交废料】" : $"【{previewProd.qualityTier}·{previewProd.category}】";
                        liveStrokePredictText.text = $"<b>✦ 预演构装:</b> <color={prodCol}><b>{previewProd.productName}</b></color> <color=#CBD5E1>{catTag}</color> | <color=#E2E8F0>{previewProd.tacticalEffect}</color>";
                    }
                }
            }

            // 7. 动态 HUD 背景光感（根据主导元素微调）
            if (liveStrokeBg != null)
            {
                Color elemCol = latestMat.ElementColor;
                liveStrokeBg.color = new Color(
                    Mathf.Lerp(0.04f, elemCol.r, 0.12f),
                    Mathf.Lerp(0.07f, elemCol.g, 0.12f),
                    Mathf.Lerp(0.14f, elemCol.b, 0.12f),
                    0.96f
                );
            }
        }

        #endregion

        #region Emergence Crafting Resolution (无废品涌现规则)

        private void ResolveCraftingFromStroke()
        {
            List<BiomechanicalMaterial> lineMats = new List<BiomechanicalMaterial>();
            pendingConsumedSlotIndices.Clear();

            foreach (var s in currentStrokeSlots)
            {
                if (s.currentMaterial != null && !lineMats.Contains(s.currentMaterial))
                {
                    lineMats.Add(s.currentMaterial);
                    if (s.currentMaterial.occupiedSlotIds != null && s.currentMaterial.occupiedSlotIds.Count > 0)
                    {
                        foreach (int id in s.currentMaterial.occupiedSlotIds)
                        {
                            if (!pendingConsumedSlotIndices.Contains(id))
                                pendingConsumedSlotIndices.Add(id);
                        }
                    }
                    else
                    {
                        if (!pendingConsumedSlotIndices.Contains(s.slotId))
                            pendingConsumedSlotIndices.Add(s.slotId);
                    }
                }
            }

            if (lineMats.Count < 2)
            {
                ResetStroke();
                return;
            }

            // 调用生体回路配方推演器
            pendingProduct = ShellCraftingRecipe.Evaluate(lineMats);
            if (pendingProduct != null)
            {
                ShowCraftResultModal(pendingProduct, lineMats);
            }
            else
            {
                ResetStroke();
            }
        }

        private void ShowCraftResultModal(CraftedProduct prod, List<BiomechanicalMaterial> consumedMats)
        {
            if (craftResultModal == null) return;

            craftResultModal.SetActive(true);

            if (craftResultName != null) craftResultName.text = prod.productName;
            if (craftResultQualityBadge != null)
            {
                craftResultQualityBadge.text = $"品质: {prod.qualityTier} ({prod.nodeCount} 节点)";
            }
            if (craftResultCategoryBadge != null)
            {
                if (prod.isAberrantScrap)
                {
                    craftResultCategoryBadge.text = "【活性生体杂交废料】";
                    craftResultCategoryBadge.color = new Color(0.95f, 0.45f, 0.85f);
                }
                else
                {
                    craftResultCategoryBadge.text = "【正规生体家具】";
                    craftResultCategoryBadge.color = new Color(0.35f, 0.95f, 0.65f);
                }
            }
            if (craftResultEffect != null) craftResultEffect.text = prod.tacticalEffect;
            if (craftResultFlavor != null) craftResultFlavor.text = prod.flavorDescription;

            if (craftResultStatsText != null)
            {
                string resonance = prod.hasMajorResonance ? " <color=#FFE050>[骨架共鸣+20%]</color>" : "";
                craftResultStatsText.text = $"<b>要素聚合：</b> <color=#C085FF>💨 P: {prod.totalP}</color>  <color=#FBD12E>⚡ E: {prod.totalE}</color>  <color=#4AE070>🌿 G: {prod.totalG}</color>  <color=#38BDF8>💧 W: {prod.totalW}</color>{resonance}";
            }

            if (craftResultIcon != null)
            {
                craftResultIcon.color = prod.themeColor;
                if (consumedMats.Count > 0 && consumedMats[0] != null)
                {
                    craftResultIcon.sprite = consumedMats[0].GetOrLoadSprite();
                }
            }

            // 渲染消耗的材料预览小图标
            if (consumedMaterialsContainer != null)
            {
                for (int i = consumedMaterialsContainer.childCount - 1; i >= 0; i--)
                {
                    var child = consumedMaterialsContainer.GetChild(i).gameObject;
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        DestroyImmediate(child);
                    else
                        Destroy(child);
#else
                    Destroy(child);
#endif
                }
                foreach (var m in consumedMats)
                {
                    GameObject chip = new GameObject("Chip");
                    chip.transform.SetParent(consumedMaterialsContainer, false);
                    Image chipImg = chip.AddComponent<Image>();
                    chipImg.sprite = m.GetOrLoadSprite();
                    chipImg.preserveAspect = true;
                    RectTransform crt = chip.GetComponent<RectTransform>();
                    crt.sizeDelta = new Vector2(40, 40);
                }
            }
        }

        public void ConfirmCraftProduct()
        {
            // 确认收纳成品：清空消耗掉的宫格（包括 2x2 与 1x1）！
            foreach (int slotIdx in pendingConsumedSlotIndices)
            {
                if (slotIdx >= 0 && slotIdx < slotItems.Length)
                {
                    slotItems[slotIdx] = null;
                }
                if (slotIdx >= 0 && slotIdx < slotViews.Count)
                {
                    slotViews[slotIdx].Set2x2RootOverlay(false);
                    slotViews[slotIdx].SetAs2x2Child(false, null);
                    slotViews[slotIdx].SetMaterial(null);
                }
            }

            if (craftResultModal != null) craftResultModal.SetActive(false);
            ResetStroke();
            UpdateBackpackFullStatus();
            RefreshBlueprintDock();

            Debug.Log($"<color=#4AFF70>[生体转化成功]</color> 获得：{pendingProduct?.productName}，已腾出 {pendingConsumedSlotIndices.Count} 个背壳空格！");
            pendingProduct = null;
            pendingConsumedSlotIndices.Clear();
        }

        public void CancelCraftProduct()
        {
            if (craftResultModal != null) craftResultModal.SetActive(false);
            ResetStroke();
            pendingProduct = null;
            pendingConsumedSlotIndices.Clear();
        }

        #endregion

        #region Floating Detail Popup

        public void ShowDetailPopup(BiomechanicalMaterial mat, bool isMajorSlot)
        {
            if (detailPopupPanel == null || mat == null) return;

            detailPopupPanel.SetActive(true);

            if (detailIcon != null)
            {
                detailIcon.sprite = mat.GetOrLoadSprite();
                detailIcon.preserveAspect = true;
            }
            if (detailName != null) detailName.text = mat.materialName;
            if (detailElementBadge != null)
            {
                detailElementBadge.text = $"元素: {mat.ElementName}";
                detailElementBadge.color = mat.ElementColor;
            }
            if (detailTypeBadge != null)
            {
                detailTypeBadge.text = isMajorSlot ? "【大格·骨架主核】" : "【小格·生体辅料】";
                detailTypeBadge.color = isMajorSlot ? new Color(0.98f, 0.82f, 0.18f) : new Color(0.6f, 0.8f, 1f);
            }
            if (detailDescription != null)
            {
                detailDescription.text = mat.shortDescription;
            }
            if (detailStatsText != null)
            {
                detailStatsText.text = $"<b>要素参数：</b> <color=#C085FF>💨 P: {mat.p}</color>  |  <color=#FBD12E>⚡ E: {mat.e}</color>  |  <color=#4AE070>🌿 G: {mat.g}</color>  |  <color=#38BDF8>💧 W: {mat.w}</color>";
            }
        }

        public void CloseDetailPopup()
        {
            if (detailPopupPanel != null) detailPopupPanel.SetActive(false);
        }

        #endregion

        #region 2x2 & 1x1 Placement and Blueprint Linkage

        public void RefreshSlotVisuals()
        {
            for (int i = 0; i < slotViews.Count; i++)
            {
                var sv = slotViews[i];
                var mat = (i < slotItems.Length) ? slotItems[i] : null;
                if (mat == null)
                {
                    sv.Set2x2RootOverlay(false);
                    sv.SetAs2x2Child(false, null);
                    sv.SetMaterial(null);
                }
                else if (mat.isMajor)
                {
                    if (mat.rootSlotId == i)
                    {
                        sv.SetMaterial(mat);
                        sv.Set2x2RootOverlay(true, mat);
                        sv.is2x2Child = false;
                    }
                    else
                    {
                        sv.Set2x2RootOverlay(false);
                        sv.SetAs2x2Child(true, mat);
                    }
                }
                else
                {
                    sv.Set2x2RootOverlay(false);
                    sv.SetAs2x2Child(false, null);
                    sv.SetMaterial(mat);
                }
            }
        }

        public bool TryPlace2x2Material(BiomechanicalMaterial mat)
        {
            if (activeConfig == null) return false;
            foreach (var slot in activeConfig.slots)
            {
                var ids = activeConfig.Get2x2SlotIds(slot.gridCoord.x, slot.gridCoord.y);
                if (ids != null && ids.Count == 4)
                {
                    bool allFree = true;
                    foreach (int id in ids)
                    {
                        if (id >= slotItems.Length || slotItems[id] != null) { allFree = false; break; }
                    }
                    if (allFree)
                    {
                        Place2x2MaterialAt(mat, ids);
                        return true;
                    }
                }
            }
            return false;
        }

        public void Place2x2MaterialAt(BiomechanicalMaterial mat, List<int> ids)
        {
            mat.rootSlotId = ids[0];
            mat.occupiedSlotIds = new List<int>(ids);

            foreach (int id in ids)
            {
                if (id < slotItems.Length) slotItems[id] = mat;
            }

            int rootId = ids[0];
            if (rootId < slotViews.Count)
            {
                var rootSlot = slotViews[rootId];
                rootSlot.SetMaterial(mat);
                rootSlot.Set2x2RootOverlay(true, mat);
                rootSlot.is2x2Child = false;
            }

            for (int i = 1; i < ids.Count; i++)
            {
                int childId = ids[i];
                if (childId < slotViews.Count)
                {
                    var childSlot = slotViews[childId];
                    childSlot.Set2x2RootOverlay(false);
                    childSlot.SetAs2x2Child(true, mat);
                }
            }
        }

        public bool TryPlace1x1Material(BiomechanicalMaterial mat)
        {
            if (activeConfig == null) return false;
            for (int i = 0; i < activeConfig.slots.Count; i++)
            {
                if (slotItems[i] == null)
                {
                    Place1x1MaterialAt(mat, i);
                    return true;
                }
            }
            return false;
        }

        public void Place1x1MaterialAt(BiomechanicalMaterial mat, int slotId)
        {
            mat.rootSlotId = slotId;
            mat.occupiedSlotIds = new List<int> { slotId };
            if (slotId < slotItems.Length) slotItems[slotId] = mat;
            if (slotId < slotViews.Count)
            {
                var view = slotViews[slotId];
                view.Set2x2RootOverlay(false);
                view.is2x2Child = false;
                view.SetMaterial(mat);
            }
        }

        public List<BiomechanicalMaterial> GetDistinctBackpackMaterials()
        {
            List<BiomechanicalMaterial> list = new List<BiomechanicalMaterial>();
            HashSet<BiomechanicalMaterial> seen = new HashSet<BiomechanicalMaterial>();
            for (int i = 0; i < slotItems.Length; i++)
            {
                var m = slotItems[i];
                if (m != null && !seen.Contains(m))
                {
                    seen.Add(m);
                    list.Add(m);
                }
            }
            return list;
        }

        public void RefreshBlueprintDock()
        {
            if (blueprintDock != null)
            {
                blueprintDock.RefreshBlueprints(GetDistinctBackpackMaterials());
            }
        }

        public void HighlightMaterialsForBlueprint(BiomechanicalBlueprint bp)
        {
            if (bp == null)
            {
                foreach (var sv in slotViews)
                {
                    sv.SetBlueprintTarget(false);
                    sv.SetDimmed(false);
                }
                return;
            }

            HashSet<string> targetIds = new HashSet<string>();
            if (bp.requiredCoreIds != null)
            {
                foreach (var cid in bp.requiredCoreIds) targetIds.Add(cid);
            }
            if (bp.requiredAnyIds != null)
            {
                foreach (var aid in bp.requiredAnyIds) targetIds.Add(aid);
            }

            Color highlightColor = bp.ThemeColor;

            foreach (var sv in slotViews)
            {
                if (sv.currentMaterial != null && targetIds.Contains(sv.currentMaterial.id))
                {
                    sv.SetBlueprintTarget(true, highlightColor);
                    if (sv.is2x2Root && sv.overlay2x2Border != null)
                    {
                        sv.overlay2x2Border.color = highlightColor;
                    }
                }
                else
                {
                    sv.SetBlueprintTarget(false);
                    sv.SetDimmed(true);
                }
            }
        }

        #endregion

        #region Backpack Full Warning & Seed Data

        private void UpdateBackpackFullStatus()
        {
            if (activeConfig == null) return;

            int occupied = 0;
            for (int i = 0; i < activeConfig.slots.Count; i++)
            {
                if (slotItems[i] != null) occupied++;
            }

            bool isFull = (occupied >= activeConfig.slots.Count);

            if (fullWarningBanner != null)
            {
                fullWarningBanner.SetActive(isFull);
            }
            if (fullWarningText != null && isFull)
            {
                fullWarningText.text = "⚠️ 背壳生体负荷已满！请划线转化构件，腾出背壳空隙";
            }
        }

        private void SeedInitialMaterials()
        {
            // 给当前海螺壳预置特色材料：含 2x2 骨架核心与 1x1 生体辅料，展示配方可即刻合成状态
            var allMats = BiomechanicalMaterialDatabase.GetAllMaterials();
            if (allMats.Count == 0 || activeConfig == null) return;

            // 1. 2x2 电系核心：砗磲绝缘外壳 + 1x1 电系辅料：电鳗放电肌束 (合成电鳗发电机)
            var matClam = allMats.Find(m => m.id == "mat_elec_clam_shell");
            var matMuscle = allMats.Find(m => m.id == "mat_elec_muscle_bundle");

            // 2. 2x2 气系核心：气虫储气泡囊 + 1x1 气系辅料：微压呼吸浮囊 (合成悬浮软床)
            var matGasBubble = allMats.Find(m => m.id == "mat_gas_bubble_sac");
            var matFloatBladder = allMats.Find(m => m.id == "mat_gas_float_bladder");

            // 3. 1x1 木系辅料：珊瑚齿轮
            var matRootGear = allMats.Find(m => m.id == "mat_wood_root_gear");

            var clam2x2Ids = activeConfig.Get2x2SlotIds(1, 0); // (1,0),(2,0),(1,1),(2,1)
            if (matClam != null && clam2x2Ids != null)
            {
                Place2x2MaterialAt(matClam.Clone(), clam2x2Ids);
            }

            var muscleSlot = activeConfig.GetSlotAt(3, 0);
            if (matMuscle != null && muscleSlot != null)
            {
                Place1x1MaterialAt(matMuscle.Clone(), muscleSlot.slotId);
            }

            var gas2x2Ids = activeConfig.Get2x2SlotIds(1, 2); // (1,2),(2,2),(1,3),(2,3)
            if (matGasBubble != null && gas2x2Ids != null)
            {
                Place2x2MaterialAt(matGasBubble.Clone(), gas2x2Ids);
            }

            var floatSlot = activeConfig.GetSlotAt(3, 2);
            if (matFloatBladder != null && floatSlot != null)
            {
                Place1x1MaterialAt(matFloatBladder.Clone(), floatSlot.slotId);
            }

            var gearSlot = activeConfig.GetSlotAt(4, 1);
            if (matRootGear != null && gearSlot != null)
            {
                Place1x1MaterialAt(matRootGear.Clone(), gearSlot.slotId);
            }

            UpdateBackpackFullStatus();
            RefreshBlueprintDock();
        }

        #endregion

        #region Debug & Toolbar Actions

        public void AddRandomMaterial()
        {
            var allMats = BiomechanicalMaterialDatabase.GetAllMaterials();
            if (allMats.Count == 0) return;

            var randomMat = allMats[UnityEngine.Random.Range(0, allMats.Count)].Clone();
            bool placed = false;
            if (randomMat.isMajor)
            {
                placed = TryPlace2x2Material(randomMat);
                if (!placed)
                {
                    var auxMats = allMats.FindAll(m => !m.isMajor);
                    if (auxMats.Count > 0)
                    {
                        randomMat = auxMats[UnityEngine.Random.Range(0, auxMats.Count)].Clone();
                        placed = TryPlace1x1Material(randomMat);
                    }
                }
            }
            else
            {
                placed = TryPlace1x1Material(randomMat);
            }

            if (placed)
            {
                UpdateBackpackFullStatus();
                RefreshBlueprintDock();
                Debug.Log($"<color=#70D2FF>[拾取材料]</color> {randomMat.materialName} ({randomMat.SizeLabel})");
            }
            else
            {
                Debug.Log("<color=#FF9070>[提示]</color> 背壳没有合适空位了，请先划线合成！");
            }
        }

        public void FillAllSlotsRandomly()
        {
            var allMats = BiomechanicalMaterialDatabase.GetAllMaterials();
            if (allMats.Count == 0 || activeConfig == null) return;

            var majorMats = allMats.FindAll(m => m.isMajor);
            var minorMats = allMats.FindAll(m => !m.isMajor);

            // 优先填入 2~3 个 2x2 核心
            for (int t = 0; t < 3; t++)
            {
                if (majorMats.Count > 0)
                {
                    var m = majorMats[UnityEngine.Random.Range(0, majorMats.Count)].Clone();
                    TryPlace2x2Material(m);
                }
            }

            // 剩下空位填满 1x1 辅料
            for (int i = 0; i < activeConfig.slots.Count; i++)
            {
                if (slotItems[i] == null && minorMats.Count > 0)
                {
                    var m = minorMats[UnityEngine.Random.Range(0, minorMats.Count)].Clone();
                    Place1x1MaterialAt(m, i);
                }
            }

            UpdateBackpackFullStatus();
            RefreshBlueprintDock();
            Debug.Log("<color=#FFE850>[背壳填满]</color> 已经随机填满所有大小槽位！");
        }

        public void ClearAllSlots()
        {
            for (int i = 0; i < slotItems.Length; i++)
            {
                slotItems[i] = null;
            }
            foreach (var sv in slotViews)
            {
                sv.Set2x2RootOverlay(false);
                sv.SetAs2x2Child(false, null);
                sv.SetMaterial(null);
            }
            UpdateBackpackFullStatus();
            ResetStroke();
            RefreshBlueprintDock();
            Debug.Log("<color=#999999>[清空背壳]</color> 已清空所有槽位。");
        }

        public void DemoStrokeCrafting()
        {
            // 自动寻找一条有效连线做示范
            if (slotViews.Count >= 2 && slotViews[0].currentMaterial != null && slotViews[1].currentMaterial != null)
            {
                currentStrokeSlots.Clear();
                currentStrokeSlots.Add(slotViews[0]);
                currentStrokeSlots.Add(slotViews[1]);
                UpdateStrokeVisuals();
                ResolveCraftingFromStroke();
            }
        }

        public void InitUIEvents()
        {
            if (modeLineBtn != null) { modeLineBtn.onClick.RemoveAllListeners(); modeLineBtn.onClick.AddListener(() => SetMode(InteractionMode.LineCrafting)); }
            if (modeReorderBtn != null) { modeReorderBtn.onClick.RemoveAllListeners(); modeReorderBtn.onClick.AddListener(() => SetMode(InteractionMode.Reorder)); }

            if (switchConchBtn != null) { switchConchBtn.onClick.RemoveAllListeners(); switchConchBtn.onClick.AddListener(() => SwitchShell(ShellType.ConchShell)); }
            if (switchClamBtn != null) { switchClamBtn.onClick.RemoveAllListeners(); switchClamBtn.onClick.AddListener(() => SwitchShell(ShellType.ClamShell)); }
            if (switchCanBtn != null) { switchCanBtn.onClick.RemoveAllListeners(); switchCanBtn.onClick.AddListener(() => SwitchShell(ShellType.RustedCanShell)); }

            if (btnAddRandom != null) { btnAddRandom.onClick.RemoveAllListeners(); btnAddRandom.onClick.AddListener(AddRandomMaterial); }
            if (btnFillAll != null) { btnFillAll.onClick.RemoveAllListeners(); btnFillAll.onClick.AddListener(FillAllSlotsRandomly); }
            if (btnClearAll != null) { btnClearAll.onClick.RemoveAllListeners(); btnClearAll.onClick.AddListener(ClearAllSlots); }
            if (btnDemoStroke != null) { btnDemoStroke.onClick.RemoveAllListeners(); btnDemoStroke.onClick.AddListener(DemoStrokeCrafting); }

            if (detailCloseBtn != null) { detailCloseBtn.onClick.RemoveAllListeners(); detailCloseBtn.onClick.AddListener(CloseDetailPopup); }
            if (craftConfirmBtn != null) { craftConfirmBtn.onClick.RemoveAllListeners(); craftConfirmBtn.onClick.AddListener(ConfirmCraftProduct); }
            if (craftCancelBtn != null) { craftCancelBtn.onClick.RemoveAllListeners(); craftCancelBtn.onClick.AddListener(CancelCraftProduct); }
            if (btnBlueprintCodex != null) { btnBlueprintCodex.onClick.RemoveAllListeners(); btnBlueprintCodex.onClick.AddListener(() => { if (codexModal != null) codexModal.Toggle(); }); }
            if (blueprintDock != null) { blueprintDock.onBlueprintSelected = HighlightMaterialsForBlueprint; }

            SetMode(InteractionMode.LineCrafting);
        }

        private void UpdateShellButtonHighlights()
        {
            Color activeColor = new Color(0.98f, 0.82f, 0.18f, 0.9f);
            Color normalColor = new Color(0.2f, 0.28f, 0.38f, 0.7f);

            if (switchConchBtn != null) switchConchBtn.image.color = currentShellType == ShellType.ConchShell ? activeColor : normalColor;
            if (switchClamBtn != null) switchClamBtn.image.color = currentShellType == ShellType.ClamShell ? activeColor : normalColor;
            if (switchCanBtn != null) switchCanBtn.image.color = currentShellType == ShellType.RustedCanShell ? activeColor : normalColor;
        }

        #endregion
    }
}
