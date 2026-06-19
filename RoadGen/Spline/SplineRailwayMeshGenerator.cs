using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace GeoDCBuildTools.RoadTool
{
    public class SplineRailwayMeshGenerator
    {
        public string id;
        public float width;
        public float scaleFactor;
        public List<Vector3> points;
        public Terrain terrain;

        public List<Vector3> vertices1;
        public List<Vector3> vertices2;
        public List<Vector3> expandVertices1;
        public List<Vector3> expandVertices2;

        private Spline spline;
        private Material material;

        public SplineRailwayMeshGenerator(string id, float width, float scaleFactor, List<Vector3> points, RoadDataConfig config, Terrain terrain)
        {
            this.id = id;
            this.width = width;
            this.scaleFactor = scaleFactor;
            this.points = points;
            this.terrain = terrain;

            material = config.railwayMatConfig.railwayMat;

            SetSpline();
            CalculateRailwayData();
        }

        public void SetSpline()
        {
            List<BezierKnot> bezierKnots = new List<BezierKnot>();
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 point = points[i];
                if (terrain != null)
                {
                    float height = terrain.SampleHeight(point);
                    point.y = height;
                }
                BezierKnot knot = new BezierKnot(point);
                bezierKnots.Add(knot);
            }
            spline = new Spline(bezierKnots);
            spline.SetTangentMode(TangentMode.AutoSmooth);
        }

        public void CalculateRailwayData()
        {
            vertices1 = new List<Vector3>();
            vertices2 = new List<Vector3>();
            expandVertices1 = new List<Vector3>();
            expandVertices2 = new List<Vector3>();
            int roadResolution = Mathf.CeilToInt(spline.GetLength() / (3 * scaleFactor));
            float step = 1f / (float)roadResolution;
            for (int i = 0; i <= roadResolution; i++)
            {
                float t = step * i;
                SplineUtility.Evaluate(spline, t, out var position, out var forward, out var upVector);
                float3 right = Vector3.Cross(forward, upVector).normalized;
                float sampleHeight = 0;
                if (terrain != null)
                {
                    sampleHeight = terrain.SampleHeight(terrain.transform.TransformPoint(position));
                }
                position.y = sampleHeight + 0.1f;
                Vector3 p1 = position + (right * width / 2 * scaleFactor);
                Vector3 p2 = position - (right * width / 2 * scaleFactor);
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
            }
        }

        public RoadMeshData CalculateRailwayMeshData()
        {
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            float lenRec = 0;
            float lenCur = 0;
            int length = vertices1.Count;
            float convertWidth = width * scaleFactor;
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
            roadMeshData.material = material;
            return roadMeshData;
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
    }
}