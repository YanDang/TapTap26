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
 const string BaselineRevision="e07520f16254b12ac7d4625c36209fc334e08650";
 static string Output { get { var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-tilesHDRecordOutput");return i>=0 && i+1<args.Length ? args[i+1] : RepaintMap.Output; } }
 static byte[] BaselineBytes(string path){
  using(var p=new System.Diagnostics.Process()){
   p.StartInfo=new System.Diagnostics.ProcessStartInfo("git","show "+BaselineRevision+":"+path){UseShellExecute=false,RedirectStandardOutput=true,CreateNoWindow=true};
   p.Start();using(var data=new MemoryStream()){p.StandardOutput.BaseStream.CopyTo(data);p.WaitForExit();if(p.ExitCode!=0)throw new Exception("Missing baseline blob: "+path);return data.ToArray();}
  }
 }
 static string Text(byte[] bytes){return System.Text.Encoding.UTF8.GetString(bytes).Replace("\r\n","\n");}
 static double entered;
 static bool done;
 static RecordMap(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
 public static void Before(){Start("before");}
 public static void After(){Start("after");}
 static void Start(string phase){
  if(phase=="before" && (!File.ReadAllBytes(RepaintMap.ScenePath).SequenceEqual(File.ReadAllBytes(RepaintMap.Baseline+"/BiomechanicalCombatScene.unity")) || Text(File.ReadAllBytes(RepaintMap.ScenePath))!=Text(BaselineBytes(RepaintMap.ScenePath))))throw new Exception("Before recording requires exact e07520f baseline scene; never label modified map BEFORE.");
  SessionState.SetBool(Key+"Captured",false);SessionState.SetBool(Key+"Failed",false);
  Directory.CreateDirectory(Output);
  var prefs=TapPrefsSnapshot.Capture();prefs.WriteJournal(Output+"/editor-prefs-"+phase+".json");
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
    File.WriteAllText(Output+"/record-"+SessionState.GetString(Key,"")+"-result.txt",(passed?"GPU capture completed":"GPU capture FAILED")+"; editor raw preferences restored="+restored+"\n");
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
   var tileSprites=new List<Sprite>();foreach(var c in map.cellBounds.allPositionsWithin)if(map.HasTile(c) && map.GetSprite(c))tileSprites.Add(map.GetSprite(c));
   var textures=tileSprites.Select(s=>s.texture).Distinct().ToArray();
   File.WriteAllText(Output+"/texture-batches-"+phase+".txt",string.Join("\n",textures.Select(t=>t.name+" "+t.width+"x"+t.height))+"\nunique="+textures.Length);
   if(phase=="after" && textures.Length!=1)throw new Exception("Chunk Tilemap needs one packed texture, got "+textures.Length);
   if(phase=="after"){
    var decor=GameObject.Find("TilesHD_Decor");
    if(!decor)throw new Exception("Missing decoration root");
    var props=decor.GetComponentsInChildren<SpriteRenderer>(true);
    if(props.Length!=15 || props.Count(s=>s.name.StartsWith("PathLamp_"))!=6 || props.Count(s=>s.name.StartsWith("EdgePipe_"))!=9 || props.Any(s=>!s.enabled || !s.gameObject.activeInHierarchy || !s.sprite || s.color.a<=0 || s.sortingOrder<=map.GetComponent<TilemapRenderer>().sortingOrder))throw new Exception("Decoration renderer contract failed");
    File.WriteAllLines(Output+"/decor-runtime-after.txt",props.OrderBy(s=>s.name).Select(s=>s.name+" position="+s.transform.position+" sprite="+s.sprite.name+" texture="+s.sprite.texture.name+" sorting="+s.sortingLayerName+":"+s.sortingOrder+" bounds="+s.bounds));
   }
   var originalPosition=cam.transform.position;float originalSize=cam.orthographicSize;
   // Same fixed framing both phases; changes are in memory only.
   cam.transform.position=new Vector3(bounds.center.x,bounds.center.y,originalPosition.z);
   cam.orthographicSize=Mathf.Max(bounds.extents.y,bounds.extents.x/(1920f/1080f))*1.10f;
   Capture(cam,phase+"-panorama");
   cam.transform.position=new Vector3(p.transform.position.x,p.transform.position.y+0.65f,originalPosition.z);
   cam.orthographicSize=2.8f;
   Capture(cam,phase+"-closeup");
   File.WriteAllText(Output+"/runtime-"+phase+".txt","GPU="+SystemInfo.graphicsDeviceName+"\nPlayer="+p.transform.position+"\nDoor="+UnityEngine.Object.FindObjectOfType<GatherDoorAuraBehavior>().transform.position+"\nTilemap="+RepaintMap.Hierarchy(map.transform)+"\nBounds="+bounds+"\nCapture=1920x1080 camera render in live PlayMode; overlay UI excluded; original sprite references on disk untouched.\n");
   cam.transform.position=originalPosition;cam.orthographicSize=originalSize;
   SessionState.SetBool(Key+"Captured",true);
  }catch(Exception e){SessionState.SetBool(Key+"Failed",true);Debug.LogException(e);}
  EditorApplication.isPlaying=false;
 }
 static void ApplyBaseline(){
  // e07520f uses sixteen coordinate variants, not the historical single atlas.
  // One in-memory sheet preserves Chunk sorting without touching PNGs/Tile assets.
  var sheet=new Texture2D(8192,7168,TextureFormat.RGBA32,false){name="e07520f_variants",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
  var sprites=new Dictionary<Sprite,Sprite>();var evidence=new List<string>();
  for(int variant=-1;variant<16;variant++){
   string path=variant<0 ? "Assets/Pic/Test/basic_ground_tiles.png" : "Assets/Pic/Test/TilesHDVariants/variant_"+variant.ToString("00")+".png";
   // AssetDatabase rect/pivot/PPU are safe only while the original metadata is intact.
   if(Text(File.ReadAllBytes(path+".meta"))!=Text(BaselineBytes(path+".meta")))throw new Exception("Baseline metadata differs: "+path);
   byte[] bytes=BaselineBytes(path);
   using(var sha=System.Security.Cryptography.SHA256.Create())evidence.Add(path+" sha256="+BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant());
   var tex=new Texture2D(2,2,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
   if(!tex.LoadImage(bytes) || tex.width!=2048 || tex.height!=1792)throw new Exception("Invalid baseline PNG: "+path);
   int x=variant<0?0:variant%4*2048,y=variant<0?0:variant/4*1792;
   if(variant>=0)sheet.SetPixels32(x,y,2048,1792,tex.GetPixels32());
   foreach(var s in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()){
    var rect=s.rect;rect.position+=new Vector2(x,y);
    var copy=Sprite.Create(variant<0?tex:sheet,rect,new Vector2(s.pivot.x/s.rect.width,s.pivot.y/s.rect.height),s.pixelsPerUnit,0,SpriteMeshType.FullRect);copy.name=s.name;
    sprites.Add(s,copy);
   }
   if(variant>=0)UnityEngine.Object.DestroyImmediate(tex);
  }
  sheet.Apply(false,false);
  var decor=GameObject.Find("TilesHD_Decor");
  if(!decor || decor.GetComponentsInChildren<SpriteRenderer>(true).Length!=15 || decor.GetComponentsInChildren<SpriteRenderer>(true).Any(s=>!s.sprite || !sprites.ContainsKey(s.sprite)))throw new Exception("Baseline decoration sprite coverage failed");
  foreach(var m in UnityEngine.Object.FindObjectsOfType<Tilemap>())foreach(var pos in m.cellBounds.allPositionsWithin)if(m.HasTile(pos) && (!(m.GetTile(pos) is Tile t) || !t.sprite || !sprites.ContainsKey(t.sprite)))throw new Exception("Baseline tile sprite coverage failed: "+pos);
  File.WriteAllLines(Output+"/baseline-textures-before.txt",evidence);
  foreach(var sr in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>(true))if(sr.sprite && sprites.TryGetValue(sr.sprite,out var baselineSprite))sr.sprite=baselineSprite;
  foreach(var m in UnityEngine.Object.FindObjectsOfType<Tilemap>()){
   var replacements=new Dictionary<TileBase,Tile>();
   foreach(var pos in m.cellBounds.allPositionsWithin){var tile=m.GetTile(pos) as Tile;if(!tile || !tile.sprite || !sprites.ContainsKey(tile.sprite))continue;
    if(!replacements.TryGetValue(tile,out var copy)){copy=UnityEngine.Object.Instantiate(tile);copy.sprite=sprites[tile.sprite];replacements[tile]=copy;}m.SetTile(pos,copy);
   }m.RefreshAllTiles();
  }
 }
 static void Capture(Camera cam,string label){
  if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)throw new Exception("No GPU");
  var rt=RenderTexture.GetTemporary(1920,1080,24,RenderTextureFormat.ARGB32);var old=cam.targetTexture;var active=RenderTexture.active;
  try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(Output+"/BiomechanicalCombatScene-"+label+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);}
  finally{cam.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);}
 }
}
}
