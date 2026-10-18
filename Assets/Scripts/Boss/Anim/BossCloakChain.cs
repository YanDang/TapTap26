using System;
using UnityEngine;
namespace GameJam.Boss
{
    /// <summary>Pure managed position solver; points include the tip, so eight bones use nine points.</summary>
    public sealed class BossCloakSolver
    {
        public Vector3[] Points { get; private set; } = new Vector3[0];
        public bool DriveRoot { get; set; }
        // Rest-shape envelope, in degrees per segment. 180 keeps the free action solver.
        // A narrow envelope preserves a curved cut of cloth, not a zero-curvature straight rod.
        public float TargetAngleLimit { get; set; } = 180f;
        Vector3[] previous = new Vector3[0];
        float[] lengths = new float[0];
        public void Reset(Vector3[] targets, bool rebindLengths = false)
        {
            Points = (Vector3[])targets.Clone(); previous = (Vector3[])targets.Clone();
            if(rebindLengths || lengths.Length != targets.Length-1) {
                lengths = new float[Math.Max(0, targets.Length - 1)];
                for (int i = 0; i < lengths.Length; i++) lengths[i] = Math.Max(.0001f, (targets[i+1]-targets[i]).magnitude);
            }
        }
        public void Translate(Vector3 delta)
        {
            for (int i=0;i<Points.Length;i++) { Points[i]+=delta; previous[i]+=delta; }
        }
        public void Mirror(float x)
        {
            for (int i = 0; i < Points.Length; i++)
            { Points[i].x = 2f*x-Points[i].x; previous[i].x = 2f*x-previous[i].x; }
        }
        public void Step(Vector3[] targets, float dt, float gravity, float damping, float stiffness, float maxBend)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            if (targets == null || targets.Length < 2) return;
            if (Points.Length != targets.Length) Reset(targets);
            // Even zero-time Play/teleport must project the new anchor (no integration).
            int steps = Math.Max(1, (int)Math.Ceiling(dt * 120f));
            float h = dt / steps;
            float drag = (float)Math.Pow(1f - Math.Max(0f, Math.Min(.99f, damping)), h * 60f);
            for (int s = 0; s < steps; s++)
            {
                Points[0] = previous[0] = targets[0];
                for (int i = 1; i < Points.Length; i++)
                {
                    Vector3 velocity = h > 0f ? (Points[i] - previous[i]) * drag : Vector3.zero;
                    previous[i] = Points[i];
                    Points[i] += velocity + new Vector3(0f, -gravity * h * h, 0f);
                    float pull = 1f - (float)Math.Exp(-Math.Max(0f, stiffness) * 6f * (1f - .7f*i/(Points.Length-1)) * h);
                    Points[i] += (targets[i] - Points[i]) * pull;
                }
                // A forward projection pins the root and exactly preserves every length and bend.
                for (int iteration = 0; iteration < 4; iteration++)
                for (int i = 1; i < Points.Length; i++)
                {
                    Vector3 d = DriveRoot && i == 1 ? targets[1]-targets[0] : Points[i]-Points[i-1];
                    if (d.sqrMagnitude < 1e-10f) d = targets[i]-targets[i-1];
                    double angle = Math.Atan2(d.y, d.x);
                    if (TargetAngleLimit < 180f)
                    {
                        Vector3 guide = targets[i]-targets[i-1];
                        double a = Math.Atan2(guide.y, guide.x);
                        double delta = Math.Atan2(Math.Sin(angle-a), Math.Cos(angle-a));
                        double limit = Math.Max(0f, TargetAngleLimit) * Math.PI/180.0;
                        angle = a + Math.Max(-limit, Math.Min(limit, delta));
                    }
                    if (i > 1)
                    {
                        Vector3 parent = Points[i-1]-Points[i-2];
                        double a = Math.Atan2(parent.y, parent.x);
                        double delta = Math.Atan2(Math.Sin(angle-a), Math.Cos(angle-a));
                        double limit = Math.Max(0f, maxBend) * Math.PI/180.0;
                        angle = a + Math.Max(-limit, Math.Min(limit, delta));
                    }
                    // A paper cape never points through its own shoulder. Project into
                    // the downward half-plane; parent already lies here, so this convex
                    // angular interval also preserves the adjacent-bend bound.
                    angle = Math.Atan2(Math.Sin(angle), Math.Cos(angle));
                    if (angle > 0.0) angle = angle < Math.PI*.5 ? 0.0 : -Math.PI;
                    Points[i] = Points[i-1] + new Vector3((float)Math.Cos(angle), (float)Math.Sin(angle), 0f) * lengths[i-1];
                }
            }
        }
    }

    /// <summary>Eight-bone cloth: animate the first two bones, then Step; standalone late update is supported.
    /// Manual Step marks the frame, so runtime LateUpdate never integrates that same frame twice.
    /// References may be assigned after Awake. ResetChain after a teleport; facing mirrors both histories.</summary>
    [DefaultExecutionOrder(10)]
    public sealed class BossCloakChain : MonoBehaviour
    {
        public Transform[] bones = new Transform[0];
        public float gravity = 9f;
        [Range(0f, 1f)] public float damping = .12f, stiffness = .18f;
        [Range(0f, 180f)] public float maxBend = 35f;
        public float teleportDistance = 3f;
        public bool automatic = true;
        [NonSerialized] public float targetAngleLimit = 180f;
        /// <summary>模拟所在的坐标系（Boss 根，不随左右翻转）。Boss 套了公告板后纸片不再躺在世界 XY 平面，所以位置/重力/旋转都在这个局部坐标里算（Claude 2026-10-18）。空 = 世界坐标。</summary>
        public Transform space;
        [NonSerialized] public float wrap, collapse;
        [NonSerialized] public int facing = 1;
        readonly BossCloakSolver solver = new BossCloakSolver { DriveRoot = true };
        Vector3[] target = new Vector3[0];
        Vector3[] axes = new Vector3[0];
        Transform[] bound = new Transform[0];
        float tipLength;
        int manualFrame = -1;
        public Vector3[] Points => solver.Points;
        public void Initialize()
        {
            if (bones == null || bones.Length == 0) return;
            bool changed = bound.Length != bones.Length;
            if (!changed) for (int i = 0; i < bones.Length; i++) if (bound[i] != bones[i]) changed = true;
            if (!changed) return;
            foreach (var b in bones) if (!b) return;
            bound = (Transform[])bones.Clone();
            axes = new Vector3[bones.Length]; target = new Vector3[bones.Length+1];
            for (int i = 0; i < bones.Length; i++)
            {
                Vector3 direction = i+1 < bones.Length ? bones[i+1].position-bones[i].position : bones[i].position-bones[Math.Max(0,i-1)].position;   // 世界向量（axes 存骨骼本地）
                if (direction.sqrMagnitude < 1e-8f) direction = Vector3.down * .3f;
                axes[i] = bones[i].InverseTransformVector(direction).normalized;
                if (i == bones.Length-1) tipLength = ToLocalV(direction).magnitude;
            }
            Capture(); solver.Reset(target, true);
        }
        void Capture()
        {
            for (int i = 0; i < bound.Length; i++) target[i] = ToLocalP(bound[i].position);
            int last = bound.Length-1;
            target[bound.Length] = target[last] + ToLocalV(bound[last].TransformVector(axes[last])).normalized * tipLength;
            // Continuous guide forces, not per-bone animation keys: bag around the front 2m,
            // then a low drape over the fallen body. Actual positions still obey Verlet constraints.
            for (int i = 1; i < target.Length; i++)
            {
                float u = (float)i/(target.Length-1);
                Vector3 bag = target[0] + new Vector3(facing*2f*Mathf.Sin(u*Mathf.PI*.5f), -1.1f*Mathf.Sin(u*Mathf.PI), 0f);
                target[i] = Vector3.Lerp(target[i], bag, wrap);
                Vector3 shroud = target[0] + new Vector3(-facing*u*3f, -.15f*Mathf.Sin(u*Mathf.PI), 0f);
                target[i] = Vector3.Lerp(target[i], shroud, collapse);
            }
        }
        public void ResetChain() { Initialize(); if (bound.Length == 0) return; Capture(); solver.Reset(target); }
        // Keep both Verlet histories coherent when a visual-only shoulder-plane
        // correction translates the entire cloth after projection.
        public void Translate(Vector3 localDelta)
        {
            if (bound.Length == 0) return;
            bound[0].position = ToWorldP(ToLocalP(bound[0].position)+localDelta);
            solver.Translate(localDelta);
        }
        public void Mirror(float worldX) { solver.Mirror(space ? 0f : worldX); }
        /// <summary>以世界点为镜像中心（局部坐标下取它的 x）。</summary>
        public void MirrorAt(Vector3 worldPoint) { solver.Mirror(ToLocalP(worldPoint).x); }
        Vector3 ToLocalP(Vector3 w) => space ? space.InverseTransformPoint(w) : w;
        Vector3 ToLocalV(Vector3 w) => space ? space.InverseTransformVector(w) : w;
        Vector3 ToWorldP(Vector3 l) => space ? space.TransformPoint(l) : l;
        Vector3 Axis => space ? space.forward : Vector3.forward;
        public void Step(float dt)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0f || dt > 86400f) throw new ArgumentOutOfRangeException(nameof(dt));
            manualFrame = Time.frameCount;
            Initialize(); if (bound.Length == 0) return;
            foreach (var b in bound) if (!b) return;
            Capture();
            if ((target[0]-solver.Points[0]).magnitude > teleportDistance) solver.Reset(target);
            solver.TargetAngleLimit = targetAngleLimit;
            solver.Step(target, dt, Mathf.Max(6f, gravity), damping, stiffness, Mathf.Min(60f, maxBend));
            for (int i = 0; i < bound.Length; i++)
            {
                Vector3 from = ToLocalV(bound[i].TransformVector(axes[i]));
                Vector3 to = solver.Points[i+1]-solver.Points[i];
                float angle = Mathf.Atan2(to.y,to.x)-Mathf.Atan2(from.y,from.x);
                bound[i].rotation = Quaternion.AngleAxis(angle*Mathf.Rad2Deg, Axis)*bound[i].rotation;
                bound[i].position = ToWorldP(solver.Points[i]);
            }
        }
        void LateUpdate() { if (automatic && manualFrame != Time.frameCount) Step(Time.deltaTime); }
    }
}
