using UnityEngine;
using UnityEngine.UI;
using GameJam.Boss;

/// <summary>Final portal presentation. One world-clock owner; no guard/payment or combat damage logic.</summary>
[DefaultExecutionOrder(-60)]
public sealed class BossDoorIntro : MonoBehaviour
{
    public const float RiftAt = .35f, SpawnAt = 1.45f, CloseAt = 2.35f, EndAt = 2.95f;
    public static BossDoorIntro Active { get; private set; }
    public bool Running { get; private set; }
    public float Elapsed { get; private set; }
    public Vector3 SpawnPosition { get; private set; }
    public BossRift Rift { get; private set; }
    GatherDoorAuraBehavior door;
    PlayerController player;
    BossBrain boss;
    float unit;
    bool spawned, closed;
    CameraController cameraControl;
    Vector3 savedPosition, savedOffset;
    float savedZoom;
    bool savedFollow, savedBounds;
    float savedMinZoom, savedMaxZoom;
    Canvas bars;
    Image topBar, bottomBar;

    public bool Begin(GatherDoorAuraBehavior owner, PlayerController target)
    {
        if (Active || Running || !owner.IsFinalRift || !target || !target.IsAlive ||
            !BossEncounterController.Instance || BossEncounterController.EncounterActive || EnemyController.CombatWorldPaused) return false;
        unit = BossEncounterController.PresentationUnitScale(target);
        if (!TrySpawnPosition(owner.transform.position, target.transform.position, unit, out var at)) return false;
        door = owner; player = target; SpawnPosition = at;
        Elapsed = 0; spawned = closed = false; boss = null;
        Running = true; Active = this;
        cameraControl = CameraController.Instance;
        if (cameraControl)
        {
            savedPosition = cameraControl.transform.position;
            savedOffset = cameraControl.followOffset;
            savedFollow = cameraControl.followPlayer;
            savedZoom = cameraControl.GetComponent<Camera>().orthographicSize;
            savedBounds = cameraControl.useBounds;
            savedMinZoom = cameraControl.minZoom; savedMaxZoom = cameraControl.maxZoom;
            cameraControl.useBounds = false;
            cameraControl.minZoom = Mathf.Min(savedMinZoom, 3.3f * unit);
            cameraControl.maxZoom = Mathf.Max(savedMaxZoom, 3.3f * unit);
            cameraControl.SetFollowPlayer(false);
        }
        return true;
    }

    // Prefer precisely 1.6 source metres toward the player. Only nearby, forward alternatives
    // are considered if terrain/furniture blocks the full boss footprint; never teleport to the player.
    public static bool TrySpawnPosition(Vector3 origin, Vector3 target, float scale, out Vector3 at)
    {
        Vector2 direction = (Vector2)(target - origin);
        if (direction.sqrMagnitude < .0001f) direction = Vector2.down;
        direction.Normalize();
        var map = IsometricPathfinder.Instance;
        foreach (float distance in new[] { 1.6f, 2f, 2.4f, 3.2f })
        foreach (float angle in new[] { 0f, -25f, 25f, -50f, 50f })
        {
            Vector2 delta = (Vector2)(Quaternion.Euler(0, 0, angle) * direction) * distance * scale;
            at = origin + (Vector3)delta;
            bool safe = !map || map.IsWalkable(map.WorldToCell(at));
            if (map) for (int i = 0; i < 8 && safe; i++)
            {
                Vector3 edge = at + Quaternion.Euler(0, 0, i * 45) * Vector3.right * (.9f * scale);
                safe = map.IsWalkable(map.WorldToCell(edge));
            }
            if (safe) return true;
        }
        at = origin;
        return false;
    }

    void Update()
    {
        if (!Running) return;
        if (!BossEncounterController.Instance || !door || !door.isActiveAndEnabled || !player || !player.IsAlive ||
            !door.IsFinalRift || (spawned && (!boss || !boss.Entity.IsAlive))) { Cancel(); return; }
        if (EnemyController.CombatWorldPaused) return;
        Elapsed += Time.deltaTime;
        float t = Elapsed;
        float focus = Mathf.SmoothStep(0, 1, t / .3f) * (1 - Mathf.SmoothStep(0, 1, (t - 2.55f) / .4f));
        if (cameraControl)
        {
            Vector2 target = SpawnPosition + Vector3.up * (2.3f * unit);
            // Keep the five-unit silhouette inside the letterboxed safe area.
            cameraControl.FocusOn(Vector2.Lerp(savedPosition, target, focus), Mathf.Lerp(savedZoom, Mathf.Max(savedZoom * .8f, 3.3f * unit), focus));
        }
        SetLetterbox(.095f * focus);
        if (!Rift && !closed && t >= RiftAt)
        {
            Rift = BossRift.Spawn(SpawnPosition + Vector3.up * (2.3f * unit), 4.6f, 1.7f, false, true, -8);
            // Ground tilemap is order 0; group the original negative internal layers
            // above it and below the boss's lowest authored sprite order (2).
            Rift.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = 1;
            Rift.transform.SetParent(transform, true);
            Rift.transform.localScale = Vector3.one * unit;
            Rift.faceCamera = false;
            Rift.GlowColor = new Color(1f, .30f, .72f);
            Rift.InnerColor = new Color(.32f, .025f, .20f);
            Rift.open = .05f; Rift.targetOpen = 1; Rift.openSpeed = 1.1f;
            cameraControl?.Shake(.12f, .5f * unit);
        }
        if (Rift) Rift.lookAt = player.transform.position + Vector3.up * unit;
        if (!spawned && t >= SpawnAt)
        {
            boss = BossEncounterController.Instance.StartEncounter(SpawnPosition, 1.6f);
            if (!boss) { Cancel(); return; }
            spawned = true;
            door.FinalBossArrived(boss);
            if (Rift) { Rift.withEye = false; Rift.flash = 1; }
            cameraControl?.Shake(.25f, .35f * unit);
        }
        if (boss && boss.Animator) boss.Animator.Vanish = 1 - Mathf.SmoothStep(0, 1, (t - SpawnAt) / .8f);
        if (!closed && t >= CloseAt)
        {
            closed = true;
            if (Rift) { Rift.Slam(); Rift.autoDestroyWhenClosed = true; }
        }
        if (t >= EndAt) Cancel();
    }

    void SetLetterbox(float height)
    {
        if (!bars) bars = BossUltDirector.CreateLetterbox(transform, out topBar, out bottomBar);
        bars.enabled = height > 0 && !PlayerSessionData.IsCombatInputBlocked;
        topBar.rectTransform.sizeDelta = bottomBar.rectTransform.sizeDelta = new Vector2(0, Screen.height * height);
    }

    public void Cancel()
    {
        if (!Running) return;
        if (boss && boss.Animator) boss.Animator.Vanish = 0;
        boss = null;
        if (Rift) Destroy(Rift.gameObject);
        Rift = null;
        if (bars) bars.enabled = false;
        if (cameraControl)
        {
            cameraControl.followOffset = savedOffset;
            cameraControl.useBounds = savedBounds;
            cameraControl.minZoom = savedMinZoom; cameraControl.maxZoom = savedMaxZoom;
            cameraControl.SetFollowPlayer(savedFollow);
            cameraControl.FocusOn(savedPosition, savedZoom);
            cameraControl = null;
        }
        Running = false;
        if (Active == this) Active = null;
        if (door) door.RefreshFinalPrompt();
    }
    void LateUpdate() { if (bars && PlayerSessionData.IsCombatInputBlocked) bars.enabled = false; }
    void OnDisable() { Cancel(); }
}
