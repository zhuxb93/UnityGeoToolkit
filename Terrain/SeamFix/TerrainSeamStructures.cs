using UnityEngine;

namespace GeoToolkit.SeamFix
{
    /// <summary>
    /// 地形缝隙信息结构
    /// </summary>
    public class TerrainSeamInfo
    {
        public Terrain terrain1;
        public Terrain terrain2;
        public SeamType seamType;
        public float maxHeightDifference;
    }

    /// <summary>
    /// 地形角点信息结构
    /// </summary>
    public class TerrainCornerInfo
    {
        public Terrain terrain;
        public Vector3 worldPosition;
        public int heightmapX;
        public int heightmapZ;
    }

    /// <summary>
    /// 地形数据信息结构（线程安全）
    /// </summary>
    public class TerrainDataInfo
    {
        public Terrain terrain;
        public Vector3 position;
        public Vector3 size;
        public int heightmapResolution;
        public float terrainHeight;
        public float[,] heights;
    }

    /// <summary>
    /// 角点数据结构（线程安全）
    /// </summary>
    public class CornerData
    {
        public TerrainCornerInfo corner;
        public float originalHeight;
        public float[,] terrainHeights;
    }

    /// <summary>
    /// 瓦片信息结构
    /// </summary>
    public class TerrainTileInfo
    {
        public Terrain terrain;
        public int z; // 缩放级别
        public int x; // X坐标
        public int y; // Y坐标
        public Vector3 position;
        public Vector3 size;
        public int heightmapResolution;
        public float terrainHeight;
        public string terrainName; // 预存储地形名称，避免跨线程访问
        
        public string TileKey => $"{z}_{x}_{y}";
    }

    /// <summary>
    /// 缝隙类型枚举
    /// </summary>
    public enum SeamType
    {
        None,
        RightLeft,   // terrain1右边与terrain2左边相邻
        LeftRight,   // terrain1左边与terrain2右边相邻
        BottomTop,   // terrain1下边与terrain2上边相邻
        TopBottom,   // terrain1上边与terrain2下边相邻
        Corner       // 角点缝隙
    }
}