using System.Collections.Generic;
using UnityEngine;

namespace Unity3DTiles
{
    public class CustomTraversal
    {
        public HashSet<Unity3DTile> requestTiles = new HashSet<Unity3DTile>();

        private Unity3DTileset tileset;
        private Unity3DTilesetSceneOptions sceneOptions;

        private Plane[] planes = new Plane[6];
        private SSECalculator sse;

        private Unity3DTilesetOptions tilesetOptions
        {
            get { return tileset.TilesetOptions; }
        }

        public CustomTraversal(Unity3DTileset tileset, Unity3DTilesetSceneOptions sceneOptions)
        {
            this.tileset = tileset;
            this.sceneOptions = sceneOptions;
            sse = new SSECalculator(tileset);
        }

        public void Run()
        {
            if (sceneOptions.ClippingCamera == null)
            {
                return;
            }

            Matrix4x4 cameraMatrix = sceneOptions.ClippingCamera.projectionMatrix * sceneOptions.ClippingCamera.worldToCameraMatrix;
            GeometryUtility.CalculateFrustumPlanes(cameraMatrix, planes);
            sse.Configure(sceneOptions.ClippingCamera);
            Vector3 cameraPositionInTileset = sceneOptions.ClippingCamera.transform.position;
            Vector3 cameraForwardInTileset = sceneOptions.ClippingCamera.transform.forward;

            DetermineFrustumSet(tileset.Root, planes, sse, cameraPositionInTileset, cameraForwardInTileset, PlaneClipMask.GetDefaultMask());

            MarkUsedSetLeaves(tileset.Root);

            AssignPrioritiesRecursively(tileset.Root);

            SkipTraversal(tileset.Root);

            ProcessRequest();

            ToggleTiles(tileset.Root);
        }

        void DetermineFrustumSet(Unity3DTile tile, Plane[] planes, SSECalculator sse, Vector3 cameraPosInTilesetFrame, Vector3 cameraFwdInTilesetFrame, PlaneClipMask mask)
        {
            tile.FrameState.Reset();

            mask = tile.BoundingVolume.IntersectPlanes(planes, mask);
            if (mask.Intersection == IntersectionType.OUTSIDE)
            {
                return;
            }
            tile.FrameState.InFrustumSet = true;
            tileset.Statistics.FrustumSet++;

            if (tile.HasEmptyContent && tile.Children.Count == 0)
            {
                return;
            }

            tile.MarkUsed();

            tile.FrameState.DistanceToCamera = tile.BoundingVolume.MinDistanceTo(cameraPosInTilesetFrame);
            tile.FrameState.ScreenSpaceError = sse.PixelError(tile.GeometricError, tile.FrameState.DistanceToCamera);
            tile.FrameState.FoveatedFactor = tile.GetFoveatedFactor(cameraPosInTilesetFrame, cameraFwdInTilesetFrame);

            if (!tile.HasEmptyContent && tile.ContentType != Unity3DTileContentType.JSON)
            {
                if (tile.FrameState.ScreenSpaceError < tileset.TilesetOptions.MaximumScreenSpaceError)
                {
                    return;
                }
            }

            if (tileset.TilesetOptions.MaxDepth > 0 && tile.Depth >= tileset.TilesetOptions.MaxDepth)
            {
                return;
            }

            for (int i = 0; i < tile.Children.Count; i++)
            {
                DetermineFrustumSet(tile.Children[i], planes, sse, cameraPosInTilesetFrame, cameraFwdInTilesetFrame, mask);
            }
        }

        void MarkUsedSetLeaves(Unity3DTile tile)
        {
            // A used leaf is a node that is used but has no children in the used set
            if (!tile.FrameState.IsUsedThisFrame)
            {
                // Not used this frame, can't be a used leaf and neither can anything beneath us
                return;
            }
            tileset.Statistics.UsedSet++;
            // If any child is used, then we are not a leaf
            bool anyChildrenUsed = false;
            for (int i = 0; i < tile.Children.Count; i++)
            {
                anyChildrenUsed = anyChildrenUsed || tile.Children[i].FrameState.IsUsedThisFrame;
                if (tile.Children[i].FrameState.IsUsedThisFrame)
                {
                    MarkUsedSetLeaves(tile.Children[i]);
                }
            }
            if (!anyChildrenUsed)
            {
                tile.FrameState.IsUsedSetLeaf = true;
            }
        }

        /// <summary>
        /// Traverse the tree, request tiles, and enable visible tiles
        /// Skip parent tiles that have a screen space error larger than
        /// MaximumScreenSpaceError*SkipScreenSpaceErrorMultiplier
        /// </summary>
        /// <param name="tile"></param>
        void SkipTraversal(Unity3DTile tile)
        {
            if (!tile.FrameState.IsUsedThisFrame)
            {
                return;
            }

            if (!tile.HasEmptyContent && tile.ContentType == Unity3DTileContentType.JSON)
            {
                if (tile.ContentState != Unity3DTileContentState.READY)
                {
                    ShowParent(tile);
                    RequestTile(tile);
                }
                else
                {
                    for (int i = 0; i < tile.Children.Count; i++)
                    {
                        SkipTraversal(tile.Children[i]);
                    }
                }
                return;
            }

            if (tile.FrameState.IsUsedSetLeaf)
            {
                if (tile.ContentState == Unity3DTileContentState.READY && tile.Content != null)
                {
                    if (tile.FrameState.InFrustumSet)
                    {
                        tile.FrameState.InRenderSet = true;
                        tileset.Statistics.TallyVisibleTile(tile);
                    }
                }
                else
                {
                    ShowParent(tile);
                    RequestTile(tile);
                }
                return;
            }

            // Draw a parent tile iff
            // 1) meets SSE cuttoff
            // 2) has content and is not empty
            // 3) one or more of its chidlren don't have content
            bool meetsSSE = tile.FrameState.ScreenSpaceError < (tilesetOptions.MaximumScreenSpaceError * tilesetOptions.SkipScreenSpaceErrorMultiplier);
            bool hasContent = tile.ContentState == Unity3DTileContentState.READY && !tile.HasEmptyContent;

            if (meetsSSE)
            {
                if (!hasContent)
                {
                    ShowParent(tile);
                    RequestTile(tile);
                }
                else
                {
                    if (tile.FrameState.InFrustumSet)
                    {
                        tile.FrameState.InRenderSet = true;
                        tileset.Statistics.TallyVisibleTile(tile);
                    }
                }
                return;
            }

            // Otherwise keep decending
            for (int i = 0; i < tile.Children.Count; i++)
            {
                if (tile.Children[i].FrameState.IsUsedThisFrame)
                {
                    SkipTraversal(tile.Children[i]);
                }
            }
        }

        void ShowParent(Unity3DTile tile)
        {
            if (tile.Parent != null && !tile.Parent.HasEmptyContent)
            {
                if (tile.Parent.ContentType == Unity3DTileContentType.JSON)
                {
                    ShowParent(tile.Parent);
                }
                else
                {
                    bool hasContent = tile.Parent.ContentState == Unity3DTileContentState.READY && tile.Parent.Content != null;
                    if (!hasContent)
                    {
                        //ShowParent(tile.Parent);
                    }
                    else
                    {
                        if (tile.Parent.FrameState.InFrustumSet)
                        {
                            tile.Parent.FrameState.InRenderSet = true;
                            tileset.Statistics.TallyVisibleTile(tile.Parent);
                        }
                    }
                }
            }
        }

        void ToggleTiles(Unity3DTile tile)
        {
            // Only consider tiles that were used this frame or the previous frame
            if (tile.FrameState.IsUsedRecently())
            {
                tile.FrameState.UsedLastFrame = false;
                if (!tile.FrameState.IsUsedThisFrame)
                {
                    // This tile was active last frame but isn't active any more
                    if (tile.Content != null)
                    {
                        tile.Content.SetActive(false);
                    }
                }
                else
                {
                    // this tile is in the used set this frame
                    if (tile.Content != null)
                    {
                        tile.Content.SetActive(tile.FrameState.InRenderSet);
                        if (tile.FrameState.InRenderSet)
                        {
                            tile.Content.EnableRenderers(true);
                        }
                    }
                    tile.FrameState.UsedLastFrame = true;
                }
                for (int i = 0; i < tile.Children.Count; i++)
                {
                    ToggleTiles(tile.Children[i]);
                }
            }
        }

        void AssignPrioritiesRecursively(Unity3DTile tile)
        {
            if (tile.FrameState.IsUsedThisFrame)
            {
                if (!tile.HasEmptyContent)
                {
                    tile.FrameState.Priority = TilePriority(tile);
                }

                int childCount = tile.Children.Count;
                for (int i = 0; i < childCount; i++)
                {
                    AssignPrioritiesRecursively(tile.Children[i]);
                }
            }
            else
            {
                tile.FrameState.Priority = float.MaxValue;
            }
        }

        float TilePriority(Unity3DTile tile)
        {
            return tile.FrameState.ScreenSpaceError + tile.FrameState.FoveatedFactor * 100;
        }

        void ProcessRequest()
        {
            foreach (var tile in requestTiles)
            {
                tile.RequestContent(tile.FrameState.Priority);
            }
            requestTiles.Clear();
        }

        void RequestTile(Unity3DTile tile)
        {
            if (!requestTiles.Contains(tile) && tile.requestFailTime < sceneOptions.MaxRequestFailTime)
            {
                requestTiles.Add(tile);
            }
        }

        public void DrawDebug()
        {
            DebugDrawUsedSet(tileset.Root);
            DebugDrawFrustumSet(tileset.Root);
        }

        void DebugDrawUsedSet(Unity3DTile tile)
        {
            if (tile.FrameState.IsUsedThisFrame)
            {
                if (tile.FrameState.IsUsedSetLeaf)
                {
                    tile.BoundingVolume.DebugDraw(Color.white, tileset.Behaviour.transform);
                }
                for (int i = 0; i < tile.Children.Count; i++)
                {
                    DebugDrawUsedSet(tile.Children[i]);
                }
            }
        }

        void DebugDrawFrustumSet(Unity3DTile tile)
        {
            if (tile.FrameState.IsUsedThisFrame && tile.FrameState.InFrustumSet)
            {
                if (tile.FrameState.IsUsedSetLeaf)
                {
                    tile.BoundingVolume.DebugDraw(Color.green, tileset.Behaviour.transform);
                }
                for (int i = 0; i < tile.Children.Count; i++)
                {
                    DebugDrawFrustumSet(tile.Children[i]);
                }
            }
        }
    }
}