using System;
using System.Globalization;
using UnityEngine;

namespace GeoToolkit
{
    /// <summary>
    /// A set of Geo and Terrain Conversion utils.
    /// </summary>
    public static class Conversions
    {
        private const int TileSize = 256;
        private const int EarthRadius = 6378137; //no seams with globe example
        private const double InitialResolution = 2 * Math.PI * EarthRadius / TileSize;
        private const double OriginShift = 2 * Math.PI * EarthRadius / 2;
        private const double WebMercMax = 20037508.342789244;

        /// <summary>
        /// Converts <see cref="T:Mapbox.Utils.Vector2d"/> struct, WGS84
        /// lat/lon to Spherical Mercator EPSG:900913 xy meters.
        /// </summary>
        /// <param name="v"> The <see cref="T:Mapbox.Utils.Vector2d"/>. </param>
        /// <returns> A <see cref="T:UnityEngine.Vector2d"/> of coordinates in meters. </returns>
        public static Vector2d LatLonToMeters(Vector2d v)
        {
            return LatLonToMeters(v.x, v.y);
        }

        /// <summary>
        /// Convert a simple string to a latitude longitude.
        /// Expects format: latitude, longitude
        /// </summary>
        /// <returns>The lat/lon as Vector2d.</returns>
        /// <param name="s">string.</param>
        public static Vector2d StringToLatLon(string s)
        {
            var latLonSplit = s.Split(',');
            if (latLonSplit.Length != 2)
            {
                throw new ArgumentException("Wrong number of arguments");
            }

            double latitude = 0;
            double longitude = 0;

            if (!double.TryParse(latLonSplit[0], NumberStyles.Any, NumberFormatInfo.InvariantInfo, out latitude))
            {
                throw new Exception(string.Format("Could not convert latitude to double: {0}", latLonSplit[0]));
            }

            if (!double.TryParse(latLonSplit[1], NumberStyles.Any, NumberFormatInfo.InvariantInfo, out longitude))
            {
                throw new Exception(string.Format("Could not convert longitude to double: {0}", latLonSplit[0]));
            }

            return new Vector2d(latitude, longitude);
        }

        /// <summary>
        /// Converts WGS84 lat/lon to Spherical Mercator EPSG:900913 xy meters.
        /// SOURCE: https://stackoverflow.com/questions/12896139/geographic-coordinates-converter.
        /// </summary>
        /// <param name="lat"> The latitude. </param>
        /// <param name="lon"> The longitude. </param>
        /// <returns> A <see cref="T:UnityEngine.Vector2d"/> of xy meters. </returns>
        public static Vector2d LatLonToMeters(double lat, double lon)
        {
            var posx = lon * OriginShift / 180;
            var posy = Math.Log(Math.Tan((90 + lat) * Math.PI / 360)) / (Math.PI / 180);
            posy = posy * OriginShift / 180;
            return new Vector2d(posx, posy);
        }

        /// <summary>
        /// Converts WGS84 lat/lon to x/y meters in reference to a center point
        /// </summary>
        /// <param name="lat"> The latitude. </param>
        /// <param name="lon"> The longitude. </param>
        /// <param name="refPoint"> A <see cref="T:UnityEngine.Vector2d"/> center point to offset resultant xy, this is usually map's center mercator</param>
        /// <param name="scale"> Scale in meters. (default scale = 1) </param>
        /// <returns> A <see cref="T:UnityEngine.Vector2d"/> xy tile ID. </returns>
        /// <example>
        /// Converts a Lat/Lon of (37.7749, 122.4194) into Unity coordinates for a map centered at (10,10) and a scale of 2.5 meters for every 1 Unity unit 
        /// <code>
        /// var worldPosition = Conversions.GeoToWorldPosition(37.7749, 122.4194, new Vector2d(10, 10), (float)2.5);
        /// // worldPosition = ( 11369163.38585, 34069138.17805 )
        /// </code>
        /// </example>
        public static Vector2d GeoToWorldPosition(double lat, double lon, Vector2d refPoint, float scale = 1)
        {
            var posx = lon * OriginShift / 180;
            var posy = Math.Log(Math.Tan((90 + lat) * Math.PI / 360)) / (Math.PI / 180);
            posy = posy * OriginShift / 180;
            return new Vector2d((posx - refPoint.x) * scale, (posy - refPoint.y) * scale);
        }

        public static Vector2d GeoToWorldPosition(Vector2d latLong, Vector2d refPoint, float scale = 1)
        {
            return GeoToWorldPosition(latLong.x, latLong.y, refPoint, scale);
        }

        public static Vector3 GeoToWorldGlobePosition(double lat, double lon, float radius)
        {
            double xPos = (radius) * Math.Cos(Mathf.Deg2Rad * lat) * Math.Cos(Mathf.Deg2Rad * lon);
            double zPos = (radius) * Math.Cos(Mathf.Deg2Rad * lat) * Math.Sin(Mathf.Deg2Rad * lon);
            double yPos = (radius) * Math.Sin(Mathf.Deg2Rad * lat);

            return new Vector3((float)xPos, (float)yPos, (float)zPos);
        }

        public static Vector3 GeoToWorldGlobePosition(Vector2d latLong, float radius)
        {
            return GeoToWorldGlobePosition(latLong.x, latLong.y, radius);
        }

        public static Vector2d GeoFromGlobePosition(Vector3 point, float radius)
        {
            float latitude = Mathf.Asin(point.y / radius);
            float longitude = Mathf.Atan2(point.z, point.x);
            return new Vector2d(latitude * Mathf.Rad2Deg, longitude * Mathf.Rad2Deg);
        }

        /// <summary>
        /// Converts Spherical Mercator EPSG:900913 in xy meters to WGS84 lat/lon.
        /// Inverse of LatLonToMeters.
        /// </summary
        /// <param name="m"> A <see cref="T:UnityEngine.Vector2d"/> of coordinates in meters.  </param>
        /// <returns> The <see cref="T:Mapbox.Utils.Vector2d"/> in lat/lon. </returns>>

        /// <example>
        /// Converts EPSG:900913 xy meter coordinates to lat lon 
        /// <code>
        /// var worldPosition =  new Vector2d (4547675.35434,13627665.27122);
        /// var latlon = Conversions.MetersToLatLon(worldPosition);
        /// // latlon = ( 37.77490, 122.41940 )
        /// </code>
        /// </example>
        public static Vector2d MetersToLatLon(Vector2d m)
        {
            var vx = (m.x / OriginShift) * 180;
            var vy = (m.y / OriginShift) * 180;
            vy = 180 / Math.PI * (2 * Math.Atan(Math.Exp(vy * Math.PI / 180)) - Math.PI / 2);
            return new Vector2d(vx, vy);
        }

        /// <summary>
        /// Gets the xy tile ID from Spherical Mercator EPSG:900913 xy coords.
        /// </summary>
        /// <param name="m"> <see cref="T:UnityEngine.Vector2d"/> XY coords in meters. </param>
        /// <param name="zoom"> Zoom level. </param>
        /// <returns> A <see cref="T:UnityEngine.Vector2d"/> xy tile ID. </returns>
        /// 
        /// <example>
        /// Converts EPSG:900913 xy meter coordinates to web mercator tile XY coordinates at zoom 12.
        /// <code>
        /// var meterXYPosition = new Vector2d (4547675.35434,13627665.27122);
        /// var tileXY = Conversions.MetersToTile (meterXYPosition, 12);
        /// // tileXY = ( 655, 2512 )
        /// </code>
        /// </example>
        public static Vector2 MetersToTile(Vector2d m, int zoom)
        {
            var p = MetersToPixels(m, zoom);
            return PixelsToTile(p);
        }

        /// <summary>
        /// Gets the tile bounds in Spherical Mercator EPSG:900913 meters from an xy tile ID.
        /// </summary>
        /// <param name="tileCoordinate"> XY tile ID. </param>
        /// <param name="zoom"> Zoom level. </param>
        /// <returns> A <see cref="T:UnityEngine.Rect"/> in meters. </returns>
        public static RectD TileBounds(Vector2 tileCoordinate, int zoom)
        {
            var min = PixelsToMeters(new Vector2d(tileCoordinate.x * TileSize, tileCoordinate.y * TileSize), zoom);
            var max = PixelsToMeters(new Vector2d((tileCoordinate.x + 1) * TileSize, (tileCoordinate.y + 1) * TileSize), zoom);
            return new RectD(min, max - min);
        }

        public static RectD TileBounds(UnwrappedTileId unwrappedTileId)
        {
            var min = PixelsToMeters(new Vector2d(unwrappedTileId.X * TileSize, unwrappedTileId.Y * TileSize), unwrappedTileId.Z);
            var max = PixelsToMeters(new Vector2d((unwrappedTileId.X + 1) * TileSize, (unwrappedTileId.Y + 1) * TileSize), unwrappedTileId.Z);
            return new RectD(min, max - min);
        }

        /// <summary>
        /// Gets the xy tile ID at the requested zoom that contains the WGS84 lat/lon point.
        /// See: https://wiki.openstreetmap.org/wiki/Slippy_map_tilenames.
        /// </summary>
        /// <param name="latitude"> The latitude. </param>
        /// <param name="longitude"> The longitude. </param>
        /// <param name="zoom"> Zoom level. </param>
        /// <returns> A <see cref="T:Mapbox.Map.UnwrappedTileId"/> xy tile ID. </returns>
        public static UnwrappedTileId LatitudeLongitudeToTileId(double latitude, double longitude, int zoom)
        {
            var x = (int)Math.Floor((longitude + 180.0) / 360.0 * Math.Pow(2.0, zoom));
            var y = (int)Math.Floor((1.0 - Math.Log(Math.Tan(latitude * Math.PI / 180.0)
                    + 1.0 / Math.Cos(latitude * Math.PI / 180.0)) / Math.PI) / 2.0 * Math.Pow(2.0, zoom));

            return new UnwrappedTileId(zoom, x, y);
        }


        /// <summary>
        /// Get coordinates for a given latitude/longitude in tile-space. Useful when comparing feature geometry to lat/lon coordinates.
        /// </summary>
        /// <returns>The longitude to tile position.</returns>
        /// <param name="coordinate">Coordinate.</param>
        /// <param name="tileZoom">The zoom level of the tile.</param>
        /// <param name="layerExtent">Layer extent. Optional, but recommended. Defaults to 4096, the standard for Mapbox Tiles</param>
        public static Vector2 LatitudeLongitudeToVectorTilePosition(Vector2d coordinate, int tileZoom, ulong layerExtent = 4096)
        {
            var coordinateTileId = Conversions.LatitudeLongitudeToTileId(
                coordinate.x, coordinate.y, tileZoom);
            var _meters = LatLonToMeters(coordinate);
            var _rect = Conversions.TileBounds(coordinateTileId);

            //vectortile space point (0 - layerExtent)
            var vectorTilePoint = new Vector2((float)((_meters - _rect.Min).x / _rect.Size.x) * layerExtent,
                                              (float)(layerExtent - ((_meters - _rect.Max).y / _rect.Size.y) * layerExtent));

            return vectorTilePoint;
        }

        /// <summary>
        /// Get coordinates for a given latitude/longitude in tile-space. Useful when comparing feature geometry to lat/lon coordinates.
        /// </summary>
        /// <returns>The longitude to tile position.</returns>
        /// <param name="coordinate">Coordinate.</param>
        /// <param name="tileZoom">The zoom level of the tile.</param>
        /// <param name="tileScale">Tile scale. Optional, but recommended. Defaults to a scale of 1.</param>
        /// <param name="layerExtent">Layer extent. Optional, but recommended. Defaults to 4096, the standard for Mapbox Tiles</param>
        public static Vector2 LatitudeLongitudeToUnityTilePosition(Vector2d coordinate, int tileZoom, float tileScale, ulong layerExtent = 4096)
        {
            var coordinateTileId = Conversions.LatitudeLongitudeToTileId(
                coordinate.x, coordinate.y, tileZoom);
            var _rect = Conversions.TileBounds(coordinateTileId);

            //vectortile space point (0 - layerExtent)
            var vectorTilePoint = LatitudeLongitudeToVectorTilePosition(coordinate, tileZoom, layerExtent);

            //UnityTile space
            var unityTilePoint = new Vector2((float)(vectorTilePoint.x / layerExtent * _rect.Size.x - (_rect.Size.x / 2)) * tileScale,
                                             (float)((layerExtent - vectorTilePoint.y) / layerExtent * _rect.Size.y - (_rect.Size.y / 2)) * tileScale);

            return unityTilePoint;
        }

        /// <summary>
        /// Gets the WGS84 longitude of the northwest corner from a tile's X position and zoom level.
        /// See: https://wiki.openstreetmap.org/wiki/Slippy_map_tilenames.
        /// </summary>
        /// <param name="x"> Tile X position. </param>
        /// <param name="zoom"> Zoom level. </param>
        /// <returns> NW Longitude. </returns>
        public static double TileXToNWLongitude(int x, int zoom)
        {
            var n = Math.Pow(2.0, zoom);
            var lon_deg = x / n * 360.0 - 180.0;
            return lon_deg;
        }

        /// <summary>
        /// Gets the WGS84 latitude of the northwest corner from a tile's Y position and zoom level.
        /// See: https://wiki.openstreetmap.org/wiki/Slippy_map_tilenames.
        /// </summary>
        /// <param name="y"> Tile Y position. </param>
        /// <param name="zoom"> Zoom level. </param>
        /// <returns> NW Latitude. </returns>
        public static double TileYToNWLatitude(int y, int zoom)
        {
            var n = Math.Pow(2.0, zoom);
            var lat_rad = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * y / n)));
            var lat_deg = lat_rad * 180.0 / Math.PI;
            return lat_deg;
        }

        /// <summary>
        /// Gets the <see cref="T:Mapbox.Utils.Vector2dBounds"/> of a tile.
        /// </summary>
        /// <param name="x"> Tile X position. </param>
        /// <param name="y"> Tile Y position. </param>
        /// <param name="zoom"> Zoom level. </param>
        /// <returns> The <see cref="T:Mapbox.Utils.Vector2dBounds"/> of the tile. </returns>
        public static Vector2dBounds TileIdToBounds(int x, int y, int zoom)
        {
            var sw = new Vector2d(TileYToNWLatitude(y, zoom), TileXToNWLongitude(x + 1, zoom));
            var ne = new Vector2d(TileYToNWLatitude(y + 1, zoom), TileXToNWLongitude(x, zoom));
            return new Vector2dBounds(sw, ne);
        }

        /// <summary>
        /// Gets the WGS84 lat/lon of the center of a tile.
        /// </summary>
        /// <param name="x"> Tile X position. </param>
        /// <param name="y"> Tile Y position. </param>
        /// <param name="zoom"> Zoom level. </param>
        /// <returns>A <see cref="T:UnityEngine.Vector2d"/> of lat/lon coordinates.</returns>
        public static Vector2d TileIdToCenterLatitudeLongitude(int x, int y, int zoom)
        {
            var bb = TileIdToBounds(x, y, zoom);
            var center = bb.Center;
            return new Vector2d(center.x, center.y);
        }


        /// <summary>
        /// Gets the Web Mercator x/y of the center of a tile.
        /// </summary>
        /// <param name="x"> Tile X position. </param>
        /// <param name="y"> Tile Y position. </param>
        /// <param name="zoom"> Zoom level. </param>
        /// <returns>A <see cref="T:UnityEngine.Vector2d"/> of lat/lon coordinates.</returns>
        public static Vector2d TileIdToCenterWebMercator(int x, int y, int zoom)
        {
            double tileCnt = Math.Pow(2, zoom);
            double centerX = x + 0.5;
            double centerY = y + 0.5;

            centerX = ((centerX / tileCnt * 2) - 1) * WebMercMax;
            centerY = (1 - (centerY / tileCnt * 2)) * WebMercMax;
            return new Vector2d(centerX, centerY);
        }

        /// <summary>
        /// Gets the meters per pixels at given latitude and zoom level for a 256x256 tile.
        /// See: https://wiki.openstreetmap.org/wiki/Zoom_levels.
        /// </summary>
        /// <param name="latitude"> The latitude. </param>
        /// <param name="zoom"> Zoom level. </param>
        /// <returns> Meters per pixel. </returns>
        public static float GetTileScaleInMeters(float latitude, int zoom)
        {
            return (float)(40075016.685578d * Math.Cos(Mathf.Deg2Rad * latitude) / Math.Pow(2f, zoom + 8));
        }

        /// <summary>
        /// Gets the degrees per tile at given zoom level for Web Mercator tile.
        /// See: https://wiki.openstreetmap.org/wiki/Zoom_levels.
        /// </summary>
        /// <param name="latitude"> The latitude. </param>
        /// <param name="zoom"> Zoom level. </param>
        /// <returns> Degrees per tile. </returns>
        public static float GetTileScaleInDegrees(float latitude, int zoom)
        {
            return (float)(360.0f / Math.Pow(2f, zoom + 8));
        }

        /// <summary>
        /// Gets height from terrain-rgb adjusted for a given scale.
        /// </summary>
        /// <param name="color"> The <see cref="T:UnityEngine.Color"/>. </param>
        /// <param name="relativeScale"> Relative scale. </param>
        /// <returns> Adjusted height in meters. </returns>
        public static float GetRelativeHeightFromColor(Color color, float relativeScale)
        {
            return GetAbsoluteHeightFromColor(color) * relativeScale;
        }

        /// <summary>
        /// Specific formula for mapbox.terrain-rgb to decode height values from pixel values.
        /// See: https://www.mapbox.com/blog/terrain-rgb/.
        /// </summary>
        /// <param name="color"> The <see cref="T:UnityEngine.Color"/>. </param>
        /// <returns> Height in meters. </returns>
        public static float GetAbsoluteHeightFromColor(Color color)
        {
            return (float)(-10000 + ((color.r * 255 * 256 * 256 + color.g * 255 * 256 + color.b * 255) * 0.1));
        }

        public static float GetAbsoluteHeightFromColor32(Color32 color)
        {
            return (float)(-10000 + ((color.r * 256 * 256 + color.g * 256 + color.b) * 0.1));
        }

        public static float GetAbsoluteHeightFromColor(float r, float g, float b)
        {
            return (-10000f + ((r * 65536f + g * 256f + b) * 0.1f));
        }

        public static double CalculateTileSize(int zoomLevel)
        {
            // 每像素的地面分辨率（以米为单位）
            double resolution = 156543.03 / Math.Pow(2, zoomLevel);

            // 瓦片的地面尺寸（以米为单位）
            double tileSize = 256 * resolution;

            return tileSize;
        }

        private static double Resolution(int zoom)
        {
            return InitialResolution / Math.Pow(2, zoom);
        }

        private static Vector2d PixelsToMeters(Vector2d p, int zoom)
        {
            var res = Resolution(zoom);
            var met = new Vector2d();
            met.x = (p.x * res - OriginShift);
            met.y = -(p.y * res - OriginShift);
            return met;
        }

        private static Vector2d MetersToPixels(Vector2d m, int zoom)
        {
            var res = Resolution(zoom);
            var pix = new Vector2d(((m.x + OriginShift) / res), ((-m.y + OriginShift) / res));
            return pix;
        }

        private static Vector2 PixelsToTile(Vector2d p)
        {
            var t = new Vector2((int)Math.Ceiling(p.x / (double)TileSize) - 1, (int)Math.Ceiling(p.y / (double)TileSize) - 1);
            return t;
        }


        #region 李强那抄来的

        public static (double lonMin, double latMin, double lonMax, double latMax, double center_lon, double center_lat
           )
           GetTileRange(int x, int y, int z, string flag = "3857")
        {
            int n = (int)Math.Pow(2, z);
            double lonMin, latMax, lonMax, latMin, centerLon, centerLat;

            if (flag == "4326")
            {
                lonMin = (x * 180.0d) / n - 180.0d;
                latMax = 90.0d - (y * 180.0d) / n;
                lonMax = ((x + 1d) * 180.0d) / n - 180.0d;
                latMin = 90.0d - ((y + 1d) * 180.0d) / n;
            }
            else if (flag == "3857")
            {
                lonMin = (x / (double)n) * 360.0d - 180.0d;
                latMax = (Math.Atan(Math.Sinh(Math.PI * (1d - (2d * y) / (double)n))) * 180.0d) / Math.PI;
                lonMax = ((x + 1d) / (double)n) * 360.0d - 180.0d;
                latMin = (Math.Atan(Math.Sinh(Math.PI * (1d - (2d * (y + 1d)) / (double)n))) * 180.0d) / Math.PI;
            }
            else
            {
                throw new ArgumentException("Invalid flag value");
            }

            centerLon = (lonMin + lonMax) / 2.0d;
            centerLat = (latMin + latMax) / 2.0d;
            return (lonMin, latMin, lonMax, latMax, centerLon, centerLat);
        }


        public static Vector3 ParseLonLatHeight(double lon, double lat, double height)
        {
            double earth_rad = 6378137.0d;
            double pos_x = lon * Math.PI * earth_rad / 180d;
            double pos_y = Math.Log(Math.Tan((90d + lat) * Math.PI / 360d)) / (Math.PI / 180d);
            pos_y = pos_y * Math.PI * earth_rad / 180d;

            return new Vector3((float)(pos_x), (float)height, (float)(pos_y));
        }

        public static Vector2 ParseLonLatHeight(double lon, double lat)
        {
            double earth_rad = 6378137.0d;
            double pos_x = lon * Math.PI * earth_rad / 180d;
            double pos_y = Math.Log(Math.Tan((90d + lat) * Math.PI / 360d)) / (Math.PI / 180d);
            pos_y = pos_y * Math.PI * earth_rad / 180d;

            return new Vector2((float)(pos_x), (float)(pos_y));
        }

        #endregion


        #region 何辉那抄来的

        /// <summary>
        /// 修改地形
        /// </summary>
        /// <param name="terrain"></param>
        /// <param name="layerName"></param>
        public static void ModifyTerrain(Terrain terrain, string layerName)
        {
            int resolution = terrain.terrainData.heightmapResolution;
            Vector3 size = terrain.terrainData.size;
            Vector3 terrainPos = terrain.transform.position;
            float[,] hts = terrain.terrainData.GetHeights(0, 0, resolution, resolution);
            RaycastHit hit;
            float rayLength = size.y * 2;
            int layerMask = 1 << LayerMask.NameToLayer(layerName);
            Vector3 origin = Vector3.zero;
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    origin.x = x + terrainPos.x;
                    origin.y = size.y;
                    origin.z = y + terrainPos.z;
                    if (Physics.Raycast(origin, Vector3.down, out hit, rayLength, layerMask))
                    {
                        hts[y, x] = (hit.point.y - terrainPos.y) / size.y;
                    }
                }
            }
            terrain.terrainData.SetHeights(0, 0, hts);
        }

        public static bool TryGetIntersectPoint(Vector3 a, Vector3 b, Vector3 c, Vector3 d, out Vector3 intersectPos)
        {
            intersectPos = Vector3.zero;

            Vector3 ab = b - a;
            Vector3 ca = a - c;
            Vector3 cd = d - c;

            Vector3 v1 = Vector3.Cross(ca, cd);

            if (Mathf.Abs(Vector3.Dot(v1, ab)) > 1e-6)
            {
                return false;
            }

            if (Vector3.Cross(ab, cd).sqrMagnitude <= 1e-6)
            {
                return false;
            }

            Vector3 ad = d - a;
            Vector3 cb = b - c;
            if (Mathf.Min(a.x, b.x) > Mathf.Max(c.x, d.x) || Mathf.Max(a.x, b.x) < Mathf.Min(c.x, d.x)
               || Mathf.Min(a.y, b.y) > Mathf.Max(c.y, d.y) || Mathf.Max(a.y, b.y) < Mathf.Min(c.y, d.y)
               || Mathf.Min(a.z, b.z) > Mathf.Max(c.z, d.z) || Mathf.Max(a.z, b.z) < Mathf.Min(c.z, d.z)
            )
                return false;

            if (Vector3.Dot(Vector3.Cross(-ca, ab), Vector3.Cross(ab, ad)) > 0
                && Vector3.Dot(Vector3.Cross(ca, cd), Vector3.Cross(cd, cb)) > 0)
            {
                Vector3 v2 = Vector3.Cross(cd, ab);
                float ratio = Vector3.Dot(v1, v2) / v2.sqrMagnitude;
                intersectPos = a + ab * ratio;
                return true;
            }

            return false;
        }

        #endregion
    }

}