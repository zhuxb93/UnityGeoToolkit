using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using Unity.Mathematics;
using Unity.Entities.Graphics;
using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
// using Unity.VisualScripting;
using Unity.Transforms;
using TMPro;
// using UnityEngine.Rendering.HighDefinition;
// using System.Linq;

public class ChunkSpawner : MonoBehaviour
{
    public Material[] Materials;
    public Mesh[] Meshes;
    public bool isRandom = true;
    public float3 appointEuler = float3.zero;
    public float appointScale = 1;
    public List<TextAsset> textAsset = new List<TextAsset>();
    private Entity cachedPrototype;
    private string currentGuid;
    public Dictionary<string, (Vector3, float3[])> terrianPosDic = new Dictionary<string, (Vector3, float3[])>();
    public Dictionary<string, bool> terrianStateDic = new Dictionary<string, bool>();
    public GameObject cameraGo;
    public float distance;
    private Queue<string> processDatas = new Queue<string>();
    private List<Vector3> data;
    Entity controller;
    EntityManager em;
    EntityQuery query;
    public bool isOpen = true;  // 默认开启，自动生成树木
    public float tileSize = 2048;
    public float tileScale = 1;
    public string dataExt = "-Tree";
    public string terrainExt = "-terrainData";
    int count = 0;
    public int test = 1;
    [Header("暴力求余数")]
    public bool isForce = true;
    [Header("isForce= False时，超过这个数量，求余数")]
    public int noForceTileCount = 3000;
    void Start()
    {
        if (Meshes.Length == 0 || Materials.Length == 0)
        {
            this.enabled = false;
        }
        if (Meshes.Length != Materials.Length)
        {
            this.enabled = false;
        }
        var terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        for (int i = 0; i < terrains.Length; i++)
        {
            var name = terrains[i].gameObject.name.Replace(terrainExt, "");
            var pos = terrains[i].gameObject.transform.position + new Vector3(tileSize * 0.5f, 0, tileSize * 0.5f);
            terrianPosDic[name] = (pos, null);
            terrianStateDic[name] = false;
        }
        uint globalcount = 0;
        uint sourceglobalcount = 0;
        for (int i = 0; i < textAsset.Count; i++)
        {
            var name = textAsset[i].name.Replace(dataExt, "");
            if (terrianPosDic.ContainsKey(name))
            {
                var _temp = terrianPosDic[name].Item1;
                data = JsonConvert.DeserializeObject<List<Vector3>>(textAsset[i].text);
                if (data != null && data.Count > 0)
                {
                    // var poss = new float3[data.Count];
                    var poss = new List<float3>();
                    var yu = data.Count-1>noForceTileCount?true:false;
                    for (int j = 0; j < data.Count; j++)
                    {
                        if (isForce)
                        {
                            if (j % test == 0)
                            {
                                poss.Add(data[j]);
                            }
                        }
                        else
                        {
                            if(yu){
                                if (j % test == 0)
                                {
                                    poss.Add(data[j]);
                                }
                               
                            }
                        }
                    }
                    if(yu)
                        Debug.LogWarning($"我是非暴力求余{name} :{data.Count}");
                    terrianPosDic[name] = (_temp, poss.ToArray());
                    sourceglobalcount += (uint)data.Count - 1;
                    globalcount += (uint)poss.Count - 1;

                }
            }
        }
        Debug.Log($"统计树木生成 : {sourceglobalcount} % {test}  = {globalcount}");
        count = Materials.Length;
        cachedPrototype = CreatePrototype();
    }
    // public void OnDrawGizmos()
    // {
    //     foreach (var item in terrianPosDic)
    //     {
    //         if (Vector3.Distance(cameraGo.transform.position, item.Value.Item1) < distance)
    //         {
    //             Gizmos.color = Color.green;
    //         }
    //         else
    //         {
    //             Gizmos.color = Color.red;
    //         }
    //         Gizmos.DrawWireCube(item.Value.Item1,new Vector3(tileSize,300,tileSize));
    //     }
    // }
    Entity CreatePrototype()
    {
        em = World.DefaultGameObjectInjectionWorld.EntityManager;
        query = em.CreateEntityQuery(typeof(SpawnedByChunk));
        var filterSettings = RenderFilterSettings.Default;
        filterSettings.ShadowCastingMode = ShadowCastingMode.On;
        filterSettings.ReceiveShadows = true;

        var renderMeshArray = new RenderMeshArray(Materials, Meshes);
        var renderMeshDescription = new RenderMeshDescription
        {
            FilterSettings = filterSettings,
            LightProbeUsage = LightProbeUsage.Off,
        };

        var prototype = em.CreateEntity();
        // ======== 关键修复：添加必需组件 ========
        // 1. 添加 LocalTransform (位置/旋转/缩放)
        em.AddComponent<LocalTransform>(prototype);

        // 2. 添加 LocalToWorld (渲染必需)
        em.AddComponent<LocalToWorld>(prototype);

        // 3. 添加 RenderMeshDescription 所需标签
        // 使用 Static 优化渲染，风力系统会直接更新 LocalToWorld 矩阵
        em.AddComponent<Static>(prototype);
        em.AddComponent<RenderBounds>(prototype);
        RenderMeshUtility.AddComponents(
                prototype,
                em,
                renderMeshDescription,
                renderMeshArray,
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)
                );


        return prototype;
    }

    public void SpawnChunk(string guid, float3[] positions)
    {
        controller = em.CreateEntity();
        // Debug.Log($"[SpawnChunk] Using prototype: {cachedPrototype}, exists = {em.Exists(cachedPrototype)}");
        em.AddComponentData(controller, new ChunkSpawnRequest
        {
            Prototype = cachedPrototype,
            ChunkGuid = guid,
            CurrentIndex = 0,
            isRandom = isRandom,
            appointEuler = appointEuler,
            appointScale = appointScale,
            tileScale = tileScale
        });

        var buffer = em.AddBuffer<SpawnPosition>(controller);
        int i = 0;
        foreach (var p in positions)
        {
            buffer.Add(new SpawnPosition
            {
                Value = p,
                MeshIndex = i % count
            });
            i++;
        }
    }

    public void DestroyChunk(string guid)
    {
        em = World.DefaultGameObjectInjectionWorld.EntityManager;
        query.SetSharedComponentFilter(new SpawnedByChunk { ChunkGuid = guid });
        em.DestroyEntity(query);
    }



    void Update()
    {
        // isOpen 默认为 true，自动生成树木，无需按 P 键
        if (!isOpen) return;
        Profiler.BeginSample("Chunk Check");
        foreach (var item in terrianPosDic)
        {
            if (terrianStateDic.ContainsKey(item.Key) == false) return;
            if (Vector3.Distance(cameraGo.transform.position, item.Value.Item1) < distance)
            {
                if (terrianStateDic[item.Key] == false)
                {
                    terrianStateDic[item.Key] = true;
                    processDatas.Enqueue(item.Key);
                }
            }
        }
        Profiler.EndSample();

        Profiler.BeginSample("Spawn Loop");
        while (processDatas.Count > 0)
        {
            var key = processDatas.Dequeue();
            if (terrianPosDic[key].Item2 != null)
            {
                if (terrianStateDic[key])
                {
                    SpawnChunk(key, terrianPosDic[key].Item2);
                }
            }
        }
        Profiler.EndSample();
    }
}
