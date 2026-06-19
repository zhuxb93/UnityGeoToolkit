# UnityGeoToolkit

[English](README.md)

UnityGeoToolkit 是一份面向 Unity 6000 的个人地理工具箱仓库，整理了地理内容生产和运行时渲染中反复出现的基础能力：64 位原生容器、二进制序列化、双精度地理数学、编辑器导入框架、地形修复、道路生成、雷达体素、3D Tiles 加载和场景辅助工具。

仓库定位是“技术积累”，不是完整业务平台。所有真实项目数据、内网端点、账号密钥、商业美术资源和业务流程壳都已移除或替换为占位说明。

## 亮点导航

- `NativeCollections64/`：支持 `ulong` 长度的 `NativeArray64`、`NativeList64`、`UnsafeList64`，并提供 `IJobParallelFor64` 调度接口。
- `Serialization/`：面向大体量缓存和瓦片数据的高性能二进制序列化工具，包含 unsafe 直存路径。
- `GeoMath/`：双精度向量、经纬度/墨卡托/瓦片行列号转换、瓦片 ID 和 Delaunay 基础算法。
- `EditorImporter/`：插件式编辑器导入框架，包含导入器骨架、通用窗口和文件/材质/瓦片坐标工具。
- `Terrain/`：相邻地形瓦片接缝检测、修复和高度平滑。
- `RoadGen/`：基于 Unity Splines 的道路/铁路网格生成、交叉口拼接和手绘道路工具。
- `RadarVoxel/` 与 `RadarEnvelope/`：雷达探测体素化和半球/扇形/环形扫描包络可视化。
- `ThirdParty/Unity3DTiles/`：基于 Unity3DTiles 的 B3DM/PNTS 解析、遍历、SSE、请求管理和缓存路线。

## 安装与依赖

1. 在 Unity Package Manager 中选择 `Add package from disk...`，指向本目录的 `package.json`。
2. 使用 Unity 6000 或兼容版本。
3. 安装 `package.json` 中声明的依赖，尤其是 `mathematics`、`burst`、`collections`、`newtonsoft-json` 和 `com.unity.splines`。
4. 本仓不包含真实地理数据或在线服务。示例建议从 `Samples~/README.md` 中的合成样条、合成瓦片和公开坐标开始。

## 使用建议

如果只想看核心亮点，建议先读 `NativeCollections64/` 和 `GeoMath/`。如果要搭建地理内容导入工具链，再从 `EditorImporter/`、`Terrain/` 和 `RoadGen/` 开始。`ThirdParty/Unity3DTiles/` 已按第三方代码处理，公开使用前请结合 `待本人确认清单.md` 复核改动比例和叙事边界。

## 脱敏与许可

- 私有品牌命名、业务地名、真实坐标清单、内网地址、密钥和商业资源均已清理。
- `LICENSE` 仅覆盖本人原创和改写部分。
- Triangle.NET、Unity3DTiles、Unity 官方包和 Newtonsoft Json 等第三方依赖按各自许可使用，详情见 `THIRD_PARTY_NOTICES.md`。
- 复核记录见 `脱敏复核报告.md`。

## 与其它仓库的关系

- `GeoMath` 可与 `CesiumforUnrealSDK` 的 `CoordinateConverter` 对照阅读。
- `Unity3DTiles` 路线可与两个 Cesium 仓库形成对比：一个强调自研 3D Tiles 加载，一个依赖成熟 Cesium 生态。
- 道路、地形、导入器和雷达工具可作为 Unity 地理编辑器工具链的基础层。

## 当前状态

本仓已完成模块抽取、中文模块说明、英文入口说明、第三方许可清单和脱敏复核。尚未在 Unity Editor 中完成真实导入编译，公开使用前建议先在 Unity 6000 工程中跑一轮本地包导入验证。
