using UnityEngine;
using GeoToolkit.GeoJSON;
using Unity.Mathematics;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GeoToolkit
{
    public class RouteGenerator
    {
        public static float GetPointHeight(Vector3 point, LayerMask layerMask)
        {
            Ray ray = new Ray(point + Vector3.up * 10000, Vector3.down);
            if (Physics.Raycast(ray, out var hitInfo, float.MaxValue, layerMask))
            {
                return hitInfo.point.y;
            }
            return 0;
        }

        public static void CreateRoute(Transform parent, GeoPlatformConfig config, string geojson, Material routeMat, LayerMask layerMask, string savePath = @"Asset")
        {
            Vector2d center = Conversions.LatLonToMeters(config.CenterLatitude, config.CenterLongitude);
            FeatureCollection collection = GeoJSONObject.Deserialize(geojson, null);
            List<List<double3>> allLines = new List<List<double3>>();
            for (int i = 0; i < collection.features.Count; i++)
            {
                FeatureObject feature = collection.features[i];
                if (feature.geometry.GetType() == typeof(MultiLineStringGeometryObject))
                {
                    MultiLineStringGeometryObject mls = (MultiLineStringGeometryObject)feature.geometry;
                    List<List<double3>> lls = mls.lonlatheights;
                    allLines.AddRange(lls);
                }
                else if (feature.geometry.GetType() == typeof(LineStringGeometryObject))
                {
                    LineStringGeometryObject lsgo = (LineStringGeometryObject)feature.geometry;
                    List<double3> ls = lsgo.lonlatheights;
                    allLines.Add(ls);
                }
            }
            List<List<double3>> combine = new List<List<double3>>();
            while (allLines.Count > 0)
            {
                List<double3> start = allLines[allLines.Count - 1];
                allLines.RemoveAt(allLines.Count - 1);
                for (int i = allLines.Count - 1; i >= 0; i--)
                {
                    List<double3> tmp = allLines[i];
                    if (math.all(start.First() == tmp.Last()))
                    {
                        start.InsertRange(0, tmp);
                        allLines.RemoveAt(i);
                    }
                    else if (math.all(start.First() == tmp.First()))
                    {
                        tmp.Reverse();
                        start.InsertRange(0, tmp);
                        allLines.RemoveAt(i);
                    }
                    else if (math.all(start.Last() == tmp.Last()))
                    {
                        tmp.Reverse();
                        start.AddRange(tmp);
                        allLines.RemoveAt(i);
                    }
                    else if (math.all(start.Last() == tmp.First()))
                    {
                        start.AddRange(tmp);
                        allLines.RemoveAt(i);
                    }
                }
                combine.Add(start);
            }

            List<CombineInstance> combineInstances = new List<CombineInstance>();

            for (int i = 0; i < combine.Count; i++)
            {
                List<double3> lines = combine[i];
                List<Vector3> routePoints = new List<Vector3>();
                for (int j = 0; j < lines.Count; j++)
                {
                    double3 pos = lines[j];
                    Vector2d worldPos = Conversions.GeoToWorldPosition(pos.y, pos.x, center);
                    worldPos *= config.GetScale();
                    Vector3 point = new Vector3((float)worldPos.x, 0, (float)worldPos.y);
                    point.y = GetPointHeight(point, layerMask) + 1;
                    routePoints.Add(point);
                }

                List<Vector3> verts = new List<Vector3>();
                List<int> tris = new List<int>();
                List<Vector2> uvs = new List<Vector2>();
                MeshGenerator.GenerateRoadMesh(routePoints, 25 * config.GetScale(), 1, ref verts, ref uvs, ref tris);
                Mesh routeMesh = new Mesh();
                routeMesh.SetVertices(verts);
                routeMesh.SetTriangles(tris, 0);
                routeMesh.SetUVs(0, uvs);
                routeMesh.RecalculateNormals();

                CombineInstance combineIns = new CombineInstance();
                combineIns.mesh = routeMesh;
                combineIns.transform = Matrix4x4.identity;
                combineInstances.Add(combineIns);
            }


            Mesh CombineMesh = new Mesh();
            CombineMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            CombineMesh.CombineMeshes(combineInstances.ToArray());



#if UNITY_EDITOR
            // 保存 Mesh
            string meshAssetPath = $"{savePath}/{parent.name}.asset";
            AssetDatabase.CreateAsset(CombineMesh, meshAssetPath);

#endif
            Material matClone = new Material(routeMat);
#if UNITY_EDITOR
            // 保存 Material（克隆一份保存，避免原始材质引用被修改）
            string matAssetPath = $"{savePath}/{parent.name}_Mat.mat";
            AssetDatabase.CreateAsset(matClone, matAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif


            GameObject route = new GameObject("route");
            route.transform.parent = parent.transform;
            route.AddComponent<MeshFilter>().mesh = CombineMesh;
            route.AddComponent<MeshRenderer>().material = matClone;
            route.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
