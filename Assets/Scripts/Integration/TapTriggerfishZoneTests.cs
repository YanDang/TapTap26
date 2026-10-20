using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 扳机鱼危险黄框回归自测：showAttackRange 必须为 false（与乌贼一致），
/// 强制出招的整个攻击状态里 Danger_Zone_Indicator 子物体都不能被激活，
/// 只有怪物自身黄光预警（ApplyFlash）。无条件运行（baseline / -tapnofeel 也覆盖）。
/// </summary>
public static class TapTriggerfishZoneTests
{
    public static IEnumerator Run(Action<bool, string> check)
    {
        var p = PlayerController.Instance;
        if (p == null) { check(false, "ZONE player available"); yield break; }

        // 刷一只真扳机鱼（Resources/Enemies/Enemy_Triggerfish.prefab）
        var e = EnemyController.CreateTriggerfishInstance(p.transform.position + Vector3.right * 30f, "ZONE_Triggerfish");
        yield return null;
        check(e != null && e.gameObject.name.StartsWith("ZONE_Triggerfish"), "ZONE triggerfish instance spawned");
        if (e == null) yield break;
        e.maxHp = e.currentHp = 1000f;
        e.currentGuardBreak = 1000f;

        check(!e.showAttackRange, "ZONE triggerfish showAttackRange is false (like squid)");

        var zone = e.transform.Find("Danger_Zone_Indicator");
        check(zone != null && !zone.gameObject.activeSelf, "ZONE danger indicator exists and starts inactive");

        // 强制出招：关掉时钟 QTE 随机性后直接调私有 StartNormalAttack，与真实出招走同一条路径
        e.enableClockQTE = false;
        typeof(EnemyController).GetMethod("StartNormalAttack", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(e, new object[] { false });
        yield return null;
        check(e.aiState == EnemyController.EnemyAIState.Attack, "ZONE triggerfish entered attack state");

        bool zoneStayedOff = zone != null && !zone.gameObject.activeSelf;
        float deadline = Time.realtimeSinceStartup + 0.6f;
        while (e != null && e.aiState == EnemyController.EnemyAIState.Attack && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            if (zone != null && zone.gameObject.activeSelf) zoneStayedOff = false;
        }
        check(zoneStayedOff, "ZONE danger indicator never activates during attack frames (no yellow box)");

        if (e != null) UnityEngine.Object.Destroy(e.gameObject);
        yield return null;
    }
}
