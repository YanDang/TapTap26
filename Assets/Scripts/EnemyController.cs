using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameJam;

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
        Attack,       // 普通攻击蓄力前摇 (黄光预警，可格挡)
        GrabWindup,   // 狂暴抓取蓄力前摇 (红光急促闪烁，触手狂舞，不可格挡)
        Grabbing,     // 触手缠绕勒紧 (周期扣血)
        Recover,      // 攻击/抓取后硬直恢复
        Pending,      // 完美格挡后虚弱处决就绪态 (通透世界慢动作，冰蓝脉冲)
        Executing,    // 处于被处决斩杀中
        Stagger,      // 破防瘫痪硬直
        BreakBarrier, // 攻击挡路防御设施
        Pacified      // 被培养箱安抚 (不攻击，走到培养箱旁发呆，挨打立刻苏醒)
    }

    [Header("Attributes (怪物基础属性)")]
    public float maxHp = 100f;
    public float currentHp = 100f;
    public float moveSpeed = 1.35f;       // 移速适度调缓，节奏从容
    public float detectRange = 14f;       // 索敌警戒范围 (覆盖整个营地防守战场，确保玩家在战场内始终处于感知域)
    public float loseTargetRange = 6.8f;  // 游击脱战拉开距离
    public float attackRange = 1.35f;     // 攻击判定半径
    public float attackDamage = 15f;
    public float attackCooldown = 2.2f;   // 攻击间隔加长
    public float attackWindup = 0.6f;     // 蓄力前摇

    [Header("Combat & Grab Tuning (战斗与抓取参数 - 来自 gamejam-integration)")]
    public float normalImpact = 0.6f;             // 普通攻击蓄力命中时点 (秒)
    public float windowStart = 0.05f;             // 格挡窗口开启时刻
    public float windowEnd = 0.5f;                // 格挡窗口结束时刻
    public float hitRange = 1.85f;                // 攻击命中有效距离
    [Range(0f, 1f)] public float grabChance = 0.45f; // 抓取发起概率
    public float grabWindup = 1.4f;               // 抓取前摇蓄力时长 (红光闪烁)
    public float grabRange = 1.65f;               // 抓取范围判定 (缩小至近战合理距离)
    public float grabHold = 2.8f;                 // 缠住时长
    public float grabTick = 0.85f;                // 缠住挤压扣血周期
    public float grabDamage = 10f;                // 每次挤压伤害
    public bool forceGrabNext = false;            // 强制下一次攻击为抓取 (测试用)
    [Header("时钟倒计时转盘 QTE (时停处决)")]
    public bool enableClockQTE = true;            // 开启时钟倒计时转盘 QTE
    [Range(0f, 1f)] public float clockChance = 0.65f;
    public float clockDuration = 1.6f;
    public bool forceClockNext = false;
    public GameJam.ClockQTE currentClock;

    public static readonly Color AttackGlow = new Color(1f, 0.8f, 0.08f); // 金黄色 = 可格挡
    public static readonly Color GrabGlow = new Color(1f, 0.08f, 0.05f);   // 鲜红色 = 抓取不可格挡
    public static readonly Color PendingGlow = new Color(0.55f, 0.9f, 1f); // 冰蓝色 = 处决就绪

    public static EnemyController Grabber;        // 当前正在进行抓取的唯一怪物
    public static EnemyController FindPending()
    {
        for (int i = 0; i < AllEnemies.Count; i++)
        {
            var e = AllEnemies[i];
            if (e != null && e.aiState == EnemyAIState.Pending && e.IsAlive)
                return e;
        }
        return null;
    }

    /// <summary>
    /// 动态构建并生成一只带完整生体动画、碰撞与 AI 的敌人实例（用于门扉试炼涌现）
    /// </summary>
    public static EnemyController CreateEnemyInstance(Vector3 position, string enemyName = "Enemy_Squid")
    {
        var go = new GameObject(enemyName);
        go.transform.position = position;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.45f;
        col.offset = new Vector2(0f, 0.2f);

        // 创建 VisualRoot
        var vrGo = new GameObject("VisualRoot");
        vrGo.transform.SetParent(go.transform, false);

        var ec = go.AddComponent<EnemyController>();
        ec.visualRoot = vrGo.transform;
        ec.autoAttachSquid = true;
        ec.squidScale = 0.85f;
        ec.squidTargetHeight = 1.75f;
        ec.detectRange = 16f;
        ec.chaseGiveUpRange = 25f;
        ec.autoRespawn = false;
        ec.spawnPosition = position;
        ec.maxHp = 100f;
        ec.currentHp = 100f;
        ec.originalColor = Color.white;
        ec.aiState = EnemyAIState.Chase;

        // 创建阴影 Shadow (在 EnemyController 初始化之后创建，避免被误判为主渲染器)
        var shadowGo = new GameObject("Shadow");
        shadowGo.transform.SetParent(go.transform, false);
        shadowGo.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        var shadowSr = shadowGo.AddComponent<SpriteRenderer>();
        shadowSr.sprite = Resources.Load<Sprite>("UI/ui_circle");
        shadowSr.color = new Color(0f, 0f, 0f, 0.35f);
        shadowGo.transform.localScale = new Vector3(0.7f, 0.35f, 1f);
        shadowSr.sortingOrder = 2;

        return ec;
    }

    public static EnemyController CreateTriggerfishInstance(Vector3 position, string enemyName = "Enemy_Triggerfish")
    {
        var prefab = Resources.Load<GameObject>("Enemies/Enemy_Triggerfish");
        if (prefab == null || prefab.GetComponent<EnemyController>() == null)
        {
            Debug.LogWarning("[EnemyController] Missing triggerfish prefab; spawning a squid instead.");
            return CreateEnemyInstance(position, enemyName);
        }
        var instance = Instantiate(prefab, position, Quaternion.identity);
        instance.name = enemyName;
        var enemy = instance.GetComponent<EnemyController>();
        enemy.spawnPosition = position;
        enemy.autoRespawn = false;
        enemy.aiState = EnemyAIState.Chase;
        return enemy;
    }

    [Header("Day Difficulty & Mutation (天数难度递增与精英异化)")]
    public int enemyDayLevel = 1;
    public bool isElite = false;

    /// <summary>
    /// 根据当前天数动态强化敌人属性（生命、攻击、攻速、移速均随天数递增）
    /// 并可赋予精英异化词缀（体型变大、粉紫光泽、更强威胁）
    /// </summary>
    public void ApplyDayDifficulty(int day, bool makeElite = false)
    {
        day = Mathf.Max(1, day);
        enemyDayLevel = day;
        isElite = makeElite;

        float dayFactor = day - 1;
        float hpMult = 1.0f + dayFactor * 0.20f;       // 基础 HP 每天 +20%
        float dmgMult = 1.0f + dayFactor * 0.15f;      // 攻击力每天 +15%
        float spdMult = 1.0f + dayFactor * 0.05f;      // 移速每天 +5%
        float cdReduce = dayFactor * 0.10f;            // 攻击间隔每天缩短 0.10s

        if (makeElite)
        {
            hpMult *= 1.35f;
            dmgMult *= 1.25f;
            spdMult *= 1.10f;
            cdReduce += 0.15f;
        }

        maxHp = Mathf.Round(maxHp * hpMult);
        currentHp = maxHp;
        attackDamage = Mathf.Round(attackDamage * dmgMult);
        moveSpeed = Mathf.Min(2.1f, moveSpeed * spdMult);
        attackCooldown = Mathf.Max(1.35f, attackCooldown - cdReduce);
        grabDamage = Mathf.Round(grabDamage * dmgMult);
        grabChance = Mathf.Min(0.70f, grabChance + dayFactor * 0.04f);

        if (makeElite)
        {
            if (visualRoot != null)
            {
                visualRoot.localScale *= 1.22f;
            }
            originalColor = new Color(1f, 0.78f, 0.95f);
            ApplyFlash(0.35f, new Color(1f, 0.2f, 0.8f));
            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 1.5f, "⚡ 【高危异化精英】出现!", new Color(1f, 0.25f, 0.85f), 0.16f);
            }
        }
    }

    private float emergenceRemaining;
    public bool IsEmerging => emergenceRemaining > 0f;

    public void BeginEmergence(float duration)
    {
        emergenceRemaining = Mathf.Max(0f, duration);
        aiState = EnemyAIState.Chase;
        stateTimer = 0f;
        attackCooldownTimer = Mathf.Max(attackCooldown, 0.5f);
        SetMoveAnimation(false);
    }

    public bool ParryWindowOpen
    {
        get
        {
            if (ExternalAI != null) return IsAlive && !IsEmerging && ExternalParryWindow != null && ExternalParryWindow(this);
            if (IsEmerging || aiState != EnemyAIState.Attack) return false;
            float end = (currentClock != null && currentClock.Open) ? currentClock.duration * 0.9f : windowEnd;
            return stateTimer >= windowStart && stateTimer <= end;
        }
    }

    [Header("Guard Break & Stagger (破防瘫痪规范参数)")]
    public float maxGuardBreak = 100f;
    public float currentGuardBreak = 100f;
    public float staggerDuration = 3.0f;

    [Header("Visuals (外观与特效)")]
    public SpriteRenderer monsterRenderer;
    public Transform visualRoot;
    public Animator enemyAnimator;
    public Transform shadowTransform;
    public Sprite clawSlashSprite;
    public Sprite targetRingSprite;
    private SpriteRenderer[] visualRenderers;
    public Renderer[] flashRenderers;
    private MaterialPropertyBlock _mpb;
    private Material billboardMat;

    [Header("Squid Rig (鱿鱼骨骼动画与面部)")]
    public SquidAnimator squid;
    public SquidFace squidFace;
    public bool autoAttachSquid = true;
    public float squidTargetHeight = 1.75f;
    public float squidHover = 0.2f;
    public float squidScale = 1.0f;

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

    public static void NotifyPlayerDefeated()
    {
        RedirectAllToFurniture();
    }

    public static void NotifyPlayerRevived()
    {
        for (int i = 0; i < AllEnemies.Count; i++)
        {
            var e = AllEnemies[i];
            if (e != null && e.IsAlive)
            {
                e.wasChasingPlayer = true;
            }
        }
    }

    public static void RedirectAllToFurniture()
    {
        for (int i = 0; i < AllEnemies.Count; i++)
        {
            var e = AllEnemies[i];
            if (e != null && e.ExternalAI == null && e.IsAlive && e.aiState != EnemyAIState.Executing && e.aiState != EnemyAIState.Pacified)
            {
                if (e.targetPlayer != null && e.targetPlayer.IsGrabbed && EnemyController.Grabber == e)
                {
                    e.targetPlayer.ThrowFrom(e);
                }
                var furn = FurnitureObject.NearestTargetable(e.transform.position, 60f);
                if (furn != null)
                {
                    e.furnitureTarget = furn;
                    e.wasChasingPlayer = false;
                    if (e.aiState == EnemyAIState.Patrol || e.aiState == EnemyAIState.Alert)
                    {
                        e.aiState = EnemyAIState.Chase;
                    }
                }
            }
        }
    }

    [Header("Biomechanical Status Effects (生体状态效果)")]
    [HideInInspector] public float slowMultiplier = 1f;
    private float slowTimer = 0f;
    private float paralysisTimer = 0f;
    [HideInInspector] public bool isPacified = false;
    private float pacifiedTimer = 0f;
    [HideInInspector] public EnemyController pacifiedTargetEnemy = null;
    [HideInInspector] public EnemyController currentTargetEnemy = null;
    private float pacifiedHeartbeatTimer = 0f;

    [Header("Wet & Electric Status (水塔淋湿与感电 - 对齐 gamejam-integration)")]
    private float wetUntil = -1f;
    public void Wet(float duration) { float strength=1; if (FilterControl(EnemyControlKind.Wet, ref strength, ref duration)) wetUntil = Mathf.Max(wetUntil, Time.time + duration); }
    public bool IsWet => Time.time < wetUntil;

    [Header("Pacify (培养箱安抚机制 - 对齐 gamejam-integration)")]
    public float pacifyImmuneUntil = 0f;
    public FurnitureObject pacifierFurniture = null;
    public bool Pacified => aiState == EnemyAIState.Pacified;
    public bool CanBePacified => AllowsControl(EnemyControlKind.Pacify) && IsAlive && !IsEmerging && (aiState == EnemyAIState.Patrol || aiState == EnemyAIState.Alert || aiState == EnemyAIState.Chase || aiState == EnemyAIState.Recover) && Grabber != this && Time.time >= pacifyImmuneUntil;

    [Header("Furniture Targeting (家具目标与游击脱战 - 对齐 gamejam-integration)")]
    public float furnitureSearchRange = 9f;
    public float chaseGiveUpRange = 18f;
    public float giveUpFurnitureRange = 16f;
    public FurnitureObject furnitureTarget = null;
    public int gaveUpChases = 0;
    private float blockedTimer = 0f;
    private bool wasChasingPlayer = false;
    private float giveUpTextCooldown = 0f;

    public void ForceTauntTarget(FurnitureObject target)
    {
        if (!AllowsControl(EnemyControlKind.Taunt)) return;
        if (isDead || aiState == EnemyAIState.Executing || aiState == EnemyAIState.Grabbing || aiState == EnemyAIState.Pacified) return;
        furnitureTarget = target;
        wasChasingPlayer = false;
        if (aiState != EnemyAIState.Attack && aiState != EnemyAIState.GrabWindup)
        {
            aiState = EnemyAIState.Chase;
        }
    }

    public void Pacify(float seconds, FurnitureObject by)
    {
        if (!CanBePacified) return;
        float strength=1;
        if (!FilterControl(EnemyControlKind.Pacify, ref strength, ref seconds)) return;
        if (currentClock != null) { currentClock.Close(); currentClock = null; }
        if (squid != null) squid.Interrupt();
        aiState = EnemyAIState.Pacified;
        pacifiedTimer = seconds;
        pacifierFurniture = by;
        furnitureTarget = null;
        wasChasingPlayer = false;
        pacifyImmuneUntil = Time.time + seconds + 4.0f;
    }

    public void WakeFromPacify()
    {
        pacifierFurniture = null;
        aiState = EnemyAIState.Chase;
    }

    void OnEnable()
    {
        if (!AllEnemies.Contains(this))
            AllEnemies.Add(this);
    }

    void OnDisable()
    {
        AllEnemies.Remove(this);
        if (Grabber == this) Grabber = null;
    }

    public bool IsAlive => currentHp > 0 && !isDead;
    public bool IsStaggered => isStaggered;
    public bool IsParalyzed => paralysisTimer > 0f;
    public bool IsAttacking => aiState == EnemyAIState.Attack || aiState == EnemyAIState.GrabWindup || aiState == EnemyAIState.Grabbing;

    private bool isDead = false;
    private bool isStaggered = false;
    private float staggerTimer = 0f;
    private float attackCooldownTimer = 0f;
    private float repathTimer = 0f;
    private float alertTimer = 0f;
    private float stateTimer = 0f;
    private float grabTickTimer = 0f;
    private bool impactDone = false;
    private float flashUntil = -1f;
    private float flashDuration = 0.12f;
    private Color flashColor = Color.white;
    private Vector3 knockVel = Vector3.zero;

    // 寻路航点
    private readonly List<Vector3> waypoints = new List<Vector3>();
    private int currentWaypointIndex = 0;
    private Vector3Int lastPlayerCell = new Vector3Int(int.MinValue, int.MinValue, 0);

    // 目标与位置
    public Vector3 spawnPosition;
    private PlayerController targetPlayer;
    private Coroutine attackCoroutine;

    // 指示器与血条
    public bool showAttackRange = false;
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
        isStaggered = false;
        currentHp = maxHp;
        currentGuardBreak = maxGuardBreak;

        detectRange = Mathf.Max(detectRange, 14f);
        chaseGiveUpRange = Mathf.Max(chaseGiveUpRange, 18f);

        spawnPosition = transform.position;

        if (visualRoot == null)
        {
            var vr = transform.Find("VisualRoot");
            if (vr != null) visualRoot = vr;
        }

        Transform sortTarget = visualRoot != null ? visualRoot : transform;
        var sg = sortTarget.GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (sg == null) sg = sortTarget.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
        sg.sortingLayerName = "Default";
        sg.sortingOrder = 10;

        if (visualRoot != null)
        {
            visualRenderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
            if (enemyAnimator == null)
            {
                enemyAnimator = visualRoot.GetComponentInChildren<Animator>();
            }
        }

        if (monsterRenderer == null)
        {
            var v = transform.Find("Visual");
            if (v != null) monsterRenderer = v.GetComponent<SpriteRenderer>();
        }

        originalColor = Color.white;
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

        _mpb = new MaterialPropertyBlock();
        billboardMat = Resources.Load<Material>("Materials/SpriteBillboard");
        if (GameJam.TimeManager.I == null)
        {
            var tm = new GameObject("TimeManager");
            tm.AddComponent<GameJam.TimeManager>();
        }

        if (enemyAnimator == null)
        {
            enemyAnimator = GetComponentInChildren<Animator>();
        }

        if (pathfinder == null)
            pathfinder = IsometricPathfinder.Instance ?? FindObjectOfType<IsometricPathfinder>();

        CreateTargetRing();
        CreateDangerZoneIndicator();
        CreateHealthBar();

        targetPlayer = FindObjectOfType<PlayerController>();

        if (autoAttachSquid)
        {
            AttachSquid();
        }
    }

    public void RegisterExtraRenderers(IEnumerable<SpriteRenderer> extra)
    {
        if (extra == null) return;
        var list = new List<SpriteRenderer>();
        if (visualRenderers != null) list.AddRange(visualRenderers);
        list.AddRange(extra);
        visualRenderers = list.ToArray();
    }

    public void AttachSquid()
    {
        if (squid != null) return;
        var prefab = Resources.Load<GameObject>("Squid/Squid");
        if (prefab == null) return;

        if (monsterRenderer != null) monsterRenderer.enabled = false;
        if (enemyAnimator != null) enemyAnimator.enabled = false;

        Transform parentTransform = visualRoot != null ? visualRoot : transform;
        foreach (Transform child in parentTransform)
        {
            if (child.name != "SquidRig")
                child.gameObject.SetActive(false);
        }

        var go = Instantiate(prefab, parentTransform);
        go.name = "SquidRig";

        // 用骨骼量尺寸计算自适应归一化缩放（Squid 原模高约 46.5 单位）
        float minY = float.MaxValue, maxY = float.MinValue, bodyX = 0f;
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            var lp = go.transform.InverseTransformPoint(t.position);
            minY = Mathf.Min(minY, lp.y);
            maxY = Mathf.Max(maxY, lp.y);
            if (t.name == "身体根") bodyX = lp.x;
        }
        float modelHeight = Mathf.Max(maxY - minY, 1e-3f);
        float k = (squidTargetHeight * squidScale) / modelHeight;
        go.transform.localScale = Vector3.one * k;
        go.transform.localPosition = new Vector3(-bodyX * k, -minY * k + squidHover, 0f);

        squid = go.GetComponent<SquidAnimator>() ?? go.AddComponent<SquidAnimator>();
        squid.motion = transform;
        squidFace = go.GetComponent<SquidFace>() ?? go.AddComponent<SquidFace>();

        if (billboardMat == null)
            billboardMat = Resources.Load<Material>("Materials/SpriteBillboard");

        var srs = go.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            if (billboardMat != null) srs[i].sharedMaterial = billboardMat;
            srs[i].color = Color.white;
        }
        flashRenderers = srs;
        RegisterExtraRenderers(srs);
    }

    void Start()
    {
        isDead = false;
        isStaggered = false;
        if (ExternalAI == null && aiState != EnemyAIState.Chase)
        {
            aiState = EnemyAIState.Patrol;
        }

        if (monsterRenderer != null)
        {
            monsterRenderer.color = Color.white;
            monsterRenderer.transform.localScale = originalScale;
            monsterRenderer.transform.localPosition = Vector3.zero;
        }
        if (squid != null)
        {
            originalColor = Color.white;
        }
        SetVisualColor(originalColor);
        SetVisualFacing(false);
        SetMoveAnimation(false);

        if (hpBarRoot != null) hpBarRoot.SetActive(true);
        UpdateHealthBar();
        UpdateGuardBar();
    }

    public void SetVisualFacing(bool faceLeft)
    {
        if (squid != null)
        {
            // 章鱼骨骼是程序化动画，通过 GrabHold(side) 与 Attack(side) 原生控制左右侧出招
            // 严禁翻转 visualRoot.localScale.x 为负，否则局部坐标系反向会导致触手反向空抓！
            if (visualRoot != null)
            {
                Vector3 s = visualRoot.localScale;
                s.x = Mathf.Abs(s.x > 0.001f ? s.x : 1f);
                visualRoot.localScale = s;
            }
            return;
        }

        if (visualRoot != null)
        {
            Vector3 s = visualRoot.localScale;
            s.x = (faceLeft ? -1f : 1f) * Mathf.Abs(s.y > 0.001f ? s.y : 1f);
            visualRoot.localScale = s;
        }
        else if (monsterRenderer != null)
        {
            monsterRenderer.flipX = faceLeft;
        }
    }

    public void SetMoveAnimation(bool moving)
    {
        if (enemyAnimator != null && enemyAnimator.enabled && enemyAnimator.runtimeAnimatorController != null)
        {
            enemyAnimator.SetFloat("Speed", moving ? 1f : 0f);
        }
    }

    public void SetVisualColor(Color color)
    {
        if (monsterRenderer != null) monsterRenderer.color = color;
        if (visualRenderers != null)
        {
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                if (visualRenderers[i] != null) visualRenderers[i].color = color;
            }
        }
    }

    public void SetVisualAlpha(float alpha)
    {
        if (monsterRenderer != null)
        {
            Color c = originalColor;
            c.a = alpha;
            monsterRenderer.color = c;
        }
        if (visualRenderers != null)
        {
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                if (visualRenderers[i] != null)
                {
                    Color c = visualRenderers[i].color;
                    c.a = alpha;
                    visualRenderers[i].color = c;
                }
            }
        }
    }

    public void SetVisualPosition(Vector3 localPos)
    {
        if (visualRoot != null) visualRoot.localPosition = localPos;
        else if (monsterRenderer != null) monsterRenderer.transform.localPosition = localPos;
    }

    /// <summary>
    /// 短暂受击闪白 (支持无缩放时间)
    /// </summary>
    public void Flash(Color c, float dur = 0.12f)
    {
        flashColor = c;
        flashDuration = dur;
        flashUntil = Time.unscaledTime + dur;
    }

    /// <summary>
    /// 骨骼多部件高光染色（黄光预警、红光狂舞、处决冰蓝脉冲）
    /// </summary>
    public void ApplyFlash(float persistent, Color persistentColor)
    {
        float hit = Mathf.Clamp01((flashUntil - Time.unscaledTime) / Mathf.Max(flashDuration, 1e-3f));
        Color c = hit > persistent ? flashColor : persistentColor;
        float amount = Mathf.Max(hit * 0.85f, persistent);

        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        if (flashRenderers != null && flashRenderers.Length > 0)
        {
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                var r = flashRenderers[i];
                if (r != null)
                {
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor("_FlashColor", c);
                    _mpb.SetFloat("_Flash", amount);
                    r.SetPropertyBlock(_mpb);
                }
            }
        }
        else if (visualRenderers != null && visualRenderers.Length > 0)
        {
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                var r = visualRenderers[i];
                if (r != null)
                {
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor("_FlashColor", c);
                    _mpb.SetFloat("_Flash", amount);
                    r.SetPropertyBlock(_mpb);
                }
            }
        }
        if (monsterRenderer != null)
        {
            monsterRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_FlashColor", c);
            _mpb.SetFloat("_Flash", amount);
            monsterRenderer.SetPropertyBlock(_mpb);
        }
    }

    /// <summary>
    /// 物理击退平滑阻尼
    /// </summary>
    public EnemyCombatPolicy CombatPolicy;
    public System.Action<EnemyController, float> ExternalAI;
    public System.Func<EnemyController, bool> ExternalParryWindow;
    public event System.Action<EnemyController> Died;
    public static bool CombatWorldPaused => PlayerSessionData.IsCombatInputBlocked || BiomechanicalAuraBase.IsCombatPaused || TimeManager.Hitstop;
    public bool CanExecute => IsAlive && !IsEmerging && (CombatPolicy == null || CombatPolicy.CanExecute(this));
    public bool FilterControl(EnemyControlKind kind, ref float strength, ref float duration, UnityEngine.Object source = null)
        => CombatPolicy == null || CombatPolicy.ControlFromSource(this, kind, ref strength, ref duration, source);
    bool AllowsControl(EnemyControlKind kind)
    { float strength=1, duration=0; return FilterControl(kind, ref strength, ref duration); }
    public void Displace(Vector3 direction, float distance, UnityEngine.Object source = null)
    {
        float duration=0;
        if (FilterControl(EnemyControlKind.Knockback, ref distance, ref duration, source)) transform.position += direction * distance;
    }
    public void Knockback(Vector3 dir, float distance)
    {
        if (IsEmerging) return;
        float duration=0;
        if (!FilterControl(EnemyControlKind.Knockback, ref distance, ref duration)) return;
        dir.z = 0f;
        knockVel = dir.normalized * distance * 8f;
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
        hpBarRoot.transform.localPosition = new Vector3(0f, 1.35f, 0f);

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
        if (isAIPaused || PlayerSessionData.isCraftingOpen || (ExternalAI != null && CombatWorldPaused))
        {
            SetMoveAnimation(false);
            return;
        }

        if (IsEmerging)
        {
            emergenceRemaining = Mathf.Max(0f, emergenceRemaining - Time.deltaTime);
            SetMoveAnimation(false);
            SetVisualAlpha(IsEmerging ? 0.45f + 0.2f * Mathf.Sin(Time.time * 12f) : 1f);
            if (!IsEmerging)
            {
                stateTimer = 0f;
                aiState = EnemyAIState.Chase;
                attackCooldownTimer = Mathf.Max(attackCooldown, 0.5f);
            }
            return;
        }

        // 麻痹硬直拦截（电鳗发电机、弹力金属等触发）
        if (paralysisTimer > 0f)
        {
            SetMoveAnimation(false);
            paralysisTimer -= Time.deltaTime;
            float shake = Mathf.Sin(Time.time * 30f) * 0.04f;
            SetVisualPosition(new Vector3(shake, 0f, 0f));

            if (paralysisTimer <= 0f)
            {
                SetVisualPosition(Vector3.zero);
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
                SetVisualColor(originalColor);
                SetVisualPosition(Vector3.zero);
                if (DamageTextManager.Instance != null)
                {
                    DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.7f, "💢 驯化解除!", Color.red, 0.10f);
                }
            }
            else
            {
                if (ExternalAI == null) UpdatePacifiedAI();
                return;
            }
        }

        // 瘫痪状态更新
        if (isStaggered)
        {
            SetMoveAnimation(false);
            staggerTimer -= Time.deltaTime;
            float shake = Mathf.Sin(Time.time * 28f) * 0.04f;
            SetVisualPosition(new Vector3(shake, 0f, 0f));

            if (staggerTimer <= 0f) RecoverFromStagger();
            return;
        }

        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.deltaTime;
        if (ExternalAI != null) { ExternalAI(this, Time.deltaTime); return; }
        if (giveUpTextCooldown > 0f)
            giveUpTextCooldown -= Time.deltaTime;

        // 物理击退平滑阻尼滑行
        if (knockVel.sqrMagnitude > 0.01f && !isDead)
        {
            Vector3 step = knockVel * Time.deltaTime;
            Vector3 cand = transform.position + step;
            if (pathfinder != null && pathfinder.groundTilemap != null)
            {
                Vector3Int cell = pathfinder.WorldToCell(cand);
                if (pathfinder.groundTilemap.HasTile(cell))
                {
                    transform.position = cand;
                }
            }
            else
            {
                transform.position = cand;
            }
            knockVel *= Mathf.Exp(-8f * Time.deltaTime);
        }

        if (squid != null)
        {
            squid.energy = (aiState == EnemyAIState.Stagger || aiState == EnemyAIState.Pending || aiState == EnemyAIState.Executing) ? 0.25f : 1.0f;
        }
        if (squidFace != null)
        {
            squidFace.SetAngry(aiState == EnemyAIState.Attack || aiState == EnemyAIState.GrabWindup || aiState == EnemyAIState.Grabbing);
        }

        if (targetPlayer == null)
            targetPlayer = FindObjectOfType<PlayerController>();

        if (targetPlayer == null)
        {
            SetMoveAnimation(false);
            return;
        }

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

            case EnemyAIState.Attack:
                UpdateAttackState(dist);
                break;

            case EnemyAIState.GrabWindup:
                UpdateGrabWindupState(dist);
                break;

            case EnemyAIState.Grabbing:
                UpdateGrabbingState(dist);
                break;

            case EnemyAIState.Recover:
                UpdateRecoverState(dist);
                break;

            case EnemyAIState.Pending:
                UpdatePendingState(dist);
                break;

            case EnemyAIState.Executing:
                UpdateExecutingState(dist);
                break;

            case EnemyAIState.BreakBarrier:
                UpdateBreakBarrierState(dist);
                break;

            case EnemyAIState.Pacified:
                UpdatePacifiedState();
                break;
        }
    }

    private bool PlayerInRange
    {
        get
        {
            if (targetPlayer == null || !targetPlayer.IsAlive) return false;
            float d = Vector2.Distance(transform.position, targetPlayer.transform.position);
            return d < detectRange || ((wasChasingPlayer || (targetPlayer.currentTarget == this)) && d < chaseGiveUpRange);
        }
    }

    private bool ThinkTarget()
    {
        if (targetPlayer == null) targetPlayer = FindObjectOfType<PlayerController>();
        bool wasChasing = wasChasingPlayer;
        bool playerInRange = PlayerInRange;
        wasChasingPlayer = false;

        if (targetPlayer && targetPlayer.Ultimate && targetPlayer.Ultimate.Taunts(this))
        { furnitureTarget=null;wasChasingPlayer=true;return true; }

        if (furnitureTarget != null && !furnitureTarget.Targetable)
        {
            furnitureTarget = null;
            // 目标家具已被摧毁或失效，仇恨立刻转移锁定存活的玩家！
            if (targetPlayer != null && targetPlayer.IsAlive)
            {
                wasChasingPlayer = true;
                playerInRange = true;
            }
        }

        var taunt = ScreamingValveAuraBehavior.GetTauntingValveNear(transform.position);
        if (AllowsControl(EnemyControlKind.Taunt) && taunt != null && taunt.parentFurniture != null && taunt.parentFurniture.Targetable)
        {
            furnitureTarget = taunt.parentFurniture;
            return true;
        }

        // 玩家处于倒地死亡或复苏等待中，仇恨强制锁定并全图围攻生态家具！
        if (targetPlayer != null && !targetPlayer.IsAlive)
        {
            wasChasingPlayer = false;
            if (furnitureTarget == null || !furnitureTarget.Targetable)
            {
                furnitureTarget = FurnitureObject.NearestTargetable(transform.position, 60f);
            }
            if (furnitureTarget != null)
            {
                return true;
            }
        }

        // 玩家在警戒范围内，仇恨优先锁定玩家（除非有尖叫阀门强制嘲讽）
        if (playerInRange)
        {
            furnitureTarget = null;
            wasChasingPlayer = true;
            return true;
        }

        // 若当前已有锁定家具目标，继续攻击家具
        if (furnitureTarget != null) return true;

        if (wasChasing)
        {
            gaveUpChases++;
            // 只有在玩家存活时才提示放弃追击，避免玩家倒地重整时刷屏混淆
            if (giveUpTextCooldown <= 0f && DamageTextManager.Instance != null && targetPlayer != null && targetPlayer.IsAlive)
            {
                giveUpTextCooldown = 3.0f; // 至少 3 秒冷却，杜绝连环跳字
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.6f, "? 放弃追击", Color.yellow, 0.11f);
            }
            furnitureTarget = FurnitureObject.NearestTargetable(transform.position, giveUpFurnitureRange);
            return furnitureTarget != null;
        }

        furnitureTarget = FurnitureObject.NearestTargetable(transform.position, furnitureSearchRange);
        return furnitureTarget != null;
    }

    private void UpdatePatrolState(float distToPlayer)
    {
        if (ThinkTarget())
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
            Vector3 moveDir = (spawnPosition - transform.position).normalized;
            SetVisualFacing(moveDir.x < -0.05f);
            SetMoveAnimation(true);
            transform.position = Vector3.MoveTowards(transform.position, spawnPosition, (moveSpeed * 0.5f) * Time.deltaTime);
        }
        else
        {
            SetMoveAnimation(false);
            if (monsterRenderer != null) monsterRenderer.transform.localPosition = Vector3.zero;
        }
    }

    private void UpdateAlertState(float distToPlayer)
    {
        SetMoveAnimation(false);
        alertTimer -= Time.deltaTime;

        if (ThinkTarget())
        {
            aiState = EnemyAIState.Chase;
            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.6f, "!", Color.red, 0.10f);
            }
            return;
        }

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
        if (!ThinkTarget())
        {
            aiState = EnemyAIState.Patrol;
            SetMoveAnimation(false);
            return;
        }

        // 锁定家具：只打家具，玩家靠近也不理
        if (furnitureTarget != null)
        {
            float distToFurn = Vector2.Distance(transform.position, furnitureTarget.transform.position);
            float reach = attackRange + (furnitureTarget.hitRadius > 0 ? furnitureTarget.hitRadius : 0.6f);
            if (distToFurn > reach)
            {
                Vector3 moveDir = (furnitureTarget.transform.position - transform.position).normalized;
                transform.position = Vector3.MoveTowards(transform.position, furnitureTarget.transform.position, moveSpeed * slowMultiplier * Time.deltaTime);
                SetVisualFacing(moveDir.x < -0.05f);
                SetMoveAnimation(true);

                // 检查是否被挡路的家具卡住
                CheckBlockedByFurniture(moveDir, Time.deltaTime);
            }
            else
            {
                SetMoveAnimation(false);
                if (attackCooldownTimer <= 0f)
                {
                    StartNormalAttack(true);
                }
            }
            return;
        }

        // 追玩家
        // 玩家正被某只怪缠住（IsGrabbed）时：其他怪暂停进攻，防止无解围殴
        if (targetPlayer != null && targetPlayer.IsGrabbed)
        {
            SetMoveAnimation(false);
            return;
        }

        // 进入近战范围且冷却就绪，发起普通挥击（黄光）或狂暴抓取（红光）
        float initiateRange = (squid != null && Grabber == null && !targetPlayer.Unavailable && (forceGrabNext || Random.value < grabChance)) ? (grabRange * 0.85f) : attackRange;
        if (distToPlayer <= initiateRange && attackCooldownTimer <= 0f)
        {
            SetMoveAnimation(false);
            if (squid != null && Grabber == null && !targetPlayer.Unavailable && (forceGrabNext || Random.value < grabChance))
            {
                StartGrab();
            }
            else
            {
                StartNormalAttack(false);
            }
            return;
        }

        // 智能绕障追击
        ChasePlayerOrBarrier();
    }

    private void CheckBlockedByFurniture(Vector3 dir, float dt)
    {
        blockedTimer += dt;
        if (blockedTimer < 0.6f) return;
        blockedTimer = 0f;

        FurnitureObject best = null;
        float bd = float.MaxValue;
        for (int i = 0; i < FurnitureObject.AllFurniture.Count; i++)
        {
            var f = FurnitureObject.AllFurniture[i];
            if (f == null || !f.Blocks || !f.Targetable || f == furnitureTarget) continue;
            float d = Vector2.Distance(transform.position, f.transform.position);
            if (d <= attackRange + 0.8f && d < bd)
            {
                bd = d;
                best = f;
            }
        }
        if (best != null)
        {
            furnitureTarget = best;
            wasChasingPlayer = false;
        }
    }

    private void UpdateBreakBarrierState(float distToPlayer)
    {
        var blockingFurn = FurnitureObject.GetNearestPlaced(transform.position, 2.2f);
        if (blockingFurn != null)
        {
            float d = Vector2.Distance(transform.position, blockingFurn.transform.position);
            float reach = attackRange + (blockingFurn.hitRadius > 0 ? blockingFurn.hitRadius : 0.6f);
            if (d > reach)
            {
                Vector3 moveDir = (blockingFurn.transform.position - transform.position).normalized;
                transform.position = Vector3.MoveTowards(transform.position, blockingFurn.transform.position, moveSpeed * slowMultiplier * Time.deltaTime);
                SetVisualFacing(moveDir.x < -0.05f);
                SetMoveAnimation(true);
            }
            else
            {
                SetMoveAnimation(false);
                if (attackCooldownTimer <= 0f)
                {
                    attackCooldownTimer = attackCooldown;
                    blockingFurn.TakeHit(attackDamage, this);
                    if (enemyAnimator != null) enemyAnimator.SetTrigger("Attack");
                    if (DamageTextManager.Instance != null)
                    {
                        DamageTextManager.Instance.ShowText(blockingFurn.transform.position + Vector3.up * 0.5f, "💥 撞击路障!", new Color(1f, 0.5f, 0.2f), 0.09f);
                    }
                }
            }
        }
        else
        {
            SetMoveAnimation(false);
            // 若路障已破或目标重新可达，切回 Chase 并锁玩家
            aiState = EnemyAIState.Chase;
            if (targetPlayer != null && targetPlayer.IsAlive)
            {
                wasChasingPlayer = true;
            }
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
                    // 如果玩家躲在全封闭防御建筑里导致无路可走，攻击阻挡的家具/路障
                    waypoints.Clear();
                    var blockingFurn = FurnitureObject.GetNearestPlaced(transform.position, 2.2f);
                    if (blockingFurn != null)
                    {
                        aiState = EnemyAIState.BreakBarrier;
                    }
                    SetMoveAnimation(false);
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
            SetVisualFacing(moveDir.x < -0.05f);
            SetMoveAnimation(true);

            if (monsterRenderer != null && enemyAnimator == null)
            {
                float bob = Mathf.Abs(Mathf.Sin(Time.time * 8f)) * 0.03f;
                monsterRenderer.transform.localPosition = new Vector3(0f, bob, 0f);
            }

            if (Vector2.Distance(transform.position, targetWp) < 0.12f)
            {
                currentWaypointIndex++;
            }
        }
        else
        {
            SetMoveAnimation(false);
        }
    }

    #region Combat & Grab State Machine Handlers (来自 gamejam-integration 核心战斗逻辑)

    private void StartNormalAttack(bool targetIsFurniture = false)
    {
        aiState = EnemyAIState.Attack;
        stateTimer = 0f;
        impactDone = false;
        SetMoveAnimation(false);

        Vector3 targetPos = (targetIsFurniture && furnitureTarget != null) ? furnitureTarget.transform.position : (targetPlayer != null ? targetPlayer.transform.position : transform.position);
        Vector3 dir = targetPos - transform.position;
        SetVisualFacing(dir.x < -0.05f);

        int side = targetPos.x > transform.position.x ? 1 : -1;
        bool useClock = !targetIsFurniture && enableClockQTE && (forceClockNext || Random.value < clockChance);
        forceClockNext = false;
        float attackImpact = useClock ? clockDuration : normalImpact;

        if (squid != null)
        {
            squid.Attack(side, -1, attackImpact);
        }
        if (enemyAnimator != null && enemyAnimator.enabled && enemyAnimator.runtimeAnimatorController != null)
        {
            enemyAnimator.SetTrigger("Attack");
        }

        if (useClock)
        {
            if (GameJam.ClockQTE.Current != null) GameJam.ClockQTE.Current.Close();
            currentClock = GameJam.ClockQTE.Show(this, clockDuration);
            Sfx.Play("sfx_sword_draw", 0.9f, true);
        }
        else
        {
            Sfx.Play("sfx_enemy_windup", 0.85f);
        }

        if (showAttackRange && dangerZoneObject != null && !targetIsFurniture)
        {
            dangerZoneObject.SetActive(true);
        }

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.5f, "!", Color.yellow, 0.11f);
        }
    }

    private void UpdateAttackState(float distToPlayer)
    {
        SetMoveAnimation(false);
        stateTimer += Time.deltaTime;

        float impactTime = (currentClock != null && currentClock.Open) ? currentClock.duration : normalImpact;
        float effectiveWindowEnd = (currentClock != null && currentClock.Open) ? impactTime * 0.9f : windowEnd;

        // 黄光预警（0.05 ~ effectiveWindowEnd 内闪烁）
        if (stateTimer <= effectiveWindowEnd + 0.05f)
        {
            float glow = 0.55f + 0.25f * Mathf.Sin(Time.time * 30f);
            ApplyFlash(glow, AttackGlow);
        }

        // 危险判定圈脉冲 (受 showAttackRange 控制，默认关闭)
        if (showAttackRange && dangerZoneObject != null && furnitureTarget == null)
        {
            float pulse = 0.35f + Mathf.Sin(stateTimer * 18f) * 0.15f;
            var mr = dangerZoneObject.GetComponent<MeshRenderer>();
            if (mr != null) mr.material.color = new Color(1f, 0.8f, 0.1f, pulse);
        }

        // 格挡窗口期如果玩家正在防御状态，立刻触发弹刀！
        if (furnitureTarget == null && stateTimer >= windowStart && stateTimer <= effectiveWindowEnd)
        {
            if (targetPlayer != null && targetPlayer.IsGuarding)
            {
                ApplyFlash(0f, Color.white);
                if (currentClock != null) currentClock.Close();
                targetPlayer.DoParry(this);
                return;
            }
        }

        // 命中时点
        if (!impactDone && stateTimer >= impactTime)
        {
            impactDone = true;
            if (currentClock != null) { currentClock.Close(); currentClock = null; }
            ApplyFlash(0f, Color.white);
            if (dangerZoneObject != null) dangerZoneObject.SetActive(false);
            SpawnClawEffect();

            if (furnitureTarget != null)
            {
                float dist = Vector2.Distance(transform.position, furnitureTarget.transform.position);
                float reach = hitRange + (furnitureTarget.hitRadius > 0 ? furnitureTarget.hitRadius : 0.6f);
                if (dist <= reach)
                {
                    furnitureTarget.TakeDamage(attackDamage, this);
                    Sfx.Play("sfx_hit");
                    CameraRig.I?.Shake(0.08f, 0.12f);
                }
            }
            else if (targetPlayer != null && !isStaggered && targetPlayer.IsAlive)
            {
                float dist = Vector2.Distance(transform.position, targetPlayer.transform.position);
                if (dist <= hitRange)
                {
                    if (targetPlayer.IsGuarding)
                    {
                        targetPlayer.DoParry(this);
                        return;
                    }
                    else if (targetPlayer.IsInvulnerable)
                    {
                        targetPlayer.NotifyDodgeAvoided();
                        if (DamageTextManager.Instance != null)
                        {
                            DamageTextManager.Instance.ShowText(targetPlayer.transform.position + Vector3.up * 0.4f, "DODGE!", new Color(0.2f, 1f, 0.8f), 0.11f);
                        }
                    }
                    else
                    {
                        targetPlayer.TakeDamage(attackDamage, this, true);
                        Sfx.Play("sfx_hit");
                        CameraRig.I?.Shake(0.12f, 0.15f);
                    }
                }
            }
        }

        // 攻击后摇结束
        if (stateTimer >= impactTime + 0.35f)
        {
            if (currentClock != null) { currentClock.Close(); currentClock = null; }
            ApplyFlash(0f, Color.white);
            if (dangerZoneObject != null) dangerZoneObject.SetActive(false);
            if (furnitureTarget != null && !furnitureTarget.Targetable)
            {
                furnitureTarget = null;
                if (targetPlayer != null && targetPlayer.IsAlive)
                {
                    wasChasingPlayer = true;
                }
            }
            aiState = EnemyAIState.Recover;
            stateTimer = 0.5f;
        }
    }

    private void UpdatePacifiedState()
    {
        pacifiedTimer -= Time.deltaTime;
        float glow = 0.3f + 0.1f * Mathf.Sin(Time.time * 3f);
        ApplyFlash(glow, new Color(0.2f, 0.65f, 0.92f));

        if (pacifiedTimer <= 0f || pacifierFurniture == null || pacifierFurniture.currentState != FurnitureObject.FurnitureState.Placed)
        {
            WakeFromPacify();
            return;
        }

        float pvDist = Vector2.Distance(transform.position, pacifierFurniture.transform.position);
        if (pvDist > 1.4f)
        {
            Vector3 moveDir = (pacifierFurniture.transform.position - transform.position).normalized;
            SetVisualFacing(moveDir.x < -0.05f);
            transform.position = Vector3.MoveTowards(transform.position, pacifierFurniture.transform.position, (moveSpeed * 0.5f * slowMultiplier) * Time.deltaTime);
            SetMoveAnimation(true);
        }
        else
        {
            SetMoveAnimation(false);
        }
    }

    private void StartGrab()
    {
        aiState = EnemyAIState.GrabWindup;
        stateTimer = 0f;
        Grabber = this;
        forceGrabNext = false;

        if (targetPlayer != null)
        {
            SetVisualFacing(targetPlayer.transform.position.x < transform.position.x);
        }

        if (squid != null)
        {
            squid.BeginFrenzy();
        }
        Sfx.Play("sfx_enemy_windup", 1.0f);

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.7f, "⚠️ 狂暴抓取!", Color.red, 0.12f);
        }
    }

    private void UpdateGrabWindupState(float distToPlayer)
    {
        stateTimer += Time.deltaTime;

        // 红光急促闪烁
        float glow = Mathf.Repeat(stateTimer * 7f, 1f) < 0.5f ? 0.85f : 0.3f;
        ApplyFlash(glow, GrabGlow);

        if (targetPlayer == null || !targetPlayer.IsAlive)
        {
            CancelGrab();
            aiState = EnemyAIState.Recover;
            stateTimer = 0.5f;
            return;
        }

        // 慢慢逼近主角
        Vector3 v = (targetPlayer.transform.position - transform.position);
        SetVisualFacing(v.x < -0.05f);
        if (v.magnitude > grabRange * 0.75f)
        {
            SetMoveAnimation(true);
            transform.position = Vector3.MoveTowards(transform.position, targetPlayer.transform.position, (moveSpeed * 0.45f * slowMultiplier) * Time.deltaTime);
        }
        else
        {
            SetMoveAnimation(false);
        }

        // 前摇时间到 -> 判定抓取
        if (stateTimer >= grabWindup)
        {
            int side = (targetPlayer.transform.position.x > transform.position.x) ? 1 : -1;
            if (v.magnitude <= grabRange && targetPlayer.TryGrab(this))
            {
                aiState = EnemyAIState.Grabbing;
                stateTimer = 0f;
                grabTickTimer = grabTick;
                if (squid != null) squid.GrabHold(side);
                CameraRig.I?.Shake(0.25f, 0.2f);
                Sfx.Play("sfx_hit");
            }
            else
            {
                // 抓空
                if (squid != null)
                {
                    squid.EndFrenzy();
                    squid.Attack(side, -1, 0.12f);
                }
                if (Grabber == this) Grabber = null;
                aiState = EnemyAIState.Recover;
                stateTimer = 0.8f;
                if (DamageTextManager.Instance != null)
                {
                    DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.6f, "抓空!", Color.gray, 0.09f);
                }
            }
        }
    }

    private void UpdateGrabbingState(float distToPlayer)
    {
        SetMoveAnimation(false);
        stateTimer += Time.deltaTime;
        ApplyFlash(0.35f, GrabGlow);

        if (targetPlayer == null || !targetPlayer.IsAlive || !targetPlayer.IsGrabbed)
        {
            CancelGrab();
            aiState = EnemyAIState.Recover;
            stateTimer = 0.8f;
            return;
        }

        grabTickTimer -= Time.deltaTime;
        if (grabTickTimer <= 0f)
        {
            grabTickTimer += grabTick;
            targetPlayer.GrabTick(grabDamage);
        }

        if (stateTimer >= grabHold && targetPlayer.IsGrabbed)
        {
            targetPlayer.ThrowFrom(this);
            if (squid != null) squid.GrabRelease();
            if (Grabber == this) Grabber = null;
            CameraRig.I?.Shake(0.35f, 0.25f);
            Sfx.Play("sfx_swing", 1f);
            aiState = EnemyAIState.Recover;
            stateTimer = 1.0f;
        }
    }

    private void UpdateRecoverState(float distToPlayer)
    {
        SetMoveAnimation(false);
        stateTimer -= Time.deltaTime;
        ApplyFlash(0f, Color.white);

        if (stateTimer <= 0f)
        {
            aiState = EnemyAIState.Chase;
            attackCooldownTimer = attackCooldown;
            if (furnitureTarget == null && targetPlayer != null && targetPlayer.IsAlive)
            {
                wasChasingPlayer = true;
            }
        }
    }

    private void UpdatePendingState(float distToPlayer)
    {
        SetMoveAnimation(false);
        stateTimer += Time.unscaledDeltaTime;

        // 冰蓝脉冲
        float glow = 0.35f + 0.2f * Mathf.Sin(Time.unscaledTime * 12f);
        ApplyFlash(glow, PendingGlow);

        if (squid != null) squid.energy = 0.25f;

        // 若 slow-mo 结束无人处决，缓慢复原
        if (stateTimer >= 3.0f || (TimeManager.I != null && !TimeManager.SlowMo))
        {
            aiState = EnemyAIState.Recover;
            stateTimer = 0.8f;
            if (squid != null) squid.energy = 1.0f;
            ApplyFlash(0f, Color.white);
            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.6f, "复苏", Color.white, 0.08f);
            }
        }
    }

    private void UpdateExecutingState(float distToPlayer)
    {
        SetMoveAnimation(false);
        if (squid != null) squid.energy = 0.2f;
    }

    public void Parried()
    {
        if (IsEmerging || !IsAlive) return;
        if (CombatPolicy != null) { CombatPolicy.Parried(this); return; }
        if (currentClock != null) { currentClock.Close(); currentClock = null; }
        aiState = EnemyAIState.Pending;
        stateTimer = 0f;
        CancelGrab();
        if (dangerZoneObject != null) dangerZoneObject.SetActive(false);
        if (squid != null)
        {
            squid.Interrupt();
            squid.energy = 0.25f;
        }
        if (squidFace != null) squidFace.SetAngry(false);
        Flash(Color.white, 0.2f);
    }

    public void BeginExecuted()
    {
        if (!CanExecute) return;
        if (currentClock != null) { currentClock.Close(); currentClock = null; }
        aiState = EnemyAIState.Executing;
        CancelGrab();
        if (dangerZoneObject != null) dangerZoneObject.SetActive(false);
        if (squid != null) squid.Interrupt();
        Flash(Color.white, 0.3f);
    }

    public void KillByExecution(Vector3 dir)
    {
        if (!CanExecute) { CancelExecution(); return; }
        float damage=CombatPolicy == null ? currentHp : CombatPolicy.ExecutionDamage(this);
        if (float.IsNaN(damage) || float.IsInfinity(damage) || damage < 0) { CancelExecution(); return; }
        currentHp = Mathf.Max(0, currentHp-damage);
        Flash(Color.white, 0.3f);
        ApplyKnockback(dir, 0.8f, 0.25f);
        UpdateHealthBar();
        if (currentHp <= 0) Die(); else CancelExecution();
    }

    public void CancelExecution()
    { if (aiState == EnemyAIState.Executing) aiState = EnemyAIState.Chase; }

    public void YieldToGrab()
    {
        if (ExternalAI != null) return;
        if (aiState == EnemyAIState.Attack || aiState == EnemyAIState.GrabWindup)
        {
            if (currentClock != null) { currentClock.Close(); currentClock = null; }
            if (dangerZoneObject != null) dangerZoneObject.SetActive(false);
            if (squid != null) squid.Interrupt();
            CancelGrab();
            aiState = EnemyAIState.Chase;
            attackCooldownTimer = 1.0f;
        }
    }

    public void CancelGrab()
    {
        if (Grabber == this) Grabber = null;
        if (squid != null)
        {
            squid.EndFrenzy();
            squid.GrabRelease();
        }
        if (squidFace != null) squidFace.SetAngry(false);
    }

    #endregion

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
        if (isDead || IsEmerging || aiState == EnemyAIState.Executing) return;
        if (CombatPolicy != null && !CombatPolicy.ReceiveDamage(this, ref damage, ref guardBreakDamage)) return;
        if (damage < 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;

        Flash(Color.white, 0.12f);
        Knockback((transform.position - attackerPos).normalized, 0.18f);

        // 如果在被安抚中挨打，醒过来
        if (aiState == EnemyAIState.Pacified || isPacified)
        {
            isPacified = false;
            pacifiedTargetEnemy = null;
            WakeFromPacify();
        }

        // 如果在处决就绪状态下，保持 Pending 不变，方便处决
        if ((CombatPolicy == null || CombatPolicy.InterruptOnHit) && aiState != EnemyAIState.Pending && aiState != EnemyAIState.Grabbing)
        {
            if (aiState == EnemyAIState.Attack && currentClock != null)
            {
                currentClock.Close();
                currentClock = null;
            }
            aiState = EnemyAIState.Chase;

            // 检查伤害来源：如果当前在攻击家具，只有受到来自玩家的主动攻击时才转火玩家
            // （避免家具的反伤、陷阱、光环等持续触发清空家具目标并狂跳放弃追击）
            bool isFromPlayer = (targetPlayer != null && Vector2.Distance(attackerPos, targetPlayer.transform.position) < 1.4f);
            if (furnitureTarget == null || isFromPlayer)
            {
                var taunt = ScreamingValveAuraBehavior.GetTauntingValveNear(transform.position);
                if (taunt == null || taunt.parentFurniture == null || !taunt.parentFurniture.Targetable)
                {
                    furnitureTarget = null;
                    wasChasingPlayer = true;
                }
            }
        }

        float finalDmg = damage;
        bool isCrit = (CombatPolicy == null || CombatPolicy.VulnerabilityBonus) && (isStaggered || aiState == EnemyAIState.Pending);
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

        if (currentGuardBreak <= 0f && !isStaggered && currentHp > 0f && aiState != EnemyAIState.Pending)
        {
            TriggerStagger();
        }

        if (currentHp <= 0f)
        {
            Die();
        }
    }

    public void TriggerStagger()
    {
        if (IsEmerging) return;
        if (!AllowsControl(EnemyControlKind.Stagger)) return;
        if (currentClock != null) { currentClock.Close(); currentClock = null; }
        isStaggered = true;
        staggerTimer = staggerDuration;
        SetMoveAnimation(false);

        if (dangerZoneObject != null) dangerZoneObject.SetActive(false);

        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 1.0f, "⚡ STAGGERED!", new Color(1f, 0.85f, 0.15f), 0.13f);
        }

        if (squid != null)
        {
            squid.energy = 0.2f;
        }

        SetVisualColor(new Color(0.7f, 0.7f, 1f, 1f));
    }

    public void RecoverFromStagger()
    {
        isStaggered = false;
        currentGuardBreak = maxGuardBreak;
        UpdateGuardBar();

        if (squid != null)
        {
            squid.energy = 1.0f;
        }

        SetVisualColor(originalColor);
        SetVisualPosition(Vector3.zero);

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.8f, "复苏", Color.white, 0.08f);
        }
    }

    private void Die()
    {
        if (isDead) return;
        if (currentClock != null) { currentClock.Close(); currentClock = null; }
        isDead = true;
        SetMoveAnimation(false);
        if (dangerZoneObject != null) dangerZoneObject.SetActive(false);
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        if (aiState == EnemyAIState.Grabbing && targetPlayer != null)
        {
            targetPlayer.ThrowFrom(this);
        }
        CancelGrab();

        if (hpBarRoot != null) hpBarRoot.SetActive(false);
        if (targetRingObject != null) targetRingObject.SetActive(false);

        if (squid != null)
        {
            squid.Die(2.0f);
        }

        if (targetPlayer != null && targetPlayer.currentTarget == this)
        {
            targetPlayer.ClearAttackTarget();
        }

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.5f, "DEMON SLAIN!", new Color(1f, 0.3f, 0.1f), 0.13f);
        }

        StartCoroutine(DeathAndRespawnRoutine());
        if (Died != null) foreach (System.Action<EnemyController> listener in Died.GetInvocationList())
        { try { listener(this); } catch (System.Exception ex) { Debug.LogException(ex); } }
    }

    private IEnumerator DeathAndRespawnRoutine()
    {
        // 1. 死亡平滑淡出
        float dur = CombatPolicy != null ? CombatPolicy.DeathDuration : 0.45f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            if (ExternalAI != null && CombatWorldPaused) { yield return null; continue; }
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            SetVisualAlpha(1f - t);
            if (monsterRenderer != null)
            {
                monsterRenderer.transform.localScale = originalScale * (1f - t * 0.4f);
            }
            yield return null;
        }

        // 死亡期间禁用碰撞与渲染，彻底避免阻挡玩家与交互
        var colliders = GetComponentsInChildren<Collider2D>();
        foreach (var col in colliders) col.enabled = false;

        if (monsterRenderer != null) monsterRenderer.enabled = false;
        if (visualRoot != null) visualRoot.gameObject.SetActive(false);

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
        isPacified = false;
        pacifiedTargetEnemy = null;
        currentTargetEnemy = null;
        furnitureTarget = null;
        wasChasingPlayer = true;
        aiState = EnemyAIState.Chase;
        lastPlayerCell = new Vector3Int(int.MinValue, int.MinValue, 0);
        waypoints.Clear();

        // 重新开启外观与碰撞
        if (monsterRenderer != null)
        {
            monsterRenderer.enabled = true;
            monsterRenderer.transform.localScale = originalScale;
            monsterRenderer.transform.localPosition = Vector3.zero;
        }
        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(true);
        }
        if (squid != null)
        {
            squid.Revive();
        }
        else
        {
            AttachSquid();
        }
        foreach (var col in colliders) col.enabled = true;

        // 复活淡入
        dur = 0.5f;
        elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dur;
            SetVisualAlpha(t);
            yield return null;
        }
        SetVisualAlpha(1f);
        SetVisualColor(originalColor);
        ApplyFlash(0f, Color.white);
        SetVisualFacing(false);
        SetMoveAnimation(false);

        if (hpBarRoot != null) hpBarRoot.SetActive(true);
        UpdateHealthBar();
        UpdateGuardBar();

        isDead = false;
        if (targetPlayer == null) targetPlayer = FindObjectOfType<PlayerController>();
        furnitureTarget = null;
        wasChasingPlayer = true;
        aiState = EnemyAIState.Chase;

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
    public void ApplyKnockback(Vector3 dir, float force, float duration = 0.35f, UnityEngine.Object source = null)
    {
        if (IsEmerging) return;
        if (isDead) return;
        if (!FilterControl(EnemyControlKind.Knockback, ref force, ref duration, source)) return;
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
        if (IsEmerging) return;
        if (!FilterControl(EnemyControlKind.Slow, ref slowPercent, ref duration)) return;
        float factor = Mathf.Clamp01(1f - slowPercent / 100f);
        slowMultiplier = Mathf.Min(slowMultiplier, factor);
        slowTimer = Mathf.Max(slowTimer, duration);
    }

    /// <summary>
    /// 施加电弧麻痹硬直（电鳗电机、带电水潭等机制）
    /// </summary>
    public void ApplyParalysis(float duration)
    {
        if (IsEmerging) return;
        if (isDead) return;
        float strength=1;
        if (!FilterControl(EnemyControlKind.Paralysis, ref strength, ref duration)) return;
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
        if (IsEmerging) return;
        if (isDead) return;
        float strength=1;
        if (!FilterControl(EnemyControlKind.Defect, ref strength, ref duration)) return;
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
        // 1. 冷却计时器每帧必须递减，彻底修复原代码中 return 导致攻击一次后永远无法再次攻击的致命 Bug
        if (attackCooldownTimer > 0f)
        {
            attackCooldownTimer -= Time.deltaTime;
        }

        // 2. 驯化期间呈现持续粉红光晕特效，清晰昭示其已“反戈投诚”
        float pulse = Mathf.PingPong(Time.time * 3.5f, 0.5f);
        SetVisualColor(Color.Lerp(originalColor, new Color(1f, 0.45f, 0.8f, 1f), pulse + 0.35f));

        // 3. 目标筛选：优先锁定除自身外的最近活跃【敌对怪物】（!other.isPacified）
        if (pacifiedTargetEnemy == null || !pacifiedTargetEnemy.IsAlive || pacifiedTargetEnemy.isPacified)
        {
            pacifiedTargetEnemy = null;
            float minD = float.MaxValue;
            for (int i = 0; i < AllEnemies.Count; i++)
            {
                var other = AllEnemies[i];
                if (other != null && other != this && other.IsAlive && !other.isPacified)
                {
                    float d = Vector2.Distance(transform.position, other.transform.position);
                    if (d < minD)
                    {
                        minD = d;
                        pacifiedTargetEnemy = other;
                    }
                }
            }
        }

        // 4. 若场上存在敌对怪物，全力冲锋并进行驯化撕咬
        if (pacifiedTargetEnemy != null)
        {
            float dist = Vector2.Distance(transform.position, pacifiedTargetEnemy.transform.position);
            if (dist <= attackRange * 1.25f)
            {
                SetMoveAnimation(false);
                if (attackCooldownTimer <= 0f)
                {
                    attackCooldownTimer = attackCooldown;
                    float dmg = attackDamage * 1.5f; // 驯化狂暴加成 150% 伤害
                    pacifiedTargetEnemy.TakeDamage(dmg, 45f, transform.position);
                    if (enemyAnimator != null) enemyAnimator.SetTrigger("Attack");
                    SpawnClawEffect();

                    // 让被攻击的敌怪产生仇恨反击，激发怪兽互殴激烈场面
                    if (pacifiedTargetEnemy.aiState != EnemyAIState.BreakBarrier)
                    {
                        pacifiedTargetEnemy.currentTargetEnemy = this;
                    }

                    if (DamageTextManager.Instance != null)
                    {
                        DamageTextManager.Instance.ShowText(pacifiedTargetEnemy.transform.position + Vector3.up * 0.7f, $"❤️ 驯化撕咬 -{dmg:0}!", new Color(1f, 0.45f, 0.75f), 0.12f);
                    }
                }
            }
            else
            {
                // 追击敌对怪物
                Vector3 moveDir = (pacifiedTargetEnemy.transform.position - transform.position).normalized;
                transform.position = Vector3.MoveTowards(transform.position, pacifiedTargetEnemy.transform.position, moveSpeed * 1.35f * slowMultiplier * Time.deltaTime);
                SetVisualFacing(moveDir.x < -0.05f);
                SetMoveAnimation(true);

                if (monsterRenderer != null && enemyAnimator == null)
                {
                    float bob = Mathf.Abs(Mathf.Sin(Time.time * 8f)) * 0.03f;
                    monsterRenderer.transform.localPosition = new Vector3(0f, bob, 0f);
                }
            }
        }
        else
        {
            // 5. 核心修复：若场上没有其他敌对怪物（例如只有 1 只怪或全被驯化），绝对不再傻站静止！
            // 转为【忠诚护卫形态】：主动靠近玩家并守卫在玩家身侧！
            if (targetPlayer == null)
            {
                targetPlayer = FindObjectOfType<PlayerController>();
            }

            if (targetPlayer != null)
            {
                float distToPlayer = Vector2.Distance(transform.position, targetPlayer.transform.position);
                if (distToPlayer > 1.85f)
                {
                    Vector3 followDir = (targetPlayer.transform.position - transform.position).normalized;
                    transform.position = Vector3.MoveTowards(transform.position, targetPlayer.transform.position, moveSpeed * slowMultiplier * Time.deltaTime);
                    SetVisualFacing(followDir.x < -0.05f);
                    SetMoveAnimation(true);

                    if (monsterRenderer != null && enemyAnimator == null)
                    {
                        float bob = Mathf.Abs(Mathf.Sin(Time.time * 8f)) * 0.03f;
                        monsterRenderer.transform.localPosition = new Vector3(0f, bob, 0f);
                    }
                }
                else
                {
                    SetMoveAnimation(false);
                    if (monsterRenderer != null)
                    {
                        monsterRenderer.transform.localPosition = Vector3.zero;
                    }
                }
            }

            // 护卫状态周期性爱心提示
            pacifiedHeartbeatTimer += Time.deltaTime;
            if (pacifiedHeartbeatTimer >= 2.5f)
            {
                pacifiedHeartbeatTimer = 0f;
                if (DamageTextManager.Instance != null)
                {
                    DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.8f, "❤️ 忠诚护卫中", new Color(1f, 0.55f, 0.85f), 0.09f);
                }
            }
        }
    }

    void OnDestroy()
    {
        AllEnemies.Remove(this);
        if (Grabber == this) Grabber = null;

        // 统一清理动态创建的 Native 材质球，防止显存与 Native 内存泄漏
        if (targetRingRenderer != null && targetRingRenderer.material != null)
        {
            Destroy(targetRingRenderer.material);
        }

        if (dangerZoneObject != null)
        {
            var mr = dangerZoneObject.GetComponent<MeshRenderer>();
            if (mr != null && mr.material != null)
            {
                Destroy(mr.material);
            }
        }

        if (hpBarRoot != null)
        {
            var renderers = hpBarRoot.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                if (r != null && r.material != null)
                {
                    Destroy(r.material);
                }
            }
        }
    }

    #endregion
}
