using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 升级版怪物控制器（带智能状态机、危险前摇判定圈、破障攻击与游击脱战机制）：
/// 1. 移动速度与攻击节奏大幅优化：
///    - 移速由 2.0 降至 1.35，告别“贴脸恐怖追击”；
///    - 攻击前摇加长至 0.75s，配合地面【高亮红色危险判定圈 (Danger Zone)】，留给玩家充足的翻滚闪避或打断反应时间；
/// 2. 丰富索敌状态机 (游击战术支持)：
///    - 包含 Patrol(巡逻游荡)、Alert(警觉疑虑)、Chase(全力追击)、BreakBarrier(破障攻坚)；
///    - 玩家逃脱至 6.8m 外或利用地形掩体拉开距离，怪物头上出现 "?" 进入警觉搜寻，2.5s 后放弃追击退回原点！玩家可畅快实施 Hit-and-Run 游击战术！
/// 3. 防御建筑战术价值 (彻底解决穿墙 Bug)：
///    - 当玩家躲在防御栅栏/掩体后导致无路可达时，怪物绝对不再穿模直冲，而是将目标转向阻挡的建筑/家具，猛烈攻击路障！
/// 4. 塞尔达式时停适配：
///    - 支持全局 isAIPaused，建造模式下怪物完全冻结停滞，给玩家静谧的战术布防空间。
/// </summary>
public class EnemyController : MonoBehaviour
{
    public enum EnemyAIState
    {
        Patrol,       // 巡逻游荡
        Alert,        // 警觉搜寻 (头上 "?")
        Chase,        // 追击玩家
        BreakBarrier  // 攻击挡路防御设施
    }

    [Header("Attributes (怪物基础属性)")]
    public float maxHp = 100f;
    public float currentHp = 100f;
    public float moveSpeed = 1.35f;       // 移速适度调缓，节奏从容
    public float detectRange = 5.5f;      // 索敌警戒范围
    public float loseTargetRange = 6.8f;  // 游击脱战拉开距离
    public float attackRange = 0.95f;     // 攻击判定半径
    public float attackDamage = 15f;
    public float attackCooldown = 2.2f;   // 攻击间隔加长
    public float attackWindup = 0.75f;    // 蓄力前摇加长，给玩家充足反应时间

    [Header("Guard Break & Stagger (破防瘫痪规范参数)")]
    public float maxGuardBreak = 100f;
    public float currentGuardBreak = 100f;
    public float staggerDuration = 3.0f;

    [Header("Visuals (外观与特效)")]
    public SpriteRenderer monsterRenderer;
    public Transform shadowTransform;
    public Sprite clawSlashSprite;
    public Sprite targetRingSprite;

    [Header("Pathfinding (智能绕障寻路)")]
    public IsometricPathfinder pathfinder;
    public float repathInterval = 0.4f;

    [Header("AI State & Controls")]
    public EnemyAIState aiState = EnemyAIState.Patrol;
    public static bool isAIPaused = false; // 建造模式下全局冻结

    [Header("Respawn (复活刷新机制)")]
    public bool autoRespawn = true;
    public float respawnDelay = 4.5f;
    [HideInInspector] public float respawnCountdown = 0f;
    public bool IsRespawning => isDead && autoRespawn;

    public static readonly List<EnemyController> AllEnemies = new List<EnemyController>();

    [Header("Biomechanical Status Effects (生体状态效果)")]
    [HideInInspector] public float slowMultiplier = 1f;
    private float slowTimer = 0f;
    private float paralysisTimer = 0f;
    [HideInInspector] public bool isPacified = false;
    private float pacifiedTimer = 0f;
    [HideInInspector] public EnemyController pacifiedTargetEnemy = null;

    void OnEnable()
    {
        if (!AllEnemies.Contains(this))
            AllEnemies.Add(this);
    }

    void OnDisable()
    {
        AllEnemies.Remove(this);
    }

    public bool IsAlive => currentHp > 0 && !isDead;
    public bool IsStaggered => isStaggered;

    private bool isDead = false;
    private bool isAttacking = false;
    private bool isStaggered = false;
    private float staggerTimer = 0f;
    private float attackCooldownTimer = 0f;
    private float repathTimer = 0f;
    private float alertTimer = 0f;

    // 寻路航点
    private readonly List<Vector3> waypoints = new List<Vector3>();
    private int currentWaypointIndex = 0;
    private Vector3Int lastPlayerCell = new Vector3Int(int.MinValue, int.MinValue, 0);

    // 目标与位置
    private Vector3 spawnPosition;
    private PlayerController targetPlayer;
    private Coroutine attackCoroutine;

    // 指示器与血条
    private GameObject targetRingObject;
    private SpriteRenderer targetRingRenderer;
    private GameObject dangerZoneObject;
    private GameObject hpBarRoot;
    private Transform hpBarFill;
    private Transform guardBarFill;

    private Color originalColor = Color.white;
    private Vector3 originalScale = Vector3.one;

    void Awake()
    {
        isDead = false;
        isAttacking = false;
        isStaggered = false;
        currentHp = maxHp;
        currentGuardBreak = maxGuardBreak;

        spawnPosition = transform.position;

        if (monsterRenderer == null)
            monsterRenderer = GetComponentInChildren<SpriteRenderer>();

        if (monsterRenderer != null)
        {
            originalColor = monsterRenderer.color;
            if (originalColor.a < 0.1f) originalColor = Color.white;
            originalColor.a = 1f;
            monsterRenderer.color = originalColor;

            originalScale = monsterRenderer.transform.localScale;
            if (originalScale.magnitude < 0.1f) originalScale = Vector3.one;
            monsterRenderer.transform.localScale = originalScale;
        }

        if (pathfinder == null)
            pathfinder = IsometricPathfinder.Instance ?? FindObjectOfType<IsometricPathfinder>();

        CreateTargetRing();
        CreateDangerZoneIndicator();
        CreateHealthBar();

        targetPlayer = FindObjectOfType<PlayerController>();
    }

    void Start()
    {
        isDead = false;
        isAttacking = false;
        isStaggered = false;
        aiState = EnemyAIState.Patrol;

        if (monsterRenderer != null)
        {
            monsterRenderer.color = Color.white;
            monsterRenderer.transform.localScale = originalScale;
            monsterRenderer.transform.localPosition = Vector3.zero;
        }
        if (hpBarRoot != null) hpBarRoot.SetActive(true);
        UpdateHealthBar();
        UpdateGuardBar();
    }

    private void CreateTargetRing()
    {
        targetRingObject = new GameObject("Target_Ring");
        targetRingObject.transform.SetParent(transform, false);
        targetRingObject.transform.localPosition = new Vector3(0f, 0.05f, 0f);

        targetRingRenderer = targetRingObject.AddComponent<SpriteRenderer>();
        if (targetRingSprite != null) targetRingRenderer.sprite = targetRingSprite;

        var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (unlitShader != null) targetRingRenderer.material = new Material(unlitShader);

        targetRingRenderer.color = new Color(1f, 0.25f, 0.25f, 0.85f);
        targetRingRenderer.sortingOrder = 7;
        targetRingObject.SetActive(false);
    }

    private void CreateDangerZoneIndicator()
    {
        // 地面蓄力危险判定圈 (Danger Zone)
        dangerZoneObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        dangerZoneObject.name = "Danger_Zone_Indicator";
        dangerZoneObject.transform.SetParent(transform, false);
        dangerZoneObject.transform.localPosition = new Vector3(0f, 0f, 0.05f);
        dangerZoneObject.transform.localScale = new Vector3(attackRange * 2.2f, attackRange * 1.5f, 1f);
        Destroy(dangerZoneObject.GetComponent<Collider>());

        var mr = dangerZoneObject.GetComponent<MeshRenderer>();
        var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (unlitShader != null) mr.material = new Material(unlitShader);

        mr.material.color = new Color(1f, 0.15f, 0.15f, 0.45f);
        mr.sortingOrder = 6;
        dangerZoneObject.SetActive(false);
    }

    private void CreateHealthBar()
    {
        hpBarRoot = new GameObject("HPBar");
        hpBarRoot.transform.SetParent(transform, false);
        hpBarRoot.transform.localPosition = new Vector3(0f, 0.85f, 0f);

        var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");

        var hpBg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        hpBg.name = "HP_BG";
        hpBg.transform.SetParent(hpBarRoot.transform, false);
        hpBg.transform.localScale = new Vector3(0.7f, 0.08f, 1f);
        Destroy(hpBg.GetComponent<Collider>());
        var hpBgMr = hpBg.GetComponent<MeshRenderer>();
        if (unlitShader != null) hpBgMr.material = new Material(unlitShader);
        hpBgMr.material.color = new Color(0.12f, 0.12f, 0.15f, 0.95f);
        hpBgMr.sortingOrder = 30;

        var hpFill = GameObject.CreatePrimitive(PrimitiveType.Quad);
        hpFill.name = "HP_Fill";
        hpFill.transform.SetParent(hpBarRoot.transform, false);
        hpFill.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        hpFill.transform.localScale = new Vector3(0.66f, 0.06f, 1f);
        Destroy(hpFill.GetComponent<Collider>());
        var hpFillMr = hpFill.GetComponent<MeshRenderer>();
        if (unlitShader != null) hpFillMr.material = new Material(unlitShader);
        hpFillMr.material.color = new Color(0.95f, 0.22f, 0.22f, 1f);
        hpFillMr.sortingOrder = 31;
        hpBarFill = hpFill.transform;

        var guardBg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        guardBg.name = "Guard_BG";
        guardBg.transform.SetParent(hpBarRoot.transform, false);
        guardBg.transform.localPosition = new Vector3(0f, -0.065f, 0f);
        guardBg.transform.localScale = new Vector3(0.7f, 0.045f, 1f);
        Destroy(guardBg.GetComponent<Collider>());
        var guardBgMr = guardBg.GetComponent<MeshRenderer>();
        if (unlitShader != null) guardBgMr.material = new Material(unlitShader);
        guardBgMr.material.color = new Color(0.08f, 0.14f, 0.2f, 0.95f);
        guardBgMr.sortingOrder = 30;

        var guardFill = GameObject.CreatePrimitive(PrimitiveType.Quad);
        guardFill.name = "Guard_Fill";
        guardFill.transform.SetParent(hpBarRoot.transform, false);
        guardFill.transform.localPosition = new Vector3(0f, -0.065f, -0.01f);
        guardFill.transform.localScale = new Vector3(0.66f, 0.035f, 1f);
        Destroy(guardFill.GetComponent<Collider>());
        var guardFillMr = guardFill.GetComponent<MeshRenderer>();
        if (unlitShader != null) guardFillMr.material = new Material(unlitShader);
        guardFillMr.material.color = new Color(0.1f, 0.85f, 1f, 1f);
        guardFillMr.sortingOrder = 31;
        guardBarFill = guardFill.transform;
    }

    void Update()
    {
        if (isDead) return;

        // 建造模式或合成工坊/温室开启下全局时停冻结，给玩家静谧布防空间
        if (isAIPaused || PlayerSessionData.isCraftingOpen) return;

        // 麻痹硬直拦截（电鳗发电机、弹力金属等触发）
        if (paralysisTimer > 0f)
        {
            paralysisTimer -= Time.deltaTime;
            if (monsterRenderer != null)
            {
                float shake = Mathf.Sin(Time.time * 30f) * 0.04f;
                monsterRenderer.transform.localPosition = new Vector3(shake, 0f, 0f);
            }
            return;
        }

        // 减速计时更新
        if (slowTimer > 0f)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0f) slowMultiplier = 1f;
        }

        // 驯化反戈逻辑（小型生体培养箱触发）
        if (isPacified)
        {
            pacifiedTimer -= Time.deltaTime;
            if (pacifiedTimer <= 0f)
            {
                isPacified = false;
                pacifiedTargetEnemy = null;
            }
            else
            {
                UpdatePacifiedAI();
                return;
            }
        }

        // 瘫痪状态更新
        if (isStaggered)
        {
            staggerTimer -= Time.deltaTime;
            if (monsterRenderer != null)
            {
                float shake = Mathf.Sin(Time.time * 28f) * 0.04f;
                monsterRenderer.transform.localPosition = new Vector3(shake, 0f, 0f);
            }

            if (staggerTimer <= 0f) RecoverFromStagger();
            return;
        }

        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.deltaTime;

        if (targetPlayer == null)
            targetPlayer = FindObjectOfType<PlayerController>();

        if (targetPlayer == null || isAttacking) return;

        float dist = Vector2.Distance(transform.position, targetPlayer.transform.position);

        // ==========================================
        // 【AI 状态机更新】
        // ==========================================
        switch (aiState)
        {
            case EnemyAIState.Patrol:
                UpdatePatrolState(dist);
                break;

            case EnemyAIState.Alert:
                UpdateAlertState(dist);
                break;

            case EnemyAIState.Chase:
                UpdateChaseState(dist);
                break;

            case EnemyAIState.BreakBarrier:
                UpdateBreakBarrierState(dist);
                break;
        }
    }

    private void UpdatePatrolState(float distToPlayer)
    {
        // 玩家靠近警戒半径，发现目标切入追击！
        if (distToPlayer <= detectRange)
        {
            aiState = EnemyAIState.Chase;
            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.6f, "!", Color.red, 0.10f);
            }
            return;
        }

        // 在出生点周围轻微徘徊
        float distToSpawn = Vector2.Distance(transform.position, spawnPosition);
        if (distToSpawn > 0.4f)
        {
            transform.position = Vector3.MoveTowards(transform.position, spawnPosition, (moveSpeed * 0.5f) * Time.deltaTime);
        }
        else
        {
            if (monsterRenderer != null) monsterRenderer.transform.localPosition = Vector3.zero;
        }
    }

    private void UpdateAlertState(float distToPlayer)
    {
        alertTimer -= Time.deltaTime;

        // 玩家重新接近，再次发现！
        if (distToPlayer <= detectRange * 0.85f)
        {
            aiState = EnemyAIState.Chase;
            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.6f, "!", Color.red, 0.10f);
            }
            return;
        }

        // 警觉期结束，玩家彻底利用掩体或距离甩开仇恨，怪返回巡逻态（游击成功！）
        if (alertTimer <= 0f)
        {
            aiState = EnemyAIState.Patrol;
            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.6f, "放弃追击...", Color.gray, 0.08f);
            }
        }
    }

    private void UpdateChaseState(float distToPlayer)
    {
        // 游击战术支持：玩家拉开距离超过 loseTargetRange，丢失目标进入警觉搜寻态
        if (distToPlayer > loseTargetRange)
        {
            aiState = EnemyAIState.Alert;
            alertTimer = 2.5f;
            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.6f, "?", Color.yellow, 0.11f);
            }
            return;
        }

        // 进入近战范围且冷却就绪，发动带危险圈的前摇挥击
        if (distToPlayer <= attackRange && attackCooldownTimer <= 0f)
        {
            attackCoroutine = StartCoroutine(PerformAttackRoutine());
            return;
        }

        // 智能绕障追击
        ChasePlayerOrBarrier();
    }

    private void UpdateBreakBarrierState(float distToPlayer)
    {
        var blockingFurn = FurnitureObject.GetNearestPlaced(transform.position, 1.4f);
        if (blockingFurn != null && attackCooldownTimer <= 0f)
        {
            attackCooldownTimer = attackCooldown;
            blockingFurn.TakeHit(attackDamage, this);
            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(blockingFurn.transform.position + Vector3.up * 0.5f, "💥 撞击路障!", new Color(1f, 0.5f, 0.2f), 0.09f);
            }
        }
        else
        {
            // 若路障已破或目标重新可达，切回 Chase
            aiState = EnemyAIState.Chase;
        }
    }

    /// <summary>
    /// 严密寻路避障追击：彻底杜绝穿墙 Bug！
    /// 若路径被完全封死，转而攻击阻挡的路障！
    /// </summary>
    private void ChasePlayerOrBarrier()
    {
        if (pathfinder == null)
            pathfinder = IsometricPathfinder.Instance ?? FindObjectOfType<IsometricPathfinder>();

        repathTimer -= Time.deltaTime;

        Vector3Int playerCell = pathfinder != null ? pathfinder.WorldToCell(targetPlayer.transform.position) : Vector3Int.zero;
        Vector3Int myCell = pathfinder != null ? pathfinder.WorldToCell(transform.position) : Vector3Int.zero;

        if (repathTimer <= 0f || playerCell != lastPlayerCell || waypoints.Count == 0 || currentWaypointIndex >= waypoints.Count)
        {
            repathTimer = repathInterval;
            lastPlayerCell = playerCell;

            if (pathfinder != null)
            {
                var cellPath = pathfinder.FindPath(myCell, playerCell);
                if (cellPath != null && cellPath.Count > 0)
                {
                    waypoints.Clear();
                    foreach (var c in cellPath)
                    {
                        waypoints.Add(pathfinder.CellToWorld(c));
                    }
                    currentWaypointIndex = 0;
                    if (waypoints.Count > 1 && Vector2.Distance(transform.position, waypoints[0]) < 0.25f)
                    {
                        currentWaypointIndex = 1;
                    }
                }
                else
                {
                    // 核心修复（需求6）：如果玩家躲在全封闭防御建筑里导致无路可走，
                    // 绝对严禁穿模直线走过去！而是就近寻找阻挡的家具/路障并进行破坏！
                    waypoints.Clear();
                    var blockingFurn = FurnitureObject.GetNearestPlaced(transform.position, 2.2f);
                    if (blockingFurn != null)
                    {
                        aiState = EnemyAIState.BreakBarrier;
                    }
                    return;
                }
            }
        }

        // 沿航点移动
        if (waypoints.Count > 0 && currentWaypointIndex < waypoints.Count)
        {
            Vector3 targetWp = waypoints[currentWaypointIndex];
            targetWp.z = transform.position.z;
            Vector3 moveDir = (targetWp - transform.position).normalized;

            transform.position = Vector3.MoveTowards(transform.position, targetWp, moveSpeed * slowMultiplier * Time.deltaTime);

            if (monsterRenderer != null)
            {
                if (moveDir.x > 0.05f) monsterRenderer.flipX = false;
                else if (moveDir.x < -0.05f) monsterRenderer.flipX = true;

                float bob = Mathf.Abs(Mathf.Sin(Time.time * 8f)) * 0.03f;
                monsterRenderer.transform.localPosition = new Vector3(0f, bob, 0f);
            }

            if (Vector2.Distance(transform.position, targetWp) < 0.12f)
            {
                currentWaypointIndex++;
            }
        }
    }

    /// <summary>
    /// 攻击协程：展示地面【红色危险判定圈 (0.75s)】 -> 抓痕判定 -> 冷却
    /// </summary>
    private IEnumerator PerformAttackRoutine()
    {
        isAttacking = true;

        // 1. 身体蓄力泛红并微涨
        if (monsterRenderer != null)
        {
            monsterRenderer.color = new Color(1f, 0.35f, 0.15f, 1f);
            monsterRenderer.transform.localScale = originalScale * 1.2f;
        }

        // 2. 激活地面危险判定圈 (Danger Zone Indicator)
        if (dangerZoneObject != null)
        {
            dangerZoneObject.SetActive(true);
        }

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.5f, "!", Color.red, 0.11f);
        }

        // 3. 0.75 秒充足蓄力反应时间（闪烁提示）
        float elapsed = 0f;
        while (elapsed < attackWindup)
        {
            elapsed += Time.deltaTime;
            if (dangerZoneObject != null)
            {
                float pulse = 0.35f + Mathf.Sin(elapsed * 18f) * 0.15f;
                var mr = dangerZoneObject.GetComponent<MeshRenderer>();
                if (mr != null) mr.material.color = new Color(1f, 0.15f, 0.15f, pulse);
            }
            yield return null;
        }

        // 4. 隐藏判定圈，恢复体型
        if (dangerZoneObject != null) dangerZoneObject.SetActive(false);

        if (monsterRenderer != null)
        {
            monsterRenderer.color = originalColor;
            monsterRenderer.transform.localScale = originalScale;
        }

        SpawnClawEffect();

        // 5. 命中结算与闪避判定
        if (targetPlayer != null && !isStaggered)
        {
            float dist = Vector2.Distance(transform.position, targetPlayer.transform.position);
            if (dist <= attackRange * 1.15f)
            {
                if (targetPlayer.IsInvulnerable)
                {
                    if (DamageTextManager.Instance != null)
                    {
                        DamageTextManager.Instance.ShowText(targetPlayer.transform.position + Vector3.up * 0.4f, "DODGE!", new Color(0.2f, 1f, 0.8f), 0.11f);
                    }
                }
                else
                {
                    targetPlayer.TakeDamage(attackDamage);
                }
            }
        }

        attackCooldownTimer = attackCooldown;
        isAttacking = false;
        attackCoroutine = null;
    }

    private void SpawnClawEffect()
    {
        if (clawSlashSprite == null || targetPlayer == null) return;

        GameObject slashGo = new GameObject("Monster_Claw_Slash");
        slashGo.transform.position = (transform.position + targetPlayer.transform.position) * 0.5f + Vector3.up * 0.2f;

        var sr = slashGo.AddComponent<SpriteRenderer>();
        sr.sprite = clawSlashSprite;
        var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (unlitShader != null) sr.material = new Material(unlitShader);
        sr.sortingOrder = 50;

        StartCoroutine(AnimateSlash(slashGo, sr));
    }

    private IEnumerator AnimateSlash(GameObject go, SpriteRenderer sr)
    {
        float dur = 0.2f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            go.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.3f, t);
            Color c = sr.color;
            c.a = 1f - t;
            sr.color = c;
            yield return null;
        }
        Destroy(go);
    }

    public void TakeDamage(float damage, Vector3 attackerPos)
    {
        TakeDamage(damage, 30f, attackerPos);
    }

    public void TakeDamage(float damage, float guardBreakDamage, Vector3 attackerPos)
    {
        if (isDead) return;

        // 受伤立即激怒进入追击！
        aiState = EnemyAIState.Chase;

        float finalDmg = damage;
        bool isCrit = isStaggered;
        if (isCrit) finalDmg *= 1.5f;

        currentHp -= finalDmg;
        currentHp = Mathf.Max(0f, currentHp);

        if (!isStaggered)
        {
            currentGuardBreak -= guardBreakDamage;
            currentGuardBreak = Mathf.Max(0f, currentGuardBreak);
        }

        UpdateHealthBar();
        UpdateGuardBar();

        if (DamageTextManager.Instance != null)
        {
            Color dmgColor = isCrit ? new Color(1f, 0.85f, 0.2f) : Color.white;
            string prefix = isCrit ? "CRIT! " : "";
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.75f, $"{prefix}{finalDmg:0}", dmgColor, isCrit ? 0.11f : 0.08f);
        }

        if (currentGuardBreak <= 0f && !isStaggered && currentHp > 0f)
        {
            TriggerStagger();
        }

        if (currentHp <= 0f)
        {
            Die();
        }
        else
        {
            StartCoroutine(DamageFlashRoutine(attackerPos));
        }
    }

    public void TriggerStagger()
    {
        isStaggered = true;
        staggerTimer = staggerDuration;

        if (dangerZoneObject != null) dangerZoneObject.SetActive(false);

        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
            isAttacking = false;
        }

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 1.0f, "⚡ STAGGERED!", new Color(1f, 0.85f, 0.15f), 0.13f);
        }

        if (monsterRenderer != null)
        {
            monsterRenderer.color = new Color(0.7f, 0.7f, 1f, 1f);
        }
    }

    public void RecoverFromStagger()
    {
        isStaggered = false;
        currentGuardBreak = maxGuardBreak;
        UpdateGuardBar();

        if (monsterRenderer != null)
        {
            monsterRenderer.color = originalColor;
            monsterRenderer.transform.localPosition = Vector3.zero;
        }

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.8f, "复苏", Color.white, 0.08f);
        }
    }

    private IEnumerator DamageFlashRoutine(Vector3 attackerPos)
    {
        if (monsterRenderer == null) yield break;

        Vector3 pushDir = (transform.position - attackerPos).normalized;
        pushDir.z = 0;
        transform.position += pushDir * 0.08f;

        monsterRenderer.color = Color.red;
        yield return new WaitForSeconds(0.08f);

        if (!isStaggered)
            monsterRenderer.color = originalColor;
        else
            monsterRenderer.color = new Color(0.7f, 0.7f, 1f, 1f);
    }

    private void Die()
    {
        isDead = true;
        if (dangerZoneObject != null) dangerZoneObject.SetActive(false);
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        if (hpBarRoot != null) hpBarRoot.SetActive(false);
        if (targetRingObject != null) targetRingObject.SetActive(false);

        if (targetPlayer != null && targetPlayer.currentTarget == this)
        {
            targetPlayer.ClearAttackTarget();
        }

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.5f, "DEMON SLAIN!", new Color(1f, 0.3f, 0.1f), 0.13f);
        }

        StartCoroutine(DeathAndRespawnRoutine());
    }

    private IEnumerator DeathAndRespawnRoutine()
    {
        // 1. 死亡平滑淡出
        float dur = 0.45f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            if (monsterRenderer != null)
            {
                Color c = originalColor;
                c.a = 1f - t;
                monsterRenderer.color = c;
                monsterRenderer.transform.localScale = originalScale * (1f - t * 0.4f);
            }
            yield return null;
        }

        // 死亡期间禁用碰撞与渲染，彻底避免阻挡玩家与交互
        var colliders = GetComponentsInChildren<Collider2D>();
        foreach (var col in colliders) col.enabled = false;

        if (monsterRenderer != null) monsterRenderer.enabled = false;

        if (!autoRespawn)
        {
            Destroy(gameObject);
            yield break;
        }

        // 2. 倒计时复活等待
        respawnCountdown = respawnDelay;
        while (respawnCountdown > 0f)
        {
            if (!isAIPaused)
            {
                respawnCountdown -= Time.deltaTime;
            }
            yield return null;
        }

        // 3. 复活刷新：重置位置回初始出生点 (营地外)
        transform.position = spawnPosition;
        currentHp = maxHp;
        currentGuardBreak = maxGuardBreak;
        isStaggered = false;
        isAttacking = false;
        aiState = EnemyAIState.Patrol;
        lastPlayerCell = new Vector3Int(int.MinValue, int.MinValue, 0);
        waypoints.Clear();

        // 重新开启外观与碰撞
        if (monsterRenderer != null)
        {
            monsterRenderer.enabled = true;
            monsterRenderer.transform.localScale = originalScale;
            monsterRenderer.transform.localPosition = Vector3.zero;
        }
        foreach (var col in colliders) col.enabled = true;

        // 复活淡入
        dur = 0.5f;
        elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            if (monsterRenderer != null)
            {
                Color c = originalColor;
                c.a = t;
                monsterRenderer.color = c;
            }
            yield return null;
        }
        if (monsterRenderer != null) monsterRenderer.color = originalColor;

        if (hpBarRoot != null) hpBarRoot.SetActive(true);
        UpdateHealthBar();
        UpdateGuardBar();

        isDead = false;

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.8f, "⚠️ 恶魔在营地外苏醒复活了!", new Color(1f, 0.45f, 0.2f), 0.12f);
        }
    }

    public void SetSelected(bool selected)
    {
        if (targetRingObject != null)
        {
            targetRingObject.SetActive(selected && IsAlive);
        }
    }

    public void UpdateHealthBar()
    {
        if (hpBarFill != null)
        {
            float ratio = Mathf.Clamp01(currentHp / Mathf.Max(maxHp, 1f));
            hpBarFill.localScale = new Vector3(0.66f * ratio, 0.06f, 1f);
            hpBarFill.localPosition = new Vector3(-0.33f * (1f - ratio), 0f, -0.01f);
        }
    }

    public void UpdateGuardBar()
    {
        if (guardBarFill != null)
        {
            float ratio = Mathf.Clamp01(currentGuardBreak / Mathf.Max(maxGuardBreak, 1f));
            guardBarFill.localScale = new Vector3(0.66f * ratio, 0.035f, 1f);
            guardBarFill.localPosition = new Vector3(-0.33f * (1f - ratio), -0.065f, -0.01f);
        }
    }

    #region 生体构装战术状态接口 (Biomechanical Status Interfaces)

    /// <summary>
    /// 强力击退物理冲量（弹力金属、水刃炮台等机制）
    /// </summary>
    public void ApplyKnockback(Vector3 dir, float force, float duration = 0.35f)
    {
        if (isDead) return;
        StartCoroutine(KnockbackRoutine(dir.normalized, force, duration));
    }

    private IEnumerator KnockbackRoutine(Vector3 dir, float force, float duration)
    {
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + dir * force;
        var pfinder = pathfinder != null ? pathfinder : (IsometricPathfinder.Instance ?? FindObjectOfType<IsometricPathfinder>());

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = Mathf.Sin(t * Mathf.PI * 0.5f);
            Vector3 cand = Vector3.Lerp(startPos, targetPos, ease);

            // 地形边界保护：严禁飞出地图边缘虚空
            if (pfinder != null && pfinder.groundTilemap != null)
            {
                Vector3Int cell = pfinder.WorldToCell(cand);
                if (!pfinder.groundTilemap.HasTile(cell))
                {
                    break;
                }
            }

            transform.position = cand;
            yield return null;
        }
    }

    /// <summary>
    /// 施加减速状态（造水塔湿渍、烂泥陷阱等机制）
    /// </summary>
    public void ApplySlow(float slowPercent, float duration)
    {
        float factor = Mathf.Clamp01(1f - slowPercent / 100f);
        slowMultiplier = Mathf.Min(slowMultiplier, factor);
        slowTimer = Mathf.Max(slowTimer, duration);
    }

    /// <summary>
    /// 施加电弧麻痹硬直（电鳗电机、带电水潭等机制）
    /// </summary>
    public void ApplyParalysis(float duration)
    {
        if (isDead) return;
        paralysisTimer = Mathf.Max(paralysisTimer, duration);
        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.85f, "⚡ 麻痹硬直!", new Color(1f, 0.9f, 0.2f), 0.10f);
        }
    }

    /// <summary>
    /// 施加生体驯化反戈（小型生体培养箱机制）：使其停止攻击玩家，转而反戈攻击同胞
    /// </summary>
    public void ApplyPacify(float duration, EnemyController targetOther = null)
    {
        if (isDead) return;
        isPacified = true;
        pacifiedTimer = duration;
        pacifiedTargetEnemy = targetOther;

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 1.0f, "❤️ 驯化反戈!", new Color(1f, 0.45f, 0.75f), 0.12f);
        }
    }

    private void UpdatePacifiedAI()
    {
        if (pacifiedTargetEnemy == null || !pacifiedTargetEnemy.IsAlive)
        {
            // 自动从全局怪兽中寻找除自身外的另一只活跃怪物
            pacifiedTargetEnemy = AllEnemies.Find(e => e != null && e != this && e.IsAlive);
        }

        if (pacifiedTargetEnemy != null)
        {
            float dist = Vector2.Distance(transform.position, pacifiedTargetEnemy.transform.position);
            if (dist <= attackRange)
            {
                if (attackCooldownTimer <= 0f)
                {
                    attackCooldownTimer = attackCooldown;
                    pacifiedTargetEnemy.TakeDamage(attackDamage, 35f, transform.position);
                    if (DamageTextManager.Instance != null)
                    {
                        DamageTextManager.Instance.ShowText(pacifiedTargetEnemy.transform.position + Vector3.up * 0.6f, "❤️ 驯化撕咬!", new Color(1f, 0.5f, 0.8f), 0.10f);
                    }
                }
            }
            else
            {
                transform.position = Vector3.MoveTowards(transform.position, pacifiedTargetEnemy.transform.position, moveSpeed * slowMultiplier * Time.deltaTime);
            }
        }
    }

    #endregion
}
