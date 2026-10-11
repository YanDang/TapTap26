using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameJam
{
    /// <summary>
    /// 屏幕下方的物品栏：10 个格子（图标 + 数量 + 数字键），右边是负重，左边是“合成工坊”按钮，上方是选中物品的说明 / 摆放提示。
    /// 格子是动态的：只显示手上有的物品（按 Inventory.Infos 的顺序往前排），合成出新东西会自动出现。
    /// 点格子或按 1–9 / 0 选中物品 → 进入摆放模式（BuildController）；再点一次同一格 / 右键 / Esc 取消。
    /// 鼠标在物品栏上时，点击不会传给主角（GameInput 先问 HandleInput）。
    /// </summary>
    public class InventoryBar : MonoBehaviour
    {
        public static InventoryBar I;
        public const int SlotCount = 10;
        const float SlotSize = 92f, Gap = 8f;

        Font _font;
        RectTransform _bar;
        readonly List<Slot> _slots = new List<Slot>();
        Text _weight, _info;
        RectTransform _workshopBtn;

        class Slot
        {
            public RectTransform rect;
            public Image frame, bg, icon;
            public Text count, key;
            public ItemId? item;
        }

        public static InventoryBar Build(Transform canvas)
        {
            var go = new GameObject("InventoryBar", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var bar = go.AddComponent<InventoryBar>();
            bar.Create();
            return bar;
        }

        void Awake() { I = this; }

        void Create()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _bar = (RectTransform)transform;
            float width = SlotCount * SlotSize + (SlotCount - 1) * Gap + 24f;
            _bar.anchorMin = _bar.anchorMax = new Vector2(0.5f, 0f);
            _bar.pivot = new Vector2(0.5f, 0f);
            _bar.anchoredPosition = new Vector2(0, 14);
            _bar.sizeDelta = new Vector2(width, SlotSize + 24f);
            var panel = gameObject.AddComponent<Image>();
            panel.color = new Color(0.05f, 0.07f, 0.12f, 0.72f);
            panel.raycastTarget = false;

            for (int i = 0; i < SlotCount; i++)
            {
                var s = new Slot();
                s.rect = NewRect("Slot" + (i + 1), _bar);
                s.rect.anchorMin = s.rect.anchorMax = new Vector2(0f, 0.5f);
                s.rect.pivot = new Vector2(0f, 0.5f);
                s.rect.anchoredPosition = new Vector2(12f + i * (SlotSize + Gap), 0f);
                s.rect.sizeDelta = new Vector2(SlotSize, SlotSize);
                s.frame = s.rect.gameObject.AddComponent<Image>();
                s.frame.raycastTarget = false;
                s.bg = Img("Bg", s.rect, new Vector2(SlotSize - 6, SlotSize - 6), new Color(0.16f, 0.2f, 0.28f, 0.95f));
                s.icon = Img("Icon", s.rect, new Vector2(SlotSize - 20, SlotSize - 20), Color.white);
                s.icon.preserveAspect = true;
                s.key = Label(s.rect, (i + 1).ToString(), 18, TextAnchor.UpperLeft, new Vector2(8, -4), new Vector2(SlotSize - 10, SlotSize - 8));
                s.count = Label(s.rect, "", 24, TextAnchor.LowerRight, new Vector2(-8, 4), new Vector2(SlotSize - 10, SlotSize - 8));
                _slots.Add(s);
            }

            _weight = Label(_bar, "", 22, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(260, 60));
            var wr = _weight.rectTransform;
            wr.anchorMin = wr.anchorMax = new Vector2(1f, 0.5f); wr.pivot = new Vector2(0f, 0.5f);
            wr.anchoredPosition = new Vector2(14, 0);

            // 合成工坊按钮（物品栏左边）：打开程序同学的采集 / 合成工坊（CraftingBridge）
            _workshopBtn = NewRect("WorkshopButton", _bar);
            _workshopBtn.anchorMin = _workshopBtn.anchorMax = new Vector2(0f, 0.5f);
            _workshopBtn.pivot = new Vector2(1f, 0.5f);
            _workshopBtn.anchoredPosition = new Vector2(-12f, 0f);
            _workshopBtn.sizeDelta = new Vector2(132f, SlotSize);
            var wb = _workshopBtn.gameObject.AddComponent<Image>();
            wb.color = new Color(0.12f, 0.42f, 0.45f, 0.92f); wb.raycastTarget = false;
            var wt = Label(_workshopBtn, "合成工坊\n<size=18>(B)</size>", 24, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(132f, SlotSize));
            wt.supportRichText = true;

            _info = Label(_bar, "", 22, TextAnchor.LowerCenter, Vector2.zero, new Vector2(1400, 60));
            var ir = _info.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 1f); ir.pivot = new Vector2(0.5f, 0f);
            ir.anchoredPosition = new Vector2(0, 8);
        }

        RectTransform NewRect(string n, Transform parent)
        {
            var go = new GameObject(n, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        Image Img(string n, Transform parent, Vector2 size, Color c)
        {
            var r = NewRect(n, parent);
            r.sizeDelta = size;
            var im = r.gameObject.AddComponent<Image>();
            im.color = c; im.raycastTarget = false;
            return im;
        }

        Text Label(Transform parent, string s, int size, TextAnchor a, Vector2 pos, Vector2 box)
        {
            var r = NewRect("Text", parent);
            var t = r.gameObject.AddComponent<Text>();
            t.font = _font; t.fontSize = size; t.alignment = a; t.text = s; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var o = r.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.85f);
            r.sizeDelta = box; r.anchoredPosition = pos;
            return t;
        }

        // ---------------------------------------------------------------- 输入
        public bool MouseOver => _bar && (RectTransformUtility.RectangleContainsScreenPoint(_bar, Input.mousePosition, null) || OverWorkshop);
        bool OverWorkshop => _workshopBtn && RectTransformUtility.RectangleContainsScreenPoint(_workshopBtn, Input.mousePosition, null);

        /// <summary>GameInput 每帧先调它。返回 true = 这次输入被物品栏吃掉了（不再传给主角 / 摆放模式）。</summary>
        public bool HandleInput()
        {
            for (int i = 0; i < SlotCount; i++)
                if (Input.GetKeyDown(i < 9 ? KeyCode.Alpha1 + i : KeyCode.Alpha0)) { Toggle(i); return true; }
            if (!MouseOver) return false;
            if (OverWorkshop && Input.GetMouseButtonDown(0)) { CraftingBridge.Open(); return true; }
            if (Input.GetMouseButtonDown(0))
                for (int i = 0; i < _slots.Count; i++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(_slots[i].rect, Input.mousePosition, null)) { Toggle(i); break; }
            if (Input.GetMouseButtonDown(1) && BuildController.I) BuildController.I.Cancel();
            return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonUp(0);
        }

        void Toggle(int slot)
        {
            var b = BuildController.I;
            if (!b) return;
            var item = _slots[slot].item;
            if (!item.HasValue) { b.Cancel(); return; }
            if (b.Selected == item) b.Cancel();
            else b.Select(item.Value);
        }

        // ---------------------------------------------------------------- 显示
        void Update()
        {
            var inv = Inventory.I;
            var b = BuildController.I;
            if (inv == null) return;
            // 只显示手上有的物品（选中的那件用完前也留着），按 Infos 顺序往前排
            int k = 0;
            foreach (var info in Inventory.Infos)
            {
                if (k >= _slots.Count) break;
                if (inv.Count(info.id) <= 0 && !(b && b.Selected == info.id)) continue;
                var slot = _slots[k++];
                if (slot.item != info.id) { slot.item = info.id; slot.icon.sprite = Inventory.IconOf(info.id); }
            }
            for (; k < _slots.Count; k++) { _slots[k].item = null; _slots[k].icon.sprite = null; }

            foreach (var s in _slots)
            {
                bool has = s.item.HasValue;
                int n = has ? inv.Count(s.item.Value) : 0;
                bool sel = has && b && b.Selected == s.item;
                s.icon.enabled = has;
                s.icon.color = n > 0 && Inventory.InfoOf(s.item ?? 0).placeable ? Color.white : new Color(1, 1, 1, 0.35f);
                s.count.text = has ? n.ToString() : "";
                s.frame.color = sel ? new Color(1f, 0.85f, 0.3f) : new Color(0.35f, 0.42f, 0.55f, 0.9f);
                s.rect.localScale = Vector3.one * (sel ? 1.06f : 1f);
            }

            int w = inv.Weight, cap = inv.Capacity;
            _weight.text = $"负重 {w} / {cap}" + (inv.Bonus > 0 ? $"\n<size=18>气泡囊 +{inv.Bonus}</size>" : "");
            _weight.supportRichText = true;
            _weight.color = w > cap ? new Color(1f, 0.4f, 0.35f) : Color.white;

            if (b && b.Active)
            {
                var info = Inventory.InfoOf(b.Selected.Value);
                bool warn = !string.IsNullOrEmpty(b.Message);
                _info.text = warn ? b.Message : $"{info.name}：{info.desc}　　{b.Hint}";
                _info.color = warn ? new Color(1f, 0.5f, 0.45f) : new Color(1f, 0.92f, 0.7f);
            }
            else
            {
                _info.text = "按 1–9 / 0 或点格子：摆放家具　　B / 左边按钮：去合成工坊（采集材料、合成家具）";
                _info.color = new Color(1, 1, 1, 0.55f);
            }
        }
    }
}
