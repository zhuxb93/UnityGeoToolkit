#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace GeoToolkit
{
    public class GeoJSONImporterWindow : EditorWindow
    {
        private GeoJSONImportSettings settings = new GeoJSONImportSettings();
        private Vector2 scrollPosition;

        // 多选数据类型
        private bool showPoint = true;
        private bool showLine = true;
        private bool showPolygon = true;

        // 折叠开关
        private bool foldPoint = true;
        private bool foldLine = true;
        private bool foldPolygon = true;

        private System.Action<GeoJSONImportSettings> onSettingsConfirmed;

        public static void ShowWindow(System.Action<GeoJSONImportSettings> onSettingsConfirmed, string[] sourcePaths, string codeSavePath, SelectType selectType, GeoPlatformConfig config)
        {
            var window = GetWindow<GeoJSONImporterWindow>("GeoJSON 样式配置器");
            window.onSettingsConfirmed = onSettingsConfirmed;
            window.settings.sourcePaths = sourcePaths;
            window.settings.codeSavePath = codeSavePath;
            window.settings.selectType = selectType;
            window.settings.config = config;
            window.minSize = new Vector2(420, 640);
        }

        public void OnEnable()
        {
            // 默认资源加载
            settings.TryLoadDefaults();
        }

        //public void OnDisable()
        //{
        //    GeoJSONImportUtility.TryDeleteEmptyFolder(settings.codeSavePath);
        //}


        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                GUILayout.Space(5);
                EditorGUILayout.LabelField("GeoJSON 样式配置器", new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter
                });
                GUILayout.Space(5);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.HelpBox("请选择需要导入的数据类型（可多选）。不同类型将显示对应的样式配置。", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            showPoint = GUILayout.Toggle(showPoint, "Point", "Button");
            showLine = GUILayout.Toggle(showLine, "Polyline", "Button");
            showPolygon = GUILayout.Toggle(showPolygon, "Polygon", "Button");
            EditorGUILayout.EndHorizontal();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("通用设置", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                settings.isRaycastHight = EditorGUILayout.Toggle("使用射线高度", settings.isRaycastHight);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(10);

                if (showPoint)
                {
                    DrawSection(ref foldPoint, "Point 样式", DrawPointSettings);
                }
                if (showLine)
                {
                    DrawSection(ref foldLine, "Line 样式", DrawLineSettings);
                }
                if (showPolygon)
                {
                    DrawSection(ref foldPolygon, "Polygon 样式", DrawPolygonSettings);
                }
            }
            EditorGUILayout.EndScrollView();

            // 底部操作按钮
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("取消", GUILayout.Width(100), GUILayout.Height(28)))
                {
                    Close();
                    GeoJSONImportUtility.TryDeleteEmptyFolder(settings.codeSavePath);
                }
                GUILayout.Space(10);
                if (GUILayout.Button("导入", GUILayout.Width(100), GUILayout.Height(28)))
                {
                    if (!showPoint && !showLine && !showPolygon)
                    {
                        EditorUtility.DisplayDialog("提示", "请至少选择一个要加载的 GeoJSON 类型！", "确定");
                        return;
                    }

                    // 保存用户选择的类型
                    settings.loadPoint = showPoint;
                    settings.loadLine = showLine;
                    settings.loadPolygon = showPolygon;

                    onSettingsConfirmed?.Invoke(settings);
                    Close();
                }
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);
        }

        private void DrawSection(ref bool fold, string title, System.Action drawContent)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                GUIStyle foldStyle = new GUIStyle(EditorStyles.foldoutHeader)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 12
                };
                fold = EditorGUILayout.BeginFoldoutHeaderGroup(fold, title, foldStyle);
                if (fold)
                {
                    EditorGUI.indentLevel++;
                    GUILayout.Space(5);
                    drawContent?.Invoke();
                    GUILayout.Space(5);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(6);
        }

        private void DrawPointSettings()
        {
            settings.pointDisplayType = (GeoJSONImportSettings.PointDisplayType)EditorGUILayout.EnumPopup("显示类型", settings.pointDisplayType);
            settings.isTileSplitting = EditorGUILayout.Toggle("按瓦片拆分", settings.isTileSplitting);
            if (settings.isTileSplitting)
            {
                EditorGUI.indentLevel++;
                settings.tileSplitLevel = EditorGUILayout.IntSlider("拆分级别", settings.tileSplitLevel, 8, 20);
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(5);

            switch (settings.pointDisplayType)
            {
                case GeoJSONImportSettings.PointDisplayType.Text:
                    settings.pointTextColor = EditorGUILayout.ColorField("文本颜色", settings.pointTextColor);
                    settings.pointTextSize = EditorGUILayout.IntField("文字大小", settings.pointTextSize);
                    settings.pointTextAsset = (TMP_FontAsset)EditorGUILayout.ObjectField("字体资源", settings.pointTextAsset, typeof(TMP_FontAsset), false);
                    break;

                case GeoJSONImportSettings.PointDisplayType.Prefab:
                    settings.pointPrefab = (GameObject)EditorGUILayout.ObjectField("预制体", settings.pointPrefab, typeof(GameObject), false);
                    settings.pointScale = EditorGUILayout.Vector3Field("缩放", settings.pointScale);
                    break;

                case GeoJSONImportSettings.PointDisplayType.Icon:
                    settings.pointIcon = (Texture2D)EditorGUILayout.ObjectField("图标贴图", settings.pointIcon, typeof(Texture2D), false);
                    settings.pointIconScale = EditorGUILayout.Vector3Field("图标缩放", settings.pointIconScale);
                    break;

                case GeoJSONImportSettings.PointDisplayType.Entities:
                    settings.entitiesPrefab = (GameObject)EditorGUILayout.ObjectField("实例化预制体", settings.entitiesPrefab, typeof(GameObject), false);
                    settings.minRenderDistance = EditorGUILayout.FloatField("最小显示距离", settings.minRenderDistance);
                    settings.maxRenderDistance = EditorGUILayout.FloatField("最大显示距离", settings.maxRenderDistance);
                    settings.entitiesScale = EditorGUILayout.Vector3Field("缩放", settings.entitiesScale);
                    break;
            }

            GUILayout.Space(5);
            settings.PointPositionOffset = EditorGUILayout.Vector3Field("位置偏移量", settings.PointPositionOffset);
        }

        private void DrawLineSettings()
        {
            settings.lineDisplayType = (GeoJSONImportSettings.LineDisplayType)EditorGUILayout.EnumPopup("显示类型", settings.lineDisplayType);

            GUILayout.Space(5);
            switch (settings.lineDisplayType)
            {
                case GeoJSONImportSettings.LineDisplayType.LineRenderer:
                case GeoJSONImportSettings.LineDisplayType.Mesh:
                    settings.lineMaterial = (Material)EditorGUILayout.ObjectField("线材质", settings.lineMaterial, typeof(Material), false);
                    settings.lineWidth = EditorGUILayout.FloatField("线宽度", settings.lineWidth);
                    break;

                case GeoJSONImportSettings.LineDisplayType.Routh:
                    settings.RouthMaterial = (Material)EditorGUILayout.ObjectField("避险道路材质", settings.RouthMaterial, typeof(Material), false);
                    break;

                case GeoJSONImportSettings.LineDisplayType.CrossSection:
                    settings.CrossSectionMaterial = (Material)EditorGUILayout.ObjectField("断面材质", settings.CrossSectionMaterial, typeof(Material), false);
                    break;
            }

            GUILayout.Space(5);
            settings.lineCombine = EditorGUILayout.Toggle("合并线段", settings.lineCombine);
        }

        private void DrawPolygonSettings()
        {
            settings.polygonDisplayType = (GeoJSONImportSettings.PolygonDisplayType)EditorGUILayout.EnumPopup("显示类型", settings.polygonDisplayType);

            GUILayout.Space(5);
            switch (settings.polygonDisplayType)
            {
                case GeoJSONImportSettings.PolygonDisplayType.Body:
                    settings.polygonMaterial = (Material)EditorGUILayout.ObjectField("多边形材质", settings.polygonMaterial, typeof(Material), false);
                    settings.isHeightField = EditorGUILayout.Toggle("使用属性字段取高", settings.isHeightField);
                    if (settings.isHeightField)
                        settings.HeightField = EditorGUILayout.TextField("高度字段名", settings.HeightField);
                    else
                        settings.polygonHeight = EditorGUILayout.FloatField("固定高度", settings.polygonHeight);

                    GUILayout.Space(3);
                    settings.polygonCombine = EditorGUILayout.Toggle("合并多边形", settings.polygonCombine);
                    settings.LoadNamePrefab = EditorGUILayout.Toggle("创建名称对象", settings.LoadNamePrefab);
                    settings.WriteOriginalPos = EditorGUILayout.Toggle("写入原始位置", settings.WriteOriginalPos);
                    break;

                case GeoJSONImportSettings.PolygonDisplayType.Water:
                    settings.waterTriangulationType = (GeoJSONImportSettings.WaterTriangulationType)EditorGUILayout.EnumPopup("三角化方式", settings.waterTriangulationType);
                    settings.WaterPosition = EditorGUILayout.Vector3Field("位置", settings.WaterPosition);
                    settings.WaterScale = EditorGUILayout.Vector3Field("缩放", settings.WaterScale);
                    break;

                case GeoJSONImportSettings.PolygonDisplayType.Border:
                    settings.borderWallMaterial = (Material)EditorGUILayout.ObjectField("墙体材质", settings.borderWallMaterial, typeof(Material), false);
                    settings.borderWallWidth = EditorGUILayout.FloatField("墙体宽度", settings.borderWallWidth);
                    settings.borderWallHeight = EditorGUILayout.FloatField("墙体高度", settings.borderWallHeight);
                    settings.borderDecalMaterial = (Material)EditorGUILayout.ObjectField("底面贴花材质", settings.borderDecalMaterial, typeof(Material), false);
                    break;

                case GeoJSONImportSettings.PolygonDisplayType.Decal:
                    settings.decalMaterial = (Material)EditorGUILayout.ObjectField("贴花材质", settings.decalMaterial, typeof(Material), false);
                    break;

                case GeoJSONImportSettings.PolygonDisplayType.RiverGrid:
                    settings.riverGridMaterial = (Material)EditorGUILayout.ObjectField("河道网格材质", settings.riverGridMaterial, typeof(Material), false);
                    break;
            }
        }
    }


    public class GeoJSONImportSettings
    {
        public string codeSavePath;
        public string[] sourcePaths;
        public SelectType selectType;
        public GeoPlatformConfig config;

        public bool loadPoint = true;
        public bool loadLine = true;
        public bool loadPolygon = true;

        /// <summary>
        /// 是否需要使用射线获取高度
        /// </summary>
        public bool isRaycastHight = false;

        // Point settings
        public enum PointDisplayType { Text, Prefab, Icon, Entities }
        public PointDisplayType pointDisplayType = PointDisplayType.Prefab;

        //Point - Text
        public bool isTileSplitting = false;
        public int tileSplitLevel = 14;
        public Color pointTextColor = Color.white;
        public int pointTextSize = 14;
        public TMP_FontAsset pointTextAsset;
        public Vector3 PointPositionOffset = new Vector3(0, 0, 0);

        //Point - Prefab
        public GameObject pointPrefab;
        public Vector3 pointScale = Vector3.one;

        //Point - Icon
        public Texture2D pointIcon;
        public Vector3 pointIconScale = Vector3.one;

        //Point - Entities
        public GameObject entitiesPrefab;
        public Vector3 entitiesScale = Vector3.one;
        public float minRenderDistance = 0;
        public float maxRenderDistance = 5000;

        // Line settings
        public enum LineDisplayType { LineRenderer, Mesh, Routh, CrossSection }
        public LineDisplayType lineDisplayType = LineDisplayType.LineRenderer;
        public Material lineMaterial;
        public float lineWidth = 1f;
        public bool lineCombine = true;

        //Routh
        public Material RouthMaterial;

        //CrossSection
        public Material CrossSectionMaterial;

        // Polygon settings
        public enum PolygonDisplayType { Body, Water, Border, Decal, RiverGrid }
        public PolygonDisplayType polygonDisplayType = PolygonDisplayType.Body;
        public Material polygonMaterial;
        public bool isHeightField = false;
        public string HeightField = "height";
        public float polygonHeight = 10f;
        public bool polygonCombine = false;
        public bool LoadNamePrefab = false;
        public bool WriteOriginalPos = false;

        //Border
        public Material borderWallMaterial;
        public float borderWallWidth = 3;
        public float borderWallHeight = 10;
        public Material borderDecalMaterial;

        //Decal
        public Material decalMaterial;

        //RiverGrid
        public Material riverGridMaterial;


        //Water
        public Vector3 WaterScale = Vector3.one;
        public Vector3 WaterPosition = new Vector3(0, 0, 0);
        public enum WaterTriangulationType { Delaunay, EarCut }
        public WaterTriangulationType waterTriangulationType = WaterTriangulationType.Delaunay;
    }

    public static class GeoJSONImportUtility
    {
        /// <summary>
        /// 删除空的导入文件夹（自动处理 .meta 文件和路径格式）。
        /// 支持相对路径（Assets/...）或绝对路径。
        /// </summary>
        public static void TryDeleteEmptyFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                Debug.LogWarning("TryDeleteEmptyFolder: 路径为空");
                return;
            }

            // 统一为正斜杠
            folderPath = folderPath.Replace("\\", "/");

            // 若是相对路径（Assets/...），转为绝对路径进行检查
            string fullPath = folderPath;
            if (!Path.IsPathRooted(folderPath))
                fullPath = Path.Combine(Directory.GetCurrentDirectory(), folderPath);

            // 延迟执行删除，确保 Unity 刷新操作结束
            try
            {
                if (!Directory.Exists(fullPath))
                {
                    Debug.Log($"TryDeleteEmptyFolder: 目录不存在 -> {fullPath}");
                    return;
                }

                bool hasFiles = Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories).Length > 0;
                if (!hasFiles)
                {
                    Debug.Log($"TryDeleteEmptyFolder: 删除空文件夹 -> {folderPath}");

                    // 使用 Unity 官方接口删除（支持 Assets 下的目录）
                    FileUtil.DeleteFileOrDirectory(folderPath);
                    FileUtil.DeleteFileOrDirectory(folderPath + ".meta");

                    AssetDatabase.Refresh();
                }
                else
                {
                    Debug.Log($"TryDeleteEmptyFolder: 文件夹非空，保留 -> {folderPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"TryDeleteEmptyFolder: 删除失败 -> {ex.Message}");
            }
        }
    }

    public static class GeoJSONSettingsExtensions
    {
        public static void TryLoadDefaults(this GeoJSONImportSettings settings)
        {
            settings.pointPrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.geoearth.platformsdk/Prefabs/POI/TextPrefabs_Poi.prefab");
            settings.pointTextAsset ??= AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Packages/com.geoearth.platformsdk/Prefabs/POI/Font/SourceHanSans-UI.asset");
            settings.pointIcon ??= AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.geoearth.platformsdk/Prefabs/POI/1x/Point_2.png");
            settings.entitiesPrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.geoearth.platformsdk/Editor/ImporterFramework/Importers/Terrain/Vega/BuiltinResources/Grass/Prefabs/Grass_01.prefab");
            settings.lineMaterial ??= AssetDatabase.LoadAssetAtPath<Material>("Packages/com.geoearth.platformsdk/Editor/ImporterFramework/Importers/GeoJSON/Materials/Defultline.mat");
            settings.polygonMaterial ??= AssetDatabase.LoadAssetAtPath<Material>("Packages/com.geoearth.platformsdk/Editor/ImporterFramework/Importers/GeoJSON/Materials/DefultPolygon.mat");
            settings.borderWallMaterial ??= AssetDatabase.LoadAssetAtPath<Material>("Packages/com.geoearth.platformsdk/Runtime/GenerateBorderTool/WallMat.mat");
            settings.borderDecalMaterial ??= AssetDatabase.LoadAssetAtPath<Material>("Packages/com.geoearth.platformsdk/Runtime/GenerateBorderTool/Shader Graphs_CustomDecal.mat");
            settings.decalMaterial ??= AssetDatabase.LoadAssetAtPath<Material>("Packages/com.geoearth.platformsdk/Editor/ImporterFramework/Importers/GeoJSON/Materials/DecalMat.mat");
            settings.riverGridMaterial ??= AssetDatabase.LoadAssetAtPath<Material>("Packages/com.geoearth.platformsdk/Editor/ImporterFramework/Importers/GeoJSON/Materials/DefultPolygon.mat");
            settings.RouthMaterial ??= AssetDatabase.LoadAssetAtPath<Material>("Packages/com.geoearth.platformsdk/Runtime/GenerateRouteTool/Route.mat");
            settings.CrossSectionMaterial ??= AssetDatabase.LoadAssetAtPath<Material>("Packages/com.geoearth.platformsdk/Editor/ImporterFramework/Importers/GeoJSON/Materials/Cross.mat");
        }
    }
}

#endif