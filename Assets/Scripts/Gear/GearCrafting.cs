using System.Collections.Generic;
using BiomechanicalCrafting;
using UnityEngine;

namespace GameJam.Gear
{
    /// <summary>
    /// 装备改装（GearDefs 的 15 件 × 3 级）的合成 / 升级规则。
    /// 材料直接读写程序同学的共用仓库 PlayerSessionData.shellSlotItems（与家具共用背壳背包），
    /// 不修改 Assets/Crafting 下的任何脚本。
    /// 配方：核心材料 ×1 + 同元素任意 ×N（N = GearDefs.AnyCount(level)）；
    /// 属性 p/e/g/w = 本次消耗材料（含核心）的 P/E/G/W 平均值。
    /// </summary>
    public static class GearCrafting
    {
        public const string Category = "装备改装";

        public static string SlotName(GearSlot s)
        {
            switch (s)
            {
                case GearSlot.Shell: return "壳";
                case GearSlot.Arm: return "钳臂";
                case GearSlot.Leg: return "机械腿";
                default: return "核心";
            }
        }

        public static string SlotChar(GearSlot s)
        {
            switch (s)
            {
                case GearSlot.Shell: return "壳";
                case GearSlot.Arm: return "钳";
                case GearSlot.Leg: return "腿";
                default: return "核";
            }
        }

        public static string ElementName(GearElement e)
        {
            switch (e)
            {
                case GearElement.Gas: return "气";
                case GearElement.Electric: return "电";
                case GearElement.Wood: return "木";
                default: return "水";
            }
        }

        public static Color ElementColor(GearElement e)
        {
            switch (e)
            {
                case GearElement.Gas: return new Color(0.75f, 0.52f, 0.99f);
                case GearElement.Electric: return new Color(0.98f, 0.82f, 0.18f);
                case GearElement.Wood: return new Color(0.29f, 0.85f, 0.44f);
                default: return new Color(0.22f, 0.74f, 0.97f);
            }
        }

        public static string CoreName(GearDef def)
        {
            var m = BiomechanicalMaterialDatabase.GetById(def.coreMaterialId);
            return m != null ? m.materialName : def.coreMaterialId;
        }

        /// <summary>背壳仓库里的每种材料实例（2x2 占 4 格只算 1 件），按槽位顺序。</summary>
        public static List<BiomechanicalMaterial> Warehouse()
        {
            PlayerSessionData.EnsureInitialized();
            var list = new List<BiomechanicalMaterial>();
            var seen = new HashSet<BiomechanicalMaterial>();
            var arr = PlayerSessionData.shellSlotItems;
            if (arr == null) return list;
            for (int i = 0; i < arr.Length; i++)
            {
                var m = arr[i];
                if (m != null && !string.IsNullOrEmpty(m.id) && seen.Add(m)) list.Add(m);
            }
            return list;
        }

        public static int CountCore(GearDef def)
        {
            if (def == null) return 0;
            int n = 0;
            foreach (var m in Warehouse()) if (m.id == def.coreMaterialId) n++;
            return n;
        }

        /// <summary>扣掉 1 个核心后，还能用于“同元素任意”的材料件数。</summary>
        public static int CountAny(GearDef def)
        {
            if (def == null) return 0;
            string element = def.element.ToString();
            int same = 0;
            foreach (var m in Warehouse()) if (m.element == element) same++;
            return Mathf.Max(0, same - (CountCore(def) > 0 ? 1 : 0));
        }

        public static int CountElement(GearElement element)
        {
            string name = element.ToString();
            int n = 0;
            foreach (var m in Warehouse()) if (m.element == name) n++;
            return n;
        }

        public static bool CanAfford(GearDef def, int level)
        {
            if (def == null || level < 1 || level > 3) return false;
            return CountCore(def) >= 1 && CountAny(def) >= GearDefs.AnyCount(level);
        }

        // #17：扣材料落盘失败时用来回滚装备；自测可注入一次失败。
        public static bool FailNextSpendForTest;
        const string GearPrefKey = "GJ_GEAR_STATE";
        const string BackpackPrefKey = "TAP_SHELL_BACKPACK_DATA";

        sealed class MaterialPlan
        {
            public BiomechanicalMaterial[] before;   // 扣材料之前的槽位（内存回滚用）
            public BiomechanicalMaterial[] after;    // 扣完、准备落盘的槽位
        }

        /// <summary>合成 Lv1；已拥有时重铸当前等级（设计允许“重新合成同等级刷属性”）。</summary>
        public static bool Craft(string id, out string message)
                {
                    if (!TapGearUI.CanModify(out message)) return false;
            var def = GearDefs.Get(id);
            if (def == null) { message = "合成失败：未知装置 " + id; return false; }
            var owned = GearInventory.Find(id);
            int level = owned != null ? Mathf.Clamp(owned.level, 1, 3) : 1;
            GearItem item;
            MaterialPlan plan;
            string why;
            if (!TryPrepare(def, level, out item, out plan, out why)) { message = "合成失败：" + why; return false; }
            if (!Commit(item, plan, out why)) { message = "合成失败：" + why; return false; }
            message = $"{(owned != null ? "重铸" : "合成")}成功：{def.name} Lv{level}（{def.stat} {item.Attr(def.stat):F0}）";
            return true;
        }

        /// <summary>升级到上一级 +1；必须已经拥有该装置（不能凭空造高级）。</summary>
        public static bool Upgrade(string id, out string message)
                {
                    if (!TapGearUI.CanModify(out message)) return false;
            var def = GearDefs.Get(id);
            if (def == null) { message = "升级失败：未知装置 " + id; return false; }
            var owned = GearInventory.Find(id);
            if (owned == null) { message = "升级失败：还没有这件装置，先合成 Lv1"; return false; }
            if (owned.level >= 3) { message = "升级失败：已经是 Lv3"; return false; }
            int target = Mathf.Clamp(owned.level + 1, 2, 3);
            GearItem item;
            MaterialPlan plan;
            string why;
            if (!TryPrepare(def, target, out item, out plan, out why)) { message = "升级失败：" + why; return false; }
            if (!Commit(item, plan, out why)) { message = "升级失败：" + why; return false; }
            message = $"升级成功：{def.name} Lv{target}（{def.stat} {item.Attr(def.stat):F0}）";
            return true;
        }

        /// <summary>先算好结果（装备数值 + 扣完材料的槽位），此时不写任何存档。</summary>
        static bool TryPrepare(GearDef def, int level, out GearItem item, out MaterialPlan plan, out string why)
        {
            item = null;
            plan = null;
            PlayerSessionData.EnsureInitialized();
            var all = Warehouse();

            BiomechanicalMaterial core = null;
            foreach (var m in all) if (m.id == def.coreMaterialId) { core = m; break; }
            if (core == null) { why = "仓库缺少核心材料「" + CoreName(def) + "」"; return false; }

            int needAny = GearDefs.AnyCount(level);
            string element = def.element.ToString();
            var picks = new List<BiomechanicalMaterial>();
            foreach (var m in all)
            {
                if (ReferenceEquals(m, core)) continue;
                if (m.element != element) continue;
                picks.Add(m);
                if (picks.Count >= needAny) break;
            }
            if (picks.Count < needAny)
            {
                why = $"同元素材料不足：{picks.Count}/{needAny}（{ElementName(def.element)}系）";
                return false;
            }

            int n = 1 + picks.Count;
            float p = core.p, e = core.e, g = core.g, w = core.w;
            foreach (var m in picks) { p += m.p; e += m.e; g += m.g; w += m.w; }

            var before = (BiomechanicalMaterial[])PlayerSessionData.shellSlotItems.Clone();
            var after = (BiomechanicalMaterial[])before.Clone();
            for (int i = 0; i < after.Length; i++)
                if (ReferenceEquals(after[i], core) || picks.Contains(after[i])) after[i] = null;

            plan = new MaterialPlan { before = before, after = after };
            item = new GearItem { id = def.id, level = level, p = p / n, e = e / n, g = g / n, w = w / n };
            why = null;
            return true;
        }

        static bool committing;
        // Delay listeners until BOTH stores are settled. Preserve exact previous bytes/existence
        // on failure; this is exception atomicity, not a claim of OS-crash atomicity across keys.
        static bool Commit(GearItem item, MaterialPlan plan, out string why)
        {
            if (committing) { why = "已有合成事务正在结算"; return false; }
            committing = true;
            bool hadGear = PlayerPrefs.HasKey(GearPrefKey), hadBackpack = PlayerPrefs.HasKey(BackpackPrefKey);
            string previousGear = PlayerPrefs.GetString(GearPrefKey), previousBackpack = PlayerPrefs.GetString(BackpackPrefKey);
            try {
                GearInventory.Put(item, false);
                if (!SpendAndSave(plan)) throw new System.InvalidOperationException("材料写入校验失败");
                GearInventory.NotifyChanged();
                why = null;
                return true;
            } catch (System.Exception ex) {
                System.Array.Copy(plan.before, PlayerSessionData.shellSlotItems, plan.before.Length);
                if (hadGear) PlayerPrefs.SetString(GearPrefKey, previousGear); else PlayerPrefs.DeleteKey(GearPrefKey);
                if (hadBackpack) PlayerPrefs.SetString(BackpackPrefKey, previousBackpack); else PlayerPrefs.DeleteKey(BackpackPrefKey);
                PlayerPrefs.Save();
                GearInventory.Reload();
                why = "材料落盘失败，装备和材料已回滚：" + ex.Message;
                return false;
            } finally {
                committing = false;
            }
        }

        static bool SpendAndSave(MaterialPlan plan)
        {
            if (FailNextSpendForTest) { FailNextSpendForTest = false; return false; }   // 自测注入
            PlayerSessionData.SaveBackpackSlots(plan.after);
            if (BackpackMatches(plan.after)) return true;
            // 保存没生效：把内存槽位也退回扣之前，保证本次合成完全无副作用。
            for (int i = 0; i < PlayerSessionData.shellSlotItems.Length; i++)
                PlayerSessionData.shellSlotItems[i] = i < plan.before.Length ? plan.before[i] : null;
            return false;
        }

        /// <summary>读回存档校验：每个槽位的材料存在性 / id 必须与预期一致（检测“扣材料没落盘”）。</summary>
        static bool BackpackMatches(BiomechanicalMaterial[] expected)
        {
            try
            {
                if (!PlayerPrefs.HasKey(BackpackPrefKey)) return false;
                string json = PlayerPrefs.GetString(BackpackPrefKey, "");
                if (string.IsNullOrEmpty(json)) return false;
                var data = JsonUtility.FromJson<PlayerSessionData.SavedBackpackData>(json);
                if (data == null || data.entries == null) return false;
                var saved = new Dictionary<int, PlayerSessionData.SavedSlotEntry>();
                foreach (var e in data.entries)
                    if (e != null && !saved.ContainsKey(e.slotId)) saved[e.slotId] = e;
                for (int i = 0; i < expected.Length; i++)
                {
                    var m = expected[i];
                    bool want = m != null && !string.IsNullOrEmpty(m.id);
                    saved.TryGetValue(i, out var entry);
                    if (want != (entry != null)) return false;
                    if (want && entry.materialId != m.id) return false;
                }
                return true;
            }
            catch { return false; }
        }
    }
}
