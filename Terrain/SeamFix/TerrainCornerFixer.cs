using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace GeoToolkit.SeamFix
{
    /// <summary>
    /// 地形角点修复算法类
    /// </summary>
    public class TerrainCornerFixer
    {
        private readonly int blendWidth;
        private readonly AnimationCurve blendCurve;

        public TerrainCornerFixer(int blendWidth, AnimationCurve blendCurve)
        {
            this.blendWidth = blendWidth;
            this.blendCurve = blendCurve;
        }

        /// <summary>
        /// 异步修复所有4瓦片共用的角点（入口函数）
        /// </summary>
        public async Task<int> FixAllCornerPointsAsync(List<Terrain> terrains)
        {
            return await FixAllCornerPointsOptimizedAsync(terrains);
        }

        /// <summary>
        /// 优化的4瓦片共用角点修复算法
        /// </summary>
        private async Task<int> FixAllCornerPointsOptimizedAsync(List<Terrain> terrains)
        {
            // 构建瓦片映射
            var tileMapping = BuildTileMapping(terrains);
            if (tileMapping.Count == 0)
            {
                return await FixAllCornerPointsLegacyAsync(terrains);
            }

            int fixedCount = 0;
            var processedCorners = new HashSet<string>();

            // 遍历所有瓦片，查找4瓦片共用的角点
            foreach (var tile in tileMapping.Values)
            {
                // 检查右下角点（最容易形成4瓦片交汇的位置）
                string cornerKey = $"{tile.x}_{tile.y}"; // 右下角点的瓦片坐标标识

                if (processedCorners.Contains(cornerKey)) continue;

                // 查找共享这个角点的4个瓦片
                var cornerTiles = FindCornerTilesOptimized(tileMapping, tile.x, tile.y, tile.z);

                if (cornerTiles.Count >= 2) // 至少2个瓦片共享才需要修复
                {
                    if (await FixCornerTilesOptimizedAsync(cornerTiles, tile.x, tile.y))
                    {
                        fixedCount++;
                    }
                    processedCorners.Add(cornerKey);
                }
            }

            return fixedCount;
        }

        /// <summary>
        /// 原始4瓦片角点修复算法（回退方案）
        /// </summary>
        private async Task<int> FixAllCornerPointsLegacyAsync(List<Terrain> terrains)
        {
            // 在主线程进行Unity API调用
            int fixedCount = 0;
            var cornerGroups = FindCornerGroups(terrains);

            // 异步处理角点组修复
            foreach (var group in cornerGroups)
            {
                if (await FixCornerGroupAsync(group))
                {
                    fixedCount++;
                }
            }

            return fixedCount;
        }

        /// <summary>
        /// 查找共享指定角点的瓦片（优化版本）
        /// </summary>
        private List<TerrainTileInfo> FindCornerTilesOptimized(Dictionary<string, TerrainTileInfo> tileMapping, int cornerX, int cornerY, int z)
        {
            var cornerTiles = new List<TerrainTileInfo>();

            // 4个可能共享右下角点的瓦片位置
            // 当前瓦片(x,y)的右下角 = 瓦片(x-1,y-1)的右下角 = 瓦片(x,y-1)的左下角 = 瓦片(x-1,y)的右上角
            var possibleTileCoords = new[]
            {
                (cornerX, cornerY),     // 左上瓦片
                (cornerX - 1, cornerY), // 右上瓦片
                (cornerX, cornerY - 1), // 左下瓦片
                (cornerX - 1, cornerY - 1) // 右下瓦片
            };

            foreach (var (x, y) in possibleTileCoords)
            {
                string tileKey = $"{z}_{x}_{y}";
                if (tileMapping.TryGetValue(tileKey, out TerrainTileInfo tile))
                {
                    cornerTiles.Add(tile);
                }
            }

            return cornerTiles;
        }

        /// <summary>
        /// 优化的角点瓦片修复
        /// </summary>
        private async Task<bool> FixCornerTilesOptimizedAsync(List<TerrainTileInfo> cornerTiles, int cornerWorldX, int cornerWorldY)
        {
            if (cornerTiles.Count < 2) return false;

            try
            {
                // 收集角点高度数据
                var cornerData = new List<(TerrainTileInfo tile, float height, float[,] heights)>();

                foreach (var tile in cornerTiles)
                {
                    float[,] heights = tile.terrain.terrainData.GetHeights(0, 0,
                        tile.heightmapResolution, tile.heightmapResolution);

                    // 确定角点在高度图中的位置
                    GetCornerPositionInHeightmap(tile, cornerWorldX, cornerWorldY, out int heightmapX, out int heightmapZ);

                    float normalizedHeight = heights[heightmapZ, heightmapX];
                    float worldHeight = normalizedHeight * tile.terrainHeight;

                    cornerData.Add((tile, worldHeight, heights));
                }

                // 在后台线程计算目标高度
                float targetHeight = await Task.Run(() =>
                {
                    return cornerData.Select(cd => cd.height).Average();
                });

                // 在主线程应用修复
                foreach (var (tile, originalHeight, heights) in cornerData)
                {
                    await FixSingleCornerOptimizedAsync(tile, heights, targetHeight, cornerWorldX, cornerWorldY);
                }

                var tileNames = string.Join(", ", cornerTiles.Select(t => t.terrainName));

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"角点修复失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 优化的单个角点修复，带融合效果
        /// </summary>
        private async Task FixSingleCornerOptimizedAsync(TerrainTileInfo tile, float[,] heights, float targetHeight, int cornerWorldX, int cornerWorldY)
        {
            int res = tile.heightmapResolution;
            float terrainHeight = tile.terrainHeight;

            // 捕获参数
            int localBlendWidth = blendWidth;
            AnimationCurve localBlendCurve = new AnimationCurve(blendCurve.keys);

            // 获取角点位置
            GetCornerPositionInHeightmap(tile, cornerWorldX, cornerWorldY, out int cornerX, out int cornerZ);

            // 在后台线程进行计算
            await Task.Run(() =>
            {
                // 在角点周围的区域进行径向融合
                for (int dz = -localBlendWidth; dz <= localBlendWidth; dz++)
                {
                    for (int dx = -localBlendWidth; dx <= localBlendWidth; dx++)
                    {
                        int x = cornerX + dx;
                        int z = cornerZ + dz;

                        // 边界检查
                        if (x < 0 || x >= res || z < 0 || z >= res) continue;

                        // 计算距离角点的距离
                        float distance = Mathf.Sqrt(dx * dx + dz * dz);

                        // 只处理在融合半径内的点
                        if (distance <= localBlendWidth)
                        {
                            float originalHeight = heights[z, x] * terrainHeight;

                            // 使用距离计算融合因子
                            float blendFactor = localBlendWidth == 1 ? 0f : localBlendCurve.Evaluate(distance / localBlendWidth);
                            float newHeight = Mathf.Lerp(targetHeight, originalHeight, blendFactor);

                            heights[z, x] = newHeight / terrainHeight;
                        }
                    }
                }
            });

            // 在主线程应用结果
            tile.terrain.terrainData.SetHeights(0, 0, heights);
        }

        /// <summary>
        /// 获取角点在高度图中的位置
        /// </summary>
        private void GetCornerPositionInHeightmap(TerrainTileInfo tile, int cornerX, int cornerY, out int heightmapX, out int heightmapZ)
        {
            int res = tile.heightmapResolution;

            // 根据瓦片相对于角点的位置确定高度图中的角点坐标
            // cornerX, cornerY 是4瓦片共享角点的世界坐标
            // 需要判断当前瓦片相对于这个角点的位置关系

            // 根据实际瓦片位置关系确定角点在heightmap中的位置
            // 基于实际数据：瓦片X增加=世界X增加，瓦片Y增加=世界Z减小
            if (tile.x == cornerX && tile.y == cornerY)
            {
                // 瓦片(cornerX, cornerY)：右上角瓦片，共享角点在其左下角（调换上下）
                heightmapX = 0;        // 左边缘
                heightmapZ = res - 1;  // 下边缘
            }
            else if (tile.x == cornerX - 1 && tile.y == cornerY)
            {
                // 瓦片(cornerX-1, cornerY)：左上角瓦片，共享角点在其右下角（调换上下）
                heightmapX = res - 1;  // 右边缘
                heightmapZ = res - 1;  // 下边缘
            }
            else if (tile.x == cornerX && tile.y == cornerY - 1)
            {
                // 瓦片(cornerX, cornerY-1)：右下角瓦片，共享角点在其左上角（调换上下）
                heightmapX = 0;        // 左边缘  
                heightmapZ = 0;        // 上边缘
            }
            else if (tile.x == cornerX - 1 && tile.y == cornerY - 1)
            {
                // 瓦片(cornerX-1, cornerY-1)：左下角瓦片，共享角点在其右上角（调换上下）
                heightmapX = res - 1;  // 右边缘
                heightmapZ = 0;        // 上边缘
            }
            else
            {
                // 不应该到达这里，输出错误信息
                heightmapX = 0;
                heightmapZ = 0;
            }
        }

        /// <summary>
        /// 查找所有4瓦片共用的角点组
        /// </summary>
        private List<List<TerrainCornerInfo>> FindCornerGroups(List<Terrain> terrains)
        {
            var cornerGroups = new List<List<TerrainCornerInfo>>();
            var processedCorners = new HashSet<Vector3>();

            foreach (var terrain in terrains)
            {
                if (terrain?.terrainData == null) continue;

                var corners = GetTerrainCorners(terrain);

                foreach (var corner in corners)
                {
                    Vector3 worldPos = corner.worldPosition;

                    // 避免重复处理同一个角点
                    if (processedCorners.Contains(worldPos))
                        continue;

                    // 查找共享这个角点的所有地形
                    var sharingTerrains = FindTerrainsAtCorner(terrains, worldPos);

                    if (sharingTerrains.Count >= 2) // 至少2个地形共享才需要修复
                    {
                        cornerGroups.Add(sharingTerrains);
                        processedCorners.Add(worldPos);
                    }
                }
            }

            return cornerGroups;
        }

        /// <summary>
        /// 异步修复角点组
        /// </summary>
        private async Task<bool> FixCornerGroupAsync(List<TerrainCornerInfo> cornerGroup)
        {
            if (cornerGroup.Count < 2) return false;

            try
            {
                // 在主线程收集角点数据
                var cornerDataList = new List<CornerData>();
                foreach (var corner in cornerGroup)
                {
                    float[,] terrainHeights = corner.terrain.terrainData.GetHeights(0, 0,
                        corner.terrain.terrainData.heightmapResolution,
                        corner.terrain.terrainData.heightmapResolution);

                    float normalizedHeight = terrainHeights[corner.heightmapZ, corner.heightmapX];
                    float worldHeight = normalizedHeight * corner.terrain.terrainData.size.y;

                    cornerDataList.Add(new CornerData
                    {
                        corner = corner,
                        originalHeight = worldHeight,
                        terrainHeights = terrainHeights
                    });
                }

                // 在后台线程计算目标高度
                float targetHeight = await Task.Run(() =>
                {
                    return cornerDataList.Select(cd => cd.originalHeight).Average();
                });

                // 在主线程应用修复
                foreach (var cornerData in cornerDataList)
                {
                    await FixCornerWithBlendingAsync(cornerData.corner, cornerData.terrainHeights, targetHeight);
                }

                var terrainNames = string.Join(", ", cornerGroup.Select(c => c.terrain.name));

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"角点修复失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 异步对单个角点进行融合修复
        /// </summary>
        private async Task FixCornerWithBlendingAsync(TerrainCornerInfo corner, float[,] terrainHeights, float targetHeight)
        {
            int res = corner.terrain.terrainData.heightmapResolution;
            float terrainHeight = corner.terrain.terrainData.size.y;

            // 捕获参数
            int localBlendWidth = blendWidth;
            AnimationCurve localBlendCurve = new AnimationCurve(blendCurve.keys);

            // 在后台线程进行计算
            await Task.Run(() =>
            {
                // 在角点周围的区域进行径向融合
                for (int dz = -localBlendWidth; dz <= localBlendWidth; dz++)
                {
                    for (int dx = -localBlendWidth; dx <= localBlendWidth; dx++)
                    {
                        int x = corner.heightmapX + dx;
                        int z = corner.heightmapZ + dz;

                        // 边界检查
                        if (x < 0 || x >= res || z < 0 || z >= res) continue;

                        // 计算距离角点的距离
                        float distance = Mathf.Sqrt(dx * dx + dz * dz);

                        // 只处理在融合半径内的点
                        if (distance <= localBlendWidth)
                        {
                            float originalHeight = terrainHeights[z, x] * terrainHeight;

                            // 使用距离计算融合因子
                            float blendFactor = localBlendWidth == 1 ? 0f : localBlendCurve.Evaluate(distance / localBlendWidth);
                            float newHeight = Mathf.Lerp(targetHeight, originalHeight, blendFactor);

                            terrainHeights[z, x] = newHeight / terrainHeight;
                        }
                    }
                }
            });

            // 在主线程应用结果
            corner.terrain.terrainData.SetHeights(0, 0, terrainHeights);
        }

        /// <summary>
        /// 查找在指定角点位置的所有地形
        /// </summary>
        private List<TerrainCornerInfo> FindTerrainsAtCorner(List<Terrain> terrains, Vector3 cornerWorldPos)
        {
            var sharingTerrains = new List<TerrainCornerInfo>();
            float tolerance = 0.1f;

            foreach (var terrain in terrains)
            {
                if (terrain?.terrainData == null) continue;

                var corners = GetTerrainCorners(terrain);

                foreach (var corner in corners)
                {
                    if (Vector3.Distance(corner.worldPosition, cornerWorldPos) < tolerance)
                    {
                        sharingTerrains.Add(corner);
                        break;
                    }
                }
            }

            return sharingTerrains;
        }

        /// <summary>
        /// 获取地形的所有角点信息
        /// </summary>
        private List<TerrainCornerInfo> GetTerrainCorners(Terrain terrain)
        {
            var corners = new List<TerrainCornerInfo>();
            Vector3 pos = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            int res = terrain.terrainData.heightmapResolution;

            // 四个角点: 左下、右下、左上、右上
            corners.Add(new TerrainCornerInfo
            {
                terrain = terrain,
                worldPosition = new Vector3(pos.x, 0, pos.z),
                heightmapX = 0,
                heightmapZ = 0
            });

            corners.Add(new TerrainCornerInfo
            {
                terrain = terrain,
                worldPosition = new Vector3(pos.x + size.x, 0, pos.z),
                heightmapX = res - 1,
                heightmapZ = 0
            });

            corners.Add(new TerrainCornerInfo
            {
                terrain = terrain,
                worldPosition = new Vector3(pos.x, 0, pos.z + size.z),
                heightmapX = 0,
                heightmapZ = res - 1
            });

            corners.Add(new TerrainCornerInfo
            {
                terrain = terrain,
                worldPosition = new Vector3(pos.x + size.x, 0, pos.z + size.z),
                heightmapX = res - 1,
                heightmapZ = res - 1
            });

            return corners;
        }

        /// <summary>
        /// 构建瓦片映射表
        /// </summary>
        private Dictionary<string, TerrainTileInfo> BuildTileMapping(List<Terrain> terrains)
        {
            var tileMapping = new Dictionary<string, TerrainTileInfo>();
            int validTileCount = 0;
            int invalidTileCount = 0;

            foreach (var terrain in terrains)
            {
                var tileInfo = ParseTerrainTile(terrain);
                if (tileInfo != null)
                {
                    tileMapping[tileInfo.TileKey] = tileInfo;
                    validTileCount++;
                }
                else
                {
                    invalidTileCount++;
                }
            }

            return tileMapping;
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
    }
}