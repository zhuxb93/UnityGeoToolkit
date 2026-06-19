using System;
using System.Collections.Generic;
using GeoToolkit;
using Newtonsoft.Json;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace GeoDCBuildTools.RoadTool
{
    /// <summary>
    /// 从 JSON 加载设施点并用 ECS 渲染（每个 subMesh -> 一个 prototype；每个 facility 管理两套实体集合：简模 + 精模）
    /// </summary>
    public class RoadFacilityRendererForLOD : MonoBehaviour
    {
        [Header("数据源")]
        public TextAsset facilityJson;

        [Header("渲染控制")]
        public float maxRenderDistance = 1024;
        public bool autoSpawnOnStart = true;
        [Tooltip("用于计算距离的相机（默认 MainCamera）")]
        public Camera targetCamera;

        [Header("LOD控制")]
        public ComputeShader lodComputeShader;
        public float lodDistanceThreshold = 512;
        public float lodUpdateInterval = 1f;

        [Header("风力效果设置")]
        [Tooltip("是否启用树木风力摇摆效果")]
        public bool enableWindEffect = true;
        [Tooltip("需要应用风力效果的设施类型名称（逗号分隔，如: Tree,Tree_Big,Tree_Small）")]
        public string windAffectedTypes = "Tree,Tree_Big,Tree_Small,PalmTree,Palm";
        private HashSet<string> windAffectedTypeSet;

        private EntityManager entityManager;

        // 每个 subMesh 对应一个 prototype（coarse / fine 各自一组）
        private List<Entity> renderPrototypesCoarse = new List<Entity>();
        private List<Entity> renderPrototypesFine = new List<Entity>();
        private Dictionary<MeshMaterialKey, int> meshMaterialPrototypeMapCoarse = new Dictionary<MeshMaterialKey, int>();
        private Dictionary<MeshMaterialKey, int> meshMaterialPrototypeMapFine = new Dictionary<MeshMaterialKey, int>();

        // 每个 facility 的分组信息（位置、旋转、缩放）和对应的 prototype 索引列表
        private List<FacilityGroup> facilityGroups = new List<FacilityGroup>();
        
        // 存储每个设施的原始类型名称，用于判断是否应用风力效果
        private List<string> facilityTypes = new List<string>();

        // 运行时实例化出来的实体集合：facilityIndex -> entity list
        private Dictionary<int, List<Entity>> activeEntitiesCoarse = new Dictionary<int, List<Entity>>();
        private Dictionary<int, List<Entity>> activeEntitiesFine = new Dictionary<int, List<Entity>>();

        private string batchId;

        private ComputeBuffer needsFineBuffer;
        private ComputeBuffer positionBuffer;
        private float lodTimer = 0f;
        private int pointCount = 0; // 等于 facilityGroups.Count

        #region DataStruct
        [Serializable]
        private class FacilityItem
        {
            public string facilityType;
            public Position position;
            public Euler euler;
            public Scale scale;
        }

        [Serializable] private class Position { public float x, y, z; }
        [Serializable] private class Euler { public float x, y, z; }
        [Serializable] private class Scale { public float x, y, z; }

       
        #endregion

        private void Reset()
        {
#if UNITY_EDITOR
            var config = AssetDatabase.LoadAssetAtPath<GeoPlatformConfig>("Assets/GeoToolkit/Config/GeoPlatformConfig.asset");
            if (config != null)
            {
                lodComputeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Packages/com.geoearth.platformsdk/Runtime/GenerateRoadTool/DataRoad/DistanceLOD.compute");
                lodDistanceThreshold = (float)config.TerrainSize / 2;
                maxRenderDistance = (float)config.TerrainSize;
            }
#endif
        }


        void Start()
        {
            if (autoSpawnOnStart)
            {
                InitializeAndSpawnFromJson();
                // UpdateVisibleEntities moved to after InitializeAndSpawnFromJson, we'll instantiate there
            }
        }

        private void Update()
        {
            if (lodComputeShader == null || pointCount == 0) return;

            lodTimer += Time.deltaTime;
            if (lodTimer >= lodUpdateInterval)
            {
                lodTimer = 0f;
                UpdateLODWithComputeShader();
            }
        }

        void OnDestroy()
        {
            if (positionBuffer != null) { positionBuffer.Release(); positionBuffer = null; }
            if (needsFineBuffer != null) { needsFineBuffer.Release(); needsFineBuffer = null; }
        }

        public void InitializeAndSpawnFromJson()
        {
            if (facilityJson == null)
            {
                Debug.LogError("❌ 缺少 JSON 文件！");
                return;
            }

            if (targetCamera == null)
                targetCamera = Camera.main;

            List<FacilityItem> items;
            try
            {
                items = JsonConvert.DeserializeObject<List<FacilityItem>>(facilityJson.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ JSON 解析失败: {e.Message}");
                return;
            }

            if (items == null || items.Count == 0)
            {
                Debug.LogWarning("⚠️ JSON 内容为空");
                return;
            }

            CollectPrefabData(items, transform);
            InitializeComputeBuffers();
            batchId = $"batch_{Guid.NewGuid():N}".Substring(0, 8);

            // 实例化所有可见实体（默认显示简模，精模创建并保持禁用）
            UpdateVisibleEntities();

            // --- 延迟一帧添加 Disabled 组件隐藏所有实体 ---
            StartCoroutine(AddDisabledComponentNextFrame());
        }

        private System.Collections.IEnumerator AddDisabledComponentNextFrame()
        {
            // 延迟一帧等待所有实体完全注册到 ECS 世界
            yield return null;

            if (entityManager == null) yield break;

            //foreach (var kv in activeEntitiesCoarse)
            //{
            //    foreach (var e in kv.Value)
            //    {
            //        if (entityManager.Exists(e) && !entityManager.HasComponent<Disabled>(e))
            //            entityManager.AddComponent<Disabled>(e);
            //    }
            //}

            foreach (var kv in activeEntitiesFine)
            {
                foreach (var e in kv.Value)
                {
                    if (entityManager.Exists(e) && !entityManager.HasComponent<Disabled>(e))
                        entityManager.AddComponent<Disabled>(e);
                }
            }

        }

        private void CollectPrefabData(List<FacilityItem> items, Transform road)
        {
            facilityGroups.Clear();
            facilityTypes.Clear();
            meshMaterialPrototypeMapCoarse.Clear();
            meshMaterialPrototypeMapFine.Clear();
            renderPrototypesCoarse.Clear();
            renderPrototypesFine.Clear();

            // 初始化风力影响类型集合
            if (windAffectedTypeSet == null)
                windAffectedTypeSet = new HashSet<string>();
            else
                windAffectedTypeSet.Clear();
            
            if (enableWindEffect && !string.IsNullOrEmpty(windAffectedTypes))
            {
                string[] types = windAffectedTypes.Split(',');
                foreach (var t in types)
                {
                    string trimmed = t.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        windAffectedTypeSet.Add(trimmed);
                }
            }

            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            for (int itemIdx = 0; itemIdx < items.Count; itemIdx++)
            {
                var item = items[itemIdx];

                // 简模 prefab（通常只有 1 subMesh）
                GameObject coarsePrefab = Resources.Load<GameObject>($"RoadFacility/{item.facilityType}");
                // 精模 prefab（可能有多个 subMesh）
                GameObject finePrefab = Resources.Load<GameObject>($"RoadFacility/Model_{item.facilityType}");

                if (coarsePrefab == null)
                {
                    Debug.LogWarning($"⚠️ 找不到预制体(简模): {item.facilityType}");
                    continue;
                }

                MeshFilter coarseMf = coarsePrefab.GetComponentInChildren<MeshFilter>();
                MeshRenderer coarseMr = coarsePrefab.GetComponentInChildren<MeshRenderer>();
                if (coarseMf == null || coarseMr == null)
                {
                    Debug.LogWarning($"⚠️ 预制体 {item.facilityType} 缺少 Mesh 或 Renderer (简模)");
                    continue;
                }

                Mesh coarseMesh = coarseMf.sharedMesh;
                Material[] coarseMaterials = coarseMr.sharedMaterials;
                int coarseSubMeshCount = coarseMesh.subMeshCount;

                Mesh fineMesh = null;
                Material[] fineMaterials = null;
                int fineSubMeshCount = 0;
                if (finePrefab != null)
                {
                    MeshFilter fineMf = finePrefab.GetComponentInChildren<MeshFilter>();
                    MeshRenderer fineMr = finePrefab.GetComponentInChildren<MeshRenderer>();
                    if (fineMf != null && fineMr != null)
                    {
                        fineMesh = fineMf.sharedMesh;
                        fineMaterials = fineMr.sharedMaterials;
                        fineSubMeshCount = fineMesh.subMeshCount;
                    }
                }

                Vector3 localPos = new Vector3(item.position.x, item.position.y, item.position.z);
                Vector3 worldPos = road.TransformPoint(localPos);
                Quaternion rotation = road.rotation * Quaternion.Euler(item.euler.x, item.euler.y, item.euler.z);
                Vector3 scale = Vector3.Scale(road.lossyScale, new Vector3(item.scale.x, item.scale.y, item.scale.z));

                FacilityGroup group = new FacilityGroup
                {
                    position = worldPos,
                    rotation = rotation,
                    scale = scale,
                    coarsePrototypeIndices = new List<int>(),
                    finePrototypeIndices = new List<int>()
                };

                // --- 创建/复用 coarse prototypes（保留你原来的"每个 subMesh 一个 prototype" 方式） ---
                for (int sm = 0; sm < coarseSubMeshCount; sm++)
                {
                    Material mat = sm < coarseMaterials.Length ? coarseMaterials[sm] : coarseMaterials[0];
                    var key = new MeshMaterialKey(coarseMesh, mat);
                    if (!meshMaterialPrototypeMapCoarse.TryGetValue(key, out int protoIndex))
                    {
                        protoIndex = renderPrototypesCoarse.Count;
                        meshMaterialPrototypeMapCoarse[key] = protoIndex;

                        Entity proto = CreateSinglePrototype(coarseMesh, mat, protoIndex, sm);
                        renderPrototypesCoarse.Add(proto);
                    }
                    group.coarsePrototypeIndices.Add(protoIndex);
                }

                // --- 创建/复用 fine prototypes（精模每个 subMesh 对应一个 prototype） ---
                if (fineMesh != null && fineSubMeshCount > 0)
                {
                    for (int sm = 0; sm < fineSubMeshCount; sm++)
                    {
                        Material mat = sm < fineMaterials.Length ? fineMaterials[sm] : fineMaterials[0];
                        var key = new MeshMaterialKey(fineMesh, mat);
                        if (!meshMaterialPrototypeMapFine.TryGetValue(key, out int protoIndex))
                        {
                            protoIndex = renderPrototypesFine.Count;
                            meshMaterialPrototypeMapFine[key] = protoIndex;

                            Entity proto = CreateSinglePrototype(fineMesh, mat, protoIndex, sm);
                            renderPrototypesFine.Add(proto);
                        }
                        group.finePrototypeIndices.Add(protoIndex);
                    }
                }
                else
                {
                    // 如果没有精模，保持 finePrototypeIndices 为空（或可选地复用 coarse）
                }

                facilityGroups.Add(group);
                // 保存设施类型，用于后续判断是否应用风力效果
                facilityTypes.Add(item.facilityType);
            }

            pointCount = facilityGroups.Count;
            if (pointCount == 0)
                Debug.LogWarning("⚠️ 没有有效设施点被加载！");
        }

        private void InitializeComputeBuffers()
        {
            if (pointCount == 0) return;

            // 每个 facility 一个点，用于距离计算
            positionBuffer = new ComputeBuffer(pointCount, sizeof(float) * 3);
            needsFineBuffer = new ComputeBuffer(pointCount, sizeof(int));

            Vector3[] positions = new Vector3[pointCount];
            int[] initialNeedsFine = new int[pointCount];
            for (int i = 0; i < pointCount; i++)
            {
                positions[i] = facilityGroups[i].position;
                initialNeedsFine[i] = 0;
            }

            positionBuffer.SetData(positions);
            needsFineBuffer.SetData(initialNeedsFine);
        }

        // 你原来的 CreateSinglePrototype 保留不变（每个 subMesh -> 一个 prototype）
        private Entity CreateSinglePrototype(Mesh mesh, Material material, int prototypeIndex, int subMeshIndex)
        {
            try
            {
                Entity prototype = entityManager.CreateEntity();
                entityManager.AddComponent<LocalTransform>(prototype);
                entityManager.AddComponent<LocalToWorld>(prototype);
                entityManager.AddComponent<AutoRenderTag>(prototype);

                var renderMeshArray = new RenderMeshArray(new[] { material }, new[] { mesh });
                var meshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0);
                meshInfo.SubMesh = (ushort)subMeshIndex;

                RenderMeshUtility.AddComponents(
                    prototype,
                    entityManager,
                    new RenderMeshDescription { FilterSettings = RenderFilterSettings.Default },
                    renderMeshArray,
                    meshInfo
                );

                entityManager.SetComponentData(prototype, new AutoRenderTag
                {
                    BatchId = "proto",
                    SourceIndex = prototypeIndex
                });
                return prototype;
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ 创建原型失败: {e.Message}");
                return Entity.Null;
            }
        }

        private void UpdateVisibleEntities()
        {
            if (entityManager == null) return;

            // 先清除旧的（如果有），再创建
            foreach (var kv in activeEntitiesCoarse)
            {
                foreach (var e in kv.Value) if (entityManager.Exists(e)) entityManager.DestroyEntity(e);
            }
            activeEntitiesCoarse.Clear();

            foreach (var kv in activeEntitiesFine)
            {
                foreach (var e in kv.Value) if (entityManager.Exists(e)) entityManager.DestroyEntity(e);
            }
            activeEntitiesFine.Clear();

            // 为每个 facility 实例化 coarse 和 fine 的实体
            for (int i = 0; i < facilityGroups.Count; i++)
            {
                var group = facilityGroups[i];
                
                // 检查当前设施是否需要风力效果（使用前缀匹配，如 "Tree" 匹配 "Tree_4", "Tree_Big" 等）
                bool shouldApplyWind = enableWindEffect && i < facilityTypes.Count && 
                                       IsWindAffectedType(facilityTypes[i]);

                var coarseList = new List<Entity>();
                foreach (int protoIndex in group.coarsePrototypeIndices)
                {
                    if (protoIndex < 0 || protoIndex >= renderPrototypesCoarse.Count) continue;
                    Entity proto = renderPrototypesCoarse[protoIndex];
                    Entity e = entityManager.Instantiate(proto);
                    entityManager.SetComponentData(e, new LocalTransform
                    {
                        Position = group.position,
                        Rotation = group.rotation,
                        Scale = group.scale.x
                    });
                    entityManager.SetComponentData(e, new AutoRenderTag
                    {
                        BatchId = batchId,
                        SourceIndex = i
                    });
                    
                    // 如果是树木类型，添加风力组件
                    if (shouldApplyWind)
                    {
                        AddWindComponents(e, group.position, group.rotation, group.scale.x);
                    }
                    
                    coarseList.Add(e);
                }
                activeEntitiesCoarse[i] = coarseList;

                var fineList = new List<Entity>();
                foreach (int protoIndex in group.finePrototypeIndices)
                {
                    if (protoIndex < 0 || protoIndex >= renderPrototypesFine.Count) continue;
                    Entity proto = renderPrototypesFine[protoIndex];
                    Entity e = entityManager.Instantiate(proto);
                    entityManager.SetComponentData(e, new LocalTransform
                    {
                        Position = group.position,
                        Rotation = group.rotation,
                        Scale = group.scale.x
                    });
                    entityManager.SetComponentData(e, new AutoRenderTag
                    {
                        BatchId = batchId,
                        SourceIndex = i
                    });
                    
                    // 如果是树木类型，添加风力组件
                    if (shouldApplyWind)
                    {
                        AddWindComponents(e, group.position, group.rotation, group.scale.x);
                    }
                    
                    // 默认隐藏精模
                    //entityManager.SetEnabled(e, false);
                    fineList.Add(e);
                }
                activeEntitiesFine[i] = fineList;
            }
        }

        /// <summary>
        /// 检查设施类型是否需要应用风力效果（使用前缀匹配）
        /// 例如：配置 "Tree" 可以匹配 "Tree_4", "Tree_Big", "Tree_Small" 等
        /// </summary>
        private bool IsWindAffectedType(string facilityType)
        {
            if (windAffectedTypeSet == null || string.IsNullOrEmpty(facilityType))
                return false;
            
            foreach (var windType in windAffectedTypeSet)
            {
                if (facilityType.StartsWith(windType, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 为实体添加风力摇摆组件
        /// </summary>
        private void AddWindComponents(Entity entity, Vector3 position, Quaternion rotation, float scale)
        {
            // 重要：移除 LocalTransform 组件，因为 LocalToWorldSystem 会根据 LocalTransform 重新计算 LocalToWorld
            // 这会覆盖 TreeWindSystem 对 LocalToWorld 的风力更新
            if (entityManager.HasComponent<LocalTransform>(entity))
            {
                entityManager.RemoveComponent<LocalTransform>(entity);
            }
            
            // 添加风力数据组件
            entityManager.AddComponentData(entity, new TreeWindData
            {
                OriginalPosition = new float3(position.x, position.y, position.z),
                OriginalRotation = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w),
                OriginalScale = scale,
                PhaseOffset = UnityEngine.Random.Range(0f, 6.28f) // 随机相位偏移，使树木摇摆不同步
            });
            
            // 添加风力影响标签
            entityManager.AddComponent<WindAffectedTag>(entity);
            
            // 添加 Static 标签优化渲染（树木不会移动，只有旋转变化）
            entityManager.AddComponent<Static>(entity);
        }

        private void UpdateLODWithComputeShader()
        {
            if (Vector3.Distance(targetCamera.transform.position, transform.position) > maxRenderDistance) return;

            if (lodComputeShader == null || pointCount == 0) return;

            lodComputeShader.SetVector("_CameraPos", targetCamera.transform.position);
            lodComputeShader.SetFloat("_DistanceThreshold", lodDistanceThreshold);
            lodComputeShader.SetInt("_PointCount", pointCount);
            lodComputeShader.SetBuffer(0, "_Positions", positionBuffer);
            lodComputeShader.SetBuffer(0, "_NeedsFine", needsFineBuffer);

            int threadGroups = Mathf.CeilToInt(pointCount / 64f);
            lodComputeShader.Dispatch(0, threadGroups, 1, 1);

            int[] needsFine = new int[pointCount];
            needsFineBuffer.GetData(needsFine);

            for (int i = 0; i < needsFine.Length; i++)
            {
                bool wantFine = needsFine[i] == 1;

                // 如果没有实体，跳过（或按需惰性创建）
                if (!activeEntitiesCoarse.TryGetValue(i, out var coarseList) &&
                    !activeEntitiesFine.TryGetValue(i, out var fineList))
                    continue;

                // 控制简模显隐
                if (coarseList != null)
                {
                    foreach (var ce in coarseList)
                    {
                        if (!entityManager.Exists(ce)) continue;

                        bool hasDisabled = entityManager.HasComponent<Disabled>(ce);

                        // 如果需要精模，则隐藏简模（添加Disabled组件）
                        if (wantFine && !hasDisabled)
                            entityManager.AddComponent<Disabled>(ce);
                        // 如果不需要精模，则显示简模（移除Disabled组件）
                        else if (!wantFine && hasDisabled)
                            entityManager.RemoveComponent<Disabled>(ce);
                    }
                }

                // 控制精模显隐
                if (activeEntitiesFine.TryGetValue(i, out var fineList2))
                {
                    foreach (var fe in fineList2)
                    {
                        if (!entityManager.Exists(fe)) continue;

                        bool hasDisabled = entityManager.HasComponent<Disabled>(fe);

                        // 如果需要精模，则显示精模（移除Disabled组件）
                        if (wantFine && hasDisabled)
                            entityManager.RemoveComponent<Disabled>(fe);
                        // 如果不需要精模，则隐藏精模（添加Disabled组件）
                        else if (!wantFine && !hasDisabled)
                            entityManager.AddComponent<Disabled>(fe);
                    }
                }
            }
        }
    }

}

