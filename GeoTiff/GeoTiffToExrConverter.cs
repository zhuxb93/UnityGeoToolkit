using BitMiracle.LibTiff.Classic;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace GeoToolkit.GeoTiffTools
{
    public sealed class GeoTiffToExrConverter
    {
        private const double NoDataEqualityTolerance = 1e-6d;

        public GeoTiffConversionResult Convert(string filePath, GeoTiffToExrOptions? options = null, CancellationToken cancellationToken = default)
        {
            options ??= new GeoTiffToExrOptions();
            GeoTiffMetadata metadata = GeoTiffMetadata.FromFile(filePath);

            IReadOnlyList<int> selectedBands = ResolveBands(metadata, options.SelectedBands);

            var bandResults = metadata.SamplesPerPixel > 1 && options.EnableMultithreading
                ? ConvertBandsInParallel(filePath, metadata, selectedBands, options, cancellationToken)
                : ConvertBandsSequential(filePath, metadata, selectedBands, options, cancellationToken);

            return new GeoTiffConversionResult(metadata, bandResults);
        }

        public Task<GeoTiffConversionResult> ConvertAsync(string filePath, GeoTiffToExrOptions? options = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Convert(filePath, options, cancellationToken), cancellationToken);
        }
        private static IReadOnlyList<GeoTiffBandResult> ConvertBandsSequential(
            string filePath,
            GeoTiffMetadata metadata,
            IReadOnlyList<int> selectedBands,
            GeoTiffToExrOptions options,
            CancellationToken token)
        {
            var results = new List<GeoTiffBandResult>(selectedBands.Count);
            foreach (int band in selectedBands)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    GeoTiffBandResult bandResult = ProcessBand(filePath, metadata, band, options, token);
                    results.Add(bandResult);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GeoTiffToExr] 文件 {Path.GetFileName(filePath)} 的波段 {band + 1} 处理失败: {ex.Message}\n{ex.StackTrace}");
                }
            }

            return results;

        }
        private static IReadOnlyList<GeoTiffBandResult> ConvertBandsInParallel(
            string filePath,
            GeoTiffMetadata metadata,
            IReadOnlyList<int> selectedBands,
            GeoTiffToExrOptions options,
            CancellationToken token)
        {
            var tasks = selectedBands
                .Select(band =>
                {
                    int bandIndex = band;
                    return Task.Run<GeoTiffBandResult?>(() =>
                    {
                        token.ThrowIfCancellationRequested();
                        try
                        {
                            return ProcessBand(filePath, metadata, bandIndex, options, token);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[GeoTiffToExr] 文件 {Path.GetFileName(filePath)} 的波段 {bandIndex + 1} 处理失败: {ex.Message}\n{ex.StackTrace}");
                            return null;
                        }
                    }, token);
                })
                .ToArray();

            Task.WhenAll(tasks).GetAwaiter().GetResult();
            var results = new List<GeoTiffBandResult>(selectedBands.Count);
            foreach (Task<GeoTiffBandResult?> task in tasks)
            {
                GeoTiffBandResult? bandResult = task.Result;
                if (bandResult != null)
                {
                    results.Add(bandResult);
                }
            }

            // Keep results ordered by band index for determinism
            results.Sort((a, b) => a.BandIndex.CompareTo(b.BandIndex));
            return results;
        }

        private static IReadOnlyList<int> ResolveBands(GeoTiffMetadata metadata, IReadOnlyCollection<int>? requestedBands)
        {
            if (requestedBands == null || requestedBands.Count == 0)
            {
                int[] allBands = new int[metadata.SamplesPerPixel];
                for (int i = 0; i < metadata.SamplesPerPixel; i++)
                {
                    allBands[i] = i;
                }

                return allBands;
            }

            var resolved = new List<int>(requestedBands.Count);
            foreach (int band in requestedBands)
            {
                if (band < 0 || band >= metadata.SamplesPerPixel)
                    throw new ArgumentOutOfRangeException(nameof(requestedBands), $"Band index {band} is out of range.");
                if (!resolved.Contains(band))
                    resolved.Add(band);
            }

            resolved.Sort();
            return resolved;
        }

        private static GeoTiffBandResult ProcessBand(
            string filePath,
            GeoTiffMetadata metadata,
            int bandIndex,
            GeoTiffToExrOptions options,
            CancellationToken token)
        {
            using Tiff tiff = Tiff.Open(filePath, "r") ?? throw new InvalidOperationException("Unable to reopen TIFF file for band processing.");

            FieldValue[]? planarConfigField = tiff.GetField(TiffTag.PLANARCONFIG);
            PlanarConfig planarConfig = planarConfigField != null
                ? (PlanarConfig)planarConfigField[0].ToInt()
                : PlanarConfig.CONTIG;

            int width = metadata.Width;
            int height = metadata.Height;
            int bytesPerSample = (metadata.BitsPerSample + 7) / 8;
            int samplesPerPixel = metadata.SamplesPerPixel;

            float[] pixels = new float[width * height];

            double minValue = double.PositiveInfinity;
            double maxValue = double.NegativeInfinity;
            bool hasValidSample = false;
            bool hasNoDataSample = false;

            double? sourceNoData = options.SourceNoDataOverride.HasValue ? options.SourceNoDataOverride.Value : metadata.NoDataValue;

            if (tiff.IsTiled())
            {
                ReadBandFromTiles(tiff, metadata, bandIndex, planarConfig, bytesPerSample, samplesPerPixel, pixels, sourceNoData,
                    ref minValue, ref maxValue, ref hasValidSample, ref hasNoDataSample, token);
            }
            else
            {
                ReadBandFromScanlines(tiff, metadata, bandIndex, planarConfig, bytesPerSample, samplesPerPixel, pixels, sourceNoData,
                    ref minValue, ref maxValue, ref hasValidSample, ref hasNoDataSample, token);
            }

            if (!hasValidSample)
            {
                minValue = maxValue = 0d;
            }

            double? outputNoDataValue = null;
            if (hasNoDataSample)
            {
                switch (options.NoDataReplacement)
                {
                    case NoDataReplacementMode.PreserveSourceValue:
                        outputNoDataValue = sourceNoData;
                        if (sourceNoData.HasValue)
                        {
                            ReplaceNoData(pixels, (float)sourceNoData.Value);
                        }
                        break;
                    case NoDataReplacementMode.UseCustomValue:
                        outputNoDataValue = options.CustomNoDataValue;
                        ReplaceNoData(pixels, options.CustomNoDataValue);
                        break;
                    case NoDataReplacementMode.UseBandMinimum:
                        double fallback = hasValidSample ? minValue : 0d;
                        outputNoDataValue = fallback;
                        ReplaceNoData(pixels, (float)fallback);
                        break;
                }
            }

            // Apply FlipY transformation if requested
            if (options.FlipY)
            {
                FlipPixelsVertically(pixels, width, height);
                Debug.Log($"[GeoTiffToExr] 波段 {bandIndex + 1} 已应用 FlipY 上下镜像翻转");
            }

            return new GeoTiffBandResult(bandIndex, pixels, minValue, maxValue, sourceNoData, outputNoDataValue);
        }

        private static void FlipPixelsVertically(float[] pixels, int width, int height)
        {
            for (int row = 0; row < height / 2; row++)
            {
                int topRowStartIndex = row * width;
                int bottomRowStartIndex = (height - 1 - row) * width;

                for (int col = 0; col < width; col++)
                {
                    int topIndex = topRowStartIndex + col;
                    int bottomIndex = bottomRowStartIndex + col;

                    // Swap pixels
                    float temp = pixels[topIndex];
                    pixels[topIndex] = pixels[bottomIndex];
                    pixels[bottomIndex] = temp;
                }
            }
        }

        private static void ReadBandFromScanlines(
            Tiff tiff,
            GeoTiffMetadata metadata,
            int bandIndex,
            PlanarConfig planarConfig,
            int bytesPerSample,
            int samplesPerPixel,
            float[] pixels,
            double? sourceNoData,
            ref double minValue,
            ref double maxValue,
            ref bool hasValidSample,
            ref bool hasNoDataSample,
            CancellationToken token)
        {
            int width = metadata.Width;
            int height = metadata.Height;
            int scanlineSize = tiff.ScanlineSize();
            byte[] scanline = new byte[scanlineSize];

            var stripCache = new Dictionary<long, StripCacheEntry>();
            int rowsPerStrip = GetRowsPerStripValue(tiff, height);

            for (int row = 0; row < height; row++)
            {
                token.ThrowIfCancellationRequested();

                bool readSuccess = planarConfig == PlanarConfig.SEPARATE
                    ? tiff.ReadScanline(scanline, row, (short)bandIndex)
                    : tiff.ReadScanline(scanline, row);

                if (!readSuccess)
                {
                    readSuccess = TryReadScanlineFromStrip(tiff, planarConfig, bandIndex, row, scanline, stripCache, rowsPerStrip, scanlineSize, height);
                    if (!readSuccess)
                    {
                        Debug.LogError($"[GeoTiffToExr] 读取扫描行失败 {row} 波段 {bandIndex + 1}");
                        FillScanlineWithNoData(pixels, width, row, sourceNoData, ref hasNoDataSample);
                        continue;
                    }
                }

                int rowOffset = row * width;
                if (planarConfig == PlanarConfig.CONTIG)
                {
                    int pixelStride = samplesPerPixel * bytesPerSample;
                    for (int col = 0; col < width; col++)
                    {
                        int pixelOffset = col * pixelStride + bandIndex * bytesPerSample;
                        double value = ReadSample(scanline, pixelOffset, metadata.BitsPerSample, metadata.SampleFormat);
                        AssignPixel(pixels, rowOffset + col, value, sourceNoData, ref minValue, ref maxValue, ref hasValidSample, ref hasNoDataSample);
                    }
                }
                else
                {
                    for (int col = 0; col < width; col++)
                    {
                        int pixelOffset = col * bytesPerSample;
                        double value = ReadSample(scanline, pixelOffset, metadata.BitsPerSample, metadata.SampleFormat);
                        AssignPixel(pixels, rowOffset + col, value, sourceNoData, ref minValue, ref maxValue, ref hasValidSample, ref hasNoDataSample);
                    }
                }
            }
        }
        private static void FillScanlineWithNoData(float[] pixels, int width, int row, double? sourceNoData, ref bool hasNoDataSample)
        {
            float replacement = sourceNoData.HasValue ? (float)sourceNoData.Value : float.NaN;
            hasNoDataSample = true;
            int start = row * width;
            int end = Math.Min(start + width, pixels.Length);
            for (int i = start; i < end; i++)
            {
                pixels[i] = replacement;
            }
        }


        private static void ReadBandFromTiles(
            Tiff tiff,
            GeoTiffMetadata metadata,
            int bandIndex,
            PlanarConfig planarConfig,
            int bytesPerSample,
            int samplesPerPixel,
            float[] pixels,
            double? sourceNoData,
            ref double minValue,
            ref double maxValue,
            ref bool hasValidSample,
            ref bool hasNoDataSample,
            CancellationToken token)
        {
            int width = metadata.Width;
            int height = metadata.Height;

            FieldValue[]? tileWidthField = tiff.GetField(TiffTag.TILEWIDTH);
            FieldValue[]? tileLengthField = tiff.GetField(TiffTag.TILELENGTH);
            int tileWidth = tileWidthField != null ? tileWidthField[0].ToInt() : width;
            int tileHeight = tileLengthField != null ? tileLengthField[0].ToInt() : Math.Min(height, tileWidth);

            if (tileWidth <= 0 || tileWidth > width)
            {
                tileWidth = width;
            }

            if (tileHeight <= 0 || tileHeight > height)
            {
                tileHeight = height;
            }

            int tileBufferSize = tiff.TileSize();
            if (tileBufferSize <= 0)
            {
                throw new IOException("TIFF tile size is invalid.");
            }

            bool isContig = planarConfig == PlanarConfig.CONTIG;
            int pixelStride = samplesPerPixel * bytesPerSample;
            int tileRowStrideContig = tileWidth * pixelStride;
            int tileRowStrideSeparate = tileWidth * bytesPerSample;
            int requiredTileRowStride = isContig ? tileRowStrideContig : tileRowStrideSeparate;
            int requiredTileSize = requiredTileRowStride * tileHeight;
            if (requiredTileSize <= 0)
            {
                requiredTileSize = tileBufferSize;
            }

            byte[] tileBuffer = new byte[Math.Max(tileBufferSize, requiredTileSize * 2)];
            short samplePlane = isContig ? (short)0 : (short)bandIndex;

            for (int tileY = 0; tileY < height; tileY += tileHeight)
            {
                int rowsInTile = Math.Min(tileHeight, height - tileY);
                for (int tileX = 0; tileX < width; tileX += tileWidth)
                {
                    token.ThrowIfCancellationRequested();

                    int colsInTile = Math.Min(tileWidth, width - tileX);
                    int readBytes;
                    try
                    {
                        readBytes = tiff.ReadTile(tileBuffer, 0, tileX, tileY, 0, samplePlane);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[GeoTiffToExr] 读取瓦片失败 ({tileX},{tileY}) 波段 {bandIndex + 1}: {ex.Message}\n{ex.StackTrace}");
                        MarkTileAsNoData(pixels, width, height, tileX, tileY, colsInTile, rowsInTile, sourceNoData, ref minValue, ref maxValue, ref hasValidSample, ref hasNoDataSample);
                        continue;
                    }
                    if (readBytes <= 0)
                    {
                        MarkTileAsNoData(pixels, width, height, tileX, tileY, colsInTile, rowsInTile, sourceNoData, ref minValue, ref maxValue, ref hasValidSample, ref hasNoDataSample);
                        Debug.LogError($"[GeoTiffToExr] 读取瓦片返回 0 ({tileX},{tileY}) 波段 {bandIndex + 1}");
                        continue;
                    }

                    for (int row = 0; row < rowsInTile; row++)
                    {
                        int destRow = tileY + row;
                        int destRowOffset = destRow * width;
                        int srcRowOffset = row * (isContig ? tileRowStrideContig : tileRowStrideSeparate);

                        for (int col = 0; col < colsInTile; col++)
                        {
                            int destIndex = destRowOffset + tileX + col;
                            int srcOffset = isContig
                                ? srcRowOffset + col * pixelStride + bandIndex * bytesPerSample
                                : srcRowOffset + col * bytesPerSample;

                            if (srcOffset + bytesPerSample > readBytes)
                            {
                                pixels[destIndex] = float.NaN;
                                hasNoDataSample = true;
                                continue;
                            }

                            double value = ReadSample(tileBuffer, srcOffset, metadata.BitsPerSample, metadata.SampleFormat);
                            AssignPixel(pixels, destIndex, value, sourceNoData, ref minValue, ref maxValue, ref hasValidSample, ref hasNoDataSample);
                        }
                    }
                }
            }
        }

        private static void MarkTileAsNoData(float[] pixels, int width, int height, int tileX, int tileY, int colsInTile, int rowsInTile, double? sourceNoData, ref double minValue, ref double maxValue, ref bool hasValidSample, ref bool hasNoDataSample)
        {
            float replacement = sourceNoData.HasValue ? (float)sourceNoData.Value : float.NaN;
            hasNoDataSample = true;
            for (int row = 0; row < rowsInTile; row++)
            {
                int destRow = tileY + row;
                if (destRow >= height)
                    break;
                int destRowOffset = destRow * width;
                for (int col = 0; col < colsInTile; col++)
                {
                    int destCol = tileX + col;
                    if (destCol >= width)
                        break;
                    int destIndex = destRowOffset + destCol;
                    pixels[destIndex] = replacement;
                }
            }
        }

        private static bool TryReadScanlineFromStrip(
            Tiff tiff,
            PlanarConfig planarConfig,
            int bandIndex,
            int row,
            byte[] destination,
            Dictionary<long, StripCacheEntry> cache,
            int rowsPerStrip,
            int scanlineLength,
            int imageHeight)
        {
            short sample = planarConfig == PlanarConfig.SEPARATE ? (short)bandIndex : (short)0;
            int stripIndex = tiff.ComputeStrip(row, sample);
            long cacheKey = (((long)sample) << 32) | (uint)stripIndex;

            if (!cache.TryGetValue(cacheKey, out StripCacheEntry entry))
            {
                int stripSize = tiff.StripSize();
                if (stripSize <= 0)
                {
                    return false;
                }

                byte[] buffer = new byte[stripSize];
                int bytesRead = tiff.ReadEncodedStrip(stripIndex, buffer, 0, stripSize);
                if (bytesRead <= 0)
                {
                    return false;
                }

                if (bytesRead < buffer.Length)
                {
                    Array.Resize(ref buffer, bytesRead);
                }

                int rowsInBuffer = Math.Max(1, bytesRead / Math.Max(1, scanlineLength));
                entry = new StripCacheEntry(buffer, bytesRead, rowsInBuffer);
                cache[cacheKey] = entry;
            }

            int stripStartRow = rowsPerStrip > 0 ? rowsPerStrip * stripIndex : 0;
            if (stripStartRow >= imageHeight)
            {
                stripStartRow = Math.Max(0, imageHeight - entry.RowsInBuffer);
            }

            int rowWithinStrip = row - stripStartRow;
            if (rowWithinStrip < 0)
            {
                return false;
            }

            int rowsAvailable = entry.RowsInBuffer;
            rowsAvailable = Math.Min(rowsAvailable, Math.Max(1, imageHeight - stripStartRow));
            if (rowWithinStrip >= rowsAvailable)
            {
                return false;
            }

            int offset = rowWithinStrip * scanlineLength;
            if (offset + scanlineLength > entry.BytesRead)
            {
                return false;
            }

            Buffer.BlockCopy(entry.Buffer, offset, destination, 0, scanlineLength);
            return true;
        }

        private static int GetRowsPerStripValue(Tiff tiff, int imageHeight)
        {
            FieldValue[]? rowsPerStripField = tiff.GetField(TiffTag.ROWSPERSTRIP);
            if (rowsPerStripField == null || rowsPerStripField.Length == 0)
            {
                return imageHeight;
            }

            int rowsPerStrip = rowsPerStripField[0].ToInt();
            if (rowsPerStrip <= 0 || rowsPerStrip > imageHeight)
            {
                return imageHeight;
            }

            return rowsPerStrip;
        }

        private readonly struct StripCacheEntry
        {
            public StripCacheEntry(byte[] buffer, int bytesRead, int rowsInBuffer)
            {
                Buffer = buffer;
                BytesRead = bytesRead;
                RowsInBuffer = rowsInBuffer;
            }

            public byte[] Buffer { get; }
            public int BytesRead { get; }
            public int RowsInBuffer { get; }
        }
        private static void AssignPixel(
            float[] pixels,
            int index,
            double value,
            double? sourceNoData,
            ref double minValue,
            ref double maxValue,
            ref bool hasValidSample,
            ref bool hasNoDataSample)
        {
            bool isNoData = false;
            double? noDataValue = sourceNoData;

            if (noDataValue.HasValue)
            {
                if (double.IsNaN(noDataValue.Value))
                {
                    isNoData = double.IsNaN(value);
                }
                else
                {
                    isNoData = Math.Abs(value - noDataValue.Value) <= NoDataEqualityTolerance;
                }
            }
            else if (double.IsNaN(value))
            {
                isNoData = true;
            }

            if (isNoData)
            {
                pixels[index] = float.NaN;
                hasNoDataSample = true;
                return;
            }

            float floatValue = (float)value;
            pixels[index] = floatValue;
            if (!double.IsNaN(value))
            {
                minValue = Math.Min(minValue, value);
                maxValue = Math.Max(maxValue, value);
                hasValidSample = true;
            }
        }

        private static void ReplaceNoData(float[] pixels, float replacement)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                if (float.IsNaN(pixels[i]))
                {
                    pixels[i] = replacement;
                }
            }
        }

        private static double ReadSample(byte[] buffer, int offset, int bitsPerSample, SampleFormat sampleFormat)
        {
            return bitsPerSample switch
            {
                8 => sampleFormat == SampleFormat.INT
                    ? (sbyte)buffer[offset]
                    : buffer[offset],
                16 => sampleFormat == SampleFormat.INT
                    ? BitConverter.ToInt16(buffer, offset)
                    : BitConverter.ToUInt16(buffer, offset),
                24 => Read24BitSample(buffer, offset, sampleFormat),
                32 => sampleFormat switch
                {
                    SampleFormat.IEEEFP => BitConverter.ToSingle(buffer, offset),
                    SampleFormat.INT => BitConverter.ToInt32(buffer, offset),
                    _ => BitConverter.ToUInt32(buffer, offset)
                },
                64 => sampleFormat switch
                {
                    SampleFormat.IEEEFP => BitConverter.ToDouble(buffer, offset),
                    SampleFormat.INT => BitConverter.ToInt64(buffer, offset),
                    _ => BitConverter.ToUInt64(buffer, offset)
                },
                _ => throw new NotSupportedException($"Unsupported bits-per-sample value: {bitsPerSample}")
            };
        }

        private static double Read24BitSample(byte[] buffer, int offset, SampleFormat sampleFormat)
        {
            int value = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16);
            if (sampleFormat == SampleFormat.INT)
            {
                if ((value & 0x800000) != 0)
                    value |= unchecked((int)0xFF000000);
                return value;
            }

            return value;
        }

    }
}















