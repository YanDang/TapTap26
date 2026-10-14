using System;
using System.Collections.Generic;
using UnityEngine;
using BiomechanicalCrafting;

[Serializable]
public class DoorTrialProgress
{
    public string doorId;
    public int day;
    public bool paid;
    public bool cleared;
}

[Serializable]
public class SavedRunFurniture
{
    public CraftedProduct product;
    public Vector3 position;
    public Quaternion rotation;
    public float durability;
}

[Serializable]
public class RunProgressData
{
    public int energy = 8;
    public bool finished;
    public int finalFurnitureCount;
    public List<string> pendingDoors = new List<string>();
    public List<DoorTrialProgress> trials = new List<DoorTrialProgress>();
    public List<SavedRunFurniture> furniture = new List<SavedRunFurniture>();
}

public static class GameRunProgress
{
    public const int MaximumEnergy = 8;
    public const int FinalDay = 7;
    public const int DoorCost = 2;
    public const int CraftCost = 1;
    public const int HomeTarget = 8;
    public static bool WorldReady { get; set; }
    public static RunProgressData Data => PlayerSessionData.runProgress;
    public static string EndingTitle(int count) => count >= HomeTarget ? "安家" : count >= 4 ? "暂时落脚" : "仍在漂泊";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        WorldReady = false;
    }

    public static void NormalizeLoadedData()
    {
        if (Data.pendingDoors == null) Data.pendingDoors = new List<string>();
        if (Data.trials == null) Data.trials = new List<DoorTrialProgress>();
        if (Data.furniture == null) Data.furniture = new List<SavedRunFurniture>();
        Data.energy = Mathf.Clamp(Data.energy + Data.pendingDoors.Count * DoorCost, 0, MaximumEnergy);
        Data.pendingDoors.Clear();
        Data.trials.RemoveAll(trial => trial == null || string.IsNullOrEmpty(trial.doorId));
    }

    public static bool IsTrialCleared(string doorId)
    {
        return Data.trials.Exists(trial => trial.doorId == doorId && trial.day == PlayerSessionData.greenhouse.day && trial.cleared);
    }

    public static bool TryReserveDoor(string doorId, out string reason)
    {
        PlayerSessionData.EnsureInitialized();
        reason = "";
        if (Data.finished) { reason = "本局已结束"; return false; }
        if (Data.pendingDoors.Contains(doorId)) { reason = "门正在开启"; return false; }
        if (Data.trials.Exists(trial => trial.doorId == doorId && trial.day == PlayerSessionData.greenhouse.day && trial.paid)) return true;
        if (Data.energy < DoorCost) { reason = "每日精力不足：开门需要 2 点，请休息到下一天"; return false; }
        Data.energy -= DoorCost;
        Data.pendingDoors.Add(doorId);
        PlayerSessionData.SaveBackpackToStorage();
        return true;
    }

    public static void RefundDoor(string doorId)
    {
        if (!Data.pendingDoors.Remove(doorId)) return;
        Data.energy = Mathf.Min(MaximumEnergy, Data.energy + DoorCost);
        PlayerSessionData.SaveBackpackToStorage();
    }

    public static void CommitDoor(string doorId)
    {
        Data.pendingDoors.Remove(doorId);
        var trial = Data.trials.Find(entry => entry.doorId == doorId);
        if (trial == null) { trial = new DoorTrialProgress { doorId = doorId }; Data.trials.Add(trial); }
        trial.day = PlayerSessionData.greenhouse.day;
        trial.paid = true;
        trial.cleared = false;
        PlayerSessionData.SaveBackpackToStorage();
    }

    public static void MarkDoorCleared(string doorId)
    {
        var trial = Data.trials.Find(entry => entry.doorId == doorId && entry.day == PlayerSessionData.greenhouse.day);
        if (trial == null) return;
        trial.cleared = true;
        PlayerSessionData.SaveBackpackToStorage();
    }

    public static bool TryCraft(CraftedProduct product, BiomechanicalMaterial[] slots, List<int> consumed, out string reason)
    {
        PlayerSessionData.EnsureInitialized();
        reason = "";
        if (Data.finished) { reason = "本局已结束"; return false; }
        if (Data.energy < CraftCost) { reason = "每日精力不足：合成需要 1 点，请返回战场休息"; return false; }
        if (product == null || slots == null || consumed == null || !string.IsNullOrEmpty(product.productionId)) { reason = "合成数据无效或已提交"; return false; }
        var materials = new List<BiomechanicalMaterial>();
        var indices = new HashSet<int>(consumed);
        foreach (int index in indices)
        {
            if (index < 0 || index >= slots.Length || slots[index] == null) { reason = "材料已变化，请重新选择"; return false; }
            if (!materials.Contains(slots[index])) materials.Add(slots[index]);
        }
        if (materials.Count < 2) { reason = "至少需要两份材料"; return false; }
        foreach (var material in materials)
            for (int index = 0; index < slots.Length; index++)
                if (ReferenceEquals(slots[index], material) && !indices.Contains(index)) { reason = "材料占用不完整"; return false; }
        var evaluated = ShellCraftingRecipe.Evaluate(materials);
        if (evaluated == null || evaluated.id != product.id) { reason = "配方已变化，请重新选择"; return false; }
        product.productionId = Guid.NewGuid().ToString("N");
        foreach (int index in indices) slots[index] = null;
        PlayerSessionData.shellSlotItems = (BiomechanicalMaterial[])slots.Clone();
        PlayerSessionData.craftedProducts.Add(product);
        Data.energy -= CraftCost;
        PlayerSessionData.SaveCraftedProductsToStorage();
        return true;
    }

    public static int CountFurniture()
    {
        var counted = new HashSet<string>();
        foreach (var furniture in FurnitureObject.AllFurniture)
        {
            if (furniture == null || !furniture.gameObject.activeInHierarchy || furniture.currentDurability <= 0f ||
                furniture.currentState != FurnitureObject.FurnitureState.Placed || furniture.Deploying) continue;
            var product = furniture.originalProduct;
            if (product == null || string.IsNullOrEmpty(product.productionId) || product.isAberrantScrap ||
                product.id == "furn_gather_door" || furniture.GetComponent<GatherDoorAuraBehavior>() != null) continue;
            counted.Add(product.productionId);
        }
        return counted.Count;
    }

    public static void CaptureWorld()
    {
        if (!WorldReady || Data.finished) return;
        var saved = new List<SavedRunFurniture>();
        var ids = new HashSet<string>();
        foreach (var furniture in FurnitureObject.AllFurniture)
        {
            if (furniture == null || !furniture.gameObject.activeInHierarchy || furniture.currentDurability <= 0f) continue;
            var product = furniture.originalProduct;
            if (product == null || string.IsNullOrEmpty(product.productionId) || !ids.Add(product.productionId) ||
                PlayerSessionData.craftedProducts.Exists(item => item != null && item.productionId == product.productionId)) continue;
            saved.Add(new SavedRunFurniture { product = product, position = furniture.transform.position,
                rotation = furniture.transform.rotation, durability = furniture.currentDurability });
        }
        Data.furniture = saved;
    }

    public static void RestoreWorld(BiomechanicalCombatAdapter adapter)
    {
        if (WorldReady) return;
        foreach (var saved in Data.furniture)
        {
            if (saved == null || saved.product == null || saved.durability <= 0f ||
                PlayerSessionData.craftedProducts.Exists(item => item != null && item.productionId == saved.product.productionId)) continue;
            bool exists = FurnitureObject.AllFurniture.Exists(furniture => furniture != null && furniture.originalProduct != null &&
                furniture.originalProduct.productionId == saved.product.productionId);
            if (exists) continue;
            var pathfinder = IsometricPathfinder.Instance;
            if (pathfinder == null) continue;
            var spawned = adapter.SpawnProductAtCell(saved.product, pathfinder.WorldToCell(saved.position), saved.position);
            if (spawned == null) continue;
            spawned.transform.rotation = saved.rotation;
            spawned.GetComponent<FurnitureObject>().currentDurability = saved.durability;
        }
        WorldReady = true;
    }

    public static void Finish()
    {
        if (Data.finished) return;
        CaptureWorld();
        Data.finalFurnitureCount = CountFurniture();
        Data.finished = true;
        PlayerSessionData.SaveBackpackToStorage();
        EnemyController.isAIPaused = true;
        GameJam.TimeManager.I.Refresh();
    }
}
