#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using GeoToolkit;

public class TestBorder
{
    [MenuItem("Test/Test1")]
    public static void Test1()
    {
        string dataPath = Path.Combine(Application.streamingAssetsPath, "GeoToolkitSamples/synthetic-border-height.geojson");
        if (File.Exists(dataPath))
        {
            string geojson = File.ReadAllText(dataPath);
            GeoPlatformConfig config = ScriptableObject.CreateInstance<GeoPlatformConfig>();
            GeoCoordinateUtils.Initialize(config);
            Vector2d center = new Vector2d(0.0, 0.0);
            double scale = GeoCoordinateUtils.CalculateDistanceRatio((float)Conversions.CalculateTileSize(14), 14);
            Material wallMat = new Material(Shader.Find("HDRP/Lit"));
            wallMat.color = Color.yellow;
            Material decalMat = Resources.Load<Material>("Shader Graphs_CustomDecal");
            GameObject border = new GameObject("Border");
            //BorderGenerator.CreateBorder(border.transform, center, geojson, 0.21f, wallMat, 10, 100, decalMat);
        }
    }
}

#endif
