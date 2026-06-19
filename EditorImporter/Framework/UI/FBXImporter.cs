#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace GeoToolkit
{
    public class FBXBatchImporter : EditorWindow
    {
        // UI状态变量
        private string sourcePath = string.Empty;
        private string codeSavePath = string.Empty;
        private string savePath = string.Empty;
        private SelectType selectType = SelectType.Folder;

        private int batchSize = 5;
        private float delayBetweenBatches = 0.5f;
        private int maxThreads = 1; // 默认单线程
        private bool skipExisting = true;
        private bool deleteAfterImport = false;


        // 操作状态
        private int processedCount;
        private int failedCount;
        private int totalFileCount;
        private List<string> logMessages = new List<string>();
        private bool isImporting;
        private Vector2 scrollPos;
        private List<string> fbxFiles = new List<string>();
        private List<string> importingQueue = new List<string>();

        // 显存监控
        private float currentGpuMemoryMB = 0;
        private float estimatedTotalGpuMemory = 2048;
        private bool gpuMonitorRunning = false;
        private float gpuSafeThreshold = 0.75f;
        private float lastGpuUpdateTime;
        private Stopwatch frameTimer = new Stopwatch();
        private Stopwatch totalImportTimer = new Stopwatch();


        // 场景设置
        private bool isLoadToSence = true;
        private bool isCombineModel = true;
        //private bool isCreateLOD = false;
        private GeoPlatformConfig config;

        /// <summary>
        /// 导入完成后触发
        /// </summary>
        private Action<string, string, string, SelectType, GeoPlatformConfig, bool, bool> onImporterConfirmed;

        public static void ShowWindow(string thisSourceFolder, string thisSaveFolder, string thisTargetFolder, SelectType thisSelectType, GeoPlatformConfig thisConfig, Action<string, string, string, SelectType, GeoPlatformConfig, bool, bool> importConfirmed)
        {
            var window = GetWindow<FBXBatchImporter>("性能优化FBX导入");
            window.codeSavePath = thisTargetFolder;
            window.savePath = thisSaveFolder;
            window.sourcePath = thisSourceFolder;
            window.selectType = thisSelectType;
            window.config = thisConfig;
            window.minSize = new Vector2(600, 550);
            window.StartGpuMemoryMonitor();
            window.onImporterConfirmed = importConfirmed;
        }



        void OnEnable()
        {
            StartGpuMemoryMonitor();
        }

        void OnDisable()
        {
            StopGpuMemoryMonitor();
        }

        void OnGUI()
        {
            frameTimer.Restart();

            GUILayout.Label("高性能FBX批量导入", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // === 路径设置 ===
            //EditorGUILayout.LabelField("路径设置", EditorStyles.boldLabel);
            //EditorGUILayout.BeginVertical(GUI.skin.box);
            //{
            //    EditorGUILayout.BeginHorizontal();
            //    GUILayout.Label("源文件夹路径", GUILayout.Width(120));
            //    sourcePath = EditorGUILayout.TextField(sourcePath);
            //    if (GUILayout.Button("浏览...", GUILayout.Width(80)))
            //    {
            //        string newPath = EditorUtility.OpenFolderPanel("选择FBX源文件夹", "", "");
            //        if (!string.IsNullOrEmpty(newPath))
            //        {
            //            sourcePath = newPath;
            //        }
            //    }
            //    EditorGUILayout.EndHorizontal();

            //    EditorGUILayout.BeginHorizontal();
            //    GUILayout.Label("项目目标目录", GUILayout.Width(120));
            //    codeSavePath = EditorGUILayout.TextField(codeSavePath);
            //    if (GUILayout.Button("浏览...", GUILayout.Width(80)))
            //    {
            //        string newPath = EditorUtility.OpenFolderPanel("选择目标目录", "Assets", "");
            //        if (!string.IsNullOrEmpty(newPath) && newPath.StartsWith(Application.dataPath))
            //        {
            //            codeSavePath = "Assets" + newPath.Substring(Application.dataPath.Length);
            //        }
            //    }
            //    EditorGUILayout.EndHorizontal();
            //}
            //EditorGUILayout.EndVertical();
            //EditorGUILayout.Space();

            // === 性能设置 ===
            EditorGUILayout.LabelField("性能优化设置", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            {
                batchSize = EditorGUILayout.IntSlider("每批导入数量", batchSize, 1, 10);
                delayBetweenBatches = EditorGUILayout.Slider("批次间隔(秒)", delayBetweenBatches, 0.1f, 5f);
                //maxThreads = EditorGUILayout.IntSlider("最大线程数", maxThreads, 1, SystemInfo.processorCount);
                //EditorGUILayout.HelpBox($"当前处理器核心数: {SystemInfo.processorCount}", MessageType.Info);
                //EditorGUILayout.HelpBox("较小的批次和较长的间隔有助于提高稳定性", MessageType.Info);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // === 场景设置 ===
            EditorGUILayout.LabelField("场景选项", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            {
                EditorGUILayout.Space();
                isLoadToSence = EditorGUILayout.Toggle("是否直接创建", isLoadToSence);
                EditorGUI.BeginDisabledGroup(!isLoadToSence);
                isCombineModel = EditorGUILayout.Toggle("是否合并模型", isCombineModel);
                //isCreateLOD = EditorGUILayout.Toggle("是否添加LOD", isCreateLOD);
                EditorGUI.EndDisabledGroup();


            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // === 优化选项 ===
            EditorGUILayout.LabelField("优化选项", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            {


                EditorGUILayout.Space();
                skipExisting = EditorGUILayout.Toggle("跳过已有文件", skipExisting);
                deleteAfterImport = EditorGUILayout.Toggle("导入后删除源文件", deleteAfterImport);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // === 控制按钮 ===
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUI.BeginDisabledGroup(isImporting || string.IsNullOrEmpty(sourcePath));
                if (GUILayout.Button("扫描FBX文件", GUILayout.Height(30)))
                {
                    EditorApplication.delayCall += ScanFBXFilesAsync;
                }
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(isImporting || fbxFiles.Count == 0);
                if (GUILayout.Button("开始批量导入", GUILayout.Height(30)))
                {
                    StartImport();
                }
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(!isImporting);
                if (GUILayout.Button("暂停导入", GUILayout.Height(30)))
                {
                    isImporting = false;
                    AddLog($"[{GetFormattedTime()}] 导入已暂停");
                }
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(!isImporting);
                if (GUILayout.Button("停止导入", GUILayout.Height(30)))
                {
                    isImporting = false;
                    importingQueue.Clear();
                    AddLog($"[{GetFormattedTime()}] 导入已停止");
                    ForceMiniGpuCleanup();
                }
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            // === 状态显示 ===
            EditorGUILayout.LabelField("导入状态", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            {
                GUILayout.Label($"扫描到的FBX文件: {fbxFiles.Count}个");

                if (isImporting)
                {
                    var progress = totalFileCount > 0 ? (float)processedCount / totalFileCount : 0f;
                    string elapsedTime = totalImportTimer.Elapsed.ToString(@"hh\:mm\:ss");

                    GUILayout.Label($"进度: {processedCount}/{totalFileCount} ({progress * 100:F1}%)");
                    GUILayout.Label($"运行时间: {elapsedTime}");
                    GUILayout.Label($"预计剩余时间: {EstimateRemainingTime(progress, totalImportTimer.Elapsed)}");

                    Rect rect = GUILayoutUtility.GetRect(100, 20);
                    EditorGUI.ProgressBar(rect, progress, "导入中...");
                }
                else
                {
                    GUILayout.Label($"状态: {(fbxFiles.Count > 0 ? "就绪" : "等待扫描")}");
                    GUILayout.Label($"上次导入: {processedCount} 成功, {failedCount} 失败");
                    GUILayout.Label($"总运行时间: {totalImportTimer.Elapsed.ToString(@"hh\:mm\:ss")}");
                }

                // 显存状态
                EditorGUILayout.Space();
                GUILayout.Label("显存状态", EditorStyles.boldLabel);

                if (gpuMonitorRunning)
                {
                    float gpuUsage = Mathf.Clamp01(currentGpuMemoryMB / estimatedTotalGpuMemory);
                    Rect gpuRect = GUILayoutUtility.GetRect(100, 20);
                    EditorGUI.ProgressBar(gpuRect, gpuUsage, $"显存使用率: {gpuUsage * 100:F1}%");
                    GUILayout.Label($"已用显存: {currentGpuMemoryMB:F1} MB / {estimatedTotalGpuMemory:F1} MB");
                    GUILayout.Label($"安全阈值: {gpuSafeThreshold * 100}% - {(gpuUsage > gpuSafeThreshold ? "警告" : "正常")}");
                }
                else
                {
                    GUILayout.Label("显存监控未启动");
                }
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // === 日志区域 ===
            EditorGUILayout.LabelField("导入日志", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(180));
            EditorGUILayout.BeginVertical(GUI.skin.box);
            {
                if (logMessages.Count == 0)
                {
                    EditorGUILayout.LabelField("日志为空", EditorStyles.miniLabel);
                }
                else
                {
                    foreach (var msg in logMessages)
                    {
                        EditorGUILayout.SelectableLabel(msg, EditorStyles.wordWrappedMiniLabel, GUILayout.Height(16));
                    }
                }
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();

            // UI性能监控
            frameTimer.Stop();
            long frameTime = frameTimer.ElapsedMilliseconds;
            if (frameTime > 30)
            {
                AddLog($"[{GetFormattedTime()}] UI绘制时间: {frameTime}ms (目标<30ms)");
            }

            if (isImporting)
            {
                Repaint();
            }
        }

        // 估计剩余时间
        private string EstimateRemainingTime(float progress, TimeSpan elapsed)
        {
            if (progress <= 0.01f || !isImporting) return "--:--:--";

            double totalExpectedSeconds = elapsed.TotalSeconds / progress;
            double remainingSeconds = totalExpectedSeconds - elapsed.TotalSeconds;

            if (double.IsInfinity(remainingSeconds) || double.IsNaN(remainingSeconds) || remainingSeconds > 86400)
                return "--:--:--";

            return TimeSpan.FromSeconds(remainingSeconds).ToString(@"hh\:mm\:ss");
        }

        void AddLog(string message)
        {
            if (logMessages.Count > 100)
            {
                logMessages.RemoveRange(0, 20);
            }

            logMessages.Add(message);
            scrollPos.y = Mathf.Infinity;
        }

        string GetFormattedTime()
        {
            return DateTime.Now.ToString("HH:mm:ss");
        }

        void StartGpuMemoryMonitor()
        {
            if (!gpuMonitorRunning)
            {
                gpuMonitorRunning = true;
                estimatedTotalGpuMemory = EstimateTotalGpuMemory();
                EditorApplication.update += UpdateGpuMemory;
            }
        }

        void StopGpuMemoryMonitor()
        {
            gpuMonitorRunning = false;
            EditorApplication.update -= UpdateGpuMemory;
        }

        void UpdateGpuMemory()
        {
            if (Time.realtimeSinceStartup - lastGpuUpdateTime > 0.5f)
            {
                lastGpuUpdateTime = Time.realtimeSinceStartup;
                currentGpuMemoryMB = GetCurrentGpuMemoryUsage() / 1024f;

                if (currentGpuMemoryMB / estimatedTotalGpuMemory > gpuSafeThreshold)
                {
                    AddLog($"[{GetFormattedTime()}] 显存警告: {currentGpuMemoryMB:F1}/{estimatedTotalGpuMemory:F1} MB");
                }
            }
        }

        void ScanFBXFilesAsync()
        {
            Thread scanThread = new Thread(() =>
            {
                try
                {
                    AddLog($"[{GetFormattedTime()}] 开始扫描: {sourcePath}");
                    Stopwatch scanTimer = Stopwatch.StartNew();

                    string actualScanPath = sourcePath;

                    // 如果是ZIP文件，先解压
                    if (selectType == SelectType.File)
                    {
                        if (!File.Exists(sourcePath))
                        {
                            EditorApplication.delayCall += () => AddLog($"[{GetFormattedTime()}] 错误：文件不存在 - {sourcePath}");
                            return;
                        }

                        if (!Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            EditorApplication.delayCall += () => AddLog($"[{GetFormattedTime()}] 错误：选择的不是ZIP文件 - {sourcePath}");
                            return;
                        }

                        try
                        {
                            string zipDirectory = Path.Combine(
                                Path.GetDirectoryName(sourcePath),
                                Path.GetFileNameWithoutExtension(sourcePath));

                            AddLog($"[{GetFormattedTime()}] 开始解压ZIP文件到: {zipDirectory}");

                            // 确保目标目录存在，如果存在则清空
                            if (Directory.Exists(zipDirectory))
                            {
                                Directory.Delete(zipDirectory, true);
                                AddLog($"[{GetFormattedTime()}] 已清空现有解压目录");
                            }

                            ZipFile.ExtractToDirectory(sourcePath, zipDirectory);
                            actualScanPath = zipDirectory;

                            AddLog($"[{GetFormattedTime()}] ZIP文件解压完成");
                        }
                        catch (Exception zipEx)
                        {
                            EditorApplication.delayCall += () => AddLog($"[{GetFormattedTime()}] ZIP文件解压错误: {zipEx.Message}");
                            return;
                        }
                    }

                    // 统一扫描文件夹
                    if (!Directory.Exists(actualScanPath))
                    {
                        EditorApplication.delayCall += () => AddLog($"[{GetFormattedTime()}] 错误：扫描路径不存在 - {actualScanPath}");
                        return;
                    }

                    //var files = Directory.GetFiles(actualScanPath, "*.fbx", SearchOption.AllDirectories)
                    //    .Concat(Directory.GetFiles(actualScanPath, "*.obj", SearchOption.AllDirectories))
                    //    .ToList();
                    var files = Directory.GetFiles(actualScanPath, "*.fbx", SearchOption.AllDirectories)
                    .Where(f => !f.ToLower().Contains("lrrl") && !f.ToLower().Contains("road"))
                    .ToList();

                    scanTimer.Stop();

                    EditorApplication.delayCall += () =>
                    {
                        sourcePath = actualScanPath;
                        fbxFiles = files;
                        AddLog($"[{GetFormattedTime()}] 扫描完成: 找到 {fbxFiles.Count} 个模型文件 ({scanTimer.Elapsed.TotalSeconds:F2}秒)");
                    };
                }
                catch (Exception e)
                {
                    EditorApplication.delayCall += () => AddLog($"[{GetFormattedTime()}] 扫描错误: {e.Message}");
                }
            });

            scanThread.Priority = System.Threading.ThreadPriority.BelowNormal;
            scanThread.Start();
        }

        void StartImport()
        {
            if (fbxFiles.Count == 0)
            {
                AddLog($"[{GetFormattedTime()}] 错误：没有模型文件可导入");
                return;
            }

            processedCount = 0;
            failedCount = 0;
            totalFileCount = fbxFiles.Count;
            importingQueue = new List<string>(fbxFiles);
            logMessages.Clear();
            isImporting = true;
            totalImportTimer.Restart();

            if (!Directory.Exists(codeSavePath))
            {
                Directory.CreateDirectory(codeSavePath);
            }

            AssetDatabase.Refresh();

            EditorCoroutineRunner.StartEditorCoroutine(ImportFBXsCoroutine());
        }

        IEnumerator ImportFBXsCoroutine()
        {
            string fullTargetPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", codeSavePath));

            if (!Directory.Exists(fullTargetPath))
            {
                Directory.CreateDirectory(fullTargetPath);
                AssetDatabase.Refresh();
                AddLog($"[{GetFormattedTime()}] 已创建目标目录: {codeSavePath}");
                yield return null;
            }

            while (importingQueue.Count > 0 && isImporting)
            {
                int currentBatchSize = Mathf.Min(batchSize, importingQueue.Count);
                List<string> batchFiles = importingQueue.Take(currentBatchSize).ToList();
                importingQueue.RemoveRange(0, currentBatchSize);

                AddLog($"[{GetFormattedTime()}] 开始导入批次: {batchFiles.Count}个文件");

                List<IEnumerator> importCoroutines = new List<IEnumerator>();
                foreach (string file in batchFiles)
                {
                    importCoroutines.Add(ImportSingleFBX(file));
                }

                yield return RunParallelCoroutines(importCoroutines, maxThreads);

                if (importingQueue.Count > 0 && isImporting)
                {
                    AddLog($"--- 批次完成，等待 {delayBetweenBatches} 秒 ---");

                    float startTime = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - startTime < delayBetweenBatches && isImporting)
                    {
                        ForceMiniGpuCleanup();

                        float gpuUsage = currentGpuMemoryMB / estimatedTotalGpuMemory;
                        if (gpuUsage > gpuSafeThreshold)
                        {
                            AddLog($"[{GetFormattedTime()}] 高显存占用({gpuUsage * 100:F1}%)，延长等待时间");
                            yield return new WaitForSeconds(1f);
                            FullGpuCleanup();
                        }

                        yield return null;
                    }
                }

                Repaint();
            }

            if (isImporting)
            {
                isImporting = false;
                AssetDatabase.Refresh();
                totalImportTimer.Stop();
                AddLog($"[{GetFormattedTime()}] 导入完成! 成功: {processedCount}, 失败: {failedCount}, 总耗时: {totalImportTimer.Elapsed}");
                onImporterConfirmed?.Invoke(sourcePath, savePath, codeSavePath, selectType, config, isLoadToSence, isCombineModel);
            }

            ForceMiniGpuCleanup();
        }

        IEnumerator RunParallelCoroutines(List<IEnumerator> coroutines, int maxConcurrent)
        {
            List<IEnumerator> activeCoroutines = new List<IEnumerator>(coroutines);
            List<IEnumerator> runningCoroutines = new List<IEnumerator>();

            while (activeCoroutines.Count > 0 || runningCoroutines.Count > 0)
            {
                while (runningCoroutines.Count < maxConcurrent && activeCoroutines.Count > 0)
                {
                    IEnumerator coroutine = activeCoroutines[0];
                    activeCoroutines.RemoveAt(0);
                    runningCoroutines.Add(coroutine);
                }

                for (int i = runningCoroutines.Count - 1; i >= 0; i--)
                {
                    if (!runningCoroutines[i].MoveNext())
                    {
                        runningCoroutines.RemoveAt(i);
                    }
                }

                yield return null;
            }
        }

        IEnumerator ImportSingleFBX(string fbxPath)
        {
            string fileName = Path.GetFileName(fbxPath);
            string targetPath = Path.Combine(codeSavePath, fileName);
            string fullTargetPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", targetPath));

            if (skipExisting && File.Exists(fullTargetPath))
            {
                processedCount++;
                AddLog($"[{GetFormattedTime()}] 跳过已存在: {fileName}");
                yield break;
            }

            try
            {
                File.Copy(fbxPath, fullTargetPath, true);
                AddLog($"[{GetFormattedTime()}] 复制文件: {fileName}");
            }
            catch (System.Exception e)
            {
                failedCount++;
                AddLog($"[{GetFormattedTime()}] 复制失败 {fileName}: {e.Message}");
                yield break;
            }

            AssetDatabase.Refresh();
            AddLog($"[{GetFormattedTime()}] 开始导入: {fileName}");

            ModelImporter importer = null;
            float startTime = Time.realtimeSinceStartup;
            bool importSucceeded = false;

            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);

            // 等待导入器初始化（修复兼容性问题）
            float waitStartTime = Time.realtimeSinceStartup;
            while (isImporting)
            {
                importer = AssetImporter.GetAtPath(targetPath) as ModelImporter;
                if (importer != null) break;

                if (Time.realtimeSinceStartup - waitStartTime > 30f)
                {
                    AddLog($"[{GetFormattedTime()}] 导入超时: {fileName}");
                    break;
                }

                yield return new WaitForSeconds(0.5f);
            }

            if (importer == null)
            {
                failedCount++;
                AddLog($"[{GetFormattedTime()}] 导入失败: 无法获取导入器 - {fileName}");
                yield break;
            }

            // 使用替代方法检测导入完成
            float checkStartTime = Time.realtimeSinceStartup;
            UnityEngine.Object loadedModel = null;

            while (isImporting && Time.realtimeSinceStartup - checkStartTime < 60f)
            {
                loadedModel = AssetDatabase.LoadMainAssetAtPath(targetPath);
                if (loadedModel != null)
                {
                    importSucceeded = true;
                    break;
                }

                yield return new WaitForSeconds(0.5f);
            }

            if (!importSucceeded)
            {
                failedCount++;
                AddLog($"[{GetFormattedTime()}] 导入超时: {fileName}");
                yield break;
            }

            try
            {
                // 应用优化设置
                ApplyModelOptimizations(importer, fileName);

                processedCount++;
                AddLog($"[{GetFormattedTime()}] 成功导入: {fileName}");

                if (deleteAfterImport && File.Exists(fbxPath))
                {
                    try
                    {
                        File.Delete(fbxPath);
                        AddLog($"[{GetFormattedTime()}] 已删除源文件: {fileName}");
                    }
                    catch (System.Exception e)
                    {
                        AddLog($"[{GetFormattedTime()}] 删除失败 {fileName}: {e.Message}");
                    }
                }
            }
            catch (System.Exception e)
            {
                failedCount++;
                AddLog($"[{GetFormattedTime()}] 导入失败 {fileName}: {e.Message}");
            }
            finally
            {
                ForceMiniGpuCleanup();
            }
        }

        void ApplyModelOptimizations(ModelImporter importer, string fileName)
        {
            importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = true;
            importer.SaveAndReimport();
            AddLog($"[{GetFormattedTime()}] 优化完成: {fileName}");
        }



        void ForceMiniGpuCleanup()
        {
            try
            {
                EditorUtility.UnloadUnusedAssetsImmediate();
                GC.Collect();
            }
            catch (Exception e)
            {
                AddLog($"[{GetFormattedTime()}] 轻量清理失败: {e.Message}");
            }
        }

        void FullGpuCleanup()
        {
            try
            {
                AddLog($"[{GetFormattedTime()}] 开始深度显存清理...");
                RenderTexture rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGBHalf);
                rt.Create();
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);

                EditorUtility.UnloadUnusedAssetsImmediate();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                Thread.Sleep(1000);
                AddLog($"[{GetFormattedTime()}] 深度显存清理完成");
            }
            catch (Exception e)
            {
                AddLog($"[{GetFormattedTime()}] 显存清理失败: {e.Message}");
            }
        }

        float GetCurrentGpuMemoryUsage()
        {
            try
            {
#if UNITY_2019_1_OR_NEWER
                // 修正单位：字节 → MB
                return UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver()
                       / (1024f * 1024f);
#else
        int textureMemory = 0;
        foreach (var tex in Resources.FindObjectsOfTypeAll<Texture>())
        {
            textureMemory += EstimateTextureMemory(tex);
        }
        return textureMemory / (1024f * 1024f); // 添加单位转换
#endif
            }
            catch { return 0; }
        }

#if !UNITY_2019_1_OR_NEWER
    private int EstimateTextureMemory(Texture texture)
    {
        int bitsPerPixel = 32;
        if (texture is Texture2D tex2D)
        {
            switch (tex2D.format)
            {
                case TextureFormat.RGBA32: bitsPerPixel = 32; break;
                case TextureFormat.RGB24: bitsPerPixel = 24; break;
                case TextureFormat.RGBAHalf: bitsPerPixel = 64; break;
                case TextureFormat.DXT1: bitsPerPixel = 4; break;
                case TextureFormat.DXT5: bitsPerPixel = 8; break;
            }
        }
        
        int mipmaps = texture.mipmapCount > 1 ? 4 : 1;
        return (int)(texture.width * texture.height * bitsPerPixel / 8 * mipmaps);
    }
#endif

        float EstimateTotalGpuMemory()
        {
            try
            {
#if UNITY_EDITOR_WIN
                return EstimateGpuMemoryWin();
#else
            return SystemInfo.graphicsMemorySize;
#endif
            }
            catch
            {
                return SystemInfo.graphicsMemorySize;
            }
        }

        float EstimateGpuMemoryWin()
        {
            try
            {
                return SystemInfo.graphicsMemorySize;
            }
            catch
            {
                return SystemInfo.graphicsMemorySize;
            }
        }
    }
}



#endif