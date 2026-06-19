using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Collections;

namespace GeoToolkit.Smooth
{
    /// <summary>
    /// 地形平滑工具类
    /// </summary>
    public static class TerrainSmoothUtils
    {
        /// <summary>
        /// 统一所有地形的最大高度范围
        /// </summary>
        public static async Task UnifyTerrainHeightRangesAsync(List<Terrain> terrains)
        {
            float maxHeight = 0f;
            foreach (var terrain in terrains)
            {
                if (terrain?.terrainData != null)
                {
                    maxHeight = Mathf.Max(maxHeight, terrain.terrainData.size.y);
                }
            }

            if (maxHeight <= 0f) return;

            foreach (var terrain in terrains)
            {
                if (terrain?.terrainData == null) continue;

                float currentHeight = terrain.terrainData.size.y;
                if (Mathf.Abs(currentHeight - maxHeight) > 0.01f)
                {
                    await AdjustTerrainHeightRange(terrain, currentHeight, maxHeight);
                }
            }
        }

        /// <summary>
        /// 调整单个地形的高度范围
        /// </summary>
        private static async Task AdjustTerrainHeightRange(Terrain terrain, float oldMaxHeight, float newMaxHeight)
        {
            var terrainData = terrain.terrainData;
            int resolution = terrainData.heightmapResolution;
            
            float[,] heights = terrainData.GetHeights(0, 0, resolution, resolution);
            
            await Task.Run(() =>
            {
                for (int y = 0; y < resolution; y++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        float worldHeight = heights[y, x] * oldMaxHeight;
                        float newNormalizedHeight = worldHeight / newMaxHeight;
                        heights[y, x] = Mathf.Clamp01(newNormalizedHeight);
                    }
                }
            });
            
            var size = terrainData.size;
            size.y = newMaxHeight;
            terrainData.size = size;
            terrainData.SetHeights(0, 0, heights);
        }

        /// <summary>
        /// 建立地形空间网格
        /// </summary>
        public static TerrainGrid BuildTerrainGrid(List<Terrain> terrains)
        {
            if (terrains == null || terrains.Count == 0) return null;

            var firstTerrain = terrains[0];
            if (firstTerrain?.terrainData == null) return null;

            var terrainSize = firstTerrain.terrainData.size;
            var terrainResolution = firstTerrain.terrainData.heightmapResolution;

            // 找到最小位置
            Vector3 minPos = firstTerrain.transform.position;
            foreach (var terrain in terrains)
            {
                if (terrain != null)
                {
                    Vector3 pos = terrain.transform.position;
                    minPos.x = Mathf.Min(minPos.x, pos.x);
                    minPos.z = Mathf.Min(minPos.z, pos.z);
                }
            }

            // 计算网格尺寸
            Vector3 maxPos = minPos;
            foreach (var terrain in terrains)
            {
                if (terrain != null)
                {
                    Vector3 pos = terrain.transform.position;
                    maxPos.x = Mathf.Max(maxPos.x, pos.x);
                    maxPos.z = Mathf.Max(maxPos.z, pos.z);
                }
            }

            int gridWidth = Mathf.RoundToInt((maxPos.x - minPos.x) / terrainSize.x) + 1;
            int gridHeight = Mathf.RoundToInt((maxPos.z - minPos.z) / terrainSize.z) + 1;

            var grid = new TerrainGrid
            {
                gridWidth = gridWidth,
                gridHeight = gridHeight,
                minPosition = minPos,
                terrainSize = terrainSize,
                terrainResolution = terrainResolution,
                terrains = new Terrain[gridWidth, gridHeight]
            };

            // 填充网格
            foreach (var terrain in terrains)
            {
                if (terrain != null)
                {
                    Vector3 pos = terrain.transform.position;
                    int gridX = Mathf.RoundToInt((pos.x - minPos.x) / terrainSize.x);
                    int gridZ = Mathf.RoundToInt((pos.z - minPos.z) / terrainSize.z);

                    if (gridX >= 0 && gridX < gridWidth && gridZ >= 0 && gridZ < gridHeight)
                    {
                        grid.terrains[gridX, gridZ] = terrain;
                    }
                }
            }

            return grid;
        }

        /// <summary>
        /// 创建空间批次（相邻的N个瓦片）
        /// </summary>
        public static List<SpatialBatch> CreateSpatialBatches(TerrainGrid grid, int maxBatchSize = 64)
        {
            var batches = new List<SpatialBatch>();
            if (grid == null) return batches;

            // 确定批次大小（比如3x3）
            int batchGridSize = Mathf.FloorToInt(Mathf.Sqrt(maxBatchSize));
            
            // 重叠步长：每个批次重叠1行/列，所以步长是batchGridSize-1
            int stepSize = Math.Max(1, batchGridSize - 1);
            
            for (int startZ = 0; startZ < grid.gridHeight; startZ += stepSize)
            {
                for (int startX = 0; startX < grid.gridWidth; startX += stepSize)
                {
                    // 计算实际批次大小，确保不超出边界
                    int actualBatchWidth = Math.Min(batchGridSize, grid.gridWidth - startX);
                    int actualBatchHeight = Math.Min(batchGridSize, grid.gridHeight - startZ);
                    
                    // 跳过太小的批次
                    if (actualBatchWidth <= 0 || actualBatchHeight <= 0) continue;
                    
                    var batch = new SpatialBatch
                    {
                        startGridX = startX,
                        startGridZ = startZ,
                        batchGridSize = Math.Min(actualBatchWidth, actualBatchHeight), // 保持正方形
                        terrains = new List<Terrain>(),
                        grid = grid
                    };

                    // 收集这个批次中的所有地形（包括重叠区域）
                    for (int z = startZ; z < startZ + batch.batchGridSize && z < grid.gridHeight; z++)
                    {
                        for (int x = startX; x < startX + batch.batchGridSize && x < grid.gridWidth; x++)
                        {
                            var terrain = grid.terrains[x, z];
                            if (terrain != null)
                            {
                                batch.terrains.Add(terrain);
                            }
                        }
                    }

                    if (batch.terrains.Count > 0)
                    {
                        batches.Add(batch);
                    }
                }
            }

            return batches;
        }

        /// <summary>
        /// 创建地形ID到名称的映射
        /// </summary>
        public static Dictionary<Terrain, string> CreateTerrainNameMapping(List<Terrain> terrains)
        {
            var mapping = new Dictionary<Terrain, string>();
            foreach (var terrain in terrains)
            {
                if (terrain != null)
                {
                    mapping[terrain] = terrain.name;
                }
            }
            return mapping;
        }

        /// <summary>
        /// 创建地形ID到尺寸的映射
        /// </summary>
        public static Dictionary<Terrain, Vector3> CreateTerrainSizeMapping(List<Terrain> terrains)
        {
            var mapping = new Dictionary<Terrain, Vector3>();
            foreach (var terrain in terrains)
            {
                if (terrain?.terrainData != null)
                {
                    mapping[terrain] = terrain.terrainData.size;
                }
            }
            return mapping;
        }

        /// <summary>
        /// 清理NativeArray资源
        /// </summary>
        public static void DisposeBigArray(BigArray bigArray)
        {
            if (bigArray?.heightData.IsCreated == true)
            {
                bigArray.heightData.Dispose();
            }
        }
    }
}