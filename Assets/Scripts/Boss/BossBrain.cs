using System;
using System.Collections.Generic;
using UnityEngine;
using GameJam.Boss;

/// <summary>XY world combat. EnemyController is the sole HP and lifecycle owner.
/// P3 observes MoveStarted/Impact/ThresholdStarted; it must not deal damage or advance clocks.</summary>
public sealed class BossBrain : MonoBehaviour
{
    public EnemyController Entity { get; private set; }
    public BossAnimator Animator { get; private set; }
    public int Phase { get; private set; } = 1;
    public int Posture { get; private set; }
    public int ThresholdIndex { get; private set; }
    public string CurrentMove { get; private set; }
    public float Elapsed { get; private set; }
    public bool AutoAttack = true;
    public float IntroHoldRemaining { get; private set; }
    public void HoldForIntro(float seconds)
    {
        Cancel();
        IntroHoldRemaining = Mathf.Max(0, seconds);
        Animator.SetLocomotion(Vector3.zero);
        Animator.SetTelegraph(0, Color.white);
        if (Player) Animator.SetFacing(Player.transform.position.x >= transform.position.x ? 1 : -1);
    }
    public int NextImpact => nextImpact;
    public float GearImpactRemaining => nextImpact<impacts.Length?impacts[nextImpact]-Elapsed:float.PositiveInfinity;
    public bool Invulnerable => thresholdActive && (ThresholdIndex==1 || ultimateCut<(ThresholdIndex==0?1:2));
    public event Action<string> MoveStarted;
    public event Action<string,int> Impact;
    public event Action<int> ThresholdStarted;
    public event Action PresentationCancelled;
    public BossAttackShape LastImpactShape { get; private set; }
    public bool LastImpactPlayerDamaged { get; private set; }
    public float UnitScale { get; private set; }
    public Vector2 Aim { get; private set; }
    public Vector2 Origin { get; private set; }
    public Vector2 Direction { get; private set; }
    public bool Yellow => !thresholdActive && IsBlockable(CurrentMove,nextImpact);
    public static bool IsBlockable(string move,int impact) => move==BossMoves.P1_Claw3 || move==BossMoves.P1_Lunge || (move==BossMoves.P2_SpinThrust && impact==0) || (move==BossMoves.P2_Claw5 && impact<4);
    static readonly float[] Marks={.7f,.5f,.3f};
    float[] impacts=Array.Empty<float>();
    int nextImpact, ultimateCut;
    float cooldown=.6f, stun, pending, worldClock, grabTime, nextGrabTick;
    bool thresholdActive, deferredLethal, ownsGrab;
    Vector2 arenaCenter;
    BossGroundEffects ground;
    readonly Dictionary<int,float> knockSources=new Dictionary<int,float>();
    PlayerController Player => PlayerController.Instance;
    public static float[] ImpactsFor(string m)
    {
        switch(m) {
            case BossMoves.P1_Claw3:return new[]{.55f,1.05f,1.9f};
            case BossMoves.P1_Lunge:return new[]{.85f};
            case BossMoves.P1_LeapSlam:return new[]{1.4f};
            case BossMoves.P1_CloakSweep:return new[]{1f};
            case BossMoves.P1_Wrap:return new[]{1.2f};
            case BossMoves.P2_SpinThrust:return new[]{.6f,1.3f};
            case BossMoves.P2_Claw5:return new[]{.4f,.75f,1f,1.45f,2.2f};
            case BossMoves.P2_EyeBeam:return new[]{.8f,1.1f,1.4f,1.7f,2f,2.3f};
            default:return Array.Empty<float>();
        }
    }
    public void Initialize(EnemyController entity, BossAnimator animator, float scale)
    {
        Entity=entity; Animator=animator; UnitScale=scale;
        arenaCenter=transform.position; ground=gameObject.AddComponent<BossGroundEffects>();
        Animator.enabled=false; Animator.Initialize();
        Entity.CombatPolicy=new Policy(this); Entity.ExternalAI=(e,dt)=>Tick(dt);
        Entity.ExternalParryWindow=e=>Yellow && nextImpact<impacts.Length && Elapsed>=impacts[nextImpact]-.3f && Elapsed<=impacts[nextImpact];
        Entity.enableClockQTE=false; Entity.autoRespawn=false; Entity.Died+=OnDeath;
    }
    void OnDeath(EnemyController e) { Cancel(); ground.Clear(); Animator.Die(2.5f); }
    void LateUpdate() { if(Entity && !Entity.IsAlive && !EnemyController.CombatWorldPaused)Animator.Tick(Time.deltaTime); }
    void OnDisable() { Cancel(); if(ground)ground.Clear(); }
    void OnDestroy() { ReleaseGrab(); if(Entity) Entity.Died-=OnDeath; }
    void ReleaseGrab() { if(ownsGrab && Player && Player.IsGrabbedBy(Entity)) Player.ThrowFrom(Entity); ownsGrab=false; if(EnemyController.Grabber==Entity) Entity.CancelGrab(); }
    void Cancel() { ReleaseGrab(); CurrentMove=null; impacts=Array.Empty<float>(); nextImpact=0; PresentationCancelled?.Invoke(); }
    public void AddPosture(UnityEngine.Object source=null)
    {
        if(!Entity || !Entity.IsAlive || Invulnerable || Posture>=3) return;
        if(source) { int id=source.GetInstanceID(); if(knockSources.TryGetValue(id,out float t) && worldClock-t<.5f) return; knockSources[id]=worldClock; }
        Posture=Mathf.Min(3,Posture+1); Cancel(); stun=.6f; Animator.Stagger(.6f);
        if(Posture==3) { pending=3; Entity.aiState=EnemyController.EnemyAIState.Pending; }
    }
    public bool ForceMove(string move)
    {
        if(!Entity.IsAlive || IntroHoldRemaining>0 || thresholdActive || Posture==3 || ImpactsFor(move).Length==0) return false;
        if(move==BossMoves.P1_Wrap && EnemyController.Grabber!=null) return false;
        Cancel(); CurrentMove=move; Elapsed=0; nextImpact=0; impacts=ImpactsFor(move);
        Origin=transform.position; Aim=Player ? (Vector2)Player.transform.position : Origin+Vector2.right;
        Direction=(Aim-Origin).normalized; if(Direction.sqrMagnitude<.01f) Direction=Vector2.right;
        Entity.aiState=EnemyController.EnemyAIState.Attack;
        Animator.SetFacing(Direction.x>=0?1:-1); Animator.Play(move,impacts); MoveStarted?.Invoke(move); return true;
    }
    public void Tick(float dt)
    {
        if(!isActiveAndEnabled || !Entity || !Entity.IsAlive || EnemyController.CombatWorldPaused || dt<=0) return;
        worldClock+=dt;
        if(IntroHoldRemaining>0) {
            IntroHoldRemaining=Mathf.Max(0,IntroHoldRemaining-dt);
            if(Player) Animator.SetFacing(Player.transform.position.x>=transform.position.x?1:-1);
            Animator.SetLocomotion(Vector3.zero); Animator.Tick(dt); return;
        }
        if(Entity.aiState==EnemyController.EnemyAIState.Executing) return;
        if(thresholdActive) { AdvanceThreshold(dt); Animator.Tick(dt); return; }
        if(ThresholdIndex<3 && Entity.currentHp/Entity.maxHp<=Marks[ThresholdIndex]) { BeginThreshold(); return; }
        if(deferredLethal && ThresholdIndex==3) { deferredLethal=false; Entity.TakeDamage(Entity.maxHp,0,transform.position); return; }
        if(stun>0) { stun-=dt; Animator.Tick(dt); return; }
        if(Posture>=3) { pending-=dt; Animator.SetTelegraph(1,Color.cyan); if(pending<=0) { Posture=0; Entity.CancelExecution(); Entity.aiState=EnemyController.EnemyAIState.Chase; } return; }
        if(ownsGrab) { grabTime+=dt; if(grabTime>=nextGrabTick) { nextGrabTick+=1; if(Player) Player.GrabTick(Player.maxHp*.2f); } if(grabTime>=2) { ReleaseGrab(); Cancel(); cooldown=1.2f; } return; }
        if(CurrentMove==null) {
            if(AutoAttack && Player && Player.IsAlive) {
                Vector2 delta=(Vector2)Player.transform.position-(Vector2)transform.position;
                Vector2 velocity=delta.magnitude>2.2f*UnitScale?delta.normalized*2.2f*UnitScale*(Phase==2?1.3f:1)*Entity.slowMultiplier:Vector2.zero;
                Vector2 before=transform.position;
                bool reached=MoveSafely(before+velocity*dt);
                // Imported gait is authored in source metres; feed actual, not blocked desired motion.
                Vector2 actual=((Vector2)transform.position-before)/(dt*UnitScale);
                Animator.SetLocomotion(new Vector3(actual.x,0,actual.y));Animator.SetFacing(delta.x>=0?1:-1);Animator.Tick(dt);
                if(!reached && cooldown<=0 && FurnitureObject.NearestTargetable(transform.position,3.5f*UnitScale)) { ForceMove(BossMoves.P1_CloakSweep);return; }
            }
            cooldown-=dt; if(!AutoAttack || !Player || !Player.IsAlive || cooldown>0) return;
            float d=Vector2.Distance(transform.position,Player.transform.position)/UnitScale, roll=UnityEngine.Random.Range(0,100);
            string m=Phase==1 ? (d>=3 ? (roll<50?BossMoves.P1_Lunge:BossMoves.P1_LeapSlam) : roll<40?BossMoves.P1_Claw3:roll<60?BossMoves.P1_CloakSweep:roll<75?BossMoves.P1_Wrap:BossMoves.P1_LeapSlam)
                : roll<30?BossMoves.P2_SpinThrust:roll<55?BossMoves.P2_Claw5:roll<70?BossMoves.P2_EyeBeam:roll<90?BossMoves.P1_LeapSlam:BossMoves.P1_Wrap;
            ForceMove(m); return;
        }
        Elapsed+=dt;
        if(CurrentMove==BossMoves.P1_Lunge && Elapsed>=.7f) MoveSafely(Origin+Direction*(6*UnitScale*Mathf.Clamp01((Elapsed-.7f)/.15f)));
        if(CurrentMove==BossMoves.P1_LeapSlam && Elapsed<1 && Player) Aim=Player.transform.position;
        Animator.SetTelegraph(1,Yellow?Color.yellow:Color.red); Animator.Tick(dt);
        while(CurrentMove!=null && nextImpact<impacts.Length && Elapsed>=impacts[nextImpact]) { int i=nextImpact; ResolveImpact(i); if(CurrentMove!=null) nextImpact=i+1; }
        if(CurrentMove!=null && !ownsGrab && Elapsed>impacts[impacts.Length-1]+.5f) { Cancel(); cooldown=UnityEngine.Random.Range(1.2f,2f)*(Phase==2?.7f:1); Entity.aiState=EnemyController.EnemyAIState.Chase; Animator.SetTelegraph(0,Color.white); }
    }
    void BeginThreshold()
    {
        Cancel(); thresholdActive=true; Elapsed=0; ultimateCut=0;
        CurrentMove=ThresholdIndex==1?BossMoves.Transform:BossMoves.Ult_Rise;
        Origin=transform.position; Aim=Player?(Vector2)Player.transform.position:Origin;
        Direction=(Aim-Origin).normalized; if(Direction.sqrMagnitude<.01f) Direction=Vector2.right;
        Animator.Play(CurrentMove,Array.Empty<float>()); ThresholdStarted?.Invoke(ThresholdIndex); MoveStarted?.Invoke(CurrentMove);
    }
    void AdvanceThreshold(float dt)
    {
        Elapsed+=dt;
        if(ThresholdIndex==1) { MoveSafely(Vector2.Lerp(Origin,arenaCenter,Mathf.Clamp01(Elapsed/.5f))); if(Elapsed>=2.5f) { Phase=2; Animator.SetPhase(2); FinishThreshold(); } return; }
        if(Elapsed<3 && Player) { Aim=Player.transform.position;Vector2 target=Aim-Origin;if(target.sqrMagnitude>.001f)Direction=target.normalized; }
        if(ultimateCut==0 && Elapsed>=3.6f) { Cleave(); ultimateCut++; if(ThresholdIndex==0) Animator.Play(BossMoves.Ult_Exhausted,Array.Empty<float>()); }
        if(ThresholdIndex==2 && ultimateCut==1) {
            if(Elapsed<4.8f && Player) Aim=Player.transform.position;
            if(Elapsed>=5.2f) { Direction=new Vector2(-Direction.y,Direction.x); Cleave(); ultimateCut++; Animator.Play(BossMoves.Ult_Exhausted,Array.Empty<float>()); Posture=3;pending=4;Entity.aiState=EnemyController.EnemyAIState.Pending; }
        }
        float end=ThresholdIndex==0?6.6f:9.2f;
        if(!Invulnerable && Posture==3) pending-=dt;
        if(Elapsed>=end) { FinishThreshold(); if(pending<=0) { Posture=0;Entity.aiState=EnemyController.EnemyAIState.Chase; } }
    }
    void FinishThreshold() { thresholdActive=false; ThresholdIndex++; Cancel(); cooldown=.5f; }
    void Cleave()
    {
        bool protectedPlayer=false;
        foreach(var f in FindObjectsOfType<FurnitureObject>()) if(IsCover(f) && InLine(f.transform.position,Aim-Direction*9*UnitScale,Direction,18*UnitScale,1.6f*UnitScale)) {
            if(Player && Vector2.Distance(f.transform.position,Player.transform.position)<=2*UnitScale) protectedPlayer=true;
            f.TakeDamage(f.currentDurability,Entity);
        }
        if(Player && !protectedPlayer && InLine(Player.transform.position,Aim-Direction*9*UnitScale,Direction,18*UnitScale,1.6f*UnitScale)) Player.TakeDamage(Player.maxHp*.6f,Entity,false);
        LastImpactShape=new BossAttackShape {kind=BossAttackShape.Kind.Line,origin=Aim-Direction*9*UnitScale,direction=Direction,length=18*UnitScale,width=1.6f*UnitScale};
        Animator.Play(BossMoves.Ult_Cleave,Array.Empty<float>()); Impact?.Invoke(BossMoves.Ult_Cleave,ultimateCut);
        ground.Add(new BossAttackShape {kind=BossAttackShape.Kind.Line,origin=Aim-Direction*9*UnitScale,direction=Direction,length=18*UnitScale,width=1.6f*UnitScale},Phase==1?BossElement.Dark:Element);
    }
    public BossElement Element=>Entity.currentHp/Entity.maxHp<=.25f?BossElement.Electric:BossElement.Water;
    public bool MoveSafely(Vector2 target)
    {
        var map=IsometricPathfinder.Instance; Vector2 start=transform.position;
        int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(start,target)/(.15f*UnitScale)));
        float radius=.6f*UnitScale; Vector2 last=start;
        for(int i=1;i<=steps;i++) {
            Vector2 point=Vector2.Lerp(start,target,i/(float)steps);bool safe=true;
            if(map) for(int j=0;j<8;j++) { Vector2 edge=point+(Vector2)(Quaternion.Euler(0,0,j*45)*Vector2.right)*radius;if(!map.IsWalkable(map.WorldToCell(edge))){safe=false;break;} }
            if(!safe)break;last=point;
        }
        transform.position=new Vector3(last.x,last.y,transform.position.z);return (last-target).sqrMagnitude<.00000001f;
    }
    public static bool IsCover(FurnitureObject f) => f && f.Targetable && (f.instanceId=="furn_coral_wall" || f.instanceId=="scrap_symbiotic_sludge" || f.instanceId=="scrap_hybrid_residue");
    public static bool InLine(Vector2 p,Vector2 origin,Vector2 direction,float length,float width) { Vector2 d=p-origin; float along=Vector2.Dot(d,direction); return along>=0 && along<=length && Mathf.Abs(d.x*direction.y-d.y*direction.x)<=width*.5f; }
    public bool Contains(Vector2 p,int i) => Shape(i).Contains(p);
    public BossAttackShape Shape(int i)
    {
        var s=new BossAttackShape { kind=BossAttackShape.Kind.Cone, origin=transform.position,direction=Direction,radius=2.6f*UnitScale,halfAngle=70 };
        if(thresholdActive && ThresholdIndex!=1) {
            s.kind=BossAttackShape.Kind.Line; s.direction=ThresholdIndex==2 && ultimateCut==1?new Vector2(-Direction.y,Direction.x):Direction;
            s.origin=Aim-s.direction*9*UnitScale;s.length=18*UnitScale;s.width=1.6f*UnitScale; return s;
        }
        switch(CurrentMove) {
            case BossMoves.P1_LeapSlam:s.kind=BossAttackShape.Kind.Circle;s.origin=Aim;s.radius=2.8f*UnitScale;break;
            case BossMoves.P1_CloakSweep:s.kind=BossAttackShape.Kind.Circle;s.radius=3.5f*UnitScale;break;
            case BossMoves.P1_Wrap:case BossMoves.Transform:s.kind=BossAttackShape.Kind.Circle;break;
            case BossMoves.P1_Lunge:s.kind=BossAttackShape.Kind.Line;s.origin=Origin;s.length=6*UnitScale;s.width=1.2f*UnitScale;break;
            case BossMoves.P2_SpinThrust:if(i==0) { s.kind=BossAttackShape.Kind.Circle;s.radius=2.4f*UnitScale; } else { s.kind=BossAttackShape.Kind.Line;s.origin=Origin;s.length=5*UnitScale;s.width=1.2f*UnitScale; } break;
            case BossMoves.P2_Claw5:if(i==4)s.kind=BossAttackShape.Kind.Circle;break;
            case BossMoves.P2_EyeBeam:s.kind=BossAttackShape.Kind.Line;s.origin=Origin;s.direction=BeamDirection(Mathf.Min(5,i));s.length=8*UnitScale;s.width=1.1f*UnitScale; ClipBeam(ref s);break;
        }
        return s;
    }
    Vector2 BeamDirection(int i) => Quaternion.Euler(0,0,-60+i*24)*Direction;
    // Snapshot the same clipped shape before furniture damage: destroying a cover this
    // impact must not let that same impact leak through it. Behind/off-axis cover is ignored.
    FurnitureObject ClipBeam(ref BossAttackShape shape)
    {
        FurnitureObject nearest=null; float distance=shape.length;
        foreach(var f in FindObjectsOfType<FurnitureObject>()) {
            if(!IsCover(f) || !shape.Contains(f.transform.position))continue;
            float along=Vector2.Dot((Vector2)f.transform.position-shape.origin,shape.direction);
            if(along<=distance) { distance=along;nearest=f; }
        }
        if(nearest)shape.length=distance;
        return nearest;
    }
    void ResolveImpact(int i)
    {
        string move=CurrentMove;
        float playerHpBefore=Player?Player.currentHp:0;
        var shape=Shape(i);
        if(move==BossMoves.P1_LeapSlam) MoveSafely(Aim);
        bool yellow=IsBlockable(move,i);
        if(move==BossMoves.P2_EyeBeam) { var cover=ClipBeam(ref shape); if(cover)cover.TakeDamage(6,Entity); }
        foreach(var f in FindObjectsOfType<FurnitureObject>()) {
            if(!f.Targetable || !shape.Contains(f.transform.position)) continue;
            if(move==BossMoves.P1_CloakSweep) f.TakeDamage(12,Entity);
        }
        if(Player && Player.IsAlive && shape.Contains(Player.transform.position)) {
            if(move==BossMoves.P1_Wrap) { if(EnemyController.Grabber==null && Player.TryGrab(Entity)) { EnemyController.Grabber=Entity; ownsGrab=true; grabTime=0; nextGrabTick=1; } }
            else Player.TakeDamage(Player.maxHp*((move==BossMoves.P1_LeapSlam || (move==BossMoves.P2_Claw5 && i==4))?.4f:.2f),Entity,yellow);
        }
        LastImpactPlayerDamaged=Player && Player.currentHp<playerHpBefore;
        LastImpactShape=shape; Impact?.Invoke(move,i);
        if(Phase==2) {
            Vector2 side=new Vector2(-Direction.y,Direction.x);
            foreach(var offset in new[]{-Direction,side,-side}) ground.Add(new BossAttackShape {kind=BossAttackShape.Kind.Circle,origin=(Vector2)transform.position+offset*2.3f*UnitScale,direction=Vector2.right,radius=1.65f*UnitScale},Element);
        }
    }
    sealed class Policy : EnemyCombatPolicy
    {
        readonly BossBrain b; public Policy(BossBrain brain) { b=brain; }
        public override bool InterruptOnHit=>false;
        public override bool VulnerabilityBonus=>false;
        public override float DeathDuration=>2.5f;
        public override bool ReceiveDamage(EnemyController e,ref float d,ref float g) {
            g=0; if(b.Invulnerable)return false;
            if(d>=e.currentHp && b.ThresholdIndex<3) { b.deferredLethal=true; d=Mathf.Max(0,e.currentHp-1); }
            return true;
        }
        public override bool CanExecute(EnemyController e)=>b.Posture==3 && !b.Invulnerable;
        public override float ExecutionDamage(EnemyController e) {
            b.Posture=0; float d=e.maxHp*.12f;
            if(d>=e.currentHp && b.ThresholdIndex<3) { b.deferredLethal=true; d=Mathf.Max(0,e.currentHp-1); }
            return d;
        }
        public override void Parried(EnemyController e) { if(b.Yellow)b.AddPosture(); }
        public override bool ControlFromSource(EnemyController e,EnemyControlKind kind,ref float strength,ref float duration,UnityEngine.Object source) {
            if(kind==EnemyControlKind.Knockback && source) b.AddPosture(source);
            return Control(e,kind,ref strength,ref duration);
        }
        public override bool Control(EnemyController e,EnemyControlKind kind,ref float strength,ref float duration) {
            switch(kind) {
                case EnemyControlKind.Knockback:return false;
                case EnemyControlKind.Stagger:case EnemyControlKind.Pacify:case EnemyControlKind.Defect:case EnemyControlKind.Taunt:return false;
                case EnemyControlKind.Paralysis:duration*=.5f; return !b.Invulnerable;
                case EnemyControlKind.Slow:strength=Mathf.Min(30,strength); return true;
                default:return true;
            }
        }
    }
}
