using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace TilesHD {
public static class RepaintMap {
 public const string ScenePath="Assets/Scenes/BiomechanicalCombatScene.unity";
 public const string Output="Docs/GameJamIntegration/tiles_hd/rework1/unity";
 public const string Baseline="Docs/GameJamIntegration/tiles_hd/rework1/baseline";
 [Serializable] public class ComponentRecord { public string path,type,json; }
 [Serializable] public class Cell { public Vector3Int position; public string asset; public Color color; public Matrix4x4 matrix; public int flags; }
 [Serializable] public class Map { public string path; public Vector3 origin, size; public List<Cell> cells=new List<Cell>(); }
 [Serializable] public class Snapshot { public string scene; public List<Map> maps=new List<Map>(); public List<string> objects=new List<string>(); }
 public static string Hierarchy(Transform t) {return t.parent ? Hierarchy(t.parent)+"/"+t.name : t.name;}
 public static Snapshot Read() {
  var s=new Snapshot {scene=ScenePath};
  foreach(var t in UnityEngine.Object.FindObjectsOfType<Transform>(true).OrderBy(t=>Hierarchy(t))) {
   s.objects.Add(Hierarchy(t)+" | "+t.localPosition+" | "+t.localRotation+" | "+t.localScale+" | "+string.Join(",",t.GetComponents<Component>().Select(c=>c?c.GetType().FullName:"MISSING")));
  }
  foreach(var t in UnityEngine.Object.FindObjectsOfType<Tilemap>(true)) {
   var m=new Map {path=Hierarchy(t.transform),origin=t.cellBounds.min,size=t.cellBounds.size};
   foreach(var p in t.cellBounds.allPositionsWithin) if(t.HasTile(p)) m.cells.Add(new Cell {position=p,asset=AssetDatabase.GetAssetPath(t.GetTile(p)),color=t.GetColor(p),matrix=t.GetTransformMatrix(p),flags=(int)t.GetTileFlags(p)});
   s.maps.Add(m);
  }
  return s;
 }
 static Dictionary<Component,string> GameplayState() {
  return UnityEngine.Object.FindObjectsOfType<Component>(true).Where(c=>c && !(c is Tilemap) && !Hierarchy(c.transform).StartsWith("TilesHD_Decor")).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
 }
 static int MaterialAt(Color32 c) {return c.g>c.r+20 && c.g>90 ? 0 : Mathf.Abs(c.r-c.g)<18 && Mathf.Abs(c.g-c.b)<18 && c.r>120 ? 1 : 2;}
 [MenuItem("TapTap/Tiles HD/Repaint")]
 public static void Repaint() {
  Directory.CreateDirectory(Output);EditorSceneManager.OpenScene(ScenePath);
  var before=Read();
  // Never overwrite the original rollback snapshot. Every invocation also gets its own backup.
  File.WriteAllText(Output+"/tilemap-backup-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+".json",JsonUtility.ToJson(before,true));
  if(!File.Exists(Output+"/tilemap-before.json"))File.WriteAllText(Output+"/tilemap-before.json",JsonUtility.ToJson(before,true));
  var state=GameplayState();
  var map=UnityEngine.Object.FindObjectsOfType<Tilemap>().Single(m=>m.name=="Tilemap");
  if(map.GetComponent<TilemapCollider2D>())throw new InvalidOperationException("Refuse to alter a collider-backed Tilemap");
  var originalPositions=before.maps.Single(m=>m.path==Hierarchy(map.transform)).cells.Select(c=>c.position).ToArray();
  // Three holes already present in 1fed22f, two adjacent in the upper-right report region.
  // This Tilemap has NO TilemapCollider2D; fill only these visual gaps, never a gameplay collider.
  var repairedHoles=new[]{new Vector3Int(-3,-12,0),new Vector3Int(16,4,0),new Vector3Int(16,6,0)};
  var positions=originalPositions.Concat(repairedHoles).Distinct().OrderBy(p=>p.y).ThenBy(p=>p.x).ToArray();
  var occupied=new HashSet<Vector3Int>(positions);
  var tiles=Enumerable.Range(0,56).Select(i=>AssetDatabase.LoadAssetAtPath<Tile>("Assets/Art/Tiles/Test/basic_ground_tiles_"+i+".asset")).ToArray();
  if(tiles.Take(40).Any(t=>!t || !t.sprite))throw new InvalidOperationException("Missing source Tile or sprite");
  var tex=new Texture2D(2,2);tex.LoadImage(File.ReadAllBytes(Baseline+"/basic_ground_tiles_original_backup.png"));
  var player=UnityEngine.Object.FindObjectOfType<PlayerController>();var spawn=player.transform.position;
  // Work in the actual isometric cell axes, not screen X/Y. The top-face center is
  // map.GetCellCenterWorld(p)+(0,.5,0); endpoints (0,0)->(9,0)->(9,9) join spawn and door.
  var centers=positions.Select(p=>map.GetCellCenterWorld(p)+new Vector3(0,.5f,0)).ToArray();
  float minX=centers.Min(w=>w.x),maxX=centers.Max(w=>w.x),minY=centers.Min(w=>w.y),maxY=centers.Max(w=>w.y);
  var stonePatches=new[]{new Rect(-12,1,3,3),new Rect(-5,-12,3,2),new Rect(14,3,3,4),new Rect(6,14,2,3)};
  var islands=new[]{new Vector2(-8,6),new Vector2(-12,-2),new Vector2(1,-11),new Vector2(14,10),new Vector2(0,14)};
  Func<Vector2,bool> road=q=>(q.x>=-1.65f&&q.x<=10.65f&&Mathf.Abs(q.y)<=1.65f)||(Mathf.Abs(q.x-9)<=1.65f&&q.y>=-1.65f&&q.y<=10.65f);
  Func<Vector2,Vector3,int> desired=(q,w)=>{
   // Small exposed soil interruption, not a repeated checkerboard.
   if(road(q))return q.x>4.15f&&q.x<4.8f&&q.y<.8f?2:1;
   // Central 40% rectangular arena remains quiet except the requested route.
   bool quiet=w.x>-5.5f&&w.x<5.5f&&w.y>-2.4f&&w.y<4.6f;
   if(quiet)return 2;
   foreach(var r in stonePatches)if(r.Contains(q))return 1;
   for(int n=0;n<islands.Length;n++){
    var d=q-islands[n];float radius=1.25f+.15f*Mathf.Sin(q.x*2+q.y+n);
    if(d.x*d.x/(radius*radius)+d.y*d.y/(radius*radius*.75f)<1)return 0;
   }
   float edge=Mathf.Min(w.x-minX,maxX-w.x,w.y-minY,maxY-w.y);
   float width=.55f+.32f*Mathf.Sin(w.x*.72f+w.y*.9f)+.18f*Mathf.Cos(w.x*1.6f-w.y*.5f);
   if(edge<width)return 0;
   return 2;
  };
  int changed=0;var counts=new Dictionary<int,int>();
  foreach(var p in positions){
   int best=3;float score=float.MaxValue;
   var center=map.GetCellCenterWorld(p);
   var wants=new int[8,8];for(int u=1;u<=7;u++)for(int v=1;v<=7;v++) {float a=u/8f,b=v/8f;wants[u,v]=desired(new Vector2(p.x+.5f-b,p.y+.5f-a),center+map.transform.TransformVector(new Vector3((a-b)*.5f,.75f-(a+b)*.25f,0)));}
   for(int i=0;i<40;i++){
    float cost=0;
    for(int u=1;u<=7;u++)for(int v=1;v<=7;v++){
     float a=u/8f,b=v/8f;int px=Mathf.RoundToInt(64+64*(a-b)),py=Mathf.RoundToInt(32*(a+b));
     int actual=MaterialAt(tex.GetPixel(i%8*128+px,tex.height-1-(i/8*128+py)));
     int want=wants[u,v];if(actual!=want)cost+=want==0||actual==0?1.3f:1;
    }
    // Two grass variants alternate deterministically; otherwise keep best geometric mask.
    if(i==0 && ((p.x+p.y)&1)==0)cost+=.05f;
    if(i==1 && ((p.x+p.y)&1)!=0)cost+=.05f;
    if(cost<score){score=cost;best=i;}
   }
   if(map.GetTile(p)!=tiles[best])changed++;
   map.SetTile(p,tiles[best]);if(!counts.ContainsKey(best))counts[best]=0;counts[best]++;
  }
  UnityEngine.Object.DestroyImmediate(tex);
  // Decoration has no collider, script, light or gameplay interaction. Stable names, not random scattering.
  var root=GameObject.Find("TilesHD_Decor");if(!root)root=new GameObject("TilesHD_Decor");
  var usedNames=new HashSet<string>();
  var renderer=map.GetComponent<TilemapRenderer>();
  Action<string,int,Vector3> prop=(name,index,position)=>{if(!tiles[index]||!tiles[index].sprite)return;usedNames.Add(name);var existing=root.transform.Find(name);var go=existing?existing.gameObject:new GameObject(name);go.transform.SetParent(root.transform,false);go.transform.position=position;var sr=go.GetComponent<SpriteRenderer>();if(!sr)sr=go.AddComponent<SpriteRenderer>();sr.sprite=tiles[index].sprite;sr.sortingLayerID=renderer.sortingLayerID;sr.sortingOrder=renderer.sortingOrder+1;};
  // Station distance along the two cell-axis segments: pairs every seven cells.
  for(int station=0;station<=14;station+=7){
   bool second=station>9;var q=second?new Vector3Int(9,station-9,0):new Vector3Int(station,0,0);
   var normal=second?Vector3Int.right:Vector3Int.up;
   prop("PathLamp_L_"+(station/7),40,map.GetCellCenterWorld(q+normal*2)+Vector3.up*.5f);
   prop("PathLamp_R_"+(station/7),41,map.GetCellCenterWorld(q-normal*2)+Vector3.up*.5f);
  }
  // Door's runtime authored location is (0,5.25); ornaments flank it, never move its owner.
  prop("DoorLamp_L",42,new Vector3(-1.15f,5.25f,0));prop("DoorLamp_R",43,new Vector3(1.15f,5.25f,0));
  var pipeGroups=new[]{new Vector3Int(-13,-1,0),new Vector3Int(12,12,0),new Vector3Int(-1,-12,0)};
  for(int group=0;group<pipeGroups.Length;group++)for(int i=0;i<3;i++){
   var q=pipeGroups[group]+(group==1?Vector3Int.down:Vector3Int.right)*i;
   if(!occupied.Contains(q))throw new Exception("Pipe group outside footprint "+q);
   prop("EdgePipe_G"+group+"_"+i,48+(group+i)%4,map.GetCellCenterWorld(q)+Vector3.up*.5f);
  }
  for(int i=root.transform.childCount-1;i>=0;i--)if(!usedNames.Contains(root.transform.GetChild(i).name))UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
  foreach(var pair in state)if(!pair.Key || EditorJsonUtility.ToJson(pair.Key)!=pair.Value)throw new InvalidOperationException("Gameplay component changed: "+pair.Key);
  // Explicit sole camera exception: apply AFTER the complete unchanged-component check.
  // Other camera parameters are not excluded and will also be independently diffed on disk.
  var cam=Camera.main;if(!cam)throw new Exception("Missing main camera");
  var originalBackground=cam.backgroundColor;
  cam.backgroundColor=new Color32(7,18,27,255);
  File.WriteAllLines(Output+"/component-before.jsonl",state.Select(k=>JsonUtility.ToJson(new ComponentRecord{path=Hierarchy(k.Key.transform),type=k.Key.GetType().FullName,json=k.Value})));
  File.WriteAllLines(Output+"/component-after.jsonl",state.Select(k=>JsonUtility.ToJson(new ComponentRecord{path=Hierarchy(k.Key.transform),type=k.Key.GetType().FullName,json=EditorJsonUtility.ToJson(k.Key)})));
  File.WriteAllText(Output+"/camera-exception.txt","Authorized Main Camera.backgroundColor only: "+originalBackground+" -> "+cam.backgroundColor);
  var after=Read();if(after.maps.Single(m=>m.path==Hierarchy(map.transform)).cells.Count!=positions.Length)throw new InvalidOperationException("Footprint changed");
  EditorSceneManager.MarkSceneDirty(map.gameObject.scene);EditorSceneManager.SaveScene(map.gameObject.scene);
  File.WriteAllText(Output+"/tilemap-after.json",JsonUtility.ToJson(after,true));
  File.WriteAllText(Output+"/repaint-result.txt","PASS unchanged existing component serialization="+state.Count+"; footprint="+positions.Length+"; changed="+changed+"\n"+string.Join("\n",counts.Select(k=>"tile "+k.Key+" = "+k.Value)));
  Debug.Log("[TilesHD] REPAINT PASS changed="+changed+" invariants="+state.Count);
 }
 [MenuItem("TapTap/Tiles HD/Restore original cells")]
 public static void RestoreOriginal() {
  // Full immutable 1fed22f scene restores its decor and camera as well as tile cells.
  // This is an explicit rollback menu, never called on load or in the build pipeline.
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  File.Copy(Baseline+"/BiomechanicalCombatScene.unity",ScenePath,true);
  AssetDatabase.ImportAsset(ScenePath,ImportAssetOptions.ForceUpdate);
  EditorSceneManager.OpenScene(ScenePath);
 }
 [MenuItem("TapTap/Tiles HD/Audit")]
 public static void Audit() {
  Directory.CreateDirectory(Output);
  EditorSceneManager.OpenScene(ScenePath);
  var s=Read();
  File.WriteAllText(Output+"/audit.json",JsonUtility.ToJson(s,true));
  if(!File.Exists(Output+"/tilemap-before.json"))File.WriteAllText(Output+"/tilemap-before.json",JsonUtility.ToJson(s,true));
  foreach(var m in s.maps) Debug.Log("[TilesHD] MAP "+m.path+" count="+m.cells.Count+" origin="+m.origin+" size="+m.size);
  Debug.Log("[TilesHD] AUDIT PASS");
 }
}
}
