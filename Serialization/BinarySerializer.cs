using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace GeoToolkit
{
    /// <summary>
    /// 基础二进制序列化器，支持 List、数组和 NativeArray
    /// </summary>
    public static class BinarySerializer
    {
        #region List<T> 支持
        public static void SaveToFile<T>(string path, List<T> list)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(fs))
            {
                writer.Write(list.Count);
                foreach (var item in list)
                {
                    WriteItem(writer, item);
                }
                writer.Flush();
                fs.Close();
            }
        }

        public static List<T> LoadFromFile<T>(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fs))
            {
                int count = reader.ReadInt32();
                List<T> list = new List<T>(count);
                for (int i = 0; i < count; i++)
                {
                    list.Add(ReadItem<T>(reader));
                }
                return list;
            }
        }

        public static List<T> LoadFromFile<T>(string path, List<T> list)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fs))
            {
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    list.Add(ReadItem<T>(reader));
                }
                return list;
            }
        }
        #endregion

        #region 数组支持
        public static void SaveToFile<T>(string path, T[] array)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(fs))
            {
                writer.Write(array.Length);
                foreach (var item in array)
                {
                    WriteItem(writer, item);
                }
                writer.Flush();
                fs.Close();
            }
        }

        public static T[] LoadFromFileAsArray<T>(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fs))
            {
                int count = reader.ReadInt32();
                T[] array = new T[count];
                for (int i = 0; i < count; i++)
                {
                    array[i] = ReadItem<T>(reader);
                }
                return array;
            }
        }

        public static void LoadFromFile<T>(string path, T[] array)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fs))
            {
                int count = reader.ReadInt32();
                if (count != array.Length)
                {
                    throw new ArgumentException($"Array length mismatch. Expected {array.Length}, but file contains {count} elements.");
                }
                for (int i = 0; i < count; i++)
                {
                    array[i] = ReadItem<T>(reader);
                }
            }
        }
        #endregion

        #region NativeArray 支持
        public static void SaveToFile<T>(string path, NativeArray<T> nativeArray) where T : unmanaged
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(fs))
            {
                writer.Write(nativeArray.Length);
                for (int i = 0; i < nativeArray.Length; i++)
                {
                    WriteItem(writer, nativeArray[i]);
                }
                writer.Flush();
                fs.Close();
            }
        }

        public static NativeArray<T> LoadFromFileAsNativeArray<T>(string path, Allocator allocator = Allocator.Persistent) where T : unmanaged
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fs))
            {
                int count = reader.ReadInt32();
                var nativeArray = new NativeArray<T>(count, allocator);
                for (int i = 0; i < count; i++)
                {
                    nativeArray[i] = ReadItem<T>(reader);
                }
                return nativeArray;
            }
        }

        public static void LoadFromFile<T>(string path, NativeArray<T> nativeArray) where T : unmanaged
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fs))
            {
                int count = reader.ReadInt32();
                if (count != nativeArray.Length)
                {
                    throw new ArgumentException($"NativeArray length mismatch. Expected {nativeArray.Length}, but file contains {count} elements.");
                }
                for (int i = 0; i < count; i++)
                {
                    nativeArray[i] = ReadItem<T>(reader);
                }
            }
        }
        #endregion

        #region 私有方法
        private static void WriteItem<T>(BinaryWriter writer, T item)
        {
            BinaryTypeSerializer.WriteItem(writer, item);
        }

        private static T ReadItem<T>(BinaryReader reader)
        {
            return BinaryTypeSerializer.ReadItem<T>(reader);
        }
        #endregion
    }
}