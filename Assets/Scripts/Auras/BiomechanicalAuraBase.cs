using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BiomechanicalCrafting;

/// <summary>
/// 生体机械构装战术光环基类：
/// 统一管理地面范围高亮光环指示圈、荧光呼吸脉冲与实体关联
/// </summary>
public abstract class BiomechanicalAuraBase : MonoBehaviour
{
    [Header("Base References")]
    public FurnitureObject parentFurniture;
    public CraftedProduct productData;

    protected GameObject rangeRing;
    protected SpriteRenderer rangeRingRenderer;
    protected Material ringMaterial;
    protected float baseRingRadius = 3f;

    protected virtual void Awake()
    {
        if (parentFurniture == null)
            parentFurniture = GetComponent<FurnitureObject>();
    }

    /// <summary>
    /// 创建地面等距投影战术光环圈
    /// </summary>
    protected GameObject CreateRangeRing(float radius, Color ringColor, string ringName = "Aura_Range_Ring")
    {
        baseRingRadius = radius;
        GameObject ringGO = new GameObject(ringName);
        ringGO.transform.SetParent(transform, false);
        ringGO.transform.localPosition = new Vector3(0f, -0.05f, 0.05f);

        // target_ring 原生即为 128x64 (2:1 等距椭圆)，等比缩放 2*radius 即可在战场上完美呈现半径为 radius 的等距范围圈
        ringGO.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

        rangeRingRenderer = ringGO.AddComponent<SpriteRenderer>();

        // 尝试加载圆环精灵，若无则使用 Resources 中的目标环
        var circleSprite = Resources.Load<Sprite>("target_ring") ?? Resources.Load<Sprite>("click_marker");
        if (circleSprite != null)
        {
            rangeRingRenderer.sprite = circleSprite;
        }

        var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
        if (unlitShader != null)
        {
            ringMaterial = new Material(unlitShader);
            rangeRingRenderer.material = ringMaterial;
        }

        rangeRingRenderer.color = ringColor;
        rangeRingRenderer.sortingOrder = 3; // 贴在地面瓷砖上方，低于角色与家具

        rangeRing = ringGO;
        return ringGO;
    }

    /// <summary>
    /// 动态刷新光环半径与等距缩放比例
    /// </summary>
    public virtual void SetRingRadius(float newRadius)
    {
        baseRingRadius = newRadius;
        if (rangeRing != null)
        {
            rangeRing.transform.localScale = new Vector3(baseRingRadius * 2f, baseRingRadius * 2f, 1f);
        }
    }

    /// <summary>
    /// 光环温和呼吸微动效果
    /// </summary>
    protected void UpdateRingPulse(float frequency = 3.5f, float amplitude = 0.06f)
    {
        if (rangeRing == null || !rangeRing.activeSelf) return;

        float sin = Mathf.Sin(Time.time * frequency) * amplitude;
        float currentScale = (baseRingRadius * 2f) * (1f + sin);
        rangeRing.transform.localScale = new Vector3(currentScale, currentScale, 1f);
    }
}
