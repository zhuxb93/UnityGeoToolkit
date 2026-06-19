#if UNITY_EDITOR
using System;
using UnityEngine;

namespace GeoToolkit.RadarVoxel
{
    /// <summary>
    /// 雷达参数配置类
    /// </summary>
    [Serializable]
    public class RadarParameters
    {
        /// <summary>
        /// 雷达扫描模式
        /// </summary>
        public enum ScanMode
        {
            Sector,         // 扇形扫描（默认）
            Conical,        // 锥形扫描
            Omnidirectional // 全向扫描
        }

        [Header("位置和朝向")]
        public Vector3 position = Vector3.zero;
        public Vector3 rotation = Vector3.zero;

        [Header("视场角设置")]
        [Range(0f, 360f)]
        public float horizontalFOV = 90f;

        [Range(0f, 180f)]
        public float verticalFOV = 60f;

        [Header("探测距离")]
        [Min(0.1f)]
        public float minDistance = 1f;

        [Min(1f)]
        public float maxDistance = 1000f;

        [Header("扫描模式")]
        public ScanMode scanMode = ScanMode.Sector;

        /// <summary>
        /// 获取旋转四元数
        /// </summary>
        public Quaternion GetRotation()
        {
            return Quaternion.Euler(rotation);
        }

        /// <summary>
        /// 验证参数有效性
        /// </summary>
        public bool Validate()
        {
            if (minDistance >= maxDistance)
            {
                Debug.LogError("最小探测距离必须小于最大探测距离");
                return false;
            }

            if (horizontalFOV <= 0 || verticalFOV <= 0)
            {
                Debug.LogError("视场角必须大于0");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 克隆参数
        /// </summary>
        public RadarParameters Clone()
        {
            return new RadarParameters
            {
                position = position,
                rotation = rotation,
                horizontalFOV = horizontalFOV,
                verticalFOV = verticalFOV,
                minDistance = minDistance,
                maxDistance = maxDistance,
                scanMode = scanMode
            };
        }
    }
}
#endif