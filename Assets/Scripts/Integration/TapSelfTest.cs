using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in Windows GPU player smoke tests; normal startup never touches saves.</summary>
public sealed class TapSelfTest : MonoBehaviour
{
    public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), "-tapselftest") >= 0;
    public static string OutputDirectory {
        get { var a=Environment.GetCommandLineArgs(); int i=Array.IndexOf(a,"-tapoutput");
            return i>=0 && i+1<a.Length ? Path.GetFullPath(a[i+1]) : (Application.isEditor ? Path.GetFullPath("Builds/Windows") : Path.GetDirectoryName(Application.dataPath)); }
    }
    TapPrefsSnapshot original; readonly List<string> lines=new List<string>(); int passed,failed; bool ending; float started;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if(!Requested) return;
        Application.runInBackground=true;
        var go=new GameObject("TapSelfTest"); DontDestroyOnLoad(go); var runner=go.AddComponent<TapSelfTest>();
        Directory.CreateDirectory(OutputDirectory);
        try {
            if(Application.platform!=RuntimePlatform.WindowsPlayer && Application.platform!=RuntimePlatform.WindowsEditor) throw new PlatformNotSupportedException("Raw PlayerPrefs protection currently supports Windows only");
            runner.original=TapPrefsSnapshot.Capture();
            runner.original.WriteJournal(Path.Combine(OutputDirectory,"prefs-before.json"));
            // Deterministic fresh run; the entire original TAP_* namespace is restored after scene teardown.
            PlayerPrefs.DeleteKey("TAP_SHELL_BACKPACK_DATA"); PlayerPrefs.DeleteKey("TAP_CRAFTED_PRODUCTS_DATA_V2"); PlayerPrefs.Save();
        } catch(Exception e) { runner.Check(false,"snapshot: "+e); runner.ending=true; Application.Quit(2); }
    }
    void Check(bool ok,string name) { if(ok) passed++; else failed++; var s=(ok?"PASS ":"FAIL ")+name; lines.Add(s); Debug.Log("[TapSelfTest] "+s); Flush(); }
    void Flush() { File.WriteAllLines(Path.Combine(OutputDirectory,"selftest.txt"),lines); }
    IEnumerator Start()
    {
        if(ending) yield break;
        started=Time.realtimeSinceStartup;
        // Flatten nested enumerators so assertion/setup exceptions always enter cleanup.
        var stack=new Stack<IEnumerator>(); stack.Push(Tests());
        while(stack.Count>0) {
            object current=null; bool moved=false;
            try { moved=stack.Peek().MoveNext(); if(moved) current=stack.Peek().Current; }
            catch(Exception e) { Check(false,"exception "+e); break; }
            if(!moved) { stack.Pop(); continue; }
            if(current is IEnumerator nested) stack.Push(nested); else yield return current;
        }
        yield return Cleanup();
    }
    IEnumerator Tests()
    {
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"-tapboss")>=0) {
            float startupDeadline=Time.realtimeSinceStartup+15;
            while(!BossEncounterController.EncounterActive && Time.realtimeSinceStartup<startupDeadline)yield return null;
            Check(BossEncounterController.EncounterActive,"P2 actual -tapboss command-line startup");
            if(BossEncounterController.Instance)BossEncounterController.Instance.CancelEncounter();
        }
        yield return SceneManager.LoadSceneAsync("BiomechanicalCombatScene",LoadSceneMode.Single);
        yield return new WaitForSecondsRealtime(2f);
        var p=PlayerController.Instance; var cam=Camera.main; var control=CameraController.Instance;
        Check(SceneManager.GetActiveScene().name=="BiomechanicalCombatScene","formal combat scene loaded");
        Check(p!=null && p.hero!=null && p.IsAlive,"live player and Hero2D");
        Check(FindObjectsOfType<BiomechanicalCombatAdapter>().Length==1,"one adapter");
        Check(FindObjectOfType<GatherDoorAuraBehavior>()!=null,"default gathering door preserved");
        Check(EnemyController.AllEnemies.Count==0,"adapter initial enemy cleanup");
        Check(cam!=null && cam.orthographic && control!=null,"orthographic camera and existing controller");
        Check(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null,"real GPU graphics device "+SystemInfo.graphicsDeviceName);
        var b=TapCombatScale.HeroBounds(p);
        bool baseline=Array.IndexOf(Environment.GetCommandLineArgs(),"-tapbaseline")>=0;
        var scale=FindObjectOfType<TapCombatScale>();
        float h=baseline ? b.size.y : scale.VisualHeight;
        lines.Add($"MEASURE baseline={baseline} screen={Screen.width}x{Screen.height} height={h:R} animatedBounds={b.size.y:R} speed={p.baseMoveSpeed:R} roll={p.rollSpeed:R} ortho={cam.orthographicSize:R} minZoom={control.minZoom:R} maxZoom={control.maxZoom:R} heightsPerSec={p.baseMoveSpeed/h:R} screenFraction={h/(2*cam.orthographicSize):R}");
        if(!baseline) {
            Check(Mathf.Abs(p.baseMoveSpeed/h-TapCombatScale.SourceHeightsPerSecond)<0.001f,"measured source visual heights per second");
            Check(Mathf.Abs(h/(2*cam.orthographicSize)-TapCombatScale.SourceScreenFraction)<0.001f,"reference screen fraction");
            Check(Mathf.Abs(control.maxZoom/control.minZoom-5f)<0.001f,"zoom range ratio retained");
            Check(Mathf.Abs(p.rollSpeed/p.baseMoveSpeed-8.5f/4f)<0.001f,"roll/run ratio retained");
        }
        yield return new WaitForEndOfFrame();
        var shot=ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(OutputDirectory,baseline?"target-before.png":"target-after.png"),shot.EncodeToPNG()); Destroy(shot);
        Check(File.Exists(Path.Combine(OutputDirectory,baseline?"target-before.png":"target-after.png")),"game framebuffer screenshot written");
        yield return null;
        Vector3 start=p.transform.position; float elapsed=0;
        while(elapsed<0.25f) { float dt=GameJam.TimeManager.SlowMo ? GameJam.TimeManager.PlayerDelta : Time.deltaTime; p.MoveWithInput(Vector2.right); elapsed+=dt; yield return null; }
        p.StopMovement();
        float distance=Vector3.Distance(start,p.transform.position);
        lines.Add($"MOVEMENT actualDistance={distance:R} elapsed={elapsed:R} expected={p.baseMoveSpeed*elapsed:R}");
        Check(distance>0.05f && Mathf.Abs(distance-p.baseMoveSpeed*elapsed)<0.01f,"actual unobstructed movement equals speed times engine delta");
        Check(p.TriggerDodgeRoll(Vector2.left),"existing dodge entry accepted");
        yield return null; Check(p.IsInvulnerable,"dodge grants invulnerability");
        // Existing roll chains ShellPunch (0.42 hit + retract), not merely rollDuration.
        float dodgeDeadline=Time.realtimeSinceStartup+5f;
        while(p.IsInvulnerable && Time.realtimeSinceStartup<dodgeDeadline) yield return null;
        Check(!p.IsInvulnerable,"dodge plus existing counter-punch ends without stuck invulnerability");
        yield return CombatTests(p);
        yield return BossQualityGates.Run(Check);
        yield return BossRegression.Run(Check);
        yield return BossPresentationTests.Run(Check);
        yield return TapUltTests.Run(Check);
        // Destroy the persistent owner to exercise the serialized standalone entry,
        // not a direct StartEncounter substitute. It must load the formal arena itself.
        Destroy(BossEncounterController.Instance.gameObject);yield return null;
        yield return SceneManager.LoadSceneAsync("BossTest",LoadSceneMode.Single);
        float entryDeadline=Time.realtimeSinceStartup+15;
        while(!BossEncounterController.EncounterActive && Time.realtimeSinceStartup<entryDeadline)yield return null;
        Check(BossEncounterController.EncounterActive && SceneManager.GetActiveScene().name=="BiomechanicalCombatScene","P2 standalone BossTest enters formal arena and spawns boss");
        if(BossEncounterController.Instance)BossEncounterController.Instance.CancelEncounter();
        PrefsRoundTrip();
    }
    IEnumerator CombatTests(PlayerController p)
    {
        var e=EnemyController.CreateEnemyInstance(p.transform.position+Vector3.right*2,"P1_Regression");
        yield return null;
        e.maxHp=e.currentHp=1000; e.currentGuardBreak=1000;
        var field=typeof(EnemyController).GetField("CombatPolicy");
        Check(field!=null,"P1 optional combat policy available");
        if(field==null) { Destroy(e.gameObject); yield break; }
        e.TakeDamage(10,20,p.transform.position);
        Check(e.currentHp==990 && e.currentGuardBreak==980,"P1 ordinary damage and guard depletion");
        e.Parried(); e.TakeDamage(10,0,p.transform.position);
        Check(e.aiState==EnemyController.EnemyAIState.Pending && e.currentHp==975,"P1 ordinary parry pending and 1.5 vulnerability");
        e.ApplySlow(40,2); e.ApplyParalysis(1); e.Wet(2);
        Check(Mathf.Approximately(e.slowMultiplier,0.6f) && e.IsParalyzed && e.IsWet,"P1 ordinary controls retained");
        int deaths=0; e.Died+=enemy=>{ deaths++; enemy.KillByExecution(Vector3.right); };
        e.BeginExecuted(); e.KillByExecution(Vector3.right); e.KillByExecution(Vector3.right);
        Check(!e.IsAlive && deaths==1,"P1 ordinary execution lethal and death reentry exactly once");
        Destroy(e.gameObject);

        e=EnemyController.CreateEnemyInstance(p.transform.position+Vector3.right*1.5f,"P1_Policy");
        yield return null;
        e.maxHp=e.currentHp=1000; e.currentGuardBreak=1000;
        var policy=new RegressionPolicy(); e.CombatPolicy=policy;
        int ticks=0; e.ExternalAI=(enemy,dt)=>ticks++;
        Vector3 origin=e.transform.position;
        yield return null; yield return null;
        Check(ticks>0 && e.transform.position==origin && EnemyController.AllEnemies.Contains(e),"P1 external AI sole owner stays registered");
        EnemyController.isAIPaused=true; int previous=ticks;
        yield return null; yield return null;
        Check(ticks==previous,"P1 external AI build pause"); EnemyController.isAIPaused=false;
        PlayerSessionData.isCraftingOpen=true; previous=ticks;
        yield return null; yield return null;
        Check(ticks==previous,"P1 external AI overlay pause"); PlayerSessionData.isCraftingOpen=false;
        GameJam.TimeManager.I.Refresh();
        GameJam.TimeManager.I.DoHitstop(0.1f); previous=ticks;
        yield return null;
        Check(ticks==previous,"P1 external AI hitstop");
        yield return new WaitForSecondsRealtime(0.15f);
        e.ExternalParryWindow=enemy=>true;
        Check(e.ParryWindowOpen,"P1 external parry window explicit owner");
        e.ExternalParryWindow=null;
        Check(!e.ParryWindowOpen,"P1 no stale ordinary parry timer for external AI");
        e.ApplyParalysis(0.4f); previous=ticks;
        yield return null;
        Check(ticks==previous && e.IsParalyzed,"P1 external AI obeys paralysis");
        yield return new WaitForSeconds(0.3f);
        Check(!e.IsParalyzed,"P1 paralysis duration policy halved");
        e.aiState=EnemyController.EnemyAIState.Attack;
        e.TakeDamage(10,200,p.transform.position);
        Check(e.currentHp==990 && e.currentGuardBreak==1000 && e.aiState==EnemyController.EnemyAIState.Attack,"P1 armored hit preserves state and filters guard damage");
        policy.rejectDamage=true; e.TakeDamage(100,0,p.transform.position); policy.rejectDamage=false;
        Check(e.currentHp==990,"P1 damage veto preserves single HP");
        e.Knockback(Vector3.right,2); e.ApplyKnockback(Vector3.right,2); e.Displace(Vector3.right,2);
        yield return null;
        Check(e.transform.position==origin,"P1 all three knockback paths resisted");
        e.ApplySlow(90,2); e.ApplyPacify(2); e.Pacify(2,null); e.Wet(2);
        Check(Mathf.Approximately(e.slowMultiplier,0.7f) && !e.isPacified && !e.Pacified && !e.CanBePacified && e.IsWet,"P1 slow clamp pacify immunity wet retained");
        e.Parried();
        p.ExecuteEnemy(e); e.BeginExecuted(); e.KillByExecution(Vector3.right);
        Check(e.currentHp==990 && e.aiState!=EnemyController.EnemyAIState.Executing && !GetPrivate<bool>(p,"isAttackingAnim"),"P1 rejected execution no player lock or executing state");
        policy.execute=true; e.BeginExecuted(); policy.execute=false; e.KillByExecution(Vector3.right);
        Check(e.currentHp==990 && e.aiState!=EnemyController.EnemyAIState.Executing,"P1 settlement rechecks eligibility");
        policy.execute=true; e.BeginExecuted(); e.KillByExecution(Vector3.right);
        Check(e.currentHp==890 && e.IsAlive && e.aiState!=EnemyController.EnemyAIState.Executing,"P1 nonlethal execution settles same HP and recovers");

        float executionHp=e.currentHp;
        p.ExecuteEnemy(e);
        p.enabled=false; p.enabled=true;
        yield return new WaitForSeconds(0.6f);
        Check(e.currentHp==executionHp && !GetPrivate<bool>(p,"isAttackingAnim"),"P1 disabling player cancels in-flight execution settlement");

        float hp=p.currentHp;
        SetPrivate(p,"isGuarding",true);
        var result=p.TakeDamage(5,e,false);
        Check(result==PlayerDamageResult.Damaged && p.currentHp==hp-5,"P1 unblockable damage ignores guard");
        SetPrivate(p,"isGuarding",true); int parries=policy.parries;
        result=p.TakeDamage(5,e,true);
        Check(result==PlayerDamageResult.Blocked && policy.parries==parries+1,"P1 block routes to actual attacker");
        SetPrivate(p,"isGuarding",true); parries=policy.parries;
        result=p.TakeDamage(5,null,true);
        Check(result==PlayerDamageResult.Blocked && policy.parries==parries,"P1 sourceless block never punishes nearby enemy");
        GameJam.TimeManager.I.EndSlowMo();
        int hits=0; float total=0;
        System.Action<EnemyController,float> hit=(enemy,amount)=>{hits++; total+=amount;}; p.DamageDealt+=hit;
        p.DealDamage(e,10,0); policy.rejectDamage=true; p.DealDamage(e,10,0); policy.rejectDamage=false;
        Check(hits==1 && total==10,"P1 actual player damage event excludes rejected hits");
        p.ClearAttackTarget();
        e.transform.position=p.transform.position+Vector3.right*0.4f;
        Vector3 smashOrigin=e.transform.position; float beforeSmash=e.currentHp; int beforeHits=hits;
        typeof(PlayerController).GetMethod("DoSmash",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(p,new object[]{0f,Vector3.right,e});
        Check(e.currentHp<beforeSmash && hits==beforeHits+1 && e.transform.position==smashOrigin,"P1 real smash damage event and displacement resistance");
        yield return new WaitForSecondsRealtime(0.2f);
        p.currentTarget=e; beforeHits=hits;
        yield return (IEnumerator)typeof(PlayerController).GetMethod("PerformAttackRoutine",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(p,null);
        Check(hits==beforeHits+1,"P1 real normal attack emits damage event");
        p.DamageDealt-=hit;

        int begins=0,ends=0,perfect=0;
        System.Action begin=()=>begins++, end=()=>ends++, perf=()=>perfect++;
        p.DodgeStarted+=begin; p.DodgeEnded+=end; p.PerfectDodge+=perf;
        SetPrivate(p,"rollCooldownTimer",0f); SetPrivate(p,"isGuarding",false);
        Check(p.TriggerDodgeRoll(Vector2.left),"P1 event dodge accepted");
        p.TakeDamage(1,e,false); p.TakeDamage(1,e,false);
        Check(begins==1 && perfect==1 && p.LastDodgeWasPerfect,"P1 perfect dodge once per real roll");
        float deadline=Time.realtimeSinceStartup+5;
        while(p.IsInvulnerable && Time.realtimeSinceStartup<deadline) yield return null;
        Check(ends==1,"P1 dodge end exactly once");
        p.NotifyDodgeAvoided(); Check(perfect==1,"P1 no perfect event outside roll");
        p.DodgeStarted-=begin; p.DodgeEnded-=end; p.PerfectDodge-=perf;
        yield return new WaitForEndOfFrame();
        var shot=ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(OutputDirectory,"p1-combat.png"),shot.EncodeToPNG()); Destroy(shot);
        Destroy(e.gameObject);
    }
    static T GetPrivate<T>(object obj,string field) => (T)obj.GetType().GetField(field,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(obj);
    static void SetPrivate(object obj,string field,object value) => obj.GetType().GetField(field,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(obj,value);
    sealed class RegressionPolicy : EnemyCombatPolicy
    {
        public bool rejectDamage,execute; public int parries;
        public override bool InterruptOnHit => false;
        public override bool VulnerabilityBonus => false;
        public override bool ReceiveDamage(EnemyController e,ref float damage,ref float guard) { guard=0; return !rejectDamage; }
        public override bool CanExecute(EnemyController e) => execute;
        public override float ExecutionDamage(EnemyController e) => 100;
        public override void Parried(EnemyController e) { parries++; }
        public override bool Control(EnemyController e,EnemyControlKind kind,ref float strength,ref float duration)
        {
            if(kind==EnemyControlKind.Paralysis) duration*=0.5f;
            if(kind==EnemyControlKind.Slow) strength=Mathf.Min(30,strength);
            return kind==EnemyControlKind.Slow || kind==EnemyControlKind.Paralysis || kind==EnemyControlKind.Wet;
        }
    }
    void PrefsRoundTrip()
    {
        string prefix="TAP_P0_"+Guid.NewGuid().ToString("N");
        string s=prefix+"_S", i=prefix+"_I", f=prefix+"_F", empty=prefix+"_EMPTY", absent=prefix+"_ABSENT";
        Check(!PlayerPrefs.HasKey(absent),"probe originally absent");
        PlayerPrefs.SetString(s,"  原文\r\n\"quoted\"\\雪  "); PlayerPrefs.SetString(empty,""); PlayerPrefs.SetInt(i,-2147483647); PlayerPrefs.SetFloat(f,-0.125f); PlayerPrefs.Save();
        var before=TapPrefsSnapshot.Capture();
        Check(before.Data.values.Exists(v=>v.name.StartsWith(s)),"raw snapshot enumerates unknown TAP key");
        PlayerPrefs.SetInt(s,42); PlayerPrefs.SetString(i,"type changed"); PlayerPrefs.DeleteKey(f); PlayerPrefs.DeleteKey(empty); PlayerPrefs.SetString(absent,"new"); PlayerPrefs.Save();
        before.Restore();
        Check(before.Matches(TapPrefsSnapshot.Capture()),"all native types and raw bytes restored, new keys absent");
        foreach(var key in new[]{s,i,f,empty,absent}) PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }
    IEnumerator Cleanup()
    {
        ending=true;
        // Unload all writers, including GameRunController.OnApplicationQuit, BEFORE restoration.
        var empty=SceneManager.CreateScene("TapSelfTestCleanup"); SceneManager.SetActiveScene(empty);
        var scenes=new List<Scene>(); for(int i=0;i<SceneManager.sceneCount;i++) { var s=SceneManager.GetSceneAt(i); if(s!=empty) scenes.Add(s); }
        foreach(var s in scenes) yield return SceneManager.UnloadSceneAsync(s);
        yield return null;
        try { original.Restore(); Check(original.Matches(TapPrefsSnapshot.Capture()),"original TAP namespace exact restoration"); }
        catch(Exception e) { Check(false,"RESTORE FAILED: "+e); }
        lines.Add($"RESULT passed={passed} failed={failed}"); Flush();
        Debug.Log($"[TapSelfTest] RESULT passed={passed} failed={failed}");
        Application.Quit(failed==0?0:1);
    }
    void Update() { if(!ending && started>0 && Time.realtimeSinceStartup-started>90) { Check(false,"90s watchdog"); StopAllCoroutines(); StartCoroutine(Cleanup()); } }
}
