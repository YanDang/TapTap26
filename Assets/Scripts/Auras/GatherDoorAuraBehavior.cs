using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using BiomechanicalCrafting;
using GameJam;

/// <summary>
/// 7. 异界采集之门 (furn_gather_door) 战术光环与空间传送逻辑：
/// - 机制与生命周期：
///   1. 部署放置 (Closed)：
///      * 处于关闭待开启状态，头顶浮动黄色交互提示标签 "[ 🚪 点击或按 F 开启门扉 ]"；
///   2. 打开门扉 (Fighting)：
///      * 开门后先预警，再从附近可通行的安全位置随机涌现 1~3 只守卫；
///      * 守卫完成出场保护后追击，且 autoRespawn 设为 false (消灭后不重复复活)；
///      * 门扉状态转为交战锁定，未消灭前无法进入；
///   3. 敌群肃清 (Cleared)：
///      * 当所有涌现的怪物均被击败后，空间裂隙震荡平息，通道稳定绽放青碧色传送光晕；
///      * 头顶提示切换为 "[ ✨ 采集通道已稳定! 点击进入温室 ]"；
///   4. 穿越传送 (Enter Collect)：
///      * 再次与门扉交互，即可无缝穿梭进入药剂工艺式采集温室 (Collect 场景)！
/// </summary>
public class GatherDoorAuraBehavior : BiomechanicalAuraBase
{
    public enum DoorState
    {
        Closed,    // 初始关闭状态，等待开启
        Fighting,  // 怪物已涌现，交战中
        Cleared,
        Spent,
        Summoning
    }

    [Header("Door Status")]
    public DoorState state = DoorState.Closed;

    public float interactionRadius = 2.2f;
    public float summonWarningDuration = 0.8f;
    public float emergenceDuration = 0.7f;
    public float minimumSpawnDistance = 0.85f;
    public float maximumDoorSpawnRadius = 2.2f;
    private float summonRemaining;
    private PlayerController summoningPlayer;

    private string doorId;
    private int trialDay = -1;
    public string DoorId => doorId ?? (doorId = gameObject.scene.name + ":" +
        transform.position.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" +
        transform.position.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" +
        transform.position.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture));

    private void RefreshDailyState()
    {
        bool used = PlayerSessionData.IsDoorUsedToday(DoorId);
        int currentDay = PlayerSessionData.greenhouse.day;
        bool dayChanged = trialDay != currentDay;
        if (dayChanged)
        {
            trialDay = currentDay;
            spawnedEnemies.Clear();
            totalTrialSpawned = 0;
            state = GameRunProgress.IsTrialCleared(DoorId) ? DoorState.Cleared : DoorState.Closed;
        }
        if (used && state != DoorState.Spent)
        {
            state = DoorState.Spent;
            UpdatePromptText();
        }
        else if (!used && state == DoorState.Spent)
        {
            state = DoorState.Closed;
            UpdatePromptText();
        }
        else if (dayChanged)
        {
            UpdatePromptText();
        }
    }

    [Header("Spawned Enemies")]
    public readonly List<EnemyController> spawnedEnemies = new List<EnemyController>();
    public int totalTrialSpawned = 0;

    public static readonly List<GatherDoorAuraBehavior> AllDoors = new List<GatherDoorAuraBehavior>();

    private void OnEnable()
    {
        if (!AllDoors.Contains(this)) AllDoors.Add(this);
    }

    private void OnDestroy()
    {
        AllDoors.Remove(this);
    }

    private Vector2 DoorCenter => (Vector2)transform.position + new Vector2(0f, 0.5f);

    /// <summary>
    /// 判断玩家是否在当前门的一格身位内（约 1.6 米范围内），用于控制头顶文字显示
    /// </summary>
    public bool IsPlayerNear(PlayerController player, out float dist)
    {
        dist = float.MaxValue;
        if (player == null || !player.IsAlive) return false;

        Vector2 playerCenter = (Vector2)player.transform.position + new Vector2(0f, 0.3f);
        dist = Vector2.Distance(DoorCenter, playerCenter);

        return dist <= 1.6f;
    }

    // 头顶 3D Billboard 状态与操作提示文本
    private GameObject promptGo;
    private TextMesh promptTextMesh;
    private Vector3 originalVisualScale = Vector3.one;

    public static readonly Color ColorClosed = new Color(1f, 0.9f, 0.45f);
    public static readonly Color ColorFighting = new Color(1f, 0.4f, 0.3f);
    public static readonly Color ColorCleared = new Color(0.25f, 1f, 0.75f);

    protected override void Awake()
    {
        base.Awake();
        CreateRangeRing(2.8f, new Color(ColorClosed.r, ColorClosed.g, ColorClosed.b, 0.35f), "GatherDoor_Ring");

        if (parentFurniture != null)
        {
            parentFurniture.isIndestructible = true;
            parentFurniture.isImmovable = true;
            if (parentFurniture.furnitureRenderer != null)
            {
                originalVisualScale = parentFurniture.furnitureRenderer.transform.localScale;
                parentFurniture.furnitureRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
                parentFurniture.furnitureRenderer.sortingOrder = 10;
            }
        }

        // 调整物理与点击感应碰撞体覆盖范围，完美包覆整座裂隙门扉
        if (parentFurniture != null && parentFurniture.furnitureCollider is CircleCollider2D circleCol)
        {
            circleCol.radius = 0.85f;
            circleCol.offset = new Vector2(0f, 0.6f);
        }

        CreateOverheadPrompt();
        UpdatePromptText();
    }

    private void CreateOverheadPrompt()
    {
        if (promptGo != null) return;

        promptGo = new GameObject("OverheadDoorPrompt");
        promptGo.transform.SetParent(transform, false);
        promptGo.transform.localPosition = new Vector3(0f, 1.45f, 0f);
        promptGo.transform.localScale = Vector3.one * 0.055f;

        promptTextMesh = promptGo.AddComponent<TextMesh>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        promptTextMesh.font = font;
        promptTextMesh.fontSize = 42;
        promptTextMesh.fontStyle = FontStyle.Bold;
        promptTextMesh.alignment = TextAlignment.Center;
        promptTextMesh.anchor = TextAnchor.MiddleCenter;

        var mr = promptGo.GetComponent<MeshRenderer>();
        if (mr != null && font != null && font.material != null)
        {
            mr.material = font.material;
            mr.sortingOrder = 220; // 悬浮在角色与建筑之上
        }

        promptGo.SetActive(false); // 默认隐藏，靠近时才显现
    }

    private void UpdatePromptText()
    {
        if (promptTextMesh == null) return;

        switch (state)
        {
            case DoorState.Summoning:
                promptTextMesh.text = "裂隙正在开启 · 守卫即将出现，请准备战斗";
                promptTextMesh.color = ColorFighting;
                break;

            case DoorState.Spent:
                promptTextMesh.text = "门已关闭 · 今日已使用，次日需重新挑战";
                promptTextMesh.color = Color.gray;
                if (rangeRingRenderer != null) rangeRingRenderer.color = Color.gray;
                break;

            case DoorState.Closed:
                int estMonsters = GetTrialSpawnCountForDoor(PlayerSessionData.greenhouse.day);
                int minYield = 3;
                int maxYield = 5;
                string extraTag = "";
                if (productData != null)
                {
                    var s = CraftedProductStats.Calculate(productData);
                    minYield = s.minGatherCount;
                    maxYield = s.maxGatherCount;
                    if (s.extraMonsters > 0)
                    {
                        extraTag = $"[过载+{s.extraMonsters}怪] ";
                    }
                }
                promptTextMesh.text = $"🚪 点击开门 · 守卫:{estMonsters}只 · 萌发:{minYield}~{maxYield} · {extraTag}耗2精力";
                promptTextMesh.color = ColorClosed;
                if (rangeRingRenderer != null)
                {
                    rangeRingRenderer.color = new Color(ColorClosed.r, ColorClosed.g, ColorClosed.b, 0.35f);
                }
                break;

            case DoorState.Fighting:
                int alive = GetAliveMonsterCount();
                promptTextMesh.text = $"⚔️ 裂隙躁动! 击溃怪物: {alive} 只剩余";
                promptTextMesh.color = ColorFighting;
                if (rangeRingRenderer != null)
                {
                    rangeRingRenderer.color = new Color(ColorFighting.r, ColorFighting.g, ColorFighting.b, 0.45f);
                }
                break;

            case DoorState.Cleared:
                promptTextMesh.text = "✨ 采集通道已稳定! 点击进入温室";
                promptTextMesh.color = ColorCleared;
                if (rangeRingRenderer != null)
                {
                    rangeRingRenderer.color = new Color(ColorCleared.r, ColorCleared.g, ColorCleared.b, 0.55f);
                }
                break;
        }
    }

    void Update()
    {
        if (!PlayerSessionData.IsCombatInputBlocked) RefreshDailyState();
        bool isPlaced = (parentFurniture != null && parentFurniture.currentState == FurnitureObject.FurnitureState.Placed);

        if (!isPlaced)
        {
            if (rangeRing != null && rangeRing.activeSelf) rangeRing.SetActive(false);
            if (promptGo != null && promptGo.activeSelf) promptGo.SetActive(false);
            return;
        }

        if (rangeRing != null && !rangeRing.activeSelf) rangeRing.SetActive(true);

        // 仅当玩家靠近门（距离约一格以内）时才显示文字；且两门相邻时仅最近的门显示，避免重叠挤占视野
        var p = PlayerController.Instance ?? FindObjectOfType<PlayerController>();
        bool isFocusedDoor = false;
        if (p != null && p.IsAlive)
        {
            if (IsPlayerNear(p, out float myDist))
            {
                bool isClosest = true;
                for (int i = 0; i < AllDoors.Count; i++)
                {
                    var other = AllDoors[i];
                    if (other == null || other == this || !other.gameObject.activeInHierarchy) continue;
                    if (other.parentFurniture == null || other.parentFurniture.currentState != FurnitureObject.FurnitureState.Placed) continue;

                    if (other.IsPlayerNear(p, out float otherDist))
                    {
                        if (otherDist < myDist || (Mathf.Approximately(otherDist, myDist) && other.GetInstanceID() < GetInstanceID()))
                        {
                            isClosest = false;
                            break;
                        }
                    }
                }
                isFocusedDoor = isClosest;
            }
        }

        if (promptGo != null)
        {
            if (isFocusedDoor)
            {
                if (!promptGo.activeSelf)
                {
                    UpdatePromptText();
                    promptGo.SetActive(true);
                }
            }
            else
            {
                if (promptGo.activeSelf)
                {
                    promptGo.SetActive(false);
                }
            }
        }

        if (state == DoorState.Summoning && !PlayerSessionData.IsCombatInputBlocked && !IsCombatPaused)
        {
            if (summoningPlayer == null || !summoningPlayer.IsAlive)
            {
                state = DoorState.Closed;
                GameRunProgress.RefundDoor(DoorId);
                UpdatePromptText();
            }
            else
            {
                summonRemaining -= Time.deltaTime;
                if (summonRemaining <= 0f) SpawnTrialEnemies(summoningPlayer);
            }
        }

        float t = Time.time;

        // 柔和生体舒缩微动效
        if (parentFurniture != null && parentFurniture.furnitureRenderer != null)
        {
            float speed = (state == DoorState.Fighting) ? 3.0f : 1.4f;
            float amp = (state == DoorState.Fighting) ? 0.05f : 0.025f;
            float breath = Mathf.Sin(t * speed) * amp;
            parentFurniture.furnitureRenderer.transform.localScale = new Vector3(
                originalVisualScale.x * (1f - breath * 0.3f),
                originalVisualScale.y * (1f + breath),
                originalVisualScale.z
            );
        }

        // 光环呼吸微脉冲
        if (rangeRingRenderer != null)
        {
            Color curCol = state == DoorState.Spent ? Color.gray : (state == DoorState.Closed ? ColorClosed : ((state == DoorState.Fighting || state == DoorState.Summoning) ? ColorFighting : ColorCleared));
            float alpha = 0.25f + 0.15f * Mathf.Sin(t * (state == DoorState.Fighting ? 4f : 2f));
            rangeRingRenderer.color = new Color(curCol.r, curCol.g, curCol.b, alpha);
        }

        // 状态更新：交战状态下检测所有涌现的怪物是否已全部被消灭
        if (state == DoorState.Fighting)
        {
            if (totalTrialSpawned > 0)
            {
                int aliveCount = GetAliveMonsterCount();
                if (aliveCount == 0)
                {
                    OnAllMonstersDefeated();
                }
                else
                {
                    UpdatePromptText();
                }
            }
        }

        // 键盘快捷按键检测：当主角对准并贴近当前门扉时，按下 F 键直接交互
        if (p != null && isFocusedDoor)
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                OnPlayerInteract(p);
            }
        }
    }

    void LateUpdate()
    {
        // 确保头顶提示朝向摄像机
        if (promptGo != null && promptGo.activeSelf && Camera.main != null)
        {
            promptGo.transform.rotation = Camera.main.transform.rotation;
        }
    }

    public int GetAliveMonsterCount()
    {
        int alive = 0;
        for (int i = 0; i < spawnedEnemies.Count; i++)
        {
            if (spawnedEnemies[i] != null && spawnedEnemies[i].IsAlive)
            {
                alive++;
            }
        }
        return alive;
    }

    /// <summary>
    /// 玩家与门扉的核心交互响应（点击或靠近按 F 触发）
    /// </summary>
    public void OnPlayerInteract(PlayerController player)
    {
        if (PlayerSessionData.IsCombatInputBlocked) return;
        RefreshDailyState();
        if (state == DoorState.Spent) return;
        if (player == null) player = PlayerController.Instance ?? FindObjectOfType<PlayerController>();
        if (player == null || !player.IsAlive || IsCombatPaused) return;
        player.ConsumeDoorInteraction();

        switch (state)
        {
            case DoorState.Closed:
                OpenDoor(player);
                break;

            case DoorState.Fighting:
                int remaining = GetAliveMonsterCount();
                if (remaining == 0)
                {
                    OnAllMonstersDefeated();
                    EnterGatheringScene(player);
                    return;
                }
                if (DamageTextManager.Instance != null)
                {
                    DamageTextManager.Instance.ShowText(
                        transform.position + Vector3.up * 1.0f,
                        $"⚠️ 裂隙震荡中! 尚有 {remaining} 只怪物未消灭!",
                        new Color(1f, 0.85f, 0.25f),
                        0.13f
                    );
                }
                break;

            case DoorState.Cleared:
                EnterGatheringScene(player);
                break;
        }
    }

    /// <summary>
    /// 打开门扉：从该位置随机涌现 1~3 只怪物！
    /// </summary>
    private void OnDisable()
    {
        AllDoors.Remove(this);
        if (Application.isPlaying && GameRunProgress.WorldReady && state == DoorState.Summoning)
            GameRunProgress.RefundDoor(DoorId);
    }

    private void OpenDoor(PlayerController player)
    {
        if (state != DoorState.Closed) return;
        if (!GameRunProgress.TryReserveDoor(DoorId, out var reason))
        {
            if (DamageTextManager.Instance != null)
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up, reason, Color.yellow, 0.13f);
            return;
        }
        spawnedEnemies.Clear();
        totalTrialSpawned = 0;
        summoningPlayer = player;
        summonRemaining = Mathf.Max(0.1f, summonWarningDuration);
        state = DoorState.Summoning;
        Sfx.Play("sfx_table_land", 1f, true);
        UpdatePromptText();
    }

    private List<Vector3> FindSafeSpawnPositions(PlayerController player, int count)
    {
        var positions = new List<Vector3>();
        var pathfinder = IsometricPathfinder.Instance;
        if (pathfinder == null || pathfinder.groundTilemap == null) return positions;
        var origin = pathfinder.WorldToCell(transform.position);
        var candidates = new List<Vector3>();
        float safeDistance = Mathf.Max(0.85f, minimumSpawnDistance);
        for (int offsetX = -12; offsetX <= 12; offsetX++)
        {
            for (int offsetY = -12; offsetY <= 12; offsetY++)
            {
                var cell = origin + new Vector3Int(offsetX, offsetY, 0);
                if (!pathfinder.IsWalkable(cell)) continue;
                var position = pathfinder.CellToWorld(cell);
                position.z = transform.position.z;
                float doorDistance = Vector2.Distance(position, transform.position);
                if (doorDistance < 0.65f || doorDistance > maximumDoorSpawnRadius ||
                    Vector2.Distance(position, player.transform.position) < safeDistance) continue;
                bool occupied = false;
                foreach (var enemy in EnemyController.AllEnemies)
                {
                    if (enemy != null && enemy.IsAlive && Vector2.Distance(position, enemy.transform.position) < 1.2f)
                    {
                        occupied = true;
                        break;
                    }
                }
                if (!occupied) candidates.Add(position);
            }
        }
        candidates.Sort((first, second) => Vector2.Distance(first, transform.position).CompareTo(Vector2.Distance(second, transform.position)));
        while (candidates.Count > 0 && positions.Count < count)
        {
            var position = candidates[0];
            positions.Add(position);
            candidates.RemoveAll(candidate => Vector2.Distance(candidate, position) < 1.05f);
        }
        return positions;
    }

    public static int GetBaseTrialSpawnCount(int day)
    {
        switch (day)
        {
            case 1: return Random.Range(1, 3); // 1~2 只 (新手熟悉环境)
            case 2: return 2;                  // 2 只
            case 3: return Random.Range(2, 4); // 2~3 只
            case 4: return 3;                  // 3 只
            case 5: return Random.Range(3, 5); // 3~4 只
            case 6: return 4;                  // 4 只
            case 7: return Random.Range(4, 6); // 4~5 只 (七日终局决战)
            default:
                int count = 2 + Mathf.FloorToInt(day * 0.5f);
                return Mathf.Clamp(count, 1, 6);
        }
    }

    public int GetTrialSpawnCountForDoor(int day)
    {
        int count = GetBaseTrialSpawnCount(day);
        if (productData != null)
        {
            var stats = CraftedProductStats.Calculate(productData);
            count += stats.extraMonsters; // 投入材料越多，出现的守卫越多！
        }
        return count;
    }

    public static int GetTrialSpawnCount(int day) => GetBaseTrialSpawnCount(day);

    private void SpawnTrialEnemies(PlayerController player)
    {
        int day = PlayerSessionData.greenhouse.day;
        int spawnCount = GetTrialSpawnCountForDoor(day);
        var positions = FindSafeSpawnPositions(player, spawnCount);
        if (positions.Count < spawnCount)
        {
            GameRunProgress.RefundDoor(DoorId);
            state = DoorState.Closed;
            UpdatePromptText();
            if (DamageTextManager.Instance != null)
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up, "空间不足，请让开门周围区域后重试", Color.yellow, 0.13f);
            return;
        }

        // 精英生成概率判断 (随天数递增)
        int eliteCount = 0;
        if (day >= 6) eliteCount = (day >= 7 && Random.value < 0.5f) ? 2 : 1;
        else if (day >= 4) eliteCount = Random.value < 0.65f ? 1 : 0;
        else if (day >= 3) eliteCount = Random.value < 0.35f ? 1 : 0;

        for (int i = 0; i < positions.Count; i++)
        {
            var position = positions[i];
            int index = totalTrialSpawned;
            bool isElite = i < eliteCount;

            // 怪物类型配比：第 1 天乌贼为主，第 2 天起扳机鱼逐步增多
            bool spawnTriggerfish = day > 1 && (index % 2 == 0 || (day >= 4 && Random.value < 0.5f));
            string typeName = spawnTriggerfish ? "Portal_Triggerfish" : "Portal_Squid";
            string prefix = isElite ? "【异化】" : "";
            string displayName = $"{prefix}{typeName}_{index + 1}";

            var enemy = spawnTriggerfish
                ? EnemyController.CreateTriggerfishInstance(position, displayName)
                : EnemyController.CreateEnemyInstance(position, displayName);

            if (enemy == null) continue;
            enemy.spawnPosition = position;
            enemy.autoRespawn = false;

            // 应用天数难度强化与精英异化
            enemy.ApplyDayDifficulty(day, isElite);

            enemy.BeginEmergence(Mathf.Max(0.1f, emergenceDuration));
            spawnedEnemies.Add(enemy);
            totalTrialSpawned++;
        }

        state = totalTrialSpawned > 0 ? DoorState.Fighting : DoorState.Closed;
        if (totalTrialSpawned > 0)
        {
            GameRunProgress.CommitDoor(DoorId);
            if (DamageTextManager.Instance != null)
            {
                string dangerTitle = PlayerSessionData.GetDangerLevelLabel(day);
                string extraNote = "";
                if (productData != null)
                {
                    var s = CraftedProductStats.Calculate(productData);
                    if (s.extraMonsters > 0) extraNote = $" (构装过载 +{s.extraMonsters}守卫)";
                }
                DamageTextManager.Instance.ShowText(
                    transform.position + Vector3.up * 1.5f,
                    $"⚔️ 第 {day} 天门扉守卫涌现！({dangerTitle} · 数量: {spawnedEnemies.Count}{extraNote})",
                    new Color(1f, 0.45f, 0.2f),
                    0.16f
                );
            }
        }
        else GameRunProgress.RefundDoor(DoorId);
        UpdatePromptText();
    }

    /// <summary>
    /// 当涌现的所有怪物全部被消灭时触发
    /// </summary>
    private void OnAllMonstersDefeated()
    {
        state = DoorState.Cleared;
        GameRunProgress.MarkDoorCleared(DoorId);

        Sfx.Play("sfx_parry", 1f, true);
        Vfx.Spawn("VFX_ParrySpark", transform.position + Vector3.up * 0.8f, 0, 1.8f, true);

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(
                transform.position + Vector3.up * 1.5f,
                "✨ 敌群已肃清! 空间裂隙稳定，点击进入采集温室!",
                new Color(0.25f, 1f, 0.65f),
                0.18f
            );
        }

        UpdatePromptText();
    }

    /// <summary>
    /// 与门扉交互穿越进入温室采集界面 (Collect 场景)
    /// </summary>
    public void EnterGatheringScene(PlayerController player)
    {
        if (PlayerSessionData.IsCombatInputBlocked) return;
        RefreshDailyState();
        if (state != DoorState.Cleared) return;
        var previousGreenhouse = PlayerSessionData.greenhouse;
        string previousDoorId = PlayerSessionData.activeGreenhouseDoorId;
        var previousProduct = PlayerSessionData.activeDoorProduct;
        if (!PlayerSessionData.TryBeginDoorVisit(DoorId, productData)) return;
        PlayerSessionData.activeDoorProduct = productData;
        System.Action rollback = () =>
        {
            PlayerSessionData.greenhouse = previousGreenhouse;
            PlayerSessionData.activeGreenhouseDoorId = previousDoorId;
            PlayerSessionData.activeDoorProduct = previousProduct;
        };
        if (!GameplaySceneTransition.TryOpen("Collect", player, rollback)) rollback();
    }

}
