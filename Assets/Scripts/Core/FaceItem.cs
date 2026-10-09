using UnityEngine;

namespace GameJam
{
    /// <summary>一个表情部件：记住生成时的原位（未调整）和手调量，RestPos 等 = 原位 + 手调。</summary>
    public class FaceItem : MonoBehaviour
    {
        /// <summary>表情调整窗口打开编辑时为 true：各部件停在“原位 + 手调”，不播小动画，方便拖。</summary>
        public static bool Editing;

        public string key;
        public Vector3 basePos, baseScale;
        public Quaternion baseRot;
        public Vector3 pxRight, pxDown;
        public FaceOverlay.Tweak tweak;

        public Vector3 RestPos => basePos + tweak.offset;
        public Quaternion RestRot => baseRot * Quaternion.Euler(0, 0, tweak.rotation);
        public Vector3 RestScale => baseScale * tweak.scale;

        public void ApplyRest()
        {
            transform.localPosition = RestPos;
            transform.localRotation = RestRot;
            transform.localScale = RestScale;
        }

        /// <summary>把当前摆放（手动拖过的）记成手调量。</summary>
        public void Capture()
        {
            tweak.offset = transform.localPosition - basePos;
            var d = Quaternion.Inverse(baseRot) * transform.localRotation;
            tweak.rotation = Mathf.DeltaAngle(0f, d.eulerAngles.z);
            tweak.scale = Mathf.Abs(baseScale.x) > 1e-6f ? transform.localScale.x / baseScale.x : 1f;
        }

        public void ResetTweak()
        {
            tweak.offset = Vector3.zero; tweak.rotation = 0f; tweak.scale = 1f;
            ApplyRest();
        }
    }
}
