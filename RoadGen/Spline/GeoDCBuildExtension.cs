using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GeoDCBuildTools.RoadTool
{
    public static class GeoDCBuildExtension
    {
        public static string Show<T>(this List<T> list)
        {
            return string.Join(", ", list);
        }

        public static string Show<T>(this T[] arr)
        {
            return string.Join(", ", arr);
        }

        public static Vector3 ToXZ(this Vector3 v)
        {
            return new Vector3(v.x, 0, v.z);
        }

        public static Vector3 XYToXZ(this Vector3 v)
        {
            return new Vector3(v.x, 0, v.y);
        }

        public static SerializableVector3 ToSerializableVector3(this Vector3 v)
        {
            return new SerializableVector3(v.x, v.y, v.z);
        }

        public static Vector3 ToVector3(this SerializableVector3 v)
        {
            return new Vector3(v.x, v.y, v.z);
        }
    }
}