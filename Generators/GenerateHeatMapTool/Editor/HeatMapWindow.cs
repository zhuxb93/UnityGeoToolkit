#if UNITY_EDITOR
using GeoToolkit;
using UnityEditor;
using UnityEngine;

public class HeatMapWindow : EditorWindow
{
    private TextAsset heatMapData;
    private GeoPlatformConfig config;
    private TileID downLeftTileId;
    private TileID upRightTileId;
    private string dlX;
    private string dlY;
    private string dlZ;
    private string urX;
    private string urY;
    private string urZ;
    private string textureOutPath;

    [MenuItem("GeoToolkit/环境组件/热力图生成工具", false, 100)]
    public static void ShowWindow()
    {
        HeatMapWindow window = GetWindow<HeatMapWindow>("热力图生成工具");
        window.maxSize = new Vector2(500, 200);
        window.minSize = new Vector2(500, 200);
        window.Show();
    }

    private void OnGUI()
    {
        heatMapData = (TextAsset)EditorGUILayout.ObjectField("选择热力图数据", heatMapData, typeof(TextAsset), false);
        GUILayout.Space(10);
        config = (GeoPlatformConfig)EditorGUILayout.ObjectField("选择SDK配置文件", config, typeof(GeoPlatformConfig), false);
        GUILayout.Space(10);
        GUILayout.BeginVertical();
        GUILayout.Label("请输入场景瓦片范围");
        GUILayout.BeginHorizontal();
        GUILayout.Label("请输入左下角瓦片id", GUILayout.Width(145));
        GUILayout.Label("x", GUILayout.Width(10));
        dlX = GUILayout.TextField(dlX, GUILayout.Width(50));
        GUILayout.Label("y", GUILayout.Width(10));
        dlY = GUILayout.TextField(dlY, GUILayout.Width(50));
        GUILayout.Label("z", GUILayout.Width(10));
        dlZ = GUILayout.TextField(dlZ, GUILayout.Width(50));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("请输入右上角瓦片id", GUILayout.Width(145));
        GUILayout.Label("x", GUILayout.Width(10));
        urX = GUILayout.TextField(urX, GUILayout.Width(50));
        GUILayout.Label("y", GUILayout.Width(10));
        urY = GUILayout.TextField(urY, GUILayout.Width(50));
        GUILayout.Label("z", GUILayout.Width(10));
        urZ = GUILayout.TextField(urZ, GUILayout.Width(50));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUILayout.Space(10);
        if (!string.IsNullOrEmpty(dlX) && !string.IsNullOrEmpty(dlY) && !string.IsNullOrEmpty(dlZ))
        {
            downLeftTileId = new TileID(int.Parse(dlX), int.Parse(dlY), int.Parse(dlZ));
        }
        if (!string.IsNullOrEmpty(urX) && !string.IsNullOrEmpty(urY) && !string.IsNullOrEmpty(urZ))
        {
            upRightTileId = new TileID(int.Parse(urX), int.Parse(urY), int.Parse(urZ));
        }
        GUILayout.BeginHorizontal();
        GUILayout.Label("热力图纹理输出路径", GUILayout.Width(145));
        textureOutPath = GUILayout.TextField(textureOutPath);
        GUILayout.EndHorizontal();
        GUILayout.Space(20);
        if (GUILayout.Button("生成热力图纹理"))
        {
            if (heatMapData == null)
            {
                Debug.LogError("请选择热力图数据");
            }
            else if (config == null)
            {
                Debug.LogError("请选择SDK配置文件");
            }
            else if (downLeftTileId == null)
            {
                Debug.LogError("请输入左下角瓦片id");
            }
            else if (upRightTileId == null)
            {
                Debug.LogError("请输入右上角瓦片id");
            }
            else if (string.IsNullOrEmpty(textureOutPath))
            {
                Debug.LogError("请输入热力图纹理输出路径");
            }
            else
            {
                Vector2d center = new Vector2d(config.CenterLatitude, config.CenterLongitude);
                BorderTileId borderTileId = new BorderTileId();
                borderTileId.downLeft = downLeftTileId;
                borderTileId.upRight = upRightTileId;
                HeatMapGenerator.CreateHeatMap(center, heatMapData.text, config.GetScale(), borderTileId, textureOutPath);
            }
        }
    }
}
#endif