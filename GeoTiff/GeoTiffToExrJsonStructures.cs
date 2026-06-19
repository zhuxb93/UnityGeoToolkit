using System;

namespace GeoToolkit.GeoTiffTools
{
    [Serializable]
    public class GeoTiffToExrMetadata
    {
        public string sourceFile;
        public string exrFile;
        public int width;
        public int height;
        public string requestedDataFormat;
        public string encodedDataFormat;
        public int channelCount;
        public GeoBounds geoBounds;
        public BandData band;
        public ChannelPackingData channelPacking;
        public BandData[] bands;

        // 非序列化字段，仅用于运行时存储JSON文件路径
        [System.NonSerialized]
        public string jsonFilePath;
    }

    [Serializable]
    public class GeoBounds
    {
        public double minLatitude;
        public double maxLatitude;
        public double minLongitude;
        public double maxLongitude;
        public double centerLatitude;
        public double centerLongitude;
    }

    [Serializable]
    public class BandData
    {
        public int index;
        public double dataMinimum;
        public double dataMaximum;
        public double? sourceNoData;
        public double? outputNoData;
    }

    [Serializable]
    public class ChannelPackingData
    {
        public string mode;
        public int requestedBandCount;
        public int encodedChannelCount;
    }
}