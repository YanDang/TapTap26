using UnityEngine;

namespace GameJam
{
    // 寄居蟹主角的程序化步态：按移动速度推进步频，四条腿对角成对交替（小跑步态），
    // 直接写 IK 目标（角色本地坐标），身体随步伐上下起伏、向前微倾；停下时脚回到原位并做呼吸起伏。
    // 只读角色根节点的位移，不关心是谁在移动它（玩家控制器 / 测试脚本 / 动画都行）。
    [DefaultExecutionOrder(-10)]
    public class HeroGait : MonoBehaviour
    {
        [System.Serializable]
        public class Leg
        {
            public string name;
            public Transform target;
            [Range(0, 1)] public float phaseOffset;
            [HideInInspector] public Vector3 rest;
        }

        [Header("步态")]
        public float strideLength = 0.9f;       // 一步在地面上滑过的距离（单位）
        public float stepHeight = 0.35f;        // 抬脚高度
        [Range(0.3f, 0.9f)] public float dutyFactor = 0.6f;   // 着地时间占比
        public float minCadence = 1.6f;         // 原地踏步（朝屏幕里外走）时的最低步频（步/秒）
        public float fullSpeed = 3f;            // 达到这个速度时步伐完全展开
        public float blendSpeed = 6f;           // 起步 / 停下的过渡快慢
        public float maxCadence = 4.5f;         // 步频上限（步/秒）：移动很快时宁可让脚略微打滑，也不让腿抽搐
        public Transform motion;                // 读取移动的节点（默认自身；游戏里是主角根节点）

        [Header("身体")]
        public Transform body;                  // 身体根
        public Transform chest;                 // 上身
        public float bobAmount = 0.08f;
        public float leanDegrees = 4f;
        public float breathAmount = 0.025f;
        public float breathRate = 0.35f;        // 次/秒

        public bool useUnscaledTime = true;     // 主角不受子弹时间影响
        public Leg[] legs;

        Vector3 lastPos;
        float phase, weight, depthWeight, lean;
        Vector3 bodyRest; Quaternion bodyRestRot, chestRestRot;
        float breathT;

        public float CurrentSpeed { get; private set; }
        /// <summary>某只脚落地（从摆动进入着地）时触发，参数 = 当前步伐强度 0..1，给头发等做“踩步弹跳”。</summary>
        public event System.Action<float> OnFootDown;
        float lastPhase;
        HeroFacing facing;

        void Start()
        {
            facing = GetComponent<HeroFacing>();
            foreach (var l in legs) if (l.target) l.rest = l.target.localPosition;
            if (body) { bodyRest = body.localPosition; bodyRestRot = body.localRotation; }
            if (chest) chestRestRot = chest.localRotation;
            if (!motion) motion = transform;
            lastPos = motion.position;
        }

        void LateUpdate()
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (dt <= 0f) return;

            if (!motion) { motion = transform; lastPos = motion.position; }    // Play 中热重载后 Start 不会再跑
            var delta = motion.position - lastPos;
            lastPos = motion.position;
            // 不含缩放的角色平面坐标（转身压扁时 scale.x≈0，不能用 InverseTransformVector），再按朝向翻转
            // 再除以角色缩放，换成角色自身的单位，步幅才和身体比例对得上（用 y 缩放，转身时 x≈0）
            var local = Quaternion.Inverse(transform.rotation) * delta / Mathf.Max(Mathf.Abs(transform.lossyScale.y), 1e-4f);
            local.x *= facing ? facing.Facing : (transform.lossyScale.x < 0 ? -1f : 1f);
            float speed = local.magnitude / dt;
            CurrentSpeed = speed;
            float sideSpeed = local.x / dt;                              // 屏幕横向（会产生步幅）
            float moving = Mathf.Clamp01(speed / fullSpeed);

            weight = Mathf.MoveTowards(weight, moving > 0.05f ? Mathf.Max(moving, 0.35f) : 0f, blendSpeed * dt);
            float sideFrac = speed > 0.01f ? Mathf.Clamp(sideSpeed / speed, -1f, 1f) : 0f;
            depthWeight = Mathf.MoveTowards(depthWeight, sideFrac, blendSpeed * dt);

            // 步频：横向移动由步幅决定，纵深移动用最低步频原地踏步
            float cadence = Mathf.Max(Mathf.Abs(sideSpeed) / Mathf.Max(strideLength, 0.01f), speed > 0.05f ? minCadence : 0f);
            lastPhase = phase;
            phase = Mathf.Repeat(phase + Mathf.Min(cadence, maxCadence) * dt, 1f);

            foreach (var l in legs)
            {
                if (!l.target) continue;
                float p = Mathf.Repeat(phase + l.phaseOffset, 1f);
                float pPrev = Mathf.Repeat(lastPhase + l.phaseOffset, 1f);
                if (weight > 0.05f && pPrev > p && OnFootDown != null) OnFootDown(weight);   // 相位绕回 0 = 刚着地
                float x, y;
                if (p < dutyFactor)
                {   // 着地：脚相对身体从前往后滑
                    float t = p / dutyFactor;
                    x = Mathf.Lerp(0.5f, -0.5f, t);
                    y = 0f;
                }
                else
                {   // 摆动：抬起、往前送
                    float t = (p - dutyFactor) / (1f - dutyFactor);
                    float e = t * t * (3f - 2f * t);
                    x = Mathf.Lerp(-0.5f, 0.5f, e);
                    y = Mathf.Sin(t * Mathf.PI);
                }
                var off = new Vector3(x * strideLength * depthWeight, y * stepHeight, 0f) * weight;
                l.target.localPosition = l.rest + off;
            }

            // 身体：走动时随步伐起伏（每个半周期一次），前倾；静止时呼吸
            breathT += dt * breathRate;
            float bob = -Mathf.Abs(Mathf.Sin(phase * Mathf.PI * 2f)) * bobAmount * weight;
            float breath = Mathf.Sin(breathT * Mathf.PI * 2f) * breathAmount * (1f - weight);
            lean = Mathf.Lerp(lean, -leanDegrees * depthWeight * weight, 1f - Mathf.Exp(-8f * dt));
            if (body)
            {
                body.localPosition = bodyRest + new Vector3(0f, bob + breath, 0f);
                body.localRotation = bodyRestRot * Quaternion.Euler(0, 0, lean * 0.5f);
            }
            if (chest)
                chest.localRotation = chestRestRot * Quaternion.Euler(0, 0, lean * 0.5f + breath * 20f);
        }
    }
}
