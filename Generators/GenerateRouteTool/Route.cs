using UnityEngine;
using System.IO;
using GeoToolkit;

public class Route : MonoBehaviour
{
    [Header("SDK configuration")]
    public GeoPlatformConfig config;
    [Header("GeoJSON path relative to StreamingAssets")]
    public string filePath;
    [Header("Output layer mask")]
    public LayerMask layerMask;
    [Header("Route material")]
    public Material routMat;

    private void Start()
    {
        GeoCoordinateUtils.Initialize(config);
        string path = Application.streamingAssetsPath + "/" + filePath;
        if (File.Exists(path))
        {
            string geojson = File.ReadAllText(path);
            //GameObject routes = RouteGeneratorTool.CreateRoute(config, geojson, routMat, layerMask);
            //routes.transform.parent = transform;
        }
    }
}
