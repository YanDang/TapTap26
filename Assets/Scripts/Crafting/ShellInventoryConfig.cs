using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 寄居蟹背壳种类（决定背包的拓扑形态、大小格子数量与布局）
/// </summary>
public enum ShellType
{
    ConchShell = 0,     // 初生海螺壳（经典不对称向心盘面，带生体缺口）
    ClamShell = 1,      // 斑纹双翼扇贝壳（左右对称翼状，易于并联）
    RustedCanShell = 2  // 废墟铁皮易拉罐壳（工业矩形高承载，带破损凹角）
}

/// <summary>
/// 单个背包宫格槽位的布局定义
/// </summary>
[Serializable]
public class ShellSlotLayout
{
    public int slotId;
    public Vector2Int gridCoord;    // 逻辑网格坐标，用于 8 向（横/竖/斜）相邻连线判定
    public bool isMajor;            // 是否为大格子（重要骨架位）
    public Vector2 relativePos;     // 在背壳盘面内的相对像素偏移 (相对于盘面中心)
    public float size = 72f;        // 槽位像素边长（大格通常 96~105px，小格通常 68~74px）

    public ShellSlotLayout(int id, int gx, int gy, bool major, Vector2 relPos, float sz)
    {
        slotId = id;
        gridCoord = new Vector2Int(gx, gy);
        isMajor = major;
        relativePos = relPos;
        size = sz;
    }

    /// <summary>
    /// 判断是否与另一个槽位在 8 个方向（横向、纵向、斜向）逻辑相邻
    /// </summary>
    public bool IsAdjacent8Way(ShellSlotLayout other)
    {
        if (other == null || other.slotId == this.slotId) return false;
        int dx = Mathf.Abs(this.gridCoord.x - other.gridCoord.x);
        int dy = Mathf.Abs(this.gridCoord.y - other.gridCoord.y);
        return (dx <= 1 && dy <= 1);
    }
}

/// <summary>
/// 背壳完整配置
/// </summary>
[Serializable]
public class ShellConfig
{
    public ShellType shellType;
    public string shellName;
    public string flavorText;
    public List<ShellSlotLayout> slots = new List<ShellSlotLayout>();

    public int TotalSlotCount => slots.Count;
    public int MajorSlotCount => slots.FindAll(s => s.isMajor).Count;

    /// <summary>
    /// 获取指定 ShellType 的标准拓扑配置
    /// </summary>
    public static ShellConfig CreateConfig(ShellType type)
    {
        ShellConfig cfg = new ShellConfig();
        cfg.shellType = type;

        switch (type)
        {
            // =========================================================================
            // 1. 初生海螺壳 (ConchShell): 24 格螺旋网格，支持多处 2x2 骨架核心与 1x1 辅料嵌套
            // =========================================================================
            case ShellType.ConchShell:
                cfg.shellName = "初生海螺壳";
                cfg.flavorText = "随风微鸣的螺旋海螺壳，网格错落有致，最适于容纳 2x2 骨架核心与 1x1 灵动辅料。";
                float unitSize = 70f;
                float step = 76f;
                int sId = 0;

                // 6 列 x 5 行有机海螺轮廓
                // 行 0 (顶层)
                for (int x = 1; x <= 4; x++) cfg.slots.Add(new ShellSlotLayout(sId++, x, 0, false, new Vector2((x - 2.5f) * step, (2f - 0) * step), unitSize));
                // 行 1
                for (int x = 0; x <= 5; x++) cfg.slots.Add(new ShellSlotLayout(sId++, x, 1, false, new Vector2((x - 2.5f) * step, (2f - 1) * step), unitSize));
                // 行 2
                for (int x = 0; x <= 5; x++) cfg.slots.Add(new ShellSlotLayout(sId++, x, 2, false, new Vector2((x - 2.5f) * step, (2f - 2) * step), unitSize));
                // 行 3
                for (int x = 1; x <= 5; x++) cfg.slots.Add(new ShellSlotLayout(sId++, x, 3, false, new Vector2((x - 2.5f) * step, (2f - 3) * step), unitSize));
                // 行 4 (收尾)
                for (int x = 2; x <= 4; x++) cfg.slots.Add(new ShellSlotLayout(sId++, x, 4, false, new Vector2((x - 2.5f) * step, (2f - 4) * step), unitSize));
                break;

            // =========================================================================
            // 2. 斑纹双翼扇贝壳 (ClamShell): 26 格对称双翼网格
            // =========================================================================
            case ShellType.ClamShell:
                cfg.shellName = "斑纹双翼扇贝壳";
                cfg.flavorText = "两侧如蝶翼展开的宽大贝壳，中央容纳 2x2 大型主心室，双翼罗列高密度辅料构件。";
                float cUnitSize = 70f;
                float cStep = 76f;
                int cId = 0;

                for (int x = 1; x <= 4; x++) cfg.slots.Add(new ShellSlotLayout(cId++, x, 0, false, new Vector2((x - 2.5f) * cStep, (2f - 0) * cStep), cUnitSize));
                for (int x = 0; x <= 5; x++) cfg.slots.Add(new ShellSlotLayout(cId++, x, 1, false, new Vector2((x - 2.5f) * cStep, (2f - 1) * cStep), cUnitSize));
                for (int x = 0; x <= 5; x++) cfg.slots.Add(new ShellSlotLayout(cId++, x, 2, false, new Vector2((x - 2.5f) * cStep, (2f - 2) * cStep), cUnitSize));
                for (int x = 0; x <= 5; x++) cfg.slots.Add(new ShellSlotLayout(cId++, x, 3, false, new Vector2((x - 2.5f) * cStep, (2f - 3) * cStep), cUnitSize));
                for (int x = 1; x <= 4; x++) cfg.slots.Add(new ShellSlotLayout(cId++, x, 4, false, new Vector2((x - 2.5f) * cStep, (2f - 4) * cStep), cUnitSize));
                break;

            // =========================================================================
            // 3. 废墟铁皮易拉罐壳 (RustedCanShell): 28 格工业高负荷方格
            // =========================================================================
            case ShellType.RustedCanShell:
                cfg.shellName = "废墟铁皮易拉罐壳";
                cfg.flavorText = "工业合金饮料罐，高规整矩形承载面，可多重并联多个 2x2 重型发电机与防线。";
                float kUnitSize = 70f;
                float kStep = 76f;
                int kId = 0;

                for (int y = 0; y < 5; y++)
                {
                    for (int x = 0; x < 6; x++)
                    {
                        // 凹陷破损两个角
                        if ((x == 0 && y == 0) || (x == 5 && y == 4)) continue;
                        cfg.slots.Add(new ShellSlotLayout(kId++, x, y, false, new Vector2((x - 2.5f) * kStep, (2f - y) * kStep), kUnitSize));
                    }
                }
                break;
        }

        return cfg;
    }

    public ShellSlotLayout GetSlotAt(int gx, int gy)
    {
        return slots.Find(s => s.gridCoord.x == gx && s.gridCoord.y == gy);
    }

    public List<int> Get2x2SlotIds(int rootGx, int rootGy)
    {
        var s0 = GetSlotAt(rootGx, rootGy);
        var s1 = GetSlotAt(rootGx + 1, rootGy);
        var s2 = GetSlotAt(rootGx, rootGy + 1);
        var s3 = GetSlotAt(rootGx + 1, rootGy + 1);
        if (s0 != null && s1 != null && s2 != null && s3 != null)
        {
            return new List<int> { s0.slotId, s1.slotId, s2.slotId, s3.slotId };
        }
        return null;
    }
}
