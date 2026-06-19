#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;

namespace GeoToolkit.RadarVoxel
{
    /// <summary>
    /// 体素FOV筛选Job - 根据距离和FOV条件筛选有效体素
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct VoxelFOVFilterJob : IJobParallelFor
    {
        [ReadOnly] public float3 startPos;
        [ReadOnly] public float3 voxelSize;
        [ReadOnly] public int gridY;
        [ReadOnly] public int gridZ;
        [ReadOnly] public long batchStartIndex;
        [ReadOnly] public float3 radarPos;
        [ReadOnly] public float3 radarForward;
        [ReadOnly] public float3 radarUp;
        [ReadOnly] public float3 radarRight;
        [ReadOnly] public float minDistance;
        [ReadOnly] public float maxDistance;
        [ReadOnly] public float horizontalFOV;
        [ReadOnly] public float verticalFOV;
        [ReadOnly] public int scanMode; // 0=Sector, 1=Conical, 2=Omnidirectional

        /// <summary>
        /// 输出bit数组 (每个int存储32个体素状态, 位OR操作保证并发安全)
        /// </summary>
        [NativeDisableParallelForRestriction]
        public NativeArray<int> bitArray;

        public void Execute(int index)
        {
            long globalIndex = batchStartIndex + index;

            // 计算xyz坐标
            int x = (int)(globalIndex / (gridY * gridZ));
            int yz = (int)(globalIndex % (gridY * gridZ));
            int y = yz / gridZ;
            int z = yz % gridZ;

            // 计算体素中心位置
            float3 voxelCenter = startPos + new float3(
                x * voxelSize.x + voxelSize.x * 0.5f,
                y * voxelSize.y + voxelSize.y * 0.5f,
                z * voxelSize.z + voxelSize.z * 0.5f
            );

            // 距离检查
            float distance = math.distance(voxelCenter, radarPos);
            if (distance < minDistance || distance > maxDistance)
            {
                return;
            }

            // 方向向量归一化
            float3 toVoxel = voxelCenter - radarPos;
            float toVoxelLength = math.length(toVoxel);
            if (toVoxelLength > 0.0001f)
            {
                toVoxel = toVoxel / toVoxelLength;
            }
            else
            {
                SetBit(index);
                return;
            }

            bool inFOV = false;

            if (scanMode == 2) // Omnidirectional
            {
                inFOV = true;
            }
            else if (scanMode == 0) // Sector
            {
                // 计算俯仰角（垂直角度）- 相对于水平面的角度
                float verticalComponent = math.dot(toVoxel, radarUp);
                float horizontalDistance = math.length(toVoxel - verticalComponent * radarUp);
                float pitchAngle = math.abs(math.degrees(math.atan2(verticalComponent, horizontalDistance)));

                // 检查垂直FOV
                bool verticalInFOV = pitchAngle <= verticalFOV / 2f;

                if (!verticalInFOV)
                {
                    inFOV = false;
                }
                else
                {
                    // 水平角度检查 - 支持0-360度范围
                    float3 horizontalProj = toVoxel - math.dot(toVoxel, radarUp) * radarUp;
                    float horizontalProjLength = math.length(horizontalProj);
                    if (horizontalProjLength > 0.0001f)
                    {
                        horizontalProj = horizontalProj / horizontalProjLength;

                        // 使用atan2计算方位角，得到-180到180度的范围
                        float horizontalX = math.dot(radarForward, horizontalProj);
                        float horizontalY = math.dot(radarRight, horizontalProj);
                        float azimuthAngle = math.degrees(math.atan2(horizontalY, horizontalX));

                        // 判断是否在水平FOV内
                        if (horizontalFOV >= 360f)
                        {
                            // 360度全方位
                            inFOV = true;
                        }
                        else if (horizontalFOV > 180f)
                        {
                            // FOV > 180度，检查是否不在盲区内
                            // 例如FOV=330度，盲区是后方30度
                            float blindZoneHalfAngle = (360f - horizontalFOV) / 2f;
                            // 盲区范围：[180-blindZoneHalfAngle, 180] 和 [-180, -180+blindZoneHalfAngle]
                            float absAngle = math.abs(azimuthAngle);
                            inFOV = absAngle <= (180f - blindZoneHalfAngle);
                        }
                        else
                        {
                            // FOV <= 180度，使用常规判断
                            inFOV = math.abs(azimuthAngle) <= horizontalFOV / 2f;
                        }
                    }
                    else
                    {
                        // 体素在正上方或正下方，只检查垂直FOV
                        inFOV = verticalInFOV;
                    }
                }
            }
            else if (scanMode == 1) // Conical
            {
                float dot = math.dot(radarForward, toVoxel);
                dot = math.clamp(dot, -1f, 1f);
                float angle = math.degrees(math.acos(dot));
                float maxAngle = math.min(horizontalFOV, verticalFOV) / 2f;
                inFOV = angle <= maxAngle;
            }

            if (inFOV)
            {
                SetBit(index);
            }
        }

        private void SetBit(int index)
        {
            int intIndex = index / 32;
            int bitIndex = index % 32;
            int mask = 1 << bitIndex;
            bitArray[intIndex] |= mask;
        }
    }

    /// <summary>
    /// 构建稀疏体素索引Job - 从bitmap构建有效体素坐标数组
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct BuildSparseIndexJob : IJob
    {
        [ReadOnly] public NativeArray<int> existenceBits;
        [ReadOnly] public int gridY, gridZ;

        [WriteOnly] public NativeArray<int3> sparseVoxelCoords;

        public void Execute()
        {
            int writeIndex = 0;
            int totalInts = existenceBits.Length;

            for (int intIndex = 0; intIndex < totalInts; intIndex++)
            {
                int bits = existenceBits[intIndex];
                if (bits == 0) continue; // 跳过全0的int

                // 遍历这个int的32个bit
                for (int bitOffset = 0; bitOffset < 32; bitOffset++)
                {
                    int mask = 1 << bitOffset;
                    if ((bits & mask) != 0)
                    {
                        // 计算全局索引和xyz坐标
                        long globalIndex = (long)intIndex * 32 + bitOffset;
                        int x = (int)(globalIndex / (gridY * gridZ));
                        int yz = (int)(globalIndex % (gridY * gridZ));
                        int y = yz / gridZ;
                        int z = yz % gridZ;

                        sparseVoxelCoords[writeIndex++] = new int3(x, y, z);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 并行生成RaycastCommand Job
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct GenerateRaycastCommandJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int3> sparseVoxelCoords;
        [ReadOnly] public float3 startPos;
        [ReadOnly] public float3 voxelSize;
        [ReadOnly] public float3 radarPos;

        [WriteOnly] public NativeArray<RaycastCommand> commands;

        public void Execute(int index)
        {
            int3 coord = sparseVoxelCoords[index];

            // 计算体素中心
            float3 voxelCenter = startPos + new float3(
                coord.x * voxelSize.x + voxelSize.x * 0.5f,
                coord.y * voxelSize.y + voxelSize.y * 0.5f,
                coord.z * voxelSize.z + voxelSize.z * 0.5f
            );

            float distance = math.distance(voxelCenter, radarPos);
            float3 direction = math.normalize(voxelCenter - radarPos);

            // 使用新的QueryParameters API
            var queryParameters = new QueryParameters
            {
                layerMask = -1,
                hitMultipleFaces = false,
                hitTriggers = QueryTriggerInteraction.UseGlobal,
                hitBackfaces = false
            };

            commands[index] = new RaycastCommand(
                radarPos,
                direction,
                queryParameters,
                distance * 1.5f
            );
        }
    }

    /// <summary>
    /// 体素Mesh数据 - 存储单个体素的mesh生成信息
    /// </summary>
    public struct VoxelMeshData
    {
        public float3 center;           // 体素中心（局部坐标）
        public byte neighborMask;       // 6个bit表示6个方向的邻居 [Front, Back, Top, Bottom, Right, Left]
        public byte voxelType;          // 0=Empty, 1=Obstacle
        public byte isValid;            // 是否有效（0=无效，1=有效）
    }

    /// <summary>
    /// 体素Mesh数据生成Job - 并行计算每个体素的mesh信息
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct VoxelMeshDataJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int3> voxelCoords;  // 体素坐标
        [ReadOnly] public float3 startPos;
        [ReadOnly] public float3 voxelSize;
        [ReadOnly] public float3 radarPos;
        [ReadOnly] public int gridX, gridY, gridZ;
        [ReadOnly] public bool showOccluded;

        // BitMap数据（只读访问）
        [ReadOnly] public NativeArray<int> existenceBits;
        [ReadOnly] public NativeArray<int> occlusionBits;

        [WriteOnly] public NativeArray<VoxelMeshData> outputs;

        public void Execute(int index)
        {
            int3 coord = voxelCoords[index];
            int x = coord.x;
            int y = coord.y;
            int z = coord.z;

            // 检查是否在FOV内
            if (!GetBit(existenceBits, x, y, z))
            {
                outputs[index] = new VoxelMeshData { isValid = 0 };
                return;
            }

            bool isOccluded = GetBit(occlusionBits, x, y, z);
            if (!showOccluded && isOccluded)
            {
                outputs[index] = new VoxelMeshData { isValid = 0 };
                return;
            }

            // 计算体素中心位置
            float3 voxelCenterWorld = startPos + new float3(
                x * voxelSize.x + voxelSize.x * 0.5f,
                y * voxelSize.y + voxelSize.y * 0.5f,
                z * voxelSize.z + voxelSize.z * 0.5f
            );
            float3 voxelCenter = voxelCenterWorld - radarPos;

            byte voxelType = (byte)(isOccluded ? 1 : 0);

            // 检查6个方向的邻居，生成neighborMask
            byte neighborMask = 0;

            // Front (+Z) - bit 0
            if (CheckNeighbor(x, y, z + 1, voxelType))
                neighborMask |= 1;

            // Back (-Z) - bit 1
            if (CheckNeighbor(x, y, z - 1, voxelType))
                neighborMask |= 2;

            // Top (+Y) - bit 2
            if (CheckNeighbor(x, y + 1, z, voxelType))
                neighborMask |= 4;

            // Bottom (-Y) - bit 3
            if (CheckNeighbor(x, y - 1, z, voxelType))
                neighborMask |= 8;

            // Right (+X) - bit 4
            if (CheckNeighbor(x + 1, y, z, voxelType))
                neighborMask |= 16;

            // Left (-X) - bit 5
            if (CheckNeighbor(x - 1, y, z, voxelType))
                neighborMask |= 32;

            outputs[index] = new VoxelMeshData
            {
                center = voxelCenter,
                neighborMask = neighborMask,
                voxelType = voxelType,
                isValid = 1
            };
        }

        private bool CheckNeighbor(int x, int y, int z, byte currentVoxelType)
        {
            if (!GetBit(existenceBits, x, y, z))
                return false;

            bool neighborOccluded = GetBit(occlusionBits, x, y, z);
            byte neighborType = (byte)(neighborOccluded ? 1 : 0);
            return neighborType == currentVoxelType;
        }

        private bool GetBit(NativeArray<int> bits, int x, int y, int z)
        {
            if (x < 0 || x >= gridX || y < 0 || y >= gridY || z < 0 || z >= gridZ)
                return false;

            long index = (long)x * gridY * gridZ + (long)y * gridZ + z;
            int intIndex = (int)(index / 32);  // 每32个bit一个int
            int bitIndex = (int)(index % 32);   // bit在int中的位置

            if (intIndex >= bits.Length)
                return false;

            int mask = 1 << bitIndex;
            return (bits[intIndex] & mask) != 0;
        }
    }

    /// <summary>
    /// 雷达体素管理器
    /// </summary>
    public static class RadarVoxelManager
    {
        /// <summary>
        /// 流式生成雷达体素 (使用bitmap和Job系统优化性能和内存)
        /// </summary>
        public static GameObject GenerateRadarVoxelsStreaming(
            RadarParameters radarParams,
            VoxelSettings voxelSettings,
            PerformanceSettings performanceSettings,
            bool showOccluded,
            bool generateColliders,
            bool enableCollisionDetection,
            Material spaceMaterial,
            Material obstacleMaterial,
            string customName = null)
        {
            VoxelBitMap existenceBitmap = null;
            VoxelBitMap occlusionBitmap = null;

            try
            {
                UpdateProgress("初始化", 0.05f);

                Bounds radarBounds = CalculateRadarBounds(radarParams);
                Vector3 voxelSize = voxelSettings.GetVoxelSize();
                Vector3 startPos = radarBounds.min;
                int gridX = Mathf.CeilToInt(radarBounds.size.x / voxelSize.x);
                int gridY = Mathf.CeilToInt(radarBounds.size.y / voxelSize.y);
                int gridZ = Mathf.CeilToInt(radarBounds.size.z / voxelSize.z);
                long totalVoxels = (long)gridX * gridY * gridZ;

                Debug.Log($"[RadarVoxel] 开始生成 | 网格:{gridX}×{gridY}×{gridZ} ({totalVoxels:N0}个) | " +
                         $"体素:{voxelSize.x}×{voxelSize.y}×{voxelSize.z}m | " +
                         $"距离:{radarParams.minDistance}-{radarParams.maxDistance}m");

                string baseName = string.IsNullOrEmpty(customName) ? "RadarVoxels" : customName;
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string objectName = $"{baseName}_{timestamp}";
                GameObject root = new GameObject(objectName);
                root.transform.position = radarParams.position;
                Dictionary<VoxelType, VoxelMeshBuilder> meshBuilders = new Dictionary<VoxelType, VoxelMeshBuilder>();
                Dictionary<VoxelType, Material> materialMap = new Dictionary<VoxelType, Material>
                {
                    { VoxelType.Empty, spaceMaterial },
                    { VoxelType.Obstacle, obstacleMaterial }
                };
                Dictionary<VoxelType, List<Mesh>> savedMeshes = new Dictionary<VoxelType, List<Mesh>>();

                // Unity Mesh最大顶点数限制 (从性能配置读取)
                int maxVerticesPerMesh = performanceSettings.maxVerticesPerMesh;

                foreach (VoxelType type in Enum.GetValues(typeof(VoxelType)))
                {
                    meshBuilders[type] = new VoxelMeshBuilder(maxVerticesPerMesh);
                    savedMeshes[type] = new List<Mesh>();
                }

                UpdateProgress("阶段1: FOV筛选", 0.15f);

                existenceBitmap = new VoxelBitMap(gridX, gridY, gridZ);
                occlusionBitmap = new VoxelBitMap(gridX, gridY, gridZ);

                // 预计算雷达方向向量和扫描模式
                Quaternion radarRotation = radarParams.GetRotation();
                Vector3 radarForward = radarRotation * Vector3.forward;
                Vector3 radarUp = radarRotation * Vector3.up;
                Vector3 radarRight = radarRotation * Vector3.right;
                int scanModeInt = radarParams.scanMode == RadarParameters.ScanMode.Sector ? 0 :
                                  radarParams.scanMode == RadarParameters.ScanMode.Conical ? 1 : 2;

                long totalInFOV = 0;

                // FOV筛选批次大小 (从性能配置读取)
                long maxVoxelsPerBatch = performanceSettings.maxVoxelsPerBatch;
                int numBatches = (int)((totalVoxels + maxVoxelsPerBatch - 1) / maxVoxelsPerBatch);

                for (int batchIndex = 0; batchIndex < numBatches; batchIndex++)
                {
                    long batchStart = batchIndex * maxVoxelsPerBatch;
                    long batchEnd = Math.Min(batchStart + maxVoxelsPerBatch, totalVoxels);
                    int batchSize = (int)(batchEnd - batchStart);

                    float batchProgress = 0.15f + 0.35f * (batchIndex / (float)numBatches);
                    UpdateProgress($"阶段1: FOV筛选 (批次 {batchIndex + 1}/{numBatches})", batchProgress);

                    int bitArraySize = (batchSize + 31) / 32;
                    using (NativeArray<int> bitArray = new NativeArray<int>(bitArraySize, Allocator.TempJob))
                    {
                        var fovJob = new VoxelFOVFilterJob
                        {
                            startPos = startPos,
                            voxelSize = voxelSize,
                            gridY = gridY,
                            gridZ = gridZ,
                            batchStartIndex = batchStart,
                            radarPos = radarParams.position,
                            radarForward = radarForward,
                            radarUp = radarUp,
                            radarRight = radarRight,
                            minDistance = radarParams.minDistance,
                            maxDistance = radarParams.maxDistance,
                            horizontalFOV = radarParams.horizontalFOV,
                            verticalFOV = radarParams.verticalFOV,
                            scanMode = scanModeInt,
                            bitArray = bitArray
                        };

                        JobHandle jobHandle = fovJob.Schedule(batchSize, performanceSettings.fovJobBatchSize);
                        jobHandle.Complete();

                        // 读取结果并写入BitMap
                        for (int i = 0; i < batchSize; i++)
                        {
                            int intIndex = i / 32;
                            int bitIndex = i % 32;
                            int mask = 1 << bitIndex;

                            if ((bitArray[intIndex] & mask) != 0)
                            {
                                long globalIndex = batchStart + i;
                                int x = (int)(globalIndex / (gridY * gridZ));
                                int yz = (int)(globalIndex % (gridY * gridZ));
                                int y = yz / gridZ;
                                int z = yz % gridZ;

                                existenceBitmap.SetBit(x, y, z, true);
                                totalInFOV++;
                            }
                        }
                    }
                }
                // 构建稀疏体素坐标数组 (用于阶段2遮挡检测 + 阶段3 Mesh生成)
                NativeArray<int3> sparseVoxelCoords = default;

                if (totalInFOV > 0)
                {
                    UpdateProgress($"阶段2: 构建稀疏索引 ({totalInFOV:N0}个体素)", 0.52f);

                    // 使用Job快速构建稀疏体素坐标数组
                    sparseVoxelCoords = new NativeArray<int3>((int)totalInFOV, Allocator.TempJob);

                    var buildIndexJob = new BuildSparseIndexJob
                    {
                        existenceBits = existenceBitmap.GetNativeArray(),
                        gridY = gridY,
                        gridZ = gridZ,
                        sparseVoxelCoords = sparseVoxelCoords
                    };

                    JobHandle buildHandle = buildIndexJob.Schedule();
                    buildHandle.Complete();
                }

                if (enableCollisionDetection && totalInFOV > 0)
                {
                    UpdateProgress($"阶段2: 遮挡检测 - 批量Raycast ({totalInFOV:N0}个)", 0.53f);

                    // 步骤2: 使用批量RaycastCommand并行处理 (从性能配置读取批次大小)
                    int raycastBatchSize = performanceSettings.raycastBatchSize;
                    int totalBatches = (int)((totalInFOV + raycastBatchSize - 1) / raycastBatchSize);
                    long occludedCount = 0;

                    for (int batchIdx = 0; batchIdx < totalBatches; batchIdx++)
                    {
                        int batchStart = batchIdx * raycastBatchSize;
                        int batchCount = Math.Min(raycastBatchSize, (int)totalInFOV - batchStart);

                        // 使用GetSubArray创建零拷贝视图
                        NativeArray<int3> batchCoords = sparseVoxelCoords.GetSubArray(batchStart, batchCount);

                        NativeArray<RaycastCommand> commands = new NativeArray<RaycastCommand>(batchCount, Allocator.TempJob);
                        NativeArray<RaycastHit> results = new NativeArray<RaycastHit>(batchCount, Allocator.TempJob);

                        try
                        {
                            // 并行生成RaycastCommand
                            var generateJob = new GenerateRaycastCommandJob
                            {
                                sparseVoxelCoords = batchCoords,
                                startPos = startPos,
                                voxelSize = voxelSize,
                                radarPos = radarParams.position,
                                commands = commands
                            };

                            JobHandle generateHandle = generateJob.Schedule(batchCount, performanceSettings.raycastCommandJobBatchSize);
                            generateHandle.Complete();

                            // 批量执行Raycast
                            JobHandle raycastHandle = RaycastCommand.ScheduleBatch(commands, results, performanceSettings.raycastExecutionBatchSize);
                            raycastHandle.Complete();

                            // 读取结果并更新occlusionBitmap
                            for (int i = 0; i < batchCount; i++)
                            {
                                if (results[i].collider != null)
                                {
                                    int3 coord = batchCoords[i];
                                    occlusionBitmap.SetBit(coord.x, coord.y, coord.z, true);
                                    occludedCount++;
                                }
                            }
                        }
                        finally
                        {
                            // batchCoords 是视图，不需要 Dispose
                            if (commands.IsCreated) commands.Dispose();
                            if (results.IsCreated) results.Dispose();
                        }

                        float progress = 0.53f + 0.07f * ((batchIdx + 1) / (float)totalBatches);
                        UpdateProgress($"阶段2: 遮挡检测 (批次 {batchIdx + 1}/{totalBatches}, 已遮挡:{occludedCount:N0})", progress);
                    }

                    Debug.Log($"[RadarVoxel] FOV筛选: {totalInFOV:N0}个 | 遮挡检测: {occludedCount:N0}个被遮挡 (使用Job+批量Raycast)");
                }
                else if (totalInFOV > 0)
                {
                    Debug.Log($"[RadarVoxel] FOV筛选: {totalInFOV:N0}个 (未启用遮挡检测)");
                }

                UpdateProgress($"阶段3: 生成Mesh - 准备数据", 0.60f);

                // 直接使用阶段2构建的稀疏坐标数组,无需重新遍历整个grid!
                int totalVoxelsToProcess = (int)totalInFOV;

                long totalVoxelsGenerated = 0;
                long totalFacesCulled = 0;
                long totalFacesGenerated = 0;

                if (totalVoxelsToProcess > 0)
                {
                    UpdateProgress($"阶段3: 生成Mesh - 并行计算 ({totalVoxelsToProcess:N0}个体素)", 0.62f);

                    // 使用阶段2的sparseVoxelCoords (已经包含所有FOV内的体素坐标)
                    NativeArray<VoxelMeshData> meshDataArray = new NativeArray<VoxelMeshData>(totalVoxelsToProcess, Allocator.TempJob);
                    NativeArray<int> existenceBits = existenceBitmap.GetNativeArray();
                    NativeArray<int> occlusionBits = occlusionBitmap.GetNativeArray();

                    try
                    {
                        var meshDataJob = new VoxelMeshDataJob
                        {
                            voxelCoords = sparseVoxelCoords,
                            startPos = startPos,
                            voxelSize = voxelSize,
                            radarPos = radarParams.position,
                            gridX = gridX,
                            gridY = gridY,
                            gridZ = gridZ,
                            showOccluded = showOccluded,
                            existenceBits = existenceBits,
                            occlusionBits = occlusionBits,
                            outputs = meshDataArray
                        };

                        JobHandle jobHandle = meshDataJob.Schedule(totalVoxelsToProcess, performanceSettings.meshDataJobBatchSize);
                        jobHandle.Complete();

                        UpdateProgress($"阶段3: 生成Mesh - 构建对象", 0.70f);

                        // 读取Job结果并构建Mesh
                        int validCount = 0;
                        for (int i = 0; i < totalVoxelsToProcess; i++)
                        {
                            if (meshDataArray[i].isValid == 1) validCount++;
                        }
                        Debug.Log($"[RadarVoxel] Job计算完成 | 有效体素: {validCount:N0}/{totalVoxelsToProcess:N0}");

                        for (int i = 0; i < totalVoxelsToProcess; i++)
                        {
                            VoxelMeshData data = meshDataArray[i];

                            if (data.isValid == 0)
                                continue;

                            // 从neighborMask恢复hasNeighbors数组
                            bool[] hasNeighbors = new bool[6];
                            for (int j = 0; j < 6; j++)
                            {
                                hasNeighbors[j] = (data.neighborMask & (1 << j)) != 0;
                            }

                            // 统计面剔除信息
                            int culledFaces = 0;
                            for (int j = 0; j < 6; j++)
                            {
                                if (hasNeighbors[j]) culledFaces++;
                            }
                            totalFacesCulled += culledFaces;
                            totalFacesGenerated += (6 - culledFaces);

                            // 转换类型
                            VoxelType voxelType = data.voxelType == 0 ? VoxelType.Empty : VoxelType.Obstacle;
                            Vector3 center = new Vector3(data.center.x, data.center.y, data.center.z);

                            // 添加到MeshBuilder
                            VoxelMeshBuilder builder = meshBuilders[voxelType];
                            bool added = builder.AddVoxelCubeWithCulling(center, voxelSize, hasNeighbors);

                            if (!added)
                            {
                                Mesh mesh = builder.BuildAndClear();
                                if (mesh != null)
                                {
                                    savedMeshes[voxelType].Add(mesh);
                                }

                                builder.AddVoxelCubeWithCulling(center, voxelSize, hasNeighbors);
                            }

                            totalVoxelsGenerated++;

                            // 更新进度
                            if (i % 5000 == 0)
                            {
                                float progress = 0.70f + 0.15f * (i / (float)totalVoxelsToProcess);
                                UpdateProgress($"阶段3: 生成Mesh - 构建中 ({totalVoxelsGenerated:N0}/{totalVoxelsToProcess:N0})", progress);
                            }
                        }
                    }
                    finally
                    {
                        // 清理临时数据
                        if (meshDataArray.IsCreated) meshDataArray.Dispose();
                    }
                }

                // 清理sparseVoxelCoords (无论是否处理了体素都要清理)
                if (sparseVoxelCoords.IsCreated) sparseVoxelCoords.Dispose();

                float cullRate = totalFacesCulled * 100.0f / (totalFacesCulled + totalFacesGenerated);
                Debug.Log($"[RadarVoxel] Mesh生成完成 | 体素:{totalVoxelsGenerated:N0}个 | " +
                         $"面:{totalFacesGenerated:N0}个 | 剔除率:{cullRate:F1}%");

                UpdateProgress("阶段4: 保存Mesh资源", 0.85f);
                foreach (var kvp in meshBuilders)
                {
                    VoxelType type = kvp.Key;
                    VoxelMeshBuilder builder = kvp.Value;

                    if (builder.VertexCount > 0)
                    {
                        Mesh mesh = builder.BuildAndClear();
                        if (mesh != null)
                        {
                            savedMeshes[type].Add(mesh);
                        }
                    }
                }

                UpdateProgress("阶段5: 创建GameObject", 0.90f);
                int totalMeshCount = 0;

                foreach (var kvp in savedMeshes)
                {
                    VoxelType type = kvp.Key;
                    List<Mesh> meshes = kvp.Value;

                    if (meshes.Count == 0)
                        continue;

                    GameObject typeParent = new GameObject($"{type}Voxels");
                    typeParent.transform.SetParent(root.transform, false);

                    Material material = materialMap[type];

                    for (int i = 0; i < meshes.Count; i++)
                    {
                        GameObject meshObj = new GameObject($"{type}_Mesh_{i}");
                        meshObj.transform.SetParent(typeParent.transform, false);

                        MeshFilter mf = meshObj.AddComponent<MeshFilter>();
                        mf.sharedMesh = meshes[i];

                        MeshRenderer mr = meshObj.AddComponent<MeshRenderer>();
                        mr.sharedMaterial = material;
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        mr.receiveShadows = false;

                        if (generateColliders)
                        {
                            MeshCollider mc = meshObj.AddComponent<MeshCollider>();
                            mc.sharedMesh = meshes[i];
                        }

                        totalMeshCount++;
                    }
                }

                UpdateProgress("完成", 1.0f);

                int emptyMeshCount = savedMeshes[VoxelType.Empty].Count;
                int obstacleMeshCount = savedMeshes[VoxelType.Obstacle].Count;
                Debug.Log($"[RadarVoxel] 生成完成 | 体素:{totalVoxelsGenerated:N0}/{totalVoxels:N0} | " +
                         $"Mesh:{totalMeshCount}个 (空间:{emptyMeshCount} 障碍:{obstacleMeshCount})");

                // 清理资源
                existenceBitmap?.Dispose();
                occlusionBitmap?.Dispose();
                meshBuilders.Clear();
                savedMeshes.Clear();
                GC.Collect();
                GC.WaitForPendingFinalizers();

                return root;
            }
            catch (Exception ex)
            {
                Debug.LogError($"生成雷达体素失败: {ex}");
                existenceBitmap?.Dispose();
                occlusionBitmap?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 计算雷达探测范围边界
        /// </summary>
        private static Bounds CalculateRadarBounds(RadarParameters radarParams)
        {
            Vector3 center = radarParams.position;
            float maxRange = radarParams.maxDistance;

            switch (radarParams.scanMode)
            {
                case RadarParameters.ScanMode.Omnidirectional:
                    return new Bounds(center, Vector3.one * maxRange * 2);

                case RadarParameters.ScanMode.Conical:
                case RadarParameters.ScanMode.Sector:
                default:
                    // 使用球形边界,具体范围由Job中的FOV筛选
                    return new Bounds(center, Vector3.one * maxRange * 2);
            }
        }

        private static void UpdateProgress(string info, float progress)
        {
            string progressText = $"{info} ({progress * 100:F1}%)";
            if (EditorUtility.DisplayCancelableProgressBar("生成雷达体素", progressText, progress))
            {
                EditorUtility.ClearProgressBar();
                throw new OperationCanceledException("用户取消操作");
            }
        }

        private static void EnsureDirectoryExists(string assetPath)
        {
            string[] folders = assetPath.Split('/');
            string currentPath = folders[0];

            for (int i = 1; i < folders.Length; i++)
            {
                string nextPath = currentPath + "/" + folders[i];

                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }

                currentPath = nextPath;
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 保存GameObject为预制体和Mesh资源
        /// </summary>
        public static string SaveAsPrefab(GameObject radarVoxelObject, string savePath, string basePrefabName)
        {
            try
            {
                string gameObjectName = radarVoxelObject.name;

                // 转换为相对于Assets的路径
                string assetPath = savePath;
                if (assetPath.StartsWith(Application.dataPath))
                {
                    assetPath = "Assets" + assetPath.Substring(Application.dataPath.Length);
                }
                assetPath = assetPath.Replace("\\", "/");

                EnsureDirectoryExists(assetPath);

                // 检查子目录是否存在,如果存在则添加数字后缀
                string finalDirectoryName = gameObjectName;
                string radarSubDirectory = Path.Combine(assetPath, finalDirectoryName).Replace("\\", "/");
                int counter = 1;

                while (AssetDatabase.IsValidFolder(radarSubDirectory))
                {
                    finalDirectoryName = $"{gameObjectName}_{counter}";
                    radarSubDirectory = Path.Combine(assetPath, finalDirectoryName).Replace("\\", "/");
                    counter++;
                }

                EnsureDirectoryExists(radarSubDirectory);

                string meshDirectory = Path.Combine(radarSubDirectory, "Meshes").Replace("\\", "/");
                EnsureDirectoryExists(meshDirectory);

                string finalPrefabName = finalDirectoryName;
                string prefabPath = Path.Combine(radarSubDirectory, $"{finalPrefabName}.prefab").Replace("\\", "/");

                // 保存所有Mesh资源
                MeshFilter[] meshFilters = radarVoxelObject.GetComponentsInChildren<MeshFilter>();
                List<string> meshPaths = new List<string>();

                for (int i = 0; i < meshFilters.Length; i++)
                {
                    MeshFilter meshFilter = meshFilters[i];
                    if (meshFilter.sharedMesh != null)
                    {
                        string meshName = $"{finalPrefabName}_Mesh_{i}";
                        string meshPath = Path.Combine(meshDirectory, $"{meshName}.asset").Replace("\\", "/");

                        if (File.Exists(meshPath))
                        {
                            AssetDatabase.DeleteAsset(meshPath);
                        }

                        Mesh meshCopy = UnityEngine.Object.Instantiate(meshFilter.sharedMesh);
                        meshCopy.name = meshName;

                        AssetDatabase.CreateAsset(meshCopy, meshPath);
                        meshPaths.Add(meshPath);
                    }
                    else
                    {
                        meshPaths.Add(null);
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // 更新 MeshFilter 和 MeshCollider 的 Mesh 引用为保存的资产
                for (int i = 0; i < meshFilters.Length; i++)
                {
                    if (!string.IsNullOrEmpty(meshPaths[i]))
                    {
                        Mesh loadedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPaths[i]);
                        if (loadedMesh != null)
                        {
                            // 更新 MeshFilter
                            meshFilters[i].sharedMesh = loadedMesh;

                            // 同时更新同一对象上的 MeshCollider（如果有）
                            MeshCollider meshCollider = meshFilters[i].GetComponent<MeshCollider>();
                            if (meshCollider != null)
                            {
                                meshCollider.sharedMesh = loadedMesh;
                            }
                        }
                        else
                        {
                            Debug.LogError($"[RadarVoxel] 无法加载Mesh: {meshPaths[i]}");
                        }
                    }
                }

                radarVoxelObject.name = finalPrefabName;

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
                    radarVoxelObject,
                    prefabPath,
                    InteractionMode.AutomatedAction);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"[RadarVoxel] 预制体已保存 | 路径: {prefabPath} | Mesh: {meshPaths.Count}个");
                return prefabPath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RadarVoxel] 保存预制体失败: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        #region 体素BitMap

        /// <summary>
        /// 3D体素位图 - 使用NativeArray高效存储体素状态
        /// </summary>
        private class VoxelBitMap : IDisposable
        {
            private NativeArray<int> bits;  // 每个int存32个bit
            private int gridX, gridY, gridZ;
            private long totalVoxels;
            private bool disposed = false;

            public VoxelBitMap(int gridX, int gridY, int gridZ)
            {
                this.gridX = gridX;
                this.gridY = gridY;
                this.gridZ = gridZ;
                this.totalVoxels = (long)gridX * gridY * gridZ;

                int intCount = (int)((totalVoxels + 31) / 32); // 每32个bit一个int
                bits = new NativeArray<int>(intCount, Allocator.Persistent);
            }

            private long GetIndex(int x, int y, int z)
            {
                return (long)x * gridY * gridZ + (long)y * gridZ + z;
            }

            private bool IsValidCoordinate(int x, int y, int z)
            {
                return x >= 0 && x < gridX && y >= 0 && y < gridY && z >= 0 && z < gridZ;
            }

            public void SetBit(int x, int y, int z, bool value)
            {
                if (!IsValidCoordinate(x, y, z)) return;

                long index = GetIndex(x, y, z);
                int intIndex = (int)(index / 32);
                int bitIndex = (int)(index % 32);

                if (intIndex >= bits.Length) return;

                int mask = 1 << bitIndex;
                if (value)
                    bits[intIndex] |= mask;
                else
                    bits[intIndex] &= ~mask;
            }

            public bool GetBit(int x, int y, int z)
            {
                if (!IsValidCoordinate(x, y, z)) return false;

                long index = GetIndex(x, y, z);
                int intIndex = (int)(index / 32);
                int bitIndex = (int)(index % 32);

                if (intIndex >= bits.Length) return false;

                int mask = 1 << bitIndex;
                return (bits[intIndex] & mask) != 0;
            }

            public NativeArray<int> GetNativeArray()
            {
                return bits;
            }

            public void Dispose()
            {
                if (disposed) return;

                if (bits.IsCreated)
                {
                    bits.Dispose();
                }

                disposed = true;
            }
        }

        #endregion

        #region 流式Mesh构建器

        /// <summary>
        /// 流式Mesh构建器 - 累积顶点数据并支持批量构建
        /// </summary>
        private class VoxelMeshBuilder
        {
            private List<Vector3> vertices;
            private List<int> triangles;
            private List<Vector3> normals;
            private List<Vector2> uvs;

            private int currentVertexCount = 0;
            public int VertexCount => currentVertexCount;
            public int MaxVertexCount { get; private set; }

            public VoxelMeshBuilder(int maxVertexCount = 4_000_000)
            {
                MaxVertexCount = maxVertexCount;

                // 预分配初始容量,避免频繁扩容 (预估10万个顶点)
                const int INITIAL_VERTEX_CAPACITY = 100_000;
                int initialCapacity = Math.Min(INITIAL_VERTEX_CAPACITY, maxVertexCount);
                vertices = new List<Vector3>(initialCapacity);
                triangles = new List<int>(initialCapacity * 3 / 2);
                normals = new List<Vector3>(initialCapacity);
                uvs = new List<Vector2>(initialCapacity);
            }

            /// <summary>
            /// 添加体素立方体 (使用面剔除优化)
            /// </summary>
            public bool AddVoxelCubeWithCulling(Vector3 center, Vector3 size, bool[] hasNeighbors)
            {
                // 计算需要生成的面数
                int facesToGenerate = 0;
                for (int i = 0; i < 6; i++)
                {
                    if (!hasNeighbors[i]) facesToGenerate++;
                }

                if (facesToGenerate == 0) return true;

                int verticesNeeded = facesToGenerate * 4;
                if (currentVertexCount + verticesNeeded > MaxVertexCount)
                {
                    return false;
                }

                Vector3 halfSize = size * 0.5f;

                // Front face (Z+)
                if (!hasNeighbors[0])
                {
                    AddQuad(
                        center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z),
                        center + new Vector3(halfSize.x, -halfSize.y, halfSize.z),
                        center + new Vector3(halfSize.x, halfSize.y, halfSize.z),
                        center + new Vector3(-halfSize.x, halfSize.y, halfSize.z),
                        Vector3.forward, currentVertexCount);
                    currentVertexCount += 4;
                }

                // Back face (Z-)
                if (!hasNeighbors[1])
                {
                    AddQuad(
                        center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z),
                        center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z),
                        center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z),
                        center + new Vector3(halfSize.x, halfSize.y, -halfSize.z),
                        Vector3.back, currentVertexCount);
                    currentVertexCount += 4;
                }

                // Top face (Y+)
                if (!hasNeighbors[2])
                {
                    AddQuad(
                        center + new Vector3(-halfSize.x, halfSize.y, halfSize.z),
                        center + new Vector3(halfSize.x, halfSize.y, halfSize.z),
                        center + new Vector3(halfSize.x, halfSize.y, -halfSize.z),
                        center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z),
                        Vector3.up, currentVertexCount);
                    currentVertexCount += 4;
                }

                // Bottom face (Y-)
                if (!hasNeighbors[3])
                {
                    AddQuad(
                        center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z),
                        center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z),
                        center + new Vector3(halfSize.x, -halfSize.y, halfSize.z),
                        center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z),
                        Vector3.down, currentVertexCount);
                    currentVertexCount += 4;
                }

                // Right face (X+)
                if (!hasNeighbors[4])
                {
                    AddQuad(
                        center + new Vector3(halfSize.x, -halfSize.y, halfSize.z),
                        center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z),
                        center + new Vector3(halfSize.x, halfSize.y, -halfSize.z),
                        center + new Vector3(halfSize.x, halfSize.y, halfSize.z),
                        Vector3.right, currentVertexCount);
                    currentVertexCount += 4;
                }

                // Left face (X-)
                if (!hasNeighbors[5])
                {
                    AddQuad(
                        center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z),
                        center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z),
                        center + new Vector3(-halfSize.x, halfSize.y, halfSize.z),
                        center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z),
                        Vector3.left, currentVertexCount);
                    currentVertexCount += 4;
                }

                return true;
            }

            private void AddQuad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
                                  Vector3 normal, int startVertex)
            {
                vertices.Add(v0);
                vertices.Add(v1);
                vertices.Add(v2);
                vertices.Add(v3);

                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);

                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1));
                uvs.Add(new Vector2(0, 1));

                triangles.Add(startVertex + 0);
                triangles.Add(startVertex + 1);
                triangles.Add(startVertex + 2);

                triangles.Add(startVertex + 0);
                triangles.Add(startVertex + 2);
                triangles.Add(startVertex + 3);
            }

            public Mesh BuildAndClear()
            {
                if (currentVertexCount == 0)
                {
                    return null;
                }

                Mesh mesh = new Mesh();
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);

                Clear();

                return mesh;
            }

            public void Clear()
            {
                vertices.Clear();
                triangles.Clear();
                normals.Clear();
                uvs.Clear();
                currentVertexCount = 0;
            }

        }

        #endregion
    }
}
#endif
