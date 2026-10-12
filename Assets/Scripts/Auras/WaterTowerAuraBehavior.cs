using UnityEngine;
using GameJam;

/// <summary>
/// 4. 脉动造水循环塔 (furn_water_tower)
/// - 分类：潮润设施 | 主导元素：Water | 主题色：潮润青蓝 (0.22, 0.74, 0.97)
/// - 机制与特效（对齐 gamejam-integration，数值与协同以主项目为主）：
///   * 地面积水与涟漪光圈：地面积水贴图 (Furniture/water_puddle) + 动态扩散双环水波 (UI/ui_ring)
///   * 心跳泵水形变：喷水前深吸气挤压、喷洒瞬间绽放水花粒子与波纹
///   * 持续造水浸染：向地面脉动喷洒活水，进入敌人减速 slowPercent% 并赋予 IsWet 潮湿状态
///   * 生态滋润协同：处于水塔水汽范围内的增生珊瑚墙自愈速度翻倍 (Moist)
///   * 【元素协同·感电暴击】：若水渍地表与【电鳗电机】电网重叠，转为带电水潭，
///     敌人受到电击伤害提升 200%（造成3倍伤害），且破防/瘫痪蓄积加倍！
/// </summary>
public class WaterTowerAuraBehavior : BiomechanicalAuraBase
{
    [Header("Water Spray Stats")]
    public float sprayRadius = 3.8f;
    public int slowPercent = 45;
    public float pulseInterval = 1.8f;

    [Header("Synergy State")]
    public bool isElectrifiedPuddle = false;
    private float pulseTimer = 0f;
    private float damageTickTimer = 0f;
    private float beatTimer = 0f;

    // Visuals & Effects (from gamejam-integration)
    private SpriteRenderer puddleRenderer;
    private SpriteRenderer[] waveRenderers;
    private Vector3 originalVisualScale = Vector3.one;

    protected override void Awake()
    {
        base.Awake();
        CreateRangeRing(sprayRadius, new Color(0.22f, 0.74f, 0.97f, 0.35f), "Water_Spray_Puddle");
        SetupVisualEffects();
    }

    private void SetupVisualEffects()
    {
        // 1. 地面真实积水贴图 (Resources/Furniture/water_puddle)
        var puddleSprite = Resources.Load<Sprite>("Furniture/water_puddle");
        if (puddleSprite != null)
        {
            var pGO = new GameObject("Puddle_Water");
            pGO.transform.SetParent(transform, false);
            pGO.transform.localPosition = new Vector3(0f, -0.04f, 0.04f);
            puddleRenderer = pGO.AddComponent<SpriteRenderer>();
            puddleRenderer.sprite = puddleSprite;
            puddleRenderer.sortingOrder = 2; // 地表之上、光环之下
            puddleRenderer.color = new Color(0.35f, 0.75f, 0.98f, 0.45f);
            float pSize = sprayRadius * 2.1f;
            pGO.transform.localScale = Vector3.one * (pSize / Mathf.Max(puddleSprite.bounds.size.x, 0.1f));
        }

        // 2. 动态扩散双层水波涟漪环 (Resources/UI/ui_ring)
        var ringSprite = Resources.Load<Sprite>("UI/ui_ring");
        if (ringSprite != null)
        {
            waveRenderers = new SpriteRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                var wGO = new GameObject($"Water_Wave_{i}");
                wGO.transform.SetParent(transform, false);
                wGO.transform.localPosition = new Vector3(0f, -0.035f + i * 0.002f, 0.03f);
                var wr = wGO.AddComponent<SpriteRenderer>();
                wr.sprite = ringSprite;
                wr.sortingOrder = 4;
                wr.color = Color.clear;
                waveRenderers[i] = wr;
            }
        }

        if (parentFurniture != null && parentFurniture.furnitureRenderer != null)
        {
            originalVisualScale = parentFurniture.furnitureRenderer.transform.localScale;
        }
    }

    void Update()
    {
        bool isPlaced = (parentFurniture != null && parentFurniture.currentState == FurnitureObject.FurnitureState.Placed);

        if (!isPlaced)
        {
            if (rangeRing != null && rangeRing.activeSelf) rangeRing.SetActive(false);
            if (puddleRenderer != null && puddleRenderer.enabled) puddleRenderer.enabled = false;
            if (waveRenderers != null)
            {
                for (int i = 0; i < waveRenderers.Length; i++)
                    if (waveRenderers[i] != null) waveRenderers[i].enabled = false;
            }
            return;
        }

        if (rangeRing != null && !rangeRing.activeSelf) rangeRing.SetActive(true);
        if (puddleRenderer != null && !puddleRenderer.enabled) puddleRenderer.enabled = true;
        if (waveRenderers != null)
        {
            for (int i = 0; i < waveRenderers.Length; i++)
                if (waveRenderers[i] != null) waveRenderers[i].enabled = true;
        }

        // 实时检测是否处于电鳗电机供电网中（触发水电协同）
        bool nowPowered = EelGeneratorAuraBehavior.IsPositionPowered(transform.position);
        if (nowPowered != isElectrifiedPuddle)
        {
            isElectrifiedPuddle = nowPowered;
            if (rangeRingRenderer != null)
            {
                rangeRingRenderer.color = isElectrifiedPuddle 
                    ? new Color(0.35f, 0.95f, 1f, 0.65f) 
                    : new Color(0.22f, 0.74f, 0.97f, 0.35f);
            }
            if (puddleRenderer != null)
            {
                puddleRenderer.color = isElectrifiedPuddle
                    ? new Color(0.5f, 0.95f, 1f, 0.65f)
                    : new Color(0.35f, 0.75f, 0.98f, 0.45f);
            }
        }

        UpdateRingPulse(2.8f, 0.04f);

        // 建造模式/战术时停期间：保持光环范围圈显示供布防参考，但暂停水气脉冲！
        if (IsCombatPaused) return;

        float dt = Time.deltaTime;
        pulseTimer += dt;

        // 扩散波纹动画 (来自 gamejam-integration)
        if (waveRenderers != null)
        {
            for (int i = 0; i < waveRenderers.Length; i++)
            {
                var wr = waveRenderers[i];
                if (wr != null && wr.sprite != null)
                {
                    float k = Mathf.Repeat(pulseTimer / pulseInterval + i * 0.5f, 1f);
                    float curRadius = Mathf.Lerp(0.8f, sprayRadius * 2.0f, k);
                    wr.transform.localScale = Vector3.one * (curRadius / Mathf.Max(wr.sprite.bounds.size.x, 0.1f));
                    float alpha = (1f - k) * (isElectrifiedPuddle ? 0.6f : 0.35f);
                    wr.color = isElectrifiedPuddle ? new Color(0.4f, 0.95f, 1f, alpha) : new Color(0.45f, 0.85f, 1f, alpha);
                }
            }
        }

        // 心跳节律挤压动画 (来自 gamejam-integration)
        if (parentFurniture != null && parentFurniture.furnitureRenderer != null)
        {
            beatTimer = Mathf.MoveTowards(beatTimer, 0f, dt * 2.5f);
            float squeeze = Mathf.Sin(beatTimer * Mathf.PI) * 0.12f;
            parentFurniture.furnitureRenderer.transform.localScale = new Vector3(
                originalVisualScale.x * (1f + squeeze),
                originalVisualScale.y * (1f - squeeze * 0.6f),
                originalVisualScale.z
            );
        }

        // 脉动喷水周期到达
        if (pulseTimer >= pulseInterval)
        {
            pulseTimer = 0f;
            beatTimer = 1.0f;
            TriggerPulseSpray();
        }

        // 持续对水洼内的敌人进行减速与感电暴击判定
        damageTickTimer += dt;
        if (damageTickTimer >= 0.8f)
        {
            damageTickTimer = 0f;
            ApplyPuddleEffects();
        }
    }

    private void TriggerPulseSpray()
    {
        // 喷射水花粒子与音效 (来自 gamejam-integration)
        Vfx.Spawn("VFX_ParrySpark", transform.position + Vector3.up * 0.6f, Random.Range(0f, 360f), 1.0f, false, null, isElectrifiedPuddle ? new Color(0.4f, 0.95f, 1f) : new Color(0.6f, 0.85f, 1f));
        Sfx.Play("sfx_dodge", 0.25f);

        if (DamageTextManager.Instance != null && isElectrifiedPuddle)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.7f, "⚡ 水电协同·脉动带电水潭!", new Color(0.3f, 0.9f, 1f), 0.09f);
        }
    }

    private void ApplyPuddleEffects()
    {
        var enemies = EnemyController.AllEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e != null && e.IsAlive && !e.isPacified)
            {
                float d = Vector2.Distance(transform.position, e.transform.position);
                if (d <= sprayRadius)
                {
                    // 1. 基础水渍减速与淋湿状态
                    e.ApplySlow(slowPercent, 1.4f);
                    e.Wet(3.0f);

                    // 2. 水电协同【感电暴击】：电伤 +200%（3倍）且破防翻倍
                    if (isElectrifiedPuddle)
                    {
                        float baseShock = 22f;
                        float critShock = baseShock * 3.0f; // +200% 提升
                        e.TakeDamage(critShock, 40f, transform.position); // 双倍破防
                        e.ApplyParalysis(0.8f);

                        if (DamageTextManager.Instance != null)
                        {
                            DamageTextManager.Instance.ShowText(e.transform.position + Vector3.up * 0.6f, $"⚡ 感电暴击 -{critShock:0}!", new Color(1f, 0.85f, 0.15f), 0.12f);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 检测某世界坐标是否处于造水塔喷淋潮湿范围内
    /// </summary>
    public static bool IsPositionMoist(Vector3 pos)
    {
        var towers = FindObjectsOfType<WaterTowerAuraBehavior>();
        for (int i = 0; i < towers.Length; i++)
        {
            var t = towers[i];
            if (t != null && t.parentFurniture != null && t.parentFurniture.currentState == FurnitureObject.FurnitureState.Placed)
            {
                if (Vector2.Distance(pos, t.transform.position) <= t.sprayRadius) return true;
            }
        }
        return false;
    }
}
