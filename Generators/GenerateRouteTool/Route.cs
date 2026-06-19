using UnityEngine;
using System.IO;
using GeoToolkit;

public class Route : MonoBehaviour
{
    [Header("SDK�����ļ�")]
    public GeoPlatformConfig config;
    [Header("Geojson�ļ���StreamingAssetsĿ¼�µ����·��")]
    public string filePath;
    [Header("�������ڲ㼶")]
    public LayerMask layerMask;
    [Header("·��������")]
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
