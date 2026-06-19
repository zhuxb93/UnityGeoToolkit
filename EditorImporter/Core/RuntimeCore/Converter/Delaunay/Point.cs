using System;
using UnityEngine;

namespace GeoToolkit
{
    [System.Serializable]
    public class Point : IEquatable<Point>
    {
        [SerializeField]
        private float X;
        [SerializeField]
        private float Y;
        [SerializeField]
        public bool isShow = false;
        public float x { get { return X; } }
        public float y { get { return Y; } }
        public Vector2 pos { get { return new Vector2(X, Y); } }

        public Point(float x, float y)
        {
            X = x;
            Y = y;
        }

        public Point(Point point)
        {
            X = point.X;
            Y = point.Y;
        }

        public void SetPosition(Vector2 pos)
        {
            X = pos.x;
            Y = pos.y;
        }

        public override int GetHashCode()
        {
            return X.GetHashCode() ^ Y.GetHashCode();
        }

        public bool Equals(Point other)
        {
            if (other == null) return false;
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is Point))
                return false;
            else
                return Equals((Point)obj);
        }

        public bool EqualsPoint(Point point)
        {
            return X == point.X && Y == point.Y;
        }
        public bool EqualsPointScale(Point point, float dis)
        {
            if (Vector2.Distance(pos, point.pos) < dis)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

}
