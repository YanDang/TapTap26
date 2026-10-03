using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;

/// <summary>
/// 升级版玩家控制器（包含 A* 寻路、短滑翻滚、自动攻击、模块化武器与家具交互系统）：
/// 1. 武器系统 (Weapon System - 基于 COMBAT_CRAFTING_INTEGRATION_SPEC 规范)：
///    - 支持装备不同特质武器（如：铁制佩剑、炽炎双手长剑）；
///    - 每次挥击根据武器属性造成伤害并削减怪物的韧性破防条；
///    - 怪物瘫痪 (STAGGER) 期间造成 1.5 倍金色暴击！
/// 2. 家具交互系统 (Furniture System - 放置 / 踢出 / 举起手持)：
///    - 靠近家具可点击 [踢出] 将其滑行冲撞敌人 (造成 125 伤害 + 180 破防)；
///    - 也可点击 [举起] 扛在头顶作为重型双手武器：
///      * 移速受到 30% 惩罚；
///      * 攻击力暴增至 142，挥舞间隔 1.94s，每次削破防 210 (绝杀破防)；
///      * 可随时投掷掷出或放下；
///      * 耐久归零时触发碎裂爆发 (trait_shatter_burst) 范围爆炸！
/// 3. 短滑翻滚躲避 (Dodge Roll)：
///    - 快速轻扫短滑触发带无敌帧的 360 度翻滚，闪避怪物前摇抓击。
/// 4. A* 寻路避障：
///    - 自动绕开栅栏、障碍物以及放置在地面的家具。
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Dependencies (依赖组件)")]
    public Grid grid;
    public Tilemap groundTilemap;
    public Tilemap buildTilemap;
    public IsometricPathfinder pathfinder;

    [Header("Health & Movement (生命与移动属性)")]
    public float maxHp = 100f;
    public float currentHp = 100f;
    public float baseMoveSpeed = 4.0f;
    public float groundHeightOffset = 0.5f;
    public bool canMove = true;

    [Header("Weapon Stats (当前装备武器)")]
    public WeaponData equippedWeapon;

    [Header("Held Furniture (当前手持家具)")]
    public FurnitureObject heldFurniture;

    [Header("Combat Visuals (战斗特效)")]
    [Tooltip("斩击光弧特效精灵")]
    public Sprite slashArcSprite;
    [Tooltip("重型砸击光弧精灵")]
    public Sprite heavySmashSprite;
    [Tooltip("当前锁定的敌人目标")]
    public EnemyController currentTarget;
    [Tooltip("近战攻击距离判定")]
    public float attackRange = 1.15f;

    [Header("Dodge Roll (短滑翻滚躲避设置)")]
    public float rollSpeed = 8.5f;
    public float rollDuration = 0.28f;
    public float rollCooldown = 0.45f;
    public float minSwipeDistance = 30f;
    public float maxSwipeDuration = 0.32f;

    [Header("Visuals (角色表现)")]
    public SpriteRenderer playerRenderer;
    public Transform shadowTransform;
    public float walkBobAmount = 0.07f;
    public float walkBobFrequency = 14f;

    [Header("Target Marker (点击移动光环)")]
    public Sprite clickMarkerSprite;
    public Color markerColor = new Color(0.2f, 1f, 0.7f, 0.85f);
    public Color blockedMarkerColor = new Color(1f, 0.3f, 0.3f, 0.85f);

    [Header("Click Sensitivity (点击灵敏度)")]
    public float clickMaxDistance = 45f;
    public float clickMaxDuration = 0.45f;

    [Header("Stamina System (精力 / 耐力规范)")]
    public float maxStamina = 100f;
    public float currentStamina = 100f;
    public float staminaRegenRate = 12f;
    public float rollStaminaCost = 15f;
    public float buildModeStaminaCost = 30f;

    [Header("Movement Control (移动与点击模式)")]
    [Tooltip("是否允许点击地面寻路移动（默认开启，与虚拟摇杆同时支持，满足不同玩家操作习惯）")]
    public bool enableClickToMove = true;

    [Header("Mobile Adaptation (手机适配与底部面板)")]
    [Tooltip("底部功能UI占屏幕高度比例（默认 0.16f，释放 84%+ 广阔视野）")]
    [Range(0f, 0.5f)]
    public float bottomUIDockRatio = 0.16f;
    [Tooltip("是否阻止点击底部面板区域触发人物移动或划动物体")]
    public bool enableBottomDockBlock = true;

    // 状态
    public bool IsInvulnerable => isRolling || isInvulnerable;
    public bool CanRoll => rollCooldownTimer <= 0f && !isRolling;
    public float RollCooldownTimer => rollCooldownTimer;
    public float RollCooldown => rollCooldown;
    private bool isRolling = false;
    private bool isInvulnerable = false;
    private float rollCooldownTimer = 0f;

    // 攻击冷却内置计时器（严格保证攻击间隔，彻底防止连续点击快速攻击作弊）
    private float attackCooldownTimer = 0f;
    private bool isAttackingAnim = false;

    // 寻路航点
    private readonly List<Vector3> waypoints = new List<Vector3>();
    private int currentWaypointIndex = 0;
    private bool isMoving = false;

    // 手势识别
    private Vector2 pointerDownPos;
    private float pointerDownTime;
    private bool isPointerDown = false;
    private int pointerFingerId = -1;

    // 连续点击敌人判定 (用于手持家具投掷)
    private EnemyController lastClickedEnemy = null;
    private float lastEnemyClickTime = 0f;
    private int consecutiveEnemyClickCount = 0;
    private const float DOUBLE_CLICK_TIME_WINDOW = 0.6f;

    // 标记
    private GameObject markerObject;
    private SpriteRenderer markerRenderer;
    private float markerTimer = 0f;

    private Camera cam;

    /// <summary>
    /// 获取当前实际移动速度（若举起家具则承受 30% 移速惩罚）
    /// </summary>
    public float EffectiveMoveSpeed
    {
        get
        {
            if (heldFurniture != null)
                return baseMoveSpeed * (1f - heldFurniture.moveSpeedPenalty);
            return baseMoveSpeed;
        }
    }

    /// <summary>
    /// 获取当前攻击力
    /// </summary>
    public float CurrentAttackDamage
    {
        get
        {
            if (heldFurniture != null) return heldFurniture.heldDamage;
            return equippedWeapon != null ? equippedWeapon.damage : 25f;
        }
    }

    /// <summary>
    /// 获取当前削韧破防力
    /// </summary>
    public float CurrentGuardBreakPower
    {
        get
        {
            if (heldFurniture != null) return heldFurniture.heldGuardBreak;
            return equippedWeapon != null ? equippedWeapon.guardBreakPower : 30f;
        }
    }

    /// <summary>
    /// 获取当前攻击间隔 (秒)
    /// </summary>
    public float CurrentAttackInterval
    {
        get
        {
            if (heldFurniture != null) return heldFurniture.heldAttackInterval;
            return equippedWeapon != null ? equippedWeapon.attackInterval : 0.75f;
        }
    }

    void Awake()
    {
        enableClickToMove = true;
        cam = Camera.main;

        if (grid == null)
            grid = FindObjectOfType<Grid>();

        if (grid != null)
        {
            var tms = grid.GetComponentsInChildren<Tilemap>();
            if (groundTilemap == null)
                groundTilemap = System.Array.Find(tms, t => t.name == "Tilemap") ?? (tms.Length > 0 ? tms[0] : null);
            if (buildTilemap == null)
                buildTilemap = System.Array.Find(tms, t => t.name != "Tilemap") ?? groundTilemap;
        }

        if (pathfinder == null)
        {
            pathfinder = GetComponent<IsometricPathfinder>() ?? FindObjectOfType<IsometricPathfinder>();
            if (pathfinder == null)
            {
                pathfinder = gameObject.AddComponent<IsometricPathfinder>();
                pathfinder.groundTilemap = groundTilemap;
                pathfinder.buildTilemap = buildTilemap;
            }
        }

        if (playerRenderer == null)
            playerRenderer = GetComponentInChildren<SpriteRenderer>();

        if (equippedWeapon == null)
            equippedWeapon = WeaponData.CreateDefault();

        CreateMarkerObject();
    }

    private void CreateMarkerObject()
    {
        markerObject = new GameObject("Player_Click_Marker");
        markerRenderer = markerObject.AddComponent<SpriteRenderer>();

        if (clickMarkerSprite != null)
            markerRenderer.sprite = clickMarkerSprite;

        var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (unlitShader != null)
            markerRenderer.material = new Material(unlitShader);

        markerRenderer.color = markerColor;
        markerRenderer.sortingOrder = 9;
        markerObject.SetActive(false);
    }

    void Update()
    {
        if (rollCooldownTimer > 0f)
            rollCooldownTimer -= Time.deltaTime;

        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.deltaTime;

        UpdateStaminaRegen();
        UpdateKeyboardMovement();
        HandleInput();
        UpdateTargetFacing();
        UpdateAutoAttack(); // 怪物在攻击范围内时自动触发近战攻击
        UpdateMovement();
        UpdateMarkerAnimation();
    }

    private void HandleInput()
    {
        if (!canMove || isRolling)
        {
            isPointerDown = false;
            pointerFingerId = -1;
            return;
        }

        // 手机触控：多点与单点触控友好处理
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                if (touch.phase == TouchPhase.Began)
                {
                    // 触摸如果落在 UI 上或落在底部控制底座区，忽略该手指作为地面交互
                    if (IsPointerOverUI(touch.fingerId) || IsInBottomDockArea(touch.position))
                        continue;

                    if (!isPointerDown)
                    {
                        pointerFingerId = touch.fingerId;
                        StartPointer(touch.position);
                    }
                }
                else if (touch.phase == TouchPhase.Ended)
                {
                    if (isPointerDown && touch.fingerId == pointerFingerId)
                    {
                        EvaluatePointerRelease(touch.position);
                        isPointerDown = false;
                        pointerFingerId = -1;
                    }
                }
                else if (touch.phase == TouchPhase.Canceled)
                {
                    if (touch.fingerId == pointerFingerId)
                    {
                        isPointerDown = false;
                        pointerFingerId = -1;
                    }
                }
            }
        }
        else
        {
            // 电脑鼠标操作
            if (Input.GetMouseButtonDown(0))
            {
                if (IsPointerOverUI(-1) || IsInBottomDockArea(Input.mousePosition))
                {
                    isPointerDown = false;
                    return;
                }
                StartPointer(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                if (isPointerDown)
                {
                    EvaluatePointerRelease(Input.mousePosition);
                }
                isPointerDown = false;
            }
        }
    }

    private void StartPointer(Vector2 screenPos)
    {
        pointerDownPos = screenPos;
        pointerDownTime = Time.time;
        isPointerDown = true;
    }

    /// <summary>
    /// 评估手势松开：
    /// 1. 划动家具 -> 踢出家具冲撞敌人；
    /// 2. 划动地面 -> 快速短滑翻滚躲避；
    /// 3. 轻触敌人 -> 锁定集火攻击（手持家具时重砸）；
    /// 4. 轻触家具 -> 靠近举起家具；
    /// 5. 手持家具时轻触身前地面 -> 平稳放下家具；
    /// 6. 轻触远处地面 -> A* 寻路避障走位。
    /// 完全无需额外屏幕按钮！
    /// </summary>
    private void EvaluatePointerRelease(Vector2 releasePos)
    {
        float duration = Time.time - pointerDownTime;
        Vector2 delta = releasePos - pointerDownPos;
        float distance = delta.magnitude;
        float speed = distance / Mathf.Max(duration, 0.001f);

        // 1. 判断是否为【快速短滑】(Swipe)
        if (distance >= minSwipeDistance && duration <= maxSwipeDuration && speed >= 120f)
        {
            Vector2 swipeDir = delta.normalized;

            // 踢家具判定：
            // 规则：
            // A. 玩家手中未手持家具 (heldFurniture == null)
            // B. 触控滑动起点必须正落在家具上 (touchDist <= 0.85f)
            // C. 玩家必须处于家具近战交互范围内 (playerDist <= 1.45f，严禁隔空超能力滑动物体！)
            if (cam != null && heldFurniture == null)
            {
                Vector3 startWorldPos = cam.ScreenToWorldPoint(pointerDownPos);
                startWorldPos.z = 0f;
                FurnitureObject touchedFurn = FurnitureObject.GetNearestPlaced(startWorldPos, 0.85f);

                if (touchedFurn != null)
                {
                    float playerDist = Vector2.Distance(transform.position, touchedFurn.transform.position);
                    if (playerDist <= 1.45f)
                    {
                        // 满足近身且手指划过家具：角色顺应手指滑动方向将家具飞速踢出冲撞！
                        Vector3 kickDir = new Vector3(swipeDir.x, swipeDir.y, 0f).normalized;
                        if (playerRenderer != null)
                        {
                            playerRenderer.flipX = kickDir.x < 0;
                        }

                        touchedFurn.Kick(kickDir);

                        if (DamageTextManager.Instance != null)
                        {
                            DamageTextManager.Instance.ShowText(touchedFurn.transform.position + Vector3.up * 0.5f, "KICK!", new Color(1f, 0.45f, 0.1f), 0.11f);
                        }
                        return;
                    }
                }
            }

            // 若手持沉重家具时遇袭执行滑动：战术放下家具在脚边并翻滚闪避！
            if (heldFurniture != null)
            {
                var dropFurn = heldFurniture;
                DropFurniture();
                dropFurn.PutDown(transform.position);
            }

            // 只要不是近身在家具上划动，一律触发玩家短滑翻滚闪避（无缝躲避怪物攻击）！
            if (rollCooldownTimer <= 0f)
            {
                StartCoroutine(PerformDodgeRollRoutine(swipeDir));
                return;
            }
        }

        // 2. 判断是否为【轻触点击】(Tap)
        if (distance < clickMaxDistance && duration <= clickMaxDuration)
        {
            OnScreenTap(releasePos);
        }
    }

    public bool IsInBottomDockArea(Vector2 screenPos)
    {
        if (!enableBottomDockBlock) return false;
        // 手机屏幕坐标 (0,0) 为左下角，y < Screen.height * bottomUIDockRatio 为底部功能底座区域
        return screenPos.y < Screen.height * bottomUIDockRatio;
    }

    private bool IsPointerOverUI(int fingerId)
    {
        if (EventSystem.current == null) return false;
        if (fingerId >= 0)
            return EventSystem.current.IsPointerOverGameObject(fingerId);
        return EventSystem.current.IsPointerOverGameObject();
    }

    public void OnScreenTap(Vector2 screenPosition)
    {
        if (cam == null || grid == null || groundTilemap == null) return;

        Vector3 worldPos = cam.ScreenToWorldPoint(screenPosition);
        worldPos.z = 0f;

        // 1. 优先检查是否点击到了敌人！
        EnemyController clickedEnemy = FindEnemyNear(worldPos, 0.95f);
        if (clickedEnemy != null && clickedEnemy.IsAlive)
        {
            SetAttackTarget(clickedEnemy);

            // 若当前正手持家具 / 临时重武器：连续点击敌人，直接把手中的武器/家具全力掷出！
            if (heldFurniture != null)
            {
                float timeSinceLastClick = Time.time - lastEnemyClickTime;
                if (clickedEnemy == lastClickedEnemy && timeSinceLastClick <= DOUBLE_CLICK_TIME_WINDOW)
                {
                    consecutiveEnemyClickCount++;
                }
                else
                {
                    consecutiveEnemyClickCount = 1;
                    lastClickedEnemy = clickedEnemy;
                }
                lastEnemyClickTime = Time.time;

                if (consecutiveEnemyClickCount >= 2)
                {
                    consecutiveEnemyClickCount = 0;
                    ThrowHeldFurnitureAtTarget(clickedEnemy);
                    return;
                }
                else
                {
                    if (DamageTextManager.Instance != null)
                    {
                        DamageTextManager.Instance.ShowText(
                            clickedEnemy.transform.position + Vector3.up * 0.8f,
                            "再点一次扔出! (TAP TO THROW)",
                            new Color(1f, 0.85f, 0.2f),
                            0.09f
                        );
                    }
                    float d = Vector2.Distance(transform.position, clickedEnemy.transform.position);
                    if (d <= attackRange)
                    {
                        PerformManualAttack();
                    }
                    return;
                }
            }

            // 未手持家具时：若在近战攻击距离内，立即挥砍；若在范围外，仅保持目标锁定，绝对不自动跑过去贴脸！
            float dist = Vector2.Distance(transform.position, clickedEnemy.transform.position);
            if (dist <= attackRange)
            {
                PerformManualAttack();
            }
            return;
        }

        // 2. 若当前正手持家具，点击角色极近身旁 (0.65m 内) 的地面 -> 平稳放下家具！
        if (heldFurniture != null)
        {
            float distToSelf = Vector2.Distance(transform.position, worldPos);
            if (distToSelf <= 0.65f)
            {
                PutDownHeldFurniture(worldPos);
                return;
            }
        }

        // 3. 若未手持家具，检查是否点击了地面的家具！
        if (heldFurniture == null)
        {
            FurnitureObject clickedFurn = FurnitureObject.GetNearestPlaced(worldPos, 0.85f);
            if (clickedFurn != null)
            {
                float distToFurn = Vector2.Distance(transform.position, clickedFurn.transform.position);
                if (distToFurn <= 1.45f)
                {
                    // 靠近时点击直接举起家具
                    HoldFurniture(clickedFurn);
                    return;
                }
                else
                {
                    // 较远时，先寻路前往家具身旁
                    Vector3Int furnCell = pathfinder != null ? pathfinder.WorldToCell(clickedFurn.transform.position) : grid.WorldToCell(clickedFurn.transform.position);
                    Vector3Int pCell = pathfinder != null ? pathfinder.WorldToCell(transform.position) : grid.WorldToCell(transform.position);
                    Vector3Int neighborCell = pathfinder != null ? pathfinder.FindNearestWalkableNeighbor(furnCell, pCell) : furnCell;
                    MoveToCell(neighborCell);
                    return;
                }
            }
        }

        // 4. 点击普通地面：在开启 enableClickToMove 时智能寻路前往指定地点！
        // 保留锁定目标不清除，方便玩家拉扯走位游击，并在怪物贴近时自动挥砍！
        MoveToWorldPoint(worldPos);
    }

    /// <summary>
    /// 世界坐标寻路移动接口
    /// </summary>
    public void MoveToWorldPoint(Vector3 worldPos)
    {
        if (!enableClickToMove || grid == null) return;
        Vector3 groundRayPos = new Vector3(worldPos.x, worldPos.y - groundHeightOffset, 0f);
        Vector3Int targetCell = grid.WorldToCell(groundRayPos);
        MoveToCell(targetCell);
    }

    /// <summary>
    /// 【连续点击敌人投掷手持武器/家具】：将头顶手持的重物全力掷向目标敌人
    /// </summary>
    public void ThrowHeldFurnitureAtTarget(EnemyController target)
    {
        if (heldFurniture == null || target == null) return;

        Vector3 diff = target.transform.position - transform.position;
        Vector3 throwDir = diff.normalized;
        float dist = diff.magnitude;

        if (playerRenderer != null)
        {
            playerRenderer.flipX = throwDir.x < 0;
        }

        var f = heldFurniture;
        DropFurniture();
        f.Throw(throwDir, dist + 0.6f);

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.8f, "💣 投掷飞砸! (THROWN)", new Color(1f, 0.35f, 0.15f), 0.13f);
        }
    }

    private void MoveToCell(Vector3Int targetCell)
    {
        Vector3 playerRayPos = new Vector3(transform.position.x, transform.position.y - groundHeightOffset, 0f);
        Vector3Int startCell = grid.WorldToCell(playerRayPos);

        List<Vector3Int> pathCells = pathfinder != null ? pathfinder.FindPath(startCell, targetCell) : null;

        if (pathCells != null && pathCells.Count > 0)
        {
            waypoints.Clear();
            foreach (var cell in pathCells)
            {
                Vector3 center = groundTilemap.GetCellCenterWorld(cell);
                Vector3 pt = new Vector3(center.x, center.y + groundHeightOffset, 0f);
                waypoints.Add(pt);
            }

            currentWaypointIndex = 0;
            isMoving = true;

            Vector3 finalDest = waypoints[waypoints.Count - 1];
            ShowMarker(finalDest, markerColor);
        }
        else if (groundTilemap != null && groundTilemap.HasTile(targetCell))
        {
            Vector3 center = groundTilemap.GetCellCenterWorld(targetCell);
            Vector3 pt = new Vector3(center.x, center.y + groundHeightOffset, 0f);
            ShowMarker(pt, blockedMarkerColor);
        }
    }

    private EnemyController FindEnemyNear(Vector3 worldPos, float radius)
    {
        var enemies = FindObjectsOfType<EnemyController>();
        EnemyController closest = null;
        float minDist = radius;

        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.IsAlive) continue;
            float d = Vector2.Distance(worldPos, enemy.transform.position);
            if (d < minDist)
            {
                minDist = d;
                closest = enemy;
            }
        }
        return closest;
    }

    public void SetAttackTarget(EnemyController enemy)
    {
        bool isNewTarget = (currentTarget != enemy);
        if (currentTarget != null && isNewTarget)
            currentTarget.SetSelected(false);

        currentTarget = enemy;
        if (currentTarget != null)
        {
            currentTarget.SetSelected(true);
            StopMovement();

            if (isNewTarget && DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(currentTarget.transform.position + Vector3.up * 0.5f, "TARGET", Color.yellow, 0.08f);
            }
        }
    }

    public void ClearAttackTarget()
    {
        if (currentTarget != null)
        {
            currentTarget.SetSelected(false);
            currentTarget = null;
        }
    }

    /// <summary>
    /// 朝向锁定的目标怪物（仅在静止站立时生效；移动时朝向移动方向）
    /// </summary>
    private void UpdateTargetFacing()
    {
        if (currentTarget == null || !currentTarget.IsAlive) return;

        if (!isMoving && !isRolling && !isAttackingAnim)
        {
            if (playerRenderer != null)
            {
                playerRenderer.flipX = currentTarget.transform.position.x < transform.position.x;
            }
        }
    }

    /// <summary>
    /// 自动攻击逻辑（根据需求重新启用）：
    /// 只要怪物进入攻击范围 (attackRange) 内，即自动出招挥砍；
    /// 核心规则：绝对不会自动寻路追着怪物打，玩家不被强行拖拽贴脸，走位与游击权100%归属玩家！
    /// </summary>
    private void UpdateAutoAttack()
    {
        if (isRolling || !canMove || isAttackingAnim) return;

        // 1. 优先检查当前锁定目标是否在近战攻击距离内
        EnemyController targetToAttack = null;
        if (currentTarget != null && currentTarget.IsAlive)
        {
            float dist = Vector2.Distance(transform.position, currentTarget.transform.position);
            if (dist <= attackRange)
            {
                targetToAttack = currentTarget;
            }
        }

        // 2. 若当前无锁定目标或目标在范围外，探测近战攻击范围内是否有其他存活怪物
        if (targetToAttack == null)
        {
            var nearbyEnemy = FindEnemyNear(transform.position, attackRange);
            if (nearbyEnemy != null && nearbyEnemy.IsAlive)
            {
                targetToAttack = nearbyEnemy;
                SetAttackTarget(nearbyEnemy);
            }
        }

        // 3. 目标在攻击范围内且攻击冷却完毕，自动出招挥砍！
        if (targetToAttack != null && attackCooldownTimer <= 0f)
        {
            attackCooldownTimer = CurrentAttackInterval;
            StartCoroutine(PerformAttackRoutine());
        }
    }

    public bool ConsumeStamina(float amount)
    {
        if (currentStamina >= amount)
        {
            currentStamina -= amount;
            return true;
        }
        return false;
    }

    private void UpdateStaminaRegen()
    {
        if (!isRolling)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenRate * Time.deltaTime);
        }
    }

    private void UpdateKeyboardMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(h) > 0.05f || Mathf.Abs(v) > 0.05f)
        {
            MoveWithInput(new Vector2(h, v).normalized);
        }
    }

    /// <summary>
    /// 虚拟摇杆与键盘 360° 平滑移动接口
    /// </summary>
    public void MoveWithInput(Vector2 moveDir)
    {
        if (!canMove || isRolling || moveDir.sqrMagnitude < 0.01f) return;

        // 摇杆主动输入时打断点击寻路
        StopMovement();

        Vector3 nextPos = transform.position + new Vector3(moveDir.x, moveDir.y, 0f) * (EffectiveMoveSpeed * Time.deltaTime);

        // 地面安全检测
        Vector3 groundRayPos = new Vector3(nextPos.x, nextPos.y - groundHeightOffset, 0f);
        Vector3Int nextCell = grid != null ? grid.WorldToCell(groundRayPos) : Vector3Int.zero;

        bool canPass = true;
        if (groundTilemap != null && !groundTilemap.HasTile(nextCell)) canPass = false;
        if (buildTilemap != null && buildTilemap.HasTile(nextCell)) canPass = false;
        if (IsometricPathfinder.DynamicBlockedCells.Contains(nextCell)) canPass = false;

        if (canPass)
        {
            transform.position = nextPos;
            if (playerRenderer != null)
            {
                if (moveDir.x > 0.05f) playerRenderer.flipX = false;
                else if (moveDir.x < -0.05f) playerRenderer.flipX = true;

                float bob = Mathf.Abs(Mathf.Sin(Time.time * walkBobFrequency)) * walkBobAmount;
                playerRenderer.transform.localPosition = new Vector3(0f, bob, 0f);
            }
        }
    }

    private IEnumerator PerformAttackRoutine()
    {
        if (currentTarget == null || !currentTarget.IsAlive) yield break;

        isAttackingAnim = true;
        Vector3 enemyPos = currentTarget.transform.position;
        Vector3 attackDir = (enemyPos - transform.position).normalized;

        float dmg = CurrentAttackDamage;
        float guardBreak = CurrentGuardBreakPower;

        // 生成斩击光弧或重砸特效
        SpawnSlashEffect(attackDir, heldFurniture != null);

        // 攻击微突进动作
        if (playerRenderer != null)
        {
            playerRenderer.transform.localPosition = attackDir * (heldFurniture != null ? 0.22f : 0.15f);
        }

        float hitDist = Vector2.Distance(transform.position, enemyPos);
        if (hitDist <= attackRange * 1.35f)
        {
            // 造成伤害并削弱破防值
            currentTarget.TakeDamage(dmg, guardBreak, transform.position);

            // 命中敌人恢复 8 点精力
            currentStamina = Mathf.Min(maxStamina, currentStamina + 8f);

            // 机制落地（需求5）：若是手持家具攻击，瞬间砸碎销毁，造成破片碎裂范围爆发！
            if (heldFurniture != null)
            {
                var f = heldFurniture;
                DropFurniture();
                f.TriggerShatterBurst();
            }
        }

        yield return new WaitForSeconds(heldFurniture != null ? 0.2f : 0.12f);

        if (playerRenderer != null)
        {
            playerRenderer.transform.localPosition = Vector3.zero;
        }
        isAttackingAnim = false;
    }

    private void SpawnSlashEffect(Vector3 dir, bool isHeavySmash)
    {
        Sprite spriteToUse = isHeavySmash ? (heavySmashSprite ?? slashArcSprite) : slashArcSprite;
        if (spriteToUse == null) return;

        GameObject slashGo = new GameObject("Player_Slash_Effect");
        slashGo.transform.position = transform.position + dir * 0.45f + Vector3.up * 0.2f;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        slashGo.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

        var sr = slashGo.AddComponent<SpriteRenderer>();
        sr.sprite = spriteToUse;
        var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (unlitShader != null) sr.material = new Material(unlitShader);

        // 手持家具猛砸为烈焰金橙色，常规剑击为青白银弧
        sr.color = isHeavySmash ? new Color(1f, 0.6f, 0.15f, 0.95f) : new Color(0.9f, 0.95f, 1f, 0.9f);
        sr.sortingOrder = 45;

        StartCoroutine(AnimateSlash(slashGo, sr, isHeavySmash));
    }

    private IEnumerator AnimateSlash(GameObject go, SpriteRenderer sr, bool isHeavy)
    {
        float dur = isHeavy ? 0.22f : 0.15f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            float startScale = isHeavy ? 1.2f : 0.9f;
            float endScale = isHeavy ? 1.9f : 1.4f;
            go.transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
            Color c = sr.color;
            c.a = 1f - t;
            sr.color = c;
            yield return null;
        }
        Destroy(go);
    }

    /// <summary>
    /// 快速短滑翻滚：拥有无敌帧，360度旋转翻滚，安全贴地防止冲出虚空
    /// （若举起家具，短滑翻滚会霸气地将家具往前抛出滑行冲撞！）
    /// </summary>
    private IEnumerator PerformDodgeRollRoutine(Vector2 swipeDir)
    {
        // 翻滚打断当前移动和自动攻击，全力闪避
        StopMovement();

        Vector3 rollDir = new Vector3(swipeDir.x, swipeDir.y, 0f).normalized;

        isRolling = true;
        isInvulnerable = true;
        rollCooldownTimer = rollCooldown;

        float elapsed = 0f;
        float totalSpin = (rollDir.x >= 0 ? -360f : 360f);

        while (elapsed < rollDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / rollDuration;

            Vector3 stepMove = rollDir * (rollSpeed * Time.deltaTime);
            Vector3 nextPos = transform.position + stepMove;

            // 地面安全检测
            Vector3 groundRayPos = new Vector3(nextPos.x, nextPos.y - groundHeightOffset, 0f);
            Vector3Int nextCell = grid != null ? grid.WorldToCell(groundRayPos) : Vector3Int.zero;

            bool canPass = true;
            if (groundTilemap != null && !groundTilemap.HasTile(nextCell))
                canPass = false;
            if (buildTilemap != null && buildTilemap.HasTile(nextCell))
                canPass = false;

            if (canPass)
            {
                transform.position = nextPos;
            }

            if (playerRenderer != null)
            {
                float currentAngle = Mathf.Lerp(0f, totalSpin, t);
                playerRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
            }

            yield return null;
        }

        if (playerRenderer != null)
        {
            playerRenderer.transform.localRotation = Quaternion.identity;
            playerRenderer.transform.localPosition = Vector3.zero;
        }

        isRolling = false;
        isInvulnerable = false;
    }

    private void UpdateMovement()
    {
        if (isRolling || !isMoving || waypoints.Count == 0 || currentWaypointIndex >= waypoints.Count)
        {
            if (!isAttackingAnim && playerRenderer != null && !isRolling)
            {
                playerRenderer.transform.localPosition = Vector3.zero;
            }
            return;
        }

        Vector3 currentTargetWp = waypoints[currentWaypointIndex];

        if (playerRenderer != null)
        {
            if (currentTargetWp.x > transform.position.x + 0.02f)
                playerRenderer.flipX = false;
            else if (currentTargetWp.x < transform.position.x - 0.02f)
                playerRenderer.flipX = true;
        }

        transform.position = Vector3.MoveTowards(transform.position, currentTargetWp, EffectiveMoveSpeed * Time.deltaTime);

        if (playerRenderer != null)
        {
            float bob = Mathf.Abs(Mathf.Sin(Time.time * walkBobFrequency)) * walkBobAmount;
            playerRenderer.transform.localPosition = new Vector3(0f, bob, 0f);
        }

        if (Vector3.Distance(transform.position, currentTargetWp) < 0.03f)
        {
            transform.position = currentTargetWp;
            currentWaypointIndex++;

            if (currentWaypointIndex >= waypoints.Count)
            {
                isMoving = false;
                waypoints.Clear();
                if (playerRenderer != null)
                {
                    playerRenderer.transform.localPosition = Vector3.zero;
                }
            }
        }
    }

    public void StopMovement()
    {
        isMoving = false;
        waypoints.Clear();
        if (playerRenderer != null && !isRolling)
        {
            playerRenderer.transform.localPosition = Vector3.zero;
        }
    }

    #region Furniture Interaction API (家具交互接口)

    /// <summary>
    /// 【举起家具】：将指定家具挂载在头顶
    /// </summary>
    public void HoldFurniture(FurnitureObject furn)
    {
        if (furn == null) return;
        heldFurniture = furn;
        furn.PickUp(this);

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.8f, "HELD FURNITURE!", new Color(1f, 0.75f, 0.2f), 0.1f);
        }
    }

    /// <summary>
    /// 【解绑手持家具】：当家具损坏、投掷或放下时调用
    /// </summary>
    public void DropFurniture()
    {
        heldFurniture = null;
    }

    /// <summary>
    /// 【放下手持家具】：将家具平稳放到脚前临近空格或点击位置
    /// </summary>
    public void PutDownHeldFurniture(Vector3? optionalDropPos = null)
    {
        if (heldFurniture == null) return;

        Vector3 dropPos = optionalDropPos ?? (transform.position + (playerRenderer != null && playerRenderer.flipX ? Vector3.left * 0.6f : Vector3.right * 0.6f));
        FurnitureObject f = heldFurniture;
        heldFurniture = null;
        f.PutDown(dropPos);

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(dropPos + Vector3.up * 0.5f, "PUT DOWN", Color.white, 0.08f);
        }
    }

    /// <summary>
    /// 【踢出附近家具】：寻找身边 1.6m 内的家具并沿面朝/目标方向踢出
    /// </summary>
    public void KickNearbyFurniture()
    {
        var furn = FurnitureObject.GetNearestPlaced(transform.position, 1.6f);
        if (furn == null) return;

        Vector3 kickDir = (playerRenderer != null && playerRenderer.flipX ? Vector3.left : Vector3.right);
        if (currentTarget != null && currentTarget.IsAlive)
        {
            kickDir = (currentTarget.transform.position - furn.transform.position).normalized;
        }

        furn.Kick(kickDir);

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.5f, "KICK!", new Color(1f, 0.45f, 0.1f), 0.1f);
        }
    }

    /// <summary>
    /// 【举起附近家具】
    /// </summary>
    public void PickUpNearbyFurniture()
    {
        var furn = FurnitureObject.GetNearestPlaced(transform.position, 1.6f);
        if (furn != null)
        {
            HoldFurniture(furn);
        }
    }

    /// <summary>
    /// 切换武器（在佩剑与炽炎大剑之间快速切换，用于测试规范数值）
    /// </summary>
    public void ToggleWeapon()
    {
        if (equippedWeapon == null || equippedWeapon.instanceId == "wpn_default_sword")
        {
            equippedWeapon = WeaponData.CreateFireGreatsword();
            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.6f, "装备: 炽炎双手长剑!", new Color(1f, 0.4f, 0.1f), 0.1f);
            }
        }
        else
        {
            equippedWeapon = WeaponData.CreateDefault();
            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.6f, "装备: 铁制佩剑", Color.white, 0.09f);
            }
        }
    }

    /// <summary>
    /// 供手机 UI 底座一键触发翻滚闪避（带无敌帧与音画特效）
    /// </summary>
    public bool TriggerDodgeRoll(Vector2? optionalDir = null)
    {
        if (rollCooldownTimer > 0f || isRolling) return false;

        // 翻滚消耗 15 点精力
        if (!ConsumeStamina(rollStaminaCost))
        {
            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.8f, "精力不足!", Color.yellow, 0.08f);
            }
            return false;
        }

        Vector2 dir = Vector2.right;
        if (optionalDir.HasValue && optionalDir.Value.sqrMagnitude > 0.01f)
        {
            dir = optionalDir.Value.normalized;
        }
        else if (currentTarget != null && currentTarget.IsAlive)
        {
            dir = (currentTarget.transform.position - transform.position).normalized;
        }
        else if (playerRenderer != null && playerRenderer.flipX)
        {
            dir = Vector2.left;
        }

        // 若手持家具，翻滚顺势往前霸气抛掷家具滑行冲撞
        if (heldFurniture != null)
        {
            var dropFurn = heldFurniture;
            DropFurniture();
            dropFurn.Throw(dir);
            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.8f, "THROW SMASH!", Color.red, 0.08f);
            }
        }

        StartCoroutine(PerformDodgeRollRoutine(dir));
        return true;
    }

    /// <summary>
    /// 供手机 UI 底座一键触发普通攻击（自动锁定附近敌人出招）
    /// </summary>
    public void PerformManualAttack()
    {
        if (isRolling) return;
        if (currentTarget == null || !currentTarget.IsAlive)
        {
            var nearest = FindEnemyNear(transform.position, 4.0f);
            if (nearest != null) SetAttackTarget(nearest);
        }

        if (currentTarget != null && currentTarget.IsAlive && attackCooldownTimer <= 0f && !isAttackingAnim)
        {
            attackCooldownTimer = CurrentAttackInterval;
            StartCoroutine(PerformAttackRoutine());
        }
    }

    #endregion

    public void TakeDamage(float damage)
    {
        if (IsInvulnerable) return;

        currentHp -= damage;
        currentHp = Mathf.Max(0f, currentHp);

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.4f, $"-{damage:0}", new Color(1f, 0.25f, 0.25f));
        }

        StartCoroutine(PlayerHurtRoutine());
    }

    private IEnumerator PlayerHurtRoutine()
    {
        if (playerRenderer != null)
        {
            playerRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            playerRenderer.color = Color.white;
        }
    }

    private void ShowMarker(Vector3 position, Color color)
    {
        if (markerObject != null)
        {
            markerObject.transform.position = position;
            markerObject.transform.localScale = Vector3.one * 1.3f;
            markerRenderer.color = color;
            markerTimer = 0.8f;
            markerObject.SetActive(true);
        }
    }

    private void UpdateMarkerAnimation()
    {
        if (markerObject == null || !markerObject.activeSelf) return;

        if (markerTimer > 0f)
        {
            markerTimer -= Time.deltaTime;
            float t = markerTimer / 0.8f;
            float scale = Mathf.Lerp(1.0f, 1.3f, t);
            markerObject.transform.localScale = new Vector3(scale, scale, 1f);

            Color c = markerRenderer.color;
            c.a = Mathf.Clamp01(t) * 0.85f;
            markerRenderer.color = c;

            if (markerTimer <= 0f)
            {
                markerObject.SetActive(false);
            }
        }
    }
}
