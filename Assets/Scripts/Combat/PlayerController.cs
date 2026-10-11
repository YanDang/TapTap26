using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Two-button control (mouse; touch maps tap=left, swipe=right later):
    ///  Right on ground : move. In combat: dodge toward the click (5 s CD) -> backflip spirit arrow -> land -> leave combat.
    ///                    In combat with dodge on CD: just walk away and leave combat.
    ///  Right on enemy  : lock on, auto-chase and auto-sweep (combat state).
    ///  Left            : clock-QTE hit / execute a parried enemy / parry a red-glowing attack / kick furniture.
    ///                    寄居蟹主角（hero != null）：战斗中按住左键 = 蓄力（松开 = 前扑砸击），短按仍是空挡。
    ///  寄居蟹主角的闪避 = 缩进壳里滚出去 → 机械臂从壳口伸出一拳砸向锁定的敌人（替代后空翻精灵之箭）。
    /// Runs on unscaled time so the player keeps full speed during the parry slow-mo.
    /// </summary>
    public class PlayerController : Unit
    {
        public enum State { Idle, Move, Chase, Attack, Parry, Kick, Hurt, Execute, Roll, Backflip, Dead, Charge, Smash, ShellPunch, Grabbed, Thrown, Place }

        [Header("Tuning")]
        public float moveSpeed = 4.5f;
        public float attackRange = 1.5f;
        public float attackInterval = 1.0f;
        public int attackDamage = 1;
        public float dodgeCooldown = 5f;
        public float dodgeDistance = 3f;
        public float rollTime = 0.5f;
        public float rollInvuln = 0.25f;
        public float parryCooldown = 0.5f;
        public float slowMoSeconds = 3f;
        public float kickRange = 1.8f;
        public float hurtInvuln = 0.8f;
        // sprite-frame events of the BackflipArrow clip (import_manifest.json -> backflipEvents)
        public int bfChargeFrame = 10, bfShootFrame = 22, bfLandFrame = 26;

        [Header("寄居蟹主角（2D 骨骼动作）")]
        public HeroActions hero;               // GameManager.AttachHero2D 设置；null = 旧序列帧主角
        public float chargeHoldDelay = 0.18f;  // 按住超过它才算蓄力，否则是短按（空挡）
        public float chargeTime = 1.0f;        // 蓄满所需时间
        public float smashLunge = 1.4f;        // 蓄力砸击往前扑的最远距离
        public int punchDamage = 3;
        public float punchRadius = 2.2f;
        public float reLockRadius = 8f;        // 处决后在这个范围内找下一个敌人自动锁定，没有就脱战
        [Header("被鱿鱼抓")]
        public float grabHoldDistance = 1.1f;  // 被缠住时拉到离鱿鱼多远
        public float throwDistance = 3f;       // 被丢出去的距离（与翻滚相同）
        public float throwFlight = 0.45f;      // 空中时间；之后躺地 + 0.5 s 缓慢起身（HeroActions "Thrown"）
        [Range(0, 1)] public float happyChance = 0.6f;   // 打倒敌人时露出高兴表情的概率
        public int grabTicks;

        public State state = State.Idle;
        public bool InCombat { get; private set; }
        public EnemyAI LockTarget { get; private set; }
        public Vector3 spawnPoint;

        // event log for the self-test / debugging
        public int parrySuccess, parryWhiff, executions, kicks, arrowsShot, timesHit, smashes, punches;
        public float lastSmashLevel;

        float _dodgeReady, _parryReady, _attackReady, _invulnUntil;
        float _t;                       // generic state timer (unscaled)
        bool _flag1, _flag2;            // per-state one-shot flags
        int _attackAlt;
        float _leftDownAt = -1f, _chargeLevel;
        KickableTable _tapTable;           // 战斗中在桌子旁按下：短按松开才踢
        CombatFurniture _tapFurn, _kickFurn;  // 同上，气系家具（玩家能踢破）
        Vector3 _knockVel;
        public int kickFurnitureDamage = 6;

        /// <summary>被弹开（气爆等）：沿 dir 滑出约 distance 米，不掉血、不打断当前动作。</summary>
        public void Knockback(Vector3 dir, float distance)
        {
            if (Dead) return;
            _knockVel = DirUtil.Flat(dir).normalized * distance * 8f;
        }

        /// <summary>离主角 range 内最近、玩家能打的家具（气系）。</summary>
        CombatFurniture NearestHittableFurniture(float range)
        {
            CombatFurniture best = null; float bd = range;
            foreach (var f in CombatFurniture.All)
            {
                if (!f || !f.PlayerCanHit) continue;
                float d = DirUtil.Flat(f.AimPoint(Pos) - Pos).magnitude - f.hitRadius;
                if (d < bd) { bd = d; best = f; }
            }
            return best;
        }

        /// <summary>攻击顺带打到气系家具（扇形：dir + 角度；角度 ≥ 180 = 圆形）。</summary>
        void HitFurniture(Vector3 center, Vector3 dir, float range, float angle, int dmg)
        {
            foreach (var f in CombatFurniture.All.ToArray())
            {
                if (!f || !f.PlayerCanHit) continue;
                var v = DirUtil.Flat(f.AimPoint(center) - center);
                if (v.magnitude > range + f.hitRadius) continue;
                if (angle < 180f && v.sqrMagnitude > 0.01f && Vector3.Angle(dir, v) > angle) continue;
                f.HitByPlayer(dmg);
            }
        }
        bool _leftHeld;
        Vector3 _moveTarget, _a, _b, _dir;
        EnemyAI _execTarget;
        KickableTable _kickTable;

        public float DodgeRemaining => Mathf.Max(0, _dodgeReady - Time.unscaledTime);
        public float ParryRemaining => Mathf.Max(0, _parryReady - Time.unscaledTime);
        bool Busy => state == State.Execute || state == State.Roll || state == State.Backflip || state == State.Dead || state == State.Place ||
                    state == State.Smash || state == State.ShellPunch || Unavailable;

        /// <summary>被缠住 / 被丢出去 / 躺地起身中：不能操作，其他怪也不会发起攻击。</summary>
        public bool Unavailable => state == State.Grabbed || state == State.Thrown;
        EnemyAI _grabber;
        float _thrownLen;

        public bool Invulnerable =>
            Time.unscaledTime < _invulnUntil || state == State.Execute || state == State.Dead ||
            (state == State.Backflip && anim.Frame >= 1 && anim.Frame < bfLandFrame) ||
            (state == State.ShellPunch && hero && hero.ShellTime < 0.85f) || Unavailable;

        /// <summary>世界方向在屏幕上朝右（+1）还是朝左（-1）。</summary>
        static int ScreenSide(Vector3 worldDir)
        {
            var cam = Camera.main;
            return Vector3.Dot(worldDir, cam ? cam.transform.right : Vector3.right) >= 0f ? 1 : -1;
        }

        Vector3 FacingVector()
        {
            var cam = Camera.main.transform;
            Vector3 f = DirUtil.Flat(cam.forward).normalized, r = DirUtil.Flat(cam.right).normalized;
            switch (facing)
            {
                case Dir.Up: return f;
                case Dir.Left: return -r;
                case Dir.Right: return r;
                default: return -f;
            }
        }

        // ------------------------------------------------------------------ 放置家具（2026-10-11）
        public const float PlaceTime = 1.0f, PlaceRelease = 0.56f;   // 整个动作 1 s；0.56 s 抛出胶囊（= HeroActions "Place" 的 hit）
        System.Action _placeRelease, _placeCancel;
        public int places;
        public bool Placing => state == State.Place;

        /// <summary>胶囊从哪飞出去：钳子的位置（换算回公告板拉近之前的世界坐标）。</summary>
        public Vector3 PlaceHandPos
        {
            get
            {
                var cam = Camera.main;
                if (hero && cam) return hero.PunchTipWorld + cam.transform.forward * 2f;
                return Pos + Vector3.up * 1.2f;
            }
        }

        /// <summary>
        /// 播放置动画：面朝 target，掏壳 → 举起 → 抛出。抛出瞬间调 release（BuildController 在那时生成家具 + 部署动画），
        /// 抛出前被打断（受击 / 被抓）调 cancel（退还物品）。正在忙（翻滚、处决、蓄力等）时返回 false。
        /// </summary>
        public bool BeginPlace(Vector3 target, System.Action release, System.Action cancel)
        {
            if (Dead || Busy) return false;
            if (state != State.Idle && state != State.Move && state != State.Chase && state != State.Attack) return false;
            CancelLeftHold();
            _placeRelease = release; _placeCancel = cancel;
            _t = 0; _flag1 = false;
            _dir = DirUtil.Flat(target - Pos);
            _dir = _dir.sqrMagnitude > 1e-4f ? _dir.normalized : FacingVector();
            Face(_dir);
            state = State.Place;
            anim.Play("Idle", facing, true);
            if (hero) hero.Play("Place");
            return true;
        }

        /// <summary>放置动画还没抛出就被打断：退还物品。</summary>
        void CancelPlace()
        {
            if (state != State.Place || _flag1) return;
            var c = _placeCancel;
            _placeRelease = _placeCancel = null;
            c?.Invoke();
        }

        // ------------------------------------------------------------------ commands
        public void RightClickGround(Vector3 p)
        {
            if (Busy) return;
            if (InCombat)
            {
                if (DodgeRemaining <= 0) { StartDodge(p); return; }
                ExitCombat();
            }
            MoveTo(p);
        }

        public void RightClickEnemy(EnemyAI e)
        {
            if (Busy || e == null || e.Dead) return;
            LockTarget = e;
            InCombat = true;
            if (state != State.Attack && state != State.Parry && state != State.Kick && state != State.Hurt) state = State.Chase;
        }

        public void LeftClick()
        {
            if (Busy) return;
            if (InCombat)
            {
                // 1. humanoid clock QTE
                var clock = ClockQTE.Current;
                if (clock != null && clock.Open)
                {
                    if (clock.TryHit()) StartExecute(clock.owner);
                    return;
                }
                // 2. execute a parried (pending) enemy during slow-mo
                var pend = EnemyAI.FindPending();
                if (pend != null) { StartExecute(pend); return; }
                // 3. parry an open red window
                if (ParryRemaining <= 0)
                {
                    var e = EnemyAI.FindParryable(this);
                    if (e != null) { DoParry(e); return; }
                }
            }
            // 4. kick furniture（寄居蟹战斗中：短按才踢，按住是蓄力，在 LeftRelease 里判定）
            var t = KickableTable.Nearest(Pos, kickRange);
            var kf = t == null ? NearestHittableFurniture(kickRange) : null;
            if (t != null && state != State.Kick && !(InCombat && hero)) { StartKick(t); return; }
            if (kf != null && state != State.Kick && !(InCombat && hero)) { StartKickFurniture(kf); return; }
            // 5. 寄居蟹：先记下按下时刻，按住 = 蓄力（点屏幕哪里都一样），松开得快 = 踢桌子 / 空挡
            if (InCombat && hero)
            {
                _leftHeld = true; _leftDownAt = Time.unscaledTime;
                _tapTable = t; _tapFurn = kf;
                return;
            }
            // 5. parry whiff (combat only), still costs the cooldown
            if (InCombat && ParryRemaining <= 0) Whiff();
        }

        /// <summary>进入摆放模式等：放弃这次按住的左键（正在蓄力就取消蓄力）。</summary>
        public void CancelLeftHold()
        {
            _leftHeld = false; _tapTable = null; _tapFurn = null;
            if (state == State.Charge) { if (hero) hero.Stop(); state = InCombat ? State.Chase : State.Idle; }
        }

        public void LeftRelease()
        {
            if (!_leftHeld) return;
            _leftHeld = false;
            if (state == State.Charge) { ReleaseCharge(); return; }
            if (Busy) return;
            if (Time.unscaledTime - _leftDownAt >= chargeHoldDelay) return;
            if (_tapTable && state != State.Kick) { StartKick(_tapTable); _tapTable = null; return; }
            if (_tapFurn && state != State.Kick) { StartKickFurniture(_tapFurn); _tapFurn = null; return; }
            if (InCombat && ParryRemaining <= 0) Whiff();
        }

        void Whiff()
        {
            _parryReady = Time.unscaledTime + parryCooldown;
            parryWhiff++;
            state = State.Parry;
            anim.Play("Block", facing, true, 1.4f);
            if (hero) hero.Play("ShellGuard");
            Sfx.Play("sfx_swing", 0.5f);
        }

        public void ExitCombat()
        {
            InCombat = false;
            LockTarget = null;
            if (TimeManager.SlowMo) TimeManager.I.EndSlowMo();   // a pending enemy recovers on its own
        }

        // ------------------------------------------------------------------ actions
        void MoveTo(Vector3 p)
        {
            _moveTarget = DirUtil.Flat(p);
            state = State.Move;
            GameManager.I.ShowMoveMarker(_moveTarget);
        }

        void StartAttack()
        {
            _attackReady = Time.unscaledTime + attackInterval;
            state = State.Attack; _flag1 = false;
            int combo = _attackAlt++;
            anim.Play((combo & 1) == 0 ? "AttackA" : "AttackB", facing, true, 1.25f);
            if (hero) hero.Play(HeroComboClip(combo));
            Sfx.Play("sfx_swing", 0.6f);
        }

        // 寄居蟹普攻三连：钳子横扫 → 钳子直戳 → 前腿踩踏
        static string HeroComboClip(int combo) => (combo % 3) == 0 ? "Sweep" : (combo % 3) == 1 ? "Thrust" : "Stomp";

        void StartCharge()
        {
            state = State.Charge; _t = 0; _chargeLevel = 0;
            hero.BeginCharge();
            Sfx.Play("sfx_arrow_charge", 0.6f);
        }

        void ReleaseCharge()
        {
            state = State.Smash; _t = 0; _flag1 = false;
            lastSmashLevel = _chargeLevel;
            _dir = LockTarget && !LockTarget.Dead ? DirUtil.Flat(LockTarget.Pos - Pos).normalized : FacingVector();
            hero.ReleaseCharge();
            Sfx.Play("sfx_swing", 0.9f);
        }

        void DoSmash()
        {
            float lv = _chargeLevel;
            int dmg = 1 + Mathf.RoundToInt(lv * 3f);
            float radius = 1.3f + 0.8f * lv;
            var center = Pos + _dir * 1.0f;
            smashes++;
            foreach (var e in EnemyAI.All.ToArray())
                if (!e.Dead && DirUtil.Flat(e.Pos - center).magnitude <= radius)
                    e.TakeDamage(dmg, DirUtil.Flat(e.Pos - Pos).normalized, 0.4f + 0.5f * lv, true);
            HitFurniture(center, _dir, radius, 180f, dmg);
            TimeManager.I.DoHitstop(0.05f + 0.1f * lv);
            CameraRig.I?.Shake(0.25f + 0.45f * lv, 0.25f + 0.2f * lv);
            if (lv > 0.95f) ScreenFX.I?.Pulse();
            Sfx.Play(lv > 0.6f ? "sfx_explosion" : "sfx_kick", 0.7f + 0.3f * lv);
            Vfx.Spawn("VFX_ArrowHit", center + Vector3.up * 0.2f, 0, 0.7f + 0.8f * lv, true);
        }

        void StartShellPunch()
        {
            state = State.ShellPunch; _flag1 = false;
            var tgt = LockTarget && !LockTarget.Dead ? LockTarget.Pos : Pos + FacingVector() * 3f;
            Face(tgt - Pos);
            hero.ShellPunch(tgt + Vector3.up * 0.9f);
            Sfx.Play("sfx_arrow_charge", 0.7f);
        }

        void DoPunchHit()
        {
            punches++;
            var tip = DirUtil.Flat(hero.PunchTipWorld);
            // 拳头画在公告板平面上，投到地面会有偏差：锁定目标还活着就以它为落点
            if (LockTarget && !LockTarget.Dead) tip = DirUtil.Flat(LockTarget.Pos);
            foreach (var e in EnemyAI.All.ToArray())
                if (!e.Dead && DirUtil.Flat(e.Pos - tip).magnitude <= punchRadius)
                    e.TakeDamage(punchDamage, DirUtil.Flat(e.Pos - Pos).normalized, 0.8f, true);
            HitFurniture(tip, Vector3.zero, punchRadius, 180f, punchDamage);
            TimeManager.I.DoHitstop(0.12f);
            CameraRig.I?.Shake(0.7f, 0.5f);
            ScreenFX.I?.Pulse();
            Sfx.Play("sfx_explosion");
            Vfx.Spawn("VFX_ArrowHit", tip + Vector3.up * 0.6f, 0, 1.5f, true);
            Vfx.Spawn("VFX_ArrowBurst", tip + Vector3.up * 0.9f, 0, 1.1f, true);
        }

        void DoSweep(float rangeBonus = 0f, int dmg = -1, float knock = 0.15f)
        {
            Vector3 dir = LockTarget ? DirUtil.Flat(LockTarget.Pos - Pos).normalized : FacingVector();
            foreach (var e in EnemyAI.All.ToArray())
            {
                if (e.Dead) continue;
                var v = DirUtil.Flat(e.Pos - Pos);
                if (v.magnitude <= attackRange + 0.5f + rangeBonus && Vector3.Angle(dir, v) <= 70f)
                    e.TakeDamage(dmg < 0 ? attackDamage : dmg, v.normalized, knock, false);
            }
            HitFurniture(Pos, dir, attackRange + 0.5f + rangeBonus, 70f, dmg < 0 ? attackDamage : dmg);
            Vfx.Spawn("VFX_Slash", Pos + dir * 0.6f + Vector3.up * 0.9f, DirUtil.ScreenAngle(dir) - 90f, 0.9f, true);
        }

        void DoParry(EnemyAI e)
        {
            _parryReady = Time.unscaledTime + parryCooldown;
            parrySuccess++;
            state = State.Parry;
            Face(e.Pos - Pos);
            anim.Play("Block", facing, true, 1.4f);
            if (hero) hero.Play("ShellGuard");
            e.Parried();
            TimeManager.I.StartSlowMo(slowMoSeconds);
            ScreenFX.I?.Pulse();
            CameraRig.I?.Shake(0.2f, 0.25f);
            Sfx.Play("sfx_parry", 1f, true);
            Sfx.Play("sfx_tongtou", 0.9f, true);
            Vfx.Spawn("VFX_ParrySpark", Vector3.Lerp(Pos, e.Pos, 0.5f) + Vector3.up * 1.1f, 0, 1.2f, true);
        }

        void StartExecute(EnemyAI e)
        {
            if (e == null || e.Dead) return;
            _execTarget = e;
            e.BeginExecuted();
            LockTarget = e; InCombat = true;
            _dir = DirUtil.Flat(e.Pos - Pos);
            if (_dir.sqrMagnitude < 0.01f) _dir = FacingVector();
            _dir.Normalize();
            _a = Pos; _b = DirUtil.Flat(e.Pos) + _dir * 1.4f;
            _t = 0; _flag1 = false;
            state = State.Execute;
            Dir side = Mathf.Abs(DirUtil.ScreenAngle(_dir)) <= 90f ? Dir.Right : Dir.Left;   // execute is rendered left/right only
            facing = side;
            anim.Play("Execute", side, true, 1f);
            if (hero) hero.Play("Execute");
            if (cc) cc.enabled = false;
            Sfx.Play("sfx_swing", 0.8f, true);
        }

        void StartDodge(Vector3 p)
        {
            _dodgeReady = Time.unscaledTime + dodgeCooldown;
            _dir = DirUtil.Flat(p - Pos);
            if (_dir.sqrMagnitude < 0.01f) _dir = LockTarget ? DirUtil.Flat(Pos - LockTarget.Pos) : -FacingVector();
            _dir.Normalize();
            _t = 0;
            _invulnUntil = Time.unscaledTime + rollInvuln;
            state = State.Roll;
            Face(_dir);
            anim.Play("Roll", facing, true, anim.set.Get("Roll").frames / anim.set.Get("Roll").fps / rollTime);
            if (hero) hero.ShellRoll(ScreenSide(_dir), rollTime);
            Sfx.Play("sfx_dodge", 0.8f);
            if (TimeManager.SlowMo) TimeManager.I.EndSlowMo();
        }

        void StartBackflip()
        {
            state = State.Backflip; _flag1 = _flag2 = false;
            if (LockTarget && !LockTarget.Dead) Face(LockTarget.Pos - Pos); else Face(-_dir);
            anim.Play("BackflipArrow", facing, true, 1f);
        }

        void StartKickFurniture(CombatFurniture f)
        {
            _kickTable = null; _kickFurn = f; _t = 0; _flag1 = false;
            _dir = DirUtil.Flat(f.AimPoint(Pos) - Pos).normalized;
            Face(_dir);
            state = State.Kick;
            anim.Play("KickTable", facing, true, 1.2f);
            if (hero) hero.Play("Stomp");
        }

        void StartKick(KickableTable t)
        {
            _kickFurn = null;
            _kickTable = t; _t = 0; _flag1 = false;
            _dir = DirUtil.Flat(t.transform.position - Pos).normalized;
            Face(_dir);
            state = State.Kick;
            anim.Play("KickTable", facing, true, 1.2f);
            if (hero) hero.Play("Stomp");
        }

        // ------------------------------------------------------------------ 鱿鱼抓取
        /// <summary>鱿鱼抓取前摇结束时调用；能被抓的状态下返回 true（翻滚 / 壳拳 / 砸击 / 处决 / 无敌中抓不到）。</summary>
        public bool TryGrab(EnemyAI by)
        {
            if (Dead || Unavailable || Time.unscaledTime < _invulnUntil) return false;
            switch (state)
            {
                case State.Idle: case State.Move: case State.Chase: case State.Attack: case State.Parry:
                case State.Kick: case State.Hurt: case State.Charge: case State.Place: break;
                default: return false;
            }
            _leftHeld = false;
            CancelPlace();
            _grabber = by; _t = 0;
            state = State.Grabbed;
            if (TimeManager.SlowMo) TimeManager.I.EndSlowMo();
            if (hero) hero.BeginStruggle();
            foreach (var e in EnemyAI.All) if (e != by) e.YieldToGrab();   // 别的怪正在出的招取消，等她起身
            Flash(new Color(1, 0.2f, 0.2f), 0.2f);
            CameraRig.I?.Shake(0.3f, 0.25f);
            Sfx.Play("sfx_player_hurt");
            return true;
        }

        /// <summary>被缠住期间每次勒紧扣血（不受无敌影响）。</summary>
        public void GrabTick(int dmg)
        {
            if (state != State.Grabbed) return;
            hp -= dmg; timesHit++; grabTicks++;
            Flash(new Color(1, 0.2f, 0.2f), 0.25f);
            CameraRig.I?.Shake(0.18f, 0.2f);
            Sfx.Play("sfx_player_hurt");
            if (hero) hero.SqueezeKick();
            if (hp <= 0)
            {
                hp = 0; ExitCombat();
                if (hero) hero.EndStruggle();
                _grabber = null;
                state = State.Dead; _t = 0;
                anim.Play("Death", facing, true);
            }
        }

        /// <summary>缠绕结束，被丢出去：飞 throwFlight 秒 → 躺地蟹腿摊开 → 0.5 s 缓慢起身，起身后才能动。</summary>
        public void ThrowFrom(EnemyAI by)
        {
            if (state != State.Grabbed) return;
            _dir = by ? DirUtil.Flat(Pos - by.Pos) : -FacingVector();
            if (_dir.sqrMagnitude < 0.01f) _dir = -FacingVector();
            _dir.Normalize();
            _grabber = null; _t = 0; _flag1 = false;
            state = State.Thrown;
            if (hero)
            {
                hero.EndStruggle();
                hero.Play("Thrown");
                _thrownLen = HeroActions.Clips["Thrown"].Length;
            }
            else _thrownLen = throwFlight + 0.9f;
            Sfx.Play("sfx_swing", 0.9f);
        }

        void OnEnable() { EnemyAI.Killed += OnEnemyKilled; }
        void OnDisable() { EnemyAI.Killed -= OnEnemyKilled; }
        void OnEnemyKilled(EnemyAI e)
        {
            if (hero && hero.Face && !Unavailable && !Dead && Random.value < happyChance) hero.Face.TriggerHappy();
        }

        public bool TakeHit(EnemyAI src, int dmg = 1)
        {
            if (Dead || Invulnerable) return false;
            CancelPlace();
            hp -= dmg; timesHit++;
            _invulnUntil = Time.unscaledTime + hurtInvuln;
            Flash(new Color(1, 0.2f, 0.2f), 0.25f);
            CameraRig.I?.Shake(0.22f, 0.25f);
            Sfx.Play("sfx_player_hurt");
            if (hp <= 0)
            {
                hp = 0; ExitCombat();
                state = State.Dead; _t = 0;
                anim.Play("Death", facing, true);
                return true;
            }
            if (state != State.Attack && state != State.Kick)
            {
                state = State.Hurt; _t = 0.3f;
                anim.Play("Hit", facing, true);
                if (hero) hero.Play("Hurt");
            }
            return true;
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            if (CraftingBridge.IsOpen) return;                // 合成工坊开着：主角也冻结（主角用的是真实时间，timeScale = 0 停不住它）
            float dt = TimeManager.PlayerDelta;
            if (_knockVel.sqrMagnitude > 0.01f && !Dead)
            {   // 被气爆弹开的滑行（被缠住时不动）
                if (state != State.Grabbed) MoveBy(_knockVel * dt);
                _knockVel *= Mathf.Exp(-8f * dt);
            }
            if (LockTarget && LockTarget.Dead && state != State.Execute && state != State.Backflip && state != State.Roll)
                ExitCombat();

            // 按住超过 chargeHoldDelay 就开始蓄力：正在出的普攻 / 空挡 / 移动都直接打断（用户：战斗中点哪都能蓄力）
            if (_leftHeld && hero && InCombat && LockTarget && !LockTarget.Dead &&
                (state == State.Chase || state == State.Idle || state == State.Attack || state == State.Parry || state == State.Move) &&
                Time.unscaledTime - _leftDownAt >= chargeHoldDelay)
                StartCharge();

            switch (state)
            {
                case State.Idle:
                    anim.Play("Idle", facing);
                    break;

                case State.Move:
                {
                    var v = DirUtil.Flat(_moveTarget - Pos);
                    if (v.magnitude < 0.08f) { state = State.Idle; break; }
                    Face(v);
                    MoveBy(Vector3.ClampMagnitude(v.normalized * moveSpeed * dt, v.magnitude));
                    anim.Play("Run", facing);
                    break;
                }

                case State.Chase:
                {
                    if (!LockTarget) { state = State.Idle; break; }
                    var v = DirUtil.Flat(LockTarget.Pos - Pos);
                    Face(v);
                    if (v.magnitude > attackRange)
                    {
                        MoveBy(v.normalized * moveSpeed * dt);
                        anim.Play("Run", facing);
                    }
                    else
                    {
                        anim.Play("Idle", facing);
                        if (Time.unscaledTime >= _attackReady && LockTarget.CanBeAutoAttacked) StartAttack();
                    }
                    break;
                }

                case State.Attack:
                    if (hero)
                    {
                        if (!_flag1 && hero.T >= hero.Hit)
                        {
                            _flag1 = true;
                            bool stomp = (_attackAlt - 1) % 3 == 2;
                            if (stomp) { DoSweep(0.3f, attackDamage + 1, 0.3f); CameraRig.I?.Shake(0.25f, 0.2f); Sfx.Play("sfx_kick", 0.8f); }
                            else DoSweep();
                        }
                        if (!hero.Playing) state = InCombat ? State.Chase : State.Idle;
                        break;
                    }
                    if (!_flag1 && anim.Normalized >= 0.45f) { _flag1 = true; DoSweep(); }
                    if (anim.Finished) state = InCombat ? State.Chase : State.Idle;
                    break;

                case State.Charge:
                    _t += dt;
                    if (!InCombat || !LockTarget || LockTarget.Dead) { hero.Stop(); _leftHeld = false; state = State.Idle; break; }
                    Face(LockTarget.Pos - Pos);
                    _chargeLevel = Mathf.Clamp01(_t / chargeTime);
                    hero.SetCharge(_chargeLevel);
                    anim.Play("Idle", facing);
                    break;

                case State.Smash:
                {
                    float prev = _t; _t += dt;
                    // 0.10–0.22 s 往前扑（扑到离敌人 1 m 处为止）
                    float maxGo = LockTarget && !LockTarget.Dead ? Mathf.Clamp(DirUtil.Flat(LockTarget.Pos - Pos).magnitude - 1.0f, 0f, smashLunge) : smashLunge * 0.5f;
                    float u0 = Mathf.Clamp01((prev - 0.10f) / 0.12f), u1 = Mathf.Clamp01((_t - 0.10f) / 0.12f);
                    if (u1 > u0) MoveBy(_dir * maxGo * (u1 - u0));
                    if (!_flag1 && hero.T >= hero.Hit) { _flag1 = true; DoSmash(); }
                    if (!hero.Playing && _t > 0.3f) state = InCombat ? State.Chase : State.Idle;
                    break;
                }

                case State.ShellPunch:
                    if (!_flag1 && hero.PunchFired) { _flag1 = true; DoPunchHit(); }
                    // 收拳后还有 0.3 s 钻出壳的恢复动作，播完才能操作（ShellPunch 属于 Busy）
                    if (hero.ShellDone && !hero.Playing)
                    {
                        ExitCombat();
                        state = State.Idle;
                    }
                    break;

                case State.Parry:
                    if (anim.Finished) state = InCombat ? State.Chase : State.Idle;
                    break;

                case State.Grabbed:
                {
                    _t += dt;
                    anim.Play("Idle", facing);
                    if (!_grabber || _grabber.Dead) { ThrowFrom(_grabber); break; }
                    // 被拉到鱿鱼跟前
                    var away = DirUtil.Flat(Pos - _grabber.Pos);
                    if (away.sqrMagnitude < 0.01f) away = -FacingVector();
                    var hold = DirUtil.Flat(_grabber.Pos) + away.normalized * grabHoldDistance;
                    Teleport(Vector3.Lerp(Pos, hold, 1f - Mathf.Exp(-10f * dt)));
                    break;
                }

                case State.Thrown:
                {
                    float prev = _t; _t += dt;
                    float u0 = EaseOut(prev / throwFlight), u1 = EaseOut(_t / throwFlight);
                    if (u1 > u0) MoveBy(_dir * throwDistance * (u1 - u0));
                    if (!_flag1 && _t >= throwFlight)
                    {
                        _flag1 = true;
                        CameraRig.I?.Shake(0.3f, 0.25f);
                        Sfx.Play("sfx_kick", 0.9f);
                    }
                    anim.Play("Idle", facing);
                    if (_t >= _thrownLen)
                    {
                        bool target = LockTarget && !LockTarget.Dead;
                        if (!target) ExitCombat();
                        state = InCombat ? State.Chase : State.Idle;
                    }
                    break;
                }

                case State.Kick:
                    _t += dt;
                    if (!_flag1 && _t >= 0.16f)
                    {
                        _flag1 = true; kicks++;
                        if (_kickTable) _kickTable.Kick(_dir);
                        if (_kickFurn && _kickFurn.Alive) _kickFurn.HitByPlayer(kickFurnitureDamage);
                        Sfx.Play("sfx_kick");
                        CameraRig.I?.Shake(0.12f, 0.15f);
                    }
                    if (anim.Finished) state = InCombat ? State.Chase : State.Idle;
                    break;

                case State.Hurt:
                    _t -= dt;
                    if (_t <= 0) state = InCombat ? State.Chase : State.Idle;
                    break;

                case State.Place:
                    _t += dt;
                    if (!_flag1 && _t >= PlaceRelease)
                    {   // 抛出胶囊：BuildController 生成家具 + 部署动画
                        _flag1 = true; places++;
                        var r = _placeRelease;
                        _placeRelease = _placeCancel = null;
                        r?.Invoke();
                        Sfx.Play("sfx_swing", 0.6f);
                    }
                    if (_t >= PlaceTime) state = InCombat && LockTarget && !LockTarget.Dead ? State.Chase : State.Idle;
                    break;

                case State.Execute:
                    UpdateExecute(dt);
                    break;

                case State.Roll:
                {
                    float prev = _t; _t += dt;
                    float u0 = EaseOut(prev / rollTime), u1 = EaseOut(_t / rollTime);
                    MoveBy(_dir * dodgeDistance * (u1 - u0));
                    if (_t >= rollTime) { if (hero) StartShellPunch(); else StartBackflip(); }
                    break;
                }

                case State.Backflip:
                    if (!_flag1 && anim.Frame >= bfChargeFrame)
                    {
                        _flag1 = true;
                        Vfx.Spawn("VFX_ArrowCharge", Pos + Vector3.up * 1.9f, 0, 1.1f, true, transform);
                        Sfx.Play("sfx_arrow_charge", 0.8f);
                    }
                    if (!_flag2 && anim.Frame >= bfShootFrame)
                    {
                        _flag2 = true; arrowsShot++;
                        var aimDir = LockTarget && !LockTarget.Dead ? DirUtil.Flat(LockTarget.Pos - Pos).normalized : FacingVector();
                        SpiritArrow.Spawn(Pos + Vector3.up * 1.9f + aimDir * 0.3f, LockTarget, aimDir);
                        CameraRig.I?.Shake(0.3f, 0.3f);
                        Sfx.Play("sfx_arrow_shoot");
                    }
                    if (anim.Finished)
                    {
                        ExitCombat();
                        state = State.Idle;
                    }
                    break;

                case State.Dead:
                    _t += dt;
                    if (_t > 2.5f)
                    {
                        hp = maxHp; Teleport(spawnPoint);
                        state = State.Idle; _invulnUntil = Time.unscaledTime + 1.5f;
                    }
                    break;
            }

            anim.SetDir(facing);
            if (hero) { UpdateHeroFacing(); UpdateHeroFace(); }
            float blink = Time.unscaledTime < _invulnUntil && state != State.Roll ? 0.25f * (Mathf.Sin(Time.unscaledTime * 40f) > 0 ? 1 : 0) : 0;
            ApplyFlash(blink, Color.white);
        }

        void UpdateExecute(float dt)
        {
            _t += dt;
            float u = Mathf.Clamp01(_t / 0.42f);
            Teleport(Vector3.Lerp(_a, _b, u * u * (3 - 2 * u)));
            if (!_flag1 && _t >= 0.28f)
            {
                _flag1 = true; executions++;
                TimeManager.I.EndSlowMo();
                TimeManager.I.DoHitstop(0.18f);
                CameraRig.I?.Shake(0.6f, 0.45f);
                ScreenFX.I?.Pulse();
                Sfx.Play("sfx_execute", 1f, true);
                var ep = _execTarget ? _execTarget.Pos : Pos;
                Vfx.Spawn("VFX_ExecuteSlash", ep + Vector3.up * 1.0f, DirUtil.ScreenAngle(_dir) - 30f, 1.3f, true);
                if (_execTarget) _execTarget.KillByExecution(_dir);
            }
            // 寄居蟹：跟着骨骼处决动作结束（约 0.8 s），不再等后台旧序列帧的 1.5 s
            bool done = hero ? (!hero.Playing && _t >= 0.42f) : anim.Finished;
            if (done)
            {
                if (cc) cc.enabled = true;
                // 附近还有敌人：自动锁定最近的一个继续战斗（格挡立刻可用）；没有才脱战
                var next = NearestEnemy(reLockRadius);
                if (next)
                {
                    LockTarget = next; InCombat = true;
                    state = State.Chase;
                }
                else
                {
                    ExitCombat();
                    state = State.Idle;
                }
            }
        }

        EnemyAI NearestEnemy(float radius)
        {
            EnemyAI best = null; float bd = radius;
            foreach (var e in EnemyAI.All)
            {
                if (!e || e.Dead) continue;
                float d = DirUtil.Flat(e.Pos - Pos).magnitude;
                if (d <= bd) { best = e; bd = d; }
            }
            return best;
        }

        // 战斗中一直面向锁定目标（翻滚时也是：壳往后滚，壳口始终朝敌人）；格挡时背对敌人，用贝壳挡
        void UpdateHeroFacing()
        {
            int side = 0;
            bool target = LockTarget && !LockTarget.Dead;
            switch (state)
            {
                case State.Parry: if (target) side = -ScreenSide(LockTarget.Pos - Pos); break;
                case State.Execute: case State.Place: side = ScreenSide(_dir); break;
                case State.Chase: case State.Attack: case State.Charge: case State.Smash:
                case State.Roll: case State.ShellPunch: case State.Hurt:
                    if (target) side = ScreenSide(LockTarget.Pos - Pos); break;
                case State.Grabbed:
                    if (_grabber) side = ScreenSide(_grabber.Pos - Pos); break;
                case State.Thrown:
                    side = -ScreenSide(_dir); break;           // 面朝丢她的鱿鱼，往后飞
            }
            if (side != 0) hero.FaceScreen(side, true); else hero.UnlockFacing();
        }

        // 表情：被缠 / 被丢 / 受击 = 痛苦；蓄力 / 砸击 / 处决 = 愤怒；普攻 / 踢桌子 = 严肃；打倒敌人随机高兴（HeroFace 里叠加）
        void UpdateHeroFace()
        {
            var f = hero.Face;
            if (!f) return;
            HeroFace.Expr e;
            switch (state)
            {
                case State.Grabbed: case State.Thrown: case State.Hurt: case State.Dead: e = HeroFace.Expr.Pain; break;
                case State.Attack: case State.Kick:                       // 普攻也用愤怒（用户 2026-10-08）
                case State.Charge: case State.Smash: case State.Execute: e = HeroFace.Expr.Angry; break;
                default: e = HeroFace.Expr.Neutral; break;
            }
            f.SetBase(e);
            f.wrapped = state == State.Grabbed;
        }

        static float EaseOut(float x) { x = Mathf.Clamp01(x); return 1 - (1 - x) * (1 - x); }
    }
}
