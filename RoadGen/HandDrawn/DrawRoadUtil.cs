using UnityEngine;

namespace GeoToolkit.DrawRoad
{
    public class DrawRoadUtil
    {
        public static string checkLayerName = "terrain collision detection";

        public static void SetCheckLayerName(string layerName)
        {
            checkLayerName = layerName;
        }

        public static float GetPointHeight(Vector3 point)
        {
            Ray ray = new Ray(point + Vector3.up * 100, Vector3.down);
            if (Physics.Raycast(ray, out var hitInfo, float.MaxValue, 1 << LayerMask.NameToLayer(checkLayerName)))
            {
                return hitInfo.point.y;
            }
            return 0;
        }
    }

}