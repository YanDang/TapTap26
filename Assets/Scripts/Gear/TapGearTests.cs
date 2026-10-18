using System;
using System.Collections;
using System.IO;
using UnityEngine;
using GameJam.Gear;

/// <summary>Production scene combat probes; not another crafting matrix. Save scope belongs to TapSelfTest.</summary>
public static class TapGearTests
{
    static void Equip(string id,int level=3){GearInventory.ResetAll();GearInventory.Put(new GearItem{id=id,level=level,p=30,e=30,g=30,w=30});GearInventory.Equip(id);}
    static IEnumerator Shot(string name){yield return new WaitForEndOfFrame();var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(TapSelfTest.OutputDirectory,name+".png"),t.EncodeToPNG());UnityEngine.Object.Destroy(t);}
    static IEnumerator Ready(PlayerController p, EnemyController e) { if(e)e.transform.position=p.transform.position+Vector3.right*100; p.StopMovement();p.ClearAttackTarget();p.ExitCombat(); float until=Time.realtimeSinceStartup+4; while(!p.CanBeginUltimate && Time.realtimeSinceStartup<until)yield return null; p.ClearAttackTarget();p.ExitCombat(); }
    static void Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(o,args);
    static IEnumerator Extended(Action<bool,string> check,PlayerController p,TapGearCombat g)
    {
        var home=p.transform.position;
        // Exercise real furniture grid owner and prohibited permanent facilities.
        yield return Ready(p,null);Equip("arm_grapple");g.Tick(8);
        var f=new GameObject("P5 movable furniture").AddComponent<FurnitureObject>();
        f.transform.position=home+Vector3.right*5*g.Unit;f.SnapToNearestGrid();yield return null;yield return Ready(p,null);g.Tick(8);var old=f.occupiedCell;var before=f.transform.position;
        check(g.TryGrapple(f.transform.position),"P5 furniture grapple accepted [lv="+TapGearCombat.Lv("arm_grapple")+" ready="+p.CanBeginUltimate+" paused="+TapGearCombat.Paused+" cooldown="+g.GrappleRemaining+" distance="+Vector2.Distance(p.transform.position,f.transform.position)+" unit="+g.Unit+" targetable="+f.Targetable+" registered="+FurnitureObject.AllFurniture.Contains(f)+"]");g.Tick(.71f);
        check(f.transform.position!=before && f.occupiedCell!=old && f.currentState==FurnitureObject.FurnitureState.Placed,"P5 grapple relocates furniture AND grid ownership");
        f.isImmovable=true;g.Tick(8);check(!g.TryGrapple(f.transform.position),"P5 grapple refuses immovable furniture");UnityEngine.Object.Destroy(f.gameObject);yield return null;
        for(int lv=1;lv<=3;lv++) {
            yield return Ready(p,null);Equip("core_antenna",lv);float charge=p.Ultimate.Charge;p.Ultimate.AddCharge(1);
            check(Mathf.Abs(p.Ultimate.Charge-charge-(1.2f+(lv-1)*.15f))<.02f,"P5 antenna actual energy Lv"+lv);
            Equip("core_nerve",lv);check(Mathf.Abs(p.CurrentAttackInterval-p.attackInterval/(1.15f+(lv-1)*.1f))<.001f,"P5 nerve attack timer Lv"+lv);
            Equip("core_siphon",lv);check(Mathf.Abs(g.MoveMultiplier-(1.5f+(lv-1)*.075f))<.001f,"P5 siphon movement multiplier Lv"+lv);
            Equip("core_fungi",lv);p.currentHp=p.maxHp*.4f;float initialHp=p.currentHp;g.Tick(5.9f);check(p.currentHp==initialHp,"P5 fungi no premature heal Lv"+lv);g.Tick(.11f);g.Tick(lv==1?4:3);check(p.currentHp>p.maxHp*.4f,"P5 fungi timed HP Lv"+lv);p.currentHp=p.maxHp;
        }
        yield return Ready(p,null);Equip("leg_jet");p.GearRestoreDodge();p.currentStamina=p.maxStamina;before=p.transform.position;
        check(p.TriggerDodgeRoll(Vector2.right),"P5 jet starts original dodge");check(g.DodgeCharges==2,"P5 jet spends one of three charges");
        float recharge=g.RechargeRemaining;PlayerSessionData.isCraftingOpen=true;g.Tick(5);check(g.RechargeRemaining==recharge && g.DodgeCharges==2,"P5 jet recharge frozen by pause");PlayerSessionData.isCraftingOpen=false;GameJam.TimeManager.I.Refresh();
        float until=Time.realtimeSinceStartup+4;while(!p.CanBeginUltimate && Time.realtimeSinceStartup<until)yield return null;
        check(Vector2.Distance(before,p.transform.position)>.1f*g.Unit,"P5 jet original dodge produces displacement");g.Tick(2.5f);check(g.DodgeCharges==3,"P5 jet timed charge recovery");p.transform.position=home;
        for(int lv=1;lv<=3;lv++) {
            yield return Ready(p,null);Equip("leg_decoy",lv);p.GearRestoreDodge();p.currentStamina=p.maxStamina;
            var e=EnemyController.CreateEnemyInstance(home+Vector3.right*100,"P5 decoy enemy");e.autoRespawn=false;e.maxHp=e.currentHp=10000;yield return null;
            e.transform.position=home+Vector3.right*2*g.Unit;float hp=e.currentHp;int explosions=g.Explosions;
            check(p.TriggerDodgeRoll(Vector2.left),"P5 decoy real dodge accepted Lv"+lv);p.TakeDamage(10,e,false);g.Tick(.01f);
            check(p.LastDodgeWasPerfect,"P5 decoy triggered by actual avoided damage Lv"+lv);
            check(lv==1?!e.GearTaunt:e.GearTaunt!=null,"P5 decoy taunt level gate Lv"+lv);
            var enemyBefore=e.transform.position;yield return null;yield return null;
            if(lv>=2)check(e.aiState==EnemyController.EnemyAIState.Chase && (e.transform.position!=enemyBefore || Vector2.Distance(e.transform.position,e.GearTaunt.position)<=e.attackRange),"P5 decoy actual AI pursuit Lv"+lv);
            GameJam.TimeManager.I.EndSlowMo();g.Tick(lv==1?.51f:1.01f);check(e.currentHp<hp && g.Explosions==explosions+1,"P5 decoy timed HP explosion Lv"+lv);
            if(lv==3){g.Tick(.12f);check(g.Explosions==explosions+2,"P5 second decoy staggered detonation");}
            g.Cancel();UnityEngine.Object.Destroy(e.gameObject);yield return null;yield return Ready(p,null);p.transform.position=home;
        }
        // Real HP recipients establish spring chain counts and eel cadence at every level.
        var enemies=new System.Collections.Generic.List<EnemyController>();
        for(int i=0;i<3;i++){var e=EnemyController.CreateEnemyInstance(home+Vector3.right*100,"P5 chain "+i);e.autoRespawn=false;e.ExternalAI=(entity,dt)=>{};e.maxHp=e.currentHp=10000;enemies.Add(e);}yield return null;
        for(int lv=1;lv<=3;lv++){
            foreach(var e in enemies)e.transform.position=home+Vector3.right*100;yield return Ready(p,null);Equip("arm_spring",lv);
            for(int i=0;i<3;i++){enemies[i].transform.position=home+Vector3.right*(i+1)*g.Unit;enemies[i].currentHp=10000;}
            g.CounterHit(new System.Collections.Generic.List<EnemyController>{enemies[0]});int damaged=0;for(int i=1;i<3;i++)if(enemies[i].currentHp<10000)damaged++;
            check(damaged==lv-1,"P5 spring actual extra HP recipients Lv"+lv);
            foreach(var e in enemies)e.transform.position=home+Vector3.right*100;yield return Ready(p,null);Equip("arm_eel",lv);
            for(int i=0;i<3;i++)enemies[i].transform.position=home+Vector3.right*(i+1)*g.Unit;
            float hp=enemies[0].currentHp;int count=lv==1?4:3;for(int i=0;i<count-1;i++)g.NormalHit(enemies[0]);check(enemies[0].currentHp==hp,"P5 eel cadence no early damage Lv"+lv);
            float secondary=enemies[1].currentHp;g.NormalHit(enemies[0]);check(enemies[0].currentHp<hp && (lv<3 || enemies[1].currentHp<secondary),"P5 eel cadence actual damage and level3 jump Lv"+lv);
        }
        foreach(var e in enemies)UnityEngine.Object.Destroy(e.gameObject);yield return null;
        // Each boss probe uses the production entity/policy; equipment is changed only out of encounter.
        foreach(string id in new[]{"arm_grapple","arm_hydro","arm_eel","shell_mobile","core_logic"}) {
            yield return Ready(p,null);Equip(id);g.Tick(8);var b=BossEncounterController.Instance.StartEncounter();b.AutoAttack=false;b.Entity.ExternalAI=(entity,dt)=>{};yield return null;
            var e=b.Entity;e.transform.position=p.transform.position+Vector3.right*3*g.Unit;before=e.transform.position;
            if(id=="arm_grapple") {var start=p.transform.position;check(g.TryGrapple(e.transform.position),"P5 boss grapple accepted");g.Tick(.15f);check(e.transform.position==before && p.transform.position!=start,"P5 boss grapple moves player not boss");}
            if(id=="arm_hydro") {float hp=e.currentHp;g.WaterSmash(Vector3.right,20);check(e.currentHp<hp && e.IsWet && e.transform.position==before && b.Posture==1,"P5 boss hydro HP wet no displacement plus posture");}
            if(id=="arm_eel") {e.Wet(4);float hp=e.currentHp;g.NormalHit(e);check(e.currentHp<hp && e.IsParalyzed,"P5 boss eel actual HP paralysis");yield return new WaitForSeconds(.4f);check(!e.IsParalyzed,"P5 boss paralysis duration halved");}
            if(id=="shell_mobile") {e.transform.position=p.transform.position+Vector3.right*g.Unit;b.ForceMove(GameJam.Boss.BossMoves.P1_LeapSlam);check(g.SetGuard(true),"P5 perfect boss shell accepted");float hpBefore=p.currentHp;p.TakeDamage(10,e,false);check(Mathf.Abs(p.currentHp-(hpBefore-5))<.01f && b.Posture==1 && e.transform.position==p.transform.position+Vector3.right*g.Unit,"P5 perfect shell red HP half and exactly one Boss posture no push");g.SetGuard(false);yield return Ready(p,e);p.SetVisualFacing(false);check(g.SetGuard(true),"P5 boss shell accepted");b.ForceMove(GameJam.Boss.BossMoves.P2_EyeBeam);var shape=b.Shape(0);p.transform.position=(Vector3)(shape.origin+shape.direction*g.Unit);g.SetGuard(false);check(g.SetGuard(true),"P5 beam guard faces source");float hp=e.currentHp,playerHp=p.currentHp;Call(b,"ResolveImpact",0);check(e.currentHp<hp && p.currentHp==playerHp,"P5 actual Boss beam reflected HP and player protected");g.SetGuard(false);}
            if(id=="core_logic") {b.ForceMove(GameJam.Boss.BossMoves.P1_Claw3);b.Tick(.12f);float elapsed=b.Elapsed;g.Tick(.01f);check(g.WarningActive && b.GearImpactRemaining>.25f && b.Elapsed==elapsed,"P5 logic warning visible 0.2s earlier without advancing impact clock");yield return Shot("p5-logic-warning");}
            var decoy=new GameObject("P5 boss taunt probe");check(!e.SetGearTaunt(decoy.transform) && !e.GearTaunt,"P5 boss taunt immunity "+id);UnityEngine.Object.Destroy(decoy);
            g.Cancel();BossEncounterController.Instance.CancelEncounter();yield return null;p.transform.position=home;p.currentHp=p.maxHp;
        }
    }
    public static IEnumerator Run(Action<bool,string> check)
    {
        var p=PlayerController.Instance;var g=p.Gear;var ui=TapGearUI.Instance;
        check(g && ui,"P5 real player combat adapter and UI installed");
        if(!g || !ui)yield break;
        check(g.Unit>.1f && Mathf.Abs(g.Unit-TapCombatScale.HeroBounds(p).size.y/2f)<.1f,"P5 scale initialized from actual hero bounds not zero Start cache");
        p.StopMovement();p.ClearAttackTarget();p.ExitCombat();p.currentHp=p.maxHp;
        GameJam.TimeManager.I.EndSlowMo();yield return new WaitForSecondsRealtime(.3f);
        GearInventory.ResetAll();
        check(ui.Toggle(),"P5 noncombat bench opens");check(PlayerSessionData.IsCombatInputBlocked && Time.timeScale==0,"P5 bench pauses production world");
        int icons=0;foreach(var d in GearDefs.All)if(Resources.Load<Sprite>("Gear/icon_"+d.id))icons++;
        check(icons==15,"P5 all 15 source icons imported");yield return Shot("p5-bench");ui.Close();check(!TapGearUI.IsOpen,"P5 bench closes");
        Equip("core_siphon");float baseSpeed=p.baseMoveSpeed*p.moveSpeedMultiplier;
        check(p.EffectiveMoveSpeed>baseSpeed*1.5f,"P5 siphon real movement speed outside combat");
        Equip("core_nerve");check(p.CurrentAttackInterval<p.attackInterval*.8f,"P5 nerve real attack interval");
        Equip("core_antenna");float charge=p.Ultimate.Charge;p.Ultimate.AddCharge(10);check(Mathf.Abs(p.Ultimate.Charge-charge-15)<.01f,"P5 antenna actual charge multiplier");
        Equip("core_fungi");p.currentHp=p.maxHp*.5f;g.Tick(6);g.Tick(4);check(p.currentHp>p.maxHp*.5f,"P5 fungi heals real player HP");p.currentHp=p.maxHp;
        Equip("arm_grapple");var e=EnemyController.CreateEnemyInstance(p.transform.position+Vector3.right*100,"P5 grapple enemy");e.autoRespawn=false;e.ExternalAI=(entity,dt)=>{};e.maxHp=e.currentHp=10000;yield return null;
        e.transform.position=p.transform.position+Vector3.right*3*g.Unit;Vector3 before=e.transform.position;check(g.TryGrapple(e.transform.position),"P5 grapple accepted real enemy");
        PlayerSessionData.isCraftingOpen=true;g.Tick(.5f);check(e.transform.position==before,"P5 paused grapple cannot move target");PlayerSessionData.isCraftingOpen=false;GameJam.TimeManager.I.Refresh();
        g.Tick(.2f);check(Vector2.Distance(p.transform.position,e.transform.position)<Vector2.Distance(p.transform.position,before),"P5 grapple actually pulls enemy XY");
        g.Cancel();Vector3 cancelled=e.transform.position;g.Tick(1);check(e.transform.position==cancelled && !g.Busy,"P5 cancelled grapple cannot settle stale pull");
        check(!ui.Toggle(),"P5 nearby enemy blocks bench");string snap=GearInventory.Snapshot();GearInventory.Equip("core_siphon");check(GearInventory.Snapshot()==snap,"P5 equip service rejects combat proximity");
        yield return Ready(p,e);
        Equip("arm_hydro");e.transform.position=p.transform.position+Vector3.right*2*g.Unit;before=e.transform.position;float hp=e.currentHp;
        check(g.WaterSmash(Vector3.right,20),"P5 hydro replaces smash");check(e.currentHp<hp && e.IsWet && e.transform.position.x>before.x,"P5 hydro actual damage wet and shared displacement");yield return Shot("p5-water");
        yield return Ready(p,e);Equip("arm_eel");e.transform.position=p.transform.position+Vector3.right*g.Unit;e.Wet(4);hp=e.currentHp;g.NormalHit(e);check(e.currentHp<hp && e.IsParalyzed,"P5 eel wet hit damages and paralyzes actual enemy");
        yield return Ready(p,e);Equip("shell_static");e.transform.position=p.transform.position+Vector3.right*g.Unit;hp=e.currentHp;g.Block();g.Block();g.Block();check(g.Stacks==3,"P5 static holds three stacks");g.Block();check(g.Stacks==0 && e.currentHp<hp,"P5 fourth block discharges real damage");
        yield return Ready(p,e);Equip("shell_mobile");p.currentHp=p.maxHp;p.currentTarget=null;p.ExitCombat();
        // Move enemy after guard starts so it cannot trigger auto attack before acceptance.
        p.SetVisualFacing(false);check(g.SetGuard(true),"P5 held shell guard accepted");e.transform.position=p.transform.position+Vector3.right*g.Unit;hp=p.currentHp;int durability=g.Durability;
        p.TakeDamage(10,e,true);check(p.currentHp==hp && g.Durability==durability-1,"P5 shell front yellow damage fully blocked");yield return Shot("p5-shell");g.SetGuard(false);
        yield return Ready(p,e);Equip("shell_float");p.currentHp=10;g.Tick(.01f);p.TakeDamage(1000,e,false);check(p.IsAlive && g.Bubble && g.FloatUses==1,"P5 lethal bubble saves real player");
        g.Cancel();p.currentHp=p.maxHp;UnityEngine.Object.Destroy(e.gameObject);yield return null;p.ClearAttackTarget();p.ExitCombat();
        Equip("leg_root");g.Tick(.01f);g.Tick(1);check(g.Rooted,"P5 stationary real player roots");p.transform.position+=Vector3.right*g.Unit;g.Tick(.05f);check(!g.Rooted,"P5 movement unroots");
        yield return Extended(check,p,g);
        GearInventory.ResetAll();g.Cancel();p.currentHp=p.maxHp;
        check(!g.Guarding && !g.Rooted && !g.Busy,"P5 cleanup releases all owned action flags");
        yield return null;
    }
}
