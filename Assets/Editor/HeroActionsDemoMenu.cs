using UnityEditor;
using UnityEngine;

public static class HeroActionsDemoMenu
{
    [MenuItem("GameJam/Hero2D/Record Combat Actions (Play mode)")]
    public static void Record()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[HeroActs] 先进入 Play 模式"); return; }
        var gm = GameJam.GameManager.I;
        if (!gm || !gm.player) { Debug.LogWarning("[HeroActs] 场景里没有 GameManager / 主角"); return; }
        gm.gameObject.AddComponent<GameJam.HeroActionsDemo>();
    }

    // 编辑器里跑战斗自测（不用打包、不用关编辑器）：结果写到工程根目录 selftest.txt，并打印到 Console
    [MenuItem("GameJam/Run Self Test (Play mode)")]
    public static void SelfTest()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[SelfTest] 先进入 Play 模式"); return; }
        var gm = GameJam.GameManager.I;
        if (!gm || !gm.player) { Debug.LogWarning("[SelfTest] 场景里没有 GameManager / 主角"); return; }
        gm.autoWaves = false;
        gm.gameObject.AddComponent<GameJam.SelfTest>();
    }
}
