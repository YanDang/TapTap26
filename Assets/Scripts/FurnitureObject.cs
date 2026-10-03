using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 家具三态实体（基于 COMBAT_CRAFTING_INTEGRATION_SPEC 规范）：
/// 1. 放置状态 (PLACED):
///    - 占据等距网格，阻挡玩家与怪物的 A* 寻路；
///    - 带有耐久度，可承受怪物攻击；
///    - 靠近时可被踢出 (Kick) 或举起 (Pick Up)。
/// 2. 滑行状态 (SLIDING):
///    - 踢出后沿冲量向量高速滑行 (slideSpeed = 11.5)；
///    - 撞击怪物造成 125 碰撞伤害与 180 削韧破防 (瞬间击穿怪物韧性并触发瘫痪)；
///    - 撞击或滑行结束后自动停在最近的有效地面网格，回退为放置态。
/// 3. 手持状态 (HELD):
///    - 被玩家举起作为重型武器，玩家移速受到 30% 惩罚；
///    - 攻击力大幅提升至 142，攻击间隔 1.94s，削韧破防 210；
///    - 可随时挥击、投掷或放下。
/// 4. 碎裂爆发 (trait_shatter_burst):
///    - 耐久度归零时产生 1.8 格高额范围破片爆破 (80 伤害 + 100 破防)。
/// </summary>
public class FurnitureObject : MonoBehaviour
{
    public enum FurnitureState
    {
        Placed,
        Sliding,
        Held
    }

    [Header("Identity & Profile")]
    public string instanceId = "furn_inst_table_2001";
    public string furnitureName = "玄岩熔火重型餐桌";
    public FurnitureState currentState = FurnitureState.Placed;

    [Header("Durability (耐久度)")]
    public float maxDurability = 680f;
    public float currentDurability = 680f;

    [Header("Kick & Slide Stats (踢出滑行参数)")]
    [Tooltip("滑行速度 (m/s，带平稳摩擦力阻尼)")]
    public float slideSpeed = 5.2f;
    [Tooltip("滑行最大距离 (米，约 2.5 格等距地块，防止飞出地图)")]
    public float slideDistance = 2.4f;
    [Tooltip("踢击冲撞伤害")]
    public float kickDamage = 110f;
    [Tooltip("踢击冲撞削韧破防 (恶魔满韧性为 100)")]
    public float kickGuardBreak = 120f;
    [Tooltip("踢击命中怪物消耗耐久")]
    public float kickDurabilityLoss = 35f;

    [Header("Held Weapon Stats (手持武器参数)")]
    [Tooltip("手持重砸单次伤害")]
    public float heldDamage = 135f;
    [Tooltip("手持重砸攻击间隔 (秒)")]
    public float heldAttackInterval = 1.55f;
    [Tooltip("手持重砸单次削韧")]
    public float heldGuardBreak = 150f;
    [Tooltip("每次重砸命中消耗耐久")]
    public float heldDurabilityLossPerHit = 25f;
    [Tooltip("手持移速惩罚比例 (20%)")]
    public float moveSpeedPenalty = 0.20f;

    [Header("Components & Visuals")]
    public SpriteRenderer furnitureRenderer;
    public Transform shadowTransform;
    public Collider2D furnitureCollider;
    public Sprite promptRingSprite;

    // 当前占据的等距网格坐标
    public Vector3Int occupiedCell { get; private set; }

    // 滑行协程引用
    private Coroutine slideCoroutine;

    // 交互提示物件
    private GameObject promptRing;
    private PlayerController holderPlayer;
    [HideInInspector] public bool isThrown = false;

    // 全局活跃家具列表
    public static readonly List<FurnitureObject> AllFurniture = new List<FurnitureObject>();

    void Awake()
    {
        if (furnitureRenderer == null)
            furnitureRenderer = GetComponentInChildren<SpriteRenderer>();

        // 彻底杜绝粉紫色材质：确保在 URP 2D 管线下始终绑定正确的 2D Unlit/Lit 材质
        if (furnitureRenderer != null)
        {
            if (furnitureRenderer.sharedMaterial == null || 
                furnitureRenderer.sharedMaterial.shader == null || 
                furnitureRenderer.sharedMaterial.shader.name.Contains("Error") ||
                furnitureRenderer.sharedMaterial.shader.name.StartsWith("Sprites/Default"))
            {
                var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
                if (unlitShader != null)
                {
                    furnitureRenderer.material = new Material(unlitShader);
                }
            }
        }

        if (furnitureCollider == null)
            furnitureCollider = GetComponent<Collider2D>();

        CreateInteractionVisual();
    }

    void OnEnable()
    {
        if (!AllFurniture.Contains(this))
            AllFurniture.Add(this);
    }

    void OnDisable()
    {
        AllFurniture.Remove(this);
        UnregisterFromGrid();
    }

    void Start()
    {
        if (currentState == FurnitureState.Placed)
        {
            SnapToNearestGrid();
        }
    }

    private void CreateInteractionVisual()
    {
        // 创建靠近时的提示光圈
        promptRing = new GameObject("Prompt_Ring");
        promptRing.transform.SetParent(transform, false);
        promptRing.transform.localPosition = new Vector3(0f, 0.05f, 0f);

        var sr = promptRing.AddComponent<SpriteRenderer>();
        var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (unlitShader != null) sr.material = new Material(unlitShader);
        sr.color = new Color(1f, 0.85f, 0.2f, 0.6f);
        sr.sortingOrder = 5;

        // 加载或生成简单的光圈形状
        if (promptRingSprite != null)
            sr.sprite = promptRingSprite;
        else
            sr.sprite = Resources.Load<Sprite>("target_ring") ?? Resources.Load<Sprite>("click_marker");
        promptRing.SetActive(false);
    }

    void Update()
    {
        if (currentState == FurnitureState.Placed)
        {
            // 动态前后遮挡深度排序
            if (furnitureRenderer != null)
            {
                furnitureRenderer.sortingOrder = Mathf.RoundToInt(-transform.position.y * 10) + 12;
            }

            // 检查玩家距离，近身 (1.4m 内) 高亮显示脚底交互金圈
            var player = FindObjectOfType<PlayerController>();
            if (player != null && player.heldFurniture == null)
            {
                float dist = Vector2.Distance(transform.position, player.transform.position);
                if (dist <= 1.4f && !promptRing.activeSelf)
                    promptRing.SetActive(true);
                else if (dist > 1.4f && promptRing.activeSelf)
                    promptRing.SetActive(false);
            }
            else if (promptRing.activeSelf)
            {
                promptRing.SetActive(false);
            }
        }
        else if (promptRing.activeSelf)
        {
            promptRing.SetActive(false);
        }
    }

    /// <summary>
    /// 对齐到当前最近的地面网格并注册为障碍物
    /// </summary>
    public void SnapToNearestGrid()
    {
        var pathfinder = IsometricPathfinder.Instance ?? FindObjectOfType<IsometricPathfinder>();
        if (pathfinder != null)
        {
            occupiedCell = pathfinder.WorldToCell(transform.position);
            transform.position = pathfinder.CellToWorld(occupiedCell);
            RegisterToGrid();
        }
        else
        {
            var grid = FindObjectOfType<Grid>();
            if (grid != null)
            {
                var tms = grid.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>();
                var ground = System.Array.Find(tms, t => t.name == "Tilemap") ?? (tms.Length > 0 ? tms[0] : null);
                if (ground != null)
                {
                    occupiedCell = ground.WorldToCell(transform.position - Vector3.up * 0.5f);
                    transform.position = ground.GetCellCenterWorld(occupiedCell) + Vector3.up * 0.5f;
                    RegisterToGrid();
                }
            }
        }
    }

    void OnDestroy()
    {
        AllFurniture.Remove(this);
        UnregisterFromGrid();
    }

    private void RegisterToGrid()
    {
        if (!AllFurniture.Contains(this))
        {
            AllFurniture.Add(this);
        }

        if (!IsometricPathfinder.DynamicBlockedCells.Contains(occupiedCell))
        {
            IsometricPathfinder.DynamicBlockedCells.Add(occupiedCell);
        }
    }

    private void UnregisterFromGrid()
    {
        IsometricPathfinder.DynamicBlockedCells.Remove(occupiedCell);
    }

    /// <summary>
    /// 【踢出家具 (Kick)】：从放置状态切换为滑行状态，沿指定方向冲撞敌人
    /// </summary>
    public void Kick(Vector3 direction)
    {
        if (currentState == FurnitureState.Held)
        {
            // 若原本处于手持状态，先解绑
            transform.SetParent(null);
            if (holderPlayer != null)
            {
                holderPlayer.DropFurniture();
                holderPlayer = null;
            }
        }

        UnregisterFromGrid();
        currentState = FurnitureState.Sliding;

        if (slideCoroutine != null)
            StopCoroutine(slideCoroutine);

        slideCoroutine = StartCoroutine(SlideRoutine(direction.normalized));
    }

    private IEnumerator SlideRoutine(Vector3 dir)
    {
        float duration = slideDistance / slideSpeed;
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 lastSafePos = startPos;

        var hitEnemies = new HashSet<EnemyController>();
        var pathfinder = IsometricPathfinder.Instance ?? FindObjectOfType<IsometricPathfinder>();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // 真实物理缓动：带平稳摩擦力阻尼的平滑减速，模拟沉重餐桌的冲撞滑行
            float easeT = Mathf.Sin(t * Mathf.PI * 0.5f);
            Vector3 candidatePos = Vector3.Lerp(startPos, startPos + dir * slideDistance, easeT);

            // 严格地形边界与障碍物拦截：
            if (pathfinder != null && pathfinder.groundTilemap != null)
            {
                Vector3Int currentCell = pathfinder.WorldToCell(candidatePos);

                // 1. 悬崖虚空检测：如果前方离开地面岛屿，严禁飞出！立即刹停在边缘！
                if (!pathfinder.groundTilemap.HasTile(currentCell))
                {
                    if (DamageTextManager.Instance != null)
                    {
                        DamageTextManager.Instance.ShowText(lastSafePos + Vector3.up * 0.4f, "HALT!", new Color(0.85f, 0.85f, 0.85f), 0.08f);
                    }
                    transform.position = lastSafePos;
                    break;
                }

                // 2. 障碍物碰撞检测：如果撞上防御方块/栅栏/建筑，立即撞停！
                if (pathfinder.buildTilemap != null && pathfinder.buildTilemap.HasTile(currentCell))
                {
                    if (DamageTextManager.Instance != null)
                    {
                        DamageTextManager.Instance.ShowText(lastSafePos + Vector3.up * 0.4f, "CLANG!", new Color(1f, 0.8f, 0.2f), 0.09f);
                    }
                    transform.position = lastSafePos;
                    break;
                }
            }

            transform.position = candidatePos;
            lastSafePos = candidatePos;

            // 撞击检测 (检测前方 0.65 范围内的敌人)
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 0.65f);
            foreach (var col in hits)
            {
                var enemy = col.GetComponent<EnemyController>() ?? col.GetComponentInParent<EnemyController>();
                if (enemy != null && enemy.IsAlive && !hitEnemies.Contains(enemy))
                {
                    hitEnemies.Add(enemy);

                    // 结算碰撞伤害与破防
                    enemy.TakeDamage(kickDamage, kickGuardBreak, transform.position);

                    if (DamageTextManager.Instance != null)
                    {
                        DamageTextManager.Instance.ShowText(enemy.transform.position + Vector3.up * 0.6f, "KICK SMASH!", new Color(1f, 0.4f, 0.1f), 0.12f);
                    }

                    // 机制落地（需求5）：家具用于冲撞攻击后立即碎裂销毁，引发破片爆炸！
                    TriggerShatterBurst();
                    yield break;
                }
            }

            yield return null;
        }

        // 若为抛掷投掷出的家具，落地直接碎裂爆发！
        if (isThrown)
        {
            TriggerShatterBurst();
            yield break;
        }

        // 滑行结束，归位回放置状态并对齐网格
        SettleIntoGrid();
    }

    /// <summary>
    /// 滑行结束归位到最近的可通行网格（绝对确保稳稳立在地面方块上，绝不悬空）
    /// </summary>
    private void SettleIntoGrid()
    {
        var pathfinder = IsometricPathfinder.Instance ?? FindObjectOfType<IsometricPathfinder>();
        if (pathfinder != null && pathfinder.groundTilemap != null)
        {
            Vector3Int cell = pathfinder.WorldToCell(transform.position);

            // 1. 若当前格无地面或被建筑占位，回退搜索临近空格
            if (!pathfinder.groundTilemap.HasTile(cell) || (pathfinder.buildTilemap != null && pathfinder.buildTilemap.HasTile(cell)))
            {
                cell = pathfinder.FindNearestWalkableNeighbor(cell, cell);
            }

            // 2. 深度兜底：若依然在虚空，在全图寻找最近的有效地面
            if (!pathfinder.groundTilemap.HasTile(cell))
            {
                float minD = float.MaxValue;
                Vector3Int bestCell = cell;
                foreach (var pos in pathfinder.groundTilemap.cellBounds.allPositionsWithin)
                {
                    if (pathfinder.groundTilemap.HasTile(pos) && (pathfinder.buildTilemap == null || !pathfinder.buildTilemap.HasTile(pos)))
                    {
                        float d = Vector3.Distance(transform.position, pathfinder.groundTilemap.GetCellCenterWorld(pos));
                        if (d < minD)
                        {
                            minD = d;
                            bestCell = pos;
                        }
                    }
                }
                cell = bestCell;
            }

            occupiedCell = cell;
            transform.position = pathfinder.CellToWorld(cell);
            RegisterToGrid();
        }
        else
        {
            SnapToNearestGrid();
        }

        currentState = FurnitureState.Placed;
    }

    /// <summary>
    /// 【举起家具 (Pick Up)】：玩家将家具举至头顶，转为重型手持武器
    /// </summary>
    public void PickUp(PlayerController player)
    {
        if (player == null) return;

        UnregisterFromGrid();
        currentState = FurnitureState.Held;
        holderPlayer = player;

        // 挂载到玩家身上，置于角色头顶偏上方
        transform.SetParent(player.transform, false);
        transform.localPosition = new Vector3(0f, 0.62f, -0.05f);
        transform.localScale = Vector3.one * 0.9f;

        if (promptRing != null)
            promptRing.SetActive(false);
    }

    /// <summary>
    /// 【投掷家具 (Throw)】：玩家将手持的家具向前掷出，变为高速滑行飞弹远距离重创目标
    /// </summary>
    public void Throw(Vector3 throwDir, float targetDist = 4.5f)
    {
        isThrown = true;
        slideDistance = Mathf.Clamp(targetDist, 2.5f, 6.0f);
        slideSpeed = 8.5f; // 投掷飞出速度更快更具冲击力
        transform.SetParent(null);
        transform.localScale = Vector3.one;
        Kick(throwDir);
    }

    /// <summary>
    /// 【放下家具 (Put Down)】：将家具平稳放在身前相邻有效格，避开与玩家自身重叠卡位
    /// </summary>
    public void PutDown(Vector3 targetWorldPos)
    {
        transform.SetParent(null);
        transform.localScale = Vector3.one;

        var pathfinder = IsometricPathfinder.Instance ?? FindObjectOfType<IsometricPathfinder>();
        var player = FindObjectOfType<PlayerController>();

        if (pathfinder != null)
        {
            Vector3Int dropCell = pathfinder.WorldToCell(targetWorldPos);

            // 若点击点落在玩家自己脚下的格子里，自动顺着朝向偏移至身前相邻有效地面格
            if (player != null)
            {
                Vector3Int playerCell = pathfinder.WorldToCell(player.transform.position);
                if (dropCell == playerCell)
                {
                    Vector3 offset = (targetWorldPos - player.transform.position);
                    if (offset.sqrMagnitude < 0.04f)
                    {
                        offset = (player.playerRenderer != null && player.playerRenderer.flipX ? Vector3.left : Vector3.right);
                    }
                    Vector3 adjacentPos = player.transform.position + offset.normalized * 0.7f;
                    dropCell = pathfinder.WorldToCell(adjacentPos);
                }
            }

            if (!pathfinder.IsWalkable(dropCell))
            {
                dropCell = pathfinder.FindNearestWalkableNeighbor(dropCell, dropCell);
            }

            occupiedCell = dropCell;
            transform.position = pathfinder.CellToWorld(dropCell);
            RegisterToGrid();
        }
        else
        {
            transform.position = targetWorldPos;
        }

        currentState = FurnitureState.Placed;
    }

    /// <summary>
    /// 受到攻击时扣除耐久度
    /// </summary>
    public void TakeHit(float damage)
    {
        currentDurability -= damage;
        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.4f, $"-{damage:0} 耐久", new Color(0.9f, 0.9f, 0.9f), 0.08f);
        }

        if (currentDurability <= 0f)
        {
            TriggerShatterBurst();
        }
    }

    /// <summary>
    /// 碎裂爆发 (trait_shatter_burst)：耐久归零时对周围产生破片范围高额爆炸
    /// </summary>
    public void TriggerShatterBurst()
    {
        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.5f, "💥 碎裂爆发!", new Color(1f, 0.3f, 0.1f), 0.12f);
        }

        // 范围伤害 80，范围破防 100
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, 1.8f);
        foreach (var col in colliders)
        {
            var enemy = col.GetComponent<EnemyController>() ?? col.GetComponentInParent<EnemyController>();
            if (enemy != null && enemy.IsAlive)
            {
                enemy.TakeDamage(80f, 100f, transform.position);
            }
        }

        UnregisterFromGrid();
        if (holderPlayer != null)
        {
            holderPlayer.DropFurniture();
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// 查找距离指定坐标最近的放置态家具
    /// </summary>
    public static FurnitureObject GetNearestPlaced(Vector3 position, float maxRange = 1.6f)
    {
        FurnitureObject best = null;
        float minDist = maxRange;

        foreach (var f in AllFurniture)
        {
            if (f == null || f.currentState != FurnitureState.Placed) continue;
            float d = Vector2.Distance(position, f.transform.position);
            if (d < minDist)
            {
                minDist = d;
                best = f;
            }
        }
        return best;
    }

    /// <summary>
    /// 获取指定网格上当前放置的家具
    /// </summary>
    public static FurnitureObject GetFurnitureAtCell(Vector3Int cell)
    {
        // 容错同步：若列表为空，同步场景中的活跃家具
        if (AllFurniture.Count == 0)
        {
            var activeFurns = FindObjectsOfType<FurnitureObject>();
            foreach (var f in activeFurns)
            {
                if (f != null && !AllFurniture.Contains(f))
                    AllFurniture.Add(f);
            }
        }

        // 清理可能已销毁的对象并查找目标
        for (int i = AllFurniture.Count - 1; i >= 0; i--)
        {
            if (AllFurniture[i] == null)
            {
                AllFurniture.RemoveAt(i);
                continue;
            }

            var f = AllFurniture[i];
            if (f.currentState == FurnitureState.Placed && f.occupiedCell == cell)
                return f;
        }

        return null;
    }
}
