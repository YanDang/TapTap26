using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace TilesHD {
// Only opt-in Editor jobs. No runtime or resource changes are shipped.
[InitializeOnLoad]
public static class RecordMap {
 const string Key="TilesHD.Record";
 static double entered;
 static bool done;
 static RecordMap(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
 public static void Before(){Start("before");}
 public static void After(){Start("after");}
 static void Start(string phase){
  SessionState.SetBool(Key+"Captured",false);SessionState.SetBool(Key+"Failed",false);
  Directory.CreateDirectory(RepaintMap.Output);
  var prefs=TapPrefsSnapshot.Capture();prefs.WriteJournal(RepaintMap.Output+"/editor-prefs-"+phase+".json");
  SessionState.SetString(Key,phase);SessionState.SetString(Key+"Prefs",JsonUtility.ToJson(prefs.Data));
  SessionState.SetFloat(Key+"Deadline",(float)EditorApplication.timeSinceStartup+180);
  EditorSceneManager.OpenScene(RepaintMap.ScenePath);
  // Create a deterministic fresh run, fully restored after all gameplay objects unload.
  foreach(var v in prefs.Data.values){/* raw journal retained; clear logical known run keys below */}
  PlayerPrefs.DeleteKey("TAP_SHELL_BACKPACK_DATA");PlayerPrefs.DeleteKey("TAP_CRAFTED_PRODUCTS_DATA_V2");PlayerPrefs.Save();
  EditorApplication.isPlaying=true;
 }
 static void Changed(PlayModeStateChange s){
  if(string.IsNullOrEmpty(SessionState.GetString(Key,"")))return;
  if(s==PlayModeStateChange.EnteredPlayMode){entered=EditorApplication.timeSinceStartup;done=false;}
  if(s==PlayModeStateChange.EnteredEditMode){
   try{
    var prefs=new TapPrefsSnapshot{Data=JsonUtility.FromJson<TapPrefsSnapshot.Journal>(SessionState.GetString(Key+"Prefs",""))};
    prefs.Restore();
    bool restored=prefs.Matches(TapPrefsSnapshot.Capture());
    bool passed=SessionState.GetBool(Key+"Captured",false) && restored && !SessionState.GetBool(Key+"Failed",false);
    SessionState.SetBool(Key+"Failed",!passed);
    File.WriteAllText(RepaintMap.Output+"/record-"+SessionState.GetString(Key,"")+"-result.txt",(passed?"GPU capture completed":"GPU capture FAILED")+"; editor raw preferences restored="+restored+"\n");
    if(passed)Debug.Log("[TilesHD] RECORD "+SessionState.GetString(Key,"")+" PASS");
    else Debug.LogError("[TilesHD] RECORD failed: capture incomplete or preferences not restored");
   }catch(Exception e){SessionState.SetBool(Key+"Failed",true);Debug.LogException(e);}
   SessionState.EraseString(Key);EditorApplication.Exit(SessionState.GetBool(Key+"Failed",false)?1:0);
  }
 }
 static void Tick(){
  string phase=SessionState.GetString(Key,"");if(phase=="")return;
  if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+"Deadline",float.MaxValue)){Debug.LogError("[TilesHD] recording timeout");EditorApplication.Exit(2);return;}
  if(!EditorApplication.isPlaying || done || EditorApplication.timeSinceStartup-entered<5 || !PlayerController.Instance || !Camera.main)return;
  done=true;
  try{
   if(phase=="before")ApplyBaseline();
   var cam=Camera.main;var p=PlayerController.Instance;
   var map=UnityEngine.Object.FindObjectOfType<Tilemap>();
   var bounds=map.GetComponent<TilemapRenderer>().bounds;
   var originalPosition=cam.transform.position;float originalSize=cam.orthographicSize;
   // Same fixed framing both phases; changes are in memory only.
   cam.transform.position=new Vector3(bounds.center.x,bounds.center.y,originalPosition.z);
   cam.orthographicSize=Mathf.Max(bounds.extents.y,bounds.extents.x/(1920f/1080f))*1.10f;
   Capture(cam,phase+"-panorama");
   cam.transform.position=new Vector3(p.transform.position.x,p.transform.position.y+0.65f,originalPosition.z);
   cam.orthographicSize=2.8f;
   Capture(cam,phase+"-closeup");
   File.WriteAllText(RepaintMap.Output+"/runtime-"+phase+".txt","GPU="+SystemInfo.graphicsDeviceName+"\nPlayer="+p.transform.position+"\nDoor="+UnityEngine.Object.FindObjectOfType<GatherDoorAuraBehavior>().transform.position+"\nTilemap="+RepaintMap.Hierarchy(map.transform)+"\nBounds="+bounds+"\nCapture=1920x1080 camera render in live PlayMode; overlay UI excluded; original sprite references on disk untouched.\n");
   cam.transform.position=originalPosition;cam.orthographicSize=originalSize;
   SessionState.SetBool(Key+"Captured",true);
  }catch(Exception e){SessionState.SetBool(Key+"Failed",true);Debug.LogException(e);}
  EditorApplication.isPlaying=false;
 }
 static void ApplyBaseline(){
  var tex=new Texture2D(2,2,TextureFormat.RGBA32,false);tex.LoadImage(File.ReadAllBytes(RepaintMap.Output+"/baseline_source/basic_ground_tiles.png"));tex.filterMode=FilterMode.Point;
  var sprites=new Dictionary<string,Sprite>();
  foreach(var s in AssetDatabase.LoadAllAssetsAtPath("Assets/Pic/Test/basic_ground_tiles.png").OfType<Sprite>()){
   // Named source cells keep their original 128px rect even if the final atlas already imported.
   int index=int.Parse(s.name.Substring(s.name.LastIndexOf('_')+1));
   sprites[s.name]=Sprite.Create(tex,new Rect(index%8*128,(6-index/8)*128,128,128),new Vector2(.5f,.25f),128,0,SpriteMeshType.FullRect);
  }
  foreach(var m in UnityEngine.Object.FindObjectsOfType<Tilemap>()){
   var replacements=new Dictionary<TileBase,Tile>();
   foreach(var pos in m.cellBounds.allPositionsWithin){var tile=m.GetTile(pos) as Tile;if(!tile || !tile.sprite || !sprites.ContainsKey(tile.sprite.name))continue;
    if(!replacements.TryGetValue(tile,out var copy)){copy=UnityEngine.Object.Instantiate(tile);copy.sprite=sprites[tile.sprite.name];replacements[tile]=copy;}m.SetTile(pos,copy);
   }m.RefreshAllTiles();
  }
 }
 static void Capture(Camera cam,string label){
  if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)throw new Exception("No GPU");
  var rt=RenderTexture.GetTemporary(1920,1080,24,RenderTextureFormat.ARGB32);var old=cam.targetTexture;var active=RenderTexture.active;
  try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(RepaintMap.Output+"/BiomechanicalCombatScene-"+label+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);}
  finally{cam.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);}
 }
}
}
