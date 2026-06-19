using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeoToolkit.DrawRoad
{
    [Serializable]
    public class RoadInfoConfig
    {
        public int id;
        public string name;
        public string description;
        public float width;
        public Material material;
    }

    [Serializable]
    public class IntersectionMatConfig
    {
        public Material intersectionMat;
        public Material crossingMat;
    }

    [Serializable]
    public class GenericMatConfig
    {
        public Material kerbMat;
        public Material sidewalkMat;
        public Material concreteMat;
    }

    [Serializable]
    public class RailwayMatConfig
    {
        public Material railwayMat;
    }

    [Serializable]
    public struct DrawRoadParameterConfig
    {
        public float kerbWidth;
        public float sidewalkWidth;
        public float subgradeHeight;
    }

    [CreateAssetMenu(fileName = "RoadConfig", menuName = "Scriptable Objects/RoadConfig")]
    public class DrawRoadDataConfig : ScriptableObject
    {
        public List<RoadInfoConfig> roadInfoConfig;
        public IntersectionMatConfig intersectionMatConfig;
        public GenericMatConfig genericMatConfig;
        public RailwayMatConfig railwayMatConfig;
        public DrawRoadParameterConfig parameterConfig;
    }
}