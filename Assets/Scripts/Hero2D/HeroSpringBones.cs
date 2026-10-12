using UnityEngine;

namespace GameJam
{
    // 2D 弹簧骨：头发 / 辫子 / 流苏 / 项链 / 贝壳 等次级运动。
    // 每根骨在角色平面内是一个角度弹簧：父骨加速运动时骨头因惯性往反方向甩，父骨转动时落后半拍，
    // 然后按刚度和阻尼回到原角度。按父 → 子顺序更新，所以链条（辫子_1 → 辫子_2）会自然传递。
    // 在 IK（-2）和蒙皮（-1）之前执行；这些骨不在任何 IK 链上。
    [DefaultExecutionOrder(-5)]
    public class HeroSpringBones : MonoBehaviour
    {
        [System.Serializable]
        public class Spring
        {
            public string boneName;
            public Transform bone;
            public float stiffness = 120f;   // 回弹力
            public float damping = 12f;      // 阻尼（越大越不晃）
            public float inertia = 0.05f;    // 对加速度的敏感度
            public float rotationLag = 0.6f; // 父骨转动时落后的比例
            public float maxAngle = 20f;     // 最大偏离（度）
            public float sway = 0f;          // 微风轻摆幅度（度）：静止时也会轻轻晃
            public float swayRate = 0.6f;    // 轻摆快慢（次/秒）
            public float stepKick = 0f;      // 每次脚落地给的冲击（度/秒）
            [HideInInspector] public Quaternion restLocal;
            [HideInInspector] public float angle, velocity, lastParentAngle;
            [HideInInspector] public Vector2 lastHead, lastVel;
            [HideInInspector] public bool primed;
            [HideInInspector] public float seed, dirX;
        }

        public bool useUnscaledTime = true;
        public float maxAcceleration = 400f;   // 防止瞬移（复活、传送）时甩飞
        public float inertiaScale = 1f;        // 整体惯性倍率（角色移动很快时调小，免得头发一直甩到极限）
        public Transform motion;               // 读取整体移动的节点（默认自身；游戏里是主角根节点）
        public Spring[] springs;

        HeroFacing facing;
        float swayT;

        void Start()
        {
            facing = GetComponent<HeroFacing>();
            var gait = GetComponent<HeroGait>();
            if (gait) gait.OnFootDown += Kick;
            for (int i = 0; i < springs.Length; i++) springs[i].seed = i * 7.31f + Random.value * 100f;
            foreach (var s in springs)
                if (s.bone) s.restLocal = s.bone.localRotation;
        }

        float PlaneAngle(Transform t)
        {
            var d = transform.InverseTransformVector(t.right);   // 含缩放：角色镜像（朝左）时方向也正确
            return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        }

        void LateUpdate()
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (dt <= 0f) return;
            dt = Mathf.Min(dt, 1f / 20f);

            if (facing && facing.IsTurning)
            {
                foreach (var sp in springs)
                    if (sp.bone) { sp.bone.localRotation = sp.restLocal * Quaternion.Euler(0, 0, sp.angle); sp.primed = false; }
                rootPrimed = false;
                return;
            }

            // 角色整体的世界位移也要算进惯性：把根节点这一帧的位移换到角色平面
            if (!motion) motion = transform;
            var rd = rootPrimed ? transform.InverseTransformVector(motion.position - lastRootWorld) : Vector3.zero;
            var rootDelta = new Vector2(rd.x, rd.y);
            foreach (var s in springs)
            {
                if (!s.bone) continue;
                s.bone.localRotation = s.restLocal;                 // 先回到原角度，再叠加弹簧偏移
                var parent = s.bone.parent;
                var headW = s.bone.position;
                var head = (Vector2)transform.InverseTransformPoint(headW);
                float parentAngle = parent ? PlaneAngle(parent) : 0f;
                if (!s.primed)
                {
                    s.lastHead = head; s.lastVel = Vector2.zero; s.lastParentAngle = parentAngle; s.primed = true;
                    continue;
                }

                var vel = (head - s.lastHead + rootDelta) / dt;
                var acc = Vector2.ClampMagnitude((vel - s.lastVel) / dt, maxAcceleration);
                s.lastHead = head; s.lastVel = vel;

                // 骨的方向（平面内）
                var dir3 = transform.InverseTransformVector(s.bone.right);
                var dir = new Vector2(dir3.x, dir3.y).normalized;
                // 惯性力矩：骨头甩向加速度的反方向
                float torque = (dir.x * -acc.y - dir.y * -acc.x) * s.inertia * inertiaScale * 60f;
                // 父骨转动：落后
                float dParent = Mathf.DeltaAngle(s.lastParentAngle, parentAngle);
                s.lastParentAngle = parentAngle;
                s.angle -= dParent * s.rotationLag;

                // 平衡位置随噪声轻摆（Perlin 平滑随机，每缕头发节奏不同）
                float swayTarget = s.sway > 0f ? (Mathf.PerlinNoise(swayT * s.swayRate, s.seed) * 2f - 1f) * s.sway : 0f;
                s.dirX = dir.x;
                s.velocity += (-s.stiffness * (s.angle - swayTarget) - s.damping * s.velocity + torque) * dt;
                s.angle += s.velocity * dt;
                if (Mathf.Abs(s.angle) > s.maxAngle)
                {
                    s.angle = Mathf.Sign(s.angle) * s.maxAngle;
                    s.velocity *= -0.2f;
                }
                s.bone.localRotation = s.restLocal * Quaternion.Euler(0, 0, s.angle);
            }
            swayT += dt;
            rootPrimed = true;
            lastRootWorld = motion.position;
        }

        Vector3 lastRootWorld; bool rootPrimed;

        // 脚落地 = 身体向下一顿：横向伸出的骨（辫子、发梢）被甩得最明显，竖直下垂的骨几乎不受影响，再叠一点随机让左右不完全对称
        void Kick(float strength)
        {
            foreach (var s in springs)
                if (s.bone && s.stepKick > 0f)
                    s.velocity += s.stepKick * strength * (Mathf.Sign(s.dirX) * Mathf.Max(Mathf.Abs(s.dirX), 0.35f)) * Random.Range(0.75f, 1.15f);
        }
    }
}
