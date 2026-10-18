using UnityEngine;
namespace GameJam.HeroUlt
{
    /// <summary>XY rings with URP shader; scale matches gameplay radius, no material asset mutation.</summary>
    public sealed class TapUltVisual : MonoBehaviour
    {
        LineRenderer line;float age,duration,radius;Color tint;Material material;
        public static GameObject Ring(Vector2 p,float radius,Color tint,float duration,Transform follow=null)
        {
            var go=new GameObject("Ult XY energy");go.transform.position=p;if(follow)go.transform.SetParent(follow,true);
            var v=go.AddComponent<TapUltVisual>();v.radius=radius;v.tint=tint;v.duration=duration;
            v.line=go.AddComponent<LineRenderer>();v.line.useWorldSpace=false;v.line.loop=true;v.line.positionCount=64;
            v.material=new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));v.line.sharedMaterial=v.material;
            v.line.sortingOrder=240;v.line.widthMultiplier=Mathf.Max(.012f,radius*.07f);v.Draw(1);return go;
        }
        void Draw(float k){float r=radius*(duration>0?Mathf.Lerp(.15f,1,k):1);for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;line.SetPosition(i,new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,0));}var c=tint;c.a=duration>0?1-k: .75f;line.startColor=line.endColor=c;}
        void Update(){if(TapHeroUlt.Paused)return;if(duration<=0)return;age+=Mathf.Min(Time.deltaTime,.05f);Draw(Mathf.Clamp01(age/duration));if(age>=duration)Destroy(gameObject);}
        void OnDestroy(){if(material)Destroy(material);}
    }
}
