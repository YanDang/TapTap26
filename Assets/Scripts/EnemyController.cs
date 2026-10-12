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
        BreakBarrier  // 攻击挡路防御设施
    }

    [Header("Attributes (怪物基础属性)")]
    public float maxHp = 100f;
    public float currentHp = 100f;
    public float moveSpeed = 1.35f;       // 移速适度调缓，节奏从容
    public float detectRange = 5.5f;      // 索敌警戒范围
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
    [Range(0f, 1f)] public float grabChance = 0.35f; // 抓取发起概率
    public float grabWindup = 1.4f;               // 抓取前摇蓄力时长 (红光闪烁)
    public float grabRange = 2.0f;                // 抓取范围判定
    public float grabHold = 2.6f;                 // 缠住时长
    public float grabTick = 0.85f;                // 缠住挤压扣血周期
    public float grabDamage = 10f;                // 每次挤压伤害
    public bool forceGrabNext = false;            // 强制下一次攻击为抓取 (测试用)

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

    public bool ParryWindowOpen => aiState == EnemyAIState.Attack && stateTimer >= windowStart && stateTimer <= windowEnd;

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
    public float squidTargetHeight = 2.4f;
    public float squidHover = 0.25f;
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

    [Header("Biomechanical Status Effects (生体状态效果)")]
    [HideInInspector] public float slowMultiplier = 1f;
    private float slowTimer = 0f;
    private float paralysisTimer = 0f;
    [HideInInspector] public bool isPacified = false;
    private float pacifiedTimer = 0f;
    [HideInInspector] public EnemyController pacifiedTargetEnemy = null;
    [HideInInspector] public EnemyController currentTargetEnemy = null;
    private float pacifiedHeartbeatTimer = 0f;

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
        isStaggered = false;
        currentHp = maxHp;
        currentGuardBreak = maxGuardBreak;

        spawnPosition = transform.position;

        if (visualRoot == null)
        {
            var vr = transform.Find("VisualRoot");
            if (vr != null) visualRoot = vr;
        }

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
            else monsterRenderer = GetComponentInChildren<SpriteRenderer>();
        }

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
        else if (visualRenderers != null && visualRenderers.Length > 0)
        {
            originalColor = visualRenderers[0].color;
            if (originalColor.a < 0.1f) originalColor = Color.white;
            originalColor.a = 1f;
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
        if (billboardMat != null)
        {
            for (int i = 0; i < srs.Length; i++)
            {
                srs[i].sharedMaterial = billboardMat;
            }
        }
        flashRenderers = srs;
        RegisterExtraRenderers(srs);
    }

    void Start()
    {
        isDead = false;
        isStaggered = false;
        aiState = EnemyAIState.Patrol;

        if (monsterRenderer != null)
        {
            monsterRenderer.color = Color.white;
            monsterRenderer.transform.localScale = originalScale;
            monsterRenderer.transform.localPosition = Vector3.zero;
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
    public void Knockback(Vector3 dir, float distance)
    {
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
        if (isAIPaused || PlayerSessionData.isCraftingOpen)
        {
            SetMoveAnimation(false);
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
                UpdatePacifiedAI();
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
        // 优先检查是否有反戈怪兽正在攻击我（触发恶魔间决斗）
        if (currentTargetEnemy != null && currentTargetEnemy.IsAlive)
        {
            float distToEnemy = Vector2.Distance(transform.position, currentTargetEnemy.transform.position);
            if (distToEnemy <= attackRange * 1.15f && attackCooldownTimer <= 0f)
            {
                SetMoveAnimation(false);
                attackCooldownTimer = attackCooldown;
                currentTargetEnemy.TakeDamage(attackDamage, 30f, transform.position);
                if (enemyAnimator != null) enemyAnimator.SetTrigger("Attack");
                SpawnClawEffect();
                if (DamageTextManager.Instance != null)
                {
                    DamageTextManager.Instance.ShowText(currentTargetEnemy.transform.position + Vector3.up * 0.7f, $"💥 恶魔互殴 -{attackDamage:0}!", Color.red, 0.10f);
                }
                return;
            }

            Vector3 enemyDir = (currentTargetEnemy.transform.position - transform.position).normalized;
            transform.position = Vector3.MoveTowards(transform.position, currentTargetEnemy.transform.position, moveSpeed * slowMultiplier * Time.deltaTime);
            SetVisualFacing(enemyDir.x < -0.05f);
            SetMoveAnimation(true);
            if (monsterRenderer != null && enemyAnimator == null)
            {
                float bob = Mathf.Abs(Mathf.Sin(Time.time * 8f)) * 0.03f;
                monsterRenderer.transform.localPosition = new Vector3(0f, bob, 0f);
            }
            return;
        }
        else
        {
            currentTargetEnemy = null;
        }

        // 游击战术支持：玩家拉开距离超过 loseTargetRange，丢失目标进入警觉搜寻态
        if (distToPlayer > loseTargetRange)
        {
            SetMoveAnimation(false);
            aiState = EnemyAIState.Alert;
            alertTimer = 2.5f;
            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.6f, "?", Color.yellow, 0.11f);
            }
            return;
        }

        // 玩家被缠 / 被丢 / 起身中：其他怪先等着，不发起攻击
        if (targetPlayer != null && targetPlayer.Unavailable)
        {
            SetMoveAnimation(false);
            return;
        }

        // 进入近战范围且冷却就绪，发起普通挥击（黄光）或狂暴抓取（红光）
        if (distToPlayer <= attackRange && attackCooldownTimer <= 0f)
        {
            SetMoveAnimation(false);
            if (squid != null && Grabber == null && !targetPlayer.Unavailable && (forceGrabNext || Random.value < grabChance))
            {
                StartGrab();
            }
            else
            {
                StartNormalAttack();
            }
            return;
        }

        // 智能绕障追击
        ChasePlayerOrBarrier();
    }

    private void UpdateBreakBarrierState(float distToPlayer)
    {
        SetMoveAnimation(false);
        var blockingFurn = FurnitureObject.GetNearestPlaced(transform.position, 1.4f);
        if (blockingFurn != null && attackCooldownTimer <= 0f)
        {
            attackCooldownTimer = attackCooldown;
            blockingFurn.TakeHit(attackDamage, this);
            if (enemyAnimator != null) enemyAnimator.SetTrigger("Attack");
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

    private void StartNormalAttack()
    {
        aiState = EnemyAIState.Attack;
        stateTimer = 0f;
        impactDone = false;
        SetMoveAnimation(false);

        if (targetPlayer != null)
        {
            Vector3 dir = targetPlayer.transform.position - transform.position;
            SetVisualFacing(dir.x < -0.05f);
        }

        int side = (targetPlayer != null && targetPlayer.transform.position.x > transform.position.x) ? 1 : -1;
        if (squid != null)
        {
            squid.Attack(side, -1, normalImpact);
        }
        if (enemyAnimator != null && enemyAnimator.enabled && enemyAnimator.runtimeAnimatorController != null)
        {
            enemyAnimator.SetTrigger("Attack");
        }
        Sfx.Play("sfx_enemy_windup", 0.85f);

        if (dangerZoneObject != null)
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

        // 黄光预警（0.05 ~ 0.55s 内闪烁）
        if (stateTimer <= windowEnd + 0.05f)
        {
            float glow = 0.55f + 0.25f * Mathf.Sin(Time.time * 30f);
            ApplyFlash(glow, AttackGlow);
        }

        // 危险判定圈脉冲
        if (dangerZoneObject != null)
        {
            float pulse = 0.35f + Mathf.Sin(stateTimer * 18f) * 0.15f;
            var mr = dangerZoneObject.GetComponent<MeshRenderer>();
            if (mr != null) mr.material.color = new Color(1f, 0.8f, 0.1f, pulse);
        }

        // 格挡窗口期如果玩家正在防御状态，立刻触发弹刀！
        if (stateTimer >= windowStart && stateTimer <= windowEnd)
        {
            if (targetPlayer != null && targetPlayer.IsGuarding)
            {
                ApplyFlash(0f, Color.white);
                targetPlayer.DoParry(this);
                return;
            }
        }

        // 命中时点
        if (!impactDone && stateTimer >= normalImpact)
        {
            impactDone = true;
            ApplyFlash(0f, Color.white);
            if (dangerZoneObject != null) dangerZoneObject.SetActive(false);
            SpawnClawEffect();

            if (targetPlayer != null && !isStaggered && targetPlayer.IsAlive)
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
                        if (DamageTextManager.Instance != null)
                        {
                            DamageTextManager.Instance.ShowText(targetPlayer.transform.position + Vector3.up * 0.4f, "DODGE!", new Color(0.2f, 1f, 0.8f), 0.11f);
                        }
                    }
                    else
                    {
                        targetPlayer.TakeDamage(attackDamage);
                        Sfx.Play("sfx_hit");
                        CameraRig.I?.Shake(0.12f, 0.15f);
                    }
                }
            }
        }

        // 攻击后摇结束
        if (stateTimer >= normalImpact + 0.35f)
        {
            ApplyFlash(0f, Color.white);
            if (dangerZoneObject != null) dangerZoneObject.SetActive(false);
            aiState = EnemyAIState.Recover;
            stateTimer = 0.5f;
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
        aiState = EnemyAIState.Executing;
        CancelGrab();
        if (dangerZoneObject != null) dangerZoneObject.SetActive(false);
        if (squid != null) squid.Interrupt();
        Flash(Color.white, 0.3f);
    }

    public void KillByExecution(Vector3 dir)
    {
        currentHp = 0f;
        Flash(Color.white, 0.3f);
        ApplyKnockback(dir, 0.8f, 0.25f);
        Die();
    }

    public void YieldToGrab()
    {
        if (aiState == EnemyAIState.Attack || aiState == EnemyAIState.GrabWindup)
        {
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
        if (isDead || aiState == EnemyAIState.Executing) return;

        Flash(Color.white, 0.12f);
        Knockback((transform.position - attackerPos).normalized, 0.18f);

        // 如果在被安抚中挨打，醒过来
        if (isPacified)
        {
            isPacified = false;
            pacifiedTargetEnemy = null;
        }

        // 如果在处决就绪状态下，保持 Pending 不变，方便处决
        if (aiState != EnemyAIState.Pending && aiState != EnemyAIState.Grabbing)
        {
            aiState = EnemyAIState.Chase;
        }

        float finalDmg = damage;
        bool isCrit = isStaggered || aiState == EnemyAIState.Pending;
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
        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(true);
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
        SetVisualColor(originalColor);
        SetVisualFacing(false);
        SetMoveAnimation(false);

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
