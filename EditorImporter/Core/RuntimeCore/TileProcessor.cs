using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace GeoToolkit
{
    public class TileProcessor
    {
        /// <summary>
        /// 计算一张瓦片内左上角和右下角的经纬度
        /// </summary>
        /// <param name="tileID"></param>
        /// <returns></returns>
        public static (double2, double2) GetTileBounds(TileID tileID)
        {
            double2 tileCenter2D = Get4326TileCenterPosition(tileID);

            // 计算16级瓦片的经度切割
            double longitudeSpan = 360.0 / Math.Pow(2, tileID.z);

            // 计算左上角和右下角的经纬度
            double leftTopY = tileCenter2D.y + (longitudeSpan / 2);
            double leftTopX = tileCenter2D.x - (longitudeSpan / 2);
            double rightBottomY = tileCenter2D.y - (longitudeSpan / 2);
            double rightBottomX = tileCenter2D.x + (longitudeSpan / 2);

            return (new double2(leftTopX, leftTopY), new double2(rightBottomX, rightBottomY));

        }


        /// <summary>
        /// 获取4326瓦片号的中心点经纬度
        /// </summary>
        /// <param name="tileID"></param>
        /// <returns></returns>
        public static double2 Get4326TileCenterPosition(TileID tileID)
        {
            int n = (int)(Math.Pow(2, tileID.z));
            double tempInterval = 180.0 / n; 

            double lon = tileID.x * tempInterval - 180.0 + tempInterval / 2.0;
            double lat = (n - tileID.y - 1) * tempInterval - 90.0 + tempInterval / 2.0;

            return new double2(lon, lat);
        }

        /// <summary>
        /// 获取3857瓦片号左下角经纬度
        /// </summary>
        /// <param name="tileID"></param>
        /// <returns></returns>
        public static double2 Get3857TileLeftBottomLonLat(TileID tileID)
        {
            // TODO: 这里的const变量应该放在一个统一的地方
            const int TileSize = 256;
            const int EarthRadius = 6378137; //no seams with globe example
            const double InitialResolution = 2 * Math.PI * EarthRadius / TileSize;
            const double WebMercMax = 20037508.342789244;

            int z = tileID.z;
            double xTile = tileID.x;
            double yTile = tileID.y;

            // 使用你的常量计算瓦片尺寸
            double tileSize = (InitialResolution * TileSize) / Math.Pow(2, z);

            // 计算中心点投影坐标（米）
            double xCenter = (xTile) * tileSize - WebMercMax;
            double yCenter = WebMercMax - (yTile + 1.0) * tileSize;

            // 转换为经纬度
            double lon = (xCenter / EarthRadius) * (180.0 / Math.PI);
            double latRad = Math.Atan(Math.Sinh(yCenter / EarthRadius)); 
            double lat = latRad * (180.0 / Math.PI);

            return new double2(lon, lat);
        }

        /// <summary>
        /// 获取3857瓦片号右上角经纬度
        /// </summary>
        /// <param name="tileID"></param>
        /// <returns></returns>
        public static double2 Get3857TileRightTopLonLat(TileID tileID)
        {
            const int TileSize = 256;
            const int EarthRadius = 6378137;
            const double InitialResolution = 2 * Math.PI * EarthRadius / TileSize;
            const double WebMercMax = 20037508.342789244;

            int z = tileID.z;
            double xTile = tileID.x;
            double yTile = tileID.y;

            // 瓦片尺寸（米）
            double tileSize = (InitialResolution * TileSize) / Math.Pow(2, z);

            // 右上角投影坐标
            double xRight = (xTile + 1.0) * tileSize - WebMercMax;
            double yTop = WebMercMax - (yTile) * tileSize;

            // 转换为经纬度
            double lon = (xRight / EarthRadius) * (180.0 / Math.PI);
            double latRad = Math.Atan(Math.Sinh(yTop / EarthRadius));
            double lat = latRad * (180.0 / Math.PI);

            return new double2(lon, lat);
        }

        /// <summary>
        /// 将经纬度和级别转换为3857投影的瓦片号
        /// </summary>
        /// <param name="lon">经度</param>
        /// <param name="lat">纬度</param>
        /// <param name="zoom">缩放级别</param>
        /// <returns>瓦片ID</returns>
        public static TileID Get3857TileIDFromLonLat(double lon, double lat, int zoom)
        {
            const int EarthRadius = 6378137;
            const double WebMercMax = 20037508.342789244;

            // 将经纬度转换为Web墨卡托投影坐标（米）
            double x = lon * (Math.PI / 180.0) * EarthRadius;
            double y = Math.Log(Math.Tan((90 + lat) * Math.PI / 360.0)) * EarthRadius;

            // 限制坐标在有效范围内
            x = Math.Max(-WebMercMax, Math.Min(WebMercMax, x));
            y = Math.Max(-WebMercMax, Math.Min(WebMercMax, y));

            // 计算瓦片总数
            int tileCount = (int)Math.Pow(2, zoom);

            // 将坐标转换为瓦片坐标
            double normalizedX = (x + WebMercMax) / (2 * WebMercMax);
            double normalizedY = (WebMercMax - y) / (2 * WebMercMax);

            int tileX = (int)Math.Floor(normalizedX * tileCount);
            int tileY = (int)Math.Floor(normalizedY * tileCount);

            // 确保瓦片坐标在有效范围内
            tileX = Math.Max(0, Math.Min(tileCount - 1, tileX));
            tileY = Math.Max(0, Math.Min(tileCount - 1, tileY));

            return new TileID(tileX, tileY, zoom);
        }

        public static double2 Get3857TileCenterLonLat(TileID tileID)
        {
            // TODO: 这里的const变量应该放在一个统一的地方
            const int TileSize = 256;
            const int EarthRadius = 6378137; //no seams with globe example
            const double InitialResolution = 2 * Math.PI * EarthRadius / TileSize;
            const double WebMercMax = 20037508.342789244;

            int z = tileID.z;
            double xTile = tileID.x;
            double yTile = tileID.y;

            // 使用你的常量计算瓦片尺寸
            double tileSize = (InitialResolution * TileSize) / Math.Pow(2, z);

            // 计算中心点投影坐标（米）
            double xCenter = (xTile + 0.5) * tileSize - WebMercMax;
            double yCenter = WebMercMax - (yTile + 0.5) * tileSize;

            // 转换为经纬度
            double lon = (xCenter / EarthRadius) * (180.0 / Math.PI);
            double latRad = Math.Atan(Math.Sinh(yCenter / EarthRadius));
            double lat = latRad * (180.0 / Math.PI);

            return new double2(lon, lat);
        }

        public static TileID LonLatToTile4326(double lat, double lon, int z)
        {
            int n = (int)(Math.Pow(2, z));
            double tempInterval = 180.0 / n;
            double mx = lon + 180.0;
            double my = lat + 90.0;
            mx /= tempInterval;
            my /= tempInterval;

            // ix,iy为瓦片编码
            int tileX = (int)(mx);
            //int y = (int)(n - my);
            int tileY = (int)(my);
            var tileID = new TileID();
            tileID.x = tileX;
            tileID.y = tileY;
            tileID.z = z;
            return tileID;

        }


        /// <summary>
        /// 细分瓦片行列号
        /// </summary>
        /// <param name="tile"></param>
        /// <param name="targetZoomLevel"></param>
        /// <returns></returns>
        public static List<TileID> SubdivideTile(TileID tile, int targetZoomLevel)
        {
            List<TileID> subTiles = new List<TileID>();

            int currentZoomLevel = (int)tile.z;
            if (targetZoomLevel <= currentZoomLevel)
            {
                subTiles.Add(tile);
                return subTiles;
            }

            int zoomDifference = targetZoomLevel - currentZoomLevel;
            int factor = (int)Math.Pow(2, zoomDifference);

            for (int i = 0; i < factor; i++)
            {
                for (int j = 0; j < factor; j++)
                {
                    TileID tileID = new TileID();
                    tileID.x = (tile.x * factor + i);
                    tileID.y = (tile.y * factor + j);
                    tileID.z = targetZoomLevel;
                    subTiles.Add(tileID);
                }
            }

            return subTiles;
        }

        /// <summary>
        /// 将瓦片调整到与目标层级相同
        /// </summary>
        public static TileID AdjustTileToSameZoomLevel(TileID tileId, int targetZoom)
        {
            if (tileId.z == targetZoom)
            {
                return tileId;
            }

            int zoomDiff = tileId.z - targetZoom;

            // 使用位运算替代 Math.Pow(2, n)
            int scale = 1 << Math.Abs(zoomDiff);

            int newX = zoomDiff > 0 ? tileId.x / scale : tileId.x * scale;
            int newY = zoomDiff > 0 ? tileId.y / scale : tileId.y * scale;

            return new TileID(newX, newY, targetZoom);
        }

        public static TileID StringToTile(string tileID)
        {
            var tildStr = tileID.Split('-');
            var tilid = new TileID(Convert.ToInt32(tildStr[1]), Convert.ToInt32(tildStr[2]), Convert.ToInt32(tildStr[0]));

            return tilid;
        }
    }


}