using BiomechanicalCrafting;

namespace GameJam
{
    public enum ItemId
    {
        GasVent, GasSac, GasDuct,                                   // 气系生产线（本工程自带，开局送）
        EelGenerator, CoralWall, WaterTower, BioIncubator,          // 正式家具（来自合成工坊）
        Table,                                                      // 可踢桌子（本工程自带，开局送）
        GasCyst, SpasmMetal, MudCoral, ScreamingValve, SymbioticSludge,   // 活性杂交废料（来自合成工坊）
        PneumaticBed, HydroCannon,                                  // 合成工坊能做、战斗里还没实装
    }

    /// <summary>
    /// 物品栏数据。两种来源（2026-10-11 接入程序同学的采集 / 合成系统）：
    /// · 合成产物（productId 非空）：数量 = 合成仓库 PlayerSessionData.craftedProducts 里这个 id 有几件；
    ///   摆放时从仓库扣掉一件（Take 记下扣掉的那件，放置被打断时 Refund 原样放回）。仓库存在 PlayerPrefs，跨局保留。
    /// · 本工程自带（气系生产线、桌子）：本地计数，开局送一批。
    /// 负重上限 = 基础 + 每个充满的气泡囊加成（GasSac.FullCount）。
    /// </summary>
    public class Inventory
    {
        public static Inventory I;

        public struct Info
        {
            public ItemId id;
            public string name, icon, desc;
            public int weight;
            public string productId;      // 合成产物的 id（CraftedProduct.id）；null = 本工程自带
            public bool placeable;        // 战斗里能不能摆（false = 还没实装）
        }

        public static readonly Info[] Infos =
        {   // 顺序必须和 ItemId 一致
            new Info { id = ItemId.GasVent, name = "气旋喷口", icon = "gas_vent", weight = 2, placeable = true, desc = "气源：每秒产气，通过管道送给气泡囊" },
            new Info { id = ItemId.GasSac,  name = "气泡囊",   icon = "gas_sac",  weight = 2, placeable = true, desc = "储气：充满后增加负重；储气越多越吸引怪物" },
            new Info { id = ItemId.GasDuct, name = "充气导管", icon = "gas_duct", weight = 1, placeable = true, desc = "从一个气系家具拖到另一个；每 2 米 1 根，最长 6 米" },
            new Info { id = ItemId.EelGenerator, name = "电鳗发电机组", icon = "eel_generator", weight = 3, placeable = true, productId = "furn_eel_generator", desc = "充满电后放电：范围内敌人受伤 + 麻痹，湿身的多受伤害" },
            new Info { id = ItemId.CoralWall,    name = "珊瑚管排墙",   icon = "coral_wall",    weight = 3, placeable = true, productId = "furn_coral_wall", desc = "挡路的掩体墙，会自愈（水汽里更快）；敌人打墙会被反伤" },
            new Info { id = ItemId.WaterTower,   name = "造水循环塔",   icon = "water_tower",   weight = 3, placeable = true, productId = "furn_water_tower", desc = "周围铺水渍：敌人减速 + 淋湿" },
            new Info { id = ItemId.BioIncubator, name = "驯化培养箱",   icon = "bio_incubator", weight = 3, placeable = true, productId = "furn_bio_incubator", desc = "范围内的敌人慢慢被安抚，暂时不攻击" },
            new Info { id = ItemId.Table,        name = "可踢桌子",     icon = "table",         weight = 2, placeable = true, desc = "在旁边点左键踢飞：路上撞怪，落地范围伤害 + 眩晕；6 秒后回到原位" },
            new Info { id = ItemId.GasCyst,         name = "气囊疙瘩",   icon = "gas_cyst",         weight = 1, placeable = true, productId = "scrap_gas_cyst", desc = "地雷：敌人走近就鼓胀爆炸，范围伤害 + 眩晕" },
            new Info { id = ItemId.SpasmMetal,      name = "痉挛金属团", icon = "spasm_metal",      weight = 1, placeable = true, productId = "scrap_spasm_metal", desc = "弹簧陷阱：踩上去的敌人被弹飞 + 麻痹" },
            new Info { id = ItemId.MudCoral,        name = "烂泥珊瑚块", icon = "mud_coral",        weight = 1, placeable = true, productId = "scrap_mud_coral", desc = "带电泥潭：减速 + 每秒电一下" },
            new Info { id = ItemId.ScreamingValve,  name = "嘶鸣排气瓣", icon = "screaming_valve",  weight = 1, placeable = true, productId = "scrap_screaming_valve", desc = "间歇尖啸，把附近的怪吸引过来打它" },
            new Info { id = ItemId.SymbioticSludge, name = "共生残渣",   icon = "symbiotic_sludge", weight = 1, placeable = true, productId = "scrap_symbiotic_sludge", desc = "会自愈的路障，打碎后变成减速碎块" },
            // 下面两个合成工坊能做出来，战斗效果还没实装：图标借用工坊的家具图（Resources/FurnitureSprites）
            new Info { id = ItemId.PneumaticBed, name = "气泡悬浮软床", icon = "FurnitureSprites/furn_pneumatic_bed", weight = 3, placeable = false, productId = "furn_pneumatic_bed", desc = "（战斗效果还没做）" },
            new Info { id = ItemId.HydroCannon,  name = "高压喷水炮",   icon = "FurnitureSprites/furn_hydro_cannon",  weight = 3, placeable = false, productId = "furn_hydro_cannon", desc = "（战斗效果还没做）" },
        };

        public static Info InfoOf(ItemId id) => Infos[(int)id];

        /// <summary>物品图标：icon 不带路径时在 Resources/Furniture 下，带路径的按原样加载。</summary>
        public static UnityEngine.Sprite IconOf(ItemId id)
        {
            var icon = InfoOf(id).icon;
            return UnityEngine.Resources.Load<UnityEngine.Sprite>(icon.Contains("/") ? icon : "Furniture/" + icon);
        }

        public int baseCapacity = 40;
        public int capacityPerSac = 10;
        readonly int[] _counts = new int[Infos.Length];

        /// <summary>最近一次 Take 从合成仓库里拿走的那件产物（放置被打断时原样放回）。</summary>
        public CraftedProduct LastTaken { get; private set; }

        public int Count(ItemId id)
        {
            var pid = InfoOf(id).productId;
            if (pid == null) return _counts[(int)id];
            int n = 0;
            foreach (var p in PlayerSessionData.GetCraftedProducts()) if (p != null && p.id == pid) n++;
            return n;
        }

        /// <summary>加 n 件。合成产物会以“粗糙”品质存进合成仓库（调试 / 自测用；正常来源是合成工坊）。</summary>
        public void Add(ItemId id, int n)
        {
            var info = InfoOf(id);
            if (info.productId == null) { _counts[(int)id] += n; return; }
            for (int i = 0; i < n; i++)
                PlayerSessionData.AddCraftedProduct(new CraftedProduct
                {
                    id = info.productId, productName = info.name, qualityTier = "粗糙", nodeCount = 2,
                    isAberrantScrap = info.productId.StartsWith("scrap_"),
                });
        }

        public bool Take(ItemId id, int n)
        {
            LastTaken = null;
            var pid = InfoOf(id).productId;
            if (pid == null)
            {
                if (_counts[(int)id] < n) return false;
                _counts[(int)id] -= n;
                return true;
            }
            if (Count(id) < n) return false;
            for (int i = 0; i < n; i++)
            {
                var p = PlayerSessionData.GetCraftedProducts().Find(x => x != null && x.id == pid);
                LastTaken = p;
                PlayerSessionData.RemoveCraftedProduct(p);
            }
            return true;
        }

        /// <summary>退还一件（放置被打断 / 落点被占）。taken = Take 时拿走的那件合成产物，原样放回仓库。</summary>
        public void Refund(ItemId id, CraftedProduct taken)
        {
            if (InfoOf(id).productId != null && taken != null) PlayerSessionData.AddCraftedProduct(taken);
            else Add(id, 1);
        }

        public int Weight
        {
            get { int w = 0; for (int i = 0; i < Infos.Length; i++) w += Count(Infos[i].id) * Infos[i].weight; return w; }
        }

        public int Bonus => capacityPerSac * GasSac.FullCount;
        public int Capacity => baseCapacity + Bonus;
    }
}
