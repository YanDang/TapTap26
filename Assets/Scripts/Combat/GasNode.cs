using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// 气系生产线的节点（喷口 GasVent、气泡囊 GasSac），用管道 GasPipe 连起来，连在一起的节点共享气体（GasNetwork）。
    /// 气系家具（用户 2026-10-09 设计）：本身没有伤害；被玩家或怪物打破时“气爆”，把周围的玩家和怪物都弹开。
    /// 玩家摆放的，坏了就没了（不重生）。
    /// </summary>
    public abstract class GasNode : CombatFurniture
    {
        public float capacity;                  // 能存多少气（喷口为 0）
        public float stored;
        public readonly List<GasPipe> pipes = new List<GasPipe>();

        public float Fill => capacity > 0f ? Mathf.Clamp01(stored / capacity) : 0f;
        public override bool PlayerCanHit => Alive;

        protected virtual float BurstRadius => 1.6f;
        protected virtual float BurstDistance => 2f;

        protected override void Awake() { base.Awake(); respawnSeconds = 0f; }

        protected override void Break()
        {
            base.Break();
            GasBurst.Do(Pos, BurstRadius, BurstDistance);
            stored = 0f;
            foreach (var p in pipes.ToArray()) if (p) p.Detach();    // 连在它身上的管道一起断
            GasNetwork.Dirty();
            Destroy(gameObject, 0.6f);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            GasNetwork.Dirty();
        }

        public bool ConnectedTo(GasNode other)
        {
            foreach (var p in pipes) if (p && p.Alive && (p.a == other || p.b == other)) return true;
            return false;
        }
    }

    /// <summary>气爆：radius 内的怪物和玩家被往外弹开 distance 米，不掉血。</summary>
    public static class GasBurst
    {
        public static int count;

        public static void Do(Vector3 center, float radius, float distance)
        {
            count++;
            var c = DirUtil.Flat(center);
            foreach (var e in EnemyAI.All.ToArray())
            {
                if (!e || e.Dead) continue;
                var v = DirUtil.Flat(e.Pos - c);
                if (v.magnitude <= radius) e.Knockback(v.sqrMagnitude > 1e-4f ? v : Vector3.right, distance);
            }
            var p = GameManager.I ? GameManager.I.player : null;
            if (p && !p.Dead)
            {
                var v = DirUtil.Flat(p.Pos - c);
                if (v.magnitude <= radius) p.Knockback(v.sqrMagnitude > 1e-4f ? v : Vector3.left, distance);
            }
            var tint = new Color(0.85f, 0.75f, 1f);
            Vfx.Spawn("VFX_ArrowBurst", c + Vector3.up * 0.5f, 0, radius * 0.55f, false, null, tint);
            CameraRig.I?.Shake(0.25f, 0.25f);
            Sfx.Play("sfx_dodge", 1f);
        }
    }
}
