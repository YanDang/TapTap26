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
    public static PlayerController Instance { get; private set; }

    [Header("Dependencies (依赖组件)")]
    public Grid grid;
    public Tilemap groundTilemap;
    public Tilemap buildTilemap;
    public IsometricPathfinder pathfinder;

    [Header("Health & Movement (生命与移动属性)")]
    public float maxHp = 100f;
    public float currentHp = 100f;
    public bool IsAlive => currentHp > 0f && !isDefeatedRoutineRunning;
    public bool Dead => currentHp <= 0f || isDefeatedRoutineRunning;
    public float baseMoveSpeed = 4.0f;
    public float groundHeightOffset = 0.5f;
    public bool canMove = true;

    [Header("Combat Stats (核心战斗属性 - 纯数值，无需武器系统)")]
    public float attackDamage = 8f;
    public float guardBreakPower = 12f;
    public float attackInterval = 0.75f;

    [Header("Weapon Compatibility (工坊数据兼容)")]
    private WeaponData _equippedWeapon;
    public WeaponData equippedWeapon
    {
        get => _equippedWeapon;
        set
        {
            _equippedWeapon = value;
            if (_equippedWeapon != null)
            {
                attackDamage = _equippedWeapon.damage;
                guardBreakPower = _equippedWeapon.guardBreakPower;
                attackInterval = _equippedWeapon.attackInterval;
            }
        }
    }

    [Header("Held Furniture (当前手持家具 - 已废弃手持)")]
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
    public float guardWindowDuration = 0.48f;
    public float parryCooldown = 0.5f;
    public float slowMoSeconds = 2.5f;
    private float _parryReady = 0f;
    public float ParryRemaining => Mathf.Max(0, _parryReady - Time.unscaledTime);
    public int parrySuccess = 0;
    public int parryWhiff = 0;
    public int smashes = 0;
    public float lastSmashLevel = 0f;

    private bool isCharging = false;
    private bool isSmashing = false;
    private float chargeLevel = 0f;
    public bool IsCharging => isCharging;
    public bool IsSmashing => isSmashing;
    public float ChargeLevel => chargeLevel;
    public float chargeHoldDelay = 0.18f;
    public float chargeTime = 1.0f;
    public float smashLunge = 1.4f;

    [Header("Combat Locking & Controls (战斗锁定与双键操作规范 - 对齐 gamejam-integration)")]
    public float reLockRadius = 8f;
    private bool _manualInCombat = false;
    public bool InCombat
    {
        get
        {
            if (_manualInCombat) return true;
            if (currentTarget != null && currentTarget.IsAlive) return true;
            var enemies = EnemyController.AllEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e != null && e.IsAlive && Vector2.Distance(transform.position, e.transform.position) <= reLockRadius)
                    return true;
            }
            return false;
        }
        private set => _manualInCombat = value;
    }
    private bool _leftHeld = false;
    private float _leftDownAt = -1f;

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

    // 状态
    public bool Unavailable => isGrabbed || isThrown || isDefeatedRoutineRunning || currentHp <= 0f;
    public bool IsGrabbed => isGrabbed;
    public bool IsThrown => isThrown;
    public bool IsInvulnerable => isRolling || isInvulnerable || Unavailable || (Ultimate && Ultimate.DashInvulnerable);
    public GameJam.HeroUlt.TapHeroUlt Ultimate => GetComponent<GameJam.HeroUlt.TapHeroUlt>();
    public bool UltimateLocked { get; private set; }
    public string UltimateEligibility => $"enabled={isActiveAndEnabled} alive={IsAlive} unavailable={Unavailable} rolling={isRolling} charging={isCharging} smashing={isSmashing} attacking={isAttackingAnim} guarding={isGuarding} placing={Placing} furniture={heldFurniture!=null} canMove={canMove} execution={executionTarget!=null}";
    public bool CanBeginUltimate => isActiveAndEnabled && IsAlive && !Unavailable && !isRolling && !isCharging && !isSmashing && !isAttackingAnim && !isGuarding && !Placing && heldFurniture == null && canMove && executionTarget == null;
    public void BeginUltimateLock() { CancelLeftHold(); StopMovement(); UltimateLocked=true; }
    public void EndUltimateLock() { UltimateLocked=false; }
    public bool CanRoll => (Gear && Gear.UsesCharges ? Gear.DodgeAvailable : rollCooldownTimer <= 0f) && !isRolling && !Unavailable;
    public float RollCooldownTimer => rollCooldownTimer;
    public float RollCooldown => rollCooldown;
    public float DodgeRemaining => Mathf.Max(0f, rollCooldownTimer);
    public float dodgeCooldown => rollCooldown;
    private bool isRolling = false;
    private bool isInvulnerable = false;
    private bool isGrabbed = false;
    private bool isThrown = false;
    private EnemyController grabberEnemy = null;
    private float rollCooldownTimer = 0f;

    // 玩家头顶仅保留精力条；HP 由 HUD 显示。保留共用根节点名称。
    private GameObject playerHpBarRoot;
    private Transform playerStaminaBarFill;
    private bool isDefeatedRoutineRunning = false;
    private float rollSafetyTimer = 0f;
    private float thrownSafetyTimer = 0f;

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
    private float touchHoldTimer = 0f;

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
            return baseMoveSpeed * moveSpeedMultiplier * BossGroundEffects.PlayerMoveMultiplier(this) * (Gear ? Gear.MoveMultiplier : 1f);
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
    /// 进入下一天时完全恢复生命与精力（安眠修整）
    /// </summary>
    public void RestFullRecovery()
    {
        float healed = maxHp - currentHp;
        currentHp = maxHp;
        currentStamina = maxStamina;

        if (hero != null && hero.Face != null)
        {
            hero.Face.TriggerHappy();
        }

        if (DamageTextManager.Instance != null)
        {
            if (healed > 0.5f)
            {
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 1.0f, $"💚 休憩安眠，生命恢复 +{healed:0} (完全回满)！", new Color(0.2f, 1f, 0.4f), 0.16f);
            }
            else
            {
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 1.0f, "✨ 精神饱满，状态最佳！", new Color(0.3f, 0.9f, 1f), 0.14f);
            }
        }
        CameraRig.I?.Shake(0.2f, 0.15f);
    }

    /// <summary>
    /// 获取当前攻击力 (纯属性数值)
    /// </summary>
    public float CurrentAttackDamage => attackDamage;

    /// <summary>
    /// 获取当前削韧破防力 (纯属性数值)
    /// </summary>
    public float CurrentGuardBreakPower => guardBreakPower;

    /// <summary>
    /// 获取当前攻击间隔 (秒)
    /// </summary>
    public float CurrentAttackInterval => attackInterval / (Gear ? Gear.AttackMultiplier : 1f);
    public GameJam.Gear.TapGearCombat Gear => GetComponent<GameJam.Gear.TapGearCombat>();
    public void GearMoveSafe(Vector3 delta) => MoveSafe(delta);
    public void GearRestoreDodge() { rollCooldownTimer=0; Gear?.RestoreDodge(); }

    void Awake()
    {
        Instance = this;
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
        if (visualRoot == null)
        {
            var vr = transform.Find("VisualRoot");
            if (vr != null) visualRoot = vr;
        }

        // 为角色视觉根节点绑定 SortingGroup，将 Hero2D 的全部肢体骨骼 (图层 1~20) 打包为不可分割的原子渲染单元 (层级 10)！
        // 彻底解决主角与门扉/家具之间肢体被部分截断、前后遮挡错乱的严重穿模 Bug！
        Transform sortTarget = visualRoot != null ? visualRoot : transform;
        var sg = sortTarget.GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (sg == null) sg = sortTarget.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
        sg.sortingLayerName = "Default";
        sg.sortingOrder = 10;

        if (Camera.main != null)
        {
            Camera.main.transparencySortMode = TransparencySortMode.CustomAxis;
            Camera.main.transparencySortAxis = new Vector3(0f, 1f, -0.26f);
        }

        if (visualRoot == null && playerRenderer == null)
            playerRenderer = GetComponentInChildren<SpriteRenderer>();

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
        CreatePlayerHealthBar();
        CacheOriginalColors();

        if (HUD.I == null)
        {
            HUD.Build();
        }
    }

    public void AttachHero2D()
    {
        if (hero != null) return;
        var prefab = Resources.Load<GameObject>("Hero2D/Hero2D");
        if (prefab == null) return;

        if (playerRenderer != null) playerRenderer.enabled = false;
        if (playerAnimator != null) playerAnimator.enabled = false;

        Transform parentTransform = visualRoot != null ? visualRoot : transform;
        var sg2 = parentTransform.GetComponent<UnityEngine.Rendering.SortingGroup>();
        if (sg2 == null) sg2 = parentTransform.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
        sg2.sortingLayerName = "Default";
        sg2.sortingOrder = 10;

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
        var enemies = EnemyController.AllEnemies;
        for (int i = 0; i < enemies.Count; i++)
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
        if (UltimateLocked) return;
        if (attacker == null || !attacker.IsAlive || attacker.IsEmerging) return;
        Ultimate?.AddCharge(5);
        Gear?.Block();
        _parryReady = Time.unscaledTime + parryCooldown;
        parrySuccess++;
        isGuarding = false;
        guardWindowTimer = 0f;

        if (attacker != null)
        {
            // 背对敌人用壳防御（HeroActions 中 ShellGuard 的设计逻辑：背壳迎敌）
            SetVisualFacing(attacker.transform.position.x > transform.position.x);
            SetAttackTarget(attacker);
            attacker.Parried();
        }

        if (hero != null)
        {
            hero.Play("ShellGuard");
        }

        TimeManager.I?.StartSlowMo(slowMoSeconds);
        ScreenFX.I?.Pulse();
        CameraRig.I?.Shake(0.2f, 0.25f);
        Sfx.Play("sfx_parry", 1f, true);
        Sfx.Play("sfx_tongtou", 0.9f, true);

        Vector3 sparkPos = (attacker != null) ? Vector3.Lerp(transform.position, attacker.transform.position, 0.5f) : (transform.position + Vector3.up * 0.5f);
        Vfx.Spawn("VFX_ParrySpark", sparkPos + Vector3.up * 0.6f, 0, 1.2f, true);

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.7f, "⚡ 完美弹反!", new Color(1f, 0.9f, 0.2f), 0.08f);
        }
    }

    public void TriggerShellGuard()
    {
        if (UltimateLocked) return;
        if (ParryRemaining > 0f || isRolling || isCharging || isSmashing || Unavailable || Dead) return;

        // 0. 时钟倒计时 QTE 判定
        var clock = GameJam.ClockQTE.Current;
        if (clock != null && clock.Open && clock.TryHit())
        {
            if (clock.owner != null) ExecuteEnemy(clock.owner);
            return;
        }

        // 1. 如果身边有正处于可格挡黄光窗口的敌人，直接触发完美弹刀！
        var parryEnemy = FindParryableEnemy();
        if (parryEnemy != null)
        {
            DoParry(parryEnemy);
            return;
        }

        // 2. 正常缩壳格挡 (Whiff)
        _parryReady = Time.unscaledTime + parryCooldown;
        parryWhiff++;
        isGuarding = true;
        guardWindowTimer = guardWindowDuration;

        StopMovement();

        // 背向最近敌人（贝壳位于寄居蟹背部，缩壳时以背部迎向威胁）
        var nearest = (currentTarget != null && currentTarget.IsAlive) ? currentTarget : NearestEnemy(6f);
        if (nearest != null)
        {
            SetVisualFacing(nearest.transform.position.x > transform.position.x);
        }

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

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.5f, "🛡️ 缩壳防御", Color.cyan, 0.08f);
        }
    }

    public void OnParrySuccess(EnemyController attacker)
    {
        DoParry(attacker);
    }

    public bool IsGrabbedBy(EnemyController by) => isGrabbed && grabberEnemy == by;

    public bool TryGrab(EnemyController by)
    {
        if (Gear && (Gear.Rooted || Gear.Bubble)) return false;
        if (Ultimate && Ultimate.TryAbsorb(by,true)) return false;
        if (UltimateLocked) return false;
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

        if (Gear && Gear.FilterDamage(grabberEnemy, false, ref dmg)) return;
        Gear?.Hurt();
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
            if (grabberEnemy != null) grabberEnemy.CancelGrab();
            TriggerDefeat();
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
        if (UltimateLocked) return;
        if (executionTarget != null || enemy == null || !enemy.CanExecute || enemy.aiState == EnemyController.EnemyAIState.Executing) return;
        executionRoutine=StartCoroutine(ExecuteRoutine(enemy));
    }

    private IEnumerator ExecuteRoutine(EnemyController enemy)
    {
        if (enemy == null || !enemy.CanExecute) yield break;
        executionTarget=enemy;
        try {
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
        if (enemy == null || !enemy.CanExecute) yield break;
        Vfx.Spawn("VFX_ExecuteSlash", enemy.transform.position + Vector3.up * 0.5f, dashDir.x >= 0 ? 0 : 180f, 1.4f, true);
        CameraRig.I?.Shake(0.35f, 0.3f);

        // 斩杀敌人
        float beforeExecution=enemy.currentHp;
        enemy.KillByExecution(dashDir);
        NotifyDamageDealt(enemy, beforeExecution);
        if (enemy.currentHp < beforeExecution) { Ultimate?.AddCharge(20); Gear?.Executed(); }
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
        } finally {
            if (enemy != null) enemy.CancelExecution();
            executionTarget=null;
            isAttackingAnim=false;
        }
    }

    public void StartCharge()
    {
        if (UltimateLocked) return;
        if (isRolling || Unavailable || isSmashing) return;
        isCharging = true;
        chargeLevel = 0f;
        StopMovement();
        if (hero != null)
        {
            hero.BeginCharge();
            hero.Face?.SetBase(HeroFace.Expr.Angry);
        }
        Sfx.Play("sfx_arrow_charge", 0.6f);
    }

    public void ReleaseCharge()
    {
        if (!isCharging) return;
        isCharging = false;
        _leftHeld = false;
        StartCoroutine(PerformSmashRoutine(chargeLevel));
    }

    public void CancelCharge()
    {
        if (!isCharging) return;
        isCharging = false;
        _leftHeld = false;
        hero?.Stop();
    }

    public void PerformChargedSmash(float level)
    {
        if (UltimateLocked) return;
        StartCoroutine(PerformSmashRoutine(level));
    }

    private IEnumerator PerformSmashRoutine(float lv)
    {
        isSmashing = true;
        lastSmashLevel = lv;
        smashes++;

        if (hero != null)
        {
            hero.ReleaseCharge();
            hero.Face?.TriggerAngry();
        }
        Sfx.Play("sfx_swing", 0.9f);

        // 确定冲刺前扑的目标和方向
        EnemyController target = (currentTarget != null && currentTarget.IsAlive) ? currentTarget : NearestEnemy(8f);
        Vector3 dir;
        if (target != null)
        {
            dir = (target.transform.position - transform.position).normalized;
            dir.z = 0;
            SetVisualFacing(dir.x < 0);
        }
        else
        {
            dir = IsFacingLeft ? Vector3.left : Vector3.right;
        }

        // 0.10 ~ 0.22s 往前扑（扑到离敌人约 0.9m 处为止，最远 smashLunge）
        float maxGo;
        if (target != null)
        {
            float curDist = Vector2.Distance(transform.position, target.transform.position);
            maxGo = Mathf.Clamp(curDist - 0.9f, 0f, smashLunge);
        }
        else
        {
            maxGo = smashLunge * 0.5f;
        }

        float t = 0f;
        bool hasHit = false;
        float smashAnimDuration = 0.78f;

        while (t < smashAnimDuration)
        {
            float prevT = t;
            float dt = GameJam.TimeManager.SlowMo ? GameJam.TimeManager.PlayerDelta : Time.deltaTime;
            t += dt;

            // 前扑位移区间 0.10s ~ 0.22s
            float u0 = Mathf.Clamp01((prevT - 0.10f) / 0.12f);
            float u1 = Mathf.Clamp01((t - 0.10f) / 0.12f);
            if (u1 > u0 && maxGo > 0.01f)
            {
                Vector3 delta = dir * (maxGo * (u1 - u0));
                MoveSafe(delta);
            }

            // 0.22s 命中帧 (hero.T >= hero.Hit) 触发重砸 DoSmash
            if (!hasHit && t >= 0.22f)
            {
                hasHit = true;
                DoSmash(lv, dir, target);
            }

            yield return null;
        }

        if (!hasHit)
        {
            DoSmash(lv, dir, target);
        }

        isSmashing = false;
    }

    private void DoSmash(float lv, Vector3 dir, EnemyController target)
    {
        float radius = 1.4f + 0.9f * lv;
        Vector3 center = transform.position + dir * 0.8f;
        float damageMult = 1.1f + lv * 0.8f;
        float dmg = CurrentAttackDamage * damageMult;
        float gb = CurrentGuardBreakPower * (1.3f + lv * 0.8f);

        if (Gear && Gear.WaterSmash(dir, dmg)) return;

        // 1. AOE 重砸敌人并击退削韧
        var enemies = EnemyController.AllEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e != null && e.IsAlive)
            {
                float dist = Vector2.Distance(center, e.transform.position);
                if (dist <= radius)
                {
                    DealDamage(e, dmg, gb);
                    Vector3 knockDir = (e.transform.position - transform.position).normalized;
                    e.Displace(knockDir, (0.35f + 0.45f * lv) * (TapCombatFeel.On ? TapCombatFeel.WorldScale : 1f), this);   // gamejam 手感：按世界尺度换算
                }
            }
        }

        // 2. 重砸范围内家具
        HitNearbyFurniture(center, dir, radius, dmg);

        // 3. 卡肉顿帧与视听震动
        TimeManager.I?.DoHitstop(0.05f + 0.1f * lv);
        CameraRig.I?.Shake(0.25f + 0.45f * lv, 0.25f + 0.2f * lv);
        if (lv > 0.95f)
        {
            ScreenFX.I?.Pulse();
        }

        Sfx.Play(lv > 0.6f ? "sfx_explosion" : "sfx_kick", 0.7f + 0.3f * lv);
        Vfx.Spawn("VFX_ArrowHit", center + Vector3.up * 0.3f, 0, 0.8f + 0.8f * lv, true);

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(center + Vector3.up * 0.8f, $"💥 震地重砸 x{damageMult:0.0}!", new Color(1f, 0.45f, 0.1f), 0.08f);
        }
    }

    private void MoveSafe(Vector3 delta)
    {
        Vector3 nextPos = transform.position + delta;
        Vector3 groundRayPos = new Vector3(nextPos.x, nextPos.y - groundHeightOffset, 0f);
        Vector3Int nextCell = grid != null ? grid.WorldToCell(groundRayPos) : Vector3Int.zero;

        bool canPass = true;
        if (groundTilemap != null && !groundTilemap.HasTile(nextCell)) canPass = false;
        if (buildTilemap != null && buildTilemap != groundTilemap && buildTilemap.HasTile(nextCell)) canPass = false;
        if (IsometricPathfinder.DynamicBlockedCells.Contains(nextCell)) canPass = false;

        if (canPass)
        {
            transform.position = nextPos;
        }
        else
        {
            Vector3 deltaX = new Vector3(delta.x, 0, 0);
            Vector3 groundRayX = new Vector3(transform.position.x + deltaX.x, transform.position.y - groundHeightOffset, 0f);
            Vector3Int cellX = grid != null ? grid.WorldToCell(groundRayX) : Vector3Int.zero;
            if (groundTilemap != null && groundTilemap.HasTile(cellX) && !IsometricPathfinder.DynamicBlockedCells.Contains(cellX))
            {
                transform.position += deltaX;
            }
        }
    }

    private void HitNearbyFurniture(Vector3 center, Vector3 dir, float radius, float dmg)
    {
        var all = FurnitureObject.AllFurniture;
        for (int i = 0; i < all.Count; i++)
        {
            var f = all[i];
            if (f != null && f.currentState == FurnitureObject.FurnitureState.Placed)
            {
                float d = Vector2.Distance(center, f.transform.position);
                if (d <= radius + 0.5f)
                {
                    f.TakeDamage(dmg);
                }
            }
        }
    }

    void OnDisable()
    {
        Ultimate?.Cancel(false);
        EndDodge();
        if (executionRoutine != null) { StopCoroutine(executionRoutine); executionRoutine=null; }
        if (executionTarget != null) executionTarget.CancelExecution();
        executionTarget=null;
        isAttackingAnim=false;
        CancelPlace();
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
        if (UltimateLocked) return;
        // 工坊或温室叠加开启时，战斗角色输入与逻辑全面静默冻结
        if (PlayerSessionData.IsCombatInputBlocked) return;

        // 阵亡状态下等待复苏协程，不处理常规操作
        if (isDefeatedRoutineRunning || currentHp <= 0f)
        {
            if (!isDefeatedRoutineRunning)
            {
                TriggerDefeat();
            }
            return;
        }

        // 缩壳翻滚超时安全守卫（防止因协程断裂或时停造成永久缩壳）
        if (isRolling)
        {
            rollSafetyTimer += Time.unscaledDeltaTime;
            if (rollSafetyTimer > 1.8f)
            {
                EndDodge();
                isRolling = false;
                isInvulnerable = false;
                rollSafetyTimer = 0f;
                if (hero != null && hero.IsInShell) hero.CancelShell();
            }
        }
        else
        {
            rollSafetyTimer = 0f;
        }

        // 击飞倒地超时安全守卫
        if (isThrown)
        {
            thrownSafetyTimer += Time.unscaledDeltaTime;
            if (thrownSafetyTimer > 2.2f)
            {
                isThrown = false;
                thrownSafetyTimer = 0f;
            }
            return;
        }
        else
        {
            thrownSafetyTimer = 0f;
        }

        // 处于被缠绕状态时，吸附在鱿鱼触手前并禁止主动操作
        if (isGrabbed)
        {
            if (grabberEnemy == null || !grabberEnemy.IsAlive)
            {
                isGrabbed = false;
                grabberEnemy = null;
                if (hero != null) { hero.EndStruggle(); if (hero.Face != null) hero.Face.wrapped = false; }
                return;
            }
            if (hero != null && hero.Face != null)
            {
                hero.Face.wrapped = true;
                hero.Face.SetBase(HeroFace.Expr.Pain);
            }
            Vector3 holdPos = grabberEnemy.transform.position + (transform.position.x > grabberEnemy.transform.position.x ? Vector3.right : Vector3.left) * 0.55f;
            transform.position = Vector3.Lerp(transform.position, holdPos, Time.deltaTime * 12f);
            SetVisualFacing(grabberEnemy.transform.position.x < transform.position.x);
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

        // 键盘按键映射：空格格挡，J 键攻击，Shift 翻滚闪避，F 键踢推附近家具
        if (Input.GetKeyDown(KeyCode.Space) && !Gear)
        {
            TriggerShellGuard();
        }
        else if (Input.GetKeyDown(KeyCode.J))
        {
            TriggerManualAttack();
        }
        else if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            TriggerDodgeRoll();
        }
        else if (Input.GetKeyDown(KeyCode.F))
        {
            KickNearbyFurniture();
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

        // 鼠标与触控按住蓄力更新（对齐 gamejam-integration）
        if (_leftHeld && hero != null && !isRolling && !Unavailable && !isSmashing)
        {
            float holdTime = Time.unscaledTime - _leftDownAt;
            if (holdTime >= chargeHoldDelay)
            {
                if (!isCharging)
                {
                    StartCharge();
                }
                chargeLevel = Mathf.Clamp01((holdTime - chargeHoldDelay) / chargeTime);
                hero.SetCharge(chargeLevel);

                var tgt = (currentTarget != null && currentTarget.IsAlive) ? currentTarget : NearestEnemy(8f);
                if (tgt != null)
                {
                    SetVisualFacing(tgt.transform.position.x < transform.position.x);
                }
            }
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

        // 手机触控：多点与单点触控友好处理（支持一手摇杆/拖曳走位，另一手点击动作按键）
        if (Input.touchCount > 0)
        {
            bool trackedFingerActive = false;
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                if (isPointerDown && touch.fingerId == pointerFingerId)
                {
                    trackedFingerActive = true;
                    if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                    {
                        touchHoldTimer += Time.deltaTime;
                        // 若按住超过 0.12s 且产生持续拖曳位移，实时更新寻路目的地实现跟手平滑走位
                        if (touchHoldTimer >= 0.12f)
                        {
                            Vector2 delta = touch.position - pointerDownPos;
                            if (delta.sqrMagnitude > 300f || touchHoldTimer > 0.25f)
                            {
                                if (cam != null && PlayerSessionData.CurrentMobileControlMode == PlayerSessionData.MobileControlMode.TapToMove)
                                {
                                    Vector3 worldPos = cam.ScreenToWorldPoint(touch.position);
                                    worldPos.z = 0f;
                                    MoveToWorldPoint(worldPos);
                                }
                            }
                        }
                    }
                    else if (touch.phase == TouchPhase.Ended)
                    {
                        EvaluatePointerRelease(touch.position);
                        isPointerDown = false;
                        pointerFingerId = -1;
                        touchHoldTimer = 0f;
                    }
                    else if (touch.phase == TouchPhase.Canceled)
                    {
                        isPointerDown = false;
                        pointerFingerId = -1;
                        touchHoldTimer = 0f;
                    }
                }
                else if (!isPointerDown && touch.phase == TouchPhase.Began)
                {
                    // 触摸如果落在 UI 上或落在底部控制底座区，忽略该手指作为地面交互
                    if (IsPointerOverUI(touch.fingerId) || IsInBottomDockArea(touch.position))
                        continue;

                    pointerFingerId = touch.fingerId;
                    touchHoldTimer = 0f;
                    StartPointer(touch.position);
                    trackedFingerActive = true;
                }
            }

            if (isPointerDown && !trackedFingerActive)
            {
                isPointerDown = false;
                pointerFingerId = -1;
                touchHoldTimer = 0f;
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
        if (UltimateLocked) return;
        if (PlayerSessionData.CurrentMobileControlMode == PlayerSessionData.MobileControlMode.Joystick) return;
        if (Unavailable || isRolling) return;
        if (InCombat)
        {
            ExitCombat();
        }
        MoveToWorldPoint(p);
    }

    public void RightClickEnemy(EnemyController e)
    {
        if (UltimateLocked) return;
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
        if (UltimateLocked) { Ultimate?.ReleaseEcho(); return; }
        _leftHeld = false;
        if (Unavailable || isRolling || isSmashing) return;

        // 1. 时钟倒计时 QTE
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
        if (ParryRemaining <= 0f)
        {
            var parryable = FindParryableEnemy();
            if (parryable != null)
            {
                DoParry(parryable);
                return;
            }
        }

        // 4. 寄居蟹主角：记录按下时刻。按住超过 chargeHoldDelay 进入蓄力，短按松开则触发空挡防御
        _leftHeld = true;
        _leftDownAt = Time.unscaledTime;
    }

    public void LeftRelease()
    {
        if (UltimateLocked) return;
        if (!_leftHeld) return;
        _leftHeld = false;

        // A. 蓄力中松开 -> 触发冲刺重砸
        if (isCharging)
        {
            ReleaseCharge();
            return;
        }

        if (Unavailable || isRolling || isSmashing) return;

        // B. 短按松开（按住时长 < chargeHoldDelay），执行防御/弹刀判定
        if (Time.unscaledTime - _leftDownAt < chargeHoldDelay)
        {
            var clock = GameJam.ClockQTE.Current;
            if (clock != null && clock.Open && clock.TryHit())
            {
                if (clock.owner != null) ExecuteEnemy(clock.owner);
                return;
            }

            var pend = EnemyController.FindPending();
            if (pend != null)
            {
                ExecuteEnemy(pend);
                return;
            }

            var parryable = FindParryableEnemy();
            if (parryable != null && ParryRemaining <= 0f)
            {
                DoParry(parryable);
                return;
            }

            if (ParryRemaining <= 0f)
            {
                TriggerShellGuard();
            }
        }
    }

    public void CancelLeftHold()
    {
        _leftHeld = false;
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
        if (UltimateLocked) return false;
        if (PlayerSessionData.IsCombatInputBlocked || Placing || Dead || Unavailable || isRolling || isCharging || isAttackingAnim) return false;
        CancelLeftHold();
        _placeRelease = release; _placeCancel = cancel;
        Placing = true;
        StopMovement();

        Vector3 dir = target - transform.position;
        dir.z = 0;
        if (dir.sqrMagnitude > 1e-4f) SetVisualFacing(dir.x < 0);

        placeRoutine = StartCoroutine(PlaceRoutine());
        return true;
    }

    private Coroutine placeRoutine;

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
        if (placeRoutine != null) StopCoroutine(placeRoutine);
        placeRoutine = null;
        var c = _placeCancel;
        _placeRelease = _placeCancel = null;
        c?.Invoke();
    }

    public void ConsumeDoorInteraction()
    {
        CancelLeftHold();
        isLeftPointerDown = false;
        isPointerDown = false;
        isGuarding = false;
        guardWindowTimer = 0f;
    }

    private bool TryInteractWithDoorAtScreenPosition(Vector2 screenPosition)
    {
        if (cam == null) return false;
        Vector3 worldPosition = cam.ScreenToWorldPoint(screenPosition);
        worldPosition.z = 0f;
        foreach (var hit in Physics2D.OverlapPointAll(worldPosition))
        {
            var door = hit.GetComponentInParent<GatherDoorAuraBehavior>();
            if (door == null || door.parentFurniture == null ||
                door.parentFurniture.currentState != FurnitureObject.FurnitureState.Placed) continue;
            ConsumeDoorInteraction();
            if (Vector2.Distance(transform.position, door.transform.position) <= door.interactionRadius + 0.8f)
                door.OnPlayerInteract(this);
            else if (PlayerSessionData.CurrentMobileControlMode == PlayerSessionData.MobileControlMode.TapToMove)
                MoveToWorldPoint(door.transform.position - Vector3.up * 1.2f);
            return true;
        }
        return false;
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
            if (PlayerSessionData.CurrentMobileControlMode == PlayerSessionData.MobileControlMode.Joystick) return;
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
            if (TryInteractWithDoorAtScreenPosition(Input.mousePosition)) return;
            leftPointerDownPos = Input.mousePosition;
            leftPointerDownTime = Time.time;
            isLeftPointerDown = true;
            LeftClick();
        }
        else if (Input.GetMouseButtonUp(0))
        {
            if (isLeftPointerDown)
            {
                float duration = Time.time - leftPointerDownTime;
                Vector2 delta = (Vector2)Input.mousePosition - leftPointerDownPos;
                float distance = delta.magnitude;
                float speed = distance / Mathf.Max(duration, 0.001f);
                bool isSwipe = (distance >= minSwipeDistance && duration <= maxSwipeDuration && speed >= 120f);

                if (isSwipe)
                {
                    CancelLeftHold();
                    EvaluateLeftClickRelease(Input.mousePosition);
                }
                else
                {
                    LeftRelease();
                }
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

        // 鼠标快速短滑 (Swipe)：第一优先级推动家具，第二优先级闪避翻滚
        if (distance >= minSwipeDistance && duration <= maxSwipeDuration && speed >= 120f)
        {
            // 在摇杆控制模式下，划动推家具与划动翻滚均已由右侧动作盘接管，屏幕划动予以屏蔽
            if (PlayerSessionData.CurrentMobileControlMode == PlayerSessionData.MobileControlMode.Joystick)
                return;

            Vector2 swipeDir = delta.normalized;

            // 1. 第一优先级：滑动推动身边的家具（优先级高于主角闪避翻滚）
            if (TrySwipePushFurniture(leftPointerDownPos, releasePos, swipeDir))
            {
                return;
            }

            // 2. 第二优先级：未命中任何可推家具时，主角执行闪避翻滚
            if (Gear && Gear.UsesCharges ? Gear.DodgeAvailable : rollCooldownTimer <= 0f)
            {
                StartCoroutine(PerformDodgeRollRoutine(swipeDir));
                return;
            }
        }
    }

    /// <summary>
    /// 【滑动推动家具】（第一优先级，高于玩家闪避翻滚）
    /// 规则：
    /// 1. 玩家近身 (2.4m 内) 存在放置态家具；
    /// 2. 滑动起点/释放点靠近家具，或滑动轨迹划过家具，或玩家在家具旁朝家具/顺势外侧划动；
    /// 3. 顺应滑动方向将家具高速推撞出去！
    /// </summary>
    public bool TrySwipePushFurniture(Vector2 startScreenPos, Vector2 releaseScreenPos, Vector2 swipeDir)
    {
        if (UltimateLocked) return false;
        if (PlayerSessionData.CurrentMobileControlMode == PlayerSessionData.MobileControlMode.Joystick)
            return false;

        if (cam == null) return false;

        Vector3 startWorldPos = cam.ScreenToWorldPoint(startScreenPos);
        startWorldPos.z = 0f;
        Vector3 releaseWorldPos = cam.ScreenToWorldPoint(releaseScreenPos);
        releaseWorldPos.z = 0f;

        FurnitureObject bestFurn = null;
        float bestScore = float.MaxValue;

        var all = FurnitureObject.AllFurniture;
        for (int i = 0; i < all.Count; i++)
        {
            var f = all[i];
            if (f == null || f.currentState != FurnitureObject.FurnitureState.Placed || f.Deploying || f.isImmovable || f.instanceId == "furn_gather_door" || f.GetComponent<GatherDoorAuraBehavior>() != null) continue;

            float playerDist = Vector2.Distance(transform.position, f.transform.position);
            if (playerDist > 2.4f) continue; // 必须在角色可触及的推动交互范围内

            float touchDist = Vector2.Distance(startWorldPos, f.transform.position);
            float releaseDist = Vector2.Distance(releaseWorldPos, f.transform.position);

            // 计算滑动轨迹线段到家具中心的距离
            Vector2 segA = startWorldPos;
            Vector2 segB = releaseWorldPos;
            Vector2 fPos = f.transform.position;
            Vector2 ab = segB - segA;
            float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector2.Dot(fPos - segA, ab) / ab.sqrMagnitude) : 0f;
            float lineDist = Vector2.Distance(fPos, segA + ab * t);

            bool isPushIntent = false;

            // 意图 A: 手指/光标划过了家具，或滑动起点/终点落在家具附近
            if (touchDist <= 1.6f || releaseDist <= 1.4f || lineDist <= 1.0f)
            {
                isPushIntent = true;
            }
            // 意图 B: 玩家紧挨着家具 (<= 1.85m)，朝向家具或从家具顺势往外侧滑动
            else if (playerDist <= 1.85f)
            {
                Vector2 toFurn = ((Vector2)f.transform.position - (Vector2)transform.position).normalized;
                if (Vector2.Dot(swipeDir, toFurn) > -0.35f)
                {
                    isPushIntent = true;
                }
            }

            if (isPushIntent)
            {
                float score = Mathf.Min(touchDist, lineDist);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestFurn = f;
                }
            }
        }

        if (bestFurn != null)
        {
            Vector3 kickDir = new Vector3(swipeDir.x, swipeDir.y, 0f).normalized;
            SetVisualFacing(kickDir.x < 0);

            bestFurn.Kick(kickDir);

            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(bestFurn.transform.position + Vector3.up * 0.5f, "PUSH!", new Color(1f, 0.45f, 0.1f), 0.11f);
            }
            return true;
        }

        return false;
    }

    private void OnLeftClickTap(Vector2 screenPosition)
    {
        if (cam == null || grid == null || groundTilemap == null) return;

        Vector3 worldPos = cam.ScreenToWorldPoint(screenPosition);
        worldPos.z = 0f;

        // 1. 检查是否点击到了敌人（左键锁定/普通攻击）
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

        // 2. 检查是否点击到了家具：若点击到了异界门扉，触发门扉互动
        FurnitureObject clickedFurn = FurnitureObject.GetNearestPlaced(worldPos, 1.8f);
        if (clickedFurn != null)
        {
            var door = clickedFurn.GetComponent<GatherDoorAuraBehavior>();
            if (door != null)
            {
                float dist = Vector2.Distance(transform.position, door.transform.position);
                if (dist <= door.interactionRadius + 0.8f)
                {
                    door.OnPlayerInteract(this);
                }
                else if (PlayerSessionData.CurrentMobileControlMode == PlayerSessionData.MobileControlMode.TapToMove)
                {
                    MoveToWorldPoint(door.transform.position - new Vector3(0f, 1.2f, 0f));
                }
                return;
            }
            return;
        }

        // 3. 左键点击空地：原地挥空出招，绝不产生误走位或误举起身边家具
        PerformManualAttack();
    }

    private void StartPointer(Vector2 screenPos)
    {
        if (TryInteractWithDoorAtScreenPosition(screenPos)) return;
        pointerDownPos = screenPos;
        pointerDownTime = Time.time;
        isPointerDown = true;
    }

    /// <summary>
    /// 评估移动端手势松开：
    /// 1. 划动家具 -> 踢出家具冲撞敌人；
    /// 2. 划动地面 -> 快速短滑翻滚躲避；
    /// 3. 轻触敌人 -> 锁定并手动近战攻击；
    /// 4. 轻触门扉 -> 开启异界门挑战；
    /// 5. 轻触地面 -> 移动至目标点（彻底杜绝误触发格挡缩壳与误蓄力）；
    /// </summary>
    private void EvaluatePointerRelease(Vector2 releasePos)
    {
        float duration = Time.time - pointerDownTime;
        Vector2 delta = releasePos - pointerDownPos;
        float distance = delta.magnitude;
        float speed = distance / Mathf.Max(duration, 0.001f);

        // 1. 判断是否为【快速短滑】(Swipe)：第一优先级推家具，第二优先级闪避翻滚
        if (distance >= minSwipeDistance && duration <= maxSwipeDuration && speed >= 120f)
        {
            // 摇杆控制模式下彻底屏蔽屏幕划动推家具与划动翻滚，避免误触
            if (PlayerSessionData.CurrentMobileControlMode == PlayerSessionData.MobileControlMode.Joystick)
                return;

            Vector2 swipeDir = delta.normalized;

            // 1. 第一优先级：滑动推动身边的家具（优先级高于主角闪避翻滚）
            if (TrySwipePushFurniture(pointerDownPos, releasePos, swipeDir))
            {
                return;
            }

            // 2. 第二优先级：未命中任何可推家具时，主角执行闪避翻滚
            if (Gear && Gear.UsesCharges ? Gear.DodgeAvailable : rollCooldownTimer <= 0f)
            {
                TriggerDodgeRoll(swipeDir);
                return;
            }
        }
        else
        {
            // 2. 判断是否为【轻触点击】(Tap)
            if (distance < clickMaxDistance && duration <= clickMaxDuration)
            {
                OnScreenTap(releasePos);
            }
        }
    }

    public bool IsInBottomDockArea(Vector2 screenPos)
    {
        return BuildDockUI.Instance != null && BuildDockUI.Instance.IsPointerOverDrawer(screenPos);
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
        if (UltimateLocked) return;
        if (cam == null || grid == null || groundTilemap == null) return;

        Vector3 worldPos = cam.ScreenToWorldPoint(screenPosition);
        worldPos.z = 0f;

        // 1. 优先检查是否点击到了敌人！
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

        // 2. 检查是否点击到了家具：若点击到了异界门扉，触发门扉互动
        FurnitureObject clickedFurn = FurnitureObject.GetNearestPlaced(worldPos, 1.8f);
        if (clickedFurn != null)
        {
            var door = clickedFurn.GetComponent<GatherDoorAuraBehavior>();
            if (door != null)
            {
                float dist = Vector2.Distance(transform.position, door.transform.position);
                if (dist <= door.interactionRadius + 0.8f)
                {
                    door.OnPlayerInteract(this);
                }
                else if (PlayerSessionData.CurrentMobileControlMode == PlayerSessionData.MobileControlMode.TapToMove)
                {
                    MoveToWorldPoint(door.transform.position - new Vector3(0f, 1.2f, 0f));
                }
                return;
            }
            return;
        }

        // 3. 点击普通地面：在点击寻路模式 (TapToMove) 下智能寻路前往指定地点！
        if (PlayerSessionData.CurrentMobileControlMode == PlayerSessionData.MobileControlMode.TapToMove)
        {
            MoveToWorldPoint(worldPos);
        }
    }

    /// <summary>
    /// 世界坐标寻路移动接口（仅在点击模式生效；摇杆模式下严格屏蔽以防屏幕任意点击打断摇杆走位）
    /// </summary>
    public void MoveToWorldPoint(Vector3 worldPos)
    {
        if (UltimateLocked) return;
        // 摇杆控制模式下彻底屏蔽触屏寻路移动，杜绝任何误触打断走位
        if (PlayerSessionData.CurrentMobileControlMode == PlayerSessionData.MobileControlMode.Joystick)
            return;

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
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.8f, "💣 投掷飞砸!", new Color(1f, 0.35f, 0.15f), 0.08f);
        }
    }

    private void MoveToCell(Vector3Int targetCell)
    {
        if (PlayerSessionData.CurrentMobileControlMode == PlayerSessionData.MobileControlMode.Joystick)
            return;

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
        if (Gear && (Gear.Guarding || Gear.Busy)) return;
        if (isRolling || !canMove || isAttackingAnim || isCharging || isSmashing || isGuarding || Unavailable) return;

        var pendingEnemy = EnemyController.FindPending();
        if (pendingEnemy != null && Vector2.Distance(transform.position, pendingEnemy.transform.position) <= attackRange * 1.5f)
            return;

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
        if (targetToAttack != null && !targetToAttack.IsEmerging &&
            targetToAttack.aiState != EnemyController.EnemyAIState.Pending &&
            targetToAttack.aiState != EnemyController.EnemyAIState.Executing && attackCooldownTimer <= 0f)
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
        if (UltimateLocked) return;
        if (Unavailable || attackCooldownTimer > 0f || isAttackingAnim || isCharging || isSmashing) return;

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
        if (UltimateLocked) return;
        if (!canMove || isRolling || isCharging || isSmashing || moveDir.sqrMagnitude < 0.01f) return;

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
        string comboClip = null;

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
                comboClip = clip;
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

        // gamejam 手感（TapCombatFeel）：挥空音效在出招时，伤害 / 光弧落在动画判定帧
        bool feel = TapCombatFeel.On && hero != null && comboClip != null;
        if (feel)
        {
            Sfx.Play("sfx_swing", 0.6f);
            float waited = 0f;
            while (hero != null && hero.Playing && hero.T < hero.Hit && waited < 0.6f)
            {
                if (isRolling || Unavailable || UltimateLocked) { isAttackingAnim = false; yield break; }   // 判定前被翻滚 / 受控打断：这一下不算
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (currentTarget != null && currentTarget.IsAlive)
            {
                attackDir = (currentTarget.transform.position - transform.position).normalized;
                if (attackDir.sqrMagnitude < 0.01f) attackDir = IsFacingLeft ? Vector3.left : Vector3.right;
            }
        }

        // 生成斩击光弧或重砸特效
        SpawnSlashEffect(attackDir, isExecution);

        // 攻击微突进动作（仅对静态 Sprite 生效，骨骼动画自带攻击动效）
        if (playerRenderer != null && visualRoot == null)
        {
            playerRenderer.transform.localPosition = attackDir * 0.15f;
        }

        if (currentTarget != null && currentTarget.IsAlive)
        {
            float hitDist = Vector2.Distance(transform.position, currentTarget.transform.position);
            if (hitDist <= attackRange * 1.35f)
            {
                // 造成伤害并削弱破防值
                float hpBefore=currentTarget.currentHp;
                var hitEnemy = currentTarget;
                DealDamage(currentTarget, dmg, guardBreak);
                if(currentTarget && currentTarget.currentHp<hpBefore) Gear?.NormalHit(currentTarget);
                if (feel && hitEnemy != null && hitEnemy.IsAlive && comboClip == "Stomp")
                    hitEnemy.Knockback(attackDir, TapCombatFeel.StompKnock * TapCombatFeel.WorldScale);   // 第三下踩踏推得更远

                // 命中敌人恢复 8 点精力
                currentStamina = Mathf.Min(maxStamina, currentStamina + 8f);
            }
        }

        if (feel && comboClip == "Stomp")
        {
            CameraRig.I?.Shake(0.2f, 0.25f);
            Sfx.Play("sfx_kick", 0.8f);
        }

        if (feel)
        {
            // 收招跟着动画走：动画放完（最多 0.3 s）才能接下一个动作
            float rest = 0f;
            while (hero != null && hero.Playing && rest < 0.3f) { rest += Time.unscaledDeltaTime; yield return null; }
            isAttackingAnim = false;
            yield break;
        }

        yield return new WaitForSeconds(0.12f);

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
        if (isRolling || (Gear && !Gear.DodgeAvailable)) yield break;
        Gear?.SpendDodge();

        // 翻滚打断当前移动和自动攻击，全力闪避
        StopMovement();

        Vector3 rollDir = new Vector3(swipeDir.x, swipeDir.y, 0f).normalized;

        isRolling = true;
        dodgeActive=true;
        dodgeStartedAt=Time.unscaledTime;
        LastDodgeWasPerfect=false;
        Emit(DodgeStarted);
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

            Vector3 stepMove = rollDir * (rollSpeed * (Gear ? Gear.DodgeMultiplier : 1f) * Time.deltaTime);
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
            float punchLen = hero.ShellPunch(punchTarget);
            Sfx.Play("sfx_arrow_charge", 0.7f);

            // 等待出拳命中时刻 (HeroActions.punchHitT = 0.42s)
            yield return new WaitForSeconds(0.42f);
            if (TapCombatFeel.On) GameJam.TimeManager.I?.DoHitstop(TapCombatFeel.PunchHitstop);   // gamejam：反击拳 0.12 s 顿帧
            CameraRig.I?.Shake(0.5f, 0.4f);
            ScreenFX.I?.Pulse();
            Sfx.Play("sfx_explosion");
            Vfx.Spawn("VFX_ArrowHit", punchTarget + Vector3.up * 0.4f, 0, 1.3f, true);
            Vfx.Spawn("VFX_ArrowBurst", punchTarget + Vector3.up * 0.6f, 0, 1.0f, true);

            var gearFirstHits = new System.Collections.Generic.List<EnemyController>();
            var enemies = EnemyController.AllEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e != null && e.IsAlive && Vector2.Distance(punchTarget, e.transform.position) <= 2.2f)
                {
                    gearFirstHits.Add(e);
                    DealDamage(e, CurrentAttackDamage * (1.25f + (GameJam.Gear.TapGearCombat.Lv("arm_spring")>0?1f:0f)), CurrentGuardBreakPower * 1.2f);
                    if (TapCombatFeel.On && e != null && e.IsAlive)
                        e.Knockback(e.transform.position - punchTarget + (e.transform.position - transform.position) * 0.01f, 0.8f * TapCombatFeel.WorldScale);   // gamejam：反击拳击退 0.8
                }
            }

            Gear?.CounterHit(gearFirstHits);

            // 等待机械臂出拳收回并钻出壳 (punchLen - 0.42s)
            float waitRemain = Mathf.Max(0.1f, punchLen - 0.42f);
            yield return new WaitForSeconds(waitRemain);

            // 兜底校验：确保退出缩壳
            if (hero.IsInShell)
            {
                hero.CancelShell();
            }
        }

        EndDodge();
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
    /// 【举起家具】：（已彻底取消拾起家具当武器功能）
    /// </summary>
    public void HoldFurniture(FurnitureObject furn)
    {
        // 彻底取消拾起家具当武器的功能
        return;
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
    /// 【举起附近家具】：（已彻底取消拾起家具当武器功能）
    /// </summary>
    public void PickUpNearbyFurniture()
    {
        // 彻底取消拾起家具当武器的功能
        return;
    }


    /// <summary>
    /// 供手机 UI 底座一键触发翻滚闪避（带无敌帧与音画特效）
    /// </summary>
    public bool TriggerDodgeRoll(Vector2? optionalDir = null)
    {
        if (UltimateLocked) return false;
        if ((Gear && Gear.UsesCharges ? !Gear.DodgeAvailable : rollCooldownTimer > 0f) || isRolling || Unavailable) return false;

        // 翻滚消耗 15 点精力
        if (!ConsumeStamina(rollStaminaCost))
        {
            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.8f, "战斗耐力不足!", Color.yellow, 0.08f);
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
        if (UltimateLocked) return;
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
        if (!IsAlive || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;
        if (Ultimate && Ultimate.TryAbsorb(null)) return;
        TakeDamageCore(damage, null, true, true);
    }

    public PlayerDamageResult TakeDamage(float damage, EnemyController attacker, bool canBlock)
    {
        if (!IsAlive || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return PlayerDamageResult.Ignored;
        if (Ultimate && Ultimate.TryAbsorb(attacker)) return PlayerDamageResult.Blocked;
        if (IsInvulnerable) { NotifyDodgeAvoided(); return PlayerDamageResult.Dodged; }
        if (Gear && Gear.FilterDamage(attacker, canBlock, ref damage)) return PlayerDamageResult.Blocked;
        bool blocked=isGuarding && canBlock;
        TakeDamageCore(damage, attacker, canBlock, false);
        return blocked ? PlayerDamageResult.Blocked : PlayerDamageResult.Damaged;
    }

    private void TakeDamageCore(float damage, EnemyController attacker, bool canBlock, bool legacy)
    {
        if (IsInvulnerable) { NotifyDodgeAvoided(); return; }
        if (legacy && Gear && Gear.FilterDamage(attacker, canBlock, ref damage)) return;
        CancelPlace();

        if (isGuarding && canBlock)
        {
            var parryEnemy = legacy ? (FindParryableEnemy() ?? NearestEnemy(attackRange + 0.8f)) : attacker;
            if (parryEnemy != null)
            {
                DoParry(parryEnemy);
                return;
            }

            // 普通格挡成功，吸收全部伤害
            // 有来源的弹反已在 DoParry 奖励并提前返回；这里只奖励无弹反目标的格挡。
            Ultimate?.AddCharge(5);
            Gear?.Block();
            isGuarding = false;
            CameraRig.I?.Shake(0.15f, 0.2f);
            Sfx.Play("sfx_parry", 0.8f, true);
            Vfx.Spawn("VFX_ParrySpark", transform.position + Vector3.up * 0.6f, 0, 1.0f, true);
            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.6f, "🛡️ 格挡抵消!", Color.cyan, 0.08f);
            }
            return;
        }

        currentHp -= damage;
        Gear?.Hurt();
        currentHp = Mathf.Max(0f, currentHp);

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.4f, $"-{damage:0}", new Color(1f, 0.25f, 0.25f));
        }

        if (currentHp <= 0f)
        {
            TriggerDefeat();
            return;
        }

        if (hero != null && !UltimateLocked)
        {
            hero.Play("Hurt");
            hero.Face?.TriggerPain();
        }

        if (hurtCoroutine != null)
        {
            StopCoroutine(hurtCoroutine);
        }
        hurtCoroutine = StartCoroutine(PlayerHurtRoutine());
    }

    EnemyController executionTarget;
    Coroutine executionRoutine;
    public event System.Action DodgeStarted, DodgeEnded, PerfectDodge;
    public event System.Action<EnemyController,float> DamageDealt;
    public int PerfectDodgeCount { get; private set; }
    public bool LastDodgeWasPerfect { get; private set; }
    bool dodgeActive;
    float dodgeStartedAt;
    static void Emit(System.Action callbacks)
    {
        if (callbacks == null) return;
        foreach (System.Action listener in callbacks.GetInvocationList())
            try { listener(); } catch (System.Exception ex) { Debug.LogException(ex); }
    }
    void EndDodge() { if (!dodgeActive) return; dodgeActive=false; Emit(DodgeEnded); }
    public void NotifyDodgeAvoided()
    {
        if (!dodgeActive || !isRolling || LastDodgeWasPerfect || Time.unscaledTime-dodgeStartedAt > (Gear ? Gear.PerfectWindow : .25f)) return;
        LastDodgeWasPerfect=true; PerfectDodgeCount++; Emit(PerfectDodge);
    }
    public void DealDamage(EnemyController enemy, float damage, float guardDamage)
    {
        if (enemy == null) return;
        float before=enemy.currentHp;
        enemy.TakeDamage(damage, guardDamage, transform.position);
        NotifyDamageDealt(enemy,before);
    }
    void NotifyDamageDealt(EnemyController enemy, float before)
    {
        float actual=Mathf.Max(0,before-enemy.currentHp);
        if (actual <= 0 || DamageDealt == null) return;
        foreach (System.Action<EnemyController,float> listener in DamageDealt.GetInvocationList())
            try { listener(enemy,actual); } catch (System.Exception ex) { Debug.LogException(ex); }
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

    public void TriggerDefeat()
    {
        Ultimate?.Cancel(true);
        if (isDefeatedRoutineRunning) return;
        StartCoroutine(DefeatAndReviveRoutine());
    }

    private IEnumerator DefeatAndReviveRoutine()
    {
        isDefeatedRoutineRunning = true;
        StopMovement();
        CancelPlace();
        if (isGrabbed) { isGrabbed = false; grabberEnemy = null; }
        if (heldFurniture != null) { DropFurniture(); }

        if (hero != null)
        {
            hero.EndStruggle();
            hero.CancelShell();
            hero.Play("Thrown");
            if (hero.Face != null)
            {
                hero.Face.wrapped = false;
                hero.Face.SetBase(HeroFace.Expr.Pain);
            }
        }

        if (DamageTextManager.Instance != null)
        {
            EnemyController.NotifyPlayerDefeated();
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.8f, "💀 倒地力竭... 怪物转火围攻家具！", Color.red, 0.16f);
        }

        // 死亡惩罚延时：5 秒复苏倒计时，期间怪物持续转火围攻家具
        for (int sec = 5; sec >= 1; sec--)
        {
            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 1.1f, $"⏳ 复活倒计时: {sec}s", new Color(1f, 0.6f, 0.2f), 0.12f);
            }
            EnemyController.RedirectAllToFurniture();
            yield return new WaitForSeconds(1.0f);
        }

        currentHp = maxHp;
        currentStamina = maxStamina;
        isThrown = false;
        EndDodge();
        isRolling = false;
        isDefeatedRoutineRunning = false;
        EnemyController.NotifyPlayerRevived();

        if (hero != null)
        {
            hero.Play("Emerge");
            if (hero.Face != null) hero.Face.TriggerHappy();
        }

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.9f, "✨ 绝境复苏! (REVIVED)", new Color(0.2f, 1f, 0.4f), 0.15f);
        }

        StartCoroutine(TemporaryInvulnerableRoutine(1.8f));
    }

    private IEnumerator TemporaryInvulnerableRoutine(float duration)
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(duration);
        isInvulnerable = false;
    }

    private void CreatePlayerHealthBar()
    {
        if (playerHpBarRoot != null) return;
        playerHpBarRoot = new GameObject("Player_HPBar");
        playerHpBarRoot.transform.SetParent(transform, false);
        playerHpBarRoot.transform.localPosition = new Vector3(0f, 1.4f, 0f);

        var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");

        // HP is displayed by the screen-space HUD; keep the shared stamina root only.
        var staBg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        staBg.name = "Stamina_BG";
        staBg.transform.SetParent(playerHpBarRoot.transform, false);
        staBg.transform.localPosition = new Vector3(0f, -0.07f, 0f);
        staBg.transform.localScale = new Vector3(0.75f, 0.05f, 1f);
        Destroy(staBg.GetComponent<Collider>());
        var staBgMr = staBg.GetComponent<MeshRenderer>();
        if (unlitShader != null) staBgMr.material = new Material(unlitShader);
        staBgMr.material.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);
        staBgMr.sortingOrder = 30;

        var staFill = GameObject.CreatePrimitive(PrimitiveType.Quad);
        staFill.name = "Stamina_Fill";
        staFill.transform.SetParent(playerHpBarRoot.transform, false);
        staFill.transform.localPosition = new Vector3(0f, -0.07f, -0.01f);
        staFill.transform.localScale = new Vector3(0.71f, 0.038f, 1f);
        Destroy(staFill.GetComponent<Collider>());
        var staFillMr = staFill.GetComponent<MeshRenderer>();
        if (unlitShader != null) staFillMr.material = new Material(unlitShader);
        staFillMr.material.color = new Color(0.18f, 0.8f, 0.98f, 1f);
        staFillMr.sortingOrder = 31;
        playerStaminaBarFill = staFill.transform;
    }

    private void UpdatePlayerHealthBar()
    {
        if (playerHpBarRoot == null) return;
        var camToFace = cam ?? Camera.main;
        if (camToFace != null)
        {
            playerHpBarRoot.transform.rotation = camToFace.transform.rotation;
        }

        float staRatio = Mathf.Clamp01(currentStamina / Mathf.Max(maxStamina, 1f));

        if (playerStaminaBarFill != null)
        {
            playerStaminaBarFill.localScale = new Vector3(0.71f * staRatio, 0.038f, 1f);
            playerStaminaBarFill.localPosition = new Vector3(-0.71f * 0.5f * (1f - staRatio), -0.07f, -0.01f);
        }
    }

    void LateUpdate()
    {
        UpdatePlayerHealthBar();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
