using UnityEngine;
using GameJam.Boss;

/// <summary>P2 diagnostic combat presentation; P3 cinematic layer is intentionally separate.</summary>
public sealed class BossCombatDisplay : MonoBehaviour
{
    public BossBrain Brain;
    LineRenderer line;
    Material material;
    void Awake() { line=gameObject.AddComponent<LineRenderer>(); material=new Material(Shader.Find("Sprites/Default")); line.sharedMaterial=material; line.useWorldSpace=true; line.widthMultiplier=.025f; line.sortingOrder=20; }
    void OnDestroy() { if(material)Destroy(material); }
    void LateUpdate()
    {
        // EnemyController.Start enables its default bar after encounter initialization.
        var smallBar=transform.Find("HPBar");if(smallBar)smallBar.gameObject.SetActive(false);
        if(!Brain || !Brain.Entity || !Brain.Entity.IsAlive || PlayerSessionData.IsCombatInputBlocked || Brain.CurrentMove==null) { line.positionCount=0; return; }
        line.startColor=line.endColor=Brain.Posture==3?Color.cyan:Brain.Yellow?Color.yellow:Color.red;
        var points=Brain.Shape(Brain.NextImpact).Outline(); line.positionCount=points.Length; line.SetPositions(points);
    }
    void OnGUI()
    {
        if(!Brain || !Brain.Entity || !Brain.Entity.IsAlive || PlayerSessionData.IsCombatInputBlocked)return;
        float w=Screen.width*.6f,x=(Screen.width-w)*.5f,y=30;
        GUI.Box(new Rect(x,y,w,58),"无名夜王  Phase "+Brain.Phase+"   架势 "+Brain.Posture+" / 3");
        GUI.color=new Color(.65f,.08f,.18f); GUI.DrawTexture(new Rect(x+5,y+29,(w-10)*Mathf.Clamp01(Brain.Entity.currentHp/Brain.Entity.maxHp),16),Texture2D.whiteTexture);
        GUI.color=Color.white; foreach(float mark in new[]{.7f,.5f,.3f})GUI.DrawTexture(new Rect(x+5+(w-10)*mark,y+27,2,20),Texture2D.whiteTexture);
    }
}
