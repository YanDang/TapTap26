using System;
using System.Collections;
using System.IO;
using UnityEngine;
using GameJam;
using GameJam.HeroUlt;

public static class TapUltTests
{
    sealed class Reject : EnemyCombatPolicy { public override bool ReceiveDamage(EnemyController e,ref float d,ref float g)=>false; }
    static IEnumerator Frame(string name){yield return new WaitForEndOfFrame();var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(TapSelfTest.OutputDirectory,name+".png"),t.EncodeToPNG());UnityEngine.Object.Destroy(t);}
    static IEnumerator Wait(Func<bool> done,float timeout=8){float end=Time.realtimeSinceStartup+timeout;while(!done() && Time.realtimeSinceStartup<end)yield return null;}
    static EnemyController Enemy(PlayerController p,Vector2 offset)
    {
        var e=EnemyController.CreateEnemyInstance(p.transform.position+(Vector3)offset,"P4 test enemy");e.maxHp=e.currentHp=10000;e.ExternalAI=(x,dt)=>{};e.autoRespawn=false;return e;
    }
    static void Idle(PlayerController p,TapHeroUlt u){u.Cancel(true);p.StopMovement();p.ClearAttackTarget();p.ExitCombat();TimeManager.I.EndSlowMo();}
    public static IEnumerator Run(Action<bool,string> check)
    {
        var p=PlayerController.Instance;var u=p.Ultimate;check(u && u.UI,"P4 runtime manager and clickable HUD installed");if(!u)yield break;
        BossEncounterController.Instance.CancelEncounter();
        yield return Wait(()=>p.IsAlive,15);yield return new WaitForSeconds(2);p.currentHp=p.maxHp;
        foreach(var enemy in new System.Collections.Generic.List<EnemyController>(EnemyController.AllEnemies))if(enemy)UnityEngine.Object.Destroy(enemy.gameObject);
        yield return null;Idle(p,u);yield return null;
        check(u.Select(UltKind.ArmSmash),"P4 out-of-combat selection accepted and persisted");
        u.UI.SwitchButton.onClick.Invoke();check(u.UI.PickerOpen,"P4 switch button opens three-card selection");yield return Frame("p4-picker");u.UI.SwitchButton.onClick.Invoke();
        u.AddCharge(100);yield return new WaitForSecondsRealtime(.15f);check(u.UI.Arcs.Active && u.UI.Arcs.ArcCount>0,"P4 full charge animated arc border");yield return Frame("p4-ready");
        check(!u.TryRelease() && u.Charge==100,"P4 arm without target does not spend charge");
        u.Cancel(true);yield return Wait(()=>p.ParryRemaining<=0 && p.CanBeginUltimate);
        p.TriggerShellGuard();float guardHp=p.currentHp;
        p.TakeDamage(1,null,true);
        check(p.currentHp==guardHp && Mathf.Abs(u.Charge-5)<.001f,"P4 successful source-less block awards five charge");
        u.Cancel(true);
        yield return Wait(()=>p.ParryRemaining<=0 && p.CanBeginUltimate);
        var blocker=Enemy(p,Vector2.right*20*u.Unit);
        yield return Wait(()=>!blocker.IsEmerging);
        p.TriggerShellGuard();guardHp=p.currentHp;
        p.TakeDamage(1,blocker,true);
        check(p.currentHp==guardHp && Mathf.Abs(u.Charge-5)<.001f,"P4 sourced block awards five charge exactly once");
        UnityEngine.Object.Destroy(blocker.gameObject);yield return null;
        Idle(p,u);yield return Wait(()=>p.CanBeginUltimate);
        var e=Enemy(p,Vector2.right*.35f);yield return null;u.Cancel(true);
        p.DealDamage(e,u.DamageUnit,0);check(Mathf.Abs(u.Charge-4)<.001f,"P4 actual player damage normalized to four charge");
        e.CombatPolicy=new Reject();float charge=u.Charge;p.DealDamage(e,100,0);p.DealDamage(e,-1,0);p.DealDamage(e,float.NaN,0);check(u.Charge==charge,"P4 invulnerable invalid negative and NaN damage give no charge");e.CombatPolicy=null;
        p.SetAttackTarget(e);check(!u.Select(UltKind.EchoShell),"P4 combat selection rejected");u.AddCharge(100);
        // The host automatically attacks nearby enemies on their first Update. Do not clear
        // its action flags: prove rejection, then wait for the actual attack coroutine.
        check(!p.CanBeginUltimate && !u.TryRelease() && u.Charge==100,"P4 ordinary attack rejects ultimate without spending charge");
        yield return Wait(()=>p.CanBeginUltimate);check(p.CanBeginUltimate,"P4 ordinary attack naturally releases eligibility");
        u.UI.ReleaseButton.onClick.Invoke();check(u.Active && p.UltimateLocked,"P4 same mobile click starts arm and acquires lock");
        Vector3 position=p.transform.position;p.MoveWithInput(Vector2.left);p.MoveToWorldPoint(position+Vector3.left);p.StartCharge();p.PerformManualAttack();p.ExecuteEnemy(e);
        check(p.transform.position==position && !p.TriggerDodgeRoll() && !p.IsCharging && !p.BeginPlace(position,null,null),"P4 movement dodge charge execute placement input locked");
        EnemyController.isAIPaused=true;float elapsed=u.Elapsed;float hp=e.currentHp;yield return new WaitForSecondsRealtime(.2f);check(u.Elapsed==elapsed && e.currentHp==hp,"P4 build pause freezes ultimate and damage");EnemyController.isAIPaused=false;
        PlayerSessionData.isCraftingOpen=true;yield return null;elapsed=u.Elapsed;yield return new WaitForSecondsRealtime(.2f);check(u.Elapsed==elapsed && !u.TryRelease(),"P4 workshop blocks release and freezes current phase");PlayerSessionData.isCraftingOpen=false;TimeManager.I.Refresh();
        yield return Wait(()=>u.Hits>0 || !u.Active);yield return Frame("p4-arm-impact");check(u.Hits>0 && e.currentHp<hp,"P4 arm actual timed cone damage");yield return Wait(()=>!u.Active);check(!p.UltimateLocked && !p.IsInvulnerable,"P4 arm restores lock and dash invulnerability");
        UnityEngine.Object.Destroy(e.gameObject);yield return null;Idle(p,u);yield return null;
        check(u.Select(UltKind.ShellCannon),"P4 choose cannon");e=Enemy(p,Vector2.right*.6f);yield return null;yield return Wait(()=>p.CanBeginUltimate);p.SetAttackTarget(e);u.AddCharge(100);check(u.TryRelease(),"P4 cannon release accepted");hp=p.currentHp;p.TakeDamage(1,e,false);check(p.currentHp==hp-1 && u.Active,"P4 cannon superarmor takes damage without cancelling");
        yield return new WaitForSecondsRealtime(.75f);yield return Frame("p4-cannon-charge");
        int explosions=u.Explosions;yield return Wait(()=>u.Explosions>explosions);check(u.Explosions>explosions,"P4 swept cannon detonates");yield return Frame("p4-cannon-explosion");
        var burn=e.GetComponent<TapUltBurn>();check(burn,"P4 actual explosion applies burn");
        if(burn){var same=TapUltBurn.Apply(u,e);check(same==burn && e.GetComponents<TapUltBurn>().Length==1,"P4 repeated burn refreshes without stacking");EnemyController.isAIPaused=true;hp=e.currentHp;yield return new WaitForSecondsRealtime(.3f);check(e.currentHp==hp,"P4 burn damage frozen during build pause");EnemyController.isAIPaused=false;yield return Wait(()=>!burn,6);check(e.currentHp<=hp-4*u.DamageUnit+.01f,"P4 burn deals four actual world-second ticks");}
        yield return Wait(()=>!u.Active);UnityEngine.Object.Destroy(e.gameObject);yield return null;Idle(p,u);yield return null;
        check(u.Select(UltKind.EchoShell),"P4 choose echo");e=Enemy(p,Vector2.right*.2f);yield return null;yield return Wait(()=>p.CanBeginUltimate);u.AddCharge(100);check(u.TryRelease(),"P4 echo starts");hp=p.currentHp;
        p.TakeDamage(1,e,true);p.TakeDamage(2,e,false);p.TakeDamage(3);check(p.currentHp==hp && u.Stacks==3,"P4 echo absorbs blockable unblockable and legacy element damage");
        check(!p.TryGrab(e) && u.Stacks==4 && e.IsParalyzed,"P4 echo rejects grab and stuns grabber");
        for(int i=0;i<8;i++)p.TakeDamage(1,e,false);check(u.Stacks==8 && p.currentHp==hp,"P4 echo eight-stack cap");yield return Frame("p4-echo-eight");
        // Reset target position after grab deflection, then release through player left-click route.
        e.transform.position=p.transform.position+Vector3.right*.2f;hp=e.currentHp;p.LeftClick();check(!u.Active && !p.UltimateLocked && e.currentHp<hp,"P4 early echo click causes wave damage and unlocks");yield return Frame("p4-echo-wave");
        yield return Wait(()=>p.CanBeginUltimate);u.AddCharge(100);check(u.TryRelease(),"P4 echo repeat release");yield return new WaitForSecondsRealtime(2.7f);check(u.Active && p.hero.InShell,"P4 echo remains visibly shelled beyond normal roll watchdog");yield return Wait(()=>!u.Active);check(!p.UltimateLocked,"P4 echo automatic five-second release");
        yield return Wait(()=>p.CanBeginUltimate);u.AddCharge(100);u.TryRelease();p.enabled=false;yield return null;check(!u.Active && !p.UltimateLocked,"P4 player disable cancels without stale action");p.enabled=true;
        yield return Wait(()=>p.CanBeginUltimate);u.AddCharge(100);u.TryRelease();u.Cancel(true);check(!u.Active && u.Charge==0 && !p.UltimateLocked,"P4 reset clears energy lock owned projectile and burn");
        UnityEngine.Object.Destroy(e.gameObject);yield return null;Idle(p,u);
        check(PlayerPrefs.GetInt(TapHeroUlt.PrefKey,-1)==(int)UltKind.EchoShell,"P4 choice uses isolated preference key");
        yield return Wait(()=>p.CanBeginUltimate);
        u.Select(UltKind.ShellCannon);
        e=Enemy(p,Vector2.right*5*u.Unit);yield return null;
        yield return Wait(()=>p.CanBeginUltimate);p.ClearAttackTarget();p.SetVisualFacing(true);
        u.AddCharge(100);check(u.TryRelease(),"P4 unlocked cannon release accepted");
        position=p.transform.position;u.Tick(1.01f);yield return null;
        var shot=UnityEngine.Object.FindObjectOfType<TapUltProjectile>();
        check(shot && shot.transform.position.x<position.x,"P4 cannon without lock fires facing not nearest enemy");
        u.Cancel(true);UnityEngine.Object.Destroy(e.gameObject);yield return null;Idle(p,u);
        // Use the real Boss entity/policy, but hold its scheduler to isolate player damage.
        yield return Wait(()=>p.CanBeginUltimate && !TapHeroUlt.Paused);
        u.Select(UltKind.ArmSmash);
        var boss=BossEncounterController.Instance.StartEncounter();
        boss.Entity.ExternalAI=(x,dt)=>{};
        boss.transform.position=p.transform.position+Vector3.right*1.2f*u.Unit;
        p.SetAttackTarget(boss.Entity);u.AddCharge(100);hp=boss.Entity.currentHp;
        Vector3 bossPosition=boss.transform.position;
        check(u.TryRelease(),"P4 arm accepts real boss target");
        u.Tick(.41f);check(u.DashInvulnerable,"P4 arm dash only is invulnerable");
        u.Tick(.01f);u.Tick(.5f);
        check(Mathf.Abs(hp-boss.Entity.currentHp-8*u.DamageUnit)<.001f && boss.Posture==2 && boss.transform.position==bossPosition,"P4 arm boss eight damage two posture no displacement");
        u.Cancel(true);BossEncounterController.Instance.CancelEncounter();yield return null;Idle(p,u);
        yield return Wait(()=>p.CanBeginUltimate && !TapHeroUlt.Paused);u.Select(UltKind.EchoShell);
        boss=BossEncounterController.Instance.StartEncounter();boss.Entity.ExternalAI=(x,dt)=>{};
        boss.transform.position=p.transform.position+Vector3.right*u.Unit;
        u.AddCharge(100);check(u.TryRelease(),"P4 echo accepts boss encounter");
        hp=p.currentHp;p.TakeDamage(p.maxHp*.6f,boss.Entity,false);
        check(p.currentHp==hp && u.Stacks==1,"P4 echo absorbs real boss sourced red damage");
        for(int i=1;i<8;i++)p.TakeDamage(1,boss.Entity,false);
        hp=boss.Entity.currentHp;u.ReleaseEcho();
        check(Mathf.Abs(hp-boss.Entity.currentHp-10*u.DamageUnit)<.001f && boss.Posture==3,"P4 eight echoes deal ten damage and cap boss posture at three");
        u.Cancel(true);BossEncounterController.Instance.CancelEncounter();yield return null;Idle(p,u);
        yield return Wait(()=>p.CanBeginUltimate && !TapHeroUlt.Paused);u.Select(UltKind.ShellCannon);
        boss=BossEncounterController.Instance.StartEncounter();boss.Entity.ExternalAI=(x,dt)=>{};
        hp=boss.Entity.currentHp;u.Explode(boss.transform.position);
        check(Mathf.Abs(hp-boss.Entity.currentHp-6*u.DamageUnit)<.001f && boss.GetComponent<TapUltBurn>(),"P4 cannon explosion damages and burns actual boss");
        u.Cancel(true);BossEncounterController.Instance.CancelEncounter();yield return null;Idle(p,u);u.Select(UltKind.EchoShell);
    }
}
