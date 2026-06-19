using System.Collections.Generic;

namespace GeoToolkit.GeoTiffTools
{
    public enum NoDataReplacementMode
    {
        PreserveSourceValue = 0,
        UseCustomValue = 1,
        UseBandMinimum = 2
    }

    public enum ExrDataFormat
    {
        Auto = 0,
        Float32 = 1,
        Float16 = 2,
        UInt16 = 3,
        UInt8 = 4
    }

    public sealed class GeoTiffToExrOptions
    {
        public IReadOnlyCollection<int>? SelectedBands { get; set; }
        public bool EnableMultithreading { get; set; } = true;
        public NoDataReplacementMode NoDataReplacement { get; set; } = NoDataReplacementMode.PreserveSourceValue;
        public float CustomNoDataValue { get; set; } = 0f;
        public float? SourceNoDataOverride { get; set; }
        public bool SplitBands { get; set; } = true;
        public ExrDataFormat OutputDataFormat { get; set; } = ExrDataFormat.Auto;
        public bool FlipY { get; set; } = false;
    }
}
