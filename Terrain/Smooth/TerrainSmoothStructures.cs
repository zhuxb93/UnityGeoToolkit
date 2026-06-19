using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GeoToolkit.Smooth
{
    /// <summary>
    /// 地形网格
    /// </summary>
    public class TerrainGrid
    {
        public int gridWidth;
        public int gridHeight;
        public Vector3 minPosition;
        public Vector3 terrainSize;
        public int terrainResolution;
        public Terrain[,] terrains;
    }

    /// <summary>
    /// 空间批次
    /// </summary>
    public class SpatialBatch
    {
        public int startGridX;
        public int startGridZ;
        public int batchGridSize;
        public List<Terrain> terrains;
        public TerrainGrid grid;
    }

    /// <summary>
    /// 大数组
    /// </summary>
    public class BigArray
    {
        public NativeArray<float> heightData;
        public int width;
        public int height;
        public int terrainResolution;
        public List<EdgePointMapping> edgePointMappings;
    }

    /// <summary>
    /// 边缘点映射
    /// </summary>
    public class EdgePointMapping
    {
        public int index1;
        public int index2;
        public float mergedValue;
    }

    /// <summary>
    /// 大数组平滑Job
    /// </summary>
    [System.Serializable]
    public struct BigArraySmoothJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> inputHeightData;
        [WriteOnly] public NativeArray<float> outputHeightData;
        public int width;
        public int height;
        public int smoothRadius;
        public float smoothStrength;
        public bool enableMinHeightSkip;
        public float minHeightThreshold;
        public bool enableMaxHeightSkip;
        public float maxHeightThreshold;

        public void Execute(int index)
        {
            int x = index % width;
            int z = index / width;

            if (inputHeightData[index] == -1f) 
            {
                outputHeightData[index] = -1f; // 保持无效点
                return;
            }

            float originalHeight = inputHeightData[index];
            
            // 检查是否需要跳过平滑处理（基于高度阈值）
            // originalHeight已经是世界单位（米），不需要再乘以terrainHeight
            bool shouldSkip = false;
            
            if (enableMinHeightSkip && originalHeight < minHeightThreshold)
            {
                shouldSkip = true;
            }
            if (enableMaxHeightSkip && originalHeight > maxHeightThreshold)
            {
                shouldSkip = true;
            }
            
            // 如果需要跳过，直接使用原始值
            if (shouldSkip)
            {
                outputHeightData[index] = originalHeight;
                return;
            }
            
            float smoothedHeight = SampleSmoothHeight(x, z);
            outputHeightData[index] = math.lerp(originalHeight, smoothedHeight, smoothStrength);
        }

        private float SampleSmoothHeight(int centerX, int centerZ)
        {
            float totalWeight = 0f;
            float weightedSum = 0f;

            for (int dz = -smoothRadius; dz <= smoothRadius; dz++)
            {
                for (int dx = -smoothRadius; dx <= smoothRadius; dx++)
                {
                    int sampleX = centerX + dx;
                    int sampleZ = centerZ + dz;

                    if (sampleX >= 0 && sampleX < width && 
                        sampleZ >= 0 && sampleZ < height)
                    {
                        int sampleIndex = sampleZ * width + sampleX;
                        if (inputHeightData[sampleIndex] != -1f) // 跳过无效点
                        {
                            float distance = math.sqrt(dx * dx + dz * dz);
                            float weight = distance == 0f ? 2f : 1f / (1f + distance);
                            
                            weightedSum += inputHeightData[sampleIndex] * weight;
                            totalWeight += weight;
                        }
                    }
                }
            }

            return totalWeight > 0f ? weightedSum / totalWeight : inputHeightData[centerZ * width + centerX];
        }
    }
}