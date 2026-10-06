using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace BiomechanicalCrafting
{
    /// <summary>
    /// 背壳背包单个宫格视图组件 (Shell Slot View)
    /// 承载大小宫格样式渲染、物品图元显示、一笔画选中光环与交互事件
    /// </summary>
    public class ShellSlotView : MonoBehaviour, 
        IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        [Header("Slot Identity")]
        public int slotId;
        public ShellSlotLayout layout;
        public BiomechanicalMaterial currentMaterial;

        [Header("UI Element References")]
        public Image bgImage;
        public Image borderImage;
        public Image itemIconImage;
        public Image elementGlowImage;
        public Image majorBadgeImage;
        public GameObject sequenceBadge;
        public Text sequenceText;
        public GameObject deltaBadge;
        public Text deltaText;

        [Header("Event Callbacks")]
        public Action<ShellSlotView, PointerEventData> onSlotPointerDown;
        public Action<ShellSlotView, PointerEventData> onSlotPointerEnter;
        public Action<ShellSlotView, PointerEventData> onSlotPointerExit;
        public Action<ShellSlotView, PointerEventData> onSlotPointerUp;
        public Action<ShellSlotView, PointerEventData> onSlotBeginDrag;
        public Action<ShellSlotView, PointerEventData> onSlotDrag;
        public Action<ShellSlotView, PointerEventData> onSlotEndDrag;
        public Action<ShellSlotView, PointerEventData> onSlotClick;

        private bool isHighlightedInPath = false;
        private int pathStepNumber = -1;

        /// <summary>
        /// 初始化宫格属性与几何尺寸
        /// </summary>
        public void Setup(ShellSlotLayout slotLayout)
        {
            layout = slotLayout;
            slotId = slotLayout.slotId;

            RectTransform rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = slotLayout.relativePos;
                rt.sizeDelta = new Vector2(slotLayout.size, slotLayout.size);
            }

            // 大小宫格差异化视觉
            if (borderImage != null)
            {
                if (slotLayout.isMajor)
                {
                    // 大格子：高亮暗金/生物脉冲边框
                    borderImage.color = new Color(0.95f, 0.78f, 0.28f, 0.9f);
                }
                else
                {
                    // 小格子：深邃青灰生体金属边框
                    borderImage.color = new Color(0.35f, 0.45f, 0.55f, 0.65f);
                }
            }

            if (majorBadgeImage != null)
            {
                majorBadgeImage.gameObject.SetActive(slotLayout.isMajor);
            }

            SetHighlighted(false, -1);
            SetMaterial(null);
        }

        /// <summary>
        /// 设置当前放入的材料
        /// </summary>
        public void SetMaterial(BiomechanicalMaterial mat)
        {
            currentMaterial = mat;

            if (mat == null)
            {
                if (itemIconImage != null)
                {
                    itemIconImage.gameObject.SetActive(false);
                    itemIconImage.sprite = null;
                }
                if (elementGlowImage != null)
                {
                    elementGlowImage.gameObject.SetActive(false);
                }
            }
            else
            {
                if (itemIconImage != null)
                {
                    itemIconImage.gameObject.SetActive(true);
                    itemIconImage.sprite = mat.GetOrLoadSprite();
                    itemIconImage.color = Color.white;
                }
                if (elementGlowImage != null)
                {
                    elementGlowImage.gameObject.SetActive(true);
                    Color elemCol = mat.ElementColor;
                    elementGlowImage.color = new Color(elemCol.r, elemCol.g, elemCol.b, 0.35f);
                }
            }
        }

        /// <summary>
        /// 设置在一笔画回路中的高亮与序号标记 (如 1 -> 2 -> 3)
        /// </summary>
        public void SetHighlighted(bool highlighted, int stepIndex = -1, Color? pulseColor = null)
        {
            isHighlightedInPath = highlighted;
            pathStepNumber = stepIndex;

            if (sequenceBadge != null)
            {
                sequenceBadge.SetActive(highlighted && stepIndex > 0);
                if (sequenceText != null && stepIndex > 0)
                {
                    sequenceText.text = stepIndex.ToString();
                }
            }

            if (deltaBadge != null)
            {
                bool showDelta = highlighted && currentMaterial != null && stepIndex > 0;
                deltaBadge.SetActive(showDelta);
                if (showDelta && deltaText != null)
                {
                    deltaText.text = currentMaterial.GetCompactDeltaString();
                }
            }

            if (borderImage != null)
            {
                if (highlighted)
                {
                    Color c = pulseColor ?? new Color(0.98f, 0.82f, 0.18f, 1f);
                    borderImage.color = c;
                }
                else
                {
                    borderImage.color = layout != null && layout.isMajor
                        ? new Color(0.95f, 0.78f, 0.28f, 0.9f)
                        : new Color(0.35f, 0.45f, 0.55f, 0.65f);
                }
            }
        }


        private bool isBlueprintTarget = false;

        /// <summary>
        /// 设置由蓝图侧边栏联动触发的目标材料高亮
        /// </summary>
        public void SetBlueprintTarget(bool isTarget, Color? glowColor = null)
        {
            isBlueprintTarget = isTarget;

            if (is2x2Root)
            {
                if (overlay2x2Border != null)
                {
                    overlay2x2Border.color = isTarget ? (glowColor ?? new Color(1f, 0.85f, 0.2f, 1f)) : (currentMaterial != null ? currentMaterial.ElementColor : Color.white);
                }
                if (overlay2x2CanvasGroup != null)
                {
                    overlay2x2CanvasGroup.alpha = 1f;
                }
                return;
            }

            if (is2x2Child)
            {
                // 子格子无需额外绘制边框，由 2x2 根覆盖层统一部署
                return;
            }

            if (isTarget)
            {
                if (borderImage != null)
                {
                    borderImage.color = glowColor ?? new Color(1f, 0.85f, 0.2f, 1f);
                }
                if (elementGlowImage != null)
                {
                    elementGlowImage.gameObject.SetActive(true);
                    elementGlowImage.color = new Color(1f, 0.85f, 0.2f, 0.55f);
                }
                SetDimmed(false);
            }
            else
            {
                if (borderImage != null && !isHighlightedInPath)
                {
                    borderImage.color = layout != null && layout.isMajor
                        ? new Color(0.95f, 0.78f, 0.28f, 0.9f)
                        : new Color(0.35f, 0.45f, 0.55f, 0.65f);
                }
                if (elementGlowImage != null)
                {
                    if (currentMaterial != null)
                    {
                        elementGlowImage.gameObject.SetActive(true);
                        Color elemCol = currentMaterial.ElementColor;
                        elementGlowImage.color = new Color(elemCol.r, elemCol.g, elemCol.b, 0.35f);
                    }
                    else
                    {
                        elementGlowImage.gameObject.SetActive(false);
                    }
                }
            }
        }

        public void SetGhostDimmed(bool dimmed)
        {
            if (itemIconImage != null && !is2x2Child && !is2x2Root)
            {
                itemIconImage.color = dimmed ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
            }
            if (is2x2Root && overlay2x2CanvasGroup != null)
            {
                overlay2x2CanvasGroup.alpha = dimmed ? 0.35f : 1f;
            }
        }

        /// <summary>
        /// 虚化/暗化非目标槽位，让被高亮的蓝图配方材料更加鲜明夺目
        /// </summary>
        public void SetDimmed(bool dimmed)
        {
            float alpha = dimmed ? 0.3f : 1f;
            if (itemIconImage != null && !is2x2Child && !is2x2Root)
            {
                itemIconImage.color = new Color(1f, 1f, 1f, alpha);
            }
            if (borderImage != null && !isHighlightedInPath && !isBlueprintTarget && !is2x2Child && !is2x2Root)
            {
                Color c = borderImage.color;
                borderImage.color = new Color(c.r, c.g, c.b, dimmed ? 0.25f : (layout != null && layout.isMajor ? 0.9f : 0.65f));
            }
            if (is2x2Root && overlay2x2CanvasGroup != null)
            {
                overlay2x2CanvasGroup.alpha = isBlueprintTarget ? 1f : alpha;
            }
        }

        [Header("2x2 Core Slot Elements")]
        public bool is2x2Root = false;
        public bool is2x2Child = false;
        public GameObject overlay2x2Root;
        public CanvasGroup overlay2x2CanvasGroup;
        public Image overlay2x2Border;
        public Image overlay2x2Icon;
        public Text overlay2x2Name;
        public Text overlay2x2Badge;
        public Text overlay2x2Stats;

        /// <summary>
        /// 激活或隐藏跨越 2x2 格子的超大核心构件卡片
        /// </summary>
        public void Set2x2RootOverlay(bool active, BiomechanicalMaterial mat = null)
        {
            is2x2Root = active;
            if (overlay2x2Root != null)
            {
                overlay2x2Root.SetActive(active);
                if (active && mat != null)
                {
                    // 保持自身底板 enabled=true 与 raycastTarget=true 接收射线，但设为透明杜绝格线穿模
                    if (bgImage != null)
                    {
                        bgImage.enabled = true;
                        bgImage.raycastTarget = true;
                        bgImage.color = Color.clear;
                    }
                    if (borderImage != null) borderImage.enabled = false;
                    if (itemIconImage != null) itemIconImage.gameObject.SetActive(false);

                    if (overlay2x2Icon != null) overlay2x2Icon.sprite = mat.GetOrLoadSprite();
                    if (overlay2x2Name != null) overlay2x2Name.text = mat.materialName;
                    if (overlay2x2Badge != null) overlay2x2Badge.text = $"【2x2 骨架核心】 {mat.ElementName}";
                    if (overlay2x2Stats != null) overlay2x2Stats.text = $"💨P:{mat.p} ⚡E:{mat.e} 🌿G:{mat.g} 💧W:{mat.w}";
                    if (overlay2x2Border != null) overlay2x2Border.color = mat.ElementColor;
                    if (overlay2x2CanvasGroup != null) overlay2x2CanvasGroup.alpha = 1f;
                }
                else
                {
                    if (!is2x2Child)
                    {
                        if (bgImage != null)
                        {
                            bgImage.enabled = true;
                            bgImage.raycastTarget = true;
                            bgImage.color = Color.white;
                        }
                        if (borderImage != null) borderImage.enabled = true;
                    }
                }
            }
        }

        /// <summary>
        /// 设置为 2x2 核心材料的被覆盖子格子
        /// </summary>
        public void SetAs2x2Child(bool isChild, BiomechanicalMaterial mat)
        {
            is2x2Child = isChild;
            currentMaterial = mat;

            if (isChild)
            {
                // 子格子处于 2x2 大卡片覆盖之下：
                // 保持 bgImage enabled=true 与 raycastTarget=true（设为透明），确保触摸子格子同样能流畅触发连线与拖拽
                if (bgImage != null)
                {
                    bgImage.enabled = true;
                    bgImage.raycastTarget = true;
                    bgImage.color = Color.clear;
                }
                if (borderImage != null) borderImage.enabled = false;
                if (itemIconImage != null) itemIconImage.gameObject.SetActive(false);
                if (elementGlowImage != null) elementGlowImage.gameObject.SetActive(false);
                if (majorBadgeImage != null) majorBadgeImage.gameObject.SetActive(false);
            }
            else
            {
                // 还原独立单格渲染与背景
                if (bgImage != null)
                {
                    bgImage.enabled = true;
                    bgImage.raycastTarget = true;
                    bgImage.color = Color.white;
                }
                if (borderImage != null) borderImage.enabled = true;
            }
        }

        // Pointer & Drag Callbacks
        public void OnPointerDown(PointerEventData eventData) => onSlotPointerDown?.Invoke(this, eventData);
        public void OnPointerEnter(PointerEventData eventData) => onSlotPointerEnter?.Invoke(this, eventData);
        public void OnPointerExit(PointerEventData eventData) => onSlotPointerExit?.Invoke(this, eventData);
        public void OnPointerUp(PointerEventData eventData) => onSlotPointerUp?.Invoke(this, eventData);
        public void OnBeginDrag(PointerEventData eventData) => onSlotBeginDrag?.Invoke(this, eventData);
        public void OnDrag(PointerEventData eventData) => onSlotDrag?.Invoke(this, eventData);
        public void OnEndDrag(PointerEventData eventData) => onSlotEndDrag?.Invoke(this, eventData);
        public void OnPointerClick(PointerEventData eventData) => onSlotClick?.Invoke(this, eventData);
    }
}
