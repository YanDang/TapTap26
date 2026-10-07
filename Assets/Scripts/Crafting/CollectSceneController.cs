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

    // 运行态数据
    private int currentDay = 1;
    private int todayHarvestedCount = 0;
    private List<HarvestItemNode> activeNodes = new List<HarvestItemNode>();

    // 经过精细校准的 15 个自然温室挂载点坐标（完美契合 Potion Craft 风格背景中的枝头、架子、挂罐、地槽）
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

    void Awake()
    {
        Instance = this;
        InitAudio();
    }

    void Start()
    {
        InitButtons();
        SproutDay(currentDay);
    }

    private void InitAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        popClip = CreateSoftPopClip();
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

    private void InitButtons()
    {
        if (btnNextDay != null)
        {
            btnNextDay.onClick.AddListener(OnNextDayClicked);
        }

        if (btnGoToCrafting != null)
        {
            btnGoToCrafting.onClick.AddListener(() =>
            {
                // 跳转到背壳合成场景
                SceneManager.LoadScene("Mix");
            });
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
        // 柔和破晓淡入淡出动效
        if (dayTransitionFadeGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < 0.25f)
            {
                elapsed += Time.deltaTime;
                dayTransitionFadeGroup.alpha = elapsed / 0.25f;
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
                elapsed += Time.deltaTime;
                dayTransitionFadeGroup.alpha = 1f - (elapsed / 0.35f);
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

        node.Setup(mat, pos, OnItemHarvested);
        activeNodes.Add(node);
    }

    /// <summary>
    /// 当玩家采摘（点击或划过）某个材料
    /// </summary>
    private void OnItemHarvested(HarvestItemNode node)
    {
        if (node == null || node.materialData == null) return;

        // 播放采摘声
        if (audioSource != null && popClip != null)
        {
            audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.15f);
            audioSource.PlayOneShot(popClip);
        }

        todayHarvestedCount++;

        // 记录到全局跨场景持久化数据中
        PlayerSessionData.gatheredBiomechanicalMaterials.Add(node.materialData);

        // 弹出清脆的生体浮动飘字提示
        SpawnFloatingText(node.transform.position, node.materialData);

        // 飞向右上角收纳篮
        Vector3 targetPos = basketIcon != null ? basketIcon.transform.position : (Vector3.right * 750 + Vector3.up * 450);
        node.FlyToDestination(targetPos, () =>
        {
            // 击中篮子时的弹跳轻震动
            StartCoroutine(PunchBasketIcon());
            UpdateHeaderUI();
        });
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
            elapsed += Time.deltaTime;
            float progress = elapsed / dur;
            float scale = 1f + Mathf.Sin(progress * Mathf.PI) * 0.28f;
            t.localScale = orig * scale;
            yield return null;
        }
        t.localScale = orig;
    }

    private void SpawnFloatingText(Vector3 worldPos, BiomechanicalMaterial mat)
    {
        if (floatingTextContainer == null) return;

        GameObject txtGO = new GameObject("FloatingText");
        txtGO.transform.SetParent(floatingTextContainer, false);
        txtGO.transform.position = worldPos + Vector3.up * 40f;

        Text t = txtGO.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 22;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;

        string colHex = ColorUtility.ToHtmlStringRGB(mat.ElementColor);
        t.text = $"<color=#{colHex}>+1 {mat.materialName}</color> <color=#DDDDDD>({mat.GetCompactDeltaString()})</color>";

        // 上浮并淡出动效
        StartCoroutine(FloatAndFadeRoutine(txtGO, t));
    }

    private IEnumerator FloatAndFadeRoutine(GameObject go, Text t)
    {
        float dur = 0.85f;
        float elapsed = 0f;
        Vector3 startPos = go.transform.position;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / dur;
            go.transform.position = startPos + Vector3.up * (p * 60f);

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

        int totalCount = PlayerSessionData.gatheredBiomechanicalMaterials.Count;
        if (basketCountText != null)
        {
            basketCountText.text = $"{totalCount}";
        }
    }

    /// <summary>
    /// 纯代码构建默认的 ItemNode 视觉层次（发光底环 + 居中精灵图）
    /// </summary>
    private GameObject CreateDefaultNodeGameObject()
    {
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
        glowImg.sprite = CreateSoftCircleSprite();

        // 2. 核心材料图标 (Icon)
        GameObject iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(go.transform, false);
        RectTransform iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.sizeDelta = new Vector2(72, 72);
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.raycastTarget = true; // 接收点击与划动事件
        iconImg.preserveAspect = true;

        HarvestItemNode node = go.AddComponent<HarvestItemNode>();
        node.nodeRect = rt;
        node.iconImage = iconImg;
        node.glowRingImage = glowImg;

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
}
