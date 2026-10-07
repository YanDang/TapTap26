using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using BiomechanicalCrafting;

/// <summary>
/// 药剂工艺式生体温室采集节点 (Harvest Item Node)
/// 支持鼠标点击、长按划过 (Swipe)、触摸拖曳一笔全收。
/// 具备生体呼吸悬浮动效、元素发光晕轮、采摘飞入收纳袋动效与飘字反馈。
/// </summary>
public class HarvestItemNode : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
{
    [Header("UI References")]
    public Image iconImage;
    public Image glowRingImage;
    public RectTransform nodeRect;

    [Header("Data")]
    public BiomechanicalMaterial materialData;
    public bool isHarvested = false;

    // 动效参数
    private Vector2 baseAnchoredPosition;
    private float floatPhase = 0f;
    private float floatSpeed = 2.5f;
    private float floatAmplitude = 8f;
    private bool isHovered = false;
    private Action<HarvestItemNode> onHarvestedCallback;

    public void Setup(BiomechanicalMaterial material, Vector2 pos, Action<HarvestItemNode> onHarvested)
    {
        materialData = material;
        baseAnchoredPosition = pos;
        onHarvestedCallback = onHarvested;
        isHarvested = false;

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

        // 设置元素专属荧光晕轮
        if (glowRingImage != null && materialData != null)
        {
            Color elemCol = materialData.ElementColor;
            elemCol.a = 0.65f;
            glowRingImage.color = elemCol;
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
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
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

        // 柔和的生体悬浮呼吸动效
        float yOffset = Mathf.Sin(Time.time * floatSpeed + floatPhase) * floatAmplitude;
        nodeRect.anchoredPosition = new Vector2(baseAnchoredPosition.x, baseAnchoredPosition.y + yOffset);

        // 悬停缩放微调
        float targetScale = isHovered ? 1.2f : 1f;
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, Time.deltaTime * 12f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TryHarvest();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;

        // 药剂工艺核心手感：如果鼠标左键处于按住拖曳状态，划过即收！
        if (Input.GetMouseButton(0))
        {
            TryHarvest();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }

    /// <summary>
    /// 触发采摘逻辑
    /// </summary>
    public void TryHarvest()
    {
        if (isHarvested || materialData == null) return;
        isHarvested = true;

        if (onHarvestedCallback != null)
        {
            onHarvestedCallback(this);
        }
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
        // 瞬间轻微膨胀，产生“拔出/成熟摘落”的清脆视觉顿感
        Vector3 initialWorldPos = transform.position;
        Vector3 startScale = transform.localScale;

        float punchDur = 0.12f;
        float elapsed = 0f;
        while (elapsed < punchDur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / punchDur;
            transform.localScale = Vector3.Lerp(startScale, startScale * 1.35f, t);
            yield return null;
        }

        // 抛物弧线飞入收纳袋
        float flyDur = 0.42f;
        elapsed = 0f;
        Vector3 midControl = (initialWorldPos + targetWorldPos) * 0.5f + Vector3.up * 80f;

        while (elapsed < flyDur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flyDur;
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
