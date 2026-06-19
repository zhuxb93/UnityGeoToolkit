using UnityEngine;
using Unity.Mathematics;
using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

public class GPUChunkSpawner : MonoBehaviour
{
    [Header("渲染资源")]
    public Material[] Materials;
    public Mesh[] Meshes;
    
    [Header("LOD 设置")]
    public float[] lodDistances = new float[] { 512f, 1024f, 2048f, 3072f,4096f,5120f,6144f  };
    public int[] lodSparseStep = new int[] { 3, 5, 10, 15, 20, 40, 80 };
    public float maxRenderDistance = 5000f;
    [Header("数据源")]
    public List<TextAsset> textAsset = new List<TextAsset>();

    
    [Header("LOD 过渡动画")]
    public bool enableLODTransition = true;
    
    public float lodTransitionDuration = 0.3f;
    
    [Header("实例设置")]
    public bool isRandom = true;
    public float3 appointEuler = float3.zero;
    public float appointScale = 1f;
    public float tileScale = 1f;
    
   
    public float tileSize = 2048;
    public string dataExt = "-Tree";
    public string terrainExt = "-terrainData";
    
    [Header("相机")]
    public GameObject cameraGo;
    public float loadDistance = 3000f;
    
    [Tooltip("每帧最大更新的 Chunk 数量")]
    public int maxChunksPerFrame = 2;
    
    [Header("调试")]
    public bool enableDebugLog = false;
    public bool drawGizmos = false;
    
    // 数据结构
    private Dictionary<string, ChunkData> chunkDataDict = new Dictionary<string, ChunkData>();
    private Dictionary<string, bool> chunkLoadedState = new Dictionary<string, bool>();
    private Queue<string> pendingLoadQueue = new Queue<string>();
    private Queue<string> pendingUnloadQueue = new Queue<string>();
    private HashSet<string> pendingLoadSet = new HashSet<string>();      
    private HashSet<string> pendingUnloadSet = new HashSet<string>();   
    
    private Dictionary<string, ChunkRenderData> chunkRenderData = new Dictionary<string, ChunkRenderData>();
    
    private Camera mainCamera;
    private Vector3 lastCameraPos;
    private Plane[] frustumPlanes = new Plane[6];
    private float lastCheckTime = 0f;
    private const float CHECK_INTERVAL = 0.1f;  
    
    private float loadDistanceSqr;
    private float maxRenderDistanceSqr;
    private float[] lodDistancesSqr;
    
    private int totalInstanceCount = 0;
    private int visibleInstanceCount = 0;
    
    private class ChunkData
    {
        public Vector3 centerPos;
        public float3[] positions;
        public int totalCount;
    }
    
    private class ChunkRenderData
    {
        public Matrix4x4[][] matricesByLOD;               
        public Matrix4x4[][] batchArrays;                
        public int[] countByLOD;                          
        public Vector3[] baseScales;                      
        public MaterialPropertyBlock propBlock;
        public bool isVisible;
        public int meshIndex;
        
        // LOD 过渡动画
        public int currentLOD;                            
        public int previousLOD;                            
        public float transitionProgress;                   
        public bool isTransitioning;                      
    }
    
    [BurstCompile]
    private struct ChunkCullingJob : IJobParallelFor
    {
        [ReadOnly] public float3 cameraPos;
        [ReadOnly] public float maxRenderDistanceSqr;
        [ReadOnly] public NativeArray<float3> chunkCenters;
        [ReadOnly] public NativeArray<float> lodDistancesSqr;
        [ReadOnly] public NativeArray<float4> frustumPlanesNative;  
        [ReadOnly] public float cullingRadius;
        
        public NativeArray<int> targetLODs;       
        public NativeArray<bool> isVisible;      
        public NativeArray<float> distancesSqr;   
        
        public void Execute(int index)
        {
            float3 center = chunkCenters[index];
            float3 diff = cameraPos - center;
            float distSqr = diff.x * diff.x + diff.y * diff.y + diff.z * diff.z;
            distancesSqr[index] = distSqr;
            
            if (distSqr > maxRenderDistanceSqr)
            {
                isVisible[index] = false;
                targetLODs[index] = -1;
                return;
            }
            
            for (int i = 0; i < 6; i++)
            {
                float4 plane = frustumPlanesNative[i];
                float dist = plane.x * center.x + plane.y * center.y + plane.z * center.z + plane.w;
                if (dist < -cullingRadius)
                {
                    isVisible[index] = false;
                    targetLODs[index] = -1;
                    return;
                }
            }
            
            isVisible[index] = true;
            
            int lod = lodDistancesSqr.Length;  // 默认最远 LOD
            for (int i = 0; i < lodDistancesSqr.Length; i++)
            {
                if (distSqr < lodDistancesSqr[i])
                {
                    lod = i;
                    break;
                }
            }
            targetLODs[index] = lod;
        }
    }
    
    private NativeArray<float3> nativeChunkCenters;
    private NativeArray<float> nativeLodDistancesSqr;
    private NativeArray<float4> nativeFrustumPlanes;
    private NativeArray<int> nativeTargetLODs;
    private NativeArray<bool> nativeIsVisible;
    private NativeArray<float> nativeDistancesSqr;
    private bool nativeArraysInitialized = false;
    
    void Awake()
    {
        Debug.Log($"[GPUChunkSpawner] Awake() called on {gameObject.name}");
        Debug.Log($"[GPUChunkSpawner] Meshes={(Meshes != null ? Meshes.Length : 0)}, Materials={(Materials != null ? Materials.Length : 0)}, textAsset={(textAsset != null ? textAsset.Count : 0)}");
    }
    
    void Start()
    {
        Debug.Log($"[GPUChunkSpawner] Start() called on {gameObject.name}");
        Debug.Log($"[GPUChunkSpawner] Meshes={(Meshes != null ? Meshes.Length : 0)}, Materials={(Materials != null ? Materials.Length : 0)}, textAsset={(textAsset != null ? textAsset.Count : 0)}");
        
        if (Meshes == null || Meshes.Length == 0 || Materials == null || Materials.Length == 0)
        {
            Debug.LogError("GPUChunkSpawner: 需要配置 Meshes 和 Materials！");
            enabled = false;
            return;
        }
        
        // 确保所有材质启用 GPU Instancing
        for (int i = 0; i < Materials.Length; i++)
        {
            if (Materials[i] != null && !Materials[i].enableInstancing)
            {
                Materials[i].enableInstancing = true;
            }
        }
        
        mainCamera = cameraGo != null ? cameraGo.GetComponent<Camera>() : Camera.main;
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        
        tileScale = tileSize / 2455f;
        loadDistanceSqr = loadDistance * loadDistance;
        maxRenderDistanceSqr = maxRenderDistance * maxRenderDistance;
        lodDistancesSqr = new float[lodDistances.Length];
        for (int i = 0; i < lodDistances.Length; i++)
        {
            lodDistancesSqr[i] = lodDistances[i] * lodDistances[i];
        }
        
        InitializeChunkData();
        
        Debug.Log($"[GPUChunkSpawner] Initialized. chunkDataDict.Count={chunkDataDict.Count}, textAsset.Count={textAsset.Count}");
    }
    
    void InitializeChunkData()
    {
        Dictionary<string, Vector3> terrainCenters = new Dictionary<string, Vector3>();
        var terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        foreach (var terrain in terrains)
        {
            var name = terrain.gameObject.name.Replace(terrainExt, "");
            var pos = terrain.transform.position + new Vector3(tileSize * 0.5f, 0, tileSize * 0.5f);
            terrainCenters[name] = pos;
        }
        
        if (enableDebugLog)
        {
            Debug.Log($"GPUChunkSpawner: 找到 {terrains.Length} 个 Terrain，{textAsset.Count} 个数据文件");
        }
        
        foreach (var asset in textAsset)
        {
            if (asset == null) continue;
            
            var name = asset.name.Replace(dataExt, "");
            
            var data = JsonConvert.DeserializeObject<List<Vector3>>(asset.text);
            if (data == null || data.Count == 0)
            {
                continue;
            }
            
            Vector3 centerPos;
            if (terrainCenters.ContainsKey(name))
            {
                centerPos = terrainCenters[name];
            }
            else
            {
                
                Vector3 sum = Vector3.zero;
                foreach (var pos in data)
                {
                    sum += pos;
                }
                centerPos = sum / data.Count;
            }
            
            var positions = new float3[data.Count];
            for (int i = 0; i < data.Count; i++)
            {
                positions[i] = data[i];
            }
            
            chunkDataDict[name] = new ChunkData
            {
                centerPos = centerPos,
                positions = positions,
                totalCount = data.Count
            };
            chunkLoadedState[name] = false;
            totalInstanceCount += data.Count;
            
            if (enableDebugLog)
            {
                Debug.Log($"GPUChunkSpawner: 加载数据 {name}，实例数: {data.Count}，中心: {centerPos}");
            }
        }
    }
    
    void Update()
    {
        if (mainCamera == null) return;
        
        // 每次检查委托状态（用于调试）
        if (WindRotationProvider != null)
        {
            if (!windDebugLogged)
            {
                Debug.Log($"[GPUChunkSpawner] WindRotationProvider is SET!");
                windDebugLogged = true;
            }
        }
        else
        {
            if (windDebugLogged)
            {
                Debug.Log($"[GPUChunkSpawner] WindRotationProvider is NULL");
                windDebugLogged = false;
            }
        }
        
        Vector3 cameraPos = mainCamera.transform.position;
        
        GeometryUtility.CalculateFrustumPlanes(mainCamera, frustumPlanes);
        
        if (Time.time - lastCheckTime > CHECK_INTERVAL)
        {
            Profiler.BeginSample("GPUChunkSpawner.CheckChunks");
            CheckChunksVisibility(cameraPos);
            Profiler.EndSample();
            lastCheckTime = Time.time;
        }
        
        Profiler.BeginSample("GPUChunkSpawner.ProcessQueue");
        ProcessLoadUnloadQueue();
        Profiler.EndSample();
        
        Profiler.BeginSample("GPUChunkSpawner.Render");
        RenderAllChunks(cameraPos);
        Profiler.EndSample();
        
        lastCameraPos = cameraPos;
    }
    
    void CheckChunksVisibility(Vector3 cameraPos)
    {
        foreach (var kvp in chunkDataDict)
        {
            string chunkName = kvp.Key;
            ChunkData chunk = kvp.Value;
            
            if (chunk.positions == null) continue;
            
            float distanceSqr = (cameraPos - chunk.centerPos).sqrMagnitude;
            bool shouldBeLoaded = distanceSqr < loadDistanceSqr;
            bool isLoaded = chunkLoadedState[chunkName];
            
            if (shouldBeLoaded && !isLoaded)
            {
                if (!pendingLoadSet.Contains(chunkName))
                {
                    pendingLoadQueue.Enqueue(chunkName);
                    pendingLoadSet.Add(chunkName);
                }
            }
            else if (!shouldBeLoaded && isLoaded)
            {
                if (!pendingUnloadSet.Contains(chunkName))
                {
                    pendingUnloadQueue.Enqueue(chunkName);
                    pendingUnloadSet.Add(chunkName);
                }
            }
        }
    }
    
    void ProcessLoadUnloadQueue()
    {
        int processed = 0;
        
        while (pendingLoadQueue.Count > 0 && processed < maxChunksPerFrame)
        {
            string chunkName = pendingLoadQueue.Dequeue();
            pendingLoadSet.Remove(chunkName);
            LoadChunk(chunkName);
            processed++;
        }
        
        while (pendingUnloadQueue.Count > 0 && processed < maxChunksPerFrame)
        {
            string chunkName = pendingUnloadQueue.Dequeue();
            pendingUnloadSet.Remove(chunkName);
            UnloadChunk(chunkName);
            processed++;
        }
    }
    
    void LoadChunk(string chunkName)
    {
        if (!chunkDataDict.ContainsKey(chunkName)) return;
        if (chunkLoadedState[chunkName]) return;
        ChunkData chunk = chunkDataDict[chunkName];
        if (chunk.positions == null || chunk.positions.Length == 0) return;
        
        Profiler.BeginSample("GPUChunkSpawner.LoadChunk");
        
        int totalCount = chunk.positions.Length;
        int lodCount = lodDistances.Length + 1;
        
        var renderData = new ChunkRenderData();
        renderData.meshIndex = Mathf.Abs(chunkName.GetHashCode()) % Meshes.Length;
        renderData.matricesByLOD = new Matrix4x4[lodCount][];
        renderData.batchArrays = new Matrix4x4[lodCount][];
        renderData.countByLOD = new int[lodCount];
        renderData.propBlock = new MaterialPropertyBlock();
        renderData.currentLOD = lodCount - 1;  
        renderData.previousLOD = lodCount - 1;
        renderData.transitionProgress = 1f;
        renderData.isTransitioning = false;
        
        Quaternion fixedRotation = Quaternion.Euler(appointEuler.x, appointEuler.y, appointEuler.z);
        float fixedScale = appointScale * tileScale;
        
        // 计算各级 LOD 的 step，确保是嵌套关系（高 LOD 的 step 是低 LOD 的倍数）
        int[] lodSteps = new int[lodCount];
        lodSteps[0] = lodSparseStep.Length > 0 ? Mathf.Max(1, lodSparseStep[0]) : 1;
        for (int lod = 1; lod < lodCount; lod++)
        {
            int configStep = lod < lodSparseStep.Length ? lodSparseStep[lod] : lodSparseStep[lodSparseStep.Length - 1];
            // 确保每级 step 是上一级的整数倍，保证嵌套子集关系
            int prevStep = lodSteps[lod - 1];
            int multiplier = Mathf.Max(1, Mathf.RoundToInt((float)configStep / prevStep));
            lodSteps[lod] = prevStep * multiplier;
        }
        
        // 先生成 LOD0（最密集）的所有矩阵
        int lod0Step = lodSteps[0];
        int lod0Count = (totalCount + lod0Step - 1) / lod0Step;
        Matrix4x4[] lod0Matrices = new Matrix4x4[lod0Count];
        Vector3[] lod0BaseScales = enableLODTransition ? new Vector3[lod0Count] : null;
        int[] lod0Indices = new int[lod0Count];  // 记录原始索引，用于嵌套抽稀
        
        int outIndex = 0;
        for (int i = 0; i < totalCount; i++)
        {
            if (i % lod0Step == 0)
            {
                float3 pos = chunk.positions[i];
                Quaternion rotation;
                float scale;
                
                if (isRandom)
                {
                    uint seed = (uint)((int)(pos.x * 1000) ^ ((int)(pos.z * 1000) << 16));
                    seed = seed * 1103515245 + 12345;
                    rotation = Quaternion.Euler(0, seed % 360, 0);
                    scale = tileScale * (0.8f + (seed % 100) * 0.004f);
                }
                else
                {
                    rotation = fixedRotation;
                    scale = fixedScale;
                }
                
                lod0Matrices[outIndex] = Matrix4x4.TRS(
                    new Vector3(pos.x, pos.y, pos.z),
                    rotation,
                    new Vector3(scale, scale, scale)
                );
                
                if (lod0BaseScales != null)
                {
                    lod0BaseScales[outIndex] = new Vector3(scale, scale, scale);
                }
                
                lod0Indices[outIndex] = i;
                outIndex++;
                if (outIndex >= lod0Count) break;
            }
        }
        
        renderData.matricesByLOD[0] = lod0Matrices;
        renderData.countByLOD[0] = outIndex;
        renderData.baseScales = lod0BaseScales;
        
        // 逐级嵌套抽稀：LOD1 从 LOD0 抽稀，LOD2 从 LOD1 抽稀，依次类推
        // 这样保证 LOD(n+1) 是 LOD(n) 的真子集
        for (int lod = 1; lod < lodCount; lod++)
        {
            // 从上一级 LOD 中抽稀
            Matrix4x4[] prevLodMatrices = renderData.matricesByLOD[lod - 1];
            int prevLodCount = renderData.countByLOD[lod - 1];
            
            // 计算相对于上一级的抽稀倍率
            int prevStep = lodSteps[lod - 1];
            int currStep = lodSteps[lod];
            int relativeStep = Mathf.Max(1, currStep / prevStep);
            
            int lodCount_i = (prevLodCount + relativeStep - 1) / relativeStep;
            Matrix4x4[] lodMatrices = new Matrix4x4[lodCount_i];
            
            int lodOutIndex = 0;
            for (int j = 0; j < prevLodCount; j++)
            {
                // 从上一级 LOD 中按相对 step 抽稀
                if (j % relativeStep == 0)
                {
                    lodMatrices[lodOutIndex] = prevLodMatrices[j];
                    lodOutIndex++;
                    if (lodOutIndex >= lodCount_i) break;
                }
            }
            
            renderData.matricesByLOD[lod] = lodMatrices;
            renderData.countByLOD[lod] = lodOutIndex;
        }
        
        renderData.batchArrays[0] = new Matrix4x4[1023];
        for (int lod = 1; lod < lodCount; lod++)
        {
            renderData.batchArrays[lod] = renderData.batchArrays[0];
        }
        
        renderData.isVisible = true;
        chunkRenderData[chunkName] = renderData;
        chunkLoadedState[chunkName] = true;
        renderListDirty = true; 
        
        Profiler.EndSample();
    }
    
    void UnloadChunk(string chunkName)
    {
        if (!chunkRenderData.ContainsKey(chunkName)) return;
        
        chunkRenderData.Remove(chunkName);
        chunkLoadedState[chunkName] = false;
        renderListDirty = true;  
        
        if (enableDebugLog)
        {
            Debug.Log($"GPUChunkSpawner: 卸载 Chunk {chunkName}");
        }
    }
    
    private Matrix4x4[] transitionMatrices;
    private const int TRANSITION_BATCH_SIZE = 1023;
    
    private List<KeyValuePair<string, ChunkRenderData>> renderList = new List<KeyValuePair<string, ChunkRenderData>>();
    private bool renderListDirty = true;
    
    private int renderDebugFrameCount = 0;
    
    void RenderAllChunks(Vector3 cameraPos)
    {
        visibleInstanceCount = 0;
        
        if (renderListDirty)
        {
            renderList.Clear();
            foreach (var kvp in chunkRenderData)
            {
                renderList.Add(kvp);
            }
            renderListDirty = false;
            
            ReallocateNativeArrays();
        }
        
        int listCount = renderList.Count;
        
        // 每300帧打印一次渲染状态
        if (renderDebugFrameCount % 300 == 0)
        {
            Debug.Log($"[GPUChunkSpawner] RenderAllChunks: listCount={listCount}, WindProvider={(WindRotationProvider != null ? "SET" : "NULL")}");
        }
        renderDebugFrameCount++;
        
        if (listCount == 0) return;
        
        if (!nativeArraysInitialized || nativeChunkCenters.Length != listCount)
        {
            ReallocateNativeArrays();
        }
        
        for (int i = 0; i < listCount; i++)
        {
            var kvp = renderList[i];
            ChunkData chunk;
            if (chunkDataDict.TryGetValue(kvp.Key, out chunk))
            {
                nativeChunkCenters[i] = new float3(chunk.centerPos.x, chunk.centerPos.y, chunk.centerPos.z);
            }
        }
        
        for (int i = 0; i < 6; i++)
        {
            Plane p = frustumPlanes[i];
            nativeFrustumPlanes[i] = new float4(p.normal.x, p.normal.y, p.normal.z, p.distance);
        }
        
        var cullingJob = new ChunkCullingJob
        {
            cameraPos = new float3(cameraPos.x, cameraPos.y, cameraPos.z),
            maxRenderDistanceSqr = maxRenderDistanceSqr,
            chunkCenters = nativeChunkCenters,
            lodDistancesSqr = nativeLodDistancesSqr,
            frustumPlanesNative = nativeFrustumPlanes,
            cullingRadius = tileSize * 0.7f,
            targetLODs = nativeTargetLODs,
            isVisible = nativeIsVisible,
            distancesSqr = nativeDistancesSqr
        };
        
        JobHandle jobHandle = cullingJob.Schedule(listCount, 32);  
        jobHandle.Complete();  
        
        float deltaTime = Time.deltaTime;
        
        for (int idx = 0; idx < listCount; idx++)
        {
            if (!nativeIsVisible[idx])
            {
                renderList[idx].Value.isVisible = false;
                continue;
            }
            
            var kvp = renderList[idx];
            ChunkRenderData renderData = kvp.Value;
            ChunkData chunk;
            if (!chunkDataDict.TryGetValue(kvp.Key, out chunk)) continue;
            
            renderData.isVisible = true;
            
            int targetLOD = nativeTargetLODs[idx];
            int maxLOD = renderData.matricesByLOD.Length - 1;
            if (targetLOD > maxLOD) targetLOD = maxLOD;
            if (targetLOD < 0) targetLOD = 0;
            
            if (enableLODTransition && targetLOD != renderData.currentLOD)
            {
                renderData.previousLOD = renderData.currentLOD;
                renderData.currentLOD = targetLOD;
                renderData.transitionProgress = 0f;
                renderData.isTransitioning = true;
            }
            
            if (renderData.isTransitioning)
            {
                renderData.transitionProgress += deltaTime / lodTransitionDuration;
                if (renderData.transitionProgress >= 1f)
                {
                    renderData.transitionProgress = 1f;
                    renderData.isTransitioning = false;
                }
            }
            
            int meshIdx = renderData.meshIndex;
            if (meshIdx >= Meshes.Length) meshIdx = 0;
            Mesh mesh = Meshes[meshIdx];
            Material material = Materials[meshIdx < Materials.Length ? meshIdx : 0];
            
            if (enableLODTransition && renderData.isTransitioning)
            {
                RenderWithTransition(mesh, material, renderData, chunk);
            }
            else
            {
                int currentLOD = renderData.currentLOD;
                Matrix4x4[] matrices = renderData.matricesByLOD[currentLOD];
                int renderCount = renderData.countByLOD[currentLOD];
                if (matrices != null && renderCount > 0)
                {
                    RenderChunkInstanced(mesh, material, matrices, renderCount, renderData.batchArrays[0], renderData.propBlock);
                    visibleInstanceCount += renderCount;
                }
            }
        }
    }
    
    void ReallocateNativeArrays()
    {
        DisposeNativeArrays();
        
        int count = renderList.Count;
        if (count == 0) return;
        
        nativeChunkCenters = new NativeArray<float3>(count, Allocator.Persistent);
        nativeTargetLODs = new NativeArray<int>(count, Allocator.Persistent);
        nativeIsVisible = new NativeArray<bool>(count, Allocator.Persistent);
        nativeDistancesSqr = new NativeArray<float>(count, Allocator.Persistent);
        nativeFrustumPlanes = new NativeArray<float4>(6, Allocator.Persistent);
        
        nativeLodDistancesSqr = new NativeArray<float>(lodDistancesSqr.Length, Allocator.Persistent);
        for (int i = 0; i < lodDistancesSqr.Length; i++)
        {
            nativeLodDistancesSqr[i] = lodDistancesSqr[i];
        }
        
        nativeArraysInitialized = true;
    }
    
    void DisposeNativeArrays()
    {
        if (nativeChunkCenters.IsCreated) nativeChunkCenters.Dispose();
        if (nativeLodDistancesSqr.IsCreated) nativeLodDistancesSqr.Dispose();
        if (nativeFrustumPlanes.IsCreated) nativeFrustumPlanes.Dispose();
        if (nativeTargetLODs.IsCreated) nativeTargetLODs.Dispose();
        if (nativeIsVisible.IsCreated) nativeIsVisible.Dispose();
        if (nativeDistancesSqr.IsCreated) nativeDistancesSqr.Dispose();
        nativeArraysInitialized = false;
    }
    
    void RenderWithTransition(Mesh mesh, Material material, ChunkRenderData renderData, ChunkData chunk)
    {
        float t = renderData.transitionProgress;
        // 使用线性插值，避免中间加速的感觉
        // 可选其他缓动：
        // float easeT = t;                                    // 线性
        // float easeT = t * t;                                // ease-in（慢进快出）
        float easeT = 1f - (1f - t) * (1f - t);             // ease-out（快进慢出）
        // float easeT = t * t * (3f - 2f * t);                // smoothstep（两端慢中间快）
        // float easeT = t * t * t * (t * (6f * t - 15f) + 10f); // smootherstep
        // float easeT = 1f - (1f - t) * (1f - t);  // ease-out：开始快，结束慢，更自然 
        
        int prevLOD = renderData.previousLOD;
        int currLOD = renderData.currentLOD;
        int prevCount = renderData.countByLOD[prevLOD];
        int currCount = renderData.countByLOD[currLOD];
        Matrix4x4[] prevMatrices = renderData.matricesByLOD[prevLOD];
        Matrix4x4[] currMatrices = renderData.matricesByLOD[currLOD];
        Vector3[] baseScales = renderData.baseScales;
        
      
        if (transitionMatrices == null || transitionMatrices.Length < TRANSITION_BATCH_SIZE)
        {
            transitionMatrices = new Matrix4x4[TRANSITION_BATCH_SIZE];
        }
        
      
        bool isUpgrade = currCount > prevCount; 
        
        if (isUpgrade)
        {
       
            RenderChunkInstanced(mesh, material, prevMatrices, prevCount, renderData.batchArrays[0], renderData.propBlock);
            visibleInstanceCount += prevCount;
            
            float scale = easeT;
            RenderScaledInstances(mesh, material, currMatrices, prevCount, currCount - prevCount, scale, renderData.propBlock);
            visibleInstanceCount += currCount - prevCount;
        }
        else
        {
            RenderChunkInstanced(mesh, material, currMatrices, currCount, renderData.batchArrays[0], renderData.propBlock);
            visibleInstanceCount += currCount;
            
            float scale = 1f - easeT;
            RenderScaledInstances(mesh, material, prevMatrices, currCount, prevCount - currCount, scale, renderData.propBlock);
        }
    }
    
    void RenderScaledInstances(Mesh mesh, Material material, Matrix4x4[] matrices, int startIndex, int count, float scaleFactor, MaterialPropertyBlock props)
    {
        if (count <= 0 || startIndex >= matrices.Length) return;
        
        int actualCount = Mathf.Min(count, matrices.Length - startIndex);
        int offset = 0;
        
        while (offset < actualCount)
        {
            int batchCount = Mathf.Min(actualCount - offset, TRANSITION_BATCH_SIZE);
            
            for (int i = 0; i < batchCount; i++)
            {
                int srcIndex = startIndex + offset + i;
                Matrix4x4 m = matrices[srcIndex];
                
                Vector3 pos = new Vector3(m.m03, m.m13, m.m23);
                Vector3 originalScale = m.lossyScale;
                Vector3 newScale = originalScale * scaleFactor;
                Quaternion rot = m.rotation;
                if (WindRotationProvider != null)
                    rot = WindRotationProvider(pos) * rot;
                
                transitionMatrices[i] = Matrix4x4.TRS(pos, rot, newScale);
            }
            
            Graphics.DrawMeshInstanced(
                mesh,
                0,
                material,
                transitionMatrices,
                batchCount,
                props,
                ShadowCastingMode.On,
                true,
                gameObject.layer
            );
            
            offset += batchCount;
        }
    }
    
    private Matrix4x4[] windBatchArray;

    /// <summary>
    /// External wind rotation provider. Set this delegate from your wind controller.
    /// Input: world position. Output: rotation quaternion to apply.
    /// </summary>
    public static System.Func<Vector3, Quaternion> WindRotationProvider;

    private bool windDebugLogged;
    private int windRenderFrameCount = 0;
    
    void RenderChunkInstanced(Mesh mesh, Material material, Matrix4x4[] matrices, int total, Matrix4x4[] batchArray, MaterialPropertyBlock props)
    {
        if (WindRotationProvider == null)
        {
            RenderChunkInstancedDirect(mesh, material, matrices, total, batchArray, props);
            return;
        }

        // 每300帧打印一次调试信息
        if (windRenderFrameCount % 300 == 0)
        {
            Debug.Log($"[GPUChunkSpawner] Wind active! Rendering {total} instances with wind rotation (frame {windRenderFrameCount})");
        }
        windRenderFrameCount++;

        if (windBatchArray == null || windBatchArray.Length < 1023)
            windBatchArray = new Matrix4x4[1023];

        const int batchSize = 1023;
        int offset = 0;

        while (offset < total)
        {
            int count = Mathf.Min(total - offset, batchSize);

            for (int i = 0; i < count; i++)
            {
                Matrix4x4 m = matrices[offset + i];
                Vector3 pos = new Vector3(m.m03, m.m13, m.m23);
                Quaternion windRot = WindRotationProvider(pos);
                windBatchArray[i] = Matrix4x4.TRS(pos, windRot * m.rotation, m.lossyScale);
            }

            Graphics.DrawMeshInstanced(mesh, 0, material, windBatchArray, count, props,
                ShadowCastingMode.On, true, gameObject.layer);

            offset += count;
        }
    }

    void RenderChunkInstancedDirect(Mesh mesh, Material material, Matrix4x4[] matrices, int total, Matrix4x4[] batchArray, MaterialPropertyBlock props)
    {
        if (total <= 1023)
        {
            Graphics.DrawMeshInstanced(mesh, 0, material, matrices, total, props,
                ShadowCastingMode.On, true, gameObject.layer);
            return;
        }

        const int batchSize = 1023;
        int offset = 0;

        while (offset < total)
        {
            int count = Mathf.Min(total - offset, batchSize);
            System.Array.Copy(matrices, offset, batchArray, 0, count);
            Graphics.DrawMeshInstanced(mesh, 0, material, batchArray, count, props,
                ShadowCastingMode.On, true, gameObject.layer);
            offset += count;
        }
    }
    
    void OnDestroy()
    {
        DisposeNativeArrays();
        chunkRenderData.Clear();
    }
    
    void OnDisable()
    {
        DisposeNativeArrays();
    }
    
    void OnGUI()
    {
        if (!enableDebugLog) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 350, 220));
        GUILayout.Label($"LOD Distances: {string.Join(", ", lodDistances)}");
        GUILayout.Label($"LOD Step: {string.Join(", ", lodSparseStep)}");
        GUILayout.Label($"Total Instances: {totalInstanceCount:N0}");
        GUILayout.Label($"Visible Instances: {visibleInstanceCount:N0}");
        GUILayout.Label($"Loaded Chunks: {chunkRenderData.Count}");
        GUILayout.Label($"Pending Load: {pendingLoadQueue.Count}");
        GUILayout.Label($"Pending Unload: {pendingUnloadQueue.Count}");
        GUILayout.EndArea();
    }
    
    void OnDrawGizmos()
    {
        if (!drawGizmos || chunkDataDict == null) return;
        
        foreach (var kvp in chunkDataDict)
        {
            ChunkData chunk = kvp.Value;
            bool isLoaded = chunkLoadedState.ContainsKey(kvp.Key) && chunkLoadedState[kvp.Key];
            
            Gizmos.color = isLoaded ? Color.green : Color.red;
            Gizmos.DrawWireCube(chunk.centerPos, new Vector3(tileSize, 300, tileSize));
        }
    }
}
