using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 等距网格 A* 寻路系统：
/// - 自动感知地面层 (可通行) 与建筑/障碍层 (不可通行)
/// - 支持 8 方向平滑移动与严格防穿墙/切角 (Corner-Cutting) 检测
/// - 遇到障碍物时自动绕道规划最优路径
/// - 若目标点恰好是障碍物，自动寻路至距离目标最近的空闲相邻格
/// </summary>
public class IsometricPathfinder : MonoBehaviour
{
    [Header("Tilemaps (网格图层)")]
    public Tilemap groundTilemap;
    public Tilemap buildTilemap;

    [Header("Pathfinding Settings (寻路参数)")]
    [Tooltip("是否允许斜向移动（带防穿角校验）")]
    public bool allowDiagonal = true;
    [Tooltip("最大搜索步数，防止死循环")]
    public int maxSearchIterations = 2000;

    private class Node
    {
        public Vector3Int cell;
        public float gCost;
        public float hCost;
        public float fCost => gCost + hCost;
        public Node parent;

        public Node(Vector3Int cell, float gCost, float hCost, Node parent = null)
        {
            this.cell = cell;
            this.gCost = gCost;
            this.hCost = hCost;
            this.parent = parent;
        }
    }

    // 4 正交方向
    private static readonly Vector3Int[] CardinalDirs = new Vector3Int[]
    {
        new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0)
    };

    // 4 对角方向
    private static readonly Vector3Int[] DiagonalDirs = new Vector3Int[]
    {
        new Vector3Int(1, 1, 0),
        new Vector3Int(1, -1, 0),
        new Vector3Int(-1, 1, 0),
        new Vector3Int(-1, -1, 0)
    };

    public static IsometricPathfinder Instance { get; private set; }

    /// <summary>
    /// 动态障碍物占据的网格（例如放置状态的家具）
    /// </summary>
    public static readonly HashSet<Vector3Int> DynamicBlockedCells = new HashSet<Vector3Int>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        if (groundTilemap == null || buildTilemap == null)
        {
            var grid = FindObjectOfType<Grid>();
            if (grid != null)
            {
                var tms = grid.GetComponentsInChildren<Tilemap>();
                if (groundTilemap == null)
                    groundTilemap = System.Array.Find(tms, t => t.name == "Tilemap") ?? (tms.Length > 0 ? tms[0] : null);
                if (buildTilemap == null)
                    buildTilemap = System.Array.Find(tms, t => t.name != "Tilemap") ?? groundTilemap;
            }
        }
    }

    /// <summary>
    /// 将世界坐标转化为地面网格坐标
    /// </summary>
    public Vector3Int WorldToCell(Vector3 worldPos, float heightOffset = 0.5f)
    {
        if (groundTilemap != null)
            return groundTilemap.WorldToCell(worldPos - Vector3.up * heightOffset);
        return Vector3Int.zero;
    }

    /// <summary>
    /// 将网格坐标转化为世界中心坐标
    /// </summary>
    public Vector3 CellToWorld(Vector3Int cell, float heightOffset = 0.5f)
    {
        if (groundTilemap != null)
            return groundTilemap.GetCellCenterWorld(cell) + Vector3.up * heightOffset;
        return Vector3.zero;
    }

    /// <summary>
    /// 判断某个网格是否可通行：
    /// 1. 必须有地面方块支撑；
    /// 2. 不能有障碍物/建筑遮挡；
    /// 3. 不能有动态放置的家具阻挡。
    /// </summary>
    public bool IsWalkable(Vector3Int cell)
    {
        if (groundTilemap == null || !groundTilemap.HasTile(cell))
            return false;

        if (buildTilemap != null && buildTilemap.HasTile(cell))
            return false;

        if (DynamicBlockedCells.Contains(cell))
            return false;

        return true;
    }

    /// <summary>
    /// A* 核心寻路算法
    /// </summary>
    public List<Vector3Int> FindPath(Vector3Int startCell, Vector3Int targetCell)
    {
        if (startCell == targetCell)
            return new List<Vector3Int> { startCell };

        // 如果目标点本身不可通行（如点击了栅栏），自动寻找目标点周围最近的可通行邻居
        if (!IsWalkable(targetCell))
        {
            targetCell = FindNearestWalkableNeighbor(targetCell, startCell);
            if (!IsWalkable(targetCell))
            {
                // 无路可走
                return null;
            }
        }

        var openSet = new List<Node>();
        var closedSet = new HashSet<Vector3Int>();
        var allNodes = new Dictionary<Vector3Int, Node>();

        Node startNode = new Node(startCell, 0f, GetHeuristic(startCell, targetCell));
        openSet.Add(startNode);
        allNodes[startCell] = startNode;

        int iterations = 0;

        while (openSet.Count > 0 && iterations < maxSearchIterations)
        {
            iterations++;

            // 选取 fCost 最低的节点
            Node current = openSet[0];
            int currentIndex = 0;
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].fCost < current.fCost || (Mathf.Approximately(openSet[i].fCost, current.fCost) && openSet[i].hCost < current.hCost))
                {
                    current = openSet[i];
                    currentIndex = i;
                }
            }

            if (current.cell == targetCell)
            {
                return RetracePath(startNode, current);
            }

            openSet.RemoveAt(currentIndex);
            closedSet.Add(current.cell);

            // 检查 4 个正交邻居
            foreach (var dir in CardinalDirs)
            {
                Vector3Int neighborCell = current.cell + dir;
                if (closedSet.Contains(neighborCell) || !IsWalkable(neighborCell))
                    continue;

                float newGCost = current.gCost + 1.0f;
                ProcessNeighbor(current, neighborCell, newGCost, targetCell, openSet, allNodes);
            }

            // 检查 4 个对角邻居（带防切角检测）
            if (allowDiagonal)
            {
                foreach (var dir in DiagonalDirs)
                {
                    Vector3Int neighborCell = current.cell + dir;
                    if (closedSet.Contains(neighborCell) || !IsWalkable(neighborCell))
                        continue;

                    // 严格防切角检测：只有当对角所夹的两侧正交格子均可通行时，才允许走对角！
                    Vector3Int orth1 = new Vector3Int(current.cell.x + dir.x, current.cell.y, 0);
                    Vector3Int orth2 = new Vector3Int(current.cell.x, current.cell.y + dir.y, 0);

                    if (!IsWalkable(orth1) || !IsWalkable(orth2))
                        continue; // 被障碍物卡角，禁止穿行

                    float newGCost = current.gCost + 1.414f;
                    ProcessNeighbor(current, neighborCell, newGCost, targetCell, openSet, allNodes);
                }
            }
        }

        // 没找到可行路径
        return null;
    }

    private void ProcessNeighbor(Node current, Vector3Int neighborCell, float newGCost, Vector3Int targetCell, List<Node> openSet, Dictionary<Vector3Int, Node> allNodes)
    {
        if (allNodes.TryGetValue(neighborCell, out Node neighbor))
        {
            if (newGCost < neighbor.gCost)
            {
                neighbor.gCost = newGCost;
                neighbor.parent = current;
            }
        }
        else
        {
            Node newNode = new Node(neighborCell, newGCost, GetHeuristic(neighborCell, targetCell), current);
            allNodes[neighborCell] = newNode;
            openSet.Add(newNode);
        }
    }

    private float GetHeuristic(Vector3Int a, Vector3Int b)
    {
        // 对角欧几里得启发函数
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private List<Vector3Int> RetracePath(Node startNode, Node endNode)
    {
        var path = new List<Vector3Int>();
        Node current = endNode;

        while (current != null && current != startNode)
        {
            path.Add(current.cell);
            current = current.parent;
        }

        path.Reverse();
        return path;
    }

    /// <summary>
    /// 当点击了障碍物时，寻找距离起点最近的可通行邻接格
    /// </summary>
    public Vector3Int FindNearestWalkableNeighbor(Vector3Int blockedCell, Vector3Int startCell)
    {
        Vector3Int bestNeighbor = blockedCell;
        float bestDist = float.MaxValue;

        foreach (var dir in CardinalDirs)
        {
            Vector3Int neighbor = blockedCell + dir;
            if (IsWalkable(neighbor))
            {
                float dist = Vector3Int.Distance(neighbor, startCell);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestNeighbor = neighbor;
                }
            }
        }

        return bestNeighbor;
    }
}
