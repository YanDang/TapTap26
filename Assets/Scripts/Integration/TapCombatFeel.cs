using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// gamejam 战斗手感对齐（运行时，只在 BiomechanicalCombatScene 生效；命令行 -tapnofeel 或 -tapbaseline 关闭，一切回到原样）。
/// 对照 gamejam 工程（rah056538-boop/gamejam）的 CameraRig / PlayerController / EnemyAI，补齐普攻和受击的反馈细节：
///   1 镜头：指数跟随（follow 8，≈0.12 s 的轻微滞后）代替 0.02 s 的硬跟随；震屏改为 38 Hz Perlin 噪声，强的覆盖弱的，
///     强度按“正交尺寸 / 5.5”换算（TapCombatScale 把镜头拉近到 ≈2.02 后，同样的震屏数值在屏幕上会强 2.7 倍）。
///   2 普攻：伤害落在寄居蟹动画的判定帧（Sweep 0.20 / Thrust 0.22 / Stomp 0.27 s），不是出招第 0 帧；挥空音效；
///     第三下踩踏额外震屏 + 踢击音效 + 更大的击退。
///   3 受击：普通怪被打断时 0.2 s 硬直（停下、触手收回）；击退距离按世界尺度换算（横扫 0.15 / 踩踏 0.3 个 gamejam 单位）。
///   4 闪避反击命中补 0.12 s 顿帧。
/// 改动的共享类只读这里的开关，没有本组件时全部是原逻辑。
/// </summary>
public sealed class TapCombatFeel : MonoBehaviour
{
    public const float ReferenceOrtho = 5.5f;      // gamejam CameraRig.orthoSize
    public const float FollowRate = 8f;            // gamejam CameraRig.follow
    public const float HitReactSeconds = 0.2f;     // gamejam EnemyAI 受击硬直
    public const float SweepKnock = 0.15f, StompKnock = 0.3f, PunchHitstop = 0.12f;

    public static bool On { get; private set; }
    /// <summary>当前世界相对 gamejam 的尺度（= 正交尺寸 / 5.5），击退距离乘它。</summary>
    public static float WorldScale { get; private set; } = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install() { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; }

    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;                         // 工坊 / 温室叠加加载不影响
        Disable();
        if (scene.name != "BiomechanicalCombatScene") return;
        var args = Environment.GetCommandLineArgs();
        if (Array.IndexOf(args, "-tapnofeel") >= 0 || Array.IndexOf(args, "-tapbaseline") >= 0) return;
        var go = new GameObject("TapCombatFeel");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<TapCombatFeel>();
    }

    static void Disable()
    {
        On = false;
        WorldScale = 1f;
        EnemyController.HitReactSeconds = 0f;
        EnemyController.DefaultHitKnock = EnemyController.OriginalHitKnock;
    }

    IEnumerator Start()
    {
        // 等 TapCombatScale 把镜头尺寸定下来（它在 Start 里等一帧），再按最终尺寸换算
        yield return null;
        yield return null;
        var control = CameraController.Instance;
        var cam = control ? control.GetComponent<Camera>() : Camera.main;
        var scale = FindObjectOfType<TapCombatScale>();
        if (scale && scale.VisualHeight > 0f) WorldScale = scale.VisualHeight / TapCombatScale.SourceWorldHeight;   // 主角身高之比，最准
        else WorldScale = cam ? Mathf.Max(0.05f, cam.orthographicSize / ReferenceOrtho) : 1f;
        if (control)
        {
            control.feelFollowRate = FollowRate;
            control.perlinShake = true;
            control.shakeReferenceSize = ReferenceOrtho;
        }
        EnemyController.HitReactSeconds = HitReactSeconds;
        EnemyController.DefaultHitKnock = SweepKnock * WorldScale;
        On = true;
        Debug.Log($"[TapFeel] on worldScale={WorldScale:R} follow={FollowRate} hitReact={HitReactSeconds} knock={EnemyController.DefaultHitKnock:R}");
    }

    void OnDestroy()
    {
        Disable();
        var control = CameraController.Instance;
        if (control) { control.feelFollowRate = 0f; control.perlinShake = false; control.shakeReferenceSize = 0f; }
    }
}
