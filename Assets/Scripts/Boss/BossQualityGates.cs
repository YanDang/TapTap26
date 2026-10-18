using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using GameJam.Boss;

/// <summary>Built-player behavioral probes. Manual combat ticks are labelled separately
/// from real-frame revival and framebuffer checks; no production scene is saved.</summary>
public static class BossQualityGates
{
    static void Call(object o,string name,params object[] args) => o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);
    static FurnitureObject Cover(Vector2 at) {
        var go=new GameObject("P2_cover_probe");go.transform.position=at;
        var f=go.AddComponent<FurnitureObject>();f.enabled=false;f.instanceId="furn_coral_wall";f.maxDurability=f.currentDurability=100;
        return f;
    }
    static void Remove(FurnitureObject f) { f.gameObject.SetActive(false);UnityEngine.Object.Destroy(f.gameObject); }
    public static IEnumerator Run(Action<bool,string> check)
    {
        // Baseline deliberately has no TapCombatScale. Measure visible world geometry
        // before spawning/animating the boss, rather than dereferencing optional setup
        // or deriving the expected height from the boss's own configured UnitScale.
        var scale=UnityEngine.Object.FindObjectOfType<TapCombatScale>();
        float heroHeight=scale ? scale.VisualHeight : TapCombatScale.HeroBounds(PlayerController.Instance).size.y;
        var encounter=BossEncounterController.Instance;var b=encounter.StartEncounter();
        b.AutoAttack=false;yield return null;
        var e=b.Entity;var p=PlayerController.Instance;var saved=p.transform.position;var bossSaved=b.transform.position;
        e.ExternalAI=(enemy,dt)=>{};
        var ground=b.GetComponent<BossGroundEffects>();ground.enabled=false;
        p.ClearAttackTarget();p.StopMovement();p.currentHp=p.maxHp;
        float u=b.UnitScale;Vector2 origin=b.transform.position;
        // All move geometries are read by the production display as well as impact resolution.
        foreach(string move in new[]{BossMoves.P1_Claw3,BossMoves.P1_Lunge,BossMoves.P1_LeapSlam,BossMoves.P1_CloakSweep,BossMoves.P1_Wrap,BossMoves.P2_SpinThrust,BossMoves.P2_Claw5,BossMoves.P2_EyeBeam}) {
            p.transform.position=origin+Vector2.right*u;b.ForceMove(move);
            bool valid=true;
            for(int i=0;i<BossBrain.ImpactsFor(move).Length;i++) {
                var s=b.Shape(i);var outline=s.Outline();
                valid&=outline.Length>=5 && outline[0]==outline[outline.Length-1];
                valid&=s.Contains(s.origin+s.direction*(s.kind==BossAttackShape.Kind.Line?s.length*.5f:s.radius*.5f));
                valid&=!s.Contains(s.origin+s.direction*(s.kind==BossAttackShape.Kind.Line?s.length+u:s.radius+u));
            }
            check(valid,"P2 shared geometry interior exterior closed outline "+move);
        }
        check(BossBrain.IsBlockable(BossMoves.P2_SpinThrust,0) && !BossBrain.IsBlockable(BossMoves.P2_SpinThrust,1) && !BossBrain.IsBlockable(BossMoves.P2_Claw5,4),"P2 guide yellow red impact semantics");
        p.transform.position=origin+Vector2.right*u;b.ForceMove(BossMoves.P2_EyeBeam);
        Vector2 ray=b.Shape(0).direction;
        var cover=Cover(origin+ray*2*u);var farther=Cover(origin+ray*4*u);
        p.transform.position=origin+ray*3*u;float hp=p.currentHp;
        check(Mathf.Abs(b.Shape(0).length-2*u)<.001f,"P2 beam telegraph clipped at nearest cover");
        Call(b,"ResolveImpact",0);
        check(p.currentHp==hp && cover.currentDurability==94 && farther.currentDurability==100,"P2 beam stops at nearest furniture no downstream damage");
        cover.transform.position=origin-ray*u;farther.transform.position=origin+ray*4*u;
        Call(b,"ResolveImpact",0);
        check(Mathf.Approximately(p.currentHp,hp-p.maxHp*.2f) && cover.currentDurability==94,"P2 cover behind source or behind player does not protect");
        Remove(cover);Remove(farther);
        p.currentHp=p.maxHp;
        // Cleave is an overhead line, not a projectile: any struck cover within source 2m protects.
        p.transform.position=origin+Vector2.right*u;b.ForceMove(BossMoves.P1_Claw3);
        cover=Cover((Vector2)p.transform.position+Vector2.right*1.99f*u);hp=p.currentHp;
        Call(b,"Cleave");
        check(p.currentHp==hp && cover.currentDurability<=0,"P2 cleave cover inside 2m shattered and protects");Remove(cover);ground.Clear();
        cover=Cover((Vector2)p.transform.position+Vector2.right*2.01f*u);
        Call(b,"Cleave");
        check(Mathf.Approximately(p.currentHp,hp-p.maxHp*.6f),"P2 cleave beyond 2m does not protect");Remove(cover);ground.Clear();
        var door=UnityEngine.Object.FindObjectOfType<GatherDoorAuraBehavior>().GetComponent<FurnitureObject>();
        float durability=door.currentDurability;door.TakeDamage(100000,e);
        check(door.currentDurability==durability && !BossBrain.IsCover(door),"P2 gathering door durability and cover exclusion");
        p.currentHp=p.maxHp;
        var area=new BossAttackShape{kind=BossAttackShape.Kind.Circle,origin=p.transform.position,direction=Vector2.right,radius=u};
        ground.Add(area,BossElement.Water);ground.Add(area,BossElement.Water);ground.Add(area,BossElement.Electric);ground.Add(area,BossElement.Electric);
        hp=p.currentHp;ground.Tick(.01f);
        check(Mathf.Approximately(p.currentHp,hp-p.maxHp*.4f) && Mathf.Approximately(BossGroundEffects.PlayerMoveMultiplier(p),.55f),"P2 water electric overlap doubles once and slow does not stack");
        hp=p.currentHp;ground.Tick(.4f);check(p.currentHp==hp,"P2 overlapping electric patches share one second cadence");
        EnemyController.isAIPaused=true;ground.Tick(10);check(ground.Count==4 && p.currentHp==hp,"P2 hazards lifetime and damage frozen in build pause");EnemyController.isAIPaused=false;
        ground.Tick(6);check(ground.Count==0 && BossGroundEffects.PlayerMoveMultiplier(p)==1,"P2 hazards expire and movement restored");
        // Path footprint sampling may not clear globally-owned obstacle cells.
        var map=IsometricPathfinder.Instance;var cell=map.WorldToCell(b.transform.position);
        bool existed=IsometricPathfinder.DynamicBlockedCells.Contains(cell);
        IsometricPathfinder.DynamicBlockedCells.Add(cell);Vector3 before=b.transform.position;
        bool moved=b.MoveSafely((Vector2)before+Vector2.right*.05f);
        check(!moved && b.transform.position==before && IsometricPathfinder.DynamicBlockedCells.Contains(cell),"P2 blocked footprint does not move or clear foreign occupancy");
        if(!existed)IsometricPathfinder.DynamicBlockedCells.Remove(cell);
        before=b.transform.position;check(!b.MoveSafely(new Vector2(10000,10000)) && Vector2.Distance(before,b.transform.position)<100,"P2 swept movement rejects map exterior");b.transform.position=bossSaved;
        // Full-sized idle/locomotion frames, deliberately framed without changing production scale.
        Call(b,"Cancel");b.Animator.SetTelegraph(0,Color.white);p.transform.position=saved;
        p.currentHp=p.maxHp;
        for(int i=0;i<120;i++)b.Animator.Tick(.025f);
        yield return new WaitForSeconds(3f); // settle real hurt animation, floating numbers and shatter effects
        var cc=CameraController.Instance;bool cameraEnabled=cc.enabled;cc.enabled=false;var cam=Camera.main;Vector3 cameraPos=cam.transform.position;
        cam.transform.position=new Vector3((saved.x+bossSaved.x)*.5f,(saved.y+bossSaved.y)*.5f+1, cameraPos.z);
        yield return Frame("p2-full-idle.png");
        float height=b.Animator.Height*b.Animator.transform.lossyScale.y;
        check(Mathf.Abs(height/heroHeight-2.5f)<.001f,"P2 authored rig height world scale 2.5 hero (not silhouette measurement)");
        var foot=b.Animator.GetComponentsInChildren<Transform>();Vector3[] pose=new Vector3[foot.Length];for(int i=0;i<foot.Length;i++)pose[i]=foot[i].localPosition;
        b.Animator.SetLocomotion(new Vector3(1,0,0));for(int i=0;i<16;i++)b.Animator.Tick(.025f);
        bool changed=false;for(int i=0;i<foot.Length;i++)changed|=(pose[i]-foot[i].localPosition).sqrMagnitude>.000001f;
        check(changed,"P2 original gait changes rig bone poses");yield return Frame("p2-full-walk.png");b.Animator.SetLocomotion(Vector3.zero);
        int steps=0,lastSide=-1;bool alternating=true;Action<int> step=side=>{alternating&=side!=lastSide;lastSide=side;steps++;};b.Animator.OnFootfall+=step;
        b.Animator.SetLocomotion(new Vector3(2.2f,0,0));
        for(int frame=0;frame<16;frame++) { for(int tick=0;tick<5;tick++)b.Animator.Tick(.05f);yield return Frame("p2-gait-"+frame+".png"); }
        b.Animator.OnFootfall-=step;b.Animator.SetLocomotion(Vector3.zero);check(steps>=2 && alternating,"P2 original gait emits alternating-cycle footfalls");
        cc.enabled=cameraEnabled;cam.transform.position=cameraPos;
        // Real coroutine revival: encounter identity, HP, phase and progress are never reset.
        p.currentHp=p.maxHp;e.TakeDamage(10,0,p.transform.position);hp=e.currentHp;int phase=b.Phase,index=b.ThresholdIndex;
        p.TakeDamage(p.maxHp*2,e,false);
        check(!p.IsAlive,"P2 lethal damage enters existing defeat");
        float deadline=Time.realtimeSinceStartup+15;while(!p.IsAlive && Time.realtimeSinceStartup<deadline)yield return null;
        check(p.IsAlive && encounter.Boss==b && e.currentHp==hp && b.Phase==phase && b.ThresholdIndex==index,"P2 real revival preserves boss identity HP phase thresholds");
        deadline=Time.realtimeSinceStartup+5;while(p.IsInvulnerable && Time.realtimeSinceStartup<deadline)yield return null;
        p.transform.position=b.transform.position+Vector3.right*u;b.ForceMove(BossMoves.P1_Wrap);Call(b,"ResolveImpact",0);
        check(p.IsGrabbed && EnemyController.Grabber==e,"P2 wrap owns unique grab");
        b.enabled=false;check(!p.IsGrabbed && EnemyController.Grabber==null && ground.Count==0,"P2 disable cleans owned grab and hazards");
        yield return new WaitForSeconds(1.5f);b.enabled=true;
        b.AddPosture();b.AddPosture();b.AddPosture();check(e.CanExecute,"P2 posture opens execution eligibility");
        b.Tick(.7f);b.Tick(3.1f);check(!e.CanExecute && e.aiState!=EnemyController.EnemyAIState.Executing,"P2 expired execution window rejects stale request");
        p.transform.position=saved;encounter.CancelEncounter();yield return null;
    }
    static IEnumerator Frame(string name) { yield return new WaitForEndOfFrame();var shot=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(TapSelfTest.OutputDirectory,name),shot.EncodeToPNG());UnityEngine.Object.Destroy(shot); }
}
