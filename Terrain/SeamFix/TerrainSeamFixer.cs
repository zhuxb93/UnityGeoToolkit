using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace GeoToolkit.SeamFix
{
    /// <summary>
    /// 地形缝隙修复算法类
    /// </summary>
    public class TerrainSeamFixer
    {
        private readonly float seamThreshold;
        private readonly bool fixVerticalSeams;
        private readonly bool fixHorizontalSeams;
        private readonly bool fixCornerPoints;
        private readonly int blendWidth;
        private readonly AnimationCurve blendCurve;

        public TerrainSeamFixer(float seamThreshold, bool fixVerticalSeams, bool fixHorizontalSeams,
            bool fixCornerPoints, int blendWidth, AnimationCurve blendCurve)
        {
            this.seamThreshold = seamThreshold;
            this.fixVerticalSeams = fixVerticalSeams;
            this.fixHorizontalSeams = fixHorizontalSeams;
            this.fixCornerPoints = fixCornerPoints;
            this.blendWidth = blendWidth;
            this.blendCurve = blendCurve;
        }

        /// <summary>
        /// 异步修复缝隙列表 - 两阶段策略：信息收集 + 统一调整
        /// </summary>
        public async Task<int> FixSeamsAsync(List<TerrainSeamInfo> seamInfoList, HashSet<Terrain> affectedTerrains)
        {
            if (seamInfoList == null || seamInfoList.Count == 0) return 0;

            try
            {
                // 阶段1：信息收集 - 计算每个地形需要的最大高度
                EditorUtility.DisplayProgressBar("修复地形缝隙", "阶段1：收集高度需求信息...", 0.1f);
                var heightRequirements = await CollectHeightRequirementsAsync(seamInfoList);


                // 阶段2：统一调整地形参数和像素值
                EditorUtility.DisplayProgressBar("修复地形缝隙", "阶段2：统一调整地形参数...", 0.3f);
                var adjustedTerrains = await AdjustTerrainHeightParametersAsync(heightRequirements);

                // 阶段3：在统一范围下修复拼缝
                EditorUtility.DisplayProgressBar("修复地形缝隙", "阶段3：修复拼缝...", 0.7f);
                int fixedCount = await PerformSeamFixWithUnifiedRangeAsync(seamInfoList, adjustedTerrains);

                // 阶段4：修复角点（如果启用）
                if (fixCornerPoints)
                {
                    EditorUtility.DisplayProgressBar("修复地形缝隙", "阶段4：修复角点...", 0.9f);
                    int cornerFixedCount = await PerformCornerFixAsync(affectedTerrains.ToList());
                    fixedCount += cornerFixedCount;
                }

                return fixedCount;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// 解析地形名称获取瓦片坐标
        /// 支持格式：{Z}-{X}-{Y}-Terrain 或类似格式
        /// </summary>
        private TerrainTileInfo ParseTerrainTile(Terrain terrain)
        {
            if (terrain?.terrainData == null) return null;

            // 在主线程获取所有需要的Unity API数据
            string name = terrain.name;
            Vector3 position = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            int heightmapResolution = terrain.terrainData.heightmapResolution;
            float terrainHeight = terrain.terrainData.size.y;

            // 尝试解析格式：14-13473-6195-Terrain
            string[] parts = name.Split('-');
            if (parts.Length >= 3)
            {
                if (int.TryParse(parts[0], out int z) &&
                    int.TryParse(parts[1], out int x) &&
                    int.TryParse(parts[2], out int y))
                {
                    return new TerrainTileInfo
                    {
                        terrain = terrain,
                        z = z,
                        x = x,
                        y = y,
                        position = position,
                        size = size,
                        heightmapResolution = heightmapResolution,
                        terrainHeight = terrainHeight,
                        terrainName = name
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// 记录地形的Undo状态
        /// </summary>
        public static void RecordTerrainUndo(Terrain terrain, string operationName)
        {
            if (terrain?.terrainData == null) return;

            // 记录TerrainData的Undo状态
            UnityEditor.Undo.RecordObject(terrain.terrainData, operationName);

            // 标记TerrainData为脏数据，确保保存
            UnityEditor.EditorUtility.SetDirty(terrain.terrainData);
        }

        /// <summary>
        /// 为一组地形记录Undo状态
        /// </summary>
        public static void RecordTerrainsUndo(IEnumerable<Terrain> terrains, string operationName)
        {
            foreach (var terrain in terrains)
            {
                RecordTerrainUndo(terrain, operationName);
            }
        }

        #region 两阶段修复策略 - 性能优化版本

        /// <summary>
        /// 地形高度需求信息
        /// </summary>
        private class TerrainHeightRequirement
        {
            public Terrain terrain;
            public float currentMaxHeight;
            public float requiredMaxHeight;
            public float originalMaxHeight; // 备份原始值
            public bool needsAdjustment => requiredMaxHeight > currentMaxHeight * 1.01f; // 1%容差避免微小调整
            public Dictionary<int, float> affectedPixels = new Dictionary<int, float>(); // 需要重计算的像素索引和原始世界高度
        }

        /// <summary>
        /// 阶段1：收集所有拼缝的高度需求信息（完全在主线程中执行）
        /// </summary>
        private async Task<Dictionary<Terrain, TerrainHeightRequirement>> CollectHeightRequirementsAsync(List<TerrainSeamInfo> seamInfoList)
        {
            var requirements = new Dictionary<Terrain, TerrainHeightRequirement>();

            // 完全在主线程中收集需求（包含Unity API调用，不能放到后台线程）
            foreach (var seam in seamInfoList)
            {
                var singleSeamRequirements = CollectSingleSeamHeightRequirements(seam);
                
                // 合并到每个地形的总需求中
                foreach (var req in singleSeamRequirements)
                {
                    if (!requirements.TryGetValue(req.terrain, out var existing))
                    {
                        requirements[req.terrain] = req;
                    }
                    else
                    {
                        // 合并需求：取更大的高度，合并像素集合
                        existing.requiredMaxHeight = Math.Max(existing.requiredMaxHeight, req.requiredMaxHeight);
                        foreach (var pixel in req.affectedPixels)
                        {
                            if (!existing.affectedPixels.ContainsKey(pixel.Key))
                            {
                                existing.affectedPixels[pixel.Key] = pixel.Value;
                            }
                        }
                    }
                }
            }

            // 过滤掉不需要调整的地形（性能优化）
            var filteredRequirements = requirements.Where(kvp => kvp.Value.needsAdjustment)
                                                  .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);


            // 返回Task是为了保持异步接口一致性，实际上是同步执行的
            return await Task.FromResult(filteredRequirements);
        }

        /// <summary>
        /// 收集单个拼缝的高度需求（包含Unity API调用，必须在主线程执行）
        /// </summary>
        private IEnumerable<TerrainHeightRequirement> CollectSingleSeamHeightRequirements(TerrainSeamInfo seam)
        {
            var requirements = new List<TerrainHeightRequirement>();

            try
            {
                var terrain1 = seam.terrain1;
                var terrain2 = seam.terrain2;

                if (terrain1?.terrainData == null || terrain2?.terrainData == null) return requirements;

                float terrainHeight1 = terrain1.terrainData.size.y;
                float terrainHeight2 = terrain2.terrainData.size.y;

                var req1 = new TerrainHeightRequirement
                {
                    terrain = terrain1,
                    currentMaxHeight = terrainHeight1,
                    requiredMaxHeight = terrainHeight1,
                    originalMaxHeight = terrainHeight1
                };

                var req2 = new TerrainHeightRequirement
                {
                    terrain = terrain2,
                    currentMaxHeight = terrainHeight2,
                    requiredMaxHeight = terrainHeight2,
                    originalMaxHeight = terrainHeight2
                };

                // 根据拼缝类型分析边缘高度需求（性能关键：只分析边缘像素）
                AnalyzeSeamHeightRequirements(seam, req1, req2);

                requirements.Add(req1);
                requirements.Add(req2);
            }
            catch (Exception ex)
            {
                Debug.LogError($"收集拼缝高度需求时出错: {ex.Message}");
            }

            return requirements;
        }

        /// <summary>
        /// 分析拼缝边缘的高度需求（性能优化：只处理边缘像素）
        /// </summary>
        private void AnalyzeSeamHeightRequirements(TerrainSeamInfo seam, TerrainHeightRequirement req1, TerrainHeightRequirement req2)
        {
            var tile1 = ParseTerrainTile(req1.terrain);
            var tile2 = ParseTerrainTile(req2.terrain);

            if (tile1 == null || tile2 == null) return;

            // 获取高度数据（在主线程中）
            float[,] heights1 = req1.terrain.terrainData.GetHeights(0, 0, tile1.heightmapResolution, tile1.heightmapResolution);
            float[,] heights2 = req2.terrain.terrainData.GetHeights(0, 0, tile2.heightmapResolution, tile2.heightmapResolution);

            bool isVertical = seam.seamType == SeamType.RightLeft || seam.seamType == SeamType.LeftRight;
            bool isHorizontal = seam.seamType == SeamType.BottomTop || seam.seamType == SeamType.TopBottom;

            if (isVertical)
            {
                AnalyzeVerticalSeamRequirements(heights1, heights2, tile1, tile2, req1, req2);
            }
            else if (isHorizontal)
            {
                AnalyzeHorizontalSeamRequirements(heights1, heights2, tile1, tile2, req1, req2);
            }
        }

        /// <summary>
        /// 分析垂直拼缝的高度需求
        /// </summary>
        private void AnalyzeVerticalSeamRequirements(float[,] heights1, float[,] heights2, 
            TerrainTileInfo tile1, TerrainTileInfo tile2,
            TerrainHeightRequirement req1, TerrainHeightRequirement req2)
        {
            int edge1 = tile1.heightmapResolution - 1; // 右边缘
            int edge2 = 0; // 左边缘

            int maxZ = Math.Min(tile1.heightmapResolution, tile2.heightmapResolution);

            for (int z = 0; z < maxZ; z++)
            {
                float height1World = heights1[z, edge1] * req1.currentMaxHeight;
                float height2World = heights2[z, edge2] * req2.currentMaxHeight;
                float targetHeight = (height1World + height2World) * 0.5f;

                // 检查地形1是否需要扩展范围
                CheckAndUpdateRequirement(req1, targetHeight, z * tile1.heightmapResolution + edge1, height1World);
                
                // 检查地形2是否需要扩展范围
                CheckAndUpdateRequirement(req2, targetHeight, z * tile2.heightmapResolution + edge2, height2World);
            }
        }

        /// <summary>
        /// 分析水平拼缝的高度需求
        /// </summary>
        private void AnalyzeHorizontalSeamRequirements(float[,] heights1, float[,] heights2,
            TerrainTileInfo tile1, TerrainTileInfo tile2,
            TerrainHeightRequirement req1, TerrainHeightRequirement req2)
        {
            int edge1 = tile1.heightmapResolution - 1; // 下边缘
            int edge2 = 0; // 上边缘

            int maxX = Math.Min(tile1.heightmapResolution, tile2.heightmapResolution);

            for (int x = 0; x < maxX; x++)
            {
                float height1World = heights1[edge1, x] * req1.currentMaxHeight;
                float height2World = heights2[edge2, x] * req2.currentMaxHeight;
                float targetHeight = (height1World + height2World) * 0.5f;

                // 检查地形1是否需要扩展范围
                CheckAndUpdateRequirement(req1, targetHeight, edge1 * tile1.heightmapResolution + x, height1World);
                
                // 检查地形2是否需要扩展范围
                CheckAndUpdateRequirement(req2, targetHeight, edge2 * tile2.heightmapResolution + x, height2World);
            }
        }

        /// <summary>
        /// 检查并更新地形高度需求（性能关键方法）
        /// </summary>
        private void CheckAndUpdateRequirement(TerrainHeightRequirement req, float targetWorldHeight, int pixelIndex, float originalWorldHeight)
        {
            // 计算需要的归一化高度
            float requiredNormHeight = targetWorldHeight / req.currentMaxHeight;

            // 如果超出当前范围，需要扩展地形高度范围
            if (requiredNormHeight > 1f)
            {
                float newRequiredMax = targetWorldHeight; // 直接使用目标世界高度作为新的最大高度
                if (newRequiredMax > req.requiredMaxHeight)
                {
                    req.requiredMaxHeight = newRequiredMax;
                }

                // 记录需要重新计算的像素（只记录受影响的）
                req.affectedPixels[pixelIndex] = originalWorldHeight;
            }
        }

        /// <summary>
        /// 阶段2：统一调整地形高度参数和重新计算像素值（性能优化）
        /// </summary>
        private async Task<Dictionary<Terrain, TerrainHeightRequirement>> AdjustTerrainHeightParametersAsync(
            Dictionary<Terrain, TerrainHeightRequirement> heightRequirements)
        {
            var adjustedTerrains = new Dictionary<Terrain, TerrainHeightRequirement>();

            foreach (var kvp in heightRequirements)
            {
                var terrain = kvp.Key;
                var requirement = kvp.Value;

                if (!requirement.needsAdjustment) continue;

                try
                {
                    // 记录Undo
                    TerrainSeamFixer.RecordTerrainUndo(terrain, "调整地形高度范围");

                    // 调整地形高度参数
                    var size = terrain.terrainData.size;
                    size.y = requirement.requiredMaxHeight;
                    terrain.terrainData.size = size;

                    // 重新计算受影响的像素值（性能优化：只处理受影响的像素）
                    await RecalculateAffectedPixelsAsync(terrain, requirement);

                    adjustedTerrains[terrain] = requirement;

                }
                catch (Exception ex)
                {
                    Debug.LogError($"调整地形 {terrain.name} 高度参数时出错: {ex.Message}");
                }
            }

            return adjustedTerrains;
        }

        /// <summary>
        /// 重新计算受影响的像素值（性能优化：只处理必要的像素）
        /// </summary>
        private async Task RecalculateAffectedPixelsAsync(Terrain terrain, TerrainHeightRequirement requirement)
        {
            if (requirement.affectedPixels.Count == 0) return;

            // 在主线程中获取Unity API数据
            var terrainData = terrain.terrainData;
            int resolution = terrainData.heightmapResolution;
            float[,] heights = terrainData.GetHeights(0, 0, resolution, resolution);

            float oldMaxHeight = requirement.originalMaxHeight;
            float newMaxHeight = requirement.requiredMaxHeight;

            // 在后台线程中进行纯数据处理
            await Task.Run(() =>
            {
                // 性能优化：批量处理受影响的像素
                var modifiedRows = new HashSet<int>();
                foreach (var kvp in requirement.affectedPixels)
                {
                    int pixelIndex = kvp.Key;
                    float originalWorldHeight = kvp.Value;

                    int z = pixelIndex / resolution;
                    int x = pixelIndex % resolution;

                    if (z >= 0 && z < resolution && x >= 0 && x < resolution)
                    {
                        // 重新计算归一化高度
                        float newNormalizedHeight = originalWorldHeight / newMaxHeight;
                        heights[z, x] = Mathf.Clamp01(newNormalizedHeight);
                        modifiedRows.Add(z);
                    }
                }

            });

            // 在主线程中应用修改
            terrainData.SetHeights(0, 0, heights);
        }

        /// <summary>
        /// 阶段3：在统一高度范围下修复拼缝（无需处理超出范围）
        /// </summary>
        private async Task<int> PerformSeamFixWithUnifiedRangeAsync(List<TerrainSeamInfo> seamInfoList, 
            Dictionary<Terrain, TerrainHeightRequirement> adjustedTerrains)
        {
            int fixedCount = 0;
            int totalSeams = seamInfoList.Count;

            for (int i = 0; i < totalSeams; i++)
            {
                var seam = seamInfoList[i];

                EditorUtility.DisplayProgressBar("修复地形缝隙",
                    $"修复拼缝 {i + 1}/{totalSeams}: {seam.terrain1.name} <-> {seam.terrain2.name}",
                    0.7f + 0.3f * i / totalSeams);

                try
                {
                    // 使用简化版的拼缝修复（无需处理超出范围）
                    bool success = await FixSeamWithUnifiedRangeAsync(seam);
                    if (success) fixedCount++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"修复拼缝时出错: {ex.Message}");
                }
            }

            return fixedCount;
        }

        /// <summary>
        /// 在统一高度范围下修复单个拼缝（简化版，无需处理超出范围）
        /// </summary>
        private async Task<bool> FixSeamWithUnifiedRangeAsync(TerrainSeamInfo seam)
        {
            try
            {
                await FixSeamOptimizedUnifiedAsync(seam.terrain1, seam.terrain2, seam.seamType);

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"修复拼缝失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 优化的拼缝修复算法（统一范围版本）
        /// </summary>
        private async Task FixSeamOptimizedUnifiedAsync(Terrain terrain1, Terrain terrain2, SeamType seamType)
        {
            // 在主线程获取地形信息
            var tile1 = ParseTerrainTile(terrain1);
            var tile2 = ParseTerrainTile(terrain2);

            if (tile1 == null || tile2 == null) return;

            bool isVertical = seamType == SeamType.RightLeft || seamType == SeamType.LeftRight;
            bool isHorizontal = seamType == SeamType.BottomTop || seamType == SeamType.TopBottom;

            int edge1, edge2;
            if (isVertical) // 垂直缝隙（左右相邻）
            {
                edge1 = tile1.heightmapResolution - 1; // 右边缘
                edge2 = 0; // 左边缘
            }
            else if (isHorizontal) // 水平缝隙（上下相邻）
            {
                edge1 = tile1.heightmapResolution - 1; // 下边缘  
                edge2 = 0; // 上边缘
            }
            else
            {
                return;
            }

            await FixSeamBetweenTilesUnifiedAsync(tile1, tile2, seamType);
        }

        /// <summary>
        /// 在统一高度范围下修复两个瓦片之间的拼缝
        /// </summary>
        private async Task FixSeamBetweenTilesUnifiedAsync(TerrainTileInfo tile1, TerrainTileInfo tile2, SeamType seamType)
        {
            // 在主线程获取高度数据
            float[,] heights1 = tile1.terrain.terrainData.GetHeights(0, 0, tile1.heightmapResolution, tile1.heightmapResolution);
            float[,] heights2 = tile2.terrain.terrainData.GetHeights(0, 0, tile2.heightmapResolution, tile2.heightmapResolution);

            await Task.Run(() =>
            {
                // 确定边缘索引
                bool isVertical = seamType == SeamType.RightLeft || seamType == SeamType.LeftRight;
                bool isHorizontal = seamType == SeamType.BottomTop || seamType == SeamType.TopBottom;

                int edge1, edge2;
                if (isVertical)
                {
                    edge1 = tile1.heightmapResolution - 1;
                    edge2 = 0;
                }
                else if (isHorizontal)
                {
                    edge1 = tile1.heightmapResolution - 1;
                    edge2 = 0;
                }
                else return;

                // 简化的拼缝修复（无需处理超出范围）
                FixSeamDirectUnified(heights1, heights2, tile1, tile2, edge1, edge2, isVertical, isHorizontal);
            });

            // 在主线程应用结果
            tile1.terrain.terrainData.SetHeights(0, 0, heights1);
            tile2.terrain.terrainData.SetHeights(0, 0, heights2);

        }

        /// <summary>
        /// 简化的直接拼缝修复（统一范围版本，无需处理超出范围）
        /// </summary>
        private void FixSeamDirectUnified(float[,] heights1, float[,] heights2,
            TerrainTileInfo tile1, TerrainTileInfo tile2,
            int edge1, int edge2, bool isVertical, bool isHorizontal)
        {
            int res1 = tile1.heightmapResolution;
            int res2 = tile2.heightmapResolution;
            float terrainHeight1 = tile1.terrainHeight;
            float terrainHeight2 = tile2.terrainHeight;

            if (isVertical) // 垂直缝隙（左右相邻）
            {
                for (int z = 0; z < Math.Min(res1, res2); z++)
                {
                    // 计算边缘点的目标高度（世界坐标平均值）
                    float height1Edge = heights1[z, edge1] * terrainHeight1;
                    float height2Edge = heights2[z, edge2] * terrainHeight2;
                    float targetHeight = (height1Edge + height2Edge) * 0.5f;

                    // 转换为归一化高度（在统一范围下，不会超出[0,1]）
                    float targetNorm1 = targetHeight / terrainHeight1;
                    float targetNorm2 = targetHeight / terrainHeight2;

                    // 直接应用目标高度（无需处理超出范围）
                    FixEdgeRegionUnified(heights1, res1, edge1, z, targetNorm1, true);
                    FixEdgeRegionUnified(heights2, res2, edge2, z, targetNorm2, true);
                }
            }
            else if (isHorizontal) // 水平缝隙（上下相邻）
            {
                for (int x = 0; x < Math.Min(res1, res2); x++)
                {
                    // 计算边缘点的目标高度（世界坐标平均值）
                    float height1Edge = heights1[edge1, x] * terrainHeight1;
                    float height2Edge = heights2[edge2, x] * terrainHeight2;
                    float targetHeight = (height1Edge + height2Edge) * 0.5f;

                    // 转换为归一化高度（在统一范围下，不会超出[0,1]）
                    float targetNorm1 = targetHeight / terrainHeight1;
                    float targetNorm2 = targetHeight / terrainHeight2;

                    // 直接应用目标高度（无需处理超出范围）
                    FixEdgeRegionUnified(heights1, res1, edge1, x, targetNorm1, false);
                    FixEdgeRegionUnified(heights2, res2, edge2, x, targetNorm2, false);
                }
            }
        }

        /// <summary>
        /// 统一范围版本的边缘区域修复（简化版，无需范围检查）
        /// </summary>
        private void FixEdgeRegionUnified(float[,] heights, int resolution, int fixedCoord, int varCoord,
            float targetHeight, bool isVertical)
        {
            for (int i = 0; i < blendWidth; i++)
            {
                int coord1, coord2;

                if (isVertical) // 垂直缝隙，沿X轴融合
                {
                    coord1 = varCoord; // z坐标固定
                    coord2 = fixedCoord - i; // x坐标向内延伸

                    if (coord2 < 0 || coord2 >= resolution) continue;
                }
                else // 水平缝隙，沿Z轴融合
                {
                    coord1 = fixedCoord - i; // z坐标向内延伸
                    coord2 = varCoord; // x坐标固定

                    if (coord1 < 0 || coord1 >= resolution) continue;
                }

                // 边缘像素(i=0)完全对齐，内部像素渐变
                if (i == 0)
                {
                    // 直接使用目标高度（已确保在范围内）
                    heights[coord1, coord2] = targetHeight;
                }
                else if (blendWidth > 1)
                {
                    float blendFactor = blendCurve.Evaluate((float)i / (blendWidth - 1));
                    float blendedHeight = Mathf.Lerp(targetHeight, heights[coord1, coord2], blendFactor);
                    // 无需Clamp01，因为已经在统一范围内
                    heights[coord1, coord2] = blendedHeight;
                }
            }
        }

        /// <summary>
        /// 阶段4：统一执行角点修复
        /// </summary>
        private async Task<int> PerformCornerFixAsync(List<Terrain> affectedTerrains)
        {
            try
            {
                // 创建角点修复器
                var cornerFixer = new TerrainCornerFixer(blendWidth, blendCurve);
                
                // 对所有受影响的地形进行角点修复
                int fixedCount = await cornerFixer.FixAllCornerPointsAsync(affectedTerrains);
                
                return fixedCount;
            }
            catch (Exception ex)
            {
                Debug.LogError($"角点修复阶段出错: {ex.Message}");
                return 0;
            }
        }

        #endregion
    }
}