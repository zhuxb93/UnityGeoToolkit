using UnityEngine;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Rendering.HighDefinition;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GeoToolkit.RadarEnvelope
{
    public class RadarEnvelopeTool 
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static int circleResolution = 30;
        public static int ringResolution = 30;
        public static int HemisphereHorNumber = 20;
        public static int HemisphereVerNumber = 30;
        public static float HemisphereHieghtFactor = 1f;

        public static (HemisphereEnvelope, FanRaderScanning) CreateHemisphere(int id, string name, Vector3 center, float radius, float ratio,
            Material hemiMat, Material scanMat,
            GameObject parent = null,
            GameObject scanningDecalPrefab = null)
        {
            GameObject hemis = new GameObject(name + "-Hemisphere");
            Vector3 tempCenter = center;
            float tempRadius = radius * ratio;
            HemisphereEnvelope hemisphereEnvelope = hemis.AddComponent<HemisphereEnvelope>();
            hemisphereEnvelope.transform.position = tempCenter;
            hemisphereEnvelope.SetHemisphereId(id);
            hemisphereEnvelope.Refresh(tempCenter, tempRadius, hemiMat);
            if (parent != null)
                hemis.transform.SetParent(parent.transform);
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();
            GenerateVectorHemisphereScanWithAngle(tempCenter, tempRadius, 45, ref verts, ref uvs, ref tris);
            Mesh mesh = new Mesh();

            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            GameObject scan = new GameObject("Scan");
            scan.transform.SetParent(hemis.transform);
            scan.transform.position = tempCenter;
            scan.transform.eulerAngles = new Vector3(0, UnityEngine.Random.Range(0, 360), 0);

            MeshFilter meshFilter = scan.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = scan.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = scanMat;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            FanRaderScanning fanRaderScanning = scan.AddComponent<FanRaderScanning>();
            fanRaderScanning.radiusWithRatio = tempRadius;
            fanRaderScanning.equipmentId = id;
    #if UNITY_EDITOR
            if (Application.isEditor && !Application.isPlaying)
            {
                string dirPath = "Assets/ImportRoot/Radar/Meshes";
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }
                string path = dirPath + "/" + hemis.name + "_" + hemisphereEnvelope.hemisphereId + "-Scan" + ".asset";
                AssetDatabase.CreateAsset(mesh, path);
                AssetDatabase.Refresh();
            }
    #endif
            if(scanningDecalPrefab != null)
            {
                GameObject decal = GameObject.Instantiate(scanningDecalPrefab);
                decal.name = "Decal";
                decal.transform.SetParent(hemis.transform);
                decal.transform.localPosition = Vector3.zero;
                DecalProjector decalProjector = decal.gameObject.GetComponent<DecalProjector>();
                decalProjector.size = new Vector3(tempRadius * 2, tempRadius * 2, 10000f);
                decalProjector.fadeFactor = 1;
            }
            if (Application.isPlaying && EnvelopeTransparentController.Instance != null)
            {

                EnvelopeTransparentController.Instance.fanRaderScannings.Add(fanRaderScanning);
            }
            else
            {
                EnvelopeTransparentController controller = GameObject.FindFirstObjectByType<EnvelopeTransparentController>();
                if (controller != null)
                {
                    controller.fanRaderScannings.Add(fanRaderScanning);
                }
            }
            return (hemisphereEnvelope, fanRaderScanning);
        }

        public static HemisphereEnvelope CreateEllipsoid(int id, string name, Vector3 center, float radius, float ratio, Material laserMat, GameObject parent = null)
        {
            GameObject hemis = new GameObject(name + "-Ellipsoid");
            Vector3 tempCenter = center;
            float tempRadius = radius * ratio;
            HemisphereEnvelope hemisphereEnvelope = hemis.AddComponent<HemisphereEnvelope>();
            hemisphereEnvelope.transform.position = tempCenter;
            hemisphereEnvelope.SetHemisphereId(id);
            hemisphereEnvelope.ellipseRatioHeight = 1.3f;
            hemisphereEnvelope.Refresh(tempCenter, tempRadius, laserMat);
            if (parent!= null)
                hemis.transform.SetParent(parent.transform);
            return hemisphereEnvelope;
        }

        public static (CircleDonutEnvelope, DonutEffectControl) CreateDonut(int id, string name, Vector3 center, float radius, float ratio, Material donutMat, GameObject parent = null)
        {
            float width_ratio = 1.2f;
            float height_ratio = 0.75f;
            Vector3 tempCenter = center;
            float tempRadius = radius * ratio;
            GameObject donut = new GameObject(name + "-Donut");
            CircleDonutEnvelope donutEnvelope = donut.AddComponent<CircleDonutEnvelope>();
            donutEnvelope.transform.position = tempCenter;
            float tubeRadius = tempRadius / (1f + width_ratio);
            List<Vector3> cir = DrawRing(tempCenter, tempCenter + Vector3.right * tubeRadius, 60);
            Material mat = new Material(donutMat);
            donutEnvelope.OnInit(GetTimestampID(), cir, tempCenter, width_ratio, height_ratio, false);
            donutEnvelope.RefreshVectorFace(mat);
            if (parent != null)
                donutEnvelope.transform.SetParent(parent.transform);
            DonutEffectControl donutEffect = donut.AddComponent<DonutEffectControl>();
            donutEffect.id = id;
            donutEffect.radiusWithRatio = tempRadius;
    #if UNITY_EDITOR
            if (Application.isEditor && !Application.isPlaying)
            {
                donutEnvelope.SaveMesh("Assets/ImportRoot/Radar/Meshes");
                AssetDatabase.Refresh();
            }
    #endif
            if (Application.isPlaying && EnvelopeTransparentController.Instance != null)
            {

                EnvelopeTransparentController.Instance.donutEffects.Add(donutEffect);
            }
            else
            {
                EnvelopeTransparentController controller = GameObject.FindFirstObjectByType<EnvelopeTransparentController>();
                if (controller != null)
                {
                    controller.donutEffects.Add(donutEffect);
                }
            }
            return (donutEnvelope, donutEffect);
        }

        /// <summary>
        /// 半球体扫描 Mesh
        /// </summary>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <param name="angle">(0, 90]</param>
        public static void GenerateVectorHemisphereScanWithAngle(Vector3 center, float radius, float angle, ref List<Vector3> vertices, ref List<Vector2> uvs, ref List<int> triangles)
        {
            float v_offset = 0.01f;//0.015f;
            int verNumber = HemisphereVerNumber;
            float verRad = angle * Mathf.Deg2Rad;
            int verNumberRad = (int)Mathf.Ceil(verRad / (Mathf.PI * 0.5f) * verNumber);
            float offset = Mathf.PI * 0.5f - verRad;
            for (int i = 0; i < verNumberRad + 1; i++)
            {
                float rad = verRad * (i / (verNumberRad * 1.0f));
                float x = Mathf.Sin(offset + rad) * radius;
                float y = Mathf.Cos(offset + rad) * radius * HemisphereHieghtFactor;

                vertices.Add(center);
                vertices.Add(center + new Vector3(x, y, 0));
            }

            // 扇形最后 增加一个三角面，调节底边uv
            Vector3 last = vertices[vertices.Count - 1];
            Vector3 last_sec = vertices[vertices.Count - 3];
            Vector3 new_last_sec = (last + last_sec) * 0.5f;
            vertices.RemoveAt(vertices.Count - 1);
            vertices.Add(new_last_sec);
            vertices.Add(center);
            vertices.Add(last);

            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i] -= center;
            }
            for (int j = 0; j < verNumberRad; j++)
            {
                int index1 = j * 2;
                int index2 = j * 2 + 1;
                // int index3 = (j + 1) * 2;
                int index4 = (j + 1) * 2 + 1;

                triangles.Add(index1);
                triangles.Add(index4);
                triangles.Add(index2);

                uvs.Add(new Vector2(j, v_offset));
                uvs.Add(new Vector2(j, 1 - v_offset));
            }

            uvs.Add(new Vector2(verNumberRad - 0.5f, v_offset));
            uvs.Add(new Vector2(verNumberRad - 0.5f, 1 - v_offset));

            int lastIndex = vertices.Count - 1;
            triangles.Add(lastIndex);
            triangles.Add(lastIndex - 1);
            triangles.Add(lastIndex - 2);
            uvs.Add(new Vector2(verNumberRad, v_offset));
            uvs.Add(new Vector2(verNumberRad, 1 - v_offset));
        }
        public static void GenerateVectorHemisphere(List<Vector3> vertices, int verNumber, ref List<Vector2> uvs, ref List<int> triangles)
        {
            int step = verNumber + 1;
            int num = vertices.Count / step;
            for (int i = 0; i < num; i++)
            {

                for (int j = 0; j < step; j++)
                {
                    if (j < step - 1)
                    {
                        if (i < num - 1)
                        {
                            int index1 = i * step + j;
                            int index2 = i * step + j + 1;
                            int index3 = (i + 1) % num * step + j;
                            int index4 = (i + 1) % num * step + j + 1;

                            triangles.Add(index1);
                            triangles.Add(index4);
                            triangles.Add(index3);

                            triangles.Add(index1);
                            triangles.Add(index2);
                            triangles.Add(index4);
                        }
                    }
                    uvs.Add(new Vector2(j, i));
                }
            }
        }
        /// <summary>
        /// 计算 圆环顶点
        /// </summary>
        /// <param name="center">中心点</param>
        /// <param name="radius">中心点到环心点距离</param>
        /// <param name="width_radius">圆环 横半径</param>
        /// <param name="height_radius">圆环 竖半径</param>
        /// <returns></returns>
        public static List<Vector3> CalcCircleDonut(Vector3 center, float radius, float width_radius, float height_radius)
        {
            center += Vector3.up * height_radius;
            Vector3 rightRingCenter = center + Vector3.right * radius;

            List<Vector3> circleVertices = new List<Vector3>();
            if (width_radius <= radius)
            {
                for (int j = 0; j < circleResolution; j++)
                {
                    float t = 2 * Mathf.PI * j / circleResolution;
                    float x = -width_radius * Mathf.Cos(t);
                    float y = height_radius * Mathf.Sin(t);

                    circleVertices.Add(rightRingCenter + new Vector3(x, y, 0)); // z设为0，即在XY平面
                }
            }
            else
            {
                for (int j = 0; j < circleResolution; j++)
                {
                    float t = 2 * Mathf.PI * j / circleResolution;
                    float x = -width_radius * Mathf.Cos(t);
                    float y = height_radius * Mathf.Sin(t);

                    Vector3 p = rightRingCenter + new Vector3(x, y, 0);
                    if (p.x > center.x)
                    {
                        if (circleVertices.Count == 0)
                        {
                            float t_x_zero = Mathf.Acos((center.x - rightRingCenter.x) / (-width_radius));
                            float y_x_zero = height_radius * Mathf.Sin(t_x_zero);
                            circleVertices.Add(new Vector3(center.x, rightRingCenter.y + Mathf.Abs(y_x_zero), rightRingCenter.z));
                        }
                        circleVertices.Add(rightRingCenter + new Vector3(x, y, 0));
                    }
                }
                Vector3 first = circleVertices[0];
                circleVertices.Add(new Vector3(first.x, rightRingCenter.y * 2 - first.y, rightRingCenter.z));
            }
            List<Vector3> ringVertices = new List<Vector3>();
            for (int j = 0; j < circleVertices.Count; j++)
            {
                Vector3 point = circleVertices[j];
                for (int k = 0; k <= ringResolution; k++) // 小于等于 Resolution: 环形闭合
                {
                    Vector3 tempCenter = new Vector3(center.x, point.y, center.z);
                    float tempRadius = Vector3.Distance(tempCenter, point);
                    float t = 2 * Mathf.PI * k / ringResolution;
                    float x = tempRadius * Mathf.Cos(t);
                    float z = tempRadius * Mathf.Sin(t);

                    ringVertices.Add(tempCenter + new Vector3(x, 0, z));
                }
            }
            return ringVertices;
        }

        public static void GenerateVectorDonut(List<Vector3> vertices, float width_ratio, ref List<Vector2> uvs, ref List<int> triangles)
        {
            int step = ringResolution + 1;
            int num = vertices.Count / step;
            for (int i = 0; i < num; i++)
            {
                for (int j = 0; j < step; j++)
                {
                    if (j < step - 1)
                    {
                        int index1 = i * step + j;
                        int index2 = i * step + j + 1;
                        int index3 = (i + 1) % num * step + j;
                        int index4 = (i + 1) % num * step + j + 1;

                        if (width_ratio < 1)
                        {
                            triangles.Add(index1);
                            triangles.Add(index4);
                            triangles.Add(index3);

                            triangles.Add(index1);
                            triangles.Add(index2);
                            triangles.Add(index4);
                        }
                        else
                        {
                            if (i < num - 1)
                            {
                                triangles.Add(index1);
                                triangles.Add(index4);
                                triangles.Add(index3);

                                triangles.Add(index1);
                                triangles.Add(index2);
                                triangles.Add(index4);
                            }
                        }
                    }
                    uvs.Add(new Vector2(j, i));
                }
            }
        }
        public static List<Vector3> DrawRing(Vector3 center, Vector3 point, int segments, float startOffsetAngle = 0)
        {
            List<Vector3> points = new List<Vector3>();
            Vector3 centerPlane = new Vector3(center.x, 0, center.z);
            Vector3 pointPlane = new Vector3(point.x, 0, point.z);
            Vector3 normal = Vector3.up;
            float radius = Vector3.Distance(centerPlane, pointPlane);
            // 计算切线和副切线
            Vector3 tangent = Vector3.zero;
            Vector3.OrthoNormalize(ref normal, ref tangent); // 自动生成正交基

            Vector3 binormal = Vector3.Cross(normal, tangent).normalized;

            float angleStep = 2 * Mathf.PI / segments;
            float startOffsetRad = startOffsetAngle * Mathf.Deg2Rad;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep + startOffsetRad;
                if (angle < 0) angle += 2 * Mathf.PI;
                else if (angle > 2 * Mathf.PI) angle -= 2 * Mathf.PI;
                Vector3 offset = tangent * (radius * Mathf.Cos(angle)) + binormal * (radius * Mathf.Sin(angle));
                Vector3 newpoint = center + offset;
                Vector3 newHeightPoint = GetPointWithRayHeight(newpoint + Vector3.up * center.y);
                points.Add(newHeightPoint);
            }
            return points;

        }
        public static string GetRayLayer()
        {
            string layer = LayerMask.LayerToName(10);
            layer = string.IsNullOrEmpty(layer) ? "Default" : layer;
            return layer;
        }
        public static Vector3 GetPointWithRayHeight(Vector3 oriPoint, GameObject selfGameObject = null)
        {
            Ray ray = new Ray(oriPoint + Vector3.up * 1000, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, LayerMask.GetMask(GetRayLayer())))
            {
                RaycastHit[] faceObjs = Physics.RaycastAll(oriPoint + Vector3.up * 1000, Vector3.down, float.MaxValue, LayerMask.GetMask("VectorFace"));
                if (faceObjs != null && faceObjs.Length > 0)
                {
                    if (selfGameObject == null)
                    {
                        return faceObjs[0].point;
                    }
                    else
                    {
                        for (int i = 0; i < faceObjs.Length; i++)
                        {
                            if (faceObjs[i].collider.gameObject != selfGameObject)
                            {
                                return faceObjs[i].point;
                            }
                        }
                    }
                }
                return hit.point;
            }
            return oriPoint;
        }
        public static ulong GetTimestampID()
        {
            TimeSpan interval = DateTime.UtcNow - Epoch;
            return (ulong)interval.TotalMilliseconds;
        }
    }
}

