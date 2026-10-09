using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    public enum Dir { Down, Left, Right, Up }

    /// <summary>All sliced frames of one character (or the VFX library). Built by GameJam/Build All.</summary>
    [CreateAssetMenu(menuName = "GameJam/Sprite Set")]
    public class SpriteSet : ScriptableObject
    {
        [Serializable]
        public class Clip
        {
            public string name;
            public float fps = 12;
            public bool loop;
            public string[] dirs;      // row order in the sheet
            public int frames;
            public Sprite[] sprites;   // row-major: dirs.Length * frames
        }

        public List<Clip> clips = new List<Clip>();
        Dictionary<string, Clip> _map;

        public Clip Get(string clipName)
        {
            if (_map == null)
            {
                _map = new Dictionary<string, Clip>();
                foreach (var c in clips) _map[c.name] = c;
            }
            _map.TryGetValue(clipName, out var clip);
            return clip;
        }

        static readonly string[] DirNames = { "down", "left", "right", "up" };

        /// <summary>Row for a direction; clips rendered only left/right map down/up onto the last horizontal side.</summary>
        public static int Row(Clip c, Dir d, Dir lastHorizontal)
        {
            int i = Array.IndexOf(c.dirs, DirNames[(int)d]);
            if (i >= 0) return i;
            i = Array.IndexOf(c.dirs, DirNames[(int)lastHorizontal]);
            return i >= 0 ? i : 0;
        }
    }

    public static class DirUtil
    {
        /// <summary>Screen-relative 4-way direction of a world-space XZ vector (Up = away from the camera).</summary>
        public static Dir FromWorld(Vector3 v)
        {
            var cam = Camera.main ? Camera.main.transform : null;
            Vector3 f = cam ? cam.forward : Vector3.forward, r = cam ? cam.right : Vector3.right;
            f.y = 0; r.y = 0; f.Normalize(); r.Normalize();
            float x = Vector3.Dot(v, r), y = Vector3.Dot(v, f);
            if (Mathf.Abs(x) >= Mathf.Abs(y)) return x > 0 ? Dir.Right : Dir.Left;
            return y > 0 ? Dir.Up : Dir.Down;
        }

        /// <summary>Angle (deg, counter-clockwise from screen right) of a world XZ vector as seen on screen.</summary>
        public static float ScreenAngle(Vector3 v)
        {
            var cam = Camera.main ? Camera.main.transform : null;
            Vector3 f = cam ? cam.forward : Vector3.forward, r = cam ? cam.right : Vector3.right;
            f.y = 0; r.y = 0; f.Normalize(); r.Normalize();
            // screen-up for a ground vector is shortened by the camera pitch, but direction is what matters here
            return Mathf.Atan2(Vector3.Dot(v, f), Vector3.Dot(v, r)) * Mathf.Rad2Deg;
        }

        public static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    }
}
