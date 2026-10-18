using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam.Gear
{
    // 寄居蟹装备改装：三方共用的数据模型与特效门面（设计见 Docs/装备改装_设计.md）。
    // Kimi：战斗逻辑读 GearInventory.Equipped / Stat；DeepSeek：改装台 UI 与合成写 GearInventory；Claude：实现 IGearFxBackend。
    // 改签名先找 Claude。

    public enum GearSlot { Shell, Arm, Leg, Core }
    public enum GearElement { Gas, Electric, Wood, Water }

    /// <summary>一件装置的静态定义。stat = 材料属性影响的那一项（P/E/G/W）。</summary>
    public sealed class GearDef
    {
        public string id, name, coreMaterialId, stat, summary;
        public GearSlot slot;
        public GearElement element;
        public string[] levelText = new string[3];
    }

    /// <summary>玩家拥有的一件装置：等级 1..3 + 合成时投入材料的 P/E/G/W 平均值。</summary>
    [Serializable]
    public sealed class GearItem
    {
        public string id;
        public int level = 1;
        public float p, e, g, w;
        public float Attr(string s) => s == "P" ? p : s == "E" ? e : s == "G" ? g : w;
    }

    public static class GearDefs
    {
        public static readonly GearDef[] All =
        {
            D("shell_mobile", "立体机动蟹壳", GearSlot.Shell, GearElement.Wood, "mat_wood_coral_plate", "G", "空格长按超过0.25秒举壳"),
            D("shell_static", "静电反应壳", GearSlot.Shell, GearElement.Electric, "mat_elec_clam_shell", "E", "格挡攒静电，满层放连锁闪电"),
            D("shell_float", "应急浮囊壳", GearSlot.Shell, GearElement.Gas, "mat_gas_bubble_sac", "P", "致命一击时鼓成气泡保命"),
            D("arm_spring", "弹簧连锁拳", GearSlot.Arm, GearElement.Wood, "mat_wood_spring_fascia", "G", "闪避反击变连锁"),
            D("arm_eel", "电鳗钳", GearSlot.Arm, GearElement.Electric, "mat_elec_muscle_bundle", "E", "普攻带电弧麻痹"),
            D("arm_hydro", "高压水钳", GearSlot.Arm, GearElement.Water, "mat_water_keratin_nozzle", "W", "蓄力变水柱，击退并湿身"),
            D("arm_grapple", "螺纹抓钩", GearSlot.Arm, GearElement.Water, "mat_water_alloy_grapple", "P", "Q 射抓钩拉人 / 拉自己 / 拉家具"),
            D("leg_decoy", "翻滚爆破装置", GearSlot.Leg, GearElement.Gas, "mat_gas_vortex_cartilage", "P", "完美闪避留下爆炸诱饵"),
            D("leg_jet", "喷气滑步", GearSlot.Leg, GearElement.Gas, "mat_gas_float_bladder", "W", "翻滚变冲刺，留减速气流"),
            D("leg_root", "珊瑚生根", GearSlot.Leg, GearElement.Wood, "mat_wood_root_gear", "G", "站定扎根减伤、拔腿冒刺"),
            D("core_fungi", "荧光共生菌群", GearSlot.Core, GearElement.Wood, "mat_wood_symbiotic_fungi", "G", "脱战回血"),
            D("core_nerve", "镀银神经索", GearSlot.Core, GearElement.Electric, "mat_elec_silver_nerve", "E", "攻速提升"),
            D("core_siphon", "循环虹吸肉管", GearSlot.Core, GearElement.Water, "mat_water_siphon_tube", "W", "移速提升"),
            D("core_logic", "破损逻辑指令片", GearSlot.Core, GearElement.Water, "mat_water_logic_chip", "E", "预读敌人、放宽完美闪避"),
            D("core_antenna", "泄压感应触角", GearSlot.Core, GearElement.Gas, "mat_gas_pressure_antenna", "P", "大招充能加快"),
        };

        static GearDef D(string id, string name, GearSlot slot, GearElement el, string core, string stat, string summary) =>
            new GearDef { id = id, name = name, slot = slot, element = el, coreMaterialId = core, stat = stat, summary = summary };

        public static GearDef Get(string id) { foreach (var d in All) if (d.id == id) return d; return null; }

        /// <summary>每级合成所需：核心材料 1 + 同元素任意材料 n（Lv1=2、Lv2=3、Lv3=5）。</summary>
        public static int AnyCount(int level) => level <= 1 ? 2 : level == 2 ? 3 : 5;
    }

    /// <summary>拥有的装置、各槽装备、存档（PlayerPrefs 键 GJ_GEAR_STATE）。战斗逻辑与 UI 共用。</summary>
    public static class GearInventory
    {
        const string Key = "GJ_GEAR_STATE";
        [Serializable] class Save { public List<GearItem> owned = new List<GearItem>(); public string[] equipped = new string[4]; }
        static Save _s;
        public static event Action Changed;

        static Save S { get { if (_s == null) Load(); return _s; } }
        public static IReadOnlyList<GearItem> Owned => S.owned;

        public static GearItem Find(string id) { foreach (var it in S.owned) if (it.id == id) return it; return null; }

        /// <summary>该槽当前装备（没有返回 null）。</summary>
        public static GearItem Equipped(GearSlot slot)
        {
            var id = S.equipped[(int)slot];
            return string.IsNullOrEmpty(id) ? null : Find(id);
        }

        /// <summary>某件装置是否已装备且等级 ≥ minLevel。战斗逻辑最常用。</summary>
        public static bool Has(string id, int minLevel = 1)
        {
            var d = GearDefs.Get(id); if (d == null) return false;
            var it = Equipped(d.slot);
            return it != null && it.id == id && it.level >= minLevel;
        }

        public static int Level(string id) { var d = GearDefs.Get(id); var it = d != null ? Equipped(d.slot) : null; return it != null && it.id == id ? it.level : 0; }

        /// <summary>装备的材料属性（该装置的 stat 项，0..100）；未装备返回 0。</summary>
        public static float Stat(string id) { var d = GearDefs.Get(id); var it = Find(id); return d == null || it == null || !Has(id) ? 0f : it.Attr(d.stat); }

        /// <summary>合成 / 升级后写入（同 id 覆盖等级与属性）。</summary>
        public static void Put(GearItem item, bool notify = true)
        {
            var old = Find(item.id);
            if (old != null) S.owned.Remove(old);
            S.owned.Add(item);
            Persist(notify);
        }

        public static void Equip(string id)
                {
                    if (!TapGearUI.CanModify(out _)) return;
            var d = GearDefs.Get(id); if (d == null || Find(id) == null) return;
            S.equipped[(int)d.slot] = id; Persist();
        }

        public static void Unequip(GearSlot slot) { if (!TapGearUI.CanModify(out _)) return; S.equipped[(int)slot] = null; Persist(); }

        /// <summary>自测用：清空 / 备份还原。</summary>
        public static void ResetAll() { _s = new Save(); Persist(); }
        public static string Snapshot() => PlayerPrefs.GetString(Key, "");
        public static void Restore(string json) { PlayerPrefs.SetString(Key, json); PlayerPrefs.Save(); _s = null; Changed?.Invoke(); }

        static void Load()
        {
            var json = PlayerPrefs.GetString(Key, "");
            try { _s = string.IsNullOrEmpty(json) ? new Save() : JsonUtility.FromJson<Save>(json) ?? new Save(); }
            catch { _s = new Save(); }
            if (_s.equipped == null || _s.equipped.Length != 4) _s.equipped = new string[4];
        }

        internal static void Reload() { _s = null; }
        internal static void NotifyChanged()
        {
            if (Changed == null) return;
            foreach (Action listener in Changed.GetInvocationList())
                try { listener(); } catch (Exception ex) { Debug.LogWarning("[GearInventory] listener failed: " + ex.Message); }
        }
        static void Persist(bool notify = true) { PlayerPrefs.SetString(Key, JsonUtility.ToJson(_s)); PlayerPrefs.Save(); if (notify) NotifyChanged(); }
    }

    /// <summary>特效 / 动画后端（Claude 实现，启动时注册到 GearFx.Backend）。</summary>
    public interface IGearFxBackend
    {
        void PerfectDodge(Vector3 heroPos);                      // “嗖” + 残影（慢动作由逻辑侧调 TimeManager）
        void Counter(Vector3 from, Vector3 to);                  // 闪避反击出拳的冲击
        void ShellGuard(Transform hero, bool on);                // 举壳开始 / 结束
        void ShellBlock(Vector3 pos, bool perfect);              // 壳挡下一击
        void StaticStacks(Transform hero, int stacks);           // 静电层数 0..3
        void ChainLightning(Vector3[] points);                   // 连锁闪电（依次经过的点）
        void FloatBubble(Transform hero, float seconds);         // 浮囊气泡
        void EelArc(Vector3 from, Vector3 to);                   // 电鳗钳电弧
        void WaterJet(Vector3 from, Vector3 to, float width);    // 水柱
        void Puddle(Vector3 pos, float radius, float seconds);   // 水洼
        void Grapple(Vector3 from, Vector3 to, float seconds);   // 抓钩链
        void Decoy(Vector3 pos, float fuse);                     // 诱饵蟹壳（fuse 秒后由逻辑侧调 Explosion）
        void Explosion(Vector3 pos, float radius);               // 小型爆炸
        void JetTrail(Vector3 from, Vector3 to, float seconds);  // 气流带
        void Root(Transform hero, bool on);                      // 扎根 / 拔腿
        void CoralSpikes(Vector3 pos, float radius);             // 珊瑚刺
        void Heal(Transform hero);                               // 回血
    }

    /// <summary>逻辑侧只调这里；没有后端时什么都不画（不影响判定）。</summary>
    public static class GearFx
    {
        public static IGearFxBackend Backend;
        public static void PerfectDodge(Vector3 p) => Backend?.PerfectDodge(p);
        public static void Counter(Vector3 a, Vector3 b) => Backend?.Counter(a, b);
        public static void ShellGuard(Transform h, bool on) => Backend?.ShellGuard(h, on);
        public static void ShellBlock(Vector3 p, bool perfect) => Backend?.ShellBlock(p, perfect);
        public static void StaticStacks(Transform h, int n) => Backend?.StaticStacks(h, n);
        public static void ChainLightning(Vector3[] pts) => Backend?.ChainLightning(pts);
        public static void FloatBubble(Transform h, float s) => Backend?.FloatBubble(h, s);
        public static void EelArc(Vector3 a, Vector3 b) => Backend?.EelArc(a, b);
        public static void WaterJet(Vector3 a, Vector3 b, float w) => Backend?.WaterJet(a, b, w);
        public static void Puddle(Vector3 p, float r, float s) => Backend?.Puddle(p, r, s);
        public static void Grapple(Vector3 a, Vector3 b, float s) => Backend?.Grapple(a, b, s);
        public static void Decoy(Vector3 p, float fuse) => Backend?.Decoy(p, fuse);
        public static void Explosion(Vector3 p, float r) => Backend?.Explosion(p, r);
        public static void JetTrail(Vector3 a, Vector3 b, float s) => Backend?.JetTrail(a, b, s);
        public static void Root(Transform h, bool on) => Backend?.Root(h, on);
        public static void CoralSpikes(Vector3 p, float r) => Backend?.CoralSpikes(p, r);
        public static void Heal(Transform h) => Backend?.Heal(h);
    }
}
