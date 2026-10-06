using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BiomechanicalCrafting
{
    /// <summary>
    /// 左侧常驻生体蓝图侧边栏 (Blueprint Sidebar Dock)
    /// 贯彻“可合成优先靠前排序”、“点击联动高亮背壳材料”与“2x2核心/1x1辅料规格说明”
    /// </summary>
    public class BlueprintSidebarDock : MonoBehaviour
    {
        public static BlueprintSidebarDock Instance { get; private set; }

        [Header("UI Containers")]
        public RectTransform cardsContainer;
        public Text titleText;
        public Text statusText;
        public Text hintText;

        [Header("Callbacks")]
        public Action<BiomechanicalBlueprint> onBlueprintSelected;

        // Runtime state
        private string selectedBlueprintId = null;
        private List<GameObject> activeCards = new List<GameObject>();
        private List<ActiveCardData> activeCardDataList = new List<ActiveCardData>();
        private static Font defaultFont;

        void Awake()
        {
            Instance = this;
            defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>
        /// 刷新蓝图列表，根据背壳当前材料判断可合成性并置顶排序
        /// </summary>
        public void RefreshBlueprints(List<BiomechanicalMaterial> backpackMaterials)
        {
            if (cardsContainer == null) return;

            // 1. 清理现有卡片
            for (int i = cardsContainer.childCount - 1; i >= 0; i--)
            {
                var child = cardsContainer.GetChild(i).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(child);
                else Destroy(child);
#else
                Destroy(child);
#endif
            }
            activeCards.Clear();
            activeCardDataList.Clear();

            // 2. 统计背壳中的所有可用材料 ID 与数量
            HashSet<string> heldMaterialIds = new HashSet<string>();
            if (backpackMaterials != null)
            {
                foreach (var m in backpackMaterials)
                {
                    if (m != null && !string.IsNullOrEmpty(m.id))
                    {
                        heldMaterialIds.Add(m.id);
                    }
                }
            }

            // 3. 评估所有蓝图的可合成性
            var allBps = BiomechanicalBlueprintDatabase.GetAllBlueprints();
            if (allBps == null || allBps.Count == 0) return;

            List<BlueprintStatusItem> evaluatedList = new List<BlueprintStatusItem>();
            int craftableCount = 0;

            foreach (var bp in allBps)
            {
                if (bp.isAberrant) continue; // 异变废料为自发涌现产物，不作为固定图鉴蓝图

                bool canCraft = EvaluateCraftability(bp, heldMaterialIds);
                if (canCraft) craftableCount++;

                evaluatedList.Add(new BlueprintStatusItem
                {
                    blueprint = bp,
                    isCraftable = canCraft
                });
            }

            // 4. 排序规则：可合成的排在最前，其次按元素分类
            evaluatedList.Sort((a, b) =>
            {
                if (a.isCraftable != b.isCraftable)
                {
                    return b.isCraftable.CompareTo(a.isCraftable); // true 排前面
                }
                return string.Compare(a.blueprint.name, b.blueprint.name, StringComparison.Ordinal);
            });

            // 5. 更新顶部汇总状态
            if (statusText != null)
            {
                statusText.text = $"可即刻合成: <color=#34D399><b>{craftableCount}</b></color> / {evaluatedList.Count}";
            }

            // 6. 生成卡片
            foreach (var item in evaluatedList)
            {
                GameObject cardGO = CreateBlueprintCard(item, heldMaterialIds);
                activeCards.Add(cardGO);
            }
        }

        private bool EvaluateCraftability(BiomechanicalBlueprint bp, HashSet<string> heldIds)
        {
            if (bp == null) return false;

            // 检查核心材料是否都在背壳中
            if (bp.requiredCoreIds != null)
            {
                foreach (var cid in bp.requiredCoreIds)
                {
                    if (!heldIds.Contains(cid)) return false;
                }
            }

            // 如果有任意可选辅料，检查是否至少拥有一种
            if (bp.requiredAnyIds != null && bp.requiredAnyIds.Count > 0)
            {
                bool hasAny = false;
                foreach (var aid in bp.requiredAnyIds)
                {
                    if (heldIds.Contains(aid))
                    {
                        hasAny = true;
                        break;
                    }
                }
                if (!hasAny) return false;
            }

            return true;
        }

        private GameObject CreateBlueprintCard(BlueprintStatusItem statusItem, HashSet<string> heldIds)
        {
            var bp = statusItem.blueprint;
            bool isCraftable = statusItem.isCraftable;
            bool isSelected = (bp.id == selectedBlueprintId);

            GameObject card = new GameObject($"BPCard_{bp.id}");
            card.transform.SetParent(cardsContainer, false);

            RectTransform rt = card.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(382, 120);

            LayoutElement le = card.AddComponent<LayoutElement>();
            le.minHeight = 120f;
            le.preferredHeight = 120f;
            le.flexibleWidth = 1f;

            // 卡片背景与边框
            Image bg = card.AddComponent<Image>();
            Color bgColor = isCraftable
                ? new Color(0.05f, 0.12f, 0.14f, 0.95f)
                : new Color(0.07f, 0.09f, 0.13f, 0.9f);
            bg.color = bgColor;

            // 边框高光
            GameObject borderGO = new GameObject("Border");
            borderGO.transform.SetParent(card.transform, false);
            RectTransform brt = borderGO.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.sizeDelta = Vector2.zero;
            Image bImg = borderGO.AddComponent<Image>();
            bImg.raycastTarget = false;

            Color borderColor = isSelected
                ? new Color(0.98f, 0.85f, 0.25f, 1f) // 选中金黄
                : (isCraftable
                    ? new Color(0.2f, 0.88f, 0.55f, 0.9f) // 可合成荧光翠绿
                    : new Color(0.25f, 0.32f, 0.42f, 0.5f)); // 灰色
            bImg.color = borderColor;

            // 1. 图标预览 (左侧)
            GameObject iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(card.transform, false);
            RectTransform irt = iconGO.AddComponent<RectTransform>();
            irt.anchorMin = new Vector2(0, 0.5f);
            irt.anchorMax = new Vector2(0, 0.5f);
            irt.pivot = new Vector2(0, 0.5f);
            irt.anchoredPosition = new Vector2(10, 0);
            irt.sizeDelta = new Vector2(72, 72);
            Image iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = bp.GetIconSprite();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // 2. 标题与品质/分类 (顶部)
            GameObject titleGO = new GameObject("Title");
            titleGO.transform.SetParent(card.transform, false);
            RectTransform trt = titleGO.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 1);
            trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0, 1);
            trt.anchoredPosition = new Vector2(90, -10);
            trt.sizeDelta = new Vector2(-100, 24);
            Text tText = titleGO.AddComponent<Text>();
            tText.font = defaultFont;
            tText.fontSize = 15;
            tText.fontStyle = FontStyle.Bold;
            tText.color = bp.ThemeColor;
            tText.raycastTarget = false;
            tText.text = bp.name;

            // 3. 可合成状态徽章 (右上角)
            GameObject badgeGO = new GameObject("CraftableBadge");
            badgeGO.transform.SetParent(card.transform, false);
            RectTransform badgRT = badgeGO.AddComponent<RectTransform>();
            badgRT.anchorMin = new Vector2(1, 1);
            badgRT.anchorMax = new Vector2(1, 1);
            badgRT.pivot = new Vector2(1, 1);
            badgRT.anchoredPosition = new Vector2(-10, -10);
            badgRT.sizeDelta = new Vector2(110, 22);
            Text bText = badgeGO.AddComponent<Text>();
            bText.font = defaultFont;
            bText.fontSize = 12;
            bText.fontStyle = FontStyle.Bold;
            bText.alignment = TextAnchor.MiddleRight;
            bText.raycastTarget = false;
            bText.text = isCraftable
                ? "<color=#34D399><b>【⚡ 可即刻合成】</b></color>"
                : "<color=#94A3B8>材料不足</color>";

            // 4. 配方材料清单 (标明 2x2 核心与 1x1 辅料及拥有状态)
            GameObject ingrGO = new GameObject("Ingredients");
            ingrGO.transform.SetParent(card.transform, false);
            RectTransform ingRT = ingrGO.AddComponent<RectTransform>();
            ingRT.anchorMin = new Vector2(0, 1);
            ingRT.anchorMax = new Vector2(1, 1);
            ingRT.pivot = new Vector2(0, 1);
            ingRT.anchoredPosition = new Vector2(90, -36);
            ingRT.sizeDelta = new Vector2(-100, 44);
            Text ingText = ingrGO.AddComponent<Text>();
            ingText.font = defaultFont;
            ingText.fontSize = 12;
            ingText.lineSpacing = 1.15f;
            ingText.raycastTarget = false;

            List<string> ingList = new List<string>();
            foreach (var cid in bp.requiredCoreIds)
            {
                var mat = BiomechanicalMaterialDatabase.GetById(cid);
                string mName = mat != null ? mat.materialName : cid;
                bool isHeld = heldIds.Contains(cid);
                string tag = isHeld ? "<color=#34D399>[✓在壳内]</color>" : "<color=#F87171>[✗缺少]</color>";
                string sizeLabel = (mat != null && mat.isMajor) ? "<color=#FBBF24>[2x2核心]</color>" : "<color=#38BDF8>[1x1辅料]</color>";
                ingList.Add($"• {mName} {sizeLabel} {tag}");
            }
            if (bp.requiredAnyIds != null && bp.requiredAnyIds.Count > 0)
            {
                bool hasAny = false;
                foreach (var aid in bp.requiredAnyIds)
                {
                    if (heldIds.Contains(aid)) { hasAny = true; break; }
                }
                string tag = hasAny ? "<color=#34D399>[✓在壳内]</color>" : "<color=#F87171>[✗缺少]</color>";
                ingList.Add($"• 同系辅料配件 <color=#38BDF8>[1x1辅料]</color> {tag}");
            }
            ingText.text = string.Join("\n", ingList);

            // 5. 简短效能
            GameObject effGO = new GameObject("EffectSummary");
            effGO.transform.SetParent(card.transform, false);
            RectTransform effRT = effGO.AddComponent<RectTransform>();
            effRT.anchorMin = new Vector2(0, 0);
            effRT.anchorMax = new Vector2(1, 0);
            effRT.pivot = new Vector2(0, 0);
            effRT.anchoredPosition = new Vector2(90, 8);
            effRT.sizeDelta = new Vector2(-100, 20);
            Text effText = effGO.AddComponent<Text>();
            effText.font = defaultFont;
            effText.fontSize = 11;
            effText.color = new Color(0.7f, 0.8f, 0.9f);
            effText.raycastTarget = false;
            effText.text = $"<color=#FFE050>战术效果:</color> {bp.formulaDescription}";

            // 6. 点击事件按钮
            Button btn = card.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = bg;
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(0.12f, 0.2f, 0.28f, 1f);
            cb.pressedColor = new Color(0.18f, 0.28f, 0.4f, 1f);
            btn.colors = cb;

            btn.onClick.AddListener(() =>
            {
                OnCardClicked(bp);
            });

            activeCardDataList.Add(new ActiveCardData
            {
                cardGO = card,
                borderImage = bImg,
                blueprint = bp,
                isCraftable = isCraftable
            });

            return card;
        }

        private void OnCardClicked(BiomechanicalBlueprint bp)
        {
            if (selectedBlueprintId == bp.id)
            {
                // 再次点击取消高亮
                ClearSelection();
            }
            else
            {
                selectedBlueprintId = bp.id;
                onBlueprintSelected?.Invoke(bp);
                RefreshSelectionHighlight();
            }
        }

        public void ClearSelection()
        {
            if (selectedBlueprintId != null)
            {
                selectedBlueprintId = null;
                onBlueprintSelected?.Invoke(null);
            }
            RefreshSelectionHighlight();
        }

        public void RefreshSelectionHighlight()
        {
            foreach (var data in activeCardDataList)
            {
                if (data == null || data.borderImage == null) continue;
                bool isThisSelected = (!string.IsNullOrEmpty(selectedBlueprintId) && data.blueprint != null && data.blueprint.id == selectedBlueprintId);

                if (isThisSelected)
                {
                    data.borderImage.color = new Color(0.98f, 0.85f, 0.25f, 1f); // 亮金色选中
                    if (data.cardGO != null) data.cardGO.transform.localScale = new Vector3(1.02f, 1.02f, 1f);
                }
                else
                {
                    if (data.cardGO != null) data.cardGO.transform.localScale = Vector3.one;
                    data.borderImage.color = data.isCraftable
                        ? new Color(0.2f, 0.88f, 0.55f, 0.9f) // 翠绿未选中
                        : new Color(0.25f, 0.32f, 0.42f, 0.5f); // 灰色未选中
                }
            }
        }

        private class ActiveCardData
        {
            public GameObject cardGO;
            public Image borderImage;
            public BiomechanicalBlueprint blueprint;
            public bool isCraftable;
        }

        private class BlueprintStatusItem
        {
            public BiomechanicalBlueprint blueprint;
            public bool isCraftable;
        }
    }
}
