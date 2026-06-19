using System.Collections.Generic;
using UnityEngine;

namespace GeoToolkit.SeamFix
{
    /// <summary>
    /// 地形工具集合类
    /// </summary>
    public static class TerrainUtils
    {
        /// <summary>
        /// 递归收集所有地形组件
        /// </summary>
        public static void CollectTerrainsRecursively(Transform parent, List<Terrain> terrains)
        {
            Terrain terrain = parent.GetComponent<Terrain>();
            if (terrain != null)
            {
                terrains.Add(terrain);
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                CollectTerrainsRecursively(parent.GetChild(i), terrains);
            }
        }

        /// <summary>
        /// 获取地形信息字符串
        /// </summary>
        public static string GetTerrainInfo(Transform transform)
        {
            Terrain terrain = transform.GetComponent<Terrain>();
            if (terrain == null || terrain.terrainData == null)
                return string.Empty;

            Vector3 size = terrain.terrainData.size;
            int res = terrain.terrainData.heightmapResolution;
            return $"地形大小: {size.x:F1} x {size.z:F1} x {size.y:F1}, 高度图分辨率: {res} x {res}";
        }

        /// <summary>
        /// 验证地形对象是否有效
        /// </summary>
        public static bool IsValidTerrain(Terrain terrain)
        {
            return terrain != null && terrain.terrainData != null;
        }

        /// <summary>
        /// 获取地形瓦片数量统计
        /// </summary>
        public static (int validCount, int invalidCount) GetTerrainStats(GameObject rootObject)
        {
            if (rootObject == null) return (0, 0);

            var terrains = new List<Terrain>();
            CollectTerrainsRecursively(rootObject.transform, terrains);

            int validCount = 0;
            int invalidCount = 0;

            foreach (var terrain in terrains)
            {
                if (IsValidTerrain(terrain))
                    validCount++;
                else
                    invalidCount++;
            }

            return (validCount, invalidCount);
        }
    }
}