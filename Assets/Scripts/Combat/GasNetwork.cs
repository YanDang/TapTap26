using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// 气体网络：管道把气系节点连成若干“网络”（连通块），每个网络共享一个气池。
    /// 拓扑变化（建 / 断管道、节点坏掉）时标记 Dirty，下一帧重算连通块；平时每帧只做流量分配：
    ///   本帧产气 = 网络里所有在工作的喷口产量之和；流量 = min(产气, 所有气泡囊剩余空间)；按剩余空间比例分给气泡囊。
    /// </summary>
    public class GasNetwork : MonoBehaviour
    {
        static bool _dirty = true;
        static readonly List<List<GasNode>> _nets = new List<List<GasNode>>();
        public static int NetworkCount => _nets.Count;
        public static void Dirty() => _dirty = true;

        /// <summary>某个节点所在网络的全部节点（自测 / 调试用）。</summary>
        public static List<GasNode> NetworkOf(GasNode n)
        {
            if (_dirty) Rebuild();
            foreach (var net in _nets) if (net.Contains(n)) return net;
            return null;
        }

        static void Rebuild()
        {
            _dirty = false;
            _nets.Clear();
            var seen = new HashSet<GasNode>();
            foreach (var f in CombatFurniture.All)
            {
                if (!(f is GasNode start) || !start.Alive || seen.Contains(start)) continue;
                var net = new List<GasNode>();
                var queue = new Queue<GasNode>();
                queue.Enqueue(start); seen.Add(start);
                while (queue.Count > 0)
                {
                    var n = queue.Dequeue();
                    net.Add(n);
                    foreach (var p in n.pipes)
                    {
                        if (!p || !p.Alive) continue;
                        var o = p.a == n ? p.b : p.a;
                        if (o && o.Alive && seen.Add(o)) queue.Enqueue(o);
                    }
                }
                _nets.Add(net);
            }
        }

        void Update()
        {
            if (_dirty) Rebuild();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            foreach (var net in _nets)
            {
                float produce = 0f, room = 0f;
                foreach (var n in net)
                {
                    if (!n || !n.Alive) continue;
                    if (n is GasVent v) produce += v.produceRate * dt;
                    else room += Mathf.Max(0f, n.capacity - n.stored);
                }
                float flow = Mathf.Min(produce, room);
                foreach (var n in net)
                {
                    if (!n || !n.Alive) continue;
                    if (n is GasVent v) v.Working = flow > 0f;
                    else if (room > 0f) n.stored = Mathf.Min(n.capacity, n.stored + flow * Mathf.Max(0f, n.capacity - n.stored) / room);
                    foreach (var p in n.pipes) if (p) p.flow = flow / dt;
                }
            }
        }
    }
}
