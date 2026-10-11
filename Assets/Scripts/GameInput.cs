using UnityEngine;

namespace GameJam
{
    /// <summary>Mouse -> player commands. (Mobile: tap = left, swipe = right; to be mapped by the programmers.)</summary>
    public class GameInput : MonoBehaviour
    {
        public static bool Disabled;

        void Update()
        {
            if (Disabled) return;
            var p = GameManager.I ? GameManager.I.player : null;
            var cam = Camera.main;
            if (!p || !cam) return;
            // 物品栏优先（点格子、数字键），其次是摆放模式；都不是才是主角的操作
            if (InventoryBar.I && InventoryBar.I.HandleInput()) return;
            if (BuildController.I && BuildController.I.Active) { BuildController.I.HandleInput(); return; }
            if (Input.GetMouseButtonDown(1))
            {
                var ray = cam.ScreenPointToRay(Input.mousePosition);
                var e = PickEnemy(ray);
                if (e) p.RightClickEnemy(e);
                else if (new Plane(Vector3.up, 0).Raycast(ray, out float d)) p.RightClickGround(ray.GetPoint(d));
            }
            if (Input.GetMouseButtonDown(0)) p.LeftClick();
            if (Input.GetMouseButtonUp(0)) p.LeftRelease();
        }

        static EnemyAI PickEnemy(Ray ray)
        {
            EnemyAI best = null; float bd = float.MaxValue;
            foreach (var h in Physics.SphereCastAll(ray, 0.45f, 300f, 1 << 10))
            {
                var e = h.collider.GetComponent<EnemyAI>();
                if (e && !e.Dead && h.distance < bd) { bd = h.distance; best = e; }
            }
            return best;
        }
    }
}
