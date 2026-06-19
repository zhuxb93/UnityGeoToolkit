#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeoToolkit;
using GeoToolkit.GeoJSON;
using GeoToolkit.Road;
using Newtonsoft.Json;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Random = UnityEngine.Random;

namespace GeoDCBuildTools.RoadTool
{

    public class SplineRoadLoader
    {
        /// <summary>
        /// 解析道路元素json
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static RoadFacilityData AnalysisRoadFacilityData(string json)
        {
            RoadFacilityData roadFacilityData = JsonConvert.DeserializeObject<RoadFacilityData>(json);
            return roadFacilityData;
        }

        /// <summary>
        /// 生成道路
        /// </summary>
        /// <param name="geojson"></param>
        /// <param name="tileId"></param>
        /// <param name="minRoadWidth"></param>
        /// <param name="maxRoadWidth"></param>
        /// <param name="kerbWidth"></param>
        /// <param name="sidewalkWidth"></param>
        /// <param name="subgradeHeight"></param>
        /// <param name="terrainSize"></param>
        /// <param name="terrain"></param>
        /// <returns></returns>
        public static GenerateRoadResult GenerateRoadPrefab(string geojson, UnwrappedTileId tileId, bool isFacility = true, float terrainSize = 2048, Terrain terrain = null, string savePath = "Assets/Geo/roadModel/")
        {
            RoadDataConfig roadDataConfig = Resources.Load<RoadDataConfig>("RoadDataConfig");
            RoadDataInfo roadDataInfo = CalculateRoadMeshData(geojson, tileId, roadDataConfig, terrainSize, terrain);

            Mesh roadMesh = new Mesh();
            roadMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            roadMesh.SetVertices(roadDataInfo.roadVertices);
            roadMesh.SetUVs(0, roadDataInfo.roadUVs);
            roadMesh.subMeshCount = roadDataInfo.roadTrianglesDict.Count;
            foreach (var item in roadDataInfo.roadTrianglesDict)
            {
                roadMesh.SetTriangles(item.Value, item.Key);
            }
            roadMesh.RecalculateNormals();

            //string path = $"Assets/Geo/roadModel/{tileId.ToString()}-roadMesh.asset";
            string path = $"{savePath}{tileId.ToString()}-roadMesh.asset";
            AssetDatabase.CreateAsset(roadMesh, path);
            AssetDatabase.Refresh();

            GameObject roadNetwork = new GameObject("ROAD&" + roadDataInfo.tileId.ToString());
            roadNetwork.CheckAddLayer("Road");
            roadNetwork.AddComponent<MeshFilter>().sharedMesh = roadMesh;
            roadNetwork.AddComponent<MeshRenderer>().materials = roadDataInfo.roadMatDict.Keys.ToArray();
            roadNetwork.AddComponent<MeshCollider>();
            if (terrain != null)
            {
                roadNetwork.transform.position = terrain.transform.position;
                Mesh expandMesh = new Mesh();
                expandMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                expandMesh.SetVertices(roadDataInfo.expandVertices);
                expandMesh.SetTriangles(roadDataInfo.expandTriangles, 0);
                GameObject expand = new GameObject("expand");
                expand.CheckAddLayer("Expand");
                expand.transform.parent = roadNetwork.transform;
                expand.transform.localPosition = Vector3.zero;
                expand.AddComponent<MeshFilter>().mesh = expandMesh;
                expand.AddComponent<MeshCollider>();
                Conversions.ModifyTerrain(terrain, "Expand");
                GameObject.DestroyImmediate(expand);
            }


            RoadFacilitySaveInfo roadFacilitySaveInfo = new RoadFacilitySaveInfo();
            roadFacilitySaveInfo.tileId = tileId.ToString();
            roadFacilitySaveInfo.facilityMarkPoints = roadDataInfo.facilityMarkPoints;
            string saveInfo = JsonConvert.SerializeObject(roadFacilitySaveInfo);
            if (isFacility)
            {
                RoadFacilityData roadFacilityData = AnalysisRoadFacilityData(saveInfo);
                //GenerateRoadFacility(roadFacilityData.facilityMarkPoints, roadNetwork.transform, $"{savePath}{tileId.ToString()}-RoadFacility.json");
                GenerateRoadFacilityToFile(roadFacilityData.facilityMarkPoints, roadNetwork.transform, terrainSize, $"{savePath}{tileId.ToString()}-RoadFacility.json");
            }
            //GenerateTrafficLanes(roadDataInfo.lanes, roadNetwork.transform);

            GenerateRoadResult generateRoadResult = new GenerateRoadResult();
            generateRoadResult.roadMesh = roadMesh;
            generateRoadResult.roadObj = roadNetwork;
            generateRoadResult.roadFacilityInfo = saveInfo;
            return generateRoadResult;
        }

        public static RoadDataInfo CalculateRoadMeshData(string geojson, UnwrappedTileId tileId, RoadDataConfig roadDataConfig, float terrainSize, Terrain terrain)
        {
            (var scaleFactor, var roadDict, var intersections) = ParsingRoadData(geojson, tileId, roadDataConfig.roadParameterConfig.minRoadWidth, roadDataConfig.roadParameterConfig.maxRoadWidth, terrainSize, terrain);
            ReserveSpace(intersections, roadDict, scaleFactor, terrain);

            Dictionary<Material, int> matDict = new Dictionary<Material, int>();
            Dictionary<int, List<int>> trianglesDict = new Dictionary<int, List<int>>();
            List<Vector3> allVertices = new List<Vector3>();
            List<Vector2> allUVs = new List<Vector2>();

            List<Vector3> expandVertices = new List<Vector3>();
            List<int> expandTriangles = new List<int>();

            List<FacilityMarkPoint> facilityMarkPoints = new List<FacilityMarkPoint>();

            List<List<SerializableVector3>> allPoly = new List<List<SerializableVector3>>();

            List<SplineRoadMeshGenerator> roadMeshGenerators = new List<SplineRoadMeshGenerator>();
            List<SplineIntersectionMeshGenerator> intersectionMeshGenerators = new List<SplineIntersectionMeshGenerator>();

            foreach (var item in roadDict)
            {
                SplineRoadMeshGenerator splineRoadMeshGenerator = new SplineRoadMeshGenerator(item.Value.id, item.Value.width, scaleFactor, item.Value.points, roadDataConfig, terrain);
                roadMeshGenerators.Add(splineRoadMeshGenerator);
                item.Value.splineRoadMeshGenerator = splineRoadMeshGenerator;
                allPoly.Add(splineRoadMeshGenerator.GetPolyVerts());

                RoadMeshData roadMeshData = splineRoadMeshGenerator.CalculateRoadMeshData();
                int offset = allVertices.Count;
                List<int> triangles = roadMeshData.triangles.ToList();
                triangles = triangles.Select(i => i += offset).ToList();
                allVertices.AddRange(roadMeshData.vertices);
                allUVs.AddRange(roadMeshData.uvs);
                if (matDict.ContainsKey(roadMeshData.material))
                {
                    int id = matDict[roadMeshData.material];
                    trianglesDict[id].AddRange(triangles);
                }
                else
                {
                    matDict.Add(roadMeshData.material, matDict.Count);
                    trianglesDict.Add(matDict[roadMeshData.material], triangles);
                }

                SidewalkMeshData sidewalkMeshData = splineRoadMeshGenerator.CalculateSidewalkMeshData();
                int offset1 = allVertices.Count;
                List<int> triangles1 = sidewalkMeshData.kerbTriangles;
                triangles1 = triangles1.Select(i => i += offset1).ToList();
                List<int> triangles2 = sidewalkMeshData.sidewalkTriangles;
                triangles2 = triangles2.Select(i => i += offset1).ToList();
                List<int> triangles3 = sidewalkMeshData.concreteTriangles;
                triangles3 = triangles3.Select(i => i += offset1).ToList();
                allVertices.AddRange(sidewalkMeshData.vertices);
                allUVs.AddRange(sidewalkMeshData.uvs);
                if (matDict.ContainsKey(sidewalkMeshData.kerbMaterial))
                {
                    int id = matDict[sidewalkMeshData.kerbMaterial];
                    trianglesDict[id].AddRange(triangles1);
                }
                else
                {
                    matDict.Add(sidewalkMeshData.kerbMaterial, matDict.Count);
                    trianglesDict.Add(matDict[sidewalkMeshData.kerbMaterial], triangles1);
                }
                if (matDict.ContainsKey(sidewalkMeshData.sidewalkMaterial))
                {
                    int id = matDict[sidewalkMeshData.sidewalkMaterial];
                    trianglesDict[id].AddRange(triangles2);
                }
                else
                {
                    matDict.Add(sidewalkMeshData.sidewalkMaterial, matDict.Count);
                    trianglesDict.Add(matDict[sidewalkMeshData.sidewalkMaterial], triangles2);
                }
                if (matDict.ContainsKey(sidewalkMeshData.concreteMaterial))
                {
                    int id = matDict[sidewalkMeshData.concreteMaterial];
                    trianglesDict[id].AddRange(triangles3);
                }
                else
                {
                    matDict.Add(sidewalkMeshData.concreteMaterial, matDict.Count);
                    trianglesDict.Add(matDict[sidewalkMeshData.concreteMaterial], triangles3);
                }

                ExpandMeshData expandMeshData = splineRoadMeshGenerator.CalculateExpandMeshData();
                int offset2 = expandVertices.Count;
                List<int> expandTris = expandMeshData.triangles.ToList();
                expandTris = expandTris.Select(i => i += offset2).ToList();
                expandVertices.AddRange(expandMeshData.vertices);
                expandTriangles.AddRange(expandTris);

                facilityMarkPoints.AddRange(splineRoadMeshGenerator.CalculateFacilityPoints());
            }

            for (int i = 0; i < intersections.Count; i++)
            {
                IntersectionInfoData splineIntersection = intersections[i];
                Dictionary<SplineRoadMeshGenerator, EndpointType> dict = new Dictionary<SplineRoadMeshGenerator, EndpointType>();
                foreach (var item in splineIntersection.roadIds)
                {
                    SplineRoadMeshGenerator splineRoadMeshGenerator = roadDict[item.Key].splineRoadMeshGenerator;
                    dict.Add(splineRoadMeshGenerator, item.Value);
                }
                SplineIntersectionMeshGenerator splineIntersectionMeshGenerator = new SplineIntersectionMeshGenerator(i, splineIntersection.point, splineIntersection.forkNum, splineIntersection.roadIds, dict, scaleFactor, roadDataConfig);
                intersectionMeshGenerators.Add(splineIntersectionMeshGenerator);
                allPoly.Add(splineIntersectionMeshGenerator.GetPolyVerts());

                IntersectionMeshData intersectionMeshData = splineIntersectionMeshGenerator.CalculateIntersectionMeshData();
                int offset = allVertices.Count;
                List<int> triangles = intersectionMeshData.triangles.ToList();
                triangles = triangles.Select(i => i += offset).ToList();
                List<int> crossTriangles = intersectionMeshData.crossTriangles.ToList();
                crossTriangles = crossTriangles.Select(i => i + offset).ToList();
                allVertices.AddRange(intersectionMeshData.vertices);
                allUVs.AddRange(intersectionMeshData.uvs);
                if (matDict.ContainsKey(intersectionMeshData.material))
                {
                    int id = matDict[intersectionMeshData.material];
                    trianglesDict[id].AddRange(triangles);
                }
                else
                {
                    matDict.Add(intersectionMeshData.material, matDict.Count);
                    trianglesDict.Add(matDict[intersectionMeshData.material], triangles);
                }
                if (matDict.ContainsKey(intersectionMeshData.crossMaterial))
                {
                    int id = matDict[intersectionMeshData.crossMaterial];
                    trianglesDict[id].AddRange(crossTriangles);
                }
                else
                {
                    matDict.Add(intersectionMeshData.crossMaterial, matDict.Count);
                    trianglesDict.Add(matDict[intersectionMeshData.crossMaterial], crossTriangles);
                }

                SidewalkMeshData sidewalkMeshData = splineIntersectionMeshGenerator.CalculateSidewalkMeshData();
                int offset1 = allVertices.Count;
                List<int> triangles1 = sidewalkMeshData.kerbTriangles;
                triangles1 = triangles1.Select(i => i += offset1).ToList();
                List<int> triangles2 = sidewalkMeshData.sidewalkTriangles;
                triangles2 = triangles2.Select(i => i += offset1).ToList();
                List<int> triangles3 = sidewalkMeshData.concreteTriangles;
                triangles3 = triangles3.Select(i => i += offset1).ToList();
                allVertices.AddRange(sidewalkMeshData.vertices);
                allUVs.AddRange(sidewalkMeshData.uvs);
                if (matDict.ContainsKey(sidewalkMeshData.kerbMaterial))
                {
                    int id = matDict[sidewalkMeshData.kerbMaterial];
                    trianglesDict[id].AddRange(triangles1);
                }
                else
                {
                    matDict.Add(sidewalkMeshData.kerbMaterial, matDict.Count);
                    trianglesDict.Add(matDict[sidewalkMeshData.kerbMaterial], triangles1);
                }
                if (matDict.ContainsKey(sidewalkMeshData.sidewalkMaterial))
                {
                    int id = matDict[sidewalkMeshData.sidewalkMaterial];
                    trianglesDict[id].AddRange(triangles2);
                }
                else
                {
                    matDict.Add(sidewalkMeshData.sidewalkMaterial, matDict.Count);
                    trianglesDict.Add(matDict[sidewalkMeshData.sidewalkMaterial], triangles2);
                }
                if (matDict.ContainsKey(sidewalkMeshData.concreteMaterial))
                {
                    int id = matDict[sidewalkMeshData.concreteMaterial];
                    trianglesDict[id].AddRange(triangles3);
                }
                else
                {
                    matDict.Add(sidewalkMeshData.concreteMaterial, matDict.Count);
                    trianglesDict.Add(matDict[sidewalkMeshData.concreteMaterial], triangles3);
                }

                ExpandMeshData expandMeshData = splineIntersectionMeshGenerator.CalculateExpandMeshData();
                int offset2 = expandVertices.Count;
                List<int> expandTris = expandMeshData.triangles.ToList();
                expandTris = expandTris.Select(i => i += offset2).ToList();
                expandVertices.AddRange(expandMeshData.vertices);
                expandTriangles.AddRange(expandTris);
            }

            //List<Spline> splines = new List<Spline>();
            //for (int i = 0; i < 20; i++)
            //{
            //    List<Route> routes = CalculateRoutes(roadMeshGenerators, intersectionMeshGenerators);
            //    if (routes.Count == 0)
            //        continue;
            //    Spline spline = MergeRoute(routes);
            //    splines.Add(spline);
            //}

            RoadDataInfo roadDataInfo = new RoadDataInfo();
            roadDataInfo.tileId = tileId;
            roadDataInfo.roadMatDict = matDict;
            roadDataInfo.roadTrianglesDict = trianglesDict;
            roadDataInfo.roadVertices = allVertices;
            roadDataInfo.roadUVs = allUVs;
            roadDataInfo.expandVertices = expandVertices;
            roadDataInfo.expandTriangles = expandTriangles;
            roadDataInfo.facilityMarkPoints = facilityMarkPoints;
            roadDataInfo.allPoly = allPoly;
            //roadDataInfo.lanes = splines;
            return roadDataInfo;
        }

        public static (float, Dictionary<int, RoadInfoData>, List<IntersectionInfoData>) ParsingRoadData(string geojson, UnwrappedTileId tileId, float minRoadWidth, float maxRoadWidth, float terrainSize, Terrain terrain)
        {
            Vector2d center = Conversions.TileIdToCenterWebMercator(tileId.X, tileId.Y, tileId.Z);
            double tileSize = Conversions.CalculateTileSize(tileId.Z);
            double scaleFactor = terrainSize / tileSize;
            Vector2d offset = Vector2d.zero;
            if (terrain != null)
            {
                offset = new Vector2d(terrainSize / 2, terrainSize / 2);
            }
            Dictionary<int, RoadInfoData> roadDict = new Dictionary<int, RoadInfoData>();
            List<IntersectionInfoData> intersections = new List<IntersectionInfoData>();
            FeatureCollection collection = GeoJSONObject.Deserialize(geojson, null);
            foreach (FeatureObject feature in collection.features)
            {
                string layer = feature.properties["layer"];
                if (layer == "road")
                {
                    int id = int.Parse(feature.properties["road_id"]);
                    float width = float.Parse(feature.properties["road_width"]);
                    width = Mathf.Clamp(width, minRoadWidth, maxRoadWidth);
                    if (feature.geometry.type == "LineString")
                    {
                        LineStringGeometryObject ls = (LineStringGeometryObject)feature.geometry;
                        List<Vector3> ePoints = new List<Vector3>();
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
                            ePoints.Add(point);
                        }

                        RoadInfoData roadData = new RoadInfoData(id, width, ePoints);
                        roadDict.Add(id, roadData);
                    }
                }
                else if (layer == "intersection")
                {
                    int forkNum = int.Parse(feature.properties["degree"]);
                    if (forkNum < 3)
                        continue;
                    List<int> roadIds = feature.properties["edge_ids"].Split(',').Select(int.Parse).ToList();
                    if (roadIds.Count != forkNum)
                        continue;
                    if (feature.geometry.type == "Point")
                    {
                        PointGeometryObject p = (PointGeometryObject)feature.geometry;
                        double3 position = p.lonlatheight;
                        Vector2d worldPos = Conversions.GeoToWorldPosition(position.y, position.x, center);
                        worldPos *= scaleFactor;
                        worldPos += offset;
                        Vector3 point = new Vector3((float)worldPos.x, 0, (float)worldPos.y);
                        if (terrain != null)
                        {
                            float height = terrain.SampleHeight(terrain.transform.TransformPoint(point));
                            point.y = height;
                        }
                        Dictionary<int, EndpointType> connectStates = new Dictionary<int, EndpointType>();
                        foreach (int id in roadIds)
                        {
                            if (roadDict[id].points[0] == point)
                            {
                                connectStates.Add(id, EndpointType.Start);
                            }
                            else if (roadDict[id].points[roadDict[id].points.Count - 1] == point)
                            {
                                connectStates.Add(id, EndpointType.End);
                            }
                        }
                        IntersectionInfoData intersection = new IntersectionInfoData(point, forkNum, connectStates);
                        intersections.Add(intersection);
                    }
                }
            }
            return ((float)scaleFactor, roadDict, intersections);
        }

        public static void CalculateIntersectionSpace(IntersectionInfoData intersection, Dictionary<int, RoadInfoData> roadDict, float scaleFactor, List<int> faultyRoads)
        {
            List<RoadInfoData> roads = intersection.roadIds.Keys.Select(id => roadDict[id]).ToList();
            roads.Sort((a, b) =>
            {
                Vector3 dir1;
                Vector3 dir2;
                int n1 = a.points.Count;
                dir1 = intersection.roadIds[a.id] == EndpointType.Start ? a.points[1] - a.points[0] : a.points[n1 - 2] - a.points[n1 - 1];
                int n2 = b.points.Count;
                dir2 = intersection.roadIds[b.id] == EndpointType.Start ? b.points[1] - b.points[0] : b.points[n2 - 2] - b.points[n2 - 1];
                float angle1 = Vector3.SignedAngle(Vector3.forward, dir1, Vector3.up);
                angle1 += angle1 < 0 ? 360 : 0;
                float angle2 = Vector3.SignedAngle(Vector3.forward, dir2, Vector3.up);
                angle2 += angle2 < 0 ? 360 : 0;
                if (angle1 > angle2)
                    return 1;
                else if (angle1 < angle2)
                    return -1;
                else
                    return 0;
            });

            for (int i = 0; i < roads.Count; i++)
            {
                RoadInfoData roadData = roads[i];
                RoadInfoData prev_road = i > 0 ? roads[i - 1] : roads[roads.Count - 1];
                RoadInfoData next_road = i < roads.Count - 1 ? roads[i + 1] : roads[0];
                List<Vector3> ps1 = prev_road.points;
                int n1 = ps1.Count;
                bool isStart1 = intersection.roadIds[prev_road.id] == EndpointType.Start;
                Vector3 prev_dir = isStart1 ? ps1[1] - ps1[0] : ps1[n1 - 2] - ps1[n1 - 1];
                List<Vector3> ps2 = next_road.points;
                int n2 = ps2.Count;
                bool isStart2 = intersection.roadIds[next_road.id] == EndpointType.Start;
                Vector3 next_dir = isStart2 ? ps2[1] - ps2[0] : ps2[n2 - 2] - ps2[n2 - 1];
                List<Vector3> ps3 = roadData.points;
                int n3 = ps3.Count;
                bool isStart3 = intersection.roadIds[roadData.id] == EndpointType.Start;
                Vector3 current_dir = isStart3 ? ps3[1] - ps3[0] : ps3[n3 - 2] - ps3[n3 - 1];
                float angle1 = Vector3.Angle(prev_dir, current_dir);
                float angle2 = Vector3.Angle(next_dir, current_dir);
                List<float> widths = new List<float> { prev_road.width, next_road.width };
                float w1 = ((180 - angle1) / 180) * (prev_road.width / widths.Max());
                float w2 = ((180 - angle2) / 180) * (next_road.width / widths.Max());
                float finalWidth = (w1 * prev_road.width + w2 * next_road.width) / (w1 + w2);
                finalWidth = finalWidth / 2 * 1.35f;
                if (intersection.roadIds[roadData.id] == EndpointType.Start)
                {
                    Vector3 start = roadData.points[0];
                    int endId = 1;
                    while (endId <= roadData.points.Count - 1)
                    {
                        if (Vector3.Distance(roadData.points[endId], start) > finalWidth * scaleFactor)
                        {
                            break;
                        }
                        endId++;
                    }

                    if (endId != roadData.points.Count)
                    {
                        int tmp = 1;
                        while (tmp < endId)
                        {
                            roadData.points.RemoveAt(1);
                            tmp++;
                        }
                        Vector3 dir = (roadData.points[1] - roadData.points[0]).normalized;
                        Vector3 newPoint = roadData.points[0] + dir * finalWidth * scaleFactor;
                        roadData.points[0] = newPoint;
                    }
                    else
                    {
                        //合并
                        //Debug.Log("需要合并道路:" + roadData.id);暂时注释这个输出
                        if (!faultyRoads.Contains(roadData.id))
                        {
                            faultyRoads.Add(roadData.id);
                        }
                    }
                }
                else
                {
                    int pointCount = roadData.points.Count;
                    Vector3 start = roadData.points[pointCount - 1];
                    int endId = pointCount - 2;
                    while (endId >= 0)
                    {
                        if (Vector3.Distance(roadData.points[endId], start) > finalWidth * scaleFactor)
                        {
                            break;
                        }
                        endId--;
                    }
                    if (endId != -1)
                    {
                        int tmp = pointCount - 2;
                        while (tmp > endId)
                        {
                            roadData.points.RemoveAt(roadData.points.Count - 2);
                            tmp--;
                        }
                        Vector3 dir = (roadData.points[roadData.points.Count - 2] - roadData.points[roadData.points.Count - 1]).normalized;
                        Vector3 newPoint = roadData.points[roadData.points.Count - 1] + dir * finalWidth * scaleFactor;
                        roadData.points[roadData.points.Count - 1] = newPoint;
                    }
                    else
                    {
                        //合并
                        //Debug.Log("需要合并道路:" + roadData.id); 暂时注释这个输出
                        if (!faultyRoads.Contains(roadData.id))
                        {
                            faultyRoads.Add(roadData.id);
                        }
                    }
                }
            }
        }

        public static void ReserveSpace(List<IntersectionInfoData> intersections, Dictionary<int, RoadInfoData> roadDict, float scaleFactor, Terrain terrain, bool changeRoad = false)
        {
            List<int> faultyRoads = new List<int>();
            for (int i = 0; i < intersections.Count; i++)
            {
                IntersectionInfoData intersection = intersections[i];
                CalculateIntersectionSpace(intersection, roadDict, scaleFactor, faultyRoads);
            }

            while (faultyRoads.Count > 0)
            {
                int i = 0;
                List<IntersectionInfoData> sips = intersections.FindAll(a => a.roadIds.ContainsKey(faultyRoads[i]));
                if (sips.Count == 2)
                {
                    int index1 = intersections.IndexOf(sips[0]);
                    int index2 = intersections.IndexOf(sips[1]);
                    IntersectionInfoData merged = MergeTwoIntersections(intersections[index1], intersections[index2], roadDict, terrain, changeRoad);
                    intersections[index1] = merged;
                    intersections.RemoveAt(index2);
                    if (changeRoad)
                    {
                        CalculateIntersectionSpace(intersections[index1], roadDict, scaleFactor, faultyRoads);
                    }
                }
                else if (sips.Count == 1)
                {
                    roadDict.Remove(faultyRoads[i]);
                    sips[0].roadIds.Remove(faultyRoads[i]);
                    if (sips[0].roadIds.Count == 1)
                    {
                        intersections.Remove(sips[0]);
                    }
                    else if (sips[0].roadIds.Count == 2)
                    {
                        List<int> ids = sips[0].roadIds.Keys.ToList();
                        List<IntersectionInfoData> l1 = intersections.FindAll(a => a.roadIds.ContainsKey(ids[0]));
                        List<IntersectionInfoData> l2 = intersections.FindAll(a => a.roadIds.ContainsKey(ids[1]));
                        // 两条路都除了这个路口都不与其他路口相连
                        if (l1.Count == 1 && l2.Count == 1)
                        {
                            RoadInfoData rd1 = roadDict[ids[0]];
                            RoadInfoData rd2 = roadDict[ids[1]];
                            List<Vector3> points = new List<Vector3>();
                            if (sips[0].roadIds[rd1.id] == EndpointType.Start)
                            {
                                rd1.points.Reverse();
                            }
                            points.AddRange(rd1.points);
                            if (sips[0].roadIds[rd2.id] == EndpointType.End)
                            {
                                rd2.points.Reverse();
                            }
                            points.AddRange(rd2.points);
                            float width = rd1.width < rd2.width ? rd1.width : rd2.width;
                            RoadInfoData merged = new RoadInfoData(rd1.id, width, points);
                            roadDict[rd1.id] = merged;
                            roadDict.Remove(rd2.id);
                            intersections.Remove(sips[0]);
                        }
                        // 两条路其中有一条路与其他路口相连
                        else if (l1.Count == 1 && l2.Count == 2)
                        {
                            RoadInfoData rd1 = roadDict[ids[0]];
                            RoadInfoData rd2 = roadDict[ids[1]];
                            List<Vector3> points = new List<Vector3>();
                            points.AddRange(rd2.points);
                            if (sips[0].roadIds[rd2.id] == EndpointType.Start)
                            {
                                if (sips[0].roadIds[rd1.id] == EndpointType.Start)
                                {
                                    rd1.points.Reverse();
                                }
                                points.InsertRange(0, rd1.points);
                            }
                            else
                            {
                                if (sips[0].roadIds[rd1.id] == EndpointType.End)
                                {
                                    rd1.points.Reverse();
                                }
                                points.AddRange(rd1.points);
                            }
                            float width = rd2.width;
                            RoadInfoData merged = new RoadInfoData(rd2.id, width, points);
                            roadDict[rd2.id] = merged;
                            roadDict.Remove(rd1.id);
                            intersections.Remove(sips[0]);
                        }
                        else if (l1.Count == 2 && l2.Count == 1)
                        {
                            RoadInfoData rd1 = roadDict[ids[0]];
                            RoadInfoData rd2 = roadDict[ids[1]];
                            List<Vector3> points = new List<Vector3>();
                            points.AddRange(rd1.points);
                            if (sips[0].roadIds[rd1.id] == EndpointType.Start)
                            {
                                if (sips[0].roadIds[rd2.id] == EndpointType.Start)
                                {
                                    rd2.points.Reverse();
                                }
                                points.InsertRange(0, rd2.points);
                            }
                            else
                            {
                                if (sips[0].roadIds[rd2.id] == EndpointType.End)
                                {
                                    rd2.points.Reverse();
                                }
                                points.AddRange(rd2.points);
                            }
                            float width = rd1.width;
                            RoadInfoData merged = new RoadInfoData(rd1.id, width, points);
                            roadDict[rd1.id] = merged;
                            roadDict.Remove(rd2.id);
                            intersections.Remove(sips[0]);
                        }
                        // 两条路都与其他路口相连
                        else if (l1.Count == 2 && l2.Count == 2)
                        {
                            RoadInfoData rd1 = roadDict[ids[0]];
                            RoadInfoData rd2 = roadDict[ids[1]];
                            List<Vector3> points = new List<Vector3>();
                            if (sips[0].roadIds[rd1.id] == EndpointType.Start)
                            {
                                rd1.points.Reverse();
                            }
                            points.AddRange(rd1.points);
                            if (sips[0].roadIds[rd2.id] == EndpointType.End)
                            {
                                rd2.points.Reverse();
                            }
                            points.AddRange(rd2.points);
                            float width = rd1.width < rd2.width ? rd1.width : rd2.width;
                            RoadInfoData merged = new RoadInfoData(rd1.id, width, points);
                            roadDict[rd1.id] = merged;
                            roadDict.Remove(rd2.id);
                            intersections.Remove(sips[0]);
                            l1.Remove(sips[0]);
                            l2.Remove(sips[0]);
                            l1[0].roadIds[rd1.id] = EndpointType.Start;
                            l2[0].roadIds.Remove(rd2.id);
                            if (!l2[0].roadIds.ContainsKey(rd1.id))
                            {
                                l2[0].roadIds.Add(rd1.id, EndpointType.End);
                            }
                        }
                    }
                }
                faultyRoads.RemoveAt(0);
            }
        }

        public static IntersectionInfoData MergeTwoIntersections(IntersectionInfoData a, IntersectionInfoData b, Dictionary<int, RoadInfoData> roadDict, Terrain terrain, bool changeRoad = false)
        {
            var roadIds1 = a.roadIds;
            var roadIds2 = b.roadIds;
            List<int> rIds1 = roadIds1.Keys.ToList();
            List<int> rIds2 = roadIds2.Keys.ToList();
            var exceptList = rIds1.Except(rIds2).Concat(rIds2.Except(rIds1)).ToList();
            var duplicateList = rIds1.Intersect(rIds2).ToList();
            foreach (var id in duplicateList)
            {
                roadDict.Remove(id);
            }
            Dictionary<int, EndpointType> concatDict = new Dictionary<int, EndpointType>();
            Vector3 centerPoint = (a.point + b.point) / 2;
            if (terrain != null)
            {
                centerPoint.y = terrain.SampleHeight(terrain.transform.TransformPoint(centerPoint));
            }
            foreach (int id in exceptList)
            {
                if (roadIds1.ContainsKey(id))
                {
                    concatDict.Add(id, roadIds1[id]);
                }
                else if (roadIds2.ContainsKey(id))
                {
                    concatDict.Add(id, roadIds2[id]);
                }

                if (changeRoad)
                {
                    if (concatDict[id] == EndpointType.Start)
                    {
                        roadDict[id].points[0] = centerPoint;
                    }
                    else if (concatDict[id] == EndpointType.End)
                    {
                        roadDict[id].points[roadDict[id].points.Count - 1] = centerPoint;
                    }
                }
            }
            int forkNum = exceptList.Count;
            IntersectionInfoData merged = new IntersectionInfoData(centerPoint, forkNum, concatDict);
            return merged;
        }

        public static void GenerateRoadFacility(List<FacilityPoint> points, Transform road)
        {
            GameObject facility = new GameObject("facility");
            facility.transform.parent = road;
            facility.transform.localPosition = Vector3.zero;
            Dictionary<FacilityType, GameObject> prefabDict = new Dictionary<FacilityType, GameObject>();
            foreach (FacilityPoint point in points)
            {
                GameObject prefab;
                if (prefabDict.ContainsKey(point.facilityType))
                {
                    prefab = prefabDict[point.facilityType];
                }
                else
                {
                    prefab = Resources.Load<GameObject>("RoadFacility/" + point.facilityType);
                    prefabDict.Add(point.facilityType, prefab);
                }
                GameObject tmp = GameObject.Instantiate(prefab);
                tmp.transform.parent = facility.transform;
                tmp.transform.localPosition = point.position;
                tmp.transform.eulerAngles = point.euler;
                tmp.transform.localScale = point.scale;
            }
        }

        public static void GenerateRoadFacilityToFile(List<FacilityPoint> points, Transform road, float terrainSize, string savePath)
        {
            // 把 Unity 的 Vector3 转换成可序列化结构
            var serializablePoints = points.ConvertAll(p => new
            {
                facilityType = p.facilityType.ToString(),
                position = new { x = p.position.x, y = p.position.y, z = p.position.z },
                euler = new { x = p.euler.x, y = p.euler.y, z = p.euler.z },
                scale = new { x = p.scale.x, y = p.scale.y, z = p.scale.z }
            });

            string json = JsonConvert.SerializeObject(serializablePoints, Formatting.Indented, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
            File.WriteAllText(savePath, json);
            AssetDatabase.Refresh();


            GameObject facility = new GameObject("facility");
            facility.transform.parent = road;
            facility.transform.localPosition = Vector3.zero;

            TextAsset jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(savePath);
            if (jsonAsset == null)
            {
                Debug.LogError($"❌ 无法加载 TextAsset: {savePath}");
            }
            else
            {
                Debug.Log($"✅ 成功加载 TextAsset: {savePath}");
            }

            //RoadFacilityRenderer roadFacility = facility.AddComponent<RoadFacilityRenderer>();
            RoadFacilityRendererForLOD roadFacility = facility.AddComponent<RoadFacilityRendererForLOD>();
            roadFacility.lodComputeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Packages/com.geoearth.platformsdk/Runtime/GenerateRoadTool/DataRoad/DistanceLOD.compute");
            roadFacility.facilityJson = jsonAsset;
            roadFacility.lodDistanceThreshold = terrainSize;
            roadFacility.maxRenderDistance = terrainSize;

        }

        public static List<Route> CalculateRoutes(List<SplineRoadMeshGenerator> roads, List<SplineIntersectionMeshGenerator> intersections)
        {
            List<Route> routes = new List<Route>();
            List<SplineIntersectionMeshGenerator> tmpIntersections = new List<SplineIntersectionMeshGenerator>(intersections);
            SplineRoadMeshGenerator road = null;
            while (tmpIntersections.Count != 0)
            {
                SplineIntersectionMeshGenerator intersection = tmpIntersections[UnityEngine.Random.Range(0, tmpIntersections.Count)];
                Dictionary<SplineRoadMeshGenerator, EndpointType> tmpRoadDict = new Dictionary<SplineRoadMeshGenerator, EndpointType>();
                for (int i = 0; i < intersection.connectedRoads.Count; i++)
                {
                    tmpRoadDict.Add(intersection.connectedRoads[i], intersection.connectedTypes[i]);
                }
                if (road != null)
                {
                    tmpRoadDict.Remove(road);
                }
                if (tmpRoadDict.Count == 0)
                {
                    break;
                }
                KeyValuePair<SplineRoadMeshGenerator, EndpointType> tmpRoad = tmpRoadDict.ElementAt(Random.Range(0, tmpRoadDict.Count));
                road = tmpRoad.Key;
                List<Lane> lanes = tmpRoad.Value == EndpointType.End ? road.leftLanes : road.rightLanes;
                Lane lane = lanes[UnityEngine.Random.Range(0, lanes.Count)];
                List<Vector3> copy = new List<Vector3>(lane.vertices);
                Route route = new Route(copy, intersection.point);
                routes.Add(route);
                tmpIntersections = new List<SplineIntersectionMeshGenerator>(road.connectedIntersections);
                tmpIntersections.Remove(intersection);
            }
            return routes;
        }

        public static Spline MergeRoute(List<Route> routes)
        {
            List<BezierKnot> bezierKnots = new List<BezierKnot>();
            for (int i = 0; i < routes.Count; i++)
            {
                List<Vector3> verts = routes[i].vertices;
                for (int j = 0; j < verts.Count; j++)
                {
                    BezierKnot bezierKnot = new BezierKnot(verts[j]);
                    bezierKnots.Add(bezierKnot);
                }
                if (i < routes.Count - 1)
                {
                    int count = routes[i].vertices.Count;
                    Vector3 p1 = routes[i].vertices[count - 1];
                    Vector3 p2 = routes[i + 1].vertices[0];
                    Vector3 p3 = routes[i + 1].point;
                    Vector3 v1 = Vector3.Lerp(p1, p2, 0.5f);
                    Vector3 v2 = Vector3.Lerp(v1, p3, 0.5f);
                    BezierKnot bezierKnot = new BezierKnot(v2);
                    bezierKnots.Add(bezierKnot);
                }
            }
            Spline spline = new Spline(bezierKnots);
            spline.SetTangentMode(TangentMode.AutoSmooth);
            return spline;
        }

        public static void GenerateTrafficLanes(List<Spline> lanes, Transform parent)
        {
            GameObject routes = new GameObject("routes");
            routes.transform.parent = parent;
            routes.transform.localPosition = Vector3.zero;
            for (int i = 0; i < lanes.Count; i++)
            {
                Spline lane = lanes[i];
                GameObject route = new GameObject($"route_{i}");
                route.transform.parent = routes.transform;
                route.transform.localPosition = Vector3.zero;
                route.AddComponent<SplineContainer>().Spline = lane;
            }
        }
    }
}
#endif