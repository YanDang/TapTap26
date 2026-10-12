using UnityEngine;
using UnityEngine.UI;

namespace GameJam
{
    /// <summary>
    /// 左上角战斗指示 HUD（来自 gamejam-integration）：
    /// 1. 闪避 (Dodge) 图标 + Radial360 冷却扇形遮罩 + 剩余冷却秒数倒计时 + "闪避" 标签
    /// 2. 格挡 (Parry) 图标 + Radial360 冷却扇形遮罩 + 剩余冷却秒数倒计时 + "格挡" 标签
    /// 3. 脱战状态置灰半透明 (0.5, 0.5, 0.5, 0.75)，进入战斗 (InCombat) 恢复全白高亮并在右侧显示 "战斗中" 提示
    /// 4. 自动全屏自适应 Canvas 架构 (1920x1080 0.5 Match)，代码自建无需手动拖入场景
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public static HUD I { get; private set; }
        public static HUD Instance => I;

        private Font _font;
        private Image _dodge;
        private Image _dodgeCd;
        private Image _parry;
        private Image _parryCd;
        private Text _dodgeTxt;
        private Text _parryTxt;
        private Text _combatTxt;
        private Text _dodgeLabel;
        private Text _parryLabel;

        public static HUD Build()
        {
            if (I != null) return I;

            var go = new GameObject("HUD", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            var h = go.AddComponent<HUD>();
            h.Create();
            return h;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInit()
        {
            if (I == null && Object.FindObjectOfType<PlayerController>() != null)
            {
                Build();
            }
        }

        void Awake()
        {
            if (I == null)
            {
                I = this;
            }
            else if (I != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        public void Create()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 1. 闪避图标 (位置 28, -28，尺寸 104x104)
            _dodge = Icon("Dodge", "ui_icon_dodge", new Vector2(28, -28), 104, out _dodgeCd);
            _dodgeTxt = Label(_dodge.transform, "", 34, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(104, 104));
            _dodgeLabel = Label(_dodge.transform, "闪避", 20, TextAnchor.UpperCenter, new Vector2(0, -60), new Vector2(104, 30));

            // 2. 格挡图标 (位置 146, -28，尺寸 104x104)
            _parry = Icon("Parry", "ui_icon_parry", new Vector2(146, -28), 104, out _parryCd);
            _parryTxt = Label(_parry.transform, "", 34, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(104, 104));
            _parryLabel = Label(_parry.transform, "格挡", 20, TextAnchor.UpperCenter, new Vector2(0, -60), new Vector2(104, 30));

            // 3. 战斗中状态文字 (位置 266, -64，尺寸 300x40)
            _combatTxt = Label(transform, "", 26, TextAnchor.UpperLeft, Vector2.zero, new Vector2(300, 40));
            Anchor(_combatTxt.rectTransform, new Vector2(266, -64));
            _combatTxt.color = new Color(1f, 0.45f, 0.4f);
        }

        // ---------------------------------------------------------------- helpers
        static RectTransform NewRect(string n, Transform parent)
        {
            var go = new GameObject(n, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Anchor(RectTransform r, Vector2 pos)
        {
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = pos;
        }

        Image Img(string n, string sprite, Transform parent, Vector2 size, Color c)
        {
            var r = NewRect(n, parent);
            var im = r.gameObject.AddComponent<Image>();
            im.sprite = Resources.Load<Sprite>("UI/" + sprite);
            im.color = c;
            im.raycastTarget = false;
            r.sizeDelta = size;
            return im;
        }

        Text Label(Transform parent, string s, int size, TextAnchor a, Vector2 pos, Vector2 box)
        {
            var r = NewRect("Text", parent);
            var t = r.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = a;
            t.text = s;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var o = r.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.8f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
            r.sizeDelta = box;
            r.anchoredPosition = pos;
            return t;
        }

        Image Icon(string n, string sprite, Vector2 pos, float size, out Image cd)
        {
            var im = Img(n, sprite, transform, new Vector2(size, size), Color.white);
            Anchor(im.rectTransform, pos);
            cd = Img(n + "CD", "ui_circle", im.transform, new Vector2(size - 10, size - 10), new Color(0, 0, 0, 0.65f));
            cd.type = Image.Type.Filled;
            cd.fillMethod = Image.FillMethod.Radial360;
            cd.fillOrigin = (int)Image.Origin360.Top;
            cd.fillClockwise = false;
            cd.fillAmount = 0;
            return im;
        }

        // ---------------------------------------------------------------- update
        void Update()
        {
            var p = PlayerController.Instance;
            if (p == null) p = FindObjectOfType<PlayerController>();
            if (!p) return;

            bool inCombat = p.InCombat;
            var grey = new Color(0.5f, 0.5f, 0.5f, 0.75f);

            // 脱战置灰，战斗中高亮
            if (_dodge != null) _dodge.color = inCombat ? Color.white : grey;
            if (_parry != null) _parry.color = inCombat ? Color.white : grey;

            // 闪避冷却扇形遮罩与剩余时间
            float dodgeRem = p.DodgeRemaining;
            float dodgeCd = Mathf.Max(p.dodgeCooldown, 0.01f);
            if (_dodgeCd != null)
            {
                _dodgeCd.fillAmount = Mathf.Clamp01(dodgeRem / dodgeCd);
            }
            if (_dodgeTxt != null)
            {
                _dodgeTxt.text = dodgeRem > 0.05f ? Mathf.CeilToInt(dodgeRem).ToString() : "";
            }

            // 格挡冷却扇形遮罩与剩余时间
            float parryRem = p.ParryRemaining;
            float parryCd = Mathf.Max(p.parryCooldown, 0.01f);
            if (_parryCd != null)
            {
                _parryCd.fillAmount = Mathf.Clamp01(parryRem / parryCd);
            }
            if (_parryTxt != null)
            {
                _parryTxt.text = parryRem > 0.05f ? Mathf.CeilToInt(parryRem).ToString() : "";
            }

            // 战斗状态提示
            if (_combatTxt != null)
            {
                _combatTxt.text = inCombat ? "战斗中" : "";
            }
        }

        /// <summary>
        /// 切换 HUD 显示状态（例如进入工坊界面时可暂时隐藏）
        /// </summary>
        public void SetVisible(bool visible)
        {
            var cv = GetComponent<Canvas>();
            if (cv != null) cv.enabled = visible;
        }
    }
}
