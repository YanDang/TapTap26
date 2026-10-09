using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    public enum EnemyKind { Humanoid, Beast }

    /// <summary>
    /// Beast (non-humanoid): attack glows red; parry inside 0.05–0.5 s -> Pending (execution-ready) while the world
    ///   is slowed. If slow-mo ends without an execution the enemy recovers to normal (otherwise it would never attack again).
    /// Humanoid: each attack has clockChance to pop a clock QTE; the attack is slowed so it lands exactly when the
    ///   clock's hand completes the turn. Hitting the sector executes directly (no pending state).
    /// Runs on world time (affected by slow-mo and hitstop).
    /// 鱿鱼（squid != null）：普通攻击 = 黄光预警（可格挡）；抓取 = 红光一直闪 + 触手狂舞，grabWindup 秒后缠住主角
    ///   （不能格挡，只能翻滚躲开 / 拉开距离），缠住 grabHold 秒、每 grabTick 秒扣血，然后丢出去。
    ///   主角被缠 / 被丢 / 起身期间（PlayerController.Unavailable）其他怪不发起攻击，同一时间只有一只在抓。
    /// 目标（ThinkTarget，用户 2026-10-09）：嘲讽中的家具（排气瓣）在范围内 → 打它；否则检测范围（aggroRange）内有玩家 → 打玩家；
    ///   都没有 → 去打 furnitureSearchRange 内最近的战斗家具。锁定家具后打到它坏为止，玩家走近也不理（嘲讽可以改锁另一件家具）。
    /// </summary>
    public class EnemyAI : Unit
    {
        public enum State { Idle, Chase, Attack, Recover, Stagger, Stunned, Pending, Executing, Dead, GrabWindup, Grabbing }

        public static readonly List<EnemyAI> All = new List<EnemyAI>();
        public static EnemyAI Grabber;                     // 正在抓（前摇或缠住）的那只
        public static event System.Action<EnemyAI> Killed;
        public static int AliveCount { get { int n = 0; foreach (var e in All) if (!e.Dead) n++; return n; } }

        [Header("Tuning")]
        public EnemyKind kind = EnemyKind.Beast;
        public float speed = 2.2f;
        public float attackRange = 1.6f;
        public float hitRange = 2.2f;
        public float aggroRange = 7f;
        public Vector2 attackCooldown = new Vector2(2.0f, 3.2f);
        [Range(0, 1)] public float clockChance = 0.5f;   // stronger enemies: lower
        public float clockDuration = 1.6f;
        public float normalImpact = 0.6f;                 // seconds from attack start to the hit
        public float windowStart = 0.05f, windowEnd = 0.5f;
        const float ImpactNorm = 0.45f;
        [Header("抓取（鱿鱼）")]
        [Range(0, 1)] public float grabChance = 0.3f;
        public float grabWindup = 1.5f, grabRange = 2.6f, grabHold = 3f, grabTick = 1f;
        public int grabDamage = 1;
        public bool forceGrab;                             // self-test / 演示：下一次攻击必定是抓取
        public static readonly Color AttackGlow = new Color(1f, 0.8f, 0.08f), GrabGlow = new Color(1f, 0.08f, 0.05f),
                                     PendingGlow = new Color(0.55f, 0.9f, 1f);                   // where the hammer lands inside the Attack_Slam clip

        public State state = State.Idle;
        public PlayerController player;
        public bool forceAttackNow;                        // self-test hook
        public SquidAnimator squid;                        // 鱿鱼外观（GameManager.useSquidEnemies）；为空 = 旧序列帧
        public SquidFace squidFace;
        [Header("家具目标")]
        public float furnitureSearchRange = 9f;           // 检测范围内没有玩家时，去打这个范围内的家具
        public CombatFurniture furnitureTarget;            // 锁定的家具（非空时只打它）

        float _t, _impactT, _attackEnd, _nextAttack, _tick;
        float _slowUntil = -1f, _slowFactor = 1f;
        Vector3 _knockVel;

        /// <summary>减速：速度乘以 factor，持续 seconds 秒（多个来源取最慢的）。</summary>
        public void Slow(float factor, float seconds)
        {
            if (Time.time >= _slowUntil) _slowFactor = factor; else _slowFactor = Mathf.Min(_slowFactor, factor);
            _slowUntil = Mathf.Max(_slowUntil, Time.time + seconds);
        }
        public float SpeedMul => Time.time < _slowUntil ? _slowFactor : 1f;

        /// <summary>被弹飞：沿 dir 滑出约 distance 米（指数减速）。</summary>
        public void Knockback(Vector3 dir, float distance)
        {
            _knockVel = DirUtil.Flat(dir).normalized * distance * 8f;
        }

        // 已经在追玩家时放宽到 1.6 倍才放弃（避免玩家在边界来回时怪物在玩家和家具之间反复横跳）
        bool PlayerInRange => player && !player.Dead &&
                              (DirUtil.Flat(player.Pos - Pos).magnitude < aggroRange * (state == State.Chase && !furnitureTarget ? 1.6f : 1f) ||
                               player.LockTarget == this);

        /// <summary>选目标。返回有没有目标（家具或玩家）。</summary>
        bool ThinkTarget()
        {
            if (furnitureTarget && !furnitureTarget.Targetable) furnitureTarget = null;
            var taunt = CombatFurniture.TauntingNear(Pos);
            if (taunt) furnitureTarget = taunt;                         // 嘲讽：强行吸引（可以从玩家或别的家具转过来）
            if (furnitureTarget) return true;                           // 已锁定家具：玩家来了也不理
            if (PlayerInRange) return true;
            furnitureTarget = CombatFurniture.NearestTargetable(Pos, furnitureSearchRange);
            return furnitureTarget != null;
        }

        Vector3 TargetPos => furnitureTarget ? furnitureTarget.Pos : player ? player.Pos : Pos;
        bool _impactDone;
        ClockQTE _clock;
        GameObject _stars;

        public bool ParryWindowOpen => state == State.Attack && kind == EnemyKind.Beast && _t >= windowStart && _t <= windowEnd;
        public bool CanBeAutoAttacked => state != State.Pending && state != State.Executing && !Dead;
        public float AttackTime => _t;
        public bool IsAngry => state == State.Attack || state == State.GrabWindup || state == State.Grabbing;
        public ClockQTE Clock => _clock;

        protected override void Awake()
        {
            base.Awake();
            All.Add(this);
            _nextAttack = Time.time + Random.Range(1.0f, 2.0f);
        }

        void OnDestroy()
        {
            All.Remove(this);
            if (Grabber == this) Grabber = null;
            CloseClock();
            if (_stars) Destroy(_stars);
        }

        public static EnemyAI FindPending()
        {
            foreach (var e in All) if (e.state == State.Pending) return e;
            return null;
        }

        public static EnemyAI FindParryable(PlayerController p)
        {
            EnemyAI best = null; float bd = float.MaxValue;
            foreach (var e in All)
            {
                if (!e.ParryWindowOpen) continue;
                float d = DirUtil.Flat(e.Pos - p.Pos).magnitude;
                if (d <= e.hitRange + 0.4f && d < bd) { best = e; bd = d; }
            }
            return best;
        }

        // ------------------------------------------------------------------ external events
        public void Parried()
        {
            state = State.Pending;
            CloseClock();
            anim.Play("Stun", facing, true, 1f);
            Flash(Color.white, 0.2f);
        }

        public void BeginExecuted()
        {
            state = State.Executing;
            CloseClock();
            anim.Play("Hit", facing, true, 0.3f);
        }

        public void KillByExecution(Vector3 dir)
        {
            hp = 0;
            Flash(Color.white, 0.3f);
            Die();
        }

        public void TakeDamage(int dmg, Vector3 dir, float knock, bool interrupt)
        {
            if (Dead || state == State.Executing) return;
            hp -= dmg;
            Flash(Color.white, 0.12f);
            if (knock > 0) MoveBy(DirUtil.Flat(dir).normalized * knock);
            if (hp <= 0) { hp = 0; Die(); return; }
            if (state == State.Grabbing) return;           // 缠住期间不会被打断（掉血照算）
            bool attacking = state == State.Attack || state == State.GrabWindup;   // 抓取前摇：只有重击（打断型）能打断
            if (state == State.Pending) return;            // keep it execution-ready
            if (!attacking || interrupt)
            {
                CloseClock();
                CancelGrab();
                state = State.Stagger; _t = 0.2f;          // 0.2 s stagger
                if (squid) squid.Interrupt();
                anim.Play("Hit", facing, true);
            }
        }

        public void Stun(float seconds)
        {
            if (Dead || state == State.Executing || state == State.Pending || state == State.Grabbing) return;
            CloseClock();
            CancelGrab();
            state = State.Stunned; _t = seconds;
            anim.Play("Stun", facing, true, 1f);
            if (_stars) Destroy(_stars);
            _stars = Vfx.Spawn("VFX_StunStars", Pos + Vector3.up * 2.3f, 0, 1f, false, transform);
        }

        void Die()
        {
            if (state == State.Grabbing && player) player.ThrowFrom(this);
            CancelGrab();
            state = State.Dead; _t = 0;
            Killed?.Invoke(this);
            CloseClock();
            if (_stars) Destroy(_stars);
            if (cc) cc.enabled = false;
            anim.Play("Death", facing, true);
            if (squid) squid.Die(2.2f);
        }

        void CloseClock()
        {
            if (_clock) _clock.Close();
            _clock = null;
        }

        /// <summary>主角被别的鱿鱼缠住：正在进行的普通攻击取消，回去待命（等主角起身再打）。</summary>
        public void YieldToGrab()
        {
            if (state != State.Attack) return;
            CloseClock();
            if (squid) squid.Interrupt();
            state = State.Chase;
            _nextAttack = Time.time + 0.5f;
        }

        void CancelGrab()
        {
            if (Grabber == this) Grabber = null;
            if (squid && (state == State.GrabWindup || state == State.Grabbing)) { squid.EndFrenzy(); squid.GrabRelease(); }
        }

        int SideToPlayer()
        {
            var cam = Camera.main;
            return Vector3.Dot(player.Pos - Pos, cam ? cam.transform.right : Vector3.right) >= 0f ? 1 : -1;
        }

        void StartGrab()
        {
            state = State.GrabWindup; _t = 0;
            Grabber = this;
            Face(player.Pos - Pos);
            anim.Play("Idle", facing, true);
            squid.BeginFrenzy();
            Sfx.Play("sfx_enemy_windup", 1f);
        }

        // ------------------------------------------------------------------ AI
        void StartAttack(bool allowGrab = true)
        {
            if (!furnitureTarget && kind == EnemyKind.Beast && squid && Grabber == null && player && !player.Unavailable &&
                (forceGrab || (allowGrab && Random.value < grabChance)))
            {
                forceGrab = false;
                StartGrab();
                return;
            }
            state = State.Attack; _t = 0; _impactDone = false;
            Face(TargetPos - Pos);
            bool useClock = !furnitureTarget && kind == EnemyKind.Humanoid && Random.value < clockChance;
            _impactT = useClock ? clockDuration : normalImpact;
            var clip = anim.set.Get("Attack_Slam");
            float clipDur = clip.frames / clip.fps;
            float spd = ImpactNorm * clipDur / _impactT;     // stretch the swing so the hammer lands at _impactT
            _attackEnd = clipDur / spd;
            anim.Play("Attack_Slam", facing, true, spd);
            if (squid)
            {
                var cam = Camera.main;
                float side = Vector3.Dot(TargetPos - Pos, cam ? cam.transform.right : Vector3.right);
                squid.Attack(side >= 0 ? 1 : -1, -1, _impactT);
            }
            Sfx.Play("sfx_enemy_windup", 0.85f);
            if (useClock)
            {
                _clock = ClockQTE.Show(this, clockDuration);
                Sfx.Play("sfx_sword_draw", 0.9f, true);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!player) player = GameManager.I ? GameManager.I.player : null;
            if (_knockVel.sqrMagnitude > 0.01f && !Dead)
            {   // 被弹飞的滑行
                MoveBy(_knockVel * dt);
                _knockVel *= Mathf.Exp(-8f * dt);
            }
            float glow = 0;

            switch (state)
            {
                case State.Idle:
                    anim.Play("Idle", facing);
                    if (ThinkTarget()) state = State.Chase;
                    break;

                case State.Chase:
                {
                    if (!ThinkTarget()) { state = State.Idle; break; }
                    if (furnitureTarget)
                    {   // 打家具：走到家具旁边就打（家具本身有半径），不会抓取
                        var fv = DirUtil.Flat(furnitureTarget.Pos - Pos);
                        Face(fv);
                        if (fv.magnitude > attackRange + furnitureTarget.hitRadius)
                        {
                            MoveBy((fv.normalized * speed * SpeedMul + Separation() + AvoidPlayer(fv)) * dt);
                            anim.Play("Walk", facing);
                        }
                        else
                        {
                            MoveBy(Separation() * dt);
                            anim.Play("Idle", facing);
                            if (Time.time >= _nextAttack) StartAttack(false);
                        }
                        break;
                    }
                    var v = DirUtil.Flat(player.Pos - Pos);
                    Face(v);
                    if (v.magnitude > attackRange)
                    {
                        MoveBy((v.normalized * speed * SpeedMul + Separation()) * dt);
                        anim.Play("Walk", facing);
                    }
                    else
                    {
                        MoveBy(Separation() * dt);
                        anim.Play("Idle", facing);
                        // 主角被缠 / 被丢 / 起身中：其他怪先等着（forceAttackNow 也保留到主角能动）
                        if ((Time.time >= _nextAttack || forceAttackNow) && !player.Unavailable)
                        {
                            bool forced = forceAttackNow; forceAttackNow = false;
                            StartAttack(!forced);   // 自测强制的攻击一律是普通攻击（除非 forceGrab）
                        }
                    }
                    break;
                }

                case State.Attack:
                    _t += dt;
                    if (kind == EnemyKind.Beast && _t <= windowEnd + 0.05f)
                        glow = 0.55f + 0.25f * Mathf.Sin(Time.time * 30f);       // 黄光预警 = 可格挡的普通攻击
                    if (!_impactDone && _t >= _impactT)
                    {
                        _impactDone = true;
                        CloseClock();
                        if (furnitureTarget)
                        {   // 打家具：不会顺手打到玩家
                            if (DirUtil.Flat(furnitureTarget.Pos - Pos).magnitude <= hitRange + furnitureTarget.hitRadius)
                                furnitureTarget.HitByEnemy(this, 1);
                        }
                        else if (player && DirUtil.Flat(player.Pos - Pos).magnitude <= hitRange)
                            if (player.TakeHit(this)) Sfx.Play("sfx_hit");
                        CameraRig.I?.Shake(0.08f, 0.12f);
                    }
                    if (_t >= _attackEnd) { state = State.Recover; _t = 0.5f; }
                    break;

                case State.GrabWindup:
                {
                    _t += dt;
                    glow = Mathf.Repeat(_t * 7f, 1f) < 0.5f ? 0.85f : 0.3f;     // 红光一直闪 = 抓取
                    if (!player || player.Dead) { CancelGrab(); state = State.Recover; _t = 0.5f; break; }
                    var v = DirUtil.Flat(player.Pos - Pos);
                    Face(v);
                    if (v.magnitude > grabRange * 0.75f) MoveBy(v.normalized * speed * 0.45f * dt);   // 慢慢逼近
                    if (_t >= grabWindup)
                    {
                        if (v.magnitude <= grabRange && player.TryGrab(this))
                        {
                            state = State.Grabbing; _t = 0; _tick = grabTick;
                            squid.GrabHold(SideToPlayer());
                            CameraRig.I?.Shake(0.25f, 0.2f);
                            Sfx.Play("sfx_hit");
                        }
                        else
                        {   // 扑空：触手甩个空
                            squid.EndFrenzy();
                            squid.Attack(SideToPlayer(), -1, 0.12f);
                            if (Grabber == this) Grabber = null;
                            state = State.Recover; _t = 0.8f;
                        }
                    }
                    break;
                }

                case State.Grabbing:
                    _t += dt;
                    glow = 0.35f;
                    if (!player || player.state != PlayerController.State.Grabbed)
                    {   // 主角被勒死等：松开
                        CancelGrab(); state = State.Recover; _t = 0.8f; break;
                    }
                    _tick -= dt;
                    if (_tick <= 0f) { _tick += grabTick; player.GrabTick(grabDamage); }
                    if (_t >= grabHold && player.state == PlayerController.State.Grabbed)
                    {
                        player.ThrowFrom(this);
                        squid.GrabRelease();
                        if (Grabber == this) Grabber = null;
                        CameraRig.I?.Shake(0.35f, 0.25f);
                        Sfx.Play("sfx_swing", 1f);
                        state = State.Recover; _t = 1.0f;
                    }
                    break;

                case State.Recover:
                    _t -= dt;
                    anim.Play("Idle", facing);
                    if (_t <= 0)
                    {
                        state = State.Chase;
                        _nextAttack = Time.time + Random.Range(attackCooldown.x, attackCooldown.y);
                    }
                    break;

                case State.Stagger:
                    _t -= dt;
                    if (_t <= 0) state = State.Chase;
                    break;

                case State.Stunned:
                    _t -= dt;
                    if (_t <= 0)
                    {
                        if (_stars) Destroy(_stars);
                        state = State.Chase;
                        _nextAttack = Time.time + 0.8f;
                    }
                    break;

                case State.Pending:
                    glow = 0.25f + 0.15f * Mathf.Sin(Time.unscaledTime * 10f);
                    if (!TimeManager.SlowMo)
                    {
                        // nobody executed us: back to normal so we keep fighting
                        state = State.Recover; _t = 0.4f;
                    }
                    break;

                case State.Executing:
                    break;

                case State.Dead:
                    _t += dt;
                    if (_t > 2.5f) Destroy(gameObject);
                    break;
            }

            anim.SetDir(facing);
            if (squid) squid.energy = state == State.Stunned || state == State.Pending || state == State.Executing ? 0.25f : 1f;
            if (squidFace) squidFace.angry = IsAngry;
            ApplyFlash(glow, state == State.Pending ? PendingGlow :
                             state == State.GrabWindup || state == State.Grabbing ? GrabGlow : AttackGlow);
        }

        /// <summary>去打家具时玩家挡在路上：往侧面绕开（玩家的碰撞体会把怪物卡住）。</summary>
        Vector3 AvoidPlayer(Vector3 toTarget)
        {
            if (!player) return Vector3.zero;
            var d = DirUtil.Flat(Pos - player.Pos);
            float m = d.magnitude;
            if (m > 1.8f || m < 1e-3f || Vector3.Dot(toTarget, -d) <= 0f) return Vector3.zero;   // 玩家不在前进方向上
            var side = Vector3.Cross(Vector3.up, toTarget.normalized);
            if (Vector3.Dot(side, d) < 0f) side = -side;                                          // 往离玩家远的那侧绕
            return (side * 1.4f + d / m * 0.4f) * speed * (1.8f - m) / 1.8f * 2f;
        }

        Vector3 Separation()
        {
            Vector3 s = Vector3.zero;
            foreach (var o in All)
            {
                if (o == this || o.Dead) continue;
                var v = DirUtil.Flat(Pos - o.Pos);
                float d = v.magnitude;
                if (d < 1.2f && d > 1e-3f) s += v / d * (1.2f - d) * 2f;
            }
            return s;
        }
    }
}
