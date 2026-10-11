using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BiomechanicalCrafting
{
    /// <summary>
    /// Canvas 内置轻量矢量连线渲染器 (UI Line Renderer)
    /// 用于绘制 8 向一笔画回路中的发光生体神经管线与脉冲能量流
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class UILineRenderer : MaskableGraphic
    {
        [Header("Line Settings")]
        public float thickness = 10f;
        public Color lineColor = new Color(0.98f, 0.82f, 0.18f, 0.95f); // 默认金色/黄色脉冲弧光
        public Color glowColor = new Color(0.98f, 0.82f, 0.18f, 0.35f);

        [Header("Path Points (Canvas Local Space)")]
        [SerializeField]
        private List<Vector2> points = new List<Vector2>();

        public void SetPoints(List<Vector2> newPoints)
        {
            points = new List<Vector2>(newPoints);
            SetVerticesDirty();
        }

        public void Clear()
        {
            points.Clear();
            SetVerticesDirty();
        }

        public void SetColor(Color c)
        {
            lineColor = c;
            glowColor = new Color(c.r, c.g, c.b, 0.4f);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (points == null || points.Count < 2)
            {
                return;
            }

            // 1. 绘制外层发光晕影 (Glow pass)
            float glowThickness = thickness * 2.2f;
            DrawPathSegments(vh, glowThickness, glowColor);

            // 2. 绘制内层高亮脉冲管核心 (Core pass)
            DrawPathSegments(vh, thickness, lineColor);

            // 3. 在每个转折节点绘制圆形关节垫 (Joint caps)
            for (int i = 0; i < points.Count; i++)
            {
                DrawCircle(vh, points[i], thickness * 0.9f, lineColor);
                DrawCircle(vh, points[i], glowThickness * 0.7f, glowColor);
            }
        }

        private void DrawPathSegments(VertexHelper vh, float width, Color color)
        {
            float halfWidth = width * 0.5f;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 p0 = points[i];
                Vector2 p1 = points[i + 1];

                Vector2 dir = (p1 - p0).normalized;
                Vector2 normal = new Vector2(-dir.y, dir.x) * halfWidth;

                Vector2 v0 = p0 - normal;
                Vector2 v1 = p0 + normal;
                Vector2 v2 = p1 + normal;
                Vector2 v3 = p1 - normal;

                int baseIndex = vh.currentVertCount;

                UIVertex vert = UIVertex.simpleVert;
                vert.color = color;

                vert.position = v0; vh.AddVert(vert);
                vert.position = v1; vh.AddVert(vert);
                vert.position = v2; vh.AddVert(vert);
                vert.position = v3; vh.AddVert(vert);

                vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
                vh.AddTriangle(baseIndex, baseIndex + 2, baseIndex + 3);
            }
        }

        private void DrawCircle(VertexHelper vh, Vector2 center, float radius, Color color)
        {
            const int segments = 12;
            int centerIndex = vh.currentVertCount;

            UIVertex vert = UIVertex.simpleVert;
            vert.color = color;
            vert.position = center;
            vh.AddVert(vert);

            for (int i = 0; i <= segments; i++)
            {
                float rad = (i / (float)segments) * Mathf.PI * 2f;
                Vector2 pos = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
                vert.position = pos;
                vh.AddVert(vert);
            }

            for (int i = 0; i < segments; i++)
            {
                vh.AddTriangle(centerIndex, centerIndex + 1 + i, centerIndex + 2 + i);
            }
        }
    }
}
