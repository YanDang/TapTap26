using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>One-shot SFX from Resources/Audio. "clean" sounds bypass the slow-mo low-pass (e.g. 通透世界, 拔刀).</summary>
    public static class Sfx
    {
        static Dictionary<string, AudioClip> _clips;
        static AudioSource _world, _clean;

        static void Init()
        {
            if (_world) return;
            _clips = new Dictionary<string, AudioClip>();
            foreach (var c in Resources.LoadAll<AudioClip>("Audio")) _clips[c.name] = c;
            var go = new GameObject("Sfx");
            Object.DontDestroyOnLoad(go);
            _world = go.AddComponent<AudioSource>();
            _clean = go.AddComponent<AudioSource>();
            _world.playOnAwake = _clean.playOnAwake = false;
            _clean.bypassListenerEffects = true;
        }

        public static void Play(string name, float volume = 1f, bool clean = false)
        {
            Init();
            if (!_clips.TryGetValue(name, out var clip)) { Debug.LogWarning("[Sfx] missing " + name); return; }
            var src = clean ? _clean : _world;
            src.pitch = (!clean && TimeManager.SlowMo) ? 0.75f : 1f;
            src.PlayOneShot(clip, volume);
        }
    }
}
