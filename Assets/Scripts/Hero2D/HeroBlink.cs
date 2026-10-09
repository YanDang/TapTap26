using UnityEngine;
using UnityEngine.U2D.Animation;

namespace GameJam
{
    // 眨眼：脸上没有眼皮骨，眼白 / 眼珠 / 眼眶三个图层都 100% 绑在「头」上。
    // 启动时把它们改成刚性挂在「头」骨下（关掉 SpriteSkin，效果与蒙皮完全一致）。
    // 「眼眶」图层里还画了下睫毛、雀斑和嘴巴，所以运行时把它按 cutRowFromTop 切成两份（SpriteClipPlane 着色器）：
    //   上半份 = 上睫毛，眨眼时压扁并往下合拢；下半份 = 下睫毛 + 雀斑 + 嘴巴，始终不动。
    // 眼白 / 眼珠同时压扁并下移，合眼位置落在下睫毛附近。
    [DefaultExecutionOrder(100)]
    public class HeroBlink : MonoBehaviour
    {
        public Transform head;
        public SpriteRenderer[] eyeParts;            // 眼白、眼珠
        public SpriteRenderer lid;                   // 眼眶（含嘴巴）
        public Material clipMaterial;                // GameJam/SpriteClipPlane
        public float cutRowFromTop = 48f;            // 眼眶图层里上睫毛与其余部分的分界（像素，从图层顶边算）

        [Header("合眼形状")]
        public float eyeClosedScale = 0f, eyeClosedDrop = 0.15f;      // 眼珠完全压没、落到上睫毛合拢的位置，避免露出一截蓝线
        public float lidClosedScale = 0.4f, lidClosedDrop = 0.15f;

        [Header("节奏")]
        public Vector2 interval = new Vector2(2.2f, 5.5f);
        public float closeTime = 0.06f, holdTime = 0.04f, openTime = 0.09f;
        [Range(0, 1)] public float doubleBlinkChance = 0.2f;
        public bool useUnscaledTime = true;

        [Header("表情（HeroFace 每帧写）")]
        public float hold = -1f;                     // ≥0：至少闭到这个程度（0.3 = 眯眼，1 = 闭眼）；-1 = 不干预
        public bool hideUpper;                       // 上睫毛完全收起（画 > < 眼时用）

        SpriteRenderer upperLid;                    // 克隆出来的上半份
        Vector3[] eyeScale, eyePos;
        Vector3 lidScale, lidPos;
        float cutLocalY;
        MaterialPropertyBlock mpb;
        static readonly int ClipPlaneId = Shader.PropertyToID("_ClipPlane");
        float timer, t = -1f;
        bool pendingDouble;

        void Start()
        {
            foreach (var r in eyeParts) Rigidify(r);
            Rigidify(lid);
            eyeScale = System.Array.ConvertAll(eyeParts, r => r ? r.transform.localScale : Vector3.one);
            eyePos = System.Array.ConvertAll(eyeParts, r => r ? r.transform.localPosition : Vector3.zero);

            if (lid && clipMaterial)
            {
                var s = lid.sprite;
                // 图层顶边往下 cutRowFromTop 像素 → 精灵本地坐标（以 pivot 为原点）
                cutLocalY = (s.rect.height - cutRowFromTop - s.pivot.y) / s.pixelsPerUnit;
                upperLid = Instantiate(lid.gameObject, lid.transform.parent).GetComponent<SpriteRenderer>();
                upperLid.name = lid.name + "_上睫毛";
                lid.sharedMaterial = clipMaterial;
                upperLid.sharedMaterial = clipMaterial;
                lidScale = upperLid.transform.localScale;
                lidPos = upperLid.transform.localPosition;
                mpb = new MaterialPropertyBlock();
            }
            timer = Random.Range(interval.x, interval.y);
        }

        void Rigidify(SpriteRenderer r)
        {
            if (!r || !head) return;
            var skin = r.GetComponent<SpriteSkin>();
            if (skin) skin.enabled = false;            // 绑定姿势下关掉蒙皮，图层就在原位
            r.transform.SetParent(head, true);
        }

        /// <summary>立即眨一次（受击、处决等时机可以调用）。</summary>
        public void Blink() { if (t < 0f) t = 0f; }

        void Update()
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float closed = 0f;    // 0 = 睁开，1 = 闭上
            if (t < 0f)
            {
                timer -= dt;
                if (timer <= 0f)
                {
                    t = 0f;
                    pendingDouble = Random.value < doubleBlinkChance;
                    timer = Random.Range(interval.x, interval.y);
                }
            }
            else
            {
                t += dt;
                if (t < closeTime) closed = t / closeTime;
                else if (t < closeTime + holdTime) closed = 1f;
                else closed = 1f - Mathf.Clamp01((t - closeTime - holdTime) / openTime);
                if (t >= closeTime + holdTime + openTime)
                {
                    closed = 0f;
                    t = -1f;
                    if (pendingDouble) { pendingDouble = false; timer = 0.12f; }
                }
            }
            Apply(hold >= 0f ? Mathf.Max(closed, hold) : closed);
        }

        void Apply(float c)
        {
            if (eyeScale == null) return;            // Play 中热重载代码后 Start 不会再跑
            float ce = Mathf.Clamp01(c * 1.8f);      // 眼珠比睫毛合得快，半闭时不会从睫毛弧线外露出一截
            for (int i = 0; i < eyeParts.Length; i++)
            {
                if (!eyeParts[i]) continue;
                var s = eyeScale[i]; s.y *= Mathf.Lerp(1f, eyeClosedScale, ce);
                eyeParts[i].transform.localScale = s;
                eyeParts[i].transform.localPosition = eyePos[i] + head.InverseTransformVector(Vector3.down * eyeClosedDrop * ce);
            }
            if (upperLid)
            {
                var s = lidScale; s.y *= hideUpper ? 0f : Mathf.Lerp(1f, lidClosedScale, c);
                upperLid.transform.localScale = s;
                upperLid.transform.localPosition = lidPos + head.InverseTransformVector(Vector3.down * lidClosedDrop * c);
            }
        }

        // 每帧按两份眼眶各自的位置更新裁剪平面（头在动、上半份在缩放）
        void LateUpdate()
        {
            if (!upperLid) return;
            SetPlane(lid, -1f);       // 下半份：只留分界线以下
            SetPlane(upperLid, 1f);   // 上半份：只留分界线以上
        }

        void SetPlane(SpriteRenderer r, float side)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            var tr = r.transform;
            var n = tr.up * side;
            var p = tr.TransformPoint(0f, cutLocalY, 0f);
            r.GetPropertyBlock(mpb);
            mpb.SetVector(ClipPlaneId, new Vector4(n.x, n.y, n.z, -Vector3.Dot(n, p)));
            r.SetPropertyBlock(mpb);
        }
    }
}
