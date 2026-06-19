using BitMiracle.LibTiff.Classic;
using System;
using System.Globalization;

namespace GeoToolkit.GeoTiffTools
{
    public sealed class GeoTiffMetadata
    {
        public string SourcePath { get; }
        public int Width { get; }
        public int Height { get; }
        public int BitsPerSample { get; }
        public int SamplesPerPixel { get; }
        public SampleFormat SampleFormat { get; }
        public string Compression { get; }
        public GeoTiffGeoBounds? GeoBounds { get; }
        public double[]? PixelScale { get; }
        public double[]? TiePoints { get; }
        public double? NoDataValue { get; }

        public GeoTiffMetadata(
            string sourcePath,
            int width,
            int height,
            int bitsPerSample,
            int samplesPerPixel,
            SampleFormat sampleFormat,
            string compression,
            GeoTiffGeoBounds? geoBounds,
            double[]? pixelScale,
            double[]? tiePoints,
            double? noDataValue)
        {
            SourcePath = sourcePath;
            Width = width;
            Height = height;
            BitsPerSample = bitsPerSample;
            SamplesPerPixel = samplesPerPixel;
            SampleFormat = sampleFormat;
            Compression = compression;
            GeoBounds = geoBounds;
            PixelScale = pixelScale;
            TiePoints = tiePoints;
            NoDataValue = noDataValue;
        }

        public static GeoTiffMetadata FromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required", nameof(filePath));
            if (!System.IO.File.Exists(filePath))
                throw new System.IO.FileNotFoundException("TIFF file not found", filePath);

            using Tiff tiff = Tiff.Open(filePath, "r") ?? throw new InvalidOperationException("Failed to open TIFF file.");

            int width = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
            int height = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
            int bitsPerSample = tiff.GetField(TiffTag.BITSPERSAMPLE)[0].ToInt();

            FieldValue[]? samplesPerPixelField = tiff.GetField(TiffTag.SAMPLESPERPIXEL);
            int samplesPerPixel = samplesPerPixelField != null ? samplesPerPixelField[0].ToInt() : 1;

            FieldValue[]? sampleFormatField = tiff.GetField(TiffTag.SAMPLEFORMAT);
            SampleFormat sampleFormat = sampleFormatField != null
                ? (SampleFormat)sampleFormatField[0].ToInt()
                : SampleFormat.UINT;

            FieldValue[]? compressionField = tiff.GetField(TiffTag.COMPRESSION);
            string compression = compressionField != null
                ? ((Compression)compressionField[0].ToInt()).ToString()
                : "Unknown";

            double[]? pixelScale = TryGetDoubleArray(tiff, (TiffTag)33550);
            double[]? tiePoints = TryGetDoubleArray(tiff, (TiffTag)33922);
            GeoTiffGeoBounds? bounds = GeoTiffGeoBounds.TryCreate(width, height, pixelScale, tiePoints);

            double? noData = TryGetNoDataValue(tiff);

            return new GeoTiffMetadata(
                filePath,
                width,
                height,
                bitsPerSample,
                samplesPerPixel,
                sampleFormat,
                compression,
                bounds,
                pixelScale,
                tiePoints,
                noData);
        }

        public ExrDataFormat GetDefaultExrDataFormat()
        {
            switch (SampleFormat)
            {
                case SampleFormat.IEEEFP:
                    if (BitsPerSample <= 16)
                        return ExrDataFormat.Float16;
                    return ExrDataFormat.Float32;

                case SampleFormat.UINT:
                    if (BitsPerSample <= 8)
                        return ExrDataFormat.UInt8;
                    if (BitsPerSample <= 16)
                        return ExrDataFormat.UInt16;
                    return ExrDataFormat.Float32;

                case SampleFormat.INT:
                    return ExrDataFormat.Float32;

                default:
                    return ExrDataFormat.Float32;
            }
        }

        private static double[]? TryGetDoubleArray(Tiff tiff, TiffTag tag)
        {
            FieldValue[]? field = tiff.GetField(tag);
            if (field == null || field.Length == 0)
                return null;

            byte[] bytes = field[1].GetBytes();
            if (bytes == null || bytes.Length == 0)
                return null;

            int elements = bytes.Length / sizeof(double);
            double[] values = new double[elements];
            for (int i = 0; i < elements; i++)
            {
                values[i] = BitConverter.ToDouble(bytes, i * sizeof(double));
            }

            return values;
        }

        private static double? TryGetNoDataValue(Tiff tiff)
        {
            FieldValue[]? field = tiff.GetField((TiffTag)42113); // GDAL_NODATA
            if (field == null || field.Length == 0)
                return null;

            string? text = field[0].ToString();
            if (string.IsNullOrEmpty(text))
                return null;

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                return value;

            return null;
        }
    }

    public readonly struct GeoTiffGeoBounds
    {
        public double MinLatitude { get; }
        public double MaxLatitude { get; }
        public double MinLongitude { get; }
        public double MaxLongitude { get; }
        public double CenterLatitude { get; }
        public double CenterLongitude { get; }

        private GeoTiffGeoBounds(double minLat, double maxLat, double minLon, double maxLon)
        {
            MinLatitude = minLat;
            MaxLatitude = maxLat;
            MinLongitude = minLon;
            MaxLongitude = maxLon;
            CenterLatitude = (minLat + maxLat) * 0.5d;
            CenterLongitude = (minLon + maxLon) * 0.5d;
        }

        public static GeoTiffGeoBounds? TryCreate(int width, int height, double[]? pixelScale, double[]? tiePoints)
        {
            if (pixelScale == null || pixelScale.Length < 2)
                return null;
            if (tiePoints == null || tiePoints.Length < 6)
                return null;

            double originX = tiePoints[3];
            double originY = tiePoints[4];
            double scaleX = pixelScale[0];
            double scaleY = pixelScale[1];

            double minLon = originX;
            double maxLon = originX + width * scaleX;
            double maxLat = originY;
            double minLat = originY - height * scaleY;

            return new GeoTiffGeoBounds(minLat, maxLat, minLon, maxLon);
        }
    }
}
