using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Matches points on the XZ plane and samples the nearest source Y value.
/// </summary>
public class XZNearestMatcher
{
    private readonly List<Vector3> _points;           // Source points.
    private readonly Dictionary<long, List<int>> _grid; // Grid hash: key -> point indices.
    private readonly float _cellSize;
    private readonly Vector2 _minBounds;             // Minimum bounds used as the grid origin.

    public int TotalPoints => _points.Count;
    public float CellSize => _cellSize;

    /// <summary>
    /// Builds a spatial index for nearest-height lookup.
    /// </summary>
    /// <param name="points">Reference points, such as DEM or terrain samples.</param>
    /// <param name="cellSize">Grid cell size. Use roughly one to two terrain sample intervals.</param>
    public XZNearestMatcher(List<Vector3> points, float cellSize = 10.0f)
    {
        _points = points ?? throw new ArgumentNullException(nameof(points));
        _cellSize = Mathf.Max(cellSize, 0.01f);
        _grid = new Dictionary<long, List<int>>();

        if (points.Count == 0) return;

        // Compute the origin used to offset grid coordinates and reduce hash collisions.
        float minX = float.MaxValue, minZ = float.MaxValue;
        foreach (var p in points)
        {
            minX = Mathf.Min(minX, p.x);
            minZ = Mathf.Min(minZ, p.z);
        }
        _minBounds = new Vector2(minX, minZ);

        // Populate the spatial grid.
        BuildGrid();
    }

    private void BuildGrid()
    {
        for (int i = 0; i < _points.Count; i++)
        {
            var p = _points[i];
            long key = GetGridKey(p.x, p.z);
            if (!_grid.TryGetValue(key, out var bucket))
            {
                bucket = new List<int>();
                _grid[key] = bucket;
            }
            bucket.Add(i);
        }
    }

    private long GetGridKey(float x, float z)
    {
        long gx = (long)((x - _minBounds.x) / _cellSize);
        long gz = (long)((z - _minBounds.y) / _cellSize);
        // Pack two 32-bit grid coordinates into one 64-bit key.
        return (gx << 32) | (gz & 0xFFFFFFFF);
    }

    /// <summary>
    /// Finds the nearest point on the XZ plane and returns its Y value.
    /// </summary>
    /// <param name="query">Point to sample.</param>
    /// <param name="maxSearchDistance">Maximum search distance on the XZ plane.</param>
    /// <returns>The matched Y value, or query.y when no point is found.</returns>
    public float FindNearestY(Vector3 query, float maxSearchDistance = 100.0f)
    {
        if (_points.Count == 0) return query.y;

        float bestY = query.y;
        float minSqrDist = maxSearchDistance * maxSearchDistance;
        bool found = false;

        int searchRadius = Mathf.CeilToInt(maxSearchDistance / _cellSize);

        int centerGridX = (int)((query.x - _minBounds.x) / _cellSize);
        int centerGridZ = (int)((query.z - _minBounds.y) / _cellSize);

        // Search neighboring grid cells within the configured radius.
        for (int ox = -searchRadius; ox <= searchRadius; ox++)
        {
            for (int oz = -searchRadius; oz <= searchRadius; oz++)
            {
                long key = ((long)(centerGridX + ox) << 32) | ((centerGridZ + oz) & 0xFFFFFFFF);
                if (!_grid.TryGetValue(key, out var bucket)) continue;

                foreach (int idx in bucket)
                {
                    var candidate = _points[idx];
                    float dx = candidate.x - query.x;
                    float dz = candidate.z - query.z;
                    float sqrDist = dx * dx + dz * dz;

                    if (sqrDist < minSqrDist)
                    {
                        minSqrDist = sqrDist;
                        bestY = candidate.y;
                        found = true;
                    }
                }
            }
        }

        return found ? bestY : query.y;
    }

    /// <summary>
    /// Applies nearest-height matching to the target list in place.
    /// </summary>
    public void MatchYValues(List<Vector3> targets, float maxSearchDistance = 100.0f)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            var p = targets[i];
            float newY = FindNearestY(p, maxSearchDistance);
            targets[i] = new Vector3(p.x, newY, p.z);
        }
    }
}
