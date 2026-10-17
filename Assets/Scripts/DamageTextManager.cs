using System.Collections;
using UnityEngine;

/// <summary>
/// 战斗浮动文字管理器：
/// - 显示伤害数值（如 "-25"）、闪避提示（"DODGE!"）、受击提示等
/// - 自动上升并平滑淡出销毁
/// </summary>
public class DamageTextManager : MonoBehaviour
{
    public static DamageTextManager Instance { get; private set; }

    [Header("Font Settings")]
    public Font textFont;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        if (textFont == null)
        {
            textFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }

    /// <summary>
    /// 在指定世界坐标生成漂浮文字（针对手机屏幕进行了尺寸精细化收紧，杜绝遮挡）
    /// </summary>
    public void ShowText(Vector3 worldPos, string content, Color color, float scale = 0.08f)
    {
        // 手机屏幕舒适化优化：整体缩放严格收紧至精致小巧尺寸（约原来的 45%~50%），消除巨大笨重感
        float targetScale = Mathf.Clamp(scale * 0.50f, 0.032f, 0.072f);

        GameObject textGo = new GameObject("DamageText");
        textGo.transform.position = worldPos + new Vector3(Random.Range(-0.10f, 0.10f), 0.25f, 0f);
        textGo.transform.localScale = Vector3.one * targetScale;

        TextMesh tm = textGo.AddComponent<TextMesh>();
        tm.text = content;
        tm.font = textFont;
        tm.fontSize = 36;
        tm.fontStyle = FontStyle.Bold;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = color;

        MeshRenderer mr = textGo.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            if (textFont != null && textFont.material != null)
            {
                mr.material = textFont.material;
            }
            mr.sortingOrder = 200; // 确保置于所有精灵之上
        }

        StartCoroutine(AnimateAndDestroy(textGo, tm));
    }

    private IEnumerator AnimateAndDestroy(GameObject go, TextMesh tm)
    {
        float duration = 0.70f;
        float elapsed = 0f;
        Vector3 startPos = go.transform.position;
        Vector3 initialScale = go.transform.localScale;
        Color initialColor = tm.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 向上轻柔漂浮
            go.transform.position = startPos + new Vector3(0f, Mathf.Sin(t * Mathf.PI * 0.5f) * 0.42f, 0f);

            // 弹出轻微回弹脉冲（避免剧烈暴胀遮挡视野）
            float scaleMult = t < 0.15f ? Mathf.Lerp(0.88f, 1.08f, t / 0.15f) : Mathf.Lerp(1.08f, 1.0f, (t - 0.15f) / 0.85f);
            go.transform.localScale = initialScale * scaleMult;

            // 渐隐
            float alpha = t > 0.45f ? Mathf.Lerp(initialColor.a, 0f, (t - 0.45f) / 0.55f) : initialColor.a;
            tm.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);

            yield return null;
        }

        Destroy(go);
    }
}
