using GeoToolkit.DrawRoad;
using GeoToolkit;
using System.Collections.Generic;
using UnityEngine;
using GeoToolkit.DrawRailway;
using System.Linq;
using GeoToolkit.Road;

public class DrawRailwayCreator : MonoBehaviour
{
    public static void CreateOneRailway(List<Vector3> points, string terrainLayerName = "Terrain", List<Terrain> terrains = null)
    {
        DrawRoadDataConfig roadDataConfig = Resources.Load<DrawRoadDataConfig>("RoadConfig");
        DrawRoadUtil.SetCheckLayerName(terrainLayerName);

        Dictionary<Material, int> matDict = new Dictionary<Material, int>();
        Dictionary<int, List<int>> trianglesDict = new Dictionary<int, List<int>>();
        List<Vector3> allVertices = new List<Vector3>();
        List<Vector2> allUVs = new List<Vector2>();
        List<Vector3> expandVertices = new List<Vector3>();
        List<int> expandTriangles = new List<int>();

        Material railwayMat = roadDataConfig.railwayMatConfig.railwayMat;
        DrawRailwayMeshGenerator railwayMeshGenerator = new DrawRailwayMeshGenerator("0", 3, 1, points, roadDataConfig);
        RoadMeshData roadMeshData = railwayMeshGenerator.CalculateRailwayMeshData();
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

        ExpandMeshData expandMeshData = railwayMeshGenerator.CalculateExpandMeshData();
        int offset2 = expandVertices.Count;
        List<int> expandTris = expandMeshData.triangles.ToList();
        expandTris = expandTris.Select(i => i += offset2).ToList();
        expandVertices.AddRange(expandMeshData.vertices);
        expandTriangles.AddRange(expandTris);

        Mesh railwayMesh = new Mesh();
        railwayMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        railwayMesh.SetVertices(allVertices);
        railwayMesh.SetUVs(0, allUVs);
        railwayMesh.subMeshCount = trianglesDict.Count;
        foreach (var a in trianglesDict)
        {
            railwayMesh.SetTriangles(a.Value, a.Key);
        }
        railwayMesh.RecalculateNormals();

        GameObject roadNetwork = new GameObject("road");
        roadNetwork.CheckAddLayer("Road");
        roadNetwork.AddComponent<MeshFilter>().mesh = railwayMesh;
        roadNetwork.AddComponent<MeshRenderer>().materials = matDict.Keys.ToArray();
        roadNetwork.AddComponent<MeshCollider>();

        if (terrains != null)
        {
            Mesh expandMesh = new Mesh();
            expandMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            expandMesh.SetVertices(expandVertices);
            expandMesh.SetTriangles(expandTriangles, 0);
            GameObject expand = new GameObject("expand");
            expand.CheckAddLayer("Expand");
            expand.transform.parent = roadNetwork.transform;
            expand.transform.localPosition = Vector3.zero;
            expand.AddComponent<MeshFilter>().mesh = expandMesh;
            expand.AddComponent<MeshCollider>();
            for (int i = 0; i < terrains.Count; i++)
            {
                Conversions.ModifyTerrain(terrains[i], "Expand");
            }
            GameObject.DestroyImmediate(expand);
        }
    }

}
