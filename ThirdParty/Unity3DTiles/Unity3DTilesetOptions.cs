/*
 * Copyright 2018, by the California Institute of Technology. ALL RIGHTS 
 * RESERVED. United States Government Sponsorship acknowledged. Any 
 * commercial use must be negotiated with the Office of Technology 
 * Transfer at the California Institute of Technology.
 * 
 * This software may be subject to U.S.export control laws.By accepting 
 * this software, the user agrees to comply with all applicable 
 * U.S.export laws and regulations. User has the responsibility to 
 * obtain export licenses, or other export authority as may be required 
 * before exporting such information to foreign countries or providing 
 * access to foreign persons.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Unity3DTiles
{
    [Serializable] //Serializable so it will show up in Unity editor inspector
    public class Unity3DTilesetOptions
    {
        [Tooltip("Full path URL to the tileset. Can be a local file or url as long as it is a full path, or can start with StreamingAssets.")]
        public string Url = null;

        [Tooltip("Controls the level of detail the tileset will be loaded to by specifying the allowed amount of on screen geometric error allowed in pixels")]
        public double MaximumScreenSpaceError = 8;

        [Tooltip("Controls what parent tiles will be skipped when loading a tileset.  This number will be multipled by MaximumScreenSpaceError and any tile with an on screen error larger than this will be skipped by the loading and rendering algorithm")]
        public double SkipScreenSpaceErrorMultiplier = 64;

        [Header("Root Transform")]

        [Tooltip("Tileset translation in right-handed tileset coordinates.")]
        [JsonConverter(typeof(Vector3Converter))]
        public Vector3 Translation = Vector3.zero;

        [Tooltip("Tileset rotation in right-handed tileset coordinates.")] 
        [JsonConverter(typeof(QuaternionConverter))]
#if UNITY_EDITOR
        [EulerAngles]
#endif
        public Quaternion Rotation = Quaternion.identity;

        [Tooltip("Tileset scale in right-handed tileset coordinates.")]
        [JsonConverter(typeof(Vector3Converter))]
        public Vector3 Scale = Vector3.one;

        [Tooltip("Max child depth that we should render. If this is zero, disregard")]
        public int MaxDepth = 0;

        public bool DebugDrawBounds = false;
    }

    [Serializable] //Serializable so it will show up in Unity editor inspector
    public class Unity3DTilesetSceneOptions
    {
        public Camera ClippingCamera;

        [Tooltip("LRU缓存最大值")]
        public int CacheMaxSize = 1000;

        [Tooltip("LRU缓存卸载比例")]
        public float MaxCacheUnloadRatio = 0.2f;

        [Tooltip("最大请求数量")]
        public int MaxConcurrentRequests = 6;

        [Tooltip("最大请求失败次数")]
        public int MaxRequestFailTime = 3;

        [Tooltip("是否限制解析GLB每帧耗时")]
        public bool UseDeferAgent = true;

        [Tooltip("解析GLB每帧耗时")]
        public float FrameTimeBudget = 0.02f;

        [Tooltip("最大B3DM解析数量")]
        public int MaxB3DMConcurrentLoads = 3;

        [Tooltip("触发资源回收上限销毁阈值")]
        public int MaxDestroyThreshold = 100;
    }
}
