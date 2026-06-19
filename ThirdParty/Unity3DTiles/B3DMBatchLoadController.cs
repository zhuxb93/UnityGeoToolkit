using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity3DTiles
{
    public class B3DMBatchLoadController
    {
        [Header("性能控制")]
        [SerializeField] private int maxConcurrentLoads = 3;

        private readonly Queue<LoadRequest> _loadQueue = new Queue<LoadRequest>();
        private readonly Dictionary<string, LoadRequest> _activeLoads = new Dictionary<string, LoadRequest>();
        private readonly List<LoadRequest> tmpList = new List<LoadRequest>();

        private Coroutine _processCoroutine;

        private bool _isPaused = false;

        public AbstractTilesetBehaviour behaviour;

        /// <summary>
        /// 加载请求结构
        /// </summary>
        public class LoadRequest
        {
            public string id;
            public byte[] data;
            public Unity3DTile tile;
            public float requestTime;
            public float startTime;
            public float endTime;
            public LoadStatus status;
            public string errorMessage;

            public float LoadDuration => endTime - startTime;
            public float QueueWaitTime => startTime - requestTime;
        }

        /// <summary>
        /// 加载状态
        /// </summary>
        public enum LoadStatus
        {
            Queued,
            Loading,
            Completed,
            Failed,
            Cancelled
        }

        #region 初始化和清理

        public void InitializeController(AbstractTilesetBehaviour behaviour)
        {
            this.behaviour = behaviour;
            maxConcurrentLoads = behaviour.SceneOptions.MaxB3DMConcurrentLoads;
        }

        public void CleanupController()
        {
            _isPaused = true;

            if (_processCoroutine != null)
            {
                this.behaviour.StopCoroutine(_processCoroutine);
            }

            CancelAllLoads();
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 添加B3DM文件到加载队列
        /// </summary>
        public string EnqueueLoad(byte[] data, Unity3DTile tile)
        {
            if (data == null || data.Length == 0)
            {
                tile.FinishedLoad(false, "无效数据");
                return null;
            }

            string requestId = GenerateRequestId();
            var request = new LoadRequest
            {
                id = requestId,
                data = data,
                tile = tile,
                requestTime = Time.realtimeSinceStartup,
                status = LoadStatus.Queued
            };

            _loadQueue.Enqueue(request);

            return requestId;
        }

        /// <summary>
        /// 取消特定的加载请求
        /// </summary>
        public bool CancelLoad(string requestId)
        {
            // 从队列中移除
            var queuedRequests = _loadQueue.ToArray();
            _loadQueue.Clear();

            bool cancelled = false;
            foreach (var request in queuedRequests)
            {
                if (request.id == requestId)
                {
                    request.status = LoadStatus.Cancelled;
                    cancelled = true;
                }
                else
                {
                    _loadQueue.Enqueue(request);
                }
            }

            // 标记活动加载为取消（无法中断正在进行的加载）
            if (_activeLoads.ContainsKey(requestId))
            {
                _activeLoads[requestId].status = LoadStatus.Cancelled;
                cancelled = true;
            }

            return cancelled;
        }

        /// <summary>
        /// 取消所有加载请求
        /// </summary>
        public void CancelAllLoads()
        {
            // 取消队列中的所有请求
            while (_loadQueue.Count > 0)
            {
                var request = _loadQueue.Dequeue();
                request.status = LoadStatus.Cancelled;
            }

            // 标记所有活动加载为取消
            foreach (var activeLoad in _activeLoads.Values)
            {
                activeLoad.status = LoadStatus.Cancelled;
            }
        }

        /// <summary>
        /// 暂停/恢复加载处理
        /// </summary>
        public void SetPaused(bool paused)
        {
            _isPaused = paused;
        }

        #endregion

        #region 核心处理逻辑

        /// <summary>
        /// 主处理协程
        /// </summary>
        public void Process()
        {
            SortRequestByPriority();
            if (!_isPaused && _loadQueue.Count > 0 && _activeLoads.Count < maxConcurrentLoads)
            {
                var request = _loadQueue.Dequeue();

                if (request.status == LoadStatus.Queued)
                {
                    UnityEngine.Profiling.Profiler.BeginSample("解析b3dm");
                    StartLoadAsync(request);
                    UnityEngine.Profiling.Profiler.EndSample();
                }
            }
        }

        /// <summary>
        /// 异步开始加载
        /// </summary>
        private async void StartLoadAsync(LoadRequest request)
        {
            request.startTime = Time.realtimeSinceStartup;
            request.status = LoadStatus.Loading;

            _activeLoads[request.id] = request;

            try
            {
                // 检查是否被取消
                if (request.status == LoadStatus.Cancelled)
                {
                    CompleteLoad(request, false, "加载被取消");
                    return;
                }

                // 执行实际的B3DM解析和加载
                bool success = await OptimizedB3DMParser.ParseB3DM(request.data, request.tile.Content.Go.transform, request.tile.ContentUrl);

                // 再次检查取消状态
                if (request.status == LoadStatus.Cancelled)
                {
                    CompleteLoad(request, false, "加载被取消");
                    return;
                }

                CompleteLoad(request, success, success ? "B3DM解析完成" : "B3DM解析失败");
            }
            catch (Exception ex)
            {
                CompleteLoad(request, false, $"加载异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 完成加载处理
        /// </summary>
        private void CompleteLoad(LoadRequest request, bool success, string errorMessage = null)
        {
            request.endTime = Time.realtimeSinceStartup;
            request.status = success ? LoadStatus.Completed : LoadStatus.Failed;
            request.errorMessage = errorMessage;

            _activeLoads.Remove(request.id);

            request.tile.FinishedLoad(success, $"{request.tile.Id}：{errorMessage}, 耗时: {request.LoadDuration:F2}s");
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 生成唯一请求ID
        /// </summary>
        private string GenerateRequestId()
        {
            return $"B3DM_{DateTime.Now.Ticks}_{UnityEngine.Random.Range(1000, 9999)}";
        }

        private void SortRequestByPriority()
        {
            if (_loadQueue.Count == 0 || _loadQueue.Count == 1)
            {
                return;
            }

            tmpList.Clear();
            tmpList.AddRange(_loadQueue);
            _loadQueue.Clear();
            tmpList.Sort((x, y) => x.tile.FrameState.Priority.CompareTo(y.tile.FrameState.Priority));
            for (int i = 0; i < tmpList.Count; i++)
            {
                if (tmpList[i].tile.FrameState.IsUsedThisFrame)
                {
                    _loadQueue.Enqueue(tmpList[i]);
                }
                else
                {
                    CompleteLoad(tmpList[i], false, "当前帧未使用该瓦片");
                }
            }
        }
        #endregion

    }
}