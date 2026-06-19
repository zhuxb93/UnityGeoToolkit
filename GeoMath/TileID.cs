using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GeoToolkit
{
    [Serializable]
    public class TileID : IEquatable<TileID>
    {
        [JsonProperty(Order = 2)]
        public int x;

        [JsonProperty(Order = 3)]
        public int y;

        [JsonProperty(Order = 1)]
        public int z;

        public TileID(int X, int Y, int Z)
        {
            x = X;
            y = Y;
            z = Z;
        }

        public TileID()
        {
        }

        public int invY()
        {
            return (1 << z) - y - 1; // 原本的 (2 << (z - 1)) 等价于 (1 << z)
        }

        public override string ToString()
        {
            return $"{z}-{x}-{y}";
        }

        // 关键：实现 Equals 和 GetHashCode
        public override bool Equals(object obj)
        {
            return Equals(obj as TileID);
        }

        public bool Equals(TileID other)
        {
            if (other is null) return false;
            return x == other.x && y == other.y && z == other.z;
        }

        public override int GetHashCode()
        {
            // 推荐哈希算法：避免冲突
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + x;
                hash = hash * 31 + y;
                hash = hash * 31 + z;
                return hash;
            }
        }

        public static bool operator ==(TileID left, TileID right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(TileID left, TileID right)
        {
            return !(left == right);
        }
    }


    [System.Serializable]
    public class TerrainJson
    {
        public List<double[]> points { get; set; }
        public List<int[]> triangles { get; set; }
    }

    public enum PlatformSDKEnum
    {
        Terrain,
        Geojson
    }

}
