using UnityEngine;

/// <summary>
/// 6. 高压自愈喷水炮 (furn_hydro_cannon)
/// - 分类：水气武器 | 主导元素：Water | 主题色：高光苍青 (0.15, 0.82, 0.88)
/// - 机制：
///   * 自动警戒：朝 shotRange 内最近敌人发射高压水流喷射刃（CD 2.2s，电网超频下 1.65s）
///   * 强力击退破防：水刃命中造成伤害并将敌人击退数米，瞬间扣除 40 点韧性破防
/// </summary>
public class HydroCannonAuraBehavior : BiomechanicalAuraBase
{
    [Header("Cannon Stats")]
    public float shotRange = 6.2f;
    public float blastImpact = 35f;
    public float cooldown = 2.2f;

    public bool isOverclocked = false;
    private float fireTimer = 0f;

    protected override void Awake()
    {
        base.Awake();
        CreateRangeRing(shotRange, new Color(0.15f, 0.82f, 0.88f, 0.35f), "Cannon_Range_Ring");
    }

    void Update()
    {
        if (parentFurniture != null && parentFurniture.currentState != FurnitureObject.FurnitureState.Placed)
        {
            if (rangeRing != null && rangeRing.activeSelf) rangeRing.SetActive(false);
            return;
        }

        if (rangeRing != null && !rangeRing.activeSelf) rangeRing.SetActive(true);
        UpdateRingPulse(2.5f, 0.05f);

        // 检测是否在电鳗电机的常驻电网中（攻速提升 25%）
        isOverclocked = EelGeneratorAuraBehavior.IsPositionPowered(transform.position);

        fireTimer -= Time.deltaTime;
        if (fireTimer <= 0f)
        {
            float actualCd = isOverclocked ? (cooldown * 0.75f) : cooldown;
            if (TryFireAtNearestEnemy())
            {
                fireTimer = actualCd;
            }
            else
            {
                fireTimer = 0.3f; // 未索到敌，快速轮询
            }
        }
    }

    private bool TryFireAtNearestEnemy()
    {
        var enemies = EnemyController.AllEnemies;
        EnemyController nearest = null;
        float minDist = shotRange;

        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e != null && e.IsAlive && !e.isPacified)
            {
                float d = Vector2.Distance(transform.position, e.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = e;
                }
            }
        }

        if (nearest != null)
        {
            Vector3 shootDir = (nearest.transform.position - transform.position).normalized;
            SpawnWaterBlade(shootDir);
            return true;
        }

        return false;
    }

    private void SpawnWaterBlade(Vector3 dir)
    {
        GameObject bladeGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bladeGO.name = "Water_Blade";
        bladeGO.transform.position = transform.position + dir * 0.5f + Vector3.up * 0.2f;
        bladeGO.transform.localScale = new Vector3(0.7f, 0.35f, 1f);

        // 旋转朝向目标
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        bladeGO.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Destroy(bladeGO.GetComponent<Collider>());

        var mr = bladeGO.GetComponent<MeshRenderer>();
        var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
        if (unlitShader != null) mr.material = new Material(unlitShader);
        mr.material.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        mr.sortingOrder = 25;

        var proj = bladeGO.AddComponent<WaterBladeProjectile>();
        proj.direction = dir;
        proj.damage = 32f;
        proj.guardBreak = 40f;
        proj.knockbackPower = blastImpact;

        if (DamageTextManager.Instance != null && isOverclocked)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.6f, "⚡ 水刃超频发射!", new Color(0.3f, 0.9f, 1f), 0.08f);
        }
    }
}
