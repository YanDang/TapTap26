using System.Collections;
using System.IO;
using UnityEngine;

namespace GameJam
{
    // Play 模式下演示并录制寄居蟹主角的战斗动作（菜单 GameJam/Hero2D/Record Combat Actions）：
    // 在主角右边放一只不还手的鱿鱼 → 锁敌普攻三连 → 按住蓄力砸 → 空挡（贝壳格挡）→ 缩壳翻滚 + 伸臂重拳。
    // 用一台跟随主角的正交特写相机以 30 fps 锁帧录到 Temp/HeroActs/<段名>_####.png。
    public class HeroActionsDemo : MonoBehaviour
    {
        public string outDir = "Temp/HeroActs";
        public float orthoSize = 2.0f;
        public int every = 2;                  // 每几帧存一张
        Camera cam; RenderTexture rt; Texture2D tex;
        string seg = "x"; int frame; bool rec;

        PlayerController P => GameManager.I.player;

        IEnumerator Start()
        {
            Directory.CreateDirectory(outDir);
            foreach (var f in Directory.GetFiles(outDir, "*.png")) File.Delete(f);
            Time.captureFramerate = 30;
            GameInput.Disabled = true;
            var main = Camera.main;
            cam = new GameObject("HeroActsCam").AddComponent<Camera>();
            cam.CopyFrom(main);
            cam.orthographic = true; cam.orthographicSize = orthoSize;
            cam.enabled = false;
            rt = new RenderTexture(720, 720, 24);
            tex = new Texture2D(720, 720, TextureFormat.RGB24, false);
            cam.targetTexture = rt;

            foreach (var e in EnemyAI.All.ToArray()) if (e) Destroy(e.gameObject);
            yield return null;
            P.maxHp = P.hp = 99;
            var right = Vector3.ProjectOnPlane(main.transform.right, Vector3.up).normalized;
            // 找一块离桌子够远的空地（否则左键会变成踢桌子），翻滚往左还要留 3 m
            for (int i = 0; i < 40; i++)
            {
                var c = P.spawnPoint + new Vector3((i % 8 - 4) * 1.5f, 0, (i / 8 - 2) * 1.5f);
                bool clear = true;
                foreach (var t in KickableTable.All)
                    if (DirUtil.Flat(t.transform.position - c).magnitude < 4f || DirUtil.Flat(t.transform.position - (c - right * 3f)).magnitude < 2f) clear = false;
                if (clear) { P.Teleport(c); break; }
            }
            yield return null;
            var b = GameManager.I.SpawnEnemy(EnemyKind.Beast, P.Pos + right * 2.2f);
            b.maxHp = b.hp = 99; b.speed = 0; b.attackRange = 0.1f;
            yield return Rec("0_idle", 0.4f);

            P.RightClickEnemy(b);
            yield return Rec("1_combo", 3.2f);
            System.Func<bool> ready = () => P.state == PlayerController.State.Chase;   // 每步都等主角回到追击再下指令
            yield return RecUntil("1_combo", ready, 0f);

            P.LeftClick();
            yield return Rec("2_charge", 1.3f);
            P.LeftRelease();
            yield return RecUntil("3_smash", ready, 0.1f);

            P.LeftClick(); P.LeftRelease();
            yield return RecUntil("4_guard", ready, 0.1f);

            every = 1;                                  // 钻壳只有 0.3 s，逐帧录
            P.RightClickGround(P.Pos - right * 3f);
            yield return RecUntil("5_shellroll_punch", () => P.state == PlayerController.State.Idle, 0.3f);

            // 红光抓取：缠住 3 s → 丢出去 → 躺地起身
            every = 2;
            b.attackRange = 2.0f; b.grabRange = 3.5f;
            P.RightClickEnemy(b);
            yield return RecUntil("6_grab", ready, 0f);
            b.forceGrab = true; b.forceAttackNow = true;
            yield return RecUntil("6_grab", () => P.state == PlayerController.State.Thrown, 0f);
            every = 1;
            yield return RecUntil("7_thrown_getup", () => !P.Unavailable, 0.3f);

            // 高兴（打倒敌人时随机触发，这里直接触发看效果）
            every = 2;
            if (P.hero && P.hero.Face) P.hero.Face.TriggerHappy(1.5f);
            yield return Rec("8_happy", 1.2f);

            Time.captureFramerate = 0;
            GameInput.Disabled = false;
            Debug.Log("[HeroActs] 录制完成 → " + Path.GetFullPath(outDir));
            Destroy(cam.gameObject);
            Destroy(this);
        }

        // 一直录到条件成立，再多录 tail 秒（主角走真实时间，固定帧数会截不全）
        IEnumerator RecUntil(string name, System.Func<bool> done, float tail)
        {
            if (seg != name) frame = 0;
            seg = name; rec = true;
            float limit = Time.realtimeSinceStartup + 15f;
            yield return null;
            while (!done() && Time.realtimeSinceStartup < limit) yield return null;
            float end = Time.realtimeSinceStartup + tail;
            while (Time.realtimeSinceStartup < end) yield return null;
            rec = false;
        }

        IEnumerator Rec(string name, float seconds)
        {
            seg = name; frame = 0; rec = true;
            float end = Time.realtimeSinceStartup + seconds;    // 主角走真实时间，录制也按真实时间
            while (Time.realtimeSinceStartup < end) yield return null;
            rec = false;
        }

        void LateUpdate()
        {
            if (!rec || !cam) return;
            var main = Camera.main;
            cam.transform.rotation = main.transform.rotation;
            cam.transform.position = P.Pos + Vector3.up * 0.9f - main.transform.forward * 30f + main.transform.right * 0.6f;
            if (frame++ % every != 0) return;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 720, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(outDir, $"{seg}_{frame:D4}.png"), tex.EncodeToPNG());
        }
    }
}
