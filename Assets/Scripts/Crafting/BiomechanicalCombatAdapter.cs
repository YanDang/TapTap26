using System.Collections.Generic;
using UnityEngine;
using BiomechanicalCrafting;

/// <summary>
/// 战斗场景生体构装桥接适配器：
/// 负责将 PlayerSessionData 跨场景仓库中的 6 大家具与 4 大战术废料动态实例化为战场等距实体，
/// 并为其自动注入物理数值、生体荧光着色与战术光环机制。
/// </summary>
public class BiomechanicalCombatAdapter : MonoBehaviour
{
    public static BiomechanicalCombatAdapter Instance { get; private set; }

    [Header("Base Furniture Prefab")]
    public GameObject genericFurniturePrefab;

    [Header("Showcase Setup")]
    public bool autoSpawnShowcaseOnStart = false;

    void Awake()
    {
        Instance = this;

        if (genericFurniturePrefab == null)
        {
            genericFurniturePrefab = Resources.Load<GameObject>("Furniture_LavaTable");
#if UNITY_EDITOR
            if (genericFurniturePrefab == null)
            {
                genericFurniturePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Furniture_LavaTable.prefab");
            }
#endif
        }

        // 清理场景中残留的预置展示构装，确保开局战场纯净无冗余家具
        var preplaced = GameObject.Find("Preplaced_Biomechanical_Furnitures");
        if (preplaced != null)
        {
            Destroy(preplaced);
        }
        var furns = FindObjectsOfType<FurnitureObject>();
        foreach (var f in furns)
        {
            if (f != null && f.name.EndsWith("_Showcase"))
            {
                Destroy(f.gameObject);
            }
        }

        // 仓库开局为空，不再自动注入初始家具，全部由玩家在工坊中合成！
    }

    void Start()
    {
        if (autoSpawnShowcaseOnStart)
        {
            var activeAuras = FindObjectsOfType<BiomechanicalAuraBase>();
            if (activeAuras.Length == 0)
            {
                SpawnShowcaseArray();
            }
        }
    }

    public void SpawnShowcaseArray()
    {
        // 1. 水电协同回路：脉动水塔 + 电鳗发电机 (间距 ~1.8m，完美重叠形成感电水潭)
        SpawnConstructDirect("furn_water_tower", "精制·脉动造水循环塔", "潮润设施", false, 20, 15, 30, 80, new Color(0.22f, 0.74f, 0.97f), new Vector3(2.0f, 0.5f, 0f));
        SpawnConstructDirect("furn_eel_generator", "精制·电鳗电机发电机组", "电网设施", false, 15, 75, 30, 20, new Color(0.98f, 0.82f, 0.18f), new Vector3(3.2f, 1.3f, 0f));

        // 2. 增生防线与反冲陷阱：珊瑚墙阻断峡道，前方放置弹簧跳板与自爆雷
        SpawnConstructDirect("furn_coral_wall", "精制·增生珊瑚管排墙", "增生防线", false, 10, 25, 80, 20, new Color(0.29f, 0.85f, 0.44f), new Vector3(-2.2f, 1.4f, 0f));
        SpawnConstructDirect("furn_coral_wall", "精制·增生珊瑚管排墙", "增生防线", false, 10, 25, 80, 20, new Color(0.29f, 0.85f, 0.44f), new Vector3(-2.2f, 2.4f, 0f));
        SpawnConstructDirect("scrap_spasm_metal", "粗糙·抽搐的痉挛金属团", "活性杂交废料", true, 50, 45, 20, 10, new Color(0.98f, 0.45f, 0.85f), new Vector3(-3.2f, 2.2f, 0f));
        SpawnConstructDirect("scrap_gas_cyst", "粗糙·膨胀的气囊疙瘩", "活性杂交废料", true, 60, 15, 20, 40, new Color(0.95f, 0.40f, 0.55f), new Vector3(-3.2f, 1.2f, 0f));

        // 3. 营地休整与水刃重炮：悬浮软床(移速+翻滚减耗) + 喷水炮(自动射击击退)
        SpawnConstructDirect("furn_pneumatic_bed", "精制·气泡微压悬浮软床", "气压设施", false, 70, 10, 35, 25, new Color(0.75f, 0.52f, 0.99f), new Vector3(0.0f, -2.4f, 0f));
        SpawnConstructDirect("furn_hydro_cannon", "精制·高压自愈喷水炮", "水气武器", false, 60, 15, 30, 65, new Color(0.15f, 0.82f, 0.88f), new Vector3(-1.6f, -0.6f, 0f));

        // 4. 生体心智驯化中心
        SpawnConstructDirect("furn_bio_incubator", "精制·小型生体驯化培养箱", "智能设施", false, 15, 50, 50, 65, new Color(0.20f, 0.65f, 0.92f), new Vector3(1.6f, 2.8f, 0f));

        // 5. 漏电烂泥与共生残渣
        SpawnConstructDirect("scrap_mud_coral", "粗糙·漏电的烂泥珊瑚块", "活性杂交废料", true, 15, 50, 25, 55, new Color(0.85f, 0.55f, 0.95f), new Vector3(0.3f, 1.5f, 0f));
        SpawnConstructDirect("scrap_symbiotic_sludge", "粗糙·活性共生杂交残渣", "活性杂交废料", true, 20, 15, 60, 35, new Color(0.7f, 0.85f, 0.5f), new Vector3(-0.5f, 2.6f, 0f));

        EnsureEnemyTrio();
        Debug.Log("<color=#38bdf8>[BiomechanicalCombatAdapter] 战场 10 大生体构装与协同阵列已成功动态部署！</color>");
    }

    private GameObject SpawnConstructDirect(string id, string name, string category, bool isScrap, int p, int e, int g, int w, Color color, Vector3 pos)
    {
        if (genericFurniturePrefab == null)
        {
            genericFurniturePrefab = Resources.Load<GameObject>("Furniture_LavaTable");
        }
        if (genericFurniturePrefab == null) return null;

        GameObject go = Instantiate(genericFurniturePrefab, pos, Quaternion.identity);
        go.name = $"{id}_Showcase";

        var prod = CreateSampleProduct(id, name, category, isScrap, p, e, g, w, color);
        var furn = go.GetComponent<FurnitureObject>() ?? go.AddComponent<FurnitureObject>();
        furn.instanceId = id;
        furn.furnitureName = name;
        furn.originalProduct = prod;

        float dura = g > 0 ? (120f + g * 1.5f) : 160f;
        furn.maxDurability = dura;
        furn.currentDurability = dura;
        furn.kickDamage = 60f + p * 0.8f;
        furn.kickGuardBreak = 80f + p * 0.5f;

        if (furn.furnitureRenderer != null)
        {
            var realSprite = Resources.Load<Sprite>("FurnitureSprites/" + id);
            if (realSprite != null)
            {
                furn.furnitureRenderer.sprite = realSprite;
                furn.furnitureRenderer.color = Color.white;
            }
            else
            {
                furn.furnitureRenderer.color = color;
            }
        }

        if (furn.furnitureCollider is CircleCollider2D circleCol1)
        {
            circleCol1.radius = 0.52f;
            circleCol1.offset = new Vector2(0f, 0.12f);
        }

        AttachTacticalAura(go, prod);
        furn.SnapToNearestGrid();
        return go;
    }

    private void EnsureEnemyTrio()
    {
        var demons = FindObjectsOfType<EnemyController>();
        if (demons.Length > 0 && demons.Length < 3)
        {
            GameObject baseDemon = demons[0].gameObject;
            baseDemon.name = "Enemy_Demon_WaterElec";
            baseDemon.transform.position = new Vector3(3.8f, 2.8f, 0f);

            GameObject demon2 = Instantiate(baseDemon, new Vector3(-4.5f, 2.2f, 0f), Quaternion.identity);
            demon2.name = "Enemy_Demon_Chokepoint";
            var c2 = demon2.GetComponent<EnemyController>();
            if (c2 != null) { c2.autoRespawn = true; c2.respawnDelay = 5f; }

            GameObject demon3 = Instantiate(baseDemon, new Vector3(1.4f, 4.2f, 0f), Quaternion.identity);
            demon3.name = "Enemy_Demon_Incubator";
            var c3 = demon3.GetComponent<EnemyController>();
            if (c3 != null) { c3.autoRespawn = true; c3.respawnDelay = 5.5f; }
        }
    }

    /// <summary>
    /// 将仓库中的指定产品部署到指定的等距网格地块上
    /// </summary>
    public GameObject SpawnProductAtCell(CraftedProduct product, Vector3Int cellPos, Vector3 worldPos)
    {
        if (product == null) return null;

        if (genericFurniturePrefab == null)
        {
            genericFurniturePrefab = Resources.Load<GameObject>("Furniture_LavaTable");
        }

        if (genericFurniturePrefab == null)
        {
            Debug.LogError("[BiomechanicalCombatAdapter] 缺少基础家具预制体，无法实例化！");
            return null;
        }

        // 1. 实例化物体
        GameObject go = Instantiate(genericFurniturePrefab, worldPos, Quaternion.identity);
        go.name = $"{product.id}_{cellPos.x}_{cellPos.y}";

        // 2. 挂载并注入 FurnitureObject 参数
        var furn = go.GetComponent<FurnitureObject>() ?? go.AddComponent<FurnitureObject>();
        furn.instanceId = product.id;
        furn.furnitureName = product.productName;
        furn.originalProduct = product;

        // 统一物理碰撞体覆盖度，使其贴合 1.0m 等距菱形地块，杜绝怪物从边缘穿模
        if (furn.furnitureCollider is CircleCollider2D circleCol2)
        {
            circleCol2.radius = 0.52f;
            circleCol2.offset = new Vector2(0f, 0.12f);
        }

        // 3. 根据构装四大要素 totalP, totalE, totalG, totalW 动态赋能物理三态数值
        float dura = product.totalG > 0 ? (120f + product.totalG * 1.5f) : 160f;
        furn.maxDurability = dura;
        furn.currentDurability = dura;
        furn.kickDamage = 60f + product.totalP * 0.8f;
        furn.kickGuardBreak = 80f + product.totalP * 0.5f;

        // 4. 着色与真实生体构装精灵渲染
        if (furn.furnitureRenderer != null)
        {
            var realSprite = Resources.Load<Sprite>("FurnitureSprites/" + product.id);
            if (realSprite != null)
            {
                furn.furnitureRenderer.sprite = realSprite;
                furn.furnitureRenderer.color = Color.white;
            }
            else
            {
                furn.furnitureRenderer.color = product.themeColor;
            }
        }

        // 5. 挂载专属战术光环行为组件
        AttachTacticalAura(go, product);

        // 6. 注册至等距网格阻挡
        furn.SnapToNearestGrid();

        // 7. 从仓库扣除 1 件
        PlayerSessionData.RemoveCraftedProduct(product);

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(worldPos + Vector3.up * 0.8f, $"✨ 部署构装: {product.productName}", product.themeColor, 0.12f);
        }

        return go;
    }

    /// <summary>
    /// 挂载各设施专属战术光环逻辑与公式数值
    /// </summary>
    public void AttachTacticalAura(GameObject go, CraftedProduct prod)
    {
        if (go == null || prod == null) return;

        switch (prod.id)
        {
            case "furn_eel_generator":
                var elec = go.GetComponent<EelGeneratorAuraBehavior>() ?? go.AddComponent<EelGeneratorAuraBehavior>();
                elec.paralyzeRadius = Mathf.Clamp(2.0f + prod.totalE / 55.0f, 2.0f, 5.0f);
                elec.shockDmg = Mathf.Clamp(Mathf.RoundToInt(18 + prod.totalE * 0.35f + prod.totalP * 0.12f), 18, 75);
                elec.powerGridRadius = 4.5f;
                elec.productData = prod;
                elec.SetRingRadius(elec.paralyzeRadius);
                break;

            case "furn_coral_wall":
                var wall = go.GetComponent<CoralWallAuraBehavior>() ?? go.AddComponent<CoralWallAuraBehavior>();
                wall.thornsDamage = Mathf.Clamp(Mathf.RoundToInt(10 + prod.totalE * 0.18f + prod.totalG * 0.16f), 10, 42);
                wall.regenRate = Mathf.Clamp(Mathf.RoundToInt(prod.totalG / 25.0f + 1), 1, 8);
                wall.productData = prod;
                wall.SetRingRadius(1.8f);
                break;

            case "furn_pneumatic_bed":
                var bed = go.GetComponent<PneumaticBedAuraBehavior>() ?? go.AddComponent<PneumaticBedAuraBehavior>();
                bed.fieldRadius = Mathf.Clamp(2.5f + prod.totalP / 45.0f, 2.5f, 5.5f);
                bed.speedBoostPercent = Mathf.Clamp(Mathf.RoundToInt(12 + prod.totalP / 14.0f), 12, 35);
                bed.productData = prod;
                bed.SetRingRadius(bed.fieldRadius);
                break;

            case "furn_water_tower":
                var water = go.GetComponent<WaterTowerAuraBehavior>() ?? go.AddComponent<WaterTowerAuraBehavior>();
                water.sprayRadius = Mathf.Clamp(2.5f + prod.totalW / 40.0f, 2.5f, 6.0f);
                water.slowPercent = Mathf.Clamp(Mathf.RoundToInt(25 + prod.totalW / 9.0f), 25, 60);
                water.productData = prod;
                water.SetRingRadius(water.sprayRadius);
                break;

            case "furn_bio_incubator":
                var incubator = go.GetComponent<BioIncubatorAuraBehavior>() ?? go.AddComponent<BioIncubatorAuraBehavior>();
                incubator.lureRange = Mathf.Clamp(3.0f + (prod.totalW + prod.totalE) / 50.0f, 3.0f, 7.5f);
                incubator.pacifyChance = Mathf.Clamp(Mathf.RoundToInt(25 + prod.totalG / 7.0f), 25, 65);
                incubator.productData = prod;
                incubator.SetRingRadius(incubator.lureRange);
                break;

            case "furn_hydro_cannon":
                var cannon = go.GetComponent<HydroCannonAuraBehavior>() ?? go.AddComponent<HydroCannonAuraBehavior>();
                cannon.shotRange = Mathf.Clamp(4.5f + prod.totalP / 38.0f, 4.5f, 8.5f);
                cannon.blastImpact = Mathf.Clamp(Mathf.RoundToInt(18 + (prod.totalP + prod.totalW) * 0.22f), 18, 65);
                cannon.productData = prod;
                cannon.SetRingRadius(cannon.shotRange);
                break;

            case "scrap_spasm_metal":
                var spring = go.GetComponent<SpasmMetalTrapBehavior>() ?? go.AddComponent<SpasmMetalTrapBehavior>();
                spring.knockbackForce = Mathf.Clamp(4.5f + prod.totalP / 30.0f, 4.5f, 7.5f);
                spring.stunDuration = Mathf.Clamp(1.2f + prod.totalE / 75.0f, 1.2f, 3.0f);
                spring.productData = prod;
                spring.SetRingRadius(1.6f);
                break;

            case "scrap_mud_coral":
                var mud = go.GetComponent<MudCoralTrapBehavior>() ?? go.AddComponent<MudCoralTrapBehavior>();
                mud.trapRadius = Mathf.Clamp(2.0f + prod.totalW / 45.0f, 2.0f, 4.5f);
                mud.shockDmg = Mathf.Clamp(Mathf.RoundToInt(12 + prod.totalE * 0.28f), 12, 52);
                mud.productData = prod;
                mud.SetRingRadius(mud.trapRadius);
                break;

            case "scrap_gas_cyst":
                var bomb = go.GetComponent<GasCystMineBehavior>() ?? go.AddComponent<GasCystMineBehavior>();
                bomb.blastRadius = Mathf.Clamp(2.0f + prod.totalP / 55.0f, 2.0f, 4.5f);
                bomb.blastDamage = Mathf.Clamp(Mathf.RoundToInt(25 + prod.totalP * 0.36f), 25, 78);
                bomb.productData = prod;
                bomb.SetRingRadius(bomb.blastRadius);
                break;

            case "scrap_symbiotic_sludge":
            case "scrap_hybrid_residue":
                var sludge = go.GetComponent<HybridResidueTrapBehavior>() ?? go.AddComponent<HybridResidueTrapBehavior>();
                sludge.productData = prod;
                sludge.SetRingRadius(2.2f);
                break;
        }
    }

    /// <summary>
    /// 确保当前仓库中拥有完整的 6 大家具与 4 大废料可供战斗布防
    /// </summary>
    public void EnsureInitialWarehouseProducts()
    {
        var existing = PlayerSessionData.GetCraftedProducts();
        if (existing.Count < 5)
        {
            // 自动注入完整的一批高品质生体构装成品
            PlayerSessionData.AddCraftedProduct(CreateSampleProduct("furn_eel_generator", "精制·电鳗电机发电机组", "电网设施", false, 15, 65, 30, 20, new Color(0.98f, 0.82f, 0.18f)));
            PlayerSessionData.AddCraftedProduct(CreateSampleProduct("furn_coral_wall", "精制·增生珊瑚管排墙", "增生防线", false, 10, 25, 75, 20, new Color(0.29f, 0.85f, 0.44f)));
            PlayerSessionData.AddCraftedProduct(CreateSampleProduct("furn_pneumatic_bed", "精制·气泡微压悬浮软床", "气压设施", false, 65, 10, 35, 25, new Color(0.75f, 0.52f, 0.99f)));
            PlayerSessionData.AddCraftedProduct(CreateSampleProduct("furn_water_tower", "精制·脉动造水循环塔", "潮润设施", false, 20, 15, 30, 75, new Color(0.22f, 0.74f, 0.97f)));
            PlayerSessionData.AddCraftedProduct(CreateSampleProduct("furn_bio_incubator", "精制·小型生体驯化培养箱", "智能设施", false, 15, 50, 45, 60, new Color(0.20f, 0.65f, 0.92f)));
            PlayerSessionData.AddCraftedProduct(CreateSampleProduct("furn_hydro_cannon", "精制·高压自愈喷水炮", "水气武器", false, 55, 15, 30, 60, new Color(0.15f, 0.82f, 0.88f)));

            PlayerSessionData.AddCraftedProduct(CreateSampleProduct("scrap_spasm_metal", "粗糙·抽搐的痉挛金属团", "活性杂交废料", true, 45, 40, 20, 10, new Color(0.98f, 0.45f, 0.85f)));
            PlayerSessionData.AddCraftedProduct(CreateSampleProduct("scrap_mud_coral", "粗糙·漏电的烂泥珊瑚块", "活性杂交废料", true, 15, 45, 25, 50, new Color(0.85f, 0.55f, 0.95f)));
            PlayerSessionData.AddCraftedProduct(CreateSampleProduct("scrap_gas_cyst", "粗糙·膨胀的气囊疙瘩", "活性杂交废料", true, 55, 15, 20, 40, new Color(0.95f, 0.40f, 0.55f)));
            PlayerSessionData.AddCraftedProduct(CreateSampleProduct("scrap_symbiotic_sludge", "粗糙·活性共生杂交残渣", "活性杂交废料", true, 20, 15, 55, 35, new Color(0.7f, 0.85f, 0.5f)));
        }
    }

    public static CraftedProduct CreateSampleProduct(string id, string name, string category, bool isScrap, int p, int e, int g, int w, Color col)
    {
        return new CraftedProduct
        {
            id = id,
            productName = name,
            category = category,
            isAberrantScrap = isScrap,
            qualityTier = "精制",
            nodeCount = 4,
            primaryElement = e > w && e > g && e > p ? "Electric" : (w > g && w > p ? "Water" : (g > p ? "Wood" : "Gas")),
            totalP = p,
            totalE = e,
            totalG = g,
            totalW = w,
            hasMajorResonance = true,
            themeColor = col,
            flavorDescription = $"生体工坊构装产物: {name}",
            tacticalEffect = $"战斗战术属性 P:{p} E:{e} G:{g} W:{w}"
        };
    }
}
