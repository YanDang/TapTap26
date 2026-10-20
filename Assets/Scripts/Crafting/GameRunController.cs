using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public sealed class GameRunController : MonoBehaviour
{
    private Text statusText;
    private GameObject endingPanel;
    private Text endingText;
    private float nextSave;

    public void Initialize(BiomechanicalCombatAdapter adapter)
    {
        PlayerSessionData.EnsureInitialized();
        GameRunProgress.RestoreWorld(adapter);
        CreateInterface();
        if (GameRunProgress.Data.finished) EnemyController.isAIPaused = true;
        PlayerSessionData.SaveBackpackToStorage();
    }

    private RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var created = new GameObject(name, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        var rect = created.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private Text Label(Transform parent, string value, int size, Vector2 dimensions, Vector2 position)
    {
        var rect = Rect("Text", parent, dimensions, position);
        var text = rect.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    private void CreateInterface()
    {
        var root = Rect("RunProgressCanvas", transform, Vector2.zero, Vector2.zero);
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root.gameObject.AddComponent<GraphicRaycaster>();
        var status = Rect("DailyEnergyPanel", root, new Vector2(350, 100), new Vector2(-28, -205));
        status.anchorMin = status.anchorMax = status.pivot = Vector2.one;
        var background = status.gameObject.AddComponent<Image>();
        background.color = Color.clear;
        background.raycastTarget = false;
        statusText = Label(status, "", 20, new Vector2(340, 94), Vector2.zero);
        var textOutline = statusText.gameObject.AddComponent<Outline>();
        textOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        textOutline.effectDistance = new Vector2(1.5f, -1.5f);

        var overlay = Rect("RunEndingPanel", root, Vector2.zero, Vector2.zero);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        overlay.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.04f, 0.06f, 0.96f);
        endingPanel = overlay.gameObject;
        endingText = Label(overlay, "", 30, new Vector2(900, 430), new Vector2(0, 90));
        var restart = Rect("BtnNewJourney", overlay, new Vector2(360, 64), new Vector2(0, -200));
        var image = restart.gameObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.4f, 0.33f);
        var button = restart.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(StartNewJourney);
        Label(restart, "开启新旅程", 26, new Vector2(350, 60), Vector2.zero);
        Label(overlay, "重新开始会清空本局材料、家具与进度", 18, new Vector2(700, 40), new Vector2(0, -265));
        endingPanel.SetActive(false);
    }

    private void Update()
    {
        if (statusText == null) return;
        var data = GameRunProgress.Data;
        int count = data.finished ? data.finalFurnitureCount : GameRunProgress.CountFurniture();
        statusText.text = $"第 {PlayerSessionData.greenhouse.day} / 7 天  ·  每日精力 {data.energy} / 8\n安家进度：{count} / 8 件家具\n开门 2 点 · 合成 1 点 · 换日补满";
        endingPanel.SetActive(data.finished);
        if (data.finished)
        {
            string description = count >= 8 ? "你保住了自己的家，也让这片陌生土地有了归属。" : count >= 4 ? "你有了容身之处，下一次可以把家建得更稳固。" : "旅程留下了足迹，但你的家还在前方。";
            endingText.text = $"七日旅程 · {GameRunProgress.EndingTitle(count)}\n\n完成天数：7 天\n留下的有效家具：{count} 件\n\n{description}\n\n0～3 漂泊 · 4～7 落脚 · 8+ 安家";
            return;
        }
        if (Time.unscaledTime >= nextSave && GameRunProgress.WorldReady)
        {
            nextSave = Time.unscaledTime + 2f;
            PlayerSessionData.SaveBackpackToStorage();
        }
    }

    public void StartNewJourney()
    {
        if (!GameRunProgress.Data.finished || GameplaySceneTransition.IsBusy) return;
        string sceneName = gameObject.scene.name;
        PlayerSessionData.StartNewRun();
        SceneManager.LoadScene(sceneName);
    }

    private void OnDestroy()
    {
        GameRunProgress.WorldReady = false;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && GameRunProgress.WorldReady) PlayerSessionData.SaveBackpackToStorage();
    }

    private void OnApplicationQuit()
    {
        if (GameRunProgress.WorldReady) PlayerSessionData.SaveBackpackToStorage();
    }
}
