using UnityEngine;
using UnityEngine.UI;
namespace GameJam.HeroUlt
{
    public sealed class TapUltUI : MonoBehaviour
    {
        TapHeroUlt owner; Canvas canvas; Image icon,progress;Text caption;GameObject picker;RectTransform iconRect;
        float rejected;
        public Button ReleaseButton { get; private set; }
        public Button SwitchButton { get; private set; }
        public UltArcGraphic Arcs { get; private set; }
        public bool PickerOpen => picker && picker.activeSelf;
        static readonly string[] Names={"钳臂·重锤","贝壳加农","壳中回响"};
        static readonly string[] Icons={"ult_icon_arm","ult_icon_cannon","ult_icon_echo"};
        static readonly string[] Desc={"冲刺重拳 · 锤飞并眩晕\nBoss 8伤 / +2架势","蓄力炮击 · 爆炸与燃烧\n6伤 + 每秒1伤持续4秒","缩壳5秒 · 吸收所有攻击\n最多8层 · 再点释放"};
        Font font;
        public static TapUltUI Build(TapHeroUlt owner)
        {
            var go=new GameObject("主角大招 HUD",typeof(RectTransform));var ui=go.AddComponent<TapUltUI>();ui.owner=owner;ui.Create();return ui;
        }
        RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size,Vector2 anchor)
        {
            var go=new GameObject(name,typeof(RectTransform));var rt=(RectTransform)go.transform;rt.SetParent(parent,false);rt.anchorMin=rt.anchorMax=anchor;rt.sizeDelta=size;rt.anchoredPosition=pos;return rt;
        }
        Image Image(string name,Transform parent,Vector2 pos,Vector2 size,Color color,Vector2 anchor)
        {var rt=Rect(name,parent,pos,size,anchor);var im=rt.gameObject.AddComponent<Image>();im.color=color;im.raycastTarget=false;return im;}
        Text Label(Transform parent,string text,Vector2 pos,Vector2 size,int fontsize,Vector2 anchor)
        {var rt=Rect("文字",parent,pos,size,anchor);var t=rt.gameObject.AddComponent<Text>();t.font=font;t.text=text;t.fontSize=fontsize;t.alignment=TextAnchor.MiddleCenter;t.color=Color.white;t.raycastTarget=false;return t;}
        Button Button(Transform parent,string text,Vector2 pos,Vector2 size,Vector2 anchor,UnityEngine.Events.UnityAction callback)
        {var bg=Image(text,parent,pos,size,new Color(.035f,.11f,.16f,.97f),anchor);bg.raycastTarget=true;var b=bg.gameObject.AddComponent<Button>();b.onClick.AddListener(callback);Label(bg.transform,text,Vector2.zero,size,21,new Vector2(.5f,.5f));return b;}
        void Create()
        {
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Match host HUD's CJK font when available.
            foreach(var t in FindObjectsOfType<Text>())if(t.font && t.font.name!="Arial" && t.font.name!="LegacyRuntime"){font=t.font;break;}
            canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=80;
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            gameObject.AddComponent<GraphicRaycaster>();var tl=new Vector2(0,1);
            ReleaseButton=Button(transform,"",new Vector2(380,-180),new Vector2(112,112),tl,Click);iconRect=(RectTransform)ReleaseButton.transform;
            icon=Image("当前大招",iconRect,Vector2.zero,new Vector2(96,96),Color.white,new Vector2(.5f,.5f));
            progress=Image("充能环",iconRect,Vector2.zero,new Vector2(112,112),Color.cyan,new Vector2(.5f,.5f));progress.sprite=Resources.Load<Sprite>("HeroUlt/ult_ring");progress.type=UnityEngine.UI.Image.Type.Filled;progress.fillMethod=UnityEngine.UI.Image.FillMethod.Radial360;
            var arcRect=Rect("就绪电弧",iconRect,Vector2.zero,new Vector2(112,112),new Vector2(.5f,.5f));Arcs=arcRect.gameObject.AddComponent<UltArcGraphic>();Arcs.raycastTarget=false;
            caption=Label(transform,"",new Vector2(380,-257),new Vector2(200,38),20,tl);
            SwitchButton=Button(transform,"切换大招",new Vector2(380,-302),new Vector2(150,38),tl,OpenPicker);
            picker=Image("大招选择",transform,Vector2.zero,new Vector2(1080,440),new Color(.015f,.04f,.065f,.98f),new Vector2(.5f,.5f)).gameObject;
            picker.GetComponent<Image>().raycastTarget=true;Label(picker.transform,"选择大招 · 仅非战斗可切换",new Vector2(0,172),new Vector2(950,50),30,new Vector2(.5f,.5f));
            for(int i=0;i<3;i++){int k=i;var b=Button(picker.transform,"",new Vector2((i-1)*340,-5),new Vector2(315,285),new Vector2(.5f,.5f),()=>{if(owner.Select((UltKind)k))picker.SetActive(false);});var im=Image("图标",b.transform,new Vector2(0,70),new Vector2(105,105),Color.white,new Vector2(.5f,.5f));im.sprite=Resources.Load<Sprite>("HeroUlt/"+Icons[i]);Label(b.transform,Names[i],new Vector2(0,-5),new Vector2(300,45),27,new Vector2(.5f,.5f));Label(b.transform,Desc[i],new Vector2(0,-76),new Vector2(300,90),20,new Vector2(.5f,.5f));}
            Button(picker.transform,"关闭",new Vector2(0,-185),new Vector2(120,40),new Vector2(.5f,.5f),()=>picker.SetActive(false));picker.SetActive(false);
        }
        public void OpenPicker(){if(owner.CanChoose)picker.SetActive(!picker.activeSelf);else rejected=.2f;}
        public void Click(){if(owner.CanChoose && !owner.Ready){OpenPicker();return;}if(!owner.TryRelease())rejected=.2f;}
        void Update()
        {
            if(!owner || !owner.Player)return;
            canvas.enabled=!PlayerSessionData.IsCombatInputBlocked && owner.Player.IsAlive;
            if(!owner.CanChoose)picker.SetActive(false);
            icon.sprite=Resources.Load<Sprite>("HeroUlt/"+Icons[(int)owner.Choice]);progress.fillAmount=owner.Charge/100;
            Arcs.Active=owner.Ready;caption.text=owner.Echo?"回响 "+owner.Stacks+" / 8":Names[(int)owner.Choice]+"  "+(owner.Ready?"[ R ]":Mathf.FloorToInt(owner.Charge)+"%");
            if(rejected>0){rejected-=Time.unscaledDeltaTime;iconRect.anchoredPosition=new Vector2(380+Mathf.Sin(rejected*100)*5,-180);}else iconRect.anchoredPosition=new Vector2(380,-180);
        }
    }
}
