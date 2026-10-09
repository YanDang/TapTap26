using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Automated combat self-test. Run the built player with "-gjselftest" (works with -batchmode -nographics).
    /// Drives the player through commands (not the mouse), logs PASS/FAIL to Logs/selftest.txt and quits.
    /// </summary>
    public class SelfTest : MonoBehaviour
    {
        public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), "-gjselftest") >= 0;
        static bool Shots => Array.IndexOf(Environment.GetCommandLineArgs(), "-gjshots") >= 0;

        void Shot(string name)
        {
            if (!Shots) return;
            var dir = Path.Combine(Application.dataPath, "..", "shots");
            Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            Log("  shot " + name);
        }
        readonly StringBuilder _log = new StringBuilder();
        int _pass, _fail;
        PlayerController P => GameManager.I.player;

        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            GameInput.Disabled = true;
            yield return new WaitForSecondsRealtime(0.5f);
            if (Shots)
            {
                foreach (var k in new[] { EnemyKind.Humanoid, EnemyKind.Beast })
                    GameManager.I.SpawnEnemy(k, P.Pos + (k == EnemyKind.Beast ? new Vector3(3, 0, 1) : new Vector3(-2.5f, 0, 2)));
                yield return new WaitForSecondsRealtime(0.8f);
                Shot("00_overview");
                yield return new WaitForSecondsRealtime(0.3f);
            }
            P.maxHp = P.hp = 99;
            foreach (var c in GasCyst.All.ToArray()) Destroy(c.gameObject);   // 场景里的气囊疙瘩会误炸别的测试，单独测
            if (P.hero) Log($"arm offset before any punch: shoulder {P.hero.ArmRestError.x:F4}, claw {P.hero.ArmRestError.y:F1}°");
            string[] tests = { "Move", "BeastParryExecute", "ExecuteRelocksNearby", "PendingRecovers", "DodgeArrowCombo", "DodgeOnCooldownWalksAway",
                               "HumanoidClockHit", "HumanoidClockMissLandsAtEnd", "ParryCooldown", "KickTable",
                               "HeroComboAndCharge", "ChargeInterruptsAttack", "SquidGrab", "GasCystExplodes", "SpasmMetalBounce", "MudCoralSlowShock",
                               "ValveTaunt", "SludgeBreakSplit", "FurnitureTargetWhenNoPlayer" };
            foreach (var t in tests)
            {
                Clear();
                yield return new WaitForSecondsRealtime(0.2f);
                Log($"--- {t}");
                IEnumerator r = null;
                try { r = (IEnumerator)GetType().GetMethod(t, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(this, null); }
                catch (Exception ex) { Check(false, "invoke " + ex.Message); }
                if (r != null) yield return Guard(r);
            }
            Log($"RESULT pass={_pass} fail={_fail}");
            var path = Path.Combine(Application.dataPath, "..", "selftest.txt");
            File.WriteAllText(path, _log.ToString());
            Debug.Log("[SelfTest] written " + Path.GetFullPath(path));
            yield return new WaitForSecondsRealtime(0.2f);
            Application.Quit(_fail == 0 ? 0 : 1);
        }

        IEnumerator Guard(IEnumerator r)
        {
            while (true)
            {
                object cur;
                try { if (!r.MoveNext()) yield break; cur = r.Current; }
                catch (Exception ex) { Check(false, "exception " + ex); yield break; }
                yield return cur;
            }
        }

        void Log(string s) { _log.AppendLine(s); Debug.Log("[SelfTest] " + s); }
        void Check(bool ok, string what) { if (ok) _pass++; else _fail++; Log((ok ? "PASS " : "FAIL ") + what); }

        void Clear()
        {
            foreach (var e in EnemyAI.All.ToArray()) Destroy(e.gameObject);
            if (ClockQTE.Current) ClockQTE.Current.Close();
            if (TimeManager.SlowMo) TimeManager.I.EndSlowMo();
            P.ExitCombat();
            P.state = PlayerController.State.Idle;
            if (P.cc) P.cc.enabled = true;
            P.Teleport(P.spawnPoint);
        }

        static IEnumerator Until(Func<bool> cond, float realTimeout)
        {
            float t = Time.realtimeSinceStartup + realTimeout;
            while (!cond() && Time.realtimeSinceStartup < t) yield return null;
        }

        EnemyAI Spawn(EnemyKind k, Vector3 offset, float clockChance = 0.5f)
        {
            var e = GameManager.I.SpawnEnemy(k, P.Pos + offset);
            e.clockChance = clockChance;
            e.aggroRange = 30;
            e.grabChance = 0;              // 随机抓取会打乱别的测试；抓取单独测
            return e;
        }

        // 红光抓取：1.5 s 前摇 → 缠住（不能动、别的怪不攻击、打不到）→ 3 s 内扣 3 血 → 丢出去约 3 m → 躺地起身后其他怪才攻击
        IEnumerator SquidGrab()
        {
            if (!GameManager.I.useSquidEnemies) { Log("  (skip: no squid)"); yield break; }
            P.Teleport(P.spawnPoint + new Vector3(0, 0, -4));
            var g = Spawn(EnemyKind.Beast, new Vector3(1.6f, 0, 0));
            g.maxHp = g.hp = 50; g.forceGrab = true; g.forceAttackNow = true;
            var o = Spawn(EnemyKind.Beast, new Vector3(0, 0, 2.6f));
            o.speed = 0; o.attackRange = 3.5f; o.hitRange = 4f;
            P.RightClickEnemy(g);
            yield return Until(() => g.state == EnemyAI.State.GrabWindup, 3f);
            Check(g.state == EnemyAI.State.GrabWindup, "squid starts the red grab windup");
            float w0 = Time.realtimeSinceStartup;
            Check(!g.ParryWindowOpen, "grab cannot be parried");
            yield return Until(() => P.state == PlayerController.State.Grabbed, 3f);
            float wind = Time.realtimeSinceStartup - w0;
            Check(P.state == PlayerController.State.Grabbed && Mathf.Abs(wind - g.grabWindup) < 0.2f, $"grab lands after the windup ({wind:F2} s)");
            int hp0 = P.hp;
            o.forceAttackNow = true;
            yield return new WaitForSecondsRealtime(0.6f);
            Check(o.state != EnemyAI.State.Attack && o.state != EnemyAI.State.GrabWindup, $"other squid waits while the hero is wrapped ({o.state})");
            Check(!P.TakeHit(o), "wrapped hero cannot be hit by others");
            P.RightClickGround(P.Pos + new Vector3(-3, 0, 0)); P.LeftClick(); P.LeftRelease();
            Check(P.state == PlayerController.State.Grabbed, "no input while wrapped");
            Shot("09_grabbed");
            yield return Until(() => P.state == PlayerController.State.Thrown, 4f);
            Check(P.state == PlayerController.State.Thrown && hp0 - P.hp == 3, $"thrown after the hold, 3 damage over 3 s ({hp0 - P.hp})");
            var from = P.Pos; float t0 = Time.realtimeSinceStartup;
            bool attackedEarly = false;
            while (P.state == PlayerController.State.Thrown && Time.realtimeSinceStartup - t0 < 3f)
            {
                if (o.state == EnemyAI.State.Attack || o.state == EnemyAI.State.GrabWindup) attackedEarly = true;
                yield return null;
            }
            float down = Time.realtimeSinceStartup - t0;
            Check(Mathf.Abs(DirUtil.Flat(P.Pos - from).magnitude - P.throwDistance) < 0.4f, $"thrown about a roll's distance ({DirUtil.Flat(P.Pos - from).magnitude:F2} m)");
            Check(down > 1.0f && down < 1.7f, $"lies down then slowly gets up ({down:F2} s)");
            Check(!attackedEarly, "others still waited while thrown / lying / getting up");
            yield return Until(() => o.state == EnemyAI.State.Attack || o.state == EnemyAI.State.GrabWindup, 2f);
            Check(o.state == EnemyAI.State.Attack, "after getting up, the other squid attacks");
        }

        // ------------------------------------------------------------------ tests
        IEnumerator Move()
        {
            var target = P.Pos + new Vector3(3, 0, 1);
            P.RightClickGround(target);
            yield return Until(() => P.state == PlayerController.State.Idle, 3f);
            Check(DirUtil.Flat(P.Pos - target).magnitude < 0.3f, $"right-click ground moves the player (err {DirUtil.Flat(P.Pos - target).magnitude:F2} m)");
        }

        IEnumerator ParryNow(EnemyAI b)
        {
            b.forceAttackNow = true;
            yield return Until(() => b.ParryWindowOpen && b.AttackTime > 0.15f, 6f);
            Check(b.ParryWindowOpen, "beast opens a red parry window");
            Shot("01_red_glow_window_" + Time.frameCount);
            P.LeftClick();
            yield return null;
        }

        IEnumerator BeastParryExecute()
        {
            var b = Spawn(EnemyKind.Beast, new Vector3(1.3f, 0, 0));
            P.RightClickEnemy(b);
            Check(P.InCombat && P.LockTarget == b, "right-click enemy locks on (combat state)");
            yield return ParryNow(b);
            Check(b.state == EnemyAI.State.Pending, "left-click in window -> enemy pending execution");
            Check(TimeManager.SlowMo && Mathf.Abs(Time.timeScale - 0.1f) < 0.01f, $"world slowed to 0.1x (timeScale={Time.timeScale})");
            yield return new WaitForSecondsRealtime(0.6f);
            Check(TimeManager.SlowMo, "slow-mo still active after 0.6 s real time");
            Shot("02_slowmo_pending");
            int ex = P.executions;
            P.LeftClick();
            yield return Until(() => P.executions > ex, 1f);
            Shot("03_execute_hitstop");
            yield return Until(() => b == null || b.Dead, 3f);
            Check(b == null || b.Dead, "second left-click executes the pending enemy");
            Check(P.executions == ex + 1, "execution counted (hitstop + shake path ran)");
            Check(!TimeManager.SlowMo, "slow-mo ends at the execution");
        }

        // 处决后附近还有敌人：自动锁定它、留在战斗中，且很快就能再格挡；附近没人才脱战
        IEnumerator ExecuteRelocksNearby()
        {
            var b = Spawn(EnemyKind.Beast, new Vector3(1.3f, 0, 0));
            var c = Spawn(EnemyKind.Beast, new Vector3(-2.5f, 0, 1f));
            c.speed = 0; c.attackRange = 0.1f;
            P.RightClickEnemy(b);
            yield return ParryNow(b);
            int ex = P.executions;
            P.LeftClick();
            yield return Until(() => P.executions > ex, 1f);
            float t0 = Time.realtimeSinceStartup;
            yield return Until(() => P.state != PlayerController.State.Execute, 3f);
            float busy = Time.realtimeSinceStartup - t0;
            Check(!P.hero || busy < 1.0f, $"execute ends with the bone animation ({busy:F2} s after the kill)");
            Check(P.InCombat && P.LockTarget == c, "after execution the nearby enemy is auto-locked (still in combat)");
            Check(P.state == PlayerController.State.Chase, "back to chasing the next enemy");

            Destroy(c.gameObject);
            yield return null;
            var d = Spawn(EnemyKind.Beast, new Vector3(1.3f, 0, 0));
            P.RightClickEnemy(d);
            yield return ParryNow(d);
            ex = P.executions;
            P.LeftClick();
            yield return Until(() => P.executions > ex, 1f);
            yield return Until(() => P.state != PlayerController.State.Execute, 3f);
            Check(!P.InCombat && P.state == PlayerController.State.Idle, "no enemy nearby -> leave combat after execution");
        }

        IEnumerator PendingRecovers()
        {
            var b = Spawn(EnemyKind.Beast, new Vector3(1.3f, 0, 0));
            P.RightClickEnemy(b);
            yield return ParryNow(b);
            Check(b.state == EnemyAI.State.Pending, "parried -> pending");
            float t0 = Time.realtimeSinceStartup;
            yield return Until(() => !TimeManager.SlowMo, 4f);
            float dur = Time.realtimeSinceStartup - t0;
            Check(dur > 2.6f && dur < 3.4f, $"slow-mo lasts ~3 s real time ({dur:F2} s)");
            yield return null; yield return null;
            Check(b.state != EnemyAI.State.Pending, $"no execution -> enemy leaves pending state ({b.state})");
            yield return Until(() => b.state == EnemyAI.State.Attack, 6f);
            Check(b.state == EnemyAI.State.Attack, "recovered enemy attacks again");
        }

        IEnumerator DodgeArrowCombo()
        {
            var b = Spawn(EnemyKind.Beast, new Vector3(1.6f, 0, 0));
            b.maxHp = b.hp = 20;
            P.RightClickEnemy(b);
            yield return new WaitForSecondsRealtime(0.3f);
            var start = P.Pos;
            int shots = P.arrowsShot, boom = SpiritArrow.explosions, hp0 = b.hp;
            P.RightClickGround(P.Pos + new Vector3(-4, 0, 0));
            Check(P.state == PlayerController.State.Roll, "in combat: right-click ground = dodge roll");
            Check(P.DodgeRemaining > 4.5f, $"dodge cooldown started ({P.DodgeRemaining:F1} s)");
            if (P.hero)
            {   // 寄居蟹：缩壳滚出去 → 从壳口伸臂一拳
                yield return Until(() => P.state == PlayerController.State.ShellPunch, 1.5f);
                Check(P.state == PlayerController.State.ShellPunch, "shell roll chains into arm punch");
                Check(DirUtil.Flat(P.Pos - start).magnitude > 2.0f, $"roll displaced the player ({DirUtil.Flat(P.Pos - start).magnitude:F2} m)");
                int pn = P.punches;
                yield return Until(() => P.punches > pn, 2f);
                Shot("04_shell_punch");
                Check(P.punches > pn && (b == null || b.hp < hp0), "arm punch hits the locked enemy");
                yield return Until(() => P.hero.ShellDone, 3f);
                float tOut = Time.realtimeSinceStartup;
                var before = P.Pos;
                P.RightClickGround(P.Pos + new Vector3(2, 0, 0));
                Check(P.state == PlayerController.State.ShellPunch && P.Pos == before, "climbing out of the shell: input locked");
                yield return Until(() => P.state == PlayerController.State.Idle, 3f);
                float rec = Time.realtimeSinceStartup - tOut;
                Check(rec > 0.22f && rec < 0.45f, $"emerge recovery ≈ 0.3 s ({rec:F2} s)");
                var armErr = P.hero.ArmRestError;
                Check(armErr.x < 0.001f && armErr.y < 2f, $"arm back in place after the punch (shoulder {armErr.x:F4}, claw {armErr.y:F1}°)");   // 钳子本来就有 ~0.3° 的 IK 微动
                Check(!P.InCombat && P.state == PlayerController.State.Idle, "after the punch the player leaves combat");
                yield break;
            }
            yield return Until(() => P.state == PlayerController.State.Backflip, 1.5f);
            Check(P.state == PlayerController.State.Backflip, "roll chains into backflip");
            Check(DirUtil.Flat(P.Pos - start).magnitude > 2.0f, $"roll displaced the player ({DirUtil.Flat(P.Pos - start).magnitude:F2} m)");
            yield return Until(() => P.anim.Frame >= P.bfChargeFrame + 2, 3f);
            Shot("04_backflip_charge");
            yield return Until(() => P.arrowsShot > shots, 3f);
            Check(P.arrowsShot > shots, "spirit arrow fired in the air");
            yield return Until(() => SpiritArrow.explosions > boom, 3f);
            Shot("05_arrow_explosion");
            Check(SpiritArrow.explosions > boom && (b == null || b.hp < hp0), "arrow hits and explodes (damage dealt)");
            yield return Until(() => P.state == PlayerController.State.Idle, 4f);
            Check(!P.InCombat && P.state == PlayerController.State.Idle, "after landing the player leaves combat");
        }

        IEnumerator DodgeOnCooldownWalksAway()
        {
            // cooldown is still running from the previous test
            var b = Spawn(EnemyKind.Beast, new Vector3(2f, 0, 0));
            P.RightClickEnemy(b);
            Check(P.DodgeRemaining > 0, "dodge still on cooldown");
            P.RightClickGround(P.Pos + new Vector3(-3, 0, 0));
            Check(P.state == PlayerController.State.Move && !P.InCombat, "on cooldown: right-click ground walks away and leaves combat");
            yield return null;
        }

        IEnumerator HumanoidClockHit()
        {
            var h = Spawn(EnemyKind.Humanoid, new Vector3(1.3f, 0, 0), 1f);
            P.RightClickEnemy(h);
            h.forceAttackNow = true;
            yield return Until(() => ClockQTE.Current != null, 6f);
            var c = ClockQTE.Current;
            Check(c != null, "humanoid attack pops the clock QTE");
            if (c == null) yield break;
            Log($"  sector start={c.sectorStart:F0} size={c.sectorSize:F0} duration={c.duration:F2}");
            yield return Until(() => c.HandAngle > c.sectorStart * 0.6f, 3f);
            Shot("06_clock_qte");
            yield return Until(() => c.InSector, 5f);
            P.LeftClick();
            Check(P.state == PlayerController.State.Execute, "click inside the sector -> direct execution");
            yield return Until(() => h == null || h.Dead, 3f);
            Check(h == null || h.Dead, "humanoid executed");
        }

        IEnumerator HumanoidClockMissLandsAtEnd()
        {
            var h = Spawn(EnemyKind.Humanoid, new Vector3(1.3f, 0, 0), 1f);
            P.RightClickEnemy(h);
            h.forceAttackNow = true;
            yield return Until(() => ClockQTE.Current != null, 6f);
            var c = ClockQTE.Current;
            if (c == null) { Check(false, "clock appeared"); yield break; }
            float tStart = Time.time - c.elapsed;
            int hits = P.timesHit;
            P.LeftClick();                                        // immediately: hand is before the sector
            Check(!c.Open, "early click spends the clock (miss)");
            yield return Until(() => P.timesHit > hits, 4f);
            float landed = Time.time - tStart;
            Check(P.timesHit > hits && Mathf.Abs(landed - h.clockDuration) < 0.15f,
                  $"missed clock -> attack lands when the clock ends ({landed:F2} s vs {h.clockDuration:F2} s)");
        }

        IEnumerator ParryCooldown()
        {
            P.Teleport(P.spawnPoint + new Vector3(0, 0, -4));      // open ground, no table in kick range
            var b = Spawn(EnemyKind.Beast, new Vector3(3.5f, 0, 0));
            b.speed = 0; b.attackRange = 0.1f;                    // keep it from attacking
            P.RightClickEnemy(b);
            P.state = PlayerController.State.Idle;
            yield return new WaitForSecondsRealtime(0.6f);
            int w = P.parryWhiff;
            Tap(); yield return null; Tap();
            Check(P.parryWhiff == w + 1, "second parry within 0.5 s is ignored");
            yield return new WaitForSecondsRealtime(0.55f);
            Tap();
            Check(P.parryWhiff == w + 2, "parry usable again after 0.5 s");
        }

        // 短按左键：寄居蟹主角的空挡在松开时判定
        void Tap() { P.LeftClick(); P.LeftRelease(); }

        // 气囊疙瘩：敌人走进触发范围 → 鼓胀 → 范围爆炸（伤害 + 眩晕），不伤主角；范围外的敌人不受影响
        IEnumerator GasCystExplodes()
        {
            var basePos = P.spawnPoint + new Vector3(0, 0, -5);
            P.Teleport(basePos);
            var cyst = GasCyst.Build(basePos + new Vector3(3f, 0, 0), null);
            yield return null;
            var a = Spawn(EnemyKind.Beast, new Vector3(6f, 0, 0));        // 会走进疙瘩
            var b = Spawn(EnemyKind.Beast, new Vector3(3f, 0, 1.8f));     // 在爆炸半径内
            var far = Spawn(EnemyKind.Beast, new Vector3(-3f, 0, -2.5f)); // 爆炸半径外
            b.speed = 0; far.speed = 0;
            a.hp = a.maxHp = 20; b.hp = b.maxHp = 20; far.hp = far.maxHp = 20;
            int php = P.hp;
            Check(cyst.IsIdle, "gas cyst waits idle");
            a.Teleport(cyst.transform.position + new Vector3(0.5f, 0, 0));   // 走进触发范围
            yield return Until(() => cyst.explosions > 0, 2f);
            Check(cyst.explosions == 1, "enemy entering the cyst triggers the explosion");
            Check(a.hp < 20 && b.hp < 20, $"enemies in the blast take damage ({20 - a.hp}, {20 - b.hp})");
            Check(a.state == EnemyAI.State.Stunned && b.state == EnemyAI.State.Stunned, "blasted enemies are stunned");
            Check(far.hp == 20, "enemy outside the blast is untouched");
            Check(P.hp == php, "the hero is not hurt by the cyst");
            Destroy(cyst.gameObject);
        }

        Vector3 FarFromEverything() => P.spawnPoint + new Vector3(0, 0, -5);

        // 痉挛金属团：敌人踩进去被弹飞约 3 m、麻痹；充能期间不会再弹
        IEnumerator SpasmMetalBounce()
        {
            P.Teleport(FarFromEverything());
            var m = SpasmMetal.Build(P.Pos + new Vector3(3f, 0, 0), null);
            yield return null;
            var e = Spawn(EnemyKind.Beast, new Vector3(5f, 0, 0));
            e.speed = 0; e.hp = e.maxHp = 20;
            var start = m.Pos + new Vector3(0.3f, 0, 0);
            e.Teleport(start);
            yield return Until(() => m.bounces > 0, 1f);
            yield return new WaitForSecondsRealtime(0.6f);
            float moved = DirUtil.Flat(e.Pos - start).magnitude;
            Check(m.bounces == 1 && moved > 2.2f, $"enemy stepping on the spasm metal is bounced away ({moved:F2} m)");
            Check(e.state == EnemyAI.State.Stunned, "bounced enemy is paralyzed");
            Destroy(m.gameObject);
        }

        // 烂泥珊瑚：泥潭里的敌人减速，每秒被电
        IEnumerator MudCoralSlowShock()
        {
            P.Teleport(FarFromEverything());
            var m = MudCoral.Build(P.Pos + new Vector3(4f, 0, 0), null);
            yield return null;
            var e = Spawn(EnemyKind.Beast, new Vector3(4.5f, 0, 0.5f));
            e.hp = e.maxHp = 20; e.attackRange = 0.1f; e.speed = 0;
            yield return new WaitForSecondsRealtime(0.3f);
            Check(e.SpeedMul < 0.8f, $"enemy in the mud pool is slowed (x{e.SpeedMul:F2})");
            int hp0 = e.hp;
            yield return new WaitForSecondsRealtime(2.2f);
            Check(m.shocks >= 2 && e.hp <= hp0 - 2, $"enemy in the mud pool is shocked every second ({m.shocks} shocks)");
            Destroy(m.gameObject);
        }

        // 嘶鸣排气瓣：尖啸时把范围内的怪拉过去打它，就算玩家就在旁边也不打玩家；排气瓣坏了才回头打玩家
        IEnumerator ValveTaunt()
        {
            P.Teleport(FarFromEverything());
            var v = ScreamingValve.Build(P.Pos + new Vector3(-3.5f, 0, 0), null);
            v.respawnSeconds = 0f; v.maxHp = v.hp = 3;           // 测试用：3 下打坏
            yield return null;
            var e = Spawn(EnemyKind.Beast, new Vector3(1.4f, 0, 0));    // 玩家就在 1.4 m 外
            e.hp = e.maxHp = 30;
            v.ScreamNow();
            yield return Until(() => e.furnitureTarget == v, 1f);
            Check(e.furnitureTarget == v, "screaming valve taunts the nearby squid");
            int hits = P.timesHit;
            yield return Until(() => !v.Alive, 15f);
            Check(!v.Alive && v.timesHit >= v.maxHp, $"taunted squid attacks the valve until it breaks ({v.timesHit} hits)");
            Check(P.timesHit == hits, "taunted squid never attacked the hero next to it");
            // 打坏排气瓣的那一招收完之后，才会对主角发起新的攻击
            yield return Until(() => e.furnitureTarget == null && e.state == EnemyAI.State.Attack, 8f);
            Check(e.furnitureTarget == null && e.state == EnemyAI.State.Attack, "after the valve breaks it goes back to the hero");
            Destroy(v.gameObject);
        }

        // 共生残渣：会自愈；打坏后分裂成 3 块减速碎块
        IEnumerator SludgeBreakSplit()
        {
            P.Teleport(FarFromEverything());
            var s = SymbioticSludge.Build(P.Pos + new Vector3(3f, 0, 0), null);
            yield return null;
            s.HitByEnemy(null, 3);
            int hurt = s.hp;
            yield return new WaitForSecondsRealtime(s.regenInterval + 0.2f);
            Check(s.hp > hurt, $"sludge regenerates ({hurt} -> {s.hp})");
            while (s.Alive) s.HitByEnemy(null, 5);
            Check(s.ChunkCount == 3, "broken sludge splits into 3 slowing chunks");
            var e = Spawn(EnemyKind.Beast, new Vector3(4f, 0, 0));
            e.speed = 0; e.attackRange = 0.1f;
            yield return null;
            var chunk = GameObject.Find("SludgeChunk");
            if (chunk) e.Teleport(chunk.transform.position);
            yield return new WaitForSecondsRealtime(0.2f);
            Check(e.SpeedMul < 0.8f, $"enemy on a sludge chunk is slowed (x{e.SpeedMul:F2})");
            Destroy(s.gameObject);
        }

        // 检测范围内没有玩家 → 去打家具；锁定家具后玩家走近也不理
        IEnumerator FurnitureTargetWhenNoPlayer()
        {
            P.Teleport(FarFromEverything());
            var home = P.Pos;
            var s = SymbioticSludge.Build(home + new Vector3(8f, 0, 0), null);
            s.maxHp = s.hp = 40; s.regenInterval = 999f;
            yield return null;
            var e = Spawn(EnemyKind.Beast, new Vector3(11f, 0, 0));
            e.aggroRange = 5f;                                   // 玩家在 11 m 外，检测不到
            yield return Until(() => e.furnitureTarget == s, 2f);
            Check(e.furnitureTarget == s, "no hero in range -> squid locks the nearby furniture");
            yield return Until(() => s.timesHit > 0, 8f);
            Check(s.timesHit > 0, "squid attacks the furniture");
            int hits = P.timesHit;
            P.Teleport(e.Pos + new Vector3(-1.2f, 0, 0));        // 玩家走到它身边
            yield return new WaitForSecondsRealtime(3f);
            Check(e.furnitureTarget == s && P.timesHit == hits, "locked on furniture: ignores the hero standing next to it");
            Destroy(s.gameObject);
        }

        // 战斗中普攻出招时按住左键：0.18 s 后直接打断普攻开始蓄力；桌子旁短按仍是踢桌子
        IEnumerator ChargeInterruptsAttack()
        {
            if (!P.hero) { Log("  (skip: old sprite hero)"); yield break; }
            P.Teleport(P.spawnPoint + new Vector3(0, 0, -4));
            var b = Spawn(EnemyKind.Beast, new Vector3(1.3f, 0, 0));
            b.maxHp = b.hp = 40; b.speed = 0; b.attackRange = 0.1f;
            P.RightClickEnemy(b);
            yield return Until(() => P.state == PlayerController.State.Attack, 3f);
            Check(P.state == PlayerController.State.Attack, "auto attack in progress");
            P.LeftClick();
            yield return new WaitForSecondsRealtime(P.chargeHoldDelay + 0.08f);
            Check(P.state == PlayerController.State.Charge, $"holding during an attack starts the charge right away ({P.state})");
            P.LeftRelease();
            yield return Until(() => P.state == PlayerController.State.Chase, 2f);

            KickableTable table = null;
            foreach (var t in KickableTable.All) if (t.IsIdle) { table = t; break; }
            if (!table) yield break;
            P.Teleport(table.transform.position - Vector3.right * 1.1f);
            b.Teleport(P.Pos + new Vector3(0, 0, 1.4f));
            yield return Until(() => P.state == PlayerController.State.Chase, 2f);
            int kicks = P.kicks;
            P.LeftClick();
            Check(P.kicks == kicks && P.state != PlayerController.State.Kick, "in combat next to a table: press alone does not kick yet");
            P.LeftRelease();
            yield return new WaitForSecondsRealtime(0.3f);
            Check(P.kicks == kicks + 1, "short tap next to a table still kicks it");
        }

        // 寄居蟹主角：普攻三连能打到人；按住左键蓄力 → 松开前扑砸击，蓄满伤害更高
        IEnumerator HeroComboAndCharge()
        {
            if (!P.hero) { Log("  (skip: old sprite hero)"); yield break; }
            P.Teleport(P.spawnPoint + new Vector3(0, 0, -4));
            var b = Spawn(EnemyKind.Beast, new Vector3(1.3f, 0, 0));
            b.maxHp = b.hp = 40; b.speed = 0; b.attackRange = 0.1f;
            P.RightClickEnemy(b);
            int hp0 = b.hp;
            yield return Until(() => b.hp <= hp0 - 3, 5f);
            Check(b.hp <= hp0 - 3, $"auto combo (sweep/thrust/stomp) deals damage ({hp0 - b.hp})");
            yield return Until(() => P.state == PlayerController.State.Chase, 2f);
            int s0 = P.smashes, hp1 = b.hp;
            P.LeftClick();
            yield return Until(() => P.state == PlayerController.State.Charge, 1.5f);
            Check(P.state == PlayerController.State.Charge, "holding left = charge");
            yield return new WaitForSecondsRealtime(P.chargeTime + 0.1f);
            Shot("08_charge_full");
            P.LeftRelease();
            Check(P.state == PlayerController.State.Smash, "release = smash");
            yield return Until(() => P.smashes > s0, 1.5f);
            Check(P.smashes > s0 && P.lastSmashLevel > 0.95f && hp1 - b.hp >= 4, $"full charge smash hits hard (level {P.lastSmashLevel:F2}, dmg {hp1 - b.hp})");
            yield return Until(() => P.state != PlayerController.State.Smash, 2f);
            Check(P.state == PlayerController.State.Chase, "back to auto combat after smash");
        }

        IEnumerator KickTable()
        {
            KickableTable table = null;
            foreach (var t in KickableTable.All) if (t.IsIdle) { table = t; break; }
            if (!table) { Check(false, "found a table"); yield break; }
            var dir = Vector3.right;
            P.Teleport(table.transform.position - dir * 1.1f);
            var e = Spawn(EnemyKind.Beast, (table.transform.position + dir * 5.2f) - P.Pos);
            e.speed = 0;
            int land = table.landings, kicks = P.kicks;
            P.LeftClick();
            yield return new WaitForSecondsRealtime(0.25f);
            Shot("07_table_flying");
            yield return Until(() => table.landings > land, 2f);
            Check(P.kicks == kicks + 1 && table.landings > land, "left-click next to a table kicks it and it lands");
            Check(e == null || e.Dead || e.state == EnemyAI.State.Stunned, $"enemy at the landing spot is stunned ({(e ? e.state.ToString() : "dead")})");
        }
    }
}
