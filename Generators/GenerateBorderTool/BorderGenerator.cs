
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Buffer;
using Poly2Tri;
using Unity.Mathematics;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace GeoToolkit.GeoJSON
{

    public struct Edge : System.IEquatable<Edge>
    {
        public int v0, v1;

        public Edge(int vertex0, int vertex1)
        {
            if (vertex0 < vertex1)
            {
                v0 = vertex0;
                v1 = vertex1;
            }
            else
            {
                v0 = vertex1;
                v1 = vertex0;
            }
        }

        public bool Equals(Edge other)
        {
            return v0 == other.v0 && v1 == other.v1;
        }

        public override bool Equals(object obj)
        {
            return obj is Edge other && Equals(other);
        }

        public override int GetHashCode()
        {
            return v0.GetHashCode() ^ (v1.GetHashCode() << 2);
        }
    }

    public class BorderGenerator
    {
        public static void CreateBorder(Transform parent, Vector2d origin, string geojson, float scale, Material wallMat, float wallWidth, float wallHeight, Material decalMat, string textureSavePath = @"Assets/GeneratedTextures")
        {
            Vector2d center = Conversions.LatLonToMeters(origin);
            FeatureCollection collection = GeoJSONObject.Deserialize(geojson, null);
            for (int m = 0; m < collection.features.Count; m++)
            {
                //if (m != 0)
                //    continue;
                FeatureObject feature = collection.features[m];
                string name = "border";
                if (feature.properties.ContainsKey("name"))
                {
                    name = feature.properties["name"];
                }
                GameObject wall = new GameObject(name);
                wall.transform.parent = parent;
                if (feature.geometry.GetType() == typeof(MultiPolygonGeometryObject))
                {
                    MultiPolygonGeometryObject mp = (MultiPolygonGeometryObject)feature.geometry;
                    List<List<List<double3>>> cs = mp.lonlatheights;
                    float heightOffset = mp.minMeshZ;
                    for (int i = 0; i < cs.Count; i++)
                    {
                        for (int j = 0; j < cs[i].Count; j++)
                        {
                            List<Vector3> points = new List<Vector3>();
                            for (int k = 0; k < cs[i][j].Count; k++)
                            {
                                double3 pos = cs[i][j][k];
                                Vector2d worldPos = Conversions.GeoToWorldPosition(pos.y, pos.x, center);
                                Vector3 point = new Vector3((float)worldPos.x, (float)(pos.z + 50 / scale), (float)worldPos.y);
                                points.Add(point);
                            }

                            List<Vector3> tmp = new List<Vector3>(points);
                            if (tmp[0] == tmp[tmp.Count - 1])
                            {
                                tmp.RemoveAt(tmp.Count - 1);
                            }
                            Mesh bottomMesh = CalculateBottomMesh(tmp, heightOffset);
                            if (bottomMesh != null)
                            {
                                GameObject decal = GenerateDecal(bottomMesh, $"{name}_{i}_{j}", scale, decalMat, 512, 512, $"{textureSavePath}/{parent.name}");
                                decal.transform.parent = wall.transform;
                            }

                            GameObject innerWall = GenerateSideWall(points, wallMat, wallHeight, $"{name}_{i}_{j}", $"{textureSavePath}/{parent.name}");
                            innerWall.transform.parent = wall.transform;

                            GeometryFactory factory = new GeometryFactory();
                            List<Coordinate> coords = points.Select(i => new Coordinate(i.x, i.z)).ToList();
                            var ring = factory.CreateLinearRing(coords.ToArray());
                            var polygon = factory.CreatePolygon(ring);
                            //Debug.LogError("ring:" + polygon.NumPoints);
                            var bufferParams = new BufferParameters
                            {
                                EndCapStyle = EndCapStyle.Flat,
                                JoinStyle = JoinStyle.Mitre,
                                QuadrantSegments = 1
                            };
                            double bufferDistance = wallWidth;
                            var buffered = polygon.Buffer(bufferDistance, bufferParams) as NetTopologySuite.Geometries.Polygon;
                            var outerRing = buffered.ExteriorRing;
                            //Debug.LogError("outerRing:" + outerRing.NumPoints);
                            List<Vector3> outer_points = new List<Vector3>();
                            for (int x = 0; x < outerRing.NumPoints; x++)
                            {
                                var pt = outerRing.GetPointN(x);
                                Vector3 point = new Vector3((float)pt.X, heightOffset, (float)pt.Y);
                                outer_points.Add(point);
                            }
                            var matcher = new XZNearestMatcher(points, 1);
                            matcher.MatchYValues(outer_points, 300);

                            GameObject outerWall = GenerateSideWall(outer_points, wallMat, wallHeight, $"{name}_{i}_{j}", $"{textureSavePath}/{parent.name}", true);
                            outerWall.transform.parent = wall.transform;

                            GameObject topWall = GenerateTopWall(points, outer_points, wallMat, wallHeight, $"{name}_{i}_{j}", $"{textureSavePath}/{parent.name}");
                            if (topWall != null)
                            {
                                topWall.transform.parent = wall.transform;
                            }
                        }
                    }
                }
                else if (feature.geometry.GetType() == typeof(PolygonGeometryObject))
                {
                    PolygonGeometryObject pgo = (PolygonGeometryObject)feature.geometry;
                    List<List<double3>> cs = pgo.lonlatheights;
                    float heightOffset = pgo.minMeshZ;
                    for (int i = 0; i < cs.Count; i++)
                    {
                        List<Vector3> points = new List<Vector3>();
                        for (int j = 0; j < cs[i].Count; j++)
                        {
                            double3 pos = cs[i][j];
                            Vector2d worldPos = Conversions.GeoToWorldPosition(pos.y, pos.x, center);
                            Vector3 point = new Vector3((float)worldPos.x, (float)(pos.z + 50 / scale), (float)worldPos.y);
                            points.Add(point);
                        }

                        List<Vector3> tmp = new List<Vector3>(points);
                        if (tmp[0] == tmp[tmp.Count - 1])
                        {
                            tmp.RemoveAt(tmp.Count - 1);
                        }
                        Mesh bottomMesh = CalculateBottomMesh(tmp, heightOffset);
                        if (bottomMesh != null)
                        {
                            GameObject decal = GenerateDecal(bottomMesh, $"{name}_{i}", scale, decalMat, 512, 512, $"{textureSavePath}/{parent.name}");
                            decal.transform.parent = wall.transform;
                        }

                        GameObject innerWall = GenerateSideWall(points, wallMat, wallHeight, $"{name}_{i}", $"{textureSavePath}/{parent.name}");
                        innerWall.transform.parent = wall.transform;

                        GeometryFactory factory = new GeometryFactory();
                        List<Coordinate> coords = points.Select(i => new Coordinate(i.x, i.z)).ToList();
                        var ring = factory.CreateLinearRing(coords.ToArray());
                        var polygon = factory.CreatePolygon(ring);
                        //Debug.LogError("ring:" + polygon.NumPoints);
                        var bufferParams = new BufferParameters
                        {
                            EndCapStyle = EndCapStyle.Flat,
                            JoinStyle = JoinStyle.Mitre,
                            QuadrantSegments = 1
                        };
                        double bufferDistance = wallWidth;
                        var buffered = polygon.Buffer(bufferDistance, bufferParams) as NetTopologySuite.Geometries.Polygon;
                        var outerRing = buffered.ExteriorRing;
                        //Debug.LogError("outerRing:" + outerRing.NumPoints);
                        List<Vector3> outer_points = new List<Vector3>();
                        for (int x = 0; x < outerRing.NumPoints; x++)
                        {
                            var pt = outerRing.GetPointN(x);
                            Vector3 point = new Vector3((float)pt.X, heightOffset, (float)pt.Y);
                            outer_points.Add(point);
                        }
                        var matcher = new XZNearestMatcher(points, 1);
                        matcher.MatchYValues(outer_points, 300);

                        GameObject outerWall = GenerateSideWall(outer_points, wallMat, wallHeight, $"{name}_{i}", $"{textureSavePath}/{parent.name}", true);
                        outerWall.transform.parent = wall.transform;

                        GameObject topWall = GenerateTopWall(points, outer_points, wallMat, wallHeight, $"{name}_{i}", $"{textureSavePath}/{parent.name}");
                        if (topWall != null)
                        {
                            topWall.transform.parent = wall.transform;
                        }
                    }
                }
                else if (feature.geometry.GetType() == typeof(LineStringGeometryObject))
                {
                    LineStringGeometryObject pgo = (LineStringGeometryObject)feature.geometry;
                    List<double3> cs = pgo.lonlatheights;
                    float heightOffset = pgo.minMeshZ;
                    List<Vector3> points = new List<Vector3>();
                    for (int j = 0; j < cs.Count; j++)
                    {
                        double3 pos = cs[j];
                        Vector2d worldPos = Conversions.GeoToWorldPosition(pos.y, pos.x, center);
                        Vector3 point = new Vector3((float)worldPos.x, (float)(pos.z + 50 / scale), (float)worldPos.y);
                        points.Add(point);
                    }
                    Vector3 last = points[0];
                    points.Add(last);

                    List<Vector3> tmp = new List<Vector3>(points);
                    if (tmp[0] == tmp[tmp.Count - 1])
                    {
                        tmp.RemoveAt(tmp.Count - 1);
                    }
                    Mesh bottomMesh = CalculateBottomMesh(tmp, heightOffset);
                    if (bottomMesh != null)
                    {
                        GameObject decal = GenerateDecal(bottomMesh, $"{name}", scale, decalMat, 512, 512, $"{textureSavePath}/{parent.name}");
                        decal.transform.parent = wall.transform;
                    }

                    GameObject innerWall = GenerateSideWall(points, wallMat, wallHeight, $"{name}", $"{textureSavePath}/{parent.name}");
                    innerWall.transform.parent = wall.transform;

                    GeometryFactory factory = new GeometryFactory();
                    List<Coordinate> coords = points.Select(i => new Coordinate(i.x, i.z)).ToList();
                    var ring = factory.CreateLinearRing(coords.ToArray());
                    var polygon = factory.CreatePolygon(ring);
                    //Debug.LogError("ring:" + polygon.NumPoints);
                    var bufferParams = new BufferParameters
                    {
                        EndCapStyle = EndCapStyle.Flat,
                        JoinStyle = JoinStyle.Mitre,
                        QuadrantSegments = 1
                    };
                    double bufferDistance = wallWidth;
                    var buffered = polygon.Buffer(bufferDistance, bufferParams) as NetTopologySuite.Geometries.Polygon;
                    var outerRing = buffered.ExteriorRing;
                    //Debug.LogError("outerRing:" + outerRing.NumPoints);
                    List<Vector3> outer_points = new List<Vector3>();
                    for (int x = 0; x < outerRing.NumPoints; x++)
                    {
                        var pt = outerRing.GetPointN(x);
                        Vector3 point = new Vector3((float)pt.X, heightOffset, (float)pt.Y);
                        outer_points.Add(point);
                    }
                    var matcher = new XZNearestMatcher(points, 1);
                    matcher.MatchYValues(outer_points, 300);

                    GameObject outerWall = GenerateSideWall(outer_points, wallMat, wallHeight, $"{name}", $"{textureSavePath}/{parent.name}", true);
                    outerWall.transform.parent = wall.transform;

                    GameObject topWall = GenerateTopWall(points, outer_points, wallMat, wallHeight, $"{name}", $"{textureSavePath}/{parent.name}");
                    if (topWall != null)
                    {
                        topWall.transform.parent = wall.transform;
                    }
                }
                else if (feature.geometry.GetType() == typeof(MultiLineStringGeometryObject))
                {
                    MultiLineStringGeometryObject pgo = (MultiLineStringGeometryObject)feature.geometry;
                    List<List<double3>> cs = pgo.lonlatheights;
                    float heightOffset = pgo.minMeshZ;
                    for (int i = 0; i < cs.Count; i++)
                    {
                        List<Vector3> points = new List<Vector3>();
                        for (int j = 0; j < cs[i].Count; j++)
                        {
                            double3 pos = cs[i][j];
                            Vector2d worldPos = Conversions.GeoToWorldPosition(pos.y, pos.x, center);
                            Vector3 point = new Vector3((float)worldPos.x, (float)(pos.z + 50 / scale), (float)worldPos.y);
                            points.Add(point);
                        }
                        Vector3 last = points[0];
                        points.Add(last);

                        List<Vector3> tmp = new List<Vector3>(points);
                        if (tmp[0] == tmp[tmp.Count - 1])
                        {
                            tmp.RemoveAt(tmp.Count - 1);
                        }
                        Mesh bottomMesh = CalculateBottomMesh(tmp, heightOffset);
                        if (bottomMesh != null)
                        {
                            GameObject decal = GenerateDecal(bottomMesh, $"{name}_{i}", scale, decalMat, 512, 512, $"{textureSavePath}/{parent.name}");
                            decal.transform.parent = wall.transform;
                        }

                        GameObject innerWall = GenerateSideWall(points, wallMat, wallHeight, $"{name}_{i}", $"{textureSavePath}/{parent.name}");
                        innerWall.transform.parent = wall.transform;

                        GeometryFactory factory = new GeometryFactory();
                        List<Coordinate> coords = points.Select(i => new Coordinate(i.x, i.z)).ToList();
                        var ring = factory.CreateLinearRing(coords.ToArray());
                        var polygon = factory.CreatePolygon(ring);
                        //Debug.LogError("ring:" + polygon.NumPoints);
                        var bufferParams = new BufferParameters
                        {
                            EndCapStyle = EndCapStyle.Flat,
                            JoinStyle = JoinStyle.Mitre,
                            QuadrantSegments = 1
                        };
                        double bufferDistance = wallWidth;
                        var buffered = polygon.Buffer(bufferDistance, bufferParams) as NetTopologySuite.Geometries.Polygon;
                        var outerRing = buffered.ExteriorRing;
                        //Debug.LogError("outerRing:" + outerRing.NumPoints);
                        List<Vector3> outer_points = new List<Vector3>();
                        for (int x = 0; x < outerRing.NumPoints; x++)
                        {
                            var pt = outerRing.GetPointN(x);
                            Vector3 point = new Vector3((float)pt.X, heightOffset, (float)pt.Y);
                            outer_points.Add(point);
                        }
                        var matcher = new XZNearestMatcher(points, 1);
                        matcher.MatchYValues(outer_points, 300);

                        GameObject outerWall = GenerateSideWall(outer_points, wallMat, wallHeight, $"{name}_{i}", $"{textureSavePath}/{parent.name}", true);
                        outerWall.transform.parent = wall.transform;

                        GameObject topWall = GenerateTopWall(points, outer_points, wallMat, wallHeight, $"{name}_{i}", $"{textureSavePath}/{parent.name}");
                        if (topWall != null)
                        {
                            topWall.transform.parent = wall.transform;
                        }
                    }
                }
            }
            parent.localScale = Vector3.one * scale;
        }

        #region Mesh To Decal

        public static void CreateDecalWithMesh(Transform parent, double2 origin, string geojson, float scale, Material decalMat, int textureWidth = 4096, int textureHeight = 4096, string textureSavePath = @"Assets/GeneratedTextures")
        {
            GeojsonParameter geojsonParameter = new GeojsonParameter() { centerLonLat = origin, isRaycastHight = false };
            FeatureCollection collection = GeoJSONObject.Deserialize(geojson, geojsonParameter);
            for (int m = 0; m < collection.features.Count; m++)
            {
                FeatureObject feature = collection.features[m];
                string name = feature.properties.ContainsKey("name") ? feature.properties["name"] : "Decal";
                string sixteenColor = feature.properties.ContainsKey("color") ? feature.properties["color"] : "#FFFFFF";
                if (feature.geometry.GetType() == typeof(MultiPolygonGeometryObject))
                {
                    MultiPolygonGeometryObject mp = (MultiPolygonGeometryObject)feature.geometry;

                    List<CombineInstance> combineIns = new List<CombineInstance>();

                    foreach (var item in mp.coordinates)
                    {
                        List<Vector3> vertex = new List<Vector3>();
                        List<int> tri = new List<int>();
                        List<Vector2> uvs = new List<Vector2>();
                        MeshGenerator.GenerateBottomMesh(item, 0, scale, ref vertex, ref tri, ref uvs);
                        Mesh PolygonMesh = new Mesh();
                        PolygonMesh.subMeshCount = 1;
                        PolygonMesh.SetVertices(vertex);
                        PolygonMesh.SetTriangles(tri, 0);
                        PolygonMesh.SetUVs(0, uvs);
                        PolygonMesh.RecalculateNormals();
                        PolygonMesh.RecalculateBounds();
                        PolygonMesh.Optimize();


                        CombineInstance combine = new CombineInstance();
                        combine.mesh = PolygonMesh;
                        combine.transform = Matrix4x4.identity;
                        combineIns.Add(combine);
                    }


                    Mesh CombineMesh = new Mesh();
                    CombineMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    CombineMesh.CombineMeshes(combineIns.ToArray());

                    #region Mesh

                    //GameObject MeshObj = new GameObject("Mesh");
                    //MeshObj.transform.parent = parent.transform;

                    //var meshFiliter = MeshObj.AddComponent<MeshFilter>();
                    //meshFiliter.mesh = CombineMesh;
                    //var meshRenderer = MeshObj.AddComponent<MeshRenderer>();
                    //meshRenderer.sharedMaterial = Material.Instantiate(decalMat);
                    //meshRenderer.sharedMaterial.SetColor("_Color", HexToColor(sixteenColor));

                    #endregion

                    GameObject decal = GenerateDecalV2(CombineMesh, $"{name}_{m}", decalMat, textureWidth, textureHeight, $"{textureSavePath}/{parent.name}");
                    decal.transform.parent = parent.transform;
                    DecalProjector decalProjector = decal.GetComponent<DecalProjector>();
                    var color = HexToColor(sixteenColor);
                    decalProjector.material.SetColor("_DecalColor", color);
                    decalProjector.material.SetColor("_MaskColor", color);

                }
            }
        }


        /// <summary>
        /// 将十六进制颜色字符串（如 "#RRGGBB" 或 "#RRGGBBAA"）转换为 Unity 的 Color。
        /// </summary>
        /// <param name="hex">十六进制颜色字符串</param>
        /// <returns>转换后的 Color 值</returns>
        public static Color HexToColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                Debug.LogWarning("空的颜色字符串");
                return Color.white;
            }

            // 去掉开头的 #
            if (hex.StartsWith("#"))
            {
                hex = hex.Substring(1);
            }

            // 如果是 RGB 格式，补上 Alpha
            if (hex.Length == 6)
            {
                hex += "FF";
            }

            if (hex.Length != 8)
            {
                Debug.LogWarning($"十六进制颜色长度错误: {hex}");
                return Color.white;
            }

            // 转换十六进制为 byte
            byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
            byte a = byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);

            // 归一化为 [0, 1] 区间
            return new Color32(r, g, b, a);
        }

        public static GameObject GenerateDecalV2(Mesh mesh, string texName, Material mat, int textureWidth, int textureHeight, string textureSavePath)
        {
            // 创建纹理并初始化为透明
            Texture2D generatedTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[textureWidth * textureHeight];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(0, 0, 0, 0); // 透明背景

            // 投影顶点到 XZ 平面
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            // 计算边界用于归一化
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var v in vertices)
            {
                if (v.x < minX) minX = v.x;
                if (v.x > maxX) maxX = v.x;
                if (v.z < minZ) minZ = v.z;
                if (v.z > maxZ) maxZ = v.z;
            }
            float width = maxX - minX;
            float height = maxZ - minZ;

            // 将所有顶点归一化到纹理空间
            Vector2[] projectedVerts = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                float u = (vertices[i].x - minX) / width;
                float v = (vertices[i].z - minZ) / height;
                projectedVerts[i] = new Vector2(u * textureWidth, v * textureHeight);
            }

            // 光栅化三角形填充贴图
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector2 p0 = projectedVerts[triangles[i]];
                Vector2 p1 = projectedVerts[triangles[i + 1]];
                Vector2 p2 = projectedVerts[triangles[i + 2]];
                RasterizeTriangle(pixels, textureWidth, textureHeight, p0, p1, p2, Color.white);
            }

            // 应用像素并保存贴图
            generatedTexture.SetPixels(pixels);
            generatedTexture.Apply();



#if UNITY_EDITOR
            if (!Directory.Exists(textureSavePath))
                Directory.CreateDirectory(textureSavePath);
            string texFullPath = Path.Combine(textureSavePath, texName + ".png");
            File.WriteAllBytes(texFullPath, generatedTexture.EncodeToPNG());
            AssetDatabase.Refresh();

            // 加载纹理资源
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texFullPath);
#else
            Texture2D tex = generatedTexture;
#endif


            // 创建并保存材质
            string matFullPath = Path.Combine(textureSavePath, texName + ".mat");
            Material decalMat = new Material(mat); // 从基础材质复制
            decalMat.SetTexture("_Texture2D", tex);  // 使用 HDRP/URP 通用命名 "_BaseMap"
                                                     //decalMat.SetColor("_BaseColor", Color.white);
                                                     //decalMat.EnableKeyword("_ALPHATEST_ON");

#if UNITY_EDITOR
            AssetDatabase.CreateAsset(decalMat, matFullPath.Replace("\\", "/"));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif

            // 创建 Decal Projector
            GameObject obj = new GameObject("Decal_" + texName);
            obj.transform.position = mesh.bounds.center + Vector3.up * 0.5f;
            obj.transform.rotation = Quaternion.Euler(90, 0, 0); // 向下投射

            DecalProjector decal = obj.AddComponent<DecalProjector>();

#if UNITY_EDITOR
            decal.material = AssetDatabase.LoadAssetAtPath<Material>(matFullPath.Replace("\\", "/"));
#else
           decal.material =decalMat;
#endif


            Vector3 meshSize = mesh.bounds.size;
            decal.size = new Vector3(meshSize.x, meshSize.z, 30000); // Z 是投射深度
            decal.drawDistance = 100000;

            return obj;
        }

        private static void RasterizeTriangle(Color[] pixels, int texWidth, int texHeight, Vector2 p0, Vector2 p1, Vector2 p2, Color color)
        {
            int minX = Mathf.FloorToInt(Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x)));
            int maxX = Mathf.CeilToInt(Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x)));
            int minY = Mathf.FloorToInt(Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y)));
            int maxY = Mathf.CeilToInt(Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y)));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (x < 0 || x >= texWidth || y < 0 || y >= texHeight) continue;
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    if (PointInTriangle(p, p0, p1, p2))
                    {
                        pixels[y * texWidth + x] = color;
                    }
                }
            }
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float s = a.y * c.x - a.x * c.y + (c.y - a.y) * p.x + (a.x - c.x) * p.y;
            float t = a.x * b.y - a.y * b.x + (a.y - b.y) * p.x + (b.x - a.x) * p.y;

            if ((s < 0) != (t < 0))
                return false;

            float A = -b.y * c.x + a.y * (c.x - b.x) + a.x * (b.y - c.y) + b.x * c.y;
            return A < 0 ? (s <= 0 && s + t >= A) : (s >= 0 && s + t <= A);
        }

        #endregion

        public static float GetPointHeight(Vector3 point, float scale)
        {
            Ray ray = new Ray(point * scale + Vector3.up * 10000, Vector3.down);
            if (Physics.Raycast(ray, out var hitInfo, float.MaxValue))
            {
                return hitInfo.point.y / scale;
            }
            return 0;
        }

        public static bool IsClockwise(IList<Vector3> vertices)
        {
            double sum = 0.0;
            var _counter = vertices.Count;
            for (int i = 0; i < _counter; i++)
            {
                Vector3 _v1 = vertices[i];
                Vector3 _v2 = vertices[(i + 1) % _counter];
                sum += (_v2.x - _v1.x) * (_v2.z + _v1.z);
            }

            return sum > 0.0;
        }

        public static Mesh CalculateBottomMesh(List<Vector3> points, float heightOffset)
        {
            points = points.Select(i => new Vector3(i.x, 0, i.z)).ToList();
            points = points.Distinct(new Vector3EqualityComparer(0.1f)).ToList();
            List<Vector3> verts = new List<Vector3>();
            verts.AddRange(points);
            //if (IsClockwise(points))
            //    points.Reverse();
            List<PolygonPoint> polygonPoints = points.Select(i => new PolygonPoint(i.x, i.z)).ToList();
            if (polygonPoints.Count < 3)
                return null;
            Poly2Tri.Polygon polygon = new Poly2Tri.Polygon(polygonPoints);
            P2T.Triangulate(polygon);
            List<DelaunayTriangle> triangles = polygon.Triangles.ToList();
            List<int> tris = new List<int>();
            foreach (var tri in triangles)
            {
                var p1 = new Vector3((float)tri.Points[0].X, 0, (float)tri.Points[0].Y);
                var p2 = new Vector3((float)tri.Points[1].X, 0, (float)tri.Points[1].Y);
                var p3 = new Vector3((float)tri.Points[2].X, 0, (float)tri.Points[2].Y);
                if (!verts.Contains(p1) || !verts.Contains(p2) || !verts.Contains(p3))
                    continue;
                tris.Add(verts.IndexOf(p3));
                tris.Add(verts.IndexOf(p2));
                tris.Add(verts.IndexOf(p1));
            }
            List<Vector2> uvs = verts.Select(i => new Vector2(i.x, i.z)).ToList();
            Mesh mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static GameObject GenerateDecal(Mesh mesh, string texName, float scale, Material mat, int textureWidth, int textureHeight, string textureSavePath)
        {
            // 投影并归一化顶点
            List<Vector2> projected = ProjectAndNormalize(mesh.vertices);

            // 创建纹理
            Texture2D generatedTexture = new Texture2D(textureWidth, textureHeight);
            for (int x = 0; x < textureWidth; x++)
            {
                for (int y = 0; y < textureHeight; y++)
                {
                    Vector2 uvPoint = new Vector2(
                        (float)x / textureWidth,
                        (float)y / textureHeight
                    );

                    bool isInside = IsPointInPolygon(uvPoint, projected.ToArray());
                    Color pixelColor = isInside ? Color.white : Color.black;
                    generatedTexture.SetPixel(x, y, pixelColor);
                }
            }
            generatedTexture.Apply();

#if UNITY_EDITOR
            // 确保保存目录存在
            if (!Directory.Exists(textureSavePath))
            {
                Directory.CreateDirectory(textureSavePath);
            }

            // 保存贴图
            string texAssetPath = $"{textureSavePath}/{texName}.png";
            byte[] pngData = generatedTexture.EncodeToPNG();
            File.WriteAllBytes(texAssetPath, pngData);
            AssetDatabase.Refresh();

            // 加载贴图资源
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texAssetPath);

#else
            Texture2D tex = generatedTexture;
#endif

            // 保存材质
            string matAssetPath = $"{textureSavePath}/{texName}_Decal.mat";
            Material decalMat;

#if UNITY_EDITOR
            if (File.Exists(matAssetPath))
            {

                decalMat = AssetDatabase.LoadAssetAtPath<Material>(matAssetPath);

            }
            else
            {
#endif
                decalMat = new Material(mat);
                decalMat.SetTexture("_Texture2D", tex);
#if UNITY_EDITOR
                AssetDatabase.CreateAsset(decalMat, matAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

            }
#endif

            // 创建贴花对象
            GameObject obj = new GameObject("decal");
            obj.transform.localPosition = mesh.bounds.center;
            obj.transform.localEulerAngles = new Vector3(90, 0, 0);

            DecalProjector decal = obj.AddComponent<DecalProjector>();
            decal.material = decalMat;

            Vector3 meshSize = mesh.bounds.size;

            decalMat.SetTextureScale("_Mask", new Vector2(meshSize.x * scale, meshSize.z * scale) / 1000);
            decal.size = new Vector3(meshSize.x * scale, meshSize.z * scale, 1000);
            decal.drawDistance = 100000;

            return obj;
        }

        public static void AddEdge(Dictionary<Edge, int> edgeCount, int v0, int v1)
        {
            Edge edge = new Edge(v0, v1);
            if (edgeCount.ContainsKey(edge))
            {
                edgeCount[edge]++;
            }
            else
            {
                edgeCount[edge] = 1;
            }
        }

        public static List<Vector3> BuildBoundaryPath(List<Edge> boundaryEdges, Vector3[] vertices)
        {
            if (boundaryEdges.Count == 0) return new List<Vector3>();

            List<Vector3> path = new List<Vector3>();
            HashSet<Edge> usedEdges = new HashSet<Edge>();

            Edge currentEdge = boundaryEdges[0];
            path.Add(vertices[currentEdge.v0]);
            path.Add(vertices[currentEdge.v1]);
            usedEdges.Add(currentEdge);

            int currentVertex = currentEdge.v1;

            while (usedEdges.Count < boundaryEdges.Count)
            {
                bool foundNext = false;

                foreach (Edge edge in boundaryEdges)
                {
                    if (usedEdges.Contains(edge)) continue;

                    if (edge.v0 == currentVertex)
                    {
                        path.Add(vertices[edge.v1]);
                        currentVertex = edge.v1;
                        usedEdges.Add(edge);
                        foundNext = true;
                        break;
                    }
                    else if (edge.v1 == currentVertex)
                    {
                        path.Add(vertices[edge.v0]);
                        currentVertex = edge.v0;
                        usedEdges.Add(edge);
                        foundNext = true;
                        break;
                    }
                }

                if (!foundNext) break;
            }

            return path;
        }

        public static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
        {
            int vertexCount = polygon.Length;
            bool isInside = false;

            int j = vertexCount - 1;
            for (int i = 0; i < vertexCount; i++)
            {
                Vector2 vi = polygon[i];
                Vector2 vj = polygon[j];

                if (((vi.y > point.y) != (vj.y > point.y)) &&
                    (point.x < (vj.x - vi.x) * (point.y - vi.y) / (vj.y - vi.y) + vi.x))
                {
                    isInside = !isInside;
                }
                j = i;
            }

            return isInside;
        }

        public static List<Vector2> ProjectAndNormalize(Vector3[] vertices)
        {
            Vector3 min = vertices[0];
            Vector3 max = vertices[0];

            foreach (Vector3 vertex in vertices)
            {
                min = Vector3.Min(min, vertex);
                max = Vector3.Max(max, vertex);
            }

            List<Vector2> points2D = new List<Vector2>();
            Vector3 size = max - min;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 point2D;
                point2D = new Vector2((vertices[i].x - min.x) / size.x, (vertices[i].z - min.z) / size.z);
                points2D.Add(point2D);
            }

            return SimplifyPathTest(points2D, 0.01f);
        }

        public static List<Vector2> SimplifyPathTest(List<Vector2> points, float tolerance)
        {
            if (points.Count <= 2) return points;

            List<Vector2> simplified = new List<Vector2>();
            simplified.Add(points[0]);

            for (int i = 1; i < points.Count; i++)
            {
                Vector2 current = points[i];
                Vector2 last = simplified[simplified.Count - 1];

                if (Vector2.Distance(current, last) > tolerance)
                {
                    simplified.Add(current);
                }
            }

            if (simplified.Count > 2)
            {
                Vector2 first = simplified[0];
                Vector2 last = simplified[simplified.Count - 1];
                if (Vector2.Distance(first, last) <= tolerance)
                {
                    simplified.RemoveAt(simplified.Count - 1);
                }
            }

            return simplified;
        }

        public static GameObject GenerateTopWall(List<Vector3> innerPoints, List<Vector3> outerPoints, Material mat, float wallHeight, string meshName, string savePath)
        {
            innerPoints.RemoveAt(innerPoints.Count - 1);
            outerPoints.RemoveAt(outerPoints.Count - 1);
            outerPoints = outerPoints.Distinct(new Vector3EqualityComparer(0.1f)).ToList();
            innerPoints = innerPoints.Distinct(new Vector3EqualityComparer(0.1f)).ToList();
            List<Vector3> verts = new List<Vector3>();
            verts.AddRange(outerPoints);
            verts.AddRange(innerPoints);
            verts = verts.Select(i => i + new Vector3(0, wallHeight, 0)).ToList();
            List<Vector3> verts1 = verts.Select(i => new Vector3(i.x, 0, i.z)).ToList();
            if (IsClockwise(outerPoints))
                outerPoints.Reverse();
            if (IsClockwise(innerPoints))
                innerPoints.Reverse();
            List<PolygonPoint> points = outerPoints.Select(i => new PolygonPoint(i.x, i.z)).ToList();
            if (points.Count < 3)
                return null;
            Poly2Tri.Polygon outerPolygon = new Poly2Tri.Polygon(points);
            List<PolygonPoint> holePoints = innerPoints.Select(i => new PolygonPoint(i.x, i.z)).ToList();
            if (holePoints.Count < 3)
                return null;
            Poly2Tri.Polygon holePolygon = new Poly2Tri.Polygon(holePoints);
            outerPolygon.AddHole(holePolygon);
            P2T.Triangulate(outerPolygon);
            List<DelaunayTriangle> triangles = outerPolygon.Triangles.ToList();
            List<int> tris = new List<int>();
            foreach (var tri in triangles)
            {
                var p1 = new Vector3((float)tri.Points[0].X, 0, (float)tri.Points[0].Y);
                var p2 = new Vector3((float)tri.Points[1].X, 0, (float)tri.Points[1].Y);
                var p3 = new Vector3((float)tri.Points[2].X, 0, (float)tri.Points[2].Y);
                if (!verts1.Contains(p1) || !verts1.Contains(p2) || !verts1.Contains(p3))
                    continue;
                tris.Add(verts1.IndexOf(p3));
                tris.Add(verts1.IndexOf(p2));
                tris.Add(verts1.IndexOf(p1));
            }
            List<Vector2> uvs = verts.Select(i => new Vector2(i.x, i.z)).ToList();
            Mesh mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();

            // 确保保存目录存在
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

#if UNITY_EDITOR
            // 保存 Mesh
            string meshAssetPath = $"{savePath}/TopWall_{meshName}.asset";
            AssetDatabase.CreateAsset(mesh, meshAssetPath);

            // 保存 Material（克隆一份保存，避免原始材质引用被修改）
            string matAssetPath = $"{savePath}/TopWall_{meshName}_Mat.mat";
#endif
            Material matClone = new Material(mat);
#if UNITY_EDITOR
            AssetDatabase.CreateAsset(matClone, matAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif
            GameObject go = new GameObject("topWall");
            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>().material = matClone;
            return go;
        }

        public static GameObject GenerateSideWall(List<Vector3> points, Material mat, float wallHeight, string meshName, string savePath, bool outer = false)
        {
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 p1 = points[i];
                Vector3 p2 = p1 + Vector3.up * wallHeight;
                Vector3 p3 = points[i + 1];
                Vector3 p4 = p3 + Vector3.up * wallHeight;
                int vertexOffset = verts.Count;
                int t1 = 0 + vertexOffset;
                int t2 = (outer ? 2 : 1) + vertexOffset;
                int t3 = 3 + vertexOffset;
                int t4 = 3 + vertexOffset;
                int t5 = (outer ? 1 : 2) + vertexOffset;
                int t6 = 0 + vertexOffset;
                Vector2 uv1 = new Vector2(0, 0);
                Vector2 uv2 = new Vector2(0, 1);
                Vector2 uv3 = new Vector2(1, 0);
                Vector2 uv4 = new Vector2(1, 1);

                verts.AddRange(new List<Vector3>() { p1, p2, p3, p4 });
                tris.AddRange(new List<int>() { t1, t2, t3, t4, t5, t6 });
                uvs.AddRange(new List<Vector2> { uv1, uv2, uv3, uv4 });
            }
            Mesh mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();

            // 确保保存目录存在
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }



            string objName = outer ? "outerWall" : "innerWall";

#if UNITY_EDITOR
            // 保存 Mesh
            string meshAssetPath = $"{savePath}/{objName}_{meshName}.asset";
            AssetDatabase.CreateAsset(mesh, meshAssetPath);

            // 保存 Material（克隆一份保存，避免原始材质引用被修改）
            string matAssetPath = $"{savePath}/{objName}_{meshName}_Mat.mat";
#endif
            Material matClone = new Material(mat);
#if UNITY_EDITOR
            AssetDatabase.CreateAsset(matClone, matAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif

            GameObject go = new GameObject(objName);
            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>().material = matClone;
            return go;
        }
    }
}
