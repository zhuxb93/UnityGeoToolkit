# RadarVoxel

## 解决什么问题
雷达探测范围可视化需要把空间离散为可检查的体素。

## 实现思路
按参数生成体素网格，并用 shader 区分遮挡和可探测区域。

## 基本用法
创建 RadarVoxelManager，设置半径、分辨率和障碍层。

## 依赖
无特殊依赖；按根 README 安装 Unity/UE 常规依赖即可。
