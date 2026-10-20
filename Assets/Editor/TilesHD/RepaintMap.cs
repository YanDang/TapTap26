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
 public const string Output="Docs/GameJamIntegration/tiles_hd/rework3/unity";
 public const string Baseline=Output+"/baseline";
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
 [MenuItem("TapTap/Tiles HD/Repaint")]
 public static void Repaint() { Rework3Map.Repaint(); }
 [MenuItem("TapTap/Tiles HD/Restore original cells")]
 public static void RestoreOriginal() {
  // Full immutable e07520f scene restores its original ground material and cells.
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
