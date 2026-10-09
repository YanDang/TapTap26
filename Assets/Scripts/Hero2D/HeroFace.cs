using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    // 主角表情：在原脸上叠程序画的表情贴片（Resources/Hero2D/Face），眼睛开合交给 HeroBlink。
    //   严肃（普攻）  ：眼睛上沿压一条斜眼线
    //   愤怒（蓄力 / 砸 / 处决）：斜眼线 + 咬牙 + 青筋（一跳一跳）
    //   痛苦（受击 / 被缠 / 被丢）：> < 眼（原眼闭上、上睫毛收起）+ 眼泪往下流 + 汗滴 + 张嘴哭
    //   高兴（打倒敌人时随机）：^ ^ 眼（同上）+ 张嘴笑 + 星星闪
    // 眼睛效果按用户在 E:\GameJamArt\hero2d\shots\{愤怒,高兴,痛苦}.png 上的标注画（2026-10-08）
    // 另外 wrapped = true 时腰上显示缠着的触手（挤压时一鼓一鼓）。
    // PlayerController 每帧 SetBase；HideBody（缩壳）期间整个隐藏由 HeroActions 处理（这里只用 SetActive，不碰 enabled）。
    [DefaultExecutionOrder(110)]    // 在 HeroBlink（100）的 Start 之后：要用它切好的眼眶图层
    public class HeroFace : MonoBehaviour
    {
        public enum Expr { Neutral, Angry, Pain, Happy }   // 严肃已删（用户 2026-10-08：普攻时无表情）

        public Expr Current { get; private set; }
        public bool wrapped;

        [Header("表情调整窗口用（GameJam/表情调整）")]
        public bool forceOn;                    // 强制显示某个表情（忽略战斗状态）
        public Expr forced;
        public bool forceWrapped;

        Expr baseExpr;
        float happyUntil = -1f, squeeze;
        HeroBlink blink;
        readonly Dictionary<string, SpriteRenderer> items = new Dictionary<string, SpriteRenderer>();
        Transform lidT;
        float docWorld;            // 1 文档像素的世界长度
        bool ready;


        public void SetBase(Expr e) => baseExpr = e;
        public void TriggerHappy(float seconds = 1.3f) => happyUntil = Time.unscaledTime + seconds;
        public void Squeeze() => squeeze = 1f;

        void Start()
        {
            blink = GetComponent<HeroBlink>();
            if (!blink || !blink.lid || !blink.head) { enabled = false; return; }
            var lid = blink.lid;
            lidT = lid.transform;
            Transform body = null;
            int maxOrder = int.MinValue;
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true)) maxOrder = Mathf.Max(maxOrder, r.sortingOrder);
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "身体根" && !t.GetComponent<UnityEngine.U2D.Animation.SpriteSkin>()) body = t;

            // 「眼眶」图层在 PSB 里的左上角（文档像素），make_face_sprites.py 的坐标以文档为准
            var docMin = new Vector2(491f, 309f);
            if (Mathf.Abs(lid.sprite.rect.width - 210f) > 1f || Mathf.Abs(lid.sprite.rect.height - 123f) > 1f)
                Debug.LogWarning($"[HeroFace] 眼眶图层尺寸变了（{lid.sprite.rect.size}），表情位置可能偏");
            docWorld = Mathf.Abs(lidT.lossyScale.y) / lid.sprite.pixelsPerUnit;

            foreach (var e in FaceOverlay.Get().hero)
            {
                bool overHair = e.name == "vein" || e.name == "sweat" || e.name.StartsWith("sparkle");
                bool isCoil = e.name == "coil";
                int order = overHair ? maxOrder + 3 : isCoil ? maxOrder + 1 : lid.sortingOrder + 3 + (e.name.StartsWith("patch") ? 0 : 1);
                var parent = isCoil && body ? body : blink.head;
                var sr = FaceOverlay.Create("hero", e, "Hero2D/Face", lid, docMin, 210f, parent, order);
                if (!sr) continue;
                items[e.name] = sr;
            }
            ready = true;
        }

        void Show(string n, bool on)
        {
            if (items.TryGetValue(n, out var r) && r.gameObject.activeSelf != on) r.gameObject.SetActive(on);
        }

        void LateUpdate()
        {
            if (!ready) return;
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            var e = baseExpr;
            if (e != Expr.Pain && now < happyUntil) e = Expr.Happy;
            if (forceOn) e = forced;
            bool showCoil = forceOn ? forceWrapped : wrapped;
            Current = e;

            bool pain = e == Expr.Pain, happy = e == Expr.Happy, angry = e == Expr.Angry;
            // 嘴巴保持原画（用户 2026-10-08：所有表情的嘴都和无表情一样），嘴部贴图不再显示
            Show("patch_mouth", false);
            Show("mouth_pain", false);
            Show("mouth_happy", false);
            Show("mouth_grit", false);
            Show("eye_pain_L", pain); Show("eye_pain_R", pain);
            Show("tear_L", pain); Show("tear_R", pain); Show("sweat", pain);
            Show("eye_angry_L", angry); Show("eye_angry_R", angry);
            Show("eye_happy_L", happy); Show("eye_happy_R", happy);
            Show("patch_lash_L", pain || happy); Show("patch_lash_R", pain || happy);   // 盖住下睫毛
            Show("patch_blue", pain || happy);       // 闭眼时盖住皮肤图层上的蓝色涂抹
            Show("vein", angry);
            Show("sparkle_1", happy); Show("sparkle_2", happy);
            Show("coil", showCoil);

            var tw = FaceOverlay.Tweaks;
            blink.hold = pain || happy ? 1f : angry ? tw.angrySquint : -1f;
            blink.hideUpper = pain || happy || angry;   // 愤怒：原上睫毛收起，由贴图里压低的眼皮代替

            if (FaceItem.Editing) return;   // 调整中：不播小动画
            // 小动画：眼泪往下流并循环、汗滴下滑、青筋一跳一跳、星星闪、触手随挤压一鼓
            Vector3 down = -lidT.up * docWorld;
            if (pain)
            {
                Slide("tear_L", down * (Mathf.Repeat(now * 1.6f, 1f) * 26f));
                Slide("tear_R", down * (Mathf.Repeat(now * 1.6f + 0.45f, 1f) * 26f));
                Slide("sweat", down * (Mathf.PingPong(now * 0.8f, 1f) * 8f));
            }
            if (angry) Pulse("vein", 1f + 0.18f * Mathf.Abs(Mathf.Sin(now * 9f)));
            if (happy)
            {
                Pulse("sparkle_1", 0.75f + 0.35f * Mathf.Abs(Mathf.Sin(now * 7f)));
                Pulse("sparkle_2", 0.75f + 0.35f * Mathf.Abs(Mathf.Sin(now * 7f + 1.3f)));
            }
            squeeze = Mathf.MoveTowards(squeeze, 0f, dt * 4f);
            if (showCoil && items.TryGetValue("coil", out var coil))
            {
                var s = coil.GetComponent<FaceItem>().RestScale;
                float k = 1f + 0.06f * Mathf.Sin(now * 5f) - 0.12f * squeeze;
                coil.transform.localScale = new Vector3(s.x * (2f - k), s.y * k, s.z);
            }
        }

        void Slide(string n, Vector3 worldOffset)
        {
            if (!items.TryGetValue(n, out var r)) return;
            var p = r.transform.parent;
            r.transform.localPosition = r.GetComponent<FaceItem>().RestPos + p.InverseTransformVector(worldOffset);
        }

        void Pulse(string n, float k)
        {
            if (items.TryGetValue(n, out var r)) r.transform.localScale = r.GetComponent<FaceItem>().RestScale * k;
        }
    }
}
