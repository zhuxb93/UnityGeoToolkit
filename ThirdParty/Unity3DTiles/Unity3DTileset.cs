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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity3DTiles.Schema;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity3DTiles
{
    /// <summary>
    /// A 3D Tiles tileset used for streaming large 3D datasets
    /// See https://github.com/AnalyticalGraphicsInc/cesium/blob/master/Source/Scene/Cesium3DTileset.js
    /// </summary>
    public class Unity3DTileset
    {
        public Unity3DTilesetOptions TilesetOptions { get; private set;}

        public Unity3DTile Root { get; private set; }

        public TileCache TileCache { get; private set; }

        public RequestManager RequestManager { get; private set;}

        public B3DMBatchLoadController B3DMBatchLoader { get; private set; }

        public AbstractTilesetBehaviour Behaviour { get; private set; }

        public Transform Transform;

        /// <summary>
        /// The deepest depth of the tree as specified by the loaded json structure.  This may increase as
        /// recursive json tilesets are loaded.  This value does not depend on what renderable content has been loaded.
        /// </summary>
        public int DeepestDepth { get; private set; }

        public readonly Unity3DTilesetStatistics Statistics = new Unity3DTilesetStatistics();

        public CustomTraversal CustomTraversal { get; private set; }

        public bool Ready { get { return Root != null; } }

        private Schema.Tileset schemaTileset;

        public Matrix4x4d transformMat = Matrix4x4d.identity;

        public void GetRootTransform(out Vector3 translation, out Quaternion rotation, out Vector3 scale,
                                     bool convertToUnityFrame = true)
        {
            var m = Matrix4x4.TRS(TilesetOptions.Translation, TilesetOptions.Rotation, TilesetOptions.Scale);
            translation = new Vector3(m.m03, m.m13, m.m23);
            rotation = m.rotation;
            scale = m.lossyScale;
        }

        public Matrix4x4 GetRootTransform(bool convertToUnityFrame = true)
        {
            GetRootTransform(out Vector3 translation, out Quaternion rotation, out Vector3 scale, convertToUnityFrame);
            return Matrix4x4.TRS(translation, rotation, scale);
        }

        public Unity3DTileset(Unity3DTilesetOptions tilesetOptions, AbstractTilesetBehaviour behaviour)
        {
            TilesetOptions = tilesetOptions;
            Behaviour = behaviour;
            Transform = behaviour.transform;
            RequestManager = behaviour.RequestManager;
            B3DMBatchLoader = behaviour.B3DMBatchLoader;
            TileCache = behaviour.TileCache;
            CustomTraversal = new CustomTraversal(this, behaviour.SceneOptions);
            DeepestDepth = 0;
            if (behaviour is TilesetBehaviour)
            {
                transformMat = (behaviour as TilesetBehaviour).transformMat;
            }
            string url = UrlUtils.ReplaceDataProtocol(tilesetOptions.Url);
            string tilesetUrl = url;
            if (!UrlUtils.GetLastPathSegment(url).EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                tilesetUrl = UrlUtils.JoinUrls(url, "tileset.json");
            }

            RequestTilesetJson(tilesetUrl);
        }

        public async void RequestTilesetJson(string tilesetUrl)
        {
            UnityWebRequest request = UnityWebRequest.Get(tilesetUrl);
            UnityWebRequestAsyncOperation asyncOperation = request.SendWebRequest();
            while (!asyncOperation.isDone)
            {
                await Task.Yield();
            }
            if (request.isDone && request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                schemaTileset = Tileset.FromJson(json);
                Root = LoadTileset(tilesetUrl, schemaTileset, null);
            }
            else
            {
                Debug.LogError("Tileset请求失败");
            }
        }

        public Unity3DTile LoadChildTileset(string tilesetUrl, string json, Unity3DTile parentTile)
        {
            Tileset tileset = Tileset.FromJson(json);
            if (tileset.Asset == null)
            {
                Debug.LogError("Tileset must have an asset property");
                return null;
            }

            string basePath = UrlUtils.GetBaseUri(tilesetUrl);
            basePath = basePath.Replace("\\", "/");
            if (!basePath.EndsWith("/")) basePath += "/";
            Unity3DTile rootTile = new Unity3DTile(this, basePath, tileset.Root, parentTile);
            Statistics.NumberOfTilesTotal++;

            Stack<Unity3DTile> stack = new Stack<Unity3DTile>();
            stack.Push(rootTile);
            while (stack.Count > 0)
            {
                Unity3DTile tile3D = stack.Pop();
                for (int i = 0; i < tile3D.SchemaTile.Children.Count; i++)
                {
                    Unity3DTile child = new Unity3DTile(this, basePath, tile3D.SchemaTile.Children[i], tile3D);
                    DeepestDepth = Math.Max(child.Depth, DeepestDepth);
                    Statistics.NumberOfTilesTotal++;
                    stack.Push(child);
                }
            }
            return rootTile;
        }

        private Unity3DTile LoadTileset(string tilesetUrl, Schema.Tileset tileset, Unity3DTile parentTile)
        {
            if (tileset.Asset == null)
            {
                Debug.LogError("Tileset must have an asset property");
                return null;
            }

            // from ECEF to Tile based Coordinates
            Matrix4x4d ECEFTransform = tileset.Root.UnityTransform();
            if (CoordinateConversion.enabled)
            {
                Matrix4x4 rootTransform = CoordinateConversion.GetTileTransformMatrix(ECEFTransform);
                TilesetOptions.Translation = rootTransform.GetPosition();
                TilesetOptions.Rotation = rootTransform.rotation;
                TilesetOptions.Scale = rootTransform.lossyScale;
            }

            // A tileset.json referenced from a tile may exist in a different directory than the root tileset.
            // Get the basePath relative to the external tileset.
            string basePath = UrlUtils.GetBaseUri(tilesetUrl);
            basePath = basePath.Replace("\\", "/");
            if (!basePath.EndsWith("/")) basePath += "/";
            Unity3DTile rootTile = new Unity3DTile(this, basePath, tileset.Root, parentTile);
            Statistics.NumberOfTilesTotal++;

            // Loop through the Tile json data and create a tree of Unity3DTiles
            Stack<Unity3DTile> stack = new Stack<Unity3DTile>();
            stack.Push(rootTile);
            while (stack.Count > 0)
            {
                Unity3DTile tile3D = stack.Pop();
                for (int i = 0; i < tile3D.SchemaTile.Children.Count; i++)
                {
                    Unity3DTile child = new Unity3DTile(this, basePath, tile3D.SchemaTile.Children[i], tile3D);
                    DeepestDepth = Math.Max(child.Depth, DeepestDepth);
                    Statistics.NumberOfTilesTotal++;
                    stack.Push(child);
                }
                // TODO consider using CullWithChildrenBounds optimization here
            }
            return rootTile;
        }

        public void Update()
        {
            Statistics.Clear();
            if (Ready)
            {
                CustomTraversal.Run();
                if (TilesetOptions.DebugDrawBounds)
                {
                    CustomTraversal.DrawDebug();
                }
            }
        }

        public void UpdateStats()
        {
            Statistics.RequestQueueLength = RequestManager.Count(t => t.Tileset == this);
            Statistics.ActiveDownloads = RequestManager.CountActiveDownloads(t => t.Tileset == this);
            Statistics.DownloadedTiles = TileCache.Count(t => t.Tileset == this);
            Statistics.ReadyTiles = TileCache.Count(t => t.Tileset == this && t.ContentState == Unity3DTileContentState.READY);
            Unity3DTilesetStatistics.MaxLoadedTiles = TileCache.MaxSize;
        }
    }
}
