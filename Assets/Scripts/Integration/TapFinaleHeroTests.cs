using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using GameJam;
using GameJam.Gear;

/// <summary>Call from TapSelfTest after encounters/overlays are closed. Outer harness owns raw save isolation.
/// No Unity launch or automatic execution. Uses production HP, gear Tick and damage entry points.</summary>
public static class TapFinaleHeroTests
{
    static bool Near(float a, float b) => Mathf.Abs(a-b)<.01f;
    static void Equip(string id, int level)
    {
        GearInventory.ResetAll();
        GearInventory.Put(new GearItem { id=id, level=level, p=30, e=30, g=30, w=30 });
        GearInventory.Equip(id);
    }
    static Rect ScreenRect(RectTransform rt)
    {
        var corners=new Vector3[4]; rt.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
    }
    public static IEnumerator Run(Action<bool,string> check)
    {
        var p=PlayerController.Instance;
        check(p && p.Gear && HUD.I,"FINALE hero production owners exist");
        if(!p || !p.Gear || !HUD.I)yield break;
        var g=p.Gear; var bar=HUD.I.GetComponentInChildren<HeroHealthDisplay>(true);
        check(bar,"FINALE hero HUD installed"); if(!bar)yield break;
        float savedHp=p.currentHp; string gear=GearInventory.Snapshot(); bool had=PlayerPrefs.HasKey("GJ_GEAR_STATE");
        p.StopMovement();p.ClearAttackTarget();p.ExitCombat();
        float deadline=Time.realtimeSinceStartup+5;
        while((!p.CanBeginUltimate || p.IsInvulnerable || TapGearCombat.Paused) && Time.realtimeSinceStartup<deadline)yield return null;
        check(TapGearUI.CanModify(out _) && !p.IsInvulnerable && !TapGearCombat.Paused,"FINALE hero fixture ready without bypassing action guards");
        if(!TapGearUI.CanModify(out _) || p.IsInvulnerable || TapGearCombat.Paused)yield break;
        try
        {
            GearInventory.ResetAll();
            check(Near(p.maxHp,100),"FINALE hero remains 100 HP");
            check(p.transform.Find("Player_HPBar"),"FINALE original overhead HP bar retained");
            p.currentHp=p.maxHp;bar.Tick(0);p.TakeDamage(20,null,false);bar.Tick(0);
            check(Near(p.currentHp,80) && bar.Caption=="80 / 100","FINALE real damage displays 80 / 100");
            check(bar.GhostFraction>bar.FillFraction && bar.DamageFlash>0,"FINALE damage ghost and flash start");
            bar.Tick(1);check(Near(bar.GhostFraction,.8f) && Near(bar.DamageFlash,0),"FINALE ghost converges and flash expires");
            p.currentHp=30;bar.Tick(0);check(bar.LowHealth,"FINALE 30 percent inclusive pulse");
            var fill=bar.transform.Find("Fill").GetComponent<Image>();var color=fill.color;
            bar.Tick(.17f);check(fill.color!=color,"FINALE low health color actually pulses");
            p.currentHp=31;bar.Tick(0);check(!bar.LowHealth,"FINALE above 30 percent no pulse");
            p.Heal(100);bar.Tick(0);check(bar.Caption=="100 / 100" && Near(bar.GhostFraction,1),"FINALE heal clamp and ghost catchup");
            foreach(var graphic in bar.GetComponentsInChildren<Graphic>())check(!graphic.raycastTarget,"FINALE HP nonblocking "+graphic.name);
            Canvas.ForceUpdateCanvases();var bounds=ScreenRect((RectTransform)bar.transform);
            foreach(var button in UnityEngine.Object.FindObjectsOfType<Selectable>())
                if(button.gameObject.activeInHierarchy && button.GetComponentInParent<Canvas>() && button.GetComponentInParent<Canvas>().enabled && button.GetComponentInParent<Canvas>().renderMode==RenderMode.ScreenSpaceOverlay)
                    check(!bounds.Overlaps(ScreenRect((RectTransform)button.transform)),"FINALE HP avoids visible control "+button.name);
            // The real damage above may still own hurt/action recovery; do not bypass it.
            deadline=Time.realtimeSinceStartup+5;
            while((!p.CanBeginUltimate || p.IsInvulnerable || TapGearCombat.Paused) && Time.realtimeSinceStartup<deadline)yield return null;
            p.ClearAttackTarget();p.ExitCombat();
            for(int lv=1;lv<=3;lv++)
            {
                Equip("core_fungi",lv);check(TapGearCombat.Lv("core_fungi")==lv,"FINALE fungi equipped Lv"+lv);
                p.currentHp=40;g.Tick(6);g.Tick(lv==1?3.99f:2.99f);
                check(Near(p.currentHp,40),"FINALE fungi no early tick Lv"+lv);g.Tick(.02f);
                check(Near(p.currentHp,40+new[]{12,16,20}[lv-1]),"FINALE fungi exact tick Lv"+lv);
                p.currentHp=40;g.Executed();check(Near(p.currentHp,lv==3?60:40),"FINALE fungi execution level gate Lv"+lv);
            }
            Equip("leg_root",3);p.currentHp=40;
            // Settle the position anchor, then start a fresh equipment clock.
            g.Tick(.001f);Equip("leg_root",3);g.Tick(.6f);
            check(g.Rooted && Near(p.currentHp,40),"FINALE root entered without premature heal");
            g.Tick(1.99f);check(Near(p.currentHp,40),"FINALE root waits two rooted seconds");
            g.Tick(.02f);check(Near(p.currentHp,50),"FINALE root heals exactly 10 per two seconds");
            g.Tick(2);check(Near(p.currentHp,60),"FINALE root repeat exact 10");
            Equip("shell_float",3);p.currentHp=10;int uses=g.FloatUses;
            p.TakeDamage(1000,null,false);
            check(p.IsAlive && Near(p.currentHp,25) && g.FloatUses==uses-1 && g.Bubble,"FINALE lethal float absorbs and heals exactly 15");
        }
        finally
        {
            g.Cancel();GearInventory.Restore(gear);
            if(!had){PlayerPrefs.DeleteKey("GJ_GEAR_STATE");PlayerPrefs.Save();GearInventory.Reload();}
            p.currentHp=savedHp;bar.Tick(0);
        }
        yield return null;
    }

    /// <summary>Real framebuffer full/low HP pair. Invoke only in a safe stationary production scene.
    /// Does not fabricate frames, change maxHP, remove objects, or capture before rendered UI updates.</summary>
    public static IEnumerator CaptureHpEvidence(string directory)
    {
        var p=PlayerController.Instance;var bar=HUD.I?HUD.I.GetComponentInChildren<HeroHealthDisplay>(true):null;
        if(!p || !bar)throw new InvalidOperationException("Hero HUD not ready");
        Directory.CreateDirectory(directory);float hp=p.currentHp;
        try
        {
            foreach(int value in new[]{100,25})
            {
                p.currentHp=value;bar.Tick(2);yield return null;yield return new WaitForEndOfFrame();
                var texture=ScreenCapture.CaptureScreenshotAsTexture();
                try { File.WriteAllBytes(Path.Combine(directory,value==100?"finale-hero-full.png":"finale-hero-low.png"),texture.EncodeToPNG()); }
                finally { UnityEngine.Object.Destroy(texture); }
                yield return null;
            }
        }
        finally { p.currentHp=hp;bar.Tick(0); }
    }
}
