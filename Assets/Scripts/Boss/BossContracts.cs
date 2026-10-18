using System;
using UnityEngine;

namespace GameJam.Boss
{
    /// <summary>
    /// 无名夜王 Boss 三方共用接口（设计见 Docs/Boss_无名夜王_设计.md）。
    /// Hermes 实现 IBossAnimator（BossAnimator），Kimi 写 BossAI 并实现 IBossInfo，DeepSeek 实现 IBossTelegraphBackend 与 BossHUD。
    /// 改签名先找 Claude；各分支只能新增实现，不能改这里。
    /// </summary>
    public static class BossMoves
    {
        public const string P1_Claw3 = "P1_Claw3", P1_Lunge = "P1_Lunge", P1_LeapSlam = "P1_LeapSlam",
                            P1_CloakSweep = "P1_CloakSweep", P1_Wrap = "P1_Wrap",
                            Transform = "Transform",
                            P2_SpinThrust = "P2_SpinThrust", P2_Claw5 = "P2_Claw5", P2_EyeBeam = "P2_EyeBeam",
                            Ult_Rise = "Ult_Rise", Ult_Cleave = "Ult_Cleave", Ult_Exhausted = "Ult_Exhausted";
    }

    public enum BossElement { None, Dark, Water, Electric }

    /// <summary>骨骼动画层。BossAI 决定“什么时候出什么招、判定在第几秒”，动画层负责把动作对齐到这些时刻。</summary>
    public interface IBossAnimator
    {
        /// <summary>出招。impacts = 每个判定点距现在的秒数（升序），动画要让对应的发力帧落在这些时刻。</summary>
        void Play(string move, float[] impacts);
        /// <summary>判定帧到达（index = impacts 下标），由动画层按时间发出。</summary>
        event Action<string, int> OnImpact;
        /// <summary>该招动作播完（可接下一招）。</summary>
        event Action<string> OnMoveEnd;
        /// <summary>移动：世界速度（XZ），动画层做走/飘/斗篷拖拽；0 = 待机呼吸。</summary>
        void SetLocomotion(Vector3 worldVelocity);
        /// <summary>朝向：+1 面向屏幕右，-1 面向屏幕左（纸片翻转）。</summary>
        void SetFacing(int side);
        /// <summary>1 = 斗篷形态，2 = 化形后（斗篷掀开、独眼大亮）。</summary>
        void SetPhase(int phase);
        /// <summary>预警光强 0..1 与颜色（黄 = 可格挡、红 = 不可格挡；闪烁由 BossAI 控制传入强度）。</summary>
        void SetTelegraph(float amount, Color color);
        void Stagger(float seconds);
        void Die(float seconds);
        /// <summary>受击闪白 / 预警要作用到的所有图层（交给 Unit.flashRenderers）。</summary>
        Renderer[] FlashRenderers { get; }
        /// <summary>爪尖、独眼、斗篷中心的世界位置，用于判定与特效。</summary>
        Transform ClawTip { get; }
        Transform Eye { get; }
        Transform CloakCenter { get; }
        /// <summary>角色在世界里的高度（米），用于血条 / 相机。</summary>
        float Height { get; }
    }

    /// <summary>Boss 血条等 UI 读取的信息（BossAI 实现）。</summary>
    public interface IBossInfo
    {
        bool BossActive { get; }
        string DisplayName { get; }
        float Hp01 { get; }
        float Posture01 { get; }
        int Phase { get; }
        BossElement Element { get; }
        /// <summary>血条上的阶段刻度（0..1），如 0.7 / 0.5 / 0.3。</summary>
        float[] PhaseMarks { get; }
    }

    /// <summary>地面预警 / 开天表现的实现后端（DeepSeek 实现并在启动时注册到 BossTelegraph.Backend）。</summary>
    public interface IBossTelegraphBackend
    {
        BossTelegraph.Handle Circle(Vector3 center, float radius, float duration, Color color);
        BossTelegraph.Handle Cone(Vector3 origin, Vector3 dir, float halfAngleDeg, float radius, float duration, Color color);
        BossTelegraph.Handle Line(Vector3 center, Vector3 dir, float length, float width, float duration, Color color);
        void SkyDarken(float amount);
        void SkyCrack(float amount);
        void Shake(float strength, float seconds);
    }

    /// <summary>
    /// 地面预警门面。BossAI 只调这里；没有注册后端时用占位实现（空物体，到时自动销毁），保证各分支都能编译运行。
    /// 返回的 Handle 可提前 Cancel；追踪型预警（开天）用 Retarget 改中心与方向。duration 到了自动消失，填充满 = 判定。
    /// </summary>
    public static class BossTelegraph
    {
        public static IBossTelegraphBackend Backend;

        public sealed class Handle
        {
            public GameObject go;
            public Action<Vector3, Vector3> retarget;
            public void Cancel() { if (go) UnityEngine.Object.Destroy(go); go = null; }
            public void Retarget(Vector3 center, Vector3 dir) => retarget?.Invoke(center, dir);
        }

        public static Handle Circle(Vector3 center, float radius, float duration, Color color) =>
            Backend != null ? Backend.Circle(center, radius, duration, color) : Stub(center, duration);
        public static Handle Cone(Vector3 origin, Vector3 dir, float halfAngleDeg, float radius, float duration, Color color) =>
            Backend != null ? Backend.Cone(origin, dir, halfAngleDeg, radius, duration, color) : Stub(origin, duration);
        public static Handle Line(Vector3 center, Vector3 dir, float length, float width, float duration, Color color) =>
            Backend != null ? Backend.Line(center, dir, length, width, duration, color) : Stub(center, duration);
        /// <summary>开天：全屏压暗（0..1）。</summary>
        public static void SkyDarken(float amount) => Backend?.SkyDarken(amount);
        /// <summary>开天：天上裂缝亮度（0..1）。</summary>
        public static void SkyCrack(float amount) => Backend?.SkyCrack(amount);
        public static void Shake(float strength, float seconds) => Backend?.Shake(strength, seconds);

        static Handle Stub(Vector3 p, float duration)
        {
            var go = new GameObject("BossTelegraph(stub)");
            go.transform.position = p;
            UnityEngine.Object.Destroy(go, duration);
            return new Handle { go = go };
        }
    }
}
