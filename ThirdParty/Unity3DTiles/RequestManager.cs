using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Http;

namespace Unity3DTiles
{
    public class Request
    {
        public Unity3DTile Tile;
        public Action Started;
        public Action<bool> Finished;

        public Request(Unity3DTile tile, Action started, Action<bool> finished)
        {
            Tile = tile;
            Started = started;
            Finished = finished;
        }

        public void Reset()
        {
            Tile = null;
            Started = null;
            Finished = null;
        }
    }

    public class RequestManager : IEnumerable<Unity3DTile>
    {
        private readonly Unity3DTilesetSceneOptions sceneOptions;
        private Queue<Request> recycleQueue = new Queue<Request>();
        private Queue<Request> queue = new Queue<Request>();
        private HashSet<Unity3DTile> activeDownloads = new HashSet<Unity3DTile>();
        private List<Request> tmpList = new List<Request>();

        private const int MAX_QUEUE_SIZE = 100;

        public static readonly HttpClient httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(30),
            MaxResponseContentBufferSize = 100 * 1024 * 1024
        };

        public IEnumerator<Unity3DTile> GetEnumerator()
        {
            foreach (var request in queue)
            {
                yield return request.Tile;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public RequestManager(Unity3DTilesetSceneOptions sceneOptions)
        {
            this.sceneOptions = sceneOptions;
        }

        public int Count(Func<Unity3DTile, bool> predicate = null)
        {
            if (predicate == null)
            {
                return queue.Count;
            } 
            int num = 0;
            foreach (var request in queue)
            {
                if (predicate(request.Tile))
                {
                    num++;
                }
            }
            return num;
        }

        public int CountActiveDownloads(Func<Unity3DTile, bool> predicate = null)
        {
            if (predicate == null)
            {
                return activeDownloads.Count;
            }
            return activeDownloads.Count(predicate);
        }

        public void ForEachQueuedDownload(Action<Unity3DTile> action)
        {
            foreach (var request in queue)
            {
                action(request.Tile);
            }
        }

        public void ForEachActiveDownload(Action<Unity3DTile> action)
        {
            foreach (var tile in activeDownloads)
            {
                action(tile);
            }
        }

        public Request GetRequestFromRecycleQueue()
        {
            if (recycleQueue.TryDequeue(out Request request))
            {
                return request;
            }
            else
            {
                return null;
            }
        }

        public void EnqueRequest(Request request)
        {
            request.Tile.ContentState = Unity3DTileContentState.LOADING;
            queue.Enqueue(request);
        }

        public void Process()
        {
            if (activeDownloads.Count >= sceneOptions.MaxConcurrentRequests || queue.Count == 0)
            {
                return;
            }

            tmpList.Clear();
            tmpList.AddRange(queue);
            queue.Clear();
            tmpList.Sort((x, y) => (int)Mathf.Sign(x.Tile.FrameState.Priority - y.Tile.FrameState.Priority));
            for (int i = 0; i < tmpList.Count; i++)
            {
                if (queue.Count < MAX_QUEUE_SIZE)
                {
                    queue.Enqueue(tmpList[i]);
                }
                else
                {
                    tmpList[i].Tile.ContentState = Unity3DTileContentState.UNLOADED;
                    tmpList[i].Reset();
                    recycleQueue.Enqueue(tmpList[i]);
                }
            }

            int newRequests = 0;
            while (activeDownloads.Count < sceneOptions.MaxConcurrentRequests && queue.Count > 0)
            {
                var request = queue.Dequeue();
                activeDownloads.Add(request.Tile);
                newRequests++;
                request.Tile.Started();
                request.Reset();
                recycleQueue.Enqueue(request);
            }
        }

        public void RemoveActiveRequest(Unity3DTile tile)
        {
            activeDownloads.Remove(tile);
        }
    }
}
