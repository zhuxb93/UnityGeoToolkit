#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeoToolkit.EditorImport;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GeoToolkit
{
    public class GeoToolkitEditor : EditorWindow
    {
        private static GeoPlatformConfig config;
        private SerializedObject serializedConfig;
        private ReorderableList treesObjectsList;
        private ReorderableList grassObjectsList;

        private const string RootPath = "Assets/ImportRoot";
        private const string RootStreamingAsset = "Assets/StreamingAssets";
        public const string TerrainSavePath = RootPath + "/Terrain";
        public const string GeoJsonSavePath = RootPath + "/GeoJSON";
        public const string AutoModelSavePath = RootPath + "/Building";
        public const string CustomModelSavePath = RootPath + "/CustomModel";
        public const string WindySavePath = RootStreamingAsset + "/Windy";
        //private const string Geo3DTilesPath = RootPath + "/3DTiles";
        private const string GaussianSavePath = RootStreamingAsset + "/";
        private const string GridCodeSavePath = RootPath + "/GridCode";

        //配置文件路径
        private const string ConfigSavePath = "Assets/GeoToolkit/Config";



        //[MenuItem("GeoToolkit/一键自动化生产")]
        public static void Automated()
        {
            GeoLoginWindow.ShowWindow();
        }

        [MenuItem("GeoToolkit/坐标转换/更新所有Anchor World->Geo", false, priority = 201)]
        public static void UpdateAllWorldToGeo()
        {
            GeoGlobeAnchor[] allGeoAnchor = FindObjectsByType<GeoGlobeAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (allGeoAnchor != null && allGeoAnchor.Length > 0)
            {
                foreach (var anchor in allGeoAnchor)
                {
                    anchor.UpdateGeoFromWorldPosition();
                }
            }
        }

        [MenuItem("GeoToolkit/坐标转换/更新所有Anchor Geo->World", false, priority = 201)]
        public static void UpdateAllGeoToWorld()
        {
            GeoGlobeAnchor[] allGeoAnchor = FindObjectsByType<GeoGlobeAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (allGeoAnchor != null && allGeoAnchor.Length > 0)
            {
                foreach (var anchor in allGeoAnchor)
                {
                    anchor.UpdateWorldPositionFromGeo();
                }
            }
        }

        [MenuItem("GeoToolkit/环境组件/添加后处理", false, 100)]
        public static void AddGlobalVolume()
        {
            // 使用 Unity 2023+ 推荐的新 API
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var enviroObjs = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(o => o.CompareTag(VolumeTag.GeoEnviro.ToString()))
                .ToArray();
            var volumeObjs = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(o => o.CompareTag(VolumeTag.GeoVolume.ToString()))
                .ToArray();

            int totalCount = lights.Length + enviroObjs.Length + volumeObjs.Length;

            bool confirm = EditorUtility.DisplayDialog(
                "警告",
                $"即将删除以下对象：\n\n" +
                $"• Light：{lights.Length} 个\n" +
                $"• Tag = {VolumeTag.GeoEnviro}：{enviroObjs.Length} 个\n" +
                $"• Tag = {VolumeTag.GeoVolume}：{volumeObjs.Length} 个\n\n" +
                $"是否继续？",
                "继续（删除并创建）",
                "取消"
            );

            if (!confirm)
                return;

            // 删除 GeoEnviro
            foreach (var o in enviroObjs)
            {
                if (o != null)
                {
                    Undo.DestroyObjectImmediate(o);
                }

            }


            // 删除 GeoVolume
            foreach (var o in volumeObjs)
            {
                if (o != null)
                {
                    Undo.DestroyObjectImmediate(o);
                }
            }


            // 删除 Light
            foreach (var l in lights)
            {
                if (l != null && l.gameObject != null)
                {
                    Undo.DestroyObjectImmediate(l.gameObject);
                }
            }

            // 创建 Global Volume
            var globalVolume = GetGlobalVolume();
            if (globalVolume.Item1 != null && globalVolume.Item2 != null)
            {
                GameObject e = GameObject.Instantiate(globalVolume.Item1);
                Undo.RegisterCreatedObjectUndo(e, "创建后处理父物体");
                e.tag = TagUtils.AddTag(VolumeTag.GeoEnviro.ToString());
                e.name = globalVolume.Item1.name;
                e.transform.position = Vector3.zero;

                GameObject g = GameObject.Instantiate(globalVolume.Item2);
                Undo.RegisterCreatedObjectUndo(g, "创建后处理父物体");
                g.tag = TagUtils.AddTag(VolumeTag.GeoVolume.ToString());
                g.name = globalVolume.Item2.name;
                g.transform.position = Vector3.zero;

                var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            }
        }


        [MenuItem("GeoToolkit/环境组件/添加相机控制器", false, 100)]
        public static void AddCameraControl()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                var cameraControl = camera.gameObject.GetComponent<CameraController>();
                if (cameraControl == null)
                {
                    camera.gameObject.AddComponent<CameraController>();
                    Undo.RegisterCreatedObjectUndo(camera.gameObject, "创建相机控制器");
                }

                var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                bool dialog = EditorUtility.DisplayDialog(
                    "友情提醒",
                    $"主相机已添加相机控制器",
                    "是");
            }
        }

        [MenuItem("GeoToolkit/环境组件/添加无人机模拟飞行", false, 100)]
        public static void AddUAVController()
        {
            CameraController cameraController = Resources.FindObjectsOfTypeAll<CameraController>().Where(c => c.gameObject.scene.IsValid()).Select(c => c.gameObject).First().GetComponent<CameraController>();
            if (cameraController != null)
            {
                var cameraAnimationEditor = cameraController.gameObject.GetComponent<CameraAnimationEditor>();

                string compassPrefabPath = "Packages/com.geoearth.platformsdk/Runtime/UAVModule/UAV/Prefabs/UAVCompass.prefab";
                GameObject compassPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(compassPrefabPath);
                GameObject compass = null;
                if (compassPrefab != null)
                {
                    compass = GameObject.Instantiate(compassPrefab);
                }

                string uavPrefabPath = "Packages/com.geoearth.platformsdk/Runtime/UAVModule/UAV/Prefabs/UAVAnchor.prefab";
                GameObject uavPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(uavPrefabPath);
                if (uavPrefab != null)
                {
                    Vector3 uavPosition = Vector3.zero;
                    SceneView sv = SceneView.lastActiveSceneView;
                    if (sv == null) return;
                    Vector2 screenCenter = new Vector2(sv.position.width * 0.5f, sv.position.height * 0.5f);
                    Ray ray = HandleUtility.GUIPointToWorldRay(screenCenter);
                    if (Physics.Raycast(ray, out var hit))
                    {
                        uavPosition = hit.point + Vector3.up * 0.09f;
                    }

                    GameObject uav = GameObject.Instantiate(uavPrefab);
                    uav.transform.position = uavPosition;
                    UAVSimCtrl simCtrl = uav.GetComponent<UAVSimCtrl>();
                    simCtrl.mainCamera = cameraController.transform;
                    simCtrl.roamCamera = cameraController;
                    if (cameraAnimationEditor != null)
                    {
                        simCtrl.animCamera = cameraAnimationEditor;
                    }
                    if (compass != null)
                    {
                        simCtrl.compassPanel = compass;
                        simCtrl.compass = compass.transform.Find("Compass Bar Panel/Compass Bar Mask/Compass").GetComponent<RawImage>();
                        simCtrl.degreeTxt = compass.transform.Find("Compass Bar Panel/Compass Bar Mask/Degrees Text").GetComponent<Text>();
                    }

                    EditorUtility.DisplayDialog("无人机操作指引", "已加载无人机模拟器。\r\n1.按C键进入/退出无人机模拟飞行。\r\n2.同时按住S  D  ←  ↓大于1秒开机\r\n3.按住  ↑  大于1秒起飞\r\n4.WASD+↑↓←→控制无人机飞行\r\n5.按V切换第一/第三人称视角\r\n6.按Q  E控制第一人称视角俯仰", "确认");
                }
                else
                {
                    if (compassPrefab != null)
                    {
                        GameObject.DestroyImmediate(compass);
                    }
                    EditorUtility.DisplayDialog("预制体丢失", "请检查Runtime/UAVModule/UAV/Prefabs目录下是否存在UAVAnchor预制体", "确认");
                }
            }
            else
            {
                Debug.LogError("请给相机添加CameraController组件");
            }
        }

        [MenuItem("GeoToolkit/环境组件/踏勘工具", false, 100)]
        public static void AddReconnaissance()
        {
            string prefabPath = "Packages/com.geoearth.platformsdk/Runtime/ReconnaissanceTool/Reconnaissance.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                if (Object.FindFirstObjectByType<EventSystem>() == null)
                {
                    GameObject go = ObjectFactory.CreateGameObject("EventSystem");
                    go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                    go.AddComponent<InputSystemUIInputModule>();
#elif !ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
                    go.AddComponent<StandaloneInputModule>();
#elif ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
                    go.AddComponent<InputSystemUIInputModule>();
#else
                    Debug.Log("未识别到任何输入系统宏（一般不会走到这里）");
#endif
                }
                GameObject tmp = GameObject.Instantiate(prefab);
                ReconnaissanceController reconnaissanceController = tmp.GetComponent<ReconnaissanceController>();
                CameraController cameraController = Resources.FindObjectsOfTypeAll<CameraController>().Where(c => c.gameObject.scene.IsValid()).Select(c => c.gameObject).First().GetComponent<CameraController>();
                reconnaissanceController.camRoot = cameraController.transform;
                if (Camera.main != null)
                {
                    reconnaissanceController.mainCamera = Camera.main;
                }
                GeoPlatformConfig config = AssetDatabase.LoadAssetAtPath<GeoPlatformConfig>("Assets/GeoToolkit/Config/GeoPlatformConfig.asset");
                reconnaissanceController.sdkConfig = config;
                EditorUtility.DisplayDialog("注意事项", "请确认SDKConfig，CamRoot，MainCamera是否已关联上", "确认");
            }
            else
            {
                EditorUtility.DisplayDialog("预制体丢失", "请检查Runtime/ReconnaissanceTool目录下是否存在Reconnaissance预制体", "确认");
            }
        }

        //[MenuItem("GeoToolkit/更新场景中所有TextMeshPro的AutoFaceAndScale")]
        //public static void AddAutoFaceAndScale()
        //{

        //}

        [MenuItem("GeoToolkit/数字孪生地形/载入地形", false, 103)]
        public static void LoadTerrain()
        {
            #region 加载Terrain

            if (config == null)
            {
                LoadConfig();
            }

            TerrainImport.LoadTerrain(TerrainSavePath, config);

            #endregion
        }

        [MenuItem("GeoToolkit/数字孪生地形/载入道路", false, 103)]
        public static void LoadRoad()
        {
            #region 加载道路

            if (config == null)
            {
                LoadConfig();
            }

            TerrainImport.LoadRoad(TerrainSavePath, config);

            #endregion
        }

        [MenuItem("GeoToolkit/数字孪生地形/生成地形位置Json", false, 103)]
        public static void CreateTerrainJson()
        {
            TerrainImport.CreatePosJson(TerrainSavePath);
        }

        [MenuItem("GeoToolkit/数字孪生地形/删除Terrain中的树和草", false, 103)]
        public static void RemoveTerrainVega()
        {
            TerrainImport.RemoveTerrainVega(TerrainSavePath);
        }


        [MenuItem("GeoToolkit/数字孪生建筑/载入建筑（合并）", false, 105)]
        public static void LoadCombineBuilding()
        {
            if (config == null)
            {
                LoadConfig();
            }
            #region 加载建筑

            AutoModelImport.LoadingBuilding(AutoModelSavePath, config, true);
            #endregion
        }


        [MenuItem("GeoToolkit/数字孪生建筑/载入建筑（不合并）", false, 105)]
        public static void LoadBuilding()
        {
            if (config == null)
            {
                LoadConfig();
            }
            #region 加载建筑

            AutoModelImport.LoadingBuilding(AutoModelSavePath, config, false);

            #endregion
        }

        [MenuItem("GeoToolkit/清理场景", false, 500)]
        public static void Clear()
        {
            bool confirm = EditorUtility.DisplayDialog(
           "确认清理场景",
           "此操作将清理场景中的所有地形和建筑，并重置配置，是否继续？",
           "确定",
           "取消"
            );
            if (!confirm) return; // 用户取消
            var GetSenceAllTerrain = TerrainImport.GetSenceAllTerrain(TerrainSavePath);

            foreach (var go in GetSenceAllTerrain)
            {
                if (go != null)
                {
                    Undo.DestroyObjectImmediate(go); // 支持撤销删除
                }
            }


            var GetSenceAllBuilding = AutoModelImport.GetSenceAllBuilding(AutoModelSavePath);
            foreach (var go in GetSenceAllBuilding)
            {
                if (go != null)
                {
                    Undo.DestroyObjectImmediate(go); // 同样支持撤销
                }
            }


            #region 清空配置文件

            string configPath = $"{ConfigSavePath}/GeoPlatformConfig.asset";
            var localConfig = AssetDatabase.LoadAssetAtPath<GeoPlatformConfig>(configPath);
            if (localConfig != null)
            {
                // 标记配置修改（以便 Ctrl+Z 撤销）
                Undo.RecordObject(localConfig, "重置 GeoPlatform 配置");
                localConfig.Initialize();
                EditorUtility.SetDirty(localConfig); // 标记需要保存
            }

            #endregion
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }

        #region 清理场景目录

        [MenuItem("GeoToolkit/清理场景目录", false, 501)]
        public static void ClearFolder()
        {
            if (!AssetDatabase.IsValidFolder(RootPath))
            {
                Debug.LogWarning($"目标文件夹不存在: {RootPath}");
                return;
            }

            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), RootPath);

            // 先清理 RootPath 下的文件（非 .meta）
            foreach (var file in Directory.GetFiles(fullPath))
            {
                if (Path.GetFileName(file) != ".meta")
                {
                    File.Delete(file);
                    Debug.Log($"已删除文件: {file}");
                }
            }

            // 遍历子目录
            foreach (var dir in Directory.GetDirectories(fullPath))
            {
                string dirName = Path.GetFileName(dir);

                // 特殊处理 Building 文件夹
                if (dirName == "Building")
                {
                    HandleBuildingFolder(dir);
                }
                else
                {
                    bool confirm = EditorUtility.DisplayDialog(
                        "确认删除文件夹",
                        $"是否删除文件夹: {dir} ?",
                        "是",
                        "否"
                    );

                    if (confirm)
                    {
                        Directory.Delete(dir, true);
                        Debug.Log($"已删除文件夹: {dir}");
                    }
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"已清理目录: {RootPath}");
        }

        /// <summary>
        /// 特殊处理 Building 文件夹：保留 Materials 和 Textures
        /// </summary>
        private static void HandleBuildingFolder(string buildingPath)
        {
            foreach (var subDir in Directory.GetDirectories(buildingPath))
            {
                string subDirName = Path.GetFileName(subDir);

                if (subDirName == "Materials" || subDirName == "Textures")
                {
                    Debug.Log($"跳过保留文件夹: {subDir}");
                    continue;
                }

                bool confirm = EditorUtility.DisplayDialog(
                    "确认删除 Building 子文件夹",
                    $"是否删除 Building/{subDirName} ?",
                    "是",
                    "否"
                );

                if (confirm)
                {
                    Directory.Delete(subDir, true);
                    Debug.Log($"已删除文件夹: {subDir}");
                }
            }

            // 删除 Building 下的文件（非 .meta）
            foreach (var file in Directory.GetFiles(buildingPath))
            {
                if (Path.GetFileName(file) != ".meta")
                {
                    File.Delete(file);
                    Debug.Log($"已删除文件: {file}");
                }
            }
        }

        #endregion




        [MenuItem("GeoToolkit/开始导入", false, 101)]
        public static void ShowWindow()
        {
            GetWindow<GeoToolkitEditor>("GeoToolkit Editor");
        }

        #region 网格化世界

        [MenuItem("GeoToolkit/环境组件/网格化场景/生成网格", false, 100)]
        public static void CreateGridWorld()
        {
            GridCodeControlPanel.ShowWindow(LoadGridCode);
        }


        [MenuItem("GeoToolkit/环境组件/网格化场景/载入网格", false, 100)]
        public static void LoadGridWorld()
        {
            if (!AssetDatabase.IsValidFolder(GridCodeSavePath))
            {
                Debug.LogWarning($"目标文件夹不存在: {GridCodeSavePath}");
                return;
            }

            string[] gridCodesubFolders = Directory.GetDirectories(GridCodeSavePath);

            if (gridCodesubFolders.Length == 0)
            {
                Debug.Log($"{GridCodeSavePath}目录中没有元素！");
                return;
            }

            foreach (var subModel in gridCodesubFolders)
            {
                // 弹出对话框询问是否加载这个文件夹
                bool loadThisFolder = EditorUtility.DisplayDialog(
                    "加载确认",
                    $"是否加载文件夹: {subModel}?",
                    "是",
                    "否");

                if (!loadThisFolder)
                {
                    continue;
                }
                // 在submodel文件夹中查找所有预制体文件
                string[] prefabFiles = Directory.GetFiles(subModel, "*.prefab", SearchOption.AllDirectories);

                if (prefabFiles.Length == 0)
                {
                    Debug.LogWarning($"在文件夹 {subModel} 中未找到预制体文件");
                    continue;
                }

                foreach (var prefab in prefabFiles)
                {
                    GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
                    GameObject instance = PrefabUtility.InstantiatePrefab(obj) as GameObject;
                    instance.transform.localPosition = Vector3.zero;
                    instance.name = "GridContainer";
                }
            }


        }


        #endregion


        private void OnEnable()
        {
            LoadConfig();
            if (config != null)
            {
                serializedConfig = new SerializedObject(config);
                //InitializeTreesList();
                //InitializeGrassList();
            }
        }


        private bool foldBaseConfig = true;
        private bool foldSceneObject = true;
        private bool foldImport = true;

        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private GUIStyle bigButtonStyle;

        private void InitStyles()
        {
            if (headerStyle != null) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13
            };

            boxStyle = new GUIStyle("HelpBox")
            {
                padding = new RectOffset(10, 10, 6, 6)
            };

            bigButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 34,
                fontSize = 12
            };
        }

        private void OnGUI()
        {
            InitStyles();

            Rect totalRect = EditorGUILayout.BeginVertical();

            if (config == null)
            {
                EditorGUILayout.HelpBox("未找到 Terrain 配置文件", MessageType.Error);
                if (GUILayout.Button("➕ 创建配置文件", bigButtonStyle))
                {
                    EditorApplication.delayCall += () =>
                    {
                        CreateConfig();
                        LoadConfig();              // 重新加载
                        serializedConfig = new SerializedObject(config);
                        Repaint();                 // 强制刷新窗口
                    };
                }
                EditorGUILayout.EndVertical();
                return;
            }

            serializedConfig.Update();

            DrawBaseConfig();
            EditorGUILayout.Space(6);

            DrawSceneObjectConfig();
            EditorGUILayout.Space(6);

            DrawImportSection();

            if (serializedConfig.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.EndVertical();

            if (Event.current.type == EventType.Repaint)
            {
                float desiredHeight = totalRect.height;
                minSize = new Vector2(minSize.x, desiredHeight);
                maxSize = new Vector2(maxSize.x, desiredHeight);
            }
        }

        #region Section Draw

        private void DrawBaseConfig()
        {
            foldBaseConfig = EditorGUILayout.Foldout(
                foldBaseConfig,
                "⚙ Terrain 基础配置",
                true,
                headerStyle
            );

            if (!foldBaseConfig) return;

            EditorGUILayout.BeginVertical(boxStyle);

            var heightProp = serializedConfig.FindProperty("AddtionHeight");
            EditorGUILayout.PropertyField(heightProp, new GUIContent("整体抬高（米）"));
            heightProp.floatValue = Mathf.Max(0, heightProp.floatValue);

            SerializedProperty updateCenterProp = serializedConfig.FindProperty("isUpdateCenter");
            EditorGUILayout.PropertyField(updateCenterProp, new GUIContent("自动更新中心点"));

            EditorGUI.BeginDisabledGroup(updateCenterProp.boolValue);
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("TileLevel"), new GUIContent("Tile 级别"));
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("CenterLongitude"), new GUIContent("中心经度"));
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("CenterLatitude"), new GUIContent("中心纬度"));
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(4);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("Scale"), new GUIContent("场景缩放（只读）"));
            EditorGUI.EndDisabledGroup();

            DrawTerrainSizePopup();

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("IsLoadRoad"), new GUIContent("加载道路"));
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("IsLoadHyda"), new GUIContent("水面挖坑"));
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("IsLoadVega"), new GUIContent("加载绿地"));

            EditorGUILayout.EndVertical();
        }

        private void DrawTerrainSizePopup()
        {
            SerializedProperty terrainSizeProp = serializedConfig.FindProperty("TerrainSize");

            var enumValues = (TerrainSize[])System.Enum.GetValues(typeof(TerrainSize));
            string[] display = enumValues.Select(v => ((int)v).ToString()).ToArray();

            int newIndex = EditorGUILayout.Popup("地形尺寸", terrainSizeProp.enumValueIndex, display);
            if (newIndex != terrainSizeProp.enumValueIndex)
                terrainSizeProp.enumValueIndex = newIndex;
        }

        private void DrawSceneObjectConfig()
        {
            foldSceneObject = EditorGUILayout.Foldout(
                foldSceneObject,
                "🌊 场景对象引用",
                true,
                headerStyle
            );

            if (!foldSceneObject) return;

            EditorGUILayout.BeginVertical(boxStyle);

            DrawObjectWithReset(
                "海洋对象",
                "OceanObject",
                GetOceanGo
            );

            DrawObjectWithReset(
                "风场对象",
                "WindyObject",
                GetWindyGo
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawObjectWithReset(string label, string propName, System.Func<GameObject> resetFunc)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(
                serializedConfig.FindProperty(propName),
                new GUIContent(label)
            );

            if (GUILayout.Button("↺", GUILayout.Width(28)))
            {
                var prop = serializedConfig.FindProperty(propName);
                prop.objectReferenceValue = resetFunc?.Invoke();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawImportSection()
        {
            foldImport = EditorGUILayout.Foldout(
                foldImport,
                "📦 数据导入",
                true,
                headerStyle
            );

            if (!foldImport) return;

            EditorGUILayout.BeginVertical(boxStyle);

            DrawImportGroup(
                "🌍 Terrain / 场景",
                2,
                ("Terrain", () => GeoImportWindow.ShowWindow(LoadTerrain, ImportType.Terrain)),
                ("绿地种树", () => TerrainImport.TerrainVegaImport(TerrainSavePath, config)),
                ("水面挖坑", () => TerrainImport.TerrainHydaImport(TerrainSavePath, config)),
                ("模型道路", () => TerrainImport.TerrainRoadImport(TerrainSavePath, config))
            );

            EditorGUILayout.Space(6);

            DrawImportGroup(
                "🏗 模型",
                2,
                ("数字表亲模型", () => GeoImportWindow.ShowWindow(LoadCombineAutoModel, ImportType.AutoModel)),
                ("自定义模型（fbx/glb/gltf）", () => GeoImportWindow.ShowWindow(LoadCustomModel, ImportType.CustomModel))
            );

            EditorGUILayout.Space(6);

            DrawImportGroup(
                "🧩 数据",
                2,
                ("GeoJSON", () => GeoImportWindow.ShowWindow(LoadGeoJSON, ImportType.GeoJSON)),
                ("风场", () => GeoImportWindow.ShowWindow(LoadWindy, ImportType.Windy)),
                ("3DTiles", () => Geo3DTilesWindow.ShowWindow(On3DTilesSettingsConfirmed)),
                ("Gaussian", () => GaussianImportWindow.ShowWindow(OnGaussianSettingsConfirmed))
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawImportGroup(string title, int column, params (string label, System.Action action)[] buttons)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            int count = buttons.Length;
            int rows = Mathf.CeilToInt(count / (float)column);

            for (int r = 0; r < rows; r++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < column; c++)
                {
                    int index = r * column + c;
                    if (index < count)
                    {
                        if (GUILayout.Button(buttons[index].label, bigButtonStyle))
                        {
                            buttons[index].action?.Invoke();
                        }
                    }
                    else
                    {
                        GUILayout.FlexibleSpace();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }



        #endregion


        private void LoadCustomModel(string folderName, SelectType type)
        {
            CustomModelImport.Import(CustomModelSavePath, folderName, type, config);
        }

        private void LoadGeoJSON(string folderName, SelectType selectType)
        {
            GeoJSONImport.Import(GeoJsonSavePath, folderName, selectType, config);

        }


        private void OnGaussianSettingsConfirmed(GaussianImportWindow.GaussianImportSettings settings)
        {
            GaussianImport.Import(GaussianSavePath, settings, config);
        }

        private void On3DTilesSettingsConfirmed(string serverPath)
        {
            Geo3DTilesImport.Import(serverPath, config);
        }

        private void LoadWindy(string folderName, SelectType selectType)
        {
            WindyImport.Import(WindySavePath, folderName, selectType, config);
        }

        private static void LoadTerrain(string folderName, SelectType selectType)
        {
            TerrainImport.Import(TerrainSavePath, folderName, selectType, config);
        }

        private static void LoadCombineAutoModel(string folderName, SelectType selectType)
        {
            AutoModelImport.Import(AutoModelSavePath, folderName, selectType, config);
        }

        //private static void LoadAutoModel(string folderName)
        //{
        //    AutoModelImport.Import(AutoModelSavePath, folderName, config, false);
        //}



        private static void Load3DGaussian(string folderName)
        {

        }




        private static void LoadGridCode(string gridName, bool isCustom, bool isLoadedSpace, int regionWidth, int regionHeight, int level, GridCodeControlPanel.LODSetting[] lodSetting, GameObject treeParent, LayerMask mask, List<GameObject> select)
        {
            var hitGridMat = GetHitGridMaterial();
            hitGridMat.renderQueue = 3000 + 10;

            var groundGridMat = GetGroundGridMaterial();
            groundGridMat.renderQueue = 3000 + 10;

            var spaceGridMat = GetSpaceGridMaterial();
            spaceGridMat.renderQueue = 3000 + 100;

            var code = string.IsNullOrEmpty(gridName) ? GeoImportWindow.GenerateUniqueCode() : gridName;

            if (isCustom)
            {

                var terrainArray = GetTerrainsFromGameObjects(select);

                GridWorldManager.GridWorld(GridCodeSavePath, code, isLoadedSpace, hitGridMat, groundGridMat, spaceGridMat, lodSetting, treeParent, mask, terrainArray, regionWidth, regionHeight);
                //GridWorldManager.SpaceGridWorld(TerrainSavePath, gridWorldMat, 3, 10, 10);
            }

        }

        private static Terrain[] GetTerrainsFromGameObjects(List<GameObject> select)
        {
            if (select == null || select.Count == 0)
            {
                Debug.LogWarning("传入的GameObject列表为空或为null");
                return new Terrain[0];
            }

            List<Terrain> terrains = new List<Terrain>();

            foreach (GameObject gameObject in select)
            {
                if (gameObject == null)
                {
                    continue;
                }

                // 检查GameObject上是否有Terrain组件
                Terrain terrain = gameObject.GetComponent<Terrain>();
                if (terrain != null)
                {
                    terrains.Add(terrain);
                }
            }

            Debug.Log($"从 {select.Count} 个GameObject中找到了 {terrains.Count} 个Terrain");
            return terrains.ToArray();
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        public static GeoPlatformConfig LoadConfig()
        {
            string configPath = $"{ConfigSavePath}/GeoPlatformConfig.asset";
            config = AssetDatabase.LoadAssetAtPath<GeoPlatformConfig>(configPath);

            if (config == null)
            {
                Debug.LogWarning($"未找到配置文件，路径: {configPath}");
                return null;
            }
            else
            {
                LoadConifgCenter();
                Debug.Log($"成功加载配置文件，路径: {configPath}");
            }

            return config;
        }

        /// <summary>
        /// 创建配置文件
        /// </summary>
        private static void CreateConfig()
        {
            config = ScriptableObject.CreateInstance<GeoPlatformConfig>();

            if (!AssetDatabase.IsValidFolder(ConfigSavePath))
            {
                if (!AssetDatabase.IsValidFolder("Assets/GeoToolkit"))
                {
                    AssetDatabase.CreateFolder("Assets", "GeoToolkit");
                }
                AssetDatabase.CreateFolder("Assets/GeoToolkit", "Config");
            }

            AssetDatabase.CreateAsset(config, $"{ConfigSavePath}/GeoPlatformConfig.asset");

            AssetDatabase.SaveAssets();    // ✅ 新增
            AssetDatabase.Refresh();       // ✅ 新增
            SaveConfig(config);
            LoadConifgCenter();
        }



        /// <summary>
        /// 加载配置文件中心点
        /// </summary>
        private static void LoadConifgCenter()
        {
            if (config != null && config.OceanObject == null)
            {
                config.OceanObject = GetOceanGo();
            }

            if (config != null && config.WindyObject == null)
            {
                config.WindyObject = GetWindyGo();
            }

            if (config != null && config.TreesObjects.Count == 0)
            {
                config.TreesObjects = GetTreesGo();
            }

            if (config != null && config.GrassObjects.Count == 0)
            {
                config.GrassObjects = GetGrassGo();
            }
            config.Scale = config.GetScale();
        }

        /// <summary>
        /// 设置配置文件中心点
        /// </summary>
        /// <param name="tileID"></param>
        private static void SetConfigCenter(string tileID)
        {
            if (config == null) return;
            bool isCenterEmpty = config.TileLevel == 0;
            if (config.isUpdateCenter || isCenterEmpty)
            {
                SetConfigCenter(config, tileID);
            }

        }

        /// <summary>
        /// 初始化坐标转换类
        /// </summary>
        /// <param name="tileID"></param>
        public static void LoadGeoCoordinate(string tileID)
        {
            LoadConfig();
            SetConfigCenter(tileID);
            GeoCoordinateUtils.Initialize(config);
        }

        /// <summary>
        /// 保存位置文件
        /// </summary>
        /// <param name="config"></param>
        public static void SaveConfig(GeoPlatformConfig config)
        {
            if (config != null)
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("配置文件已保存。");
            }
            else
            {
                Debug.LogWarning("配置文件为空，无法保存。");
            }
        }

        /// <summary>
        /// 设置配置文件中心点
        /// </summary>
        /// <param name="config"></param>
        /// <param name="tileID"></param>
        public static void SetConfigCenter(GeoPlatformConfig config, string tileID)
        {
            if (string.IsNullOrEmpty(tileID))
            {
                Debug.LogWarning("TileID 数据为空，无法设置中心点。");
                return;
            }

            var centerTile = TileProcessor.StringToTile(tileID);
            var centerPos = TileProcessor.Get3857TileLeftBottomLonLat(centerTile);
            config.CenterLongitude = centerPos.x;
            config.CenterLatitude = centerPos.y;
            config.TileLevel = centerTile.z;
            config.Scale = config.GetScale();
            //config.TerrainSize = 2048;
            SaveConfig(config);


        }




        /// <summary>
        /// 序列化集合对象
        /// </summary>
        private void InitializeTreesList()
        {
            // 初始化 ReorderableList
            treesObjectsList = new ReorderableList(
                serializedConfig,
                serializedConfig.FindProperty("TreesObjects"),
                true, true, true, true
            );

            // 设置 ReorderableList 的标题
            treesObjectsList.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, "树木集合");
            };

            // 设置 ReorderableList 的元素绘制逻辑
            treesObjectsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                var element = treesObjectsList.serializedProperty.GetArrayElementAtIndex(index);
                EditorGUI.ObjectField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    element,
                    typeof(GameObject),
                    GUIContent.none
                );
            };
        }

        private void InitializeGrassList()
        {
            // 初始化 ReorderableList
            grassObjectsList = new ReorderableList(
                serializedConfig,
                serializedConfig.FindProperty("GrassObjects"),
                true, true, true, true
            );

            // 设置 ReorderableList 的标题
            grassObjectsList.drawHeaderCallback = (Rect rect) =>
            {
                // 绘制标题
                EditorGUI.LabelField(rect, "草地集合");

                // 在标题右侧添加重置按钮
                float buttonWidth = 60f;
                Rect buttonRect = new Rect(rect.x + rect.width - buttonWidth + 3, rect.y, buttonWidth, rect.height);
                if (GUI.Button(buttonRect, "重置"))
                {
                    // 调用方法获取草地对象列表
                    List<GameObject> grassObjects = GetGrassGo(); // 替换为你的实际方法

                    // 更新序列化属性
                    SerializedProperty grassObjectsProp = serializedConfig.FindProperty("GrassObjects");
                    if (grassObjectsProp != null && grassObjectsProp.isArray)
                    {
                        grassObjectsProp.ClearArray();

                        // 添加新的草地对象
                        for (int i = 0; i < grassObjects.Count; i++)
                        {
                            grassObjectsProp.InsertArrayElementAtIndex(i);
                            SerializedProperty element = grassObjectsProp.GetArrayElementAtIndex(i);
                            element.objectReferenceValue = grassObjects[i];
                        }

                        serializedConfig.ApplyModifiedProperties();
                        EditorUtility.SetDirty(config);
                        AssetDatabase.SaveAssets();

                        // 刷新界面
                        grassObjectsList.index = -1;
                    }
                }
            };

            // 设置 ReorderableList 的元素绘制逻辑
            grassObjectsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                var element = grassObjectsList.serializedProperty.GetArrayElementAtIndex(index);
                EditorGUI.ObjectField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    element,
                    typeof(GameObject),
                    GUIContent.none
                );
            };
        }


        private static (GameObject, GameObject) GetGlobalVolume()
        {
            var enviroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.geoearth.platformsdk/Examples/Global Volume/Style 4/Enviro 3.prefab");
            var globeVolume = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.geoearth.platformsdk/Examples/Global Volume/Style 4/Global Volume.prefab");
            return (enviroPrefab, globeVolume);
        }

        private static Material GetHitGridMaterial()
        {
            return AssetDatabase.LoadAssetAtPath<Material>("Packages/com.geoearth.platformsdk/Editor/ImporterFramework/Importers/GridCode/Materials/HitGrid.mat");
        }

        private static Material GetGroundGridMaterial()
        {
            return AssetDatabase.LoadAssetAtPath<Material>("Packages/com.geoearth.platformsdk/Editor/ImporterFramework/Importers/GridCode/Materials/GroundGrid.mat");
        }

        private static Material GetSpaceGridMaterial()
        {
            return AssetDatabase.LoadAssetAtPath<Material>("Packages/com.geoearth.platformsdk/Editor/ImporterFramework/Importers/GridCode/Materials/SpaceGrid.mat");
        }

        /// <summary>
        /// 从SDK中获取Ocean对象
        /// </summary>
        /// <returns></returns>
        public static GameObject GetOceanGo()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.geoearth.platformsdk/Prefabs/CrestWater/CrestWater.prefab");
        }

        //public static Material GetRoadMaterial()
        //{
        //    return AssetDatabase.LoadAssetAtPath<Material>("Packages/com.geoearth.platformsdk/Shaders/TrafficFlowLine.mat");
        //}

        public static GameObject GetWindyGo()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.geoearth.platformsdk/Editor/ImporterFramework/Importers/Windy/WindyEffect.prefab");
        }





        //绿地资源路径
        private const string VegaResourcesPath = "Packages/com.geoearth.platformsdk/Editor/ImporterFramework/Importers/Terrain/Vega/";

        public static List<GameObject> GetGrassGo()
        {
            List<GameObject> grass = new List<GameObject>();
            var tree1 = AssetDatabase.LoadAssetAtPath<GameObject>($"{VegaResourcesPath}BuiltinResources/Grass/Prefabs/Grass_01.prefab");
            var tree2 = AssetDatabase.LoadAssetAtPath<GameObject>($"{VegaResourcesPath}BuiltinResources/Flower/Prefabs/Flower_01.prefab");
            var tree3 = AssetDatabase.LoadAssetAtPath<GameObject>($"{VegaResourcesPath}BuiltinResources/Stones/Prefabs/Stones_01.prefab");

            grass.Add(tree1);
            grass.Add(tree2);
            grass.Add(tree3);

            return grass;
        }

        public static List<GameObject> GetTreesGo()
        {
            List<GameObject> trees = new List<GameObject>();
            var tree1 = AssetDatabase.LoadAssetAtPath<GameObject>($"{VegaResourcesPath}BuiltinResources/Trees/Prefabs/Tree_01.prefab");
            var tree2 = AssetDatabase.LoadAssetAtPath<GameObject>($"{VegaResourcesPath}BuiltinResources/Trees/Prefabs/Tree_02.prefab");
            var tree3 = AssetDatabase.LoadAssetAtPath<GameObject>($"{VegaResourcesPath}BuiltinResources/Trees/Prefabs/Tree_03.prefab");
            var tree4 = AssetDatabase.LoadAssetAtPath<GameObject>($"{VegaResourcesPath}BuiltinResources/Trees/Prefabs/Tree_04.prefab");
            var tree5 = AssetDatabase.LoadAssetAtPath<GameObject>($"{VegaResourcesPath}BuiltinResources/Trees/Prefabs/Tree_05.prefab");

            trees.Add(tree1);
            trees.Add(tree2);
            trees.Add(tree3);
            trees.Add(tree4);
            trees.Add(tree5);

            //resourceReference = new ResourceReference();
            //resourceReference.grassTerrainLayerPath = $"{RootPath}BuiltinResources/TerrainLayers/Prefabs/GrassLayer_01.terrainlayer";
            //resourceReference.treesPrefabPath = new List<string>();
            //resourceReference.treesPrefabPath.Add($"{RootPath}BuiltinResources/Trees/Prefabs/Tree_01.prefab");
            //resourceReference.treesPrefabPath.Add($"{RootPath}BuiltinResources/Trees/Prefabs/Tree_02.prefab");
            //resourceReference.treesPrefabPath.Add($"{RootPath}BuiltinResources/Trees/Prefabs/Tree_03.prefab");
            //resourceReference.treesPrefabPath.Add($"{RootPath}BuiltinResources/Trees/Prefabs/Tree_04.prefab");
            //resourceReference.treesPrefabPath.Add($"{RootPath}BuiltinResources/Trees/Prefabs/Tree_05.prefab");

            //resourceReference.grassPrefabPath = new List<string>();
            //resourceReference.grassPrefabPath.Add($"{RootPath}BuiltinResources/Grass/Prefabs/Grass_01.prefab");
            //resourceReference.grassPrefabPath.Add($"{RootPath}BuiltinResources/Flower/Prefabs/Flower_01.prefab");
            //resourceReference.grassPrefabPath.Add($"{RootPath}BuiltinResources/Stones/Prefabs/Stones_01.prefab");

            return trees;
        }
    }

}


#endif