using System.Collections.Generic;

namespace GeoToolkit.GeoTiffTools
{
    public sealed class GeoTiffConversionResult
    {
        public GeoTiffMetadata Metadata { get; }
        public IReadOnlyList<GeoTiffBandResult> Bands { get; }

        public GeoTiffConversionResult(GeoTiffMetadata metadata, IReadOnlyList<GeoTiffBandResult> bands)
        {
            Metadata = metadata;
            Bands = bands;
        }
    }
}
