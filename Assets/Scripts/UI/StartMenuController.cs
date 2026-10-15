using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class StartMenuController : MonoBehaviour
{
    [SerializeField] private string gameTitle = "生体机械";
    [SerializeField] private string gameplayScene = "BiomechanicalCombatScene";
    private const string VolumeKey = "TAP_MASTER_VOLUME";
    private readonly Color cream = new Color(0.96f, 0.90f, 0.72f);
    private readonly Color brass = new Color(0.71f, 0.56f, 0.31f);
    private readonly Color ink = new Color(0.025f, 0.075f, 0.07f, 0.96f);
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
        artwork.color = new Color(0.64f, 0.71f, 0.62f);
        artwork.raycastTarget = false;
        if (artwork.texture != null)
        {
            var aspect = background.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspect.aspectRatio = (float)artwork.texture.width / artwork.texture.height;
        }
        var shade = Panel("Shade", canvasRect, Vector2.zero, Vector2.zero, new Color(0.01f, 0.045f, 0.04f, 0.30f));
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
        Label("Edition", menu, "T A P T A P  2 0 2 6   /   七 日 旅 程", 20, new Vector2(0, 414), new Vector2(580, 40), brass);
        Label("Title", menu, gameTitle, 112, new Vector2(0, 264), new Vector2(590, 180), cream);
        Label("EnglishTitle", menu, "B I O M E C H A N I C A L", 19, new Vector2(0, 163), new Vector2(570, 38), brass);
        Label("Tagline", menu, "拾取荒野的馈赠，拼成自己的家。", 26, new Vector2(0, 100), new Vector2(590, 55), cream);
        hasSave = PlayerSessionData.TryGetSavedJourneyInfo(out int day, out bool finished);
        string summary = hasSave ? (finished ? "旅程已结束 · 你的归处，已有答案" : $"旅程记录  /  第 {day} 天 · 共 7 天")
            : (PlayerSessionData.HasStoredProgress ? "记录暂不可读取 · 重新开始前将再次确认" : "一副背壳，一片荒野，七天安家。 ");
        Label("SaveSummary", menu, summary, 22, new Vector2(0, 26), new Vector2(590, 50), new Color(0.65f, 0.77f, 0.68f));
        primaryButton = ActionButton("Primary", menu, hasSave ? (finished ? "查看结局   →" : "继续旅程   →") : "开始旅程   →",
            new Vector2(0, -60), new Vector2(550, 88), () => { if (hasSave) BeginJourney(false); else RequestNewJourney(); }, true);
        var restart = ActionButton("NewJourney", menu, PlayerSessionData.HasStoredProgress ? "重新开始" : "尚无旅程记录",
            new Vector2(0, -166), new Vector2(550, 76), RequestNewJourney);
        restart.interactable = PlayerSessionData.HasStoredProgress;
        ActionButton("HowToPlay", menu, "操作说明", new Vector2(-143, -264), new Vector2(264, 76), ShowHelp);
        ActionButton("Settings", menu, "声音设置", new Vector2(143, -264), new Vector2(264, 76), ShowSettings);
        ActionButton("Quit", menu, "退出游戏", new Vector2(0, -356), new Vector2(550, 66), ShowQuit);
        Label("Version", menu, "本地自动保存  ·  v" + Application.version, 18, new Vector2(0, -416), new Vector2(590, 32), brass);
        Label("Days", composition, "07", 130, new Vector2(670, 326), new Vector2(220, 170), cream);
        Label("DaysCaption", composition, "D A Y S  T O  B E L O N G", 18, new Vector2(610, 220), new Vector2(360, 42), cream);
        var caption = Panel("WorldCaption", composition, new Vector2(455, -336), new Vector2(650, 164), new Color(0.02f, 0.09f, 0.075f, 0.86f));
        Panel("CaptionRule", caption.transform, new Vector2(-303, 0), new Vector2(3, 110), brass);
        Label("WorldHeading", caption.transform, "让荒野，成为归处。", 36, new Vector2(10, 28), new Vector2(570, 60), cream);
        Label("WorldLoop", caption.transform, "探索采集  /  背壳合成  /  构筑家园", 23, new Vector2(10, -34), new Vector2(570, 44), cream);
        EventSystem.current.SetSelectedGameObject(primaryButton.gameObject);
    }

    private void CreateAtmosphere(Transform parent)
    {
        motes = new RectTransform[14];
        moteOrigins = new Vector2[motes.Length];
        var sprite = Resources.Load<Sprite>("UI/ui_circle");
        for (int index = 0; index < motes.Length; index++)
        {
            var position = new Vector2(-20 + index * 59, -360 + (index * 131 % 740));
            var mote = Panel("Firefly", parent, position, Vector2.one * (4 + index % 4), new Color(0.65f, 1f, 0.7f, 0.4f));
            mote.sprite = sprite;
            motes[index] = mote.rectTransform;
            moteOrigins[index] = position;
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
            motes[index].anchoredPosition = moteOrigins[index] + new Vector2(Mathf.Sin(Time.unscaledTime * 0.3f + index) * 14, Mathf.Sin(Time.unscaledTime * 0.45f + index * 2) * 22);
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
        Label("ModalTitle", card.transform, title, 44, new Vector2(0, 255), new Vector2(830, 76), cream);
        Panel("Rule", card.transform, new Vector2(0, 197), new Vector2(800, 2), brass);
        Label("ModalBody", card.transform, body, 27, new Vector2(0, 20), new Vector2(810, 320), cream);
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
        var card = OpenModal("开启新的旅程？", @"这会覆盖当前旅程的材料、家具与天数。

声音设置会保留。
如果还想回到原来的家，请选择“保留记录”。");
        Select(ActionButton("Cancel", card, "保留记录", new Vector2(-210, -245), new Vector2(380, 80), CloseModal));
        ActionButton("ConfirmRestart", card, "确认重新开始", new Vector2(210, -245), new Vector2(380, 80), () => BeginJourney(true), true);
    }

    private void ShowHelp()
    {
        var card = OpenModal("旅途指南", @"点地面移动，短滑翻滚；点击敌人锁定目标。
靠近采集门，点击门扉发起挑战。
战斗界面点击“背包 / 合成”，进入工坊。
点击建造锤子，选择成品后点击地面部署。
在工坊与温室，点击返回按钮回到战场。
每日精力有限，休息推进天数，第 7 天结算。

电脑：Tab 打开合成 · B 打开建造抽屉");
        Select(ActionButton("CloseHelp", card, "准备好了", new Vector2(0, -245), new Vector2(500, 80), CloseModal, true));
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
        var card = OpenModal("暂别这片荒野？", @"已保存的旅程会留在这里。
下次启动后，选择“继续旅程”即可返回。");
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
        var card = OpenModal("正在前往荒野", "正在准备你的背壳与家园……");
        loadingLabel = Label("LoadingProgress", card, "0%", 32, new Vector2(0, -110), new Vector2(600, 70), cream);
        Panel("LoadingTrack", card, new Vector2(0, -180), new Vector2(760, 12), new Color(0.12f, 0.22f, 0.17f));
        loadingFill = Panel("LoadingFill", card, new Vector2(-380, -180), new Vector2(0, 12), brass);
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
        var card = OpenModal("暂时无法出发", @"战斗场景未能加载，你的原有记录没有被覆盖。
请确认 Build Settings 包含 BiomechanicalCombatScene。");
        Select(ActionButton("CloseError", card, "返回", new Vector2(0, -245), new Vector2(500, 80), CloseModal));
    }
}
