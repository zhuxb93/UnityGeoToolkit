using System;
using UnityEngine;

namespace GeoToolkit
{

    [System.Serializable]
    public class Triangle
    {
        private Vector2 CircumCentre;
        private float CircumRadius;

        public Point[] vertices { get; } = new Point[3];
        public Point vertA { get { return vertices[0]; } }
        public Point vertB { get { return vertices[1]; } }
        public Point vertC { get { return vertices[2]; } }

        public int vertAIndex;
        public int vertBIndex;
        public int vertCIndex;
        public Vector2 circumCentre { get { return CircumCentre; } }
        public float circumRadius { get { return CircumRadius; } }
        public float inSideCircumRadius;
        public float area;
        public string ToString()
        {
            return $"({vertAIndex},{vertBIndex},{vertCIndex})";
        }

        public Triangle(Point pointA, Point pointB, Point pointC, int pointAIndex, int pointBIndex, int pointCIndex)
        {
            bool isCounterClockwise = IsCounterClockwise(pointA, pointB, pointC);

            vertices[0] = pointA;
            vertices[1] = isCounterClockwise ? pointB : pointC;
            vertices[2] = isCounterClockwise ? pointC : pointB;
            vertAIndex = pointAIndex;
            vertBIndex = isCounterClockwise ? pointBIndex : pointCIndex;
            vertCIndex = isCounterClockwise ? pointCIndex : pointBIndex;
            CircumCentre = ComputeCircumCentre();
            CircumRadius = ComputeCircumRadius();
            var r = CalculateInradius();
            inSideCircumRadius = r.Item1;
            area = r.Item2;
        }

        public Vector2 GetCentroid()
        {
            return new Vector2((vertA.x + vertB.x + vertC.x) / 3, (vertA.y + vertB.y + vertC.y) / 3);
        }
        public Edge[] GetEdges()
        {
            return new Edge[]
            {
            new Edge(vertA, vertB),
            new Edge(vertB, vertC),
            new Edge(vertA, vertC),
            };
        }

        public Edge GetOppositeEdge(Point p)
        {
            if (!p.Equals(vertA) && !p.Equals(vertB))
            {
                return new Edge(vertA, vertB);
            }
            if (!p.Equals(vertB) && !p.Equals(vertC))
            {
                return new Edge(vertB, vertC);
            }
            if (!p.Equals(vertA) && !p.Equals(vertC))
            {
                return new Edge(vertA, vertC);
            }
            throw new Exception("The point is not a vertex of the triangle.");
        }
        private bool IsCounterClockwise(Point pointA, Point pointB, Point pointC)
        {
            float result = (pointB.x - pointA.x) * (pointC.y - pointA.y) - (pointC.x - pointA.x) * (pointB.y - pointA.y);
            return result > 0;
        }
        public bool ContainsEdge(Edge edge)
        {
            int sharedVerts = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (vertices[i].EqualsPoint(edge.vertexA) || vertices[i].EqualsPoint(edge.vertexB))
                {
                    sharedVerts++;
                }
            }
            return sharedVerts == 2;
        }
        public (Vector2, Vector2) FindObtuseAngleAndBisectorIntersection()
        {
            float angleA = CalculateAngle(vertB.pos, vertA.pos, vertC.pos);
            float angleB = CalculateAngle(vertA.pos, vertB.pos, vertC.pos);
            float angleC = CalculateAngle(vertA.pos, vertC.pos, vertB.pos);

            Vector2 obtusePoint = Vector2.zero;
            Vector2 bisectorIntersection = Vector2.zero;

            if (angleA >= 90)
            {
                obtusePoint = vertA.pos;
                bisectorIntersection = CalculateBisectorIntersection(vertA.pos, vertB.pos, vertC.pos);
            }
            else if (angleB >= 90)
            {
                obtusePoint = vertB.pos;
                bisectorIntersection = CalculateBisectorIntersection(vertB.pos, vertA.pos, vertC.pos);
            }
            else if (angleC >= 90)
            {
                obtusePoint = vertC.pos;
                bisectorIntersection = CalculateBisectorIntersection(vertC.pos, vertA.pos, vertB.pos);
            }

            return (obtusePoint, bisectorIntersection);
        }
        public float CalculateAngle(Vector2 point1, Vector2 point2, Vector2 point3)
        {
            Vector2 vec1 = point1 - point2;
            Vector2 vec2 = point3 - point2;
            float dotProduct = Vector2.Dot(vec1, vec2);
            float magnitudeProduct = vec1.magnitude * vec2.magnitude;
            return Mathf.Acos(dotProduct / magnitudeProduct) * Mathf.Rad2Deg;
        }
        public (float, float) CalculateInradius()
        {
            var x1 = vertA.pos[0];
            var x2 = vertB.pos[0];
            var x3 = vertC.pos[0];
            var y1 = vertA.pos[1];
            var y2 = vertB.pos[1];
            var y3 = vertC.pos[1];
            // 计算边长
            double a = Math.Sqrt(Math.Pow(x2 - x3, 2) + Math.Pow(y2 - y3, 2));
            double b = Math.Sqrt(Math.Pow(x1 - x3, 2) + Math.Pow(y1 - y3, 2));
            double c = Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2));

            // 计算半周长
            double s = (a + b + c) / 2;

            // 计算面积
            double area = Math.Sqrt(s * (s - a) * (s - b) * (s - c));

            // 计算内接圆半径
            double inradius = area / s;

            return ((float)inradius, (float)area);
        }
        public Vector2 CalculateBisectorIntersection(Vector2 A, Vector2 B, Vector2 C)
        {
            // 使用角平分线定理计算角平分线与对边的交点
            float AB = Vector2.Distance(A, B);
            float AC = Vector2.Distance(A, C);
            float ratio = AB / AC;

            Vector2 intersection = (B + ratio * C) / (1 + ratio);
            return intersection;
        }
        public Vector2 ComputeCircumCentre()
        {
            Vector2 A = vertA.pos;
            Vector2 B = vertB.pos;
            Vector2 C = vertC.pos;
            Vector2 SqrA = new Vector2(Mathf.Pow(A.x, 2f), Mathf.Pow(A.y, 2f));
            Vector2 SqrB = new Vector2(Mathf.Pow(B.x, 2f), Mathf.Pow(B.y, 2f));
            Vector2 SqrC = new Vector2(Mathf.Pow(C.x, 2f), Mathf.Pow(C.y, 2f));

            float D = (A.x * (B.y - C.y) + B.x * (C.y - A.y) + C.x * (A.y - B.y)) * 2f;
            float x = ((SqrA.x + SqrA.y) * (B.y - C.y) + (SqrB.x + SqrB.y) * (C.y - A.y) + (SqrC.x + SqrC.y) * (A.y - B.y)) / D;
            float y = ((SqrA.x + SqrA.y) * (C.x - B.x) + (SqrB.x + SqrB.y) * (A.x - C.x) + (SqrC.x + SqrC.y) * (B.x - A.x)) / D;
            return new Vector2(x, y);
        }

        public float ComputeCircumRadius()
        {
            Vector2 circumCentre = ComputeCircumCentre();
            return Vector2.Distance(circumCentre, vertices[0].pos);
        }
        public bool ArePointsCollinear(float scale = 0)
        {
            // 向量 p2 -> p1
            Vector2 vector1 = (vertB.pos - vertA.pos).normalized;
            // 向量 p3 -> p2
            Vector2 vector2 = (vertC.pos - vertA.pos).normalized;

            // 计算向量的叉积
            float crossProduct = vector1.x * vector2.y - vector1.y * vector2.x;
            // 如果叉积接近于零，则认为这三个点在一条直线上
            return Mathf.Approximately(crossProduct, scale);
        }


    }

}