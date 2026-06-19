using Unity.Mathematics;
using UnityEngine;

namespace GeoToolkit
{
    public class GeoCoordinateUtils
    {
        private static GeoPlatformConfig sdkConfig;
        //private const double EarthRadius = 6378137.0; // 地球半径（单位：米）

        private static double distanceRatio = 1;

        /// <summary>
        /// 初始化或更新设置
        /// </summary>
        public static void Initialize(GeoPlatformConfig config)
        {
            if (config == null)
            {
                Debug.LogError("提供的设置不能为null");
                return;
            }

            sdkConfig = config;
            distanceRatio = CalculateDistanceRatio((int)sdkConfig.TerrainSize, sdkConfig.TileLevel);
        }

        #region 计算比例尺

        /// <summary>
        /// 根据尺寸和级别计算比例尺
        /// </summary>
        /// <param name="size"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        public static double CalculateDistanceRatio(float size, int level)
        {
            var Scale = Conversions.CalculateTileSize(level);
            return size / Scale;
        }

        #endregion


        #region 经纬度转世界坐标 为什么要有这么多重载啊

        /// <summary>
        /// 经纬度转换为 Unity 世界坐标
        /// </summary>
        public static Vector3 LatLonToWorld(double2 latLon)
        {
            if (sdkConfig == null)
            {
                Debug.LogError("GeoCoordinateUtils 未初始化或缺少中心点信息");
                return Vector3.zero;
            }
            var CenterMeter = Conversions.LatLonToMeters(sdkConfig.CenterLatitude, sdkConfig.CenterLongitude);
            var PosMeter = Conversions.LatLonToMeters(latLon.y, latLon.x);
            // TODO: 这个distanceRatio应该存起来，之后坐标转换会用到，表示的是数据中的1个单位对应多少米
            //这个跟数据有关，比如geo的terrain使用1024*1024个unity单位，贴了一张15级的影像&地形，就用下面的参数去算出这个值
            //这个值应该让用户能调，比如用户不用咱们的terrain，他自己有一些奇葩数据，那他要告诉我们他的数据跟米的比例关系，我们才能把地理坐标转换到他的数据上
            //这个值用户应该在构建场景的时候就确定下来，运行时最好不要更改，改了没有意义
            //var Scale = Conversions.CalculateTileSize(CenterTile.z);
            //var distanceRatio = sdkConfig.TerrainSize / Scale;

            var PosWorld = (PosMeter - CenterMeter) * distanceRatio; //把米换算成unity单位
            return new Vector3((float)PosWorld.x, 0, (float)PosWorld.y);
        }

        public static Vector3 LatLonToWorld(double3 latLon)
        {
            if (sdkConfig == null)
            {
                Debug.LogError("GeoCoordinateUtils 未初始化或缺少中心点信息");
                return Vector3.zero;
            }
            var CenterMeter = Conversions.LatLonToMeters(sdkConfig.CenterLatitude, sdkConfig.CenterLongitude);
            Vector2d PosMeter = Conversions.LatLonToMeters(latLon.y, latLon.x);

            var PosWorld = (PosMeter - CenterMeter) * distanceRatio; //把米换算成unity单位
            return new Vector3((float)PosWorld.x, (float)(latLon.z * distanceRatio + sdkConfig.AddtionHeight), (float)PosWorld.y);
        }

        public static Vector3 LatLonToWorld(double3 latLon, double inputDistanceRatio)
        {
            if (sdkConfig == null)
            {
                Debug.LogError("GeoCoordinateUtils 未初始化或缺少中心点信息");
                return Vector3.zero;
            }
            var CenterMeter = Conversions.LatLonToMeters(sdkConfig.CenterLatitude, sdkConfig.CenterLongitude);
            Vector2d PosMeter = Conversions.LatLonToMeters(latLon.y, latLon.x);

            var PosWorld = (PosMeter - CenterMeter) * inputDistanceRatio; //把米换算成unity单位
            return new Vector3((float)PosWorld.x, (float)(latLon.z * inputDistanceRatio), (float)PosWorld.y);
        }

        /// <summary>
        /// 获取给定经纬高对应的世界坐标，如果高度为0，则从该点向下发射射线获取地形高度。
        /// </summary>
        public static Vector3 GetWorldPositionWithRaycast(double2 centerLonLat, double3 latLon, bool isRaycast)
        {
            Vector2d CenterMeter = Conversions.LatLonToMeters(centerLonLat.y, centerLonLat.x);
            Vector2d PosMeter = Conversions.LatLonToMeters(latLon.y, latLon.x);

            Vector2d PosWorld = (PosMeter - CenterMeter) * distanceRatio;
            Vector3 position = new Vector3((float)PosWorld.x, (float)(latLon.z), (float)PosWorld.y);

            if (isRaycast)
            {
                position = GetRayPosition(position);
            }
            else
            {
                position.y = position.y * (float)distanceRatio + sdkConfig.AddtionHeight;
            }

            return position;
        }
        #endregion

        /// <summary>
        /// 通过射线检测位置
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public static Vector3 GetRayPosition(Vector3 position)
        {
            // 直接使用 Layer 10 进行射线检测，不再检查名称是否为空
            // 因为即使 Layer 名称未配置，Layer 编号仍然有效
            int terrainLayer = 10;
            Ray ray = new Ray(position + Vector3.up * 10000f, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, 1 << terrainLayer))
            {
                return hit.point;
            }

            // 如果射线没有命中，尝试通过 Terrain.activeTerrains 获取高度作为回退
            foreach (var terrain in Terrain.activeTerrains)
            {
                Vector3 tPos = terrain.transform.position;
                Vector3 tSize = terrain.terrainData.size;

                // 判断 position 是否在该 Terrain 范围内
                if (position.x >= tPos.x && position.x <= tPos.x + tSize.x &&
                    position.z >= tPos.z && position.z <= tPos.z + tSize.z)
                {
                    float y = terrain.SampleHeight(position) + tPos.y;
                    return new Vector3(position.x, y, position.z);
                }
            }

            return position;
        }

        /// <summary>
        /// 通过射线采集Terrain上的位置（WorldPosition）
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public static Vector3 GetRayPositionForTerrain(Vector3 position)
        {
            int layer = 10; // 你的 Terrain Layer

            // 射线检测
            Ray ray = new Ray(position + Vector3.up * 10000f, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, 1 << layer))
            {
                return hit.point;
            }

            // 如果射线没有命中，尝试通过 Terrain.activeTerrains 获取高度
            foreach (var terrain in Terrain.activeTerrains)
            {
                Vector3 tPos = terrain.transform.position;
                Vector3 tSize = terrain.terrainData.size;

                // 判断 position 是否在该 Terrain 范围内
                if (position.x >= tPos.x && position.x <= tPos.x + tSize.x &&
                    position.z >= tPos.z && position.z <= tPos.z + tSize.z)
                {
                    float y = terrain.SampleHeight(position) + tPos.y;
                    return new Vector3(position.x, y, position.z);
                }
            }

            // 如果仍然没有命中任何 Terrain，则返回原始位置
            return position;
        }



        #region 世界坐标转经纬度

        /// <summary>
        /// 世界坐标轴换经纬度
        /// </summary>
        /// <param name="worldPos"></param>
        /// <returns></returns>
        public static double3 WorldToLatLon(Vector3 worldPos)
        {
            if (sdkConfig == null)
            {
                Debug.LogError("GeoCoordinateUtils 未初始化或缺少中心点信息");
                return new double3(0, 0, 0);
            }

            // 获取中心点信息
            var CenterMeter = Conversions.LatLonToMeters(sdkConfig.CenterLatitude, sdkConfig.CenterLongitude);

            // 计算比例尺
            var Scale = Conversions.CalculateTileSize(sdkConfig.TileLevel);
            var distanceRatio = (int)sdkConfig.TerrainSize / Scale;

            // 将世界坐标转换回米制坐标
            Vector2d posMeter = new Vector2d(
                worldPos.x / distanceRatio + CenterMeter.x,
                worldPos.z / distanceRatio + CenterMeter.y
            );

            // 将米制坐标转换回经纬度
            var latLon = Conversions.MetersToLatLon(posMeter);

            // 高度值直接使用世界坐标的y值（或根据需要进行比例转换）
            double altitude = worldPos.y; // 如果之前有乘以distanceRatio，这里需要除以

            return new double3(latLon.x, latLon.y, altitude);
        }

        /// <summary>
        /// 世界坐标转经纬度
        /// </summary>
        /// <param name="worldPos"></param>
        /// <param name="inputDistanceRatio"></param>
        /// <returns></returns>
        public static double3 WorldToLatLon(Vector3 worldPos, double inputDistanceRatio)
        {
            if (sdkConfig == null)
            {
                Debug.LogError("GeoCoordinateUtils 未初始化或缺少中心点信息");
                return new double3(0, 0, 0);
            }

            var CenterMeter = Conversions.LatLonToMeters(sdkConfig.CenterLatitude, sdkConfig.CenterLongitude);

            // 将世界坐标转换回米制坐标
            Vector2d posMeter = new Vector2d(
                worldPos.x / inputDistanceRatio + CenterMeter.x,
                worldPos.z / inputDistanceRatio + CenterMeter.y
            );

            // 将米制坐标转换回经纬度
            var latLon = Conversions.MetersToLatLon(posMeter);

            // 高度值直接使用世界坐标的y值（或根据需要进行比例转换）
            double altitude = worldPos.y; // 如果之前有乘以distanceRatio，这里需要除以

            return new double3(latLon.x, latLon.y, altitude);
        }

        #endregion



        /// <summary>
        /// 计算Terrain在场景中摆放的位置，以左下角为基准点
        /// </summary>
        /// <param name="tileId"></param>
        /// <param name="tileSize"></param>
        /// <returns></returns>
        public static Vector3 CalcTileLocalPos(TileID tileId, TerrainSize tileSize = TerrainSize.Terrain_1024)
        {
            if (sdkConfig == null)
            {
                Debug.LogError("GeoCoordinateUtils 未初始化或缺少中心点信息");
                return Vector3.zero;
            }
            double2 inputPos = TileProcessor.Get3857TileLeftBottomLonLat(tileId);
            Vector3 inputWorld = LatLonToWorld(inputPos);
            return inputWorld;
        }

    }

}
