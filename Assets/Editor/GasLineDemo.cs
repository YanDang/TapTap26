using GameJam;
using UnityEditor;
using UnityEngine;

// Play 模式下：在主角附近找空地，用物品栏的原料摆一条演示生产线（喷口 → 气泡囊 → 气泡囊），并选中“充气导管”格子
public static class GasLineDemo
{
    [MenuItem("GameJam/Furniture/Demo Gas Line (Play mode)")]
    static void Demo()
    {
        if (!Application.isPlaying || !BuildController.I) { Debug.LogWarning("[Gas] 先进入 Play 模式"); return; }
        var b = BuildController.I;
        var p = GameManager.I.player.Pos;
        var vent = Place(b, ItemId.GasVent, p, 2f, 4f);
        var sac1 = vent ? Place(b, ItemId.GasSac, vent.Pos, 3f, 5f) : null;
        var sac2 = sac1 ? Place(b, ItemId.GasSac, sac1.Pos, 3f, 5f) : null;
        if (vent && sac1) b.PlacePipe(vent, sac1);
        if (sac1 && sac2) b.PlacePipe(sac1, sac2);
        b.Select(ItemId.GasDuct);
        Debug.Log($"[Gas] demo: vent={(bool)vent} sac1={(bool)sac1} sac2={(bool)sac2} pipes={b.placedPipes}");
    }

    // 以 center 为圆心、半径 rMin–rMax 的一圈上找第一个能放的位置
    static GasNode Place(BuildController b, ItemId id, Vector3 center, float rMin, float rMax)
    {
        for (float r = rMin; r <= rMax; r += 0.5f)
            for (int i = 0; i < 16; i++)
            {
                float a = i * 22.5f * Mathf.Deg2Rad;
                var pos = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r;
                if (b.CanPlaceNode(pos, out _)) return b.PlaceNode(id, pos);
            }
        return null;
    }
}
