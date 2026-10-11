using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace GameJam
{
    /// <summary>
    /// 接入程序同学的采集 / 合成系统（TapTap26，2026-10-11；代码在 Assets/Crafting，命名空间 BiomechanicalCrafting）。
    /// 打开合成工坊：B / Tab，或点物品栏左边的“合成工坊”按钮 →
    ///   战斗暂停（timeScale = 0，主角和输入停住，HUD 隐藏）→ 叠加加载 Mix 场景（背壳背包 + 连线合成）。
    ///   Mix 里点“采集温室”切到 Collect 场景采材料，再点回工坊；Mix 里 ESC / B / “返回战斗”按钮回来。
    /// 回来时（PlayerSessionData.hasJustReturnedFromCrafting）恢复时间、HUD 和输入。
    /// 合成出的成品进合成仓库 PlayerSessionData.craftedProducts，物品栏直接读它（Inventory）。
    /// 对方的 Mix / Collect 场景自带一台 MainCamera：加载后把它改成不带 MainCamera 标签、画在我们相机之上，
    /// 免得我们的代码（公告板、鼠标拾取）用 Camera.main 拿错相机。
    /// </summary>
    public class CraftingBridge : MonoBehaviour
    {
        public const string MixScene = "Mix", CollectScene = "Collect";
        static CraftingBridge I;

        public static bool IsOpen => PlayerSessionData.isCraftingOpen;
        public static int opens, returns;               // 自测用
        public static string LastRefusal { get; private set; }

        Canvas _hud;
        float _reopenBlockUntil;
        bool _openRequested;

        void Awake()
        {
            I = this;
            PlayerSessionData.EnsureInitialized();
            PlayerSessionData.isCraftingOpen = false;
            PlayerSessionData.hasJustReturnedFromCrafting = false;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // 对方的场景在叠加模式下会删掉自己的 EventSystem，靠宿主提供（我们原来没有，按钮点不了）
            if (!FindObjectOfType<EventSystem>())
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; }

        /// <summary>打开合成工坊（按钮 / 快捷键 / 自测都走这里）。返回有没有开始打开；打不开时原因在 LastRefusal。</summary>
        public static bool Open() => I && I.TryOpen();

        bool TryOpen()
        {
            var p = GameManager.I ? GameManager.I.player : null;
            string why = IsOpen || _openRequested ? "工坊已经开着" :
                         Time.realtimeSinceStartup < _reopenBlockUntil ? "刚回来" :
                         TimeManager.SlowMo ? "处决慢动作中" :
                         p && (p.Dead || p.Unavailable || p.Placing) ? "现在走不开" : null;
            LastRefusal = why;
            if (why != null) return false;

            BuildController.I?.Cancel();
            if (p) p.CancelLeftHold();
            PlayerSessionData.returnCombatSceneName = SceneManager.GetActiveScene().name;
            PlayerSessionData.isCraftingOpen = true;
            PlayerSessionData.hasJustReturnedFromCrafting = false;
            Time.timeScale = 0f;                                     // 战斗冻结（工坊的界面动画用的是 unscaled 时间）
            if (!_hud && HUD.I) _hud = HUD.I.GetComponent<Canvas>();
            if (_hud) _hud.enabled = false;                          // 我们的 HUD 层级更高，会盖住工坊界面
            _openRequested = true;
            opens++;
            SceneManager.LoadSceneAsync(MixScene, LoadSceneMode.Additive);
            return true;
        }

        void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            if (s.name != MixScene && s.name != CollectScene) return;
            _openRequested = false;
            var ours = CameraRig.I ? CameraRig.I.GetComponent<Camera>() : null;
            foreach (var root in s.GetRootGameObjects())
                foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                {
                    if (cam.CompareTag("MainCamera")) cam.tag = "Untagged";
                    if (ours) cam.depth = ours.depth + 10;               // 工坊画在战斗画面上面（它自己清屏）
                }
        }

        void Update()
        {
            // 从工坊回来（Mix 的 ReturnToCombatScene 已经把 timeScale 设回 1、卸载了场景）
            if (PlayerSessionData.hasJustReturnedFromCrafting && !IsOpen) Restore();

            if (IsOpen || GameInput.Disabled) return;
            if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.Tab)) Open();
        }

        void Restore()
        {
            PlayerSessionData.hasJustReturnedFromCrafting = false;
            if (!TimeManager.SlowMo) Time.timeScale = 1f;
            if (_hud) _hud.enabled = true;
            _reopenBlockUntil = Time.realtimeSinceStartup + 0.3f;    // 同一下 B 键别又把工坊打开
            returns++;
        }
    }
}
