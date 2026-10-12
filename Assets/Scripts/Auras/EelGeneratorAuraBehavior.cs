using System.Collections.Generic;
using UnityEngine;
using GameJam;

/// <summary>
/// 1. 电鳗电机发电机组 (furn_eel_generator)
/// - 分类：电网设施 | 主导元素：Electric | 主题色：暖黄电弧 (0.98, 0.82, 0.18)
/// - 机制与特效（对齐 gamejam-integration，数值与协同以主项目为主）：
///   * 地面黄色电弧闪烁：放电瞬间生成地面旋转电弧 (Furniture/mud_spark)
///   * 充能高频蜂鸣抖动：越接近放电，模型微颤抖动越剧烈，地圈高光越炽热
///   * 静电反制与电涌：靠近半径内的敌怪，每 1.5 秒受到一次电弧跳跃伤害，附加 1.0 秒麻痹
///   * 水电协同【湿身导电】：对身上有水渍 (IsWet) 的敌人触发额外 50% 导电伤害与感电火花
///   * 常驻供电：以自身为圆心半径 4.5m 形成电网供电区，赋能附近造水塔与喷水炮
/// </summary>
public class EelGeneratorAuraBehavior : BiomechanicalAuraBase
{
    [Header("Generator Stats")]
    public float paralyzeRadius = 3.2f;
    public int shockDmg = 28;
    public float powerGridRadius = 4.5f;
    public float interval = 1.5f;

    private float timer = 0f;
    private float flashTimer = 0f;
    private SpriteRenderer arcRenderer;
    private Vector3 originalVisualScale = Vector3.one;

    public static readonly List<EelGeneratorAuraBehavior> ActiveGenerators = new List<EelGeneratorAuraBehavior>();
    static readonly Color ArcColor = new Color(1f, 0.92f, 0.35f);

    protected override void Awake()
    {
        base.Awake();
        CreateRangeRing(paralyzeRadius, new Color(0.98f, 0.82f, 0.18f, 0.35f), "Electric_Aura_Ring");
        SetupVisualEffects();
    }

    private void SetupVisualEffects()
    {
        // 地面电弧贴图 (Resources/Furniture/mud_spark)
        var sparkSprite = Resources.Load<Sprite>("Furniture/mud_spark");
        if (sparkSprite != null)
        {
            var arcGO = new GameObject("Ground_Electric_Arc");
            arcGO.transform.SetParent(transform, false);
            arcGO.transform.localPosition = new Vector3(0f, -0.04f, 0.04f);
            arcRenderer = arcGO.AddComponent<SpriteRenderer>();
            arcRenderer.sprite = sparkSprite;
            arcRenderer.sortingOrder = 4;
            arcRenderer.color = Color.clear;
            float arcSize = paralyzeRadius * 1.8f;
            arcGO.transform.localScale = Vector3.one * (arcSize / Mathf.Max(sparkSprite.bounds.size.x, 0.1f));
        }

        if (parentFurniture != null && parentFurniture.furnitureRenderer != null)
        {
            originalVisualScale = parentFurniture.furnitureRenderer.transform.localScale;
        }
    }

    void OnEnable()
    {
        if (!ActiveGenerators.Contains(this))
            ActiveGenerators.Add(this);
    }

    void OnDisable()
    {
        ActiveGenerators.Remove(this);
    }

    void Update()
    {
        bool isPlaced = (parentFurniture != null && parentFurniture.currentState == FurnitureObject.FurnitureState.Placed);

        if (!isPlaced)
        {
            if (rangeRing != null && rangeRing.activeSelf) rangeRing.SetActive(false);
            if (arcRenderer != null && arcRenderer.enabled) arcRenderer.enabled = false;
            return;
        }

        if (rangeRing != null && !rangeRing.activeSelf) rangeRing.SetActive(true);

        float dt = Time.deltaTime;
        float k = Mathf.Clamp01(timer / interval);

        // 充能抖动与地圈高光动态 (来自 gamejam-integration)
        if (parentFurniture != null && parentFurniture.furnitureRenderer != null)
        {
            float buzz = Mathf.Sin(Time.time * 60f) * 0.035f * k * k;
            parentFurniture.furnitureRenderer.transform.localScale = new Vector3(
                originalVisualScale.x * (1f + buzz + flashTimer * 0.15f),
                originalVisualScale.y * (1f - buzz + flashTimer * 0.1f),
                originalVisualScale.z
            );
        }

        // 光圈充能越满越亮
        if (rangeRingRenderer != null)
        {
            float alpha = Mathf.Lerp(0.2f, 0.65f, k) + (flashTimer > 0f ? 0.3f : 0f);
            rangeRingRenderer.color = new Color(0.98f, 0.85f, 0.2f, alpha);
        }

        // 电弧闪烁衰减
        if (flashTimer > 0f)
        {
            flashTimer = Mathf.MoveTowards(flashTimer, 0f, dt * 4f);
            if (arcRenderer != null)
            {
                arcRenderer.enabled = true;
                arcRenderer.color = new Color(ArcColor.r, ArcColor.g, ArcColor.b, flashTimer * 0.9f);
                arcRenderer.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            }
        }
        else if (arcRenderer != null && arcRenderer.enabled)
        {
            arcRenderer.enabled = false;
        }

        UpdateRingPulse(4.2f, 0.05f);

        // 建造模式/战术时停期间暂停攻击判定
        if (IsCombatPaused) return;

        timer += dt;
        if (timer >= interval)
        {
            timer = 0f;
            TriggerElectricPulse();
        }
    }

    private void TriggerElectricPulse()
    {
        flashTimer = 1.0f;
        bool anyHit = false;

        // 查找半径内的所有活体敌人
        var enemies = EnemyController.AllEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e != null && e.IsAlive && !e.isPacified)
            {
                float d = Vector2.Distance(transform.position, e.transform.position);
                if (d <= paralyzeRadius)
                {
                    anyHit = true;
                    int finalDmg = shockDmg;
                    if (e.IsWet)
                    {
                        finalDmg += Mathf.Max(1, Mathf.RoundToInt(shockDmg * 0.5f)); // 湿身多受 50% 伤害
                    }
                    e.TakeDamage(finalDmg, 25f, transform.position);
                    e.ApplyParalysis(1.0f);

                    // 电弧命中 Spark 特效 (来自 gamejam-integration)
                    Vfx.Spawn("VFX_ParrySpark", e.transform.position + Vector3.up * 0.8f, Random.Range(0f, 360f), 1.1f, false, null, ArcColor);

                    if (DamageTextManager.Instance != null)
                    {
                        string label = e.IsWet ? $"⚡ 湿身感电 -{finalDmg}!" : $"⚡ 电弧跳跃 -{finalDmg}!";
                        DamageTextManager.Instance.ShowText(e.transform.position + Vector3.up * 0.7f, label, new Color(1f, 0.88f, 0.2f), 0.11f);
                    }
                }
            }
        }

        if (anyHit)
        {
            CameraRig.I?.Shake(0.15f, 0.15f);
            Sfx.Play("sfx_parry", 0.8f);
        }
    }

    /// <summary>
    /// 检测某世界坐标是否处于任意电鳗电机的常驻电网供电范围内 (4.5m)
    /// </summary>
    public static bool IsPositionPowered(Vector3 pos)
    {
        for (int i = 0; i < ActiveGenerators.Count; i++)
        {
            var gen = ActiveGenerators[i];
            if (gen != null && gen.parentFurniture != null && gen.parentFurniture.currentState == FurnitureObject.FurnitureState.Placed)
            {
                if (Vector2.Distance(pos, gen.transform.position) <= gen.powerGridRadius)
                    return true;
            }
        }
        return false;
    }
}
