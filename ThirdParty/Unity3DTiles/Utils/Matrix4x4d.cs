using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GeoToolkit;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Bindings;
using UnityEngine.Scripting;

namespace Unity3DTiles
{
    public struct Matrix4x4d : IFormattable
    {
        public double m00;

        public double m10;

        public double m20;

        public double m30;

        public double m01;

        public double m11;

        public double m21;

        public double m31;

        public double m02;

        public double m12;

        public double m22;

        public double m32;

        public double m03;

        public double m13;

        public double m23;

        public double m33;

        public Vector3d position => GetPosition();
        public Quaternion rotation => GetRotation();
        public Vector3d lossyScale => GetLossyScale();

        private static readonly Matrix4x4d zeroMatrix = new Matrix4x4d(new Vector4d(0d, 0d, 0d, 0d), new Vector4d(0d, 0d, 0d, 0d), new Vector4d(0d, 0d, 0d, 0d), new Vector4d(0d, 0d, 0d, 0d));

        private static readonly Matrix4x4d identityMatrix = new Matrix4x4d(new Vector4d(1d, 0d, 0d, 0d), new Vector4d(0d, 1d, 0d, 0d), new Vector4d(0d, 0d, 1d, 0d), new Vector4d(0d, 0d, 0d, 1d));

        public static Matrix4x4d zero => zeroMatrix;

        public static Matrix4x4d identity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return identityMatrix;
            }
        }

        public Matrix4x4d(Vector4d column0, Vector4d column1, Vector4d column2, Vector4d column3)
        {
            m00 = column0.x;
            m01 = column1.x;
            m02 = column2.x;
            m03 = column3.x;
            m10 = column0.y;
            m11 = column1.y;
            m12 = column2.y;
            m13 = column3.y;
            m20 = column0.z;
            m21 = column1.z;
            m22 = column2.z;
            m23 = column3.z;
            m30 = column0.w;
            m31 = column1.w;
            m32 = column2.w;
            m33 = column3.w;
        }
        public double this[int row, int column]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return this[row + column * 4];
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                this[row + column * 4] = value;
            }
        }
        public double this[int index]
        {
            get
            {
                return index switch
                {
                    0 => m00,
                    1 => m10,
                    2 => m20,
                    3 => m30,
                    4 => m01,
                    5 => m11,
                    6 => m21,
                    7 => m31,
                    8 => m02,
                    9 => m12,
                    10 => m22,
                    11 => m32,
                    12 => m03,
                    13 => m13,
                    14 => m23,
                    15 => m33,
                    _ => throw new IndexOutOfRangeException("Invalid matrix index!"),
                };
            }
            set
            {
                switch (index)
                {
                    case 0:
                        m00 = value;
                        break;
                    case 1:
                        m10 = value;
                        break;
                    case 2:
                        m20 = value;
                        break;
                    case 3:
                        m30 = value;
                        break;
                    case 4:
                        m01 = value;
                        break;
                    case 5:
                        m11 = value;
                        break;
                    case 6:
                        m21 = value;
                        break;
                    case 7:
                        m31 = value;
                        break;
                    case 8:
                        m02 = value;
                        break;
                    case 9:
                        m12 = value;
                        break;
                    case 10:
                        m22 = value;
                        break;
                    case 11:
                        m32 = value;
                        break;
                    case 12:
                        m03 = value;
                        break;
                    case 13:
                        m13 = value;
                        break;
                    case 14:
                        m23 = value;
                        break;
                    case 15:
                        m33 = value;
                        break;
                    default:
                        throw new IndexOutOfRangeException("Invalid matrix index!");
                }
            }
        }

        public static Matrix4x4d ToMatrix4x4d(Matrix4x4 matrix4x4)
        {
            Vector4 column0 = matrix4x4.GetColumn(0);
            Vector4 column1 = matrix4x4.GetColumn(1);
            Vector4 column2 = matrix4x4.GetColumn(2);
            Vector4 column3 = matrix4x4.GetColumn(3);

            Matrix4x4d mat = Matrix4x4d.identity;
            mat.m00 = column0.x;
            mat.m01 = column1.x;
            mat.m02 = column2.x;
            mat.m03 = column3.x;
            mat.m10 = column0.y;
            mat.m11 = column1.y;
            mat.m12 = column2.y;
            mat.m13 = column3.y;
            mat.m20 = column0.z;
            mat.m21 = column1.z;
            mat.m22 = column2.z;
            mat.m23 = column3.z;
            mat.m30 = column0.w;
            mat.m31 = column1.w;
            mat.m32 = column2.w;
            mat.m33 = column3.w;
            return mat;
        }

        public static Matrix4x4 ToMatrix4x4(Matrix4x4d matrix4x4d)
        {
            Vector4d column0 = matrix4x4d.GetColumn(0);
            Vector4d column1 = matrix4x4d.GetColumn(1);
            Vector4d column2 = matrix4x4d.GetColumn(2);
            Vector4d column3 = matrix4x4d.GetColumn(3);

            Matrix4x4 mat = Matrix4x4.identity;
            mat.m00 = (float)column0.x;
            mat.m01 = (float)column1.x;
            mat.m02 = (float)column2.x;
            mat.m03 = (float)column3.x;
            mat.m10 = (float)column0.y;
            mat.m11 = (float)column1.y;
            mat.m12 = (float)column2.y;
            mat.m13 = (float)column3.y;
            mat.m20 = (float)column0.z;
            mat.m21 = (float)column1.z;
            mat.m22 = (float)column2.z;
            mat.m23 = (float)column3.z;
            mat.m30 = (float)column0.w;
            mat.m31 = (float)column1.w;
            mat.m32 = (float)column2.w;
            mat.m33 = (float)column3.w;
            return mat;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return GetColumn(0).GetHashCode() ^ (GetColumn(1).GetHashCode() << 2) ^ (GetColumn(2).GetHashCode() >> 2) ^ (GetColumn(3).GetHashCode() >> 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object other)
        {
            if (other is Matrix4x4 other2)
            {
                return Equals(other2);
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Matrix4x4d other)
        {
            return GetColumn(0).Equals(other.GetColumn(0)) && GetColumn(1).Equals(other.GetColumn(1)) && GetColumn(2).Equals(other.GetColumn(2)) && GetColumn(3).Equals(other.GetColumn(3));
        }

        public static Matrix4x4d operator *(Matrix4x4d lhs, Matrix4x4d rhs)
        {
            Matrix4x4d result = default(Matrix4x4d);
            result.m00 = lhs.m00 * rhs.m00 + lhs.m01 * rhs.m10 + lhs.m02 * rhs.m20 + lhs.m03 * rhs.m30;
            result.m01 = lhs.m00 * rhs.m01 + lhs.m01 * rhs.m11 + lhs.m02 * rhs.m21 + lhs.m03 * rhs.m31;
            result.m02 = lhs.m00 * rhs.m02 + lhs.m01 * rhs.m12 + lhs.m02 * rhs.m22 + lhs.m03 * rhs.m32;
            result.m03 = lhs.m00 * rhs.m03 + lhs.m01 * rhs.m13 + lhs.m02 * rhs.m23 + lhs.m03 * rhs.m33;
            result.m10 = lhs.m10 * rhs.m00 + lhs.m11 * rhs.m10 + lhs.m12 * rhs.m20 + lhs.m13 * rhs.m30;
            result.m11 = lhs.m10 * rhs.m01 + lhs.m11 * rhs.m11 + lhs.m12 * rhs.m21 + lhs.m13 * rhs.m31;
            result.m12 = lhs.m10 * rhs.m02 + lhs.m11 * rhs.m12 + lhs.m12 * rhs.m22 + lhs.m13 * rhs.m32;
            result.m13 = lhs.m10 * rhs.m03 + lhs.m11 * rhs.m13 + lhs.m12 * rhs.m23 + lhs.m13 * rhs.m33;
            result.m20 = lhs.m20 * rhs.m00 + lhs.m21 * rhs.m10 + lhs.m22 * rhs.m20 + lhs.m23 * rhs.m30;
            result.m21 = lhs.m20 * rhs.m01 + lhs.m21 * rhs.m11 + lhs.m22 * rhs.m21 + lhs.m23 * rhs.m31;
            result.m22 = lhs.m20 * rhs.m02 + lhs.m21 * rhs.m12 + lhs.m22 * rhs.m22 + lhs.m23 * rhs.m32;
            result.m23 = lhs.m20 * rhs.m03 + lhs.m21 * rhs.m13 + lhs.m22 * rhs.m23 + lhs.m23 * rhs.m33;
            result.m30 = lhs.m30 * rhs.m00 + lhs.m31 * rhs.m10 + lhs.m32 * rhs.m20 + lhs.m33 * rhs.m30;
            result.m31 = lhs.m30 * rhs.m01 + lhs.m31 * rhs.m11 + lhs.m32 * rhs.m21 + lhs.m33 * rhs.m31;
            result.m32 = lhs.m30 * rhs.m02 + lhs.m31 * rhs.m12 + lhs.m32 * rhs.m22 + lhs.m33 * rhs.m32;
            result.m33 = lhs.m30 * rhs.m03 + lhs.m31 * rhs.m13 + lhs.m32 * rhs.m23 + lhs.m33 * rhs.m33;
            return result;
        }

        public static Vector4d operator *(Matrix4x4d lhs, Vector4d vector)
        {
            Vector4d result = default(Vector4d);
            result.x = lhs.m00 * vector.x + lhs.m01 * vector.y + lhs.m02 * vector.z + lhs.m03 * vector.w;
            result.y = lhs.m10 * vector.x + lhs.m11 * vector.y + lhs.m12 * vector.z + lhs.m13 * vector.w;
            result.z = lhs.m20 * vector.x + lhs.m21 * vector.y + lhs.m22 * vector.z + lhs.m23 * vector.w;
            result.w = lhs.m30 * vector.x + lhs.m31 * vector.y + lhs.m32 * vector.z + lhs.m33 * vector.w;
            return result;
        }


        public static bool operator ==(Matrix4x4d lhs, Matrix4x4d rhs)
        {
            return lhs.GetColumn(0) == rhs.GetColumn(0) && lhs.GetColumn(1) == rhs.GetColumn(1) && lhs.GetColumn(2) == rhs.GetColumn(2) && lhs.GetColumn(3) == rhs.GetColumn(3);
        }

        public static bool operator !=(Matrix4x4d lhs, Matrix4x4d rhs)
        {
            return !(lhs == rhs);
        }

        //
        // Summary:
        //     Get a column of the matrix.
        //
        // Parameters:
        //   index:
        public Vector4d GetColumn(int index)
        {
            return index switch
            {
                0 => new Vector4d(m00, m10, m20, m30),
                1 => new Vector4d(m01, m11, m21, m31),
                2 => new Vector4d(m02, m12, m22, m32),
                3 => new Vector4d(m03, m13, m23, m33),
                _ => throw new IndexOutOfRangeException("Invalid column index!"),
            };
        }

        public Vector3d GetColumn3D(int index)
        {
            return index switch
            {
                0 => new Vector3d(m00, m10, m20),
                1 => new Vector3d(m01, m11, m21),
                2 => new Vector3d(m02, m12, m22),
                3 => new Vector3d(m03, m13, m23),
                _ => throw new IndexOutOfRangeException("Invalid column index!"),
            };
        }

        public void SetColumn(int index, Vector4d column)
        {
            this[0, index] = column.x;
            this[1, index] = column.y;
            this[2, index] = column.z;
            this[3, index] = column.w;
        }

        //
        // Summary:
        //     Returns a row of the matrix.
        //
        // Parameters:
        //   index:
        public Vector4d GetRow(int index)
        {
            return index switch
            {
                0 => new Vector4d(m00, m01, m02, m03),
                1 => new Vector4d(m10, m11, m12, m13),
                2 => new Vector4d(m20, m21, m22, m23),
                3 => new Vector4d(m30, m31, m32, m33),
                _ => throw new IndexOutOfRangeException("Invalid row index!"),
            };
        }
        public void SetRow(int index, Vector4d row)
        {
            this[index, 0] = row.x;
            this[index, 1] = row.y;
            this[index, 2] = row.z;
            this[index, 3] = row.w;
        }
        public Vector3d MultiplyPoint(Vector3d point)
        {
            Vector3d result = default(Vector3d);
            result.x = m00 * point.x + m01 * point.y + m02 * point.z + m03;
            result.y = m10 * point.x + m11 * point.y + m12 * point.z + m13;
            result.z = m20 * point.x + m21 * point.y + m22 * point.z + m23;
            double num = m30 * point.x + m31 * point.y + m32 * point.z + m33;
            num = 1f / num;
            result.x *= num;
            result.y *= num;
            result.z *= num;
            return result;
        }
        public Vector3d MultiplyVector(Vector3d vector)
        {
            Vector3d result = default(Vector3d);
            result.x = m00 * vector.x + m01 * vector.y + m02 * vector.z;
            result.y = m10 * vector.x + m11 * vector.y + m12 * vector.z;
            result.z = m20 * vector.x + m21 * vector.y + m22 * vector.z;
            return result;
        }
        public static Matrix4x4d Scale(Vector3d vector)
        {
            Matrix4x4d result = default(Matrix4x4d);
            result.m00 = vector.x;
            result.m01 = 0f;
            result.m02 = 0f;
            result.m03 = 0f;
            result.m10 = 0f;
            result.m11 = vector.y;
            result.m12 = 0f;
            result.m13 = 0f;
            result.m20 = 0f;
            result.m21 = 0f;
            result.m22 = vector.z;
            result.m23 = 0f;
            result.m30 = 0f;
            result.m31 = 0f;
            result.m32 = 0f;
            result.m33 = 1f;
            return result;
        }
        public Vector3d GetPosition()
        {
            return new Vector3d(m03, m13, m23);
        }

        public Quaternion GetRotation()
        {
            GetRotation_Injected(out Quaternion ret);
            return ret;
        }

        public Vector3d GetLossyScale()
        {
            GetLossyScale_Injected(out Vector3d ret);
            return ret;
        }

        private void GetRotation_Injected(out Quaternion ret)
        {
            ret = Quaternion.identity;

            // 提取 X、Y、Z 轴向量（包含旋转 + 缩放）
            Vector3d xAxis = GetColumn3D(0);
            Vector3d yAxis = GetColumn3D(1);
            Vector3d zAxis = GetColumn3D(2);
            // 计算缩放（各轴向量的长度）
            double sx = xAxis.Length();
            double sy = yAxis.Length();
            double sz = zAxis.Length();

            Vector3d scale = new Vector3d(sx, sy, sz);

            // 判断是否有缩放，避免除零
            if (sx == 0 || sy == 0 || sz == 0)
            {
                ret = Quaternion.identity;
                return;
            }

            // 归一化得到纯旋转矩阵的列
            xAxis /= sx;
            yAxis /= sy;
            zAxis /= sz;

            // 从旋转矩阵提取 Quaternion
            ret = Quaternion.LookRotation(new Vector3((float)zAxis.x, (float)zAxis.y, (float)zAxis.z), new Vector3((float)yAxis.x, (float)yAxis.y, (float)yAxis.z)); // Z=forward, Y=up
        }

        private void GetLossyScale_Injected(out Vector3d ret)
        {
            // 提取 X、Y、Z 轴向量（包含旋转 + 缩放）
            Vector3d xAxis = GetColumn3D(0);
            Vector3d yAxis = GetColumn3D(1);
            Vector3d zAxis = GetColumn3D(2);
            // 计算缩放（各轴向量的长度）
            double sx = xAxis.Length();
            double sy = yAxis.Length();
            double sz = zAxis.Length();
            ret = new Vector3d(sx, sy, sz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(string format)
        {
            return ToString(format, null);
        }
        //
        // Summary:
        //     Returns a formatted string for this matrix.
        //
        // Parameters:
        //   format:
        //     A numeric format string.
        //
        //   formatProvider:
        //     An object that specifies culture-specific formatting.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (string.IsNullOrEmpty(format))
            {
                format = "F5";
            }

            if (formatProvider == null)
            {
                formatProvider = CultureInfo.InvariantCulture.NumberFormat;
            }

            return string.Format("{0}\t{1}\t{2}\t{3}\n{4}\t{5}\t{6}\t{7}\n{8}\t{9}\t{10}\t{11}\n{12}\t{13}\t{14}\t{15}\n", m00.ToString(format, formatProvider), m01.ToString(format, formatProvider), m02.ToString(format, formatProvider), m03.ToString(format, formatProvider), m10.ToString(format, formatProvider), m11.ToString(format, formatProvider), m12.ToString(format, formatProvider), m13.ToString(format, formatProvider), m20.ToString(format, formatProvider), m21.ToString(format, formatProvider), m22.ToString(format, formatProvider), m23.ToString(format, formatProvider), m30.ToString(format, formatProvider), m31.ToString(format, formatProvider), m32.ToString(format, formatProvider), m33.ToString(format, formatProvider));
        }
    }
}
