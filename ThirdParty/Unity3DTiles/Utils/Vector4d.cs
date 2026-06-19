using UnityEngine;

namespace Unity3DTiles
{

    public struct Vector4d
    {
        public double x;
        public double y;
        public double z;
        public double w;

        public Vector4d(double x, double y, double z, double w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public Vector4d Set(double x, double y, double z, double w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
            return this;
        }

        public double Length()
        {
            return System.Math.Sqrt(x * x + y * y + z * z + w * w);
        }

        public double SqrtLength()
        {
            return x * x + y * y + z * z;
        }

        public static Vector4d zero => new Vector4d(0, 0, 0, 0);

        // public static double Distance(Vector4d a, Vector4d b)
        // {
        //     return System.Math.Sqrt(System.Math.Pow(a.x - b.x, 2.0) + System.Math.Pow(a.y - b.y, 2.0) + System.Math.Pow(a.z - b.z, 2.0) + System.Math.Pow(a.w - b.w, 2.0));
        // }

        public override bool Equals(object o)
        {
            var v = (Vector4d)o;
            const double epsilon = 1e-11;

            return System.Math.Abs(v.x - x) < epsilon &&
                    System.Math.Abs(v.y - y) < epsilon &&
                    System.Math.Abs(v.z - z) < epsilon &&
                    System.Math.Abs(v.w - w) < epsilon;
        }

        public override int GetHashCode()
        {
            return x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2) ^ (w.GetHashCode() >> 2);
        }

        public static bool operator !=(Vector4d lhs, Vector4d rhs)
        {
            return !lhs.Equals(rhs);
        }

        public static bool operator ==(Vector4d lhs, Vector4d rhs)
        {
            return lhs.Equals(rhs);
        }

        public static Vector4d operator -(Vector4d v)
        {
            return new Vector4d(-v.x, -v.y, -v.z, -v.w);
        }

        public static Vector4d operator +(Vector4d lhs, Vector4d rhs)
        {
            return new Vector4d(lhs.x + rhs.x, lhs.y + rhs.y, lhs.z + rhs.z, lhs.w + rhs.w);
        }
        public static Vector4d operator +(Vector4d v, double a)
        {
            return new Vector4d(v.x + a, v.y + a, v.z + a, v.w + a);
        }
        public static Vector4d operator +(double a, Vector4d v)
        {
            return new Vector4d(v.x + a, v.y + a, v.z + a, v.w + a);
        }

        public static Vector4d operator -(Vector4d lhs, Vector4d rhs)
        {
            return new Vector4d(lhs.x - rhs.x, lhs.y - rhs.y, lhs.z - rhs.z, lhs.w - rhs.w);
        }
        public static Vector4d operator -(Vector4d v, double a)
        {
            return new Vector4d(v.x - a, v.y - a, v.z - a, v.w - a);
        }
        public static Vector4d operator -(double a, Vector4d v)
        {
            return new Vector4d(a - v.x, a - v.y, a - v.z, a - v.w);
        }

        public static Vector4d operator *(Vector4d v, float a)
        {
            return new Vector4d(v.x * a, v.y * a, v.z * a, v.w * a);
        }
        public static Vector4d operator *(float a, Vector4d v)
        {
            return new Vector4d(v.x * a, v.y * a, v.z * a, v.w * a);
        }
        public static Vector4d operator *(Vector4d v, double a)
        {
            return new Vector4d(v.x * a, v.y * a, v.z * a, v.w * a);
        }
        public static Vector4d operator *(double a, Vector4d v)
        {
            return new Vector4d(v.x * a, v.y * a, v.z * a, v.w * a);
        }

        public static Vector4d operator /(Vector4d v, float a)
        {
            return new Vector4d(v.x / a, v.y / a, v.z / a, v.w / a);
        }
        public static Vector4d operator /(float a, Vector4d v)
        {
            return new Vector4d(v.x / a, v.y / a, v.z / a, v.w / a);
        }

        public static explicit operator Vector4d(UnityEngine.Vector4 v)
        {
            return new Vector4d(v.x, v.y, v.z, v.w);
        }

        public static double Dot(Vector4d lhs, Vector4d rhs) { return lhs.x * rhs.x + lhs.y * rhs.y + lhs.z * rhs.z + lhs.w * rhs.w; }

        public static Vector4d Smerp(Vector4d lhs, Vector4d rhs, double t)
        {
            double one_d = 1 - t;
            return new Vector4d(lhs.x * t + rhs.x * one_d, lhs.y * t + rhs.y * one_d, lhs.z * t + rhs.z * one_d, lhs.w * t + rhs.w * one_d);
        }

        public static double Angle(Vector4d from, Vector4d to)
        {
            return System.Math.Acos(Dot(Normalize(from), Normalize(to))) * (180 / System.Math.PI);
        }

        public static Vector4d Normalize(Vector4d v)
        {
            var inverseLength = 1.0 / v.Length();
            return new Vector4d(v.x * inverseLength, v.y * inverseLength, v.z * inverseLength, v.w * inverseLength);
        }

        public override string ToString()
        {
            return " ( " + x + " " + y + " " + z + " " + w + " ) ";
        }

        public static Vector4d Zero => new Vector4d(0, 0, 0, 0);
        public static Vector4d One => new Vector4d(1, 1, 1, 1);
    }
}
