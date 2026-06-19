using System;
using System.Collections.Generic;
using GeoToolkit;
using Newtonsoft.Json;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public class ChunkSpawnerForLOD : MonoBehaviour
{
    #region Inspector - Render
    [Header("Render Data")]
    public GameObject coarsePrefab;
    public GameObject finePrefab;
    #endregion

    #region Inspector - Spawn Config
    [Header("Spawn Config")]
    public bool isRandom = true;
    public float3 appointEuler = float3.zero;
    public float appointScale = 1f;
    public float tileScale = 1f;
    #endregion

    #region Inspector - Distance Control
    [Header("Chunk Distance Control")]
    public GameObject cameraGo;
    public bool isOpen = false;
    #endregion

    #region Inspector - Tile Config
    [Header("Tile Config")]
    public float tileSize = 2048;
    public string dataExt = "-Tree";
    public string terrainExt = "-terrainData";
    #endregion

    #region Inspector - Sampling Rule
    [Header("Sampling Rule")]
    public bool isForce = true;

    [Tooltip("isForce = false 且数据量超过该值时才进行求余")]
    public int noForceTileCount = 3000;

    public int test = 1;
    #endregion

    #region Inspector - Data Source
    [Header("Tree Position Json")]
    public List<TextAsset> textAssets = new();
    #endregion

    #region Runtime Cache

    private readonly Dictionary<string, TerrainChunkData> chunkDataMap = new();

    #endregion

    public float maxRenderDistance = 1024;
    public ComputeShader lodComputeShader;
    public float lodDistanceThreshold = 512;
    public float lodUpdateInterval = 1f;
    private EntityManager entityManager;

    // 每个 subMesh 对应一个 prototype（coarse / fine 各自一组）
    private List<Entity> renderPrototypesCoarse = new List<Entity>();
    private List<Entity> renderPrototypesFine = new List<Entity>();
    private Dictionary<MeshMaterialKey, int> meshMaterialPrototypeMapCoarse = new Dictionary<MeshMaterialKey, int>();
    private Dictionary<MeshMaterialKey, int> meshMaterialPrototypeMapFine = new Dictionary<MeshMaterialKey, int>();

    private List<FacilityGroup> facilityGroups = new List<FacilityGroup>();

    // 按地块分组的实体缓存：chunkKey -> facilityIndex -> entity list
    private Dictionary<string, Dictionary<int, List<Entity>>> activeEntitiesCoarseByChunk = new Dictionary<string, Dictionary<int, List<Entity>>>();
    private Dictionary<string, Dictionary<int, List<Entity>>> activeEntitiesFineByChunk = new Dictionary<string, Dictionary<int, List<Entity>>>();

    // 设施点与地块的映射关系
    private Dictionary<int, string> facilityToChunkMap = new Dictionary<int, string>();

    private string batchId;

    private Dictionary<string, List<int>> chunkFacilityIndices = new Dictionary<string, List<int>>();
    private Dictionary<string, ComputeBuffer> chunkPositionBuffers = new Dictionary<string, ComputeBuffer>();
    private Dictionary<string, ComputeBuffer> chunkNeedsFineBuffers = new Dictionary<string, ComputeBuffer>();
    private float lodTimer = 0f;
    private int pointCount = 0;

    #region Unity Lifecycle
    private void Start()
    {
        if (!isOpen) return;

        CacheTerrainChunks();
        LoadTreePositionData();

        // 按地块初始化ComputeBuffer
        InitializeChunkComputeBuffers();

        batchId = $"batch_{Guid.NewGuid():N}".Substring(0, 8);
        UpdateVisibleEntities();
        StartCoroutine(AddDisabledComponentNextFrame());
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

    private System.Collections.IEnumerator AddDisabledComponentNextFrame()
    {
        yield return null;

        if (entityManager == null) yield break;

        foreach (var chunkDict in activeEntitiesFineByChunk.Values)
        {
            foreach (var entityList in chunkDict.Values)
            {
                foreach (var e in entityList)
                {
                    if (entityManager.Exists(e) && !entityManager.HasComponent<Disabled>(e))
                        entityManager.AddComponent<Disabled>(e);
                }
            }
        }

        Debug.Log($"🟡 延迟一帧后已为所有精模实体添加 Disabled 组件（已隐藏）");
    }

    #endregion

    #region Terrain & Data Load
    private void CacheTerrainChunks()
    {
        var terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);

        foreach (var terrain in terrains)
        {
            string key = terrain.gameObject.name.Replace(terrainExt, "");
            Vector3 center =
                terrain.transform.position +
                new Vector3(tileSize * 0.5f, 0, tileSize * 0.5f);

            chunkDataMap[key] = new TerrainChunkData(center);
        }
    }

    private void LoadTreePositionData()
    {
        uint sourceCount = 0;
        uint sampledCount = 0;

        facilityGroups.Clear();
        meshMaterialPrototypeMapCoarse.Clear();
        meshMaterialPrototypeMapFine.Clear();
        renderPrototypesCoarse.Clear();
        renderPrototypesFine.Clear();
        activeEntitiesCoarseByChunk.Clear();
        activeEntitiesFineByChunk.Clear();
        facilityToChunkMap.Clear();
        chunkFacilityIndices.Clear(); // 新增：清空地块设施索引

        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        int globalFacilityIndex = 0;

        foreach (var asset in textAssets)
        {
            string key = asset.name.Replace(dataExt, "");
            if (!chunkDataMap.ContainsKey(key))
                continue;

            var positions = JsonConvert.DeserializeObject<List<Vector3>>(asset.text);
            if (positions == null || positions.Count == 0)
                continue;

            // 初始化该地块的设施索引列表
            if (!chunkFacilityIndices.ContainsKey(key))
                chunkFacilityIndices[key] = new List<int>();

            bool needSampling = !isForce && positions.Count - 1 > noForceTileCount;
            var result = new List<float3>(positions.Count);

            for (int i = 0; i < positions.Count; i++)
            {
                if (isForce || needSampling)
                {
                    if (i % test == 0)
                    {
                        var treePos = positions[i];
                        CollectPrefabData(treePos, globalFacilityIndex, key);
                        result.Add(treePos);

                        // 记录该设施点属于当前地块
                        chunkFacilityIndices[key].Add(globalFacilityIndex);
                        globalFacilityIndex++;
                    }
                }
            }

            if (needSampling)
                Debug.LogWarning($"非暴力求余采样: {key}, 原始数量: {positions.Count}");

            chunkDataMap[key].SpawnPositions = result.ToArray();
            sourceCount += (uint)positions.Count - 1;
            sampledCount += (uint)result.Count - 1;
        }

        pointCount = facilityGroups.Count;
        if (pointCount == 0)
            Debug.LogWarning("⚠️ 没有有效设施点被加载！");

        Debug.Log($"树木统计: {sourceCount} % {test} = {sampledCount}");
    }
    #endregion

    private void CollectPrefabData(float3 item, int facilityIndex, string chunkKey)
    {
        MeshFilter coarseMf = coarsePrefab.GetComponentInChildren<MeshFilter>();
        MeshRenderer coarseMr = coarsePrefab.GetComponentInChildren<MeshRenderer>();

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

        Vector3 localPos = item;
        Quaternion rotation = Quaternion.identity;
        Vector3 scale = Vector3.one * tileScale;

        FacilityGroup group = new FacilityGroup
        {
            position = localPos,
            rotation = rotation,
            scale = scale,
            coarsePrototypeIndices = new List<int>(),
            finePrototypeIndices = new List<int>(),
            chunkKey = chunkKey
        };

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

        facilityGroups.Add(group);
        facilityToChunkMap[facilityIndex] = chunkKey;
    }

    private void InitializeChunkComputeBuffers()
    {
        // 清理旧的缓冲区
        foreach (var buffer in chunkPositionBuffers.Values) buffer.Release();
        foreach (var buffer in chunkNeedsFineBuffers.Values) buffer.Release();
        chunkPositionBuffers.Clear();
        chunkNeedsFineBuffers.Clear();

        // 为每个地块创建独立的ComputeBuffer
        foreach (var chunkKey in chunkFacilityIndices.Keys)
        {
            var facilityIndices = chunkFacilityIndices[chunkKey];
            int chunkPointCount = facilityIndices.Count;

            if (chunkPointCount == 0) continue;

            // 创建该地块的位置缓冲区
            Vector3[] chunkPositions = new Vector3[chunkPointCount];
            for (int i = 0; i < chunkPointCount; i++)
            {
                int facilityIndex = facilityIndices[i];
                chunkPositions[i] = facilityGroups[facilityIndex].position;
            }

            var positionBuffer = new ComputeBuffer(chunkPointCount, sizeof(float) * 3);
            positionBuffer.SetData(chunkPositions);
            chunkPositionBuffers[chunkKey] = positionBuffer;

            // 创建该地块的LOD结果缓冲区
            var needsFineBuffer = new ComputeBuffer(chunkPointCount, sizeof(int));
            int[] initialNeedsFine = new int[chunkPointCount];
            needsFineBuffer.SetData(initialNeedsFine);
            chunkNeedsFineBuffers[chunkKey] = needsFineBuffer;
        }

        Debug.Log($"✅ 已为 {chunkPositionBuffers.Count} 个地块初始化ComputeBuffer");
    }

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

        // 清除旧的实体缓存
        foreach (var chunkDict in activeEntitiesCoarseByChunk.Values)
        {
            foreach (var entityList in chunkDict.Values)
            {
                foreach (var e in entityList)
                {
                    if (entityManager.Exists(e)) entityManager.DestroyEntity(e);
                }
            }
        }
        activeEntitiesCoarseByChunk.Clear();

        foreach (var chunkDict in activeEntitiesFineByChunk.Values)
        {
            foreach (var entityList in chunkDict.Values)
            {
                foreach (var e in entityList)
                {
                    if (entityManager.Exists(e)) entityManager.DestroyEntity(e);
                }
            }
        }
        activeEntitiesFineByChunk.Clear();

        // 按地块分组创建实体
        for (int i = 0; i < facilityGroups.Count; i++)
        {
            var group = facilityGroups[i];
            string chunkKey = group.chunkKey;

            // 初始化地块字典
            if (!activeEntitiesCoarseByChunk.ContainsKey(chunkKey))
                activeEntitiesCoarseByChunk[chunkKey] = new Dictionary<int, List<Entity>>();
            if (!activeEntitiesFineByChunk.ContainsKey(chunkKey))
                activeEntitiesFineByChunk[chunkKey] = new Dictionary<int, List<Entity>>();

            // 创建coarse实体
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
                coarseList.Add(e);
            }
            activeEntitiesCoarseByChunk[chunkKey][i] = coarseList;

            // 创建fine实体
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
                fineList.Add(e);
            }
            activeEntitiesFineByChunk[chunkKey][i] = fineList;
        }
    }

    private void UpdateLODWithComputeShader()
    {
        if (lodComputeShader == null || pointCount == 0)
            return;

        Vector3 camPos = cameraGo.transform.position;
        int processedPoints = 0; // 统计实际处理的点数

        // 只处理活跃地块
        foreach (var chunk in chunkDataMap)
        {
            string chunkKey = chunk.Key;
            float dist = Vector3.Distance(camPos, chunk.Value.Center);

            // 地块级别距离检测
            if (dist <= maxRenderDistance)
            {
                if (!chunkFacilityIndices.ContainsKey(chunkKey) ||
                    !chunkPositionBuffers.ContainsKey(chunkKey))
                    continue;

                var facilityIndices = chunkFacilityIndices[chunkKey];
                int chunkPointCount = facilityIndices.Count;

                if (chunkPointCount == 0) continue;

                processedPoints += chunkPointCount;

                // 获取该地块的ComputeBuffer
                var positionBuffer = chunkPositionBuffers[chunkKey];
                var needsFineBuffer = chunkNeedsFineBuffers[chunkKey];

                // 设置ComputeShader参数（只处理当前地块的数据）
                lodComputeShader.SetVector("_CameraPos", camPos);
                lodComputeShader.SetFloat("_DistanceThreshold", lodDistanceThreshold);
                lodComputeShader.SetInt("_PointCount", chunkPointCount);
                lodComputeShader.SetBuffer(0, "_Positions", positionBuffer);
                lodComputeShader.SetBuffer(0, "_NeedsFine", needsFineBuffer);

                // 分派计算（只计算当前地块的数据量）
                int threadGroups = Mathf.CeilToInt(chunkPointCount / 64f);
                lodComputeShader.Dispatch(0, threadGroups, 1, 1);

                // 获取计算结果
                int[] needsFine = new int[chunkPointCount];
                needsFineBuffer.GetData(needsFine);

                // 更新该地块内实体的显示状态
                UpdateChunkEntitiesLOD(chunkKey, facilityIndices, needsFine);
            }
        }

    }

    private void UpdateChunkEntitiesLOD(string chunkKey, List<int> facilityIndices, int[] needsFine)
    {
        if (!activeEntitiesCoarseByChunk.ContainsKey(chunkKey) ||
            !activeEntitiesFineByChunk.ContainsKey(chunkKey))
            return;

        var coarseChunkDict = activeEntitiesCoarseByChunk[chunkKey];
        var fineChunkDict = activeEntitiesFineByChunk[chunkKey];

        for (int i = 0; i < facilityIndices.Count; i++)
        {
            int facilityIndex = facilityIndices[i];
            bool wantFine = needsFine[i] == 1;

            // 更新coarse实体
            if (coarseChunkDict.TryGetValue(facilityIndex, out var coarseList))
            {
                foreach (var ce in coarseList)
                {
                    if (!entityManager.Exists(ce)) continue;

                    bool disabled = entityManager.HasComponent<Disabled>(ce);
                    if (wantFine && !disabled)
                        entityManager.AddComponent<Disabled>(ce);
                    else if (!wantFine && disabled)
                        entityManager.RemoveComponent<Disabled>(ce);
                }
            }

            // 更新fine实体
            if (fineChunkDict.TryGetValue(facilityIndex, out var fineList))
            {
                foreach (var fe in fineList)
                {
                    if (!entityManager.Exists(fe)) continue;

                    bool disabled = entityManager.HasComponent<Disabled>(fe);
                    if (wantFine && disabled)
                        entityManager.RemoveComponent<Disabled>(fe);
                    else if (!wantFine && !disabled)
                        entityManager.AddComponent<Disabled>(fe);
                }
            }
        }
    }
    private void OnDestroy()
    {
        // 释放所有ComputeBuffer
        foreach (var buffer in chunkPositionBuffers.Values) buffer.Release();
        foreach (var buffer in chunkNeedsFineBuffers.Values) buffer.Release();
        chunkPositionBuffers.Clear();
        chunkNeedsFineBuffers.Clear();
    }
    #region Helper Class
    private class TerrainChunkData
    {
        public Vector3 Center;
        public float3[] SpawnPositions;
        public bool IsSpawned;

        public TerrainChunkData(Vector3 center)
        {
            Center = center;
            IsSpawned = false;
        }
    }



    #endregion
}
