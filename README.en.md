# UnityGeoToolkit

[中文](README.md)

UnityGeoToolkit is a personal geospatial toolkit archive for Unity 6000. It collects reusable building blocks that repeatedly appear in geospatial content production and runtime visualization: 64-bit native containers, binary serialization, double-precision geospatial math, editor import frameworks, terrain repair, road generation, radar voxelization, and scene utilities.

The repository is a technical archive rather than a complete business platform. Real project data, internal endpoints, credentials, commercial art assets, and business workflow shells have been removed or replaced with placeholders.

## Highlights

- `NativeCollections64/`: `NativeArray64`, `NativeList64`, and `UnsafeList64` with `ulong` length support, plus an `IJobParallelFor64` scheduling interface.
- `Serialization/`: high-performance binary serialization for large caches and tile data, including unsafe direct-write paths.
- `GeoMath/`: double-precision vectors, longitude/latitude, Web Mercator, tile row/column conversion, tile IDs, and basic Delaunay algorithms.
- `EditorImporter/`: a plugin-style editor import framework with importer skeletons, shared windows, file utilities, material helpers, and tile-coordinate tools.
- `Terrain/`: seam detection, seam repair, and height smoothing for adjacent terrain tiles.
- `RoadGen/`: Unity Splines based road and railway mesh generation, intersection stitching, and hand-drawn road tools.
- `RadarVoxel/` and `RadarEnvelope/`: radar detection voxelization and hemisphere/fan/ring scan-envelope visualization.

## Installation And Dependencies

1. In Unity Package Manager, choose `Add package from disk...` and select this repository's `package.json`.
2. Use Unity 6000 or a compatible version.
3. Install the dependencies listed in `package.json`, especially `mathematics`, `burst`, `collections`, `newtonsoft-json`, and `com.unity.splines`.
4. This repository does not include real geospatial datasets or online services. Start with the synthetic splines, synthetic tiles, and public coordinate examples described in `Samples~/README.md`.

## Usage Notes

If you only want the core technical pieces, start with `NativeCollections64/` and `GeoMath/`. If you are building a geospatial content import pipeline, continue with `EditorImporter/`, `Terrain/`, and `RoadGen/`; radar-related modules start at `RadarVoxel/` and `RadarEnvelope/`.

## Sanitization And Licensing

- Private brand names, business place names, real coordinate lists, internal URLs, credentials, and commercial assets have been removed.
- `LICENSE` only covers original or rewritten code in this repository.
- Triangle.NET, Unity packages, and Newtonsoft Json remain governed by their own licenses. See `THIRD_PARTY_NOTICES.md`.
- See `脱敏复核报告.md` for the sanitization review.

## Related Repositories

- `GeoMath` can be compared with `CesiumforUnrealSDK`'s `CoordinateConverter`.
- Compared with the two Cesium repositories, this repository focuses on editor import frameworks, terrain/road/radar tooling, and 64-bit containers instead of runtime 3D geospatial tile loading.
- The road, terrain, importer, and radar tools can serve as a foundation for Unity geospatial editor tooling.

## Current Status

The module extraction, Chinese module notes, English entry document, third-party notices, and sanitization review are complete. The package has not yet been imported and compiled in Unity Editor; run a Unity 6000 local package import before production use.
