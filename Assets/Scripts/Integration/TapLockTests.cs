using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// TapCombatFeel 锁定追击自测（A-F）：
/// 右键按住不误走、锁定后直线追击、锁定优先不打旁边的怪、攻击帧不滑步、锁定目标死亡后停手。
/// 只在 -tapbaseline / -tapnofeel 之外的正常构建里运行（与 TapFeelTests 同一门控）。
/// </summary>
public static class TapLockTests
{
    static T Private<T>(object o, string f) => (T)o.GetType().GetField(f, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    static void SetPrivate(object o, string f, object v) => o.GetType().GetField(f, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, v);

    /// <summary>与 PlayerController.CanStepTo / MoveWithInput 相同的可走判定。</summary>
    static bool Walkable(PlayerController p, Vector3 world)
    {
        if (p.grid == null) return true;
        Vector3Int cell = p.grid.WorldToCell(new Vector3(world.x, world.y - p.groundHeightOffset, 0f));
        if (p.groundTilemap != null && !p.groundTilemap.HasTile(cell)) return false;
        if (p.buildTilemap != null && p.buildTilemap != p.groundTilemap && p.buildTilemap.HasTile(cell)) return false;
        if (IsometricPathfinder.DynamicBlockedCells.Contains(cell)) return false;
        return true;
    }

    static bool ClearLine(PlayerController p, Vector3 origin, Vector3 dir, float length)
    {
        for (float d = 0.2f; d <= length; d += 0.1f)
            if (!Walkable(p, origin + dir * d)) return false;
        return true;
    }

    /// <summary>找一条足够长、整段可走的直线通道，保证测到的是“直线追击”而不是绕障寻路。</summary>
    static Vector3 FindClearDir(PlayerController p, float length)
    {
        Vector3[] dirs =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down,
            (Vector3.right + Vector3.up).normalized, (Vector3.left + Vector3.up).normalized,
            (Vector3.right + Vector3.down).normalized, (Vector3.left + Vector3.down).normalized
        };
        for (int i = 0; i < dirs.Length; i++)
            if (ClearLine(p, p.transform.position, dirs[i], length + 0.3f)) return dirs[i];
        return Vector3.zero;
    }

    static EnemyController Spawn(PlayerController p, Vector3 at, string name)
    {
        var e = EnemyController.CreateEnemyInstance(at, name);
        e.maxHp = e.currentHp = 1000f;
        e.currentGuardBreak = 1000f;
        e.ExternalAI = (enemy, dt) => { };   // 静止靶子：外部 AI 独占，敌人不追不打，只测玩家移动
        return e;
    }

    static void StopAll(PlayerController p, float cooldown = 0f)
    {
        p.StopMovement();
        p.ClearAttackTarget();
        p.ExitCombat();
        SetPrivate(p, "attackCooldownTimer", cooldown);
    }

    public static IEnumerator Run(Action<bool, string> check)
    {
        var p = PlayerController.Instance;
        if (p == null) { check(false, "LOCK player available"); yield break; }

        // 点击寻路测试需要 TapToMove；若玩家设置过摇杆模式，测试期间临时切回并在结束时还原。
        var savedMode = PlayerSessionData.CurrentMobileControlMode;
        bool restoreMode = savedMode != PlayerSessionData.MobileControlMode.TapToMove;
        if (restoreMode) PlayerSessionData.CurrentMobileControlMode = PlayerSessionData.MobileControlMode.TapToMove;

        StopAll(p);
        Vector3 dir = FindClearDir(p, p.attackRange * 3.1f);
        check(dir != Vector3.zero, "LOCK straight walkable lane found");
        if (dir == Vector3.zero) dir = Vector3.right;

        // ---------------------------------------------------------------- B: 锁定后直线追击
        var chaseTarget = Spawn(p, p.transform.position + dir * (p.attackRange * 3f), "LOCK_Chase");
        yield return null;
        SetPrivate(p, "attackCooldownTimer", 5f);   // 到位先不出招，避免击退把靶子推远导致再次起步
        p.RightClickEnemy(chaseTarget);

        float startDist = Vector3.Distance(p.transform.position, chaseTarget.transform.position);
        float prevDist = startDist;
        bool monotonic = true, heading = true;
        float chaseDeadline = Time.realtimeSinceStartup + 4f;
        while (Time.realtimeSinceStartup < chaseDeadline && prevDist > p.attackRange * 0.9f)
        {
            Vector3 before = p.transform.position;
            Vector3 toTarget = chaseTarget.transform.position - before;
            toTarget.z = 0f;
            yield return null;
            Vector3 step = p.transform.position - before;
            float dist = Vector3.Distance(p.transform.position, chaseTarget.transform.position);
            if (dist > prevDist + 1e-3f) monotonic = false;
            if (step.magnitude > 1e-5f && Vector3.Angle(step, toTarget) > 30f) heading = false;
            prevDist = dist;
        }
        check(monotonic && prevDist < startDist - 0.5f, $"LOCK chase distance decreases monotonically ({startDist:0.##}->{prevDist:0.##})");
        check(heading, "LOCK every chase step points at the target (<30 deg)");
        check(prevDist <= p.attackRange * 0.9f + 0.05f, $"LOCK chase stops at attack range (dist={prevDist:0.###} range={p.attackRange:0.###})");
        Vector3 arrived = p.transform.position;
        yield return new WaitForSeconds(0.3f);
        check(Vector3.Distance(arrived, p.transform.position) < 0.02f, "LOCK chase holds position after arriving (0.3s drift < 0.02)");
        StopAll(p);
        UnityEngine.Object.Destroy(chaseTarget.gameObject);
        yield return null;

        // ------------------------------------------------- C: 锁定 A 时，旁边更近的 B 不会被改锁 / 掉血
        var lockA = Spawn(p, p.transform.position + dir * (p.attackRange * 3f), "LOCK_A");
        Vector3 perp = new Vector3(dir.y, -dir.x, 0f);
        var lockB = Spawn(p, p.transform.position + perp * (p.attackRange * 0.5f), "LOCK_B");
        SetPrivate(p, "attackCooldownTimer", 5f);   // 布置帧别自动出招，避免污染下一步
        yield return null;
        SetPrivate(p, "attackCooldownTimer", 0f);   // 旧逻辑会在这一帧改锁 B 并出招
        float bHp = lockB.currentHp;
        p.RightClickEnemy(lockA);
        yield return new WaitForSeconds(0.25f);
        check(p.currentTarget == lockA, "LOCK keeps the right-clicked target A instead of nearer B");
        check(lockB.currentHp == bHp, "LOCK nearer enemy B takes no damage while A is locked");
        StopAll(p);
        UnityEngine.Object.Destroy(lockA.gameObject);
        UnityEngine.Object.Destroy(lockB.gameObject);
        yield return null;

        // ------------------------------------------------- D: 出招瞬间停步，攻击帧内位置完全不变
        var attackTarget = Spawn(p, p.transform.position + dir * (p.attackRange * 0.6f), "LOCK_Attack");
        SetPrivate(p, "attackCooldownTimer", 5f);   // 进帧后不要自动出招，手动触发本组测试
        yield return null;
        p.SetAttackTarget(attackTarget);
        SetPrivate(p, "attackCooldownTimer", 0f);
        p.MoveToWorldPoint(p.transform.position + dir * (p.attackRange * 2f));   // 先挂一条会滑步的路径
        bool pathBeforeAttack = Private<bool>(p, "isMoving");
        p.TriggerManualAttack();
        bool attacking = Private<bool>(p, "isAttackingAnim");
        check(attacking, "LOCK manual attack starts");
        check(pathBeforeAttack, "LOCK pending walk path was active before the attack");
        check(!Private<bool>(p, "isMoving"), "LOCK attack start cancels the pending walk path");
        Vector3 attackPos = p.transform.position;
        float maxDrift = 0f;
        float attackDeadline = Time.realtimeSinceStartup + 2f;
        while (Private<bool>(p, "isAttackingAnim") && Time.realtimeSinceStartup < attackDeadline)
        {
            yield return null;
            maxDrift = Mathf.Max(maxDrift, Vector3.Distance(attackPos, p.transform.position));
        }
        check(!Private<bool>(p, "isAttackingAnim"), "LOCK attack animation ends");
        check(maxDrift < 0.001f, $"LOCK no sliding during attack frames (max drift={maxDrift:0.####})");
        StopAll(p);
        UnityEngine.Object.Destroy(attackTarget.gameObject);
        yield return null;

        // ------------------------------------------------- A: 右键按住 —— 起点在怪上不寻路，起点在地面才寻路
        var holdEnemy = Spawn(p, p.transform.position + dir * (p.attackRange * 0.5f), "LOCK_Hold");
        yield return null;
        p.StopMovement();
        p.ClearAttackTarget();
        p.ExitCombat();
        p.HandleRightPress(true, holdEnemy.transform.position);
        check(p.currentTarget == holdEnemy, "LOCK right press on enemy locks it");
        check(Private<float>(p, "rightClickHoldTimer") == 0f, "LOCK right press on enemy clears the hold timer");
        for (int i = 0; i < 5; i++) p.HandleRightHold(0.1f, holdEnemy.transform.position);   // 按住 0.5 s
        check(!Private<bool>(p, "isMoving"), "LOCK hold begun on enemy never pathfinds to the mouse");
        StopAll(p);
        UnityEngine.Object.Destroy(holdEnemy.gameObject);
        yield return null;

        Vector3 groundPoint = p.transform.position + dir * (p.attackRange * 1.5f);
        p.HandleRightPress(false, groundPoint);
        p.StopMovement();   // 清掉按下瞬间的寻路，只验证“按住 0.1 s”这一下
        check(!Private<bool>(p, "isMoving"), "LOCK ground press path cleared before hold probe");
        p.HandleRightHold(0.1f, groundPoint);
        check(Private<bool>(p, "isMoving"), "LOCK hold begun on ground pathfinds after 0.1s");
        StopAll(p);
        yield return null;

        // ------------------------------------------------- F: 锁定目标死亡 → 清锁定、停止追击、不改追旁边的怪
        var dieTarget = Spawn(p, p.transform.position + dir * (p.attackRange * 3f), "LOCK_Die");
        var bystander = Spawn(p, p.transform.position - dir * (p.attackRange * 2f), "LOCK_Bystander");
        yield return null;
        SetPrivate(p, "attackCooldownTimer", 5f);
        p.RightClickEnemy(dieTarget);
        yield return null;
        yield return null;   // 让追击先跑起来
        dieTarget.TakeDamage(999999f, 0f, p.transform.position);
        yield return null;
        yield return null;
        check(p.currentTarget == null, "LOCK dead target is cleared (no auto re-lock to bystander)");
        Vector3 deadPos = p.transform.position;
        yield return new WaitForSeconds(0.3f);
        check(Vector3.Distance(deadPos, p.transform.position) < 0.02f, "LOCK player stops chasing after the locked target dies");
        check(p.currentTarget == null && bystander.IsAlive, "LOCK bystander stays alive and unselected");
        StopAll(p);
        UnityEngine.Object.Destroy(dieTarget.gameObject);
        UnityEngine.Object.Destroy(bystander.gameObject);
        yield return null;

        if (restoreMode) PlayerSessionData.CurrentMobileControlMode = savedMode;
        StopAll(p);
    }
}
