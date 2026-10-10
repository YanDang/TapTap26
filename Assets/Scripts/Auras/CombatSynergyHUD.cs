using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗场景构装协同 HUD 说明面板：
/// 实时监测并展示战场上激活的生体构装协同效应，帮助玩家直观理解家具与战斗的联动！
/// </summary>
public class CombatSynergyHUD : MonoBehaviour
{
    private bool showSynergyGuide = true;
    private GUIStyle headerStyle;
    private GUIStyle itemStyle;
    private GUIStyle hintStyle;
    private bool stylesInit = false;

    private void InitStyles()
    {
        if (stylesInit) return;

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        headerStyle.normal.textColor = new Color(1f, 0.88f, 0.35f);

        itemStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = true
        };
        itemStyle.normal.textColor = new Color(0.9f, 0.95f, 1f);

        hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleLeft
        };
        hintStyle.normal.textColor = new Color(0.6f, 0.8f, 0.95f);

        stylesInit = true;
    }

    void OnGUI()
    {
        var gmm = FindObjectOfType<GameModeManager>();
        if (gmm != null && gmm.IsBuildMode) return; // 建造模式隐藏

        if (PlayerSessionData.isCraftingOpen) return;

        InitStyles();

        float panelW = 340f;
        float panelH = showSynergyGuide ? 245f : 36f;
        float startX = 16f;
        float startY = 110f; // 紧贴顶部血条 HUD 下方

        // 半透明深邃生体风格底板
        GUI.Box(new Rect(startX, startY, panelW, panelH), "");

        // 折叠 / 展开按钮
        string toggleText = showSynergyGuide ? "⚙️ 构装战斗协同面板 [收起 ▲]" : "⚙️ 构装战斗协同面板 [展开 ▼]";
        if (GUI.Button(new Rect(startX + 6f, startY + 4f, panelW - 12f, 26f), toggleText))
        {
            showSynergyGuide = !showSynergyGuide;
        }

        if (!showSynergyGuide) return;

        float curY = startY + 34f;
        float lineH = 28f;

        // 1. 水电协同状态检测
        bool hasElectroWater = false;
        var waterTowers = FindObjectsOfType<WaterTowerAuraBehavior>();
        foreach (var wt in waterTowers)
        {
            if (wt.isElectrifiedPuddle)
            {
                hasElectroWater = true;
                break;
            }
        }

        string waterStatus = hasElectroWater 
            ? "<color=#38bdf8>⚡ <b>【水电协同·感电水潭】</b></color> <color=#4ade80>已激活!</color> (电伤+200% 破防翻倍)" 
            : "<color=#94a3b8>⚡ 【水电协同】脉动水塔 + 电鳗发电机 (重叠激活)</color>";
        GUI.Label(new Rect(startX + 10f, curY, panelW - 20f, lineH), waterStatus, itemStyle);
        curY += lineH;

        // 2. 增生防线与荆棘
        GUI.Label(new Rect(startX + 10f, curY, panelW - 20f, lineH), "🌿 <color=#4ade80><b>【增生珊瑚墙】</b></color> A*硬阻挡 + 荆棘反噬 + 战后自愈", itemStyle);
        curY += lineH;

        // 3. 气压力场
        GUI.Label(new Rect(startX + 10f, curY, panelW - 20f, lineH), "💨 <color=#c084fc><b>【气泡悬浮床】</b></color> 移速+25% 翻滚精力-25% 减速冲刺怪", itemStyle);
        curY += lineH;

        // 4. 水刃重炮
        GUI.Label(new Rect(startX + 10f, curY, panelW - 20f, lineH), "🌊 <color=#38bdf8><b>【高压喷水炮】</b></color> 自动索敌水刃 + 强力击退 + 40点破韧", itemStyle);
        curY += lineH;

        // 5. 生体驯化
        GUI.Label(new Rect(startX + 10f, curY, panelW - 20f, lineH), "❤️ <color=#f472b6><b>【生体培养箱】</b></color> 脉冲聚怪 + 概率心智反戈互殴", itemStyle);
        curY += lineH;

        // 6. 杂交废料
        GUI.Label(new Rect(startX + 10f, curY, panelW - 20f, lineH), "💣 <color=#fb923c><b>【活性废料】</b></color> 弹簧弹飞麻痹 / 延时自爆 / 疗愈水雾", itemStyle);
        curY += lineH;

        GUI.Label(new Rect(startX + 10f, curY, panelW - 20f, 22f), "💡 靠近家具按 [F] 踢飞碎裂，靠近点击可举起投掷", hintStyle);
    }
}
