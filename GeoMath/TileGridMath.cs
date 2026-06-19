using System.Collections.Generic;
using UnityEngine;
namespace GeoToolkit
{
    public class TileGridMath
    {
        #region GEO
        ///// <summary>
        ///// 根据经纬度 计算瓦片行列号
        ///// </summary>
        ///// <param name="longitude"></param>
        ///// <param name="latitude"></param>
        ///// <param name="zoom"></param>
        ///// <returns></returns>
        //public static int[] LongitudeLatitudeToTileId(double longitude, double latitude, int zoom)
        //{
        //    var x = (int)Mathd.Floor((longitude + 180.0) / 360.0 * Mathd.Pow(2.0d, zoom));
        //    var y = (int)Mathd.Floor((1.0 - Mathd.Log(Mathd.Tan(latitude * Mathd.PI / 180.0)
        //            + 1.0 / Mathd.Cos(latitude * Mathd.PI / 180.0)) / Mathd.PI) / 2.0 * Mathd.Pow(2.0d, zoom));
        //    return new int[3] { zoom, x, y };
        //}

        ///// <summary>
        ///// 根据墨卡托坐标 计算 经纬度
        ///// </summary>
        ///// <param name="mx"></param>
        ///// <param name="my"></param>
        ///// <returns></returns>
        //public static (double x, double y) MercatorToLonLat(double mx, double my)
        //{
        //    double OriginShift = 20037508.34d; // 2*PI*R/2
        //    double x = mx / OriginShift * 180;
        //    double y = my / OriginShift * 180;
        //    y = 180 / Math.PI * (2 * Math.Atan(Math.Exp(y * Math.PI / 180)) - Math.PI / 2);
        //    return (x, y);
        //}
        ///// <summary>
        ///// 根据经纬度 计算墨卡托坐标
        ///// </summary>
        ///// <param name="lon"></param>
        ///// <param name="lat"></param>
        ///// <param name="height"></param>
        ///// <returns></returns>
        //public static (double x, double y, double z) LonLatHeightToMercator(double lon, double lat, double height)
        //{
        //    double earth_rad = 6378137.0d;
        //    double pos_x = lon * Math.PI * earth_rad / 180d;
        //    double pos_y = Math.Log(Math.Tan((90d + lat) * Math.PI / 360d)) / (Math.PI / 180d);
        //    pos_y = pos_y * Math.PI * earth_rad / 180d;

        //    return (pos_x, height, pos_y);
        //}
        ///// <summary>
        ///// 根据瓦片行列号 计算瓦片中心墨卡托坐标
        ///// </summary>
        ///// <param name="x"></param>
        ///// <param name="y"></param>
        ///// <param name="z"></param>
        ///// <returns></returns>
        //public static (double x, double y) TileIdToCenterWebMercator(int x, int y, int z)
        //{
        //    double tileCnt = Math.Pow(2, z);
        //    double centerX = x + 0.5d;
        //    double centerY = y + 0.5d;
        //    centerX = ((centerX / tileCnt * 2) - 1) * 20037508.342789244d;
        //    centerY = (1 - (centerY / tileCnt * 2)) * 20037508.342789244d;
        //    return (centerX, centerY);

        //}

        ///// <summary>
        ///// 计算瓦片 经纬度范围 & 中心点经纬度
        ///// </summary>
        ///// <param name="x"></param>
        ///// <param name="y"></param>
        ///// <param name="z"></param>
        ///// <param name="flag"></param>
        ///// <returns></returns>
        ///// <exception cref="ArgumentException"></exception>
        //public static (double lonMin, double latMin, double lonMax, double latMax, double center_lon, double center_lat
        //    )
        //    GetTileRange(int x, int y, int z, string flag = "3857")
        //{
        //    int n = (int)Math.Pow(2, z);
        //    double lonMin, latMax, lonMax, latMin, centerLon, centerLat;

        //    if (flag == "4326")
        //    {
        //        lonMin = (x * 180.0d) / n - 180.0d;
        //        latMax = 90.0d - (y * 180.0d) / n;
        //        lonMax = ((x + 1d) * 180.0d) / n - 180.0d;
        //        latMin = 90.0d - ((y + 1d) * 180.0d) / n;
        //    }
        //    else if (flag == "3857")
        //    {
        //        lonMin = (x / (double)n) * 360.0d - 180.0d;
        //        latMax = (Math.Atan(Math.Sinh(Math.PI * (1d - (2d * y) / (double)n))) * 180.0d) / Math.PI;
        //        lonMax = ((x + 1d) / (double)n) * 360.0d - 180.0d;
        //        latMin = (Math.Atan(Math.Sinh(Math.PI * (1d - (2d * (y + 1d)) / (double)n))) * 180.0d) / Math.PI;
        //    }
        //    else
        //    {
        //        throw new ArgumentException("Invalid flag value");
        //    }

        //    centerLon = (lonMin + lonMax) / 2.0d;
        //    centerLat = (latMin + latMax) / 2.0d;
        //    return (lonMin, latMin, lonMax, latMax, centerLon, centerLat);
        //}

        ///// <summary>
        ///// 计算瓦片尺寸
        ///// </summary>
        ///// <param name="zoomLevel"></param>
        ///// <returns></returns>
        //public static double CalculateTileSize(int zoomLevel)
        //{
        //    // 每像素的地面分辨率（以米为单位）
        //    double resolution = 156543.03 / Math.Pow(2, zoomLevel);

        //    // 瓦片的地面尺寸（以米为单位）
        //    double tileSize = 256 * resolution;

        //    return tileSize;
        //}
        #endregion

        #region Unity
        /// <summary>
        /// 判断顶点是否在 多个多边形内
        /// </summary>
        /// <param name="areas"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static bool IsPointInMultiPolygon(List<List<Vector3>> areas, Vector3 point)
        {
            if (areas == null) return false;
            for (int i = 0; i < areas.Count; i++)
            {
                if (IsPointInPolygon(areas[i], point))
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 判断顶点是否在多边形内
        /// </summary>
        /// <param name="area"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static bool IsPointInPolygon(List<Vector3> area, Vector3 point)
        {
            bool inside = false;
            int j = area.Count - 1;
            for (int i = 0; i < area.Count; j = i++)
            {
                if (((area[i].z <= point.z && point.z < area[j].z) ||
                     (area[j].z <= point.z && point.z < area[i].z)) &&
                    (point.x < (area[j].x - area[i].x) * (point.z - area[i].z) / (area[j].z - area[i].z) + area[i].x))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>
        /// 多边形剔除重复顶点
        /// </summary>
        /// <param name="points"></param>
        public static void CullOverlapPoint(ref List<Vector3> points, float threshold = 0.1f) 
        {
            bool cull = true;
            while (cull)
            {
                int removeIndex = -1;
                for (int i = 0; i < points.Count - 1; i++)
                {
                    Vector3 point1 = points[i];
                    Vector3 point2 = points[i + 1];

                    if (Vector3.Distance(point1, point2) < threshold)
                    {
                        removeIndex = i + 1;
                        break;
                    }
                }

                if (points.Count <= 1)
                {
                    cull = false;
                }
                else
                {
                    if (removeIndex != -1)
                    {
                        points.RemoveAt(removeIndex);
                        cull = true;
                    }
                    else
                    {
                        cull = false;
                    }
                }
            }

            if (points.Count == 3 && Vector3.Distance(points[0], points[2]) < threshold) 
            {
                points.RemoveAt(2);
            }
        }
        /// <summary>
        /// 多边形剔除共线
        /// </summary>
        /// <param name="points"></param>
        public static void CullCollinear(ref List<Vector3> points, float threshold = 2)
        {
            bool cull = true;
            while (cull)
            {
                int removeIndex = -1;
                for (int i = 0; i < points.Count - 2; i++)
                {
                    Vector3 point1 = points[i];
                    Vector3 point2 = points[i + 1];
                    Vector3 point3 = points[i + 2];

                    Vector3 dir1 = (point1 - point2).normalized;
                    Vector3 dir2 = (point3 - point2).normalized;

                    if (Mathf.Abs(GetAngle(dir1, dir2) - 180) < threshold)
                    {
                        removeIndex = i + 1;
                        break;
                    }
                }

                if (points.Count < 3)
                {
                    cull = false;
                }
                else
                {
                    if (removeIndex != -1)
                    {
                        points.RemoveAt(removeIndex);
                        cull = true;
                    }
                    else
                    {
                        cull = false;
                    }
                }
            }
        }
        /// <summary>
        /// 计算向量夹角
        /// </summary>
        /// <param name="vector1"></param>
        /// <param name="vector2"></param>
        /// <returns></returns>
        public static float GetAngle(Vector3 vector1, Vector3 vector2)
        {
            float angle = Vector3.Angle(vector1, vector2);
            if (angle < 0)
            {
                angle += 360;
            }
            return angle;
        }
        #endregion

        /// <summary>
        /// 获取两个向量的交点
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="d"></param>
        /// <param name="intersectPos"></param>
        /// <returns></returns>
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
    }
}

