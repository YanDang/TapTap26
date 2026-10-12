using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// World time = Time.timeScale (enemies, projectiles, VFX). The player runs on unscaled time so they keep
    /// full speed during the parry slow-mo; hitstop freezes everyone.
    /// </summary>
    public class TimeManager : MonoBehaviour
    {
        public static TimeManager I;
        public float slowScale = 0.1f;
        float _slowUntil = -1, _slowStart = -1, _hitstopUntil = -1;

        public static bool SlowMo => I && UnityEngine.Time.unscaledTime < I._slowUntil;
        public static bool Hitstop => I && UnityEngine.Time.unscaledTime < I._hitstopUntil;
        public static float SlowMoRemaining => I ? Mathf.Max(0, I._slowUntil - UnityEngine.Time.unscaledTime) : 0;
        public static float SlowMoElapsed => I && SlowMo ? UnityEngine.Time.unscaledTime - I._slowStart : 0;

        /// <summary>Delta time for the player (and other "unscaled" actors): real time, but 0 during hitstop.</summary>
        public static float PlayerDelta => Hitstop ? 0 : Mathf.Min(UnityEngine.Time.unscaledDeltaTime, 0.05f);

        void Awake() { I = this; Apply(); }

        public void StartSlowMo(float realSeconds)
        {
            _slowStart = UnityEngine.Time.unscaledTime;
            _slowUntil = _slowStart + realSeconds;
            Apply();
        }

        public void EndSlowMo() { _slowUntil = -1; Apply(); }

        public void DoHitstop(float realSeconds)
        {
            _hitstopUntil = Mathf.Max(_hitstopUntil, UnityEngine.Time.unscaledTime + realSeconds);
            Apply();
        }

        void Update() => Apply();

        void Apply()
        {
            float ts = Hitstop || PlayerSessionData.isCraftingOpen ? 0f : (SlowMo ? slowScale : 1f);   // 合成工坊开着 = 战斗暂停
            UnityEngine.Time.timeScale = ts;
            UnityEngine.Time.fixedDeltaTime = 0.02f * Mathf.Max(ts, 0.05f);
        }
    }
}
