using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BiomechanicalCrafting
{
    /// <summary>
    /// 生体机械蓝图与配方图鉴面板 (Blueprint Codex Modal)
    /// 让玩家随时查阅所有可合成的标准设施、生体杂交废料以及四要素 P/E/G/W 动力学影响机制
    /// </summary>
    public class BlueprintCodexModal : MonoBehaviour
    {
        public static BlueprintCodexModal Instance { get; private set; }

        [Header("UI References")]
        public GameObject modalRoot;
        public RectTransform cardsContainer;
        public Button closeBtn;
        public Button tabAllBtn;
        public Button tabElecBtn;
        public Button tabWoodBtn;
        public Button tabGasBtn;
        public Button tabWaterBtn;
        public Button tabScrapBtn;

        private string currentFilter = "All";
        private List<GameObject> spawnedCards = new List<GameObject>();

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            if (closeBtn != null) closeBtn.onClick.AddListener(Close);
            if (tabAllBtn != null) tabAllBtn.onClick.AddListener(() => FilterCategory("All"));
            if (tabElecBtn != null) tabElecBtn.onClick.AddListener(() => FilterCategory("电网设施"));
            if (tabWoodBtn != null) tabWoodBtn.onClick.AddListener(() => FilterCategory("增生防线"));
            if (tabGasBtn != null) tabGasBtn.onClick.AddListener(() => FilterCategory("气压设施"));
            if (tabWaterBtn != null) tabWaterBtn.onClick.AddListener(() => FilterCategory("潮润设施"));
            if (tabScrapBtn != null) tabScrapBtn.onClick.AddListener(() => FilterCategory("活性杂交废料"));
        }

        public void Open()
        {
            if (modalRoot != null) modalRoot.SetActive(true);
            RefreshCards();
        }

        public void Close()
        {
            if (modalRoot != null) modalRoot.SetActive(false);
        }

        public bool IsOpen => (modalRoot != null && modalRoot.activeSelf);

        public void Toggle()
        {
            if (modalRoot != null)
            {
                if (modalRoot.activeSelf) Close();
                else Open();
            }
        }

        public void FilterCategory(string category)
        {
            currentFilter = category;
            RefreshCards();
        }

        public void RefreshCards()
        {
            if (cardsContainer == null) return;

            // 清理旧卡片
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
            spawnedCards.Clear();

            var allBps = BiomechanicalBlueprintDatabase.GetAllBlueprints();
            if (allBps == null) return;

            foreach (var bp in allBps)
            {
                if (currentFilter != "All")
                {
                    if (currentFilter == "活性杂交废料" && !bp.isAberrant) continue;
                    if (currentFilter != "活性杂交废料" && bp.category != currentFilter) continue;
                }

                GameObject cardGO = CreateBlueprintCard(bp);
                spawnedCards.Add(cardGO);
            }
        }

        private GameObject CreateBlueprintCard(BiomechanicalBlueprint bp)
        {
            GameObject card = new GameObject($"Card_{bp.id}");
            card.transform.SetParent(cardsContainer, false);

            RectTransform rt = card.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(820, 115);

            Image bg = card.AddComponent<Image>();
            bg.color = new Color(0.09f, 0.13f, 0.19f, 0.95f);

            // 1. 图标预览
            GameObject iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(card.transform, false);
            RectTransform irt = iconGO.AddComponent<RectTransform>();
            irt.anchorMin = new Vector2(0, 0.5f);
            irt.anchorMax = new Vector2(0, 0.5f);
            irt.pivot = new Vector2(0, 0.5f);
            irt.anchoredPosition = new Vector2(16, 0);
            irt.sizeDelta = new Vector2(80, 80);
            Image iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = bp.GetIconSprite();
            iconImg.preserveAspect = true;

            // 2. 标题与分类徽章
            GameObject titleGO = new GameObject("Title");
            titleGO.transform.SetParent(card.transform, false);
            RectTransform trt = titleGO.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 1);
            trt.anchorMax = new Vector2(0, 1);
            trt.pivot = new Vector2(0, 1);
            trt.anchoredPosition = new Vector2(110, -12);
            trt.sizeDelta = new Vector2(400, 26);
            Text tText = titleGO.AddComponent<Text>();
            tText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tText.fontSize = 17;
            tText.fontStyle = FontStyle.Bold;
            tText.color = bp.ThemeColor;
            tText.text = $"{bp.name}  <size=13><color=#A0B2C6>【{bp.category}】</color></size>";

            // 3. 核心配方需求
            GameObject reqGO = new GameObject("Requirements");
            reqGO.transform.SetParent(card.transform, false);
            RectTransform rrt = reqGO.AddComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0, 1);
            rrt.anchorMax = new Vector2(0, 1);
            rrt.pivot = new Vector2(0, 1);
            rrt.anchoredPosition = new Vector2(110, -40);
            rrt.sizeDelta = new Vector2(680, 24);
            Text reqText = reqGO.AddComponent<Text>();
            reqText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            reqText.fontSize = 13;
            reqText.color = new Color(0.95f, 0.85f, 0.45f);

            string reqStr = "<b>核心组件：</b>";
            if (bp.isAberrant)
            {
                reqStr += "<color=#F080D0>【异变涌现】无需固定蓝图，混乱连接或多系失衡时即刻自发衍生！</color>";
            }
            else
            {
                List<string> coreNames = new List<string>();
                foreach (var cid in bp.requiredCoreIds)
                {
                    var m = BiomechanicalMaterialDatabase.GetById(cid);
                    string sizeLabel = (m != null && m.isMajor) ? " [2x2大核心·占4格]" : " [1x1辅料]";
                    coreNames.Add((m != null ? m.materialName : cid) + sizeLabel);
                }
                reqStr += string.Join(" + ", coreNames);
                if (bp.requiredAnyIds.Count > 0)
                {
                    List<string> anyNames = new List<string>();
                    foreach (var aid in bp.requiredAnyIds)
                    {
                        var am = BiomechanicalMaterialDatabase.GetById(aid);
                        if (am != null) anyNames.Add(am.materialName);
                    }
                    string anyDesc = anyNames.Count > 0 ? string.Join("/", anyNames) : "对应系任意辅料";
                    reqStr += $" + 辅料({anyDesc}) [1x1]";
                }
            }
            reqText.text = reqStr;

            // 4. 四大要素 P/E/G/W 动力学与实战效果
            GameObject effGO = new GameObject("EffectFormula");
            effGO.transform.SetParent(card.transform, false);
            RectTransform ert = effGO.AddComponent<RectTransform>();
            ert.anchorMin = new Vector2(0, 1);
            ert.anchorMax = new Vector2(0, 1);
            ert.pivot = new Vector2(0, 1);
            ert.anchoredPosition = new Vector2(110, -66);
            ert.sizeDelta = new Vector2(690, 42);
            Text effText = effGO.AddComponent<Text>();
            effText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            effText.fontSize = 12;
            effText.color = new Color(0.8f, 0.9f, 0.98f);
            effText.text = $"<b>要素动力学：</b>{bp.formulaDescription}";

            return card;
        }
    }
}
