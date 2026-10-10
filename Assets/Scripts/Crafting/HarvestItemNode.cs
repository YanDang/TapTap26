using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using BiomechanicalCrafting;

/// <summary>
/// 药剂工艺式生体温室采集节点 (Harvest Item Node)
/// 支持鼠标点击、长按划过 (Swipe)、触摸拖曳一笔全收。
/// 具备生体呼吸悬浮动效、元素发光晕轮、采摘飞入收纳袋动效、空间不足拒收抖动与飘字反馈。
/// </summary>
public class HarvestItemNode : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    public Image iconImage;
    public Image glowRingImage;
    public RectTransform nodeRect;

    [System.NonSerialized] public Image borderRingImage;
    [System.NonSerialized] public GameObject badgeGO;
    [System.NonSerialized] public Text badgeText;
    [System.NonSerialized] public GameObject nameTagGO;
    [System.NonSerialized] public Text nameText;
    [System.NonSerialized] public RectTransform iconRect;
    [System.NonSerialized] public RectTransform glowRect;

    [Header("Data")]
    public BiomechanicalMaterial materialData;
    public bool isHarvested = false;

    // 悬停交互事件
    public Action<HarvestItemNode> onHoverEnter;
    public Action<HarvestItemNode> onHoverExit;

    // 动效参数
    private Vector2 baseAnchoredPosition;
    private float floatPhase = 0f;
    private float floatSpeed = 2.5f;
    private float floatAmplitude = 8f;
    private bool isHovered = false;
    private bool isShaking = false;

    // 采摘回调委托：返回 true 表示成功放入背包并允许采摘；返回 false 表示背包已满，拒绝采摘
    private Func<HarvestItemNode, bool> onHarvestAttempt;

    public void Setup(BiomechanicalMaterial material, Vector2 pos, Func<HarvestItemNode, bool> onAttempt)
    {
        materialData = material;
        baseAnchoredPosition = pos;
        onHarvestAttempt = onAttempt;
        isHarvested = false;
        isShaking = false;

        if (nodeRect == null) nodeRect = GetComponent<RectTransform>();
        nodeRect.anchoredPosition = pos;

        floatPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        floatSpeed = UnityEngine.Random.Range(1.8f, 2.8f);
        floatAmplitude = UnityEngine.Random.Range(5f, 10f);

        // 设置图标与精灵
        if (iconImage != null && materialData != null)
        {
            Sprite s = materialData.GetOrLoadSprite();
            if (s != null)
            {
                iconImage.sprite = s;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.enabled = false;
            }
        }

        // 核心骨架 (2x2) vs 生体辅料 (1x1) 的尺寸与外观视觉分级
        if (materialData != null)
        {
            if (materialData.isMajor)
            {
                // 2x2 骨架核心：明显大号尺寸、双层金色微光外环、醒目核心角标
                if (nodeRect != null) nodeRect.sizeDelta = new Vector2(96, 96);
                if (iconRect != null) iconRect.sizeDelta = new Vector2(80, 80);
                if (glowRect != null) glowRect.sizeDelta = new Vector2(136, 136);

                if (glowRingImage != null)
                {
                    Color goldGlow = new Color(0.98f, 0.85f, 0.25f, 0.82f);
                    glowRingImage.color = goldGlow;
                }
                if (borderRingImage != null)
                {
                    borderRingImage.gameObject.SetActive(true);
                    borderRingImage.color = new Color(0.98f, 0.82f, 0.18f, 0.95f);
                }
                if (badgeText != null)
                {
                    badgeText.text = "🦴 核心 2x2";
                    badgeText.color = new Color(0.98f, 0.88f, 0.35f);
                }
                if (nameText != null)
                {
                    nameText.text = $"[🦴] {materialData.materialName}";
                    nameText.color = new Color(0.98f, 0.88f, 0.45f);
                }
            }
            else
            {
                // 1x1 生体辅料：精致小巧尺寸、对应元素柔和光晕、轻巧元素角标
                if (nodeRect != null) nodeRect.sizeDelta = new Vector2(74, 74);
                if (iconRect != null) iconRect.sizeDelta = new Vector2(60, 60);
                if (glowRect != null) glowRect.sizeDelta = new Vector2(98, 98);

                if (glowRingImage != null)
                {
                    Color elemCol = materialData.ElementColor;
                    elemCol.a = 0.65f;
                    glowRingImage.color = elemCol;
                }
                if (borderRingImage != null)
                {
                    borderRingImage.gameObject.SetActive(false);
                }
                if (badgeText != null)
                {
                    string elemSymbol = materialData.ElementEnum switch
                    {
                        BiomechanicalElement.Electric => "⚡ 电",
                        BiomechanicalElement.Gas => "💨 气",
                        BiomechanicalElement.Wood => "🌿 木",
                        BiomechanicalElement.Water => "💧 水",
                        _ => "辅料"
                    };
                    badgeText.text = $"{elemSymbol} · 辅料";
                    badgeText.color = materialData.ElementColor;
                }
                if (nameText != null)
                {
                    string prefix = materialData.ElementEnum switch
                    {
                        BiomechanicalElement.Electric => "⚡",
                        BiomechanicalElement.Gas => "💨",
                        BiomechanicalElement.Wood => "🌿",
                        BiomechanicalElement.Water => "💧",
                        _ => "✦"
                    };
                    nameText.text = $"[{prefix}] {materialData.materialName}";
                    nameText.color = Color.white;
                }
            }
        }

        // 入场萌发缩放动效
        transform.localScale = Vector3.zero;
        StartCoroutine(SproutAnimation());
    }

    private IEnumerator SproutAnimation()
    {
        float dur = 0.45f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            // 弹性回弹曲线
            float scale = Mathf.Sin(t * Mathf.PI * 0.7f) * 1.15f;
            if (t > 0.8f) scale = Mathf.Lerp(1.15f, 1f, (t - 0.8f) / 0.2f);
            transform.localScale = Vector3.one * scale;
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    void Update()
    {
        if (isHarvested) return;

        // 柔和的生体悬浮呼吸动效 (非拒绝抖动状态下，采用 unscaledTime 确保时停下温室自然呼吸)
        if (!isShaking)
        {
            float yOffset = Mathf.Sin(Time.unscaledTime * floatSpeed + floatPhase) * floatAmplitude;
            nodeRect.anchoredPosition = new Vector2(baseAnchoredPosition.x, baseAnchoredPosition.y + yOffset);
        }

        // 悬停缩放微调
        float targetScale = isHovered ? 1.2f : 1f;
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, Time.unscaledDeltaTime * 12f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TryHarvest();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        if (!isHarvested)
        {
            onHoverEnter?.Invoke(this);
        }

        // 药剂工艺核心手感：如果鼠标左键处于按住拖曳状态，划过即收！
        if (Input.GetMouseButton(0))
        {
            TryHarvest();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        onHoverExit?.Invoke(this);
    }

    /// <summary>
    /// 触发采摘逻辑：若背包未满，顺利采摘；若背包已满，拒绝采摘并产生拒收顿感
    /// </summary>
    public void TryHarvest()
    {
        if (isHarvested || materialData == null) return;

        bool canHarvest = true;
        if (onHarvestAttempt != null)
        {
            canHarvest = onHarvestAttempt(this);
        }

        if (canHarvest)
        {
            isHarvested = true;
            onHoverExit?.Invoke(this);
            if (badgeGO != null) badgeGO.SetActive(false);
            if (nameTagGO != null) nameTagGO.SetActive(false);
        }
        else
        {
            // 背包空间不足，拒绝采摘，触发拒收横向抖动
            if (!isShaking)
            {
                StartCoroutine(RefusalShakeRoutine());
            }
        }
    }

    private IEnumerator RefusalShakeRoutine()
    {
        isShaking = true;
        float dur = 0.25f;
        float elapsed = 0f;
        Vector2 origPos = nodeRect.anchoredPosition;

        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float xOffset = Mathf.Sin(elapsed * 60f) * 8f * (1f - t);
            nodeRect.anchoredPosition = new Vector2(origPos.x + xOffset, origPos.y);
            yield return null;
        }

        nodeRect.anchoredPosition = origPos;
        isShaking = false;
    }

    /// <summary>
    /// 飞向收纳目标位置的收割动效
    /// </summary>
    public void FlyToDestination(Vector3 targetWorldPos, Action onFinished)
    {
        StartCoroutine(FlyRoutine(targetWorldPos, onFinished));
    }

    private IEnumerator FlyRoutine(Vector3 targetWorldPos, Action onFinished)
    {
        if (badgeGO != null) Destroy(badgeGO);
        if (nameTagGO != null) Destroy(nameTagGO);
        if (borderRingImage != null) borderRingImage.enabled = false;
        if (glowRingImage != null) glowRingImage.enabled = false;

        // 瞬间轻微膨胀，产生“拔出/成熟摘落”的清脆视觉顿感
        Vector3 initialWorldPos = transform.position;
        Vector3 startScale = transform.localScale;

        float punchDur = 0.12f;
        float elapsed = 0f;
        while (elapsed < punchDur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / punchDur);
            transform.localScale = Vector3.Lerp(startScale, startScale * 1.35f, t);
            yield return null;
        }

        // 抛物弧线飞入收纳袋
        float flyDur = 0.42f;
        elapsed = 0f;
        Vector3 midControl = (initialWorldPos + targetWorldPos) * 0.5f + Vector3.up * 80f;

        while (elapsed < flyDur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / flyDur);
            // 二次贝塞尔曲线
            Vector3 currentPos = Mathf.Pow(1 - t, 2) * initialWorldPos + 2 * (1 - t) * t * midControl + Mathf.Pow(t, 2) * targetWorldPos;
            transform.position = currentPos;
            transform.localScale = Vector3.Lerp(startScale * 1.35f, Vector3.zero, t * t);

            yield return null;
        }

        onFinished?.Invoke();
        Destroy(gameObject);
    }
}
