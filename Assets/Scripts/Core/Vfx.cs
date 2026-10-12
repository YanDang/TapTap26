using UnityEngine;

namespace GameJam
{
    /// <summary>Spawns flipbook VFX (additive billboards) from the VFX sprite set.</summary>
    public static class Vfx
    {
        static SpriteSet _set;
        static Material _add;

        public static GameObject Spawn(string clip, Vector3 pos, float roll = 0, float scale = 1, bool unscaled = false,
                                       Transform follow = null, Color? tint = null, float speed = 1)
        {
            if (!_set) _set = Resources.Load<SpriteSet>("SpriteSets/VFX");
            if (!_add) _add = Resources.Load<Material>("Materials/SpriteAdditive");
            var go = new GameObject("VFX_" + clip);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = _add;
            if (tint.HasValue) sr.color = tint.Value;
            go.transform.localScale = Vector3.one * scale;
            var anim = go.AddComponent<SpriteAnimator>();
            anim.set = _set; anim.sr = sr; anim.unscaled = unscaled;
            anim.Play(clip, Dir.Down, true, speed);
            var bb = go.AddComponent<Billboard>();
            bb.towardCamera = 3f; bb.roll = roll;
            if (follow) { bb.anchor = follow; bb.worldOffset = pos - follow.position; }
            else
            {
                var a = new GameObject("VFXAnchor").transform;
                a.position = pos;
                bb.anchor = a;
                go.AddComponent<DestroyWith>().other = a.gameObject;
            }
            if (anim.Clip != null && !anim.Clip.loop) go.AddComponent<AutoDestroyWhenDone>().anim = anim;
            return go;
        }
    }

    public class AutoDestroyWhenDone : MonoBehaviour
    {
        public SpriteAnimator anim;
        void Update() { if (anim && anim.Finished) Destroy(gameObject); }
    }

    public class DestroyWith : MonoBehaviour
    {
        public GameObject other;
        void OnDestroy() { if (other) Destroy(other); }
    }
}
