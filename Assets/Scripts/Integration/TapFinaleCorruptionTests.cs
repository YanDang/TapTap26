using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Opt-in: run inside TapSelfTest's save isolation. Does not register itself or launch Unity.</summary>
public static class TapFinaleCorruptionTests
{
    public static void Numbers(Action<bool, string> check)
    {
        int[] days = { 1, 4, 7 };
        float[] hp = { 100, 175, 250 }, hit = { 15, 20.4f, 25.8f }, grab = { 10, 13.6f, 17.2f };
        for (int i = 0; i < days.Length; i++)
        {
            int level = TapCorruptionRules.LevelForDay(days[i]);
            check(level == i * 3, "FINALE corruption level day " + days[i]);
            check(Near(100 * TapCorruptionRules.HpMultiplier(level), hp[i]), "FINALE HP day " + days[i]);
            check(Near(15 * TapCorruptionRules.DamageMultiplier(level), hit[i]), "FINALE hit day " + days[i]);
            check(Near(10 * TapCorruptionRules.DamageMultiplier(level), grab[i]), "FINALE grab day " + days[i]);
        }
        check(TapCorruptionRules.LevelForDay(int.MinValue) == 0 && TapCorruptionRules.LevelForDay(int.MaxValue) == 6, "FINALE day clamp including integer boundaries");
    }
    static bool Near(float a, float b) => Math.Abs(a - b) < .002f;

    public static IEnumerator Run(Action<bool, string> check)
    {
        Numbers(check);
        var greenhouse = PlayerSessionData.greenhouse;
        int oldDay = greenhouse.day;
        var owned = new List<EnemyController>();
        try
        {
            foreach (int day in new[] { 1, 4, 7 })
            {
                greenhouse.day = day;
                for (int kind = 0; kind < 3; kind++)
                {
                    Vector3 at = new Vector3(100, 100 + kind * 3, 0);
                    var e = kind == 0 ? EnemyController.CreateEnemyInstance(at) : kind == 1
                        ? EnemyController.CreateTriggerfishInstance(at) : new GameObject("FINALE direct entity").AddComponent<EnemyController>();
                    owned.Add(e); e.transform.position = at; e.autoRespawn = false; e.ExternalAI = (entity, dt) => { };
                    float baseHp = e.maxHp, baseHit = e.attackDamage, baseGrab = e.grabDamage;
                    yield return null; // actual Start, not direct Apply only
                    var c = e.GetComponent<TapCorruption>();
                    float hm = day == 1 ? 1 : day == 4 ? 1.75f : 2.5f;
                    float dm = day == 1 ? 1 : day == 4 ? 1.36f : 1.72f;
                    check(c && c.Level == day - 1, "FINALE all spawn routes attach day " + day + " kind " + kind);
                    check(Near(e.maxHp, baseHp * hm) && Near(e.currentHp, e.maxHp), "FINALE real spawn HP");
                    check(Near(e.DamageToPlayer(baseHit), baseHit * dm) && Near(e.DamageToPlayer(baseGrab), baseGrab * dm), "FINALE both player damage routes");
                    check(Near(e.attackDamage, baseHit) && Near(e.grabDamage, baseGrab), "FINALE raw furniture/base damage unchanged");
                    float hp = e.maxHp; e.currentHp -= 7;
                    TapCorruption.Apply(e, day);
                    check(Near(e.maxHp, hp) && Near(e.currentHp, hp - 7), "FINALE repeated Apply neither stacks nor heals");
                    e.SetVisualColor(Color.white); e.ApplyFlash(0, Color.white);
                    var renderer = e.flashRenderers != null && e.flashRenderers.Length > 0 ? e.flashRenderers[0] as SpriteRenderer : e.monsterRenderer;
                    if (renderer)
                    {
                        check(day == 1 ? Near(renderer.color.g, 1) : renderer.color.g < .65f && renderer.color.b > renderer.color.g, "FINALE purple-red body tint");
                        var block = new MaterialPropertyBlock();
                        foreach (Color warning in new[] { EnemyController.AttackGlow, EnemyController.GrabGlow, EnemyController.PendingGlow })
                        {
                            e.ApplyFlash(.1f, warning); renderer.GetPropertyBlock(block);
                            check(block.GetColor("_FlashColor") == warning && Near(block.GetFloat("_Flash"), .1f), "FINALE weaker warning overrides corruption");
                        }
                    }
                    else check(kind == 2, "FINALE authored monster has renderers");
                    check(day == 1 ? !c.Pool : c.Pool && c.Pool.sprite == BossFxAssets.Dot && c.Motes && c.Motes.GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture == BossFxAssets.Dot.texture, "FINALE soft pool and textured spores");
                }
                var elite = EnemyController.CreateEnemyInstance(new Vector3(100, 120, 0)); owned.Add(elite);
                elite.ExternalAI = (entity, dt) => { }; elite.ApplyDayDifficulty(day, true);
                yield return null;
                check(Near(elite.maxHp, 135 * (day == 1 ? 1 : day == 4 ? 1.75f : 2.5f)), "FINALE elite HP only one daily factor");
                check(Near(elite.DamageToPlayer(elite.attackDamage), day == 1 ? 18.75f : day == 4 ? 25.5f : 32.25f) &&
                    Near(elite.DamageToPlayer(elite.grabDamage), day == 1 ? 12.5f : day == 4 ? 17f : 21.5f), "FINALE elite hit and grab preserve elite multiplier once");
                float before = elite.maxHp; elite.ApplyDayDifficulty(day, true);
                check(Near(before, elite.maxHp), "FINALE elite difficulty idempotent");
            }
            greenhouse.day = 1;
            var respawn = EnemyController.CreateEnemyInstance(new Vector3(100, 130, 0), "FINALE respawn");
            owned.Add(respawn); respawn.ExternalAI = (entity, dt) => { }; respawn.autoRespawn = true; respawn.respawnDelay = .01f;
            yield return null;
            greenhouse.day = 7;
            check(Near(respawn.maxHp, 100), "FINALE living enemy keeps its spawn-day snapshot");
            respawn.TakeDamage(10000, 0, respawn.transform.position);
            float reviveDeadline = Time.realtimeSinceStartup + 8;
            while (!respawn.IsAlive && Time.realtimeSinceStartup < reviveDeadline) yield return null;
            check(respawn.IsAlive && Near(respawn.maxHp, 250) && Near(respawn.currentHp, 250) && respawn.Corruption.Level == 6,
                "FINALE real respawn refreshes current day once");
            yield return DamageRoutes(check);
            greenhouse.day = 7;
            // Actual production factory, including inactive-root construction and Brain.Initialize.
            var encounter = BossEncounterController.Instance;
            check(encounter && !BossEncounterController.EncounterActive, "FINALE Boss probe starts with no existing encounter");
            if (encounter && !BossEncounterController.EncounterActive)
            {
                var brain = encounter.StartEncounter();
                check(brain, "FINALE real Boss factory accepted");
                if (brain)
                {
                    brain.AutoAttack = false;
                    var boss = brain.Entity; owned.Add(boss);
                    float expected = PlayerController.Instance.attackDamage * 80;
                    yield return null;
                    check(!boss.GetComponent<TapCorruption>() && Near(boss.maxHp, expected) && Near(boss.DamageToPlayer(20), 20), "FINALE Boss factory ordering excludes HP damage and FX");
                    check(!TapCorruption.Apply(boss, 7) && Near(boss.maxHp, expected), "FINALE explicit Boss Apply rejected");
                }
            }
        }
        finally
        {
            greenhouse.day = oldDay;
            foreach (var e in owned) if (e) UnityEngine.Object.Destroy(e.gameObject);
        }
        yield return null;
    }

    // Caller runs the suite with no equipped gear (GearInventory.ResetAll inside the save envelope),
    // so mitigation/healing cannot hide an incorrect raw attack multiplier.
    public static IEnumerator DamageRoutes(Action<bool, string> check)
    {
        var p = PlayerController.Instance;
        bool cleanGear = true;
        foreach (GameJam.Gear.GearSlot slot in Enum.GetValues(typeof(GameJam.Gear.GearSlot)))
            cleanGear &= GameJam.Gear.GearInventory.Equipped(slot) == null;
        check(p && cleanGear, "FINALE damage probe requires live player and unequipped gear");
        if (!p || !cleanGear) yield break;
        float originalHp = p.currentHp; Vector3 originalPosition = p.transform.position;
        int originalDay = PlayerSessionData.greenhouse.day;
        EnemyController e = null;
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        Action<string, object> field = (name, value) => typeof(EnemyController).GetField(name, flags).SetValue(e, value);
        try
        {
            foreach (int day in new[] { 1, 4, 7 })
            {
                PlayerSessionData.greenhouse.day = day;
                e = EnemyController.CreateEnemyInstance(originalPosition + Vector3.right * 100, "FINALE damage route");
                e.ExternalAI = (entity, dt) => { }; e.enableClockQTE = false;
                yield return null;
                float deadline = Time.realtimeSinceStartup + 6;
                while ((!p.CanBeginUltimate || p.IsInvulnerable || p.IsGuarding) && Time.realtimeSinceStartup < deadline) yield return null;
                bool ready = p.CanBeginUltimate && !p.IsInvulnerable && !p.IsGuarding;
                check(ready, "FINALE actual hit readiness day " + day);
                if (!ready) yield break;
                p.currentHp = p.maxHp; float before = p.currentHp;
                e.transform.position = p.transform.position + Vector3.right * .1f;
                e.aiState = EnemyController.EnemyAIState.Attack; e.normalImpact = 0;
                field("targetPlayer", p); field("stateTimer", 0f); field("impactDone", false);
                typeof(EnemyController).GetMethod("UpdateAttackState", flags).Invoke(e, new object[] { 0f });
                check(Near(before - p.currentHp, day == 1 ? 15 : day == 4 ? 20.4f : 25.8f), "FINALE actual normal impact HP loss day " + day);
                e.transform.position = originalPosition + Vector3.right * 100;
                deadline = Time.realtimeSinceStartup + 6;
                while ((!p.CanBeginUltimate || p.IsInvulnerable) && Time.realtimeSinceStartup < deadline) yield return null;
                bool grabbed = p.TryGrab(e);
                check(grabbed, "FINALE real TryGrab day " + day);
                if (grabbed)
                {
                    e.aiState = EnemyController.EnemyAIState.Grabbing;
                    field("stateTimer", 0f); field("grabTickTimer", 0f); before = p.currentHp;
                    typeof(EnemyController).GetMethod("UpdateGrabbingState", flags).Invoke(e, new object[] { 0f });
                    check(Near(before - p.currentHp, day == 1 ? 10 : day == 4 ? 13.6f : 17.2f), "FINALE actual grab tick HP loss day " + day);
                    p.ThrowFrom(e);
                }
                UnityEngine.Object.Destroy(e.gameObject); e = null;
                deadline = Time.realtimeSinceStartup + 6;
                while ((p.Unavailable || p.IsInvulnerable) && Time.realtimeSinceStartup < deadline) yield return null;
                p.transform.position = originalPosition;
            }
        }
        finally
        {
            if (e) { if (p.IsGrabbedBy(e)) p.ThrowFrom(e); UnityEngine.Object.Destroy(e.gameObject); }
            p.currentHp = originalHp; p.transform.position = originalPosition;
            PlayerSessionData.greenhouse.day = originalDay;
        }
    }

    /// <summary>Caller frames camera first. Leaves no enemies behind; waits after GPU capture before cleanup.</summary>
    public static IEnumerator CaptureComparison(Vector3 center, float spacing, string pngPath)
    {
        var enemies = new List<EnemyController>();
        int oldDay = PlayerSessionData.greenhouse.day;
        var cc = CameraController.Instance;
        bool oldFollow = cc.followPlayer;
        var oldPosition = cc.transform.position;
        float oldZoom = cc.GetComponent<Camera>().orthographicSize;
        bool oldBounds = cc.useBounds;
        cc.useBounds = false;
        cc.SetFollowPlayer(false);
        cc.FocusOn(center + Vector3.up * .6f, Mathf.Max(oldZoom, spacing * 1.5f));
        try
        {
            int[] days = { 1, 4, 7 };
            for (int i = 0; i < 3; i++)
            {
                PlayerSessionData.greenhouse.day = days[i];
                var e = EnemyController.CreateEnemyInstance(center + Vector3.right * ((i - 1) * spacing), "Corruption_Day" + days[i]);
                enemies.Add(e); e.ExternalAI = (entity, dt) => { }; TapCorruption.Apply(e, days[i]);
            }
            yield return null;
            yield return new WaitForSeconds(.4f);
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(pngPath, image.EncodeToPNG()); }
            finally { UnityEngine.Object.Destroy(image); }
            yield return null; // do NOT Destroy in the capture frame
        }
        finally
        {
            PlayerSessionData.greenhouse.day = oldDay;
            cc.SetFollowPlayer(oldFollow);
            cc.useBounds = oldBounds;
            cc.FocusOn(oldPosition, oldZoom);
            foreach (var e in enemies) if (e) UnityEngine.Object.Destroy(e.gameObject);
        }
    }
}
