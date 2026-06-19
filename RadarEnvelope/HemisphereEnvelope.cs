using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace GeoToolkit.RadarEnvelope
{
    public class HemisphereEnvelope : MonoBehaviour
    {
        public int hemisphereId;
        [HideInInspector]
        public float ellipseRatioHeight = 1;
        
        public void SetHemisphereId(int id)
        {
            hemisphereId = id;
        }
        public void Refresh(Vector3 center, float radius, Material material = null)
        {
            float heightRadius = radius; 
            if (ellipseRatioHeight != 0 && ellipseRatioHeight != 1)
            {
                heightRadius = radius * ellipseRatioHeight; 
            }

            List<float> planeHeights = new List<float>();
            List<float> planeRadius = new List<float>();
            for (int j = 0; j < RadarEnvelopeTool.HemisphereHorNumber + 1; j++)
            {
                float rad = Mathf.PI * 0.5f * (j / (RadarEnvelopeTool.HemisphereHorNumber * 1.0f));
                planeRadius.Add(Mathf.Cos(rad) * radius);
                planeHeights.Add(Mathf.Sin(rad) * heightRadius * RadarEnvelopeTool.HemisphereHieghtFactor);
            }
            planeHeights.Reverse();
            planeRadius.Reverse();
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> point_uvs = new List<Vector2>();
            for (int j = 0; j < planeHeights.Count; j++)
            {
                Vector3 tempPlaneCenter = center + Vector3.up * planeHeights[j];
                float tempPlaneRadius = planeRadius[j];
                for (int k = 0; k < RadarEnvelopeTool.HemisphereVerNumber + 1; k++)
                {
                    float rad = 2 * Mathf.PI * (k / (RadarEnvelopeTool.HemisphereVerNumber * 1.0f));

                    float x = -Mathf.Cos(rad) * tempPlaneRadius;
                    float z = Mathf.Sin(rad) * tempPlaneRadius;
                    vertices.Add(tempPlaneCenter + new Vector3(x, 0, z));
                }
            }
            for(int j = 0; j < vertices.Count; j++)
            {
                vertices[j] -= center;
            }
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();
            RadarEnvelopeTool.GenerateVectorHemisphere(vertices, RadarEnvelopeTool.HemisphereVerNumber, ref uvs, ref tris);

            MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();
            bool isExist = meshFilter.sharedMesh != null;
            Mesh mesh = isExist ? meshFilter.sharedMesh : new Mesh();
            
            mesh.vertices = vertices.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            if (!isExist)
            {
                #if UNITY_EDITOR
                if (Application.isEditor && !Application.isPlaying)
                {
                    string dirPath = "Assets/ImportRoot/Radar/Meshes";
                    if (!Directory.Exists(dirPath))
                    {
                        Directory.CreateDirectory(dirPath);
                    }
                    string path = dirPath + "/" + gameObject.name + "_" + hemisphereId + ".asset";
                    AssetDatabase.CreateAsset(mesh, path);
                }
                #endif
            }
            
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
        
    }
}

