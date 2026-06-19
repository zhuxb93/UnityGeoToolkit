#if UNITY_EDITOR
using GeoToolkit.GeoTiffTools;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace GeoToolkit.EditorTools.GeoTiff
{
    public sealed class GeoTiffToExrWindow : EditorWindow
    {
        private string _tiffPath = string.Empty;
        private string _outputDirectory = string.Empty;

        private GeoTiffMetadata? _metadata;
        private bool[]? _bandSelections;
        private bool _useSourceNoDataOverride;
        private bool _splitBands = true;
        private ExrDataFormat _requestedDataFormat = ExrDataFormat.Auto;
        private float _sourceNoDataOverrideValue = 0f;
        private NoDataReplacementMode _replacementMode = NoDataReplacementMode.PreserveSourceValue;
        private float _customNoDataValue = 0f;
        private bool _enableMultithreading = true;
        private bool _flipY = false;

        private Vector2 _scrollPosition;
        private Task<GeoTiffConversionResult>? _runningTask;
        private CancellationTokenSource? _cancellationTokenSource;
        private string? _statusMessage;

        private readonly GeoTiffToExrConverter _converter = new GeoTiffToExrConverter();

        [MenuItem("GeoToolkit/数据工具/TIFF 转 EXR", false, priority = 200)]
        private static void Open()
        {
            GetWindow<GeoTiffToExrWindow>("TIFF 转 EXR");
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.Space();
            DrawSourceSection();
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(_metadata == null))
            {
                DrawMetadataSection();
                EditorGUILayout.Space();
                DrawOptionsSection();
            }

            EditorGUILayout.Space(10);
            DrawActionSection();
            EditorGUILayout.Space(10);
            DrawStatusSection();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSourceSection()
        {
            EditorGUILayout.LabelField("源文件", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.TextField("TIFF 路径", _tiffPath);
                if (GUILayout.Button("选择", GUILayout.Width(80)))
                {
                    string path = EditorUtility.OpenFilePanel("选择 GeoTIFF", string.Empty, "tif,tiff");
                    if (!string.IsNullOrEmpty(path))
                    {
                        TryLoadMetadata(path);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.TextField("输出目录", _outputDirectory);
                if (GUILayout.Button("浏览", GUILayout.Width(80)))
                {
                    string selected = EditorUtility.OpenFolderPanel("选择输出目录", string.IsNullOrEmpty(_outputDirectory) ? Application.dataPath : _outputDirectory, string.Empty);
                    if (!string.IsNullOrEmpty(selected))
                    {
                        _outputDirectory = selected;
                    }
                }
            }
        }

        private void DrawMetadataSection()
        {
            if (_metadata == null)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("图像信息", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("尺寸", $"{_metadata.Width} x {_metadata.Height}");
                EditorGUILayout.LabelField("位深", _metadata.BitsPerSample.ToString(CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("波段数", _metadata.SamplesPerPixel.ToString(CultureInfo.InvariantCulture));
                EditorGUILayout.LabelField("Sample Format", _metadata.SampleFormat.ToString());
                EditorGUILayout.LabelField("压缩方式", _metadata.Compression);

                if (_metadata.GeoBounds.HasValue)
                {
                    GeoTiffGeoBounds bounds = _metadata.GeoBounds.Value;
                    EditorGUILayout.LabelField("Min Lat/Lon", $"{bounds.MinLatitude:F6}, {bounds.MinLongitude:F6}");
                    EditorGUILayout.LabelField("Max Lat/Lon", $"{bounds.MaxLatitude:F6}, {bounds.MaxLongitude:F6}");
                    EditorGUILayout.LabelField("中心", $"{bounds.CenterLatitude:F6}, {bounds.CenterLongitude:F6}");
                }
                else
                {
                    EditorGUILayout.LabelField("地理范围", "未找到地理标定信息");
                }

                string noDataText = _metadata.NoDataValue.HasValue
                    ? _metadata.NoDataValue.Value.ToString(CultureInfo.InvariantCulture)
                    : "未定义";
                EditorGUILayout.LabelField("源 NODATA", noDataText);

                DrawBandSelection();
            }
        }

        private void DrawBandSelection()
        {
            if (_metadata == null || _bandSelections == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("波段选择", EditorStyles.boldLabel);

            for (int i = 0; i < _bandSelections.Length; i++)
            {
                _bandSelections[i] = EditorGUILayout.ToggleLeft($"波段 {i + 1}", _bandSelections[i]);
            }
        }

        private void DrawOptionsSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("处理选项", EditorStyles.boldLabel);

                _enableMultithreading = EditorGUILayout.Toggle("并行处理", _enableMultithreading);

                _useSourceNoDataOverride = EditorGUILayout.Toggle("覆盖输入 NODATA", _useSourceNoDataOverride);
                if (_useSourceNoDataOverride)
                {
                    _sourceNoDataOverrideValue = EditorGUILayout.FloatField("输入 NODATA 值", _sourceNoDataOverrideValue);
                }

                _replacementMode = (NoDataReplacementMode)EditorGUILayout.EnumPopup("输出 NODATA 策略", _replacementMode);
                if (_replacementMode == NoDataReplacementMode.UseCustomValue)
                {
                    _customNoDataValue = EditorGUILayout.FloatField("输出 NODATA 值", _customNoDataValue);
                }

                _splitBands = EditorGUILayout.Toggle("拆分波段输出", _splitBands);
                _requestedDataFormat = (ExrDataFormat)EditorGUILayout.EnumPopup("EXR 数据格式", _requestedDataFormat);
                _flipY = EditorGUILayout.Toggle("上下镜像翻转 (FlipY)", _flipY);
            }
        }

        private void DrawActionSection()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_runningTask != null))
                {
                    if (GUILayout.Button("开始转换", GUILayout.Height(32)))
                    {
                        StartConversion();
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
                EditorGUILayout.HelpBox("正在处理 TIFF，请稍候...", MessageType.Info);
                Repaint();
            }

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
            }
        }

        private void TryLoadMetadata(string path)
        {
            try
            {
                GeoTiffMetadata metadata = GeoTiffMetadata.FromFile(path);
                _metadata = metadata;
                _tiffPath = path;
                _statusMessage = $"成功载入 {_metadata.Width}x{_metadata.Height} 的 GeoTIFF";

                _bandSelections = new bool[metadata.SamplesPerPixel];
                for (int i = 0; i < _bandSelections.Length; i++)
                {
                    _bandSelections[i] = true;
                }

                _splitBands = metadata.SamplesPerPixel > 1;
                _requestedDataFormat = metadata.GetDefaultExrDataFormat();

                if (string.IsNullOrEmpty(_outputDirectory) && !string.IsNullOrEmpty(path))
                {
                    _outputDirectory = Path.GetDirectoryName(path) ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                _statusMessage = $"加载失败：{ex.Message}";
                _metadata = null;
                _bandSelections = null;
                _splitBands = true;
                _requestedDataFormat = ExrDataFormat.Auto;
            }
        }

        private void StartConversion()
        {
            if (_metadata == null)
            {
                EditorUtility.DisplayDialog("提示", "请先选择有效的 GeoTIFF 文件。", "好的");
                return;
            }

            if (string.IsNullOrEmpty(_outputDirectory))
            {
                EditorUtility.DisplayDialog("提示", "请指定输出目录。", "好的");
                return;
            }

            if (_bandSelections == null || _bandSelections.All(selected => !selected))
            {
                EditorUtility.DisplayDialog("提示", "至少需要选择一个波段。", "好的");
                return;
            }

            IReadOnlyCollection<int> selectedBands = CollectSelectedBands();

            var options = new GeoTiffToExrOptions
            {
                SelectedBands = selectedBands,
                EnableMultithreading = _enableMultithreading,
                NoDataReplacement = _replacementMode,
                CustomNoDataValue = _customNoDataValue,
                SourceNoDataOverride = _useSourceNoDataOverride ? _sourceNoDataOverrideValue : (float?)null,
                SplitBands = _splitBands,
                OutputDataFormat = ResolveEffectiveDataFormat(),
                FlipY = _flipY
            };

            _statusMessage = "开始处理 TIFF...";
            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = _cancellationTokenSource.Token;

            _runningTask = _converter.ConvertAsync(_tiffPath, options, token);
            _ = MonitorTaskAsync(_runningTask, token);
        }

        private IReadOnlyCollection<int> CollectSelectedBands()
        {
            if (_bandSelections == null)
                return Array.Empty<int>();

            var result = new List<int>();
            for (int i = 0; i < _bandSelections.Length; i++)
            {
                if (_bandSelections[i])
                    result.Add(i);
            }

            return result;
        }

        private async Task MonitorTaskAsync(Task<GeoTiffConversionResult> task, CancellationToken token)
        {
            try
            {
                while (!task.IsCompleted)
                {
                    await Task.Delay(100, token).ConfigureAwait(false);
                }

                GeoTiffConversionResult result = await task.ConfigureAwait(false);
                EditorApplication.delayCall += () => HandleCompletion(result);
            }
            catch (OperationCanceledException)
            {
                _statusMessage = "处理已取消";
                Debug.LogWarning("[GeoTiffToExr] 用户取消了 TIFF 转换。");
            }
            catch (Exception ex)
            {
                _statusMessage = $"处理失败：{ex.Message}";
                Debug.LogError($"[GeoTiffToExr] 转换任务失败：{ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                _runningTask = null;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void HandleCompletion(GeoTiffConversionResult result)
        {
            if (string.IsNullOrEmpty(_outputDirectory))
            {
                _statusMessage = "输出目录无效";
                Debug.LogError("[GeoTiffToExr] 输出目录为空或无效，无法写入结果。");
                return;
            }

            try
            {
                Directory.CreateDirectory(_outputDirectory);
            }
            catch (Exception ex)
            {
                _statusMessage = $"创建输出目录失败：{ex.Message}";
                Debug.LogError($"[GeoTiffToExr] 创建输出目录失败：{ex.Message}\n{ex.StackTrace}");
                return;
            }

            if (result.Bands.Count == 0)
            {
                _statusMessage = "没有可导出的波段，详见控制台";
                Debug.LogWarning("[GeoTiffToExr] 没有成功的波段可以导出，可能全部处理失败。");
                return;
            }

            bool wroteAny = false;

            if (!_splitBands && result.Bands.Count > 1)
            {
                if (TryWriteCombinedOutputs(result))
                {
                    wroteAny = true;
                }
                else
                {
                    Debug.LogWarning("[GeoTiffToExr] 无法合并输出，自动回退为拆分波段输出。");
                }
            }

            if (_splitBands || !wroteAny)
            {
                foreach (GeoTiffBandResult band in result.Bands)
                {
                    if (TryWriteBandOutput(result.Metadata, band))
                    {
                        wroteAny = true;
                    }
                }
            }

            if (wroteAny)
            {
                _statusMessage = $"转换完成，输出目录：{_outputDirectory}";
                Debug.Log($"[GeoTiffToExr] 转换完成，输出目录：{_outputDirectory}");
            }
            else
            {
                _statusMessage = "所有选定波段写入失败，请检查控制台";
                Debug.LogError("[GeoTiffToExr] 所有选定波段写入失败。");
            }
        }

        private bool TryWriteBandOutput(GeoTiffMetadata metadata, GeoTiffBandResult band)
        {
            ExrDataFormat requestedFormat = ResolveEffectiveDataFormat();
            string baseName = Path.GetFileNameWithoutExtension(metadata.SourcePath);
            int bandNumber = band.BandIndex + 1;
            string exrPath = Path.Combine(_outputDirectory, $"{baseName}_band{bandNumber}.exr");
            string jsonPath = Path.Combine(_outputDirectory, $"{baseName}_band{bandNumber}.json");

            try
            {
                ExrWriteResult writeResult = WriteExrFile(metadata.Width, metadata.Height, band.Pixels, 1, exrPath, requestedFormat);
                WriteBandMetadataJson(metadata, band, exrPath, jsonPath, requestedFormat, writeResult.EncodedFormat, writeResult.ActualChannels);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GeoTiffToExr] 写入波段 {band.BandIndex + 1} 失败：{ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        private bool TryWriteCombinedOutputs(GeoTiffConversionResult result)
        {
            int bandCount = result.Bands.Count;
            if (bandCount <= 1)
            {
                return false;
            }

            if (bandCount > 4)
            {
                Debug.LogWarning("[GeoTiffToExr] 波段数量超过 4 个，无法合并为单个 EXR。");
                return false;
            }

            int width = result.Metadata.Width;
            int height = result.Metadata.Height;
            int pixelCount = width * height;

            foreach (GeoTiffBandResult band in result.Bands)
            {
                if (band.Pixels.Length != pixelCount)
                {
                    Debug.LogError($"[GeoTiffToExr] 波段 {band.BandIndex + 1} 像素数量与图像尺寸不匹配，无法合并。");
                    return false;
                }
            }

            ExrDataFormat requestedFormat = ResolveEffectiveDataFormat();
            ResolveTextureFormat(bandCount, ResolveEncodedDataFormat(requestedFormat), out int actualChannelsForBuffer);
            float[] combined = new float[pixelCount * actualChannelsForBuffer];

            for (int i = 0; i < pixelCount; i++)
            {
                for (int channel = 0; channel < actualChannelsForBuffer; channel++)
                {
                    float value = channel < bandCount ? result.Bands[channel].Pixels[i] : 0f;
                    combined[i * actualChannelsForBuffer + channel] = value;
                }
            }

            string baseName = Path.GetFileNameWithoutExtension(result.Metadata.SourcePath);
            string exrPath = Path.Combine(_outputDirectory, $"{baseName}_combined.exr");
            string jsonPath = Path.Combine(_outputDirectory, $"{baseName}_combined.json");

            try
            {
                ExrWriteResult writeResult = WriteExrFile(width, height, combined, bandCount, exrPath, requestedFormat);
                WriteCombinedMetadataJson(result, requestedFormat, writeResult.EncodedFormat, writeResult.ActualChannels, exrPath, jsonPath);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GeoTiffToExr] 写入合并 EXR 失败：{ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        private ExrWriteResult WriteExrFile(int width, int height, float[] sourcePixels, int requestedChannels, string exrPath, ExrDataFormat requestedFormat)
        {
            ExrDataFormat encodedFormat = ResolveEncodedDataFormat(requestedFormat);
            TextureFormat textureFormat = ResolveTextureFormat(requestedChannels, encodedFormat, out int actualChannels);
            int expectedLength = width * height * actualChannels;

            float[] pixelData = PreparePixelData(sourcePixels, expectedLength, requestedFormat);

            Texture2D texture = new Texture2D(width, height, textureFormat, mipChain: false, linear: true);
            texture.SetPixelData(pixelData, 0);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            Texture2D.EXRFlags flags = encodedFormat == ExrDataFormat.Float16
                ? Texture2D.EXRFlags.None
                : Texture2D.EXRFlags.OutputAsFloat;
            byte[] bytes = texture.EncodeToEXR(flags);
            File.WriteAllBytes(exrPath, bytes);

            UnityEngine.Object.DestroyImmediate(texture);

            return new ExrWriteResult(actualChannels, encodedFormat);
        }

        private TextureFormat ResolveTextureFormat(int requestedChannels, ExrDataFormat encodedFormat, out int actualChannels)
        {
            bool useHalf = encodedFormat == ExrDataFormat.Float16;

            if (requestedChannels <= 1)
            {
                actualChannels = 1;
                return useHalf ? TextureFormat.RHalf : TextureFormat.RFloat;
            }

            if (requestedChannels == 2)
            {
                actualChannels = 2;
                return useHalf ? TextureFormat.RGHalf : TextureFormat.RGFloat;
            }

            actualChannels = requestedChannels >= 4 ? 4 : requestedChannels;
            return useHalf ? TextureFormat.RGBAHalf : TextureFormat.RGBAFloat;
        }

        private float[] PreparePixelData(float[] source, int expectedLength, ExrDataFormat requestedFormat)
        {
            float[] working = source;
            if (working.Length != expectedLength)
            {
                float[] resized = new float[expectedLength];
                int copyLength = Math.Min(working.Length, expectedLength);
                Array.Copy(working, resized, copyLength);
                working = resized;
                Debug.LogWarning($"[GeoTiffToExr] 像素数组长度 {source.Length} 与期望 {expectedLength} 不一致，已自动调整。");
            }

            if (requestedFormat == ExrDataFormat.UInt8 || requestedFormat == ExrDataFormat.UInt16)
            {
                float max = requestedFormat == ExrDataFormat.UInt8 ? 255f : 65535f;
                float[] converted = new float[working.Length];
                for (int i = 0; i < working.Length; i++)
                {
                    float value = working[i];
                    if (float.IsNaN(value) || float.IsInfinity(value))
                        value = 0f;
                    value = Mathf.Clamp(value, 0f, max);
                    converted[i] = Mathf.Round(value);
                }
                return converted;
            }

            return working;
        }

        private ExrDataFormat ResolveEffectiveDataFormat()
        {
            if (_requestedDataFormat == ExrDataFormat.Auto)
            {
                return _metadata?.GetDefaultExrDataFormat() ?? ExrDataFormat.Float32;
            }

            return _requestedDataFormat;
        }

        private static ExrDataFormat ResolveEncodedDataFormat(ExrDataFormat requested)
        {
            return requested == ExrDataFormat.Float16 ? ExrDataFormat.Float16 : ExrDataFormat.Float32;
        }

        private static string GetDataFormatLabel(ExrDataFormat format)
        {
            return format switch
            {
                ExrDataFormat.Auto => "Auto",
                ExrDataFormat.Float16 => "Float16",
                ExrDataFormat.Float32 => "Float32",
                ExrDataFormat.UInt16 => "UInt16",
                ExrDataFormat.UInt8 => "UInt8",
                _ => format.ToString()
            };
        }

        private static string GetEncodedFormatLabel(ExrDataFormat format)
        {
            return format switch
            {
                ExrDataFormat.Float16 => "Float16",
                _ => "Float32"
            };
        }


        private void WriteBandMetadataJson(GeoTiffMetadata metadata, GeoTiffBandResult band, string exrPath, string jsonPath, ExrDataFormat requestedFormat, ExrDataFormat encodedFormat, int channelCount)
        {
            var jsonMetadata = new GeoTiffToExrMetadata
            {
                sourceFile = Path.GetFileName(metadata.SourcePath),
                exrFile = Path.GetFileName(exrPath),
                width = metadata.Width,
                height = metadata.Height,
                requestedDataFormat = GetDataFormatLabel(requestedFormat),
                encodedDataFormat = GetEncodedFormatLabel(encodedFormat),
                channelCount = channelCount,
                geoBounds = ConvertGeoBounds(metadata.GeoBounds),
                band = new BandData
                {
                    index = band.BandIndex,
                    dataMinimum = band.DataMinimum,
                    dataMaximum = band.DataMaximum,
                    sourceNoData = band.SourceNoDataValue,
                    outputNoData = band.OutputNoDataValue
                }
            };

            string json = JsonUtility.ToJson(jsonMetadata, true);
            File.WriteAllText(jsonPath, json);
        }

        private void WriteCombinedMetadataJson(GeoTiffConversionResult result, ExrDataFormat requestedFormat, ExrDataFormat encodedFormat, int encodedChannels, string exrPath, string jsonPath)
        {
            var bands = new BandData[result.Bands.Count];
            for (int i = 0; i < result.Bands.Count; i++)
            {
                GeoTiffBandResult band = result.Bands[i];
                bands[i] = new BandData
                {
                    index = band.BandIndex,
                    dataMinimum = band.DataMinimum,
                    dataMaximum = band.DataMaximum,
                    sourceNoData = band.SourceNoDataValue,
                    outputNoData = band.OutputNoDataValue
                };
            }

            var jsonMetadata = new GeoTiffToExrMetadata
            {
                sourceFile = Path.GetFileName(result.Metadata.SourcePath),
                exrFile = Path.GetFileName(exrPath),
                width = result.Metadata.Width,
                height = result.Metadata.Height,
                requestedDataFormat = GetDataFormatLabel(requestedFormat),
                encodedDataFormat = GetEncodedFormatLabel(encodedFormat),
                channelCount = encodedChannels,
                geoBounds = ConvertGeoBounds(result.Metadata.GeoBounds),
                channelPacking = new ChannelPackingData
                {
                    mode = "Combined",
                    requestedBandCount = result.Bands.Count,
                    encodedChannelCount = encodedChannels
                },
                bands = bands
            };

            string json = JsonUtility.ToJson(jsonMetadata, true);
            File.WriteAllText(jsonPath, json);
        }

        private GeoBounds ConvertGeoBounds(GeoTiffGeoBounds? geoBounds)
        {
            if (!geoBounds.HasValue)
                return null;

            var bounds = geoBounds.Value;
            return new GeoBounds
            {
                minLatitude = bounds.MinLatitude,
                maxLatitude = bounds.MaxLatitude,
                minLongitude = bounds.MinLongitude,
                maxLongitude = bounds.MaxLongitude,
                centerLatitude = bounds.CenterLatitude,
                centerLongitude = bounds.CenterLongitude
            };
        }

        private readonly struct ExrWriteResult
        {
            public ExrWriteResult(int actualChannels, ExrDataFormat encodedFormat)
            {
                ActualChannels = actualChannels;
                EncodedFormat = encodedFormat;
            }

            public int ActualChannels { get; }
            public ExrDataFormat EncodedFormat { get; }
        }

    }
}
#endif
