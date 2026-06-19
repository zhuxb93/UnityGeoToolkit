using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeoDCBuildTools
{
    [Serializable]
    public class RoadMatConfig
    {
        public int laneCount;
        public Material laneMat;
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
    public struct RoadParameterConfig
    {
        public float kerbWidth;
        public float sidewalkWidth;
        public float subgradeHeight;
        public float minRoadWidth;
        public float maxRoadWidth;
    }

    [CreateAssetMenu(fileName = "RoadDataConfig", menuName = "Scriptable Objects/RoadDataConfig")]
    public class RoadDataConfig : ScriptableObject
    {
        public List<RoadMatConfig> roadMatConfig;
        public IntersectionMatConfig intersectionMatConfig;
        public GenericMatConfig genericMatConfig;
        public RailwayMatConfig railwayMatConfig;
        public RoadParameterConfig roadParameterConfig;
    }
}