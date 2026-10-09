using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class SpotlightQA
{
    const string ScenePath = "Assets/SpotlightScene/Scenes/Nirun_Ecostation.unity";
    const string Pending = "SpotlightQA.Pending", Deadline = "SpotlightQA.Deadline";
    static readonly List<string> lines = new List<string>();
    static int failures;
    static double runtimeStart = -1;
    static float fixedStart;
    static Component[] motions;
    static Vector3[] positions;
    static SpotlightQA() { EditorApplication.update += Tick; }
    static string Audit { get { return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Audit"); } }
    static void Check(bool ok, string detail) { lines.Add((ok ? "PASS " : "FAIL ") + detail); if (!ok) failures++; Debug.Log("[SpotlightQA] " + lines.Last()); }
    static void Begin() { lines.Clear(); failures = 0; }
    static void Save(string name) { Directory.CreateDirectory(Audit); lines.Add("RESULT=" + (failures == 0 ? "PASS" : "FAIL") + "; failures=" + failures); File.WriteAllLines(Path.Combine(Audit, name + ".txt"), lines); }
    static Component[] Components() { return UnityEngine.Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(x => x != null && x.gameObject.scene.IsValid()).ToArray(); }
    static string Role(Component c) { var f = c.GetType().GetField("role"); return f == null ? "" : Convert.ToString(f.GetValue(c)); }
    static bool Clear(Vector3 p) { return !Physics.CheckCapsule(p + Vector3.up * .45f, p + Vector3.up * 1.4f, .35f, 1 << 8, QueryTriggerInteraction.Ignore); }
    static void Route(string name, Vector3[] points)
    {
        int blocked=0, samples=0;
        for(int j=1;j<points.Length;j++) { int n=Mathf.CeilToInt(Vector3.Distance(points[j-1],points[j])/.25f); for(int i=0;i<=n;i++) { var p=Vector3.Lerp(points[j-1],points[j],(float)i/n); samples++; if(!Clear(p)) blocked++; } }
        Check(blocked==0, "Major route " + name + " samples=" + samples + " blocked=" + blocked);
    }
    static void Validate(bool open)
    {
        Check(File.Exists(ScenePath), "Saved scene exists: " + ScenePath);
        if (!File.Exists(ScenePath)) return;
        if (open) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Check(scene.name == "Nirun_Ecostation", "Active scene=" + scene.name);
        Check(scene.GetRootGameObjects().Any(g => g.name == "Nirun_Ecostation_Root"), "Expected scene root");
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        int missing = all.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        Check(missing == 0, "Missing scripts=" + missing + "; GameObjects=" + all.Length);
        foreach (var r in all.SelectMany(t => t.GetComponents<Renderer>())) Check(r.sharedMaterials.Length > 0 && r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.name != "Hidden/InternalErrorShader"), "Renderer materials/shader: " + r.name);
        foreach (var m in all.SelectMany(t => t.GetComponents<MeshFilter>())) Check(m.sharedMesh != null, "MeshFilter mesh: " + m.name);
        foreach (var m in all.SelectMany(t => t.GetComponents<SkinnedMeshRenderer>())) Check(m.sharedMesh != null, "Skinned mesh: " + m.name);
        foreach (var m in all.SelectMany(t => t.GetComponents<MeshCollider>())) Check(m.sharedMesh != null, "MeshCollider mesh: " + m.name);
        var anchors = Components().Where(c => c.GetType().Name == "SpotlightAnchor").ToArray();
        foreach (string role in new [] { "PlayerSpawn", "EnemyEntrance", "BuildZone", "Resource", "Repair", "Patrol" }) { int n = anchors.Count(a => Role(a) == role); Check(role == "PlayerSpawn" ? n == 1 : role == "EnemyEntrance" ? n == 3 : n > 0, "Anchor " + role + " count=" + n); }
        foreach (var a in anchors) { var f = a.GetType().GetField("size"); Check(f != null && f.FieldType == typeof(Vector3), "Anchor size contract: " + a.name); }
        Vector3[] expected = { new Vector3(0,0,-5), new Vector3(-13,0,-6), new Vector3(13,0,-6), new Vector3(0,0,11) };
        foreach (var p in expected) Check(anchors.Any(a => Role(a) == (p == expected[0] ? "PlayerSpawn" : "EnemyEntrance") && Vector3.Distance(a.transform.position,p) < .01f), "Anchor expected position=" + p);
        Physics.SyncTransforms();
        foreach (var p in expected) { Check(Clear(p), "Spawn/entrance obstacle-free=" + p); RaycastHit hit; bool grounded = Physics.Raycast(p+Vector3.up*2, Vector3.down,out hit, 3,1<<9,QueryTriggerInteraction.Ignore); Check(grounded && Mathf.Abs(hit.point.y) < .025f, "Walkable layer9 raycast at " + p + " hit=" + grounded + " y=" + (grounded ? hit.point.y.ToString("F4") : "N/A")); }
        int blocked=0; for(int i=0;i<=24;i++) if(!Clear(new Vector3(0,0,-5+i*.25f))) blocked++;
        Check(blocked==0,"Central front route x=0,z=-5..1 radius=.35 blocked samples="+blocked);
        Route("West entrance to plaza", new [] { expected[1], expected[0], new Vector3(0,0,-1) });
        Route("East entrance to plaza", new [] { expected[2], expected[0], new Vector3(0,0,-1) });
        Route("North entrance east bypass to plaza", new [] { expected[3], new Vector3(5,0,11), new Vector3(5,0,-3), new Vector3(0,0,-1) });
        foreach(var a in anchors.Where(a=>Role(a)=="Patrol")) Check(Clear(a.transform.position), "Patrol obstacle-free: "+a.name);
        var ground = all.FirstOrDefault(t => t.name == "Ground");
        Check(ground != null, "Ground object exists");
        if(ground != null) { var c=ground.GetComponent<Collider>(); Check(c != null && c.gameObject.layer==9 && Mathf.Abs(c.bounds.min.y+.25f)<.025f && Mathf.Abs(c.bounds.max.y)<.025f,"Ground bounds bottom=-.25 top=0, walkable layer9: "+(c==null?"no collider":c.bounds.ToString())); }
        var camera = all.SelectMany(t=>t.GetComponents<Camera>()).FirstOrDefault(c=>c.name=="Main Camera"); Check(camera!=null && camera.orthographic,"Main Camera orthographic");
        Check(Components().Any(c=>c.GetType().Name=="SpotlightEcoMotion"),"Eco motion components exist");
    }
    public static void RunReadback() { Begin(); try { Validate(true); } catch(Exception e) { Check(false,e.ToString()); } Save("readback-report"); if(failures>0) EditorApplication.Exit(1); }
    public static void RunSmoke()
    {
        Begin(); try { Validate(true); Save("smoke-preflight"); SessionState.SetBool(Pending,true); SessionState.SetString(Deadline,DateTime.UtcNow.AddSeconds(90).Ticks.ToString()); EditorApplication.isPlaying=true; } catch(Exception e) { Check(false,e.ToString()); Save("playmode-report"); EditorApplication.Exit(1); }
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Pending,false)) return;
        long ticks; if(!long.TryParse(SessionState.GetString(Deadline,"0"),out ticks) || DateTime.UtcNow.Ticks > ticks) { Begin(); Check(false,"Play Mode watchdog timeout"); Finish(); return; }
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try {
            if(runtimeStart<0) { Begin(); motions=Components().Where(c=>c.GetType().Name=="SpotlightEcoMotion").ToArray(); positions=motions.Select(c=>c.transform.position).ToArray(); fixedStart=Time.fixedTime; runtimeStart=EditorApplication.timeSinceStartup; return; }
            if(EditorApplication.timeSinceStartup-runtimeStart<1.2 || Time.fixedTime-fixedStart<.8f) return;
            Validate(false);
            Check(motions.Length>0,"Runtime motion count="+motions.Length);
            for(int i=0;i<motions.Length;i++) Check(motions[i]!=null && Vector3.Distance(positions[i],motions[i].transform.position)>.0001f,"Runtime motion actual change: "+(motions[i]==null?"destroyed":motions[i].name)+" delta="+(motions[i]==null?0:Vector3.Distance(positions[i],motions[i].transform.position)));
            Check(Time.fixedTime-fixedStart>=.8f,"Physics time advanced="+(Time.fixedTime-fixedStart)); Finish();
        } catch(Exception e) { Check(false,e.ToString()); Finish(); }
    }
    static void Finish() { Save("playmode-report"); SessionState.EraseBool(Pending); SessionState.EraseString(Deadline); EditorApplication.isPlaying=false; int exit=failures==0?0:1; EditorApplication.delayCall += ()=>EditorApplication.Exit(exit); }
}
