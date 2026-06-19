#if UNITY_EDITOR
using GeoToolkit.GeoTiffTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace GeoToolkit.EditorTools.GeoTiff
{
    public sealed class GeoTiffToDecalWindow : EditorWindow
    {
        private string _jsonFolderPath = string.Empty;
        private string _outputPrefabDirectory = string.Empty;
        private Transform _parentTransform;
        private Material _defaultDecalMaterial;
        private float _decalHeight = 500f;
        private float _projectionDepth = 1000f;
        private Vector3 _decalRotation = new Vector3(90f, 0f, 0f);

        private Vector2 _scrollPosition;
        private Task<List<GeoTiffToExrMetadata>>? _runningTask;
        private CancellationTokenSource? _cancellationTokenSource;
        private string? _statusMessage;
        private List<GeoTiffToExrMetadata>? _foundJsonFiles;

        private readonly GeoTiffToDecalProcessor _processor = new GeoTiffToDecalProcessor();

        [MenuItem("GeoToolkit/数据工具/JSON 转贴花",false, priority = 200)]
        private static void Open()
        {
            GetWindow<GeoTiffToDecalWindow>("JSON 转贴花");
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.Space();
            DrawSourceSection();
            EditorGUILayout.Space();
            DrawOptionsSection();
            EditorGUILayout.Space(10);
            DrawActionSection();
            EditorGUILayout.Space(10);
            DrawStatusSection();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSourceSection()
        {
            EditorGUILayout.LabelField("源设置", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.TextField("JSON 文件夹", _jsonFolderPath);
                if (GUILayout.Button("选择", GUILayout.Width(80)))
                {
                    string selected = EditorUtility.OpenFolderPanel("选择包含 JSON 文件的文件夹", string.IsNullOrEmpty(_jsonFolderPath) ? Application.dataPath : _jsonFolderPath, string.Empty);
                    if (!string.IsNullOrEmpty(selected))
                    {
                        _jsonFolderPath = selected;
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.TextField("预制体输出目录", _outputPrefabDirectory);
                if (GUILayout.Button("浏览", GUILayout.Width(80)))
                {
                    string selected = EditorUtility.OpenFolderPanel("选择预制体输出目录", string.IsNullOrEmpty(_outputPrefabDirectory) ? Application.dataPath : _outputPrefabDirectory, string.Empty);
                    if (!string.IsNullOrEmpty(selected))
                    {
                        _outputPrefabDirectory = selected;
                    }
                }
            }
        }

        private void DrawOptionsSection()
        {
            EditorGUILayout.LabelField("贴花选项", EditorStyles.boldLabel);

            _parentTransform = (Transform)EditorGUILayout.ObjectField("父物体", _parentTransform, typeof(Transform), true);
            _defaultDecalMaterial = (Material)EditorGUILayout.ObjectField("默认贴花材质", _defaultDecalMaterial, typeof(Material), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("位置和旋转", EditorStyles.boldLabel);

            _decalHeight = EditorGUILayout.FloatField("贴花高度", _decalHeight);
            _projectionDepth = EditorGUILayout.FloatField("投影深度", _projectionDepth);
            _decalRotation = EditorGUILayout.Vector3Field("贴花旋转", _decalRotation);

            EditorGUILayout.HelpBox("贴花高度：距离地面的高度，确保贴花能够投射到地面", MessageType.Info);
            EditorGUILayout.HelpBox("投影深度：贴花投射的深度，需要足够深以覆盖地形", MessageType.Info);
            EditorGUILayout.HelpBox("贴花旋转：默认(90,0,0)向下投射，可根据需要调整", MessageType.Info);

            EditorGUILayout.Space();

            // 显示配置状态
            var config = GeoToolkitEditor.LoadConfig();
            if (config != null)
            {
                EditorGUILayout.HelpBox($"✓ GeoToolkit 配置已加载", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("⚠ 未找到 GeoToolkit 配置，坐标转换可能不准确", MessageType.Warning);
            }

            EditorGUILayout.HelpBox("如果未指定父物体，GameObject 将放置在场景根目录", MessageType.Info);
            EditorGUILayout.HelpBox("如果未指定默认材质，将使用系统默认的 HDRP Decal 材质", MessageType.Info);
        }

        private void DrawActionSection()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_runningTask != null))
                {
                    if (GUILayout.Button("扫描 JSON 文件", GUILayout.Height(32)))
                    {
                        StartScanJsonFiles();
                    }
                }

                using (new EditorGUI.DisabledScope(_runningTask != null || _foundJsonFiles == null || _foundJsonFiles.Count == 0))
                {
                    if (GUILayout.Button("生成贴花", GUILayout.Height(32)))
                    {
                        StartGenerateDecals();
                    }
                }

                using (new EditorGUI.DisabledScope(_runningTask == null))
                {
                    if (GUILayout.Button("取消", GUILayout.Width(80), GUILayout.Height(32)))
                    {
                        _cancellationTokenSource?.Cancel();
                    }
                }
            }
        }

        private void DrawStatusSection()
        {
            if (_runningTask != null)
            {
                EditorGUILayout.HelpBox("正在处理，请稍候...", MessageType.Info);
                Repaint();
            }

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
            }

            if (_foundJsonFiles != null && _foundJsonFiles.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"找到 {_foundJsonFiles.Count} 个有效的 JSON 文件", EditorStyles.boldLabel);

                if (_foundJsonFiles.Count <= 10)
                {
                    foreach (var json in _foundJsonFiles)
                    {
                        EditorGUILayout.LabelField($"• {json.sourceFile} → {json.exrFile}");
                    }
                }
                else
                {
                    for (int i = 0; i < 5; i++)
                    {
                        EditorGUILayout.LabelField($"• {_foundJsonFiles[i].sourceFile} → {_foundJsonFiles[i].exrFile}");
                    }
                    EditorGUILayout.LabelField($"... 还有 {_foundJsonFiles.Count - 10} 个文件");
                    for (int i = _foundJsonFiles.Count - 5; i < _foundJsonFiles.Count; i++)
                    {
                        EditorGUILayout.LabelField($"• {_foundJsonFiles[i].sourceFile} → {_foundJsonFiles[i].exrFile}");
                    }
                }
            }
        }

        private void StartScanJsonFiles()
        {
            if (string.IsNullOrEmpty(_jsonFolderPath))
            {
                EditorUtility.DisplayDialog("提示", "请先选择 JSON 文件夹。", "好的");
                return;
            }

            if (!Directory.Exists(_jsonFolderPath))
            {
                EditorUtility.DisplayDialog("错误", "指定的文件夹不存在。", "好的");
                return;
            }

            _statusMessage = "正在扫描 JSON 文件...";
            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = _cancellationTokenSource.Token;

            _runningTask = _processor.ScanJsonFilesAsync(_jsonFolderPath, token);
            _ = MonitorScanTaskAsync(_runningTask, token);
        }

        private void StartGenerateDecals()
        {
            if (_foundJsonFiles == null || _foundJsonFiles.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有找到有效的 JSON 文件。", "好的");
                return;
            }

            if (string.IsNullOrEmpty(_outputPrefabDirectory))
            {
                EditorUtility.DisplayDialog("提示", "请指定预制体输出目录。", "好的");
                return;
            }

            // 加载配置
            var config = GeoToolkitEditor.LoadConfig();
            if (config == null)
            {
                EditorUtility.DisplayDialog("错误", "无法加载 GeoToolkit 配置，坐标转换可能不准确。请检查配置文件。", "好的");
            }

            var options = new GeoTiffToDecalOptions
            {
                ParentTransform = _parentTransform,
                DefaultDecalMaterial = _defaultDecalMaterial,
                OutputPrefabDirectory = _outputPrefabDirectory,
                DecalHeight = _decalHeight,
                ProjectionDepth = _projectionDepth,
                DecalRotation = _decalRotation,
                Config = config
            };

            _statusMessage = "正在生成贴花...";
            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = _cancellationTokenSource.Token;

            _runningTask = _processor.GenerateDecalsAsync(_foundJsonFiles, options, token);
            _ = MonitorGenerateTaskAsync(_runningTask, token);
        }

        private async Task MonitorScanTaskAsync(Task<List<GeoTiffToExrMetadata>> task, CancellationToken token)
        {
            try
            {
                while (!task.IsCompleted)
                {
                    await Task.Delay(100, token).ConfigureAwait(false);
                }

                List<GeoTiffToExrMetadata> result = await task.ConfigureAwait(false);
                EditorApplication.delayCall += () => HandleScanCompletion(result);
            }
            catch (OperationCanceledException)
            {
                _statusMessage = "扫描已取消";
                Debug.LogWarning("[GeoTiffToDecal] 用户取消了 JSON 扫描。");
            }
            catch (Exception ex)
            {
                _statusMessage = $"扫描失败：{ex.Message}";
                Debug.LogError($"[GeoTiffToDecal] 扫描任务失败：{ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                _runningTask = null;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private async Task MonitorGenerateTaskAsync(Task<List<GeoTiffToExrMetadata>> task, CancellationToken token)
        {
            try
            {
                while (!task.IsCompleted)
                {
                    await Task.Delay(100, token).ConfigureAwait(false);
                }

                await task.ConfigureAwait(false);
                EditorApplication.delayCall += () => HandleGenerateCompletion();
            }
            catch (OperationCanceledException)
            {
                _statusMessage = "生成已取消";
                Debug.LogWarning("[GeoTiffToDecal] 用户取消了贴花生成。");
            }
            catch (Exception ex)
            {
                _statusMessage = $"生成失败：{ex.Message}";
                Debug.LogError($"[GeoTiffToDecal] 生成任务失败：{ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                _runningTask = null;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void HandleScanCompletion(List<GeoTiffToExrMetadata> result)
        {
            _foundJsonFiles = result;
            _statusMessage = $"扫描完成，找到 {result.Count} 个有效的 JSON 文件";
            Debug.Log($"[GeoTiffToDecal] 扫描完成，找到 {result.Count} 个有效的 JSON 文件");
        }

        private void HandleGenerateCompletion()
        {
            _statusMessage = $"贴花生成完成，输出到：{_outputPrefabDirectory}";
            Debug.Log($"[GeoTiffToDecal] 贴花生成完成，输出目录：{_outputPrefabDirectory}");
        }
    }
}
#endif