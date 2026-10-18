using UnityEngine;
using UnityEngine.UI;
namespace GameJam.Gear
{
    public sealed class TapGearUI : MonoBehaviour
    {
        public static TapGearUI Instance;
        public static bool IsOpen => Instance && Instance.panel && Instance.panel.activeSelf;
        public TapGearCombat Owner;
        GameObject panel;Text message,slots;Font font;Canvas canvas;
        public Button OpenButton, GrappleButton;
        readonly Text[] details=new Text[15];
        public static bool CanModify(out string why)
        {
            var p=PlayerController.Instance;
            why=null;
            if(!p){if(!Application.isPlaying)return true;why="主角未就绪";return false;}
            if(!p.IsAlive || p.Unavailable || p.Placing || p.UltimateLocked || p.InCombat || BossEncounterController.EncounterActive || (p.Gear && (p.Gear.Busy || p.Gear.Guarding))){why="战斗或动作中不能改装";return false;}
            if(!IsOpen && (PlayerSessionData.IsCombatInputBlocked || !p.CanBeginUltimate || (p.Ultimate && p.Ultimate.UI && p.Ultimate.UI.PickerOpen))){why="请先关闭其他界面并结束动作";return false;}
            foreach(var e in EnemyController.AllEnemies)if(e && e.IsAlive && Vector2.Distance(e.transform.position,p.transform.position)<9*(p.Gear?p.Gear.Unit:1)){why="附近有敌人";return false;}
            return true;
        }
        public static TapGearUI Build(TapGearCombat owner){var go=new GameObject("装备改装 HUD",typeof(RectTransform));var u=go.AddComponent<TapGearUI>();u.Owner=owner;u.Create();return u;}
        RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size){var g=new GameObject(name,typeof(RectTransform));var r=(RectTransform)g.transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.sizeDelta=size;r.anchoredPosition=pos;return r;}
        Image Box(Transform parent,Vector2 pos,Vector2 size,Color color){var r=Rect("面板",parent,pos,size);var im=r.gameObject.AddComponent<Image>();im.color=color;return im;}
        Text Label(Transform parent,string text,Vector2 pos,Vector2 size,int px){var t=Rect("文字",parent,pos,size).gameObject.AddComponent<Text>();t.font=font;t.text=text;t.color=Color.white;t.fontSize=px;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;}
        Button Button(Transform parent,string text,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action){var im=Box(parent,pos,size,new Color(.05f,.23f,.28f,.98f));var b=im.gameObject.AddComponent<Button>();b.onClick.AddListener(action);Label(im.transform,text,Vector2.zero,size,19);return b;}
        void Create()
        {
            Instance=this;font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");foreach(var t in FindObjectsOfType<Text>())if(t.font && t.font.name!="Arial" && t.font.name!="LegacyRuntime"){font=t.font;break;}
            canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=2000;gameObject.AddComponent<GraphicRaycaster>();var sc=gameObject.AddComponent<CanvasScaler>();sc.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;sc.referenceResolution=new Vector2(1920,1080);sc.matchWidthOrHeight=.5f;
            OpenButton=Button(transform,"G 装备改装",new Vector2(-580,160),new Vector2(150,42),()=>Toggle());
            GrappleButton=Button(transform,"Q 抓钩",new Vector2(770,-275),new Vector2(118,70),()=>Owner.GrappleNearest());
            panel=Box(transform,Vector2.zero,new Vector2(1680,930),new Color(.015f,.035f,.055f,.99f)).gameObject;
            Label(panel.transform,"寄居蟹 · 装备改装 / 与家具共用背壳仓库",new Vector2(0,420),new Vector2(1500,55),32);
            slots=Label(panel.transform,"",new Vector2(0,365),new Vector2(1580,50),22);
            for(int i=0;i<GearDefs.All.Length;i++){
                var def=GearDefs.All[i];int k=i;float x=(i%3-1)*535,y=280-(i/3)*130;
                var card=Box(panel.transform,new Vector2(x,y),new Vector2(520,122),new Color(.06f,.095f,.12f,1));
                var icon=Box(card.transform,new Vector2(-217,12),new Vector2(64,64),Color.white);icon.sprite=Resources.Load<Sprite>("Gear/icon_"+def.id);icon.raycastTarget=false;
                details[i]=Label(card.transform,"",new Vector2(25,24),new Vector2(435,68),18);
                Button(card.transform,"合成/重铸",new Vector2(-150,-40),new Vector2(112,32),()=>Craft(def.id));
                Button(card.transform,"升级",new Vector2(-24,-40),new Vector2(92,32),()=>Upgrade(def.id));
                Button(card.transform,"装备",new Vector2(84,-40),new Vector2(92,32),()=>Equip(def.id));
                Button(card.transform,"卸下",new Vector2(192,-40),new Vector2(92,32),()=>{if(CanModify(out var why))GearInventory.Unequip(def.slot);else message.text=why;Refresh();});
            }
            message=Label(panel.transform,"",new Vector2(0,-375),new Vector2(1580,65),22);
            Button(panel.transform,"关闭 [ G / Esc ]",new Vector2(0,-430),new Vector2(250,42),Close);panel.SetActive(false);
        }
        public bool Toggle(){if(IsOpen){Close();return true;}if(!CanModify(out var why)){Debug.Log("[GearBench] "+why);return false;}Owner.Player.StopMovement();Owner.Cancel();panel.SetActive(true);message.text="空格短按格挡 / 长按举壳 · Shift 原闪避 · Q 抓钩";Refresh();TimeManager.I.Refresh();return true;}
        public void Close(){if(panel)panel.SetActive(false);TimeManager.I.Refresh();}
        public bool Craft(string id){if(!CanModify(out var why)){message.text=why;return false;}bool ok=GearCrafting.Craft(id,out why);message.text=why;Refresh();return ok;}
        public bool Upgrade(string id){if(!CanModify(out var why)){message.text=why;return false;}bool ok=GearCrafting.Upgrade(id,out why);message.text=why;Refresh();return ok;}
        public void Equip(string id){if(CanModify(out var why))GearInventory.Equip(id);else message.text=why;Refresh();}
        public void Refresh(){if(!slots)return;slots.text="";foreach(GearSlot s in System.Enum.GetValues(typeof(GearSlot))){var it=GearInventory.Equipped(s);slots.text+=GearCrafting.SlotName(s)+": "+(it==null?"空":GearDefs.Get(it.id).name+" Lv"+it.level)+"    ";}for(int i=0;i<15;i++){var d=GearDefs.All[i];var it=GearInventory.Find(d.id);int next=it==null?1:Mathf.Min(3,it.level+1);details[i].text=d.name+"  "+(it==null?"未合成":new string('★',it.level)+" "+d.stat+it.Attr(d.stat).ToString("F0"))+"\n核心 "+GearCrafting.CountCore(d)+"/1 · "+GearCrafting.ElementName(d.element)+"材料 "+GearCrafting.CountAny(d)+"/"+GearDefs.AnyCount(next);}}
        void Update(){if(!Owner){Destroy(gameObject);return;}if(Input.GetKeyDown(KeyCode.G))Toggle();if(IsOpen && Input.GetKeyDown(KeyCode.Escape))Close();if(IsOpen && !CanModify(out _))Close();GrappleButton.interactable=!IsOpen && TapGearCombat.Lv("arm_grapple")>0 && Owner.GrappleRemaining<=0 && !TapGearCombat.Paused;}
        void OnDestroy(){if(Instance==this)Instance=null;if(TimeManager.I)TimeManager.I.Refresh();}
    }
}
