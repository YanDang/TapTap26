using System.Collections.Generic;
using UnityEngine;
namespace GameJam.Gear
{
    /// <summary>URP XY presentation adapter. Geometry lives in gameplay XY, not source XZ.
    /// Owned effects are explicitly cancelled and do not age while overlays/hitstop pause combat.</summary>
    public sealed class TapGearFx : MonoBehaviour, IGearFxBackend
    {
        static TapGearFx backend;static Material material;
        class Effect { public TapGearCombat owner;public GameObject go;public LineRenderer line;public Transform follow;public float left,total;public string key;public double born=Time.realtimeSinceStartupAsDouble;public bool aged; }
        readonly List<Effect> effects=new List<Effect>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(backend)return;var g=new GameObject("Gear XY FX");DontDestroyOnLoad(g);backend=g.AddComponent<TapGearFx>();GearFx.Backend=backend;}
        static TapGearCombat Owner => PlayerController.Instance?PlayerController.Instance.Gear:null;
        static float Unit => Owner?Owner.Unit:.2f;
        public static int Live => backend?backend.effects.Count:0;
        public static void Clear(TapGearCombat owner){if(!backend)return;for(int i=backend.effects.Count-1;i>=0;i--)if(backend.effects[i].owner==owner){Destroy(backend.effects[i].go);backend.effects.RemoveAt(i);}}
        void Remove(Transform t,string key){for(int i=effects.Count-1;i>=0;i--)if(effects[i].follow==t && effects[i].key==key){Destroy(effects[i].go);effects.RemoveAt(i);}}
        void Line(Vector3[] points,Color color,float width,float seconds,Transform follow=null,string key=null)
        {
            // The production 2D renderer does not render the URP forward particle pass.
            // Reuse P3's verified Universal2D vertex-color shader, not an error-free invisible shader.
            if(!material)material=BossFxAssets.Material;
            var go=new GameObject("GearFX "+(key??"pulse"));var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=!follow;line.widthMultiplier=Mathf.Max(.008f,width);line.positionCount=points.Length;line.SetPositions(points);line.startColor=line.endColor=color;line.sortingOrder=65;line.alignment=LineAlignment.TransformZ;line.numCapVertices=3;
            if(follow)go.transform.SetParent(follow,false);effects.Add(new Effect{owner=Owner,go=go,line=line,follow=follow,key=key,left=seconds,total=seconds});
        }
        void Ring(Vector3 p,float radius,Color color,float seconds,Transform follow=null,string key=null){var pts=new Vector3[49];for(int i=0;i<pts.Length;i++){float a=i*Mathf.PI*2/48;pts[i]=p+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius;}Line(pts,color,Unit*.08f,seconds,follow,key);}
        void Update(){for(int i=effects.Count-1;i>=0;i--){var e=effects[i];if(!e.go || !e.owner || !e.owner.isActiveAndEnabled || !e.owner.Player.IsAlive){if(e.go)Destroy(e.go);effects.RemoveAt(i);continue;}e.line.enabled=!PlayerSessionData.IsCombatInputBlocked;if(TapGearCombat.Paused)continue;if(e.total<0)continue;float dt=TimeManager.PlayerDelta;if(!e.aged){dt=Mathf.Min(dt,Mathf.Max(0,(float)(Time.realtimeSinceStartupAsDouble-e.born)*Time.timeScale));e.aged=true;}e.left-=dt;if(e.left<=0){Destroy(e.go);effects.RemoveAt(i);}}}
        public static void Warning(Transform hero,bool on){if(!backend)return;backend.Remove(hero,"warning");if(on)backend.Ring(Vector3.up*Unit*1.8f,Unit*.25f,Color.yellow,-1,hero,"warning");}
        public void PerfectDodge(Vector3 p){Ring(p,Unit,Color.cyan,.3f);Sfx.Play("sfx_roll",.7f,true);}
        public void Counter(Vector3 a,Vector3 b){Line(new[]{a,b},Color.white,Unit*.12f,.25f);Ring(b,Unit*.6f,Color.yellow,.3f);}
        public void ShellGuard(Transform h,bool on){Remove(h,"guard");if(on)Ring(Vector3.zero,Unit*1.1f,Color.green,-1,h,"guard");}
        public void ShellBlock(Vector3 p,bool perfect){Ring(p,Unit*1.5f,perfect?Color.yellow:Color.green,.3f);}
        public void StaticStacks(Transform h,int n){Remove(h,"static");if(n>0)Ring(Vector3.zero,Unit*(.6f+n*.12f),Color.yellow,-1,h,"static");}
        public void ChainLightning(Vector3[] p){Line(p,Color.yellow,Unit*.12f,.35f);Sfx.Play("sfx_parry",.5f,true);}
        public void FloatBubble(Transform h,float s){Ring(Vector3.zero,Unit*1.3f,new Color(.7f,.5f,1),s,h,"bubble");}
        public void EelArc(Vector3 a,Vector3 b){Line(new[]{a,Vector3.Lerp(a,b,.5f)+Vector3.up*Unit*.2f,b},Color.yellow,Unit*.08f,.35f);}
        public void WaterJet(Vector3 a,Vector3 b,float w){Line(new[]{a,b},new Color(.3f,.8f,1),w,.4f);}
        public void Puddle(Vector3 p,float r,float s){Ring(p,r,Color.cyan,s);}
        public void Grapple(Vector3 a,Vector3 b,float s){Line(new[]{a,b},new Color(.7f,.8f,.9f),Unit*.1f,s);}
        public void Decoy(Vector3 p,float s){Ring(p,Unit*.7f,new Color(.8f,.5f,1),s);}
        public void Explosion(Vector3 p,float r){Ring(p,r,new Color(1,.5f,.2f),.4f);Sfx.Play("sfx_explosion",.4f,true);}
        public void JetTrail(Vector3 a,Vector3 b,float s){Line(new[]{a,b},new Color(.75f,.5f,1),Unit*.6f,s);}
        public void Root(Transform h,bool on){Remove(h,"root");if(on)Ring(Vector3.zero,Unit*.75f,new Color(.5f,1,.5f),-1,h,"root");}
        public void CoralSpikes(Vector3 p,float r){Ring(p,r,new Color(1,.5f,.4f),.45f);}
        public void Heal(Transform h){Ring(Vector3.zero,Unit*.9f,Color.green,.6f,h,"heal");}
    }
}
