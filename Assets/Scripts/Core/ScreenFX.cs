using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// "通透世界" screen filter for the parry slow-mo: darkened, desaturated, cold vignette plus an expanding pulse ring.
    /// Also muffles world audio through the listener's low-pass filter (clean SFX bypass it).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ScreenFX : MonoBehaviour
    {
        public static ScreenFX I;
        public Material mat;
        float _amount, _pulse = 2f;
        AudioLowPassFilter _lp;

        void Awake()
        {
            I = this;
            _lp = GetComponent<AudioLowPassFilter>();
            if (!mat) mat = Resources.Load<Material>("Materials/ScreenFX");
        }

        public void Pulse() => _pulse = 0f;

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float target = TimeManager.SlowMo ? 1f : 0f;
            _amount = Mathf.MoveTowards(_amount, target, dt * (target > _amount ? 5f : 3f));
            _pulse += dt * 1.6f;
            if (_lp) _lp.cutoffFrequency = Mathf.Lerp(22000f, 700f, _amount);
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (!mat || (_amount < 0.001f && _pulse > 1.5f)) { Graphics.Blit(src, dst); return; }
            mat.SetFloat("_Amount", _amount);
            mat.SetFloat("_Pulse", _pulse);
            Graphics.Blit(src, dst, mat);
        }
    }
}
