using UnityEngine;

namespace GameJam
{
    /// <summary>Plays SpriteSet clips on a SpriteRenderer. Player-side animators run on unscaled time.</summary>
    public class SpriteAnimator : MonoBehaviour
    {
        public SpriteSet set;
        public SpriteRenderer sr;
        public bool unscaled;          // true: ignores world slow-mo (still frozen by hitstop)

        public string ClipName { get; private set; }
        public Dir Dir { get; private set; } = Dir.Down;
        public float Time { get; private set; }
        public float Speed { get; set; } = 1;
        Dir _lastHorizontal = Dir.Right;
        SpriteSet.Clip _clip;

        public SpriteSet.Clip Clip => _clip;
        public float Duration => _clip == null ? 0 : _clip.frames / _clip.fps;
        public int FrameRaw => _clip == null ? 0 : Mathf.FloorToInt(Time * _clip.fps);
        public int Frame => _clip == null ? 0 : (_clip.loop ? FrameRaw % _clip.frames : Mathf.Min(FrameRaw, _clip.frames - 1));
        public bool Finished => _clip != null && !_clip.loop && FrameRaw >= _clip.frames;
        public float Normalized => Duration <= 0 ? 0 : Time / Duration;

        public void Play(string clipName, Dir dir, bool restart = false, float speed = 1)
        {
            SetDir(dir);
            Speed = speed;
            if (!restart && clipName == ClipName) return;
            var c = set ? set.Get(clipName) : null;
            if (c == null) { Debug.LogWarning($"[SpriteAnimator] missing clip {clipName} on {name}"); return; }
            _clip = c; ClipName = clipName; Time = 0;
            Apply();
        }

        public void SetDir(Dir d)
        {
            Dir = d;
            if (d == Dir.Left || d == Dir.Right) _lastHorizontal = d;
        }

        void Update()
        {
            if (_clip == null) return;
            float dt = unscaled ? TimeManager.PlayerDelta : UnityEngine.Time.deltaTime;
            Time += dt * Speed;
            Apply();
        }

        void Apply()
        {
            if (_clip == null || sr == null) return;
            int row = SpriteSet.Row(_clip, Dir, _lastHorizontal);
            int i = row * _clip.frames + Frame;
            if (i >= 0 && i < _clip.sprites.Length) sr.sprite = _clip.sprites[i];
        }
    }
}
