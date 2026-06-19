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


namespace Unity3DTiles
{
    public class TilesetBehaviour : AbstractTilesetBehaviour
    {
        public Unity3DTilesetOptions TilesetOptions = new Unity3DTilesetOptions();
        public Unity3DTileset Tileset;

        public GeoToolkit.GeoPlatformConfig platformConfig;

        [HideInInspector]
        public Matrix4x4d transformMat = Matrix4x4d.identity;

        public float extraAddtionHeight = 15;

        private float lastExtraAddtionHeight = 0;
        public override bool Ready()
        {
            return Tileset != null && Tileset.Ready;
        }

        public override BoundingSphere BoundingSphere(Func<Unity3DTileset, bool> filter = null)
        {
            if (Tileset == null || (filter != null && !filter(Tileset)))
            {
                return new BoundingSphere(Vector3.zero, 0);
            }
            return Tileset.Root.BoundingVolume.GetBoundingSphere();
        }

        public override int DeepestDepth()
        {
            return Tileset != null ? Tileset.DeepestDepth : 0;
        }

        public override void ClearForcedTiles()
        {

        }

        public virtual void MakeTileset()
        {
            Tileset = new Unity3DTileset(TilesetOptions, this);
            Stats = Tileset.Statistics;
        }

        protected override void _start()
        {
            CoordinateConversion.Initialize(platformConfig, extraAddtionHeight);

            lastExtraAddtionHeight = extraAddtionHeight;

            MakeTileset();
        }

        protected override void _lateUpdate()
        {
            if (Tileset != null)
            {
                Tileset.Update();

                if (lastExtraAddtionHeight != extraAddtionHeight)
                {
                    float offset = extraAddtionHeight - lastExtraAddtionHeight;
                    for (int i = 0; i < transform.childCount; i++)
                    {
                        transform.GetChild(i).transform.localPosition += Vector3.up * offset;
                    }
                    lastExtraAddtionHeight = extraAddtionHeight;
                }
            }
        }

        protected override void UpdateStats()
        {
            Tileset.UpdateStats();
        }
    }
}
