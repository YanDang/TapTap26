using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using BiomechanicalCrafting;

public static class ShellInventorySceneBuilder
{
    private static Font defaultFont;

    [MenuItem("BiomechanicalCrafting/Build Mix Backpack Scene")]
    public static void BuildMixScene()
    {
        // 1. 确保打开 Mix 场景
        string scenePath = "Assets/Scenes/Mix.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 2. 配置主摄像机
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camGO = new GameObject("Main Camera");
            mainCam = camGO.AddComponent<Camera>();
            camGO.tag = "MainCamera";
        }
        mainCam.clearFlags = CameraClearFlags.SolidColor;
        mainCam.backgroundColor = new Color(0.05f, 0.07f, 0.1f, 1f); // 极深生体夜色
        mainCam.orthographic = true;
        mainCam.orthographicSize = 5f;
        mainCam.transform.position = new Vector3(0, 0, -10);

        // 3. 配置 EventSystem
        EventSystem es = Object.FindObjectOfType<EventSystem>();
        if (es == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            es = esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }

        // 4. 清理旧的 Canvas / 界面
        Canvas oldCanvas = Object.FindObjectOfType<Canvas>();
        if (oldCanvas != null)
        {
            Object.DestroyImmediate(oldCanvas.gameObject);
        }

        // 5. 创建主 Canvas
        GameObject canvasGO = new GameObject("BackpackCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // 6. 全屏背景底板
        GameObject bgGO = CreateUIObject("DarkBackground", canvasGO.transform);
        StretchFull(bgGO.GetComponent<RectTransform>());
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.04f, 0.06f, 0.09f, 1f);
        bgImg.raycastTarget = true;
        Button bgBtn = bgGO.AddComponent<Button>();
        bgBtn.transition = Selectable.Transition.None;

        // 7. 挂载主控制器
        ShellInventoryController controller = canvasGO.AddComponent<ShellInventoryController>();
        bgBtn.onClick.AddListener(() => {
            if (controller != null && controller.blueprintDock != null)
            {
                controller.blueprintDock.ClearSelection();
            }
        });

        // =========================================================================
        // 8. 顶部导航栏 (TopHeader)
        // =========================================================================
        GameObject headerGO = CreateUIObject("TopHeader", canvasGO.transform);
        RectTransform headerRT = headerGO.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0, 1);
        headerRT.anchorMax = new Vector2(1, 1);
        headerRT.pivot = new Vector2(0.5f, 1);
        headerRT.anchoredPosition = new Vector2(0, 0);
        headerRT.sizeDelta = new Vector2(0, 90);

        Image headerBg = headerGO.AddComponent<Image>();
        headerBg.color = new Color(0.08f, 0.11f, 0.16f, 0.95f);

        // 主标题
        GameObject titleGO = CreateText(headerGO.transform, "TitleText", "生体机械背壳工坊", 26, FontStyle.Bold, new Color(0.98f, 0.82f, 0.18f), TextAnchor.MiddleLeft);
        RectTransform titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 0.5f);
        titleRT.anchorMax = new Vector2(0, 0.5f);
        titleRT.pivot = new Vector2(0, 0.5f);
        titleRT.anchoredPosition = new Vector2(30, 0);
        titleRT.sizeDelta = new Vector2(280, 50);

        // 📖 蓝图图鉴按钮 (顶部导航)
        Button btnCodex = CreateButton(headerGO.transform, "BtnBlueprintCodex", "📖 蓝图图鉴", new Vector2(130, 48), new Color(0.32f, 0.44f, 0.72f, 0.95f), Color.white, 14);
        RectTransform codexRT = btnCodex.GetComponent<RectTransform>();
        codexRT.anchorMin = new Vector2(0, 0.5f);
        codexRT.anchorMax = new Vector2(0, 0.5f);
        codexRT.pivot = new Vector2(0, 0.5f);
        codexRT.anchoredPosition = new Vector2(320, 0);
        controller.btnBlueprintCodex = btnCodex;

        // 模式切换按钮组 (中间偏右)
        GameObject modeGroupGO = CreateUIObject("ModeButtons", headerGO.transform);
        RectTransform modeGroupRT = modeGroupGO.GetComponent<RectTransform>();
        modeGroupRT.anchorMin = new Vector2(0.5f, 0.5f);
        modeGroupRT.anchorMax = new Vector2(0.5f, 0.5f);
        modeGroupRT.anchoredPosition = new Vector2(10, 0);
        modeGroupRT.sizeDelta = new Vector2(360, 56);

        Button modeLineBtn = CreateButton(modeGroupGO.transform, "ModeLineBtn", "⚡ 连线转化模式", new Vector2(170, 50), new Color(0.98f, 0.82f, 0.18f, 0.9f), Color.black, 16);
        modeLineBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-90, 0);
        controller.modeLineBtn = modeLineBtn;
        controller.modeLineBtnText = modeLineBtn.GetComponentInChildren<Text>();

        Button modeReorderBtn = CreateButton(modeGroupGO.transform, "ModeReorderBtn", "✋ 拖拽重排模式", new Vector2(170, 50), new Color(0.2f, 0.3f, 0.4f, 0.7f), Color.white, 16);
        modeReorderBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(90, 0);
        controller.modeReorderBtn = modeReorderBtn;
        controller.modeReorderBtnText = modeReorderBtn.GetComponentInChildren<Text>();

        // 壳类型切换按钮组 (最右侧)
        GameObject shellGroupGO = CreateUIObject("ShellSwitchers", headerGO.transform);
        RectTransform shellGroupRT = shellGroupGO.GetComponent<RectTransform>();
        shellGroupRT.anchorMin = new Vector2(1, 0.5f);
        shellGroupRT.anchorMax = new Vector2(1, 0.5f);
        shellGroupRT.pivot = new Vector2(1, 0.5f);
        shellGroupRT.anchoredPosition = new Vector2(-30, 0);
        shellGroupRT.sizeDelta = new Vector2(480, 56);

        Button switchConch = CreateButton(shellGroupGO.transform, "SwitchConch", "🐚 海螺壳 (16格)", new Vector2(150, 46), new Color(0.98f, 0.82f, 0.18f, 0.9f), Color.black, 14);
        switchConch.GetComponent<RectTransform>().anchoredPosition = new Vector2(-330, 0);
        controller.switchConchBtn = switchConch;

        Button switchClam = CreateButton(shellGroupGO.transform, "SwitchClam", "🦪 扇贝壳 (18格)", new Vector2(150, 46), new Color(0.2f, 0.3f, 0.4f, 0.7f), Color.white, 14);
        switchClam.GetComponent<RectTransform>().anchoredPosition = new Vector2(-165, 0);
        controller.switchClamBtn = switchClam;

        Button switchCan = CreateButton(shellGroupGO.transform, "SwitchCan", "🥫 铁皮罐 (20格)", new Vector2(150, 46), new Color(0.2f, 0.3f, 0.4f, 0.7f), Color.white, 14);
        switchCan.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);
        controller.switchCanBtn = switchCan;

        // =========================================================================
        // 9. 背包装满警告条 (BackpackFullBanner)
        // =========================================================================
        GameObject warningGO = CreateUIObject("FullWarningBanner", canvasGO.transform);
        RectTransform warnRT = warningGO.GetComponent<RectTransform>();
        warnRT.anchorMin = new Vector2(0.5f, 1f);
        warnRT.anchorMax = new Vector2(0.5f, 1f);
        warnRT.pivot = new Vector2(0.5f, 1f);
        warnRT.anchoredPosition = new Vector2(0, -96);
        warnRT.sizeDelta = new Vector2(720, 42);

        Image warnBg = warningGO.AddComponent<Image>();
        warnBg.color = new Color(0.85f, 0.32f, 0.18f, 0.92f);

        GameObject warnTxtGO = CreateText(warningGO.transform, "WarnText", "⚠️ 背壳生体负荷已满！请划线转化构件，腾出背壳空隙", 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        StretchFull(warnTxtGO.GetComponent<RectTransform>());
        controller.fullWarningBanner = warningGO;
        controller.fullWarningText = warnTxtGO.GetComponent<Text>();
        warningGO.SetActive(false);

        // =========================================================================
        // 9.5 左侧常驻生体蓝图侧边栏 (BlueprintDockPanel)
        // =========================================================================
        GameObject dockGO = CreateUIObject("BlueprintDockPanel", canvasGO.transform);
        RectTransform dockRT = dockGO.GetComponent<RectTransform>();
        dockRT.anchorMin = new Vector2(0, 0.5f);
        dockRT.anchorMax = new Vector2(0, 0.5f);
        dockRT.pivot = new Vector2(0, 0.5f);
        dockRT.anchoredPosition = new Vector2(24, -3);
        dockRT.sizeDelta = new Vector2(410, 880);

        Image dockBg = dockGO.AddComponent<Image>();
        dockBg.color = new Color(0.06f, 0.09f, 0.14f, 0.96f);
        dockBg.raycastTarget = true;
        Button dockBgBtn = dockGO.AddComponent<Button>();
        dockBgBtn.transition = Selectable.Transition.None;
        dockBgBtn.onClick.AddListener(() => {
            if (controller != null && controller.blueprintDock != null)
            {
                controller.blueprintDock.ClearSelection();
            }
        });

        // 侧边栏头部
        GameObject dHeaderGO = CreateUIObject("DockHeader", dockGO.transform);
        RectTransform dhRT = dHeaderGO.GetComponent<RectTransform>();
        dhRT.anchorMin = new Vector2(0, 1);
        dhRT.anchorMax = new Vector2(1, 1);
        dhRT.pivot = new Vector2(0.5f, 1);
        dhRT.anchoredPosition = new Vector2(0, -8);
        dhRT.sizeDelta = new Vector2(-20, 72);

        GameObject dTitleGO = CreateText(dHeaderGO.transform, "Title", "⚡ 生体战术配方蓝图", 18, FontStyle.Bold, new Color(0.98f, 0.82f, 0.18f), TextAnchor.MiddleLeft);
        RectTransform dtRT = dTitleGO.GetComponent<RectTransform>();
        dtRT.anchorMin = new Vector2(0, 1);
        dtRT.anchorMax = new Vector2(1, 1);
        dtRT.pivot = new Vector2(0, 1);
        dtRT.anchoredPosition = new Vector2(8, 0);
        dtRT.sizeDelta = new Vector2(-16, 26);

        GameObject dStatusGO = CreateText(dHeaderGO.transform, "Status", "可即刻合成: <color=#34D399><b>2</b></color> / 12", 13, FontStyle.Bold, new Color(0.8f, 0.9f, 1f), TextAnchor.MiddleLeft);
        RectTransform dstatRT = dStatusGO.GetComponent<RectTransform>();
        dstatRT.anchorMin = new Vector2(0, 1);
        dstatRT.anchorMax = new Vector2(1, 1);
        dstatRT.pivot = new Vector2(0, 1);
        dstatRT.anchoredPosition = new Vector2(8, -26);
        dstatRT.sizeDelta = new Vector2(-16, 22);

        GameObject dHintGO = CreateText(dHeaderGO.transform, "Hint", "点击蓝图高亮背壳材料 ✦ 再次点击取消", 11, FontStyle.Normal, new Color(0.6f, 0.72f, 0.85f), TextAnchor.MiddleLeft);
        RectTransform dhiRT = dHintGO.GetComponent<RectTransform>();
        dhiRT.anchorMin = new Vector2(0, 1);
        dhiRT.anchorMax = new Vector2(1, 1);
        dhiRT.pivot = new Vector2(0, 1);
        dhiRT.anchoredPosition = new Vector2(8, -48);
        dhiRT.sizeDelta = new Vector2(-16, 20);

        // 蓝图卡片滚动容器
        GameObject dScrollGO = CreateUIObject("CardsScrollArea", dockGO.transform);
        RectTransform dscRT = dScrollGO.GetComponent<RectTransform>();
        dscRT.anchorMin = new Vector2(0, 0);
        dscRT.anchorMax = new Vector2(1, 1);
        dscRT.pivot = new Vector2(0.5f, 0.5f);
        dscRT.offsetMin = new Vector2(8, 12);
        dscRT.offsetMax = new Vector2(-8, -84);

        ScrollRect dsr = dScrollGO.AddComponent<ScrollRect>();
        dsr.horizontal = false;
        dsr.vertical = true;
        dsr.scrollSensitivity = 30f;
        dsr.movementType = ScrollRect.MovementType.Clamped;

        GameObject dViewport = CreateUIObject("Viewport", dScrollGO.transform);
        StretchFull(dViewport.GetComponent<RectTransform>());
        Image vImg = dViewport.AddComponent<Image>();
        vImg.color = new Color(0, 0, 0, 0.01f);
        dViewport.AddComponent<RectMask2D>();
        dsr.viewport = dViewport.GetComponent<RectTransform>();

        GameObject dContent = CreateUIObject("Content", dViewport.transform);
        RectTransform dcontRT = dContent.GetComponent<RectTransform>();
        dcontRT.anchorMin = new Vector2(0, 1);
        dcontRT.anchorMax = new Vector2(1, 1);
        dcontRT.pivot = new Vector2(0.5f, 1);
        dcontRT.anchoredPosition = Vector2.zero;
        dcontRT.sizeDelta = new Vector2(0, 300);

        VerticalLayoutGroup dvlg = dContent.AddComponent<VerticalLayoutGroup>();
        dvlg.padding = new RectOffset(4, 4, 4, 4);
        dvlg.spacing = 8;
        dvlg.childForceExpandWidth = true;
        dvlg.childForceExpandHeight = false;
        dvlg.childControlWidth = true;
        dvlg.childControlHeight = false;

        ContentSizeFitter dcsf = dContent.AddComponent<ContentSizeFitter>();
        dcsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        dsr.content = dcontRT;

        BlueprintSidebarDock dockComp = dockGO.AddComponent<BlueprintSidebarDock>();
        dockComp.cardsContainer = dcontRT;
        dockComp.titleText = dTitleGO.GetComponent<Text>();
        dockComp.statusText = dStatusGO.GetComponent<Text>();
        dockComp.hintText = dHintGO.GetComponent<Text>();
        controller.blueprintDock = dockComp;

        // =========================================================================
        // 10. 背壳展示主面板 (ShellCenterPanel)
        // =========================================================================
        GameObject shellPlateGO = CreateUIObject("ShellPlate", canvasGO.transform);
        RectTransform plateRT = shellPlateGO.GetComponent<RectTransform>();
        plateRT.anchorMin = new Vector2(0.5f, 0.5f);
        plateRT.anchorMax = new Vector2(0.5f, 0.5f);
        plateRT.anchoredPosition = new Vector2(235, -3);
        plateRT.sizeDelta = new Vector2(950, 720);

        Image plateImg = shellPlateGO.AddComponent<Image>();
        plateImg.color = new Color(0.07f, 0.1f, 0.15f, 0.85f);
        plateImg.raycastTarget = true;
        controller.shellPlateImage = plateImg;

        Button plateBtn = shellPlateGO.AddComponent<Button>();
        plateBtn.transition = Selectable.Transition.None;
        plateBtn.onClick.AddListener(() => {
            if (controller != null && controller.blueprintDock != null)
            {
                controller.blueprintDock.ClearSelection();
            }
        });

        // 背壳名称与韵味简述
        GameObject sTitleGO = CreateText(shellPlateGO.transform, "ShellTitle", "初生海螺壳", 22, FontStyle.Bold, new Color(0.85f, 0.92f, 0.98f), TextAnchor.MiddleCenter);
        RectTransform stRT = sTitleGO.GetComponent<RectTransform>();
        stRT.anchorMin = new Vector2(0.5f, 1f);
        stRT.anchorMax = new Vector2(0.5f, 1f);
        stRT.anchoredPosition = new Vector2(0, -28);
        stRT.sizeDelta = new Vector2(600, 32);
        controller.shellTitleText = sTitleGO.GetComponent<Text>();

        GameObject sFlavorGO = CreateText(shellPlateGO.transform, "ShellFlavor", "随风微鸣的螺旋海螺壳，网格错落有致，最适于灵动的一笔画长链串联。", 14, FontStyle.Italic, new Color(0.55f, 0.7f, 0.82f), TextAnchor.MiddleCenter);
        RectTransform sfRT = sFlavorGO.GetComponent<RectTransform>();
        sfRT.anchorMin = new Vector2(0.5f, 1f);
        sfRT.anchorMax = new Vector2(0.5f, 1f);
        sfRT.anchoredPosition = new Vector2(0, -56);
        sfRT.sizeDelta = new Vector2(800, 26);
        controller.shellFlavorText = sFlavorGO.GetComponent<Text>();

        // 实时连线聚能推演条 (Live Stroke Synthesis HUD)
        GameObject liveHudGO = CreateUIObject("LiveStrokeHUD", shellPlateGO.transform);
        RectTransform hudRT = liveHudGO.GetComponent<RectTransform>();
        hudRT.anchorMin = new Vector2(0.5f, 1f);
        hudRT.anchorMax = new Vector2(0.5f, 1f);
        hudRT.pivot = new Vector2(0.5f, 1f);
        hudRT.anchoredPosition = new Vector2(0, -82);
        hudRT.sizeDelta = new Vector2(880, 58);

        Image hudBg = liveHudGO.AddComponent<Image>();
        hudBg.color = new Color(0.04f, 0.07f, 0.12f, 0.94f);
        controller.liveStrokeBg = hudBg;
        controller.liveStrokeHUD = liveHudGO;

        // 行1：节点状态 (左) + 最新接入增量 (右)
        GameObject nodeTxtGO = CreateText(liveHudGO.transform, "LiveNodeText", "<color=#94A3B8>[生体回路待机]</color>", 13, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
        RectTransform liveNodeRT = nodeTxtGO.GetComponent<RectTransform>();
        liveNodeRT.anchorMin = new Vector2(0, 0.5f);
        liveNodeRT.anchorMax = new Vector2(0, 0.5f);
        liveNodeRT.pivot = new Vector2(0, 0.5f);
        liveNodeRT.anchoredPosition = new Vector2(16, 13);
        liveNodeRT.sizeDelta = new Vector2(280, 24);
        controller.liveStrokeNodeText = nodeTxtGO.GetComponent<Text>();

        GameObject deltaTxtGO = CreateText(liveHudGO.transform, "LiveDeltaText", "<color=#94A3B8>按住滑动连接相邻材料，实时聚合推演 P/E/G/W 动力学与战术构装</color>", 13, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
        RectTransform liveDeltaRT = deltaTxtGO.GetComponent<RectTransform>();
        liveDeltaRT.anchorMin = new Vector2(0, 0.5f);
        liveDeltaRT.anchorMax = new Vector2(1, 0.5f);
        liveDeltaRT.pivot = new Vector2(0, 0.5f);
        liveDeltaRT.anchoredPosition = new Vector2(300, 13);
        liveDeltaRT.sizeDelta = new Vector2(-316, 24);
        controller.liveStrokeDeltaText = deltaTxtGO.GetComponent<Text>();

        // 行2：聚合总能量池 (左) + 预演产物预测 (右)
        GameObject totalTxtGO = CreateText(liveHudGO.transform, "LiveTotalText", "<b>聚合能量:</b> <color=#64748B>💨 P: 0 | ⚡ E: 0 | 🌿 G: 0 | 💧 W: 0</color>", 13, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
        RectTransform liveTotalRT = totalTxtGO.GetComponent<RectTransform>();
        liveTotalRT.anchorMin = new Vector2(0, 0.5f);
        liveTotalRT.anchorMax = new Vector2(0, 0.5f);
        liveTotalRT.pivot = new Vector2(0, 0.5f);
        liveTotalRT.anchoredPosition = new Vector2(16, -13);
        liveTotalRT.sizeDelta = new Vector2(330, 24);
        controller.liveStrokeTotalText = totalTxtGO.GetComponent<Text>();

        GameObject predictTxtGO = CreateText(liveHudGO.transform, "LivePredictText", "<color=#64748B>✦ 预演构装: 等待回路连线...</color>", 13, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
        RectTransform livePredictRT = predictTxtGO.GetComponent<RectTransform>();
        livePredictRT.anchorMin = new Vector2(0, 0.5f);
        livePredictRT.anchorMax = new Vector2(1, 0.5f);
        livePredictRT.pivot = new Vector2(0, 0.5f);
        livePredictRT.anchoredPosition = new Vector2(350, -13);
        livePredictRT.sizeDelta = new Vector2(-366, 24);
        controller.liveStrokePredictText = predictTxtGO.GetComponent<Text>();

        // 槽位挂载容器 (先创建网格底板)
        GameObject slotsContainerGO = CreateUIObject("SlotsContainer", shellPlateGO.transform);
        RectTransform scRT = slotsContainerGO.GetComponent<RectTransform>();
        scRT.anchorMin = new Vector2(0.5f, 0.5f);
        scRT.anchorMax = new Vector2(0.5f, 0.5f);
        scRT.anchoredPosition = new Vector2(0, -30);
        scRT.sizeDelta = Vector2.zero;
        controller.shellContainer = scRT;

        // 连线渲染器对象 (后创建，确保连线能量电缆在所有网格底板上方清晰渲染)
        GameObject lineRendererGO = CreateUIObject("StrokeLineRenderer", shellPlateGO.transform);
        RectTransform lrRT = lineRendererGO.GetComponent<RectTransform>();
        lrRT.anchorMin = new Vector2(0.5f, 0.5f);
        lrRT.anchorMax = new Vector2(0.5f, 0.5f);
        lrRT.anchoredPosition = new Vector2(0, -30);
        lrRT.sizeDelta = Vector2.zero;
        UILineRenderer lineRend = lineRendererGO.AddComponent<UILineRenderer>();
        lineRend.thickness = 12f;
        lineRend.raycastTarget = false;
        controller.strokeLineRenderer = lineRend;

        // =========================================================================
        // 11. 底部操作工具栏 (BottomToolbar)
        // =========================================================================
        GameObject bottomBarGO = CreateUIObject("BottomToolbar", canvasGO.transform);
        RectTransform botRT = bottomBarGO.GetComponent<RectTransform>();
        botRT.anchorMin = new Vector2(0, 0);
        botRT.anchorMax = new Vector2(1, 0);
        botRT.pivot = new Vector2(0.5f, 0);
        botRT.anchoredPosition = new Vector2(0, 0);
        botRT.sizeDelta = new Vector2(0, 84);

        Image botBg = bottomBarGO.AddComponent<Image>();
        botBg.color = new Color(0.06f, 0.09f, 0.13f, 0.95f);

        GameObject actGroupGO = CreateUIObject("ActionButtons", bottomBarGO.transform);
        RectTransform actRT = actGroupGO.GetComponent<RectTransform>();
        actRT.anchorMin = new Vector2(0, 0.5f);
        actRT.anchorMax = new Vector2(0, 0.5f);
        actRT.pivot = new Vector2(0, 0.5f);
        actRT.anchoredPosition = new Vector2(470, 0);
        actRT.sizeDelta = new Vector2(660, 52);

        Button btnAdd = CreateButton(actGroupGO.transform, "BtnAddRandom", "🎲 拾取随机材料", new Vector2(150, 48), new Color(0.18f, 0.35f, 0.5f, 0.9f), Color.white, 14);
        btnAdd.GetComponent<RectTransform>().anchoredPosition = new Vector2(75, 0);
        controller.btnAddRandom = btnAdd;

        Button btnFill = CreateButton(actGroupGO.transform, "BtnFillAll", "🎒 填满背壳", new Vector2(140, 48), new Color(0.25f, 0.45f, 0.35f, 0.9f), Color.white, 14);
        btnFill.GetComponent<RectTransform>().anchoredPosition = new Vector2(230, 0);
        controller.btnFillAll = btnFill;

        Button btnClear = CreateButton(actGroupGO.transform, "BtnClearAll", "🗑️ 清空背壳", new Vector2(140, 48), new Color(0.5f, 0.22f, 0.22f, 0.9f), Color.white, 14);
        btnClear.GetComponent<RectTransform>().anchoredPosition = new Vector2(380, 0);
        controller.btnClearAll = btnClear;

        Button btnDemo = CreateButton(actGroupGO.transform, "BtnDemoStroke", "💡 连线示例", new Vector2(140, 48), new Color(0.48f, 0.32f, 0.65f, 0.9f), Color.white, 14);
        btnDemo.GetComponent<RectTransform>().anchoredPosition = new Vector2(530, 0);
        controller.btnDemoStroke = btnDemo;

        GameObject hintTxtGO = CreateText(bottomBarGO.transform, "HintText", "【操作指引】点击左侧蓝图高亮配方；按住滑动进行 8 向连线；点击材料查看数值；按 B 打开蓝图图鉴；Tab/Space 切换整理。", 12, FontStyle.Normal, new Color(0.6f, 0.72f, 0.85f), TextAnchor.MiddleRight);
        RectTransform hintRT = hintTxtGO.GetComponent<RectTransform>();
        hintRT.anchorMin = new Vector2(1, 0.5f);
        hintRT.anchorMax = new Vector2(1, 0.5f);
        hintRT.pivot = new Vector2(1, 0.5f);
        hintRT.anchoredPosition = new Vector2(-24, 0);
        hintRT.sizeDelta = new Vector2(760, 48);

        // =========================================================================
        // 12. 独立材料说明浮窗 (Floating Detail Popup)
        // =========================================================================
        GameObject detailGO = CreateUIObject("DetailPopupPanel", canvasGO.transform);
        StretchFull(detailGO.GetComponent<RectTransform>());
        Image detailMask = detailGO.AddComponent<Image>();
        detailMask.color = new Color(0, 0, 0, 0.45f);

        GameObject detailCardGO = CreateUIObject("DetailCard", detailGO.transform);
        RectTransform dcRT = detailCardGO.GetComponent<RectTransform>();
        dcRT.anchorMin = new Vector2(0.5f, 0.5f);
        dcRT.anchorMax = new Vector2(0.5f, 0.5f);
        dcRT.anchoredPosition = Vector2.zero;
        dcRT.sizeDelta = new Vector2(420, 360);
        Image dcBg = detailCardGO.AddComponent<Image>();
        dcBg.color = new Color(0.09f, 0.13f, 0.19f, 0.98f);

        // 图标
        GameObject dIconGO = CreateUIObject("DetailIcon", detailCardGO.transform);
        RectTransform diRT = dIconGO.GetComponent<RectTransform>();
        diRT.anchorMin = new Vector2(0.5f, 1f);
        diRT.anchorMax = new Vector2(0.5f, 1f);
        diRT.anchoredPosition = new Vector2(0, -56);
        diRT.sizeDelta = new Vector2(76, 76);
        Image dIconImg = dIconGO.AddComponent<Image>();
        dIconImg.preserveAspect = true;
        controller.detailIcon = dIconImg;

        // 名称
        GameObject dNameGO = CreateText(detailCardGO.transform, "DetailName", "电鳗放电肌束", 20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        RectTransform dnRT = dNameGO.GetComponent<RectTransform>();
        dnRT.anchorMin = new Vector2(0.5f, 1f);
        dnRT.anchorMax = new Vector2(0.5f, 1f);
        dnRT.anchoredPosition = new Vector2(0, -108);
        dnRT.sizeDelta = new Vector2(380, 28);
        controller.detailName = dNameGO.GetComponent<Text>();

        // 元素与大格角标
        GameObject dElemGO = CreateText(detailCardGO.transform, "DetailElem", "元素: 电 / 脉冲 (E)", 14, FontStyle.Bold, new Color(0.98f, 0.82f, 0.18f), TextAnchor.MiddleCenter);
        RectTransform deRT = dElemGO.GetComponent<RectTransform>();
        deRT.anchorMin = new Vector2(0.5f, 1f);
        deRT.anchorMax = new Vector2(0.5f, 1f);
        deRT.anchoredPosition = new Vector2(-75, -138);
        deRT.sizeDelta = new Vector2(170, 24);
        controller.detailElementBadge = dElemGO.GetComponent<Text>();

        GameObject dTypeGO = CreateText(detailCardGO.transform, "DetailType", "【大格·骨架主核】", 14, FontStyle.Bold, new Color(0.4f, 0.85f, 1f), TextAnchor.MiddleCenter);
        RectTransform dtypeRT = dTypeGO.GetComponent<RectTransform>();
        dtypeRT.anchorMin = new Vector2(0.5f, 1f);
        dtypeRT.anchorMax = new Vector2(0.5f, 1f);
        dtypeRT.anchoredPosition = new Vector2(75, -138);
        dtypeRT.sizeDelta = new Vector2(170, 24);
        controller.detailTypeBadge = dTypeGO.GetComponent<Text>();

        // 简短说明
        GameObject dDescGO = CreateText(detailCardGO.transform, "DetailDesc", "紧密排布的强力生物电细胞，受压时能产生高频黄色电弧。", 14, FontStyle.Normal, new Color(0.8f, 0.85f, 0.92f), TextAnchor.MiddleCenter);
        RectTransform ddRT = dDescGO.GetComponent<RectTransform>();
        ddRT.anchorMin = new Vector2(0.5f, 0.5f);
        ddRT.anchorMax = new Vector2(0.5f, 0.5f);
        ddRT.anchoredPosition = new Vector2(0, -15);
        ddRT.sizeDelta = new Vector2(380, 50);
        controller.detailDescription = dDescGO.GetComponent<Text>();

        // 四大参数 P/E/G/W 详细标签
        GameObject dStatsGO = CreateText(detailCardGO.transform, "DetailStats", "<b>要素参数：</b> <color=#C085FF>💨 P: 12</color> | <color=#FBD12E>⚡ E: 54</color> | <color=#4AE070>🌿 G: 16</color> | <color=#38BDF8>💧 W: 12</color>", 14, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        RectTransform dsRT = dStatsGO.GetComponent<RectTransform>();
        dsRT.anchorMin = new Vector2(0.5f, 0.5f);
        dsRT.anchorMax = new Vector2(0.5f, 0.5f);
        dsRT.anchoredPosition = new Vector2(0, -65);
        dsRT.sizeDelta = new Vector2(380, 26);
        controller.detailStatsText = dStatsGO.GetComponent<Text>();

        // 关闭按钮
        Button dCloseBtn = CreateButton(detailCardGO.transform, "DetailCloseBtn", "关闭", new Vector2(140, 40), new Color(0.2f, 0.3f, 0.45f, 0.9f), Color.white, 14);
        dCloseBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -145);
        controller.detailCloseBtn = dCloseBtn;

        controller.detailPopupPanel = detailGO;
        detailGO.SetActive(false);

        // =========================================================================
        // 13. 无废品涌现转化展示面板 (Emergence Crafting Result Modal)
        // =========================================================================
        GameObject resultGO = CreateUIObject("CraftResultModal", canvasGO.transform);
        StretchFull(resultGO.GetComponent<RectTransform>());
        Image resultMask = resultGO.AddComponent<Image>();
        resultMask.color = new Color(0, 0, 0, 0.65f);

        GameObject rCardGO = CreateUIObject("ResultCard", resultGO.transform);
        RectTransform rcRT = rCardGO.GetComponent<RectTransform>();
        rcRT.anchorMin = new Vector2(0.5f, 0.5f);
        rcRT.anchorMax = new Vector2(0.5f, 0.5f);
        rcRT.anchoredPosition = Vector2.zero;
        rcRT.sizeDelta = new Vector2(580, 540);
        Image rcBg = rCardGO.AddComponent<Image>();
        rcBg.color = new Color(0.08f, 0.11f, 0.17f, 0.98f);

        // 头部提示
        GameObject rHeadGO = CreateText(rCardGO.transform, "ResultHead", "【生体涌现转化完成】", 22, FontStyle.Bold, new Color(0.98f, 0.82f, 0.18f), TextAnchor.MiddleCenter);
        RectTransform rhRT = rHeadGO.GetComponent<RectTransform>();
        rhRT.anchorMin = new Vector2(0.5f, 1f);
        rhRT.anchorMax = new Vector2(0.5f, 1f);
        rhRT.anchoredPosition = new Vector2(0, -28);
        rhRT.sizeDelta = new Vector2(400, 32);

        // 分类与品质标签
        GameObject rCatGO = CreateText(rCardGO.transform, "ResultCat", "【正规生体家具】", 15, FontStyle.Bold, new Color(0.35f, 0.95f, 0.65f), TextAnchor.MiddleCenter);
        RectTransform rcatRT = rCatGO.GetComponent<RectTransform>();
        rcatRT.anchorMin = new Vector2(0.5f, 1f);
        rcatRT.anchorMax = new Vector2(0.5f, 1f);
        rcatRT.anchoredPosition = new Vector2(-100, -64);
        rcatRT.sizeDelta = new Vector2(200, 26);
        controller.craftResultCategoryBadge = rCatGO.GetComponent<Text>();

        GameObject rQualGO = CreateText(rCardGO.transform, "ResultQual", "品质: 精制 (4 节点)", 15, FontStyle.Bold, new Color(0.98f, 0.85f, 0.35f), TextAnchor.MiddleCenter);
        RectTransform rqualRT = rQualGO.GetComponent<RectTransform>();
        rqualRT.anchorMin = new Vector2(0.5f, 1f);
        rqualRT.anchorMax = new Vector2(0.5f, 1f);
        rqualRT.anchoredPosition = new Vector2(100, -64);
        rqualRT.sizeDelta = new Vector2(200, 26);
        controller.craftResultQualityBadge = rQualGO.GetComponent<Text>();

        // 成品名称
        GameObject rNameGO = CreateText(rCardGO.transform, "ResultName", "精制·电鳗电机发电机组", 24, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        RectTransform rnRT = rNameGO.GetComponent<RectTransform>();
        rnRT.anchorMin = new Vector2(0.5f, 1f);
        rnRT.anchorMax = new Vector2(0.5f, 1f);
        rnRT.anchoredPosition = new Vector2(0, -102);
        rnRT.sizeDelta = new Vector2(500, 36);
        controller.craftResultName = rNameGO.GetComponent<Text>();

        // 图元预览
        GameObject rIconGO = CreateUIObject("ResultIcon", rCardGO.transform);
        RectTransform riRT = rIconGO.GetComponent<RectTransform>();
        riRT.anchorMin = new Vector2(0.5f, 1f);
        riRT.anchorMax = new Vector2(0.5f, 1f);
        riRT.anchoredPosition = new Vector2(0, -165);
        riRT.sizeDelta = new Vector2(80, 80);
        Image rIconImg = rIconGO.AddComponent<Image>();
        rIconImg.preserveAspect = true;
        controller.craftResultIcon = rIconImg;

        // 四大要素聚合数值条
        GameObject rStatsGO = CreateText(rCardGO.transform, "ResultStats", "<b>要素聚合：</b> <color=#C085FF>💨 P: 65</color>  <color=#FBD12E>⚡ E: 98</color>  <color=#4AE070>🌿 G: 42</color>  <color=#38BDF8>💧 W: 35</color>", 14, FontStyle.Bold, new Color(0.95f, 0.85f, 0.45f), TextAnchor.MiddleCenter);
        RectTransform rsRT = rStatsGO.GetComponent<RectTransform>();
        rsRT.anchorMin = new Vector2(0.5f, 1f);
        rsRT.anchorMax = new Vector2(0.5f, 1f);
        rsRT.anchoredPosition = new Vector2(0, -225);
        rsRT.sizeDelta = new Vector2(520, 26);
        controller.craftResultStatsText = rStatsGO.GetComponent<Text>();

        // 物理/实战效果描述 (由 P/E/G/W 驱动实时数值)
        GameObject rEffGO = CreateText(rCardGO.transform, "ResultEffect", "持续向周围传导黄色脉冲弧光，为连接的设施供电；被触碰时麻痹敌人。", 15, FontStyle.Normal, new Color(0.98f, 0.95f, 0.7f), TextAnchor.MiddleCenter);
        RectTransform reRT = rEffGO.GetComponent<RectTransform>();
        reRT.anchorMin = new Vector2(0.5f, 1f);
        reRT.anchorMax = new Vector2(0.5f, 1f);
        reRT.anchoredPosition = new Vector2(0, -270);
        reRT.sizeDelta = new Vector2(500, 52);
        controller.craftResultEffect = rEffGO.GetComponent<Text>();

        // 韵味世界观文本
        GameObject rFlavGO = CreateText(rCardGO.transform, "ResultFlavor", "砗磲贝壳紧紧闭合包裹着放电肌，向外吐出滋滋电火花。", 13, FontStyle.Italic, new Color(0.6f, 0.72f, 0.85f), TextAnchor.MiddleCenter);
        RectTransform rfRT = rFlavGO.GetComponent<RectTransform>();
        rfRT.anchorMin = new Vector2(0.5f, 1f);
        rfRT.anchorMax = new Vector2(0.5f, 1f);
        rfRT.anchoredPosition = new Vector2(0, -325);
        rfRT.sizeDelta = new Vector2(500, 36);
        controller.craftResultFlavor = rFlavGO.GetComponent<Text>();

        // 消耗材料小卡片行
        GameObject rConsGO = CreateUIObject("ConsumedMatsContainer", rCardGO.transform);
        RectTransform rconsRT = rConsGO.GetComponent<RectTransform>();
        rconsRT.anchorMin = new Vector2(0.5f, 1f);
        rconsRT.anchorMax = new Vector2(0.5f, 1f);
        rconsRT.anchoredPosition = new Vector2(0, -380);
        rconsRT.sizeDelta = new Vector2(480, 48);
        HorizontalLayoutGroup hlg = rConsGO.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 10;
        controller.consumedMaterialsContainer = rconsRT;

        // 确定与取消按钮
        Button rConfirmBtn = CreateButton(rCardGO.transform, "ResultConfirmBtn", "收纳成品并腾出背壳空间", new Vector2(240, 50), new Color(0.25f, 0.75f, 0.45f, 0.95f), Color.white, 16);
        rConfirmBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(-110, -460);
        controller.craftConfirmBtn = rConfirmBtn;

        Button rCancelBtn = CreateButton(rCardGO.transform, "ResultCancelBtn", "取消", new Vector2(140, 50), new Color(0.35f, 0.4f, 0.5f, 0.9f), Color.white, 16);
        rCancelBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(130, -460);
        controller.craftCancelBtn = rCancelBtn;

        controller.craftResultModal = resultGO;
        resultGO.SetActive(false);

        // =========================================================================
        // 14. 蓝图图鉴手册面板 (BlueprintCodexModal)
        // =========================================================================
        GameObject codexRoot = CreateUIObject("BlueprintCodexModal", canvasGO.transform);
        StretchFull(codexRoot.GetComponent<RectTransform>());
        Image codexMask = codexRoot.AddComponent<Image>();
        codexMask.color = new Color(0, 0, 0, 0.75f);

        GameObject codexCard = CreateUIObject("CodexCard", codexRoot.transform);
        RectTransform ccRT = codexCard.GetComponent<RectTransform>();
        ccRT.anchorMin = new Vector2(0.5f, 0.5f);
        ccRT.anchorMax = new Vector2(0.5f, 0.5f);
        ccRT.anchoredPosition = Vector2.zero;
        ccRT.sizeDelta = new Vector2(900, 680);
        Image ccBg = codexCard.AddComponent<Image>();
        ccBg.color = new Color(0.07f, 0.1f, 0.15f, 0.98f);

        // 标题
        GameObject cHead = CreateText(codexCard.transform, "CodexHead", "📖 生体机械蓝图与配方图鉴", 22, FontStyle.Bold, new Color(0.98f, 0.82f, 0.18f), TextAnchor.MiddleLeft);
        RectTransform chRT = cHead.GetComponent<RectTransform>();
        chRT.anchorMin = new Vector2(0, 1);
        chRT.anchorMax = new Vector2(0, 1);
        chRT.pivot = new Vector2(0, 1);
        chRT.anchoredPosition = new Vector2(28, -20);
        chRT.sizeDelta = new Vector2(500, 36);

        // 关闭按钮
        Button cClose = CreateButton(codexCard.transform, "CodexCloseBtn", "✕ 关闭", new Vector2(100, 36), new Color(0.35f, 0.2f, 0.2f, 0.9f), Color.white, 14);
        RectTransform clrRT = cClose.GetComponent<RectTransform>();
        clrRT.anchorMin = new Vector2(1, 1);
        clrRT.anchorMax = new Vector2(1, 1);
        clrRT.pivot = new Vector2(1, 1);
        clrRT.anchoredPosition = new Vector2(-28, -20);

        // 分类标签栏
        GameObject tabsRow = CreateUIObject("TabsRow", codexCard.transform);
        RectTransform trRT = tabsRow.GetComponent<RectTransform>();
        trRT.anchorMin = new Vector2(0, 1);
        trRT.anchorMax = new Vector2(1, 1);
        trRT.pivot = new Vector2(0.5f, 1);
        trRT.anchoredPosition = new Vector2(0, -68);
        trRT.sizeDelta = new Vector2(-56, 40);

        Button tabAll = CreateButton(tabsRow.transform, "TabAll", "全部", new Vector2(90, 36), new Color(0.2f, 0.3f, 0.45f, 0.9f), Color.white, 13);
        tabAll.GetComponent<RectTransform>().anchoredPosition = new Vector2(-360, 0);

        Button tabElec = CreateButton(tabsRow.transform, "TabElec", "⚡ 电网设施", new Vector2(110, 36), new Color(0.35f, 0.3f, 0.15f, 0.9f), Color.white, 13);
        tabElec.GetComponent<RectTransform>().anchoredPosition = new Vector2(-250, 0);

        Button tabWood = CreateButton(tabsRow.transform, "TabWood", "🌿 增生防线", new Vector2(110, 36), new Color(0.15f, 0.35f, 0.2f, 0.9f), Color.white, 13);
        tabWood.GetComponent<RectTransform>().anchoredPosition = new Vector2(-130, 0);

        Button tabGas = CreateButton(tabsRow.transform, "TabGas", "💨 气压设施", new Vector2(110, 36), new Color(0.3f, 0.18f, 0.4f, 0.9f), Color.white, 13);
        tabGas.GetComponent<RectTransform>().anchoredPosition = new Vector2(-10, 0);

        Button tabWater = CreateButton(tabsRow.transform, "TabWater", "💧 潮润设施", new Vector2(110, 36), new Color(0.15f, 0.28f, 0.45f, 0.9f), Color.white, 13);
        tabWater.GetComponent<RectTransform>().anchoredPosition = new Vector2(110, 0);

        Button tabScrap = CreateButton(tabsRow.transform, "TabScrap", "🧪 活性杂交废料", new Vector2(130, 36), new Color(0.4f, 0.18f, 0.35f, 0.9f), Color.white, 13);
        tabScrap.GetComponent<RectTransform>().anchoredPosition = new Vector2(245, 0);

        // 滚动区域
        GameObject scrollGO = CreateUIObject("ScrollArea", codexCard.transform);
        RectTransform scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0, 0);
        scrollRT.anchorMax = new Vector2(1, 1);
        scrollRT.anchoredPosition = new Vector2(0, -65);
        scrollRT.sizeDelta = new Vector2(-56, -150);

        ScrollRect sr = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 25f;

        GameObject viewport = CreateUIObject("Viewport", scrollGO.transform);
        StretchFull(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<RectMask2D>();
        sr.viewport = viewport.GetComponent<RectTransform>();

        GameObject content = CreateUIObject("Content", viewport.transform);
        RectTransform contRT = content.GetComponent<RectTransform>();
        contRT.anchorMin = new Vector2(0, 1);
        contRT.anchorMax = new Vector2(1, 1);
        contRT.pivot = new Vector2(0.5f, 1);
        contRT.anchoredPosition = Vector2.zero;
        contRT.sizeDelta = new Vector2(0, 300);

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = contRT;

        BlueprintCodexModal codexComp = codexRoot.AddComponent<BlueprintCodexModal>();
        codexComp.modalRoot = codexRoot;
        codexComp.cardsContainer = contRT;
        codexComp.closeBtn = cClose;
        codexComp.tabAllBtn = tabAll;
        codexComp.tabElecBtn = tabElec;
        codexComp.tabWoodBtn = tabWood;
        codexComp.tabGasBtn = tabGas;
        codexComp.tabWaterBtn = tabWater;
        codexComp.tabScrapBtn = tabScrap;

        controller.codexModal = codexComp;
        codexRoot.SetActive(false);

        // =========================================================================
        // 15. 拖拽幽灵对象 (DragGhost)
        // =========================================================================
        GameObject ghostGO = CreateUIObject("DragGhost", canvasGO.transform);
        RectTransform ghostRT = ghostGO.GetComponent<RectTransform>();
        ghostRT.sizeDelta = new Vector2(76, 76);
        Image ghostImg = ghostGO.AddComponent<Image>();
        ghostImg.raycastTarget = false;
        ghostImg.preserveAspect = true;
        controller.dragGhost = ghostRT;
        controller.dragGhostIcon = ghostImg;
        ghostGO.SetActive(false);

        // 16. 初始化事件与默认背壳
        controller.InitUIEvents();
        
        // 清理临时生成物，保持场景文件纯净小巧，运行时由 Start() 统一动态生成，杜绝构建二进制 level0 序列化冲突
        if (controller.shellContainer != null)
        {
            for (int i = controller.shellContainer.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(controller.shellContainer.GetChild(i).gameObject);
            }
        }

        // 17. 保存场景
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("<color=#4AFF70>[Mix 场景背壳背包构建成功]</color> 已生成完整的寄居蟹背壳网格 UI、蓝图图鉴手册、P/E/G/W 动力学与涌现推演器！");
    }

    [MenuItem("BiomechanicalCrafting/Build Standalone Windows Player")]
    public static void BuildStandalonePlayer()
    {
        BuildMixScene();
        string buildPath = "C:/Users/YiLog/Desktop/test/Mix/TapTap2026.exe";
        string[] scenes = new string[] { "Assets/Scenes/Mix.unity" };
        var report = BuildPipeline.BuildPlayer(scenes, buildPath, BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log($"<color=#4AFF70>[Build Standalone Player]</color> 构建结果: {report.summary.result}, 耗时: {report.summary.totalTime.TotalSeconds:F1}s, 产物大小: {report.summary.totalSize / 1024 / 1024}MB, 错误数: {report.summary.totalErrors}");
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    private static GameObject CreateText(Transform parent, string name, string content, int fontSize, FontStyle style, Color color, TextAnchor align)
    {
        GameObject go = CreateUIObject(name, parent);
        Text t = go.AddComponent<Text>();
        t.font = defaultFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = content;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return go;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 size, Color bgColor, Color textColor, int fontSize)
    {
        GameObject go = CreateUIObject(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.color = bgColor;

        Button btn = go.AddComponent<Button>();

        GameObject txtGO = CreateText(go.transform, "Text", label, fontSize, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
        StretchFull(txtGO.GetComponent<RectTransform>());

        return btn;
    }
}
