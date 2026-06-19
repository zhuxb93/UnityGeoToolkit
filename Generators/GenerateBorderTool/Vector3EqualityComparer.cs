using System.Collections.Generic;
using UnityEngine;

public class Vector3EqualityComparer : IEqualityComparer<Vector3>
{
    private float tolerance;

    public Vector3EqualityComparer(float tolerance = 0.001f)
    {
        this.tolerance = tolerance;
    }

    public bool Equals(Vector3 x, Vector3 y)
    {
        return Vector3.SqrMagnitude(x - y) < tolerance * tolerance;
    }

    public int GetHashCode(Vector3 obj)
    {
        // Quantize coordinates before hashing so nearby points compare consistently.
        int x = Mathf.RoundToInt(obj.x / tolerance);
        int y = Mathf.RoundToInt(obj.y / tolerance);
        int z = Mathf.RoundToInt(obj.z / tolerance);
        return x ^ (y << 11) ^ (z << 22);
    }
}
