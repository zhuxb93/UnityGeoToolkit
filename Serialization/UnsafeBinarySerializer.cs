using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace GeoToolkit
{
    /// <summary>
    /// 高性能 unsafe 二进制序列化器，使用内存拷贝优化性能
    /// </summary>
    public static class UnsafeBinarySerializer
    {
        #region List<T> 支持
        public static void SaveToFile<T>(string path, List<T> list) where T : unmanaged
        {
            if (list == null || list.Count == 0)
                throw new ArgumentException("List is null or empty");

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            int count = list.Count;
            bw.Write(count);

            using var nativeArray = new NativeArray<T>(list.ToArray(), Allocator.Temp);
            var byteSize = count * UnsafeUtility.SizeOf<T>();
            unsafe
            {
                var ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(nativeArray);
                var bytes = new byte[byteSize];
                UnsafeUtility.MemCpy(PinnedArray<byte>(bytes), ptr, byteSize);
                bw.Write(bytes);
            }
            bw.Flush();
            fs.Close();
        }

        public static List<T> LoadFromFile<T>(string path) where T : unmanaged
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            int count = br.ReadInt32();
            int byteSize = count * UnsafeUtility.SizeOf<T>();
            byte[] bytes = br.ReadBytes(byteSize);

            var result = new NativeArray<T>(count, Allocator.Temp);
            unsafe
            {
                var ptr = NativeArrayUnsafeUtility.GetUnsafePtr(result);
                UnsafeUtility.MemCpy(ptr, PinnedArray<byte>(bytes), byteSize);
            }

            var list = new List<T>(count);
            for (int i = 0; i < count; i++)
                list.Add(result[i]);

            result.Dispose();
            return list;
        }
        #endregion

        #region 数组支持
        public static void SaveToFile<T>(string path, T[] array) where T : unmanaged
        {
            if (array == null || array.Length == 0)
                throw new ArgumentException("Array is null or empty");

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            int count = array.Length;
            bw.Write(count);

            var byteSize = count * UnsafeUtility.SizeOf<T>();
            unsafe
            {
                var ptr = PinnedArray(array);
                var bytes = new byte[byteSize];
                UnsafeUtility.MemCpy(PinnedArray<byte>(bytes), ptr, byteSize);
                bw.Write(bytes);
            }
            bw.Flush();
            fs.Close();
        }

        public static T[] LoadFromFileAsArray<T>(string path) where T : unmanaged
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            int count = br.ReadInt32();
            int byteSize = count * UnsafeUtility.SizeOf<T>();
            byte[] bytes = br.ReadBytes(byteSize);

            var result = new T[count];
            unsafe
            {
                var ptr = PinnedArray(result);
                UnsafeUtility.MemCpy(ptr, PinnedArray<byte>(bytes), byteSize);
            }

            return result;
        }

        public static void LoadFromFile<T>(string path, T[] array) where T : unmanaged
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            int count = br.ReadInt32();
            if (count != array.Length)
            {
                throw new ArgumentException($"Array length mismatch. Expected {array.Length}, but file contains {count} elements.");
            }

            int byteSize = count * UnsafeUtility.SizeOf<T>();
            byte[] bytes = br.ReadBytes(byteSize);

            unsafe
            {
                var ptr = PinnedArray(array);
                UnsafeUtility.MemCpy(ptr, PinnedArray<byte>(bytes), byteSize);
            }
        }
        #endregion

        #region NativeArray 支持
        public static void SaveToFile<T>(string path, NativeArray<T> nativeArray) where T : unmanaged
        {
            if (!nativeArray.IsCreated || nativeArray.Length == 0)
                throw new ArgumentException("NativeArray is not created or empty");

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            int count = nativeArray.Length;
            bw.Write(count);

            var byteSize = count * UnsafeUtility.SizeOf<T>();
            unsafe
            {
                var ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(nativeArray);
                var bytes = new byte[byteSize];
                UnsafeUtility.MemCpy(PinnedArray<byte>(bytes), ptr, byteSize);
                bw.Write(bytes);
            }
            bw.Flush();
            fs.Close();
        }

        public static NativeArray<T> LoadFromFileAsNativeArray<T>(string path, Allocator allocator = Allocator.Persistent) where T : unmanaged
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            int count = br.ReadInt32();
            int byteSize = count * UnsafeUtility.SizeOf<T>();
            byte[] bytes = br.ReadBytes(byteSize);

            var result = new NativeArray<T>(count, allocator);
            unsafe
            {
                var ptr = NativeArrayUnsafeUtility.GetUnsafePtr(result);
                UnsafeUtility.MemCpy(ptr, PinnedArray<byte>(bytes), byteSize);
            }

            return result;
        }

        public static void LoadFromFile<T>(string path, NativeArray<T> nativeArray) where T : unmanaged
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            int count = br.ReadInt32();
            if (count != nativeArray.Length)
            {
                throw new ArgumentException($"NativeArray length mismatch. Expected {nativeArray.Length}, but file contains {count} elements.");
            }

            int byteSize = count * UnsafeUtility.SizeOf<T>();
            byte[] bytes = br.ReadBytes(byteSize);

            unsafe
            {
                var ptr = NativeArrayUnsafeUtility.GetUnsafePtr(nativeArray);
                UnsafeUtility.MemCpy(ptr, PinnedArray<byte>(bytes), byteSize);
            }
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 将数组固定为内存指针（仅作用于 MemCpy）
        /// </summary>
        private static unsafe void* PinnedArray<T>(T[] array) where T : unmanaged
        {
            fixed (void* ptr = array)
                return ptr;
        }
        #endregion
    }
} 