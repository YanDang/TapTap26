using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>Left-click next to it: the table flies away from the player, knocks back enemies on its path and
    /// on landing deals area damage, stuns everyone nearby and shakes the screen. Respawns at home after a while.</summary>
    public class KickableTable : MonoBehaviour
    {
        public static readonly List<KickableTable> All = new List<KickableTable>();
        public float flyDistance = 5.5f, flyTime = 0.55f, landRadius = 3.5f, stunSeconds = 2f, respawnSeconds = 6f;
        public int pathDamage = 1, landDamage = 2;

        enum S { Idle, Flying, Gone }
        S _s;
        Vector3 _home, _start, _end, _dir;
        float _t;
        Transform _visual;
        BoxCollider _col;
        readonly HashSet<EnemyAI> _hit = new HashSet<EnemyAI>();
        public bool IsIdle => _s == S.Idle;
        public int landings;

        public static KickableTable Build(Vector3 pos, float yaw)
        {
            var root = new GameObject("KickableTable");
            root.layer = 11;
            root.transform.SetPositionAndRotation(DirUtil.Flat(pos), Quaternion.Euler(0, yaw, 0));
            var t = root.AddComponent<KickableTable>();
            t._col = root.AddComponent<BoxCollider>();
            t._col.size = new Vector3(1.3f, 0.8f, 0.8f); t._col.center = new Vector3(0, 0.4f, 0);
            t._visual = new GameObject("Visual").transform;
            t._visual.SetParent(root.transform, false);
            var wood = new Material(Shader.Find("Standard")) { color = new Color(0.55f, 0.36f, 0.2f) };
            wood.SetFloat("_Glossiness", 0.15f);
            Part(t._visual, new Vector3(0, 0.74f, 0), new Vector3(1.3f, 0.1f, 0.8f), wood);
            foreach (var x in new[] { -0.55f, 0.55f })
                foreach (var z in new[] { -0.3f, 0.3f })
                    Part(t._visual, new Vector3(x, 0.35f, z), new Vector3(0.09f, 0.7f, 0.09f), wood);
            return t;
        }

        static void Part(Transform parent, Vector3 pos, Vector3 size, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = size;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        void Awake() { All.Add(this); }
        void Start() { _home = transform.position; }
        void OnDestroy() { All.Remove(this); }

        public static KickableTable Nearest(Vector3 p, float range)
        {
            KickableTable best = null; float bd = range;
            foreach (var t in All)
            {
                if (!t.IsIdle) continue;
                float d = DirUtil.Flat(t.transform.position - p).magnitude - 0.5f;   // measure to the table edge
                if (d < bd) { bd = d; best = t; }
            }
            return best;
        }

        public void Kick(Vector3 dir)
        {
            if (_s != S.Idle) return;
            _dir = DirUtil.Flat(dir).normalized;
            _start = transform.position;
            float dist = flyDistance;
            if (Physics.Raycast(_start + Vector3.up * 0.5f, _dir, out var hit, flyDistance, 1 << 8)) dist = Mathf.Max(0.5f, hit.distance - 0.6f);
            _end = _start + _dir * dist;
            _t = 0; _s = S.Flying; _hit.Clear();
            _col.enabled = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_s == S.Flying)
            {
                _t += dt;
                float u = Mathf.Clamp01(_t / flyTime);
                var p = Vector3.Lerp(_start, _end, u);
                p.y = 0.9f * 4 * u * (1 - u);
                transform.position = p;
                _visual.Rotate(Vector3.Cross(Vector3.up, _dir), 900f * dt, Space.World);
                foreach (var e in EnemyAI.All.ToArray())
                {
                    if (e.Dead || _hit.Contains(e)) continue;
                    if (DirUtil.Flat(e.Pos - p).magnitude < 1.0f)
                    {
                        _hit.Add(e);
                        e.TakeDamage(pathDamage, _dir, 0.9f, true);
                    }
                }
                if (u >= 1) Land();
            }
            else if (_s == S.Gone && _t > 0)
            {
                _t -= dt;
                if (_t <= 0)
                {
                    transform.position = _home;
                    _visual.localRotation = Quaternion.identity;
                    _visual.gameObject.SetActive(true);
                    _col.enabled = true;
                    _s = S.Idle;
                }
            }
        }

        void Land()
        {
            landings++;
            var p = DirUtil.Flat(transform.position);
            foreach (var e in EnemyAI.All.ToArray())
            {
                if (e.Dead) continue;
                if (DirUtil.Flat(e.Pos - p).magnitude <= landRadius)
                {
                    e.TakeDamage(landDamage, e.Pos - p, 0.5f, true);
                    e.Stun(stunSeconds);
                }
            }
            CameraRig.I?.Shake(0.5f, 0.4f);
            Sfx.Play("sfx_table_land");
            Vfx.Spawn("VFX_ArrowHit", p + Vector3.up * 0.4f, 0, 1.6f, false, null, new Color(1f, 0.8f, 0.55f));
            _visual.gameObject.SetActive(false);
            _s = S.Gone; _t = respawnSeconds;
        }
    }
}
