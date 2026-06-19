using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

namespace GeoToolkit.Smooth
{
    /// <summary>
    /// 地形平滑处理器 - 主要的平滑逻辑
    /// </summary>
    public class TerrainSmoothProcessor
    {
        private readonly int smoothRadius;
        private readonly int smoothIterations;
        private readonly float smoothStrength;
        private readonly int maxBatchSize;
        private readonly bool enableMinHeightSkip;
        private readonly float minHeightThreshold;
        private readonly bool enableMaxHeightSkip;
        private readonly float maxHeightThreshold;
        
        // 用于跟踪当前处理状态
        private TerrainGrid currentGrid;
        private HashSet<Terrain> processedTerrains;

        public TerrainSmoothProcessor(int smoothRadius = 3, int smoothIterations = 1, float smoothStrength = 0.5f,
            int maxBatchSize = 64, bool enableMinHeightSkip = false, float minHeightThreshold = 0f,
            bool enableMaxHeightSkip = false, float maxHeightThreshold = 1000f)
        {
            this.smoothRadius = smoothRadius;
            this.smoothIterations = smoothIterations;
            this.smoothStrength = smoothStrength;
            this.maxBatchSize = maxBatchSize;
            this.enableMinHeightSkip = enableMinHeightSkip;
            this.minHeightThreshold = minHeightThreshold;
            this.enableMaxHeightSkip = enableMaxHeightSkip;
            this.maxHeightThreshold = maxHeightThreshold;
        }

        /// <summary>
        /// 对所有地形进行跨地形平滑处理
        /// </summary>
        public async Task<int> SmoothTerrainsAsync(List<Terrain> terrains)
        {
            if (terrains == null || terrains.Count == 0) return 0;

            try
            {
                // 步骤1：统一高度值范围
                EditorUtility.DisplayProgressBar("跨地形平滑", "统一地形高度范围...", 0.1f);
                await TerrainSmoothUtils.UnifyTerrainHeightRangesAsync(terrains);

                // 步骤2：建立地形空间网格
                EditorUtility.DisplayProgressBar("跨地形平滑", "建立地形空间网格...", 0.2f);
                currentGrid = TerrainSmoothUtils.BuildTerrainGrid(terrains);

                // 步骤3：按相邻关系分批处理
                EditorUtility.DisplayProgressBar("跨地形平滑", "创建处理批次...", 0.3f);
                var batches = TerrainSmoothUtils.CreateSpatialBatches(currentGrid, maxBatchSize);

                int processedCount = 0;
                processedTerrains = new HashSet<Terrain>(); // 追踪已处理的地形，避免重复更新

                // 步骤4：处理每个批次
                for (int iteration = 0; iteration < smoothIterations; iteration++)
                {
                    for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
                    {
                        float progress = 0.3f + 0.6f * ((iteration * batches.Count + batchIndex) / (float)(smoothIterations * batches.Count));
                        EditorUtility.DisplayProgressBar("跨地形平滑", 
                            $"第{iteration + 1}/{smoothIterations}次迭代，批次{batchIndex + 1}/{batches.Count}...", progress);

                        await ProcessSpatialBatchAsync(batches[batchIndex], processedTerrains);
                        processedCount += batches[batchIndex].terrains.Count;
                    }
                }

                // 关键步骤：全局边缘同步，确保所有批次间边界无缝
                EditorUtility.DisplayProgressBar("跨地形平滑", "全局边缘同步...", 0.95f);
                PerformGlobalEdgeSync();

                EditorUtility.DisplayProgressBar("跨地形平滑", "完成平滑处理...", 1.0f);

                return terrains.Count;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                
                // 清理成员变量
                currentGrid = null;
                processedTerrains = null;
            }
        }

        /// <summary>
        /// 处理单个空间批次
        /// </summary>
        private async Task ProcessSpatialBatchAsync(SpatialBatch batch, HashSet<Terrain> processedTerrains)
        {
            // 步骤1：在主线程获取所有地形高度数据和Unity API数据
            var terrainHeights = new Dictionary<Terrain, float[,]>();
            var terrainNames = TerrainSmoothUtils.CreateTerrainNameMapping(batch.terrains);
            var terrainSizes = TerrainSmoothUtils.CreateTerrainSizeMapping(batch.terrains);
            
            foreach (var terrain in batch.terrains)
            {
                var terrainData = terrain.terrainData;
                var heights = terrainData.GetHeights(0, 0, batch.grid.terrainResolution, batch.grid.terrainResolution);
                terrainHeights[terrain] = heights;
            }

            // 步骤2：创建大的NativeArray并拼接瓦片
            var bigArray = await CreateBigNativeArrayAsync(batch, terrainHeights, terrainNames, terrainSizes);

            try
            {
                // 步骤3：使用JobSystem进行平滑
                await SmoothWithJobSystemAsync(bigArray);

                // 步骤3.5：平滑后同步边缘点对，确保无缝
                SynchronizeEdgePointsAfterSmoothing(bigArray);

                // 步骤4：还原回各个地形（带有边缘融合策略）
                await RestoreToTerrainsAsync(batch, bigArray, terrainNames, terrainSizes, processedTerrains);
            }
            finally
            {
                // 清理NativeArray资源
                TerrainSmoothUtils.DisposeBigArray(bigArray);
            }
        }

        /// <summary>
        /// 创建大的NativeArray并拼接瓦片
        /// </summary>
        private async Task<BigArray> CreateBigNativeArrayAsync(SpatialBatch batch, Dictionary<Terrain, float[,]> terrainHeights,
            Dictionary<Terrain, string> terrainNames, Dictionary<Terrain, Vector3> terrainSizes)
        {
            return await Task.Run(() =>
            {
                var grid = batch.grid;
                int resolution = grid.terrainResolution;
                
                // 计算大数组的尺寸
                int batchWidth = batch.batchGridSize * resolution;
                int batchHeight = batch.batchGridSize * resolution;
                
                // 创建大的NativeArray
                var heightData = new NativeArray<float>(batchWidth * batchHeight, Allocator.TempJob);
                
                // 初始化为-1
                for (int i = 0; i < heightData.Length; i++)
                {
                    heightData[i] = -1f;
                }

                var bigArray = new BigArray
                {
                    heightData = heightData,
                    width = batchWidth,
                    height = batchHeight,
                    terrainResolution = resolution,
                    edgePointMappings = new List<EdgePointMapping>()
                };

                // 拼接各个瓦片
                for (int gridZ = batch.startGridZ; gridZ < batch.startGridZ + batch.batchGridSize && gridZ < grid.gridHeight; gridZ++)
                {
                    for (int gridX = batch.startGridX; gridX < batch.startGridX + batch.batchGridSize && gridX < grid.gridWidth; gridX++)
                    {
                        var terrain = grid.terrains[gridX, gridZ];
                        if (terrain != null && terrainHeights.ContainsKey(terrain))
                        {
                            AppendTerrainToBigArray(bigArray, terrainHeights[terrain], 
                                gridX - batch.startGridX, gridZ - batch.startGridZ, terrainNames[terrain],
                                terrainSizes[terrain]);
                        }
                    }
                }

                // 合并边缘点
                MergeEdgePoints(bigArray, batch);

                return bigArray;
            });
        }

        /// <summary>
        /// 将单个地形数据填充到大数组中
        /// </summary>
        private void AppendTerrainToBigArray(BigArray bigArray, float[,] terrainHeights, 
            int gridOffsetX, int gridOffsetZ, string terrainName, Vector3 terrainSize)
        {
            int resolution = bigArray.terrainResolution;
            int startX = gridOffsetX * resolution;
            int startZ = gridOffsetZ * resolution;

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int bigArrayX = startX + x;
                    int bigArrayZ = startZ + z;
                    
                    if (bigArrayX < bigArray.width && bigArrayZ < bigArray.height)
                    {
                        int index = bigArrayZ * bigArray.width + bigArrayX;
                        
                        // 转换到世界单位（米）
                        float worldHeight = terrainHeights[z, x] * terrainSize.y;
                        bigArray.heightData[index] = worldHeight;
                    }
                }
            }
        }

        /// <summary>
        /// 合并边缘点（相邻瓦片的共享边变成1个点）
        /// </summary>
        private void MergeEdgePoints(BigArray bigArray, SpatialBatch batch)
        {
            int resolution = bigArray.terrainResolution;

            // 处理垂直边缘（相邻瓦片的左右边）
            for (int gridZ = 0; gridZ < batch.batchGridSize; gridZ++)
            {
                for (int gridX = 0; gridX < batch.batchGridSize - 1; gridX++)
                {
                    int rightEdgeX = (gridX + 1) * resolution;
                    int leftEdgeX = rightEdgeX - 1;
                    
                    for (int z = 0; z < resolution; z++)
                    {
                        int bigArrayZ = gridZ * resolution + z;
                        if (bigArrayZ >= bigArray.height) continue;

                        int leftIndex = bigArrayZ * bigArray.width + leftEdgeX;
                        int rightIndex = bigArrayZ * bigArray.width + rightEdgeX;

                        if (leftIndex < bigArray.heightData.Length && rightIndex < bigArray.heightData.Length)
                        {
                            float leftHeight = bigArray.heightData[leftIndex];
                            float rightHeight = bigArray.heightData[rightIndex];

                            if (leftHeight != -1f && rightHeight != -1f)
                            {
                                // 合并：两个点都设为平均值，作为共享点参与平滑
                                float averageHeight = (leftHeight + rightHeight) * 0.5f;
                                bigArray.heightData[leftIndex] = averageHeight;
                                bigArray.heightData[rightIndex] = averageHeight;

                                // 记录边缘点映射 - 这两个点是共享的，平滑后需要同步
                                bigArray.edgePointMappings.Add(new EdgePointMapping
                                {
                                    index1 = leftIndex,
                                    index2 = rightIndex,
                                    mergedValue = averageHeight
                                });
                            }
                        }
                    }
                }
            }

            // 处理水平边缘（相邻瓦片的上下边）
            for (int gridZ = 0; gridZ < batch.batchGridSize - 1; gridZ++)
            {
                for (int gridX = 0; gridX < batch.batchGridSize; gridX++)
                {
                    int bottomEdgeZ = (gridZ + 1) * resolution;
                    int topEdgeZ = bottomEdgeZ - 1;
                    
                    for (int x = 0; x < resolution; x++)
                    {
                        int bigArrayX = gridX * resolution + x;
                        if (bigArrayX >= bigArray.width) continue;

                        int topIndex = topEdgeZ * bigArray.width + bigArrayX;
                        int bottomIndex = bottomEdgeZ * bigArray.width + bigArrayX;

                        if (topIndex < bigArray.heightData.Length && bottomIndex < bigArray.heightData.Length)
                        {
                            float topHeight = bigArray.heightData[topIndex];
                            float bottomHeight = bigArray.heightData[bottomIndex];

                            if (topHeight != -1f && bottomHeight != -1f)
                            {
                                // 合并：两个点都设为平均值，作为共享点参与平滑
                                float averageHeight = (topHeight + bottomHeight) * 0.5f;
                                bigArray.heightData[topIndex] = averageHeight;
                                bigArray.heightData[bottomIndex] = averageHeight;

                                // 记录边缘点映射 - 这两个点是共享的，平滑后需要同步
                                bigArray.edgePointMappings.Add(new EdgePointMapping
                                {
                                    index1 = topIndex,
                                    index2 = bottomIndex,
                                    mergedValue = averageHeight
                                });
                            }
                        }
                    }
                }
            }
        }


        /// <summary>
        /// 使用JobSystem进行平滑处理
        /// </summary>
        private async Task SmoothWithJobSystemAsync(BigArray bigArray)
        {
            await Task.Run(() =>
            {
                // 创建输出缓冲区
                var outputHeightData = new NativeArray<float>(bigArray.heightData.Length, Allocator.TempJob);
                
                try
                {
                    for (int iteration = 0; iteration < smoothIterations; iteration++)
                    {
                        var smoothJob = new BigArraySmoothJob
                        {
                            inputHeightData = bigArray.heightData,
                            outputHeightData = outputHeightData,
                            width = bigArray.width,
                            height = bigArray.height,
                            smoothRadius = this.smoothRadius,
                            smoothStrength = this.smoothStrength,
                            enableMinHeightSkip = this.enableMinHeightSkip,
                            minHeightThreshold = this.minHeightThreshold,
                            enableMaxHeightSkip = this.enableMaxHeightSkip,
                            maxHeightThreshold = this.maxHeightThreshold
                        };

                        var jobHandle = smoothJob.Schedule(bigArray.heightData.Length, 64);
                        
                        // 立即完成job确保线程安全
                        jobHandle.Complete();
                        
                        // 交换输入输出缓冲区，为下一次迭代做准备
                        var temp = bigArray.heightData;
                        bigArray.heightData = outputHeightData;
                        outputHeightData = temp;
                    }
                }
                finally
                {
                    // 清理输出缓冲区
                    if (outputHeightData.IsCreated)
                    {
                        outputHeightData.Dispose();
                    }
                }
            });
        }

        /// <summary>
        /// 平滑后同步边缘点对，确保共享边缘无缝
        /// </summary>
        private void SynchronizeEdgePointsAfterSmoothing(BigArray bigArray)
        {
            int synchronizedPairs = 0;
            
            foreach (var mapping in bigArray.edgePointMappings)
            {
                // 取两个边缘点平滑后的平均值
                float height1 = bigArray.heightData[mapping.index1];
                float height2 = bigArray.heightData[mapping.index2];
                
                if (height1 != -1f && height2 != -1f)
                {
                    float synchronizedHeight = (height1 + height2) * 0.5f;
                    bigArray.heightData[mapping.index1] = synchronizedHeight;
                    bigArray.heightData[mapping.index2] = synchronizedHeight;
                    synchronizedPairs++;
                }
            }
        }

        /// <summary>
        /// 还原回各个地形
        /// </summary>
        private async Task RestoreToTerrainsAsync(SpatialBatch batch, BigArray bigArray, 
            Dictionary<Terrain, string> terrainNames, Dictionary<Terrain, Vector3> terrainSizes, HashSet<Terrain> processedTerrains)
        {
            // 在后台线程计算新的高度数据
            var terrainHeights = await Task.Run(() =>
            {
                var results = new Dictionary<Terrain, float[,]>();
                
                for (int gridZ = batch.startGridZ; gridZ < batch.startGridZ + batch.batchGridSize && gridZ < batch.grid.gridHeight; gridZ++)
                {
                    for (int gridX = batch.startGridX; gridX < batch.startGridX + batch.batchGridSize && gridX < batch.grid.gridWidth; gridX++)
                    {
                        var terrain = batch.grid.terrains[gridX, gridZ];
                        if (terrain != null)
                        {
                            var newHeights = ExtractTerrainFromBigArray(bigArray, terrain, 
                                gridX - batch.startGridX, gridZ - batch.startGridZ, terrainNames[terrain],
                                terrainSizes[terrain]);
                            results[terrain] = newHeights;
                        }
                    }
                }
                
                return results;
            });

            // 在主线程应用到地形（使用边缘融合策略）
            foreach (var kvp in terrainHeights)
            {
                var terrain = kvp.Key;
                var newHeights = kvp.Value;
                
                // 检查这个地形是否已经在之前的批次中被处理过
                bool isAlreadyProcessed = processedTerrains.Contains(terrain);
                
                if (isAlreadyProcessed)
                {
                    // 如果已处理过，使用边缘融合策略
                    BlendWithExistingTerrain(terrain, newHeights);
                }
                else
                {
                    // 第一次处理，直接应用
                    ApplyHeightsToTerrain(terrain, newHeights);
                    processedTerrains.Add(terrain);
                }
            }
        }

        /// <summary>
        /// 从BigArray中提取单个地形的高度数据
        /// </summary>
        private float[,] ExtractTerrainFromBigArray(BigArray bigArray, Terrain terrain, int gridOffsetX, int gridOffsetZ, 
            string terrainName, Vector3 terrainSize)
        {
            int resolution = bigArray.terrainResolution;
            int startX = gridOffsetX * resolution;
            int startZ = gridOffsetZ * resolution;
            
            var newHeights = new float[resolution, resolution];

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int bigArrayX = startX + x;
                    int bigArrayZ = startZ + z;
                    
                    if (bigArrayX < bigArray.width && bigArrayZ < bigArray.height)
                    {
                        int index = bigArrayZ * bigArray.width + bigArrayX;
                        float worldHeight = bigArray.heightData[index];
                        
                        if (worldHeight != -1f)
                        {
                            // 转换回归一化高度
                            float normalizedHeight = worldHeight / terrainSize.y;
                            newHeights[z, x] = Mathf.Clamp01(normalizedHeight);
                        }
                        else
                        {
                            // 保持原始值（这种情况应该很少发生）
                            newHeights[z, x] = 0f;
                        }
                    }
                }
            }

            return newHeights;
        }

        /// <summary>
        /// 融合地形高度数据（确保相邻边缘点合并成1个）
        /// </summary>
        private void BlendWithExistingTerrain(Terrain terrain, float[,] newHeights)
        {
            var terrainData = terrain.terrainData;
            int resolution = terrainData.heightmapResolution;
            
            // 获取当前地形高度
            var currentHeights = terrainData.GetHeights(0, 0, resolution, resolution);
            
            // 找到地形在网格中的位置
            var (gridX, gridZ) = FindTerrainGridPosition(terrain);
            if (gridX == -1) return; // 找不到位置
            
            // 先进行内部区域的加权融合
            float newWeight = 0.6f;
            float currentWeight = 0.4f;
            
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    currentHeights[z, x] = currentHeights[z, x] * currentWeight + newHeights[z, x] * newWeight;
                }
            }
            
            // 关键：对相邻边缘进行点对点同步，确保边缘点合并成1个
            SynchronizeAdjacentEdges(terrain, currentHeights, gridX, gridZ);
            
            // 应用融合后的高度
            Undo.RegisterCompleteObjectUndo(terrainData, $"跨地形平滑融合 - {terrain.name}");
            terrainData.SetHeights(0, 0, currentHeights);
        }

        /// <summary>
        /// 找到地形在网格中的位置
        /// </summary>
        private (int gridX, int gridZ) FindTerrainGridPosition(Terrain terrain)
        {
            if (currentGrid == null) return (-1, -1);
            
            for (int z = 0; z < currentGrid.gridHeight; z++)
            {
                for (int x = 0; x < currentGrid.gridWidth; x++)
                {
                    if (currentGrid.terrains[x, z] == terrain)
                    {
                        return (x, z);
                    }
                }
            }
            return (-1, -1);
        }

        /// <summary>
        /// 同步相邻地形的边缘点，确保边缘点合并成1个
        /// </summary>
        private void SynchronizeAdjacentEdges(Terrain terrain, float[,] heights, int gridX, int gridZ)
        {
            int resolution = heights.GetLength(0);
            if (currentGrid == null) return;
            
            // 检查左邻居
            if (gridX > 0 && currentGrid.terrains[gridX - 1, gridZ] != null)
            {
                var leftTerrain = currentGrid.terrains[gridX - 1, gridZ];
                if (processedTerrains.Contains(leftTerrain))
                {
                    var leftHeights = leftTerrain.terrainData.GetHeights(0, 0, resolution, resolution);
                    
                    // 同步左边缘：当前地形左边 = 左邻居右边
                    for (int z = 0; z < resolution; z++)
                    {
                        float avgHeight = (heights[z, 0] + leftHeights[z, resolution - 1]) * 0.5f;
                        heights[z, 0] = avgHeight;
                        leftHeights[z, resolution - 1] = avgHeight;
                    }
                    
                    leftTerrain.terrainData.SetHeights(0, 0, leftHeights);
                }
            }
            
            // 检查右邻居
            if (gridX + 1 < currentGrid.gridWidth && currentGrid.terrains[gridX + 1, gridZ] != null)
            {
                var rightTerrain = currentGrid.terrains[gridX + 1, gridZ];
                if (processedTerrains.Contains(rightTerrain))
                {
                    var rightHeights = rightTerrain.terrainData.GetHeights(0, 0, resolution, resolution);
                    
                    // 同步右边缘：当前地形右边 = 右邻居左边
                    for (int z = 0; z < resolution; z++)
                    {
                        float avgHeight = (heights[z, resolution - 1] + rightHeights[z, 0]) * 0.5f;
                        heights[z, resolution - 1] = avgHeight;
                        rightHeights[z, 0] = avgHeight;
                    }
                    
                    rightTerrain.terrainData.SetHeights(0, 0, rightHeights);
                }
            }
            
            // 检查上邻居
            if (gridZ > 0 && currentGrid.terrains[gridX, gridZ - 1] != null)
            {
                var topTerrain = currentGrid.terrains[gridX, gridZ - 1];
                if (processedTerrains.Contains(topTerrain))
                {
                    var topHeights = topTerrain.terrainData.GetHeights(0, 0, resolution, resolution);
                    
                    // 同步上边缘：当前地形上边 = 上邻居下边
                    for (int x = 0; x < resolution; x++)
                    {
                        float avgHeight = (heights[0, x] + topHeights[resolution - 1, x]) * 0.5f;
                        heights[0, x] = avgHeight;
                        topHeights[resolution - 1, x] = avgHeight;
                    }
                    
                    topTerrain.terrainData.SetHeights(0, 0, topHeights);
                }
            }
            
            // 检查下邻居  
            if (gridZ + 1 < currentGrid.gridHeight && currentGrid.terrains[gridX, gridZ + 1] != null)
            {
                var bottomTerrain = currentGrid.terrains[gridX, gridZ + 1];
                if (processedTerrains.Contains(bottomTerrain))
                {
                    var bottomHeights = bottomTerrain.terrainData.GetHeights(0, 0, resolution, resolution);
                    
                    // 同步下边缘：当前地形下边 = 下邻居上边
                    for (int x = 0; x < resolution; x++)
                    {
                        float avgHeight = (heights[resolution - 1, x] + bottomHeights[0, x]) * 0.5f;
                        heights[resolution - 1, x] = avgHeight;
                        bottomHeights[0, x] = avgHeight;
                    }
                    
                    bottomTerrain.terrainData.SetHeights(0, 0, bottomHeights);
                }
            }
        }

        /// <summary>
        /// 直接应用高度到地形
        /// </summary>
        private void ApplyHeightsToTerrain(Terrain terrain, float[,] newHeights)
        {
            var terrainData = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(terrainData, $"跨地形平滑 - {terrain.name}");
            terrainData.SetHeights(0, 0, newHeights);
        }


        /// <summary>
        /// 全局边缘同步，确保所有批次间边界无缝
        /// </summary>
        private void PerformGlobalEdgeSync()
        {
            if (currentGrid?.terrains == null) return;

            // 遍历所有地形，同步相邻边缘
            for (int z = 0; z < currentGrid.gridHeight; z++)
            {
                for (int x = 0; x < currentGrid.gridWidth; x++)
                {
                    var terrain = currentGrid.terrains[x, z];
                    if (terrain == null) continue;

                    // 同步右边缘（与右邻居）
                    if (x + 1 < currentGrid.gridWidth && currentGrid.terrains[x + 1, z] != null)
                    {
                        SyncTerrainEdge(terrain, currentGrid.terrains[x + 1, z], true); // true = 垂直边缘
                    }
                    
                    // 同步下边缘（与下邻居）
                    if (z + 1 < currentGrid.gridHeight && currentGrid.terrains[x, z + 1] != null)
                    {
                        SyncTerrainEdge(terrain, currentGrid.terrains[x, z + 1], false); // false = 水平边缘
                    }
                }
            }
        }

        /// <summary>
        /// 同步两个相邻地形的共享边缘
        /// </summary>
        private void SyncTerrainEdge(Terrain terrain1, Terrain terrain2, bool isVerticalEdge)
        {
            var data1 = terrain1.terrainData;
            var data2 = terrain2.terrainData;
            int resolution = data1.heightmapResolution;
            
            var heights1 = data1.GetHeights(0, 0, resolution, resolution);
            var heights2 = data2.GetHeights(0, 0, resolution, resolution);
            
            if (isVerticalEdge)
            {
                // 垂直边缘：terrain1右边 = terrain2左边
                for (int z = 0; z < resolution; z++)
                {
                    float height1 = heights1[z, resolution - 1]; // terrain1右边
                    float height2 = heights2[z, 0]; // terrain2左边
                    float syncHeight = (height1 + height2) * 0.5f;
                    
                    heights1[z, resolution - 1] = syncHeight;
                    heights2[z, 0] = syncHeight;
                }
            }
            else
            {
                // 水平边缘：terrain1下边 = terrain2上边
                for (int x = 0; x < resolution; x++)
                {
                    float height1 = heights1[resolution - 1, x]; // terrain1下边
                    float height2 = heights2[0, x]; // terrain2上边
                    float syncHeight = (height1 + height2) * 0.5f;
                    
                    heights1[resolution - 1, x] = syncHeight;
                    heights2[0, x] = syncHeight;
                }
            }
            
            data1.SetHeights(0, 0, heights1);
            data2.SetHeights(0, 0, heights2);
        }
    }
}