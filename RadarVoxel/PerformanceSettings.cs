#if UNITY_EDITOR
using System;
using UnityEngine;

namespace GeoToolkit.RadarVoxel
{
    /// <summary>
    /// 性能配置类 - 控制批次大小和性能参数
    /// </summary>
    [Serializable]
    public class PerformanceSettings
    {
        [Header("批次大小配置")]
        [Tooltip("FOV筛选批次大小（每批处理的体素数量）")]
        [Range(100_000, 10_000_000)]
        public int maxVoxelsPerBatch = 5_000_000;

        [Tooltip("Raycast批次大小（每批处理的射线数量）")]
        [Range(50_000, 2_000_000)]
        public int raycastBatchSize = 500_000;

        [Tooltip("Mesh最大顶点数（单个Mesh的最大顶点数限制）")]
        [Range(1_000_000, 10_000_000)]
        public int maxVerticesPerMesh = 4_000_000;

        [Header("Job并行度配置")]
        [Tooltip("FOV筛选Job的批次大小")]
        [Range(16, 1024)]
        public int fovJobBatchSize = 64;

        [Tooltip("Raycast命令生成Job的批次大小")]
        [Range(256, 4096)]
        public int raycastCommandJobBatchSize = 1024;

        [Tooltip("Raycast执行的批次大小")]
        [Range(16, 256)]
        public int raycastExecutionBatchSize = 64;

        [Tooltip("Mesh数据生成Job的批次大小")]
        [Range(128, 2048)]
        public int meshDataJobBatchSize = 512;

        /// <summary>
        /// 获取默认配置
        /// </summary>
        public static PerformanceSettings Default()
        {
            return new PerformanceSettings
            {
                maxVoxelsPerBatch = 5_000_000,
                raycastBatchSize = 500_000,
                maxVerticesPerMesh = 4_000_000,
                fovJobBatchSize = 64,
                raycastCommandJobBatchSize = 1024,
                raycastExecutionBatchSize = 64,
                meshDataJobBatchSize = 512
            };
        }

        /// <summary>
        /// 快速模式（较小的批次，适合低配置机器）
        /// </summary>
        public static PerformanceSettings Fast()
        {
            return new PerformanceSettings
            {
                maxVoxelsPerBatch = 1_000_000,
                raycastBatchSize = 100_000,
                maxVerticesPerMesh = 1_000_000,
                fovJobBatchSize = 128,
                raycastCommandJobBatchSize = 512,
                raycastExecutionBatchSize = 32,
                meshDataJobBatchSize = 256
            };
        }

        /// <summary>
        /// 高性能模式（较大的批次，适合高配置机器）
        /// </summary>
        public static PerformanceSettings HighPerformance()
        {
            return new PerformanceSettings
            {
                maxVoxelsPerBatch = 10_000_000,
                raycastBatchSize = 1_000_000,
                maxVerticesPerMesh = 8_000_000,
                fovJobBatchSize = 32,
                raycastCommandJobBatchSize = 2048,
                raycastExecutionBatchSize = 128,
                meshDataJobBatchSize = 1024
            };
        }

        /// <summary>
        /// 验证配置有效性
        /// </summary>
        public bool Validate()
        {
            if (maxVoxelsPerBatch <= 0)
            {
                Debug.LogError("FOV筛选批次大小必须大于0");
                return false;
            }

            if (raycastBatchSize <= 0)
            {
                Debug.LogError("Raycast批次大小必须大于0");
                return false;
            }

            if (maxVerticesPerMesh <= 0)
            {
                Debug.LogError("Mesh最大顶点数必须大于0");
                return false;
            }

            if (fovJobBatchSize <= 0 || raycastCommandJobBatchSize <= 0 ||
                raycastExecutionBatchSize <= 0 || meshDataJobBatchSize <= 0)
            {
                Debug.LogError("Job批次大小必须大于0");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 克隆配置
        /// </summary>
        public PerformanceSettings Clone()
        {
            return new PerformanceSettings
            {
                maxVoxelsPerBatch = maxVoxelsPerBatch,
                raycastBatchSize = raycastBatchSize,
                maxVerticesPerMesh = maxVerticesPerMesh,
                fovJobBatchSize = fovJobBatchSize,
                raycastCommandJobBatchSize = raycastCommandJobBatchSize,
                raycastExecutionBatchSize = raycastExecutionBatchSize,
                meshDataJobBatchSize = meshDataJobBatchSize
            };
        }
    }
}
#endif
