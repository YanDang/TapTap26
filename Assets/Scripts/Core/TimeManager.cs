using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// World time = Time.timeScale (enemies, projectiles, VFX). The player runs on unscaled time so they keep
    /// full speed during the parry slow-mo; hitstop freezes everyone.
    /// </summary>
    public class TimeManager : MonoBehaviour
    {
        private static TimeManager _i;
        public static TimeManager I
        {
            get
            {
                if (_i == null)
                {
                    _i = FindObjectOfType<TimeManager>();
                    if (_i == null)
                    {
                        var go = new GameObject("TimeManager");
                        _i = go.AddComponent<TimeManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _i;
            }
            set => _i = value;
        }

        public float slowScale = 0.1f;
        float _slowUntil = -1, _slowStart = -1, _hitstopUntil = -1;
        private bool overlayPaused;
        private float overlayPauseStarted;
        private float Clock => overlayPaused ? overlayPauseStarted : UnityEngine.Time.unscaledTime;

        public static bool SlowMo => I != null && I.Clock < I._slowUntil;
        public static bool Hitstop => I != null && I.Clock < I._hitstopUntil;
        public static float SlowMoRemaining => I != null ? Mathf.Max(0, I._slowUntil - I.Clock) : 0;
        public static float SlowMoElapsed => (I != null && SlowMo) ? I.Clock - I._slowStart : 0;

        /// <summary>Delta time for the player (and other "unscaled" actors): real time, but 0 during hitstop.</summary>
        public static float PlayerDelta => Hitstop ? 0 : Mathf.Min(UnityEngine.Time.unscaledDeltaTime, 0.05f);

        void Awake() { if (_i == null) _i = this; Apply(); }

        public void StartSlowMo(float realSeconds)
        {
            _slowStart = Clock;
            _slowUntil = _slowStart + realSeconds;
            Apply();
        }

        public void EndSlowMo() { _slowUntil = -1; Apply(); }

        public void DoHitstop(float realSeconds)
        {
            _hitstopUntil = Mathf.Max(_hitstopUntil, Clock + realSeconds);
            Apply();
        }

        void Update() => Apply();

        public void Refresh() => Apply();

        void Apply()
        {
            bool blocked = PlayerSessionData.IsCombatInputBlocked;
            if (blocked && !overlayPaused)
            {
                overlayPauseStarted = UnityEngine.Time.unscaledTime;
                overlayPaused = true;
            }
            else if (!blocked && overlayPaused)
            {
                float duration = UnityEngine.Time.unscaledTime - overlayPauseStarted;
                if (_slowUntil > overlayPauseStarted) { _slowUntil += duration; _slowStart += duration; }
                if (_hitstopUntil > overlayPauseStarted) _hitstopUntil += duration;
                overlayPaused = false;
            }
            float ts = Hitstop || blocked ? 0f : (SlowMo ? slowScale : 1f);
            UnityEngine.Time.timeScale = ts;
            UnityEngine.Time.fixedDeltaTime = 0.02f * Mathf.Max(ts, 0.05f);
        }
    }
}
