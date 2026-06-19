using System;
using System.IO;
using Unity.Mathematics;
using UnityEngine;

namespace GeoToolkit
{
    /// <summary>
    /// 二进制类型序列化器，处理 Unity 内置类型的序列化和反序列化
    /// </summary>
    public static class BinaryTypeSerializer
    {
        /// <summary>
        /// 写入单个项目到二进制流
        /// </summary>
        public static void WriteItem<T>(BinaryWriter writer, T item)
        {
            switch (item)
            {
                case int v: writer.Write(v); break;
                case uint v: writer.Write(v); break;
                case long v: writer.Write(v); break;
                case ulong v: writer.Write(v); break;
                case short v: writer.Write(v); break;
                case ushort v: writer.Write(v); break;
                case byte v: writer.Write(v); break;
                case sbyte v: writer.Write(v); break;
                case float v: writer.Write(v); break;
                case double v: writer.Write(v); break;
                case bool v: writer.Write(v); break;
                case string v: writer.Write(v); break;
                case Vector2 v: WriteVector2(writer, v); break;
                case Vector3 v: WriteVector3(writer, v); break;
                case Vector4 v: WriteVector4(writer, v); break;
                case Quaternion v: WriteQuaternion(writer, v); break;
                case Color v: WriteColor(writer, v); break;
                case float2 v: WriteFloat2(writer, v); break;
                case float3 v: WriteFloat3(writer, v); break;
                case float4 v: WriteFloat4(writer, v); break;
                case double2 v: WriteDouble2(writer, v); break;
                case double3 v: WriteDouble3(writer, v); break;
                case double4 v: WriteDouble4(writer, v); break;
                default: throw new NotSupportedException($"Unsupported type: {typeof(T)}");
            }
        }

        /// <summary>
        /// 从二进制流读取单个项目
        /// </summary>
        public static T ReadItem<T>(BinaryReader reader)
        {
            object value;
            Type type = typeof(T);

            if (type == typeof(int)) value = reader.ReadInt32();
            else if (type == typeof(uint)) value = reader.ReadUInt32();
            else if (type == typeof(long)) value = reader.ReadInt64();
            else if (type == typeof(ulong)) value = reader.ReadUInt64();
            else if (type == typeof(short)) value = reader.ReadInt16();
            else if (type == typeof(ushort)) value = reader.ReadUInt16();
            else if (type == typeof(byte)) value = reader.ReadByte();
            else if (type == typeof(sbyte)) value = reader.ReadSByte();
            else if (type == typeof(float)) value = reader.ReadSingle();
            else if (type == typeof(double)) value = reader.ReadDouble();
            else if (type == typeof(bool)) value = reader.ReadBoolean();
            else if (type == typeof(string)) value = reader.ReadString();
            else if (type == typeof(Vector2)) value = ReadVector2(reader);
            else if (type == typeof(Vector3)) value = ReadVector3(reader);
            else if (type == typeof(Vector4)) value = ReadVector4(reader);
            else if (type == typeof(Quaternion)) value = ReadQuaternion(reader);
            else if (type == typeof(Color)) value = ReadColor(reader);
            else if (type == typeof(float2)) value = ReadFloat2(reader);
            else if (type == typeof(float3)) value = ReadFloat3(reader);
            else if (type == typeof(float4)) value = ReadFloat4(reader);
            else if (type == typeof(double2)) value = ReadDouble2(reader);
            else if (type == typeof(double3)) value = ReadDouble3(reader);
            else if (type == typeof(double4)) value = ReadDouble4(reader);
            else throw new NotSupportedException($"Unsupported type: {type}");

            return (T)value;
        }

        #region Unity 类型序列化方法
        private static void WriteVector2(BinaryWriter writer, Vector2 v)
        {
            writer.Write(v.x);
            writer.Write(v.y);
        }

        private static Vector2 ReadVector2(BinaryReader reader)
        {
            return new Vector2(reader.ReadSingle(), reader.ReadSingle());
        }

        private static void WriteVector3(BinaryWriter writer, Vector3 v)
        {
            writer.Write(v.x);
            writer.Write(v.y);
            writer.Write(v.z);
        }

        private static Vector3 ReadVector3(BinaryReader reader)
        {
            return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static void WriteVector4(BinaryWriter writer, Vector4 v)
        {
            writer.Write(v.x);
            writer.Write(v.y);
            writer.Write(v.z);
            writer.Write(v.w);
        }

        private static Vector4 ReadVector4(BinaryReader reader)
        {
            return new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static void WriteQuaternion(BinaryWriter writer, Quaternion v)
        {
            writer.Write(v.x);
            writer.Write(v.y);
            writer.Write(v.z);
            writer.Write(v.w);
        }

        private static Quaternion ReadQuaternion(BinaryReader reader)
        {
            return new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static void WriteColor(BinaryWriter writer, Color v)
        {
            writer.Write(v.r);
            writer.Write(v.g);
            writer.Write(v.b);
            writer.Write(v.a);
        }

        private static Color ReadColor(BinaryReader reader)
        {
            return new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static void WriteFloat2(BinaryWriter writer, float2 v)
        {
            writer.Write(v.x);
            writer.Write(v.y);
        }

        private static float2 ReadFloat2(BinaryReader reader)
        {
            return new float2(reader.ReadSingle(), reader.ReadSingle());
        }

        private static void WriteFloat3(BinaryWriter writer, float3 v)
        {
            writer.Write(v.x);
            writer.Write(v.y);
            writer.Write(v.z);
        }

        private static float3 ReadFloat3(BinaryReader reader)
        {
            return new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static void WriteFloat4(BinaryWriter writer, float4 v)
        {
            writer.Write(v.x);
            writer.Write(v.y);
            writer.Write(v.z);
            writer.Write(v.w);
        }

        private static float4 ReadFloat4(BinaryReader reader)
        {
            return new float4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static void WriteDouble2(BinaryWriter writer, double2 v)
        {
            writer.Write(v.x);
            writer.Write(v.y);
        }

        private static double2 ReadDouble2(BinaryReader reader)
        {
            return new double2(reader.ReadDouble(), reader.ReadDouble());
        }

        private static void WriteDouble3(BinaryWriter writer, double3 v)
        {
            writer.Write(v.x);
            writer.Write(v.y);
            writer.Write(v.z);
        }

        private static double3 ReadDouble3(BinaryReader reader)
        {
            return new double3(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
        }

        private static void WriteDouble4(BinaryWriter writer, double4 v)
        {
            writer.Write(v.x);
            writer.Write(v.y);
            writer.Write(v.z);
            writer.Write(v.w);
        }

        private static double4 ReadDouble4(BinaryReader reader)
        {
            return new double4(reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
        }
        #endregion
    }
} 