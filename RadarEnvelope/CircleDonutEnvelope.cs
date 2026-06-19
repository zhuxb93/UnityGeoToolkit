using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace GeoToolkit.RadarEnvelope
{
    public class CircleDonutEnvelope : MonoBehaviour
    {
        public int circleDonutId;
        public List<Vector3> polygon = new List<Vector3>();
        public float width_ratio;
        public float height_ratio;
        public Vector3 circleDonutCenter = Vector3.zero;
        private Vector3[] vertices = null;
        private bool isSaveMesh = false;
        public void OnInit(ulong id, List<Vector3> polygon, Vector3 center, float width_ratio, float height_ratio, bool saveMesh, int donutId = 0)
        {
            this.width_ratio = width_ratio;
            this.height_ratio = height_ratio;
            this.isSaveMesh = saveMesh;
            this.polygon = polygon;
            circleDonutId = donutId;
            circleDonutCenter = center;
        }
        public void SetDonutId(int dId)
        {
            circleDonutId = dId;
        }
        void Start()
        {
            vertices = gameObject.GetComponent<MeshFilter>().sharedMesh.vertices;
        }
        
        public void RefreshVectorFace(Material material = null)
        {
            float3 plane = new float3(transform.position.x, 0, transform.position.z);
            Vector3 newCenter = circleDonutCenter;
            Vector3 newEdgePoint = polygon[0];
            float newRadius = Vector2.Distance(new Vector2(newCenter.x, newCenter.z), new Vector2(newEdgePoint.x, newEdgePoint.z));
            // List<Vector3> cirs = VectorUtil.DrawRing(newCenter, newEdgePoint, 60);

            List<Vector3> ringVertices = RadarEnvelopeTool.CalcCircleDonut(newCenter, newRadius, width_ratio * newRadius, height_ratio * newRadius);

            List<Vector2> ringUvs = new List<Vector2>();
            List<int> triangles = new List<int>();
            RadarEnvelopeTool.GenerateVectorDonut(ringVertices, width_ratio, ref ringUvs, ref triangles);

            for (int i = 0; i < ringVertices.Count; i++)
            {
                ringVertices[i] -= newCenter;
            }
            
            MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();
            Mesh mesh = meshFilter.sharedMesh != null ? meshFilter.sharedMesh : new Mesh();
            mesh.SetVertices(ringVertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, ringUvs);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            meshFilter.sharedMesh = mesh;
            if (material != null)
            {
                MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                    meshRenderer = gameObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
        public void SaveMesh(string dirPath)
        {
            if (isSaveMesh)
            {
#if UNITY_EDITOR
                MeshFilter meshFilter = gameObject.GetComponentInChildren<MeshFilter>();
                if (meshFilter != null)
                {
                    if (!Directory.Exists(dirPath))
                    {
                        Directory.CreateDirectory(dirPath);
                    }
                    string path = dirPath + "/" + gameObject.name + "_" + circleDonutId + ".asset";
                    if (!AssetDatabase.AssetPathExists(path))
                    {
                        AssetDatabase.CreateAsset(meshFilter.sharedMesh, path);
                    }
                }
                AssetDatabase.Refresh();
#endif
            }
        }
        
    }
    
}
