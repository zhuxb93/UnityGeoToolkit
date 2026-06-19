namespace GeoToolkit.GeoTiffTools
{
    public sealed class GeoTiffBandResult
    {
        public int BandIndex { get; }
        public float[] Pixels { get; }
        public double DataMinimum { get; }
        public double DataMaximum { get; }
        public double? SourceNoDataValue { get; }
        public double? OutputNoDataValue { get; }

        public GeoTiffBandResult(int bandIndex, float[] pixels, double dataMinimum, double dataMaximum, double? sourceNoDataValue, double? outputNoDataValue)
        {
            BandIndex = bandIndex;
            Pixels = pixels;
            DataMinimum = dataMinimum;
            DataMaximum = dataMaximum;
            SourceNoDataValue = sourceNoDataValue;
            OutputNoDataValue = outputNoDataValue;
        }
    }
}
