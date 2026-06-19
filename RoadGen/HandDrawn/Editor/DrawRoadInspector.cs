#if UNITY_EDITOR
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace GeoToolkit.DrawRoad
{
    [CustomEditor(typeof(DrawRoadBase))]
    public class DrawRoadInspector : Editor
    {
        public bool isInit = false;
        public string initBtnTxt = "Start Draw";
        public int activeRoad = -1;
        public Texture2D lineAATex;
        public int currentRoadId = -1;
        public int currentNodeId = -1;
        public CurveNode lastClickPoint;
        public DrawRoadCurve lastRoadCurve;
        public DrawIntersection lastIntersection;

        public static int curveDrawResolution = 64;
        public static Vector3[] curveDrawingBuffer = new Vector3[curveDrawResolution + 1];

        public DrawRoadBase drawRoad;

        private void OnEnable()
        {
            drawRoad = target as DrawRoadBase;
            ResetRoadData();
            lineAATex = Resources.Load<Texture2D>("TangentLineAATex");
            lastClickPoint = null;
        }

        public void ResetRoadData()
        {
            if (drawRoad.roadCurves == null)
                return;
            for (int i = 0; i < drawRoad.roadCurves.Count; i++)
            {
                DrawRoadCurve roadCurve = drawRoad.roadCurves[i];
                List<CurveNode> curveNodes = roadCurve.curveNodes;
                for (int j = 0; j < curveNodes.Count; j++)
                {
                    if (curveNodes[j].nodeType == NodeType.Intersection)
                    {
                        DrawIntersection intersection = drawRoad.intersections.Where(val => val.node.point == curveNodes[j].point).FirstOrDefault();
                        curveNodes[j] = intersection.node;
                    }
                }
            }
        }

        private void OnSceneGUI()
        {
            Update();
            UpdateSelection();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            initBtnTxt = isInit ? "退出绘制" : "开始绘制";
            if (GUILayout.Button(initBtnTxt))
            {
                isInit = !isInit;
                if (!isInit)
                {
                    lastClickPoint = null;
                    lastRoadCurve = null;
                    lastIntersection = null;
                }
            }
            if (isInit)
            {
                string[] content = { "两车道", "四车道", "六车道", "八车道", "十车道" };
                activeRoad = GUILayout.Toolbar(activeRoad, content);
                if (GUILayout.Button("生成道路"))
                {
                    Debug.LogError("生成道路");
                    Dictionary<int, RoadInfoData> roadDict = new Dictionary<int, RoadInfoData>();
                    List<IntersectionInfoData> intersections = new List<IntersectionInfoData>();
                    for (int i = 0; i < drawRoad.roadCurves.Count; i++)
                    {
                        DrawRoadCurve road = drawRoad.roadCurves[i];
                        List<Vector3> points = road.curveNodes.Select(i => i.point).ToList();
                        RoadInfoData roadData = new RoadInfoData(road.id, road.roadInfo.width, points, road.roadInfo.material);
                        roadDict.Add(roadData.id, roadData);
                    }
                    for (int i = 0; i < drawRoad.intersections.Count; i++)
                    {
                        int id = drawRoad.intersections[i].id;
                        Vector3 point = drawRoad.intersections[i].node.point;
                        Dictionary<int, EndpointType> connectStates = new Dictionary<int, EndpointType>();
                        int forkNum = 0;
                        foreach (var road in roadDict.Values)
                        {
                            if (road.points.First() == point)
                            {
                                forkNum++;
                                connectStates.Add(road.id, EndpointType.Start);
                            }
                            else if (road.points.Last() == point)
                            {
                                forkNum++;
                                connectStates.Add(road.id, EndpointType.End);
                            }
                        }
                        IntersectionInfoData intersection = new IntersectionInfoData(id, point, forkNum, connectStates);
                        intersections.Add(intersection);
                    }
                    RoadDataInfo roadDataInfo = DrawRoadCreator.CalculateRoadMeshData(roadDict, intersections, drawRoad.roadConfig);

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

                    string basePath = "Assets/Geo/roadModel";
                    if (!Directory.Exists(basePath))
                    {
                        Directory.CreateDirectory(basePath);
                    }
                    string path = $"{basePath}/{drawRoad.name}-roadMesh.asset";
                    AssetDatabase.CreateAsset(roadMesh, path);
                    AssetDatabase.Refresh();

                    GameObject roadNetwork = new GameObject($"road-{drawRoad.name}");
                    roadNetwork.AddComponent<MeshFilter>().mesh = roadMesh;
                    roadNetwork.AddComponent<MeshRenderer>().materials = roadDataInfo.roadMatDict.Keys.ToArray();
                    roadNetwork.AddComponent<MeshCollider>();

                    RoadFacilitySaveInfo roadFacilitySaveInfo = new RoadFacilitySaveInfo();
                    roadFacilitySaveInfo.tileId = drawRoad.name;
                    roadFacilitySaveInfo.facilityMarkPoints = roadDataInfo.facilityMarkPoints;
                    string saveInfo = JsonConvert.SerializeObject(roadFacilitySaveInfo);
                    RoadFacilityData roadFacilityData = DrawRoadCreator.AnalysisRoadFacilityData(saveInfo);
                    DrawRoadCreator.GenerateRoadFacility(roadFacilityData.facilityMarkPoints, roadNetwork.transform);
                    //DrawRoadCreator.GenerateTrafficLanes(roadDataInfo.lanes, roadNetwork.transform);
                }
            }
            else
            {
                activeRoad = -1;
            }
        }

        private void Update()
        {
            if (!isInit)
                return;
            DrawRoadCurves();
            if (activeRoad == -1)
                return;
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Delete)
            {
                Event.current.Use();
                if (lastClickPoint != null)
                {
                    if (lastClickPoint.nodeType == NodeType.Intersection)
                    {
                        List<DrawRoadCurve> roadCurves = drawRoad.roadCurves.Where(x => x.curveNodes.Contains(lastClickPoint)).ToList();
                        for (int i = 0; i < roadCurves.Count; i++)
                        {
                            DrawRoadCurve roadCurve = roadCurves[i];
                            roadCurve.curveNodes.Remove(lastClickPoint);
                            if (roadCurve.curveNodes.Count == 0 || roadCurve.curveNodes.Count == 1)
                            {
                                drawRoad.roadCurves.Remove(roadCurve);
                            }
                        }
                        DrawIntersection intersection = drawRoad.intersections.Where(x => x.node == lastClickPoint).First();
                        drawRoad.intersections.Remove(intersection);
                    }
                    else if (lastClickPoint.nodeType == NodeType.Road)
                    {
                        DrawRoadCurve roadCurve = drawRoad.roadCurves.Where(x => x.curveNodes.Contains(lastClickPoint)).FirstOrDefault();
                        roadCurve.curveNodes.Remove(lastClickPoint);
                        if (roadCurve.curveNodes.Count == 0 || roadCurve.curveNodes.Count == 1)
                        {
                            drawRoad.roadCurves.Remove(roadCurve);
                        }
                    }
                }
            }
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                if (Physics.Raycast(ray, out var hit))
                {
                    RoadInfoConfig roadInfo = drawRoad.roadConfig.roadInfoConfig.Where(i => i.id == activeRoad).First();
                    if (VerifyPointIntersection(hit.point, out DrawIntersection intersection))
                    {
                        if (!Event.current.control)
                        {
                            Debug.LogError("点击交点");
                            lastClickPoint = intersection.node;
                            lastRoadCurve = null;
                            lastIntersection = intersection;
                        }
                        else
                        {
                            if (lastClickPoint.nodeType == NodeType.Intersection && lastIntersection != null)
                            {
                                Debug.LogError("点击交点，上一个点是交点");
                                List<CurveNode> nodes = new List<CurveNode>() { lastClickPoint, intersection.node };
                                DrawRoadCurve roadCurve = new DrawRoadCurve(drawRoad.roadCurves.Count, nodes, roadInfo);
                                drawRoad.roadCurves.Add(roadCurve);
                                lastClickPoint = intersection.node;
                                lastRoadCurve = null;
                                lastIntersection = intersection;
                            }
                            else if (lastClickPoint.nodeType == NodeType.Road && lastRoadCurve != null)
                            {
                                if (lastClickPoint == lastRoadCurve.curveNodes.First())
                                {
                                    Debug.LogError("点击交点，上一个点是道路起点");
                                    lastRoadCurve.curveNodes.Insert(0, intersection.node);
                                }
                                else if (lastClickPoint == lastRoadCurve.curveNodes.Last())
                                {
                                    Debug.LogError("点击交点，上一个点是道路终点");
                                    lastRoadCurve.curveNodes.Add(intersection.node);
                                }
                                else
                                {
                                    Debug.LogError("点击交点，上一个点是道路中间点");
                                    List<DrawRoadCurve> seperated = SeparateRoadCurve(lastClickPoint, lastRoadCurve);
                                    lastClickPoint.nodeType = NodeType.Intersection;
                                    drawRoad.roadCurves.Remove(lastRoadCurve);
                                    drawRoad.roadCurves.AddRange(seperated);
                                    List<CurveNode> nodes = new List<CurveNode> { lastClickPoint, intersection.node };
                                    DrawRoadCurve roadCurve = new DrawRoadCurve(drawRoad.roadCurves.Count, nodes, roadInfo);
                                    drawRoad.roadCurves.Add(roadCurve);
                                    DrawIntersection intersection1 = new DrawIntersection(drawRoad.intersections.Count, lastClickPoint);
                                    drawRoad.intersections.Add(intersection1);
                                }
                                lastClickPoint = intersection.node;
                                lastRoadCurve = null;
                                lastIntersection = intersection;
                            }
                        }

                    }
                    else if (VerifyPointRoad(hit.point, out DrawRoadCurve road, out int nodeIndex))
                    {
                        CurveNode currentNode = road.curveNodes[nodeIndex];
                        if (!Event.current.control)
                        {
                            Debug.LogError("点击道路点");
                            lastClickPoint = currentNode;
                            lastRoadCurve = road;
                            lastIntersection = null;
                        }
                        else
                        {
                            if (lastClickPoint.nodeType == NodeType.Intersection && lastIntersection != null)
                            {
                                if (currentNode == road.curveNodes.First())
                                {
                                    Debug.LogError("点击道路起点，上一个点是交点");
                                    road.curveNodes.Insert(0, lastClickPoint);
                                    lastClickPoint = currentNode;
                                    lastRoadCurve = road;
                                    lastIntersection = null;
                                }
                                else if (currentNode == road.curveNodes.Last())
                                {
                                    Debug.LogError("点击道路终点，上一个点是交点");
                                    road.curveNodes.Add(lastClickPoint);
                                    lastClickPoint = currentNode;
                                    lastRoadCurve = road;
                                    lastIntersection = null;
                                }
                                else
                                {
                                    Debug.LogError("点击道路中间点，上一个点是交点");
                                    List<DrawRoadCurve> seperated = SeparateRoadCurve(currentNode, road);
                                    currentNode.nodeType = NodeType.Intersection;
                                    drawRoad.roadCurves.Remove(road);
                                    drawRoad.roadCurves.AddRange(seperated);
                                    List<CurveNode> nodes = new List<CurveNode>() { lastClickPoint, currentNode };
                                    DrawRoadCurve roadCurve = new DrawRoadCurve(drawRoad.roadCurves.Count, nodes, roadInfo);
                                    drawRoad.roadCurves.Add(roadCurve);
                                    DrawIntersection intersection1 = new DrawIntersection(drawRoad.intersections.Count, currentNode);
                                    drawRoad.intersections.Add(intersection1);
                                    lastClickPoint = currentNode;
                                    lastRoadCurve = null;
                                    lastIntersection = intersection1;
                                }
                            }
                            else if (lastClickPoint.nodeType == NodeType.Road && lastRoadCurve != null)
                            {
                                if (road == lastRoadCurve)
                                {
                                    List<int> lastIndex = road.curveNodes.Select((value, index) => new { value, index }).Where(g => g.value == lastClickPoint).Select(g => g.index).ToList();
                                    int currentIndex = road.curveNodes.IndexOf(currentNode);
                                    if (lastIndex.Where(i => Mathf.Abs(i - currentIndex) == 1).ToList().Count > 0)
                                    {
                                        Debug.LogError("两点相邻");
                                        return;
                                    }
                                    if (currentNode == road.curveNodes.First())
                                    {
                                        if (lastClickPoint == lastRoadCurve.curveNodes.Last())
                                        {
                                            Debug.LogError("点击道路起点，上一个点是同条道路上的终点");
                                            road.curveNodes.Add(currentNode);
                                            lastClickPoint = currentNode;
                                            lastRoadCurve = road;
                                            lastIntersection = null;
                                        }
                                        else
                                        {
                                            Debug.LogError("点击道路起点，上一个点是同条道路上的中间点");
                                            List<DrawRoadCurve> seperated = SeparateRoadCurve(lastClickPoint, road);
                                            lastClickPoint.nodeType = NodeType.Intersection;
                                            seperated[0].curveNodes.Add(currentNode);
                                            drawRoad.roadCurves.Remove(road);
                                            drawRoad.roadCurves.AddRange(seperated);
                                            DrawIntersection intersection1 = new DrawIntersection(drawRoad.intersections.Count, lastClickPoint);
                                            drawRoad.intersections.Add(intersection1);
                                            lastClickPoint = seperated[0].curveNodes.Last();
                                            lastRoadCurve = road;
                                            lastIntersection = null;
                                        }
                                    }
                                    else if (currentNode == road.curveNodes.Last())
                                    {
                                        if (lastClickPoint == lastRoadCurve.curveNodes.First())
                                        {
                                            Debug.LogError("点击道路终点，上一个点是同条道路上的起点");
                                            road.curveNodes.Insert(0, currentNode);
                                            lastClickPoint = currentNode;
                                            lastRoadCurve = road;
                                            lastIntersection = null;
                                        }
                                        else
                                        {
                                            Debug.LogError("点击道路终点，上一个点是同条道路上的中间点");
                                            List<DrawRoadCurve> seperated = SeparateRoadCurve(lastClickPoint, road);
                                            lastClickPoint.nodeType = NodeType.Intersection;
                                            seperated[1].curveNodes.Insert(0, currentNode);
                                            drawRoad.roadCurves.Remove(road);
                                            drawRoad.roadCurves.AddRange(seperated);
                                            DrawIntersection intersection1 = new DrawIntersection(drawRoad.intersections.Count, lastClickPoint);
                                            drawRoad.intersections.Add(intersection1);
                                            lastClickPoint = currentNode;
                                            lastRoadCurve = road;
                                            lastIntersection = null;
                                        }
                                    }
                                    else
                                    {
                                        List<DrawRoadCurve> separated = SeparateRoadCurve(currentNode, lastRoadCurve);
                                        currentNode.nodeType = NodeType.Intersection;
                                        drawRoad.roadCurves.Remove(road);
                                        drawRoad.roadCurves.AddRange(separated);
                                        DrawIntersection intersection1 = new DrawIntersection(drawRoad.intersections.Count, currentNode);
                                        drawRoad.intersections.Add(intersection1);
                                        if (lastClickPoint == lastRoadCurve.curveNodes.First())
                                        {
                                            Debug.LogError("点击道路中间点，上一个点是同条道路上的起点");
                                            separated[0].curveNodes.Insert(0, currentNode);
                                        }
                                        else if (lastClickPoint == lastRoadCurve.curveNodes.Last())
                                        {
                                            Debug.LogError("点击道路中间点，上一个点是同条道路上的终点");
                                            separated[1].curveNodes.Add(currentNode);
                                        }
                                        else
                                        {
                                            int index1 = road.curveNodes.IndexOf(lastClickPoint);
                                            int index2 = road.curveNodes.IndexOf(currentNode);
                                            if (index1 > index2)
                                            {
                                                Debug.LogError("点击道路中间点，上一个点是同条道路上的中间点，上个点id大于这个点");
                                                List<DrawRoadCurve> separated1 = SeparateRoadCurve(lastClickPoint, separated[1]);
                                                lastClickPoint.nodeType = NodeType.Intersection;
                                                drawRoad.roadCurves.Remove(separated[1]);
                                                drawRoad.roadCurves.AddRange(separated1);
                                                List<CurveNode> nodes = new List<CurveNode> { lastClickPoint, currentNode };
                                                DrawRoadCurve roadCurve = new DrawRoadCurve(drawRoad.roadCurves.Count, nodes, roadInfo);
                                                drawRoad.roadCurves.Add(roadCurve);
                                                DrawIntersection intersection2 = new DrawIntersection(drawRoad.intersections.Count, lastClickPoint);
                                                drawRoad.intersections.Add(intersection2);
                                            }
                                            else
                                            {
                                                Debug.LogError("点击道路中间点，上一个点是同条道路上的中间点，上个点id小于这个点");
                                                List<DrawRoadCurve> separated1 = SeparateRoadCurve(lastClickPoint, separated[0]);
                                                lastClickPoint.nodeType = NodeType.Intersection;
                                                drawRoad.roadCurves.Remove(separated[0]);
                                                drawRoad.roadCurves.AddRange(separated1);
                                                List<CurveNode> nodes = new List<CurveNode> { lastClickPoint, currentNode };
                                                DrawRoadCurve roadCurve = new DrawRoadCurve(drawRoad.roadCurves.Count, nodes, roadInfo);
                                                drawRoad.roadCurves.Add(roadCurve);
                                                DrawIntersection intersection2 = new DrawIntersection(drawRoad.intersections.Count, lastClickPoint);
                                                drawRoad.intersections.Add(intersection2);
                                            }
                                        }
                                        lastClickPoint = currentNode;
                                        lastRoadCurve = null;
                                        lastIntersection = intersection1;
                                    }
                                }
                                else
                                {
                                    if (currentNode == road.curveNodes.First())
                                    {
                                        if (lastClickPoint == lastRoadCurve.curveNodes.First())
                                        {
                                            Debug.LogError("点击道路起点，上一个点是不同道路上的起点");
                                            var reversed = lastRoadCurve.curveNodes.AsEnumerable().Reverse().ToList();
                                            road.curveNodes.InsertRange(0, reversed);
                                            drawRoad.roadCurves.Remove(lastRoadCurve);
                                        }
                                        else if (lastClickPoint == lastRoadCurve.curveNodes.Last())
                                        {
                                            Debug.LogError("点击道路起点，上一个点是不同道路上的终点");
                                            road.curveNodes.InsertRange(0, lastRoadCurve.curveNodes);
                                            drawRoad.roadCurves.Remove(lastRoadCurve);
                                        }
                                        else
                                        {
                                            Debug.LogError("点击道路起点，上一个点是不同道路上的中间点");
                                            List<DrawRoadCurve> seperated = SeparateRoadCurve(lastClickPoint, lastRoadCurve);
                                            lastClickPoint.nodeType = NodeType.Intersection;
                                            drawRoad.roadCurves.Remove(lastRoadCurve);
                                            drawRoad.roadCurves.AddRange(seperated);
                                            road.curveNodes.Insert(0, lastClickPoint);
                                            DrawIntersection intersection1 = new DrawIntersection(drawRoad.intersections.Count, lastClickPoint);
                                            drawRoad.intersections.Add(intersection1);
                                        }
                                        lastClickPoint = currentNode;
                                        lastRoadCurve = road;
                                        lastIntersection = null;
                                    }
                                    else if (currentNode == road.curveNodes.Last())
                                    {
                                        if (lastClickPoint == lastRoadCurve.curveNodes.Last())
                                        {
                                            Debug.LogError("点击道路终点，上一个点是不同道路上的终点");
                                            var reversed = lastRoadCurve.curveNodes.AsEnumerable().Reverse().ToList();
                                            road.curveNodes.AddRange(reversed);
                                            drawRoad.roadCurves.Remove(lastRoadCurve);
                                        }
                                        else if (lastClickPoint == lastRoadCurve.curveNodes.First())
                                        {
                                            Debug.LogError("点击道路终点，上一个点是不同道路上的起点");
                                            road.curveNodes.AddRange(lastRoadCurve.curveNodes);
                                            drawRoad.roadCurves.Remove(lastRoadCurve);
                                        }
                                        else
                                        {
                                            Debug.LogError("点击道路终点，上一个点是不同道路上的中间点");
                                            List<DrawRoadCurve> seperated = SeparateRoadCurve(lastClickPoint, lastRoadCurve);
                                            lastClickPoint.nodeType = NodeType.Intersection;
                                            drawRoad.roadCurves.Remove(lastRoadCurve);
                                            drawRoad.roadCurves.AddRange(seperated);
                                            road.curveNodes.Add(lastClickPoint);
                                            DrawIntersection intersection1 = new DrawIntersection(drawRoad.intersections.Count, lastClickPoint);
                                            drawRoad.intersections.Add(intersection1);
                                        }
                                        lastClickPoint = currentNode;
                                        lastRoadCurve = road;
                                        lastIntersection = null;
                                    }
                                    else
                                    {
                                        List<DrawRoadCurve> separated = SeparateRoadCurve(currentNode, road);
                                        currentNode.nodeType = NodeType.Intersection;
                                        drawRoad.roadCurves.Remove(road);
                                        drawRoad.roadCurves.AddRange(separated);
                                        DrawIntersection intersection1 = new DrawIntersection(drawRoad.intersections.Count, currentNode);
                                        drawRoad.intersections.Add(intersection1);
                                        if (lastClickPoint == lastRoadCurve.curveNodes.First())
                                        {
                                            Debug.LogError("点击道路中间点，上一个点是不同道路上的起点");
                                            lastRoadCurve.curveNodes.Insert(0, currentNode);
                                        }
                                        else if (lastClickPoint == lastRoadCurve.curveNodes.Last())
                                        {
                                            Debug.LogError("点击道路中间点，上一个点是不同道路上的终点");
                                            lastRoadCurve.curveNodes.Add(currentNode);
                                        }
                                        else
                                        {
                                            Debug.LogError("点击道路中间点，上一个点是不同道路上的中间点");
                                            List<DrawRoadCurve> seperated = SeparateRoadCurve(lastClickPoint, lastRoadCurve);
                                            lastClickPoint.nodeType = NodeType.Intersection;
                                            drawRoad.roadCurves.Remove(lastRoadCurve);
                                            drawRoad.roadCurves.AddRange(seperated);
                                            List<CurveNode> nodes = new List<CurveNode> { lastClickPoint, currentNode };
                                            DrawRoadCurve roadCurve = new DrawRoadCurve(drawRoad.roadCurves.Count, nodes, roadInfo);
                                            drawRoad.roadCurves.Add(roadCurve);
                                            DrawIntersection intersection2 = new DrawIntersection(drawRoad.intersections.Count, lastClickPoint);
                                            drawRoad.intersections.Add(intersection2);
                                        }
                                        lastClickPoint = currentNode;
                                        lastRoadCurve = null;
                                        lastIntersection = intersection1;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (!Event.current.control)
                        {
                            lastClickPoint = null;
                            lastRoadCurve = null;
                            lastIntersection = null;
                            return;
                        }
                        if (lastClickPoint != null)
                        {
                            if (lastClickPoint.nodeType == NodeType.Intersection)
                            {
                                Debug.LogError("创建新的道路点，上一个点是交点");
                                CurveNode node = new CurveNode(NodeType.Road, hit.point);
                                List<CurveNode> nodes = new List<CurveNode>() { lastClickPoint, node };
                                DrawRoadCurve roadCurve = new DrawRoadCurve(drawRoad.roadCurves.Count, nodes, roadInfo);
                                drawRoad.roadCurves.Add(roadCurve);
                                lastClickPoint = node;
                                lastRoadCurve = roadCurve;
                            }
                            else if (lastClickPoint.nodeType == NodeType.Road)
                            {
                                if (lastClickPoint == lastRoadCurve.curveNodes.Last())
                                {
                                    Debug.LogError("创建新的道路点，上一个点是道路终点");
                                    CurveNode node = new CurveNode(NodeType.Road, hit.point);
                                    lastRoadCurve.curveNodes.Add(node);
                                    lastClickPoint = node;
                                }
                                else if (lastClickPoint == lastRoadCurve.curveNodes.First())
                                {
                                    Debug.LogError("创建新的道路点，上一个点是道路起点");
                                    CurveNode node = new CurveNode(NodeType.Road, hit.point);
                                    lastRoadCurve.curveNodes.Insert(0, node);
                                    lastClickPoint = node;
                                }
                                else
                                {
                                    Debug.LogError("创建新的道路点，上一个点是道路中间点");
                                    List<DrawRoadCurve> separated = SeparateRoadCurve(lastClickPoint, lastRoadCurve);
                                    lastClickPoint.nodeType = NodeType.Intersection;
                                    drawRoad.roadCurves.Remove(lastRoadCurve);
                                    drawRoad.roadCurves.AddRange(separated);
                                    CurveNode node = new CurveNode(NodeType.Road, hit.point);
                                    List<CurveNode> nodes = new List<CurveNode>() { lastClickPoint, node };
                                    DrawRoadCurve roadCurve = new DrawRoadCurve(drawRoad.roadCurves.Count, nodes, roadInfo);
                                    drawRoad.roadCurves.Add(roadCurve);
                                    DrawIntersection intersection1 = new DrawIntersection(drawRoad.intersections.Count, lastClickPoint);
                                    drawRoad.intersections.Add(intersection1);
                                    lastClickPoint = node;
                                    lastRoadCurve = roadCurve;
                                }
                            }
                        }
                        else
                        {
                            Debug.LogError("创建新的道路点");
                            CurveNode node = new CurveNode(NodeType.Road, hit.point);
                            DrawRoadCurve roadCurve = new DrawRoadCurve(drawRoad.roadCurves.Count, new List<CurveNode> { node }, roadInfo);
                            lastClickPoint = node;
                            lastRoadCurve = roadCurve;
                            drawRoad.roadCurves.Add(roadCurve);
                        }
                    }
                }
            }
        }

        private bool VerifyIntersectionAdjacent(CurveNode roadNode, CurveNode intersectionNode)
        {
            return false;
        }

        private void UpdateSelection()
        {
            if (activeRoad == -1)
                return;
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                GUIUtility.hotControl = 0;
                Event.current.Use();
                Selection.activeGameObject = drawRoad.gameObject;
            }
        }

        private void DrawRoadCurves()
        {
            foreach (DrawRoadCurve item in drawRoad.roadCurves)
            {
                List<BezierKnot> bezierKnots = new List<BezierKnot>();
                List<CurveNode> roadNodes = item.curveNodes;
                for (int i = 0; i < roadNodes.Count; i++)
                {
                    Vector3 point = roadNodes[i].point;
                    BezierKnot knot = new BezierKnot(point);
                    bezierKnots.Add(knot);
                }
                Spline spline = new Spline(bezierKnots);
                spline.SetTangentMode(TangentMode.AutoSmooth);
                for (int i = 0; i < spline.GetCurveCount(); i++)
                {
                    BezierCurve curve = spline.GetCurve(i);
                    ComputeCurveBuffer(curve, curveDrawingBuffer);
                    Handles.color = Color.white;
                    Handles.DrawAAPolyLine(lineAATex, 5, curveDrawingBuffer);
                }
                for (int i = 0; i < roadNodes.Count; i++)
                {
                    Handles.color = Color.white;
                    if (roadNodes[i] == lastClickPoint)
                    {
                        Handles.color = Color.blue;
                        Vector3 targetPos = Handles.PositionHandle(roadNodes[i].point, Quaternion.identity);
                        roadNodes[i].point = targetPos;
                    }
                    Handles.SphereHandleCap(0, roadNodes[i].point, Quaternion.identity, HandleUtility.GetHandleSize(roadNodes[i].point) / 6, EventType.Repaint);
                }
            }
        }

        private void ComputeCurveBuffer(BezierCurve curve, Vector3[] buffer)
        {
            float segmentPercentage = 1f / curveDrawResolution;
            for (int i = 0; i <= curveDrawResolution; ++i)
            {
                buffer[i] = CurveUtility.EvaluatePosition(curve, i * segmentPercentage);
            }
        }

        private bool VerifyPointIntersection(Vector3 point, out DrawIntersection intersection)
        {
            if (drawRoad.intersections != null && drawRoad.intersections.Count > 0)
            {
                for (int i = 0; i < drawRoad.intersections.Count; i++)
                {
                    DrawIntersection drawIntersection = drawRoad.intersections[i];
                    if (Vector3.Distance(point, drawIntersection.node.point) <= HandleUtility.GetHandleSize(drawIntersection.node.point) / 6)
                    {
                        intersection = drawIntersection;
                        return true;
                    }
                }
            }
            intersection = null;
            return false;
        }

        private bool VerifyPointRoad(Vector3 point, out DrawRoadCurve road, out int nodeIndex)
        {
            if (drawRoad.roadCurves != null && drawRoad.roadCurves.Count > 0)
            {
                foreach (var roadCurve in drawRoad.roadCurves)
                {
                    List<Vector3> points = roadCurve.curveNodes.Select(i => i.point).ToList();
                    for (int i = 0; i < points.Count; i++)
                    {
                        if (Vector3.Distance(points[i], point) <= HandleUtility.GetHandleSize(points[i]) / 6)
                        {
                            road = roadCurve;
                            nodeIndex = i;
                            return true;
                        }
                    }
                }
            }
            road = null;
            nodeIndex = -1;
            return false;
        }

        private List<DrawRoadCurve> SeparateRoadCurve(CurveNode node, DrawRoadCurve roadCurve)
        {
            List<CurveNode> nodes = roadCurve.curveNodes;
            if (node == nodes.First() || node == nodes.Last())
                return null;
            int nodeIndex = nodes.IndexOf(node);
            List<CurveNode> part1 = nodes.GetRange(0, nodeIndex + 1);
            DrawRoadCurve roadCurve1 = new DrawRoadCurve(roadCurve.id, part1, roadCurve.roadInfo);
            List<CurveNode> part2 = nodes.GetRange(nodeIndex, nodes.Count - nodeIndex);
            DrawRoadCurve roadCurve2 = new DrawRoadCurve(drawRoad.roadCurves.Count, part2, roadCurve.roadInfo);
            return new List<DrawRoadCurve> { roadCurve1, roadCurve2 };
        }

        private void FocusSceneView()
        {
            SceneView scene = SceneView.sceneViews[0] as SceneView;
            scene.Focus();
        }

        [MenuItem("Test/test")]
        public static void Test1()
        {
            //List<int> nodes = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            //int id = nodes.IndexOf(4);
            //List<int> part1 = nodes.GetRange(0, id + 1);
            //List<int> part2 = nodes.GetRange(id, nodes.Count - id);
            //Debug.LogError(string.Join(", ", part1));
            //Debug.LogError(string.Join(", ", part2));

            //List<int> list = new List<int> { 1, 2, 3, 4, 2, 5, 6, 1 };
            //var duplicates = list.GroupBy(x => x)
            //                     .SelectMany(g => g.Skip(1))
            //                     .Distinct()
            //                     .ToList();
            //Debug.LogError("重复元素: " + string.Join(", ", duplicates));

            List<int> list = new List<int> { 1, 2, 3, 4, 2, 5, 6, 1 };
            var duplicates = list
                .Select((value, index) => new { value, index })
                .GroupBy(x => x.value)
                .Where(g => g.Count() > 1)
                .Select(g => new { value = g.Key, indices = g.Select(x => x.index).ToList() });

            //foreach (var dup in duplicates)
            //{
            //    Debug.LogError($"元素 {dup.value} 的索引是：{string.Join(", ", dup.indices)}");
            //}

            var x = list.Select((value, index) => new { value, index });
            foreach (var item in x)
            {
                Debug.LogError("value:" + item.value + "  index:" + item.index);
            }
        }
    }
}
#endif
