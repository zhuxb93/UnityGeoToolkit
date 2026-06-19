#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;

namespace GeoToolkit.RadarVoxel
{
    /// <summary>
    /// 雷达体素化控制面板
    /// </summary>
    public class RadarVoxelControlPanel : EditorWindow
    {
        private static RadarVoxelControlPanel window;

        private RadarParameters radarParams = new RadarParameters();
        private VoxelSettings voxelSettings = new VoxelSettings();
        private PerformanceSettings performanceSettings = PerformanceSettings.Default();

        private bool showOccludedAreas = true;
        private bool enableCollisionDetection = true;

        private const string ImportRootPath = "Assets/ImportRoot/RadarVoxel";
        private string prefabName = "RadarVoxel";
        private bool generateColliders = false;

        private Material spaceMaterial;
        private Material obstacleMaterial;

        private Vector2 scrollPosition;
        private bool showRadarSettings = true;
        private bool showVoxelSettings = true;
        private bool showPerformanceSettings = false;
        private bool showVisualizationSettings = true;
        private bool showOutputSettings = true;

        private bool isGenerating = false;

        // EditorPrefs 键名
        private const string PREF_PREFIX = "RadarVoxelPanel_";
        private const string PREF_RADAR_POS = PREF_PREFIX + "RadarPos";
        private const string PREF_RADAR_ROT = PREF_PREFIX + "RadarRot";
        private const string PREF_RADAR_H_FOV = PREF_PREFIX + "HorizontalFOV";
        private const string PREF_RADAR_V_FOV = PREF_PREFIX + "VerticalFOV";
        private const string PREF_RADAR_MIN_DIST = PREF_PREFIX + "MinDistance";
        private const string PREF_RADAR_MAX_DIST = PREF_PREFIX + "MaxDistance";
        private const string PREF_RADAR_SCAN_MODE = PREF_PREFIX + "ScanMode";
        private const string PREF_VOXEL_WIDTH = PREF_PREFIX + "VoxelWidth";
        private const string PREF_VOXEL_HEIGHT = PREF_PREFIX + "VoxelHeight";
        private const string PREF_VOXEL_DEPTH = PREF_PREFIX + "VoxelDepth";
        private const string PREF_SHOW_OCCLUDED = PREF_PREFIX + "ShowOccluded";
        private const string PREF_ENABLE_COLLISION = PREF_PREFIX + "EnableCollision";
        private const string PREF_PREFAB_NAME = PREF_PREFIX + "PrefabName";
        private const string PREF_GENERATE_COLLIDERS = PREF_PREFIX + "GenerateColliders";

        // 性能配置相关
        private const string PREF_PERF_MAX_VOXELS = PREF_PREFIX + "Perf_MaxVoxelsPerBatch";
        private const string PREF_PERF_RAYCAST_BATCH = PREF_PREFIX + "Perf_RaycastBatchSize";
        private const string PREF_PERF_MAX_VERTICES = PREF_PREFIX + "Perf_MaxVerticesPerMesh";
        private const string PREF_PERF_FOV_JOB = PREF_PREFIX + "Perf_FovJobBatchSize";
        private const string PREF_PERF_RAYCAST_CMD_JOB = PREF_PREFIX + "Perf_RaycastCmdJobBatchSize";
        private const string PREF_PERF_RAYCAST_EXEC = PREF_PREFIX + "Perf_RaycastExecBatchSize";
        private const string PREF_PERF_MESH_JOB = PREF_PREFIX + "Perf_MeshJobBatchSize";

        [MenuItem("GeoToolkit/行业组件/雷达体素化/控制面板", false, priority = 300)]
        public static void ShowWindow()
        {
            window = GetWindow<RadarVoxelControlPanel>("雷达体素化控制面板");
            window.minSize = new Vector2(400, 600);
        }

        private void OnEnable()
        {
            LoadDefaultMaterials();
            LoadSettings();  // 加载保存的配置
            RadarVoxelGizmos.SetRadarParameters(radarParams);
            RadarVoxelGizmos.ToggleGizmos(true);
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("雷达体素化控制面板", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 雷达参数设置
            DrawRadarSettings();

            EditorGUILayout.Space();

            // 体素化设置
            DrawVoxelSettings();

            EditorGUILayout.Space();

            // 性能配置
            DrawPerformanceSettings();

            EditorGUILayout.Space();

            // 可视化设置
            DrawVisualizationSettings();

            EditorGUILayout.Space();

            // 输出设置
            DrawOutputSettings();

            EditorGUILayout.Space();

            // 操作按钮
            DrawActionButtons();

            EditorGUILayout.EndScrollView();

            // 如果有任何GUI值改变，自动保存配置
            if (EditorGUI.EndChangeCheck())
            {
                SaveSettings();
            }
        }

        private void DrawRadarSettings()
        {
            showRadarSettings = EditorGUILayout.Foldout(showRadarSettings, "雷达参数设置", true);
            if (!showRadarSettings) return;

            EditorGUI.indentLevel++;

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("位置和朝向", EditorStyles.boldLabel);
            radarParams.position = EditorGUILayout.Vector3Field("位置", radarParams.position);
            radarParams.rotation = EditorGUILayout.Vector3Field("朝向", radarParams.rotation);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("视场角设置", EditorStyles.boldLabel);
            radarParams.horizontalFOV = EditorGUILayout.Slider("水平FOV", radarParams.horizontalFOV, 0f, 360f);
            radarParams.verticalFOV = EditorGUILayout.Slider("垂直FOV", radarParams.verticalFOV, 0f, 180f);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("探测距离", EditorStyles.boldLabel);
            radarParams.minDistance = EditorGUILayout.FloatField("最小距离", radarParams.minDistance);
            radarParams.maxDistance = EditorGUILayout.FloatField("最大距离", radarParams.maxDistance);

            EditorGUILayout.Space();

            radarParams.scanMode = (RadarParameters.ScanMode)EditorGUILayout.EnumPopup("扫描模式", radarParams.scanMode);

            if (GUILayout.Button("从场景选择雷达位置"))
            {
                if (Selection.activeTransform != null)
                {
                    radarParams.position = Selection.activeTransform.position;
                    radarParams.rotation = Selection.activeTransform.eulerAngles;
                }
                else
                {
                    EditorUtility.DisplayDialog("提示", "请先在场景中选择一个对象作为雷达位置", "确定");
                }
            }

            EditorGUILayout.EndVertical();

            EditorGUI.indentLevel--;

            RadarVoxelGizmos.SetRadarParameters(radarParams);
        }

        private void DrawVoxelSettings()
        {
            showVoxelSettings = EditorGUILayout.Foldout(showVoxelSettings, "体素化设置", true);
            if (!showVoxelSettings) return;

            EditorGUI.indentLevel++;

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("体素尺寸", EditorStyles.boldLabel);
            voxelSettings.voxelWidth = EditorGUILayout.FloatField("宽度", voxelSettings.voxelWidth);
            voxelSettings.voxelHeight = EditorGUILayout.FloatField("高度", voxelSettings.voxelHeight);
            voxelSettings.voxelDepth = EditorGUILayout.FloatField("深度", voxelSettings.voxelDepth);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("遮挡分析", EditorStyles.boldLabel);
            enableCollisionDetection = EditorGUILayout.Toggle("启用遮挡分析", enableCollisionDetection);

            EditorGUILayout.HelpBox(
                enableCollisionDetection
                    ? "使用单射线检测,将被遮挡的体素标记为障碍物类型"
                    : "禁用遮挡分析将提升生成速度,所有体素标记为空间体素",
                enableCollisionDetection ? MessageType.Info : MessageType.Warning);

            EditorGUILayout.EndVertical();

            EditorGUI.indentLevel--;
        }

        private void DrawPerformanceSettings()
        {
            showPerformanceSettings = EditorGUILayout.Foldout(showPerformanceSettings, "性能配置 (高级)", true);
            if (!showPerformanceSettings) return;

            EditorGUI.indentLevel++;

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("预设配置", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("快速模式"))
            {
                performanceSettings = PerformanceSettings.Fast();
            }
            if (GUILayout.Button("默认模式"))
            {
                performanceSettings = PerformanceSettings.Default();
            }
            if (GUILayout.Button("高性能模式"))
            {
                performanceSettings = PerformanceSettings.HighPerformance();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("批次大小配置", EditorStyles.boldLabel);

            performanceSettings.maxVoxelsPerBatch = EditorGUILayout.IntSlider(
                new GUIContent("FOV筛选批次", "每批处理的体素数量，越大占用内存越多但可能更快"),
                performanceSettings.maxVoxelsPerBatch,
                100_000,
                10_000_000
            );

            performanceSettings.raycastBatchSize = EditorGUILayout.IntSlider(
                new GUIContent("Raycast批次", "每批处理的射线数量"),
                performanceSettings.raycastBatchSize,
                50_000,
                2_000_000
            );

            performanceSettings.maxVerticesPerMesh = EditorGUILayout.IntSlider(
                new GUIContent("Mesh最大顶点数", "单个Mesh的最大顶点数限制"),
                performanceSettings.maxVerticesPerMesh,
                1_000_000,
                10_000_000
            );

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Job并行度配置", EditorStyles.boldLabel);

            performanceSettings.fovJobBatchSize = EditorGUILayout.IntSlider(
                new GUIContent("FOV Job批次", "FOV筛选Job的批次大小，越小并行度越高"),
                performanceSettings.fovJobBatchSize,
                16,
                1024
            );

            performanceSettings.raycastCommandJobBatchSize = EditorGUILayout.IntSlider(
                new GUIContent("Raycast命令Job批次", "Raycast命令生成Job的批次大小"),
                performanceSettings.raycastCommandJobBatchSize,
                256,
                4096
            );

            performanceSettings.raycastExecutionBatchSize = EditorGUILayout.IntSlider(
                new GUIContent("Raycast执行批次", "Raycast执行的批次大小"),
                performanceSettings.raycastExecutionBatchSize,
                16,
                256
            );

            performanceSettings.meshDataJobBatchSize = EditorGUILayout.IntSlider(
                new GUIContent("Mesh数据Job批次", "Mesh数据生成Job的批次大小"),
                performanceSettings.meshDataJobBatchSize,
                128,
                2048
            );

            EditorGUILayout.HelpBox(
                "快速模式适合低配置机器，使用较小批次降低内存占用。\n" +
                "高性能模式适合高配置机器，使用较大批次提升处理速度。\n" +
                "建议保持默认值，除非遇到性能或内存问题。",
                MessageType.Info
            );

            EditorGUILayout.EndVertical();

            EditorGUI.indentLevel--;
        }

        private void DrawVisualizationSettings()
        {
            showVisualizationSettings = EditorGUILayout.Foldout(showVisualizationSettings, "可视化设置", true);
            if (!showVisualizationSettings) return;

            EditorGUI.indentLevel++;

            EditorGUILayout.BeginVertical("box");

            showOccludedAreas = EditorGUILayout.Toggle("显示遮挡区域", showOccludedAreas);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("材质设置", EditorStyles.boldLabel);
            spaceMaterial = (Material)EditorGUILayout.ObjectField("空间材质", spaceMaterial, typeof(Material), false);
            obstacleMaterial = (Material)EditorGUILayout.ObjectField("障碍物材质", obstacleMaterial, typeof(Material), false);

            EditorGUILayout.EndVertical();

            EditorGUI.indentLevel--;
        }

        private void DrawOutputSettings()
        {
            showOutputSettings = EditorGUILayout.Foldout(showOutputSettings, "输出设置", true);
            if (!showOutputSettings) return;

            EditorGUI.indentLevel++;

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("保存路径", ImportRootPath);
            prefabName = EditorGUILayout.TextField("预制体名称", prefabName);
            generateColliders = EditorGUILayout.Toggle("生成碰撞体", generateColliders);

            EditorGUILayout.EndVertical();

            EditorGUI.indentLevel--;
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = !isGenerating;

            if (GUILayout.Button("生成并保存", GUILayout.Height(30)))
            {
                GenerateAndSaveRadarVoxels();
            }

            GUI.enabled = true;

            if (GUILayout.Button("清除所有雷达", GUILayout.Height(30)))
            {
                ClearAllRadarVoxels();
            }

            if (GUILayout.Button("关闭", GUILayout.Height(30)))
            {
                Close();
            }

            EditorGUILayout.EndHorizontal();

            if (isGenerating)
            {
                EditorGUILayout.HelpBox("正在生成并保存体素...", MessageType.Info);
            }
        }

        private void LoadDefaultMaterials()
        {
            spaceMaterial = FindOrCreateMaterial(
                "RadarVoxel_Space",
                "GeoToolkit/RadarVoxel",
                mat =>
                {
                    mat.SetColor("_Color", new Color(0.2f, 0.8f, 1f, 0.3f));
                    mat.SetColor("_EdgeColor", new Color(0.5f, 1f, 1f, 1f));
                    mat.SetFloat("_EdgeWidth", 0.15f);
                    mat.SetFloat("_EdgeIntensity", 2.0f);
                    mat.SetFloat("_Transparency", 0.3f);
                    mat.SetFloat("_FresnelPower", 2.0f);
                }
            );

            obstacleMaterial = FindOrCreateMaterial(
                "RadarVoxel_Obstacle",
                "GeoToolkit/RadarVoxelSolid",
                mat =>
                {
                    mat.SetColor("_Color", new Color(1f, 0.2f, 0.2f, 0.8f));
                    mat.SetColor("_EdgeColor", new Color(1f, 0.5f, 0.5f, 1f));
                    mat.SetFloat("_EdgeWidth", 0.1f);
                    mat.SetFloat("_EdgeIntensity", 1.5f);
                    mat.SetFloat("_Transparency", 0.8f);
                }
            );
        }

        /// <summary>
        /// 查找或创建材质
        /// 先在Package内的Materials目录查找,找不到再在ImportRoot创建
        /// </summary>
        private Material FindOrCreateMaterial(string materialName, string shaderName, System.Action<Material> setupAction)
        {
            string fileName = $"{materialName}.mat";

            // 方法1: 获取当前脚本所在目录(适用于Package发布后)
            string[] scriptGuids = AssetDatabase.FindAssets($"{GetType().Name} t:Script");
            foreach (string guid in scriptGuids)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                if (scriptPath.Contains("RadarVoxelControlPanel"))
                {
                    string scriptDirectory = Path.GetDirectoryName(scriptPath).Replace("\\", "/");
                    string localMaterialsPath = Path.Combine(scriptDirectory, "Materials").Replace("\\", "/");
                    string localMatPath = Path.Combine(localMaterialsPath, fileName).Replace("\\", "/");

                    Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(localMatPath);
                    if (existingMat != null)
                    {
                        return existingMat;
                    }
                    break;
                }
            }

            // 方法2: 其他备用路径搜索
            string[] searchPaths = new string[]
            {
                "Packages/GeoToolkit/Editor/ImporterFramework/Importers/RadarVoxel/Materials",
                "Packages/com.geo.platformsdk/Editor/ImporterFramework/Importers/RadarVoxel/Materials",
                "Assets/GeoToolkit/RadarVoxel/Materials",
                "Assets/RadarVoxel/Materials"
            };

            foreach (string searchPath in searchPaths)
            {
                string matPath = Path.Combine(searchPath, fileName).Replace("\\", "/");
                Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (existingMat != null)
                {
                    return existingMat;
                }
            }

            // 方法3: 全局搜索
            string[] guids = AssetDatabase.FindAssets($"{materialName} t:Material");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (assetPath.Contains("RadarVoxel") && assetPath.Contains("Materials"))
                {
                    Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                    if (existingMat != null && existingMat.name == materialName)
                    {
                        return existingMat;
                    }
                }
            }

            // 没找到,创建新材质
            string materialPath = Path.Combine(ImportRootPath, "Materials").Replace("\\", "/");
            EnsureDirectoryExists(materialPath);

            string newMatPath = Path.Combine(materialPath, fileName).Replace("\\", "/");
            return CreateMaterial(newMatPath, shaderName, setupAction);
        }

        /// <summary>
        /// 创建新材质
        /// </summary>
        private Material CreateMaterial(string path, string shaderName, System.Action<Material> setupAction)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[RadarVoxel] 未找到Shader: {shaderName}, 使用Standard");
                shader = Shader.Find("Standard");
            }

            Material mat = new Material(shader);
            setupAction?.Invoke(mat);

            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[RadarVoxel] 创建新材质: {path}");
            return mat;
        }

        /// <summary>
        /// 确保目录存在
        /// </summary>
        private void EnsureDirectoryExists(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            string[] folders = assetPath.Split('/');
            string currentPath = folders[0];

            for (int i = 1; i < folders.Length; i++)
            {
                string nextPath = currentPath + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                currentPath = nextPath;
            }

            AssetDatabase.Refresh();
        }

        private void GenerateAndSaveRadarVoxels()
        {
            if (!ValidateSettings()) return;

            if (string.IsNullOrWhiteSpace(prefabName))
            {
                EditorUtility.DisplayDialog("错误", "预制体名称不能为空", "确定");
                return;
            }

            try
            {
                isGenerating = true;
                EditorUtility.DisplayProgressBar("生成体素", "正在生成雷达体素...", 0f);

                GameObject generated = RadarVoxelManager.GenerateRadarVoxelsStreaming(
                    radarParams,
                    voxelSettings,
                    performanceSettings,
                    showOccludedAreas,
                    generateColliders,
                    enableCollisionDetection,
                    spaceMaterial,
                    obstacleMaterial,
                    prefabName
                );

                if (generated != null)
                {
                    EditorUtility.DisplayProgressBar("保存资源", "正在保存预制体和Mesh...", 0.9f);

                    string savedPath = RadarVoxelManager.SaveAsPrefab(
                        generated,
                        ImportRootPath,
                        prefabName
                    );

                    if (!string.IsNullOrEmpty(savedPath))
                    {
                        // 选中生成的对象并定位到它
                        Selection.activeGameObject = generated;
                        SceneView.lastActiveSceneView?.FrameSelected();

                        // 生成成功，不弹窗提示，控制台日志已记录
                        Debug.Log($"[RadarVoxel] ✓ 生成成功并保存到: {savedPath}");
                    }
                    else
                    {
                        // 只在失败时弹窗提示
                        EditorUtility.DisplayDialog("警告",
                            "体素已生成但保存失败，请查看控制台日志",
                            "确定");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"生成雷达体素失败: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("错误", $"生成失败: {ex.Message}", "确定");
            }
            finally
            {
                isGenerating = false;
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// 清除场景中的所有雷达体素对象
        /// </summary>
        private void ClearAllRadarVoxels()
        {
            // 搜索并清除所有雷达体素对象（以RadarVoxel开头的对象）
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            int destroyedCount = 0;

            foreach (GameObject obj in allObjects)
            {
                // 检查对象名称是否以"RadarVoxel"开头且没有父对象
                if (obj.name.StartsWith("RadarVoxel") && obj.transform.parent == null)
                {
                    DestroyImmediate(obj);
                    destroyedCount++;
                }
            }

            if (destroyedCount > 0)
            {
                Debug.Log($"[RadarVoxel] 已清除 {destroyedCount} 个雷达体素对象");
            }
            else
            {
                Debug.Log("[RadarVoxel] 场景中没有找到雷达体素对象");
            }
        }

        private bool ValidateSettings()
        {
            if (!radarParams.Validate())
            {
                EditorUtility.DisplayDialog("错误", "雷达参数设置无效，请检查", "确定");
                return false;
            }

            if (!voxelSettings.Validate())
            {
                EditorUtility.DisplayDialog("错误", "体素设置无效，请检查", "确定");
                return false;
            }

            return true;
        }

        private void OnDestroy()
        {
            // 面板销毁时不清除场景中的对象，让用户自己管理
            // 只关闭Gizmos显示
            RadarVoxelGizmos.ToggleGizmos(false);

            // 注意：不再调用 ClearPreview()，场景中的体素对象将被保留
            // 如果需要清除，用户可以点击"清除场景"按钮或手动删除对象
        }

        #region 配置持久化

        /// <summary>
        /// 保存配置到EditorPrefs
        /// </summary>
        private void SaveSettings()
        {
            // 保存雷达参数
            EditorPrefs.SetString(PREF_RADAR_POS, $"{radarParams.position.x},{radarParams.position.y},{radarParams.position.z}");
            EditorPrefs.SetString(PREF_RADAR_ROT, $"{radarParams.rotation.x},{radarParams.rotation.y},{radarParams.rotation.z}");
            EditorPrefs.SetFloat(PREF_RADAR_H_FOV, radarParams.horizontalFOV);
            EditorPrefs.SetFloat(PREF_RADAR_V_FOV, radarParams.verticalFOV);
            EditorPrefs.SetFloat(PREF_RADAR_MIN_DIST, radarParams.minDistance);
            EditorPrefs.SetFloat(PREF_RADAR_MAX_DIST, radarParams.maxDistance);
            EditorPrefs.SetInt(PREF_RADAR_SCAN_MODE, (int)radarParams.scanMode);

            // 保存体素参数
            EditorPrefs.SetFloat(PREF_VOXEL_WIDTH, voxelSettings.voxelWidth);
            EditorPrefs.SetFloat(PREF_VOXEL_HEIGHT, voxelSettings.voxelHeight);
            EditorPrefs.SetFloat(PREF_VOXEL_DEPTH, voxelSettings.voxelDepth);

            // 保存可视化参数
            EditorPrefs.SetBool(PREF_SHOW_OCCLUDED, showOccludedAreas);
            EditorPrefs.SetBool(PREF_ENABLE_COLLISION, enableCollisionDetection);

            // 保存输出参数
            EditorPrefs.SetString(PREF_PREFAB_NAME, prefabName);
            EditorPrefs.SetBool(PREF_GENERATE_COLLIDERS, generateColliders);

            // 保存性能配置
            EditorPrefs.SetInt(PREF_PERF_MAX_VOXELS, performanceSettings.maxVoxelsPerBatch);
            EditorPrefs.SetInt(PREF_PERF_RAYCAST_BATCH, performanceSettings.raycastBatchSize);
            EditorPrefs.SetInt(PREF_PERF_MAX_VERTICES, performanceSettings.maxVerticesPerMesh);
            EditorPrefs.SetInt(PREF_PERF_FOV_JOB, performanceSettings.fovJobBatchSize);
            EditorPrefs.SetInt(PREF_PERF_RAYCAST_CMD_JOB, performanceSettings.raycastCommandJobBatchSize);
            EditorPrefs.SetInt(PREF_PERF_RAYCAST_EXEC, performanceSettings.raycastExecutionBatchSize);
            EditorPrefs.SetInt(PREF_PERF_MESH_JOB, performanceSettings.meshDataJobBatchSize);
        }

        /// <summary>
        /// 从EditorPrefs加载配置
        /// </summary>
        private void LoadSettings()
        {
            // 加载雷达参数
            if (EditorPrefs.HasKey(PREF_RADAR_POS))
            {
                string[] pos = EditorPrefs.GetString(PREF_RADAR_POS).Split(',');
                if (pos.Length == 3)
                {
                    radarParams.position = new Vector3(
                        float.Parse(pos[0]),
                        float.Parse(pos[1]),
                        float.Parse(pos[2])
                    );
                }
            }

            if (EditorPrefs.HasKey(PREF_RADAR_ROT))
            {
                string[] rot = EditorPrefs.GetString(PREF_RADAR_ROT).Split(',');
                if (rot.Length == 3)
                {
                    radarParams.rotation = new Vector3(
                        float.Parse(rot[0]),
                        float.Parse(rot[1]),
                        float.Parse(rot[2])
                    );
                }
            }

            radarParams.horizontalFOV = EditorPrefs.GetFloat(PREF_RADAR_H_FOV, radarParams.horizontalFOV);
            radarParams.verticalFOV = EditorPrefs.GetFloat(PREF_RADAR_V_FOV, radarParams.verticalFOV);
            radarParams.minDistance = EditorPrefs.GetFloat(PREF_RADAR_MIN_DIST, radarParams.minDistance);
            radarParams.maxDistance = EditorPrefs.GetFloat(PREF_RADAR_MAX_DIST, radarParams.maxDistance);

            if (EditorPrefs.HasKey(PREF_RADAR_SCAN_MODE))
            {
                radarParams.scanMode = (RadarParameters.ScanMode)EditorPrefs.GetInt(PREF_RADAR_SCAN_MODE);
            }

            // 加载体素参数
            voxelSettings.voxelWidth = EditorPrefs.GetFloat(PREF_VOXEL_WIDTH, voxelSettings.voxelWidth);
            voxelSettings.voxelHeight = EditorPrefs.GetFloat(PREF_VOXEL_HEIGHT, voxelSettings.voxelHeight);
            voxelSettings.voxelDepth = EditorPrefs.GetFloat(PREF_VOXEL_DEPTH, voxelSettings.voxelDepth);

            // 加载可视化参数
            showOccludedAreas = EditorPrefs.GetBool(PREF_SHOW_OCCLUDED, showOccludedAreas);
            enableCollisionDetection = EditorPrefs.GetBool(PREF_ENABLE_COLLISION, enableCollisionDetection);

            // 加载输出参数
            prefabName = EditorPrefs.GetString(PREF_PREFAB_NAME, prefabName);
            generateColliders = EditorPrefs.GetBool(PREF_GENERATE_COLLIDERS, generateColliders);

            // 加载性能配置
            if (EditorPrefs.HasKey(PREF_PERF_MAX_VOXELS))
            {
                performanceSettings.maxVoxelsPerBatch = EditorPrefs.GetInt(PREF_PERF_MAX_VOXELS, performanceSettings.maxVoxelsPerBatch);
                performanceSettings.raycastBatchSize = EditorPrefs.GetInt(PREF_PERF_RAYCAST_BATCH, performanceSettings.raycastBatchSize);
                performanceSettings.maxVerticesPerMesh = EditorPrefs.GetInt(PREF_PERF_MAX_VERTICES, performanceSettings.maxVerticesPerMesh);
                performanceSettings.fovJobBatchSize = EditorPrefs.GetInt(PREF_PERF_FOV_JOB, performanceSettings.fovJobBatchSize);
                performanceSettings.raycastCommandJobBatchSize = EditorPrefs.GetInt(PREF_PERF_RAYCAST_CMD_JOB, performanceSettings.raycastCommandJobBatchSize);
                performanceSettings.raycastExecutionBatchSize = EditorPrefs.GetInt(PREF_PERF_RAYCAST_EXEC, performanceSettings.raycastExecutionBatchSize);
                performanceSettings.meshDataJobBatchSize = EditorPrefs.GetInt(PREF_PERF_MESH_JOB, performanceSettings.meshDataJobBatchSize);
            }
        }

        #endregion
    }
}
#endif