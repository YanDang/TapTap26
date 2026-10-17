using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class StartMenuController : MonoBehaviour
{
    [SerializeField] private string gameTitle = "螺壳海底游记";
    [SerializeField] private string gameplayScene = "BiomechanicalCombatScene";
    private const string VolumeKey = "TAP_MASTER_VOLUME";
    private readonly Color cream = new Color(0.96f, 0.90f, 0.72f);
    private readonly Color brass = new Color(0.71f, 0.56f, 0.31f);
    private readonly Color ink = new Color(0.025f, 0.075f, 0.07f, 0.96f);
    private readonly Color neonCyan = new Color(0.25f, 0.92f, 0.86f);
    private Font font;
    private RectTransform safeRoot;
    private CanvasGroup menuGroup;
    private CanvasGroup fadeGroup;
    private Button primaryButton;
    private GameObject modal;
    private Text volumeLabel;
    private Text loadingLabel;
    private Image loadingFill;
    private bool loading;
    private bool hasSave;
    private Rect lastSafeArea;
    private Vector2 lastScreenSize;
    private RectTransform[] motes;
    private Vector2[] moteOrigins;
    private float[] moteSpeeds;
    private Color[] moteBaseColors;

    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 0.8f));
        CreateCamera();
        CreateInterface();
        ApplySafeArea();
        StartCoroutine(FadeIn());
    }

    private void CreateCamera()
    {
        if (Camera.main != null) return;
        var cameraObject = new GameObject("MenuCamera", typeof(Camera), typeof(AudioListener));
        cameraObject.transform.SetParent(transform, false);
        cameraObject.tag = "MainCamera";
        var menuCamera = cameraObject.GetComponent<Camera>();
        menuCamera.clearFlags = CameraClearFlags.SolidColor;
        menuCamera.backgroundColor = ink;
        menuCamera.cullingMask = 0;
        menuCamera.orthographic = true;
    }

    private RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var created = new GameObject(name, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        var rect = created.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Text Label(string name, Transform parent, string value, int size, Vector2 position, Vector2 dimensions, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        var text = Rect(name, parent, position, dimensions).gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.text = value;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.supportRichText = false;
        return text;
    }

    private Button ActionButton(string name, Transform parent, string caption, Vector2 position, Vector2 size, Action action, bool primary = false)
    {
        var border = Panel(name, parent, position, size, brass);
        border.raycastTarget = true;
        var face = Panel("Face", border.transform, Vector2.zero, size - Vector2.one * 3f,
            primary ? new Color(0.64f, 0.48f, 0.24f) : new Color(0.07f, 0.15f, 0.13f));
        var button = border.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.2f, 1.15f, 0.95f);
        colors.pressedColor = new Color(0.65f, 0.8f, 0.72f);
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        button.colors = colors;
        Label("Caption", border.transform, caption, 28, Vector2.zero, size - new Vector2(16, 4), cream);
        button.onClick.AddListener(() => action());
        return button;
    }

    private void CreateInterface()
    {
        if (EventSystem.current == null)
        {
            var events = new GameObject("MenuEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            events.transform.SetParent(transform, false);
        }
        var canvasRect = Rect("StartMenuCanvas", transform, Vector2.zero, Vector2.zero);
        var canvas = canvasRect.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        canvasRect.gameObject.AddComponent<GraphicRaycaster>();
        fadeGroup = canvasRect.gameObject.AddComponent<CanvasGroup>();

        var background = Rect("GreenhouseIllustration", canvasRect, Vector2.zero, Vector2.zero);
        Stretch(background);
        var artwork = background.gameObject.AddComponent<RawImage>();
        artwork.texture = Resources.Load<Texture2D>("CollectBackground");
        artwork.color = new Color(0.58f, 0.68f, 0.64f);
        artwork.raycastTarget = false;
        if (artwork.texture != null)
        {
            var aspect = background.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspect.aspectRatio = (float)artwork.texture.width / artwork.texture.height;
        }
        var shade = Panel("Shade", canvasRect, Vector2.zero, Vector2.zero, new Color(0.01f, 0.045f, 0.05f, 0.35f));
        Stretch(shade.rectTransform);
        safeRoot = Rect("SafeArea", canvasRect, Vector2.zero, Vector2.zero);
        Stretch(safeRoot);
        var composition = Rect("Composition", safeRoot, Vector2.zero, new Vector2(1760, 940));
        CreateAtmosphere(composition);
        var menu = Rect("Menu", composition, new Vector2(-485, 0), new Vector2(670, 920));
        menuGroup = menu.gameObject.AddComponent<CanvasGroup>();
        Panel("BrassFrame", menu, Vector2.zero, new Vector2(670, 920), brass);
        Panel("DarkGlass", menu, Vector2.zero, new Vector2(666, 916), ink);
        Panel("InnerRule", menu, new Vector2(0, 374), new Vector2(550, 2), brass);
        Label("Edition", menu, "T A P T A P  2 0 2 6   ·   霓 润 城   /   生 态 站 纪 事", 18, new Vector2(0, 414), new Vector2(580, 40), brass);
        Label("Title", menu, gameTitle, 86, new Vector2(0, 266), new Vector2(630, 150), cream);
        Label("EnglishTitle", menu, "S H E L L  O D Y S S E Y  :  N E O N  D E E P", 18, new Vector2(0, 172), new Vector2(590, 36), brass);
        Label("Tagline", menu, "水族已去，机械成林。背一枚螺壳，寻一处容身所。", 23, new Vector2(0, 108), new Vector2(610, 52), cream);
        hasSave = PlayerSessionData.TryGetSavedJourneyInfo(out int day, out bool finished);
        string summary = hasSave ? (finished ? "生态归处 · 你的家园已有答案" : $"生态站日志  /  第 {day} 天 · 共 7 天")
            : (PlayerSessionData.HasStoredProgress ? "记录暂不可读取 · 重新开始前将再次确认" : "初醒的寄居蟹，失控的机械林，七日筑家。");
        Label("SaveSummary", menu, summary, 21, new Vector2(0, 36), new Vector2(590, 48), new Color(0.45f, 0.85f, 0.80f));
        primaryButton = ActionButton("Primary", menu, hasSave ? (finished ? "查看家园   →" : "继续筑家   →") : "开启漫游   →",
            new Vector2(0, -56), new Vector2(550, 84), () => { if (hasSave) BeginJourney(false); else RequestNewJourney(); }, true);
        var restart = ActionButton("NewJourney", menu, PlayerSessionData.HasStoredProgress ? "重新开始" : "尚无生态记录",
            new Vector2(0, -154), new Vector2(550, 72), RequestNewJourney);
        restart.interactable = PlayerSessionData.HasStoredProgress;
        ActionButton("HowToPlay", menu, "生存指南", new Vector2(-143, -244), new Vector2(264, 72), ShowHelp);
        ActionButton("Lore", menu, "生态档案", new Vector2(143, -244), new Vector2(264, 72), ShowLore);
        ActionButton("Settings", menu, "声音设置", new Vector2(-143, -332), new Vector2(264, 70), ShowSettings);
        ActionButton("Quit", menu, "暂别生态", new Vector2(143, -332), new Vector2(264, 70), ShowQuit);
        Label("Version", menu, "本地自动保存  ·  v" + Application.version, 17, new Vector2(0, -412), new Vector2(590, 30), brass);
        Label("Days", composition, "07", 130, new Vector2(670, 326), new Vector2(220, 170), cream);
        Label("DaysCaption", composition, "E C O L O G Y  S T A T I O N  /  0 7  D A Y S", 17, new Vector2(610, 220), new Vector2(360, 42), cream);
        var caption = Panel("WorldCaption", composition, new Vector2(455, -336), new Vector2(650, 164), new Color(0.02f, 0.09f, 0.075f, 0.88f));
        Panel("CaptionRule", caption.transform, new Vector2(-303, 0), new Vector2(3, 110), brass);
        Label("WorldHeading", caption.transform, "机械与森林共生，这里本没有敌人。", 30, new Vector2(10, 28), new Vector2(570, 60), cream);
        Label("WorldLoop", caption.transform, "生体原料采集  /  背壳回路推演  /  生态设施筑巢", 22, new Vector2(10, -34), new Vector2(570, 44), new Color(0.55f, 0.88f, 0.82f));
        EventSystem.current.SetSelectedGameObject(primaryButton.gameObject);
    }

    private void CreateAtmosphere(Transform parent)
    {
        motes = new RectTransform[24];
        moteOrigins = new Vector2[motes.Length];
        moteSpeeds = new float[motes.Length];
        moteBaseColors = new Color[motes.Length];
        var sprite = Resources.Load<Sprite>("UI/ui_circle");

        Color[] palette = new Color[]
        {
            new Color(0.25f, 0.95f, 0.88f, 0.55f), // 霓润水系青
            new Color(0.98f, 0.86f, 0.35f, 0.50f), // 电鳗储能金
            new Color(0.95f, 0.48f, 0.68f, 0.45f), // 珊瑚自生粉
            new Color(0.35f, 0.92f, 0.62f, 0.50f), // 活体海藻绿
            new Color(0.70f, 0.55f, 0.98f, 0.45f)  // 气泡微压紫
        };

        for (int index = 0; index < motes.Length; index++)
        {
            var position = new Vector2(-150 + index * 42, -420 + (index * 137 % 860));
            Color baseCol = palette[index % palette.Length];
            float sz = 4f + (index % 5) * 1.5f;
            var mote = Panel("BioMote", parent, position, Vector2.one * sz, baseCol);
            if (sprite != null) mote.sprite = sprite;
            motes[index] = mote.rectTransform;
            moteOrigins[index] = position;
            moteSpeeds[index] = 0.6f + (index % 4) * 0.25f;
            moteBaseColors[index] = baseCol;
        }
    }

    private IEnumerator FadeIn()
    {
        fadeGroup.alpha = 0;
        for (float elapsed = 0; elapsed < 0.65f; elapsed += Time.unscaledDeltaTime)
        {
            fadeGroup.alpha = Mathf.SmoothStep(0, 1, elapsed / 0.65f);
            yield return null;
        }
        fadeGroup.alpha = 1;
    }

    private void Update()
    {
        ApplySafeArea();
        for (int index = 0; index < motes.Length; index++)
        {
            if (motes[index] == null) continue;
            float spd = (moteSpeeds != null && index < moteSpeeds.Length) ? moteSpeeds[index] : 1f;
            float xOffset = Mathf.Sin(Time.unscaledTime * 0.35f * spd + index) * 18f;
            float yOffset = Mathf.Sin(Time.unscaledTime * 0.5f * spd + index * 1.7f) * 26f;
            motes[index].anchoredPosition = moteOrigins[index] + new Vector2(xOffset, yOffset);

            var img = motes[index].GetComponent<Image>();
            if (img != null && moteBaseColors != null && index < moteBaseColors.Length)
            {
                float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 1.8f + index);
                Color c = moteBaseColors[index];
                img.color = new Color(c.r, c.g, c.b, c.a * pulse);
            }
        }
        if (Input.GetKeyDown(KeyCode.Escape) && !loading)
        {
            if (modal != null) CloseModal();
            else ShowQuit();
        }
    }

    private void ApplySafeArea()
    {
        var size = new Vector2(Screen.width, Screen.height);
        if (size.x <= 0 || size.y <= 0 || (Screen.safeArea == lastSafeArea && size == lastScreenSize)) return;
        lastSafeArea = Screen.safeArea;
        lastScreenSize = size;
        safeRoot.anchorMin = lastSafeArea.min / size;
        safeRoot.anchorMax = lastSafeArea.max / size;
        safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
    }

    private RectTransform OpenModal(string title, string body)
    {
        CloseModal();
        menuGroup.interactable = false;
        menuGroup.blocksRaycasts = false;
        var overlay = Panel("ModalOverlay", safeRoot.parent, Vector2.zero, Vector2.zero, new Color(0, 0.025f, 0.02f, 0.86f));
        Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;
        modal = overlay.gameObject;
        var card = Panel("ModalCard", overlay.transform, Vector2.zero, new Vector2(940, 690), brass);
        Panel("Interior", card.transform, Vector2.zero, new Vector2(936, 686), ink);
        Label("ModalTitle", card.transform, title, 42, new Vector2(0, 255), new Vector2(830, 76), cream);
        Panel("Rule", card.transform, new Vector2(0, 197), new Vector2(800, 2), brass);
        Label("ModalBody", card.transform, body, 25, new Vector2(0, 20), new Vector2(810, 320), cream);
        return card.rectTransform;
    }

    private void CloseModal()
    {
        if (modal != null)
        {
            modal.SetActive(false);
            Destroy(modal);
            modal = null;
        }
        menuGroup.interactable = !loading;
        menuGroup.blocksRaycasts = !loading;
        if (EventSystem.current != null && primaryButton != null && !loading)
            EventSystem.current.SetSelectedGameObject(primaryButton.gameObject);
    }

    private void Select(Button button)
    {
        EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    private void RequestNewJourney()
    {
        if (loading) return;
        if (!PlayerSessionData.HasStoredProgress) { BeginJourney(true); return; }
        var card = OpenModal("开启新的生态旅程？", @"这会重置当前生态站的材料、家具与天数。

声音设置会保留。
如果还想回到原来的家，请选择“保留记录”。");
        var body = card.Find("ModalBody").GetComponent<RectTransform>();
        body.sizeDelta = new Vector2(810, 240);
        body.anchoredPosition = new Vector2(0, 30);
        Select(ActionButton("Cancel", card, "保留记录", new Vector2(-210, -245), new Vector2(380, 80), CloseModal));
        ActionButton("ConfirmRestart", card, "确认重新开始", new Vector2(210, -245), new Vector2(380, 80), () => BeginJourney(true), true);
    }

    private void ShowHelp()
    {
        var card = OpenModal("寄居蟹生存指南", @"【移动与探索】点击地面漫游，短滑轻快翻滚；点击漫游生物进行防卫。
【生体采集】靠近废墟边缘的采集门，消灭失控守卫后进入温室采集原料。
【背壳推演】点击“背包 / 合成”，以一笔画回路连接生物组织与机械零件。
【筑巢部署】点击建造锤，选择合成的生态家具或活性废料布置在站区。
【共生防卫】利用珊瑚墙自愈、电鳗机放电、培养箱安抚本能争夺资源的生物。
【七日结算】每日精力有限，合理分配采集与建造，在第 7 天迎来专属结局。

快捷键：Tab 打开背壳合成 · B 打开建造抽屉 · Esc 暂停");
        var body = card.Find("ModalBody").GetComponent<RectTransform>();
        body.sizeDelta = new Vector2(830, 380);
        body.anchoredPosition = new Vector2(0, 5);
        Select(ActionButton("CloseHelp", card, "准备好了", new Vector2(0, -245), new Vector2(500, 80), CloseModal, true));
    }

    private void ShowLore()
    {
        var card = OpenModal("霓润城 · 生态档案", @"【水族森林与霓润城】
水族曾创造出独特的“生物机械”文明——会输送水流的海藻、储存雷电的水母、自动生长的珊瑚，机械、植物与生物浑然一体。
然而水族的撤退快得毫无缘由，未停机的设施与陆地植物交融疯长，演化成一片无人能解的霓虹机械森林。

【寄居蟹与生态站】
你扮演一只在森林深处苏醒的寄居蟹，背负着随手拾得的小小贝壳，四处寻觅容身之所。
偶然间，你发现了一座停运的水族生态站。尽管自动装置停摆、设施失控，但这里依然能成为遮风避雨的温暖归宿。

【共生与无序的漫游者】
在这片森林里，本没有真正的敌人。巡游的机械与新生生物仅是遵循本能争夺资源。
收集原料、推演背壳回路、构筑生态家具与培养箱，与这片活着的世界达成共生。");
        var body = card.Find("ModalBody").GetComponent<RectTransform>();
        body.sizeDelta = new Vector2(830, 400);
        body.anchoredPosition = new Vector2(0, 10);
        Select(ActionButton("CloseLore", card, "知悉世界", new Vector2(0, -245), new Vector2(500, 80), CloseModal, true));
    }

    private void ShowSettings()
    {
        var card = OpenModal("声音设置", "调整全局声音大小。设置会保存在本机。");
        var body = card.Find("ModalBody").GetComponent<RectTransform>();
        body.anchoredPosition = new Vector2(0, 122);
        body.sizeDelta = new Vector2(810, 70);
        volumeLabel = Label("Volume", card, "", 48, new Vector2(0, 10), new Vector2(390, 85), cream);
        UpdateVolumeLabel();
        ActionButton("DecreaseVolume", card, "−", new Vector2(-280, 10), new Vector2(120, 88), () => ChangeVolume(-0.1f));
        ActionButton("IncreaseVolume", card, "+", new Vector2(280, 10), new Vector2(120, 88), () => ChangeVolume(0.1f));
        ActionButton("Mute", card, "静音 / 恢复", new Vector2(0, -112), new Vector2(500, 76), () => SetVolume(AudioListener.volume > 0 ? 0 : 0.8f));
        Select(ActionButton("CloseSettings", card, "完成", new Vector2(0, -245), new Vector2(500, 80), CloseModal, true));
    }

    private void ChangeVolume(float change) => SetVolume(AudioListener.volume + change);

    private void SetVolume(float volume)
    {
        AudioListener.volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
        PlayerPrefs.Save();
        UpdateVolumeLabel();
    }

    private void UpdateVolumeLabel()
    {
        volumeLabel.text = Mathf.RoundToInt(AudioListener.volume * 100) + "%";
    }

    private void ShowQuit()
    {
        var card = OpenModal("暂别霓润生态站？", @"已保存的筑家进度会留在这枚螺壳中。
下次启动后，选择“继续筑家”即可返回。");
        var body = card.Find("ModalBody").GetComponent<RectTransform>();
        body.sizeDelta = new Vector2(810, 240);
        body.anchoredPosition = new Vector2(0, 30);
        Select(ActionButton("Stay", card, "再待一会", new Vector2(-210, -245), new Vector2(380, 80), CloseModal));
        ActionButton("QuitConfirm", card, "退出游戏", new Vector2(210, -245), new Vector2(380, 80), Quit);
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BeginJourney(bool newJourney)
    {
        if (loading) return;
        if (!Application.CanStreamedLevelBeLoaded(gameplayScene))
        {
            ShowLoadError();
            return;
        }
        loading = true;
        var card = OpenModal("正在前往生态站", "正在整理背壳与生态站设施……");
        var body = card.Find("ModalBody").GetComponent<RectTransform>();
        body.sizeDelta = new Vector2(810, 120);
        body.anchoredPosition = new Vector2(0, 60);
        loadingLabel = Label("LoadingProgress", card, "0%", 32, new Vector2(0, -60), new Vector2(600, 70), cream);
        Panel("LoadingTrack", card, new Vector2(0, -140), new Vector2(760, 12), new Color(0.12f, 0.22f, 0.17f));
        loadingFill = Panel("LoadingFill", card, new Vector2(-380, -140), new Vector2(0, 12), brass);
        loadingFill.rectTransform.pivot = new Vector2(0, 0.5f);
        StartCoroutine(LoadJourney(newJourney));
    }

    private IEnumerator LoadJourney(bool newJourney)
    {
        yield return null;
        AsyncOperation operation = null;
        try
        {
            operation = SceneManager.LoadSceneAsync(gameplayScene, LoadSceneMode.Single);
            if (operation != null) operation.allowSceneActivation = false;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        if (operation == null)
        {
            loading = false;
            ShowLoadError();
            yield break;
        }
        while (operation.progress < 0.9f)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            loadingLabel.text = Mathf.RoundToInt(progress * 100) + "%";
            loadingFill.rectTransform.sizeDelta = new Vector2(760 * progress, 12);
            yield return null;
        }
        PlayerSessionData.EnsureInitialized();
        if (newJourney) PlayerSessionData.StartNewRun();
        PlayerSessionData.isCraftingOpen = false;
        PlayerSessionData.hasJustReturnedFromCrafting = false;
        Time.timeScale = 1;
        Time.fixedDeltaTime = 0.02f;
        loadingLabel.text = "100% · 出发";
        loadingFill.rectTransform.sizeDelta = new Vector2(760, 12);
        yield return null;
        operation.allowSceneActivation = true;
    }

    private void ShowLoadError()
    {
        var card = OpenModal("暂时无法前往生态站", @"生态场景未能加载，你的原有记录没有被覆盖。
请确认 Build Settings 包含 BiomechanicalCombatScene。");
        var body = card.Find("ModalBody").GetComponent<RectTransform>();
        body.sizeDelta = new Vector2(810, 220);
        body.anchoredPosition = new Vector2(0, 30);
        Select(ActionButton("CloseError", card, "返回", new Vector2(0, -245), new Vector2(500, 80), CloseModal));
    }
}
