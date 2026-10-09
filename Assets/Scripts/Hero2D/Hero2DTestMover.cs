using UnityEngine;

namespace GameJam
{
    // 仅用于 Hero2DTest 场景：WASD / 方向键移动，左右移动时翻转朝向；Shift 加速。
    // W/S 模拟“往屏幕里/外走”（2.5D 里的纵深移动），只在画面上轻微上下，腿原地踏步。
    public class Hero2DTestMover : MonoBehaviour
    {
        public float speed = 2.2f, runMultiplier = 1.8f;
        public float depthScreenFactor = 0.35f;
        public bool autoDemo;            // 打开后自动左右来回走、停顿，用于录制 / 截图
        float demoT;

        void Update()
        {
            Vector2 input;
            if (autoDemo)
            {
                demoT += Time.deltaTime;
                float c = demoT % 8f;
                input = c < 2.5f ? Vector2.right : c < 4f ? Vector2.zero : c < 6.5f ? Vector2.left : Vector2.zero;
            }
            else input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            float s = speed * (Input.GetKey(KeyCode.LeftShift) ? runMultiplier : 1f);
            var move = new Vector3(input.x, input.y * depthScreenFactor, 0f);
            if (move.sqrMagnitude > 1f) move.Normalize();
            transform.position += move * s * Time.deltaTime;

            if (Mathf.Abs(input.x) > 0.01f && !GetComponent<HeroFacing>())
            {
                var sc = transform.localScale;
                sc.x = Mathf.Abs(sc.x) * Mathf.Sign(input.x);
                transform.localScale = sc;
            }
        }
    }
}
