using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GeoToolkit.GeoTiffTools
{
    public class GeoTiffToDecalOptions
    {
        public Transform ParentTransform { get; set; }
        public Material DefaultDecalMaterial { get; set; }
        public string OutputPrefabDirectory { get; set; }
        public float DecalHeight { get; set; } = 500f; // 贴花距离地面的高度
        public float ProjectionDepth { get; set; } = 1000f; // 投影深度
        public Vector3 DecalRotation { get; set; } = new Vector3(90f, 0f, 0f); // 贴花旋转角度
        public GeoPlatformConfig Config { get; set; } // 从编辑器传递的配置
    }

    public sealed class GeoTiffToDecalProcessor
    {
        private static bool _coordinateUtilsInitialized = false;
        public async Task<List<GeoTiffToExrMetadata>> ScanJsonFilesAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => ScanJsonFiles(folderPath, cancellationToken), cancellationToken);
        }

        public async Task<List<GeoTiffToExrMetadata>> GenerateDecalsAsync(List<GeoTiffToExrMetadata> jsonMetadataList, GeoTiffToDecalOptions options, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => GenerateDecals(jsonMetadataList, options, cancellationToken), cancellationToken);
        }

        private List<GeoTiffToExrMetadata> ScanJsonFiles(string folderPath, CancellationToken cancellationToken)
        {
            var results = new List<GeoTiffToExrMetadata>();

            if (!Directory.Exists(folderPath))
            {
                Debug.LogError($"[GeoTiffToDecal] 文件夹不存在：{folderPath}");
                return results;
            }

            string[] jsonFiles = Directory.GetFiles(folderPath, "*.json", SearchOption.AllDirectories);
            Debug.Log($"[GeoTiffToDecal] 找到 {jsonFiles.Length} 个 JSON 文件");

            foreach (string jsonFile in jsonFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    GeoTiffToExrMetadata metadata = ParseJsonFile(jsonFile);
                    if (metadata != null && IsValidGeoTiffToExrJson(metadata, jsonFile))
                    {
                        // 存储 JSON 文件路径以便后续查找 EXR 和生成预制体名称
                        metadata.sourceFile = metadata.sourceFile ?? Path.GetFileName(jsonFile);
                        metadata.jsonFilePath = jsonFile; // 存储完整的JSON文件路径

                        results.Add(metadata);
                        Debug.Log($"[GeoTiffToDecal] 解析成功：{Path.GetFileName(jsonFile)}");
                    }
                    else
                    {
                        Debug.LogWarning($"[GeoTiffToDecal] 跳过无效的 JSON 文件：{Path.GetFileName(jsonFile)}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GeoTiffToDecal] 跳过无效的 JSON 文件 {Path.GetFileName(jsonFile)}：{ex.Message}");
                }
            }

            Debug.Log($"[GeoTiffToDecal] 扫描完成，找到 {results.Count} 个有效的 GeoTiffToExr JSON 文件");
            return results;
        }

        private List<GeoTiffToExrMetadata> GenerateDecals(List<GeoTiffToExrMetadata> jsonMetadataList, GeoTiffToDecalOptions options, CancellationToken cancellationToken)
        {
            var processedFiles = new List<GeoTiffToExrMetadata>();

#if UNITY_EDITOR
            foreach (var metadata in jsonMetadataList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    EditorApplication.delayCall += () =>
                    {
                        try
                        {
                            CreateDecalGameObject(metadata, options);
                            Debug.Log($"[GeoTiffToDecal] 成功处理：{metadata.sourceFile}");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[GeoTiffToDecal] 生成贴花失败 {metadata.sourceFile}：{ex.Message}\n{ex.StackTrace}");
                        }
                    };
                    processedFiles.Add(metadata);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GeoTiffToDecal] 处理元数据失败 {metadata.sourceFile}：{ex.Message}\n{ex.StackTrace}");
                }
            }
#else
            Debug.LogWarning("[GeoTiffToDecal] GenerateDecals 只能在编辑器中使用");
#endif

            return processedFiles;
        }

        private void EnsureCoordinateUtilsInitialized(GeoPlatformConfig config)
        {
            // 避免重复初始化
            if (_coordinateUtilsInitialized)
                return;

            try
            {
                if (config != null)
                {
                    GeoCoordinateUtils.Initialize(config);
                    _coordinateUtilsInitialized = true;
                    Debug.Log("[GeoTiffToDecal] 坐标转换工具已初始化");
                }
                else
                {
                    Debug.LogWarning("[GeoTiffToDecal] 配置为空，坐标转换可能不准确");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GeoTiffToDecal] 初始化坐标转换工具失败：{ex.Message}");
            }
        }

        private GeoTiffToExrMetadata ParseJsonFile(string jsonFilePath)
        {
            try
            {
                if (!File.Exists(jsonFilePath))
                {
                    Debug.LogWarning($"[GeoTiffToDecal] JSON 文件不存在：{jsonFilePath}");
                    return null;
                }

                string jsonContent = File.ReadAllText(jsonFilePath);
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    Debug.LogWarning($"[GeoTiffToDecal] JSON 文件为空：{Path.GetFileName(jsonFilePath)}");
                    return null;
                }

                var metadata = JsonUtility.FromJson<GeoTiffToExrMetadata>(jsonContent);
                if (metadata != null)
                {
                    // 确保基本字段不为 null
                    metadata.sourceFile = metadata.sourceFile ?? "";
                    metadata.exrFile = metadata.exrFile ?? "";
                    metadata.requestedDataFormat = metadata.requestedDataFormat ?? "";
                    metadata.encodedDataFormat = metadata.encodedDataFormat ?? "";
                }

                return metadata;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GeoTiffToDecal] 解析 JSON 失败 {Path.GetFileName(jsonFilePath)}：{ex.Message}");
                return null;
            }
        }

        private bool IsValidGeoTiffToExrJson(GeoTiffToExrMetadata metadata, string jsonFilePath)
        {
            try
            {
                // 基本字段检查
                if (metadata == null)
                {
                    Debug.LogWarning($"[GeoTiffToDecal] JSON 解析为 null：{Path.GetFileName(jsonFilePath)}");
                    return false;
                }

                if (string.IsNullOrEmpty(metadata.exrFile))
                {
                    Debug.LogWarning($"[GeoTiffToDecal] 缺少 exrFile 字段：{Path.GetFileName(jsonFilePath)}");
                    return false;
                }

                if (metadata.width <= 0 || metadata.height <= 0)
                {
                    Debug.LogWarning($"[GeoTiffToDecal] 无效的宽高 ({metadata.width}x{metadata.height})：{Path.GetFileName(jsonFilePath)}");
                    return false;
                }

                // 检查是否是 GeoTiffToExr 生成的 JSON
                // 必须有 requestedDataFormat 或 encodedDataFormat 其中之一
                if (string.IsNullOrEmpty(metadata.requestedDataFormat) && string.IsNullOrEmpty(metadata.encodedDataFormat))
                {
                    Debug.LogWarning($"[GeoTiffToDecal] 不是 GeoTiffToExr 生成的 JSON（缺少数据格式字段）：{Path.GetFileName(jsonFilePath)}");
                    return false;
                }

                // 检查是否有波段数据或通道包装数据
                if (metadata.band == null && metadata.channelPacking == null && (metadata.bands == null || metadata.bands.Length == 0))
                {
                    Debug.LogWarning($"[GeoTiffToDecal] 缺少波段数据：{Path.GetFileName(jsonFilePath)}");
                    return false;
                }

                // EXR 文件存在性检查（不是必须的，因为我们会用 AssetDatabase 搜索）
                string jsonDirectory = Path.GetDirectoryName(jsonFilePath);
                if (!string.IsNullOrEmpty(jsonDirectory))
                {
                    string exrPath = Path.Combine(jsonDirectory, metadata.exrFile);
                    if (File.Exists(exrPath))
                    {
                        Debug.Log($"[GeoTiffToDecal] 在 JSON 同目录找到 EXR：{exrPath}");
                        return true;
                    }
                }

                // 即使本地没找到 EXR，也认为是有效的，让后续的搜索逻辑处理
                Debug.Log($"[GeoTiffToDecal] JSON 有效，但本地未找到 EXR，将通过搜索查找：{metadata.exrFile}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GeoTiffToDecal] 验证 JSON 时发生错误 {Path.GetFileName(jsonFilePath)}：{ex.Message}");
                return false;
            }
        }

#if UNITY_EDITOR
        private void CreateDecalGameObject(GeoTiffToExrMetadata metadata, GeoTiffToDecalOptions options)
        {
            try
            {
                // 使用JSON文件名作为GameObject和预制体名称，包含波段信息
                string gameObjectName = !string.IsNullOrEmpty(metadata.jsonFilePath)
                    ? Path.GetFileNameWithoutExtension(metadata.jsonFilePath)
                    : Path.GetFileNameWithoutExtension(metadata.exrFile);

                var gameObject = new GameObject(gameObjectName);

                if (options.ParentTransform != null)
                {
                    gameObject.transform.SetParent(options.ParentTransform);
                }

                var decalProjector = gameObject.AddComponent<DecalProjector>();

                Material clonedMaterial = CreateOrCloneMaterial(metadata, options);
                decalProjector.material = clonedMaterial;

                SetupDecalProjector(decalProjector, metadata, options);

                PositionDecalUsingCoordinates(gameObject, metadata, options);

                if (!string.IsNullOrEmpty(options.OutputPrefabDirectory))
                {
                    CreatePrefab(gameObject, options.OutputPrefabDirectory, gameObjectName);
                }

                Debug.Log($"[GeoTiffToDecal] 成功创建贴花：{gameObjectName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GeoTiffToDecal] 创建贴花失败：{ex.Message}\n{ex.StackTrace}");
            }
        }

        private Material CreateOrCloneMaterial(GeoTiffToExrMetadata metadata, GeoTiffToDecalOptions options)
        {
            // 查找 EXR 纹理
            Texture2D exrTexture = FindExrTexture(metadata.exrFile);

            if (exrTexture != null && metadata.band != null)
            {
                // 使用热力图材质创建工具
                float minValue = (float)metadata.band.dataMinimum;
                float maxValue = (float)metadata.band.dataMaximum;

#if UNITY_EDITOR
                // 创建热力图贴花材质
                Material heatmapMaterial = HeatmapDecalMaterial.CreateHeatmapMaterial(
                    exrTexture, minValue, maxValue);

                if (heatmapMaterial != null)
                {
                    // 保存材质到文件
                    string materialPath = SaveMaterialToFile(heatmapMaterial, metadata, options.OutputPrefabDirectory);
                    if (!string.IsNullOrEmpty(materialPath))
                    {
                        // 重新加载保存的材质
                        heatmapMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    }

                    Debug.Log($"[GeoTiffToDecal] 已创建热力图材质，数据范围: [{minValue:F2}, {maxValue:F2}]");
                    return heatmapMaterial;
                }
#endif
            }

            // 回退到原有逻辑
            Material baseMaterial = options.DefaultDecalMaterial;

            if (baseMaterial == null)
            {
                var defaultDecalShader = Shader.Find("HDRP/Decal");
                if (defaultDecalShader != null)
                {
                    baseMaterial = new Material(defaultDecalShader);
                }
                else
                {
                    Debug.LogWarning("[GeoTiffToDecal] 找不到默认的 HDRP Decal 着色器，使用标准材质");
                    baseMaterial = new Material(Shader.Find("Standard"));
                }
            }

            Material clonedMaterial = UnityEngine.Object.Instantiate(baseMaterial);
            clonedMaterial.name = $"{Path.GetFileNameWithoutExtension(metadata.sourceFile)}_DecalMaterial";

            if (exrTexture != null)
            {
                clonedMaterial.SetTexture("_BaseColorMap", exrTexture);
                clonedMaterial.SetTexture("_MainTex", exrTexture);

                if (metadata.band != null)
                {
                    float minValue = (float)metadata.band.dataMinimum;
                    float maxValue = (float)metadata.band.dataMaximum;

                    if (clonedMaterial.HasProperty("_MinValue"))
                        clonedMaterial.SetFloat("_MinValue", minValue);
                    if (clonedMaterial.HasProperty("_MaxValue"))
                        clonedMaterial.SetFloat("_MaxValue", maxValue);
                }

                Debug.Log($"[GeoTiffToDecal] 已为材质设置纹理：{AssetDatabase.GetAssetPath(exrTexture)}");
            }
            else
            {
                Debug.LogWarning($"[GeoTiffToDecal] 无法找到 EXR 纹理：{metadata.exrFile}");
            }

#if UNITY_EDITOR
            // 也保存回退材质到文件
            string fallbackMaterialPath = SaveMaterialToFile(clonedMaterial, metadata, options.OutputPrefabDirectory);
            if (!string.IsNullOrEmpty(fallbackMaterialPath))
            {
                // 重新加载保存的材质
                clonedMaterial = AssetDatabase.LoadAssetAtPath<Material>(fallbackMaterialPath);
            }
#endif

            return clonedMaterial;
        }

        private Texture2D FindExrTexture(string exrFileName)
        {
            if (string.IsNullOrEmpty(exrFileName))
                return null;

            try
            {
                // 方法1：直接搜索整个项目
                string[] guids = AssetDatabase.FindAssets($"{Path.GetFileNameWithoutExtension(exrFileName)} t:Texture2D");
                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileName(assetPath).Equals(exrFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                        if (texture != null)
                        {
                            Debug.Log($"[GeoTiffToDecal] 找到纹理：{assetPath}");
                            return texture;
                        }
                    }
                }

                // 方法2：尝试常见路径
                string[] commonPaths = {
                    "Assets",
                    "Assets/Textures",
                    "Assets/EXR",
                    "Assets/Output",
                    "Assets/ImportRoot"
                };

                foreach (string basePath in commonPaths)
                {
                    string testPath = Path.Combine(basePath, exrFileName).Replace("\\", "/");
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(testPath);
                    if (texture != null)
                    {
                        Debug.Log($"[GeoTiffToDecal] 在常见路径找到纹理：{testPath}");
                        return texture;
                    }
                }

                Debug.LogWarning($"[GeoTiffToDecal] 在所有路径中都未找到纹理：{exrFileName}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GeoTiffToDecal] 查找纹理时发生错误：{ex.Message}");
                return null;
            }
        }

        private void SetupDecalProjector(DecalProjector decalProjector, GeoTiffToExrMetadata metadata, GeoTiffToDecalOptions options)
        {
            // 设置投影尺寸，使用配置的投影深度
            decalProjector.size = new Vector3(metadata.width, metadata.height, options.ProjectionDepth);

            decalProjector.fadeFactor = 1f;
            decalProjector.uvBias = Vector4.zero;
            decalProjector.uvScale = Vector4.one;

            decalProjector.drawDistance = 10000f; // 增加绘制距离
            decalProjector.fadeScale = 1f;

            decalProjector.scaleMode = DecalScaleMode.InheritFromHierarchy;
        }

        private void PositionDecalUsingCoordinates(GameObject gameObject, GeoTiffToExrMetadata metadata, GeoTiffToDecalOptions options)
        {
            if (metadata.geoBounds != null)
            {
                try
                {
                    // 确保坐标转换工具已初始化
                    EnsureCoordinateUtilsInitialized(options.Config);

                    // 获取边界经纬度
                    double minLat = metadata.geoBounds.minLatitude;
                    double maxLat = metadata.geoBounds.maxLatitude;
                    double minLon = metadata.geoBounds.minLongitude;
                    double maxLon = metadata.geoBounds.maxLongitude;

                    // 将边界点转换为世界坐标
                    Vector3 minWorldPos = GeoCoordinateUtils.LatLonToWorld(new double2(minLon, minLat));
                    Vector3 maxWorldPos = GeoCoordinateUtils.LatLonToWorld(new double2(maxLon, maxLat));

                    // 计算世界坐标的中心点
                    Vector3 worldCenter = new Vector3(
                        (minWorldPos.x + maxWorldPos.x) * 0.5f,
                        options.DecalHeight,
                        (minWorldPos.z + maxWorldPos.z) * 0.5f
                    );

                    // 计算世界坐标的尺寸
                    Vector3 worldSize = new Vector3(
                        Mathf.Abs(maxWorldPos.x - minWorldPos.x),
                        options.DecalHeight,
                        Mathf.Abs(maxWorldPos.z - minWorldPos.z)
                    );

                    // 设置位置
                    gameObject.transform.position = worldCenter;

                    // 设置旋转
                    gameObject.transform.rotation = Quaternion.Euler(options.DecalRotation);

                    // 设置缩放 - 根据世界坐标尺寸和原始纹理尺寸计算缩放比例
                    Vector3 scale = new Vector3(
                        worldSize.x / metadata.width,
                        worldSize.z / metadata.height,
                        1f
                    );
                    gameObject.transform.localScale = scale;

                    Debug.Log($"[GeoTiffToDecal] 已设置位置：{worldCenter} 缩放：{scale} 旋转：{options.DecalRotation}");
                    Debug.Log($"[GeoTiffToDecal] 地理边界：({minLat:F6}, {minLon:F6}) 到 ({maxLat:F6}, {maxLon:F6})");
                    Debug.Log($"[GeoTiffToDecal] 世界边界：{minWorldPos} 到 {maxWorldPos}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GeoTiffToDecal] 坐标转换失败，使用默认位置：{ex.Message}");
                    gameObject.transform.position = new Vector3(0, options.DecalHeight, 0);
                    gameObject.transform.rotation = Quaternion.Euler(options.DecalRotation);
                    gameObject.transform.localScale = Vector3.one;
                }
            }
            else
            {
                Debug.LogWarning("[GeoTiffToDecal] JSON 中缺少地理边界信息，使用默认位置");
                gameObject.transform.position = new Vector3(0, options.DecalHeight, 0);
                gameObject.transform.rotation = Quaternion.Euler(options.DecalRotation);
                gameObject.transform.localScale = Vector3.one;
            }
        }

        private string SaveMaterialToFile(Material material, GeoTiffToExrMetadata metadata, string outputDirectory)
        {
            try
            {
                if (string.IsNullOrEmpty(outputDirectory))
                {
                    Debug.LogWarning("[GeoTiffToDecal] 输出目录为空，无法保存材质");
                    return null;
                }

                // 创建Materials子目录
                string materialsDirectory = Path.Combine(outputDirectory, "Materials");
                Directory.CreateDirectory(materialsDirectory);

                // 转换为Assets相对路径
                string assetPath = materialsDirectory;
                if (assetPath.StartsWith(Application.dataPath))
                {
                    assetPath = "Assets" + assetPath.Substring(Application.dataPath.Length);
                }
                assetPath = assetPath.Replace("\\", "/");

                // 生成材质文件名
                string materialName = $"{Path.GetFileNameWithoutExtension(metadata.sourceFile)}_HeatmapDecal";
                string materialPath = Path.Combine(assetPath, $"{materialName}.mat").Replace("\\", "/");

                // 检查文件是否已存在，如果存在则添加序号
                int counter = 1;
                string originalMaterialPath = materialPath;
                while (File.Exists(materialPath))
                {
                    materialPath = Path.Combine(assetPath, $"{materialName}_{counter}.mat").Replace("\\", "/");
                    counter++;
                }

                // 保存材质到文件
                AssetDatabase.CreateAsset(material, materialPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"[GeoTiffToDecal] 已保存材质文件：{materialPath}");
                return materialPath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GeoTiffToDecal] 保存材质文件失败：{ex.Message}");
                return null;
            }
        }

        private void CreatePrefab(GameObject gameObject, string outputDirectory, string prefabName)
        {
            try
            {
                Directory.CreateDirectory(outputDirectory);

                string assetPath = outputDirectory;
                if (assetPath.StartsWith(Application.dataPath))
                {
                    assetPath = "Assets" + assetPath.Substring(Application.dataPath.Length);
                }
                assetPath = assetPath.Replace("\\", "/");

                string prefabPath = Path.Combine(assetPath, $"{prefabName}.prefab").Replace("\\", "/");

                PrefabUtility.SaveAsPrefabAssetAndConnect(gameObject, prefabPath, InteractionMode.AutomatedAction);

                Debug.Log($"[GeoTiffToDecal] 已创建预制体：{prefabPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GeoTiffToDecal] 创建预制体失败：{ex.Message}");
            }
        }
#endif
    }
}