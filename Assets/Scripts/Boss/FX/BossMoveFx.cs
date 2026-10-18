using System.Collections.Generic;
using UnityEngine;
using GameJam.Boss;

/// <summary>XY presentation; consumes resolved shape snapshots, never authorizes damage.</summary>
public sealed class BossMoveFx : MonoBehaviour {
 class Timed {public float age,life;public double bornAt=Time.realtimeSinceStartupAsDouble;public bool updated;}
 sealed class Trace : Timed {public LineRenderer line;public Color color;}
 sealed class Mote : Timed { public SpriteRenderer sprite; public Vector3 velocity; public Color color; }
 // Unity deltaTime describes the previous frame, including work before an
 // Impact callback created this effect. Never charge that pre-birth work.
 static float Delta(Timed item){float dt=Time.deltaTime;if(!item.updated){dt=Mathf.Min(dt,Mathf.Max(0,(float)(Time.realtimeSinceStartupAsDouble-item.bornAt)*Time.timeScale));item.updated=true;}return dt;}
 readonly List<Mote> motes=new List<Mote>();
 readonly List<Trace> traces=new List<Trace>();
 BossBrain b;public int Strikes,Footsteps; public float Clock {get;private set;}
 public int LiveCount=>traces.Count;
 public string TraceDiagnostic=>string.Join(";",traces.ConvertAll(t=>$"{t.line.name}:{t.age}/{t.life}"));
 void OnEnable(){b=GetComponent<BossBrain>();if(!b || !b.Animator)return;b.MoveStarted+=Begin;b.PresentationCancelled+=Clear;b.Impact+=Hit;b.Animator.OnFootfall+=Step;}
 void OnDisable(){if(b){b.MoveStarted-=Begin;b.PresentationCancelled-=Clear;b.Impact-=Hit;if(b.Animator)b.Animator.OnFootfall-=Step;}Clear();}
 public void Clear(){foreach(var t in traces)if(t.line)Destroy(t.line.gameObject);traces.Clear();foreach(var m in motes)if(m.sprite)Destroy(m.sprite.gameObject);motes.Clear();}
 public void Mist(Vector3 at,int count,Color color,float spread=1){for(int i=0;i<count;i++){var go=new GameObject("BossMist");go.transform.SetParent(transform,false);go.transform.position=at;var sr=go.AddComponent<SpriteRenderer>();sr.sprite=BossFxAssets.Dot;sr.sharedMaterial=BossFxAssets.Material;sr.sortingOrder=72;sr.color=color;go.transform.localScale=Vector3.one*b.UnitScale*BossVisualRandom.Range(.25f,.6f);motes.Add(new Mote{sprite=sr,color=color,life=BossVisualRandom.Range(.7f,1.4f),velocity=new Vector3(BossVisualRandom.Range(-1f,1f)*spread,BossVisualRandom.Range(-.7f,.3f))*b.UnitScale});}}
 public void Stroke(string name,Vector3[] points,float width,Color color,float life,int order=65){
 var g=new GameObject(name);g.transform.SetParent(transform,false);var l=g.AddComponent<LineRenderer>();l.sharedMaterial=BossFxAssets.Material;l.useWorldSpace=true;l.alignment=LineAlignment.TransformZ;l.positionCount=points.Length;l.SetPositions(points);l.widthMultiplier=width;l.sortingOrder=order;l.startColor=l.endColor=color;traces.Add(new Trace{line=l,color=color,life=life});}
 public void Burst(Vector3 at,float radius,string label="GroundCrack"){
 for(int k=0;k<12;k++){float a=k*Mathf.PI/6;Vector3 dir=new Vector3(Mathf.Cos(a),Mathf.Sin(a));var pts=new Vector3[5];for(int i=0;i<5;i++)pts[i]=at+dir*radius*i/4+new Vector3(-dir.y,dir.x)*Mathf.Sin(i*3+k)*radius*.08f;Stroke(label,pts,.06f*b.UnitScale,new Color(.75f,.5f,1),1.2f);}}
 void Begin(string m){Clear();if(m==BossMoves.Transform){Mist(b.Animator.CloakCenter.position,24,new Color(.12f,.16f,.38f,.9f),3);Burst(b.Animator.Eye.position,3*b.UnitScale,"TransformVeins");for(int i=0;i<14;i++){Vector3 c=b.Animator.CloakCenter.position;float a=i*2.4f;Vector3 d=new Vector3(Mathf.Cos(a),Mathf.Sin(a))*b.UnitScale;Stroke("CloakShard",new[]{c+d,c+d*2,c+d*2+Vector3.up*.2f*b.UnitScale},.22f*b.UnitScale,new Color(.12f,.18f,.4f),2.5f);}}}
 void Step(int side){if(EnemyController.CombatWorldPaused)return;Footsteps++;var foot=side==0?b.Animator.foot:b.Animator.otherFoot;Mist(foot?foot.position:transform.position,6,new Color(.3f,.26f,.36f,.6f));GetComponent<BossAudio>()?.Play("boss_step_"+BossVisualRandom.Range(1,4),.5f);}
 void Hit(string m,int i){Strikes++;var s=b.LastImpactShape;float u=b.UnitScale;
 if(m==BossMoves.Ult_Cleave){Vector3 a=s.origin,z=s.origin+s.direction*s.length;Vector3 sky=(Vector3)b.Aim+Vector3.up*4*u;for(int j=0;j<4;j++){Vector3 side=new Vector3(-s.direction.y,s.direction.x)*(j-1.5f)*.32f*u;Stroke("GiantClaw",new[]{sky+side*2,(Vector3)b.Aim+side},.12f*u,new Color(.9f,.85f,1,.65f),.3f,90);traces[traces.Count-1].line.widthCurve=AnimationCurve.Linear(0,1,1,.1f);Stroke("CleaveScar",new[]{a+side,z+side},.12f*u,new Color(.7f,.4f,1),5);}return;}
 if(m==BossMoves.P2_EyeBeam){Vector3 a=s.origin,z=s.origin+s.direction*s.length;Stroke("BeamFloor",new[]{a,z},s.width,new Color(.6f,.3f,1,.65f),.32f);Stroke("BeamCore",new[]{a,z},s.width*.24f,Color.white,.32f);Stroke("BeamDrop",new[]{b.Animator.Eye.position,a},.12f*u,Color.white,.32f);return;}
 if(m==BossMoves.P1_CloakSweep || m==BossMoves.P1_Wrap){Stroke("CloakArc",s.Outline(),.35f*u,new Color(.18f,.22f,.5f),.5f);}
 else {Vector3 at=b.Animator.ClawTip.position;for(int j=0;j<3;j++)Stroke("ClawSilver",new[]{at+Vector3.up*(j*.2f*u),at+(Vector3)b.Direction*1.8f*u+Vector3.up*(j*.2f*u)},.09f*u,Color.white,.25f);}
 if(m==BossMoves.P1_LeapSlam || i==BossBrain.ImpactsFor(m).Length-1)Burst(s.origin,s.radius>0?s.radius:1.5f*u);
 }
 void Update(){if(!b || !b.isActiveAndEnabled || !b.Entity.IsAlive){Clear();return;}foreach(var t in traces)if(t.line)t.line.enabled=!PlayerSessionData.IsCombatInputBlocked;if(EnemyController.CombatWorldPaused)return;Clock+=Time.deltaTime;for(int j=motes.Count-1;j>=0;j--){var m=motes[j];float dt=Delta(m);m.age+=dt;if(m.age>=m.life){Destroy(m.sprite.gameObject);motes.RemoveAt(j);continue;}m.sprite.transform.position+=m.velocity*dt;m.sprite.transform.localScale+=Vector3.one*dt*b.UnitScale*.3f;var color=m.color;color.a*=1-m.age/m.life;m.sprite.color=color;}for(int i=traces.Count-1;i>=0;i--){var t=traces[i];t.age+=Delta(t);if(t.age>=t.life){Destroy(t.line.gameObject);traces.RemoveAt(i);continue;}var c=t.color;c.a*=1-t.age/t.life;t.line.startColor=t.line.endColor=c;}}
}
