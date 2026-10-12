using UnityEngine;

namespace GameJam
{
    // 朝向与转身：按“相对镜头的横向速度”决定朝左 / 朝右，转身时横向压扁再展开（纸片翻转），
    // 中途略微纵向拉伸，比瞬间翻转顺眼。以后做 4 方向时，正面 / 背面视图的切换也放在这里（压到最扁时换图）。
    [DefaultExecutionOrder(-20)]
    public class HeroFacing : MonoBehaviour
    {
        public float turnTime = 0.14f;
        public float turnThreshold = 0.25f;     // 横向速度超过它才转身（单位/秒），避免纵深移动时来回抖
        public float stretch = 0.06f;
        public bool useUnscaledTime = true;
        public Transform target;                // 被翻转的节点（默认自身；游戏里是专门的翻转节点，保证绕身体中心翻）
        public Transform motion;                // 读取移动的节点（默认自身；游戏里是主角根节点）

        public int lockSide;                    // ≠0：锁定朝向（攻击 / 翻滚时一直面向敌人），不随移动自动转身

        public int Facing { get; private set; } = 1;        // +1 = 美术原图朝向，-1 = 翻转
        public bool IsTurning => t >= 0f;

        float t = -1f; int from = 1;
        Vector3 baseScale, lastPos;

        void Start()
        {
            if (!target) target = transform;
            if (!motion) motion = transform;
            baseScale = target.localScale;
            baseScale.x = Mathf.Abs(baseScale.x);
            Facing = target.localScale.x < 0 ? -1 : 1;
            lastPos = motion.position;
        }

        /// <summary>直接指定朝向（锁敌时面向目标等）。</summary>
        public void Face(int dir)
        {
            dir = dir < 0 ? -1 : 1;
            if (dir == Facing || IsTurning) return;
            from = Facing; Facing = dir; t = 0f;
        }

        void Update()
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (dt <= 0f) return;
            if (!motion) { motion = transform; lastPos = motion.position; }    // Play 中热重载后 Start 不会再跑
            if (!target) { target = transform; baseScale = target.localScale; baseScale.x = Mathf.Abs(baseScale.x); }
            var delta = motion.position - lastPos;
            lastPos = motion.position;
            var cam = Camera.main;
            var right = cam ? cam.transform.right : Vector3.right;
            float side = Vector3.Dot(delta, right) / dt;
            if (lockSide != 0) Face(lockSide);
            else if (Mathf.Abs(side) > turnThreshold) Face(side > 0 ? 1 : -1);

            var s = baseScale;
            if (IsTurning)
            {
                t += dt / Mathf.Max(turnTime, 0.01f);
                float k = Mathf.Clamp01(t);
                // from → 0 → Facing：前半段压扁，后半段展开
                float x = Mathf.Cos(k * Mathf.PI);              // 1 → -1
                s.x = baseScale.x * from * x;
                s.y = baseScale.y * (1f + stretch * Mathf.Sin(k * Mathf.PI));
                if (t >= 1f) t = -1f;
            }
            if (!IsTurning) s.x = baseScale.x * Facing;
            target.localScale = s;
        }
    }
}
