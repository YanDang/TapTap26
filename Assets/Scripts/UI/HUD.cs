using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameJam
{
    /// <summary>Top-left: dodge + parry icons (grey outside combat, white in combat, radial cooldown) and hearts.
    /// Enemy HP bars and the "处决!" prompt follow enemies. Bottom: control hint.</summary>
    public class HUD : MonoBehaviour
    {
        public static HUD I;
        Font _font;
        Image _dodge, _dodgeCd, _parry, _parryCd;
        Text _dodgeTxt, _combatTxt;
        readonly List<Image> _hearts = new List<Image>();
        readonly Dictionary<EnemyAI, (RectTransform root, Image fill, Text label)> _bars =
            new Dictionary<EnemyAI, (RectTransform, Image, Text)>();
        RectTransform _barLayer;

        public static HUD Build()
        {
            var go = new GameObject("HUD", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var h = go.AddComponent<HUD>();
            h.Create();
            return h;
        }

        void Awake() { I = this; }

        void Create()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _barLayer = NewRect("Bars", transform);
            _barLayer.anchorMin = Vector2.zero; _barLayer.anchorMax = Vector2.one;
            _barLayer.offsetMin = _barLayer.offsetMax = Vector2.zero;

            _dodge = Icon("Dodge", "ui_icon_dodge", new Vector2(28, -28), 104, out _dodgeCd);
            _parry = Icon("Parry", "ui_icon_parry", new Vector2(146, -28), 104, out _parryCd);
            _dodgeTxt = Label(_dodge.transform, "", 34, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(104, 104));
            Label(_dodge.transform, "闪避", 20, TextAnchor.UpperCenter, new Vector2(0, -60), new Vector2(104, 30));
            Label(_parry.transform, "格挡", 20, TextAnchor.UpperCenter, new Vector2(0, -60), new Vector2(104, 30));
            _combatTxt = Label(transform, "", 26, TextAnchor.UpperLeft, Vector2.zero, new Vector2(300, 40));
            Anchor(_combatTxt.rectTransform, new Vector2(266, -64));
            for (int i = 0; i < 5; i++)
            {
                var im = Img("Heart" + i, "ui_heart", transform, new Vector2(40, 40), new Color(1f, 0.3f, 0.4f));
                Anchor(im.rectTransform, new Vector2(30 + i * 46, -176));
                _hearts.Add(im);
            }
            var hint = Label(transform, "右键：移动 / 点敌人锁定战斗      左键：踢家具 · 格挡 · 处决      战斗中右键地面：闪避 + 精灵之箭",
                             22, TextAnchor.LowerCenter, Vector2.zero, new Vector2(1600, 40));
            var r = hint.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0); r.pivot = new Vector2(0.5f, 0); r.anchoredPosition = new Vector2(0, 18);
            hint.color = new Color(1, 1, 1, 0.75f);
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
            r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = pos;
        }

        Image Img(string n, string sprite, Transform parent, Vector2 size, Color c)
        {
            var r = NewRect(n, parent);
            var im = r.gameObject.AddComponent<Image>();
            im.sprite = Resources.Load<Sprite>("UI/" + sprite); im.color = c; im.raycastTarget = false;
            r.sizeDelta = size;
            return im;
        }

        Text Label(Transform parent, string s, int size, TextAnchor a, Vector2 pos, Vector2 box)
        {
            var r = NewRect("Text", parent);
            var t = r.gameObject.AddComponent<Text>();
            t.font = _font; t.fontSize = size; t.alignment = a; t.text = s; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var o = r.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.8f);
            r.sizeDelta = box; r.anchoredPosition = pos;
            return t;
        }

        Image Icon(string n, string sprite, Vector2 pos, float size, out Image cd)
        {
            var im = Img(n, sprite, transform, new Vector2(size, size), Color.white);
            Anchor(im.rectTransform, pos);
            cd = Img(n + "CD", "ui_circle", im.transform, new Vector2(size - 10, size - 10), new Color(0, 0, 0, 0.65f));
            cd.type = Image.Type.Filled; cd.fillMethod = Image.FillMethod.Radial360;
            cd.fillOrigin = (int)Image.Origin360.Top; cd.fillClockwise = false; cd.fillAmount = 0;
            return im;
        }

        // ---------------------------------------------------------------- update
        void Update()
        {
            var p = GameManager.I ? GameManager.I.player : null;
            if (!p) return;
            var grey = new Color(0.5f, 0.5f, 0.5f, 0.75f);
            _dodge.color = p.InCombat ? Color.white : grey;      // dodge only works in combat
            _parry.color = p.InCombat ? Color.white : grey;      // parry only works in combat
            _dodgeCd.fillAmount = p.DodgeRemaining / p.dodgeCooldown;
            _dodgeTxt.text = p.DodgeRemaining > 0 ? Mathf.CeilToInt(p.DodgeRemaining).ToString() : "";
            _parryCd.fillAmount = p.ParryRemaining / p.parryCooldown;
            _combatTxt.text = p.InCombat ? "战斗中" : "";
            _combatTxt.color = new Color(1f, 0.45f, 0.4f);
            for (int i = 0; i < _hearts.Count; i++) _hearts[i].enabled = i < p.hp;
            UpdateBars();
        }

        void UpdateBars()
        {
            var cam = Camera.main;
            foreach (var e in EnemyAI.All)
            {
                if (e.Dead) continue;
                if (!_bars.ContainsKey(e))
                {
                    var root = NewRect("Bar", _barLayer);
                    root.sizeDelta = new Vector2(84, 10);
                    var bg = root.gameObject.AddComponent<Image>(); bg.color = new Color(0, 0, 0, 0.6f); bg.raycastTarget = false;
                    var fill = Img("Fill", "ui_circle", root, new Vector2(80, 6), e.kind == EnemyKind.Beast ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.75f, 0.3f));
                    fill.sprite = null;
                    fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0, 0.5f);
                    fill.rectTransform.pivot = new Vector2(0, 0.5f);
                    fill.rectTransform.anchoredPosition = new Vector2(2, 0);
                    var label = Label(root, "", 26, TextAnchor.LowerCenter, new Vector2(0, 26), new Vector2(200, 36));
                    _bars[e] = (root, fill, label);
                }
            }
            var dead = new List<EnemyAI>();
            foreach (var kv in _bars)
            {
                var e = kv.Key;
                if (!e || e.Dead) { if (kv.Value.root) Destroy(kv.Value.root.gameObject); dead.Add(e); continue; }
                kv.Value.root.position = cam.WorldToScreenPoint(e.Pos + Vector3.up * 2.25f);
                kv.Value.fill.rectTransform.sizeDelta = new Vector2(80f * e.hp / e.maxHp, 6);
                bool pending = e.state == EnemyAI.State.Pending;
                kv.Value.label.text = pending ? "处决!" : (e.state == EnemyAI.State.Stunned ? "眩晕" : "");
                kv.Value.label.color = pending ? new Color(1f, 0.85f, 0.3f) : Color.white;
                kv.Value.label.transform.localScale = Vector3.one * (pending ? 1f + 0.15f * Mathf.Sin(Time.unscaledTime * 12f) : 1f);
            }
            foreach (var e in dead) _bars.Remove(e);
        }
    }
}
