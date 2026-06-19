using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace GeoDCBuildTools.RoadTool
{
    /// <summary>
    /// 从 JSON 加载设施点并用 ECS 渲染
    /// </summary>
    public class RoadFacilityRenderer : MonoBehaviour
    {
        [Header("数据源")]
        public TextAsset facilityJson;

        [Header("渲染控制")]
        public float maxRenderDistance = 5000f;
        public float updateInterval = 0.5f;
        public bool autoSpawnOnStart = true;
        [Tooltip("用于计算距离的相机（默认 MainCamera）")]
        public Camera targetCamera;

        private EntityManager entityManager;
        private List<Entity> renderPrototypes = new List<Entity>();
        private List<ChildRenderData> allPoints = new List<ChildRenderData>();
        private Dictionary<MeshMaterialKey, int> meshMaterialPrototypeMap = new Dictionary<MeshMaterialKey, int>();
        private List<MeshMaterialKey> meshMaterialKeys = new List<MeshMaterialKey>();

        private Dictionary<int, Entity> activeEntities = new Dictionary<int, Entity>(); // 当前活跃点
        private float timeSinceLastUpdate = 0f;
        private string batchId;

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

        private struct ChildRenderData
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public int prototypeIndex;
        }

        private struct MeshMaterialKey
        {
            public Mesh Mesh;
            public Material Material;
            public MeshMaterialKey(Mesh mesh, Material material)
            {
                Mesh = mesh; Material = material;
            }
            public override bool Equals(object obj)
            {
                if (!(obj is MeshMaterialKey)) return false;
                var other = (MeshMaterialKey)obj;
                return Mesh == other.Mesh && Material == other.Material;
            }
            public override int GetHashCode()
            {
                return (Mesh?.GetHashCode() ?? 0) ^ ((Material?.GetHashCode() ?? 0) * 397);
            }
        }
        #endregion

        void Start()
        {
            if (autoSpawnOnStart)
            {
                InitializeAndSpawnFromJson();
                UpdateVisibleEntities();
            }
              
        }

        //void Update()
        //{
        //    if (targetCamera == null)
        //        targetCamera = Camera.main;

        //    if (entityManager == null || renderPrototypes.Count == 0)
        //        return;

        //    timeSinceLastUpdate += Time.deltaTime;
        //    if (timeSinceLastUpdate >= updateInterval)
        //    {
        //        timeSinceLastUpdate = 0f;
        //        UpdateVisibleEntities();
        //    }
        //}

      
        void OnDestroy()
        {
            // World 在 Editor 停止 Play 时已经被销毁，此时直接退出
            if (World.DefaultGameObjectInjectionWorld == null ||
                !World.DefaultGameObjectInjectionWorld.IsCreated)
            {
                return;
            }

            // entityManager 已在 World 存在时才可使用
            if (entityManager == null)
                return;

            try
            {
                foreach (var kv in activeEntities)
                {
                    if (entityManager.Exists(kv.Value))
                        entityManager.DestroyEntity(kv.Value);
                }

                foreach (var proto in renderPrototypes)
                {
                    if (entityManager.Exists(proto))
                        entityManager.DestroyEntity(proto);
                }
            }
            catch
            {
                // PlayMode exit 时强制忽略所有 ECS 操作
            }
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
            CreateRenderPrototypes();

            batchId = $"batch_{Guid.NewGuid():N}".Substring(0, 8);
            //Debug.Log($"✅ 初始化完成，共 {allPoints.Count} 个点位，{meshMaterialKeys.Count} 种材质组合");
        }

        private void CollectPrefabData(List<FacilityItem> items, Transform road)
        {
            allPoints.Clear();
            meshMaterialPrototypeMap.Clear();
            meshMaterialKeys.Clear();

            foreach (var item in items)
            {
                GameObject prefab = Resources.Load<GameObject>($"RoadFacility/{item.facilityType}");
                if (prefab == null)
                {
                    Debug.LogWarning($"⚠️ 找不到预制体: {item.facilityType}");
                    continue;
                }

                MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
                MeshRenderer mr = prefab.GetComponentInChildren<MeshRenderer>();
                if (mf == null || mr == null)
                {
                    Debug.LogWarning($"⚠️ 预制体 {item.facilityType} 缺少 Mesh 或 Renderer");
                    continue;
                }

                Mesh mesh = mf.sharedMesh;
                Material mat = mr.sharedMaterial;
                var key = new MeshMaterialKey(mesh, mat);

                if (!meshMaterialPrototypeMap.TryGetValue(key, out int protoIndex))
                {
                    protoIndex = meshMaterialKeys.Count;
                    meshMaterialKeys.Add(key);
                    meshMaterialPrototypeMap[key] = protoIndex;
                }

                Vector3 localPos = new Vector3(item.position.x, item.position.y, item.position.z);
                Vector3 worldPos = road.TransformPoint(localPos);

                allPoints.Add(new ChildRenderData
                {
                    position = worldPos,
                    rotation = road.rotation * Quaternion.Euler(item.euler.x, item.euler.y, item.euler.z),
                    scale = Vector3.Scale(road.lossyScale, new Vector3(item.scale.x, item.scale.y, item.scale.z)),
                    prototypeIndex = protoIndex
                });
            }
        }

        private void CreateRenderPrototypes()
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            renderPrototypes.Clear();

            for (int i = 0; i < meshMaterialKeys.Count; i++)
            {
                var key = meshMaterialKeys[i];
                Entity proto = CreateSinglePrototype(key.Mesh, key.Material, i);
                if (proto != Entity.Null)
                    renderPrototypes.Add(proto);
            }
        }

        private Entity CreateSinglePrototype(Mesh mesh, Material material, int prototypeIndex)
        {
            try
            {
                Entity prototype = entityManager.CreateEntity();
                entityManager.AddComponent<LocalTransform>(prototype);
                entityManager.AddComponent<LocalToWorld>(prototype);
                entityManager.AddComponent<AutoRenderTag>(prototype);

                var renderMeshArray = new RenderMeshArray(new[] { material }, new[] { mesh });
                RenderMeshUtility.AddComponents(
                    prototype,
                    entityManager,
                    new RenderMeshDescription { FilterSettings = RenderFilterSettings.Default },
                    renderMeshArray,
                    MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)
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
            if (targetCamera == null) return;

            Vector3 camPos = targetCamera.transform.position;
          
            for (int i = 0; i < allPoints.Count; i++)
            {
                var d = allPoints[i];
             
                bool currentlyActive = activeEntities.ContainsKey(i);
                // 创建实体
                Entity proto = renderPrototypes[d.prototypeIndex];
                Entity e = entityManager.Instantiate(proto);
                entityManager.SetComponentData(e, new LocalTransform
                {
                    Position = d.position,
                    Rotation = d.rotation,
                    Scale = d.scale.x
                });
                entityManager.SetComponentData(e, new AutoRenderTag
                {
                    BatchId = batchId,
                    SourceIndex = i
                });
                activeEntities[i] = e;
            }

            //if (created > 0 || destroyed > 0)
            //{
            //    Debug.Log($"🔄 距离裁剪更新：+{created} / -{destroyed}，当前活跃 {activeEntities.Count} 个");
            //}
        }

        /// <summary>
        /// 动态裁剪更新：创建/销毁实体
        /// </summary>
        //private void UpdateVisibleEntities()
        //{
        //    if (targetCamera == null) return;

        //    Vector3 camPos = targetCamera.transform.position;
        //    int created = 0;
        //    int destroyed = 0;

        //    for (int i = 0; i < allPoints.Count; i++)
        //    {
        //        var d = allPoints[i];
        //        float dist = Vector3.Distance(camPos, d.position);
        //        bool isVisible = dist <= maxRenderDistance;

        //        bool currentlyActive = activeEntities.ContainsKey(i);

        //        if (isVisible && !currentlyActive)
        //        {
        //            // 创建实体
        //            Entity proto = renderPrototypes[d.prototypeIndex];
        //            Entity e = entityManager.Instantiate(proto);
        //            entityManager.SetComponentData(e, new LocalTransform
        //            {
        //                Position = d.position,
        //                Rotation = d.rotation,
        //                Scale = d.scale.x
        //            });
        //            entityManager.SetComponentData(e, new AutoRenderTag
        //            {
        //                BatchId = batchId,
        //                SourceIndex = i
        //            });
        //            activeEntities[i] = e;
        //            created++;
        //        }
        //        else if (!isVisible && currentlyActive)
        //        {
        //            // 销毁实体
        //            Entity e = activeEntities[i];
        //            if (entityManager.Exists(e))
        //                entityManager.DestroyEntity(e);
        //            activeEntities.Remove(i);
        //            destroyed++;
        //        }
        //    }

        //    //if (created > 0 || destroyed > 0)
        //    //{
        //    //    Debug.Log($"🔄 距离裁剪更新：+{created} / -{destroyed}，当前活跃 {activeEntities.Count} 个");
        //    //}
        //}


        public struct AutoRenderTag : IComponentData
        {
            public FixedString64Bytes BatchId;
            public int SourceIndex;
        }
    }

   
}
