using System.Collections.Generic;
using UnityEngine;
using GameJam;

/// <summary>
/// 废料 5: 嘶鸣的过载排气瓣 (scrap_screaming_valve)
/// - 机制：【强力诱敌嘲讽】
///   间歇性喷射蒸汽尖啸（持续 screamDuration 秒，间歇 restDuration 秒）。
///   尖啸期间 tauntRange (7.0m) 内的所有怪物强行被吸引过来围攻它，
///   即使正在追玩家也会转头，锁定后打到它坏为止！
/// </summary>
public class ScreamingValveAuraBehavior : BiomechanicalAuraBase
{
    [Header("Taunt Stats")]
    public float tauntRange = 7.0f;
    public float screamDuration = 10.0f;
    public float restDuration = 5.0f;
    public float startDelay = 1.0f;

    public bool IsScreaming { get; private set; } = false;

    private float cycleTimer = 0f;
    private float waveTimer = 0f;
    private readonly List<GameObject> activeWaveRings = new List<GameObject>();

    public static readonly List<ScreamingValveAuraBehavior> ActiveValves = new List<ScreamingValveAuraBehavior>();

    protected override void Awake()
    {
        base.Awake();
        CreateRangeRing(tauntRange, new Color(0.95f, 0.45f, 0.2f, 0.35f), "Taunt_Range_Ring");
        cycleTimer = -startDelay;
    }

    void OnEnable()
    {
        if (!ActiveValves.Contains(this))
            ActiveValves.Add(this);
    }

    void OnDisable()
    {
        ActiveValves.Remove(this);
        ClearWaveRings();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        ActiveValves.Remove(this);
        ClearWaveRings();
    }

    void Update()
    {
        if (parentFurniture != null && parentFurniture.currentState != FurnitureObject.FurnitureState.Placed)
        {
            if (rangeRing != null && rangeRing.activeSelf) rangeRing.SetActive(false);
            IsScreaming = false;
            return;
        }

        if (rangeRing != null && !rangeRing.activeSelf) rangeRing.SetActive(true);

        if (IsCombatPaused) return;

        float dt = Time.deltaTime;
        cycleTimer += dt;

        float totalCycle = screamDuration + restDuration;
        if (cycleTimer >= totalCycle)
        {
            cycleTimer -= totalCycle;
        }

        bool wasScreaming = IsScreaming;
        IsScreaming = (cycleTimer >= 0f && cycleTimer < screamDuration);

        if (IsScreaming && !wasScreaming)
        {
            Sfx.Play("sfx_sword_draw", 0.9f);
            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.8f, "📢 蒸汽尖啸! (TAUNT)", new Color(1f, 0.45f, 0.15f), 0.14f);
            }
        }

        if (IsScreaming)
        {
            UpdateRingPulse(5.5f, 0.12f);

            // 尖啸声波扩散动画
            waveTimer += dt;
            if (waveTimer >= 0.55f)
            {
                waveTimer = 0f;
                SpawnSoundWave();
            }

            // 强行诱引范围内的所有怪兽
            TauntNearbyEnemies();
        }
        else
        {
            UpdateRingPulse(1.5f, 0.04f);
        }

        UpdateWaveRings(dt);
    }

    private void TauntNearbyEnemies()
    {
        var enemies = EnemyController.AllEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e != null && e.IsAlive && !e.isPacified)
            {
                float d = Vector2.Distance(transform.position, e.transform.position);
                if (d <= tauntRange)
                {
                    e.ForceTauntTarget(parentFurniture);
                }
            }
        }
    }

    private void SpawnSoundWave()
    {
        GameObject waveGO = new GameObject("Scream_Wave");
        waveGO.transform.position = transform.position + new Vector3(0f, -0.05f, 0.05f);
        waveGO.transform.localScale = Vector3.one * 0.2f;

        var sr = waveGO.AddComponent<SpriteRenderer>();
        var circleSprite = Resources.Load<Sprite>("target_ring") ?? Resources.Load<Sprite>("click_marker");
        if (circleSprite != null) sr.sprite = circleSprite;
        var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
        if (unlitShader != null) sr.material = new Material(unlitShader);
        sr.color = new Color(1f, 0.5f, 0.2f, 0.7f);
        sr.sortingOrder = 4;

        activeWaveRings.Add(waveGO);
    }

    private void UpdateWaveRings(float dt)
    {
        for (int i = activeWaveRings.Count - 1; i >= 0; i--)
        {
            var ring = activeWaveRings[i];
            if (ring == null)
            {
                activeWaveRings.RemoveAt(i);
                continue;
            }

            float currentScale = ring.transform.localScale.x;
            float maxScale = tauntRange * 2f;
            currentScale += dt * (tauntRange * 1.8f);
            ring.transform.localScale = new Vector3(currentScale, currentScale, 1f);

            var sr = ring.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                float alpha = Mathf.Clamp01(1f - (currentScale / maxScale));
                sr.color = new Color(1f, 0.5f, 0.2f, alpha * 0.65f);
            }

            if (currentScale >= maxScale)
            {
                Destroy(ring);
                activeWaveRings.RemoveAt(i);
            }
        }
    }

    private void ClearWaveRings()
    {
        for (int i = 0; i < activeWaveRings.Count; i++)
        {
            if (activeWaveRings[i] != null) Destroy(activeWaveRings[i]);
        }
        activeWaveRings.Clear();
    }

    /// <summary>
    /// 获取当前在 pos 附近正在尖啸嘲讽的排气瓣（优先最近）
    /// </summary>
    public static ScreamingValveAuraBehavior GetTauntingValveNear(Vector3 pos)
    {
        ScreamingValveAuraBehavior best = null;
        float minD = float.MaxValue;
        for (int i = 0; i < ActiveValves.Count; i++)
        {
            var v = ActiveValves[i];
            if (v != null && v.IsScreaming && v.parentFurniture != null && v.parentFurniture.currentState == FurnitureObject.FurnitureState.Placed)
            {
                float d = Vector2.Distance(pos, v.transform.position);
                if (d <= v.tauntRange && d < minD)
                {
                    minD = d;
                    best = v;
                }
            }
        }
        return best;
    }
}
