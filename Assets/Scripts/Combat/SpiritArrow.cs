using UnityEngine;

namespace GameJam
{
    /// <summary>Homing spirit arrow from the backflip combo; big explosion + heavy shake on hit.</summary>
    public class SpiritArrow : MonoBehaviour
    {
        public float speed = 18f, turnRate = 10f, radius = 2.6f, life = 2.5f;
        public int damage = 3;
        public static int explosions;

        EnemyAI _target;
        Vector3 _vel, _lastAim;
        float _age;
        Billboard _fx;

        public static SpiritArrow Spawn(Vector3 from, EnemyAI target, Vector3 fallbackDir)
        {
            var go = new GameObject("SpiritArrow");
            go.transform.position = from;
            var a = go.AddComponent<SpiritArrow>();
            a._target = target && !target.Dead ? target : null;
            a._lastAim = a._target ? a._target.Pos + Vector3.up * 0.9f : from + fallbackDir.normalized * 8f;
            a._vel = (a._lastAim - from).normalized * a.speed;
            var fx = Vfx.Spawn("VFX_ArrowFly", from, 0, 0.6f, false, go.transform);
            a._fx = fx.GetComponent<Billboard>();
            fx.transform.SetParent(go.transform, true);
            return a;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_target && !_target.Dead) _lastAim = _target.Pos + Vector3.up * 0.9f;
            var desired = (_lastAim - transform.position).normalized * speed;
            _vel = Vector3.RotateTowards(_vel, desired, turnRate * dt, 0);
            transform.position += _vel * dt;
            if (_fx)
            {
                // screen-space heading including the vertical component
                var cam = Camera.main.transform;
                _fx.roll = Mathf.Atan2(Vector3.Dot(_vel, cam.up), Vector3.Dot(_vel, cam.right)) * Mathf.Rad2Deg;
            }
            if ((transform.position - _lastAim).magnitude < 0.6f || _age > life) Explode();
        }

        void Explode()
        {
            explosions++;
            var p = transform.position;
            foreach (var e in EnemyAI.All.ToArray())
                if (!e.Dead && DirUtil.Flat(e.Pos - p).magnitude <= radius)
                    e.TakeDamage(damage, e.Pos - p, 0.7f, true);
            Vfx.Spawn("VFX_ArrowHit", p, 0, 1.7f);
            Vfx.Spawn("VFX_ArrowBurst", p + Vector3.up * 0.3f, 0, 1.3f);
            CameraRig.I?.Shake(0.7f, 0.5f);
            Sfx.Play("sfx_explosion");
            Destroy(gameObject);
        }
    }
}
