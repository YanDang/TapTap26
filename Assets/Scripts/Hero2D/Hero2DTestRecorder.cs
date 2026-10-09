using System.IO;
using UnityEngine;

namespace GameJam
{
    // 仅用于 Hero2DTest：镜头水平跟随主角；record 打开时以固定 30 帧/秒推进游戏时间，
    // 每隔 everyN 帧存一张图到 <工程>/Temp/HeroShots/seq_XXXX.png，共 frames 张，然后停止。
    public class Hero2DTestRecorder : MonoBehaviour
    {
        public Transform follow;
        public float followSharpness = 6f;
        public bool record;
        public int frames = 48, everyN = 3, captureFps = 30;
        int frame, saved;
        HeroSpringBones springs; float[] amin, amax;
        string dir;

        void Start()
        {
            if (!record) return;
            Time.captureFramerate = captureFps;
            if (follow)
            {   // 录制时让主角组件也用固定步长的游戏时间
                foreach (var g in follow.GetComponentsInChildren<HeroGait>()) g.useUnscaledTime = false;
                foreach (var g in follow.GetComponentsInChildren<HeroSpringBones>()) g.useUnscaledTime = false;
                foreach (var g in follow.GetComponentsInChildren<HeroBlink>()) { g.useUnscaledTime = false; g.interval = new Vector2(0.5f, 0.7f); }  // 录制时多眨几次便于检查
                foreach (var g in follow.GetComponentsInChildren<HeroFacing>()) g.useUnscaledTime = false;
            }
            springs = follow ? follow.GetComponentInChildren<HeroSpringBones>() : null;
            if (springs) { amin = new float[springs.springs.Length]; amax = new float[springs.springs.Length]; }
            dir = Path.Combine(Application.dataPath, "../Temp/HeroShots");
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "seq_*.png")) File.Delete(f);
        }

        void LateUpdate()
        {
            if (follow)
            {
                var p = transform.position;
                p.x = Mathf.Lerp(p.x, follow.position.x, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
                transform.position = p;
            }
            if (!record || saved >= frames) return;
            if (springs)
                for (int i = 0; i < amin.Length; i++)
                { amin[i] = Mathf.Min(amin[i], springs.springs[i].angle); amax[i] = Mathf.Max(amax[i], springs.springs[i].angle); }
            if (frame++ % everyN == 0)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"seq_{saved:0000}.png"));
                saved++;
                if (saved >= frames)
                {
                    Time.captureFramerate = 0;
                    var sb = new System.Text.StringBuilder("[Hero2D] 录制完成 " + dir + " | 弹簧摆动范围（度）：");
                    if (springs)
                        for (int i = 0; i < amin.Length; i++)
                            sb.Append($" | {springs.springs[i].boneName} {amin[i]:F1}~{amax[i]:F1}/±{springs.springs[i].maxAngle:F0}");
                    Debug.Log(sb.ToString());
                }
            }
        }
    }
}
