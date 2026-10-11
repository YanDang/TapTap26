namespace GameJam
{
    public enum ItemId { GasVent, GasSac, GasDuct, EelGenerator, CoralWall, WaterTower, BioIncubator, Table }

    /// <summary>
    /// 物品栏数据：每种物品的数量、负重。负重上限 = 基础 + 每个充满的气泡囊加成（GasSac.FullCount）。
    /// 现在还没有采集（那是程序同学的采集系统），开局直接给一批气系原料和 4 种正式家具用来试玩。
    /// </summary>
    public class Inventory
    {
        public static Inventory I;

        public struct Info
        {
            public ItemId id;
            public string name, icon, desc;
            public int weight;
        }

        public static readonly Info[] Infos =
        {
            new Info { id = ItemId.GasVent, name = "气旋喷口", icon = "gas_vent", weight = 2, desc = "气源：每秒产气，通过管道送给气泡囊" },
            new Info { id = ItemId.GasSac,  name = "气泡囊",   icon = "gas_sac",  weight = 2, desc = "储气：充满后增加负重；储气越多越吸引怪物" },
            new Info { id = ItemId.GasDuct, name = "充气导管", icon = "gas_duct", weight = 1, desc = "从一个气系家具拖到另一个；每 2 米 1 根，最长 6 米" },
            // 4 个正式家具（2026-10-11）：顺序要和 ItemId 一致
            new Info { id = ItemId.EelGenerator, name = "电鳗发电机组", icon = "eel_generator", weight = 3, desc = "充满电后放电：范围内敌人受伤 + 麻痹，湿身的多受伤害" },
            new Info { id = ItemId.CoralWall,    name = "珊瑚管排墙",   icon = "coral_wall",    weight = 3, desc = "挡路的掩体墙，会自愈（水汽里更快）；敌人打墙会被反伤" },
            new Info { id = ItemId.WaterTower,   name = "造水循环塔",   icon = "water_tower",   weight = 3, desc = "周围铺水渍：敌人减速 + 淋湿" },
            new Info { id = ItemId.BioIncubator, name = "驯化培养箱",   icon = "bio_incubator", weight = 3, desc = "范围内的敌人慢慢被安抚，暂时不攻击" },
            new Info { id = ItemId.Table,        name = "可踢桌子",     icon = "table",         weight = 2, desc = "在旁边点左键踢飞：路上撞怪，落地范围伤害 + 眩晕；6 秒后回到原位" },
        };

        public static Info InfoOf(ItemId id) => Infos[(int)id];

        public int baseCapacity = 40;          // 开局的气系原料 20 + 正式家具 15 + 桌子 4
        public int capacityPerSac = 10;
        readonly int[] _counts = new int[Infos.Length];

        public int Count(ItemId id) => _counts[(int)id];
        public void Add(ItemId id, int n) => _counts[(int)id] += n;

        public bool Take(ItemId id, int n)
        {
            if (_counts[(int)id] < n) return false;
            _counts[(int)id] -= n;
            return true;
        }

        public int Weight
        {
            get { int w = 0; for (int i = 0; i < _counts.Length; i++) w += _counts[i] * Infos[i].weight; return w; }
        }

        public int Bonus => capacityPerSac * GasSac.FullCount;
        public int Capacity => baseCapacity + Bonus;
    }
}
