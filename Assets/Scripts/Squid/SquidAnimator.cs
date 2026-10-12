using UnityEngine;

namespace GameJam
{
    // 鱿鱼怪的程序化动画（2D 骨骼）。
    // 浮空：身体缓慢上下浮动 + 轻微倾斜漂移；移动时朝前进方向倾，触须被拖在身后。
    // 触须：蛇舞 —— 每根触须整根做顺滑的 S 形行波（从根传到梢），根部再叠一层缓慢的大幅左右摇摆；
    //       5 根各自的快慢、相位、幅度起伏都不同，像各自被笛声牵着跳。
    // 攻击：Attack() 随机挑一根触手：往后蜷起蓄力（可指定命中时刻，蓄力久了会发抖）→ 从根到梢依次甩出（鞭子）→ 收回。
    //       甩到最远时触发 OnStrike(触手名)。
    // 状态：energy < 1 时触须下垂变慢（眩晕 / 待处决）；Die() 下沉并淡出。
    [DefaultExecutionOrder(-8)]
    public class SquidAnimator : MonoBehaviour
    {
        [System.Serializable]
        public class Tentacle
        {
            public string name;
            public Transform[] bones;          // 根 → 梢
            public Transform[] claw;           // 末端爪指（可空），攻击时张开
            [Range(0, 1)] public float phase;  // 蛇舞相位
            [HideInInspector] public Quaternion[] rest, clawRest;
            [HideInInspector] public float rate, swayRate, seed, ph;   // ph = 蛇舞相位累加（狂舞时走得更快）
        }

        [Header("浮空")]
        public float hoverAmount = 1.6f;       // 上下浮动幅度（本地单位；鱿鱼原图约 57 单位高）
        public float hoverRate = 0.45f;        // 浮动快慢（次/秒）
        public float driftTilt = 4f;           // 身体缓慢倾斜（度）
        public float moveTilt = 10f;           // 移动时朝前进方向倾（度）
        public float fullSpeed = 3f;

        [Header("蛇舞")]
        public float danceAmplitude = 6.5f;    // 每节摆角（度）：S 形的弯曲程度
        public float danceWaves = 1.3f;        // 一根触须上有几个波（>1 才是 S 形）
        public float danceRate = 0.5f;         // 波沿触须传递的快慢（次/秒）
        [Range(0, 0.5f)] public float rateVariance = 0.25f;   // 每根触须快慢差异
        public float rootSway = 9f;            // 根部大幅摇摆（度，分摊到前 3 节）
        public float rootSwayRate = 0.22f;
        public float trail = 2.5f;             // 移动时触须被拖在后面（每节度数，按速度）
        public float mantleSway = 3f;          // 外套膜摇摆（度）
        public float finFlap = 10f;            // 鳍扇动（度）

        [Header("攻击")]
        public float windupTime = 0.35f, strikeTime = 0.16f, holdTime = 0.08f, recoverTime = 0.45f;
        public float windupCurl = 7f;          // 蓄力时每节往后蜷（度）
        public float strikeBend = 9f;          // 甩出时每节往前（度），14 节 ≈ 126°
        public float whipDelay = 0.012f;       // 每节比上一节晚多少秒（鞭子感）
        public float windupQuiver = 1.5f;      // 蓄力时间长时的发抖（度）
        public float clawOpen = 25f;
        public float lunge = 1.5f;             // 攻击时身体往前探（本地单位）

        [Header("抓取")]
        public float frenzyAmp = 2.6f;         // 狂舞（抓取前摇）时蛇舞幅度倍增
        public float frenzyRate = 2.2f;        // 狂舞时波动快慢倍增
        public float frenzyJitter = 9f;        // 狂舞时额外的乱扭（度）
        public float grabReach = 7f;           // 缠住时触手每节往前（度）
        public float grabCoil = 26f;           // 梢部卷起缠绕（每节度数）

        public Transform body, mantle1, mantle2, finL, finR;
        public Tentacle[] tentacles;
        public Transform motion;
        public bool useUnscaledTime;
        [Range(0, 1)] public float energy = 1f;   // 1 正常；低于 1 触须下垂、动作变慢

        /// <summary>甩到最远（命中时机）时触发，参数 = 触手名。</summary>
        public event System.Action<string> OnStrike;
        public bool IsAttacking => atkIndex >= 0;
        public bool IsDead { get; private set; }

        float t, weight, velX, seed, energyNow = 1f, dieT;
        Vector3 lastPos, bodyRest;
        Quaternion bodyRestRot, mantle1Rest, mantle2Rest, finLRest, finRRest;
        int atkIndex = -1; float atkT, windup; int atkDir = 1; bool struck;
        SpriteRenderer[] renderers; Color[] baseColors;

        float frenzy, frenzyTarget;
        int grabIndex = -1, grabDir = 1; float grabW, grabT, releaseT = -1f;
        public bool IsGrabbing => grabIndex >= 0;

        /// <summary>抓取前摇：所有触手像蛇一样大幅度 S 形乱飘。</summary>
        public void BeginFrenzy() => frenzyTarget = 1f;
        public void EndFrenzy() => frenzyTarget = 0f;

        /// <summary>缠住：挑一根最长的触手往 dir 方向伸出，梢部卷起来，持续挤压，直到 GrabRelease。</summary>
        public void GrabHold(int dir)
        {
            if (IsDead || tentacles == null || tentacles.Length == 0) return;
            int best = 0;
            for (int i = 1; i < tentacles.Length; i++) if (tentacles[i].bones.Length > tentacles[best].bones.Length) best = i;
            grabIndex = best; grabDir = dir < 0 ? -1 : 1; grabT = 0f; releaseT = -1f;
            atkIndex = -1;
            frenzyTarget = 0.45f;              // 其余触手还在扭，但没那么疯
        }

        /// <summary>甩出去：缠绕的触手猛地往前一甩再收回。</summary>
        public void GrabRelease() { if (grabIndex >= 0) releaseT = 0f; frenzyTarget = 0f; }

        void Start()
        {
            if (!motion) motion = transform;
            lastPos = motion.position;
            seed = Random.value * 100f;
            for (int k = 0; k < tentacles.Length; k++)
            {
                var tc = tentacles[k];
                tc.rest = System.Array.ConvertAll(tc.bones, b => b.localRotation);
                tc.clawRest = tc.claw == null ? new Quaternion[0] : System.Array.ConvertAll(tc.claw, b => b.localRotation);
                tc.seed = seed + k * 13.7f;
                tc.rate = danceRate * Random.Range(1f - rateVariance, 1f + rateVariance);
                tc.swayRate = rootSwayRate * Random.Range(0.8f, 1.25f);
                if (tc.phase == 0f) tc.phase = Random.value;
            }
            if (body) { bodyRest = body.localPosition; bodyRestRot = body.localRotation; }
            if (mantle1) mantle1Rest = mantle1.localRotation;
            if (mantle2) mantle2Rest = mantle2.localRotation;
            if (finL) finLRest = finL.localRotation;
            if (finR) finRRest = finR.localRotation;
        }

        /// <summary>
        /// 随机用一根触手攻击。dir = +1 朝角色本地 +X 打，-1 朝 -X。
        /// impactAt &gt; 0 时按“从现在起多少秒命中”拉伸蓄力，保证甩到最远的瞬间正好是命中时刻。正在攻击时忽略。
        /// </summary>
        public bool Attack(int dir = 1, int tentacle = -1, float impactAt = -1f)
        {
            if (IsAttacking || IsDead || tentacles == null || tentacles.Length == 0) return false;
            atkIndex = tentacle >= 0 ? tentacle % tentacles.Length : Random.Range(0, tentacles.Length);
            int n = tentacles[atkIndex].bones.Length;
            windup = impactAt > 0f ? Mathf.Max(0.12f, impactAt - strikeTime - whipDelay * n) : windupTime;
            atkT = 0f; atkDir = dir < 0 ? -1 : 1; struck = false;
            return true;
        }

        /// <summary>攻击被打断（受击硬直）：直接进入收回阶段。</summary>
        public void Interrupt()
        {
            if (!IsAttacking) return;
            struck = true;
            atkT = Mathf.Max(atkT, windup + strikeTime + holdTime + whipDelay * tentacles[atkIndex].bones.Length);
        }

        /// <summary>死亡：触须下垂、身体下沉、整体淡出（约 duration 秒）。</summary>
        public void Die(float duration = 2f)
        {
            if (IsDead) return;
            IsDead = true; dieT = 0f; atkIndex = -1; grabIndex = -1; grabW = 0f;
            dieDuration = duration;
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            baseColors = System.Array.ConvertAll(renderers, r => r.color);
        }

        /// <summary>复活刷新：重置死亡状态、恢复姿态与完整不透明度</summary>
        public void Revive()
        {
            IsDead = false;
            dieT = 0f;
            energyNow = 1f;
            atkIndex = -1;
            grabIndex = -1;
            grabW = 0f;
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i])
                    {
                        Color c = (baseColors != null && i < baseColors.Length) ? baseColors[i] : Color.white;
                        c.a = 1f;
                        renderers[i].color = c;
                    }
                }
            }
        }
        float dieDuration = 2f;

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        static float Noise(float x, float y) => Mathf.PerlinNoise(x, y) * 2f - 1f;   // -1..1

        void LateUpdate()
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (dt <= 0f || tentacles == null) return;
            if (!motion) motion = transform;

            energyNow = Mathf.MoveTowards(energyNow, IsDead ? 0f : energy, 2f * dt);
            float tempo = Mathf.Lerp(0.35f, 1f, energyNow);          // 没力气时动作变慢
            t += dt * tempo;
            frenzy = Mathf.MoveTowards(frenzy, IsDead ? 0f : frenzyTarget, dt * 2.5f);
            if (grabIndex >= 0)
            {
                grabT += dt;
                if (releaseT >= 0f) { releaseT += dt; grabW = Mathf.MoveTowards(grabW, 0f, dt / 0.35f); if (releaseT > 0.45f) { grabIndex = -1; releaseT = -1f; } }
                else grabW = Mathf.MoveTowards(grabW, 1f, dt / 0.15f);
            }

            // 速度换到角色自身单位（除以缩放），取横向分量用于前倾 / 拖拽
            var delta = motion.position - lastPos;
            lastPos = motion.position;
            var local = Quaternion.Inverse(transform.rotation) * delta / Mathf.Max(Mathf.Abs(transform.lossyScale.y), 1e-4f);
            float speed = local.magnitude / dt;
            weight = Mathf.MoveTowards(weight, Mathf.Clamp01(speed / fullSpeed), 3f * dt);
            velX = Mathf.Lerp(velX, Mathf.Clamp(local.x / dt / fullSpeed, -1f, 1f), 1f - Mathf.Exp(-4f * dt));

            // ---- 触须
            for (int ti = 0; ti < tentacles.Length; ti++)
            {
                var tc = tentacles[ti];
                int n = tc.bones.Length;
                bool attacking = ti == atkIndex;
                float atkWeight = attacking ? AttackBlend() : 0f;
                float ampMod = (0.75f + 0.25f * Mathf.Sin(t * 0.3f + tc.seed)) * Mathf.Lerp(0.35f, 1f, energyNow) * (1f + frenzy * frenzyAmp);  // 幅度缓慢起伏
                float sway = Mathf.Sin((t * tc.swayRate + tc.phase * 0.7f) * Mathf.PI * 2f) * rootSway * Mathf.Lerp(0.3f, 1f, energyNow) * (1f + frenzy);
                tc.ph += dt * tempo * tc.rate * (1f + frenzy * frenzyRate);
                bool grabbing = ti == grabIndex;

                for (int i = 0; i < n; i++)
                {
                    float u = (i + 1f) / n;                                        // 0 根 → 1 梢
                    // S 形行波：相位沿触须递减 → 波从根传到梢
                    float wave = Mathf.Sin((tc.ph + tc.phase - u * danceWaves) * Mathf.PI * 2f) * danceAmplitude * (0.6f + 0.4f * u) * ampMod
                               + Noise(t * 3.1f + tc.seed, u * 4f) * frenzyJitter * frenzy;
                    float root = i < 3 ? sway / 3f : 0f;                            // 根部摇摆分摊到前 3 节
                    float drag = -velX * trail * u * weight;                        // 被拖在身后
                    float dance = wave + root + drag;                               // 没力气时的“下垂”靠 ampMod 衰减体现
                    float atk = attacking ? AttackAngle(i) * atkDir : 0f;
                    float ang = Mathf.Lerp(dance, 0f, atkWeight) + atk;
                    if (grabbing)
                    {   // 前段伸向主角，梢部卷起缠住，带挤压脉动；甩出时整根猛往前一甩
                        float g = u < 0.6f ? grabReach : grabReach * 0.4f + grabCoil * (u - 0.6f) / 0.4f;
                        g += Mathf.Sin(grabT * 14f + u * 3f) * 2.5f;
                        if (releaseT >= 0f) g += strikeBend * 0.8f * Mathf.Sin(Mathf.Clamp01(releaseT / 0.3f) * Mathf.PI);
                        ang = Mathf.Lerp(dance, g * grabDir, grabW);
                    }
                    tc.bones[i].localRotation = tc.rest[i] * Quaternion.Euler(0, 0, ang);
                }
                for (int i = 0; i < tc.claw.Length; i++)
                {
                    int finger = i / 3;                                            // 爪指按“指1 的 3 节、指2 的 3 节…”排序
                    float sign = finger == 0 ? -1f : finger == 2 ? 1f : 0.3f;     // 3 根爪指：两边往外张，中间少动
                    float open = attacking ? ClawOpen() : Mathf.Max(0f, Mathf.Sin((tc.ph * 2f + tc.phase) * Mathf.PI * 2f)) * 8f * energyNow * (1f + frenzy);
                    tc.claw[i].localRotation = tc.clawRest[i] * Quaternion.Euler(0, 0, sign * open);
                }
            }

            // ---- 身体：浮动 + 漂移倾斜 + 移动前倾 + 攻击前探 + 死亡下沉
            float hover = Mathf.Sin(t * hoverRate * Mathf.PI * 2f) * hoverAmount * Mathf.Lerp(0.4f, 1f, energyNow);
            float sink = 0f;
            if (IsDead)
            {
                dieT += dt;
                float k = Smooth(dieT / dieDuration);
                sink = -hoverAmount * 4f * k;
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i]) { var c = baseColors[i]; c.a *= 1f - k; renderers[i].color = c; }
            }
            float tilt = Noise(t * 0.25f + seed, 11f) * driftTilt - velX * moveTilt;
            float lungeX = atkIndex >= 0 ? LungeCurve() * lunge * atkDir : 0f;
            lungeX += grabW * lunge * 0.6f * grabDir;
            if (body)
            {
                body.localPosition = bodyRest + new Vector3(lungeX, hover + sink, 0f);
                body.localRotation = bodyRestRot * Quaternion.Euler(0, 0, tilt);
            }
            float msway = Mathf.Sin(t * 0.4f * Mathf.PI * 2f + seed) * mantleSway + velX * mantleSway;   // 外套膜也被拖着
            if (mantle1) mantle1.localRotation = mantle1Rest * Quaternion.Euler(0, 0, msway * 0.6f);
            if (mantle2) mantle2.localRotation = mantle2Rest * Quaternion.Euler(0, 0, msway * 0.4f);
            float flap = Mathf.Sin(t * Mathf.PI * 2f * Mathf.Lerp(0.8f, 2.2f, weight)) * finFlap * energyNow;
            if (finL) finL.localRotation = finLRest * Quaternion.Euler(0, 0, flap);
            if (finR) finR.localRotation = finRRest * Quaternion.Euler(0, 0, -flap);

            // ---- 攻击计时（用真实 dt，命中时刻要和 AI 对齐，不受 energy 影响）
            if (atkIndex >= 0)
            {
                atkT += dt;
                float strikeEnd = windup + strikeTime + whipDelay * tentacles[atkIndex].bones.Length;
                if (!struck && atkT >= strikeEnd) { struck = true; OnStrike?.Invoke(tentacles[atkIndex].name); }
                if (atkT >= strikeEnd + holdTime + recoverTime) atkIndex = -1;
            }
        }

        // 第 i 节的攻击角度：蓄力往后蜷（久了会发抖）→ 带延迟甩向前 → 保持 → 收回
        float AttackAngle(int i)
        {
            float tt = atkT - i * whipDelay;
            if (tt < windup)
            {
                float k = Smooth(tt / Mathf.Min(windup, 0.35f));
                float quiver = windup > 0.6f ? Mathf.Sin(atkT * 40f + i) * windupQuiver * Smooth((tt - 0.3f) / 0.3f) : 0f;
                return -windupCurl * k + quiver;
            }
            float s = Smooth((tt - windup) / strikeTime);
            float fwd = Mathf.Lerp(-windupCurl, strikeBend, s);
            float rest = tt - windup - strikeTime - holdTime;
            if (rest <= 0f) return fwd;
            return Mathf.Lerp(strikeBend, 0f, Smooth(rest / recoverTime));
        }

        float AttackBlend()   // 攻击期间压掉蛇舞
        {
            float total = windup + strikeTime + holdTime + recoverTime + whipDelay * 16;
            return Smooth(atkT / 0.1f) * (1f - Smooth((atkT - (total - recoverTime * 0.5f)) / (recoverTime * 0.5f)));
        }

        float ClawOpen()
        {
            float s = windup + strikeTime * 0.5f;
            if (atkT < s) return clawOpen * Smooth(atkT / s);
            return clawOpen * (1f - Smooth((atkT - s - holdTime) / 0.15f));
        }

        float LungeCurve()
        {
            float a = windup, b = a + strikeTime, c = b + holdTime;
            if (atkT < a) return -0.3f * Smooth(atkT / Mathf.Min(a, 0.35f));
            if (atkT < b) return Mathf.Lerp(-0.3f, 1f, Smooth((atkT - a) / strikeTime));
            if (atkT < c) return 1f;
            return 1f - Smooth((atkT - c) / recoverTime);
        }
    }
}
