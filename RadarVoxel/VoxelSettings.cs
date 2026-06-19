#if UNITY_EDITOR
using System;
using UnityEngine;

namespace GeoToolkit.RadarVoxel
{
    /// <summary>
    /// 体素设置配置类
    /// </summary>
    [Serializable]
    public class VoxelSettings
    {
        [Header("体素尺寸")]
        [Min(0.1f)]
        public float voxelWidth = 10f;

        [Min(0.1f)]
        public float voxelHeight = 10f;

        [Min(0.1f)]
        public float voxelDepth = 10f;

        /// <summary>
        /// 获取体素尺寸向量
        /// </summary>
        public Vector3 GetVoxelSize()
        {
            return new Vector3(voxelWidth, voxelHeight, voxelDepth);
        }

        /// <summary>
        /// 验证设置有效性
        /// </summary>
        public bool Validate()
        {
            if (voxelWidth <= 0 || voxelHeight <= 0 || voxelDepth <= 0)
            {
                Debug.LogError("体素尺寸必须大于0");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 克隆设置
        /// </summary>
        public VoxelSettings Clone()
        {
            return new VoxelSettings
            {
                voxelWidth = voxelWidth,
                voxelHeight = voxelHeight,
                voxelDepth = voxelDepth
            };
        }
    }
}
#endif