using System;
using System.Collections;
using System.IO;
using UnityEngine;
using GameJam.Boss;
public static class BossPresentationTests {
 static IEnumerator Frame(string n){yield return new WaitForEndOfFrame();var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(TapSelfTest.OutputDirectory,n+".png"),t.EncodeToPNG());UnityEngine.Object.Destroy(t);}
 public static IEnumerator Run(Action<bool,string> check){
 var clips=Resources.LoadAll<AudioClip>("Audio");int count=0;bool pcm=true;var log=new System.Text.StringBuilder();foreach(var c in clips){if(!c.name.StartsWith("boss_") && !new System.Collections.Generic.List<string>{"workshop","ult_rise","ult_impact","victory"}.Contains(c.name))continue;count++;c.LoadAudioData();var samples=new float[Mathf.Min(c.samples*c.channels,24000)];bool ok=c.GetData(samples,0);double power=0;foreach(float x in samples)power+=x*x;pcm &= ok && c.samples>0 && power>0;log.AppendLine(c.name+" samples="+c.samples+" channels="+c.channels+" hz="+c.frequency+" read="+ok+" power="+power);}
 File.WriteAllText(Path.Combine(TapSelfTest.OutputDirectory,"p3-audio-readback.txt"),log.ToString());check(count>=32 && pcm,"P3 all boss SFX and six BGM decoded nonzero PCM");check(BossAudio.Clip("boss_sigh") && BossAudio.Clip("boss_step_3"),"P3 sigh and all steps imported");
 var enc=BossEncounterController.Instance;enc.CancelEncounter();yield return null;
 float ready=Time.realtimeSinceStartup+15;while(!PlayerController.Instance.IsAlive && Time.realtimeSinceStartup<ready)yield return null;
 PlayerController.Instance.currentHp=PlayerController.Instance.maxHp;yield return new WaitForSeconds(3);
 var b=enc.StartEncounter();b.AutoAttack=false;b.Entity.ExternalAI=(e,dt)=>{};var d=b.GetComponent<BossUltDirector>();var fx=b.GetComponent<BossMoveFx>();var audio=b.GetComponent<BossAudio>();var cc=CameraController.Instance;var camera=Camera.main;float zoom=camera.orthographicSize;bool follow=cc.followPlayer;
 yield return null;b.Entity.TakeDamage(b.Entity.maxHp*.31f,0,PlayerController.Instance.transform.position);b.Tick(.01f);
 for(int i=0;i<8;i++){b.Tick(.1f);yield return null;}yield return Frame("p3-sigh-rift");check(d.Active && d.RiftCount>0 && d.Letterbox>0,"P3 sigh closeup rift and letterbox");
 var bars=b.GetComponentInChildren<Canvas>();bool safeBars=bars && bars.sortingOrder<0;foreach(var image in b.GetComponentsInChildren<UnityEngine.UI.Image>())safeBars &= !image.raycastTarget;check(safeBars,"P3 letterbox behind HUD and does not intercept input");
 float elapsed=b.Elapsed,clock=fx.Clock;float vanish=b.Animator.Vanish;Vector3 pausedCamera=camera.transform.position;EnemyController.isAIPaused=true;yield return null;int played=audio.Played;yield return new WaitForSecondsRealtime(.3f);b.Tick(1);check(b.Elapsed==elapsed && fx.Clock==clock && b.Animator.Vanish==vanish && audio.Paused && audio.Played==played && camera.transform.position==pausedCamera,"P3 build pause freezes choreography FX and audio");yield return Frame("p3-paused");EnemyController.isAIPaused=false;yield return null;check(!audio.Paused,"P3 resumes owned audio");
 PlayerSessionData.isCraftingOpen=true;yield return null;elapsed=b.Elapsed;yield return new WaitForSecondsRealtime(.1f);b.Tick(1);check(b.Elapsed==elapsed && audio.Paused,"P3 workshop overlay preserves timeline and pauses audio");PlayerSessionData.isCraftingOpen=false;yield return null;
 for(int i=0;i<16;i++){b.Tick(.1f);yield return null;}
 // Boss Tick is accelerated by this fixture; rift opening uses the real FX clock.
 BossRift sky=null;foreach(var r in b.GetComponentsInChildren<BossRift>())if(r.withEye)sky=r;
 float eyeDeadline=Time.realtimeSinceStartup+2;while(sky && sky.open<.95f && Time.realtimeSinceStartup<eyeDeadline)yield return null;
 check(sky && sky.open>=.95f,"P3 sky eye reaches fully open on real FX clock");
 yield return Frame("p3-sky-eye");check(d.Stage=="SkyEye" && b.Animator.Vanish>.99f,"P3 vanished boss and tracking sky eye");
 foreach(var r in b.GetComponentsInChildren<BossRift>())foreach(var mr in r.GetComponentsInChildren<MeshRenderer>(true))Debug.Log($"[P3Eye] eye={r.withEye} open={r.open} {mr.name} active={mr.gameObject.activeInHierarchy} enabled={mr.enabled} order={mr.sortingOrder} bounds={mr.bounds} local={mr.transform.localPosition} scale={mr.transform.localScale} material={mr.sharedMaterial.shader.name}");
 var iris=sky?sky.transform.Find("Iris"):null;var pupil=iris?iris.Find("Pupil"):null;check(iris && pupil && pupil.gameObject.activeInHierarchy && pupil.GetComponent<Renderer>().bounds.size.y>.02f*b.UnitScale,"P3 sky eye has visible sized iris and black pupil");
 b.Tick(.65f);yield return null;yield return new WaitForSeconds(.1f);yield return Frame("p3-sky-eye-locked");
 for(int i=0;i<30 && fx.Strikes==0;i++){b.Tick(.05f);yield return null;}yield return Frame("p3-giant-claw-emerge");check(fx.Strikes>0 && d.Stage=="Emerge","P3 authoritative cleave event giant claw and emergence");
 b.enabled=false;yield return null;yield return new WaitForSeconds(.2f);check(!d.Active && b.Animator.Vanish==0 && d.Letterbox==0 && cc.followPlayer==follow && Mathf.Abs(camera.orthographicSize-zoom)<.02f,"P3 disable cancellation restores camera vanish black bars");yield return Frame("p3-cancel-restored");enc.CancelEncounter();yield return null;
 b=enc.StartEncounter();b.AutoAttack=false;b.Entity.ExternalAI=(e,dt)=>{};fx=b.GetComponent<BossMoveFx>();
 // Natural completion, transformation and second-threshold X cuts on the production timeline.
 b.Entity.TakeDamage(b.Entity.maxHp*.31f,0,PlayerController.Instance.transform.position);b.Tick(.01f);
 for(int i=0;i<140 && b.ThresholdIndex==0;i++){PlayerController.Instance.currentHp=PlayerController.Instance.maxHp;b.Tick(.05f);yield return null;}
 check(b.ThresholdIndex==1 && !b.GetComponent<BossUltDirector>().Active && b.Animator.Vanish==0,"P3 natural ultimate completion restores presentation");
 b.Entity.TakeDamage(b.Entity.maxHp*.21f,0,PlayerController.Instance.transform.position);b.Tick(.01f);yield return null;yield return Frame("p3-transform");check(fx.LiveCount>0,"P3 transformation veins and cloak fragments");
 for(int i=0;i<55 && b.ThresholdIndex==1;i++){b.Tick(.05f);yield return null;}
 b.Entity.TakeDamage(b.Entity.maxHp*.21f,0,PlayerController.Instance.transform.position);b.Tick(.01f);int cuts=0;Action<string,int> onHit=(m,i)=>{if(m==BossMoves.Ult_Cleave)cuts++;};b.Impact+=onHit;
 for(int i=0;i<110 && cuts<2;i++){PlayerController.Instance.currentHp=PlayerController.Instance.maxHp;b.Tick(.05f);yield return null;}
 yield return Frame("p3-double-cleave");check(cuts==2 && b.GetComponent<BossUltDirector>().Stage=="Emerge","P3 thirty-percent X emits both cuts before emergence");b.Impact-=onHit;
 b.enabled=false;yield return null;check(!MusicDirector.Instance.WarningPlaying,"P3 cancellation stops ultimate music warning");enc.CancelEncounter();yield return null;
 b=enc.StartEncounter();b.AutoAttack=false;b.Entity.ExternalAI=(e,dt)=>{};fx=b.GetComponent<BossMoveFx>();
 foreach(string m in new[]{BossMoves.P1_Claw3,BossMoves.P1_Lunge,BossMoves.P1_LeapSlam,BossMoves.P1_CloakSweep,BossMoves.P1_Wrap,BossMoves.P2_SpinThrust,BossMoves.P2_Claw5,BossMoves.P2_EyeBeam}){
 // Reproduce PNG encoding / a slow frame BEFORE the FX is born. Its lifetime
 // must not include this pre-birth work on the next Update.
 if(m==BossMoves.P2_SpinThrust)System.Threading.Thread.Sleep(300);
 PlayerController.Instance.currentHp=PlayerController.Instance.maxHp;bool started=b.ForceMove(m);int strikesBefore=fx.Strikes;
 Debug.Log($"[P3Clock] {m} started={started} paused={EnemyController.CombatWorldPaused} hitstop={GameJam.TimeManager.Hitstop} elapsed={b.Elapsed} strikes={fx.Strikes}");
 b.Tick(BossBrain.ImpactsFor(m)[0]+.01f);
 check(started && fx.Strikes==strikesBefore+1,"P3 authoritative first impact exactly once "+m);
 Debug.Log($"[P3Clock] after {m} paused={EnemyController.CombatWorldPaused} elapsed={b.Elapsed} strikes={fx.Strikes} live={fx.LiveCount}");
 yield return null;
 Debug.Log($"[P3Clock] yielded {m} dt={Time.deltaTime} elapsed={b.Elapsed} strikes={fx.Strikes} live={fx.LiveCount} ages={fx.TraceDiagnostic}");
 check(fx.LiveCount>0,"P3 resolved XY FX "+m);yield return Frame("p3-"+m);
 }
 yield return new WaitForSeconds(.5f);check(fx.LiveCount==0,"P3 short lived FX still expire after birth timing fix");
 check(BossFxAssets.Shader && BossFxAssets.Shader.isSupported,"P3 actual URP shader supported");enc.CancelEncounter();yield return null;
 }
}
