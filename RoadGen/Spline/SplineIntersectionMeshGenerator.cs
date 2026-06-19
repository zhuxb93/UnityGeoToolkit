using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GeoToolkit;
using UnityEngine;
using UnityEngine.Splines;

namespace GeoDCBuildTools.RoadTool
{
    public class SplineIntersectionMeshGenerator
    {
        public int id;
        public Vector3 originPoint;
        public Vector3 point;
        public int forkNum;
        public Dictionary<int, EndpointType> roadIds;
        public List<SplineRoadMeshGenerator> connectedRoads;
        public List<EndpointType> connectedTypes;
        public float scaleFactor;
        public RoadDataConfig config;

        public float kerbWidth;
        public float sidewalkWidth;
        public float subgradeHeight;

        public List<JunctionInfo> junctionInfos;
        public List<JunctionPoint> junctionPoints;
        public List<List<JunctionPoint>> sidewalkPointsList;

        public Material intersectionMat;
        public Material crossingMat;
        public Material kerbMat;
        public Material sidewalkMat;
        public Material sideMat;

        public SplineIntersectionMeshGenerator(int id, Vector3 point, int forkNum, Dictionary<int, EndpointType> roadIds, Dictionary<SplineRoadMeshGenerator, EndpointType> connectedRoadDict, float scaleFactor, RoadDataConfig config)
        {
            this.id = id;
            this.originPoint = point;
            this.point = point;
            this.point.y += 0.1f;
            this.forkNum = forkNum;
            this.roadIds = roadIds;
            this.connectedRoads = connectedRoadDict.Keys.ToList();
            this.connectedTypes = connectedRoadDict.Values.ToList();
            this.scaleFactor = scaleFactor;
            this.config = config;

            kerbWidth = config.roadParameterConfig.kerbWidth;
            sidewalkWidth = config.roadParameterConfig.sidewalkWidth;
            subgradeHeight = config.roadParameterConfig.subgradeHeight;

            intersectionMat = config.intersectionMatConfig.intersectionMat;
            crossingMat = config.intersectionMatConfig.crossingMat;
            kerbMat = config.genericMatConfig.kerbMat;
            sidewalkMat = config.genericMatConfig.sidewalkMat;
            sideMat = config.genericMatConfig.concreteMat;

            CalculateIntersectionData();

            for (int i = 0; i < connectedRoads.Count; i++)
            {
                connectedRoads[i].connectedIntersections.Add(this);
                connectedRoads[i].connectedTypes.Add(connectedTypes[i]);
            }
        }

        public void CalculateIntersectionData()
        {
            CalculateJunctionInfo();
            BezierSmooth();
        }

        public void CalculateJunctionInfo()
        {
            // 计算junction
            junctionInfos = new List<JunctionInfo>();
            for (int i = 0; i < connectedRoads.Count; i++)
            {
                SplineRoadMeshGenerator road = connectedRoads[i];
                EndpointType endpointType = connectedTypes[i];
                if (endpointType == EndpointType.Start)
                {
                    Vector3 dir1 = (road.vertices1[1] - road.vertices1[0]).normalized;
                    Vector3 dir2 = (road.vertices2[0] - road.vertices1[0]).normalized;
                    Vector3 norm1 = Vector3.Cross(dir1, dir2).normalized;
                    JunctionOriginPoint point1 = new JunctionOriginPoint(road.vertices1[0], norm1, -dir2);
                    Vector3 dir3 = (road.vertices2[1] - road.vertices2[0]).normalized;
                    Vector3 dir4 = (road.vertices1[0] - road.vertices2[0]).normalized;
                    Vector3 norm2 = Vector3.Cross(dir4, dir3).normalized;
                    JunctionOriginPoint point2 = new JunctionOriginPoint(road.vertices2[0], norm2, -dir4);
                    Vector3 centerPoint = (road.vertices1[0] + road.vertices2[0]) / 2;
                    JunctionInfo junctionInfo = new JunctionInfo(centerPoint, point1, point2);
                    junctionInfos.Add(junctionInfo);
                }
                else if (endpointType == EndpointType.End)
                {
                    int vertCount = road.vertices1.Count;
                    Vector3 dir1 = (road.vertices1[vertCount - 2] - road.vertices1[vertCount - 1]).normalized;
                    Vector3 dir2 = (road.vertices2[vertCount - 1] - road.vertices1[vertCount - 1]).normalized;
                    Vector3 norm1 = Vector3.Cross(dir2, dir1).normalized;
                    JunctionOriginPoint point1 = new JunctionOriginPoint(road.vertices1[vertCount - 1], norm1, -dir2);
                    Vector3 dir3 = (road.vertices2[vertCount - 2] - road.vertices2[vertCount - 1]).normalized;
                    Vector3 dir4 = (road.vertices1[vertCount - 1] - road.vertices2[vertCount - 1]).normalized;
                    Vector3 norm2 = Vector3.Cross(dir3, dir4).normalized;
                    JunctionOriginPoint point2 = new JunctionOriginPoint(road.vertices2[vertCount - 1], norm2, -dir4);
                    Vector3 centerPoint = (road.vertices1[vertCount - 1] + road.vertices2[vertCount - 1]) / 2;
                    JunctionInfo junctionInfo = new JunctionInfo(centerPoint, point2, point1);
                    junctionInfos.Add(junctionInfo);
                }
            }

            // 排序
            junctionInfos.Sort((a, b) =>
            {
                Vector3 dir1 = (a.centerPoint - point).ToXZ().normalized;
                Vector3 dir2 = (b.centerPoint - point).ToXZ().normalized;
                float angle1 = Vector3.SignedAngle(Vector3.forward, dir1, Vector3.up);
                if (angle1 < 0)
                    angle1 += 360;
                float angle2 = Vector3.SignedAngle(Vector3.forward, dir2, Vector3.up);
                if (angle2 < 0)
                    angle2 += 360;
                if (angle1 > angle2)
                    return 1;
                else if (angle1 < angle2)
                    return -1;
                else
                    return 0;
            });

            // 检查junction是否有相交，取交点为公共点
            for (int i = 0; i < junctionInfos.Count; i++)
            {
                int nextId = i < junctionInfos.Count - 1 ? i + 1 : 0;
                Vector3 p1 = junctionInfos[i].p1.point.ToXZ();
                Vector3 p2 = junctionInfos[i].p2.point.ToXZ();
                Vector3 p3 = junctionInfos[nextId].p1.point.ToXZ();
                Vector3 p4 = junctionInfos[nextId].p2.point.ToXZ();
                bool isIntersect = Conversions.TryGetIntersectPoint(p1, p2, p3, p4, out var point);
                if (isIntersect)
                {
                    float h = (junctionInfos[i].p1.point.y + junctionInfos[nextId].p1.point.y) / 2;
                    point.y = h;
                    junctionInfos[i].p2.point = point;
                    junctionInfos[nextId].p1.point = point;
                }
            }

            // 计算斑马线的方向
            for (int i = 0; i < junctionInfos.Count; i++)
            {
                Vector3 dir1 = (point - junctionInfos[i].centerPoint).normalized;
                Vector3 dir2 = (junctionInfos[i].p1.point - junctionInfos[i].p2.point).normalized;
                Vector3 normal = Vector3.Cross(dir1, dir2).normalized;
                Vector3 expand = Vector3.Cross(dir2, normal).normalized;
                junctionInfos[i].p1.crossNormal = normal;
                junctionInfos[i].p1.crossExpand = expand;
                junctionInfos[i].p2.crossNormal = normal;
                junctionInfos[i].p2.crossExpand = expand;
            }
        }

        public void BezierSmooth()
        {
            junctionPoints = new List<JunctionPoint>();
            sidewalkPointsList = new List<List<JunctionPoint>>();
            for (int i = 0; i < junctionInfos.Count; i++)
            {
                int nextId = i < junctionInfos.Count - 1 ? i + 1 : 0;
                Vector3 p1 = junctionInfos[i].p2.point;
                Vector3 p2 = junctionInfos[nextId].p1.point;
                if (p1 == p2)
                {
                    junctionPoints.Add(junctionInfos[i].p1);
                    continue;
                }
                List<JunctionPoint> sidewalkPoints = new List<JunctionPoint>();
                int step = Mathf.FloorToInt(Vector3.Distance(p1, p2) / (1 * scaleFactor));
                if (step == 0)
                {
                    junctionPoints.Add(junctionInfos[i].p1);
                    junctionPoints.Add(junctionInfos[i].p2);

                    sidewalkPoints.Add(junctionInfos[i].p2);
                    sidewalkPoints.Add(junctionInfos[nextId].p1);
                    continue;
                }

                Vector3 mid = Vector3.Lerp(p1, p2, 0.5f);
                //float diss = Vector3.Distance(p1, p2);
                //diss = Mathf.Clamp(diss, 10, 40);
                //float maxRad = Mathf.Lerp(0.2f, 0.4f, (40 - diss) / 30);
                //Debug.LogError("diss:" + Vector3.Distance(p1, p2) + "  maxRad:" + maxRad);
                // 计算路口相邻两个扩展向量之间的夹角，根据夹角大小动态调整贝塞尔曲线的弧度
                float angle = Vector3.Angle(junctionInfos[i].p2.expand, junctionInfos[nextId].p1.expand);
                //float rate = Mathf.Lerp(0, maxRad, angle / 180);
                float rate = Mathf.Pow(angle / 180, 2);
                Vector3 c = Vector3.Lerp(mid, point, rate);
                BezierCurve curve = new BezierCurve(p1, c, p2);

                junctionPoints.Add(junctionInfos[i].p1);
                junctionPoints.Add(junctionInfos[i].p2);

                sidewalkPoints.Add(junctionInfos[i].p2);

                // 调节两个向量之间的夹角小于180度，避免进行球面插值计算时结果不符合预期
                float signAngle = Vector3.SignedAngle(junctionInfos[i].p2.expand, junctionInfos[nextId].p1.expand, Vector3.Cross(p2 - p1, mid - point));
                Vector3 startExpand = junctionInfos[i].p2.expand;
                if (angle > 90 && angle - signAngle > Mathf.Epsilon)
                {
                    Quaternion q = Quaternion.AngleAxis(180 - angle + 1, Vector3.Cross(p2 - p1, mid - point));
                    startExpand = q * junctionInfos[i].p2.expand;
                }

                for (float t = 1; t < step; t++)
                {
                    Vector3 pos = CurveUtility.EvaluatePosition(curve, t / step);
                    Vector3 normal = Vector3.Slerp(junctionInfos[i].p2.normal, junctionInfos[nextId].p1.normal, t / step);
                    Vector3 expand = Vector3.Slerp(startExpand, junctionInfos[nextId].p1.expand, t / step);
                    JunctionBezierPoint jbp = new JunctionBezierPoint(pos, normal, expand);
                    junctionPoints.Add(jbp);
                    sidewalkPoints.Add(jbp);
                }
                sidewalkPoints.Add(junctionInfos[nextId].p1);
                sidewalkPointsList.Add(sidewalkPoints);
            }
        }

        public IntersectionMeshData CalculateIntersectionMeshData()
        {
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<int> crossTris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            verts.Add(point);
            uvs.Add(new Vector2(point.z, point.x));
            for (int i = 0; i < junctionPoints.Count; i++)
            {
                verts.Add(junctionPoints[i].point);
                int t1 = 0;
                int t2 = i + 1;
                int t3 = i + 2;
                if (i == junctionPoints.Count - 1)
                {
                    t3 = 1;
                }
                tris.AddRange(new List<int> { t1, t2, t3 });
                uvs.Add(new Vector2(junctionPoints[i].point.z, junctionPoints[i].point.x));
            }

            for (int i = 0; i < junctionInfos.Count; i++)
            {
                Vector3 normal = junctionInfos[i].p1.crossNormal;
                Vector3 expandDir = junctionInfos[i].p1.crossExpand;
                Vector3 p1 = junctionInfos[i].p1.point + normal * 0.01f;
                Vector3 p2 = junctionInfos[i].p2.point + normal * 0.01f;
                Vector3 p3 = p1 + expandDir * 1.5f * scaleFactor;
                Vector3 p4 = p2 + expandDir * 1.5f * scaleFactor;
                float ratio = Vector3.Distance(p1, p2) / (9 * scaleFactor);

                int offset = verts.Count;
                int t1 = offset + 0;
                int t2 = offset + 1;
                int t3 = offset + 3;
                int t4 = offset + 3;
                int t5 = offset + 2;
                int t6 = offset + 0;

                Vector2 uv1 = new Vector2(0, 0);
                Vector2 uv2 = new Vector2(ratio, 0);
                Vector2 uv3 = new Vector2(0, 1);
                Vector2 uv4 = new Vector2(ratio, 1);

                verts.AddRange(new List<Vector3> { p1, p2, p3, p4 });
                crossTris.AddRange(new List<int> { t1, t2, t3, t4, t5, t6 });
                uvs.AddRange(new List<Vector2> { uv1, uv2, uv3, uv4 });
            }

            IntersectionMeshData intersectionMeshData = new IntersectionMeshData();
            intersectionMeshData.vertices = verts.ToArray();
            intersectionMeshData.triangles = tris.ToArray();
            intersectionMeshData.crossTriangles = crossTris.ToArray();
            intersectionMeshData.uvs = uvs.ToArray();
            intersectionMeshData.material = intersectionMat;
            intersectionMeshData.crossMaterial = crossingMat;
            return intersectionMeshData;
        }

        public SidewalkMeshData CalculateSidewalkMeshData()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles1 = new List<int>();
            List<int> triangles2 = new List<int>();
            List<int> triangles3 = new List<int>();
            for (int i = 0; i < sidewalkPointsList.Count; i++)
            {
                List<JunctionPoint> sidewalkPoints = sidewalkPointsList[i];
                List<Vector3> originVerts = new List<Vector3>();
                List<Vector3> kerbVerts1 = new List<Vector3>();
                List<Vector3> kerbVerts2 = new List<Vector3>();
                List<Vector3> sidewalkVerts = new List<Vector3>();
                List<Vector3> kerbVerts3 = new List<Vector3>();
                List<Vector3> kerbVerts4 = new List<Vector3>();
                List<Vector3> sideVerts = new List<Vector3>();
                for (int j = 0; j < sidewalkPoints.Count; j++)
                {
                    Vector3 vert1 = sidewalkPoints[j].point;
                    originVerts.Add(vert1);
                    Vector3 vert2 = vert1 + sidewalkPoints[j].normal * kerbWidth * scaleFactor;
                    kerbVerts1.Add(vert2);
                    Vector3 vert3 = vert2 + sidewalkPoints[j].expand * kerbWidth * scaleFactor;
                    kerbVerts2.Add(vert3);
                    Vector3 vert4 = vert3 + sidewalkPoints[j].expand * sidewalkWidth * scaleFactor;
                    sidewalkVerts.Add(vert4);
                    Vector3 vert5 = vert4 + sidewalkPoints[j].expand * kerbWidth * scaleFactor;
                    kerbVerts3.Add(vert5);
                    Vector3 polyVert = vert5 - new Vector3(0, 0.1f, 0);
                    Vector3 vert6 = vert5 - sidewalkPoints[j].normal * kerbWidth * scaleFactor;
                    kerbVerts4.Add(vert6);
                    Vector3 vert7 = vert6 - sidewalkPoints[j].normal * subgradeHeight * scaleFactor;
                    sideVerts.Add(vert7);
                }

                List<Vector3> verts = new List<Vector3>();
                List<int> tris1 = new List<int>();
                List<int> tris2 = new List<int>();
                List<int> tris3 = new List<int>();
                List<Vector2> uv = new List<Vector2>();
                float lenRec = 0;
                float lenCur = 0;
                for (int j = 0; j < originVerts.Count - 1; j++)
                {
                    Vector3 v1 = originVerts[j];
                    Vector3 v2 = kerbVerts1[j];
                    Vector3 v3 = originVerts[j + 1];
                    Vector3 v4 = kerbVerts1[j + 1];
                    Vector3 v5 = kerbVerts2[j];
                    Vector3 v6 = kerbVerts2[j + 1];

                    Vector3 v7 = kerbVerts2[j];
                    Vector3 v8 = sidewalkVerts[j];
                    Vector3 v9 = kerbVerts2[j + 1];
                    Vector3 v10 = sidewalkVerts[j + 1];

                    Vector3 v11 = sidewalkVerts[j];
                    Vector3 v12 = kerbVerts3[j];
                    Vector3 v13 = sidewalkVerts[j + 1];
                    Vector3 v14 = kerbVerts3[j + 1];
                    Vector3 v15 = kerbVerts4[j];
                    Vector3 v16 = kerbVerts4[j + 1];

                    Vector3 v17 = kerbVerts4[j];
                    Vector3 v18 = sideVerts[j];
                    Vector3 v19 = kerbVerts4[j + 1];
                    Vector3 v20 = sideVerts[j + 1];

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
                    uv.AddRange(new List<Vector2> { uv1, uv2, uv3, uv4, uv5, uv6, uv7, uv8, uv9, uv10, uv11, uv12, uv13, uv14, uv15, uv16, uv17, uv18, uv19, uv20 });
                }

                int offset1 = vertices.Count;
                List<int> t1s = tris1.Select(i => i += offset1).ToList();
                List<int> t2s = tris2.Select(i => i += offset1).ToList();
                List<int> t3s = tris3.Select(i => i += offset1).ToList();
                triangles1.AddRange(t1s);
                triangles2.AddRange(t2s);
                triangles3.AddRange(t3s);
                vertices.AddRange(verts);
                uvs.AddRange(uv);
            }

            SidewalkMeshData sidewalkMeshData = new SidewalkMeshData();
            sidewalkMeshData.vertices = vertices;
            sidewalkMeshData.uvs = uvs;
            sidewalkMeshData.kerbTriangles = triangles1;
            sidewalkMeshData.sidewalkTriangles = triangles2;
            sidewalkMeshData.concreteTriangles = triangles3;
            sidewalkMeshData.kerbMaterial = kerbMat;
            sidewalkMeshData.sidewalkMaterial = sidewalkMat;
            sidewalkMeshData.concreteMaterial = sideMat;
            return sidewalkMeshData;
        }

        public ExpandMeshData CalculateExpandMeshData()
        {
            List<Vector3> exVerts = new List<Vector3>();
            List<int> exTris = new List<int>();
            for (int i = 0; i < junctionPoints.Count; i++)
            {
                int nextId = i == junctionPoints.Count - 1 ? 0 : i + 1;
                Vector3 v1 = junctionPoints[i].point;
                Vector3 v2 = junctionPoints[nextId].point;
                Vector3 v3 = point;

                int offset = exVerts.Count;
                int t1 = offset + 0;
                int t2 = offset + 1;
                int t3 = offset + 2;

                exVerts.AddRange(new List<Vector3> { v1, v2, v3 });
                exTris.AddRange(new List<int> { t1, t2, t3 });
            }
            for (int i = 0; i < sidewalkPointsList.Count; i++)
            {
                List<JunctionPoint> sidewalkPoints = sidewalkPointsList[i];
                for (int j = 0; j < sidewalkPoints.Count - 1; j++)
                {
                    Vector3 v1 = sidewalkPoints[j].point;
                    Vector3 v2 = sidewalkPoints[j].point + sidewalkPoints[j].expand * ((sidewalkWidth + kerbWidth * 2) * scaleFactor + Mathf.Sqrt(2));
                    Vector3 v3 = sidewalkPoints[j + 1].point;
                    Vector3 v4 = sidewalkPoints[j + 1].point + sidewalkPoints[j + 1].expand * ((sidewalkWidth + kerbWidth * 2) * scaleFactor + Mathf.Sqrt(2));

                    int offset = exVerts.Count;
                    int t1 = offset + 0;
                    int t2 = offset + 1;
                    int t3 = offset + 3;
                    int t4 = offset + 3;
                    int t5 = offset + 2;
                    int t6 = offset + 0;

                    exVerts.AddRange(new List<Vector3> { v1, v2, v3, v4 });
                    exTris.AddRange(new List<int> { t1, t2, t3, t4, t5, t6 });
                }
            }
            for (int i = 0; i < exVerts.Count; i++)
            {
                exVerts[i] -= new Vector3(0, 0.1f, 0);
            }

            ExpandMeshData expandMeshData = new ExpandMeshData();
            expandMeshData.vertices = exVerts.ToArray();
            expandMeshData.triangles = exTris.ToArray();
            return expandMeshData;
        }

        public List<SerializableVector3> GetPolyVerts()
        {
            List<Vector3> polyVertices = new List<Vector3>();
            for (int i = 0; i < sidewalkPointsList.Count; i++)
            {
                List<JunctionPoint> sidewalkPoints = sidewalkPointsList[i];
                for (int j = 0; j < sidewalkPoints.Count; j++)
                {
                    Vector3 polyVert = sidewalkPoints[j].point + sidewalkPoints[j].expand * (kerbWidth * 2 + sidewalkWidth) * scaleFactor;
                    polyVert -= new Vector3(0, 0.1f, 0);
                    polyVertices.Add(polyVert);
                }
            }
            List<SerializableVector3> res = polyVertices.Select(a => a.ToSerializableVector3()).ToList();
            return res;
        }
    }
}