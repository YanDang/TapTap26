using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 把 Hero2D 预制体里骨骼 / IK 目标在角色根坐标下的位置、朝向导出到 Temp/hero_rig.txt，写动作时用来定坐标。
public static class Hero2DRigDump
{
    [MenuItem("GameJam/Hero2D/Dump Rig")]
    public static void Dump()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Hero2D/Hero2D.prefab");
        var sb = new StringBuilder();
        foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
        {
            var p = prefab.transform.InverseTransformPoint(t.position);
            var r = prefab.transform.InverseTransformDirection(t.right);
            string path = t.name;
            for (var q = t.parent; q && q != prefab.transform; q = q.parent) path = q.name + "/" + path;
            bool skin = t.GetComponent<UnityEngine.U2D.Animation.SpriteSkin>();
            sb.AppendLine($"{path}\tpos=({p.x:F2},{p.y:F2})\tdir={Mathf.Atan2(r.y, r.x) * Mathf.Rad2Deg:F0}\t{(skin ? "LAYER" : "")}");
        }
        File.WriteAllText("Temp/hero_rig.txt", sb.ToString(), Encoding.UTF8);
        Debug.Log("[Hero2D] rig dumped");
    }

    [MenuItem("GameJam/Squid/Dump Layers")]
    public static void DumpSquid()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Squid/Squid.prefab");
        var sb = new StringBuilder();
        foreach (var r in prefab.GetComponentsInChildren<SpriteRenderer>(true))
            sb.AppendLine($"{r.name}\trect={r.sprite?.rect}\tppu={r.sprite?.pixelsPerUnit}\tpivot={r.sprite?.pivot}\torder={r.sortingOrder}\tlocalPos={r.transform.localPosition}\tscale={r.transform.localScale}");
        foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            if (t.name.Contains("身体")) sb.AppendLine("bone " + t.name + " skin=" + (t.GetComponent<UnityEngine.U2D.Animation.SpriteSkin>() != null));
        File.WriteAllText("Temp/squid_layers.txt", sb.ToString(), Encoding.UTF8);
        Debug.Log("[Squid] layers dumped");
    }
}
