using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using BiomechanicalCrafting;

/// <summary>
/// 药剂工艺式温室采集场景控制器 (Collect Scene Controller)
/// 负责控制生体水培温室的日常运作、天数轮转、材料随机萌发、一划全收与收纳存储。
/// </summary>
public class CollectSceneController : MonoBehaviour
{
    public static CollectSceneController Instance { get; private set; }

    [Header("UI Header")]
    public Text dayTitleText;
    public Text daySubText;
    public Button btnNextDay;
    public Button btnGoToCrafting;
    public RectTransform basketContainer;
    public Text basketCountText;
    public Image basketIcon;

    [Header("Sprouting & Nodes")]
    public Transform nodeSpawnContainer;
    public GameObject nodePrefab;
    public CanvasGroup dayTransitionFadeGroup;

    [Header("Floating Text")]
    public Transform floatingTextContainer;

    [Header("Audio")]
    private AudioSource audioSource;
    private AudioClip popClip;
    private AudioClip refusalClip;

    // 运行态数据
    private int currentDay = 1;
    private int todayHarvestedCount = 0;
    private List<HarvestItemNode> activeNodes = new List<HarvestItemNode>();

    // 悬停轻量属性透视卡片 (Hover Tooltip Card)
    private GameObject hoverTooltipGO;
    private RectTransform hoverTooltipRT;
    private Text hoverTitleText;
    private Text hoverTypeText;
    private Text hoverDescText;
    private Text hoverStatsText;

    // 程序化精灵缓存
    private Sprite softCircleSprite;
    private Sprite pillBgSprite;

    // 经过精细校准的 17 个自然温室挂载点坐标（完美契合 Potion Craft 风格背景中的枝头、架子、挂罐、地槽）
    private readonly Vector2[] SpawnAnchors = new Vector2[]
    {
        new Vector2(-710, 240),  // 0: 左上珊瑚枝头 (悬挂气系/电系)
        new Vector2(-540, 310),  // 1: 左侧拱形铜架顶
        new Vector2(-510, 75),   // 2: 左侧木架上层
        new Vector2(-510, -85),  // 3: 左侧木架中层
        new Vector2(-760, -320), // 4: 左下大型水培罐口
        new Vector2(-590, -330), // 5: 左下玻璃温室瓶
        new Vector2(-380, -320), // 6: 左侧蘑菇培养罩
        new Vector2(-220, 310),  // 7: 顶部悬吊青色烧瓶
        new Vector2(210, 310),   // 8: 顶部悬吊琥珀烧瓶
        new Vector2(225, 25),    // 9: 中央悬吊植物挂篮 A
        new Vector2(340, 0),     // 10: 中央悬吊植物挂篮 B
        new Vector2(580, 110),   // 11: 右侧拱架上层枝蔓
        new Vector2(580, -95),   // 12: 右侧木架平台
        new Vector2(560, 290),   // 13: 右上悬挂翠绿试剂瓶
        new Vector2(700, 260),   // 14: 右上角悬挂长颈瓶
        new Vector2(540, -330),  // 15: 右下菌群培养罐
        new Vector2(810, -300)   // 16: 右下角高压生体圆柱管
    };

    private bool isTransitioning = false;

    void Awake()
    {
        Instance = this;
        InitAudio();

        // EventSystem 处理：若全局尚无可用 EventSystem（单场景独立测试），则激活本场景预置的 EventSystem；
        // 若在 Additive 叠加模式运行（场景数 > 1，已由战斗底座提供），本场景预置的 EventSystem（初始未激活）直接销毁，彻底消除引擎警告！
        if (UnityEngine.EventSystems.EventSystem.current == null && FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var myRoots = gameObject.scene.GetRootGameObjects();
            foreach (var r in myRoots)
            {
                if (r != null && r.name == "EventSystem")
                {
                    r.SetActive(true);
                    break;
                }
            }
        }
        else if (UnityEngine.SceneManagement.SceneManager.sceneCount > 1)
        {
            var myRoots = gameObject.scene.GetRootGameObjects();
            foreach (var r in myRoots)
            {
                if (r != null && r.name == "EventSystem")
                {
                    Destroy(r);
                }
                if (r != null && r.name == "Main Camera")
                {
                    var al = r.GetComponent<AudioListener>();
                    if (al != null) Destroy(al);
                }
            }
        }
    }

    void Update()
    {
        // 支持 ESC 快捷键平滑返回合成背壳工坊
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            GoToCraftingScene();
        }
    }

    void Start()
    {
        PlayerSessionData.EnsureInitialized();
        InitButtons();
        InitHoverTooltip();
        SproutDay(currentDay);
    }

    private void InitAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        popClip = CreateSoftPopClip();
        refusalClip = CreateRefusalClip();
    }

    /// <summary>
    /// 程序化生成清脆温润的生体拔出/水滴音效 (无需外挂音频文件，纯原生采样合成)
    /// </summary>
    private AudioClip CreateSoftPopClip()
    {
        int sampleRate = 44100;
        float duration = 0.12f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            // 快速滑音频感 (从 480Hz 瞬间滑向 920Hz，模拟拔出啵的一声)
            float freq = Mathf.Lerp(480f, 960f, t / duration);
            float env = Mathf.Exp(-t * 36f); // 快速指数衰减
            samples[i] = Mathf.Sin(2 * Mathf.PI * freq * t) * env * 0.4f;
        }

        AudioClip clip = AudioClip.Create("BioPop", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// 程序化生成低沉的背包已满拒收音效 (低频双谐波顿挫音)
    /// </summary>
    private AudioClip CreateRefusalClip()
    {
        int sampleRate = 44100;
        float duration = 0.18f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float freq = Mathf.Lerp(180f, 110f, t / duration);
            float env = Mathf.Exp(-t * 20f);
            samples[i] = (Mathf.Sin(2 * Mathf.PI * freq * t) + Mathf.Sin(2 * Mathf.PI * (freq * 0.5f) * t) * 0.5f) * env * 0.45f;
        }

        AudioClip clip = AudioClip.Create("RefusalBonk", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void InitButtons()
    {
        if (btnNextDay == null)
        {
            var n = GameObject.Find("BtnNextDay")?.GetComponent<Button>();
            if (n != null) btnNextDay = n;
        }
        if (btnNextDay != null)
        {
            btnNextDay.onClick.RemoveAllListeners();
            btnNextDay.onClick.AddListener(OnNextDayClicked);
        }

        if (btnGoToCrafting == null)
        {
            var c = GameObject.Find("BtnGoCrafting")?.GetComponent<Button>();
            if (c != null) btnGoToCrafting = c;
        }
        if (btnGoToCrafting != null)
        {
            btnGoToCrafting.onClick.RemoveAllListeners();
            btnGoToCrafting.onClick.AddListener(GoToCraftingScene);
        }
    }

    /// <summary>
    /// 返回背壳工坊 (Mix)：
    /// 若处于 Additive 叠加模式（后台常驻战斗场景），则叠加异步加载 Mix，完成后卸载本温室 Collect 场景，无缝守护战斗现场！
    /// </summary>
    public void GoToCraftingScene()
    {
        if (isTransitioning) return;
        isTransitioning = true;

        Debug.Log("<color=#70D2FF>[CollectScene]</color> 正在进入背壳工坊 (Mix)...");
        PlayerSessionData.SaveBackpackSlots(PlayerSessionData.shellSlotItems);

        if (SceneManager.sceneCount > 1)
        {
            var myScene = gameObject.scene;
            var loadOp = SceneManager.LoadSceneAsync("Mix", LoadSceneMode.Additive);
            loadOp.completed += (_) =>
            {
                SceneManager.UnloadSceneAsync(myScene);
            };
        }
        else
        {
            SceneManager.LoadScene("Mix");
        }
    }

    /// <summary>
    /// 推进至下一天
    /// </summary>
    public void OnNextDayClicked()
    {
        StartCoroutine(NextDayRoutine());
    }

    private IEnumerator NextDayRoutine()
    {
        // 柔和破晓淡入淡出动效 (采用 unscaledDeltaTime 确保任何时停或冻结下正常流转)
        if (dayTransitionFadeGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < 0.25f)
            {
                elapsed += Time.unscaledDeltaTime;
                dayTransitionFadeGroup.alpha = Mathf.Clamp01(elapsed / 0.25f);
                yield return null;
            }
        }

        currentDay++;
        SproutDay(currentDay);

        if (dayTransitionFadeGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < 0.35f)
            {
                elapsed += Time.unscaledDeltaTime;
                dayTransitionFadeGroup.alpha = 1f - Mathf.Clamp01(elapsed / 0.35f);
                yield return null;
            }
            dayTransitionFadeGroup.alpha = 0f;
        }
    }

    /// <summary>
    /// 萌发指定日期的材料批次
    /// </summary>
    public void SproutDay(int day)
    {
        HideHoverTooltip();
        todayHarvestedCount = 0;
        UpdateHeaderUI();

        // 清除旧的未收集残余
        foreach (var node in activeNodes)
        {
            if (node != null && !node.isHarvested)
            {
                Destroy(node.gameObject);
            }
        }
        activeNodes.Clear();

        // 获取全部配置材料
        List<BiomechanicalMaterial> allMaterials = BiomechanicalMaterialDatabase.GetAllMaterials();
        if (allMaterials == null || allMaterials.Count == 0)
        {
            Debug.LogWarning("[CollectScene] 未能读取到材料列表！");
            return;
        }

        // 随机挑选 7 ~ 10 个锚点进行萌发生长
        int spawnCount = UnityEngine.Random.Range(7, 11);
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < SpawnAnchors.Length; i++) availableIndices.Add(i);

        // 洗牌算法
        for (int i = 0; i < availableIndices.Count; i++)
        {
            int r = UnityEngine.Random.Range(i, availableIndices.Count);
            int temp = availableIndices[i];
            availableIndices[i] = availableIndices[r];
            availableIndices[r] = temp;
        }

        for (int i = 0; i < spawnCount && i < availableIndices.Count; i++)
        {
            int anchorIdx = availableIndices[i];
            Vector2 pos = SpawnAnchors[anchorIdx];

            // 随机选取材料（70% 概率辅料 1x1，30% 概率核心骨架 2x2）
            BiomechanicalMaterial selectedMat = PickRandomMaterial(allMaterials);
            if (selectedMat != null)
            {
                CreateItemNode(selectedMat, pos);
            }
        }
    }

    private BiomechanicalMaterial PickRandomMaterial(List<BiomechanicalMaterial> list)
    {
        bool wantMajor = UnityEngine.Random.value < 0.28f;
        List<BiomechanicalMaterial> subList = list.FindAll(m => m.isMajor == wantMajor);
        if (subList.Count == 0) subList = list;
        return subList[UnityEngine.Random.Range(0, subList.Count)];
    }

    private void CreateItemNode(BiomechanicalMaterial mat, Vector2 pos)
    {
        GameObject go;
        if (nodePrefab != null)
        {
            go = Instantiate(nodePrefab, nodeSpawnContainer);
        }
        else
        {
            go = CreateDefaultNodeGameObject();
            go.transform.SetParent(nodeSpawnContainer, false);
        }

        HarvestItemNode node = go.GetComponent<HarvestItemNode>();
        if (node == null) node = go.AddComponent<HarvestItemNode>();

        node.Setup(mat, pos, OnItemHarvestAttempt);
        activeNodes.Add(node);
    }

    /// <summary>
    /// 当玩家尝试采摘（点击或划过）某个生体材料节点：
    /// 校验背壳背包是否仍有合适拓扑槽位。若背包已满则拒绝采摘并给予顿挫警报；若成功则飞入收纳篮并记录对应槽位。
    /// </summary>
    private bool OnItemHarvestAttempt(HarvestItemNode node)
    {
        if (node == null || node.materialData == null) return false;

        // 尝试按序分配进入背壳背包槽位
        bool success = PlayerSessionData.TryAddMaterialToShellBackpack(node.materialData, out int targetSlotId, out string failReason);
        if (!success)
        {
            // 背包空间不足：播放低沉拒收提示音
            if (audioSource != null && refusalClip != null)
            {
                audioSource.pitch = 1f;
                audioSource.PlayOneShot(refusalClip);
            }

            // 弹出醒目的红色拒收飘字
            SpawnFloatingFailText(node.transform.position, failReason);

            // 右上角收纳篮轻微震颤警示
            StartCoroutine(ShakeBasketContainer());

            return false;
        }

        // 成功采摘放入背壳背包！播放清脆温润拔出音
        if (audioSource != null && popClip != null)
        {
            audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.15f);
            audioSource.PlayOneShot(popClip);
        }

        todayHarvestedCount++;

        // 记录历史采摘流水
        PlayerSessionData.gatheredBiomechanicalMaterials.Add(node.materialData);

        // 弹出清脆的生体浮动飘字提示（含目标槽位号）
        SpawnFloatingText(node.transform.position, node.materialData, targetSlotId);

        // 飞向右上角收纳篮
        Vector3 targetPos = basketIcon != null ? basketIcon.transform.position : (Vector3.right * 750 + Vector3.up * 450);
        node.FlyToDestination(targetPos, () =>
        {
            // 击中篮子时的弹跳轻震动
            StartCoroutine(PunchBasketIcon());
            UpdateHeaderUI();
        });

        return true;
    }

    private IEnumerator PunchBasketIcon()
    {
        if (basketIcon == null) yield break;
        Transform t = basketIcon.transform;
        Vector3 orig = Vector3.one;

        float dur = 0.18f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / dur);
            float scale = 1f + Mathf.Sin(progress * Mathf.PI) * 0.28f;
            t.localScale = orig * scale;
            yield return null;
        }
        t.localScale = orig;
    }

    private IEnumerator ShakeBasketContainer()
    {
        if (basketContainer == null) yield break;
        Vector2 orig = basketContainer.anchoredPosition;
        float dur = 0.22f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float offset = Mathf.Sin(elapsed * 55f) * 6f * (1f - Mathf.Clamp01(elapsed / dur));
            basketContainer.anchoredPosition = new Vector2(orig.x + offset, orig.y);
            yield return null;
        }
        basketContainer.anchoredPosition = orig;
    }

    private void SpawnFloatingText(Vector3 worldPos, BiomechanicalMaterial mat, int slotId = -1)
    {
        if (floatingTextContainer == null) return;

        GameObject txtGO = new GameObject("FloatingText");
        txtGO.transform.SetParent(floatingTextContainer, false);
        txtGO.transform.position = worldPos + Vector3.up * 45f;

        RectTransform rt = txtGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(380, 42);

        Text t = txtGO.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 20;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;

        string colHex = ColorUtility.ToHtmlStringRGB(mat.ElementColor);
        if (mat.isMajor)
        {
            t.text = $"<color=#FDE047>✨ 获得骨架核心：</color><color=#{colHex}><b>{mat.materialName}</b></color> <color=#FDE047>[🦴 2x2]</color>";
        }
        else
        {
            t.text = $"<color=#{colHex}><b>+1 {mat.materialName}</b></color> <color=#CBD5E1>[{mat.ElementName}]</color>";
        }

        // 上浮并淡出动效
        StartCoroutine(FloatAndFadeRoutine(txtGO, t));
    }

    private void SpawnFloatingFailText(Vector3 worldPos, string reason)
    {
        if (floatingTextContainer == null) return;

        GameObject txtGO = new GameObject("FloatingFailText");
        txtGO.transform.SetParent(floatingTextContainer, false);
        txtGO.transform.position = worldPos + Vector3.up * 45f;

        RectTransform rt = txtGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(380, 42);

        Text t = txtGO.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 20;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        t.text = $"<color=#FF4D4F>⚠️ {reason}</color>";

        StartCoroutine(FloatAndFadeRoutine(txtGO, t));
    }

    private IEnumerator FloatAndFadeRoutine(GameObject go, Text t)
    {
        float dur = 0.85f;
        float elapsed = 0f;
        Vector3 startPos = go.transform.position;

        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(elapsed / dur);
            go.transform.position = startPos + Vector3.up * (p * 55f);

            Color c = t.color;
            c.a = 1f - Mathf.Pow(p, 2f);
            t.color = c;

            yield return null;
        }

        Destroy(go);
    }

    private void UpdateHeaderUI()
    {
        if (dayTitleText != null)
        {
            dayTitleText.text = $"第 {currentDay} 天";
        }

        if (daySubText != null)
        {
            string phase = (currentDay % 4) switch
            {
                1 => "【气压喷涌潮】",
                2 => "【电弧脉冲期】",
                3 => "【珊瑚萌发期】",
                _ => "【潮润渗流期】"
            };
            daySubText.text = $"生体培育周期 · {phase}";
        }

        int occupied = PlayerSessionData.GetOccupiedSlotCount();
        int total = PlayerSessionData.GetTotalSlotCount();
        if (basketCountText != null)
        {
            bool isFull = occupied >= total;
            string col = isFull ? "#FF5555" : "#FFFFFF";
            basketCountText.text = $"<color={col}>{occupied}</color> / {total}";
        }
    }

    #region Hover Tooltip Card (轻量悬停透视卡片)

    private void InitHoverTooltip()
    {
        if (hoverTooltipGO != null) return;

        hoverTooltipGO = new GameObject("HoverTooltipCard");
        Transform parentT = floatingTextContainer != null ? floatingTextContainer : transform;
        hoverTooltipGO.transform.SetParent(parentT, false);

        hoverTooltipRT = hoverTooltipGO.AddComponent<RectTransform>();
        hoverTooltipRT.sizeDelta = new Vector2(260, 130);
        hoverTooltipRT.pivot = new Vector2(0.5f, 0f);

        Image cardBg = hoverTooltipGO.AddComponent<Image>();
        cardBg.color = new Color(0.06f, 0.09f, 0.15f, 0.96f);
        cardBg.raycastTarget = false;

        // 标题
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(hoverTooltipGO.transform, false);
        RectTransform tRT = titleGO.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0, 1);
        tRT.anchorMax = new Vector2(1, 1);
        tRT.pivot = new Vector2(0, 1);
        tRT.anchoredPosition = new Vector2(12, -8);
        tRT.sizeDelta = new Vector2(-24, 24);
        hoverTitleText = titleGO.AddComponent<Text>();
        hoverTitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hoverTitleText.fontSize = 15;
        hoverTitleText.fontStyle = FontStyle.Bold;
        hoverTitleText.alignment = TextAnchor.MiddleLeft;
        hoverTitleText.raycastTarget = false;

        // 规格与元素
        GameObject typeGO = new GameObject("TypeBadge");
        typeGO.transform.SetParent(hoverTooltipGO.transform, false);
        RectTransform tpRT = typeGO.AddComponent<RectTransform>();
        tpRT.anchorMin = new Vector2(0, 1);
        tpRT.anchorMax = new Vector2(1, 1);
        tpRT.pivot = new Vector2(0, 1);
        tpRT.anchoredPosition = new Vector2(12, -32);
        tpRT.sizeDelta = new Vector2(-24, 20);
        hoverTypeText = typeGO.AddComponent<Text>();
        hoverTypeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hoverTypeText.fontSize = 11;
        hoverTypeText.fontStyle = FontStyle.Bold;
        hoverTypeText.alignment = TextAnchor.MiddleLeft;
        hoverTypeText.raycastTarget = false;

        // 简短描述
        GameObject descGO = new GameObject("Desc");
        descGO.transform.SetParent(hoverTooltipGO.transform, false);
        RectTransform dcRT = descGO.AddComponent<RectTransform>();
        dcRT.anchorMin = new Vector2(0, 1);
        dcRT.anchorMax = new Vector2(1, 1);
        dcRT.pivot = new Vector2(0, 1);
        dcRT.anchoredPosition = new Vector2(12, -54);
        dcRT.sizeDelta = new Vector2(-24, 38);
        hoverDescText = descGO.AddComponent<Text>();
        hoverDescText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hoverDescText.fontSize = 11;
        hoverDescText.alignment = TextAnchor.UpperLeft;
        hoverDescText.color = new Color(0.72f, 0.8f, 0.9f);
        hoverDescText.raycastTarget = false;

        // 属性栏
        GameObject statsGO = new GameObject("Stats");
        statsGO.transform.SetParent(hoverTooltipGO.transform, false);
        RectTransform stRT = statsGO.AddComponent<RectTransform>();
        stRT.anchorMin = new Vector2(0, 0);
        stRT.anchorMax = new Vector2(1, 0);
        stRT.pivot = new Vector2(0, 0);
        stRT.anchoredPosition = new Vector2(12, 8);
        stRT.sizeDelta = new Vector2(-24, 22);
        hoverStatsText = statsGO.AddComponent<Text>();
        hoverStatsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hoverStatsText.fontSize = 11;
        hoverStatsText.fontStyle = FontStyle.Bold;
        hoverStatsText.alignment = TextAnchor.MiddleLeft;
        hoverStatsText.raycastTarget = false;

        hoverTooltipGO.SetActive(false);
    }

    public void ShowHoverTooltip(HarvestItemNode node)
    {
        if (node == null || node.materialData == null || node.isHarvested) return;
        if (hoverTooltipGO == null) InitHoverTooltip();

        var mat = node.materialData;
        hoverTooltipGO.SetActive(true);
        hoverTooltipRT.position = node.transform.position + Vector3.up * 85f;

        // 限制在屏幕可见边界内
        Vector3 pos = hoverTooltipRT.localPosition;
        pos.x = Mathf.Clamp(pos.x, -760f, 760f);
        pos.y = Mathf.Clamp(pos.y, -420f, 420f);
        hoverTooltipRT.localPosition = pos;

        if (hoverTitleText != null)
        {
            hoverTitleText.text = mat.materialName;
            hoverTitleText.color = mat.ElementColor;
        }
        if (hoverTypeText != null)
        {
            hoverTypeText.text = mat.isMajor ? "【大格 · 骨架主核 2x2】" : $"【小格 · 生体辅料 1x1】 · {mat.ElementName}";
            hoverTypeText.color = mat.isMajor ? new Color(0.98f, 0.82f, 0.18f) : new Color(0.7f, 0.88f, 1f);
        }
        if (hoverDescText != null)
        {
            hoverDescText.text = mat.shortDescription;
        }
        if (hoverStatsText != null)
        {
            hoverStatsText.text = $"<color=#C085FF>💨 P: {mat.p}</color>  |  <color=#FBD12E>⚡ E: {mat.e}</color>  |  <color=#4AE070>🌿 G: {mat.g}</color>  |  <color=#38BDF8>💧 W: {mat.w}</color>";
        }
    }

    public void HideHoverTooltip()
    {
        if (hoverTooltipGO != null) hoverTooltipGO.SetActive(false);
    }

    #endregion

    /// <summary>
    /// 纯代码构建升级版的 ItemNode 视觉层次（发光底环 + 金色骨架环 + 精灵图 + 顶部规格角标 + 底部常驻名称胶囊）
    /// </summary>
    private GameObject CreateDefaultNodeGameObject()
    {
        if (softCircleSprite == null) softCircleSprite = CreateSoftCircleSprite();
        if (pillBgSprite == null) pillBgSprite = CreateCapsuleSprite(120, 26, new Color(0.05f, 0.08f, 0.14f, 0.94f));

        GameObject go = new GameObject("HarvestNode");
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(80, 80);

        // 1. 发光外环 (Glow Ring)
        GameObject glowGO = new GameObject("GlowRing");
        glowGO.transform.SetParent(go.transform, false);
        RectTransform glowRT = glowGO.AddComponent<RectTransform>();
        glowRT.sizeDelta = new Vector2(100, 100);
        Image glowImg = glowGO.AddComponent<Image>();
        glowImg.raycastTarget = false;
        glowImg.sprite = softCircleSprite;

        // 2. 核心边框环 (Border Ring, 用于 2x2 核心金光外圈)
        GameObject borderGO = new GameObject("BorderRing");
        borderGO.transform.SetParent(go.transform, false);
        RectTransform borderRT = borderGO.AddComponent<RectTransform>();
        borderRT.sizeDelta = new Vector2(88, 88);
        Image borderImg = borderGO.AddComponent<Image>();
        borderImg.raycastTarget = false;
        borderImg.sprite = softCircleSprite;
        borderGO.SetActive(false);

        // 3. 核心材料图标 (Icon)
        GameObject iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(go.transform, false);
        RectTransform iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.sizeDelta = new Vector2(70, 70);
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.raycastTarget = true; // 接收点击与划动事件
        iconImg.preserveAspect = true;

        // 4. 顶部小角标 (Top Badge, 规格/元素标识)
        GameObject badgeGO = new GameObject("TopBadge");
        badgeGO.transform.SetParent(go.transform, false);
        RectTransform badgeRT = badgeGO.AddComponent<RectTransform>();
        badgeRT.anchorMin = new Vector2(0.5f, 1f);
        badgeRT.anchorMax = new Vector2(0.5f, 1f);
        badgeRT.pivot = new Vector2(0.5f, 0f);
        badgeRT.anchoredPosition = new Vector2(0, 4);
        badgeRT.sizeDelta = new Vector2(76, 20);
        Image badgeBg = badgeGO.AddComponent<Image>();
        badgeBg.sprite = pillBgSprite;
        badgeBg.color = new Color(0.04f, 0.07f, 0.12f, 0.95f);
        badgeBg.raycastTarget = false;

        GameObject badgeTxtGO = new GameObject("Text");
        badgeTxtGO.transform.SetParent(badgeGO.transform, false);
        RectTransform btRT = badgeTxtGO.AddComponent<RectTransform>();
        btRT.anchorMin = Vector2.zero;
        btRT.anchorMax = Vector2.one;
        btRT.sizeDelta = Vector2.zero;
        Text badgeTxt = badgeTxtGO.AddComponent<Text>();
        badgeTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        badgeTxt.fontSize = 11;
        badgeTxt.fontStyle = FontStyle.Bold;
        badgeTxt.alignment = TextAnchor.MiddleCenter;
        badgeTxt.raycastTarget = false;

        // 5. 底部常驻名称胶囊标牌 (Bottom Name Capsule)
        GameObject nameGO = new GameObject("NameCapsule");
        nameGO.transform.SetParent(go.transform, false);
        RectTransform nameRT = nameGO.AddComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0.5f, 0f);
        nameRT.anchorMax = new Vector2(0.5f, 0f);
        nameRT.pivot = new Vector2(0.5f, 1f);
        nameRT.anchoredPosition = new Vector2(0, -6);
        nameRT.sizeDelta = new Vector2(110, 24);
        Image nameBg = nameGO.AddComponent<Image>();
        nameBg.sprite = pillBgSprite;
        nameBg.color = new Color(0.06f, 0.09f, 0.14f, 0.95f);
        nameBg.raycastTarget = false;

        GameObject nameTxtGO = new GameObject("Text");
        nameTxtGO.transform.SetParent(nameGO.transform, false);
        RectTransform ntRT = nameTxtGO.AddComponent<RectTransform>();
        ntRT.anchorMin = Vector2.zero;
        ntRT.anchorMax = Vector2.one;
        ntRT.sizeDelta = Vector2.zero;
        Text nameTxt = nameTxtGO.AddComponent<Text>();
        nameTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameTxt.fontSize = 12;
        nameTxt.fontStyle = FontStyle.Bold;
        nameTxt.alignment = TextAnchor.MiddleCenter;
        nameTxt.raycastTarget = false;

        // 挂载控制器
        HarvestItemNode node = go.AddComponent<HarvestItemNode>();
        node.nodeRect = rt;
        node.iconImage = iconImg;
        node.glowRingImage = glowImg;
        node.borderRingImage = borderImg;
        node.iconRect = iconRT;
        node.glowRect = glowRT;
        node.badgeGO = badgeGO;
        node.badgeText = badgeTxt;
        node.nameTagGO = nameGO;
        node.nameText = nameTxt;
        node.onHoverEnter = ShowHoverTooltip;
        node.onHoverExit = (n) => HideHoverTooltip();

        return go;
    }

    private Sprite CreateSoftCircleSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color centerCol = new Color(1f, 1f, 1f, 0.7f);
        Color transparent = new Color(1f, 1f, 1f, 0f);

        float center = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                if (dist < 1f)
                {
                    float alpha = Mathf.SmoothStep(1f, 0f, dist);
                    tex.SetPixel(x, y, new Color(centerCol.r, centerCol.g, centerCol.b, centerCol.a * alpha));
                }
                else
                {
                    tex.SetPixel(x, y, transparent);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreateCapsuleSprite(int width, int height, Color fillColor)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float radius = height * 0.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (x < radius)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius));
                    tex.SetPixel(x, y, dist <= radius ? fillColor : Color.clear);
                }
                else if (x > width - radius)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(width - radius, radius));
                    tex.SetPixel(x, y, dist <= radius ? fillColor : Color.clear);
                }
                else
                {
                    tex.SetPixel(x, y, fillColor);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }
}
