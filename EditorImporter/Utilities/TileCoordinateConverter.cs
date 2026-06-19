#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace GeoToolkit
{
    public class TileCoordinateConverter : EditorWindow
    {
        private string tileInput = ""; // Combined tile input (z-x-y)
        private string coordinateInput = ""; // Combined coordinate input (x,y)
        private string zoomLevel = "";

        private Vector2 scrollPosition;
        private GeoPlatformConfig config;

        [MenuItem("GeoToolkit/坐标转换/瓦片和经纬度转换工具", false, priority = 201)]
        public static void ShowWindow()
        {
            GetWindow<TileCoordinateConverter>("瓦片坐标转换器");
        }

        private void OnEnable()
        {
            config = GeoToolkitEditor.LoadConfig();
            GeoCoordinateUtils.Initialize(config);
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // 标题样式
            GUIStyle titleStyle = new GUIStyle(EditorStyles.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 14;
            titleStyle.alignment = TextAnchor.MiddleCenter;

            EditorGUILayout.LabelField("瓦片坐标 ↔ 经纬度转换器", titleStyle);
            EditorGUILayout.Space(15);

            // 瓦片号转经纬度部分
            EditorGUILayout.LabelField("瓦片坐标转经纬度", EditorStyles.boldLabel);

            // 瓦片坐标输入 (Z-X-Y)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Tile (Z-X-Y):", GUILayout.Width(80));
            tileInput = EditorGUILayout.TextField(tileInput);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("转换 →", GUILayout.Width(100)))
            {
                ConvertTileToLatLon();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // 经纬度显示区域
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("坐标 (X,Y):", GUILayout.Width(80));
            coordinateInput = EditorGUILayout.TextField(coordinateInput);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // 缩放级别输入
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("缩放级别:", GUILayout.Width(60));
            zoomLevel = EditorGUILayout.TextField(zoomLevel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("← 转换", GUILayout.Width(100)))
            {
                ConvertLatLonToTile();
            }

            EditorGUILayout.EndHorizontal();


            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("应用到中心点配置", GUILayout.Width(100)))
            {
                ApplyToConfig();
            }
            EditorGUILayout.EndHorizontal();



            EditorGUILayout.Space(10);

            // 错误提示区域
            if (!string.IsNullOrEmpty(errorMessage))
            {
                EditorGUILayout.HelpBox(errorMessage, MessageType.Error);
            }

            EditorGUILayout.EndScrollView();
        }

        private void ApplyToConfig()
        {
            if (!ValidateTileInput(out string err))
            {
                errorMessage = $"无法应用到配置：{err}";
                return;
            }

            // 通过校验，才允许写配置
            GeoToolkitEditor.SetConfigCenter(config, tileInput);
            errorMessage = "";
        }

        private string errorMessage = "";

        private void ConvertTileToLatLon()
        {
            try
            {
                string[] tileParts = tileInput.Split('-');
                if (tileParts.Length != 3)
                {
                    errorMessage = "瓦片坐标格式应为 z-x-y";
                    return;
                }

                var pos2D = TileProcessor.Get3857TileLeftBottomLonLat(
                    new TileID(Convert.ToInt32(tileParts[1]), Convert.ToInt32(tileParts[2]), Convert.ToInt32(tileParts[0])));
                coordinateInput = $"{pos2D.x},{pos2D.y}";
                errorMessage = "";
            }
            catch (Exception e)
            {
                errorMessage = "瓦片坐标转换失败，请检查输入是否为有效的 z-x-y 数字格式";
            }
        }

        private void ConvertLatLonToTile()
        {
            try
            {
                string[] coordParts = coordinateInput.Split(',');
                if (coordParts.Length != 2)
                {
                    errorMessage = "坐标格式应为 x,y";
                    return;
                }

                var tilid = TileProcessor.Get3857TileIDFromLonLat(
                    double.Parse(coordParts[0]),
                    double.Parse(coordParts[1]),
                    Convert.ToInt32(zoomLevel));
                tileInput = $"{tilid.z}-{tilid.x}-{tilid.y}";
                errorMessage = "";
            }
            catch (Exception e)
            {
                errorMessage = "经纬度转换为瓦片失败，请检查坐标数值和缩放级别是否正确";
            }
        }

        private bool ValidateTileInput(out string error)
        {
            error = "";

            if (string.IsNullOrWhiteSpace(tileInput))
            {
                error = "瓦片坐标不能为空";
                return false;
            }

            string[] parts = tileInput.Split('-');
            if (parts.Length != 3)
            {
                error = "瓦片坐标格式应为 z-x-y";
                return false;
            }

            if (!int.TryParse(parts[0], out int z) ||
                !int.TryParse(parts[1], out int x) ||
                !int.TryParse(parts[2], out int y))
            {
                error = "瓦片坐标必须为整数";
                return false;
            }

            if (z < 0)
            {
                error = "缩放级别 z 不能小于 0";
                return false;
            }

            int max = 1 << z;
            if (x < 0 || y < 0 || x >= max || y >= max)
            {
                error = $"在 z={z} 时，x 和 y 的范围应为 0 ~ {max - 1}";
                return false;
            }

            return true;
        }

    }
}

#endif