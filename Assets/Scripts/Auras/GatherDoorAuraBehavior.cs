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
///      * 玩家点击或交互打开后，从门扉位置随机涌现 1~3 只异界怪兽 (Enemy_Squid)；
///      * 怪物立即锁定玩家并追击，且 autoRespawn 设为 false (消灭后不重复复活)；
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
        Cleared    // 敌群肃清，通道稳定，可传送进入采集
    }

    [Header("Door Status")]
    public DoorState state = DoorState.Closed;
    public float interactionRadius = 2.2f;

    [Header("Spawned Enemies")]
    public readonly List<EnemyController> spawnedEnemies = new List<EnemyController>();
    public int totalTrialSpawned = 0;

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
    }

    private void UpdatePromptText()
    {
        if (promptTextMesh == null) return;

        switch (state)
        {
            case DoorState.Closed:
                promptTextMesh.text = "🚪 点击或按 [F] 开启裂隙门";
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
        bool isPlaced = (parentFurniture != null && parentFurniture.currentState == FurnitureObject.FurnitureState.Placed);

        if (!isPlaced)
        {
            if (rangeRing != null && rangeRing.activeSelf) rangeRing.SetActive(false);
            if (promptGo != null && promptGo.activeSelf) promptGo.SetActive(false);
            return;
        }

        if (rangeRing != null && !rangeRing.activeSelf) rangeRing.SetActive(true);
        if (promptGo != null && !promptGo.activeSelf) promptGo.SetActive(true);

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
            Color curCol = (state == DoorState.Closed) ? ColorClosed : (state == DoorState.Fighting ? ColorFighting : ColorCleared);
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

        // 键盘快捷按键检测：当主角靠近门扉 <= 2.2m 时，按下 F 键直接交互
        var p = PlayerController.Instance ?? FindObjectOfType<PlayerController>();
        if (p != null)
        {
            float dist = Vector2.Distance(transform.position, p.transform.position);
            if (dist <= interactionRadius)
            {
                if (Input.GetKeyDown(KeyCode.F))
                {
                    OnPlayerInteract(p);
                }
            }
        }
    }

    void LateUpdate()
    {
        // 确保头顶提示朝向摄像机
        if (promptGo != null && Camera.main != null)
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
        if (player == null) player = PlayerController.Instance ?? FindObjectOfType<PlayerController>();

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
    private void OpenDoor(PlayerController player)
    {
        if (state != DoorState.Closed) return;

        int spawnCount = Random.Range(1, 4); // 随机 1~3 只
        spawnedEnemies.Clear();
        totalTrialSpawned = 0;

        Vector3 basePos = transform.position;

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-1.3f, 1.3f),
                Random.Range(-1.2f, -0.3f), // 稍向下偏移，确保完全在地图内
                0f
            );
            Vector3 spawnPos = basePos + offset;

            EnemyController ec = EnemyController.CreateEnemyInstance(spawnPos, $"Portal_Enemy_{i + 1}");
            if (ec != null)
            {
                ec.spawnPosition = spawnPos;
                ec.autoRespawn = false; // 严禁打败后自动复活，确保通关可判定
                ec.currentHp = ec.maxHp;
                ec.aiState = EnemyController.EnemyAIState.Chase; // 立即扑向玩家发起围攻
                spawnedEnemies.Add(ec);
                totalTrialSpawned++;
            }
        }

        state = DoorState.Fighting;

        Sfx.Play("sfx_table_land", 1f, true);
        Vfx.Spawn("VFX_ParrySpark", basePos + Vector3.up * 0.8f, 0, 1.5f, true);

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(
                basePos + Vector3.up * 1.5f,
                $"🌀 异界裂隙开启! 涌现了 {spawnCount} 只怪兽!",
                new Color(1f, 0.45f, 0.2f),
                0.16f
            );
        }

        UpdatePromptText();
    }

    /// <summary>
    /// 当涌现的所有怪物全部被消灭时触发
    /// </summary>
    private void OnAllMonstersDefeated()
    {
        state = DoorState.Cleared;

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
        if (PlayerSessionData.isCraftingOpen) return;
        PlayerSessionData.isCraftingOpen = true;

        PlayerSessionData.returnCombatSceneName = SceneManager.GetActiveScene().name;

        if (player != null && player.equippedWeapon != null)
        {
            PlayerSessionData.currentEquippedWeapon = player.equippedWeapon;
        }

        // 隐藏左上角战斗 HUD
        if (GameJam.HUD.I != null)
        {
            GameJam.HUD.I.SetVisible(false);
        }

        var camCtrl = FindObjectOfType<CameraController>();
        if (camCtrl != null) camCtrl.enabled = false;
        // 保持 AudioListener 开启，确保叠加场景中的 2D 采集音效能正常被听到，同时消除全局无侦听器的警告

        Sfx.Play("sfx_parry", 0.9f, true);

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(
                transform.position + Vector3.up * 1.2f,
                "🌀 穿梭裂隙... 前往生体采集温室!",
                new Color(0.2f, 1f, 0.7f),
                0.16f
            );
        }

        Time.timeScale = 0f; // 冻结当前战场，保留所有现场怪兽与建筑状态
        SceneManager.LoadSceneAsync("Collect", LoadSceneMode.Additive);
    }

    void OnMouseDown()
    {
        // 允许直接鼠标点击门扉触发交互
        var p = PlayerController.Instance ?? FindObjectOfType<PlayerController>();
        if (p != null)
        {
            float dist = Vector2.Distance(transform.position, p.transform.position);
            if (dist <= interactionRadius + 0.8f)
            {
                OnPlayerInteract(p);
            }
            else
            {
                p.MoveToWorldPoint(transform.position - new Vector3(0f, 1.2f, 0f));
            }
        }
    }
}
