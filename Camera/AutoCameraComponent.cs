using UnityEngine;

namespace GeoToolkit
{
    [ExecuteAlways]
    public class AutoCameraComponent : MonoBehaviour
    {
        [Header("Camera Reference")]
        public Transform m_CameraTransform;

        [Header("Face To Camera Settings")]
        public bool enableFaceToCamera = false;

        [Header("Face To Camera LookY")]
        public bool enableFaceToCameraLockY = true;

        [Header("Scale With Camera Settings")]
        public bool enableScaleWithCamera = false;
        public float adjustScaleFactor = 0.01f;

        [Header("Min Display Distance Settings")]
        public bool enableMinDisplayDistanceCheck = false;
        public float minDisplayDistance = float.MaxValue;

        [Header("Max Display Distance Settings")]
        public bool enableMaxDisplayDistanceCheck = false;
        public float maxDisplayDistance = 0f;

        [Header("Distance Calculation Mode")]
        public bool useRaycastDown = false;  // true = 射线模式，false = 普通距离

        private Transform[] children;

        void Start()
        {
            if (m_CameraTransform == null)
            {
                m_CameraTransform = Camera.main?.transform;
            }

            // Get all immediate children only
            children = new Transform[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            {
                children[i] = transform.GetChild(i);
            }
        }

        void Update()
        {
            if (children == null || children.Length == 0 || m_CameraTransform == null) return;

            foreach (Transform child in children)
            {
                if (child != null)
                {
                    if (enableFaceToCamera)
                    {
                        FaceToCamera(child);
                    }

                    if (enableScaleWithCamera)
                    {
                        ScaleWithCamera(child);
                    }

                    if (enableMinDisplayDistanceCheck || enableMaxDisplayDistanceCheck)
                    {
                        DisplayDistance(child);
                    }
                }
            }
        }

        private void FaceToCamera(Transform target)
        {
            //target.LookAt(target.position + m_CameraTransform.rotation * Vector3.forward,
            //              m_CameraTransform.rotation * Vector3.up);
            Vector3 dirToCamera = m_CameraTransform.position - target.position;
            if (enableFaceToCameraLockY)
            {
                dirToCamera.y = 0;
            }
            Quaternion targetRot = Quaternion.LookRotation(-dirToCamera);
            target.rotation = targetRot;
        }

        private void ScaleWithCamera(Transform target)
        {
            float distance = GetDistance(target.position);
            target.localScale = Vector3.one * distance * adjustScaleFactor;
        }

        private void DisplayDistance(Transform target)
        {
            float distance = GetDistance(target.position);

            bool withinMin = !enableMinDisplayDistanceCheck || distance <= minDisplayDistance;
            bool withinMax = !enableMaxDisplayDistanceCheck || distance >= maxDisplayDistance;

            bool shouldBeActive = withinMin && withinMax;

            if (target.gameObject.activeSelf != shouldBeActive)
            {
                target.gameObject.SetActive(shouldBeActive);
            }
        }

        private float GetDistance(Vector3 targetPos)
        {
            if (!useRaycastDown)
            {
                // 普通距离：相机到物体
                return Vector3.Distance(targetPos, m_CameraTransform.position);
            }
            else
            {
                // 射线距离：相机往下打一根射线
                if (Physics.Raycast(m_CameraTransform.position, Vector3.down, out RaycastHit hit))
                {
                    return Vector3.Distance(m_CameraTransform.position, hit.point);
                }
                else
                {
                    // 如果没打中，就退回普通距离
                    return Vector3.Distance(targetPos, m_CameraTransform.position);
                }
            }
        }
    }

}
