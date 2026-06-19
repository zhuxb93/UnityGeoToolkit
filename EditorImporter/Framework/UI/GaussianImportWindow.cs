#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GeoToolkit
{
    public class GaussianImportWindow : EditorWindow
    {
        // 静态方法用于创建和显示窗口
        public static void ShowWindow(System.Action<GaussianImportSettings> onSettingsConfirmed)
        {
            var window = GetWindow<GaussianImportWindow>("Gaussian样式配置器");
            window.onSettingsConfirmed = onSettingsConfirmed;
            window.minSize = new Vector2(400, 600);
        }

        // 回调函数，当用户确认设置时调用
        private System.Action<GaussianImportSettings> onSettingsConfirmed;

        // 存储所有设置的数据结构
        public class GaussianImportSettings
        {
            public GameObject gaussianSplatsPrefab;
            public GameObject gaussianSplatsHDRPPass;

            public double gaussianLongitude;

            public double gaussianLatitude;

            public double gaussianHeight;
        }

        public void OnEnable()
        {
            GameObject defaultPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.geoearth.gaussiansplatting/Prefab/GaussianSplatsPrefab.prefab");
            if (defaultPrefab != null)
            {
                settings.gaussianSplatsPrefab = defaultPrefab;

            }

            GameObject gaussianSplatsHDRPPass = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.geoearth.gaussiansplatting/Prefab/GaussianSplatHDRPPass.prefab");
            if (gaussianSplatsHDRPPass != null)
            {
                settings.gaussianSplatsHDRPPass = gaussianSplatsHDRPPass;

            }

            var config = GeoToolkitEditor.LoadConfig();
            settings.gaussianLongitude = config.CenterLongitude;
            settings.gaussianLatitude = config.CenterLatitude;
            settings.gaussianHeight = 0;

        }

        private GaussianImportSettings settings = new GaussianImportSettings();

        private Vector2 scrollPosition;

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            //GUILayout.Label("Geojson样式配置器", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("GaussianSplatsPrefab", EditorStyles.boldLabel);

            settings.gaussianSplatsPrefab = (GameObject)EditorGUILayout.ObjectField("GaussianPrefab", settings.gaussianSplatsPrefab, typeof(GameObject), false);

            EditorGUILayout.LabelField("GaussianSplatsHDRPPass", EditorStyles.boldLabel);

            settings.gaussianSplatsHDRPPass = (GameObject)EditorGUILayout.ObjectField("HDRPPass", settings.gaussianSplatsHDRPPass, typeof(GameObject), false);

            GUILayout.Label("高斯摆放经纬高", EditorStyles.boldLabel);
            settings.gaussianLongitude = EditorGUILayout.DoubleField("经度", settings.gaussianLongitude);
            settings.gaussianLatitude = EditorGUILayout.DoubleField("纬度", settings.gaussianLatitude);
            settings.gaussianHeight = EditorGUILayout.DoubleField("高度", settings.gaussianHeight);
            EditorGUILayout.Space();

            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("取消", GUILayout.Width(100)))
            {
                Close();
            }

            if (GUILayout.Button("导入", GUILayout.Width(100)))
            {
                onSettingsConfirmed?.Invoke(settings);
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }


    }
}

#endif