using UnityEngine;
namespace GeoToolkit
{
    public static class Vector3dExtensions
    {
        public static Vector3 ToVector3(this Vector3d value)
        {
            return new Vector3((float)value.x, (float)value.y, (float)value.z);
        }

        public static Vector3d ToVector3d(this Vector3 value)
        {
            return new Vector3d(value.x, value.y, value.z);
        }
    }

}