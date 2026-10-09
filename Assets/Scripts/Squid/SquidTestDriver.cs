using UnityEngine;

namespace GameJam
{
    // 仅用于 SquidTest 场景：自动左右来回爬、隔一会儿随机用一根触手攻击；也可手动：A/D 移动，J 攻击。
    public class SquidTestDriver : MonoBehaviour
    {
        public bool autoDemo = true;
        public float speed = 2.5f;
        public float attackEvery = 1.6f;
        SquidAnimator anim;
        float t, nextAttack = 1.2f;

        void Start()
        {
            anim = GetComponentInChildren<SquidAnimator>();
            if (anim) anim.OnStrike += n => Debug.Log("[Squid] 命中时机：" + n);
        }

        void Update()
        {
            t += Time.deltaTime;
            float x;
            if (autoDemo)
            {
                float c = t % 6f;
                x = c < 1.8f ? 1f : c < 3f ? 0f : c < 4.8f ? -1f : 0f;
                if (t >= nextAttack && anim) { anim.Attack(Random.value < 0.5f ? 1 : -1); nextAttack = t + attackEvery; }
            }
            else
            {
                x = Input.GetAxisRaw("Horizontal");
                if (Input.GetKeyDown(KeyCode.J) && anim) anim.Attack(Random.value < 0.5f ? 1 : -1);
            }
            if (anim && anim.IsAttacking) x = 0f;
            transform.position += new Vector3(x * speed * Time.deltaTime, 0f, 0f);
        }
    }
}
