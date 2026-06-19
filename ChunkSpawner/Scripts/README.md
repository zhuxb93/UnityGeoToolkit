# ChunkSpawner 树木生成与风吹系统

## 概述

ChunkSpawner 是一个基于 Unity DOTS/ECS 的高性能树木生成系统，支持根据相机距离动态加载/卸载地形块上的树木，并实现了风吹摇摆效果。

---

## 文件结构

```
ChunkSpawner/Scrpits/
├── ChunkSpawner.cs      # 主控制器，管理地形块和树木生成
├── ChunkSpawnSystem.cs  # ECS 系统，处理实际的树木实体生成
├── SpawnPosition.cs     # 数据结构定义（组件、请求等）
├── GPUChunkSpawner.cs   # GPU 实例化渲染（可选）
├── TreeWindSystem.cs    # ECS 风力系统，处理树木摇摆
└── TreeWindController.cs # MonoBehaviour 风力控制器（位于 Assets/Script/）
```

---

## 核心组件

### 1. ChunkSpawner.cs

主控制器脚本，负责：
- 加载地形数据和树木位置数据
- 根据相机距离动态生成/销毁树木块
- 创建实体原型（Entity Prototype）

#### 关键参数

| 参数 | 类型 | 说明 |
|------|------|------|
| `Materials` | Material[] | 树木材质数组 |
| `Meshes` | Mesh[] | 树木网格数组 |
| `isOpen` | bool | 是否启用树木生成（默认 true） |
| `distance` | float | 相机检测距离 |
| `tileSize` | float | 地形块大小 |
| `test` | int | 采样间隔（用于减少树木数量） |

### 2. ChunkSpawnSystem.cs

ECS 系统，处理 `ChunkSpawnRequest` 请求：
- 批量实例化树木实体
- 为每个树木添加 `TreeWindData` 和 `WindAffectedTag` 组件
- 使用 Burst Compile 优化性能

### 3. SpawnPosition.cs

定义所有必需的 ECS 组件和数据结构：

```csharp
// 动态缓冲区，存储树木位置
public struct SpawnPosition : IBufferElementData { public float3 Value; public int MeshIndex; }

// 生成请求组件
public struct ChunkSpawnRequest : IComponentData { ... }

// 块标识组件
public struct SpawnedByChunk : ISharedComponentData { public string ChunkGuid; }

// 风力数据组件
public struct TreeWindData : IComponentData {
    public float3 OriginalPosition;    // 原始位置
    public quaternion OriginalRotation; // 原始旋转
    public float OriginalScale;         // 原始缩放
    public float PhaseOffset;          // 随机相位偏移
}

// 风力影响标签
public struct WindAffectedTag : IComponentData { }

// 全局风力设置（单例）
public struct GlobalWindSettings : IComponentData {
    public float WindStrength;    // 风力强度 (0-1)
    public float WindAngle;       // 风向角度（弧度）
    public float WindFrequency;   // 风吹频率
    public float MaxTiltDegrees;  // 最大倾斜角度
}
```

---

## 风吹系统

### TreeWindSystem.cs

ECS 系统实现树木风吹摇摆效果：

#### 特点

1. **高性能**：使用 Burst Compile 和 Job System 并行处理
2. **直接更新 LocalToWorld**：避免与 `LocalToWorldSystem` 产生 Job 依赖冲突
3. **在 LateSimulationSystemGroup 运行**：确保在渲染前完成更新

#### 工作原理

```
TreeWindSystem (LateSimulationSystemGroup)
    ↓
读取 GlobalWindSettings（风力参数）
    ↓
遍历所有带有 WindAffectedTag 的实体
    ↓
计算风吹摇摆（正弦波 + 相位偏移）
    ↓
直接更新 LocalToWorld 矩阵
    ↓
Dependency.Complete() 确保同步完成
```

#### 风摇摆算法

```csharp
// 计算风吹摇摆
float phase = pos.x * 0.1f + pos.z * 0.07f + phaseOffset;
float wave = sin(time * frequency + phase) * strength;
wave += sin(time * frequency * 2.3f + phase * 1.7f) * 0.3f * strength;

// 计算倾斜角度
float tiltDegrees = wave * maxTilt;
quaternion windRotation = AxisAngle(windAxis, tiltDegrees);

// 组合旋转
finalRotation = windRotation * originalRotation;
```

### TreeWindController.cs

MonoBehaviour 控制器，提供方便的风力控制接口：

#### 使用方式

1. 在场景中添加一个 GameObject 并挂载 `TreeWindController`
2. 通过代码或 Inspector 设置风力参数

```csharp
// 获取实例
var controller = TreeWindController.Instance;

// 设置风力强度（0-1），带过渡效果
controller.SetWindStrength(0.5f);

// 立即设置风力强度，无过渡
controller.SetWindStrengthImmediate(0.8f);

// 设置风向角度（度）
controller.SetWindAngle(45f);
```

#### 参数说明

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `windStrength` | float | 0 | 当前风力强度 (0-1) |
| `windAngle` | float | 45 | 风向角度（度） |
| `windFrequency` | float | 1.2 | 风吹频率 |
| `transitionDuration` | float | 3 | 过渡时间（秒） |

---

## 与天气系统集成

### LHZTEST.cs 集成示例

```csharp
// 在天气切换时设置树木风力
private void SetTreeWind(string weatherName)
{
    float windStrength = GetTreeWindForWeather(weatherName);
    if (treeWindController != null)
    {
        treeWindController.SetWindStrength(windStrength);
    }
}

// 不同天气对应的风力
private float GetTreeWindForWeather(string weatherName)
{
    switch (weatherName)
    {
        case "Maldives_1Year": return 0.2f;
        case "Maldives_25Year": return 0.45f;
        case "Maldives_50Year": return 0.7f;
        case "Maldives_100Year": return 1.0f;
        default: return 0f;
    }
}
```

---

## 性能优化

### 已实现的优化

1. **ECS 架构**：数据导向设计，内存布局紧凑
2. **Burst Compile**：C# 代码编译为原生代码
3. **Job System**：多线程并行处理
4. **Static 优化**：实体添加 `Static` 标签优化渲染
5. **距离剔除**：只生成相机附近的树木

### 性能建议

- 调整 `test` 参数减少树木数量
- 调整 `distance` 控制生成范围
- 风力为 0 时自动跳过更新

---

## 常见问题

### Q: 为什么使用 Static 组件？

A: `Static` 组件告诉 Unity 这些实体不会移动，可以优化渲染批处理。风吹效果通过直接修改 `LocalToWorld` 矩阵实现，绕过了 `LocalToWorldSystem` 的计算。

### Q: 为什么出现 Job 依赖冲突？

A: 当外部代码（如 Crest Water）同步调用 `Camera.Render()` 时，如果 ECS Job 还在运行，会产生冲突。解决方案是：
1. 在 `LateSimulationSystemGroup` 运行
2. 直接更新 `LocalToWorld` 而非 `LocalTransform`
3. 使用 `Dependency.Complete()` 同步完成

### Q: 如何添加新的树木类型？

A: 在 `ChunkSpawner` 的 Inspector 中：
1. 添加新的 Mesh 到 `Meshes` 数组
2. 添加对应的 Material 到 `Materials` 数组
3. 确保两个数组长度相同

---

## 与 RoadFacilityRendererForLOD 集成

路边设施渲染器 (`RoadFacilityRendererForLOD`) 也支持风力摇摆效果。

### 配置方法

1. 在 Inspector 中勾选 `Enable Wind Effect`
2. 在 `Wind Affected Types` 中填写需要应用风力效果的设施类型名称（逗号分隔）
   - 例如: `Tree,Tree_Big,Tree_Small,PalmTree,Palm`

### 工作原理

- 当设施类型匹配 `Wind Affected Types` 列表时，会自动为该设施的实体添加 `TreeWindData` 和 `WindAffectedTag` 组件
- 风力效果由同一个 `TreeWindSystem` 处理，与 ChunkSpawner 生成的树木共享风力参数
- 通过 `TreeWindController` 控制风力强度，所有树木（包括路边树木）会同步摇摆

### 注意事项

- 设施类型名称必须与 JSON 数据中的 `facilityType` 字段完全匹配
- 如果需要添加新的树木类型，只需在 `Wind Affected Types` 中添加对应的类型名称即可

---

## 版本历史

- **v1.0** - 初始版本，基础树木生成
- **v1.1** - 添加 ECS 风吹系统
- **v1.2** - 修复 Job 依赖冲突，改用直接更新 LocalToWorld
- **v1.3** - 移除 P 键逻辑，自动生成树木
- **v1.4** - 添加 RoadFacilityRendererForLOD 风力效果支持，路边树木也可随天气摇摆
