using UnityEngine;

namespace Unity3DTiles
{
    public class SSECalculator
    {
        private Camera cam;
        private float sseDenominator;    // used for perspective
        private float pixelSize;         // used for orthographic      
        private Unity3DTileset tileset;

        public SSECalculator(Unity3DTileset tileset)
        {
            this.tileset = tileset;
        }

        public void Configure(Camera cam)
        {
            this.cam = cam;
            if (cam.orthographic)
            {
                pixelSize = Mathf.Max(cam.orthographicSize * 2, cam.orthographicSize * 2 * cam.aspect) / Mathf.Max(cam.pixelHeight, cam.pixelWidth);
            }
            else
            {
                sseDenominator = 2 * Mathf.Tan(0.5f * cam.fieldOfView * Mathf.Deg2Rad);
            }
        }

        public float PixelError(float tileError, float distFromCamera)
        {
            if (tileError == 0)
            {
                return 0; // Leaf tiles have no screenspace error
            }
            return ProjectDistanceOnTileToScreen(tileError, distFromCamera);
        }

        public float ProjectDistanceOnTileToScreen(float distOnTile, float distFromCamera)
        {
            if (cam.orthographic)
            {
                return distOnTile / pixelSize;
            }
            else
            {
                distFromCamera = Mathf.Max(distFromCamera, 0.00001f);
                return (distOnTile * cam.pixelHeight) / (distFromCamera * sseDenominator);
            }
        }
    }
}
