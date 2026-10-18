using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameJam.Boss;

namespace GameJam.Gear
{
    /// <summary>Target XY combat adapter. Never owns enemy HP, AI or the existing dodge/counter.
    /// All duration state advances on the pause-aware player clock; disabling cancels owned work.</summary>
    [DefaultExecutionOrder(-70)]
    public sealed class TapGearCombat : MonoBehaviour
    {
        public PlayerController Player { get; private set; }
        // Start runs before TapHeroUlt.Start: never cache its then-zero Unit.
        public float Unit => Player && Player.Ultimate && Player.Ultimate.Unit>0 ? Player.Ultimate.Unit : Mathf.Max(.01f,TapCombatScale.HeroBounds(Player).size.y/2f);
        public bool Guarding { get; private set; }
        public bool Rooted { get; private set; }
        public int Durability { get; private set; }
        public int Stacks { get; private set; }
        public int FloatUses { get; private set; }
        public int Grapples, EelArcs, Explosions, Heals, WaterJets, Chains;
        public float GrappleRemaining => grappleCooldown;
        public bool Bubble => bubble > 0;
        public bool Busy => pull != null;
        public static int Lv(string id) => GearInventory.Level(id);
        public static float Stat(string id,float low,float high) => Mathf.Lerp(low,high,Mathf.Clamp01(GearInventory.Stat(id)/60f));
        public float MoveMultiplier => (Lv("core_siphon")>0 ? 1+Stat("core_siphon",.08f,.12f)+(Lv("core_siphon")-1)*.075f+(!Player.InCombat?.4f:0) : 1) * (Guarding ? (Lv("shell_mobile")>=2?.4f:0):1);
        public float AttackMultiplier => Lv("core_nerve")>0 ? 1+Stat("core_nerve",.1f,.2f)+(Lv("core_nerve")-1)*.1f:1;
        public float ChargeMultiplier => Lv("core_antenna")>0 ? 1+Stat("core_antenna",.15f,.25f)+(Lv("core_antenna")-1)*.15f:1;
        public float PerfectWindow => .25f+(Lv("core_logic")>0 ? Stat("core_logic",.06f,.1f)+(Lv("core_logic")-1)*.04f:0);
        public float DodgeMultiplier => Lv("leg_jet")>0?1.6f:1;
        public int DodgeCharges { get; private set; } = 3;
        public float RechargeRemaining { get; private set; }
        public bool UsesCharges => Lv("leg_jet")>=2;
        public bool DodgeAvailable => !UsesCharges || DodgeCharges>0;
        public void SpendDodge(){if(UsesCharges){DodgeCharges=Mathf.Max(0,DodgeCharges-1);if(RechargeRemaining<=0)RechargeRemaining=Lv("core_siphon")>=3?2:2.5f;}}
        public void RestoreDodge(){DodgeCharges=Mathf.Min(3,DodgeCharges+1);}
        public bool WarningActive { get; private set; }
        public float WarningLead => Lv("core_logic")>0?.45f:.25f;
        float clock, guardAt, broken, bubble, grappleCooldown, still, healClock, safeClock, rootHeal;
        float spaceHeld; bool spaceDown, rolled, wasUlt;
        int hits; Vector3 anchor, dodgeStart; Vector2 guardDirection;
        readonly HashSet<EnemyController> encounters = new HashSet<EnemyController>();
        readonly HashSet<EnemyController> rammed = new HashSet<EnemyController>();
        readonly List<Zone> zones=new List<Zone>();
        class Zone { public Vector3 a,b; public float radius,left; public int kind; public GameObject decoy; }
        class Pull { public Transform target; public EnemyController enemy; public FurnitureObject furniture; public bool self; public float remaining; }
        Pull pull;
        TapGearUI ui;
        public static bool Paused => EnemyController.CombatWorldPaused;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
        static void Loaded(Scene s,LoadSceneMode m){foreach(var p in FindObjectsOfType<PlayerController>())if(!p.GetComponent<TapGearCombat>())p.gameObject.AddComponent<TapGearCombat>();}
        void Awake(){Player=GetComponent<PlayerController>();}
        void Start(){anchor=transform.position;Refresh();ui=TapGearUI.Build(this);}
        void OnEnable(){GearInventory.Changed+=Refresh;Player.DodgeStarted+=DodgeStart;Player.DodgeEnded+=DodgeEnd;Player.PerfectDodge+=Perfect;}
        void OnDisable(){GearInventory.Changed-=Refresh;Player.DodgeStarted-=DodgeStart;Player.DodgeEnded-=DodgeEnd;Player.PerfectDodge-=Perfect;Cancel();if(ui)ui.Close();}
        void OnDestroy(){if(ui)Destroy(ui.gameObject);}
        public void Cancel(){SetGuard(false);Rooted=false;pull=null;bubble=0;spaceDown=false;spaceHeld=0;rolled=false;foreach(var z in zones)if(z.decoy)Destroy(z.decoy);zones.Clear();WarningActive=false;GearFx.Root(transform,false);TapGearFx.Clear(this);}
        void Refresh(){Cancel();Durability=Mathf.RoundToInt(Stat("shell_mobile",3,8));Stacks=0;DodgeCharges=3;RechargeRemaining=0;FloatUses=Lv("shell_float")>=3?2:Lv("shell_float")>0?1:0;hits=0;safeClock=healClock=still=0;}
        void Update()
        {
            if(!Player || !Player.isActiveAndEnabled || !Player.IsAlive){Cancel();return;}
            if(Paused){spaceDown=false;spaceHeld=0;SetGuard(false);return;}
            Tick(TimeManager.PlayerDelta);
            if(Input.GetKeyDown(KeyCode.Q)) {var cam=Camera.main;if(cam){var a=cam.ScreenToWorldPoint(Input.mousePosition);a.z=transform.position.z;TryGrapple(a);}}
            if(Input.GetKeyDown(KeyCode.Space)){spaceDown=true;spaceHeld=0;}
            if(spaceDown && Input.GetKey(KeyCode.Space)){spaceHeld+=TimeManager.PlayerDelta;if(spaceHeld>.25f)SetGuard(true);}
            if(Input.GetKeyUp(KeyCode.Space)){if(spaceDown && spaceHeld<=.25f)Player.TriggerShellGuard();spaceDown=false;SetGuard(false);}
        }
        public void Tick(float dt)
        {
            if(Paused || !isActiveAndEnabled || !Player || !Player.isActiveAndEnabled || !Player.IsAlive || dt<=0)return;
            if(UsesCharges && DodgeCharges<3){RechargeRemaining-=dt;if(RechargeRemaining<=0){DodgeCharges++;RechargeRemaining=DodgeCharges<3?(Lv("core_siphon")>=3?2:2.5f):0;}}
            bool warning=false;
            foreach(var e in Nearby(transform.position,12*Unit)){var b=e.GetComponent<BossBrain>();float remaining=b?b.GearImpactRemaining:e.GearImpactRemaining;if(remaining>=0 && remaining<=WarningLead){warning=true;break;}}
            if(warning!=WarningActive){WarningActive=warning;TapGearFx.Warning(transform,warning);}
            clock+=dt;grappleCooldown=Mathf.Max(0,grappleCooldown-dt);bubble=Mathf.Max(0,bubble-dt);
            if(broken>0){broken-=dt;if(broken<=0)Durability=Mathf.RoundToInt(Stat("shell_mobile",3,8));}
            if(Guarding && (Player.Unavailable || Player.UltimateLocked || Player.Placing || rolled))SetGuard(false);
            var target=Player.currentTarget;
            if(target && target.IsAlive && encounters.Add(target))FloatUses=Lv("shell_float")>=3?2:Lv("shell_float")>0?1:0;
            if(Lv("core_fungi")>0){float previous=safeClock;safeClock+=dt;if(safeClock>=6){healClock+=Mathf.Max(0,safeClock-6)-Mathf.Max(0,previous-6);float interval=Mathf.Max(2,Stat("core_fungi",5,3)-(Lv("core_fungi")>=2?1:0));if(healClock>=interval){healClock-=interval;Heal();}}}
            if(Lv("leg_root")>0){bool moved=Vector2.Distance(anchor,transform.position)>.15f*Unit || rolled || Player.Unavailable || Player.UltimateLocked;if(moved){anchor=transform.position;still=0;if(Rooted){Rooted=false;GearFx.Root(transform,false);Blast(transform.position,1.5f*Unit,2);GearFx.CoralSpikes(transform.position,1.5f*Unit);}}else still+=dt;
                if(!Rooted && still>=(Lv("leg_root")>=2?.6f:1)){Rooted=true;GearFx.Root(transform,true);}if(Rooted && Lv("leg_root")>=3){rootHeal+=dt;if(rootHeal>=2){rootHeal-=2;Heal();}}else rootHeal=0;}
            if(rolled && Lv("leg_jet")>=3)foreach(var e in Nearby(transform.position,1.2f*Unit))if(rammed.Add(e))e.Displace(((Vector2)(e.transform.position-transform.position)).normalized,1.5f*Unit,this);
            bool ult=Player.Ultimate && Player.Ultimate.Active;if(wasUlt && !ult && Lv("core_antenna")>=3)Player.GearRestoreDodge();wasUlt=ult;
            TickPull(dt);
            for(int i=zones.Count-1;i>=0;i--){var z=zones[i];z.left-=dt;if(z.left<=0){if(z.kind==2){Blast(z.a,z.radius,3);GearFx.Explosion(z.a,z.radius);Explosions++;}if(z.decoy)Destroy(z.decoy);zones.RemoveAt(i);continue;}if(z.kind==2){if(z.decoy)foreach(var e in Nearby(z.a,z.radius*2))e.SetGearTaunt(z.decoy.transform);continue;}
                foreach(var e in EnemyController.AllEnemies.ToArray())if(e && e.IsAlive && Distance(e.transform.position,z.a,z.b)<=z.radius){if(z.kind==0)e.ApplySlow(30f,.25f);else e.Wet(.25f);}}
        }
        public bool SetGuard(bool on)
        {
            if(on && (Paused || Lv("shell_mobile")==0 || broken>0 || !Player.CanBeginUltimate || Player.UltimateLocked || Busy))return false;
            if(Guarding==on)return Guarding;
            Guarding=on;if(on){guardAt=clock;Player.StopMovement();var e=Player.NearestEnemy(30*Unit);guardDirection=e?((Vector2)(e.transform.position-transform.position)).normalized:(Player.IsFacingLeft?Vector2.left:Vector2.right);}
            if(Player.hero){if(on)Player.hero.BeginUltEcho();else if(!Player.UltimateLocked)Player.hero.CancelShell();}GearFx.ShellGuard(transform,on);return Guarding;
        }
        public bool FilterDamage(EnemyController src,bool blockable,ref float damage)
        {
            if(!isActiveAndEnabled)return false;
            if(Bubble)return true;
            if(Guarding && src && Vector2.Angle(guardDirection,(Vector2)(src.transform.position-transform.position))<=75){bool perfect=clock-guardAt<=.15f;Durability--;GearFx.ShellBlock(transform.position,perfect);
                if(Lv("shell_mobile")>=3 && src.GetComponent<BossBrain>()?.CurrentMove==BossMoves.P2_EyeBeam){Damage(src,1);GearFx.Counter(transform.position,src.transform.position);if(Durability<=0){broken=3;SetGuard(false);}return true;}
                if(perfect){var boss=src.GetComponent<BossBrain>();if(boss)boss.AddPosture(this);else src.Parried();if(Lv("shell_mobile")>=3)foreach(var e in Nearby(transform.position,2*Unit))e.Displace(((Vector2)(e.transform.position-transform.position)).normalized,2*Unit,this);}
                if(Durability<=0){broken=3;SetGuard(false);}if(blockable)return true;damage*=.5f;}
            if(Rooted)damage*=1-Stat("leg_root",.3f,.5f);
            if(Lv("shell_float")>0 && FloatUses>0 && damage>=Player.currentHp){FloatUses--;bubble=1.5f;Heal();GearFx.FloatBubble(transform,1.5f);if(Lv("shell_float")>=2)foreach(var e in Nearby(transform.position,3*Unit))e.Displace(((Vector2)(e.transform.position-transform.position)).normalized,Stat("shell_float",2,5)*Unit,this);return true;}
            return false;
        }
        public void Hurt(){safeClock=healClock=0;if(Lv("shell_static")>0){Stacks=Mathf.Min(3,Stacks+1);GearFx.StaticStacks(transform,Stacks);}}
        public void Block()
        {
            if(Lv("shell_static")==0)return;if(Stacks<3){Stacks++;GearFx.StaticStacks(transform,Stacks);return;}
            var points=new List<Vector3>{transform.position};var list=Nearby(transform.position,6*Unit);int max=Lv("shell_static")+2;
            foreach(var e in list){if(points.Count>max)break;Damage(e,Stat("shell_static",1,4)*(e.IsWet?1.5f:1));if(Lv("shell_static")>=3)e.ApplyParalysis(.5f);points.Add(e.transform.position);}Stacks=0;GearFx.StaticStacks(transform,0);GearFx.ChainLightning(points.ToArray());
        }
        void Heal(){if(Player.currentHp>=Player.maxHp)return;Player.Heal(Player.maxHp/5f);Heals++;GearFx.Heal(transform);}
        public void Executed(){if(Lv("core_fungi")>=3)Heal();}
        public void NormalHit(EnemyController e)
        {
            if(!e || !e.IsAlive)return;hits++;
            if(Lv("core_nerve")>=3 && hits%5==0)Damage(e,1);
            if(Lv("arm_eel")==0 || (!e.IsWet && hits%(Lv("arm_eel")>=2?3:4)!=0))return;
            Shock(e,transform.position);if(Lv("arm_eel")>=3)foreach(var other in Nearby(e.transform.position,4*Unit))if(other!=e){Shock(other,e.transform.position);break;}
        }
        void Shock(EnemyController e,Vector3 from){Damage(e,1);e.ApplyParalysis(Stat("arm_eel",.4f,.9f));GearFx.EelArc(from,e.transform.position);EelArcs++;}
        public void CounterHit(List<EnemyController> first)
        {
            if(Lv("arm_spring")<2 || first.Count==0)return;var hit=new HashSet<EnemyController>(first);Vector3 point=first[0].transform.position;
            for(int i=1;i<Lv("arm_spring");i++){EnemyController next=null;foreach(var e in Nearby(point,Stat("arm_spring",5,8)*Unit))if(!hit.Contains(e)){next=e;break;}if(!next)break;hit.Add(next);GearFx.Counter(point,next.transform.position);Damage(next,3);point=next.transform.position;Chains++;}
            if(Lv("arm_spring")>=3)foreach(var e in hit)if(e && e.IsAlive){Vector2 d=point-e.transform.position;e.Displace(d.normalized,d.magnitude*.7f,this);}
        }
        public bool WaterSmash(Vector3 dir,float damage)
        {
            if(Lv("arm_hydro")==0)return false;dir.z=0;dir.Normalize();if(dir.sqrMagnitude<.01f)dir=Player.IsFacingLeft?Vector3.left:Vector3.right;
            float range=(Stat("arm_hydro",4,6)+(Lv("arm_hydro")>=2?2:0)+(Lv("arm_hydro")>=3?1:0))*Unit;Vector3 end=transform.position+dir*range;
            foreach(var e in Nearby(transform.position,range)){var d=e.transform.position-transform.position;float along=Vector3.Dot(d,dir);if(along<0 || along>range || (d-dir*along).magnitude>1.1f*Unit)continue;Player.DealDamage(e,damage,0);e.Wet(4);e.Displace(dir,2*Unit,this);if(Lv("arm_hydro")<3)break;}
            GearFx.WaterJet(transform.position,end,2.2f*Unit);if(Lv("arm_hydro")>=3){zones.Add(new Zone{a=end,b=end,radius=1.5f*Unit,left=6,kind=1});GearFx.Puddle(end,1.5f*Unit,6);}WaterJets++;return true;
        }
        void DodgeStart(){SetGuard(false);rolled=true;dodgeStart=transform.position;rammed.Clear();}
        void DodgeEnd(){if(rolled && Lv("leg_jet")>0){float life=Stat("leg_jet",1.5f,3);zones.Add(new Zone{a=dodgeStart,b=transform.position,radius=1.2f*Unit,left=life});GearFx.JetTrail(dodgeStart,transform.position,life);}rolled=false;}
        void Perfect(){GearFx.PerfectDodge(transform.position);TimeManager.I.StartSlowMo(Lv("core_logic")>=3?.3f:.15f);if(Lv("leg_decoy")>0)for(int i=0;i<(Lv("leg_decoy")>=3?2:1);i++){var p=dodgeStart+Vector3.right*(i*.9f*Unit);float life=(Lv("leg_decoy")>=2?1:.5f)+i*.12f;var decoy=Lv("leg_decoy")>=2?new GameObject("Gear taunt decoy"):null;if(decoy)decoy.transform.position=p;zones.Add(new Zone{a=p,b=p,radius=Stat("leg_decoy",2,3.5f)*Unit,left=life,kind=2,decoy=decoy});GearFx.Decoy(p,life);}}
        public bool TryGrapple(Vector3 aim)
        {
            if(Paused || Busy || grappleCooldown>0 || Lv("arm_grapple")==0 || !Player.CanBeginUltimate || Player.UltimateLocked || Guarding)return false;
            EnemyController target=null;float best=2.4f*Unit;foreach(var e in Nearby(transform.position,10*Unit)){float d=Vector2.Distance(e.transform.position,aim);if(d<best){best=d;target=e;}}
            FurnitureObject furniture=null;
            foreach(var f in FurnitureObject.AllFurniture)if(f && f.Targetable && !f.isImmovable && Vector2.Distance(transform.position,f.transform.position)<=10*Unit){float d=Vector2.Distance(f.transform.position,aim);if(d<best){best=d;furniture=f;target=null;}}
            if(!target && !furniture)return false;
            var t=target?target.transform:furniture.transform;
            pull=new Pull{target=t,enemy=target,furniture=furniture,self=target && target.GetComponent<BossBrain>()!=null,remaining=.7f};Player.StopMovement();grappleCooldown=Lv("arm_grapple")>=2?4:6;Grapples++;GearFx.Grapple(transform.position,t.position,.7f);return true;
        }
        void TickPull(float dt)
        {
            if(pull==null)return;if(!pull.target || (pull.enemy && !pull.enemy.IsAlive) || Player.Unavailable || Player.UltimateLocked || Player.Placing || rolled){pull=null;return;}
            var d=pull.target.position-transform.position;float stop=(pull.self?2.2f:1.4f)*Unit;float move=Mathf.Min(Mathf.Max(0,d.magnitude-stop),Stat("arm_grapple",8,20)*Unit*dt);
            if(pull.furniture){pull.remaining-=dt;if(pull.remaining<=0){pull.furniture.GrappleTo(transform.position+d.normalized*1.4f*Unit);pull=null;}return;}
            if(pull.self)Player.GearMoveSafe(d.normalized*move);else pull.enemy.Displace(-d.normalized,move,this);
            pull.remaining-=dt;if(d.magnitude<=stop+.02f || pull.remaining<=0){if(Lv("arm_grapple")>=3 && Vector2.Distance(transform.position,pull.target.position)<=3*Unit){Damage(pull.enemy,3);GearFx.Counter(transform.position,pull.target.position);}pull=null;}
        }
        public void GrappleNearest(){var e=Player.currentTarget?Player.currentTarget:Player.NearestEnemy(10*Unit);if(e)TryGrapple(e.transform.position);}
        void Blast(Vector3 p,float radius,float damage){foreach(var e in Nearby(p,radius))Damage(e,damage);}
        void Damage(EnemyController e,float units){if(e && e.IsAlive)Player.DealDamage(e,units*Player.CurrentAttackDamage,0);}
        static float Distance(Vector3 p,Vector3 a,Vector3 b){Vector2 ab=b-a,ap=p-a;return (ap-ab*(ab.sqrMagnitude<.0001f?0:Mathf.Clamp01(Vector2.Dot(ap,ab)/ab.sqrMagnitude))).magnitude;}
        static List<EnemyController> Nearby(Vector3 p,float radius){var r=new List<EnemyController>();foreach(var e in EnemyController.AllEnemies.ToArray())if(e && e.IsAlive && Vector2.Distance(e.transform.position,p)<=radius)r.Add(e);r.Sort((a,b)=>Vector2.Distance(a.transform.position,p).CompareTo(Vector2.Distance(b.transform.position,p)));return r;}
    }
}
