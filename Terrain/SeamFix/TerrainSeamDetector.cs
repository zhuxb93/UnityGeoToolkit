using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace GeoToolkit.SeamFix
{
    /// <summary>
    /// 地形缝隙检测算法类
    /// </summary>
    public class TerrainSeamDetector
    {
        private readonly float seamThreshold;

        public TerrainSeamDetector(float seamThreshold)
        {
            this.seamThreshold = seamThreshold;
        }

        /// <summary>
        /// 异步检测地形缝隙
        /// </summary>
        public async Task<List<TerrainSeamInfo>> DetectSeamsAsync(List<Terrain> terrains)
        {
            if (terrains == null || terrains.Count == 0)
                return new List<TerrainSeamInfo>();

            return await DetectSeamsOptimizedAsync(terrains);
        }

        /// <summary>
        /// 优化的缝隙检测算法，基于瓦片坐标
        /// </summary>
        private async Task<List<TerrainSeamInfo>> DetectSeamsOptimizedAsync(List<Terrain> terrains)
        {
            var seamInfoList = new List<TerrainSeamInfo>();

            // 构建瓦片映射
            var tileMapping = BuildTileMapping(terrains);
            if (tileMapping.Count == 0)
            {
                return await DetectSeamsLegacyAsync(terrains);
            }


            var tileList = tileMapping.Values.ToList();
            int totalTiles = tileList.Count;
            int processedTiles = 0;

            // 在主线程预先获取所有瓦片的高度数据
            var heightDataCache = new Dictionary<string, float[,]>();
            foreach (var tile in tileList)
            {
                heightDataCache[tile.TileKey] = tile.terrain.terrainData.GetHeights(0, 0,
                    tile.heightmapResolution, tile.heightmapResolution);
            }

            // 为每个瓦片检查其右邻和下邻瓦片（避免重复检查）
            var seamDetectionTasks = new List<Task<List<TerrainSeamInfo>>>();

            foreach (var tile in tileList)
            {
                var task = Task.Run(() => CheckTileNeighborsOptimized(tileMapping, tile, heightDataCache));
                seamDetectionTasks.Add(task);
            }

            // 等待所有检测任务完成
            while (seamDetectionTasks.Count > 0)
            {
                var completedTask = await Task.WhenAny(seamDetectionTasks);
                seamDetectionTasks.Remove(completedTask);

                var results = await completedTask;
                if (results != null)
                {
                    seamInfoList.AddRange(results);
                }

                processedTiles++;
                EditorUtility.DisplayProgressBar("地形缝隙检测",
                    $"已检测 {processedTiles}/{totalTiles} 个瓦片",
                    (float)processedTiles / totalTiles);
            }

            return seamInfoList;
        }

        /// <summary>
        /// 原始检测算法（回退方案）
        /// </summary>
        private async Task<List<TerrainSeamInfo>> DetectSeamsLegacyAsync(List<Terrain> terrains)
        {
            var seamInfoList = new List<TerrainSeamInfo>();

            // 计算需要检测的地形对数量
            int totalPairs = terrains.Count * (terrains.Count - 1) / 2;
            int processedPairs = 0;

            // 在主线程预处理地形数据
            var terrainDataList = new List<TerrainDataInfo>();
            foreach (var terrain in terrains)
            {
                if (terrain?.terrainData == null) continue;

                terrainDataList.Add(new TerrainDataInfo
                {
                    terrain = terrain,
                    position = terrain.transform.position,
                    size = terrain.terrainData.size,
                    heightmapResolution = terrain.terrainData.heightmapResolution,
                    terrainHeight = terrain.terrainData.size.y,
                    heights = terrain.terrainData.GetHeights(0, 0, terrain.terrainData.heightmapResolution, terrain.terrainData.heightmapResolution)
                });
            }

            // 异步检测相邻地形之间的缝隙
            var seamDetectionTasks = new List<Task<TerrainSeamInfo>>();

            for (int i = 0; i < terrainDataList.Count; i++)
            {
                for (int j = i + 1; j < terrainDataList.Count; j++)
                {
                    var data1 = terrainDataList[i];
                    var data2 = terrainDataList[j];

                    // 创建异步检测任务，传入预处理的数据
                    var task = Task.Run(() => CheckSeamBetweenTerrainsWithData(data1, data2));
                    seamDetectionTasks.Add(task);
                }
            }

            // 等待所有检测任务完成，并显示进度
            while (seamDetectionTasks.Count > 0)
            {
                var completedTask = await Task.WhenAny(seamDetectionTasks);
                seamDetectionTasks.Remove(completedTask);

                var result = await completedTask;
                if (result != null)
                {
                    seamInfoList.Add(result);
                }

                processedPairs++;
                EditorUtility.DisplayProgressBar("地形缝隙检测",
                    $"已检测 {processedPairs}/{totalPairs} 个地形对",
                    (float)processedPairs / totalPairs);
            }

            return seamInfoList;
        }

        /// <summary>
        /// 检查单个瓦片的相邻瓦片缝隙（优化版本，线程安全）
        /// </summary>
        private List<TerrainSeamInfo> CheckTileNeighborsOptimized(Dictionary<string, TerrainTileInfo> tileMapping,
            TerrainTileInfo tile, Dictionary<string, float[,]> heightDataCache)
        {
            var seams = new List<TerrainSeamInfo>();

            if (!heightDataCache.TryGetValue(tile.TileKey, out float[,] tileHeights))
                return seams;

            // 检查东邻瓦片（X+1,Y+0）- 对应tile的右边缘
            var eastNeighbor = GetNeighborTile(tileMapping, tile, 1, 0);
            if (eastNeighbor != null && heightDataCache.TryGetValue(eastNeighbor.TileKey, out float[,] eastHeights))
            {
                // 验证两个瓦片是否真的在东西方向相邻
                if (IsEastWestNeighbor(tile, eastNeighbor))
                {
                    var seam = CheckSeamBetweenTilesOptimized(tile, eastNeighbor, SeamType.RightLeft, tileHeights, eastHeights);
                    if (seam != null) seams.Add(seam);
                }
            }

            // 检查西邻瓦片（X-1,Y+0）- 对应tile的左边缘
            var westNeighbor = GetNeighborTile(tileMapping, tile, -1, 0);
            if (westNeighbor != null && heightDataCache.TryGetValue(westNeighbor.TileKey, out float[,] westHeights))
            {
                // 验证两个瓦片是否真的在东西方向相邻
                if (IsEastWestNeighbor(westNeighbor, tile)) // 注意参数顺序：westNeighbor在东，tile在西
                {
                    var seam = CheckSeamBetweenTilesOptimized(westNeighbor, tile, SeamType.RightLeft, westHeights, tileHeights);
                    if (seam != null) seams.Add(seam);
                }
            }

            // 检查南邻瓦片（X+0,Y-1）- 对应tile的下边缘（注意：Y减小是向南）
            var southNeighbor = GetNeighborTile(tileMapping, tile, 0, -1);
            if (southNeighbor != null && heightDataCache.TryGetValue(southNeighbor.TileKey, out float[,] southHeights))
            {
                // 验证两个瓦片是否真的在南北方向相邻
                if (IsNorthSouthNeighbor(tile, southNeighbor))
                {
                    var seam = CheckSeamBetweenTilesOptimized(tile, southNeighbor, SeamType.BottomTop, tileHeights, southHeights);
                    if (seam != null) seams.Add(seam);
                }
            }

            // 检查北邻瓦片（X+0,Y+1）- 对应tile的上边缘（注意：Y增加是向北）
            var northNeighbor = GetNeighborTile(tileMapping, tile, 0, 1);
            if (northNeighbor != null && heightDataCache.TryGetValue(northNeighbor.TileKey, out float[,] northHeights))
            {
                // 验证两个瓦片是否真的在南北方向相邻
                if (IsNorthSouthNeighbor(northNeighbor, tile)) // 注意参数顺序：northNeighbor在北，tile在南
                {
                    var seam = CheckSeamBetweenTilesOptimized(northNeighbor, tile, SeamType.BottomTop, northHeights, tileHeights);
                    if (seam != null) seams.Add(seam);
                }
            }

            return seams;
        }

        /// <summary>
        /// 优化的瓦片间缝隙检测，直接定位相邻边缘点（线程安全版本）
        /// </summary>
        private TerrainSeamInfo CheckSeamBetweenTilesOptimized(TerrainTileInfo tile1, TerrainTileInfo tile2,
            SeamType seamType, float[,] heights1, float[,] heights2)
        {
            float maxDiff = 0f;
            int res1 = tile1.heightmapResolution;
            int res2 = tile2.heightmapResolution;

            // 直接检测相邻边缘点的高度差
            if (seamType == SeamType.RightLeft)
            {
                // tile1的右边缘（最后一列）与 tile2的左边缘（第一列）
                for (int z = 0; z < Math.Min(res1, res2); z++)
                {
                    float height1 = heights1[z, res1 - 1] * tile1.terrainHeight;
                    float height2 = heights2[z, 0] * tile2.terrainHeight;
                    maxDiff = Mathf.Max(maxDiff, Mathf.Abs(height1 - height2));
                }
            }
            else if (seamType == SeamType.BottomTop)
            {
                // tile1的下边缘（最后一行）与 tile2的上边缘（第一行）
                for (int x = 0; x < Math.Min(res1, res2); x++)
                {
                    float height1 = heights1[res1 - 1, x] * tile1.terrainHeight;
                    float height2 = heights2[0, x] * tile2.terrainHeight;
                    maxDiff = Mathf.Max(maxDiff, Mathf.Abs(height1 - height2));
                }
            }

            if (maxDiff > seamThreshold)
            {
                return new TerrainSeamInfo
                {
                    terrain1 = tile1.terrain,
                    terrain2 = tile2.terrain,
                    seamType = seamType,
                    maxHeightDifference = maxDiff
                };
            }

            return null;
        }

        /// <summary>
        /// 使用预处理数据的缝隙检测函数（线程安全）
        /// </summary>
        private TerrainSeamInfo CheckSeamBetweenTerrainsWithData(TerrainDataInfo data1, TerrainDataInfo data2)
        {
            if (data1 == null || data2 == null || data1.terrain == null || data2.terrain == null)
                return null;

            // 判断是否相邻
            SeamType seamType = GetSeamType(data1.position, data1.size, data2.position, data2.size);
            if (seamType == SeamType.None)
                return null;

            // 检测缝隙
            float maxHeightDiff = CheckSeamHeightDifferenceWithData(data1, data2, seamType);

            if (maxHeightDiff > seamThreshold)
            {
                return new TerrainSeamInfo
                {
                    terrain1 = data1.terrain,
                    terrain2 = data2.terrain,
                    seamType = seamType,
                    maxHeightDifference = maxHeightDiff
                };
            }

            return null;
        }

        /// <summary>
        /// 使用预处理数据的缝隙高度差检测（线程安全）
        /// </summary>
        private float CheckSeamHeightDifferenceWithData(TerrainDataInfo data1, TerrainDataInfo data2, SeamType seamType)
        {
            float maxDiff = 0f;
            int res1 = data1.heightmapResolution;
            int res2 = data2.heightmapResolution;
            float terrainHeight1 = data1.terrainHeight;
            float terrainHeight2 = data2.terrainHeight;
            float[,] heights1 = data1.heights;
            float[,] heights2 = data2.heights;

            switch (seamType)
            {
                case SeamType.RightLeft:
                    // terrain1的右边缘与terrain2的左边缘
                    for (int z = 0; z < Math.Min(res1, res2); z++)
                    {
                        float height1 = heights1[z, res1 - 1] * terrainHeight1;
                        float height2 = heights2[z, 0] * terrainHeight2;
                        maxDiff = Mathf.Max(maxDiff, Mathf.Abs(height1 - height2));
                    }
                    break;

                case SeamType.LeftRight:
                    // terrain1的左边缘与terrain2的右边缘
                    for (int z = 0; z < Math.Min(res1, res2); z++)
                    {
                        float height1 = heights1[z, 0] * terrainHeight1;
                        float height2 = heights2[z, res2 - 1] * terrainHeight2;
                        maxDiff = Mathf.Max(maxDiff, Mathf.Abs(height1 - height2));
                    }
                    break;

                case SeamType.BottomTop:
                    // terrain1的下边缘与terrain2的上边缘
                    for (int x = 0; x < Math.Min(res1, res2); x++)
                    {
                        float height1 = heights1[res1 - 1, x] * terrainHeight1;
                        float height2 = heights2[0, x] * terrainHeight2;
                        maxDiff = Mathf.Max(maxDiff, Mathf.Abs(height1 - height2));
                    }
                    break;

                case SeamType.TopBottom:
                    // terrain1的上边缘与terrain2的下边缘
                    for (int x = 0; x < Math.Min(res1, res2); x++)
                    {
                        float height1 = heights1[0, x] * terrainHeight1;
                        float height2 = heights2[res2 - 1, x] * terrainHeight2;
                        maxDiff = Mathf.Max(maxDiff, Mathf.Abs(height1 - height2));
                    }
                    break;
            }

            return maxDiff;
        }

        /// <summary>
        /// 获取两个地形的缝隙类型
        /// </summary>
        private SeamType GetSeamType(Vector3 pos1, Vector3 size1, Vector3 pos2, Vector3 size2)
        {
            float tolerance = 0.1f;

            // 检查右邻接 (terrain1的右边与terrain2的左边相邻)
            bool rightLeftAlign = Mathf.Abs(pos1.x + size1.x - pos2.x) < tolerance;
            bool zPositionAlign = Mathf.Abs(pos1.z - pos2.z) < tolerance;
            bool zSizeMatch = Mathf.Abs(size1.z - size2.z) < tolerance;

            if (rightLeftAlign && zPositionAlign && zSizeMatch)
            {
                return SeamType.RightLeft;
            }

            // 检查左邻接 (terrain1的左边与terrain2的右边相邻)
            bool leftRightAlign = Mathf.Abs(pos2.x + size2.x - pos1.x) < tolerance;
            if (leftRightAlign && zPositionAlign && zSizeMatch)
            {
                return SeamType.LeftRight;
            }

            // 检查下邻接 (terrain1的下边与terrain2的上边相邻)
            bool bottomTopAlign = Mathf.Abs(pos1.z + size1.z - pos2.z) < tolerance;
            bool xPositionAlign = Mathf.Abs(pos1.x - pos2.x) < tolerance;
            bool xSizeMatch = Mathf.Abs(size1.x - size2.x) < tolerance;

            if (bottomTopAlign && xPositionAlign && xSizeMatch)
            {
                return SeamType.BottomTop;
            }

            // 检查上邻接 (terrain1的上边与terrain2的下边相邻)
            bool topBottomAlign = Mathf.Abs(pos2.z + size2.z - pos1.z) < tolerance;
            if (topBottomAlign && xPositionAlign && xSizeMatch)
            {
                return SeamType.TopBottom;
            }

            // 放宽边缘要求，检查是否有部分重叠的邻接关系
            if (rightLeftAlign && HasZOverlap(pos1, size1, pos2, size2))
            {
                return SeamType.RightLeft;
            }

            if (leftRightAlign && HasZOverlap(pos1, size1, pos2, size2))
            {
                return SeamType.LeftRight;
            }

            if (bottomTopAlign && HasXOverlap(pos1, size1, pos2, size2))
            {
                return SeamType.BottomTop;
            }

            if (topBottomAlign && HasXOverlap(pos1, size1, pos2, size2))
            {
                return SeamType.TopBottom;
            }

            // 检查对角相邻 (共用一个角点)
            // 右下-左上角对齐
            if (Mathf.Abs(pos1.x + size1.x - pos2.x) < tolerance &&
                Mathf.Abs(pos1.z + size1.z - pos2.z) < tolerance)
                return SeamType.Corner;

            // 左下-右上角对齐
            if (Mathf.Abs(pos2.x + size2.x - pos1.x) < tolerance &&
                Mathf.Abs(pos1.z + size1.z - pos2.z) < tolerance)
                return SeamType.Corner;

            // 右上-左下角对齐
            if (Mathf.Abs(pos1.x + size1.x - pos2.x) < tolerance &&
                Mathf.Abs(pos2.z + size2.z - pos1.z) < tolerance)
                return SeamType.Corner;

            // 左上-右下角对齐
            if (Mathf.Abs(pos2.x + size2.x - pos1.x) < tolerance &&
                Mathf.Abs(pos2.z + size2.z - pos1.z) < tolerance)
                return SeamType.Corner;

            return SeamType.None;
        }

        /// <summary>
        /// 检查两个地形在Z轴方向是否有重叠
        /// </summary>
        private bool HasZOverlap(Vector3 pos1, Vector3 size1, Vector3 pos2, Vector3 size2)
        {
            float z1Start = pos1.z;
            float z1End = pos1.z + size1.z;
            float z2Start = pos2.z;
            float z2End = pos2.z + size2.z;

            // 检查是否有重叠
            return !(z1End <= z2Start || z2End <= z1Start);
        }

        /// <summary>
        /// 检查两个地形在X轴方向是否有重叠
        /// </summary>
        private bool HasXOverlap(Vector3 pos1, Vector3 size1, Vector3 pos2, Vector3 size2)
        {
            float x1Start = pos1.x;
            float x1End = pos1.x + size1.x;
            float x2Start = pos2.x;
            float x2End = pos2.x + size2.x;

            // 检查是否有重叠
            return !(x1End <= x2Start || x2End <= x1Start);
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
        /// 构建瓦片映射表
        /// </summary>
        private Dictionary<string, TerrainTileInfo> BuildTileMapping(List<Terrain> terrains)
        {
            var tileMapping = new Dictionary<string, TerrainTileInfo>();

            foreach (var terrain in terrains)
            {
                var tileInfo = ParseTerrainTile(terrain);
                if (tileInfo != null)
                {
                    tileMapping[tileInfo.TileKey] = tileInfo;
                }
            }

            return tileMapping;
        }

        /// <summary>
        /// 获取相邻瓦片
        /// </summary>
        private TerrainTileInfo GetNeighborTile(Dictionary<string, TerrainTileInfo> tileMapping,
            TerrainTileInfo tile, int deltaX, int deltaY)
        {
            string neighborKey = $"{tile.z}_{tile.x + deltaX}_{tile.y + deltaY}";
            tileMapping.TryGetValue(neighborKey, out TerrainTileInfo neighbor);
            return neighbor;
        }

        /// <summary>
        /// 验证两个瓦片是否在东西方向相邻（基于实际世界坐标）
        /// </summary>
        private bool IsEastWestNeighbor(TerrainTileInfo tile1, TerrainTileInfo tile2)
        {
            float tolerance = 0.1f;

            // 检查tile1的右边缘是否与tile2的左边缘相邻
            bool rightLeftAdjacent = Mathf.Abs(tile1.position.x + tile1.size.x - tile2.position.x) < tolerance;

            // 检查Z坐标是否对齐且大小匹配
            bool zAligned = Mathf.Abs(tile1.position.z - tile2.position.z) < tolerance;
            bool zSizeMatched = Mathf.Abs(tile1.size.z - tile2.size.z) < tolerance;


            if (rightLeftAdjacent && zAligned && zSizeMatched)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 验证两个瓦片是否在南北方向相邻（基于实际世界坐标）
        /// </summary>
        private bool IsNorthSouthNeighbor(TerrainTileInfo tile1, TerrainTileInfo tile2)
        {
            float tolerance = 0.1f;

            // 检查tile1的下边缘是否与tile2的上边缘相邻
            bool bottomTopAdjacent = Mathf.Abs(tile1.position.z + tile1.size.z - tile2.position.z) < tolerance;

            // 检查X坐标是否对齐且大小匹配
            bool xAligned = Mathf.Abs(tile1.position.x - tile2.position.x) < tolerance;
            bool xSizeMatched = Mathf.Abs(tile1.size.x - tile2.size.x) < tolerance;


            if (bottomTopAdjacent && xAligned && xSizeMatched)
            {
                return true;
            }

            return false;
        }
    }
}