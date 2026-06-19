using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
// using UnityEngine.Rendering.HighDefinition;

[InternalBufferCapacity(8)]
public struct SpawnPosition : IBufferElementData
{
    public float3 Value;
    public int MeshIndex; 
}

public struct ChunkSpawnRequest : IComponentData
{
    public Entity Prototype;
    public FixedString64Bytes ChunkGuid;
    public int CurrentIndex;
    public bool isRandom;
    public float3 appointEuler;
    public float appointScale;
    public float tileScale;
}

public struct SpawnedByChunk : ISharedComponentData
{
    public FixedString64Bytes ChunkGuid;
}

/// <summary>
/// 风力数据组件 - 存储每个树木实体的风力相关数据
/// </summary>
public struct TreeWindData : IComponentData
{
    public float3 OriginalPosition;  // 原始位置
    public quaternion OriginalRotation;  // 原始旋转
    public float OriginalScale;  // 原始缩放
    public float PhaseOffset;  // 相位偏移（用于随机化摇摆）
}

/// <summary>
/// 标记实体需要风力效果
/// </summary>
public struct WindAffectedTag : IComponentData { }

/// <summary>
/// 全局风力设置组件 - 作为单例实体存在
/// </summary>
public struct GlobalWindSettings : IComponentData
{
    public float WindStrength;    // 风力强度 (0-1)
    public float WindAngle;       // 风向角度（弧度）
    public float WindFrequency;   // 风吹频率
    public float MaxTiltDegrees;  // 最大倾斜角度
}
