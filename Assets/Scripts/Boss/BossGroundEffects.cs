using System.Collections.Generic;
using UnityEngine;
using GameJam.Boss;

/// <summary>Encounter-owned world hazards, with one global electric tick across overlapping patches.
/// No coroutine/Time.time expiry: build and overlay pauses preserve the remaining lifetime.</summary>
public sealed class BossGroundEffects : MonoBehaviour
{
    sealed class Patch { public BossAttackShape shape; public BossElement element; public float remaining=6; public LineRenderer visual; }
    readonly List<Patch> patches=new List<Patch>();
    BossBrain brain; float electricTimer,wetRemaining; Material material;
    public int Count=>patches.Count;
    public bool Wet=>wetRemaining>0;
    public static float PlayerMoveMultiplier(PlayerController player)
    {
        var encounter=BossEncounterController.Instance;
        if(!encounter || !encounter.Boss)return 1;
        var effects=encounter.Boss.GetComponent<BossGroundEffects>();
        if(!effects)return 1;
        foreach(var p in effects.patches) if((p.element==BossElement.Water || p.element==BossElement.Dark) && p.shape.Contains(player.transform.position))return .55f;
        return 1;
    }
    void Awake() { brain=GetComponent<BossBrain>(); material=new Material(Shader.Find("Sprites/Default")); }
    public void Add(BossAttackShape shape,BossElement element)
    {
        var go=new GameObject("BossGround_"+element); go.transform.SetParent(transform,false);
        var line=go.AddComponent<LineRenderer>(); line.useWorldSpace=true; line.sharedMaterial=material; line.widthMultiplier=.04f;line.sortingOrder=-8;
        line.startColor=line.endColor=element==BossElement.Water?Color.cyan:element==BossElement.Electric?new Color(.65f,.3f,1):Color.gray;
        var points=shape.Outline();line.positionCount=points.Length;line.SetPositions(points);
        patches.Add(new Patch{shape=shape,element=element,visual=line});
    }
    void Update()
    {
        if(!brain || !brain.Entity || !brain.Entity.IsAlive) { Clear();return; }
        foreach(var p in patches) if(p.visual)p.visual.enabled=!PlayerSessionData.IsCombatInputBlocked;
        if(EnemyController.CombatWorldPaused)return;
        Tick(Time.deltaTime);
    }
    public void Tick(float dt)
    {
        if(EnemyController.CombatWorldPaused || dt<=0)return;
        var player=PlayerController.Instance; bool water=false,electric=false;
        for(int i=patches.Count-1;i>=0;i--) {
            var p=patches[i];p.remaining-=dt;
            if(p.remaining<=0){Destroy(p.visual.gameObject);patches.RemoveAt(i);continue;}
            p.visual.enabled=!PlayerSessionData.IsCombatInputBlocked;
            if(player && p.shape.Contains(player.transform.position)){water|=p.element==BossElement.Water;electric|=p.element==BossElement.Electric;}
        }
        wetRemaining=water?1:Mathf.Max(0,wetRemaining-dt);
        electricTimer-=dt;
        if(player && electric && electricTimer<=0) { electricTimer=1;player.TakeDamage(player.maxHp*(Wet?.4f:.2f),brain.Entity,false); }
    }
    public void Clear() { foreach(var p in patches)if(p.visual)Destroy(p.visual.gameObject);patches.Clear();wetRemaining=0; }
    void OnDestroy() { Clear();if(material)Destroy(material); }
}
