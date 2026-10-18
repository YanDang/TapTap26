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
        PrefsRoundTrip();
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
