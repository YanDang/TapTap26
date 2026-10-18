using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class BossRegression
{
    public static IEnumerator Run(Action<bool,string> check)
    {
        var encounter=BossEncounterController.Instance;
        var b=encounter.StartEncounter();
        check(b!=null,"P2 production boss spawn"); if(!b)yield break;
        b.AutoAttack=false;
        yield return null;
        var e=b.Entity; float hp=e.currentHp;
        check(e.enabled && EnemyController.AllEnemies.Contains(e) && e.GetComponentsInChildren<EnemyController>().Length==1,"P2 single registered HP entity");
        check(encounter.StartEncounter()==b,"P2 encounter idempotent");
        check(b.Animator && b.Animator.layers.Length>10,"P2 original layered rig bound");
        e.TakeDamage(10,100,Vector3.zero);
        check(Mathf.Approximately(e.currentHp,hp-10) && !e.IsStaggered,"P2 ordinary damage no guard break");
        b.AddPosture(); b.AddPosture(); b.AddPosture();
        hp=e.currentHp; e.BeginExecuted(); e.KillByExecution(Vector3.right);
        check(Mathf.Approximately(e.currentHp,hp-e.maxHp*.12f) && b.Posture==0 && e.aiState!=EnemyController.EnemyAIState.Executing,"P2 execution twelve percent recovers");
        b.ForceMove(GameJam.Boss.BossMoves.P1_Claw3);
        EnemyController.isAIPaused=true; float t=b.Elapsed;
        yield return null; yield return null;
        check(b.Elapsed==t,"P2 build pause freezes attack"); EnemyController.isAIPaused=false;
        PlayerSessionData.isCraftingOpen=true; yield return null; yield return null;
        check(b.Elapsed==t,"P2 overlay pause freezes attack"); PlayerSessionData.isCraftingOpen=false; GameJam.TimeManager.I.Refresh();
        var door=UnityEngine.Object.FindObjectOfType<GatherDoorAuraBehavior>();
        var state=door.state; door.OnPlayerInteract(PlayerController.Instance);
        check(door.state==state,"P2 door interaction denied during encounter");
        e.ApplySlow(90,1); e.ApplyParalysis(2);
        check(Mathf.Approximately(e.slowMultiplier,.7f) && e.IsParalyzed,"P2 slow capped and paralysis accepted");
        Vector3 pos=e.transform.position; e.ApplyKnockback(Vector3.right,5);
        check(e.transform.position==pos,"P2 knockback immune");
        yield return new WaitForEndOfFrame(); var shot=ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(TapSelfTest.OutputDirectory,"p2-boss.png"),shot.EncodeToPNG()); UnityEngine.Object.Destroy(shot);
        var order=new List<int>(); b.ThresholdStarted+=i=>order.Add(i);
        e.TakeDamage(e.maxHp*10,0,Vector3.zero);
        check(e.IsAlive && e.currentHp==1,"P2 lethal damage deferred before mandatory thresholds");
        int deaths=0; encounter.Defeated+=dead=>deaths++;
        // Exercise actual brain/Enemy damage/death methods; manual stepping is explicitly not wall-clock play evidence.
        for(int i=0;i<500 && e.IsAlive;i++)b.Tick(.1f);
        check(order.Count==3 && order[0]==0 && order[1]==1 && order[2]==2 && b.Phase==2,"P2 lethal crossing 70 then 50 then 30 exactly once");
        check(!e.IsAlive && deaths==1 && !BossEncounterController.EncounterActive,"P2 deferred death exactly once unlocks encounter");
        encounter.CancelEncounter(); yield return null;
    }
}
