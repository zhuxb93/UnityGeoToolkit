#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GeoToolkit.RadarVoxel
{
    /// <summary>
    /// 雷达体素Gizmos绘制辅助类
    /// </summary>
    [InitializeOnLoad]
    public static class RadarVoxelGizmos
    {
        private static RadarParameters currentRadarParams;
        private static bool enableGizmos = true;

        static RadarVoxelGizmos()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        /// <summary>
        /// 设置当前要绘制的雷达参数
        /// </summary>
        public static void SetRadarParameters(RadarParameters radarParams)
        {
            currentRadarParams = radarParams;
        }

        /// <summary>
        /// 切换Gizmos显示
        /// </summary>
        public static void ToggleGizmos(bool enabled)
        {
            enableGizmos = enabled;
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Scene视图绘制回调
        /// </summary>
        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!enableGizmos || currentRadarParams == null)
                return;

            DrawRadarRange();
            DrawDirectionArrow();
            DrawDistanceRings();
            DrawFOVWireframe();
        }

        /// <summary>
        /// 绘制雷达范围
        /// </summary>
        private static void DrawRadarRange()
        {
            Handles.color = new Color(0.2f, 0.8f, 1f, 0.3f);

            switch (currentRadarParams.scanMode)
            {
                case RadarParameters.ScanMode.Omnidirectional:
                    DrawOmnidirectionalRange();
                    break;

                case RadarParameters.ScanMode.Conical:
                    DrawConicalRange();
                    break;

                case RadarParameters.ScanMode.Sector:
                    DrawSectorRange();
                    break;
            }
        }

        /// <summary>
        /// 绘制全向范围
        /// </summary>
        private static void DrawOmnidirectionalRange()
        {
            Handles.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            Handles.DrawWireDisc(
                currentRadarParams.position,
                Vector3.up,
                currentRadarParams.maxDistance
            );

            if (currentRadarParams.minDistance > 0.1f)
            {
                Handles.color = new Color(1f, 0.5f, 0.2f, 0.5f);
                Handles.DrawWireDisc(
                    currentRadarParams.position,
                    Vector3.up,
                    currentRadarParams.minDistance
                );
            }

            Handles.DrawWireDisc(
                currentRadarParams.position,
                Vector3.right,
                currentRadarParams.maxDistance
            );

            Handles.DrawWireDisc(
                currentRadarParams.position,
                Vector3.forward,
                currentRadarParams.maxDistance
            );
        }

        /// <summary>
        /// 绘制锥形范围
        /// </summary>
        private static void DrawConicalRange()
        {
            Quaternion rotation = currentRadarParams.GetRotation();
            Vector3 forward = rotation * Vector3.forward;
            Vector3 up = rotation * Vector3.up;
            Vector3 right = rotation * Vector3.right;

            float halfAngle = Mathf.Min(currentRadarParams.horizontalFOV, currentRadarParams.verticalFOV) / 2f;
            float angleRad = halfAngle * Mathf.Deg2Rad;

            Vector3 apex = currentRadarParams.position;
            Vector3 baseCenter = apex + forward * currentRadarParams.maxDistance;
            float baseRadius = currentRadarParams.maxDistance * Mathf.Tan(angleRad);

            Handles.color = new Color(0.2f, 0.8f, 1f, 0.5f);

            Vector3[] edgePoints = new Vector3[]
            {
                baseCenter + up * baseRadius,
                baseCenter - up * baseRadius,
                baseCenter + right * baseRadius,
                baseCenter - right * baseRadius
            };

            foreach (var point in edgePoints)
            {
                Handles.DrawLine(apex, point);
            }

            Handles.DrawWireDisc(baseCenter, forward, baseRadius);

            if (currentRadarParams.minDistance > 0.1f)
            {
                Vector3 minBaseCenter = apex + forward * currentRadarParams.minDistance;
                float minBaseRadius = currentRadarParams.minDistance * Mathf.Tan(angleRad);
                Handles.color = new Color(1f, 0.5f, 0.2f, 0.5f);
                Handles.DrawWireDisc(minBaseCenter, forward, minBaseRadius);
            }
        }

        /// <summary>
        /// 绘制扇形范围
        /// </summary>
        private static void DrawSectorRange()
        {
            Quaternion rotation = currentRadarParams.GetRotation();
            Vector3 forward = rotation * Vector3.forward;
            Vector3 up = rotation * Vector3.up;
            Vector3 right = rotation * Vector3.right;

            float horizontalHalfAngle = currentRadarParams.horizontalFOV / 2f;
            float verticalHalfAngle = currentRadarParams.verticalFOV / 2f;

            Vector3 apex = currentRadarParams.position;

            Handles.color = new Color(0.2f, 0.8f, 1f, 0.3f);
            DrawSectorPlane(apex, forward, up, horizontalHalfAngle, currentRadarParams.maxDistance);

            Handles.color = new Color(0.8f, 0.2f, 1f, 0.3f);
            DrawSectorPlane(apex, forward, right, verticalHalfAngle, currentRadarParams.maxDistance);

            Handles.color = new Color(0.2f, 0.8f, 1f, 0.5f);

            for (int h = -1; h <= 1; h += 2)
            {
                for (int v = -1; v <= 1; v += 2)
                {
                    Quaternion hRot = Quaternion.AngleAxis(h * horizontalHalfAngle, up);
                    Quaternion vRot = Quaternion.AngleAxis(v * verticalHalfAngle, right);
                    Vector3 direction = hRot * vRot * forward;
                    Vector3 endPoint = apex + direction * currentRadarParams.maxDistance;
                    Handles.DrawLine(apex, endPoint);
                }
            }
        }

        /// <summary>
        /// 绘制扇形平面
        /// </summary>
        private static void DrawSectorPlane(Vector3 center, Vector3 forward, Vector3 axis, float halfAngle, float radius)
        {
            int segments = Mathf.CeilToInt(halfAngle * 2 / 5f);
            segments = Mathf.Max(segments, 8);

            Vector3 prevPoint = center;
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments);
                Quaternion rot = Quaternion.AngleAxis(angle, axis);
                Vector3 direction = rot * forward;
                Vector3 point = center + direction * radius;

                if (i > 0)
                {
                    Handles.DrawLine(prevPoint, point);
                }
                Handles.DrawLine(center, point);
                prevPoint = point;
            }
        }

        /// <summary>
        /// 绘制方向箭头
        /// </summary>
        private static void DrawDirectionArrow()
        {
            Quaternion rotation = currentRadarParams.GetRotation();
            Vector3 forward = rotation * Vector3.forward;
            Vector3 arrowEnd = currentRadarParams.position + forward * (currentRadarParams.maxDistance * 0.3f);

            Handles.color = new Color(1f, 1f, 0f, 0.8f);
            Handles.ArrowHandleCap(
                0,
                currentRadarParams.position,
                rotation,
                currentRadarParams.maxDistance * 0.3f,
                EventType.Repaint
            );
        }

        /// <summary>
        /// 绘制距离环
        /// </summary>
        private static void DrawDistanceRings()
        {
            int ringCount = 5;
            float distanceStep = (currentRadarParams.maxDistance - currentRadarParams.minDistance) / ringCount;

            Quaternion rotation = currentRadarParams.GetRotation();
            Vector3 forward = rotation * Vector3.forward;

            for (int i = 1; i <= ringCount; i++)
            {
                float distance = currentRadarParams.minDistance + distanceStep * i;
                float alpha = 0.2f * (1f - (float)i / ringCount);

                Handles.color = new Color(0.5f, 0.5f, 0.5f, alpha);

                switch (currentRadarParams.scanMode)
                {
                    case RadarParameters.ScanMode.Omnidirectional:
                        Handles.DrawWireDisc(currentRadarParams.position, Vector3.up, distance);
                        break;

                    case RadarParameters.ScanMode.Conical:
                        float halfAngle = Mathf.Min(currentRadarParams.horizontalFOV, currentRadarParams.verticalFOV) / 2f;
                        float radius = distance * Mathf.Tan(halfAngle * Mathf.Deg2Rad);
                        Vector3 ringCenter = currentRadarParams.position + forward * distance;
                        Handles.DrawWireDisc(ringCenter, forward, radius);
                        break;
                }

                if (i == ringCount || i == 1)
                {
                    Vector3 labelPos = currentRadarParams.position + forward * distance;
                    Handles.Label(labelPos, $"{distance:F0}m");
                }
            }
        }

        /// <summary>
        /// 绘制FOV线框
        /// </summary>
        private static void DrawFOVWireframe()
        {
            if (currentRadarParams.scanMode == RadarParameters.ScanMode.Omnidirectional)
                return;

            Quaternion rotation = currentRadarParams.GetRotation();
            Vector3 forward = rotation * Vector3.forward;
            Vector3 up = rotation * Vector3.up;
            Vector3 right = rotation * Vector3.right;

            Vector3 labelPos = currentRadarParams.position + forward * (currentRadarParams.maxDistance * 0.5f);

            Handles.Label(
                labelPos + up * 20,
                $"H-FOV: {currentRadarParams.horizontalFOV:F1}°"
            );

            Handles.Label(
                labelPos - up * 20,
                $"V-FOV: {currentRadarParams.verticalFOV:F1}°"
            );
        }

    }
}
#endif