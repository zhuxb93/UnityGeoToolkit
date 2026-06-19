using System;
using UnityEngine;

namespace Unity3DTiles
{
    public class CoordinateConversion
    {
        // WGS84椭球参数
        private const double a = 6378137.0;                   // 长半轴
        private const double f = 1.0 / 298.257223563;         // 扁率
        private const double b = a * (1 - f);                 // 短半轴
        private const double e2 = 2 * f - f * f;              // 第一偏心率平方
        private const double ep2 = (a * a - b * b) / (b * b); // 第二偏心率平方
        private const double OriginShift = 2 * Math.PI * a / 2;

        public static double centerLongitude = 0;
        public static double centerLatitude = 0;
        public static float addtionHeight = 50;
        public static double tileScale = 1;
        public static bool enabled = false;

        private static double centerMeterX = 0;
        private static double centerMeterZ = 0;

        public static void Initialize(GeoToolkit.GeoPlatformConfig platformConfig, float extraAddtionHeight = 0)
        {
            if (platformConfig != null)
            {
                centerLongitude = platformConfig.CenterLongitude;
                centerLatitude = platformConfig.CenterLatitude;
                addtionHeight = platformConfig.AddtionHeight;
                tileScale = (double)platformConfig.TerrainSize / CalculateTileSize(platformConfig.TileLevel);
                addtionHeight += extraAddtionHeight;
                enabled = true;
            }
            else
            {
                enabled = false;
                return;
            }
            double[] mers = LatLonToMeters(centerLatitude, centerLongitude);
            centerMeterX = mers[0];
            centerMeterZ = mers[1];
        }
        /// <summary>
        /// 将ECEF坐标转成unity相对原点坐标的局部坐标
        /// </summary>
        /// <param name="position"></param>
        /// <param name="originLongitude"></param>
        /// <param name="originLatitude"></param>
        /// <returns></returns>
        public static Vector3 ConvertECEFToUnityLocalPosition(Vector3 position, double originLongitude, double originLatitude)
        {
            if (!enabled) throw new Exception("No platform config provided");
            double[] blh = ECEFToLLA(position.x, position.y, position.z);
            double deltaLon = blh[1] - originLongitude;
            double deltaLat = blh[0] - originLatitude;
            //const double METERS_PER_DEGREE = 111319.488;
            (double latMeters, double lonMeters) = MetersPerDegreeAdvanced(originLatitude);
            double x = deltaLon * lonMeters;
            double z = deltaLat * latMeters; // Z轴
            return new Vector3((float)x, (float)blh[2], (float)z);
        }

        public static (double latMeters, double lonMeters) MetersPerDegreeAdvanced(double latitude)
        {
            if (!enabled) throw new Exception("No platform config provided");
            // 计算椭球偏心率平方
            double eSquared = 1 - (b * b) / (a * a);

            // 纬度转弧度
            double latRad = latitude * Math.PI / 180.0;
            double sinLat = Math.Sin(latRad);

            // 公共分母计算
            double denominator = 1 - eSquared * sinLat * sinLat;

            // 计算子午圈曲率半径（纬度方向）
            double M = a * (1 - eSquared) / Math.Pow(denominator, 1.5);
            double latMeters = M * (Math.PI / 180.0);

            // 计算卯酉圈曲率半径（经度方向）
            double N = a / Math.Sqrt(denominator);
            double lonMeters = N * Math.Cos(latRad) * (Math.PI / 180.0);

            return (latMeters, lonMeters);
        }

        /// <summary>
        /// 将经纬高转换为ECEF坐标
        /// </summary>
        /// <param name="lat">纬度（度）</param>
        /// <param name="lon">经度（度）</param>
        /// <param name="height">高程（米）</param>
        /// <param name="x">输出X坐标</param>
        /// <param name="y">输出Y坐标</param>
        /// <param name="z">输出Z坐标</param>
        public static double[] LLAToECEF(double lat, double lon, double height)
        {
            if (!enabled) throw new Exception("No platform config provided");
            double latRad = DegreesToRadians(lat);
            double lonRad = DegreesToRadians(lon);

            double sinLat = Math.Sin(latRad);
            double N = a / Math.Sqrt(1 - e2 * sinLat * sinLat);

            double x = (N + height) * Math.Cos(latRad) * Math.Cos(lonRad);
            double y = (N + height) * Math.Cos(latRad) * Math.Sin(lonRad);
            double z = (N * (1 - e2) + height) * sinLat;
            return new double[] { x, y, z };
        }

        /// <summary>
        /// 将ECEF坐标转换为经纬高
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="z">Z坐标</param>
        /// <param name="lat">输出纬度（度）</param>
        /// <param name="lon">输出经度（度）</param>
        /// <param name="height">输出高程（米）</param>
        public static double[] ECEFToLLA(double x, double y, double z)
        {
            if (!enabled) throw new Exception("No platform config provided");
            // 经度计算（直接解析解）
            double lon = Math.Atan2(y, x);

            // 迭代计算纬度和高度
            double p = Math.Sqrt(x * x + y * y);
            double theta = Math.Atan2(z * a, p * b);

            double sinTheta = Math.Sin(theta);
            double cosTheta = Math.Cos(theta);

            double lat = Math.Atan2(z + ep2 * b * Math.Pow(sinTheta, 3), p - e2 * a * Math.Pow(cosTheta, 3));

            double sinLat = Math.Sin(lat);
            double N = a / Math.Sqrt(1 - e2 * sinLat * sinLat);
            double height = p / Math.Cos(lat) - N;

            // 高精度迭代（通常2-3次即可收敛）
            for (int i = 0; i < 2; i++)
            {
                sinLat = Math.Sin(lat);
                N = a / Math.Sqrt(1 - e2 * sinLat * sinLat);
                height = p / Math.Cos(lat) - N;
                double newLat = Math.Atan2(z, p * (1 - e2 * N / (N + height)));
                if (Math.Abs(newLat - lat) < 1e-15) break;
                lat = newLat;
            }

            // 转换为角度
            lat = RadiansToDegrees(lat);
            lon = RadiansToDegrees(lon);

            return new double[] { lat, lon, height };
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        private static double RadiansToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }
        public static double CalculateTileSize(int zoomLevel)
        {
            // 每像素的地面分辨率（以米为单位）
            double resolution = 156543.03 / Math.Pow(2, zoomLevel);

            // 瓦片的地面尺寸（以米为单位）
            double tileSize = 256 * resolution;

            return tileSize;
        }
        public static Vector3 LatLonHeightToWorldPosition(double lat, double lon, double height)
        {
            if (!enabled) throw new Exception("No platform config provided");
            double[] mers = LatLonToMeters(lat, lon);
            Vector3 c_pos = new Vector3((float)(mers[0] - centerMeterX), (float)height, (float)(mers[1] - centerMeterZ));
            Vector3 s_pos = c_pos * (float)tileScale;
            return s_pos + Vector3.up * addtionHeight;
        }

        public static double[] LatLonToMeters(double lat, double lon)
        {
            if (!enabled) throw new Exception("No platform config provided");
            var posx = lon * OriginShift / 180;
            var posy = Math.Log(Math.Tan((90 + lat) * Math.PI / 360)) / (Math.PI / 180);
            posy = posy * OriginShift / 180;
            return new double[2] { posx, posy };
        }

        /// <summary>
        /// 获取 ENU 旋转矩阵（从 ECEF 到 ENU）
        /// </summary>
        public static Matrix4x4d GetENURotationMatrix(double latRad, double lonRad)
        {
            if (!enabled) throw new Exception("No platform config provided");
            double sinLat = System.Math.Sin(latRad * Mathf.Deg2Rad);
            double cosLat = System.Math.Cos(latRad * Mathf.Deg2Rad);
            double sinLon = System.Math.Sin(lonRad * Mathf.Deg2Rad);
            double cosLon = System.Math.Cos(lonRad * Mathf.Deg2Rad);

            // ENU 旋转矩阵（3x3）
            // 行：东、北、天
            // 列：X、Y、Z（ECEF）
            Matrix4x4d m = new Matrix4x4d();

            m.SetRow(0, new Vector4d(-sinLon, cosLon, 0, 0)); // 东
            m.SetRow(1, new Vector4d(cosLat * cosLon, cosLat * sinLon, sinLat, 0)); // 天
            m.SetRow(2, new Vector4d(-sinLat * cosLon, -sinLat * sinLon, cosLat, 0)); // 北
            m.SetRow(3, new Vector4d(0, 0, 0, 1));

            return m;
        }

        public static Matrix4x4d GetENURotationMatrix2(double latRad, double lonRad)
        {
            if (!enabled) throw new Exception("No platform config provided");
            double sinLat = System.Math.Sin(latRad * Mathf.Deg2Rad);
            double cosLat = System.Math.Cos(latRad * Mathf.Deg2Rad);
            double sinLon = System.Math.Sin(lonRad * Mathf.Deg2Rad);
            double cosLon = System.Math.Cos(lonRad * Mathf.Deg2Rad);

            // ENU 旋转矩阵（3x3）
            // 行：东、北、天
            // 列：X、Y、Z（ECEF）
            Matrix4x4d m = new Matrix4x4d();

            m.SetRow(0, new Vector4d(-sinLon, cosLon, 0, 0)); // 东
            m.SetRow(1, new Vector4d(-sinLat * cosLon, -sinLat * sinLon, cosLat, 0)); // 北
            m.SetRow(2, new Vector4d(cosLat * cosLon, cosLat * sinLon, sinLat, 0)); // 天
            m.SetRow(3, new Vector4d(0, 0, 0, 1));

            return m;
        }

        public static Matrix4x4 GetTileTransformMatrix(Matrix4x4d rootTransform)
        {
            if (!enabled) throw new Exception("No platform config provided");

            // 计算实际enu位置
            double[] llh = ECEFToLLA(rootTransform.m03, rootTransform.m13, rootTransform.m23);
            Vector3 position = LatLonHeightToWorldPosition(llh[0], llh[1], llh[2]);
            Matrix4x4d enuRotationMatrix = GetENURotationMatrix2(llh[0], llh[1]);
            Quaternion rotation = enuRotationMatrix.GetRotation() * rootTransform.GetRotation();
            Vector3 scale = new Vector3((float)rootTransform.lossyScale.x, (float)rootTransform.lossyScale.y, (float)rootTransform.lossyScale.z);
            scale *= (float)tileScale;
            Matrix4x4 tileTransform = Matrix4x4.TRS(position, rotation, scale);
            Matrix4x4 ECEFtoTile = tileTransform * Matrix4x4d.ToMatrix4x4(rootTransform).inverse;
            return ECEFtoTile;
        }
    }
}
