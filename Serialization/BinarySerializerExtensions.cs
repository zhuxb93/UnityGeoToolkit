using System.Collections.Generic;
using Unity.Collections;

namespace GeoToolkit
{
    /// <summary>
    /// BinarySerializer 扩展方法，提供更便捷的 API
    /// </summary>
    public static class BinarySerializerExtensions
    {
        #region List<T> 扩展方法
        /// <summary>
        /// 保存 List 到文件
        /// </summary>
        public static void SaveToFile<T>(this List<T> list, string path)
        {
            BinarySerializer.SaveToFile(path, list);
        }

        /// <summary>
        /// 从文件加载到现有 List
        /// </summary>
        public static void LoadFromFile<T>(this List<T> list, string path)
        {
            BinarySerializer.LoadFromFile(path, list);
        }
        #endregion

        #region 数组扩展方法
        /// <summary>
        /// 保存数组到文件
        /// </summary>
        public static void SaveToFile<T>(this T[] array, string path)
        {
            BinarySerializer.SaveToFile(path, array);
        }

        /// <summary>
        /// 从文件加载到现有数组
        /// </summary>
        public static void LoadFromFile<T>(this T[] array, string path)
        {
            BinarySerializer.LoadFromFile(path, array);
        }
        #endregion

        #region NativeArray 扩展方法
        /// <summary>
        /// 保存 NativeArray 到文件
        /// </summary>
        public static void SaveToFile<T>(this NativeArray<T> nativeArray, string path) where T : unmanaged
        {
            BinarySerializer.SaveToFile(path, nativeArray);
        }

        /// <summary>
        /// 从文件加载到现有 NativeArray
        /// </summary>
        public static void LoadFromFile<T>(this NativeArray<T> nativeArray, string path) where T : unmanaged
        {
            BinarySerializer.LoadFromFile(path, nativeArray);
        }
        #endregion

        #region Unsafe 扩展方法
        /// <summary>
        /// 使用 unsafe 方法保存 List 到文件（高性能）
        /// </summary>
        public static void SaveToFileUnsafe<T>(this List<T> list, string path) where T : unmanaged
        {
            UnsafeBinarySerializer.SaveToFile(path, list);
        }

        /// <summary>
        /// 使用 unsafe 方法从文件加载 List（高性能）
        /// </summary>
        public static List<T> LoadFromFileUnsafe<T>(string path) where T : unmanaged
        {
            return UnsafeBinarySerializer.LoadFromFile<T>(path);
        }

        /// <summary>
        /// 使用 unsafe 方法保存数组到文件（高性能）
        /// </summary>
        public static void SaveToFileUnsafe<T>(this T[] array, string path) where T : unmanaged
        {
            UnsafeBinarySerializer.SaveToFile(path, array);
        }

        /// <summary>
        /// 使用 unsafe 方法从文件加载数组（高性能）
        /// </summary>
        public static T[] LoadFromFileAsArrayUnsafe<T>(string path) where T : unmanaged
        {
            return UnsafeBinarySerializer.LoadFromFileAsArray<T>(path);
        }

        /// <summary>
        /// 使用 unsafe 方法从文件加载到现有数组（高性能）
        /// </summary>
        public static void LoadFromFileUnsafe<T>(this T[] array, string path) where T : unmanaged
        {
            UnsafeBinarySerializer.LoadFromFile(path, array);
        }

        /// <summary>
        /// 使用 unsafe 方法保存 NativeArray 到文件（高性能）
        /// </summary>
        public static void SaveToFileUnsafe<T>(this NativeArray<T> nativeArray, string path) where T : unmanaged
        {
            UnsafeBinarySerializer.SaveToFile(path, nativeArray);
        }

        /// <summary>
        /// 使用 unsafe 方法从文件加载 NativeArray（高性能）
        /// </summary>
        public static NativeArray<T> LoadFromFileAsNativeArrayUnsafe<T>(string path, Allocator allocator = Allocator.Persistent) where T : unmanaged
        {
            return UnsafeBinarySerializer.LoadFromFileAsNativeArray<T>(path, allocator);
        }

        /// <summary>
        /// 使用 unsafe 方法从文件加载到现有 NativeArray（高性能）
        /// </summary>
        public static void LoadFromFileUnsafe<T>(this NativeArray<T> nativeArray, string path) where T : unmanaged
        {
            UnsafeBinarySerializer.LoadFromFile(path, nativeArray);
        }
        #endregion
    }
} 