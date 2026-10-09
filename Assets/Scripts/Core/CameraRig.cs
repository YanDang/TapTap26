using UnityEngine;

namespace GameJam
{
    /// <summary>Follows the player keeping the scene's fixed oblique ortho view; screen shake on unscaled time.</summary>
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig I;
        public Transform target;
        public float orthoSize = 5.5f;
        public float follow = 8f;
        Vector3 _offset;
        float _shakeAmp, _shakeDur, _shakeT;
        Vector3 _base;

        void Awake()
        {
            I = this;
            var cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            // offset = camera position minus the ground point it looks at
            var ray = new Ray(transform.position, transform.forward);
            float t = -ray.origin.y / ray.direction.y;
            _offset = transform.position - ray.GetPoint(t);
            _base = transform.position;
        }

        public void Snap()
        {
            if (target) _base = target.position + _offset;
            transform.position = _base;
        }

        /// <summary>amp in metres: ~0.15 small, ~0.3 medium, ~0.6 big.</summary>
        public void Shake(float amp, float dur)
        {
            if (amp >= _shakeAmp * Mathf.Clamp01(1 - _shakeT / Mathf.Max(_shakeDur, 1e-3f)))
            {
                _shakeAmp = amp; _shakeDur = dur; _shakeT = 0;
            }
        }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (target) _base = Vector3.Lerp(_base, target.position + _offset, 1 - Mathf.Exp(-follow * dt));
            Vector3 shake = Vector3.zero;
            if (_shakeT < _shakeDur)
            {
                _shakeT += dt;
                float k = _shakeAmp * (1 - _shakeT / _shakeDur);
                float n = Time.unscaledTime * 38f;
                shake = transform.right * (Mathf.PerlinNoise(n, 0.3f) - 0.5f) * 2 * k
                      + transform.up * (Mathf.PerlinNoise(0.7f, n) - 0.5f) * 2 * k;
            }
            transform.position = _base + shake;
        }
    }
}
