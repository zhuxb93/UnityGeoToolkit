using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace GeoToolkit.DrawRoad
{
    public class DrawRoadMeshGenerator
    {
        public int id;
        public float width;
        public float scaleFactor;
        public List<Vector3> points;

        public float roadWidth;
        public float kerbWidth;
        public float sidewalkWidth;
        public float subgradeHeight;

        public int laneCount;
        public float laneWidth;

        public List<Vector3> splinePoints;
        public List<Vector3> vertices1;
        public List<Vector3> vertices2;
        public List<Vector3> expandVertices1;
        public List<Vector3> expandVertices2;
        public List<Vector3> polyVertices1;
        public List<Vector3> polyVertices2;

        public Spline spline;

        public Material roadMat;
        public Material kerbMat;
        public Material sidewalkMat;
        public Material sideMat;

        public List<DrawIntersectionMeshGenerator> connectedIntersections = new List<DrawIntersectionMeshGenerator>();
        public List<EndpointType> connectedTypes = new List<EndpointType>();
        public List<Lane> leftLanes = new List<Lane>();
        public List<Lane> rightLanes = new List<Lane>();

        public DrawRoadMeshGenerator(int id, float width, float scaleFactor, List<Vector3> points, Material roadMat, DrawRoadDataConfig roadDataConfig)
        {
            this.id = id;
            this.width = width;
            this.scaleFactor = scaleFactor;
            this.points = points;

            this.kerbWidth = roadDataConfig.parameterConfig.kerbWidth;
            this.sidewalkWidth = roadDataConfig.parameterConfig.sidewalkWidth;
            this.subgradeHeight = roadDataConfig.parameterConfig.subgradeHeight;
            roadWidth = width - 2 * sidewalkWidth - 4 * kerbWidth;

            this.roadMat = roadMat;
            kerbMat = roadDataConfig.genericMatConfig.kerbMat;
            sidewalkMat = roadDataConfig.genericMatConfig.sidewalkMat;
            sideMat = roadDataConfig.genericMatConfig.concreteMat;

            SetSpline();
            CalculateRoadData();
        }

        public void SetSpline()
        {
            List<BezierKnot> bezierKnots = new List<BezierKnot>();
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 point = points[i];
                point.y = DrawRoadUtil.GetPointHeight(point);
                BezierKnot knot = new BezierKnot(point);
                bezierKnots.Add(knot);
            }
            spline = new Spline(bezierKnots);
            spline.SetTangentMode(TangentMode.AutoSmooth);
        }

        public void CalculateRoadData()
        {
            CalculateLaneCount();
            CalculateVerts();
        }

        public void CalculateLaneCount()
        {
            if (roadWidth < 8)
            {
                laneCount = 2;
            }
            else if (roadWidth >= 8 && roadWidth < 12)
            {
                laneCount = 4;
            }
            else if (roadWidth >= 12 && roadWidth < 18)
            {
                laneCount = 6;
            }
            else if (roadWidth >= 18 && roadWidth < 24)
            {
                laneCount = 8;
            }
            else
            {
                laneCount = 10;
            }
            laneWidth = roadWidth / laneCount;
        }

        public void CalculateVerts()
        {
            splinePoints = new List<Vector3>();
            vertices1 = new List<Vector3>();
            vertices2 = new List<Vector3>();
            expandVertices1 = new List<Vector3>();
            expandVertices2 = new List<Vector3>();
            polyVertices1 = new List<Vector3>();
            polyVertices2 = new List<Vector3>();

            leftLanes = new List<Lane>();
            rightLanes = new List<Lane>();
            for (int i = 0; i < laneCount / 2; i++)
            {
                Lane lane1 = new Lane(LaneType.Left, i, new List<Vector3>());
                leftLanes.Add(lane1);
                Lane lane2 = new Lane(LaneType.Right, i, new List<Vector3>());
                rightLanes.Add(lane2);
            }

            List<float> heights = new List<float>();

            int roadResolution = Mathf.CeilToInt(spline.GetLength() / 3);
            float step = 1f / (float)roadResolution;
            for (int i = 0; i <= roadResolution; i++)
            {
                float t = step * i;
                SplineUtility.Evaluate(spline, t, out var position, out var forward, out var upVector);
                float3 right = Vector3.Cross(forward, upVector).normalized;
                float sampleHeight = DrawRoadUtil.GetPointHeight(position);
                heights.Add(sampleHeight);

                position.y = sampleHeight + 0.1f;
                splinePoints.Add(position);

                Vector3 p1 = position + (right * roadWidth / 2 * scaleFactor);
                Vector3 p2 = position - (right * roadWidth / 2 * scaleFactor);
                p1.y = sampleHeight + 0.1f;
                p2.y = sampleHeight + 0.1f;
                vertices1.Add(p1);
                vertices2.Add(p2);

                Vector3 ep1 = position + (right * (width / 2 * scaleFactor + Mathf.Sqrt(2)));
                Vector3 ep2 = position + (-right * (width / 2 * scaleFactor + Mathf.Sqrt(2)));
                ep1.y = sampleHeight;
                ep2.y = sampleHeight;
                expandVertices1.Add(ep1);
                expandVertices2.Add(ep2);

                Vector3 pp1 = position + (right * (width / 2 * scaleFactor));
                Vector3 pp2 = position + (-right * (width / 2 * scaleFactor));
                pp1.y = sampleHeight;
                pp2.y = sampleHeight;
                polyVertices1.Add(pp1);
                polyVertices2.Add(pp2);

                for (int j = 0; j < laneCount / 2; j++)
                {
                    Vector3 laneVert1 = position + (right * (j * laneWidth + laneWidth / 2) * scaleFactor);
                    Vector3 laneVert2 = position - (right * (j * laneWidth + laneWidth / 2) * scaleFactor);
                    laneVert1.y = sampleHeight + 0.2f;
                    laneVert2.y = sampleHeight + 0.2f;
                    leftLanes[j].vertices.Insert(0, laneVert1);
                    rightLanes[j].vertices.Add(laneVert2);
                }
            }

            float max = heights.Max();
            float min = heights.Min();
            if (max - min < 2 && spline.Knots.Count() == 2)
            {
                splinePoints = new List<Vector3> { splinePoints.First(), splinePoints.Last() };
                vertices1 = new List<Vector3> { vertices1.First(), vertices1.Last() };
                vertices2 = new List<Vector3> { vertices2.First(), vertices2.Last() };
                expandVertices1 = new List<Vector3>() { expandVertices1.First(), expandVertices1.Last() };
                expandVertices2 = new List<Vector3>() { expandVertices2.First(), expandVertices2.Last() };
                polyVertices1 = new List<Vector3> { polyVertices1.First(), polyVertices1.Last() };
                polyVertices2 = new List<Vector3> { polyVertices2.First(), polyVertices2.Last() };
            }
        }

        public RoadMeshData CalculateRoadMeshData()
        {
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            float lenRec = 0;
            float lenCur = 0;
            int length = splinePoints.Count;
            float convertWidth = roadWidth * scaleFactor;
            for (int i = 0; i < length - 1; i++)
            {
                Vector3 p1 = vertices1[i];
                Vector3 p2 = vertices2[i];
                Vector3 p3 = vertices1[i + 1];
                Vector3 p4 = vertices2[i + 1];
                float average = (Vector3.Distance(p3, p1) + Vector3.Distance(p4, p2)) / 2;
                lenCur += average;

                int vertexOffset = 4 * i;

                int t1 = vertexOffset + 0;
                int t2 = vertexOffset + 2;
                int t3 = vertexOffset + 3;

                int t4 = vertexOffset + 3;
                int t5 = vertexOffset + 1;
                int t6 = vertexOffset + 0;

                Vector2 uv1 = new Vector2(0, lenRec / convertWidth);
                Vector2 uv2 = new Vector2(1, lenRec / convertWidth);
                Vector2 uv3 = new Vector2(0, lenCur / convertWidth);
                Vector2 uv4 = new Vector2(1, lenCur / convertWidth);

                verts.AddRange(new List<Vector3> { p1, p2, p3, p4 });
                tris.AddRange(new List<int> { t1, t2, t3, t4, t5, t6 });
                uvs.AddRange(new List<Vector2> { uv1, uv2, uv3, uv4 });

                lenRec = lenCur;
            }

            RoadMeshData roadMeshData = new RoadMeshData();
            roadMeshData.vertices = verts.ToArray();
            roadMeshData.triangles = tris.ToArray();
            roadMeshData.uvs = uvs.ToArray();
            roadMeshData.material = roadMat;
            return roadMeshData;
        }

        public (List<Vector3> verts1, List<Vector3> verts2, List<Vector3> verts3, List<Vector3> verts4, List<Vector3> verts5, List<Vector3> verts6) CalculateLeftSidewalkVertices()
        {
            List<Vector3> verts1 = new List<Vector3>();
            List<Vector3> verts2 = new List<Vector3>();
            List<Vector3> verts3 = new List<Vector3>();
            List<Vector3> verts4 = new List<Vector3>();
            List<Vector3> verts5 = new List<Vector3>();
            List<Vector3> verts6 = new List<Vector3>();
            for (int i = 0; i < vertices1.Count; i++)
            {
                Vector3 vert;
                Vector3 p1 = vertices1[i];
                Vector3 p2 = vertices2[i];
                if (i == 0)
                {
                    Vector3 p3 = vertices1[i + 1];
                    Vector3 dir1 = Vector3.Cross(p3 - p1, p2 - p1).normalized;
                    vert = p1 + dir1 * kerbWidth * scaleFactor;
                }
                else if (i == vertices1.Count - 1)
                {
                    Vector3 p4 = vertices1[i - 1];
                    Vector3 dir2 = Vector3.Cross(p2 - p1, p4 - p1).normalized;
                    vert = p1 + dir2 * kerbWidth * scaleFactor;
                }
                else
                {
                    Vector3 p3 = vertices1[i + 1];
                    Vector3 p4 = vertices1[i - 1];
                    Vector3 dir1 = Vector3.Cross(p3 - p1, p2 - p1).normalized;
                    Vector3 dir2 = Vector3.Cross(p2 - p1, p4 - p1).normalized;
                    Vector3 dir = ((dir1 + dir2) / 2).normalized;
                    float angle = Vector3.Angle(dir1, dir2);
                    if (angle >= 90)
                    {
                        angle = 180 - angle;
                        dir = -dir;
                    }
                    float rad = angle / 2 * Mathf.Deg2Rad;
                    vert = p1 + dir * kerbWidth * scaleFactor / Mathf.Cos(rad);
                }
                verts1.Add(vert);

                Vector3 vert1 = vert + (p1 - p2).normalized * kerbWidth * scaleFactor;
                verts2.Add(vert1);

                Vector3 vert2 = vert1 + (p1 - p2).normalized * sidewalkWidth * scaleFactor;
                verts3.Add(vert2);

                Vector3 vert3 = vert2 + (p1 - p2).normalized * kerbWidth * scaleFactor;
                verts4.Add(vert3);

                Vector3 vert4 = vert3 + (p1 - vert).normalized * kerbWidth * scaleFactor;
                verts5.Add(vert4);

                Vector3 vert5 = vert4 + (p1 - vert).normalized * subgradeHeight * scaleFactor;
                verts6.Add(vert5);
            }
            return (verts1, verts2, verts3, verts4, verts5, verts6);
        }

        public (List<Vector3> verts1, List<Vector3> verts2, List<Vector3> verts3, List<Vector3> verts4, List<Vector3> verts5, List<Vector3> verts6) CalculateRightSidewalkVertices()
        {
            List<Vector3> verts1 = new List<Vector3>();
            List<Vector3> verts2 = new List<Vector3>();
            List<Vector3> verts3 = new List<Vector3>();
            List<Vector3> verts4 = new List<Vector3>();
            List<Vector3> verts5 = new List<Vector3>();
            List<Vector3> verts6 = new List<Vector3>();
            for (int i = 0; i < vertices2.Count; i++)
            {
                Vector3 vert;
                Vector3 p1 = vertices2[i];
                Vector3 p2 = vertices1[i];
                if (i == 0)
                {
                    Vector3 p3 = vertices2[i + 1];
                    Vector3 dir1 = Vector3.Cross(p2 - p1, p3 - p1).normalized;
                    vert = p1 + dir1 * kerbWidth * scaleFactor;
                }
                else if (i == vertices2.Count - 1)
                {
                    Vector3 p4 = vertices2[i - 1];
                    Vector3 dir2 = Vector3.Cross(p4 - p1, p2 - p1).normalized;
                    vert = p1 + dir2 * kerbWidth * scaleFactor;
                }
                else
                {
                    Vector3 p3 = vertices2[i + 1];
                    Vector3 p4 = vertices2[i - 1];
                    Vector3 dir1 = Vector3.Cross(p2 - p1, p3 - p1).normalized;
                    Vector3 dir2 = Vector3.Cross(p4 - p1, p2 - p1).normalized;
                    Vector3 dir = ((dir1 + dir2) / 2).normalized;
                    float angle = Vector3.Angle(dir1, dir2);
                    if (angle >= 90)
                    {
                        angle = 180 - angle;
                        dir = -dir;
                    }
                    float rad = angle / 2 * Mathf.Deg2Rad;
                    vert = p1 + dir * kerbWidth * scaleFactor / Mathf.Cos(rad);
                    //vert = p1 + dir * kerbWidth * scaleFactor;
                }
                verts1.Add(vert);

                Vector3 vert1 = vert + (p1 - p2).normalized * kerbWidth * scaleFactor;
                verts2.Add(vert1);

                Vector3 vert2 = vert1 + (p1 - p2).normalized * sidewalkWidth * scaleFactor;
                verts3.Add(vert2);

                Vector3 vert3 = vert2 + (p1 - p2).normalized * kerbWidth * scaleFactor;
                verts4.Add(vert3);

                Vector3 vert4 = vert3 + (p1 - vert).normalized * kerbWidth * scaleFactor;
                verts5.Add(vert4);

                Vector3 vert5 = vert4 + (p1 - vert).normalized * subgradeHeight * scaleFactor;
                verts6.Add(vert5);
            }
            return (verts1, verts2, verts3, verts4, verts5, verts6);
        }

        public (List<Vector3> lVerts, List<int> lTris1, List<int> lTris2, List<int> lTris3, List<Vector2> lUVs) CalculateLeftSidewalkMeshData(List<Vector3> originVerts, List<Vector3> kerbVerts1, List<Vector3> kerbVerts2, List<Vector3> sidewalkVerts, List<Vector3> kerbVerts3, List<Vector3> kerbVerts4, List<Vector3> sideVerts)
        {
            List<Vector3> verts = new List<Vector3>();
            List<int> tris1 = new List<int>();
            List<int> tris2 = new List<int>();
            List<int> tris3 = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            float lenRec = 0;
            float lenCur = 0;
            for (int i = 0; i < originVerts.Count - 1; i++)
            {
                Vector3 v1 = originVerts[i];
                Vector3 v2 = kerbVerts1[i];
                Vector3 v3 = originVerts[i + 1];
                Vector3 v4 = kerbVerts1[i + 1];
                Vector3 v5 = kerbVerts2[i];
                Vector3 v6 = kerbVerts2[i + 1];

                Vector3 v7 = kerbVerts2[i];
                Vector3 v8 = sidewalkVerts[i];
                Vector3 v9 = kerbVerts2[i + 1];
                Vector3 v10 = sidewalkVerts[i + 1];

                Vector3 v11 = sidewalkVerts[i];
                Vector3 v12 = kerbVerts3[i];
                Vector3 v13 = sidewalkVerts[i + 1];
                Vector3 v14 = kerbVerts3[i + 1];
                Vector3 v15 = kerbVerts4[i];
                Vector3 v16 = kerbVerts4[i + 1];

                Vector3 v17 = kerbVerts4[i];
                Vector3 v18 = sideVerts[i];
                Vector3 v19 = kerbVerts4[i + 1];
                Vector3 v20 = sideVerts[i + 1];

                int offset = verts.Count;
                int t1 = offset + 0;
                int t2 = offset + 1;
                int t3 = offset + 3;

                int t4 = offset + 3;
                int t5 = offset + 2;
                int t6 = offset + 0;

                int t7 = offset + 1;
                int t8 = offset + 4;
                int t9 = offset + 5;

                int t10 = offset + 5;
                int t11 = offset + 3;
                int t12 = offset + 1;


                int t13 = offset + 6;
                int t14 = offset + 7;
                int t15 = offset + 9;

                int t16 = offset + 9;
                int t17 = offset + 8;
                int t18 = offset + 6;


                int t19 = offset + 10;
                int t20 = offset + 11;
                int t21 = offset + 13;

                int t22 = offset + 13;
                int t23 = offset + 12;
                int t24 = offset + 10;

                int t25 = offset + 11;
                int t26 = offset + 14;
                int t27 = offset + 15;

                int t28 = offset + 15;
                int t29 = offset + 13;
                int t30 = offset + 11;


                int t31 = offset + 16;
                int t32 = offset + 17;
                int t33 = offset + 19;

                int t34 = offset + 19;
                int t35 = offset + 18;
                int t36 = offset + 16;

                lenCur += Vector3.Distance(v2, v4) / scaleFactor;
                Vector2 uv1 = new Vector2(lenRec, 0);
                Vector2 uv2 = new Vector2(lenRec, 1);
                Vector2 uv3 = new Vector2(lenCur, 0);
                Vector2 uv4 = new Vector2(lenCur, 1);
                Vector2 uv5 = new Vector2(lenRec, 0);
                Vector2 uv6 = new Vector2(lenCur, 0);

                Vector2 uv7 = new Vector2(lenRec, 0);
                Vector2 uv8 = new Vector2(lenRec, sidewalkWidth);
                Vector2 uv9 = new Vector2(lenCur, 0);
                Vector2 uv10 = new Vector2(lenCur, sidewalkWidth);

                Vector2 uv11 = new Vector2(lenRec, 0);
                Vector2 uv12 = new Vector2(lenRec, 1);
                Vector2 uv13 = new Vector2(lenCur, 0);
                Vector2 uv14 = new Vector2(lenCur, 1);
                Vector2 uv15 = new Vector2(lenRec, 0);
                Vector2 uv16 = new Vector2(lenCur, 0);

                Vector2 uv17 = new Vector2(lenRec, 1);
                Vector2 uv18 = new Vector2(lenRec, 0);
                Vector2 uv19 = new Vector2(lenCur, 1);
                Vector2 uv20 = new Vector2(lenCur, 0);
                lenRec = lenCur;

                verts.AddRange(new List<Vector3> { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14, v15, v16, v17, v18, v19, v20 });
                tris1.AddRange(new List<int> { t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t19, t20, t21, t22, t23, t24, t25, t26, t27, t28, t29, t30 });
                tris2.AddRange(new List<int> { t13, t14, t15, t16, t17, t18 });
                tris3.AddRange(new List<int> { t31, t32, t33, t34, t35, t36 });
                uvs.AddRange(new List<Vector2> { uv1, uv2, uv3, uv4, uv5, uv6, uv7, uv8, uv9, uv10, uv11, uv12, uv13, uv14, uv15, uv16, uv17, uv18, uv19, uv20 });
            }

            return (verts, tris1, tris2, tris3, uvs);
        }

        public (List<Vector3> rVerts, List<int> rTris1, List<int> rTris2, List<int> rTris3, List<Vector2> rUVs) CalculateRightSidewalkMesh(List<Vector3> originVerts, List<Vector3> kerbVerts1, List<Vector3> kerbVerts2, List<Vector3> sidewalkVerts, List<Vector3> kerbVerts3, List<Vector3> kerbVerts4, List<Vector3> sideVerts)
        {
            List<Vector3> verts = new List<Vector3>();
            List<int> tris1 = new List<int>();
            List<int> tris2 = new List<int>();
            List<int> tris3 = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            float lenRec = 0;
            float lenCur = 0;
            for (int i = 0; i < originVerts.Count - 1; i++)
            {
                Vector3 v1 = originVerts[i];
                Vector3 v2 = kerbVerts1[i];
                Vector3 v3 = originVerts[i + 1];
                Vector3 v4 = kerbVerts1[i + 1];
                Vector3 v5 = kerbVerts2[i];
                Vector3 v6 = kerbVerts2[i + 1];

                Vector3 v7 = kerbVerts2[i];
                Vector3 v8 = sidewalkVerts[i];
                Vector3 v9 = kerbVerts2[i + 1];
                Vector3 v10 = sidewalkVerts[i + 1];

                Vector3 v11 = sidewalkVerts[i];
                Vector3 v12 = kerbVerts3[i];
                Vector3 v13 = sidewalkVerts[i + 1];
                Vector3 v14 = kerbVerts3[i + 1];
                Vector3 v15 = kerbVerts4[i];
                Vector3 v16 = kerbVerts4[i + 1];

                Vector3 v17 = kerbVerts4[i];
                Vector3 v18 = sideVerts[i];
                Vector3 v19 = kerbVerts4[i + 1];
                Vector3 v20 = sideVerts[i + 1];

                int offset = verts.Count;
                int t1 = offset + 0;
                int t2 = offset + 2;
                int t3 = offset + 3;

                int t4 = offset + 3;
                int t5 = offset + 1;
                int t6 = offset + 0;

                int t7 = offset + 1;
                int t8 = offset + 3;
                int t9 = offset + 5;

                int t10 = offset + 5;
                int t11 = offset + 4;
                int t12 = offset + 1;

                int t13 = offset + 6;
                int t14 = offset + 8;
                int t15 = offset + 9;

                int t16 = offset + 9;
                int t17 = offset + 7;
                int t18 = offset + 6;

                int t19 = offset + 10;
                int t20 = offset + 12;
                int t21 = offset + 13;

                int t22 = offset + 13;
                int t23 = offset + 11;
                int t24 = offset + 10;

                int t25 = offset + 11;
                int t26 = offset + 13;
                int t27 = offset + 15;

                int t28 = offset + 15;
                int t29 = offset + 14;
                int t30 = offset + 11;


                int t31 = offset + 16;
                int t32 = offset + 18;
                int t33 = offset + 19;

                int t34 = offset + 19;
                int t35 = offset + 17;
                int t36 = offset + 16;

                lenCur += Vector3.Distance(v2, v4) / scaleFactor;
                Vector2 uv1 = new Vector2(lenRec, 0);
                Vector2 uv2 = new Vector2(lenRec, 1);
                Vector2 uv3 = new Vector2(lenCur, 0);
                Vector2 uv4 = new Vector2(lenCur, 1);
                Vector2 uv5 = new Vector2(lenRec, 0);
                Vector2 uv6 = new Vector2(lenCur, 0);

                Vector2 uv7 = new Vector2(lenRec, 0);
                Vector2 uv8 = new Vector2(lenRec, sidewalkWidth);
                Vector2 uv9 = new Vector2(lenCur, 0);
                Vector2 uv10 = new Vector2(lenCur, sidewalkWidth);

                Vector2 uv11 = new Vector2(lenRec, 0);
                Vector2 uv12 = new Vector2(lenRec, 1);
                Vector2 uv13 = new Vector2(lenCur, 0);
                Vector2 uv14 = new Vector2(lenCur, 1);
                Vector2 uv15 = new Vector2(lenRec, 0);
                Vector2 uv16 = new Vector2(lenCur, 0);

                Vector2 uv17 = new Vector2(lenRec, 1);
                Vector2 uv18 = new Vector2(lenRec, 0);
                Vector2 uv19 = new Vector2(lenCur, 1);
                Vector2 uv20 = new Vector2(lenCur, 0);
                lenRec = lenCur;

                verts.AddRange(new List<Vector3> { v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14, v15, v16, v17, v18, v19, v20 });
                tris1.AddRange(new List<int> { t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t19, t20, t21, t22, t23, t24, t25, t26, t27, t28, t29, t30 });
                tris2.AddRange(new List<int> { t13, t14, t15, t16, t17, t18 });
                tris3.AddRange(new List<int> { t31, t32, t33, t34, t35, t36 });
                uvs.AddRange(new List<Vector2> { uv1, uv2, uv3, uv4, uv5, uv6, uv7, uv8, uv9, uv10, uv11, uv12, uv13, uv14, uv15, uv16, uv17, uv18, uv19, uv20 });
            }

            return (verts, tris1, tris2, tris3, uvs);
        }

        public SidewalkMeshData CalculateSidewalkMeshData()
        {
            int length = vertices1.Count;
            if (length == 1)
                return new SidewalkMeshData();

            (var vertsL1, var vertsL2, var vertsL3, var vertsL4, var vertsL5, var vertsL6) = CalculateLeftSidewalkVertices();
            (List<Vector3> lVerts, List<int> lTris1, List<int> lTris2, List<int> lTris3, List<Vector2> lUVs) = CalculateLeftSidewalkMeshData(vertices1, vertsL1, vertsL2, vertsL3, vertsL4, vertsL5, vertsL6);
            (var vertsR1, var vertsR2, var vertsR3, var vertsR4, var vertsR5, var vertsR6) = CalculateRightSidewalkVertices();
            (List<Vector3> rVerts, List<int> rTris1, List<int> rTris2, List<int> rTris3, List<Vector2> rUVs) = CalculateRightSidewalkMesh(vertices2, vertsR1, vertsR2, vertsR3, vertsR4, vertsR5, vertsR6);
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles1 = new List<int>();
            List<int> triangles2 = new List<int>();
            List<int> triangles3 = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            vertices.AddRange(lVerts);
            uvs.AddRange(lUVs);
            triangles1.AddRange(lTris1);
            triangles2.AddRange(lTris2);
            triangles3.AddRange(lTris3);
            int offset = vertices.Count;
            List<int> t1 = rTris1.Select(i => i += offset).ToList();
            List<int> t2 = rTris2.Select(i => i += offset).ToList();
            List<int> t3 = rTris3.Select(i => i += offset).ToList();
            triangles1.AddRange(t1);
            triangles2.AddRange(t2);
            triangles3.AddRange(t3);
            vertices.AddRange(rVerts);
            uvs.AddRange(rUVs);
            SidewalkMeshData sidewalkMeshData = new SidewalkMeshData();
            sidewalkMeshData.vertices = vertices;
            sidewalkMeshData.kerbTriangles = triangles1;
            sidewalkMeshData.sidewalkTriangles = triangles2;
            sidewalkMeshData.concreteTriangles = triangles3;
            sidewalkMeshData.uvs = uvs;
            sidewalkMeshData.kerbMaterial = kerbMat;
            sidewalkMeshData.sidewalkMaterial = sidewalkMat;
            sidewalkMeshData.concreteMaterial = sideMat;
            return sidewalkMeshData;
        }

        public ExpandMeshData CalculateExpandMeshData()
        {
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            for (int i = 0; i < expandVertices1.Count - 1; i++)
            {
                Vector3 p1 = expandVertices1[i];
                Vector3 p2 = expandVertices2[i];
                Vector3 p3 = expandVertices1[i + 1];
                Vector3 p4 = expandVertices2[i + 1];

                int t1 = 4 * i + 0;
                int t2 = 4 * i + 2;
                int t3 = 4 * i + 3;

                int t4 = 4 * i + 3;
                int t5 = 4 * i + 1;
                int t6 = 4 * i + 0;

                verts.AddRange(new List<Vector3> { p1, p2, p3, p4 });
                tris.AddRange(new List<int> { t1, t2, t3, t4, t5, t6 });
            }
            ExpandMeshData expandMeshData = new ExpandMeshData();
            expandMeshData.vertices = verts.ToArray();
            expandMeshData.triangles = tris.ToArray();
            return expandMeshData;
        }

        public List<FacilityMarkPoint> CalculateFacilityPoints()
        {
            List<FacilityMarkPoint> facilityMarkPoints = new List<FacilityMarkPoint>();
            List<BezierKnot> bezierKnots = new List<BezierKnot>();
            for (int i = 0; i < splinePoints.Count; i++)
            {
                Vector3 point = splinePoints[i];
                BezierKnot knot = new BezierKnot(point);
                bezierKnots.Add(knot);
            }
            Spline s = new Spline(bezierKnots);
            s.SetTangentMode(TangentMode.AutoSmooth);

            facilityMarkPoints.AddRange(CalculateTreeLampPoints(s));
            //if (roadWidth > 12)
            //{
            //    facilityMarkPoints.AddRange(CalculateRailingPoints(s));
            //}
            return facilityMarkPoints;
        }

        public List<FacilityMarkPoint> CalculateTreeLampPoints(Spline spline)
        {
            List<FacilityMarkPoint> facilityMarkPoints = new List<FacilityMarkPoint>();
            float xOffset = (roadWidth / 2 + kerbWidth + sidewalkWidth / 3) * scaleFactor;
            float yOffset = kerbWidth * scaleFactor;
            int resolution = Mathf.CeilToInt(spline.GetLength() / (20 * scaleFactor));
            float step = 1.0f / resolution;
            for (int i = 0; i <= resolution; i++)
            {
                if (i == 0 || i == resolution)
                    continue;
                float t = step * i;
                SplineUtility.Evaluate(spline, t, out var position, out var forward, out var up);
                float3 right = Vector3.Cross(forward, up).normalized;
                FacilityType type;
                if (roadWidth < 8)
                {
                    type = i % 2 == 1 ? FacilityType.Lamp_2 : FacilityType.Tree_2;
                }
                else if (roadWidth >= 8 && roadWidth < 12)
                {
                    type = i % 2 == 1 ? FacilityType.Lamp_4 : FacilityType.Tree_4;
                }
                else if (roadWidth >= 12 && roadWidth < 18)
                {
                    type = i % 2 == 1 ? FacilityType.Lamp_6 : FacilityType.Tree_6;
                }
                else if (roadWidth >= 18 && roadWidth < 24)
                {
                    type = i % 2 == 1 ? FacilityType.Lamp_8 : FacilityType.Tree_8;
                }
                else
                {
                    type = i % 2 == 1 ? FacilityType.Lamp_10 : FacilityType.Tree_10;
                }
                Vector3 pos1 = position + (right * xOffset);
                FacilityMarkPoint p1 = new FacilityMarkPoint();
                p1.facilityType = type;
                p1.position = pos1.ToSerializableVector3();
                Vector3 dir1 = -right;
                Vector3 scale1 = Vector3.one * scaleFactor;
                if (i % 2 != 1)
                {
                    float randomAngle = UnityEngine.Random.Range(-90, 90);
                    dir1 = Quaternion.AngleAxis(randomAngle, up) * -right;
                    scale1 *= UnityEngine.Random.Range(0.9f, 1.1f);
                }
                p1.euler = (Quaternion.LookRotation(dir1).eulerAngles).ToSerializableVector3();
                p1.scale = scale1.ToSerializableVector3();

                Vector3 pos2 = position + (-right * xOffset);
                FacilityMarkPoint p2 = new FacilityMarkPoint();
                p2.facilityType = type;
                p2.position = pos2.ToSerializableVector3();
                Vector3 dir2 = right;
                Vector3 scale2 = Vector3.one * scaleFactor;
                if (i % 2 != 1)
                {
                    float randomAngle = UnityEngine.Random.Range(-90, 90);
                    dir2 = Quaternion.AngleAxis(randomAngle, up) * right;
                    scale2 *= UnityEngine.Random.Range(0.9f, 1.1f);
                }
                p2.euler = (Quaternion.LookRotation(dir2).eulerAngles).ToSerializableVector3();
                p2.scale = scale2.ToSerializableVector3();

                facilityMarkPoints.Add(p1);
                facilityMarkPoints.Add(p2);

                if (i % 2 != 1)
                {
                    FacilityMarkPoint p3 = new FacilityMarkPoint();
                    p3.facilityType = FacilityType.Altar_01;
                    p3.scale = (Vector3.one * scaleFactor).ToSerializableVector3();
                    Quaternion rot3 = Quaternion.LookRotation(-right, up);
                    p3.euler = rot3.eulerAngles.ToSerializableVector3();
                    p3.position = (pos1 + rot3 * Vector3.up * yOffset).ToSerializableVector3();

                    FacilityMarkPoint p4 = new FacilityMarkPoint();
                    p4.facilityType = FacilityType.Altar_01;
                    p4.scale = (Vector3.one * scaleFactor).ToSerializableVector3();
                    Quaternion rot4 = Quaternion.LookRotation(right, up);
                    p4.euler = rot4.eulerAngles.ToSerializableVector3();
                    p4.position = (pos2 + rot4 * Vector3.up * yOffset).ToSerializableVector3();

                    facilityMarkPoints.Add(p3);
                    facilityMarkPoints.Add(p4);
                }
            }
            return facilityMarkPoints;
        }

        public List<FacilityMarkPoint> CalculateRailingPoints(Spline spline)
        {
            List<FacilityMarkPoint> facilityMarkPoints = new List<FacilityMarkPoint>();
            List<FacilityMarkPoint> railings = new List<FacilityMarkPoint>();
            int resolution = Mathf.CeilToInt(spline.GetLength() / (2.5f * scaleFactor));
            float step = 1.0f / resolution;
            for (int i = 0; i <= resolution; i++)
            {
                float t = step * i;
                SplineUtility.Evaluate(spline, t, out var position, out var forward, out var up);
                float3 right = Vector3.Cross(forward, up).normalized;
                FacilityMarkPoint f = new FacilityMarkPoint();
                f.facilityType = FacilityType.Railing01;
                f.position = ((Vector3)position).ToSerializableVector3();
                f.euler = Quaternion.LookRotation(right, up).eulerAngles.ToSerializableVector3();
                f.scale = (Vector3.one * scaleFactor).ToSerializableVector3();
                railings.Add(f);
                facilityMarkPoints.Add(f);
            }

            for (int i = 0; i < railings.Count - 1; i++)
            {
                FacilityMarkPoint r1 = railings[i];
                FacilityMarkPoint r2 = railings[i + 1];
                Vector3 localUp1 = Quaternion.Euler(r1.euler.ToVector3()) * Vector3.up;
                Vector3 p1_1 = r1.position.ToVector3() + localUp1 * 1 * scaleFactor;
                Vector3 p1_2 = r1.position.ToVector3() + localUp1 * 0.35f * scaleFactor;
                Vector3 localUp2 = Quaternion.Euler(r2.euler.ToVector3()) * Vector3.up;
                Vector3 p2_1 = r2.position.ToVector3() + localUp2 * 1 * scaleFactor;
                Vector3 p2_2 = r2.position.ToVector3() + localUp2 * 0.35f * scaleFactor;
                float dis1 = Vector3.Distance(p1_1, p2_1);
                float factor1 = dis1 / (2.5f * scaleFactor);
                float dis2 = Vector3.Distance(p1_2, p2_2);
                float factor2 = dis2 / (2.5f * scaleFactor);
                Vector3 pos1 = (p1_1 + p2_1) / 2;
                Vector3 pos2 = (p1_2 + p2_2) / 2;
                Vector3 dir = (p2_1 - p1_1).normalized;
                Vector3 upward = Vector3.Cross(dir, Vector3.up).normalized;
                Vector3 forward = Vector3.Cross(upward, dir).normalized;
                Vector3 euler = Quaternion.LookRotation(forward, upward).eulerAngles;

                FacilityMarkPoint f1 = new FacilityMarkPoint();
                f1.facilityType = FacilityType.Railing02;
                f1.position = pos1.ToSerializableVector3();
                f1.euler = euler.ToSerializableVector3();
                Vector3 scale1 = Vector3.one * scaleFactor;
                scale1.x = scale1.x * factor1;
                f1.scale = scale1.ToSerializableVector3();

                FacilityMarkPoint f2 = new FacilityMarkPoint();
                f2.facilityType = FacilityType.Railing02;
                f2.position = pos2.ToSerializableVector3();
                f2.euler = euler.ToSerializableVector3();
                Vector3 scale2 = Vector3.one * scaleFactor;
                scale2.x = scale2.x * factor2;
                f2.scale = scale2.ToSerializableVector3();

                Vector3 upward1 = Vector3.Cross(dir, Vector3.up).normalized;
                Vector3 forward1 = Vector3.Cross(upward1, dir).normalized;
                Vector3 euler1 = Quaternion.LookRotation(forward1, upward1).eulerAngles;
                Vector3 p1 = pos1 - forward1 * (0.65f / 2) * scaleFactor;
                Vector3 p2 = (pos1 + p1_1) / 2 - forward1 * (0.65f / 2) * scaleFactor;
                Vector3 p3 = (pos1 + p2_1) / 2 - forward1 * (0.65f / 2) * scaleFactor;

                FacilityMarkPoint f3 = new FacilityMarkPoint();
                f3.facilityType = FacilityType.Railing03;
                f3.position = p1.ToSerializableVector3();
                f3.euler = euler1.ToSerializableVector3();
                f3.scale = (Vector3.one * scaleFactor).ToSerializableVector3();

                FacilityMarkPoint f4 = new FacilityMarkPoint();
                f4.facilityType = FacilityType.Railing03;
                f4.position = p2.ToSerializableVector3();
                f4.euler = euler1.ToSerializableVector3();
                f4.scale = (Vector3.one * scaleFactor).ToSerializableVector3();

                FacilityMarkPoint f5 = new FacilityMarkPoint();
                f5.facilityType = FacilityType.Railing03;
                f5.position = p3.ToSerializableVector3();
                f5.euler = euler1.ToSerializableVector3();
                f5.scale = (Vector3.one * scaleFactor).ToSerializableVector3();

                facilityMarkPoints.Add(f1);
                facilityMarkPoints.Add(f2);
                facilityMarkPoints.Add(f3);
                facilityMarkPoints.Add(f4);
                facilityMarkPoints.Add(f5);
            }

            return facilityMarkPoints;
        }

        public List<SerializableVector3> GetPolyVerts()
        {
            List<Vector3> poly = new List<Vector3>();
            poly.AddRange(polyVertices1);
            List<Vector3> reversed = new List<Vector3>(polyVertices2);
            reversed.Reverse();
            poly.AddRange(reversed);
            List<SerializableVector3> res = poly.Select(a => a.ToSerializableVector3()).ToList();
            return res;
        }
    }
}
