using UnityEngine;
using UnityEngine.UI;

namespace GameJam
{
    /// <summary>
    /// 攻击倒计时时钟 QTE：怪物攻击前摇时在头顶弹出一个带指针旋转的时钟。
    /// 表盘上有一段随机金黄色判定扇区（Sector），指针转动与攻击前摇严格同步。
    /// 玩家在指针转入黄色扇区区间内点击鼠标左键 / J / Space -> 触发一击必杀绝杀处决！
    /// </summary>
    public class ClockQTE : MonoBehaviour
    {
        public static ClockQTE Current;
        public EnemyController owner;
        public float duration, elapsed, sectorStart, sectorSize;
        bool _resolved, _closing;
        float _closeT;
        RectTransform _rt, _hand, _sector;
        Image _sectorImg;
        public bool Open => !_resolved && !_closing;
        public float HandAngle => Mathf.Clamp01(elapsed / Mathf.Max(duration, 0.01f)) * 360f;
        public bool InSector => HandAngle >= sectorStart && HandAngle <= sectorStart + sectorSize;

        public static ClockQTE Show(EnemyController owner, float duration)
        {
            if (Current) Current.Close();
            Canvas canvas = null;
            var canvases = FindObjectsOfType<Canvas>();
            foreach (var cv in canvases)
            {
                if (cv.renderMode == RenderMode.ScreenSpaceOverlay && cv.isActiveAndEnabled)
                {
                    canvas = cv;
                    break;
                }
            }
            if (canvas == null)
            {
                var cgo = new GameObject("ClockQTECanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = cgo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 999;
                var scaler = cgo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }

            var go = new GameObject("ClockQTE", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var c = go.AddComponent<ClockQTE>();
            c.owner = owner;
            c.duration = duration;
            c.sectorSize = Random.Range(45f, 90f);                      // 随机判定窗口大小
            c.sectorStart = Random.Range(100f, 320f - c.sectorSize);    // 随机扇区起始角度
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
            _sector.localRotation = Quaternion.Euler(0, 0, -sectorStart);  // 顺时针自12点钟方向偏转
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
            im.color = col;
            im.raycastTarget = false;
            im.rectTransform.sizeDelta = size;
            return im;
        }

        /// <summary>玩家点击/出招判定。如果指针在扇形内则返回 true 并触发处决；返回 false 则失败。</summary>
        public bool TryHit()
        {
            if (!Open) return false;
            _resolved = true;
            bool ok = InSector;
            if (_sectorImg != null)
                _sectorImg.color = ok ? Color.white : new Color(1f, 0.15f, 0.1f, 0.9f);
            if (_rt != null)
                _rt.localScale = Vector3.one * (ok ? 1.35f : 1f);

            if (Current == this) Current = null;

            if (ok)
            {
                Close();
            }
            else
            {
                StartCoroutine(FailCloseRoutine());
            }
            return ok;
        }

        private System.Collections.IEnumerator FailCloseRoutine()
        {
            yield return new WaitForSecondsRealtime(0.35f);
            Close();
        }

        public void Close()
        {
            if (Current == this) Current = null;
            if (!_closing) { _closing = true; _closeT = 0.25f; }
        }

        void OnDisable()
        {
            if (Current == this) Current = null;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Update()
        {
            if (!_closing && (owner == null || !owner.IsAlive))
            {
                Close();
                return;
            }

            if (!_resolved && !_closing)
            {
                elapsed += Time.deltaTime;
                // 超时自动判定失败，平滑淡出并销毁
                if (elapsed >= duration)
                {
                    _resolved = true;
                    if (_sectorImg != null) _sectorImg.color = new Color(1f, 0.15f, 0.1f, 0.9f);
                    if (Current == this) Current = null;
                    StartCoroutine(FailCloseRoutine());
                }
            }

            if (_hand) _hand.localRotation = Quaternion.Euler(0, 0, -HandAngle);
            float target = _closing ? 0f : (_resolved ? _rt.localScale.x : 1f);
            if (!_resolved || _closing) _rt.localScale = Vector3.one * Mathf.MoveTowards(_rt.localScale.x, target, Time.unscaledDeltaTime * 8f);
            var cam = Camera.main ?? CameraController.Instance?.GetComponent<Camera>() ?? FindObjectOfType<Camera>();
            if (owner && cam)
            {
                var sp = cam.WorldToScreenPoint(owner.transform.position + Vector3.up * 2.2f);
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
