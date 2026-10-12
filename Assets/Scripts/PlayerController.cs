using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;
using GameJam;

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
    public bool IsAlive => currentHp > 0f;
    public bool Dead => currentHp <= 0f;
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
    [Tooltip("骨骼动画控制器组件")]
    public Animator playerAnimator;
    [Tooltip("骨骼动画根节点（用于整体左右镜像翻转）")]
    public Transform visualRoot;
    public float walkBobAmount = 0.07f;
    public float walkBobFrequency = 14f;

    [Header("Hero2D (寄居蟹骨骼角色与动作驱动)")]
    public HeroActions hero;
    public bool autoAttachHero2D = true;
    public float hero2DScale = 0.22f;
    private HeroFacing heroFacing;
    private HeroGait heroGait;
    private HeroSpringBones heroSprings;

    [Header("Hero2D Combat (战斗扩展: 连击 / 格挡 / 蓄力 / 处决)")]
    private int comboStep = 0;
    private float lastAttackTime = 0f;
    private bool isGuarding = false;
    private float guardCooldownTimer = 0f;
    private float guardWindowTimer = 0f;
    public bool IsGuarding => isGuarding;
    public float guardWindowDuration = 0.45f;
    public float guardCooldown = 0.5f;

    private bool isCharging = false;
    private float chargeLevel = 0f;
    public float chargeHoldThreshold = 0.22f;

    [Header("Combat Locking & Controls (战斗锁定与双键操作规范 - 对齐 gamejam-integration)")]
    public float reLockRadius = 8f;
    public bool InCombat { get; private set; } = false;
    public float chargeHoldDelay = 0.18f;
    private bool _leftHeld = false;
    private float _leftDownAt = -1f;
    private FurnitureObject _tapFurn = null;

    [Header("Place Furniture (放置家具动作 - 来自 gamejam-integration)")]
    public float placeActionTime = 1.0f;
    public const float PlaceTime = 1.0f, PlaceRelease = 0.56f;
    private System.Action _placeRelease, _placeCancel;
    public bool Placing { get; private set; } = false;
    public Vector3 PlaceHandPos => transform.position + Vector3.up * 1.0f + (IsFacingLeft ? Vector3.left : Vector3.right) * 0.4f;

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
    public bool Unavailable => isGrabbed || isThrown;
    public bool IsGrabbed => isGrabbed;
    public bool IsThrown => isThrown;
    public bool IsInvulnerable => isRolling || isInvulnerable || Unavailable;
    public bool CanRoll => rollCooldownTimer <= 0f && !isRolling && !Unavailable;
    public float RollCooldownTimer => rollCooldownTimer;
    public float RollCooldown => rollCooldown;
    private bool isRolling = false;
    private bool isInvulnerable = false;
    private bool isGrabbed = false;
    private bool isThrown = false;
    private EnemyController grabberEnemy = null;
    private float rollCooldownTimer = 0f;

    // 攻击冷却内置计时器（严格保证攻击间隔，彻底防止连续点击快速攻击作弊）
    private float attackCooldownTimer = 0f;
    private bool isAttackingAnim = false;

    // 寻路航点
    private readonly List<Vector3> waypoints = new List<Vector3>();
    private int currentWaypointIndex = 0;
    private bool isMoving = false;
    private bool isMovingByKeyboard = false;

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

    // 电脑鼠标操作状态 (右键移动，左键家具与战术目标交互)
    private float rightClickHoldTimer = 0f;
    private bool isLeftPointerDown = false;
    private Vector2 leftPointerDownPos;
    private float leftPointerDownTime;

    // 标记
    private GameObject markerObject;
    private SpriteRenderer markerRenderer;
    private float markerTimer = 0f;

    private Camera cam;
    private Coroutine hurtCoroutine = null;
    private SpriteRenderer[] cachedVisualRenderers = null;
    private Color[] cachedOriginalColors = null;

    [HideInInspector] public float moveSpeedMultiplier = 1f;
    [HideInInspector] public float staminaCostMultiplier = 1f;

    /// <summary>
    /// 获取当前实际移动速度（若举起家具则承受 30% 移速惩罚，可受气压床增益）
    /// </summary>
    public float EffectiveMoveSpeed
    {
        get
        {
            float spd = baseMoveSpeed;
            if (heldFurniture != null)
                spd *= (1f - heldFurniture.moveSpeedPenalty);
            return spd * moveSpeedMultiplier;
        }
    }

    /// <summary>
    /// 恢复玩家生命值（生体疗愈水雾等机制）
    /// </summary>
    public void Heal(float amount)
    {
        currentHp = Mathf.Min(maxHp, currentHp + amount);
        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.8f, $"+{amount:0} HP", Color.green, 0.11f);
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
                buildTilemap = System.Array.Find(tms, t => t.name != "Tilemap" && t != groundTilemap);
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

        if (playerAnimator == null)
            playerAnimator = GetComponentInChildren<Animator>();
        if (visualRoot == null && playerAnimator != null)
            visualRoot = playerAnimator.transform;

        if (visualRoot == null && playerRenderer == null)
            playerRenderer = GetComponentInChildren<SpriteRenderer>();

        if (equippedWeapon == null)
            equippedWeapon = WeaponData.CreateDefault();

        if (TimeManager.I == null)
        {
            var tmGo = new GameObject("TimeManager");
            tmGo.AddComponent<TimeManager>();
        }

        if (autoAttachHero2D)
        {
            AttachHero2D();
        }

        CreateMarkerObject();
        CacheOriginalColors();
    }

    public void AttachHero2D()
    {
        if (hero != null) return;
        var prefab = Resources.Load<GameObject>("Hero2D/Hero2D");
        if (prefab == null) return;

        if (playerRenderer != null) playerRenderer.enabled = false;
        if (playerAnimator != null) playerAnimator.enabled = false;

        Transform parentTransform = visualRoot != null ? visualRoot : transform;
        if (visualRoot != null)
        {
            Vector3 s = visualRoot.localScale;
            s.x = Mathf.Abs(s.x);
            visualRoot.localScale = s;
        }

        foreach (Transform child in parentTransform)
        {
            if (child.name != "Hero2D_Flip")
                child.gameObject.SetActive(false);
        }

        var flipGo = new GameObject("Hero2D_Flip");
        flipGo.transform.SetParent(parentTransform, false);
        flipGo.transform.localScale = Vector3.one * hero2DScale;

        var go = Instantiate(prefab, flipGo.transform);
        go.name = "Hero2D";

        float footY = float.MaxValue, bodyX = 0f;
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            var lp = go.transform.InverseTransformPoint(t.position);
            if (t.name.EndsWith("腿_目标")) footY = Mathf.Min(footY, lp.y);
            if (t.name == "身体根") bodyX = lp.x;
        }
        go.transform.localPosition = new Vector3(-bodyX, footY == float.MaxValue ? 0f : -footY, 0f);

        heroFacing = go.GetComponent<HeroFacing>() ?? go.AddComponent<HeroFacing>();
        heroFacing.target = flipGo.transform;
        heroFacing.motion = transform;
        heroFacing.Init();

        heroGait = go.GetComponent<HeroGait>() ?? go.AddComponent<HeroGait>();
        heroGait.motion = transform;
        heroGait.strideLength = 0.75f;
        heroGait.stepHeight = 0.22f;

        heroSprings = go.GetComponent<HeroSpringBones>() ?? go.AddComponent<HeroSpringBones>();
        heroSprings.motion = transform;

        var billboardMat = Resources.Load<Material>("Materials/SpriteBillboard");
        if (billboardMat != null)
        {
            foreach (var r in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                r.sharedMaterial = billboardMat;
            }
        }

        if (!go.GetComponent<HeroFace>()) go.AddComponent<HeroFace>();
        hero = go.GetComponent<HeroActions>() ?? go.AddComponent<HeroActions>();
    }

    public EnemyController FindParryableEnemy()
    {
        EnemyController best = null;
        float bd = float.MaxValue;
        var enemies = FindObjectsOfType<EnemyController>();
        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (e == null || !e.IsAlive || !e.ParryWindowOpen) continue;
            float d = Vector2.Distance(transform.position, e.transform.position);
            if (d <= e.hitRange + 0.8f && d < bd)
            {
                best = e;
                bd = d;
            }
        }
        return best;
    }

    public void DoParry(EnemyController attacker)
    {
        OnParrySuccess(attacker);
    }

    public void TriggerShellGuard()
    {
        if (guardCooldownTimer > 0f || isRolling || isCharging || heldFurniture != null || Unavailable) return;

        // 0. 时钟倒计时 QTE 判定（按空格也能触发处决）
        var clock = GameJam.ClockQTE.Current;
        if (clock != null && clock.Open)
        {
            if (clock.TryHit())
            {
                if (clock.owner != null) ExecuteEnemy(clock.owner);
                return;
            }
        }

        // 1. 如果身边有正处于可格挡黄光窗口的敌人，直接触发完美弹刀！
        var parryEnemy = FindParryableEnemy();
        if (parryEnemy != null)
        {
            DoParry(parryEnemy);
            return;
        }

        // 2. 正常缩壳格挡
        isGuarding = true;
        guardWindowTimer = guardWindowDuration;
        guardCooldownTimer = guardCooldown;

        StopMovement();

        if (hero != null)
        {
            hero.Play("ShellGuard");
        }
        else
        {
            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.5f, "GUARD", Color.cyan, 0.09f);
            }
        }
        Sfx.Play("sfx_swing", 0.5f);
    }

    public void OnParrySuccess(EnemyController attacker)
    {
        isGuarding = false;
        guardWindowTimer = 0f;
        guardCooldownTimer = 0.3f;

        TimeManager.I?.StartSlowMo(2.5f);
        ScreenFX.I?.Pulse();
        CameraRig.I?.Shake(0.2f, 0.25f);
        Sfx.Play("sfx_parry", 1f, true);
        Sfx.Play("sfx_tongtou", 0.9f, true);

        Vector3 sparkPos = (attacker != null) ? Vector3.Lerp(transform.position, attacker.transform.position, 0.5f) : (transform.position + (IsFacingLeft ? Vector3.left : Vector3.right) * 0.5f);
        Vfx.Spawn("VFX_ParrySpark", sparkPos + Vector3.up * 0.6f, 0, 1.2f, true);

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.7f, "⚡ 完美格挡! (PERFECT PARRY)", new Color(1f, 0.9f, 0.2f), 0.15f);
        }

        if (attacker != null)
        {
            SetAttackTarget(attacker);
            attacker.Parried();
        }

        if (hero != null)
        {
            hero.Play("ShellGuard");
        }
    }

    public bool TryGrab(EnemyController by)
    {
        if (currentHp <= 0 || IsInvulnerable || Unavailable) return false;

        // 打断现有动作与寻路
        CancelPlace();
        StopMovement();
        isCharging = false;
        isGuarding = false;
        isAttackingAnim = false;

        if (heldFurniture != null)
        {
            var f = heldFurniture;
            DropFurniture();
            f.PutDown(transform.position);
        }

        isGrabbed = true;
        grabberEnemy = by;

        if (TimeManager.I != null && TimeManager.SlowMo)
        {
            TimeManager.I.EndSlowMo();
        }

        if (hero != null)
        {
            hero.BeginStruggle();
            if (hero.Face != null)
            {
                hero.Face.wrapped = true;
                hero.Face.SetBase(HeroFace.Expr.Pain);
            }
        }

        if (by != null)
        {
            SetVisualFacing(by.transform.position.x < transform.position.x);
        }

        // 别的怪先不打玩家
        foreach (var e in EnemyController.AllEnemies)
        {
            if (e != null && e != by) e.YieldToGrab();
        }

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.6f, "被触手缠住!", Color.red, 0.12f);
        }

        CameraRig.I?.Shake(0.3f, 0.25f);
        Sfx.Play("sfx_player_hurt");

        if (hurtCoroutine != null) StopCoroutine(hurtCoroutine);
        hurtCoroutine = StartCoroutine(PlayerHurtRoutine());

        return true;
    }

    public void GrabTick(float dmg)
    {
        if (!isGrabbed) return;

        currentHp = Mathf.Max(0f, currentHp - dmg);
        if (hero != null)
        {
            hero.SqueezeKick();
            if (hero.Face != null)
            {
                hero.Face.wrapped = true;
                hero.Face.TriggerPain();
            }
        }

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.5f, $"-{dmg:0}", Color.red, 0.1f);
        }

        CameraRig.I?.Shake(0.18f, 0.2f);
        Sfx.Play("sfx_player_hurt");

        if (hurtCoroutine != null) StopCoroutine(hurtCoroutine);
        hurtCoroutine = StartCoroutine(PlayerHurtRoutine());

        if (currentHp <= 0f)
        {
            if (hero != null)
            {
                hero.EndStruggle();
                if (hero.Face != null) hero.Face.wrapped = false;
            }
            isGrabbed = false;
            grabberEnemy = null;
            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.8f, "DEFEATED", Color.red, 0.15f);
            }
        }
    }

    public void ThrowFrom(EnemyController by)
    {
        if (!isGrabbed) return;
        isGrabbed = false;
        if (hero != null)
        {
            hero.EndStruggle();
            if (hero.Face != null) hero.Face.wrapped = false;
        }
        var enemy = grabberEnemy != null ? grabberEnemy : by;
        grabberEnemy = null;
        StartCoroutine(ThrownRoutine(enemy));
    }

    private IEnumerator ThrownRoutine(EnemyController by)
    {
        isThrown = true;
        StopMovement();

        Vector3 throwDir = (by != null) ? (transform.position - by.transform.position).normalized : (IsFacingLeft ? Vector3.right : Vector3.left);
        if (throwDir.sqrMagnitude < 0.01f) throwDir = Vector3.right;
        throwDir.z = 0;

        float throwFlight = 0.45f;
        float throwDistance = 2.4f;

        if (hero != null)
        {
            hero.EndStruggle();
            if (hero.Face != null) hero.Face.wrapped = false;
            hero.Play("Thrown");
        }
        SetVisualFacing(throwDir.x < 0);

        Sfx.Play("sfx_swing", 0.9f);

        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + throwDir * throwDistance;

        while (elapsed < throwFlight)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / throwFlight);
            float ease = Mathf.Sin(t * Mathf.PI * 0.5f);
            Vector3 cand = Vector3.Lerp(startPos, targetPos, ease);

            // 地形边界保护
            Vector3 groundRayPos = new Vector3(cand.x, cand.y - groundHeightOffset, 0f);
            Vector3Int nextCell = grid != null ? grid.WorldToCell(groundRayPos) : Vector3Int.zero;
            if (groundTilemap == null || groundTilemap.HasTile(nextCell))
            {
                transform.position = cand;
            }
            yield return null;
        }

        CameraRig.I?.Shake(0.3f, 0.25f);
        Sfx.Play("sfx_kick", 0.9f);

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.4f, "砸在地面!", new Color(1f, 0.5f, 0.2f), 0.09f);
        }

        // 瘫地缓慢起身：等待 Thrown 动作完全播完（约 1.3s）后再恢复玩家控制
        float remaining = (HeroActions.Clips.TryGetValue("Thrown", out var c) ? c.Length : 1.3f) - throwFlight;
        yield return new WaitForSeconds(Mathf.Max(0.5f, remaining));

        isThrown = false;
    }

    public void ExecuteEnemy(EnemyController enemy)
    {
        if (enemy == null || !enemy.IsAlive) return;
        StartCoroutine(ExecuteRoutine(enemy));
    }

    private IEnumerator ExecuteRoutine(EnemyController enemy)
    {
        isAttackingAnim = true;
        StopMovement();
        enemy.BeginExecuted();

        Vector3 dashDir = (enemy.transform.position - transform.position).normalized;
        dashDir.z = 0f;
        SetVisualFacing(dashDir.x < 0);

        if (hero != null)
        {
            hero.Play("Execute");
            hero.Face?.TriggerAngry();
        }

        if (TimeManager.I != null) TimeManager.I.EndSlowMo();
        ScreenFX.I?.Pulse();
        Sfx.Play("sfx_execute", 1f, true);

        // 瞬间闪身冲刺到敌人面前/穿身而过
        Vector3 startPos = transform.position;
        Vector3 targetPos = enemy.transform.position - dashDir * 0.25f;
        float dashTime = 0.12f;
        float elapsed = 0f;
        while (elapsed < dashTime)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / dashTime);
            yield return null;
        }

        // 处决大刀光特效
        Vfx.Spawn("VFX_ExecuteSlash", enemy.transform.position + Vector3.up * 0.5f, dashDir.x >= 0 ? 0 : 180f, 1.4f, true);
        CameraRig.I?.Shake(0.35f, 0.3f);

        // 斩杀敌人
        enemy.KillByExecution(dashDir);
        if (hero != null && hero.Face != null && Random.value < 0.6f) hero.Face.TriggerHappy();

        yield return new WaitForSeconds(0.35f);
        isAttackingAnim = false;

        // 附近还有敌人：自动锁定最近的一个继续战斗（格挡立刻可用）；没有才脱战
        var next = NearestEnemy(reLockRadius);
        if (next != null)
        {
            currentTarget = next;
            InCombat = true;
        }
        else
        {
            ExitCombat();
        }
    }

    public void PerformChargedSmash(float level)
    {
        float damageMultiplier = Mathf.Lerp(1.5f, 3.2f, level);
        float radius = Mathf.Lerp(1.4f, 2.5f, level);
        float dmg = CurrentAttackDamage * damageMultiplier;

        if (hero != null)
        {
            hero.ReleaseCharge();
            hero.Face?.TriggerAngry();
        }

        Vector3 forward = IsFacingLeft ? Vector3.left : Vector3.right;
        transform.position += forward * (0.35f + level * 0.45f);

        var enemies = EnemyController.AllEnemies;
        foreach (var enemy in enemies.ToArray())
        {
            if (enemy == null || !enemy.IsAlive) continue;
            float d = Vector2.Distance(transform.position, enemy.transform.position);
            if (d <= radius)
            {
                enemy.TakeDamage(dmg, CurrentGuardBreakPower * 2f, transform.position);
            }
        }

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.8f, $"💥 震地重砸! (SMASH x{damageMultiplier:0.0})", new Color(1f, 0.45f, 0.1f), 0.14f);
        }

        Sfx.Play("sfx_explosion");
    }

    void OnDisable()
    {
        if (hurtCoroutine != null)
        {
            StopCoroutine(hurtCoroutine);
            hurtCoroutine = null;
        }
        ResetVisualColors();
    }

    public bool IsFacingLeft
    {
        get
        {
            if (heroFacing != null) return heroFacing.Facing < 0;
            if (visualRoot != null) return visualRoot.localScale.x < 0;
            return playerRenderer != null && playerRenderer.flipX;
        }
    }

    /// <summary>
    /// 统一设置角色左右朝向（兼容单个 SpriteRenderer 与复合骨骼动画节点整体镜像翻转）
    /// </summary>
    public void SetVisualFacing(bool faceLeft)
    {
        if (heroFacing != null)
        {
            // 当启用 Hero2D 骨骼动画时，翻转由 heroFacing 的纸片翻转缩放统一处理
            // visualRoot 的 scale.x 必须保持绝对正值，防止双重负号翻转抵消导致永远朝右！
            if (visualRoot != null)
            {
                Vector3 s = visualRoot.localScale;
                s.x = Mathf.Abs(s.x);
                visualRoot.localScale = s;
            }
            heroFacing.Face(faceLeft ? -1 : 1);
            return;
        }

        if (visualRoot != null)
        {
            Vector3 s = visualRoot.localScale;
            s.x = (faceLeft ? -1f : 1f) * Mathf.Abs(s.y);
            visualRoot.localScale = s;
        }
        else if (playerRenderer != null)
        {
            playerRenderer.flipX = faceLeft;
        }
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
        // 工坊或温室叠加开启时，战斗角色输入与逻辑全面静默冻结
        if (PlayerSessionData.isCraftingOpen) return;

        // 处于被缠绕状态时，吸附在鱿鱼触手前并禁止主动操作
        if (isGrabbed)
        {
            if (hero != null && hero.Face != null)
            {
                hero.Face.wrapped = true;
                hero.Face.SetBase(HeroFace.Expr.Pain);
            }
            if (grabberEnemy != null && grabberEnemy.IsAlive)
            {
                Vector3 holdPos = grabberEnemy.transform.position + (transform.position.x > grabberEnemy.transform.position.x ? Vector3.right : Vector3.left) * 0.55f;
                transform.position = Vector3.Lerp(transform.position, holdPos, Time.deltaTime * 12f);
                SetVisualFacing(grabberEnemy.transform.position.x < transform.position.x);
            }
            else
            {
                ThrowFrom(grabberEnemy);
            }
            return;
        }

        if (isThrown)
        {
            return;
        }

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

        // 空格键触发贝壳格挡，J 键手动普通攻击
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TriggerShellGuard();
        }
        else if (Input.GetKeyDown(KeyCode.J))
        {
            TriggerManualAttack();
        }

        if (isGuarding)
        {
            guardWindowTimer -= Time.deltaTime;
            if (guardWindowTimer <= 0f) isGuarding = false;
        }
        if (guardCooldownTimer > 0f)
        {
            guardCooldownTimer -= Time.deltaTime;
        }

        if (InCombat && (currentTarget == null || !currentTarget.IsAlive))
        {
            var next = NearestEnemy(reLockRadius);
            if (next != null)
            {
                currentTarget = next;
            }
            else
            {
                ExitCombat();
            }
        }

        UpdateHeroFace();

        // 鼠标左键长按蓄力机制（对齐 gamejam-integration）
        if (_leftHeld && hero != null && !isRolling && !Unavailable)
        {
            float holdTime = Time.unscaledTime - _leftDownAt;
            if (holdTime >= chargeHoldDelay)
            {
                if (!isCharging)
                {
                    isCharging = true;
                    hero.BeginCharge();
                    Sfx.Play("sfx_arrow_charge", 0.6f);
                }
                chargeLevel = Mathf.Clamp01((holdTime - chargeHoldDelay) / 1.0f);
                hero.SetCharge(chargeLevel);
            }
        }
        else if (Input.GetMouseButtonUp(0) && isCharging)
        {
            isCharging = false;
            PerformChargedSmash(chargeLevel);
        }
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
            // 电脑鼠标操作：右键移动，左键与家具及战术目标交互
            HandleMouseInput();
        }
    }

    private EnemyController GetEnemyAtScreenPos(Vector2 screenPos, Vector3 worldPos)
    {
        for (int i = 0; i < EnemyController.AllEnemies.Count; i++)
        {
            var e = EnemyController.AllEnemies[i];
            if (e != null && e.IsAlive)
            {
                if (Vector2.Distance(worldPos, e.transform.position) <= 1.2f)
                    return e;
            }
        }
        return null;
    }

    public void RightClickGround(Vector3 p)
    {
        if (Unavailable || isRolling) return;
        if (InCombat)
        {
            if (CanRoll)
            {
                Vector3 rollDir = (p - transform.position).normalized;
                TriggerDodgeRoll(new Vector2(rollDir.x, rollDir.y));
                ExitCombat();
                return;
            }
            ExitCombat();
        }
        MoveToWorldPoint(p);
    }

    public void RightClickEnemy(EnemyController e)
    {
        if (e == null || !e.IsAlive || Unavailable || isRolling) return;
        currentTarget = e;
        InCombat = true;
        StopMovement();
        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(e.transform.position + Vector3.up * 0.8f, "🎯 锁定", Color.red, 0.10f);
        }
    }

    public void ExitCombat()
    {
        InCombat = false;
        currentTarget = null;
        if (TimeManager.I != null && TimeManager.SlowMo)
        {
            TimeManager.I.EndSlowMo();
        }
    }

    public void LeftClick()
    {
        if (Unavailable || isRolling) return;

        if (InCombat)
        {
            // 1. 时钟 QTE
            var clock = GameJam.ClockQTE.Current;
            if (clock != null && clock.Open)
            {
                if (clock.TryHit())
                {
                    if (clock.owner != null) ExecuteEnemy(clock.owner);
                    return;
                }
            }

            // 2. 处决虚弱态怪物
            var pend = EnemyController.FindPending();
            if (pend != null)
            {
                ExecuteEnemy(pend);
                return;
            }

            // 3. 黄光格挡窗口弹刀
            if (guardCooldownTimer <= 0f)
            {
                var parryable = FindParryableEnemy();
                if (parryable != null)
                {
                    DoParry(parryable);
                    return;
                }
            }
        }

        // 4. 踢桌子 / 家具
        var nearestFurn = FurnitureObject.GetNearestPlaced(transform.position, 1.8f);
        if (nearestFurn != null && !InCombat && heldFurniture == null)
        {
            Vector3 kickDir = IsFacingLeft ? Vector3.left : Vector3.right;
            nearestFurn.Kick(kickDir);
            return;
        }

        // 5. 寄居蟹：战斗中记录按下时刻，按住 = 蓄力，短按 = 踢 / 空挡
        if (InCombat && hero != null)
        {
            _leftHeld = true;
            _leftDownAt = Time.unscaledTime;
            _tapFurn = nearestFurn;
            return;
        }

        // 6. 空挡 (whiff)
        if (InCombat && guardCooldownTimer <= 0f)
        {
            TriggerShellGuard();
        }
    }

    public void LeftRelease()
    {
        if (!_leftHeld) return;
        _leftHeld = false;

        if (isCharging)
        {
            isCharging = false;
            PerformChargedSmash(chargeLevel);
            return;
        }

        if (Unavailable || isRolling) return;
        if (Time.unscaledTime - _leftDownAt >= chargeHoldDelay) return;

        if (_tapFurn != null && heldFurniture == null)
        {
            Vector3 kickDir = (_tapFurn.transform.position - transform.position).normalized;
            _tapFurn.Kick(kickDir);
            _tapFurn = null;
            return;
        }

        if (InCombat && guardCooldownTimer <= 0f)
        {
            TriggerShellGuard();
        }
    }

    public void CancelLeftHold()
    {
        _leftHeld = false;
        _tapFurn = null;
        if (isCharging)
        {
            isCharging = false;
            hero?.Stop();
        }
    }

    public EnemyController NearestEnemy(float radius)
    {
        EnemyController best = null;
        float bd = radius;
        for (int i = 0; i < EnemyController.AllEnemies.Count; i++)
        {
            var e = EnemyController.AllEnemies[i];
            if (e == null || !e.IsAlive) continue;
            float d = Vector2.Distance(transform.position, e.transform.position);
            if (d <= bd)
            {
                best = e;
                bd = d;
            }
        }
        return best;
    }

    private void UpdateHeroFace()
    {
        if (hero == null || hero.Face == null) return;
        var f = hero.Face;
        HeroFace.Expr e;
        if (isGrabbed || isThrown || Dead) e = HeroFace.Expr.Pain;
        else if (isCharging || isAttackingAnim || isGuarding) e = HeroFace.Expr.Angry;
        else e = HeroFace.Expr.Neutral;
        f.SetBase(e);
        f.wrapped = isGrabbed;
    }

    public bool BeginPlace(Vector3 target, System.Action release, System.Action cancel)
    {
        if (Dead || Unavailable || isRolling || isCharging || isAttackingAnim) return false;
        CancelLeftHold();
        _placeRelease = release; _placeCancel = cancel;
        Placing = true;
        StopMovement();

        Vector3 dir = target - transform.position;
        dir.z = 0;
        if (dir.sqrMagnitude > 1e-4f) SetVisualFacing(dir.x < 0);

        StartCoroutine(PlaceRoutine());
        return true;
    }

    private IEnumerator PlaceRoutine()
    {
        if (hero != null) hero.Play("Place");
        yield return new WaitForSeconds(PlaceRelease);
        if (Placing)
        {
            var r = _placeRelease;
            _placeRelease = _placeCancel = null;
            r?.Invoke();
            Sfx.Play("sfx_swing", 0.6f);
        }
        yield return new WaitForSeconds(PlaceTime - PlaceRelease);
        Placing = false;
    }

    public void CancelPlace()
    {
        if (!Placing) return;
        Placing = false;
        StopCoroutine(PlaceRoutine());
        var c = _placeCancel;
        _placeRelease = _placeCancel = null;
        c?.Invoke();
    }

    private void HandleMouseInput()
    {
        // 1. 鼠标右键 (Button 1)：专用于战斗锁定敌人与闪避/移动脱战
        if (Input.GetMouseButtonDown(1))
        {
            if (!IsPointerOverUI(-1) && !IsInBottomDockArea(Input.mousePosition))
            {
                if (cam != null)
                {
                    Vector3 worldPos = cam.ScreenToWorldPoint(Input.mousePosition);
                    worldPos.z = 0f;
                    EnemyController clickedEnemy = GetEnemyAtScreenPos(Input.mousePosition, worldPos);
                    if (clickedEnemy != null)
                    {
                        RightClickEnemy(clickedEnemy);
                    }
                    else
                    {
                        RightClickGround(worldPos);
                        rightClickHoldTimer = 0f;
                    }
                }
            }
        }
        else if (Input.GetMouseButton(1))
        {
            if (!IsPointerOverUI(-1) && !IsInBottomDockArea(Input.mousePosition))
            {
                rightClickHoldTimer += Time.deltaTime;
                if (rightClickHoldTimer >= 0.1f)
                {
                    rightClickHoldTimer = 0f;
                    if (cam != null)
                    {
                        Vector3 worldPos = cam.ScreenToWorldPoint(Input.mousePosition);
                        worldPos.z = 0f;
                        MoveToWorldPoint(worldPos);
                    }
                }
            }
        }

        // 2. 鼠标左键 (Button 0)：QTE / 处决 / 弹刀 / 蓄力砸击 / 踢家具
        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUI(-1) || IsInBottomDockArea(Input.mousePosition))
            {
                isLeftPointerDown = false;
                return;
            }
            leftPointerDownPos = Input.mousePosition;
            leftPointerDownTime = Time.time;
            isLeftPointerDown = true;
            LeftClick();
        }
        else if (Input.GetMouseButtonUp(0))
        {
            if (isLeftPointerDown)
            {
                LeftRelease();
                EvaluateLeftClickRelease(Input.mousePosition);
            }
            isLeftPointerDown = false;
        }
    }

    private void EvaluateLeftClickRelease(Vector2 releasePos)
    {
        float duration = Time.time - leftPointerDownTime;
        Vector2 delta = releasePos - leftPointerDownPos;
        float distance = delta.magnitude;
        float speed = distance / Mathf.Max(duration, 0.001f);

        // A. 鼠标快速短滑 (Swipe)：划动踢飞家具或闪避翻滚
        if (distance >= minSwipeDistance && duration <= maxSwipeDuration && speed >= 120f)
        {
            Vector2 swipeDir = delta.normalized;

            // 踢家具判定：触控/光标滑动起点落在家具上，且角色处于近身交互范围内 (playerDist <= 1.95f)
            if (cam != null && heldFurniture == null)
            {
                Vector3 startWorldPos = cam.ScreenToWorldPoint(leftPointerDownPos);
                startWorldPos.z = 0f;
                FurnitureObject touchedFurn = FurnitureObject.GetNearestPlaced(startWorldPos, 1.1f);

                if (touchedFurn != null)
                {
                    float playerDist = Vector2.Distance(transform.position, touchedFurn.transform.position);
                    if (playerDist <= 1.95f)
                    {
                        Vector3 kickDir = new Vector3(swipeDir.x, swipeDir.y, 0f).normalized;
                        SetVisualFacing(kickDir.x < 0);

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

            if (rollCooldownTimer <= 0f)
            {
                StartCoroutine(PerformDodgeRollRoutine(swipeDir));
                return;
            }
        }

        // B. 点击判定 (Click)：家具交互、目标攻击、放下或投掷
        if (distance < clickMaxDistance && duration <= clickMaxDuration)
        {
            OnLeftClickTap(releasePos);
        }
    }

    private void OnLeftClickTap(Vector2 screenPosition)
    {
        if (cam == null || grid == null || groundTilemap == null) return;

        Vector3 worldPos = cam.ScreenToWorldPoint(screenPosition);
        worldPos.z = 0f;

        // 1. 如果当前手中正举着家具：
        if (heldFurniture != null)
        {
            // A. 如果点击到了敌人 -> 将手持家具全力掷向目标砸向敌人！
            EnemyController enemy = FindEnemyNear(worldPos, 1.0f);
            if (enemy != null && enemy.IsAlive)
            {
                ThrowHeldFurnitureAtTarget(enemy);
                return;
            }

            // B. 如果点击的是地面 -> 平稳放下家具至点击处！
            PutDownHeldFurniture(worldPos);
            return;
        }

        // 2. 如果未手持家具，检查是否点击了地面的家具（核心交互需求！）
        FurnitureObject clickedFurn = FurnitureObject.GetNearestPlaced(worldPos, 1.1f);
        if (clickedFurn != null)
        {
            float distToFurn = Vector2.Distance(transform.position, clickedFurn.transform.position);
            if (distToFurn <= 1.95f)
            {
                // 靠近时点击直接举起家具
                HoldFurniture(clickedFurn);
                return;
            }
            else
            {
                // 较远时，先寻路前往家具身旁以便交互
                Vector3Int furnCell = pathfinder != null ? pathfinder.WorldToCell(clickedFurn.transform.position) : grid.WorldToCell(clickedFurn.transform.position);
                Vector3Int pCell = pathfinder != null ? pathfinder.WorldToCell(transform.position) : grid.WorldToCell(transform.position);
                Vector3Int neighborCell = pathfinder != null ? pathfinder.FindNearestWalkableNeighbor(furnCell, pCell) : furnCell;
                MoveToCell(neighborCell);
                return;
            }
        }

        // 3. 检查是否点击到了敌人（左键锁定/普通攻击）
        EnemyController clickedEnemy = FindEnemyNear(worldPos, 0.95f);
        if (clickedEnemy != null && clickedEnemy.IsAlive)
        {
            SetAttackTarget(clickedEnemy);
            float dist = Vector2.Distance(transform.position, clickedEnemy.transform.position);
            if (dist <= attackRange)
            {
                PerformManualAttack();
            }
            return;
        }

        // 4. 左键点击空地：
        // 关键：左键绝对不触发地面寻路移动（移动已全权赋予鼠标右键！）
        // 若身边 1.95m 内有现存家具，点击附近地面可智能吸附举起身边最近的家具；
        var nearbyFurn = FurnitureObject.GetNearestPlaced(transform.position, 1.95f);
        if (nearbyFurn != null)
        {
            HoldFurniture(nearbyFurn);
            return;
        }

        // 否则如果在近战姿态下，原地挥空出招，绝不产生误走位
        PerformManualAttack();
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
            // B. 触控滑动起点必须正落在家具上 (touchDist <= 1.1f)
            // C. 玩家必须处于家具近战交互范围内 (playerDist <= 1.95f)
            if (cam != null && heldFurniture == null)
            {
                Vector3 startWorldPos = cam.ScreenToWorldPoint(pointerDownPos);
                startWorldPos.z = 0f;
                FurnitureObject touchedFurn = FurnitureObject.GetNearestPlaced(startWorldPos, 1.1f);

                if (touchedFurn != null)
                {
                    float playerDist = Vector2.Distance(transform.position, touchedFurn.transform.position);
                    if (playerDist <= 1.95f)
                    {
                        // 满足近身且手指划过家具：角色顺应手指滑动方向将家具飞速踢出冲撞！
                        Vector3 kickDir = new Vector3(swipeDir.x, swipeDir.y, 0f).normalized;
                        SetVisualFacing(kickDir.x < 0);

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
            FurnitureObject clickedFurn = FurnitureObject.GetNearestPlaced(worldPos, 1.1f);
            if (clickedFurn != null)
            {
                float distToFurn = Vector2.Distance(transform.position, clickedFurn.transform.position);
                if (distToFurn <= 1.95f)
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

        SetVisualFacing(throwDir.x < 0);

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
        var enemies = EnemyController.AllEnemies;
        EnemyController closest = null;
        float minDist = radius;

        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.IsAlive || enemy.isPacified) continue;
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

        if (!isMoving && !isMovingByKeyboard && !isRolling && !isAttackingAnim)
        {
            SetVisualFacing(currentTarget.transform.position.x < transform.position.x);
        }
    }

    /// <summary>
    /// 自动攻击逻辑（根据需求重新启用）：
    /// 只要怪物进入攻击范围 (attackRange) 内，即自动出招挥砍；
    /// 核心规则：绝对不会自动寻路追着怪物打，玩家不被强行拖拽贴脸，走位与游击权100%归属玩家！
    /// </summary>
    private void UpdateAutoAttack()
    {
        if (isRolling || !canMove || isAttackingAnim || Unavailable) return;

        // 优先检查附近是否有处决就绪 (Pending) 的怪物
        var pendingEnemy = EnemyController.FindPending();
        if (pendingEnemy != null && Vector2.Distance(transform.position, pendingEnemy.transform.position) <= attackRange * 1.5f)
        {
            ExecuteEnemy(pendingEnemy);
            return;
        }

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
        float actualCost = amount * staminaCostMultiplier;
        if (currentStamina >= actualCost)
        {
            currentStamina -= actualCost;
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
            isMovingByKeyboard = true;
            MoveWithInput(new Vector2(h, v).normalized);
        }
        else
        {
            isMovingByKeyboard = false;
        }
    }

    /// <summary>
    /// 手动释放普通攻击（支持空挥、锁定攻击与处决就绪绝杀）
    /// </summary>
    public void TriggerManualAttack()
    {
        if (Unavailable || attackCooldownTimer > 0f || isAttackingAnim) return;

        // 0. 时钟倒计时 QTE 判定
        var clock = GameJam.ClockQTE.Current;
        if (clock != null && clock.Open)
        {
            if (clock.TryHit())
            {
                if (clock.owner != null) ExecuteEnemy(clock.owner);
            }
            else
            {
                Sfx.Play("sfx_swing", 0.6f);
            }
            return;
        }

        // 1. 优先检查附近是否有处决就绪 (Pending) 的怪物！
        var pendingEnemy = EnemyController.FindPending();
        if (pendingEnemy != null && Vector2.Distance(transform.position, pendingEnemy.transform.position) <= 4.5f)
        {
            ExecuteEnemy(pendingEnemy);
            return;
        }

        // 2. 迎击弹刀：若附近怪物正处于黄光攻击窗口，出刀同样可以弹刀！
        var parryEnemy = FindParryableEnemy();
        if (parryEnemy != null)
        {
            DoParry(parryEnemy);
            return;
        }

        // 3. 若当前未锁定敌人，但在前方附近有怪物，自动辅助锁定
        if (currentTarget == null || !currentTarget.IsAlive)
        {
            currentTarget = FindEnemyNear(transform.position, 3.5f);
        }

        attackCooldownTimer = CurrentAttackInterval;
        StartCoroutine(PerformAttackRoutine());
    }

    /// <summary>
    /// 虚拟摇杆与键盘 360° 平滑移动接口
    /// </summary>
    public void MoveWithInput(Vector2 moveDir)
    {
        if (!canMove || isRolling || moveDir.sqrMagnitude < 0.01f) return;

        // 摇杆/键盘输入时打断点击寻路路径
        isMoving = false;
        waypoints.Clear();

        float moveDt = GameJam.TimeManager.SlowMo ? GameJam.TimeManager.PlayerDelta : Time.deltaTime;
        Vector3 nextPos = transform.position + new Vector3(moveDir.x, moveDir.y, 0f) * (EffectiveMoveSpeed * moveDt);

        // 地面安全检测
        Vector3 groundRayPos = new Vector3(nextPos.x, nextPos.y - groundHeightOffset, 0f);
        Vector3Int nextCell = grid != null ? grid.WorldToCell(groundRayPos) : Vector3Int.zero;

        bool canPass = true;
        if (groundTilemap != null && !groundTilemap.HasTile(nextCell)) canPass = false;
        if (buildTilemap != null && buildTilemap != groundTilemap && buildTilemap.HasTile(nextCell)) canPass = false;
        if (IsometricPathfinder.DynamicBlockedCells.Contains(nextCell)) canPass = false;

        if (canPass)
        {
            transform.position = nextPos;

            if (moveDir.x > 0.05f) SetVisualFacing(false);
            else if (moveDir.x < -0.05f) SetVisualFacing(true);

            if (playerAnimator != null && playerAnimator.enabled && playerAnimator.runtimeAnimatorController != null)
            {
                playerAnimator.SetFloat("Speed", 1f);
            }
            else if (playerRenderer != null)
            {
                float bob = Mathf.Abs(Mathf.Sin(Time.time * walkBobFrequency)) * walkBobAmount;
                playerRenderer.transform.localPosition = new Vector3(0f, bob, 0f);
            }
        }
    }

    private IEnumerator PerformAttackRoutine()
    {
        isAttackingAnim = true;
        Vector3 attackDir = IsFacingLeft ? Vector3.left : Vector3.right;

        if (currentTarget != null && currentTarget.IsAlive)
        {
            Vector3 enemyPos = currentTarget.transform.position;
            attackDir = (enemyPos - transform.position).normalized;
            SetVisualFacing(attackDir.x < 0);
        }

        bool isExecution = (currentTarget != null && currentTarget.IsStaggered);

        if (hero != null)
        {
            if (isExecution)
            {
                hero.Play("Execute");
                hero.Face?.TriggerHappy();
                TimeManager.I?.EndSlowMo();
            }
            else
            {
                if (Time.time - lastAttackTime > 1.2f) comboStep = 0;
                string clip = comboStep == 0 ? "Sweep" : (comboStep == 1 ? "Thrust" : "Stomp");
                hero.Play(clip);
                hero.Face?.TriggerAngry();
                comboStep = (comboStep + 1) % 3;
                lastAttackTime = Time.time;
            }
        }
        else if (playerAnimator != null && playerAnimator.enabled && playerAnimator.runtimeAnimatorController != null)
        {
            playerAnimator.SetTrigger("Attack");
        }

        float dmg = isExecution ? CurrentAttackDamage * 2.5f : CurrentAttackDamage;
        float guardBreak = isExecution ? CurrentGuardBreakPower * 2.5f : CurrentGuardBreakPower;

        // 生成斩击光弧或重砸特效
        SpawnSlashEffect(attackDir, heldFurniture != null || isExecution);

        // 攻击微突进动作（仅对静态 Sprite 生效，骨骼动画自带攻击动效）
        if (playerRenderer != null && visualRoot == null)
        {
            playerRenderer.transform.localPosition = attackDir * (heldFurniture != null ? 0.22f : 0.15f);
        }

        if (currentTarget != null && currentTarget.IsAlive)
        {
            float hitDist = Vector2.Distance(transform.position, currentTarget.transform.position);
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
        }

        yield return new WaitForSeconds(heldFurniture != null ? 0.2f : 0.12f);

        if (playerRenderer != null)
        {
            playerRenderer.transform.localPosition = Vector3.zero;
        }

        yield return new WaitForSeconds(0.2f);
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
        if (sharedSlashMaterial == null)
        {
            var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (unlitShader != null) sharedSlashMaterial = new Material(unlitShader);
        }
        if (sharedSlashMaterial != null) sr.sharedMaterial = sharedSlashMaterial;

        // 手持家具猛砸为烈焰金橙色，常规剑击为青白银弧
        sr.color = isHeavySmash ? new Color(1f, 0.6f, 0.15f, 0.95f) : new Color(0.9f, 0.95f, 1f, 0.9f);
        sr.sortingOrder = 45;

        StartCoroutine(AnimateSlash(slashGo, sr, isHeavySmash));
    }

    private static Material sharedSlashMaterial;

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

        if (hero != null)
        {
            hero.ShellRoll(rollDir.x >= 0 ? 1 : -1, rollDuration);
        }

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
            if (buildTilemap != null && buildTilemap != groundTilemap && buildTilemap.HasTile(nextCell))
                canPass = false;

            if (canPass)
            {
                transform.position = nextPos;
            }

            if (hero == null)
            {
                if (visualRoot != null)
                {
                    float currentAngle = Mathf.Lerp(0f, totalSpin, t);
                    visualRoot.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
                }
                else if (playerRenderer != null)
                {
                    float currentAngle = Mathf.Lerp(0f, totalSpin, t);
                    playerRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
                }
            }

            yield return null;
        }

        if (hero == null)
        {
            if (visualRoot != null)
            {
                visualRoot.localRotation = Quaternion.identity;
            }
            if (playerRenderer != null)
            {
                playerRenderer.transform.localRotation = Quaternion.identity;
                playerRenderer.transform.localPosition = Vector3.zero;
            }
        }

        if (hero != null)
        {
            Vector3 punchTarget = (currentTarget != null && currentTarget.IsAlive) ? currentTarget.transform.position : (transform.position + rollDir * 2.2f);
            hero.ShellPunch(punchTarget);
            Sfx.Play("sfx_arrow_charge", 0.7f);

            yield return new WaitForSeconds(0.15f);
            CameraRig.I?.Shake(0.5f, 0.4f);
            ScreenFX.I?.Pulse();
            Sfx.Play("sfx_explosion");
            Vfx.Spawn("VFX_ArrowHit", punchTarget + Vector3.up * 0.4f, 0, 1.3f, true);
            Vfx.Spawn("VFX_ArrowBurst", punchTarget + Vector3.up * 0.6f, 0, 1.0f, true);

            var enemies = EnemyController.AllEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e != null && e.IsAlive && Vector2.Distance(punchTarget, e.transform.position) <= 2.2f)
                {
                    e.TakeDamage(CurrentAttackDamage * 2.0f, CurrentGuardBreakPower * 2.0f, transform.position);
                }
            }
        }

        isRolling = false;
        isInvulnerable = false;
    }

    private void UpdateMovement()
    {
        if (isRolling || !isMoving || waypoints.Count == 0 || currentWaypointIndex >= waypoints.Count)
        {
            if (playerAnimator != null && playerAnimator.enabled && playerAnimator.runtimeAnimatorController != null && !isMovingByKeyboard)
            {
                playerAnimator.SetFloat("Speed", 0f);
            }
            if (!isAttackingAnim && playerRenderer != null && !isRolling)
            {
                playerRenderer.transform.localPosition = Vector3.zero;
            }
            return;
        }

        Vector3 currentTargetWp = waypoints[currentWaypointIndex];

        if (currentTargetWp.x > transform.position.x + 0.02f)
            SetVisualFacing(false);
        else if (currentTargetWp.x < transform.position.x - 0.02f)
            SetVisualFacing(true);

        float pathDt = GameJam.TimeManager.SlowMo ? GameJam.TimeManager.PlayerDelta : Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, currentTargetWp, EffectiveMoveSpeed * pathDt);

        if (playerAnimator != null && playerAnimator.enabled && playerAnimator.runtimeAnimatorController != null)
        {
            playerAnimator.SetFloat("Speed", 1f);
        }
        else if (playerRenderer != null)
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
                if (playerAnimator != null && playerAnimator.enabled && playerAnimator.runtimeAnimatorController != null && !isMovingByKeyboard)
                {
                    playerAnimator.SetFloat("Speed", 0f);
                }
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
        isMovingByKeyboard = false;
        waypoints.Clear();
        if (playerAnimator != null && playerAnimator.enabled && playerAnimator.runtimeAnimatorController != null)
        {
            playerAnimator.SetFloat("Speed", 0f);
        }
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

        Vector3 dropPos = optionalDropPos ?? (transform.position + (IsFacingLeft ? Vector3.left * 0.6f : Vector3.right * 0.6f));
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
        var furn = FurnitureObject.GetNearestPlaced(transform.position, 1.95f);
        if (furn == null) return;

        Vector3 kickDir = (IsFacingLeft ? Vector3.left : Vector3.right);
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
        var furn = FurnitureObject.GetNearestPlaced(transform.position, 1.95f);
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
        if (rollCooldownTimer > 0f || isRolling || Unavailable) return false;

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
        else if (IsFacingLeft)
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
    /// 供手机 UI 底座一键触发普通攻击（自动锁定附近敌人出招或绝杀处决）
    /// </summary>
    public void PerformManualAttack()
    {
        if (isRolling || Unavailable) return;

        // 优先检查附近是否有处决就绪 (Pending) 的怪物！
        var pendingEnemy = EnemyController.FindPending();
        if (pendingEnemy != null && Vector2.Distance(transform.position, pendingEnemy.transform.position) <= 3.6f)
        {
            ExecuteEnemy(pendingEnemy);
            return;
        }

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

    private void CacheOriginalColors()
    {
        if (cachedVisualRenderers != null && cachedVisualRenderers.Length > 0) return;

        if (visualRoot != null)
        {
            cachedVisualRenderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        }
        else if (playerRenderer != null)
        {
            cachedVisualRenderers = new SpriteRenderer[] { playerRenderer };
        }

        if (cachedVisualRenderers != null)
        {
            cachedOriginalColors = new Color[cachedVisualRenderers.Length];
            for (int i = 0; i < cachedVisualRenderers.Length; i++)
            {
                if (cachedVisualRenderers[i] != null)
                {
                    Color c = cachedVisualRenderers[i].color;
                    // 防止受击泛红状态已被污染固化，若检测到纯红则兜底恢复默认纯白
                    if (c.r > 0.9f && c.g < 0.25f && c.b < 0.25f)
                        c = Color.white;
                    cachedOriginalColors[i] = c;
                }
            }
        }
    }

    public void ResetVisualColors()
    {
        if (cachedVisualRenderers != null && cachedOriginalColors != null)
        {
            for (int i = 0; i < cachedVisualRenderers.Length; i++)
            {
                if (cachedVisualRenderers[i] != null)
                {
                    cachedVisualRenderers[i].color = cachedOriginalColors[i];
                }
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (IsInvulnerable) return;
        CancelPlace();

        if (isGuarding)
        {
            OnParrySuccess(null);
            return;
        }

        currentHp -= damage;
        currentHp = Mathf.Max(0f, currentHp);

        if (hero != null)
        {
            hero.Play("Hurt");
            hero.Face?.TriggerPain();
        }

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.4f, $"-{damage:0}", new Color(1f, 0.25f, 0.25f));
        }

        if (hurtCoroutine != null)
        {
            StopCoroutine(hurtCoroutine);
        }
        hurtCoroutine = StartCoroutine(PlayerHurtRoutine());
    }

    private IEnumerator PlayerHurtRoutine()
    {
        CacheOriginalColors();

        if (cachedVisualRenderers != null && cachedVisualRenderers.Length > 0)
        {
            // 受击受创闪烁：高亮绯红闪烁（保留贴图透明度和明暗），闪烁时间 0.12s
            Color hurtFlashColor = new Color(1f, 0.35f, 0.35f, 1f);
            for (int i = 0; i < cachedVisualRenderers.Length; i++)
            {
                if (cachedVisualRenderers[i] != null)
                {
                    hurtFlashColor.a = cachedOriginalColors != null ? cachedOriginalColors[i].a : 1f;
                    cachedVisualRenderers[i].color = hurtFlashColor;
                }
            }

            yield return new WaitForSeconds(0.12f);

            // 彻底还原至缓存的绝对真实原始色，彻底解决连续受击叠加导致的永久变红 Bug
            ResetVisualColors();
        }
        hurtCoroutine = null;
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

    void OnDestroy()
    {
        if (markerRenderer != null && markerRenderer.material != null)
        {
            Destroy(markerRenderer.material);
        }
        if (sharedSlashMaterial != null)
        {
            Destroy(sharedSlashMaterial);
            sharedSlashMaterial = null;
        }
    }
}
