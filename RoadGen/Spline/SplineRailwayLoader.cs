#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using GeoToolkit;
using GeoToolkit.GeoJSON;
using GeoToolkit.Road;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace GeoDCBuildTools.RoadTool
{
    public class SplineRailwayLoader
    {
        public static GenerateRailwayResult GenerateRailwayPrefab(string geojson, UnwrappedTileId tileId, float terrainSize = 2048, Terrain terrain = null, string savePath = "Assets/Geo/roadModel/")
        {
            RoadDataConfig roadDataConfig = Resources.Load<RoadDataConfig>("RoadDataConfig");
            RailwayDataInfo railwayDataInfo = CalculateRailwayMeshData(geojson, tileId, roadDataConfig, terrainSize, terrain);

            Mesh railwayMesh = new Mesh();
            railwayMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            railwayMesh.SetVertices(railwayDataInfo.roadVertices);
            railwayMesh.SetUVs(0, railwayDataInfo.roadUVs);
            railwayMesh.subMeshCount = railwayDataInfo.roadTrianglesDict.Count;
            foreach (var item in railwayDataInfo.roadTrianglesDict)
            {
                railwayMesh.SetTriangles(item.Value, item.Key);
            }
            railwayMesh.RecalculateNormals();

            string path = $"{savePath}{tileId.ToString()}-railwayMesh.asset";
            AssetDatabase.CreateAsset(railwayMesh, path);
            AssetDatabase.Refresh();

            GameObject railwayNetwork = new GameObject("RAILWAY&" + railwayDataInfo.tileId.ToString());
            railwayNetwork.CheckAddLayer("Road");
            railwayNetwork.AddComponent<MeshFilter>().mesh = railwayMesh;
            railwayNetwork.AddComponent<MeshRenderer>().materials = railwayDataInfo.roadMatDict.Keys.ToArray();
            railwayNetwork.AddComponent<MeshCollider>();

            if (terrain != null)
            {
                railwayNetwork.transform.position = terrain.transform.position;
                Mesh expandMesh = new Mesh();
                expandMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                expandMesh.SetVertices(railwayDataInfo.expandVertices);
                expandMesh.SetTriangles(railwayDataInfo.expandTriangles, 0);
                GameObject expand = new GameObject("expand");
                expand.CheckAddLayer("Expand");
                expand.transform.parent = railwayNetwork.transform;
                expand.transform.localPosition = Vector3.zero;
                expand.AddComponent<MeshFilter>().mesh = expandMesh;
                expand.AddComponent<MeshCollider>();
                Conversions.ModifyTerrain(terrain, "Expand");
                GameObject.DestroyImmediate(expand);
            }

            GenerateRailwayResult generateRailwayResult = new GenerateRailwayResult();
            generateRailwayResult.railwayMesh = railwayMesh;
            generateRailwayResult.railwayObj = railwayNetwork;
            return generateRailwayResult;
        }

        public static RailwayDataInfo CalculateRailwayMeshData(string geojson, UnwrappedTileId tileId, RoadDataConfig roadDataConfig, float terrainSize, Terrain terrain)
        {
            (var scaleFactor, var railwayDict) = ParsingRailwayData(geojson, tileId, terrainSize, terrain);
            Dictionary<Material, int> matDict = new Dictionary<Material, int>();
            Dictionary<int, List<int>> trianglesDict = new Dictionary<int, List<int>>();
            List<Vector3> allVertices = new List<Vector3>();
            List<Vector2> allUVs = new List<Vector2>();

            List<Vector3> expandVertices = new List<Vector3>();
            List<int> expandTriangles = new List<int>();

            foreach (var item in railwayDict)
            {
                SplineRailwayMeshGenerator splineRailwayMeshGenerator = new SplineRailwayMeshGenerator(item.Value.id, 3, scaleFactor, item.Value.points, roadDataConfig, terrain);
                RoadMeshData railwayMeshData = splineRailwayMeshGenerator.CalculateRailwayMeshData();
                int offset = allVertices.Count;
                List<int> triangles = railwayMeshData.triangles.ToList();
                triangles = triangles.Select(i => i += offset).ToList();
                allVertices.AddRange(railwayMeshData.vertices);
                allUVs.AddRange(railwayMeshData.uvs);
                if (matDict.ContainsKey(railwayMeshData.material))
                {
                    int id = matDict[railwayMeshData.material];
                    trianglesDict[id].AddRange(triangles);
                }
                else
                {
                    matDict.Add(railwayMeshData.material, matDict.Count);
                    trianglesDict.Add(matDict[railwayMeshData.material], triangles);
                }

                ExpandMeshData expandMeshData = splineRailwayMeshGenerator.CalculateExpandMeshData();
                int offset2 = expandVertices.Count;
                List<int> expandTris = expandMeshData.triangles.ToList();
                expandTris = expandTris.Select(i => i += offset2).ToList();
                expandVertices.AddRange(expandMeshData.vertices);
                expandTriangles.AddRange(expandTris);
            }

            RailwayDataInfo railwayDataInfo = new RailwayDataInfo();
            railwayDataInfo.tileId = tileId;
            railwayDataInfo.roadMatDict = matDict;
            railwayDataInfo.roadTrianglesDict = trianglesDict;
            railwayDataInfo.roadVertices = allVertices;
            railwayDataInfo.roadUVs = allUVs;
            railwayDataInfo.expandVertices = expandVertices;
            railwayDataInfo.expandTriangles = expandTriangles;
            return railwayDataInfo;
        }

        public static (float, Dictionary<string, RailwayData>) ParsingRailwayData(string geojson, UnwrappedTileId tileId, float terrainSize, Terrain terrain)
        {
            Vector2d center = Conversions.TileIdToCenterWebMercator(tileId.X, tileId.Y, tileId.Z);
            double tileSize = Conversions.CalculateTileSize(tileId.Z);
            double scaleFactor = terrainSize / tileSize;
            Vector2d offset = Vector2d.zero;
            if (terrain != null)
            {
                offset = new Vector2d(terrainSize / 2, terrainSize / 2);
            }
            Dictionary<string, RailwayData> railwayDict = new Dictionary<string, RailwayData>();
            FeatureCollection collection = GeoJSONObject.Deserialize(geojson, null);
            foreach (FeatureObject feature in collection.features)
            {
                string featureType = feature.properties["feature_type"];
                string id = feature.properties["id"];
                if (featureType == "lrrl")
                {
                    if (feature.geometry.GetType() == typeof(LineStringGeometryObject))
                    {
                        LineStringGeometryObject ls = (LineStringGeometryObject)feature.geometry;
                        List<Vector3> points = new List<Vector3>();
                        for (int i = 0; i < ls.lonlatheights.Count; i++)
                        {
                            double3 po = ls.lonlatheights[i];
                            Vector2d worldPos = Conversions.GeoToWorldPosition(po.y, po.x, center);
                            worldPos *= scaleFactor;
                            worldPos += offset;
                            Vector3 point = new Vector3((float)worldPos.x, 0, (float)worldPos.y);
                            if (terrain != null)
                            {
                                float height = terrain.SampleHeight(terrain.transform.TransformPoint(point));
                                point.y = height;
                            }
                            points.Add(point);
                        }
                        RailwayData eRoad = new RailwayData(id, points);
                        railwayDict.Add(id, eRoad);
                    }
                    else if (feature.geometry.GetType() == typeof(MultiLineStringGeometryObject))
                    {
                        MultiLineStringGeometryObject mls = (MultiLineStringGeometryObject)feature.geometry;
                        List<List<double3>> cs = mls.lonlatheights;
                        for (int k = 0; k < cs.Count; k++)
                        {
                            List<double3> item = cs[k];
                            List<Vector3> points = new List<Vector3>();
                            for (int i = 0; i < item.Count; i++)
                            {
                                double3 po = item[i];
                                Vector2d worldPos = Conversions.GeoToWorldPosition(po.y, po.x, center);
                                worldPos *= scaleFactor;
                                worldPos += offset;
                                Vector3 point = new Vector3((float)worldPos.x, 0, (float)worldPos.y);
                                if (terrain != null)
                                {
                                    float height = terrain.SampleHeight(terrain.transform.TransformPoint(point));
                                    point.y = height;
                                }
                                points.Add(point);
                            }
                            RailwayData eRoad = new RailwayData(id, points);
                            railwayDict.Add(string.Format("{0}_{1}", id, k), eRoad);
                        }
                    }
                }
            }
            return ((float)scaleFactor, railwayDict);
        }
    }
}

#endif