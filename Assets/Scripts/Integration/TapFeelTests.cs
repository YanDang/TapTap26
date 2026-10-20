using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>TapCombatFeel（gamejam 战斗手感对齐）的自测：普攻判定帧、受击硬直、击退换算、镜头跟随与震屏。</summary>
public static class TapFeelTests
{
    static T Private<T>(object o, string f) => (T)o.GetType().GetField(f, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);

    public static IEnumerator Run(Action<bool, string> check)
    {
        var p = PlayerController.Instance;
        var cc = CameraController.Instance;
        check(TapCombatFeel.On, "FEEL installed in combat scene");
        check(cc && cc.feelFollowRate == TapCombatFeel.FollowRate && cc.perlinShake && cc.shakeReferenceSize == TapCombatFeel.ReferenceOrtho, "FEEL camera exponential follow and Perlin shake enabled");
        check(TapCombatFeel.WorldScale > 0.1f && TapCombatFeel.WorldScale < 1f
              && Mathf.Abs(EnemyController.DefaultHitKnock - TapCombatFeel.SweepKnock * TapCombatFeel.WorldScale) < 1e-4f,
              $"FEEL knockback scaled to world (scale={TapCombatFeel.WorldScale:0.###} knock={EnemyController.DefaultHitKnock:0.###})");

        // 普攻：伤害落在动画判定帧，不是出招第 0 帧
        var e = EnemyController.CreateEnemyInstance(p.transform.position + Vector3.right * 30f, "FEEL_Target");
        yield return null;
        e.maxHp = e.currentHp = 1000; e.currentGuardBreak = 1000;
        float wait = Time.realtimeSinceStartup + 1.5f;
        while (Private<bool>(p, "isAttackingAnim") && Time.realtimeSinceStartup < wait) yield return null;
        typeof(PlayerController).GetField("attackCooldownTimer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(p, 0f);
        e.transform.position = p.transform.position + Vector3.right * (p.attackRange * 0.6f);
        p.SetAttackTarget(e);
        p.TriggerManualAttack();
        float t0 = Time.realtimeSinceStartup;
        check(Private<bool>(p, "isAttackingAnim") && e.currentHp == 1000, "FEEL attack started without frame-0 damage");
        while (e.currentHp == 1000 && Time.realtimeSinceStartup - t0 < 1f) yield return null;
        float delay = Time.realtimeSinceStartup - t0;
        check(e.currentHp < 1000 && delay > 0.12f && delay < 0.6f, $"FEEL damage lands on hero hit frame (delay={delay:0.###}s)");
        check(Private<float>(e, "hitReactTimer") > 0f, "FEEL ordinary enemy hit react started");
        Vector3 held = e.transform.position;
        yield return new WaitForSeconds(0.1f);
        check(e.aiState != EnemyController.EnemyAIState.Attack, "FEEL hit react keeps enemy from attacking");
        yield return new WaitForSeconds(0.25f);
        check(Private<float>(e, "hitReactTimer") <= 0f, "FEEL hit react expires");
        wait = Time.realtimeSinceStartup + 1.5f;
        while (Private<bool>(p, "isAttackingAnim") && Time.realtimeSinceStartup < wait) yield return null;
        check(!Private<bool>(p, "isAttackingAnim"), "FEEL attack recovery ends");
        p.ClearAttackTarget(); p.ExitCombat();
        UnityEngine.Object.Destroy(e.gameObject);
        yield return null;

        // 震屏：有偏移、按正交尺寸换算后不超过上限、结束后归零
        var cam = cc.GetComponent<Camera>();
        float limit = 0.3f * cam.orthographicSize / TapCombatFeel.ReferenceOrtho + 1e-3f;
        cc.Shake(0.3f, 0.3f);
        float maxOff = 0f; float until = Time.realtimeSinceStartup + 0.2f;
        while (Time.realtimeSinceStartup < until) { maxOff = Mathf.Max(maxOff, Private<Vector3>(cc, "shakeOffset").magnitude); yield return null; }
        check(maxOff > 1e-4f && maxOff <= limit * 1.42f, $"FEEL Perlin shake scaled to zoom (max={maxOff:0.####} limit={limit:0.####})");
        yield return new WaitForSecondsRealtime(0.25f);
        check(Private<Vector3>(cc, "shakeOffset") == Vector3.zero, "FEEL shake offset returns to zero");
    }
}
