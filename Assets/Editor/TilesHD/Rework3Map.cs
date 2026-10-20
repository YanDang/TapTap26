using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace TilesHD {
// Narrow rework3: no props, occupancy, camera, gameplay, original Tile or atlas writes.
public static class Rework3Map {
 const string MaterialPath="Assets/Art/Tiles/Test/TilesHDWorldLight.mat";
 static string O=>RepaintMap.Output;
 static bool Edge(Vector3Int p) { return (p.x>=-2&&p.x<=11&&(p.y==-2||p.y==2)) || (p.y>=-2&&p.y<=11&&(p.x==7||p.x==11)) || (p.x==-2&&p.y>=-1&&p.y<=1) || (p.y==11&&p.x>=8&&p.x<=10); }
 // Every boundary has a smooth notch every 2.5 cells, phase-offset between sides.
 static bool Stone(float x,float y) {
  float h=1.68f+.39f*Mathf.Sin(x*2.513274f+(y>0?.7f:2.1f));
  float v=1.68f+.39f*Mathf.Sin(y*2.513274f+(x>9?1.4f:3.2f));
  bool road=(x>=-1.65f&&x<=10.65f&&Mathf.Abs(y)<=h)||(y>=-1.65f&&y<=10.65f&&Mathf.Abs(x-9)<=v);
  // Broken stone at the inside bend and both outer corner shoulders (within local rim).
  foreach(var q in new[]{new Vector2(6.8f,2.2f),new Vector2(10.9f,-2.15f),new Vector2(11.1f,10.8f),new Vector2(-2.1f,1.8f)})
   if((new Vector2(x,y)-q).sqrMagnitude<.16f)road=true;
  return road;
 }
 static int Classify(Color c) {return c.g>c.r+20f/255&&c.g>90f/255?0:Mathf.Abs(c.r-c.g)<18f/255&&Mathf.Abs(c.g-c.b)<18f/255&&c.r>120f/255?1:2;}
 public static void Repaint() {
  Directory.CreateDirectory(O);EditorSceneManager.OpenScene(RepaintMap.ScenePath);
  var before=RepaintMap.Read(); if(!File.Exists(O+"/tilemap-before.json"))File.WriteAllText(O+"/tilemap-before.json",JsonUtility.ToJson(before,true));
  File.WriteAllText(O+"/tilemap-backup-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+".json",JsonUtility.ToJson(before,true));
  var map=UnityEngine.Object.FindObjectsOfType<Tilemap>().Single(m=>m.name=="Tilemap");var renderer=map.GetComponent<TilemapRenderer>();
  if(map.GetComponent<TilemapCollider2D>() || before.maps.Single(m=>m.path==RepaintMap.Hierarchy(map.transform)).cells.Count!=698)throw new Exception("Footprint/collider mismatch");
  var state=UnityEngine.Object.FindObjectsOfType<Component>(true).Where(c=>c&&c!=map).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
  var tex=new Texture2D(2,2);tex.LoadImage(File.ReadAllBytes("Assets/Pic/Test/basic_ground_tiles_original_backup.png"));
  var edits=new List<string>();int changed=0;
  foreach(var p in map.cellBounds.allPositionsWithin) {
   if(!map.HasTile(p)||!Edge(p))continue;
   int best=3;float score=float.MaxValue;
   // Use existing soil/stone transition masks only. No grass/material reconstruction.
   foreach(int i in new[]{2,3,16,17,18,19,20,21,22,23,24,25,26,27}) {
    float cost=0;
    for(int u=1;u<=15;u++)for(int v=1;v<=15;v++) {
     float a=u/16f,b=v/16f;int px=Mathf.RoundToInt(64+64*(a-b)),py=Mathf.RoundToInt(32*(a+b));
     int actual=Classify(tex.GetPixel(i%8*128+px,tex.height-1-(i/8*128+py)));
     int want=Stone(p.x+.5f-b,p.y+.5f-a)?1:2;if(actual!=want)cost+=actual==0?10:1;
    }
    if(cost<score){score=cost;best=i;}
   }
   string path=VariantTiles.Folder+"/v"+VariantTiles.Index(p).ToString("00")+"_tile_"+best.ToString("00")+".asset";
   var tile=AssetDatabase.LoadAssetAtPath<Tile>(path);if(!tile||!tile.sprite)throw new Exception("Missing "+path);
   var prev=AssetDatabase.GetAssetPath(map.GetTile(p));if(map.GetTile(p)!=tile){map.SetTile(p,tile);changed++;}
   edits.Add(p.x+","+p.y+","+best+","+prev+","+path);
  }
  UnityEngine.Object.DestroyImmediate(tex);
  var shader=Shader.Find("TilesHD/ContinuousWorldLight");if(!shader||!shader.isSupported)throw new Exception("World shader unavailable");
  var mat=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,MaterialPath);}if(mat.shader!=shader)throw new Exception("Unexpected material");
  const string lightPath="Assets/Art/Tiles/Test/TilesHDSoilLight.png";
  AssetDatabase.ImportAsset(lightPath);
  var li=(TextureImporter)AssetImporter.GetAtPath(lightPath);
  if(li.sRGBTexture || li.mipmapEnabled || li.textureCompression!=TextureImporterCompression.Uncompressed || li.maxTextureSize!=8192 || li.npotScale!=TextureImporterNPOTScale.None){li.sRGBTexture=false;li.mipmapEnabled=false;li.textureCompression=TextureImporterCompression.Uncompressed;li.maxTextureSize=8192;li.npotScale=TextureImporterNPOTScale.None;li.wrapMode=TextureWrapMode.Clamp;li.filterMode=FilterMode.Bilinear;li.SaveAndReimport();}
  var light=AssetDatabase.LoadAssetAtPath<Texture2D>(lightPath);if(!light)throw new Exception("Missing soil light correction");
  if(mat.GetTexture("_SoilLight")!=light){mat.SetTexture("_SoilLight",light);EditorUtility.SetDirty(mat);}
  renderer.sharedMaterial=mat;
  foreach(var pair in state) {
   if(pair.Key==renderer)continue;
   if(!pair.Key||EditorJsonUtility.ToJson(pair.Key)!=pair.Value)throw new Exception("Protected component changed "+pair.Key);
  }
  // Sole renderer exception is material reference; mode, sorting, bounds and all other fields protected.
  var material=renderer.sharedMaterial;var priorJson=state[renderer];
  var oldMat=AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
  // Compare before/after with material serialized field removed, without touching the renderer again.
  Func<string,string> strip=s=>System.Text.RegularExpressions.Regex.Replace(s,"\"m_Materials\":\\[.*?\\]","\"m_Materials\":[]");
  if(strip(EditorJsonUtility.ToJson(renderer))!=strip(priorJson))throw new Exception("Renderer fields other than material changed");
  File.WriteAllLines(O+"/component-before.jsonl",state.Select(k=>JsonUtility.ToJson(new RepaintMap.ComponentRecord{path=RepaintMap.Hierarchy(k.Key.transform),type=k.Key.GetType().FullName,json=k.Value})));
  File.WriteAllLines(O+"/component-after.jsonl",state.Select(k=>JsonUtility.ToJson(new RepaintMap.ComponentRecord{path=RepaintMap.Hierarchy(k.Key.transform),type=k.Key.GetType().FullName,json=EditorJsonUtility.ToJson(k.Key)})));
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(map.gameObject.scene);EditorSceneManager.SaveScene(map.gameObject.scene);
  File.WriteAllText(O+"/tilemap-after.json",JsonUtility.ToJson(RepaintMap.Read(),true));File.WriteAllLines(O+"/road-edge.csv",new[]{"x,y,tile,before,after"}.Concat(edits));
  File.WriteAllText(O+"/repaint-result.txt","PASS changed="+changed+"; candidate rim="+edits.Count+"; protected components="+state.Count+"; renderer material only exception\n");
  Debug.Log("[TilesHD] REWORK3 PASS changed="+changed);
 }
 public static void Readback(){EditorSceneManager.OpenScene(RepaintMap.ScenePath);File.WriteAllText(O+"/fresh-readback.json",JsonUtility.ToJson(RepaintMap.Read(),true));Debug.Log("[TilesHD] FRESH READBACK PASS");}
}
}
