using UnityEngine;
using GameJam;
namespace GameJam.HeroUlt
{
    public sealed class TapUltProjectile : MonoBehaviour
    {
        TapHeroUlt owner; Vector2 direction; float traveled; int generation;
        public static TapUltProjectile Spawn(TapHeroUlt owner, Vector2 from, Vector2 direction)
        {
            var go=new GameObject("贝壳加农炮弹");go.transform.position=from;
            var p=go.AddComponent<TapUltProjectile>();p.owner=owner;p.direction=direction;p.generation=owner.Generation;
            var v=TapUltVisual.Ring(from,.24f*owner.Unit,new Color(1,.8f,.2f),0,go.transform);
            return p;
        }
        void Update()
        {
            if(!owner || !owner.Player.IsAlive || owner.Generation!=generation){Destroy(gameObject);return;}
            if(TapHeroUlt.Paused)return;
            float step=Mathf.Min(25*owner.Unit*Time.deltaTime,18*owner.Unit-traveled);
            Vector2 from=transform.position,to=from+direction*step;
            float nearest=step; bool hit=false;
            // Sweep all candidates and choose earliest hit, not AllEnemies iteration order.
            foreach(var e in EnemyController.AllEnemies)if(e && e.IsAlive){
                Vector2 d=(Vector2)e.transform.position-from;float along=Vector2.Dot(d,direction);
                float r=.62f*owner.Unit;
                if(along>=-r && along<=step+r && (d-direction*Mathf.Clamp(along,0,step)).magnitude<=r){nearest=Mathf.Min(nearest,Mathf.Max(0,along-r));hit=true;}
            }
            foreach(var h in Physics2D.CircleCastAll(from,.15f*owner.Unit,direction,step)){
                if(!h.collider || h.collider.isTrigger || h.collider.GetComponentInParent<PlayerController>() || h.collider.GetComponentInParent<EnemyController>())continue;
                var f=h.collider.GetComponentInParent<FurnitureObject>();
                if(f && !f.Blocks)continue;
                nearest=Mathf.Min(nearest,h.distance);hit=true;
            }
            // Tilemap ground boundaries are not necessarily colliders. Substep map query avoids tunnelling.
            for(float d=.2f*owner.Unit;d<=step;d+=.2f*owner.Unit)if(!owner.Walkable(from+direction*d)){nearest=Mathf.Min(nearest,d);hit=true;break;}
            traveled+=nearest;transform.position=from+direction*nearest;
            if(hit || traveled>=18*owner.Unit-.001f){owner.Explode(transform.position);Destroy(gameObject);}
        }
    }
}
