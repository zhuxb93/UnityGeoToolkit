using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Jobs;

/// <summary>
/// 风力系统 - 直接更新 LocalToWorld 矩阵来实现树木摇摆
/// 在 LateSimulationSystemGroup 中运行，避免与渲染系统产生 Job 冲突
/// 注意：TreeWindData、WindAffectedTag、GlobalWindSettings 定义在 SpawnPosition.cs 中
/// </summary>
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public partial class TreeWindSystem : SystemBase
{
    private Entity _windSettingsEntity;

    protected override void OnCreate()
    {
        // 创建全局风力设置单例实体
        _windSettingsEntity = EntityManager.CreateEntity(typeof(GlobalWindSettings));
        EntityManager.SetComponentData(_windSettingsEntity, new GlobalWindSettings
        {
            WindStrength = 0f,
            WindAngle = 0.785398f, // 45度（弧度）
            WindFrequency = 1.2f,
            MaxTiltDegrees = 12f
        });
    }

    protected override void OnUpdate()
    {
        // 获取全局风力设置
        if (!EntityManager.Exists(_windSettingsEntity)) return;

        var windSettings = EntityManager.GetComponentData<GlobalWindSettings>(_windSettingsEntity);

        float time = (float)SystemAPI.Time.ElapsedTime;
        float windStrength = windSettings.WindStrength;
        float windAngle = windSettings.WindAngle;
        float windFrequency = windSettings.WindFrequency;
        float maxTilt = windSettings.MaxTiltDegrees;

        // 风向对应的倾斜轴
        float sinAngle = math.sin(windAngle);
        float cosAngle = math.cos(windAngle);
        float3 windAxis = new float3(-sinAngle, 0, cosAngle);

        // 直接更新 LocalToWorld 矩阵
        // 注意：即使风力为0也要更新矩阵，确保树木初始渲染正确
        var job = new WindUpdateJob
        {
            Time = time,
            WindStrength = windStrength,
            WindFrequency = windFrequency,
            MaxTilt = maxTilt,
            WindAxis = windAxis,
            LocalToWorldType = GetComponentTypeHandle<LocalToWorld>(false),
            TreeWindDataType = GetComponentTypeHandle<TreeWindData>(true)
        };

        Dependency = job.ScheduleParallel(GetEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(WindAffectedTag), typeof(LocalToWorld), typeof(TreeWindData) }
        }), Dependency);
        
        // 同步完成，确保在 Crest Water 等外部系统调用 Camera.Render 之前完成
        Dependency.Complete();
    }

    /// <summary>
    /// 风力更新 Job - 直接更新 LocalToWorld 矩阵
    /// </summary>
    [Unity.Burst.BurstCompile]
    private struct WindUpdateJob : IJobChunk
    {
        public float Time;
        public float WindStrength;
        public float WindFrequency;
        public float MaxTilt;
        public float3 WindAxis;

        public ComponentTypeHandle<LocalToWorld> LocalToWorldType;
        [Unity.Collections.ReadOnly] public ComponentTypeHandle<TreeWindData> TreeWindDataType;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
        {
            var localToWorlds = chunk.GetNativeArray(LocalToWorldType);
            var windDatas = chunk.GetNativeArray(TreeWindDataType);

            for (int i = 0; i < chunk.Count; i++)
            {
                var windData = windDatas[i];

                // 计算风吹摇摆
                float phase = windData.OriginalPosition.x * 0.1f + windData.OriginalPosition.z * 0.07f + windData.PhaseOffset;
                float wave = math.sin(Time * WindFrequency + phase) * WindStrength;
                wave += math.sin(Time * WindFrequency * 2.3f + phase * 1.7f) * 0.3f * WindStrength;

                // 计算倾斜角度
                float tiltDegrees = wave * MaxTilt;
                float tiltRad = math.radians(tiltDegrees);

                // 创建风吹旋转
                quaternion windRotation = quaternion.AxisAngle(WindAxis, tiltRad);

                // 组合旋转：原始旋转 * 风吹旋转
                quaternion finalRotation = math.mul(windRotation, windData.OriginalRotation);

                // 构建 LocalToWorld 矩阵
                float3 position = windData.OriginalPosition;
                float scale = windData.OriginalScale;
                
                // 创建 TRS 矩阵
                float4x4 localToWorld = new float4x4(finalRotation, position);
                
                // 应用缩放
                localToWorld.c0.x *= scale;
                localToWorld.c1.y *= scale;
                localToWorld.c2.z *= scale;
                
                localToWorlds[i] = new LocalToWorld { Value = localToWorld };
            }
        }
    }

    /// <summary>
    /// 设置全局风力参数
    /// </summary>
    public static void SetWindParameters(float strength, float angleDegrees, float frequency = 1.2f)
    {
        var system = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<TreeWindSystem>();
        if (system != null && system.EntityManager.Exists(system._windSettingsEntity))
        {
            var settings = system.EntityManager.GetComponentData<GlobalWindSettings>(system._windSettingsEntity);
            settings.WindStrength = math.clamp(strength, 0f, 1f);
            settings.WindAngle = math.radians(angleDegrees);
            settings.WindFrequency = frequency;
            system.EntityManager.SetComponentData(system._windSettingsEntity, settings);
        }
    }

    /// <summary>
    /// 获取当前风力强度
    /// </summary>
    public static float GetWindStrength()
    {
        var system = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<TreeWindSystem>();
        if (system != null && system.EntityManager.Exists(system._windSettingsEntity))
        {
            var settings = system.EntityManager.GetComponentData<GlobalWindSettings>(system._windSettingsEntity);
            return settings.WindStrength;
        }
        return 0f;
    }
}