using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using BiomechanicalCrafting;
using GameJam.Gear;

/// <summary>Production additive scenes and UI raycast/callback probes, not pause-flag substitutes.
/// Runs only inside TapSelfTest's native save snapshot/supervisor envelope.</summary>
public static class TapP6Tests
{
    static IEnumerator Wait(Func<bool> done,float timeout=20){float end=Time.realtimeSinceStartup+timeout;while(!done() && Time.realtimeSinceStartup<end)yield return null;}
    static IEnumerator Frame(string name){yield return new WaitForEndOfFrame();var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(TapSelfTest.OutputDirectory,name+".png"),t.EncodeToPNG());UnityEngine.Object.Destroy(t);}
    static bool Pointer(Button button)
    {
        if(!button || !button.IsInteractable() || !EventSystem.current)return false;
        Canvas.ForceUpdateCanvases();var rt=(RectTransform)button.transform;
        var p=RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(rt.rect.center));
        if(p.x<0 || p.y<0 || p.x>=Screen.width || p.y>=Screen.height)return false;
        var data=new PointerEventData(EventSystem.current){position=p,button=PointerEventData.InputButton.Left};
        var results=new List<RaycastResult>();EventSystem.current.RaycastAll(data,results);
        if(results.Count==0 || ExecuteEvents.GetEventHandler<IPointerClickHandler>(results[0].gameObject)!=button.gameObject){Debug.Log("[P6] raycast blocked "+button.name+" top="+(results.Count>0?results[0].gameObject.name:"none"));return false;}
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);return true;
    }
    public static IEnumerator Run(Action<bool,string> check)
    {
        var p=PlayerController.Instance;var encounter=BossEncounterController.Instance;
        var camera=CameraController.Instance;bool cameraEnabled=camera.enabled;
        var boss=encounter.StartEncounter();boss.AutoAttack=false;yield return null;
        boss.Entity.TakeDamage(10,0,p.transform.position);float hp=boss.Entity.currentHp;
        check(boss.ForceMove(GameJam.Boss.BossMoves.P1_Claw3),"P6 real boss windup prepared");
        float elapsed=boss.Elapsed;int phase=boss.Phase;int impact=boss.NextImpact;
        check(GameplaySceneTransition.TryOpen("Mix",p),"P6 production Mix additive request accepted");
        yield return Wait(()=>!GameplaySceneTransition.IsBusy && SceneManager.GetSceneByName("Mix").isLoaded);
        var mix=UnityEngine.Object.FindObjectOfType<ShellInventoryController>();
        check(mix && SceneManager.GetSceneByName("Mix").isLoaded,"P6 actual Mix scene and production controller loaded");
        yield return new WaitForSecondsRealtime(.4f);
        check(boss.Entity.currentHp==hp && boss.Phase==phase && boss.Elapsed==elapsed && boss.NextImpact==impact,"P6 real Mix preserves Boss HP phase windup impact index");
        check(PlayerSessionData.IsCombatInputBlocked && !camera.enabled && !GameJam.HUD.I.GetComponent<Canvas>().enabled,"P6 Mix input lock camera and HUD hidden");
        check(!TapGearUI.Instance.GetComponent<Canvas>().enabled,"P6 gear HUD hidden over actual Mix");
        yield return Frame("p6-mix");
        check(mix && Pointer(mix.btnReturnToCombat),"P6 Mix return raycast and real pointer callback");
        yield return Wait(()=>!GameplaySceneTransition.IsBusy && !SceneManager.GetSceneByName("Mix").isLoaded);
        check(!SceneManager.GetSceneByName("Mix").isLoaded && !PlayerSessionData.IsCombatInputBlocked && camera.enabled==cameraEnabled && GameJam.HUD.I.GetComponent<Canvas>().enabled,"P6 Mix unloaded camera input HUD restored");
        check(boss && boss.Entity.currentHp==hp && boss.Phase==phase,"P6 same Boss entity HP phase after Mix return");
        float resume=boss.Elapsed;yield return null;yield return null;
        check(boss.Elapsed>resume,"P6 Boss windup resumes after real Mix return");
        var door=UnityEngine.Object.FindObjectOfType<GatherDoorAuraBehavior>();door.EnterGatheringScene(p);
        check(!GameplaySceneTransition.IsBusy && !SceneManager.GetSceneByName("Collect").isLoaded,"P6 production Collect door remains forbidden during Boss encounter");
        encounter.CancelEncounter();yield return null;p.ClearAttackTarget();p.ExitCombat();
        // Separate door fixture; retain default door and production entry, not direct TryOpen Collect.
        var secondGo=new GameObject("P6 independent gathering door");secondGo.transform.position=door.transform.position+Vector3.right*4;
        var second=secondGo.AddComponent<GatherDoorAuraBehavior>();
        yield return null;
        GreenhouseDayState firstState=null;int firstHarvest=0;
        foreach(var d in new[]{door,second})
        {
            // Author only trial completion fixture. RefreshDailyState is still production-owned.
            typeof(GatherDoorAuraBehavior).GetMethod("RefreshDailyState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(d,null);
            d.state=GatherDoorAuraBehavior.DoorState.Cleared;d.EnterGatheringScene(p);
            yield return Wait(()=>!GameplaySceneTransition.IsBusy && SceneManager.GetSceneByName("Collect").isLoaded);
            var collect=CollectSceneController.Instance;
            check(collect && SceneManager.GetSceneByName("Collect").isLoaded,"P6 actual Collect scene loaded via door "+d.DoorId);
            check(PlayerSessionData.IsDoorUsedToday(d.DoorId) && PlayerSessionData.activeGreenhouseDoorId==d.DoorId,"P6 production Collect Start commits this door day");
            var state=PlayerSessionData.greenhouse;
            if(firstState!=null)check(!ReferenceEquals(state,firstState) && state.nodes.Count>0 && state.harvestedCount==0 && firstState.harvestedCount==firstHarvest,"P6 second door independent resource nodes and harvest count");
            var nodes=UnityEngine.Object.FindObjectsOfType<HarvestItemNode>();int before=state.harvestedCount;
            check(nodes.Length>0,"P6 greenhouse has real harvest nodes");if(nodes.Length>0)nodes[0].TryHarvest();
            yield return new WaitForSecondsRealtime(.3f);
            check(state.harvestedCount==before+1,"P6 production harvest increments one resource");
            if(firstState==null){firstState=state;firstHarvest=state.harvestedCount;}
            check(!TapGearUI.Instance.GetComponent<Canvas>().enabled,"P6 gear HUD hidden over actual Collect");
            yield return Frame(d==door?"p6-collect-a":"p6-collect-b");
            check(collect && Pointer(collect.btnReturnToCombat),"P6 Collect return raycast and real callback");
            yield return Wait(()=>!GameplaySceneTransition.IsBusy && !SceneManager.GetSceneByName("Collect").isLoaded);
            check(!PlayerSessionData.IsCombatInputBlocked && camera.enabled==cameraEnabled && GameJam.HUD.I.GetComponent<Canvas>().enabled,"P6 Collect return camera input HUD restored");
            d.EnterGatheringScene(p);check(!GameplaySceneTransition.IsBusy && !SceneManager.GetSceneByName("Collect").isLoaded && !PlayerSessionData.TryBeginDoorVisit(d.DoorId),"P6 same door cannot reopen on same day");
        }
        PlayerSessionData.LoadBackpackFromStorage();
        var savedA=PlayerSessionData.greenhouseDoors.Find(d=>d.doorId==door.DoorId);
        var savedB=PlayerSessionData.greenhouseDoors.Find(d=>d.doorId==second.DoorId);
        check(savedA!=null && savedB!=null && savedA.greenhouse.harvestedCount==1 && savedB.greenhouse.harvestedCount==1 && !ReferenceEquals(savedA.greenhouse,savedB.greenhouse) && PlayerSessionData.IsDoorUsedToday(door.DoorId) && PlayerSessionData.IsDoorUsedToday(second.DoorId),"P6 production storage reload retains separate door resources and daily use");
        int day=PlayerSessionData.greenhouse.day;PlayerSessionData.greenhouse=new GreenhouseDayState{day=day+1};
        check(PlayerSessionData.TryBeginDoorVisit(door.DoorId) && PlayerSessionData.greenhouse.day==day+1 && PlayerSessionData.greenhouse.harvestedCount==0,"P6 next-day first door gets fresh independent state");
        check(!PlayerSessionData.IsDoorUsedToday(second.DoorId),"P6 next-day second door independently available");
        UnityEngine.Object.Destroy(secondGo);p.StopMovement();p.ClearAttackTarget();p.ExitCombat();yield return Wait(()=>p.CanBeginUltimate);
        // ReturnRoutine clears the overlay lock at end of frame; wait for the HUD's own Update.
        yield return Wait(()=>p.Ultimate.UI.GetComponent<Canvas>().enabled);yield return null;
        check(Pointer(p.Ultimate.UI.SwitchButton) && p.Ultimate.UI.PickerOpen,"P6 mobile ult switch actual hit target callback opens picker");
        if(p.Ultimate.UI.PickerOpen)p.Ultimate.UI.SwitchButton.onClick.Invoke();
        check(Pointer(TapGearUI.Instance.OpenButton) && TapGearUI.IsOpen,"P6 gear button actual hit target callback opens bench");
        TapGearUI.Instance.Close();yield return null;
        // Exercise the new attack-button hit target with real equipment and a registered normal enemy.
        GearInventory.ResetAll();GearInventory.Put(new GearItem{id="arm_grapple",level=3,p=30,e=30,g=30,w=30});GearInventory.Equip("arm_grapple");
        var enemy=EnemyController.CreateEnemyInstance(p.transform.position+Vector3.right*100,"P6 pointer target");enemy.autoRespawn=false;enemy.ExternalAI=(e,dt)=>{};
        yield return null;yield return Wait(()=>p.CanBeginUltimate);p.Gear.Tick(8);yield return null;
        enemy.transform.position=p.transform.position+Vector3.right*3*p.Gear.Unit;Vector3 targetBefore=enemy.transform.position;
        check(Pointer(TapGearUI.Instance.GrappleButton) && p.Gear.Busy,"P6 grapple screen hit invokes real equipment action");
        p.Gear.Tick(.2f);check(Vector2.Distance(p.transform.position,enemy.transform.position)<Vector2.Distance(p.transform.position,targetBefore),"P6 grapple pointer callback pulls actual enemy");
        p.Gear.Cancel();UnityEngine.Object.Destroy(enemy.gameObject);yield return null;p.ClearAttackTarget();p.ExitCombat();yield return Wait(()=>p.CanBeginUltimate);GearInventory.ResetAll();
        p.Ultimate.Cancel(true);check(p.Ultimate.Select(GameJam.HeroUlt.UltKind.EchoShell),"P6 pointer echo choice prepared");p.Ultimate.AddCharge(100);yield return null;
        check(Pointer(p.Ultimate.UI.ReleaseButton) && p.Ultimate.Echo && p.UltimateLocked,"P6 ultimate screen hit invokes real release lock");
        p.Ultimate.Cancel(true);yield return null;
        var f=new GameObject("P6 ordinary furniture").AddComponent<FurnitureObject>();f.transform.position=p.transform.position+Vector3.left*2;yield return null;
        float durability=f.currentDurability;int notices=0;f.onHitTaken+=(damage,attacker)=>notices++;
        f.TakeDamage(7);check(f.currentDurability==durability-7 && notices==1,"P6 ordinary furniture damage and observer unchanged");
        var cell=f.occupiedCell;f.PickUp(p);check(f.currentState==FurnitureObject.FurnitureState.Held && !IsometricPathfinder.DynamicBlockedCells.Contains(cell),"P6 ordinary furniture pickup releases grid");
        f.PutDown(p.transform.position+Vector3.left*2);check(f.currentState==FurnitureObject.FurnitureState.Placed && IsometricPathfinder.DynamicBlockedCells.Contains(f.occupiedCell),"P6 ordinary furniture putdown restores grid");
        f.Kick(Vector3.left);yield return Wait(()=>f.currentState==FurnitureObject.FurnitureState.Placed,3);
        check(f.currentState==FurnitureObject.FurnitureState.Placed && IsometricPathfinder.DynamicBlockedCells.Contains(f.occupiedCell),"P6 ordinary kick coroutine settles and restores grid");
        f.isIndestructible=true;durability=f.currentDurability;f.TakeDamage(999);check(f.currentDurability==durability,"P6 permanent furniture remains indestructible");
        UnityEngine.Object.Destroy(f.gameObject);yield return new WaitForSecondsRealtime(2);
        check(TapGearUI.Instance.GetComponent<Canvas>().enabled,"P6 gear HUD restored after overlay and bench close");
        yield return Frame("p6-restored-hud");
    }
}
