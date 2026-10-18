using UnityEngine;
using GameJam.Boss;
/// <summary>Encounter-owned 2D voices; pause reserves voices, cancellation releases them.</summary>
public sealed class BossAudio : MonoBehaviour {
 BossBrain b;AudioSource[] voices=new AudioSource[16];AudioSource loop;bool paused;float hp,playerHp;int posture;EnemyController.EnemyAIState state;
 public int Played {get;private set;} public bool Paused=>paused;
 public static AudioClip Clip(string name)=>Resources.Load<AudioClip>("Audio/"+name);
 void OnEnable(){b=GetComponent<BossBrain>();for(int i=0;i<voices.Length;i++){voices[i]=gameObject.AddComponent<AudioSource>();voices[i].playOnAwake=false;voices[i].spatialBlend=0;}loop=gameObject.AddComponent<AudioSource>();loop.playOnAwake=false;loop.loop=true;b.MoveStarted+=Begin;b.Impact+=Hit;b.PresentationCancelled+=StopAll;b.Entity.Died+=Death;hp=b.Entity.currentHp;playerHp=PlayerController.Instance.currentHp;}
 public AudioSource Play(string name,float volume=.65f){if(paused || !isActiveAndEnabled)return null;var clip=Clip(name);if(!clip){Debug.LogError("[P3] missing Audio/"+name);return null;}foreach(var s in voices)if(s && !s.isPlaying){s.clip=clip;s.volume=volume;s.Play();Played++;return s;}return null;}
 public void Loop(string name){StopLoop();loop.clip=Clip(name);loop.volume=.45f;if(loop.clip){loop.Play();if(paused)loop.Pause();}}
 public void StopLoop(){if(loop)loop.Stop();}
 public void StopAll(){foreach(var s in voices)if(s)s.Stop();StopLoop();MusicDirector.Instance?.StopWarning();}
 void Begin(string m){if(m==BossMoves.Transform)Play("boss_transform_rip");if(m==BossMoves.P2_EyeBeam)Play("boss_beam_charge");if(m==BossMoves.Ult_Rise)MusicDirector.Instance?.Accent("ult_rise");}
 void Hit(string m,int i){if(m==BossMoves.Ult_Cleave){Play("boss_cleave_impact",.85f);MusicDirector.Instance?.Accent("ult_impact");return;}if(m==BossMoves.P2_EyeBeam){if(!loop.isPlaying)Loop("boss_beam");return;}if(m==BossMoves.P1_CloakSweep || m==BossMoves.P1_Wrap)Play("boss_cloak_sweep");else{Play("boss_claw_swing_"+(i%3+1));if(b.LastImpactPlayerDamaged)Play("boss_claw_hit_"+(i%3+1));}if(m==BossMoves.P1_LeapSlam || i==BossBrain.ImpactsFor(m).Length-1)Play("boss_slam");playerHp=PlayerController.Instance.currentHp;}
 void Death(EnemyController e){StopAll();Play("boss_death");MusicDirector.Instance?.Accent("victory");}
 void Update(){bool p=EnemyController.CombatWorldPaused;if(p!=paused){paused=p;foreach(var s in voices)if(s){if(p)s.Pause();else s.UnPause();}if(p)loop.Pause();else loop.UnPause();}if(p || !b)return;if(b.Entity.currentHp<hp && b.Entity.IsAlive)Play("boss_hurt");if(b.Posture>=3 && posture<3)Play("boss_posture_break");if(b.Entity.aiState==EnemyController.EnemyAIState.Executing && state!=b.Entity.aiState)Play("boss_execution");hp=b.Entity.currentHp;posture=b.Posture;state=b.Entity.aiState;}
 void OnDisable(){StopAll();if(b){b.MoveStarted-=Begin;b.Impact-=Hit;b.PresentationCancelled-=StopAll;if(b.Entity)b.Entity.Died-=Death;}foreach(var s in voices)if(s)Destroy(s);if(loop)Destroy(loop);}
}
