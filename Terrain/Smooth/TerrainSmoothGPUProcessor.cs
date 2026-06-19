using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Unity.Collections;

namespace GeoToolkit.Smooth
{
    /// <summary>
    /// 地形平滑GPU处理器 - 使用ComputeShader加速
    /// </summary>
    public class TerrainSmoothGPUProcessor
    {
        private readonly int smoothRadius;
        private readonly int smoothIterations;
        private readonly float smoothStrength;
        private readonly int maxBatchSize;
        private readonly bool enableMinHeightSkip;
        private readonly float minHeightThreshold;
        private readonly bool enableMaxHeightSkip;
        private readonly float maxHeightThreshold;
        
        // ComputeShader相关
        private ComputeShader computeShader;
        private int kernelIndex;
        
        // 用于跟踪当前处理状态
        private TerrainGrid currentGrid;
        private HashSet<Terrain> processedTerrains;

        public TerrainSmoothGPUProcessor(int smoothRadius = 3, int smoothIterations = 1, float smoothStrength = 0.5f,
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
            
            InitializeComputeShader();
        }
        
        /// <summary>
        /// 初始化ComputeShader
        /// 文件位置: Packages/GeoToolkit/Runtime/Resources/TerrainSmoothCompute.compute
        /// </summary>
        private void InitializeComputeShader()
        {
            // 从Resources目录加载ComputeShader
            computeShader = Resources.Load<ComputeShader>("TerrainSmoothCompute");
            
            if (computeShader == null)
            {
                // 尝试备用路径
                string shaderPath = "Packages/GeoToolkit/Runtime/Resources/TerrainSmoothCompute.compute";
                computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(shaderPath);
            }
            
            if (computeShader == null)
            {
                Debug.LogError("无法加载ComputeShader: TerrainSmoothCompute。请确保文件位于: Packages/GeoToolkit/Runtime/Resources/TerrainSmoothCompute.compute");
                return;
            }
            
            kernelIndex = computeShader.FindKernel("TerrainSmoothMain");
            if (kernelIndex < 0)
            {
                Debug.LogError("无法找到ComputeShader内核: TerrainSmoothMain");
            }
        }

        /// <summary>
        /// 对所有地形进行跨地形平滑处理（GPU加速版本）
        /// </summary>
        public async Task<int> SmoothTerrainsAsync(List<Terrain> terrains)
        {
            if (terrains == null || terrains.Count == 0) return 0;
            if (computeShader == null || kernelIndex < 0) 
            {
                Debug.LogError("ComputeShader未正确初始化，无法使用GPU加速");
                return 0;
            }

            try
            {
                // 步骤1：统一高度值范围
                EditorUtility.DisplayProgressBar("GPU地形平滑", "统一地形高度范围...", 0.1f);
                await TerrainSmoothUtils.UnifyTerrainHeightRangesAsync(terrains);

                // 步骤2：建立地形空间网格
                EditorUtility.DisplayProgressBar("GPU地形平滑", "建立地形空间网格...", 0.2f);
                currentGrid = TerrainSmoothUtils.BuildTerrainGrid(terrains);

                // 步骤3：按相邻关系分批处理
                EditorUtility.DisplayProgressBar("GPU地形平滑", "创建处理批次...", 0.3f);
                var batches = TerrainSmoothUtils.CreateSpatialBatches(currentGrid, maxBatchSize);

                int processedCount = 0;
                processedTerrains = new HashSet<Terrain>();

                // 步骤4：处理每个批次（使用GPU）
                for (int iteration = 0; iteration < smoothIterations; iteration++)
                {
                    for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
                    {
                        float progress = 0.3f + 0.6f * ((iteration * batches.Count + batchIndex) / (float)(smoothIterations * batches.Count));
                        EditorUtility.DisplayProgressBar("GPU地形平滑", 
                            $"GPU加速第{iteration + 1}/{smoothIterations}次迭代，批次{batchIndex + 1}/{batches.Count}...", progress);

                        await ProcessSpatialBatchWithGPUAsync(batches[batchIndex], processedTerrains);
                        processedCount += batches[batchIndex].terrains.Count;
                    }
                }

                // 关键步骤：全局边缘同步，确保所有批次间边界无缝
                EditorUtility.DisplayProgressBar("GPU地形平滑", "全局边缘同步...", 0.95f);
                PerformGlobalEdgeSync();

                EditorUtility.DisplayProgressBar("GPU地形平滑", "完成GPU平滑处理...", 1.0f);

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
        /// 使用GPU处理单个空间批次
        /// </summary>
        private async Task ProcessSpatialBatchWithGPUAsync(SpatialBatch batch, HashSet<Terrain> processedTerrains)
        {
            // 步骤1：在主线程获取所有地形高度数据
            var terrainHeights = new Dictionary<Terrain, float[,]>();
            var terrainNames = TerrainSmoothUtils.CreateTerrainNameMapping(batch.terrains);
            var terrainSizes = TerrainSmoothUtils.CreateTerrainSizeMapping(batch.terrains);
            
            foreach (var terrain in batch.terrains)
            {
                var terrainData = terrain.terrainData;
                var heights = terrainData.GetHeights(0, 0, batch.grid.terrainResolution, batch.grid.terrainResolution);
                terrainHeights[terrain] = heights;
            }

            // 步骤2：创建大的数组并拼接瓦片（在主线程中执行）
            var bigArrayData = CreateBigArrayForGPU(batch, terrainHeights, terrainNames, terrainSizes);
            
            // 步骤3：使用GPU ComputeShader进行平滑
            await SmoothWithGPUAsync(bigArrayData);

            // 步骤3.5：平滑后同步边缘点对，确保无缝
            SynchronizeEdgePointsAfterSmoothing(bigArrayData);

            // 步骤4：还原回各个地形
            await RestoreToTerrainsAsync(batch, bigArrayData, terrainNames, terrainSizes, processedTerrains);
        }

        /// <summary>
        /// 创建适用于GPU的大数组数据（在主线程中执行）
        /// </summary>
        private GPUBigArrayData CreateBigArrayForGPU(SpatialBatch batch, Dictionary<Terrain, float[,]> terrainHeights,
            Dictionary<Terrain, string> terrainNames, Dictionary<Terrain, Vector3> terrainSizes)
        {
            var grid = batch.grid;
            int resolution = grid.terrainResolution;
            
            // 计算大数组的尺寸
            int batchWidth = batch.batchGridSize * resolution;
            int batchHeight = batch.batchGridSize * resolution;
            
            // 创建大的float数组
            var heightData = new float[batchWidth * batchHeight];
            
            // 初始化为-1
            for (int i = 0; i < heightData.Length; i++)
            {
                heightData[i] = -1f;
            }

            var bigArrayData = new GPUBigArrayData
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
                        AppendTerrainToGPUArray(bigArrayData, terrainHeights[terrain], 
                            gridX - batch.startGridX, gridZ - batch.startGridZ, terrainNames[terrain],
                            terrainSizes[terrain]);
                    }
                }
            }

            // 合并边缘点
            MergeEdgePointsForGPU(bigArrayData, batch);

            return bigArrayData;
        }
        
        /// <summary>
        /// 将单个地形数据填充到GPU数组中
        /// </summary>
        private void AppendTerrainToGPUArray(GPUBigArrayData bigArrayData, float[,] terrainHeights, 
            int gridOffsetX, int gridOffsetZ, string terrainName, Vector3 terrainSize)
        {
            int resolution = bigArrayData.terrainResolution;
            int startX = gridOffsetX * resolution;
            int startZ = gridOffsetZ * resolution;

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int bigArrayX = startX + x;
                    int bigArrayZ = startZ + z;
                    
                    if (bigArrayX < bigArrayData.width && bigArrayZ < bigArrayData.height)
                    {
                        int index = bigArrayZ * bigArrayData.width + bigArrayX;
                        
                        // 转换到世界单位（米）
                        float worldHeight = terrainHeights[z, x] * terrainSize.y;
                        bigArrayData.heightData[index] = worldHeight;
                    }
                }
            }
        }

        /// <summary>
        /// GPU版本的边缘点合并
        /// </summary>
        private void MergeEdgePointsForGPU(GPUBigArrayData bigArrayData, SpatialBatch batch)
        {
            int resolution = bigArrayData.terrainResolution;

            // 处理垂直边缘
            for (int gridZ = 0; gridZ < batch.batchGridSize; gridZ++)
            {
                for (int gridX = 0; gridX < batch.batchGridSize - 1; gridX++)
                {
                    int rightEdgeX = (gridX + 1) * resolution;
                    int leftEdgeX = rightEdgeX - 1;
                    
                    for (int z = 0; z < resolution; z++)
                    {
                        int bigArrayZ = gridZ * resolution + z;
                        if (bigArrayZ >= bigArrayData.height) continue;

                        int leftIndex = bigArrayZ * bigArrayData.width + leftEdgeX;
                        int rightIndex = bigArrayZ * bigArrayData.width + rightEdgeX;

                        if (leftIndex < bigArrayData.heightData.Length && rightIndex < bigArrayData.heightData.Length)
                        {
                            float leftHeight = bigArrayData.heightData[leftIndex];
                            float rightHeight = bigArrayData.heightData[rightIndex];

                            if (leftHeight != -1f && rightHeight != -1f)
                            {
                                float averageHeight = (leftHeight + rightHeight) * 0.5f;
                                bigArrayData.heightData[leftIndex] = averageHeight;
                                bigArrayData.heightData[rightIndex] = averageHeight;

                                bigArrayData.edgePointMappings.Add(new EdgePointMapping
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

            // 处理水平边缘
            for (int gridZ = 0; gridZ < batch.batchGridSize - 1; gridZ++)
            {
                for (int gridX = 0; gridX < batch.batchGridSize; gridX++)
                {
                    int bottomEdgeZ = (gridZ + 1) * resolution;
                    int topEdgeZ = bottomEdgeZ - 1;
                    
                    for (int x = 0; x < resolution; x++)
                    {
                        int bigArrayX = gridX * resolution + x;
                        if (bigArrayX >= bigArrayData.width) continue;

                        int topIndex = topEdgeZ * bigArrayData.width + bigArrayX;
                        int bottomIndex = bottomEdgeZ * bigArrayData.width + bigArrayX;

                        if (topIndex < bigArrayData.heightData.Length && bottomIndex < bigArrayData.heightData.Length)
                        {
                            float topHeight = bigArrayData.heightData[topIndex];
                            float bottomHeight = bigArrayData.heightData[bottomIndex];

                            if (topHeight != -1f && bottomHeight != -1f)
                            {
                                float averageHeight = (topHeight + bottomHeight) * 0.5f;
                                bigArrayData.heightData[topIndex] = averageHeight;
                                bigArrayData.heightData[bottomIndex] = averageHeight;

                                bigArrayData.edgePointMappings.Add(new EdgePointMapping
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
        /// 使用GPU ComputeShader进行平滑处理（在主线程中执行）
        /// </summary>
        private async Task SmoothWithGPUAsync(GPUBigArrayData bigArrayData)
        {
            // GPU操作必须在主线程中执行
            int dataLength = bigArrayData.heightData.Length;
            
            // 创建GPU缓冲区
            ComputeBuffer inputBuffer = null;
            ComputeBuffer outputBuffer = null;
            
            try
            {
                inputBuffer = new ComputeBuffer(dataLength, sizeof(float));
                outputBuffer = new ComputeBuffer(dataLength, sizeof(float));
                
                for (int iteration = 0; iteration < smoothIterations; iteration++)
                {
                    // 上传数据到GPU
                    inputBuffer.SetData(bigArrayData.heightData);
                    
                    // 设置ComputeShader参数
                    computeShader.SetBuffer(kernelIndex, "inputHeightBuffer", inputBuffer);
                    computeShader.SetBuffer(kernelIndex, "outputHeightBuffer", outputBuffer);
                    computeShader.SetInt("width", bigArrayData.width);
                    computeShader.SetInt("height", bigArrayData.height);
                    computeShader.SetInt("smoothRadius", smoothRadius);
                    computeShader.SetFloat("smoothStrength", smoothStrength);
                    computeShader.SetBool("enableMinHeightSkip", enableMinHeightSkip);
                    computeShader.SetFloat("minHeightThreshold", minHeightThreshold);
                    computeShader.SetBool("enableMaxHeightSkip", enableMaxHeightSkip);
                    computeShader.SetFloat("maxHeightThreshold", maxHeightThreshold);
                    
                    // 计算调度组数（8x8线程组）
                    int groupsX = (bigArrayData.width + 7) / 8;
                    int groupsY = (bigArrayData.height + 7) / 8;
                    
                    // 调度GPU计算
                    computeShader.Dispatch(kernelIndex, groupsX, groupsY, 1);
                    
                    // 从GPU读取结果
                    outputBuffer.GetData(bigArrayData.heightData);
                    
                    // 交换缓冲区为下一次迭代做准备
                    var temp = inputBuffer;
                    inputBuffer = outputBuffer;
                    outputBuffer = temp;
                }
                
                // 让出一帧来避免阻塞UI
                await Task.Yield();
            }
            finally
            {
                // 清理GPU缓冲区
                inputBuffer?.Release();
                outputBuffer?.Release();
            }
        }

        /// <summary>
        /// GPU版本的边缘点同步
        /// </summary>
        private void SynchronizeEdgePointsAfterSmoothing(GPUBigArrayData bigArrayData)
        {
            foreach (var mapping in bigArrayData.edgePointMappings)
            {
                float height1 = bigArrayData.heightData[mapping.index1];
                float height2 = bigArrayData.heightData[mapping.index2];
                
                if (height1 != -1f && height2 != -1f)
                {
                    float synchronizedHeight = (height1 + height2) * 0.5f;
                    bigArrayData.heightData[mapping.index1] = synchronizedHeight;
                    bigArrayData.heightData[mapping.index2] = synchronizedHeight;
                }
            }
        }

        /// <summary>
        /// GPU版本的还原到地形
        /// </summary>
        private async Task RestoreToTerrainsAsync(SpatialBatch batch, GPUBigArrayData bigArrayData, 
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
                            var newHeights = ExtractTerrainFromGPUArray(bigArrayData, terrain, 
                                gridX - batch.startGridX, gridZ - batch.startGridZ, terrainNames[terrain],
                                terrainSizes[terrain]);
                            results[terrain] = newHeights;
                        }
                    }
                }
                
                return results;
            });

            // 在主线程应用到地形
            foreach (var kvp in terrainHeights)
            {
                var terrain = kvp.Key;
                var newHeights = kvp.Value;
                
                bool isAlreadyProcessed = processedTerrains.Contains(terrain);
                
                if (isAlreadyProcessed)
                {
                    BlendWithExistingTerrain(terrain, newHeights);
                }
                else
                {
                    ApplyHeightsToTerrain(terrain, newHeights);
                    processedTerrains.Add(terrain);
                }
            }
        }

        /// <summary>
        /// 从GPU数组中提取单个地形的高度数据
        /// </summary>
        private float[,] ExtractTerrainFromGPUArray(GPUBigArrayData bigArrayData, Terrain terrain, int gridOffsetX, int gridOffsetZ, 
            string terrainName, Vector3 terrainSize)
        {
            int resolution = bigArrayData.terrainResolution;
            int startX = gridOffsetX * resolution;
            int startZ = gridOffsetZ * resolution;
            
            var newHeights = new float[resolution, resolution];

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int bigArrayX = startX + x;
                    int bigArrayZ = startZ + z;
                    
                    if (bigArrayX < bigArrayData.width && bigArrayZ < bigArrayData.height)
                    {
                        int index = bigArrayZ * bigArrayData.width + bigArrayX;
                        float worldHeight = bigArrayData.heightData[index];
                        
                        if (worldHeight != -1f)
                        {
                            float normalizedHeight = worldHeight / terrainSize.y;
                            newHeights[z, x] = Mathf.Clamp01(normalizedHeight);
                        }
                        else
                        {
                            newHeights[z, x] = 0f;
                        }
                    }
                }
            }

            return newHeights;
        }

        // 复用原有的边缘处理方法
        private void BlendWithExistingTerrain(Terrain terrain, float[,] newHeights)
        {
            var terrainData = terrain.terrainData;
            int resolution = terrainData.heightmapResolution;
            
            var currentHeights = terrainData.GetHeights(0, 0, resolution, resolution);
            
            var (gridX, gridZ) = FindTerrainGridPosition(terrain);
            if (gridX == -1) return;
            
            float newWeight = 0.6f;
            float currentWeight = 0.4f;
            
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    currentHeights[z, x] = currentHeights[z, x] * currentWeight + newHeights[z, x] * newWeight;
                }
            }
            
            SynchronizeAdjacentEdges(terrain, currentHeights, gridX, gridZ);
            
            Undo.RegisterCompleteObjectUndo(terrainData, $"GPU跨地形平滑融合 - {terrain.name}");
            terrainData.SetHeights(0, 0, currentHeights);
        }

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

        private void ApplyHeightsToTerrain(Terrain terrain, float[,] newHeights)
        {
            var terrainData = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(terrainData, $"GPU跨地形平滑 - {terrain.name}");
            terrainData.SetHeights(0, 0, newHeights);
        }

        private void PerformGlobalEdgeSync()
        {
            if (currentGrid?.terrains == null) return;

            for (int z = 0; z < currentGrid.gridHeight; z++)
            {
                for (int x = 0; x < currentGrid.gridWidth; x++)
                {
                    var terrain = currentGrid.terrains[x, z];
                    if (terrain == null) continue;

                    if (x + 1 < currentGrid.gridWidth && currentGrid.terrains[x + 1, z] != null)
                    {
                        SyncTerrainEdge(terrain, currentGrid.terrains[x + 1, z], true);
                    }
                    
                    if (z + 1 < currentGrid.gridHeight && currentGrid.terrains[x, z + 1] != null)
                    {
                        SyncTerrainEdge(terrain, currentGrid.terrains[x, z + 1], false);
                    }
                }
            }
        }

        private void SyncTerrainEdge(Terrain terrain1, Terrain terrain2, bool isVerticalEdge)
        {
            var data1 = terrain1.terrainData;
            var data2 = terrain2.terrainData;
            int resolution = data1.heightmapResolution;
            
            var heights1 = data1.GetHeights(0, 0, resolution, resolution);
            var heights2 = data2.GetHeights(0, 0, resolution, resolution);
            
            if (isVerticalEdge)
            {
                for (int z = 0; z < resolution; z++)
                {
                    float height1 = heights1[z, resolution - 1];
                    float height2 = heights2[z, 0];
                    float syncHeight = (height1 + height2) * 0.5f;
                    
                    heights1[z, resolution - 1] = syncHeight;
                    heights2[z, 0] = syncHeight;
                }
            }
            else
            {
                for (int x = 0; x < resolution; x++)
                {
                    float height1 = heights1[resolution - 1, x];
                    float height2 = heights2[0, x];
                    float syncHeight = (height1 + height2) * 0.5f;
                    
                    heights1[resolution - 1, x] = syncHeight;
                    heights2[0, x] = syncHeight;
                }
            }
            
            data1.SetHeights(0, 0, heights1);
            data2.SetHeights(0, 0, heights2);
        }
    }

    /// <summary>
    /// GPU版本的大数组数据结构
    /// </summary>
    public class GPUBigArrayData
    {
        public float[] heightData;
        public int width;
        public int height;
        public int terrainResolution;
        public List<EdgePointMapping> edgePointMappings;
    }
}