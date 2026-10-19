using System;
using System.Collections;
using UnityEngine;
using GameJam.Boss;

/// <summary>Opt-in integration tests. Caller owns PlayerPrefs snapshot and formal scene setup.</summary>
public static class TapFinaleDoorTests
{
    public static IEnumerator Run(Action<bool, string> check)
    {
        var encounter = BossEncounterController.Instance;
        var player = PlayerController.Instance;
        var door = GatherDoorAuraBehavior.AllDoors.Find(d => d && d.IsInitialDoor);
        check(encounter && player && door, "FINALE initial portal identified by authored product marker");
        if (!encounter || !player || !door) yield break;
        check(!BossEncounterController.EncounterActive, "FINALE starts without another encounter");
        if (BossEncounterController.EncounterActive) yield break;
        int day = PlayerSessionData.greenhouse.day;
        var progress = PlayerSessionData.runProgress;
        var visits = PlayerSessionData.greenhouseDoors;
        var state = door.state;
        Vector3 playerPosition = player.transform.position;
        bool paused = EnemyController.isAIPaused;
        bool worldReady = GameRunProgress.WorldReady;
        bool follow = CameraController.Instance && CameraController.Instance.followPlayer;
        // Isolate trial reservations from the rest of the suite; the outer runner restores disk saves.
        PlayerSessionData.runProgress = new RunProgressData();
        PlayerSessionData.greenhouseDoors = new System.Collections.Generic.List<GreenhouseDoorState>();
        try
        {
            PlayerSessionData.greenhouse.day = 6;
            check(!door.IsFinalRift, "FINALE day 6 remains ordinary portal");
            door.OnPlayerInteract(player);
            check(door.state == GatherDoorAuraBehavior.DoorState.Summoning && GameRunProgress.Data.energy == 6,
                "FINALE day 6 retains ordinary two-energy guard warning");
            door.enabled = false; // Existing refund contract, before ordinary guards emerge.
            door.enabled = true;
            check(GameRunProgress.Data.energy == 8, "FINALE ordinary cancellation refunds reservation");
            PlayerSessionData.greenhouse.day = GameRunProgress.FinalDay;
            GameRunProgress.Data.energy = 0; // Boss must be accessible even with no daily energy.
            check(door.IsFinalRift, "FINALE day 7 initial portal becomes final rift");
            check(!PlayerSessionData.TryRestUntilNextDay(player, out string blockedReason) &&
                blockedReason == "终焉海神尚未被击败——去北边的终焉裂隙之门",
                "B2 day 7 undefeated final boss rejects rest with exact guidance");
            GameRunProgress.Finish();
            check(!GameRunProgress.Data.finished, "B2 day 7 undefeated final boss rejects direct settlement");
            // Keep RED runs diagnostic without letting an old unguarded Finish poison the encounter fixture.
            GameRunProgress.Data.finished = false;
            EnemyController.isAIPaused = false;
            door.OnPlayerInteract(player);
            check(door.FinalSummoning && door.PromptText.Contains("终焉裂隙之门"), "FINALE interaction begins named final summon");
            check(GameRunProgress.Data.energy == 0 && GameRunProgress.Data.pendingDoors.Count == 0,
                "FINALE summon neither charges nor reserves energy");
            var intro = door.GetComponent<BossDoorIntro>();
            check(intro != null, "FINALE production intro director exists");
            if (!intro) yield break;
            door.OnPlayerInteract(player);
            check(door.FinalSummons == 1 && !encounter.StartEncounter(), "FINALE duplicate interaction and developer spawn blocked during intro");
            EnemyController.isAIPaused = true;
            float before = intro.Elapsed;
            yield return null; yield return null;
            check(intro.Elapsed == before, "FINALE summon freezes during build pause");
            EnemyController.isAIPaused = false;
            float deadline = Time.realtimeSinceStartup + 12f;
            while (intro.Elapsed < .4f && Time.realtimeSinceStartup < deadline) yield return null;
            check(intro.Rift && intro.Rift.withEye && intro.Rift.baseOrder == -8 && !encounter.Boss,
                "FINALE rift with eye behind boss opens before spawn");
            while (intro.Elapsed < 1f && Time.realtimeSinceStartup < deadline) yield return null;
            yield return Capture("finale-rift-open.png");
            while (!encounter.Boss && intro.Running && Time.realtimeSinceStartup < deadline) yield return null;
            var boss = encounter.Boss;
            check(boss && intro.Elapsed >= 1.45f, "FINALE boss emerges no earlier than 1.45 seconds");
            if (!boss) yield break;
            check(Vector2.Distance(boss.transform.position, intro.SpawnPosition) < .001f && boss.IntroHoldRemaining > 1.3f,
                "FINALE boss spawns at fixed door-front rift and holds");
            check(boss.Animator.Vanish > .7f && boss.name.Contains("终焉海神"), "FINALE named boss begins vanished");
            check(door.totalTrialSpawned == 0 && door.spawnedEnemies.Count == 0, "FINALE no ordinary guards summoned");
            Vector3 at = boss.transform.position;
            check(!boss.ForceMove(BossMoves.P1_Lunge), "FINALE intro rejects forced attacks");
            EnemyController.isAIPaused = true;
            float hold = boss.IntroHoldRemaining;
            yield return null; yield return null;
            check(boss.IntroHoldRemaining == hold && boss.transform.position == at, "FINALE intro hold respects pause");
            EnemyController.isAIPaused = false;
            float movementDeadline = Time.realtimeSinceStartup + 3;
            float holdUntil = boss.IntroHoldRemaining - .25f;
            while (boss.IntroHoldRemaining > holdUntil && Time.realtimeSinceStartup < movementDeadline) yield return null;
            check(boss.transform.position == at && boss.CurrentMove == null, "FINALE enabled AI cannot move or attack during hold");
            boss.AutoAttack = false;
            door.OnPlayerInteract(player);
            check(encounter.Boss == boss && door.FinalSummons == 1 && GameRunProgress.Data.energy == 0,
                "FINALE live boss interaction is idempotent and free");
            while (intro.Elapsed < 2.15f && Time.realtimeSinceStartup < deadline) yield return null;
            yield return Capture("finale-boss-emerges.png");
            while (intro.Running && Time.realtimeSinceStartup < deadline) yield return null;
            check(!intro.Running && !intro.Rift && boss.Animator.Vanish == 0,
                "FINALE rift closes and vanish resets at intro completion");
            check(!CameraController.Instance || CameraController.Instance.followPlayer == follow, "FINALE camera follow restored");
            check(boss.transform.position == at && boss.CurrentMove == null, "FINALE boss remains still throughout entry");
            // Same lethal/threshold progression as BossRegression; use real death event, not Destroy-as-victory.
            player.transform.position = at + Vector3.right * 100;
            boss.Entity.TakeDamage(boss.Entity.maxHp * 10, 0, at);
            for (int i = 0; i < 600 && boss.Entity.IsAlive; i++) boss.Tick(.1f);
            check(!boss.Entity.IsAlive && door.FinalBossDefeated && !door.IsFinalRift,
                "FINALE real death restores ordinary portal");
            PlayerSessionData.LoadBackpackFromStorage(); // Read the death callback's disk save, not the live object.
            check(GameRunProgress.Data.finalBossDefeated && door.FinalBossDefeated && !door.IsFinalRift,
                "B2 actual save reload retains final victory and ordinary portal");
            // Mandatory threshold attacks may kill the player even far from the boss.
            // Let the production revival finish before testing a living player's interaction.
            float reviveDeadline = Time.realtimeSinceStartup + 15f;
            while (!player.IsAlive && Time.realtimeSinceStartup < reviveDeadline) yield return null;
            check(player.IsAlive, "FINALE player revived after lethal threshold sequence");
            GameRunProgress.Data.energy = 8;
            Debug.Log($"[FinaleDoor] before ordinary interaction state={door.state} alive={player.IsAlive} blocked={PlayerSessionData.IsCombatInputBlocked} paused={BiomechanicalAuraBase.IsCombatPaused} used={PlayerSessionData.IsDoorUsedToday(door.DoorId)}");
            door.OnPlayerInteract(player);
            check(door.state == GatherDoorAuraBehavior.DoorState.Summoning && GameRunProgress.Data.energy == 6,
                "FINALE defeated portal resumes normal paid guard trial before greenhouse");
            // Disabling the door refunds energy but does not cancel its Summoning state.
            // Finish the actual post-victory trial instead of bypassing the existing enemy safety gate.
            float guardsDeadline = Time.realtimeSinceStartup + 5f;
            while (door.state == GatherDoorAuraBehavior.DoorState.Summoning && Time.realtimeSinceStartup < guardsDeadline)
                yield return null;
            check(door.spawnedEnemies.Count > 0 && !PlayerSessionData.CanRestUntilNextDay(player, out _),
                "B2 victory does not bypass ordinary living-enemy rest safety");
            yield return null; // Let each guard's Start initialize its emergence protection.
            float emergeDeadline = Time.realtimeSinceStartup + 5f;
            while (door.spawnedEnemies.Exists(e => e && e.IsEmerging) && Time.realtimeSinceStartup < emergeDeadline)
                yield return null;
            check(!door.spawnedEnemies.Exists(e => e && e.IsEmerging), "B2 trial guards complete emergence before damage probe");
            foreach (var guard in door.spawnedEnemies.ToArray())
                if (guard && guard.IsAlive) guard.TakeDamage(guard.maxHp * 10f, 0f, player.transform.position);
            float restDeadline = Time.realtimeSinceStartup + 6f;
            while (!PlayerSessionData.CanRestUntilNextDay(player, out _) && Time.realtimeSinceStartup < restDeadline)
                yield return null;
            bool rested = PlayerSessionData.TryRestUntilNextDay(player, out string restReason);
            Debug.Log($"[B2Rest] accepted={rested} reason={restReason} door={door.state} alive={player.IsAlive} invulnerable={player.IsInvulnerable}");
            foreach (var e in EnemyController.AllEnemies)
                if(e) Debug.Log($"[B2Rest] enemy={e.name} alive={e.IsAlive} respawning={e.IsRespawning}");
            check(rested && GameRunProgress.Data.finished,
                "B2 defeated final boss permits day 7 rest settlement");
            PlayerSessionData.LoadBackpackFromStorage();
            check(GameRunProgress.Data.finished && door.FinalBossDefeated,
                "B2 settled victory survives production save reload");
            PlayerSessionData.StartNewRun();
            check(!door.FinalBossDefeated && !GameRunProgress.Data.finished && PlayerSessionData.greenhouse.day == 1,
                "B2 new journey resets persisted final victory");
            PlayerSessionData.LoadBackpackFromStorage();
            check(!door.FinalBossDefeated, "B2 new journey reset survives production save reload");
            for (int earlyDay = 1; earlyDay <= 6; earlyDay++)
            {
                PlayerSessionData.greenhouse.day = earlyDay;
                check(!GameRunProgress.FinalBossRequired, "B2 day " + earlyDay + " has no final boss rest gate");
            }
            PlayerSessionData.greenhouse.day = 7;
            check(door.IsFinalRift && !PlayerSessionData.TryRestUntilNextDay(player, out _),
                "B2 same portal requires new victory in a new journey");
        }
        finally
        {
            door.enabled = false;
            encounter.CancelEncounter();
            PlayerSessionData.greenhouse.day = day;
            PlayerSessionData.runProgress = progress;
            PlayerSessionData.greenhouseDoors = visits;
            GameRunProgress.WorldReady = worldReady;
            player.transform.position = playerPosition;
            EnemyController.isAIPaused = paused;
            door.state = state;
            door.enabled = true;
        }
        yield return null;
    }
    static IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        var c = Camera.main; var intro = BossDoorIntro.Active;
        Debug.Log($"[FinaleFrame] {name} camera={c.transform.position} rotation={c.transform.eulerAngles} size={c.orthographicSize} bounds={CameraController.Instance.useBounds} spawn={(intro ? intro.SpawnPosition : Vector3.zero)}");
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(TapSelfTest.OutputDirectory, name), image.EncodeToPNG());
        UnityEngine.Object.Destroy(image);
        yield return null;
    }
}
