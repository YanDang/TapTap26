using UnityEngine;
using UnityEngine.UI;
using BiomechanicalCrafting;

/// <summary>
/// 构装仓库卡片条目组件：
/// 展示构装图标、名称与分类，响应点击并高亮当前选中项。
/// </summary>
public class FurnitureCardItem : MonoBehaviour
{
    public CraftedProduct Product { get; private set; }
    private BuildDockUI dockUI;
    private Image cardBg;
    private Image iconImage;
    private Text nameText;
    private Button button;

    public void Setup(BuildDockUI parentUI, CraftedProduct product, bool isSelected)
    {
        this.dockUI = parentUI;
        this.Product = product;

        cardBg = GetComponent<Image>();
        button = GetComponent<Button>();

        // 查找或初始化图标与文字
        var iconTransform = transform.Find("Icon");
        if (iconTransform != null) iconImage = iconTransform.GetComponent<Image>();

        var nameTransform = transform.Find("NameText");
        if (nameTransform != null) nameText = nameTransform.GetComponent<Text>();

        // 绑定图标
        if (iconImage != null && product != null)
        {
            var sp = Resources.Load<Sprite>("FurnitureSprites/" + product.id);
            if (sp != null)
            {
                iconImage.sprite = sp;
                iconImage.color = Color.white;
            }
            else
            {
                iconImage.color = new Color(0.6f, 0.6f, 0.7f, 0.5f);
            }
        }

        // 绑定文字
        if (nameText != null && product != null)
        {
            string shortName = product.productName;
            if (shortName.Contains("·"))
            {
                var parts = shortName.Split('·');
                if (parts.Length > 1) shortName = parts[1];
            }
            if (shortName.Length > 6) shortName = shortName.Substring(0, 6);
            nameText.text = shortName;
        }

        // 绑定按键点击
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }

        SetSelected(isSelected);
    }

    private void OnClick()
    {
        if (dockUI != null && Product != null)
        {
            dockUI.OnCardSelected(Product);
        }
    }

    public void SetSelected(bool selected)
    {
        if (cardBg != null && dockUI != null)
        {
            cardBg.sprite = selected ? dockUI.cardBgSelected : dockUI.cardBgNormal;
            transform.localScale = selected ? new Vector3(1.05f, 1.05f, 1f) : Vector3.one;
        }
    }
}
