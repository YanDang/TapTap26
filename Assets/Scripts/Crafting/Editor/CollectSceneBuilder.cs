using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using BiomechanicalCrafting;

namespace BiomechanicalCrafting.Editor
{
    public static class CollectSceneBuilder
    {
        private static Font defaultFont;

        [MenuItem("BiomechanicalCrafting/Build Collect Harvesting Scene")]
        public static void BuildCollectScene()
        {
            string scenePath = "Assets/Scenes/Collect.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 1. 配置主摄像机 (2D 正交正视)
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camGO = new GameObject("Main Camera");
                mainCam = camGO.AddComponent<Camera>();
                camGO.tag = "MainCamera";
            }
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.06f, 0.05f, 0.04f, 1f); // 配合羊皮纸古雅深色
            mainCam.orthographic = true;
            mainCam.orthographicSize = 5f;
            mainCam.transform.position = new Vector3(0, 0, -10);

            // 2. 配置 EventSystem
            EventSystem es = Object.FindObjectOfType<EventSystem>();
            if (es == null)
            {
                GameObject esGO = new GameObject("EventSystem");
                es = esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }

            // 3. 清理已有的旧 Canvas
            Canvas oldCanvas = Object.FindObjectOfType<Canvas>();
            if (oldCanvas != null)
            {
                Object.DestroyImmediate(oldCanvas.gameObject);
            }

            // 4. 创建主 Canvas
            GameObject canvasGO = new GameObject("CollectCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // 5. 挂载主控制器
            CollectSceneController controller = canvasGO.AddComponent<CollectSceneController>();

            // 6. 全屏背景（生体机械药剂工艺温室精美手绘插画）
            GameObject bgGO = CreateUIObject("FullBackground", canvasGO.transform);
            StretchFull(bgGO.GetComponent<RectTransform>());
            Image bgImg = bgGO.AddComponent<Image>();
            bgImg.raycastTarget = false;

            Sprite bgSprite = Resources.Load<Sprite>("CollectBackground");
            if (bgSprite != null)
            {
                bgImg.sprite = bgSprite;
                bgImg.color = Color.white;
            }
            else
            {
                bgImg.color = new Color(0.12f, 0.09f, 0.07f, 1f);
                Debug.LogWarning("[CollectSceneBuilder] Resources/CollectBackground 精灵正在编译或未找到，已配置默认底色。");
            }

            // 7. 顶部装饰与状态栏 (TopHeader)
            GameObject headerGO = CreateUIObject("TopHeader", canvasGO.transform);
            RectTransform headerRT = headerGO.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.anchoredPosition = new Vector2(0, 0);
            headerRT.sizeDelta = new Vector2(0, 95);

            // 顶部暗铜渐变遮罩底板
            Image headerBg = headerGO.AddComponent<Image>();
            headerBg.color = new Color(0.08f, 0.06f, 0.05f, 0.88f);

            // 7.1 左侧：天数信息面板
            GameObject dayInfoGO = CreateUIObject("DayInfoPanel", headerGO.transform);
            RectTransform dayInfoRT = dayInfoGO.GetComponent<RectTransform>();
            dayInfoRT.anchorMin = new Vector2(0, 0.5f);
            dayInfoRT.anchorMax = new Vector2(0, 0.5f);
            dayInfoRT.pivot = new Vector2(0, 0.5f);
            dayInfoRT.anchoredPosition = new Vector2(35, 0);
            dayInfoRT.sizeDelta = new Vector2(360, 70);

            GameObject dayTitleGO = CreateText(dayInfoGO.transform, "DayTitle", "第 1 天", 30, FontStyle.Bold, new Color(0.98f, 0.82f, 0.28f), TextAnchor.UpperLeft);
            RectTransform dayTitleRT = dayTitleGO.GetComponent<RectTransform>();
            dayTitleRT.anchorMin = new Vector2(0, 0.45f);
            dayTitleRT.anchorMax = new Vector2(1, 1);
            dayTitleRT.offsetMin = Vector2.zero;
            dayTitleRT.offsetMax = Vector2.zero;
            controller.dayTitleText = dayTitleGO.GetComponent<Text>();

            GameObject daySubGO = CreateText(dayInfoGO.transform, "DaySub", "生体培育周期 · 【气压喷涌潮】", 17, FontStyle.Normal, new Color(0.65f, 0.82f, 0.95f), TextAnchor.LowerLeft);
            RectTransform daySubRT = daySubGO.GetComponent<RectTransform>();
            daySubRT.anchorMin = new Vector2(0, 0);
            daySubRT.anchorMax = new Vector2(1, 0.45f);
            daySubRT.offsetMin = Vector2.zero;
            daySubRT.offsetMax = Vector2.zero;
            controller.daySubText = daySubGO.GetComponent<Text>();

            // 7.2 中间：操作按钮组
            GameObject centerButtonsGO = CreateUIObject("CenterButtons", headerGO.transform);
            RectTransform centerBtnsRT = centerButtonsGO.GetComponent<RectTransform>();
            centerBtnsRT.anchorMin = new Vector2(0.5f, 0.5f);
            centerBtnsRT.anchorMax = new Vector2(0.5f, 0.5f);
            centerBtnsRT.pivot = new Vector2(0.5f, 0.5f);
            centerBtnsRT.anchoredPosition = new Vector2(-40, 0);
            centerBtnsRT.sizeDelta = new Vector2(500, 60);

            // 【迎来新的一天 / 破晓萌发】按钮
            Button btnNextDay = CreateButton(centerButtonsGO.transform, "BtnNextDay", "☀ 迎来新的一天 (破晓萌发)", new Vector2(245, 52), new Color(0.18f, 0.32f, 0.45f, 0.95f), new Color(0.98f, 0.92f, 0.75f), 16);
            RectTransform btnNextDayRT = btnNextDay.GetComponent<RectTransform>();
            btnNextDayRT.anchoredPosition = new Vector2(-130, 0);
            controller.btnNextDay = btnNextDay;

            // 【前往背壳工坊 (Mix)】按钮
            Button btnGoMix = CreateButton(centerButtonsGO.transform, "BtnGoCrafting", "🐚 前往背壳工坊 (Mix)", new Vector2(230, 52), new Color(0.16f, 0.42f, 0.32f, 0.95f), new Color(0.85f, 0.98f, 0.85f), 16);
            RectTransform btnGoMixRT = btnGoMix.GetComponent<RectTransform>();
            btnGoMixRT.anchoredPosition = new Vector2(125, 0);
            controller.btnGoToCrafting = btnGoMix;

            // 7.3 右侧：采集收纳篮统计 (Basket Counter)
            GameObject basketGO = CreateUIObject("BasketCounter", headerGO.transform);
            RectTransform basketRT = basketGO.GetComponent<RectTransform>();
            basketRT.anchorMin = new Vector2(1, 0.5f);
            basketRT.anchorMax = new Vector2(1, 0.5f);
            basketRT.pivot = new Vector2(1, 0.5f);
            basketRT.anchoredPosition = new Vector2(-35, 0);
            basketRT.sizeDelta = new Vector2(180, 60);
            controller.basketContainer = basketRT;

            // 篮子外框背景
            Image basketBg = basketGO.AddComponent<Image>();
            basketBg.color = new Color(0.15f, 0.12f, 0.09f, 0.92f);

            // 篮子图标
            GameObject iconGO = CreateUIObject("BasketIcon", basketGO.transform);
            RectTransform iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0, 0.5f);
            iconRT.anchorMax = new Vector2(0, 0.5f);
            iconRT.pivot = new Vector2(0.5f, 0.5f);
            iconRT.anchoredPosition = new Vector2(35, 0);
            iconRT.sizeDelta = new Vector2(40, 40);
            Image basketIcon = iconGO.AddComponent<Image>();
            basketIcon.color = new Color(0.95f, 0.78f, 0.28f, 1f);
            controller.basketIcon = basketIcon;

            GameObject basketTxtGO = CreateText(basketGO.transform, "CountText", "0", 24, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            RectTransform basketTxtRT = basketTxtGO.GetComponent<RectTransform>();
            basketTxtRT.anchorMin = new Vector2(0, 0);
            basketTxtRT.anchorMax = new Vector2(1, 1);
            basketTxtRT.offsetMin = new Vector2(65, 0);
            basketTxtRT.offsetMax = new Vector2(-10, 0);
            controller.basketCountText = basketTxtGO.GetComponent<Text>();

            // 8. 节点生成容器 (全屏绝对对齐)
            GameObject spawnContainerGO = CreateUIObject("NodeSpawnContainer", canvasGO.transform);
            StretchFull(spawnContainerGO.GetComponent<RectTransform>());
            controller.nodeSpawnContainer = spawnContainerGO.transform;

            // 9. 浮动文字层 (FloatingTextContainer)
            GameObject floatTextGO = CreateUIObject("FloatingTextContainer", canvasGO.transform);
            StretchFull(floatTextGO.GetComponent<RectTransform>());
            controller.floatingTextContainer = floatTextGO.transform;

            // 10. 日夜轮转全屏淡出遮罩 (DayTransitionFade)
            GameObject fadeGO = CreateUIObject("DayTransitionFade", canvasGO.transform);
            StretchFull(fadeGO.GetComponent<RectTransform>());
            Image fadeImg = fadeGO.AddComponent<Image>();
            fadeImg.color = new Color(0.04f, 0.03f, 0.02f, 1f);
            fadeImg.raycastTarget = false;
            CanvasGroup fadeCG = fadeGO.AddComponent<CanvasGroup>();
            fadeCG.alpha = 0f;
            controller.dayTransitionFadeGroup = fadeCG;

            // 11. 底部提示栏 (Bottom Tip Banner)
            GameObject tipGO = CreateUIObject("BottomTip", canvasGO.transform);
            RectTransform tipRT = tipGO.GetComponent<RectTransform>();
            tipRT.anchorMin = new Vector2(0.5f, 0);
            tipRT.anchorMax = new Vector2(0.5f, 0);
            tipRT.pivot = new Vector2(0.5f, 0);
            tipRT.anchoredPosition = new Vector2(0, 20);
            tipRT.sizeDelta = new Vector2(680, 42);

            Image tipBg = tipGO.AddComponent<Image>();
            tipBg.color = new Color(0.06f, 0.05f, 0.04f, 0.82f);
            tipBg.raycastTarget = false;

            GameObject tipTxtGO = CreateText(tipGO.transform, "Text", "🌿 药剂工艺式采集：鼠标点击或【按住左键直接划过】枝头材料，即可顺滑一笔全收！", 15, FontStyle.Normal, new Color(0.85f, 0.88f, 0.78f), TextAnchor.MiddleCenter);
            StretchFull(tipTxtGO.GetComponent<RectTransform>());

            // 12. 保存场景
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // 13. 确保在 Build Settings 中启用了 Collect 场景
            EnsureSceneInBuildSettings("Assets/Scenes/Collect.unity");
            EnsureSceneInBuildSettings("Assets/Scenes/Mix.unity");

            Debug.Log("<color=#4AFF70>[Collect 场景构建成功]</color> 已生成药剂工艺风格生体机械温室背景、天数萌发轮转系统与一划全收交互！");
        }

        private static void EnsureSceneInBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int idx = scenes.FindIndex(s => s.path == scenePath);
            if (idx >= 0)
            {
                scenes[idx].enabled = true;
            }
            else
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
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
}
