using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Splines;

namespace GeoToolkit.DrawRoad
{
    #region ��ʼ���������
    public enum EndpointType
    {
        Start,
        End
    }

    public class RoadInfoData
    {
        public int id;
        public float width;
        public List<Vector3> points;
        public DrawRoadMeshGenerator roadMeshGenerator;
        public Material roadMat;

        public RoadInfoData(int id, float width, List<Vector3> points, Material roadMat)
        {
            this.id = id;
            this.width = width;
            this.points = points;
            this.roadMat = roadMat;
        }
    }

    public class IntersectionInfoData
    {
        public int id;
        public Vector3 point;
        public int forkNum;
        public Dictionary<int, EndpointType> roadIds;


        public IntersectionInfoData(int id, Vector3 point, int forkNum, Dictionary<int, EndpointType> roadIds)
        {
            this.id = id;
            this.point = point;
            this.forkNum = forkNum;
            this.roadIds = roadIds;
        }
    }

    public class RailwayData
    {
        public string id;
        public List<Vector3> points;

        public RailwayData(string id, List<Vector3> points)
        {
            this.id = id;
            this.points = points;
        }
    }
    #endregion

    #region ���ɿɱ༭���
    [Serializable]
    public class JunctionPoint
    {
        public Vector3 point;
        public Vector3 normal;
        public Vector3 expand;

        public JunctionPoint(Vector3 point, Vector3 normal, Vector3 expand)
        {
            this.point = point;
            this.normal = normal;
            this.expand = expand;
        }
    }

    [Serializable]
    public class JunctionOriginPoint : JunctionPoint
    {
        public Vector3 crossNormal;
        public Vector3 crossExpand;

        public JunctionOriginPoint(Vector3 point, Vector3 normal, Vector3 expand) : base(point, normal, expand)
        {
        }
    }

    [Serializable]
    public class JunctionBezierPoint : JunctionPoint
    {
        public JunctionBezierPoint(Vector3 point, Vector3 normal, Vector3 expand) : base(point, normal, expand)
        {
        }
    }

    [Serializable]
    public class JunctionInfo
    {
        public Vector3 centerPoint;
        public JunctionOriginPoint p1;
        public JunctionOriginPoint p2;

        public JunctionInfo(Vector3 centerPoint, JunctionOriginPoint p1, JunctionOriginPoint p2)
        {
            this.centerPoint = centerPoint;
            this.p1 = p1;
            this.p2 = p2;
        }
    }
    #endregion

    #region �ϲ�mesh���
    public class RoadMeshData
    {
        public Vector3[] vertices;
        public int[] triangles;
        public Vector2[] uvs;
        public Material material;
    }

    public class IntersectionMeshData
    {
        public Vector3[] vertices;
        public int[] triangles;
        public int[] crossTriangles;
        public Vector2[] uvs;
        public Material material;
        public Material crossMaterial;
    }

    public class SidewalkMeshData
    {
        public List<Vector3> vertices;
        public List<Vector2> uvs;
        public List<int> kerbTriangles;
        public List<int> sidewalkTriangles;
        public List<int> concreteTriangles;
        public Material kerbMaterial;
        public Material sidewalkMaterial;
        public Material concreteMaterial;
    }

    public class ExpandMeshData
    {
        public Vector3[] vertices;
        public int[] triangles;
    }
    #endregion

    #region ������α༭����
    public struct SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        public SerializableVector3(float _x, float _y, float _z)
        {
            x = _x;
            y = _y;
            z = _z;
        }
    }
    #endregion

    #region �����������
    public enum FacilityType
    {
        Lamp_2,
        Lamp_4,
        Lamp_6,
        Lamp_8,
        Lamp_10,
        Tree_2,
        Tree_4,
        Tree_6,
        Tree_8,
        Tree_10,
        Railing01,
        Railing02,
        Railing03,
        Altar_01,
    }

    public class RoadFacilityData
    {
        public string tileId;
        public List<FacilityPoint> facilityMarkPoints;
    }

    public class FacilityPoint
    {
        public FacilityType facilityType;
        public Vector3 scale;
        public Vector3 position;
        public Vector3 euler;
    }

    public class RoadFacilitySaveInfo
    {
        public string tileId;
        public List<FacilityMarkPoint> facilityMarkPoints;
    }

    public class FacilityMarkPoint
    {
        public FacilityType facilityType;
        public SerializableVector3 scale;
        public SerializableVector3 position;
        public SerializableVector3 euler;
    }

    public class RoadDataInfo
    {
        public UnwrappedTileId tileId;
        public Dictionary<Material, int> roadMatDict;
        public Dictionary<int, List<int>> roadTrianglesDict;
        public List<Vector3> roadVertices;
        public List<Vector2> roadUVs;
        public List<Vector3> expandVertices;
        public List<int> expandTriangles;
        public List<FacilityMarkPoint> facilityMarkPoints;
        public List<List<SerializableVector3>> allPoly;
        public List<Spline> lanes;
    }

    public class RailwayDataInfo
    {
        public UnwrappedTileId tileId;
        public Dictionary<Material, int> roadMatDict;
        public Dictionary<int, List<int>> roadTrianglesDict;
        public List<Vector3> roadVertices;
        public List<Vector2> roadUVs;
        public List<Vector3> expandVertices;
        public List<int> expandTriangles;
    }

    public class GenerateRoadResult
    {
        public Mesh roadMesh;
        public GameObject roadObj;
        public string roadFacilityInfo;
    }

    public class GenerateRailwayResult
    {
        public Mesh railwayMesh;
        public GameObject railwayObj;
    }
    #endregion

    # region ��ͨϵͳ���
    public enum LaneType
    {
        Left,
        Right
    }

    [Serializable]
    public class Lane
    {
        public LaneType type;
        public int id;
        public List<Vector3> vertices = new List<Vector3>();

        public Lane(LaneType type, int id, List<Vector3> vertices)
        {
            this.type = type;
            this.id = id;
            this.vertices = vertices;
        }
    }

    [Serializable]
    public class Route
    {
        public List<Vector3> vertices = new List<Vector3>();
        public Vector3 point;

        public Route(List<Vector3> vertices, Vector3 point)
        {
            this.vertices = vertices;
            this.point = point;
        }
    }
    #endregion
}