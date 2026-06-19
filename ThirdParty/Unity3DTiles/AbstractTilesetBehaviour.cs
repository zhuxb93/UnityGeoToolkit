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

using GLTFast;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Unity3DTiles
{
    public abstract class AbstractTilesetBehaviour : MonoBehaviour
    {
        //not readonly to show up in inspector
        public Unity3DTilesetSceneOptions SceneOptions = new Unity3DTilesetSceneOptions();

        public Unity3DTilesetStatistics Stats; //not readonly to show up in inspector

        public RequestManager RequestManager { get; private set; }

        public B3DMBatchLoadController B3DMBatchLoader { get; private set; }

        public CustomDeferAgent CustomDeferAgent { get; private set; }

        public TileCache TileCache { get; private set; }

        private bool unloadAssetsPending;
        private AsyncOperation lastUnloadAssets;

        public abstract bool Ready();

        public abstract BoundingSphere BoundingSphere(Func<Unity3DTileset, bool> filter = null);

        public abstract int DeepestDepth();

        public abstract void ClearForcedTiles();

        public void RequestUnloadUnusedAssets()
        {
            unloadAssetsPending = true;
        }

        public void Update()
        {
            _update();
            if (SceneOptions.UseDeferAgent)
            {
                CustomDeferAgent.Update();
            }
        }

        public void LateUpdate()
        {
            TileCache.MarkAllUnused();

            _lateUpdate();

            RequestManager.Process();
            B3DMBatchLoader.Process();

            if (unloadAssetsPending)
            {
                if (lastUnloadAssets == null || lastUnloadAssets.isDone)
                {
                    unloadAssetsPending = false;
                    lastUnloadAssets = Resources.UnloadUnusedAssets();
                }
            }

            UpdateStats();
        }

        protected virtual void UpdateStats()
        {
            //override in subclass
        }

        protected virtual void _update()
        {
            //override in subclass
        }

        protected virtual void _lateUpdate()
        {
            //override in subclass
        }

        public void Start()
        {
            B3DMBatchLoader = new B3DMBatchLoadController();
            B3DMBatchLoader.InitializeController(this);
            if (SceneOptions.UseDeferAgent)
            {
                CustomDeferAgent = new CustomDeferAgent(SceneOptions.FrameTimeBudget);
                GltfImport.SetDefaultDeferAgent(CustomDeferAgent);
            }
            RequestManager = new RequestManager(SceneOptions);
            TileCache = new TileCache(SceneOptions);

            if (SceneOptions.ClippingCamera == null)
            {
                SceneOptions.ClippingCamera = Camera.main;
            }

            _start();
        }

        protected virtual void _start()
        {
            //override in subclass
        }

        private void OnDestroy()
        {
            B3DMBatchLoader.CleanupController();
            GltfImport.UnsetDefaultDeferAgent(CustomDeferAgent);
        }
    }
}
