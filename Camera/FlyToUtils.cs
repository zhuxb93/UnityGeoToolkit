using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
namespace GeoToolkit
{
    public static class FlyToUtils
    {
        public static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform child in root)
            {
                if (child.name == name)
                    return child;
                Transform result = FindChild(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        public static Transform FindParent(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name)
                return root;
            return FindParent(root.parent, name);
        }
        public static Transform FindParentStartWith(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name.StartsWith(name))
                return root;
            return FindParentStartWith(root.parent, name);
        }
        public static Transform HitObjectStartWith(string startWith)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return null;
            }
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hitInfo, float.MaxValue, LayerMask.GetMask("POI")))
            {
                if(string.IsNullOrEmpty(startWith))
                {
                    return hitInfo.collider.gameObject.transform;
                }
                return FindParentStartWith(hitInfo.collider.gameObject.transform, startWith);
            }
            return null;
        }

        /// <summary>
        /// 平滑移动相机到观察目标的最佳位置和角度
        /// </summary>
        /// <param name="target">观察目标</param>
        /// <param name="duration">移动持续时间（默认3秒）</param>
        /// <param name="distance">与目标的水平距离（默认10米）</param>
        /// <param name="rotate">水平旋转偏移角度（默认0度）</param>
        /// <param name="skew">垂直倾斜角度（默认0度）</param>
        public static Sequence FollowTo(Transform cameraTransform, Transform target,
                           float duration = 1f,
                           float distance = 10f,
                           float rotate = 0f,
                           float skew = 0f,
                           Sequence sequence = null)
        {
            cameraTransform.DOKill();

            if (sequence == null)
                sequence = DOTween.Sequence();
            sequence.SetEase(Ease.InOutQuad);

            // 计算目标点的位置
            Vector3 direction = Quaternion.Euler(skew, rotate, 0) * Vector3.back; // 旋转方向
            Vector3 targetPosition = target.position + direction * distance; // 计算目标位置

            // 计算新的目标旋转，使相机始终朝向目标
            Quaternion targetRotation = Quaternion.LookRotation(target.position - targetPosition, Vector3.up);

            // 逐步插值移动
            sequence.Append(cameraTransform.DOMove(targetPosition, duration).SetEase(Ease.InOutQuad));
            sequence.Join(cameraTransform.DORotateQuaternion(targetRotation, duration).SetEase(Ease.InOutQuad));

            return sequence;
        }

        /// <summary>
        /// 平滑移动物体到指定位置和旋转
        /// </summary>
        /// <param name="cameraTransform">移动的物体（如相机）</param>
        /// <param name="targetPosition">目标世界坐标位置</param>
        /// <param name="targetRotation">目标旋转角度</param>
        /// <param name="duration">移动持续时间（秒）</param>    
        public static Sequence FlyTo(
            Transform cameraTransform,
            Vector3 targetPosition,
            Quaternion targetRotation,
            float duration = 1f,
            Sequence sequence = null)
        {
            cameraTransform.DOKill(); // 终止现有动画

            sequence ??= DOTween.Sequence(); // 简化 null 判断
            sequence.SetEase(Ease.InOutQuad); // 默认缓动曲线

            // 同步执行移动和旋转动画
            sequence.Join(cameraTransform.DOMove(targetPosition, duration));
            sequence.Join(cameraTransform.DORotateQuaternion(targetRotation, duration));

            return sequence;
        }

        public static void GetTargetRotateSkew(Transform cameraTransform, Transform target, out float rotate, out float skew)
        {
            Vector3 direction = target.position - cameraTransform.position;
            Vector3 horizontalDirection = new Vector3(direction.x, 0, direction.z);
            rotate = Vector3.SignedAngle(Vector3.forward, horizontalDirection, Vector3.up);
            skew = Vector3.SignedAngle(horizontalDirection, direction, Vector3.right);
        }
    }
}
