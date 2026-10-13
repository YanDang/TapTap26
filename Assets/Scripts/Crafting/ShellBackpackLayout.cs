using System.Collections.Generic;
using BiomechanicalCrafting;

public static class ShellBackpackLayout
{
    public static bool TryRepack(BiomechanicalMaterial[] source, ShellConfig target, out BiomechanicalMaterial[] result)
    {
        result = null;
        var unique = new List<BiomechanicalMaterial>();
        foreach (var material in source) if (material != null && !unique.Contains(material)) unique.Add(material);
        var major = unique.FindAll(material => material.isMajor);
        var minor = unique.FindAll(material => !material.isMajor);
        if (major.Count * 4 + minor.Count > target.TotalSlotCount) return false;
        var footprints = new List<int[]>();
        foreach (var slot in target.slots)
        {
            var ids = new List<int>();
            foreach (var candidate in target.slots)
                if (candidate.gridCoord.x >= slot.gridCoord.x && candidate.gridCoord.x <= slot.gridCoord.x + 1 && candidate.gridCoord.y >= slot.gridCoord.y && candidate.gridCoord.y <= slot.gridCoord.y + 1) ids.Add(candidate.slotId);
            if (ids.Count == 4) footprints.Add(ids.ToArray());
        }
        var used = new bool[target.TotalSlotCount];
        var chosen = new List<int[]>();
        if (!PlaceMajor(footprints, 0, major.Count, used, chosen)) return false;
        result = new BiomechanicalMaterial[source.Length];
        for (int index = 0; index < major.Count; index++) Assign(result, major[index], chosen[index]);
        int next = 0;
        foreach (var material in minor)
        {
            while (used[next]) next++;
            Assign(result, material, new[] { next });
            used[next] = true;
        }
        return true;
    }

    private static bool PlaceMajor(List<int[]> footprints, int start, int count, bool[] used, List<int[]> chosen)
    {
        if (count == 0) return true;
        for (int index = start; index < footprints.Count; index++)
        {
            var footprint = footprints[index];
            bool free = true;
            foreach (int slot in footprint) if (used[slot]) free = false;
            if (!free) continue;
            foreach (int slot in footprint) used[slot] = true;
            chosen.Add(footprint);
            if (PlaceMajor(footprints, index + 1, count - 1, used, chosen)) return true;
            chosen.RemoveAt(chosen.Count - 1);
            foreach (int slot in footprint) used[slot] = false;
        }
        return false;
    }

    private static void Assign(BiomechanicalMaterial[] slots, BiomechanicalMaterial source, int[] footprint)
    {
        var material = source.Clone();
        material.rootSlotId = footprint[0];
        material.occupiedSlotIds = new List<int>(footprint);
        foreach (int slot in footprint) slots[slot] = material;
    }
}
