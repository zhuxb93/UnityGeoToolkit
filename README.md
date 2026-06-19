# UnityGeoToolkit

这是一个面向 Unity 6000 的个人地理工具箱仓库，整理 64 位原生容器、二进制序列化、地理数学、编辑器导入框架、地形处理、道路生成、雷达体素、3D Tiles 解析和若干场景工具。

## 亮点导航
- NativeCollections64：支持 ulong 长度的 NativeArray64 / NativeList64 / UnsafeList64 与 IJobParallelFor64。
- GeoMath：双精度向量、瓦片行列号、经纬度/墨卡托转换和 Delaunay 基础算法。
- EditorImporter：插件式编辑器导入框架与通用工具窗口。
- Terrain：地形瓦片接缝检测、修复和高度平滑。
- RoadGen：基于 Unity Splines 的道路/铁路网格生成与手绘道路工具。
- ThirdParty/Unity3DTiles：基于 Unity3DTiles 的 3D Tiles 加载、遍历和缓存路线。

## 构建与运行
1. 在 Unity Package Manager 中以本地包方式添加本目录。
2. 安装 package.json 中列出的 Unity 官方依赖。
3. 先从 Samples~/README.md 的合成样条和合成瓦片数据开始验证；本仓不包含真实业务数据、真实服务地址或商业资产。

## 工程笔记
这个仓库的重点不是做一个完整平台，而是把多次项目中沉淀出的“地理编辑器基础件”拆出来：大数据量容器、地理坐标数学、导入器骨架、地形和道路工具各自独立，方便以后按需复用。

## 与其它仓库的关系
- GeoMath 可与 CesiumforUnrealSDK 的 CoordinateConverter 对照。
- Unity3DTiles 路线和两个 Cesium 仓库形成生态对比。
- 道路、地形和编辑器导入框架可作为 Unity 地理内容生产工具链的基础层。
