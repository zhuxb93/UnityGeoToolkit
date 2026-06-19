using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace GeoToolkit.Smooth
{
    /// <summary>
    /// 地形平滑入口类
    /// </summary>
    public static class TerrainSmoothEntry
    {
        /// <summary>
        /// 执行跨地形平滑
        /// </summary>
        /// <param name="terrains">要平滑的地形列表</param>
        /// <param name="smoothRadius">平滑半径</param>
        /// <param name="smoothIterations">平滑迭代次数</param>
        /// <param name="smoothStrength">平滑强度</param>
        /// <param name="maxBatchSize">最大批次大小</param>
        /// <returns>处理的地形数量</returns>
        public static async Task<int> SmoothTerrainsAsync(List<Terrain> terrains, 
            int smoothRadius = 3, int smoothIterations = 1, float smoothStrength = 0.5f, int maxBatchSize = 64)
        {
            var processor = new TerrainSmoothProcessor(smoothRadius, smoothIterations, smoothStrength, maxBatchSize);
            return await processor.SmoothTerrainsAsync(terrains);
        }

        /// <summary>
        /// 收集Transform下的所有地形
        /// </summary>
        /// <param name="parent">父对象</param>
        /// <returns>地形列表</returns>
        public static List<Terrain> CollectTerrains(Transform parent)
        {
            var terrains = new List<Terrain>();
            CollectTerrainsRecursively(parent, terrains);
            return terrains;
        }

        /// <summary>
        /// 递归收集地形
        /// </summary>
        private static void CollectTerrainsRecursively(Transform parent, List<Terrain> terrains)
        {
            if (parent == null) return;

            // 检查当前对象是否有地形组件
            Terrain terrain = parent.GetComponent<Terrain>();
            if (terrain != null && terrain.terrainData != null)
            {
                terrains.Add(terrain);
            }

            // 递归检查子对象
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                CollectTerrainsRecursively(child, terrains);
            }
        }
    }
}