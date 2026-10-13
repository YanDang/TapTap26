using System;
using System.Collections.Generic;
using BiomechanicalCrafting;
using UnityEngine;

public sealed class CraftedPlacementTransaction : IDisposable
{
    private static readonly HashSet<Vector3Int> reservedCells = new HashSet<Vector3Int>();
    public readonly CraftedProduct Product;
    public readonly Vector3Int Cell;
    private bool finished;

    private CraftedPlacementTransaction(CraftedProduct product, Vector3Int cell)
    {
        Product = product;
        Cell = cell;
    }

    public static bool IsReserved(Vector3Int cell) => reservedCells.Contains(cell);

    public static CraftedPlacementTransaction TryBegin(CraftedProduct product, Vector3Int cell)
    {
        if (reservedCells.Contains(cell)) return null;
        var transaction = new CraftedPlacementTransaction(product, cell);
        if (!PlayerSessionData.TryReserveProduct(product, transaction)) return null;
        reservedCells.Add(cell);
        return transaction;
    }

    public GameObject Commit(BiomechanicalCombatAdapter adapter, Vector3 position, Func<bool> canPlace)
    {
        if (finished) return null;
        GameObject spawned = null;
        try
        {
            if (adapter == null || !canPlace()) return null;
            spawned = adapter.SpawnProductAtCell(Product, Cell, position);
            if (spawned == null) return null;
            if (!PlayerSessionData.CommitReservedProduct(Product, this))
            {
                spawned.SetActive(false);
                UnityEngine.Object.Destroy(spawned);
                return null;
            }
            return spawned;
        }
        catch (Exception exception)
        {
            if (spawned != null) { spawned.SetActive(false); UnityEngine.Object.Destroy(spawned); }
            Debug.LogException(exception);
            return null;
        }
        finally { Dispose(); }
    }

    public void Dispose()
    {
        if (finished) return;
        finished = true;
        reservedCells.Remove(Cell);
        PlayerSessionData.ReleaseReservedProduct(Product, this);
    }
}
