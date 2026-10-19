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
 public const string Output="Docs/GameJamIntegration/tiles_hd/unity";
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
  var positions=before.maps.Single(m=>m.path==Hierarchy(map.transform)).cells.Select(c=>c.position).ToArray();
  var occupied=new HashSet<Vector3Int>(positions);
  var tiles=Enumerable.Range(0,56).Select(i=>AssetDatabase.LoadAssetAtPath<Tile>("Assets/Art/Tiles/Test/basic_ground_tiles_"+i+".asset")).ToArray();
  if(tiles.Take(40).Any(t=>!t || !t.sprite))throw new InvalidOperationException("Missing source Tile or sprite");
  var tex=new Texture2D(2,2);tex.LoadImage(File.ReadAllBytes(Output+"/baseline_source/basic_ground_tiles_original_backup.png"));
  var player=UnityEngine.Object.FindObjectOfType<PlayerController>();var spawn=player.transform.position;
  // Shared continuous material field: quiet soil arena, northbound 2-3-cell road, organic perimeter meadow.
  var centers=positions.Select(p=>map.GetCellCenterWorld(p)+new Vector3(0,.5f,0)).ToArray();
  float minX=centers.Min(w=>w.x),maxX=centers.Max(w=>w.x),minY=centers.Min(w=>w.y),maxY=centers.Max(w=>w.y);
  Func<Vector3,int> desired=w=>{
   // World-space signed perimeter distance varies *within* a cell; integer-cell erosion
   // would incorrectly choose only pure tiles and leave a hard grass/soil seam.
   float edge=Mathf.Min(w.x-minX,maxX-w.x,w.y-minY,maxY-w.y);
   float meadowWidth=.82f+.12f*Mathf.Sin(w.x*.8f+w.y*.65f);
   float roadX=Mathf.Lerp(spawn.x,0,Mathf.Clamp01((w.y-spawn.y)/(5-spawn.y)))+.16f*Mathf.Sin(w.y*1.3f);
   bool road=w.y>spawn.y-1 && w.y<5.7f && Mathf.Abs(w.x-roadX)<.68f;
   if(road)return 1;
   if(edge<meadowWidth)return 0;
   return 2;
  };
  int changed=0;var counts=new Dictionary<int,int>();
  foreach(var p in positions){
   int best=3;float score=float.MaxValue;
   var center=map.GetCellCenterWorld(p);
   var wants=new int[8,8];for(int u=1;u<=7;u++)for(int v=1;v<=7;v++) {float a=u/8f,b=v/8f;wants[u,v]=desired(center+map.transform.TransformVector(new Vector3((a-b)*.5f,.75f-(a+b)*.25f,0)));}
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
  Action<string,int,Vector3> prop=(name,index,position)=>{if(!tiles[index]||!tiles[index].sprite)return;usedNames.Add(name);var existing=root.transform.Find(name);var go=existing?existing.gameObject:new GameObject(name);go.transform.SetParent(root.transform,false);go.transform.position=position;var sr=go.GetComponent<SpriteRenderer>()??go.AddComponent<SpriteRenderer>();sr.sprite=tiles[index].sprite;sr.sortingLayerID=renderer.sortingLayerID;sr.sortingOrder=renderer.sortingOrder+1;};
  for(int step=0;step<3;step++){float y=spawn.y+.4f+step*1.75f;if(y>4.3f)break;prop("PathLamp_L_"+step,40,new Vector3(-1.15f,y,0));prop("PathLamp_R_"+step,41,new Vector3(1.15f,y,0));}
  // Sparse pipes placed only at existing outer cells, away from route and combat center.
  int pipe=0;foreach(var p in positions.OrderBy(p=>p.x).ThenBy(p=>p.y)){if(occupied.Contains(p+Vector3Int.left)||occupied.Contains(p+Vector3Int.down))continue;var w=map.GetCellCenterWorld(p);if(Mathf.Abs(w.x)<3 || pipe>=6)continue;prop("EdgePipe_"+pipe,48+pipe%2,w);pipe++;}
  for(int i=root.transform.childCount-1;i>=0;i--)if(!usedNames.Contains(root.transform.GetChild(i).name))UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
  foreach(var pair in state)if(!pair.Key || EditorJsonUtility.ToJson(pair.Key)!=pair.Value)throw new InvalidOperationException("Gameplay component changed: "+pair.Key);
  var after=Read();if(after.maps.Single(m=>m.path==Hierarchy(map.transform)).cells.Count!=positions.Length)throw new InvalidOperationException("Footprint changed");
  EditorSceneManager.MarkSceneDirty(map.gameObject.scene);EditorSceneManager.SaveScene(map.gameObject.scene);
  File.WriteAllText(Output+"/tilemap-after.json",JsonUtility.ToJson(after,true));
  File.WriteAllText(Output+"/repaint-result.txt","PASS unchanged existing component serialization="+state.Count+"; footprint="+positions.Length+"; changed="+changed+"\n"+string.Join("\n",counts.Select(k=>"tile "+k.Key+" = "+k.Value)));
  Debug.Log("[TilesHD] REPAINT PASS changed="+changed+" invariants="+state.Count);
 }
 [MenuItem("TapTap/Tiles HD/Restore original cells")]
 public static void RestoreOriginal() {
  EditorSceneManager.OpenScene(ScenePath);
  var snapshot=JsonUtility.FromJson<Snapshot>(File.ReadAllText(Output+"/tilemap-before.json"));
  foreach(var m in UnityEngine.Object.FindObjectsOfType<Tilemap>()) {
   var source=snapshot.maps.Single(s=>s.path==Hierarchy(m.transform));m.ClearAllTiles();
   foreach(var c in source.cells){m.SetTile(c.position,AssetDatabase.LoadAssetAtPath<TileBase>(c.asset));m.SetTileFlags(c.position,TileFlags.None);m.SetColor(c.position,c.color);m.SetTransformMatrix(c.position,c.matrix);m.SetTileFlags(c.position,(TileFlags)c.flags);}
  }
  var decor=GameObject.Find("TilesHD_Decor");if(decor)UnityEngine.Object.DestroyImmediate(decor);
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
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
