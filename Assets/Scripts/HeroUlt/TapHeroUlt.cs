using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameJam;
using GameJam.Boss;

namespace GameJam.HeroUlt
{
    public enum UltKind { ArmSmash, ShellCannon, EchoShell }
    /// <summary>XY port of source UltManager/ArmSmash/ShellCannon/EchoShell. One action owner;
    /// actual HP delta events are the only damage charge source. No inventory writes.</summary>
    [DefaultExecutionOrder(-80)]
    public sealed class TapHeroUlt : MonoBehaviour
    {
        public const string PrefKey = "GJ_UltChoice";
        public PlayerController Player { get; private set; }
        public UltKind Choice { get; private set; }
        public float Charge { get; private set; }
        public bool Ready => Charge >= 100;
        public bool Active { get; private set; }
        public bool Echo => Active && Choice == UltKind.EchoShell;
        public bool DashInvulnerable => Active && Choice == UltKind.ArmSmash && stage == 1;
        public int Stacks { get; private set; }
        public int Hits { get; private set; }
        public int Explosions { get; private set; }
        public float Elapsed => elapsed;
        public float Unit { get; private set; }
        public float DamageUnit { get; private set; }
        public TapUltUI UI { get; private set; }
        public int Generation { get; private set; }
        int stage;
        float elapsed, shake;
        Vector2 direction;
        EnemyController target;
        GameObject actionFx;
        readonly List<TapUltProjectile> projectiles = new List<TapUltProjectile>();
        readonly List<TapUltBurn> burns = new List<TapUltBurn>();
        public static bool Paused => EnemyController.CombatWorldPaused;
        public bool CanChoose => Player && Player.IsAlive && !Player.InCombat && !BossEncounterController.EncounterActive && !Active && !Paused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install() { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; }
        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var p in FindObjectsOfType<PlayerController>())
                if (!p.GetComponent<TapHeroUlt>()) p.gameObject.AddComponent<TapHeroUlt>();
        }
        void Start()
        {
            Player = GetComponent<PlayerController>();
            Unit = Player.hero ? TapCombatScale.HeroBounds(Player).size.y / TapCombatScale.SourceWorldHeight : .11f;
            DamageUnit = Player.CurrentAttackDamage;
            Choice = (UltKind)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, 2);
            Player.DamageDealt += Damage;
            UI = TapUltUI.Build(this);
        }
        void Damage(EnemyController e, float amount) { AddCharge(amount / Mathf.Max(.01f, DamageUnit) * 4); }
        public void AddCharge(float amount)
        {
            if (!Player || !Player.IsAlive || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            bool was = Ready; Charge = Mathf.Clamp(Charge + amount, 0, 100);
            if (!was && Ready) Sfx.Play("sfx_parry", .6f, true);
        }
        public bool Select(UltKind kind)
        {
            if (!CanChoose || (int)kind < 0 || (int)kind > 2) return false;
            Choice = kind; PlayerPrefs.SetInt(PrefKey, (int)kind); PlayerPrefs.Save(); return true;
        }
        void Update()
        {
            if (!Player) return;
            if (!Player.IsAlive || !Player.enabled) { Cancel(true); return; }
            // World pause suspends timeline, projectile, burns and input. Death/disable cancels.
            if (Paused) return;
            if (Input.GetKeyDown(KeyCode.R)) TryRelease();
            if (Echo && Input.GetMouseButtonDown(0) && !(UnityEngine.EventSystems.EventSystem.current && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())) ReleaseEcho();
            if (!Active) return;
            Tick(TimeManager.PlayerDelta);
        }
        public bool TryRelease()
        {
            if (Echo) { if (!Paused) ReleaseEcho(); return !Paused; }
            if (!Player || Paused || Active || !Ready || !Player.CanBeginUltimate) {
                Debug.Log($"[P4 release rejected] paused={Paused} active={Active} charge={Charge} {(Player ? Player.UltimateEligibility : "no player")}"); return false;
            }
            // Only the arm acquires a nearest target. Cannon without a lock fires
            // along the player's facing, even when a nearby enemy is behind her.
            target = Player.currentTarget && Player.currentTarget.IsAlive ? Player.currentTarget :
                (Choice == UltKind.ArmSmash ? Nearest(12 * Unit) : null);
            if (Choice == UltKind.ArmSmash && !target) return false;
            direction = target ? ((Vector2)(target.transform.position - Player.transform.position)).normalized : (Player.IsFacingLeft ? Vector2.left : Vector2.right);
            if (direction.sqrMagnitude < .01f) direction = Vector2.right;
            Player.BeginUltimateLock();
            Active = true; stage = 0; elapsed = 0; Stacks = 0; Charge = 0;
            Player.SetVisualFacing(direction.x < 0);
            if (Choice == UltKind.EchoShell) Player.hero?.BeginUltEcho();
            else Player.hero?.BeginCharge();
            actionFx = TapUltVisual.Ring(Player.transform.position, .6f * Unit, Choice == UltKind.EchoShell ? Color.cyan : new Color(1,.7f,.2f), 0, Player.transform);
            return true;
        }
        EnemyController Nearest(float range)
        {
            EnemyController best = null;
            foreach (var e in EnemyController.AllEnemies) if (e && e.IsAlive) {
                float d = Vector2.Distance(Player.transform.position,e.transform.position);
                if (d < range) { best=e; range=d; }
            }
            return best;
        }
        public void Tick(float dt)
        {
            if (!Active || Paused || dt <= 0) return;
            if (!Player.IsAlive) { Cancel(true); return; }
            elapsed += dt;
            if (Choice == UltKind.EchoShell) { if (elapsed >= 5) ReleaseEcho(); return; }
            if (target && target.IsAlive && stage == 0) direction = ((Vector2)(target.transform.position - Player.transform.position)).normalized;
            Player.hero?.SetCharge(Mathf.Clamp01(elapsed / (Choice == UltKind.ArmSmash ? .4f : 1f)));
            if (Choice == UltKind.ArmSmash) {
                if (stage == 0 && elapsed >= .4f) { stage=1; elapsed=0; TimeManager.I.SetActionScale(this,.25f); }
                else if (stage == 1) {
                    if (!target || !target.IsAlive) { Cancel(false); return; }
                    Vector2 from=Player.transform.position, diff=(Vector2)target.transform.position-from;
                    direction=diff.sqrMagnitude>.00001f?diff.normalized:direction;
                    float step=Mathf.Min(Mathf.Max(0,diff.magnitude-1.2f*Unit),14*Unit*dt);
                    Vector2 next=from+direction*step;
                    if (step > 0 && !Walkable(next)) { Cancel(false); return; }
                    Player.transform.position=new Vector3(next.x,next.y,Player.transform.position.z);
                    if (diff.magnitude <= 1.2f*Unit+.01f || elapsed >= 2) {
                        stage=2;elapsed=0;TimeManager.I.ClearActionScale(this);
                        Player.hero?.ShellPunch(target.transform.position+Vector3.up*.2f);
                    }
                } else if (stage == 2 && elapsed >= (Player.hero ? Player.hero.PunchHitTime : .42f)) {
                    stage=3; elapsed=0; Area(Player.transform.position,3*Unit,6,2,true);
                    CameraRig.I?.Shake(.5f,.35f); TapUltVisual.Ring(Player.transform.position,3*Unit,Color.white,.5f);
                    TimeManager.I.DoHitstop(.06f);
                } else if (stage == 3 && elapsed >= .65f) End();
            } else {
                Player.hero?.SetUltCannonAim(stage==0 ? Mathf.Clamp01(elapsed) : Mathf.Clamp01(1-elapsed/.35f));
                if (stage==0 && elapsed>=1) {
                    stage=1;elapsed=0;
                    projectiles.Add(TapUltProjectile.Spawn(this,Player.transform.position,direction));
                    TimeManager.I.SetActionScale(this,.05f);
                    Sfx.Play("sfx_explosion",.8f); CameraRig.I?.Shake(.25f,.2f);
                } else if (stage==1 && elapsed>=.15f) { TimeManager.I.ClearActionScale(this);stage=2;elapsed=0; }
                else if(stage==2 && elapsed>=.35f) End();
            }
        }
        public bool Walkable(Vector2 p) { var map=IsometricPathfinder.Instance; return !map || map.IsWalkable(map.WorldToCell(p)); }
        public bool TryAbsorb(EnemyController source, bool grab=false)
        {
            if (!Echo || !Player.IsAlive || Paused) return false;
            Stacks=Mathf.Min(8,Stacks+1);
            TapUltVisual.Ring(Player.transform.position,(1+.12f*Stacks)*Unit,Color.cyan,.35f);
            Sfx.Play("sfx_parry",.6f,true);
            if (grab && source) { source.ApplyKnockback(source.transform.position-Player.transform.position,1.8f*Unit); source.ApplyParalysis(1); }
            return true;
        }
        public bool Taunts(EnemyController e) => Echo && Player.IsAlive && e && !e.GetComponent<BossBrain>() && Vector2.Distance(e.transform.position,Player.transform.position)<=6*Unit;
        public void ReleaseEcho()
        {
            if (!Echo || Paused) return;
            float r=(3+.6f*Stacks)*Unit;
            Area(Player.transform.position,r,2+Stacks,Stacks/2,false);
            TapUltVisual.Ring(Player.transform.position,r,Color.cyan,.65f); End();
        }
        void Area(Vector2 center,float radius,float damage,int posture,bool cone)
        {
            foreach(var e in new List<EnemyController>(EnemyController.AllEnemies)) {
                if(!e || !e.IsAlive)continue;
                Vector2 d=(Vector2)e.transform.position-center;
                if(d.magnitude>radius || (cone && d.sqrMagnitude>.0001f && Vector2.Angle(direction,d)>80))continue;
                var boss=e.GetComponent<BossBrain>();float before=e.currentHp;
                Player.DealDamage(e,(cone && boss ? 8:damage)*DamageUnit,0);
                if(e.currentHp>=before)continue;
                Hits++;
                if(boss) { for(int i=0;i<posture;i++)boss.AddPosture(); }
                else if(e.IsAlive) { if(cone)e.ApplyKnockback(direction,6*Unit);e.ApplyParalysis(1.5f); }
            }
        }
        public void Explode(Vector2 center)
        {
            if(!Player || !Player.IsAlive || Paused)return;
            Explosions++; TapUltVisual.Ring(center,3.5f*Unit,new Color(1,.45f,.05f),.65f);
            Sfx.Play("sfx_explosion",.8f); CameraRig.I?.Shake(.35f,.2f);
            foreach(var e in new List<EnemyController>(EnemyController.AllEnemies)) {
                if(!e || !e.IsAlive || Vector2.Distance(e.transform.position,center)>3.5f*Unit)continue;
                float hp=e.currentHp;Player.DealDamage(e,6*DamageUnit,0);
                if(e.currentHp>=hp)continue;
                Hits++;if(e.IsAlive){e.ApplyKnockback((Vector2)e.transform.position-center,2*Unit);var burn=TapUltBurn.Apply(this,e);if(!burns.Contains(burn))burns.Add(burn);}
            }
        }
        void End()
        {
            Active=false; TimeManager.I.ClearActionScale(this);
            if(actionFx)Destroy(actionFx);actionFx=null;
            if(Player){Player.EndUltimateLock();Player.hero?.CancelShell();Player.hero?.Stop();Player.hero?.SetUltCannonAim(0);}
        }
        public void Cancel(bool clearCharge)
        {
            Generation++; End(); foreach(var p in projectiles)if(p)Destroy(p.gameObject);projectiles.Clear();
            foreach(var b in burns)if(b)Destroy(b);burns.Clear();
            if(clearCharge)Charge=0;
        }
        void OnDisable(){if(Player)Cancel(false);}
        void OnDestroy(){if(Player)Player.DamageDealt-=Damage;if(UI)Destroy(UI.gameObject);}
    }
}
