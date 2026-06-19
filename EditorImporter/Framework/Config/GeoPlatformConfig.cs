using System.Collections.Generic;
using UnityEngine;

namespace GeoToolkit
{
    [CreateAssetMenu(fileName = "GeoPlatformConfig", menuName = "GeoToolkit/Config")]
    public class GeoPlatformConfig : ScriptableObject
    {
        /// <summary>
        /// 是否更新中心点
        /// </summary>
        public bool isUpdateCenter = true;

        public float AddtionHeight = 50;

        public int TileLevel;

        public double CenterLongitude;

        public double CenterLatitude;

        public TerrainSize TerrainSize = TerrainSize.Terrain_512;

        public float Scale;

        public bool IsLoadRoad = true;

        public bool IsLoadHyda = true;

        public bool IsLoadVega = true;

        public GameObject OceanObject;

        public GameObject WindyObject;

        [Header("绿地树对象集合")]
        public List<GameObject> TreesObjects = new List<GameObject>();

        [Header("绿地草对象集合")]
        public List<GameObject> GrassObjects = new List<GameObject>();
        /// <summary>
        /// 初始化中心点
        /// </summary>
        public void Initialize()
        {
            AddtionHeight = 50;
            isUpdateCenter = true;
            TileLevel = 0;
            CenterLongitude = 0;
            CenterLatitude = 0;
            IsLoadRoad = true;
            IsLoadHyda = true;
            IsLoadVega = true;
            TerrainSize = TerrainSize.Terrain_512;

            //TreesObjects = GetTreesGo();
            //GrassObjects = GetGrassGo();
            //OceanObject = GetOceanGo();
            //WindyObject = GetWindyGo();

        }

        public float GetScale()
        {
            var Scale = (float)Conversions.CalculateTileSize(TileLevel);
            float distanceRatio = (int)TerrainSize / Scale;
            return distanceRatio;
        }

    }


    public enum TerrainSize : int
    {
        Terrain_4096 = 4096,
        Terrain_2048 = 2048,
        Terrain_1024 = 1024,
        Terrain_512 = 512,
        Terrain_256 = 256,
        Terrain_128 = 128,
        Terrain_64 = 64,
        Terrain_32 = 32
    }
}