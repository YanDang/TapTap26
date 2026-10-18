using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

/// <summary>Windows PlayerPrefs snapshot: registry names, native types and raw bytes (not guessed GetString conversions).
/// Only TAP_* values are owned. New TAP_* values are removed on restore. Call after scene teardown and PlayerPrefs.Save.
/// A durable JSON journal permits recovery after a killed process. Never DeleteAll.</summary>
public sealed class TapPrefsSnapshot
{
    [Serializable] public sealed class Value { public string name; public uint type; public byte[] bytes; }
    [Serializable] public sealed class Journal { public string path; public List<Value> values = new List<Value>(); }
    public Journal Data;
    static readonly IntPtr HKCU = new IntPtr(unchecked((int)0x80000001));
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode)] static extern int RegOpenKeyEx(IntPtr key, string path, uint options, int access, out IntPtr result);
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode)] static extern int RegCreateKeyEx(IntPtr key, string path, int reserved, string cls, int options, int access, IntPtr security, out IntPtr result, out int disposition);
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode)] static extern int RegEnumValue(IntPtr key, uint index, StringBuilder name, ref uint length, IntPtr reserved, out uint type, byte[] data, ref uint size);
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode)] static extern int RegSetValueEx(IntPtr key, string name, int reserved, uint type, byte[] data, uint size);
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode)] static extern int RegDeleteValue(IntPtr key, string name);
    [DllImport("advapi32.dll")] static extern int RegCloseKey(IntPtr key);
    static void Check(int result) { if(result != 0) throw new IOException("Registry error " + result); }
    public static string CurrentPath => "Software\\" + (Application.isEditor ? "Unity\\UnityEditor\\" : "") + Application.companyName + "\\" + Application.productName;
    public static TapPrefsSnapshot Capture(string path = null)
    {
        var snapshot = new TapPrefsSnapshot { Data = new Journal { path = path ?? CurrentPath } };
        int status = RegOpenKeyEx(HKCU, snapshot.Data.path, 0, 0x20019, out var key);
        if(status == 2) return snapshot;
        Check(status);
        try {
            for(uint i=0;;i++) {
                uint length=16384, size=0; uint type;
                var name = new StringBuilder(16384);
                status = RegEnumValue(key,i,name,ref length,IntPtr.Zero,out type,null,ref size);
                if(status == 259) break;
                if(status != 0 && status != 234) Check(status);
                var bytes = new byte[size]; length=16384;
                Check(RegEnumValue(key,i,name,ref length,IntPtr.Zero,out type,bytes,ref size));
                if(name.ToString().StartsWith("TAP_", StringComparison.Ordinal) || name.ToString().StartsWith("GJ_UltChoice", StringComparison.Ordinal)) snapshot.Data.values.Add(new Value { name=name.ToString(), type=type, bytes=bytes });
            }
        } finally { RegCloseKey(key); }
        return snapshot;
    }
    public void WriteJournal(string file) { File.WriteAllText(file, JsonUtility.ToJson(Data,true)); }
    public void Restore()
    {
        // Flush Unity's cache BEFORE raw replacement; do not set converted values afterward.
        PlayerPrefs.Save();
        var now = Capture(Data.path);
        Check(RegCreateKeyEx(HKCU,Data.path,0,null,0,0x2001f,IntPtr.Zero,out var key,out _));
        try {
            foreach(var v in now.Data.values) Check(RegDeleteValue(key,v.name));
            foreach(var v in Data.values) Check(RegSetValueEx(key,v.name,0,v.type,v.bytes,(uint)v.bytes.Length));
        } finally { RegCloseKey(key); }
        if(!Matches(Capture(Data.path))) throw new IOException("TAP_* raw restore mismatch");
    }
    public bool Matches(TapPrefsSnapshot other)
    {
        if(Data.values.Count != other.Data.values.Count) return false;
        foreach(var a in Data.values) {
            var b=other.Data.values.Find(v=>v.name==a.name);
            if(b==null || a.type!=b.type || Convert.ToBase64String(a.bytes)!=Convert.ToBase64String(b.bytes)) return false;
        }
        return true;
    }
}
