using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GeoToolkit
{
    public static class MathHelper
    {
        public static Vector2 MidPointOfLine(Point p1, Point p2)
        {
            return new Vector2((p1.x + p2.x) / 2, (p1.y + p2.y) / 2);
        }

        public static float GradientOfLine(Point p1, Point p2)
        {
            return (p2.y - p1.y) / (p2.x - p1.x);
        }

        public static float NegativeReciprocal(float value)
        {
            return -(1 / value);
        }

        public static Vector2 LineIntersection(float A1, float B1, float C1, float A2, float B2, float C2)
        {
            float delta = A1 * B2 - A2 * B1;
            if (delta == 0)
            {
                throw new ArgumentException("Lines are parallel");
            }

            float x = (B2 * C1 - B1 * C2) / delta;
            float y = (A1 * C2 - A2 * C1) / delta;
            return new Vector2(x, y);
        }

        public static bool IsPointInPolygonRay(Vector2 point, List<Vector2> vertices, float epsilon = 0)
        {
            int intersectCount = 0;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector2 vertex1 = vertices[i];
                Vector2 vertex2 = vertices[(i + 1) % vertices.Count];

                // 确保vertex1.y <= vertex2.y
                if (vertex1.y > vertex2.y)
                {
                    Vector2 temp = vertex1;
                    vertex1 = vertex2;
                    vertex2 = temp;
                }

                if (epsilon > 0)
                {
                    // Check if point is exactly on a vertex
                    if (Mathf.Abs(point.x - vertex1.x) < epsilon && Mathf.Abs(point.y - vertex1.y) < epsilon ||
                        Mathf.Abs(point.x - vertex2.x) < epsilon && Mathf.Abs(point.y - vertex2.y) < epsilon)
                    {
                        return true;
                    }
                }

                // 如果点在vertex1和vertex2之间，并且在横向射线左边，则认为射线与多边形边相交
                if (point.y > vertex1.y && point.y <= vertex2.y &&
                    point.x < (vertex2.x - vertex1.x) * (point.y - vertex1.y) / (vertex2.y - vertex1.y) + vertex1.x)
                {
                    intersectCount++;
                }
            }

            return intersectCount % 2 == 1;
        }

        //计算一个点，在一个三维空间的三角形内，这个点的高度取值
        public static float CalculatePointHeight(Vector3 v1, Vector3 v2, Vector3 v3, Vector2 p)
        {
            float x1 = v1.x, y1 = v1.z, z1 = v1.y;
            float x2 = v2.x, y2 = v2.z, z2 = v2.y;
            float x3 = v3.x, y3 = v3.z, z3 = v3.y;

            float x = p.x, y = p.y;

            // 计算重心坐标
            float denominator = (y2 - y3) * (x1 - x3) + (x3 - x2) * (y1 - y3);
            float lambda1 = ((y2 - y3) * (x - x3) + (x3 - x2) * (y - y3)) / denominator;
            float lambda2 = ((y3 - y1) * (x - x3) + (x1 - x3) * (y - y3)) / denominator;
            float lambda3 = 1 - lambda1 - lambda2;

            // 使用重心坐标计算内部点的高度
            float height = lambda1 * z1 + lambda2 * z2 + lambda3 * z3;
            return height;
        }

        public static float CalculatePolygonArea(List<Vector2> polygon)
        {
            int n = polygon.Count;
            float area = 0f;

            for (int i = 0; i < n; i++)
            {
                Vector2 current = polygon[i];
                Vector2 next = polygon[(i + 1) % n];
                area += current.x * next.y - next.x * current.y;
            }

            return Mathf.Abs(area) / 2f;
        }

        /// <summary>
        /// 计算多边形的重心（中心点）。
        /// </summary>
        /// <param name="polygon">表示多边形顶点的 Vector2 列表。</param>
        /// <returns>多边形的重心。</returns>
        public static Vector2 CalculatePolygonCentroid(List<Vector2> polygon)
        {
            float centroidX = 0, centroidY = 0;
            foreach (var point in polygon)
            {
                centroidX += point.x;
                centroidY += point.y;
            }

            int numPoints = polygon.Count;
            return new Vector2(centroidX / numPoints, centroidY / numPoints);
        }

        /// <summary>
        /// 使用高斯分布在给定的多边形内生成一组点。
        /// </summary>
        /// <param name="polygon">表示多边形顶点的 Vector2 列表。</param>
        /// <param name="count">要在多边形内生成的点的数量。</param>
        /// <param name="meanX">高斯分布的 X 坐标的均值（平均值）。</param>
        /// <param name="meanY">高斯分布的 Y 坐标的均值（平均值）。</param>
        /// <param name="stdDevX">高斯分布的 X 坐标的标准差。</param>
        /// <param name="stdDevY">高斯分布的 Y 坐标的标准差。</param>
        /// <returns>在多边形内生成的 Vector2 点的列表。</returns>
        public static List<Vector2> GenerateGaussianPointsInPolygon(List<Vector2> polygon, int count, float meanX,
            float meanY, float stdDevX, float stdDevY)
        {
            List<Vector2> points = new List<Vector2>();
            System.Random rand = new System.Random();

            while (points.Count < count)
            {
                // 生成高斯分布的随机点
                float x = (float)(meanX + stdDevX * BoxMullerTransform(rand));
                float y = (float)(meanY + stdDevY * BoxMullerTransform(rand));
                Vector2 point = new Vector2(x, y);

                // 检查点是否在多边形内
                if (IsPointInPolygonRay(point, polygon))
                {
                    points.Add(point);
                }
            }

            return points;
        }

        // Box-Muller变换生成高斯分布的随机数
        private static float BoxMullerTransform(System.Random rand)
        {
            float u1 = (float)rand.NextDouble();
            float u2 = (float)rand.NextDouble();
            return Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
        }

        public static void InsertPointsAtSpecifiedDistance(ref List<Vector2> points,
            ref Dictionary<Vector2, Vector3> Point3DDic, float distance)
        {
            List<Vector2> result = new List<Vector2>();

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 start = points[i];
                Vector3 start3d = Point3DDic[start];
                Vector2 end = points[(i + 1) % points.Count];
                Vector3 end3d = Point3DDic[end];
                result.Add(start);

                float segmentDistance = Vector2.Distance(start, end);
                int numNewPoints = Mathf.FloorToInt(segmentDistance / distance);
                float rDistance = segmentDistance / numNewPoints;
                for (int j = 1; j <= numNewPoints; j++)
                {
                    float t = j * rDistance / segmentDistance;
                    Vector3 newPoint = Vector3.Lerp(start3d, end3d, t);
                    Vector2 newPoint2d = new Vector2(newPoint.x, newPoint.z);
                    result.Add(newPoint2d);
                    Point3DDic[newPoint2d] = newPoint;
                }
            }

            // 添加最后一个点
            if (points.Count > 0)
            {
                result.Add(points[points.Count - 1]);
            }

            points = result;
        }

        /// <summary>
        /// 获取多边形的边界框。
        /// </summary>
        /// <param name="polygon">表示多边形顶点的 Vector2 列表。</param>
        /// <returns>多边形的边界框。</returns>
        public static Rect GetPolygonBounds(List<Vector2> polygon)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var point in polygon)
            {
                if (point.x < minX) minX = point.x;
                if (point.y < minY) minY = point.y;
                if (point.x > maxX) maxX = point.x;
                if (point.y > maxY) maxY = point.y;
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        public static List<Vector2> GeneratePoissonPointsInPolygon(List<Vector2> polygon, float radius)
        {
            // 计算多边形的边界框
            Rect bounds = GetPolygonBounds(polygon);
            Vector2 center = CalculatePolygonCentroid(polygon);
            bounds.center = center;
            Vector2 sampleRegionSize = new Vector2(bounds.width, bounds.height);
            // 使用泊松盘采样生成点
            List<Vector2> points = GeneratePoissonDiskSamplingPoints(radius, sampleRegionSize);

            // 筛选在多边形内的点
            List<Vector2> pointsInPolygon = new List<Vector2>();
            foreach (var point in points)
            {
                if (IsPointInPolygonRay(point, polygon))
                {
                    pointsInPolygon.Add(point);
                }
            }

            return pointsInPolygon;
        }

        private static List<Vector2> GeneratePoissonDiskSamplingPoints(float radius, Vector2 sampleRegionSize,
            int numSamplesBeforeRejection = 30)
        {
            float cellSize = radius / Mathf.Sqrt(2);
            int[,] grid = new int[Mathf.CeilToInt(sampleRegionSize.x / cellSize),
                Mathf.CeilToInt(sampleRegionSize.y / cellSize)];
            List<Vector2> points = new List<Vector2>();
            List<Vector2> spawnPoints = new List<Vector2>();

            spawnPoints.Add(sampleRegionSize / 2);
            while (spawnPoints.Count > 0)
            {
                int spawnIndex = UnityEngine.Random.Range(0, spawnPoints.Count);
                Vector2 spawnCentre = spawnPoints[spawnIndex];
                bool candidateAccepted = false;

                for (int i = 0; i < numSamplesBeforeRejection; i++)
                {
                    float angle = UnityEngine.Random.value * Mathf.PI * 2;
                    Vector2 dir = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                    Vector2 candidate = spawnCentre + dir * UnityEngine.Random.Range(radius, 2 * radius);
                    if (IsValid(candidate, sampleRegionSize, cellSize, radius, points, grid))
                    {
                        points.Add(candidate);
                        spawnPoints.Add(candidate);
                        grid[(int)(candidate.x / cellSize), (int)(candidate.y / cellSize)] = points.Count;
                        candidateAccepted = true;
                        break;
                    }
                }

                if (!candidateAccepted)
                {
                    spawnPoints.RemoveAt(spawnIndex);
                }
            }

            return points;
        }

        private static bool IsValid(Vector2 candidate, Vector2 sampleRegionSize, float cellSize, float radius,
            List<Vector2> points, int[,] grid)
        {
            if (candidate.x >= 0 && candidate.x < sampleRegionSize.x && candidate.y >= 0 &&
                candidate.y < sampleRegionSize.y)
            {
                int cellX = (int)(candidate.x / cellSize);
                int cellY = (int)(candidate.y / cellSize);
                int searchStartX = Mathf.Max(0, cellX - 2);
                int searchEndX = Mathf.Min(cellX + 2, grid.GetLength(0) - 1);
                int searchStartY = Mathf.Max(0, cellY - 2);
                int searchEndY = Mathf.Min(cellY + 2, grid.GetLength(1) - 1);

                for (int x = searchStartX; x <= searchEndX; x++)
                {
                    for (int y = searchStartY; y <= searchEndY; y++)
                    {
                        int pointIndex = grid[x, y] - 1;
                        if (pointIndex != -1)
                        {
                            float sqrDst = (candidate - points[pointIndex]).sqrMagnitude;
                            if (sqrDst < radius * radius)
                            {
                                return false;
                            }
                        }
                    }
                }

                return true;
            }

            return false;
        }

        // 方法：判断两个多边形是否有交集
        private static bool PolygonsIntersect(List<Vector2> polygon1, List<Vector2> polygon2)
        {
            return !SATCheck(polygon1, polygon2) && !SATCheck(polygon2, polygon1);
        }

        // 使用分离轴定理（SAT）检测多边形是否相交
        private static bool SATCheck(List<Vector2> polygon1, List<Vector2> polygon2)
        {
            foreach (Vector2 p1 in polygon1)
            {
                Vector2 edge = polygon1[(polygon1.IndexOf(p1) + 1) % polygon1.Count] - p1;
                Vector2 axis = new Vector2(-edge.y, edge.x).normalized;

                if (!IsOverlap(polygon1, polygon2, axis))
                    return true;
            }

            return false;
        }

        // 方法：检测在指定轴上的投影是否重叠
        private static bool IsOverlap(List<Vector2> polygon1, List<Vector2> polygon2, Vector2 axis)
        {
            float minA = float.MaxValue, maxA = float.MinValue;
            foreach (Vector2 p in polygon1)
            {
                float dot = Vector2.Dot(axis, p);
                minA = Mathf.Min(minA, dot);
                maxA = Mathf.Max(maxA, dot);
            }

            float minB = float.MaxValue, maxB = float.MinValue;
            foreach (Vector2 p in polygon2)
            {
                float dot = Vector2.Dot(axis, p);
                minB = Mathf.Min(minB, dot);
                maxB = Mathf.Max(maxB, dot);
            }

            return !(maxA < minB || maxB < minA);
        }

        // 方法：合并有交集的多边形，并剔除交集点
        private static List<Vector2> MergePolygons(List<Vector2> polygon1, List<Vector2> polygon2)
        {
            List<Vector2> mergedPolygon = new List<Vector2>();

            // 找到多边形的交点
            List<Vector2> intersectionPoints = FindIntersectionPoints(polygon1, polygon2);

            // 合并多边形顶点，剔除交点
            foreach (Vector2 point in polygon1)
            {
                if (!intersectionPoints.Contains(point))
                    mergedPolygon.Add(point);
            }

            foreach (Vector2 point in polygon2)
            {
                if (!intersectionPoints.Contains(point))
                    mergedPolygon.Add(point);
            }

            // 添加交点
            mergedPolygon.AddRange(intersectionPoints);

            // 重新排序顶点以确保正确的多边形形状
            mergedPolygon = SortVertices(mergedPolygon);

            return mergedPolygon;
        }

        // 方法：找到两个多边形的交点
        private static List<Vector2> FindIntersectionPoints(List<Vector2> polygon1, List<Vector2> polygon2)
        {
            List<Vector2> intersectionPoints = new List<Vector2>();

            for (int i = 0; i < polygon1.Count; i++)
            {
                Vector2 a1 = polygon1[i];
                Vector2 a2 = polygon1[(i + 1) % polygon1.Count];

                for (int j = 0; j < polygon2.Count; j++)
                {
                    Vector2 b1 = polygon2[j];
                    Vector2 b2 = polygon2[(j + 1) % polygon2.Count];

                    if (LineSegmentsIntersect(a1, a2, b1, b2, out Vector2 intersection))
                    {
                        if (!intersectionPoints.Contains(intersection))
                            intersectionPoints.Add(intersection);
                    }
                }
            }

            return intersectionPoints;
        }

        // 方法：检测线段是否相交，并找到交点
        private static bool LineSegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 intersection)
        {
            intersection = Vector2.zero;

            float d = (p1.x - p2.x) * (p3.y - p4.y) - (p1.y - p2.y) * (p3.x - p4.x);
            if (d == 0) return false;

            float xi = ((p3.x - p4.x) * (p1.x * p2.y - p1.y * p2.x) - (p1.x - p2.x) * (p3.x * p4.y - p3.y * p4.x)) / d;
            float yi = ((p3.y - p4.y) * (p1.x * p2.y - p1.y * p2.x) - (p1.y - p2.y) * (p3.x * p4.y - p3.y * p4.x)) / d;
            intersection = new Vector2(xi, yi);

            if (IsPointOnLineSegment(intersection, p1, p2) && IsPointOnLineSegment(intersection, p3, p4))
                return true;

            return false;
        }

        // 方法：检测点是否在线段上
        private static bool IsPointOnLineSegment(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
        {
            return Mathf.Min(lineStart.x, lineEnd.x) <= point.x && point.x <= Mathf.Max(lineStart.x, lineEnd.x) &&
                   Mathf.Min(lineStart.y, lineEnd.y) <= point.y && point.y <= Mathf.Max(lineStart.y, lineEnd.y);
        }

        // // 方法：对顶点进行排序以形成正确的多边形
        // private  static List<Vector2> SortVertices(List<Vector2> vertices)
        // {
        //     Vector2 center = Vector2.zero;
        //     foreach (Vector2 vertex in vertices)
        //     {
        //         center += vertex;
        //     }
        //     center /= vertices.Count;
        //
        //     vertices.Sort((a, b) => Mathf.Atan2(a.y - center.y, a.x - center.x).CompareTo(Mathf.Atan2(b.y - center.y, b.x - center.x)));
        //     return vertices;
        // }

        // 方法：合并所有有交集的多边形
        public static List<List<Vector2>> MergeIntersectingPolygons(List<List<Vector2>> polygons)
        {
            bool merged;
            do
            {
                merged = false;
                for (int i = 0; i < polygons.Count; i++)
                {
                    for (int j = i + 1; j < polygons.Count; j++)
                    {
                        if (PolygonsIntersect(polygons[i], polygons[j]))
                        {
                            List<Vector2> mergedPolygon = MergePolygons(polygons[i], polygons[j]);
                            polygons.RemoveAt(j);
                            polygons[i] = mergedPolygon;
                            merged = true;
                            break;
                        }
                    }

                    if (merged)
                        break;
                }
            } while (merged);

            return polygons;
        }

        public static Triangle GenerateSupraTriangle(PointBounds bounds, float Margin)
        {
            float dMax = Mathf.Max(bounds.maxX - bounds.minX, bounds.maxY - bounds.minY) * Margin;
            float xCen = (bounds.minX + bounds.maxX) * 0.5f;
            float yCen = (bounds.minY + bounds.maxY) * 0.5f;

            float x1 = xCen - 0.866f * dMax;
            float x2 = xCen + 0.866f * dMax;
            float x3 = xCen;

            float y1 = yCen - 0.5f * dMax;
            float y2 = yCen - 0.5f * dMax;
            float y3 = yCen + dMax;

            Point pointA = new Point(x1, y1);
            Point pointB = new Point(x2, y2);
            Point pointC = new Point(x3, y3);

            return new Triangle(pointA, pointB, pointC, -1, -1, -1);
        }

        public static PointBounds GetPointBounds(List<Point> points)
        {
            float minX = Mathf.Infinity;
            float minY = Mathf.Infinity;
            float maxX = Mathf.NegativeInfinity;
            float maxY = Mathf.NegativeInfinity;

            for (int i = 0; i < points.Count; i++)
            {
                Point p = points[i];
                if (minX > p.x)
                {
                    minX = p.x;
                }

                if (minY > p.y)
                {
                    minY = p.y;
                }

                if (maxX < p.x)
                {
                    maxX = p.x;
                }

                if (maxY < p.y)
                {
                    maxY = p.y;
                }
            }

            return new PointBounds(minX, minY, maxX, maxY);
        }

        public static (List<Point>, List<Point>) FindConcaveSortPoints(List<Point> points)
        {
            List<Point> concavePoints = new List<Point>();

            List<Point> sortedPoints = SortPoints(points);

            int n = sortedPoints.Count;
            for (int i = 0; i < n; i++)
            {
                Point prev = sortedPoints[(i + n - 1) % n];
                Point current = sortedPoints[i];
                Point next = sortedPoints[(i + 1) % n];

                if (IsConcavePoint(prev, current, next))
                {
                    concavePoints.Add(current);
                }
            }

            return (concavePoints, sortedPoints);
        }

        public static List<Point> SortPoints(List<Point> points)
        {
            Point basePoint = points[0];
            foreach (var point in points)
            {
                if (point.y < basePoint.y || (point.y == basePoint.y && point.x < basePoint.x))
                {
                    basePoint = point;
                }
            }

            points.Sort((a, b) =>
            {
                float angleA = Mathf.Atan2(a.y - basePoint.y, a.x - basePoint.x);
                float angleB = Mathf.Atan2(b.y - basePoint.y, b.x - basePoint.x);
                return angleA.CompareTo(angleB);
            });

            return points;
        }

        public static List<Vector2> SortVertices(List<Vector2> points)
        {
            Vector2 basePoint = points[0];
            foreach (var point in points)
            {
                if (point.y < basePoint.y || (point.y == basePoint.y && point.x < basePoint.x))
                {
                    basePoint = point;
                }
            }

            points.Sort((a, b) =>
            {
                float angleA = Mathf.Atan2(a.y - basePoint.y, a.x - basePoint.x);
                float angleB = Mathf.Atan2(b.y - basePoint.y, b.x - basePoint.x);
                return angleA.CompareTo(angleB);
            });

            return points;
        }

        public static List<Vector2> SortVertices2(List<Vector2> vertices)
        {
            // 使用 Andrew's monotone chain 方法找到凸包
            List<Vector2> hull = GetConvexHull(vertices);

            // 插入凹角点
            List<Vector2> finalSortedVertices = InsertConcavePoints(hull, vertices);

            return finalSortedVertices;
        }

        private static bool IsConcavePoint(Point prev, Point current, Point next)
        {
            Vector2 v1 = current.pos - prev.pos;
            Vector2 v2 = next.pos - current.pos;

            float cross = v1.x * v2.y - v1.y * v2.x;
            return cross < 0;
        }

        private static bool IsConcave(Vector2 prev, Vector2 current, Vector2 next)
        {
            Vector2 v1 = current - prev;
            Vector2 v2 = next - current;

            float cross = v1.x * v2.y - v1.y * v2.x;
            return cross < 0;
        }

        private static List<Vector2> GetConvexHull(List<Vector2> points)
        {
            points = points.OrderBy(p => p.x).ThenBy(p => p.y).ToList();
            List<Vector2> hull = new List<Vector2>();

            // 下半部分
            foreach (var p in points)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0)
                {
                    hull.RemoveAt(hull.Count - 1);
                }

                hull.Add(p);
            }

            // 上半部分
            int t = hull.Count + 1;
            for (int i = points.Count - 1; i >= 0; i--)
            {
                Vector2 p = points[i];
                while (hull.Count >= t && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0)
                {
                    hull.RemoveAt(hull.Count - 1);
                }

                hull.Add(p);
            }

            hull.RemoveAt(hull.Count - 1);
            return hull;
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b)
        {
            return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        }

        private static List<Vector2> InsertConcavePoints(List<Vector2> hull, List<Vector2> points)
        {
            HashSet<Vector2> hullSet = new HashSet<Vector2>(hull);
            List<Vector2> concavePoints = points.Where(p => !hullSet.Contains(p)).ToList();

            foreach (var p in concavePoints)
            {
                int insertIndex = -1;
                float minDistance = float.MaxValue;

                for (int i = 0; i < hull.Count; i++)
                {
                    Vector2 a = hull[i];
                    Vector2 b = hull[(i + 1) % hull.Count];
                    float distance = DistanceFromPointToLineSegment(a, b, p);

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        insertIndex = i + 1;
                    }
                }

                hull.Insert(insertIndex, p);
            }

            return hull;
        }

        private static float DistanceFromPointToLineSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            float lengthSquared = (b - a).sqrMagnitude;
            if (lengthSquared == 0.0f) return (p - a).magnitude;

            float t = Mathf.Max(0, Mathf.Min(1, Vector2.Dot(p - a, b - a) / lengthSquared));
            Vector2 projection = a + t * (b - a);
            return (p - projection).magnitude;
        }

        // 多个polygon，已知第0个是外围的，后面N个是内部的，将这个复杂多边形转为简单多边形
        public static List<Vector2> MultiPolygonInsertPoints(List<List<Vector2>> pointsList)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var outSideList = new List<Vector2>(pointsList[0]);
            if (pointsList.Count <= 1) return outSideList;

            HashSet<Vector2> usedHash = new HashSet<Vector2>();
            int count = outSideList.Count;
            object lockObj = new object(); // lock object for thread safety

            Parallel.For(1, pointsList.Count, i =>
            {
                Vector2 rightmostPoint = FindMaxXPoint(pointsList[i]);
                Vector2 closestIntersection = Vector2.zero;
                int closestEdgeIndex = -1;
                float closestDistance = float.MaxValue;
                var dir = Vector2.right;

                for (int j = 0; j < count; j++)
                {
                    Vector2 pointA1 = outSideList[j];
                    Vector2 pointA2 = outSideList[(j + 1) % count];
                    Vector2 intersection = GetIntersectionPoint(rightmostPoint, rightmostPoint + dir * 99999, pointA1, pointA2);

                    if (intersection != Vector2.zero)
                    {
                        float distance = Vector2.Distance(rightmostPoint, intersection);
                        if (distance < closestDistance)
                        {
                            var tempClosestIntersection = intersection;
                            Vector2 closestVertex = Vector2.Distance(closestIntersection, pointA1) < Vector2.Distance(closestIntersection, pointA2) ? pointA1 : pointA2;
                            if (!usedHash.Contains(closestIntersection))
                            {
                                closestIntersection = tempClosestIntersection;
                                closestDistance = distance;
                                closestEdgeIndex = j;
                                dir = Vector2.right;
                            }
                            else
                            {
                                int idx = outSideList.IndexOf(closestVertex);
                                dir = (outSideList[idx] - rightmostPoint).normalized;
                                j--;
                            }
                        }
                    }
                }

                if (closestIntersection != Vector2.zero && closestEdgeIndex != -1)
                {
                    Vector2 pointA1 = outSideList[closestEdgeIndex];
                    Vector2 pointA2 = outSideList[(closestEdgeIndex + 1) % outSideList.Count];
                    Vector2 closestVertex = Vector2.Distance(closestIntersection, pointA1) < Vector2.Distance(closestIntersection, pointA2) ? pointA1 : pointA2;
                    int insertIndex = outSideList.IndexOf(closestVertex) + 1;
                    var newList = new List<Vector2>(pointsList[i]);
                    int insertIdx = newList.IndexOf(rightmostPoint);
                    List<Vector2> mergerList =
                        ReorderListStartingAtIndex(newList, insertIdx, rightmostPoint, closestVertex);

                    lock (lockObj)
                    {
                        outSideList.InsertRange(insertIndex, mergerList);
                        usedHash.Add(closestVertex);
                        count = outSideList.Count;
                    }
                }
            });
            sw.Stop();
            Debug.Log($"岛洞插入外围List 耗时： {sw.ElapsedMilliseconds}");
            return outSideList;
        }

        private static List<Vector2> ReorderListStartingAtIndex(List<Vector2> list, int index, params Vector2[] poss)
        {
            List<Vector2> temp = new List<Vector2>();

            // Add elements from index + 1 to end of the list
            for (int i = index; i < list.Count; i++)
            {
                temp.Add(list[i]);
            }

            // Add elements from start of the list to index
            for (int i = 0; i < index; i++)
            {
                temp.Add(list[i]);
            }

            foreach (var v2 in poss)
            {
                temp.Add(v2);
            }

            return temp;
        }

        private static Vector2 GetIntersectionPoint(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            float denominator = (p2.x - p1.x) * (p4.y - p3.y) - (p2.y - p1.y) * (p4.x - p3.x);

            if (denominator == 0)
            {
                // Lines are parallel
                return Vector2.zero;
            }

            float t = ((p3.x - p1.x) * (p4.y - p3.y) - (p3.y - p1.y) * (p4.x - p3.x)) / denominator;
            float u = ((p3.x - p1.x) * (p2.y - p1.y) - (p3.y - p1.y) * (p2.x - p1.x)) / denominator;

            if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
            {
                // Intersection point
                Vector2 intersectionPoint = new Vector2(
                    p1.x + t * (p2.x - p1.x),
                    p1.y + t * (p2.y - p1.y)
                );
                return intersectionPoint;
            }
            else
            {
                // No intersection within the segments
                return Vector2.zero;
            }
        }

        private static bool IsBetween(Vector2 a, Vector2 b, Vector2 c)
        {
            return (c.x >= Mathf.Min(a.x, b.x) && c.x <= Mathf.Max(a.x, b.x) && c.y >= Mathf.Min(a.y, b.y) &&
                    c.y <= Mathf.Max(a.y, b.y));
        }

        private static Vector2 FindMaxXPoint(List<Vector2> vertices)
        {
            Vector2 rightmostPoint = vertices[0];
            foreach (Vector2 point in vertices)
            {
                if (point.x > rightmostPoint.x ||
                    (Mathf.Approximately(point.x, rightmostPoint.x) && point.y > rightmostPoint.y))
                {
                    rightmostPoint = point;
                }
            }

            return rightmostPoint;
        }
    }

}