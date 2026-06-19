using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GeoToolkit
{
    public static class DelaunayHelper
    {
        private const float Margin = 3f;


        public static List<Point> VectorToDelaunPoints(List<Vector2> poslist)
        {
            var points = new List<Point>();
            for (int i = 0; i < poslist.Count; i++)
            {
                points.Add(new Point(poslist[i].x, poslist[i].y));
            }

            return points;
        }
        public static List<Vector2> DelaunPointsToVector2(List<Point> poslist)
        {
            var points = new List<Vector2>();
            for (int i = 0; i < poslist.Count; i++)
            {
                points.Add(new Vector2(poslist[i].x, poslist[i].y));
            }

            return points;
        }
        public static List<Triangle> Delaun(List<Point> sourcepoints, bool Limit = true)
        {
            var points = new List<Point>();
            for (int i = 0; i < sourcepoints.Count; i++)
            {
                points.Add(sourcepoints[i]);
            }

            int pointsCount = sourcepoints.Count;
            List<Triangle> triangles = new List<Triangle>();
            List<Edge> limitEdge = new List<Edge>();
            if (Limit)
            {
                var conv = MathHelper.FindConcaveSortPoints(points);

                Debug.Log(sourcepoints.Count);
                Debug.Log(conv.Item1.Count);
                Debug.Log(conv.Item2.Count);
                for (int i = 1; i < conv.Item1.Count; i++)
                {
                    Debug.Log(-1 % 20);
                    Debug.Log($"{conv.Item2.IndexOf(conv.Item1[i])}");
                    Debug.Log($"{(conv.Item2.IndexOf(conv.Item1[i]) - 1)}");
                    Debug.Log($"{(conv.Item2.IndexOf(conv.Item1[i]) - 1) % pointsCount}");
                    var pre = conv.Item2[(conv.Item2.IndexOf(conv.Item1[i]) - 1) % pointsCount];
                    var next = conv.Item2[(conv.Item2.IndexOf(conv.Item1[i]) + 1) % pointsCount];
                    limitEdge.Add(new Edge(new Point(pre.pos.x, pre.pos.y), new Point(next.pos.x, next.pos.y)));
                }

                points = conv.Item2;
            }

            PointBounds bounds = MathHelper.GetPointBounds(sourcepoints);
            Triangle supraTriangle = MathHelper.GenerateSupraTriangle(bounds, Margin);
            triangles.Add(supraTriangle);
            for (int pIndex = 0; pIndex < points.Count; pIndex++)
            {
                //获取插入点
                Point p = points[pIndex];

                List<Triangle> badTriangles = new List<Triangle>();
                //遍历三角形，哪个三角形的外接圆包含这个点
                for (int triIndex = triangles.Count - 1; triIndex >= 0; triIndex--)
                {
                    Triangle triangle = triangles[triIndex];

                    float dist = Vector2.Distance(p.pos, triangle.circumCentre);
                    if (dist < triangle.circumRadius)
                    {
                        badTriangles.Add(triangle);
                    }
                }


                List<Edge> polygon = new List<Edge>();
                //遍历包含这个点的外接圆
                for (int i = 0; i < badTriangles.Count; i++)
                {
                    Triangle triangle = badTriangles[i];
                    //获取这个三角形的边
                    Edge[] edges = triangle.GetEdges();
                    //遍历所有边
                    for (int j = 0; j < edges.Length; j++)
                    {
                        bool rejectEdge = false;
                        for (int t = 0; t < badTriangles.Count; t++)
                        {
                            if (t != i && badTriangles[t].ContainsEdge(edges[j]))
                            {
                                rejectEdge = true;
                            }
                        }

                        if (!rejectEdge)
                        {
                            polygon.Add(edges[j]);
                        }
                    }
                }

                for (int i = badTriangles.Count - 1; i >= 0; i--)
                {
                    triangles.Remove(badTriangles[i]);
                }

                for (int i = 0; i < polygon.Count; i++)
                {
                    Edge edge = polygon[i];
                    Point pointA = new Point(p.x, p.y);
                    Point pointB = new Point(edge.vertexA);
                    Point pointC = new Point(edge.vertexB);
                    var tri = new Triangle(pointA, pointB, pointC, points.IndexOf(pointA), points.IndexOf(pointB),
                        points.IndexOf(pointC));

                    Edge[] edges = tri.GetEdges();
                    bool rejectEdge = false;
                    for (int j = 0; j < limitEdge.Count; j++)
                    {
                        for (int k = 0; k < edges.Length; k++)
                        {
                            if (limitEdge[j].EqualsEdge(edges[k]))
                            {
                                rejectEdge = true;
                                break;
                            }
                        }
                    }

                    if (!rejectEdge)

                        triangles.Add(tri);
                }
            }

            for (int i = triangles.Count - 1; i >= 0; i--)
            {
                Triangle triangle = triangles[i];
                for (int j = 0; j < triangle.vertices.Length; j++)
                {
                    bool removeTriangle = false;
                    Point vertex = triangle.vertices[j];
                    for (int s = 0; s < supraTriangle.vertices.Length; s++)
                    {
                        if (vertex.EqualsPoint(supraTriangle.vertices[s]))
                        {
                            removeTriangle = true;
                            break;
                        }
                    }

                    if (removeTriangle)
                    {
                        triangles.RemoveAt(i);
                        break;
                    }
                }
            }
            for (int i = triangles.Count - 1; i >= 0; i--)
            {
                Triangle triangle = triangles[i];
                if (!MathHelper.IsPointInPolygonRay(triangle.GetCentroid(), DelaunPointsToVector2(points)))
                {
                    triangles.RemoveAt(i);
                }
            }
            return triangles;
        }

        public static (List<Point>, List<Triangle>) DelaunTesselation(List<Point> sourcepoints, List<List<Vector2>> polygons = null, int iterations = 0)
        {
            Stopwatch sw = new Stopwatch();
            sw.Restart();

            var points = new List<Point>(sourcepoints);
            List<Triangle> triangles = new List<Triangle>();
            PointBounds bounds = MathHelper.GetPointBounds(points);
            Triangle supraTriangle = MathHelper.GenerateSupraTriangle(bounds, Margin);
            triangles.Add(supraTriangle);

            sw.Stop();
            // Debug.Log($"耗时 (德劳内计算-Ready): {sw.ElapsedMilliseconds} 毫秒");

            sw.Restart();
            triangles = CALC(points, triangles, null, supraTriangle, polygons);

            // for (int i = 0; i < iterations; i++)
            // {
            //     List<Point> newPoints = new List<Point>();
            //     
            //     Parallel.For(0, triangles.Count, j =>
            //     {
            //         var triangle = triangles[j];
            //         if (triangle.inSideCircumRadius < 5) return;
            //         
            //         float centroidX = (triangle.vertA.x + triangle.vertB.x + triangle.vertC.x) / 3;
            //         float centroidY = (triangle.vertA.y + triangle.vertB.y + triangle.vertC.y) / 3;
            //         var newPoint = new Point(centroidX, centroidY);
            //         
            //         lock (newPoints)
            //         {
            //             bool isadd = true;
            //             for (int k = 1; k < polygons.Count; k++)
            //             {
            //                 if (MathHelper.IsPointInPolygonRay(new Vector2(centroidX,centroidY), polygons[k]))
            //                 {
            //                     isadd = false;
            //                     break;
            //                 }
            //             }
            //             if(isadd)
            //             newPoints.Add(newPoint);
            //         }
            //     });
            //
            //     if (newPoints.Count > 0)
            //     {
            //         points.AddRange(newPoints);
            //         triangles = CALC(points, triangles, null, supraTriangle,polygons, false);
            //     }
            //     else
            //     {
            //         break;
            //     }
            // }

            sw.Stop();
            // Debug.Log($"耗时 (德劳内计算- {iterations}次细分): {sw.ElapsedMilliseconds} 毫秒");

            return (points, triangles);
        }
        private static List<Triangle> CALC(List<Point> points, List<Triangle> triangles, List<Edge> limitEdge, Triangle supraTriangle, List<List<Vector2>> inPolygons = null, bool isJudgeInPolygon = true)
        {
            // Stopwatch sw = new Stopwatch();
            // sw.Restart();
            for (int pIndex = 0; pIndex < points.Count; pIndex++)
            {
                //获取插入点
                Point p = points[pIndex];

                List<Triangle> badTriangles = new List<Triangle>();
                //遍历三角形，哪个三角形的外接圆包含这个点
                for (int triIndex = triangles.Count - 1; triIndex >= 0; triIndex--)
                {
                    Triangle triangle = triangles[triIndex];

                    float dist = Vector2.Distance(p.pos, triangle.circumCentre);
                    if (dist < triangle.circumRadius)
                    {
                        badTriangles.Add(triangle);
                    }
                }


                List<Edge> polygon = new List<Edge>();
                //遍历包含这个点的外接圆
                for (int i = 0; i < badTriangles.Count; i++)
                {
                    Triangle triangle = badTriangles[i];
                    //获取这个三角形的边
                    Edge[] edges = triangle.GetEdges();
                    //遍历所有边
                    for (int j = 0; j < edges.Length; j++)
                    {
                        bool rejectEdge = false;
                        for (int t = 0; t < badTriangles.Count; t++)
                        {
                            if (t != i && badTriangles[t].ContainsEdge(edges[j]))
                            {
                                rejectEdge = true;
                            }
                        }

                        if (!rejectEdge)
                        {
                            polygon.Add(edges[j]);
                        }
                    }
                }

                for (int i = badTriangles.Count - 1; i >= 0; i--)
                {
                    triangles.Remove(badTriangles[i]);
                }

                if (limitEdge != null)
                {
                    for (int i = 0; i < polygon.Count; i++)
                    {
                        Edge edge = polygon[i];
                        Point pointA = new Point(p.x, p.y);
                        Point pointB = new Point(edge.vertexA);
                        Point pointC = new Point(edge.vertexB);
                        var tri = new Triangle(pointA, pointB, pointC, points.IndexOf(pointA), points.IndexOf(pointB),
                            points.IndexOf(pointC));
                        if (tri.ArePointsCollinear()) continue;
                        Edge[] edges = tri.GetEdges();
                        bool rejectEdge = false;
                        for (int j = 0; j < limitEdge.Count; j++)
                        {
                            for (int k = 0; k < edges.Length; k++)
                            {
                                if (limitEdge[j].EqualsEdge(edges[k]))
                                {
                                    rejectEdge = true;
                                    break;
                                }
                            }
                        }
                        if (!rejectEdge)
                            triangles.Add(tri);
                    }
                }
                else
                {
                    for (int i = 0; i < polygon.Count; i++)
                    {
                        Edge edge = polygon[i];
                        Point pointA = new Point(p.x, p.y);
                        Point pointB = new Point(edge.vertexA);
                        Point pointC = new Point(edge.vertexB);
                        var tri = new Triangle(pointA, pointB, pointC, points.IndexOf(pointA), points.IndexOf(pointB),
                            points.IndexOf(pointC));
                        triangles.Add(tri);
                    }
                }

            }

            if (supraTriangle != null)
            {
                for (int i = triangles.Count - 1; i >= 0; i--)
                {
                    Triangle triangle = triangles[i];
                    for (int j = 0; j < triangle.vertices.Length; j++)
                    {
                        bool removeTriangle = false;
                        Point vertex = triangle.vertices[j];
                        for (int s = 0; s < supraTriangle.vertices.Length; s++)
                        {
                            if (vertex.EqualsPoint(supraTriangle.vertices[s]))
                            {
                                removeTriangle = true;
                                break;
                            }
                        }

                        if (removeTriangle)
                        {
                            triangles.RemoveAt(i);
                            break;
                        }
                    }
                }
            }

            if (isJudgeInPolygon)
            {
                for (int i = triangles.Count - 1; i >= 0; i--)
                {
                    Triangle triangle = triangles[i];
                    if (!MathHelper.IsPointInPolygonRay(triangle.GetCentroid(), DelaunPointsToVector2(points)))
                    {
                        triangles.RemoveAt(i);
                    }
                }
                // for (int i = triangles.Count - 1; i >= 0; i--)
                // {
                //     Triangle triangle = triangles[i];
                //     for (int j = 1; j < inPolygons.Count; j++)
                //     {
                //         if (MathHelper.IsPointInPolygonRay(triangle.GetCentroid(), inPolygons[j]))
                //         {
                //             triangles.RemoveAt(i);
                //         }
                //     }
                // }
            }

            // sw.Stop();
            // Debug.Log($"耗时 (德劳内计算-CALC): {sw.ElapsedMilliseconds} 毫秒");
            return triangles;
        }

        public static Mesh CreateMeshFromTriangulation(List<Triangle> triangulation)
        {
            Mesh mesh = new Mesh();

            int vertexCount = triangulation.Count * 3;

            Vector3[] verticies = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[vertexCount];

            int vertexIndex = 0;
            int triangleIndex = 0;
            for (int i = 0; i < triangulation.Count; i++)
            {
                Triangle triangle = triangulation[i];

                verticies[vertexIndex] = new Vector3(triangle.vertA.x, triangle.vertA.y, 0f);
                verticies[vertexIndex + 1] = new Vector3(triangle.vertB.x, triangle.vertB.y, 0f);
                verticies[vertexIndex + 2] = new Vector3(triangle.vertC.x, triangle.vertC.y, 0f);

                uvs[vertexIndex] = triangle.vertA.pos;
                uvs[vertexIndex + 1] = triangle.vertB.pos;
                uvs[vertexIndex + 2] = triangle.vertC.pos;

                triangles[triangleIndex] = vertexIndex + 2;
                triangles[triangleIndex + 1] = vertexIndex + 1;
                triangles[triangleIndex + 2] = vertexIndex;

                vertexIndex += 3;
                triangleIndex += 3;
            }

            mesh.vertices = verticies;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            return mesh;
        }
    }
}