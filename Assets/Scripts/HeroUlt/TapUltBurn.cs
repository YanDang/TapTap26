using UnityEngine;
namespace GameJam.HeroUlt
{
    public sealed class TapUltBurn : MonoBehaviour
    {
        TapHeroUlt owner; EnemyController enemy; float remaining,tick; int generation;
        GameObject visual;
        public int Ticks { get; private set; }
        public static TapUltBurn Apply(TapHeroUlt owner,EnemyController enemy)
        {
            var b=enemy.GetComponent<TapUltBurn>();if(!b)b=enemy.gameObject.AddComponent<TapUltBurn>();
            b.owner=owner;b.enemy=enemy;b.remaining=4;b.generation=owner.Generation;
            if(!b.visual)b.visual=TapUltVisual.Ring(enemy.transform.position,.65f*owner.Unit,new Color(1,.3f,.02f),0,enemy.transform);
            return b;
        }
        void Update()
        {
            if(!owner || !enemy || !enemy.IsAlive || !owner.Player.IsAlive || generation!=owner.Generation){Destroy(this);return;}
            if(TapHeroUlt.Paused)return;
            float dt=Mathf.Min(Time.deltaTime,remaining);remaining-=dt;tick+=dt;
            if(tick>=.9999f){tick-=1;Ticks++;owner.Player.DealDamage(enemy,owner.DamageUnit,0);enemy.Flash(new Color(1,.4f,.05f));}
            if(remaining<=0)Destroy(this);
        }
        void OnDestroy(){if(visual)Destroy(visual);}
    }
}
