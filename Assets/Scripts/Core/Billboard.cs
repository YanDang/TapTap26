using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// Keeps a sprite parallel to the (orthographic) camera and pulls it toward the camera along the view
    /// direction, so it never sinks into the ground. With an ortho camera this does not change size or screen position.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        public Transform anchor;          // world point the sprite pivot sits on (usually the character root)
        public Vector3 worldOffset;       // e.g. lift above the ground
        public float towardCamera = 2f;
        public float roll;                // in-plane rotation (deg), used by VFX

        void LateUpdate()
        {
            var cam = Camera.main;
            if (!cam) return;
            var basePos = (anchor ? anchor.position : transform.position) + worldOffset;
            transform.SetPositionAndRotation(basePos - cam.transform.forward * towardCamera,
                cam.transform.rotation * Quaternion.Euler(0, 0, roll));
        }
    }
}
