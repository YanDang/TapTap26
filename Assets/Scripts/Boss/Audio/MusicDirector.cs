using UnityEngine;
/// <summary>Two PCM decks. Crossfade clock freezes with world, no global AudioListener mutation.</summary>
public sealed class MusicDirector : MonoBehaviour {
 public static MusicDirector Instance {get;private set;}
 AudioSource[] decks=new AudioSource[2];AudioSource accent;int front;float fade=2;bool paused;string requested="";
 public string Track=>requested;public bool Paused=>paused;
 public bool WarningPlaying=>accent && accent.isPlaying && accent.clip && accent.clip.name!="victory";
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){if(!Instance)new GameObject("BossMusicDirector").AddComponent<MusicDirector>();}
 void Awake(){Instance=this;DontDestroyOnLoad(gameObject);for(int i=0;i<2;i++){var child=new GameObject("MusicDeck"+i);child.transform.SetParent(transform,false);decks[i]=child.AddComponent<AudioSource>();decks[i].playOnAwake=false;decks[i].loop=true;decks[i].volume=0;child.AddComponent<AudioLowPassFilter>();}accent=gameObject.AddComponent<AudioSource>();accent.playOnAwake=false;}
 public void StopWarning(){if(accent && accent.clip && accent.clip.name!="victory")accent.Stop();}
 public void Accent(string name){accent.clip=BossAudio.Clip("Music/"+name);accent.volume=.4f;accent.Play();if(paused)accent.Pause();}
 void Update(){var encounter=BossEncounterController.Instance;var b=encounter?encounter.Boss:null;bool p=EnemyController.CombatWorldPaused;if(p!=paused){paused=p;foreach(var s in decks){if(p)s.Pause();else s.UnPause();}if(p)accent.Pause();else accent.UnPause();}if(paused)return;
 string next=b && b.isActiveAndEnabled && b.Entity.IsAlive?(b.Phase==1?"boss_phase1":"boss_phase2"):"workshop";
 if(next!=requested && fade>=2){requested=next;int back=1-front;decks[back].Stop();decks[back].clip=BossAudio.Clip("Music/"+next);decks[back].volume=0;if(decks[back].clip){if(decks[front].clip && next.StartsWith("boss") && decks[front].clip.name.StartsWith("boss"))decks[back].timeSamples=decks[front].timeSamples%decks[back].clip.samples;decks[back].Play();}fade=0;}
 fade=Mathf.Min(2,fade+Time.deltaTime);float gain=b && b.Invulnerable?.18f:.45f;if(fade<2){decks[front].volume=(1-fade/2)*gain;decks[1-front].volume=fade/2*gain;}else if(decks[1-front].isPlaying){decks[front].Stop();front=1-front;decks[front].volume=gain;}else decks[front].volume=gain;
 foreach(var f in GetComponentsInChildren<AudioLowPassFilter>())f.cutoffFrequency=Time.timeScale<.99f?1800:22000;
 }
 void OnDestroy(){if(Instance==this)Instance=null;}
}
