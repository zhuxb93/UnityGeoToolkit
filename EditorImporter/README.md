# EditorImporter

## 解决什么问题
编辑器导入器容易变成一次性窗口，后续难以复用。

## 实现思路
用 Core/Framework/Utilities 拆分导入配置、通用窗口和文件处理工具。

## 基本用法
在自己的 importer 里复用 EditorFileUtils、MaterialUtils、TileCoordinateConverter。

## 依赖
无特殊依赖；按根 README 安装 Unity/UE 常规依赖即可。
