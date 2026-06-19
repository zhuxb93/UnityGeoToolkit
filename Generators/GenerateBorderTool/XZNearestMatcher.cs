using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// ������ XZ ƽ�������ƥ�����������ڵ��Ρ�����ƥ�䣩
/// </summary>
public class XZNearestMatcher
{
    private readonly List<Vector3> _points;           // ԭʼ�㼯
    private readonly Dictionary<long, List<int>> _grid; // Grid Hash: key -> �������б�
    private readonly float _cellSize;
    private readonly Vector2 _minBounds;             // ���� grid �������

    public int TotalPoints => _points.Count;
    public float CellSize => _cellSize;

    /// <summary>
    /// ���캯���������ռ���������
    /// </summary>
    /// <param name="points">�ο����б����� DEM�����ε㣩</param>
    /// <param name="cellSize">�����С�����飺���ηֱ��ʵ� 1~2 ����</param>
    public XZNearestMatcher(List<Vector3> points, float cellSize = 10.0f)
    {
        _points = points ?? throw new ArgumentNullException(nameof(points));
        _cellSize = Mathf.Max(cellSize, 0.01f);
        _grid = new Dictionary<long, List<int>>();

        if (points.Count == 0) return;

        // ����߽磨���� grid ����ƫ�ƣ����ٹ�ϣ��ͻ��
        float minX = float.MaxValue, minZ = float.MaxValue;
        foreach (var p in points)
        {
            minX = Mathf.Min(minX, p.x);
            minZ = Mathf.Min(minZ, p.z);
        }
        _minBounds = new Vector2(minX, minZ);

        // ������������
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
        // 64λ��ϣ��ʹ�� Z-order ���߷��ϲ����򵥰棩
        return (gx << 32) | (gz & 0xFFFFFFFF);
    }

    /// <summary>
    /// �� XZ ƽ���������㣬�������� Y ֵ
    /// </summary>
    /// <param name="query">��ѯ��</param>
    /// <param name="maxSearchDistance">����������루XZ ƽ�棩</param>
    /// <returns>ƥ��� Y ֵ��δ�ҵ��򷵻� query.y</returns>
    public float FindNearestY(Vector3 query, float maxSearchDistance = 100.0f)
    {
        if (_points.Count == 0) return query.y;

        float bestY = query.y;
        float minSqrDist = maxSearchDistance * maxSearchDistance;
        bool found = false;

        int searchRadius = Mathf.CeilToInt(maxSearchDistance / _cellSize);

        int centerGridX = (int)((query.x - _minBounds.x) / _cellSize);
        int centerGridZ = (int)((query.z - _minBounds.y) / _cellSize);

        // ������Χ����
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
    /// ����ƥ�䣺��Ч���� listB
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