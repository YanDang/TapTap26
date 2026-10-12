using System.Collections.Generic;
using UnityEngine;

namespace GameJam
{
    // 表情叠加贴图：按 PSB 文档像素坐标（左上原点）放到骨骼角色身上。
    // 布局由 E:\GameJamArt\hero2d\scripts\make_face_sprites.py 生成（Resources/Hero2D/Face/face_layout.json）。
    // 手调：菜单 GameJam/表情调整（FaceTweakWindow）把每个部件相对原位的偏移 / 旋转 / 缩放存进 face_tweaks.json，运行时叠加。
    public static class FaceOverlay
    {
        [System.Serializable] public class Entry { public string name; public float x, y, k; }
        [System.Serializable] public class Layout { public Entry[] hero; public Entry[] squid; }

        [System.Serializable]
        public class Tweak
        {
            public string key;                 // "hero/eye_angry_L"、"squid/sq_lid" …
            public Vector3 offset;             // 相对原位的偏移（父骨本地坐标）
            public float rotation;             // 度
            public float scale = 1f;
        }

        [System.Serializable]
        public class TweakFile
        {
            public List<Tweak> items = new List<Tweak>();
            public float angrySquint = 0f;        // 愤怒时眼睛再额外闭多少（0 睁开 – 1 闭上）；眼皮已经画在贴图里，默认不眯
        }

        public const string TweakResource = "Hero2D/Face/face_tweaks";
        public const string TweakAssetPath = "Assets/Resources/Hero2D/Face/face_tweaks.json";

        static Layout _layout;
        static TweakFile _tweaks;

        // 编辑器关掉“进 Play 重载域”时静态变量会留到下一次运行：每次进 Play 重新读文件
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _layout = null; _tweaks = null; FaceItem.Editing = false; }

        public static Layout Get()
        {
            if (_layout == null)
            {
                var ta = Resources.Load<TextAsset>("Hero2D/Face/face_layout");
                _layout = ta ? JsonUtility.FromJson<Layout>(ta.text) : new Layout { hero = new Entry[0], squid = new Entry[0] };
            }
            return _layout;
        }

        public static TweakFile Tweaks
        {
            get
            {
                if (_tweaks == null)
                {
                    var ta = Resources.Load<TextAsset>(TweakResource);
                    _tweaks = ta ? JsonUtility.FromJson<TweakFile>(ta.text) : new TweakFile();
                }
                return _tweaks;
            }
        }

        public static Tweak GetTweak(string key)
        {
            foreach (var t in Tweaks.items) if (t.key == key) return t;
            var n = new Tweak { key = key };
            Tweaks.items.Add(n);
            return n;
        }

        /// <summary>
        /// refLayer：一个图层（绑定姿势下），docMin = 该图层在 PSB 文档里的左上角像素，docWidth = 它在文档里的宽度
        /// （导入时图层会被裁边、可能被缩小，用 sprite 宽 / docWidth 换算）。用它把文档坐标换成世界坐标，
        /// 然后挂到 parent（通常是骨骼）下，让贴图跟着骨骼动。k = 贴图 1 像素等于多少文档像素。group = "hero" / "squid"。
        /// </summary>
        public static SpriteRenderer Create(string group, Entry e, string resDir, SpriteRenderer refLayer, Vector2 docMin, float docWidth, Transform parent, int order)
        {
            var spr = Resources.Load<Sprite>(resDir + "/" + e.name);
            if (!spr || !refLayer || !refLayer.sprite) { Debug.LogWarning("[Face] 缺贴图 " + resDir + "/" + e.name); return null; }
            var s = refLayer.sprite;
            float ppu = s.pixelsPerUnit;
            float sc = s.rect.width / docWidth;                        // 贴图像素 / 文档像素
            var px = new Vector2((e.x - docMin.x) * sc, s.rect.height - (e.y - docMin.y) * sc);
            var local = (Vector3)((px - s.pivot) / ppu);

            var go = new GameObject("表情_" + e.name);
            go.transform.SetPositionAndRotation(refLayer.transform.TransformPoint(local), refLayer.transform.rotation);
            go.transform.SetParent(parent, true);
            var want = refLayer.transform.lossyScale * (e.k * sc * spr.pixelsPerUnit / ppu);
            var ps = parent.lossyScale;
            go.transform.localScale = new Vector3(want.x / ps.x, want.y / ps.y, 1f);

            var item = go.AddComponent<FaceItem>();
            item.key = group + "/" + e.name;
            item.basePos = go.transform.localPosition;
            item.baseRot = go.transform.localRotation;
            item.baseScale = go.transform.localScale;
            // 文档里 1 像素往右 / 往下，在父骨本地坐标里是多少（窗口里按像素微调用）
            float docWorld = s.rect.width / docWidth / ppu;
            item.pxRight = parent.InverseTransformVector(refLayer.transform.TransformVector(Vector3.right * docWorld));
            item.pxDown = parent.InverseTransformVector(refLayer.transform.TransformVector(Vector3.down * docWorld));
            item.tweak = GetTweak(item.key);
            item.ApplyRest();

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spr;
            sr.sortingLayerID = refLayer.sortingLayerID;
            sr.sortingOrder = order;
            go.SetActive(false);
            return sr;
        }
    }
}
