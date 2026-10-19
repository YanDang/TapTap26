using UnityEngine;
using GameJam.Boss;
/// <summary>Presentation-only translation of source timeline to P2's 3.6/5.2s impacts.</summary>
[DefaultExecutionOrder(-50)]
public sealed class BossUltDirector : MonoBehaviour {
 BossBrain b;BossRift ground,sky;BossAudio audio;CameraController cc;
 bool active,opened,closed,sighed,locked,exhaust;float exhaustAt;
 bool savedFollow;Vector3 savedPosition,savedOffset;float savedZoom;
 Canvas bars;UnityEngine.UI.Image topBar,bottomBar;
 public bool Active=>active;public float Letterbox {get;private set;}
 public int RiftCount {get;private set;} public string Stage {get;private set;}="None";
 void OnEnable(){b=GetComponent<BossBrain>();audio=GetComponent<BossAudio>();b.MoveStarted+=Begin;b.Impact+=Hit;b.PresentationCancelled+=Cancel;}
 void Begin(string move){if(move!=BossMoves.Ult_Rise)return;Cancel();active=true;opened=closed=sighed=locked=exhaust=false;cc=CameraController.Instance;if(cc){savedFollow=cc.followPlayer;savedPosition=cc.transform.position;savedOffset=cc.followOffset;savedZoom=cc.GetComponent<Camera>().orthographicSize;cc.SetFollowPlayer(false);}Stage="Sigh";}
 BossRift Rift(Vector3 p,bool horizontal,bool eye){float u=b.UnitScale;var r=BossRift.Spawn(p,4.3f,1.4f,horizontal,eye,eye?74:-8);r.transform.SetParent(transform,true);r.transform.localScale=Vector3.one*u;r.faceCamera=false;r.targetOpen=1;RiftCount++;audio.Play("boss_rift_open");return r;}
 void Hit(string move,int index){if(move!=BossMoves.Ult_Cleave || !active)return;locked=false;if(sky)sky.flash=1;if(b.ThresholdIndex==0 || index==1){exhaust=true;exhaustAt=b.Elapsed;Stage="Emerge";if(sky){Destroy(sky.gameObject);sky=null;}if(ground)Destroy(ground.gameObject);ground=Rift(transform.position+Vector3.up*2*b.UnitScale,false,false);audio.StopLoop();} }
 void Update(){
 if(!b || !b.isActiveAndEnabled || !b.Entity.IsAlive){Cancel();return;}
 if(!active)return;
 if(EnemyController.CombatWorldPaused)return;
 float t=b.Elapsed,u=b.UnitScale;
 if(!sighed && t>=.3f){sighed=true;audio.Play("boss_sigh",.9f);GetComponent<BossMoveFx>().Mist(b.Animator.Eye.position,20,new Color(.35f,.2f,.5f,.6f));}
 if(!opened && t>=.55f){opened=true;ground=Rift(transform.position+Vector3.up*2*u,false,false);}
 if(!closed && t>=1.6f){closed=true;if(ground){ground.Slam();ground.autoDestroyWhenClosed=true;}audio.Play("boss_rift_close");}
 if(exhaust){b.Animator.Vanish=1-Mathf.Clamp01((t-exhaustAt)/.35f);if(ground && t-exhaustAt>.55f){ground.targetOpen=0;ground.autoDestroyWhenClosed=true;}Letterbox=0;RestoreCamera();return;}
 b.Animator.Vanish=Mathf.SmoothStep(0,1,(t-1.05f)/.5f);
 if(t<1.9f){Stage="Sigh";Letterbox=.095f*Mathf.Clamp01(t/.3f);if(cc)cc.FocusOn(b.Animator.Eye.position,savedZoom*.60f);}
 else {Letterbox=0;Stage="SkyEye";if(!sky){sky=Rift((Vector3)b.Aim+Vector3.up*4*u,true,true);audio.Loop("boss_eye_hum");}sky.transform.position=(Vector3)b.Aim+Vector3.up*4*u;sky.lookAt=b.Aim;bool lockNow=t>=3 && (t<3.6f || t>=4.8f);sky.squint=lockNow?1:0;if(lockNow && !locked){locked=true;sky.flash=1;audio.Play("boss_eye_lock");audio.Play("boss_cleave_whoosh");}if(cc)cc.FocusOn((Vector2)b.Aim+Vector2.up*1.5f*u,savedZoom);}
 }
 void RestoreCamera(){if(!cc)return;cc.followOffset=savedOffset;cc.SetFollowPlayer(savedFollow);cc.FocusOn(savedPosition,savedZoom);cc=null;}
 public void Cancel(){if(b && b.Animator)b.Animator.Vanish=0;active=false;Letterbox=0;if(bars)bars.enabled=false;Stage="None";if(ground)Destroy(ground.gameObject);if(sky)Destroy(sky.gameObject);ground=sky=null;RestoreCamera();if(audio)audio.StopLoop();}
 // IMGUI runs after overlay canvases and clips their HUD regardless of GUI.depth.
 // A non-interactive, lower-order overlay draws the bars behind existing UI.
 void LateUpdate(){
 bool visible=Letterbox>0 && !PlayerSessionData.IsCombatInputBlocked;
 if(visible && !bars)bars=CreateLetterbox(transform,out topBar,out bottomBar);
 if(!bars)return;bars.enabled=visible;
 topBar.rectTransform.sizeDelta=bottomBar.rectTransform.sizeDelta=new Vector2(0,Screen.height*Letterbox);
 }
 public static Canvas CreateLetterbox(Transform parent,out UnityEngine.UI.Image top,out UnityEngine.UI.Image bottom){var go=new GameObject("BossLetterbox",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(parent,false);var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=-32768;top=Bar(canvas,"Top",true);bottom=Bar(canvas,"Bottom",false);return canvas;}
 static UnityEngine.UI.Image Bar(Canvas canvas,string name,bool top){var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(canvas.transform,false);var image=go.GetComponent<UnityEngine.UI.Image>();image.color=Color.black;image.raycastTarget=false;var r=image.rectTransform;r.anchorMin=new Vector2(0,top?1:0);r.anchorMax=new Vector2(1,top?1:0);r.pivot=new Vector2(.5f,top?1:0);r.anchoredPosition=Vector2.zero;return image;}
 void OnDisable(){if(b){b.MoveStarted-=Begin;b.Impact-=Hit;b.PresentationCancelled-=Cancel;}Cancel();}
}
