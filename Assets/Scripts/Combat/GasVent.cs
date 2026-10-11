using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// 气旋喷口（原料「气旋喷口软骨」）：气系生产线的气源，每秒产 produceRate 份气，通过管道送到同一网络的气泡囊。
    /// 自己不存气；网络里没有地方放气时停工。正在送气时会一阵阵喷白汽。
    /// </summary>
    public class GasVent : GasNode
    {
        public float produceRate = 1.5f;
        public bool Working { get; set; }       // GasNetwork 每帧写：这一帧有没有往外送气
        float _puff, _seed;

        public static GasVent Build(Vector3 pos, Material mat)
        {
            var root = new GameObject("GasVent");
            root.transform.position = DirUtil.Flat(pos);
            var v = root.AddComponent<GasVent>();
            v.maxHp = v.hp = 8;
            v.hitRadius = 0.5f;
            v.BuildVisual("gas_vent", 0.8f, mat, 0f, Color.clear);
            GasNetwork.Dirty();
            return v;
        }

        protected override void Awake() { base.Awake(); _seed = Random.value * 10f; }

        protected override void Update()
        {
            base.Update();
            if (!Alive) return;
            float t = Time.time + _seed;
            // 工作时一鼓一鼓地往外喷气
            float pump = Working ? Mathf.Max(0f, Mathf.Sin(t * 7f)) * 0.12f : 0.02f * Mathf.Sin(t * 2f);
            SetScale(1f + pump, 1f - pump * 0.5f);
            if (Working)
            {
                _puff += Time.deltaTime;
                if (_puff > 0.8f)
                {
                    _puff = 0f;
                    Vfx.Spawn("VFX_ParrySpark", Pos + Vector3.up * 0.85f, Random.Range(0f, 360f), 0.6f, false, null, new Color(0.95f, 0.95f, 1f, 0.8f));
                }
            }
        }
    }
}
