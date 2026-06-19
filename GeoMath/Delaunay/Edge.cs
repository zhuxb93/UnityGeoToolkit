using System.Collections.Generic;
using UnityEngine;

namespace GeoToolkit
{
    [System.Serializable]
    public class Edge
    {
        private Point VertexA;
        private Point VertexB;
        private Vector2 MidPoint;
        private float Gradient;

        public Point vertexA { get { return VertexA; } }
        public Point vertexB { get { return VertexB; } }
        public Vector2 midPoint { get { return MidPoint; } }
        public float gradient { get { return Gradient; } }

        public Edge(Point vertexA, Point vertexB)
        {
            VertexA = vertexA;
            VertexB = vertexB;
            MidPoint = ComputeMidPoint();
            Gradient = ComputeGradient();
        }

        public override int GetHashCode()
        {
            return vertexA.pos.GetHashCode() ^ vertexB.pos.GetHashCode();
        }

        public Vector2 ComputeMidPoint()
        {
            return MathHelper.MidPointOfLine(VertexA, VertexB);
        }

        public float ComputeGradient()
        {
            return MathHelper.GradientOfLine(VertexA, VertexB);
        }

        public float ComputeLength()
        {
            return Vector2.Distance(vertexA.pos, vertexB.pos);
        }

        public List<Point> Split(float dis)
        {
            float len = Vector2.Distance(vertexA.pos, vertexB.pos);
            int times = (int)(len / dis);
            // 创建一个列表来存储分割点
            List<Point> splitPoints = new List<Point>();
            if (times <= 0) return null;
            // 计算每个分割点的位置
            for (int i = 1; i <= times; i++)
            {
                // 计算插值因子 t，范围在 0 到 1 之间
                float t = (float)i / (times + 1);
                // 计算分割点的位置
                Vector2 splitPos = Vector2.Lerp(vertexA.pos, vertexB.pos, t);
                // 创建新的 Point 对象并添加到列表中
                splitPoints.Add(new Point(splitPos.x, splitPos.y));
            }

            return splitPoints;
        }

        public bool EqualsEdge(Edge edge)
        {
            return VertexA.pos == edge.VertexA.pos && VertexB.pos == edge.VertexB.pos || VertexA.pos == edge.VertexB.pos && VertexB.pos == edge.VertexA.pos;
        }
        public bool Equals(Edge other)
        {
            return (vertexA.Equals(other.vertexA) && vertexB.Equals(other.vertexB)) ||
                   (vertexA.Equals(other.vertexB) && vertexB.Equals(other.vertexA));
        }
    }

}