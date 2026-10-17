using System;
using System.Collections.Generic;
using UnityEngine;
using BiomechanicalCrafting;

[Serializable]
public sealed class GreenhouseDayState
{
    public int day = 1;
    public int harvestedCount;
    public List<GreenhouseNodeState> nodes = new List<GreenhouseNodeState>();
}

[Serializable]
public sealed class GreenhouseDoorState
{
    public string doorId;
    public int lastUsedDay;
    public GreenhouseDayState greenhouse = new GreenhouseDayState();
}

[Serializable]
public sealed class GreenhouseNodeState
{
    public string materialId;
    public Vector2 position;
    public bool harvested;
}
