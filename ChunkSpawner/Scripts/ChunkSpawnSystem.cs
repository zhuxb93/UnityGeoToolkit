// using Unity.Burst;
// using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
// using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ChunkSpawnSystem : SystemBase
{
    private const int MaxPerFrame = 1500;
    private EndSimulationEntityCommandBufferSystem _ecbSystem;

    protected override void OnCreate()
    {
        _ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var ecb = _ecbSystem.CreateCommandBuffer().AsParallelWriter();
        var maxPerFrame = MaxPerFrame;
        var time = SystemAPI.Time.ElapsedTime;

        Entities
            .WithName("ChunkSpawner")
            .ForEach((Entity controller, int entityInQueryIndex,
                ref ChunkSpawnRequest request,
                in DynamicBuffer<SpawnPosition> positions) =>
            {
                int remaining = positions.Length - request.CurrentIndex;
                int count = math.min(remaining, maxPerFrame);

                var random = new Unity.Mathematics.Random((uint)(entityInQueryIndex + 1) * 0x9F89ABC1);

                for (int i = 0; i < count; i++)
                {
                    int index = request.CurrentIndex + i;
                    float3 pos = positions[index].Value;
                    int meshIndex = positions[index].MeshIndex;
                    float3 euler = float3.zero;
                    float scale = 0;
                    if (request.isRandom)
                    {
                        euler = random.NextFloat3(new float3(0, 0, 0), new float3(0, 2 * math.PI, 0));
                        scale = random.NextFloat(0.35f, 0.7f) * request.tileScale;
                    }
                    else
                    {
                        euler = request.appointEuler;
                        scale = request.appointScale;
                    }

                    var instance = ecb.Instantiate(entityInQueryIndex, request.Prototype);

                    // 计算旋转
                    quaternion rotation = quaternion.EulerXYZ(euler);
                    
                    //设置 Transform
                    ecb.SetComponent(entityInQueryIndex, instance, new LocalTransform
                    {
                        Position = pos,
                        Rotation = rotation,
                        Scale = scale
                    });

                    // 设置对应的 mesh/material
                    ecb.SetComponent(entityInQueryIndex, instance, new MaterialMeshInfo
                    {
                        Material = MaterialMeshInfo.ArrayIndexToStaticIndex(meshIndex),
                        Mesh = MaterialMeshInfo.ArrayIndexToStaticIndex(meshIndex),
                        SubMesh = 0
                    });

                    // 设置归属 Chunk Guid
                    ecb.AddSharedComponent(entityInQueryIndex, instance, new SpawnedByChunk
                    {
                        ChunkGuid = request.ChunkGuid
                    });

                    // 添加风力效果组件
                    ecb.AddComponent(entityInQueryIndex, instance, new TreeWindData
                    {
                        OriginalPosition = pos,
                        OriginalRotation = rotation,
                        OriginalScale = scale,
                        PhaseOffset = random.NextFloat(0f, 6.28f)  // 随机相位偏移
                    });
                    ecb.AddComponent<WindAffectedTag>(entityInQueryIndex, instance);
                }

                request.CurrentIndex += count;

                if (request.CurrentIndex >= positions.Length)
                {
                    ecb.DestroyEntity(entityInQueryIndex, controller);
                }
                else
                {
                    ecb.SetComponent(entityInQueryIndex, controller, request);
                }
            }).ScheduleParallel();

        _ecbSystem.AddJobHandleForProducer(Dependency);
    }
}
