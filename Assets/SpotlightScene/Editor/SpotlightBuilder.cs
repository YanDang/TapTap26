using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using SpotlightScene;

public static class SpotlightBuilder {
 const string A="Assets/SpotlightScene";
 public const string ScenePath=A+"/Scenes/Nirun_Ecostation.unity";
 static Transform root;
 static Material stone,tile,moss,shell,wood,water,kelp,coral,cyan,purple,amber,dark;
 static System.Random rng;
 static Vector3 V(float x,float y,float z) { return new Vector3(x,y,z); }
 static Material Mat(string name, Color color, float metallic=0, Color? glow=null) {
  string path=A+"/Materials/"+name+".mat";
  var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(!m) { m=new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m,path); }
  m.color=color; m.SetFloat("_Metallic",metallic); m.SetFloat("_Glossiness",.3f);
  if(glow.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor",glow.Value); }
  EditorUtility.SetDirty(m); return m;
 }
 static Transform Group(string name, Transform parent=null) { var g=new GameObject(name); g.transform.SetParent(parent?parent:root,false); return g.transform; }
 static GameObject Shape(string name, PrimitiveType type, Vector3 pos,Vector3 scale,Material mat,Transform parent,bool solid=false) {
  var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=scale;
  g.GetComponent<Renderer>().sharedMaterial=mat;
  var col=g.GetComponent<Collider>(); if(!solid) UnityEngine.Object.DestroyImmediate(col); else g.layer=8;
  return g;
 }
 static GameObject Box(string n,Vector3 p,Vector3 s,Material m,Transform t,bool solid=false) { return Shape(n,PrimitiveType.Cube,p,s,m,t,solid); }
 static GameObject Ball(string n,Vector3 p,Vector3 s,Material m,Transform t) { return Shape(n,PrimitiveType.Sphere,p,s,m,t); }
 static GameObject Cyl(string n,Vector3 p,Vector3 s,Material m,Transform t,bool solid=false) { return Shape(n,PrimitiveType.Cylinder,p,s,m,t,solid); }
 static float R(float a,float b) { return a+(float)rng.NextDouble()*(b-a); }
 static void Tube(string name,Vector3[] pts,float radius,Material mat,Transform parent,int sides=7) {
  var verts=new List<Vector3>();var triangles=new List<int>();
  for(int i=0;i<pts.Length;i++) {
   Vector3 tangent=(pts[Math.Min(i+1,pts.Length-1)]-pts[Math.Max(i-1,0)]).normalized;
   Vector3 normal=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.9f?Vector3.forward:Vector3.up).normalized;
   Vector3 bi=Vector3.Cross(tangent,normal);
   for(int j=0;j<sides;j++) { float a=j*Mathf.PI*2/sides;verts.Add(pts[i]+radius*(normal*Mathf.Cos(a)+bi*Mathf.Sin(a))); }
   if(i==0) continue;
   for(int j=0;j<sides;j++) {int a=(i-1)*sides+j,b=(i-1)*sides+(j+1)%sides,c=i*sides+j,d=i*sides+(j+1)%sides; triangles.AddRange(new[]{a,c,b,b,c,d});}
  }
  // Capped ends, reliable closed geometry for external import.
  verts.Add(pts[0]);int c0=verts.Count-1; verts.Add(pts[pts.Length-1]);int c1=verts.Count-1;
  for(int j=0;j<sides;j++){triangles.AddRange(new[]{c0,(j+1)%sides,j,c1,(pts.Length-1)*sides+j,(pts.Length-1)*sides+(j+1)%sides});}
  var mesh=new Mesh{name=name};mesh.SetVertices(verts);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
  string path=A+"/Meshes/"+name+".asset";var prior=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(prior){EditorUtility.CopySerialized(mesh,prior);UnityEngine.Object.DestroyImmediate(mesh);mesh=prior;}else AssetDatabase.CreateAsset(mesh,path);
  var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=mat;
 }
 static Vector3[] Arc(Vector3 center,float rx,float ry,int steps=20) {var p=new Vector3[steps+1];for(int i=0;i<=steps;i++){float a=i*Mathf.PI/steps;p[i]=center+V(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry,0);}return p;}
 static void Ring(string name,Vector3 center,float r,float thick,Material mat,Transform parent) {var p=new Vector3[41];for(int i=0;i<=40;i++){float a=i*Mathf.PI*2/40;p[i]=center+V(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);}Tube(name,p,thick,mat,parent);}
 static void Anchor(string name,string role,Vector3 p,Vector3 size) { var g=Group(name,GameObject.Find("Gameplay_Anchors").transform);g.position=p;var a=g.gameObject.AddComponent<SpotlightAnchor>();a.role=role;a.size=size; }
 static void GlowLight(string name,Vector3 pos,Color color,float intensity,float range,Transform parent) {var t=Group(name,parent);t.localPosition=pos;var l=t.gameObject.AddComponent<Light>();l.type=LightType.Point;l.color=color;l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;}
 static void Plant(string name,Vector3 p,float h,Transform parent) {
  var g=Group(name,parent);g.localPosition=p;
  Tube(name+"_stem",new[]{V(0,0,0),V(.1f,h*.35f,0),V(-.12f,h*.7f,.06f),V(.08f,h,0)},.065f,kelp,g);
  for(int j=0;j<5;j++){float y=.2f+j*h*.14f;float dir=j%2==0?1:-1; var leaf=Ball("Water_Leaf",V(dir*.25f,y,0),V(.65f,.09f,.22f),kelp,g);leaf.transform.localRotation=Quaternion.Euler(0,j*36,dir*30);}
 }
 static void Coral(string name,Vector3 p,float h,Transform parent) {
  var g=Group(name,parent);g.localPosition=p;
  Tube(name+"_trunk",new[]{V(0,0,0),V(0,h*.55f,0),V(.1f,h,0)},.12f,coral,g);
  for(int i=0;i<3;i++){float a=i*2.1f;Vector3 end=V(Mathf.Cos(a)*h*.45f,h*(.6f+i*.14f),Mathf.Sin(a)*h*.45f);Tube(name+"_branch"+i,new[]{V(0,h*.3f,0),end*.7f,end},.075f,coral,g);Ball("Luminous_Polyp",end,V(.18f,.18f,.18f),i%2==0?cyan:purple,g);}
 }
 static void Station() {
  var g=Group("Repairable_Shell_Ecostation");
  Box("Station_Foundation",V(0,-.12f,7),V(8,.24f,6),stone,g); // Flush with y=0 gameplay ground.
  Box("Rear_Wall",V(0,1.35f,9.8f),V(8,2.7f,.35f),shell,g,true);
  Box("Left_Low_Wall",V(-3.85f,.5f,7),V(.3f,1,5.4f),shell,g,true);
  Box("Right_Broken_Wall",V(3.85f,.55f,8),V(.3f,1.1f,3.8f),shell,g,true);
  for(int i=0;i<4;i++){float z=4.8f+i*1.5f;Tube("Shell_Rib_"+i,Arc(V(0,.25f,z),3.8f,3.7f),.16f,shell,g,8);}
  // Rear ribs leave the foreground open; intentionally roofless for player readability.
  for(int i=0;i<9;i++){float a=i*Mathf.PI/8;Tube("Shell_Back_Vein_"+i,new[]{V(0,.3f,9.55f),V(Mathf.Cos(a)*2.8f,Mathf.Sin(a)*2.4f+.3f,9.55f)},.045f,wood,g);}
  Box("Door_Membrane",V(0,1.15f,9.57f),V(1.8f,2.1f,.06f),dark,g);
  Tube("Door_Living_Rim",Arc(V(0,.45f,9.47f),1.05f,1.9f),.075f,cyan,g);
  Box("Repair_Console",V(-2.3f,.58f,8.5f),V(1.2f,.9f,.7f),wood,g,true);
  Box("Repair_Console_Core",V(-2.3f,1.06f,8.5f),V(.8f,.04f,.5f),amber,g);
  for(int i=0;i<4;i++){Ball("Warm_Spore",V(-2.8f+i*1.85f,2.75f,9.4f),V(.2f,.2f,.2f),amber,g);}
  GlowLight("Warm_Safe_Light",V(0,2.5f,7),new Color(1,.62f,.29f),3,10,g);
  Anchor("Repair_Station_Interaction","Repair",V(-2.3f,0,7.1f),V(1.4f,1,1.2f));
 }
 static void Reservoir() {
  var g=Group("Algae_Water_Resource_Garden");g.localPosition=V(-9,0,4.4f);
  Cyl("Reservoir_Base",V(0,.18f,0),V(5,.18f,5),stone,g,true);
  Cyl("Water_Surface",V(0,.4f,0),V(4.4f,.018f,4.4f),water,g);
  Ring("Reservoir_Rim",V(0,.48f,0),2.3f,.13f,shell,g);
  for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Plant("Living_Algae_"+i,V(Mathf.Cos(a)*1.7f,.42f,Mathf.Sin(a)*1.7f),R(.7f,1.6f),g);}
  Tube("Water_Delivery_Vine",new[]{V(1.8f,.6f,0),V(2.8f,1.6f,.5f),V(3.3f,1.8f,1.6f),V(4.2f,.6f,2)},.17f,kelp,g);
  for(int i=0;i<5;i++)Ball("Water_Pulse_Node",V(2.5f+i*.36f,1.6f,1.2f),V(.13f,.13f,.13f),cyan,g);
  GlowLight("Algae_Cyan_Light",V(0,1.5f,0),Color.cyan,2,8,g);
  for(int i=0;i<3;i++){var t=Group("Resource_Pod_"+i,g);t.localPosition=V(-1.8f+i*1.8f,0,-3.4f);Box("Cultivation_Base",V(0,.25f,0),V(1.3f,.5f,1.1f),wood,t,true);Plant("Harvest_Algae_"+i,V(0,.5f,0),.85f,t);Anchor("Harvest_Water_"+i,"Resource",t.position+V(0,0,-1.1f),V(1,1,1));}
 }
 static void Nursery() {
  var g=Group("Lightning_Jellyfish_Nursery");g.localPosition=V(9,0,4.3f);
  for(int i=0;i<3;i++) {
   var t=Group("Jellyfish_Cradle_"+i,g);t.localPosition=V((i-1)*2.1f,0,i%2==0?0:1.6f);
   Cyl("Cradle_Base",V(0,.2f,0),V(1.8f,.2f,1.8f),stone,t,true);
   Ring("Cradle_Energy_Ring_"+i,V(0,.48f,0),.85f,.065f,purple,t);
   for(int j=0;j<3;j++){float a=j*Mathf.PI*2/3;Tube("Cradle_Stem_"+i+"_"+j,new[]{V(Mathf.Cos(a)*.8f,.3f,Mathf.Sin(a)*.8f),V(Mathf.Cos(a)*.95f,1.3f,Mathf.Sin(a)*.95f)},.065f,shell,t);}
   var jelly=Group("Floating_Jellyfish_"+i,t);jelly.localPosition=V(0,1.95f,0);var motion=jelly.gameObject.AddComponent<SpotlightEcoMotion>();motion.origin=jelly.localPosition;motion.phase=i*1.8f;motion.speed=1.2f;
   Ball("Bell",Vector3.zero,V(1.25f,.6f,1.25f),i==1?cyan:purple,jelly);
   Ball("Bell_Nucleus",V(0,.1f,0),V(.35f,.3f,.35f),amber,jelly);
   for(int k=0;k<6;k++){float a=k*Mathf.PI/3;float x=Mathf.Cos(a)*.4f,z=Mathf.Sin(a)*.4f;Tube("Jelly_Tendril_"+i+"_"+k,new[]{V(x,-.12f,z),V(x*1.1f,-.5f,z),V(x*.7f,-1,z+.12f)},.035f,k%2==0?cyan:purple,jelly);}
   GlowLight("Bioelectric_Light",V(0,1.8f,0),new Color(.6f,.25f,1),1.5f,5,t);
   Anchor("Harvest_Electric_"+i,"Resource",t.position+V(0,0,-1.7f),V(1,1,1));
  }
 }
 static void Landscape() {
  var g=Group("Wet_Stone_Ground");var floor=Box("Ground",V(0,-.125f,0),V(32,.25f,26),stone,g,true);floor.layer=9;
  for(int x=-7;x<=7;x++)for(int z=-6;z<=5;z++) {
   var b=Box("Wet_Paving",V(x*2.08f,.012f,z*2.08f),V(2,.025f,2),((x+z)%7==0)?moss:tile,g);b.transform.localRotation=Quaternion.Euler(0,R(-1,1),0);
  }
  var paths=Group("Water_Channel_Inlays");
  Box("Water_Channel_West",V(-6.1f,.043f,2.5f),V(.16f,.018f,16),water,paths);
  Box("Water_Channel_East",V(6.1f,.043f,2.5f),V(.16f,.018f,16),water,paths);
  Box("Water_Channel_Cross",V(0,.043f,-4),V(12.3f,.018f,.16f),water,paths);
  var edge=Group("Coral_Root_Ruins");
  for(int i=0;i<24;i++) {
   float x=i<12?-14.7f:14.7f;float z=-11+(i%12)*1.95f;
   if(z>-8&&z<-4)continue; // Two clear enemy entrances.
   var rock=Ball("Boundary_Root_Stone",V(x,.45f,z),V(R(1,2),R(.8f,1.8f),R(1,1.8f)),stone,edge);
   rock.layer=8;rock.AddComponent<BoxCollider>();
   if(i%2==0)Coral("Edge_Coral_"+i,V(x- Mathf.Sign(x)*.3f,0,z),R(1.1f,2.2f),edge);
  }
  for(int i=0;i<12;i++) {float x=-13+i*2.35f;if(Mathf.Abs(x)<2)continue;Box("Rear_Ruin",V(x,.65f,11.8f),V(1.8f,1.3f,.7f),shell,edge,true);Coral("Rear_Coral_"+i,V(x,0,12.2f),R(1.2f,2.4f),edge);}
  for(int i=0;i<8;i++){float x=-12+i*3.4f;Coral("Foreground_Coral_"+i,V(x,0,-11.8f),R(.6f,1.1f),edge);}
  for(int i=0;i<35;i++){float x=R(-13,13),z=R(-11,11);if(Mathf.Abs(x)<6||Mathf.Abs(z+6)<1.5f)continue;Ball("Moss_Islet",V(x,.025f,z),V(R(.4f,1.2f),.08f,R(.4f,1.2f)),moss,edge);}
  var plaza=Group("Open_Building_Plaza");
  Ring("Build_Plaza_Inlay",V(0,.06f,-1),3.8f,.03f,wood,plaza);
  // Clear radius 3.8 area. Grid graphics are not furniture and have no colliders.
  for(int i=-2;i<=2;i++){Box("Build_Grid_X",V(i*1.5f,.035f,-1),V(.015f,.01f,6),wood,plaza);Box("Build_Grid_Z",V(0,.035f,-1+i*1.5f),V(6,.01f,.015f),wood,plaza);}
 }
 static void CameraAndLight() {
  var g=Group("Lighting_And_Camera");var cam=Group("Main Camera",g).gameObject.AddComponent<Camera>();cam.tag="MainCamera";cam.transform.position=V(24,28,-30);cam.transform.LookAt(V(0,.3f,1));cam.orthographic=true;cam.orthographicSize=16.8f;cam.nearClipPlane=.1f;cam.farClipPlane=150;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.025f,.052f,.09f);cam.allowHDR=true;
  var key=Group("Moon_Key",g).gameObject.AddComponent<Light>();key.type=LightType.Directional;key.transform.rotation=Quaternion.Euler(50,-35,0);key.color=new Color(.65f,.82f,1);key.intensity=1.1f;key.shadows=LightShadows.Soft;
  RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.28f,.38f,.48f);RenderSettings.ambientEquatorColor=new Color(.15f,.22f,.29f);RenderSettings.ambientGroundColor=new Color(.06f,.09f,.12f);RenderSettings.fog=false;RenderSettings.skybox=null;
  QualitySettings.shadowDistance=70;QualitySettings.shadows=ShadowQuality.All;QualitySettings.antiAliasing=4;
 }
 [MenuItem("Spotlight/Build Ecostation")]
 public static void Build() {
  try {
   Directory.CreateDirectory("Audit");Directory.CreateDirectory("Exports");
   if(File.Exists(ScenePath)){string backup="Audit/Backups/"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(backup);File.Copy(ScenePath,backup+"/Nirun_Ecostation.unity");if(File.Exists(ScenePath+".meta"))File.Copy(ScenePath+".meta",backup+"/Nirun_Ecostation.unity.meta");}
   foreach(string d in new[]{"Scenes","Materials","Meshes","Prefabs"})Directory.CreateDirectory(A+"/"+d);
   AssetDatabase.Refresh();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);root=new GameObject("Nirun_Ecostation_Root").transform;rng=new System.Random(421);
   var so=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);so.FindProperty("layers").GetArrayElementAtIndex(8).stringValue="SpotlightObstacle";so.FindProperty("layers").GetArrayElementAtIndex(9).stringValue="SpotlightGround";so.ApplyModifiedProperties();
   stone=Mat("Basalt",new Color(.13f,.21f,.25f));tile=Mat("Wet_Paving",new Color(.28f,.38f,.4f),.15f);moss=Mat("Moss",new Color(.09f,.29f,.23f));shell=Mat("Shell_Ivory",new Color(.66f,.73f,.68f));wood=Mat("Living_Bark",new Color(.32f,.24f,.18f));water=Mat("Deep_Teal_Water",new Color(.025f,.33f,.42f),.5f);kelp=Mat("Water_Algae",new Color(.14f,.5f,.34f));coral=Mat("Rose_Coral",new Color(.55f,.23f,.37f));cyan=Mat("Bio_Cyan",new Color(.12f,.85f,.82f),0,new Color(.03f,.65f,.55f));purple=Mat("Bio_Violet",new Color(.58f,.31f,.85f),0,new Color(.38f,.12f,.6f));amber=Mat("Safe_Amber",new Color(1,.67f,.28f),0,new Color(1,.48f,.14f));dark=Mat("Dormant_Membrane",new Color(.05f,.18f,.2f));
   Group("Gameplay_Anchors");Landscape();Station();Reservoir();Nursery();CameraAndLight();
   Anchor("PlayerSpawn","PlayerSpawn",V(0,0,-5),V(1,2,1));
   Anchor("EnemyEntrance_West","EnemyEntrance",V(-13,0,-6),V(2,2,2));Anchor("EnemyEntrance_East","EnemyEntrance",V(13,0,-6),V(2,2,2));Anchor("EnemyEntrance_North","EnemyEntrance",V(0,0,11),V(2,2,2));
   Anchor("BuildZone_Central","BuildZone",V(0,0,-1),V(6,1,6));Anchor("BuildZone_West","BuildZone",V(-4,0,-3),V(2,1,2));Anchor("BuildZone_East","BuildZone",V(4,0,-3),V(2,1,2));
   int j=0;foreach(var p in new[]{V(-5,0,-3),V(-5,0,3),V(5,0,3),V(5,0,-3)})Anchor("Patrol_"+j++,"Patrol",p,V(1,1,1));
   EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
   PrefabUtility.SaveAsPrefabAsset(root.gameObject,A+"/Prefabs/Nirun_Ecostation.prefab");AssetDatabase.SaveAssets();
   Validate();CaptureAll();
   AssetDatabase.ExportPackage(A,"Exports/Nirun_Ecostation.unitypackage",ExportPackageOptions.Recurse|ExportPackageOptions.IncludeDependencies);
   File.WriteAllText("Audit/build_complete.flag",DateTime.Now.ToString("O"));Debug.Log("[SpotlightBuilder] PASS scene="+ScenePath+" transforms="+root.GetComponentsInChildren<Transform>().Length);
  } catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
 }
 [MenuItem("Spotlight/Validate Ecostation")]
 public static void Validate() {
  if(!GameObject.Find("Nirun_Ecostation_Root"))throw new Exception("Missing scene root");
  Physics.SyncTransforms();var anchors=UnityEngine.Object.FindObjectsOfType<SpotlightAnchor>();
  foreach(var a in anchors.Where(a=>a.role=="PlayerSpawn"||a.role=="EnemyEntrance"||a.role=="Patrol"))if(Physics.CheckCapsule(a.transform.position+Vector3.up*.45f,a.transform.position+Vector3.up*1.4f,.35f,1<<8))throw new Exception("Blocked anchor "+a.name);
  foreach(var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())if(!r.sharedMaterial||!r.GetComponent<MeshFilter>().sharedMesh)throw new Exception("Missing geometry "+r.name);
  Debug.Log("[SpotlightBuilder] VALIDATION PASS anchors="+anchors.Length);
 }
 static void Render(Camera cam,int width,int height,string path) {
  var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);rt.antiAliasing=4;cam.targetTexture=rt;cam.aspect=(float)width/height;cam.Render();var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());RenderTexture.active=old;cam.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);
 }
 [MenuItem("Spotlight/Capture Previews")]
 public static void CaptureAll() {
  Directory.CreateDirectory("Audit/Previews");var cam=Camera.main;var pos=cam.transform.position;var rot=cam.transform.rotation;float size=cam.orthographicSize;
  Render(cam,1920,1080,"Audit/Previews/Ecostation_Overview.png");
  cam.orthographicSize=9.8f;cam.transform.position=V(14,19,-20);cam.transform.LookAt(V(0,0,2));Render(cam,1080,1920,"Audit/Previews/Ecostation_Portrait.png");
  cam.orthographicSize=8;cam.transform.position=V(12,15,-12);cam.transform.LookAt(V(0,1,5));Render(cam,1600,1200,"Audit/Previews/Ecostation_Detail.png");
  cam.transform.position=V(0,40,0);cam.transform.rotation=Quaternion.Euler(90,0,0);cam.orthographicSize=14;Render(cam,1600,1400,"Audit/Previews/Ecostation_Top.png");cam.transform.position=pos;cam.transform.rotation=rot;cam.orthographicSize=size;cam.ResetAspect();
 }
}
