using UnityEngine;
using UnityEngine.UI;

namespace GameJam
{
    /// <summary>
    /// Humanoid attack QTE: a small clock above the enemy. The hand sweeps one full turn in the attack's wind-up;
    /// a randomly sized and placed sector is the opening. Click while the hand is inside it -> execution.
    /// Runs on world time so it stays in sync with the (slowed) attack animation.
    /// </summary>
    public class ClockQTE : MonoBehaviour
    {
        public static ClockQTE Current;
        public EnemyAI owner;
        public float duration, elapsed, sectorStart, sectorSize;
        bool _resolved, _closing;
        float _closeT;
        RectTransform _rt, _hand, _sector;
        Image _sectorImg;
        public bool Open => !_resolved && !_closing;
        public float HandAngle => Mathf.Clamp01(elapsed / duration) * 360f;
        public bool InSector => HandAngle >= sectorStart && HandAngle <= sectorStart + sectorSize;

        public static ClockQTE Show(EnemyAI owner, float duration)
        {
            if (Current) Current.Close();
            var go = new GameObject("ClockQTE", typeof(RectTransform));
            go.transform.SetParent(HUD.I.transform, false);
            var c = go.AddComponent<ClockQTE>();
            c.owner = owner; c.duration = duration;
            c.sectorSize = Random.Range(38f, 85f);                      // random opening size
            c.sectorStart = Random.Range(110f, 335f - c.sectorSize);    // random position, never right at the start
            c.Build();
            Current = c;
            return c;
        }

        void Build()
        {
            _rt = (RectTransform)transform;
            _rt.sizeDelta = new Vector2(150, 150);
            Img("Face", "ui_clock_face", new Vector2(150, 150), Color.white);
            _sectorImg = Img("Sector", "ui_circle", new Vector2(118, 118), new Color(1f, 0.78f, 0.25f, 0.85f));
            _sectorImg.type = Image.Type.Filled;
            _sectorImg.fillMethod = Image.FillMethod.Radial360;
            _sectorImg.fillOrigin = (int)Image.Origin360.Top;
            _sectorImg.fillClockwise = true;
            _sectorImg.fillAmount = sectorSize / 360f;
            _sector = _sectorImg.rectTransform;
            _sector.localRotation = Quaternion.Euler(0, 0, -sectorStart);  // clockwise from 12 o'clock
            var hand = Img("Hand", "ui_clock_hand", new Vector2(10, 62), Color.white);
            _hand = hand.rectTransform;
            _hand.pivot = new Vector2(0.5f, 0.06f);
            _hand.anchoredPosition = Vector2.zero;
            _rt.localScale = Vector3.one * 0.2f;
        }

        Image Img(string n, string sprite, Vector2 size, Color col)
        {
            var go = new GameObject(n, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var im = go.AddComponent<Image>();
            im.sprite = Resources.Load<Sprite>("UI/" + sprite);
            im.color = col; im.raycastTarget = false;
            im.rectTransform.sizeDelta = size;
            return im;
        }

        /// <summary>Player's left click. Returns true on a successful hit; either way the clock is spent.</summary>
        public bool TryHit()
        {
            if (!Open) return false;
            _resolved = true;
            bool ok = InSector;
            _sectorImg.color = ok ? Color.white : new Color(1f, 0.15f, 0.1f, 0.9f);
            _rt.localScale = Vector3.one * (ok ? 1.35f : 1f);
            return ok;
        }

        public void Close()
        {
            if (Current == this) Current = null;
            if (!_closing) { _closing = true; _closeT = 0.25f; }
        }

        void Update()
        {
            if (!_closing && (owner == null || owner.Dead)) Close();
            if (!_resolved && !_closing) elapsed += Time.deltaTime;
            if (_hand) _hand.localRotation = Quaternion.Euler(0, 0, -HandAngle);
            float target = _closing ? 0f : (_resolved ? _rt.localScale.x : 1f);
            if (!_resolved || _closing) _rt.localScale = Vector3.one * Mathf.MoveTowards(_rt.localScale.x, target, Time.unscaledDeltaTime * 8f);
            if (owner && Camera.main)
            {
                var sp = Camera.main.WorldToScreenPoint(owner.Pos + Vector3.up * 2.7f);
                _rt.position = sp;
            }
            if (_closing)
            {
                _closeT -= Time.unscaledDeltaTime;
                if (_closeT <= 0) Destroy(gameObject);
            }
        }
    }
}
