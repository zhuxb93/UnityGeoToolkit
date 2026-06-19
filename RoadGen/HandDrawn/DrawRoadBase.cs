using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace GeoToolkit.DrawRoad
{
    public enum NodeType
    {
        Road,
        Intersection
    }

    [Serializable]
    public class CurveNode
    {
        public NodeType nodeType;
        public Vector3 point;

        public CurveNode(NodeType nodeType, Vector3 point)
        {
            this.nodeType = nodeType;
            this.point = point;
        }
    }

    [Serializable]
    public class DrawRoadCurve
    {
        public int id;
        public List<CurveNode> curveNodes;
        public RoadInfoConfig roadInfo;

        public DrawRoadCurve(int id, List<CurveNode> curveNodes, RoadInfoConfig roadInfo)
        {
            this.id = id;
            this.curveNodes = curveNodes;
            this.roadInfo = roadInfo;
        }
    }

    [Serializable]
    public class DrawIntersection
    {
        public int id;
        public CurveNode node;
        //public List<int> roadIds;

        public DrawIntersection(int id, CurveNode node)
        {
            this.id = id;
            this.node = node;
        }

        //public void SetRoadIds(List<int> roadIds)
        //{
        //    this.roadIds = roadIds;
        //}
    }

    public class DrawRoadBase : MonoBehaviour
    {
        public DrawRoadDataConfig roadConfig;
        public string name;

        //public List<Vector3> roadPoints;
        public List<DrawRoadCurve> roadCurves;
        public List<DrawIntersection> intersections;

#if UNITY_EDITOR
        [MenuItem("GeoToolkit/数字孪生道路/创建手绘道路", false, 104)]
        public static void CreateRoadDrawer()
        {
            GameObject roadNetwork = new GameObject("Road Network");
            DrawRoadBase road = roadNetwork.AddComponent<DrawRoadBase>();
            Selection.activeGameObject = roadNetwork;
        }
#endif
    }
}