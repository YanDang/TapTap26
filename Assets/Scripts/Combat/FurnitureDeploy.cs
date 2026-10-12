using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    /// <summary>
    /// 家具部署动画系统（对齐 gamejam-integration）：
    /// 1. 飞行：主角掏出的“家具胶囊”沿抛物线从主角手部飞到落点；
    /// 2. 落地：落地冲击波（VFX_ArrowHit）+ 落地音效（sfx_table_land）+ 微震屏；
    /// 3. 弹起：家具实体弹性拉伸回弹长出（GrowTime 0.45s），地面光环圈从中心逐渐铺开；
    /// 整个过程家具处于 Deploying 状态，不参与碰撞阻挡，怪物不会将其选为目标，部署完毕后正式生效。
    /// </summary>
    public class FurnitureDeploy : MonoBehaviour
    {
        public const float GrowTime = 0.45f;

        private FurnitureObject _furn;
        private List<Collider2D> _colliders = new List<Collider2D>();
        private List<BiomechanicalAuraBase> _auras = new List<BiomechanicalAuraBase>();
        private Transform _visualRoot;
        private Vector3 _originalVisualScale = Vector3.one;

        private Vector3 _fromPos;
        private Vector3 _toPos;
        private float _flightDuration = 0.35f;
        private Sprite _icon;
        private System.Action _onDone;

        public bool Deploying { get; private set; } = true;

        public static FurnitureDeploy Begin(GameObject furnitureRoot, Sprite icon, Vector3 fromPos, System.Action onDone = null)
        {
            var deploy = furnitureRoot.AddComponent<FurnitureDeploy>();
            deploy._icon = icon;
            deploy._fromPos = fromPos;
            deploy._toPos = furnitureRoot.transform.position;
            deploy._onDone = onDone;
            deploy.StartDeploy();
            return deploy;
        }

        private void StartDeploy()
        {
            _furn = GetComponent<FurnitureObject>();
            if (_furn != null)
            {
                _visualRoot = _furn.furnitureRenderer != null ? _furn.furnitureRenderer.transform : transform;
            }
            else
            {
                _visualRoot = transform.Find("Visual") ?? transform;
            }

            if (_visualRoot != null)
            {
                _originalVisualScale = _visualRoot.localScale;
                _visualRoot.localScale = Vector3.zero; // 先隐藏
            }

            foreach (var col in GetComponentsInChildren<Collider2D>())
            {
                if (col.enabled)
                {
                    col.enabled = false;
                    _colliders.Add(col);
                }
            }

            foreach (var aura in GetComponentsInChildren<BiomechanicalAuraBase>())
            {
                if (aura.enabled)
                {
                    aura.enabled = false;
                    _auras.Add(aura);
                }
            }

            StartCoroutine(DeployRoutine());
        }

        private IEnumerator DeployRoutine()
        {
            // 1. 创建飞行胶囊
            GameObject capsule = new GameObject("DeployCapsule");
            capsule.transform.position = _fromPos;
            var sr = capsule.AddComponent<SpriteRenderer>();
            sr.sprite = _icon;
            sr.sortingOrder = 50;

            var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            if (unlitShader != null) sr.material = new Material(unlitShader);

            if (_icon != null)
            {
                float maxDim = Mathf.Max(_icon.bounds.size.x, _icon.bounds.size.y);
                if (maxDim > 0.001f) capsule.transform.localScale = Vector3.one * (0.6f / maxDim);
            }

            float elapsed = 0f;
            float dist = Vector2.Distance(_fromPos, _toPos);
            _flightDuration = Mathf.Clamp(0.25f + dist * 0.035f, 0.25f, 0.55f);

            while (elapsed < _flightDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _flightDuration);

                // 2.5D 抛物线插值：高度沿 y 轴弧线抛起
                Vector3 current = Vector3.Lerp(_fromPos, _toPos, t);
                float height = Mathf.Sin(t * Mathf.PI) * (1.2f + dist * 0.15f);
                current.y += height;

                capsule.transform.position = current;
                capsule.transform.Rotate(0f, 0f, -720f * Time.deltaTime);

                yield return null;
            }

            Destroy(capsule);

            // 2. 落地震波与特效
            Vfx.Spawn("VFX_ArrowHit", _toPos + Vector3.up * 0.3f, 0, 1.2f, false, null, new Color(0.85f, 0.95f, 1f));
            Sfx.Play("sfx_table_land", 0.65f);
            CameraRig.I?.Shake(0.12f, 0.15f);

            // 3. 弹性长出与范围圈铺开
            elapsed = 0f;
            while (elapsed < GrowTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / GrowTime);

                // 弹性缩放曲线：先拔高拉长 -> 压扁回弹 -> 恢复标准
                float scaleY, scaleX;
                if (t < 0.4f)
                {
                    float u = t / 0.4f;
                    scaleY = Mathf.Lerp(0f, 1.35f, u);
                    scaleX = Mathf.Lerp(0.2f, 0.75f, u);
                }
                else if (t < 0.75f)
                {
                    float u = (t - 0.4f) / 0.35f;
                    scaleY = Mathf.Lerp(1.35f, 0.85f, u);
                    scaleX = Mathf.Lerp(0.75f, 1.15f, u);
                }
                else
                {
                    float u = (t - 0.75f) / 0.25f;
                    scaleY = Mathf.Lerp(0.85f, 1.0f, u);
                    scaleX = Mathf.Lerp(1.15f, 1.0f, u);
                }

                if (_visualRoot != null)
                {
                    _visualRoot.localScale = new Vector3(_originalVisualScale.x * scaleX, _originalVisualScale.y * scaleY, _originalVisualScale.z);
                }

                yield return null;
            }

            if (_visualRoot != null)
            {
                _visualRoot.localScale = _originalVisualScale;
            }

            // 4. 恢复所有物理碰撞体与光环组件
            for (int i = 0; i < _colliders.Count; i++)
            {
                if (_colliders[i] != null) _colliders[i].enabled = true;
            }
            for (int i = 0; i < _auras.Count; i++)
            {
                if (_auras[i] != null) _auras[i].enabled = true;
            }

            Deploying = false;
            _onDone?.Invoke();

            Destroy(this);
        }
    }
}
