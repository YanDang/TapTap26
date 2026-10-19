using UnityEngine;
using UnityEngine.UI;

namespace GameJam
{
    /// <summary>Read-only screen HP view. PlayerController remains the sole health owner;
    /// its original overhead HPBar is deliberately retained (pending art confirmation).</summary>
    public sealed class HeroHealthDisplay : MonoBehaviour
    {
        const float Width=240, Height=28, InnerWidth=228;
        Image fill, border;
        RectTransform ghostRect, fillRect;
        Text number;
        CanvasGroup group;
        float shown=-1, ghost=1, flash, phase;
        public string Caption => number ? number.text : string.Empty;
        public float FillFraction => Mathf.Max(0,shown);
        public float GhostFraction => ghost;
        public float DamageFlash => flash;
        public bool LowHealth => shown>0 && shown<=.3f;

        public static HeroHealthDisplay Build(Transform parent, Font font)
        {
            var existing=parent.GetComponentInChildren<HeroHealthDisplay>(true);
            if(existing)return existing;
            var root=new GameObject("HeroHp",typeof(RectTransform));
            var rt=(RectTransform)root.transform;rt.SetParent(parent,false);
            rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);
            // Between dodge/parry labels (bottom 162) and inventory (top 205).
            // Right edge 268 stays left of ultimate/release/caption (x >= 280).
            rt.anchoredPosition=new Vector2(28,-170);rt.sizeDelta=new Vector2(Width,Height);
            var view=root.AddComponent<HeroHealthDisplay>();view.Create(font);return view;
        }
        Image Box(string name,Transform parent,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform));var rt=(RectTransform)go.transform;
            rt.SetParent(parent,false);rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.sizeDelta=size;
            var image=go.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }
        void Create(Font font)
        {
            group=gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
            Box("Plate",transform,new Vector2(Width,Height),new Color(.012f,.02f,.035f,.94f));
            border=Box("Border",transform,new Vector2(Width-4,Height-4),new Color(.75f,.38f,.44f,.9f));
            Box("Background",transform,new Vector2(Width-8,Height-8),new Color(.03f,.02f,.04f,1));
            ghostRect=Box("Ghost",transform,new Vector2(InnerWidth,16),new Color(1,.95f,.9f,.9f)).rectTransform;
            fill=Box("Fill",transform,new Vector2(InnerWidth,16),new Color(.65f,.08f,.18f));fillRect=fill.rectTransform;
            foreach(var r in new[]{ghostRect,fillRect}){r.pivot=new Vector2(0,.5f);r.anchoredPosition=new Vector2(-InnerWidth*.5f,0);}
            var label=new GameObject("HealthNumber",typeof(RectTransform));var lr=(RectTransform)label.transform;
            lr.SetParent(transform,false);lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=new Vector2(8,0);lr.offsetMax=new Vector2(-8,0);
            number=label.AddComponent<Text>();number.font=font;number.fontSize=18;number.alignment=TextAnchor.MiddleRight;number.color=Color.white;number.raycastTarget=false;
            Tick(0);
        }
        void LateUpdate(){Tick(Time.unscaledDeltaTime);}
        public void Tick(float dt)
        {
            if(!number)return;
            var player=PlayerController.Instance;
            group.alpha=player && !PlayerSessionData.IsCombatInputBlocked && !(player.Ultimate && player.Ultimate.UI && player.Ultimate.UI.PickerOpen) ? 1 : 0;
            if(!player)return;
            dt=Mathf.Max(0,dt);phase+=dt;
            float hp=player.maxHp>0?Mathf.Clamp01(player.currentHp/player.maxHp):0;
            if(shown<0)ghost=hp;
            else if(hp<shown-.0001f)flash=.35f;
            shown=hp;
            ghost=hp>ghost?hp:Mathf.MoveTowards(ghost,hp,(ghost-hp>.25f?.9f:.45f)*dt);
            fillRect.sizeDelta=new Vector2(InnerWidth*hp,16);ghostRect.sizeDelta=new Vector2(InnerWidth*ghost,16);
            number.text=Mathf.CeilToInt(Mathf.Max(0,player.currentHp))+" / "+Mathf.CeilToInt(Mathf.Max(0,player.maxHp));
            float pulse=LowHealth?.5f+.5f*Mathf.Sin(phase*9):0;
            fill.color=Color.Lerp(new Color(.65f,.08f,.18f),new Color(1,.35f,.32f),pulse*.6f);
            flash=Mathf.Max(0,flash-dt);
            border.color=Color.Lerp(new Color(.75f,.38f,.44f,.9f),new Color(1,.2f,.25f,1),Mathf.Max(flash/.35f,pulse*.7f));
        }
    }
}
