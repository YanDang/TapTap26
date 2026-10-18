using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using BiomechanicalCrafting;
using GameJam.Gear;

// Batch-only behavioral tests. Preserve raw strings/existence and session references.
public static class TapGearCraftingTests
{
    public static void Run()
    {
        const string gear="GJ_GEAR_STATE", backpack="TAP_SHELL_BACKPACK_DATA";
        bool hg=PlayerPrefs.HasKey(gear), hb=PlayerPrefs.HasKey(backpack);
        string gs=PlayerPrefs.GetString(gear), bs=PlayerPrefs.GetString(backpack);
        var slots=PlayerSessionData.shellSlotItems;
        var raw=TapPrefsSnapshot.Capture();
        int passed=0, exitCode=0;
        try {
            PlayerSessionData.EnsureInitialized();
            var type=typeof(PlayerController).Assembly.GetType("GameJam.Gear.GearCrafting");
            Check(type!=null,"crafting behavior entry must exist");
            var inv=typeof(PlayerController).Assembly.GetType("GameJam.Gear.GearInventory");
            var craft=type.GetMethod("Craft");
            PlayerSessionData.shellSlotItems=new BiomechanicalMaterial[32];
            PlayerPrefs.SetString(gear,"{\"owned\":[],\"equipped\":[null,null,null,null]}");
            inv.GetMethod("Restore").Invoke(null,new object[]{PlayerPrefs.GetString(gear)});
            var args=new object[]{"shell_mobile",null};
            string before=PlayerPrefs.GetString(gear);
            Check(!(bool)craft.Invoke(null,args),"empty warehouse refuses craft");
            Check(PlayerPrefs.GetString(gear)==before,"insufficient materials preserve gear");passed+=2;
            var core=BiomechanicalMaterialDatabase.GetById("mat_wood_coral_plate").Clone();
            core.rootSlotId=0;core.occupiedSlotIds.AddRange(new[]{0,1,8,9});
            foreach(int i in core.occupiedSlotIds)PlayerSessionData.shellSlotItems[i]=core;
            foreach(int i in new[]{2,3}) { var m=BiomechanicalMaterialDatabase.GetById("mat_wood_spring_fascia").Clone();m.rootSlotId=i;m.occupiedSlotIds.Add(i);PlayerSessionData.shellSlotItems[i]=m; }
            PlayerSessionData.SaveBackpackToStorage();
            string materialBefore=PlayerPrefs.GetString(backpack);
            type.GetField("FailNextSpendForTest").SetValue(null,true);
            Check(!(bool)craft.Invoke(null,args),"injected failed spend refuses craft");
            Check(PlayerPrefs.GetString(gear)==before && PlayerPrefs.GetString(backpack)==materialBefore,"failed spend raw save atomicity");
            Check(ReferenceEquals(PlayerSessionData.shellSlotItems[0],core),"failed spend preserves material identity");passed+=3;
            // A UI listener must not turn a craft into a free item by interrupting payment.
            Action brokenListener=()=>{throw new InvalidOperationException("test listener failure");};
            var changed=inv.GetEvent("Changed"); changed.AddEventHandler(null,brokenListener);
            bool accepted=false;
            try { accepted=(bool)craft.Invoke(null,args); } catch(TargetInvocationException) { }
            finally { changed.RemoveEventHandler(null,brokenListener); }
            Check((accepted && PlayerSessionData.shellSlotItems.All(x=>x==null)) || (!accepted && PlayerPrefs.GetString(gear)==before && PlayerPrefs.GetString(backpack)==materialBefore),"listener exception cannot grant unpaid gear");passed++;
            if(accepted) {
                inv.GetMethod("Restore").Invoke(null,new object[]{before});
                PlayerSessionData.shellSlotItems=new BiomechanicalMaterial[32];
                foreach(int i in core.occupiedSlotIds)PlayerSessionData.shellSlotItems[i]=core;
                foreach(int i in new[]{2,3}) {var m=BiomechanicalMaterialDatabase.GetById("mat_wood_spring_fascia").Clone();m.rootSlotId=i;m.occupiedSlotIds.Add(i);PlayerSessionData.shellSlotItems[i]=m;}
                PlayerSessionData.SaveBackpackToStorage();
            }
            Check((bool)craft.Invoke(null,args),"real warehouse payment creates equipment");
            Check(PlayerSessionData.shellSlotItems.All(x=>x==null),"multi-cell core charged once and all occupied cells removed");
            var item=inv.GetMethod("Find").Invoke(null,new object[]{"shell_mobile"});
            Check(item!=null && (int)item.GetType().GetField("level").GetValue(item)==1,"crafted level one persisted");passed+=3;
            foreach(var def in GearDefs.All) {
                GearInventory.ResetAll();
                for(int level=1;level<=3;level++) {
                    PlayerSessionData.shellSlotItems=new BiomechanicalMaterial[32];
                    var source=BiomechanicalMaterialDatabase.GetById(def.coreMaterialId);
                    Check(source!=null && source.element==def.element.ToString(),def.id+" real material id/element");
                    // Multiple distinct instances of the same ID are distinct inventory items.
                    for(int i=0;i<=GearDefs.AnyCount(level);i++) {
                        var material=source.Clone();
                        int root=source.isMajor?(i/4)*16+(i%4)*2:i;
                        material.rootSlotId=root;
                        material.occupiedSlotIds.AddRange(source.isMajor?new[]{root,root+1,root+8,root+9}:new[]{root});
                        foreach(int cell in material.occupiedSlotIds)PlayerSessionData.shellSlotItems[cell]=material;
                    }
                    bool ok=level==1?GearCrafting.Craft(def.id,out _):GearCrafting.Upgrade(def.id,out _);
                    Check(ok && GearInventory.Find(def.id).level==level,def.id+" craft/upgrade level "+level);
                    Check(PlayerSessionData.shellSlotItems.All(x=>x==null),def.id+" exact instance payment "+level);
                    var got=GearInventory.Find(def.id);
                    Check(got.p==source.p && got.e==source.e && got.g==source.g && got.w==source.w,def.id+" actual material averages "+level);
                    passed+=4;
                }
                string cap=GearInventory.Snapshot();
                Check(!GearCrafting.Upgrade(def.id,out _) && GearInventory.Snapshot()==cap,def.id+" max upgrade no mutation");passed++;
            }
            // An originally absent equipment key must remain absent after failure.
            GearInventory.ResetAll(); PlayerPrefs.DeleteKey(gear); GearInventory.Restore("");PlayerPrefs.DeleteKey(gear);
            PlayerSessionData.shellSlotItems=new BiomechanicalMaterial[32];
            for(int i=0;i<3;i++)PlayerSessionData.shellSlotItems[i]=BiomechanicalMaterialDatabase.GetById("mat_wood_spring_fascia").Clone();
            GearCrafting.FailNextSpendForTest=true;
            Check(!GearCrafting.Craft("arm_spring",out _) && !PlayerPrefs.HasKey(gear),"failed spend preserves originally absent equipment key");passed++;
            Debug.Log("[P5 crafting tests] PASS "+passed);
        } catch(Exception e) { Debug.LogError("[P5 crafting tests] FAIL "+e);exitCode=1; }
        finally {
            PlayerSessionData.shellSlotItems=slots;
            if(hg)PlayerPrefs.SetString(gear,gs);else PlayerPrefs.DeleteKey(gear);
            if(hb)PlayerPrefs.SetString(backpack,bs);else PlayerPrefs.DeleteKey(backpack);
            PlayerPrefs.Save();
            raw.Restore();
            Debug.Log("[P5 crafting tests] raw namespace restored="+raw.Matches(TapPrefsSnapshot.Capture()));
        }
        UnityEditor.EditorApplication.Exit(exitCode);
    }
    static void Check(bool condition,string label){if(!condition)throw new Exception(label);Debug.Log("[P5] PASS "+label);}
}
