using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace GeoToolkit
{
    public class CameraController : MonoBehaviour
    {
        private const float k_MouseSensitivityMultiplier = 3.0f;
        public static Func<bool> SlideoutUIFunc;
        public float m_LookSpeed = 50f;
        [Header("最小移动速度")]
        public float m_MinMoveSpeed = 50f;
        [Header("最大移动速度")]
        public float m_MoveSpeed = 500;
        [Header("最小滚轮移动速度")]
        public float m_MinScrollSpeed = 300;
        [Header("最大滚轮移动速度")]
        public float m_ScrollSpeed = 1000;
        public float m_Turbo = 5f;
        [Header("旋转阻尼系数")]
        public float rotationSmoothingFactor = 25;
        [Header("平移阻尼系数")]
        public float movementSmoothingFactor = 10;
        [Header("离地距离")]
        public float mindis = 3;
        [Header("离地线性差值最大计算距离")]
        public float maxdis = 600;

        [Header("相机可碰撞的图层")]
        public LayerMask RaycastLayerMask;

        [Header("一键回到Start位姿")]
        public bool enableReturnToStart = true;
        public KeyCode returnToStartKey = KeyCode.B;

        public enum ReturnSpace { World, Local }
        public ReturnSpace returnSpace = ReturnSpace.Local;

        private Vector3 _startPosition;
        private Quaternion _startRotation;

        public bool isProtectWallWithSphereCast = false;
        public float collisionRadius = 0.5f;

        [Header("聚焦设置")]
        public bool isOn= false;
        public bool useSmoothFocus = true;
        public float focusMoveSpeed = 5f;
        public  float eps = 0.02f; 
        [Range(0.1f, 1f)]
        public float focusScreenWidthRatio = 0.5f;
        public LayerMask focusLayerMask = -1;
        private Vector3 targetPosition;
        [HideInInspector]
        public bool isFocusing = false;
        private GameObject focusedObject;

        [Header("跟踪设置")]
        public float followSpeedFactor = 10;
        private Transform followTarget;
        private Vector3 followOffset;
        private Vector3 followDirection;
        private bool isFollow;
        private bool enableHandle;
        private float followSpeed;
        private float followDistance;
        private float targetDistance;


        // 用于阻尼的运动速度和旋转角速度（非相机状态，仅作为过渡用）
        private Vector3 moveVelocity = Vector3.zero;
        // 保存当前角速度（单位：角度/秒）
        private Vector2 rotVelocity = Vector2.zero;

        // 记录与地面的距离，用于计算高度因子
        private float currentDis = 0f;

        private bool isFocused = true;
        private bool ignoreNextMouseInput = false;

        private Ray ray;
        private RaycastHit raycastHit;

        // 输入累加（注意：这些变量仅用于计算期望的瞬时速度）
        private float inputVertical;
        private float inputHorizontal;
        private float inputUpAxis;
        private float inputScrollWheel;
        private Vector2 mouseRotationInput;    // 右键旋转输入
        private Vector2 mouseTranslationInput; // 左键拖动平移输入
        
        [HideInInspector]
        public bool isShieldRotation = false;

        public Camera mainCamera;

        void Start()
        {
            ray = new Ray();
            ray.direction = Vector3.down;

            // ===== 新增：记录Start时的位姿 =====
            if (returnSpace == ReturnSpace.World)
            {
                _startPosition = transform.position;
                _startRotation = transform.rotation;
            }
            else // Local
            {
                _startPosition = transform.localPosition;
                _startRotation = transform.localRotation;
            }


            //string layerName = LayerMask.LayerToName(10);
            //RaycastLayerMask = !string.IsNullOrEmpty(layerName) ? 10 : 0;
        }

        private void OnDisable()
        {
            isFollow = false;
            isFocusing = false;
        }

        public void StopFocusing()
        {
            isFocusing = false;
        }

        void OnApplicationFocus(bool hasFocus)
        {
            isFocused = hasFocus;
            if (hasFocus)
            {
                ignoreNextMouseInput = true;
            }
        }

        void Update()
        {
            if (DebugManager.instance.displayRuntimeUI || !isFocused)
                return;

            if (isFollow)
            {
                if (enableHandle)
                {
                    if (Input.GetAxis("Mouse ScrollWheel") != 0)
                    {
                        targetDistance -= Input.GetAxis("Mouse ScrollWheel") * Vector3.Distance(transform.position, followTarget.position);
                        targetDistance = targetDistance < 1 ? 1 : targetDistance;
                    }

                    if (Input.GetMouseButton(0))
                    {
                        if (EventSystem.current.IsPointerOverGameObject())
                        {
                            return;
                        }
                        float x = Input.GetAxis("Mouse X") * 5;
                        float y = Input.GetAxis("Mouse Y") * 5;
                        followDirection = Quaternion.AngleAxis(x, Vector3.up) * Quaternion.AngleAxis(-y, transform.right) * followDirection;
                    }
                }

                return;
            }

            if (isFocusing)
            {
                MoveCameraToTarget();
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(-1))
            {
                if (IsPointerOverUI())
                    return;
            }
            if (SlideoutUIFunc!= null && SlideoutUIFunc())
            {
                return;
            }
            if (enableReturnToStart && Input.GetKeyDown(returnToStartKey))
            {
                if (returnSpace == ReturnSpace.World)
                {
                    transform.SetPositionAndRotation(_startPosition, _startRotation);
                }
                else
                {
                    transform.localPosition = _startPosition;
                    transform.localRotation = _startRotation;
                }
                moveVelocity = Vector3.zero;
                rotVelocity = Vector2.zero;
                ignoreNextMouseInput = true;
                return;
            }
         
            UpdateInputs();
            UpdateRotation();
            UpdateMovement();
        }

        private void FixedUpdate()
        {
            if (isFollow)
            {
                if (!enableHandle)
                {
                    followDirection = followTarget.TransformDirection(followOffset);
                }
                else
                {
                    followSpeed += followSpeedFactor * Time.fixedDeltaTime;

                    followDistance = Mathf.Lerp(followDistance, targetDistance, 5 * Time.fixedDeltaTime);
                    followDirection = followDirection.normalized * followDistance;
                }
                Vector3 targetPos = followTarget.position - followDirection;
                Quaternion targetRot = Quaternion.LookRotation(followDirection);
                transform.position = Vector3.Lerp(transform.position, targetPos, followSpeed * Time.fixedDeltaTime);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, followSpeed * Time.fixedDeltaTime);
            }
        }

        /// <summary>
        /// 自由跟随相机
        /// </summary>
        /// <param name="target">跟随目标</param>
        /// <param name="distance">跟随距离</param>
        public void FollowFree(Transform target, float distance, float followSpeed = 5)
        {
            isFocusing = false;
            followTarget = target;
            followDistance = distance;
            targetDistance = followDistance;
            followDirection = transform.forward * distance;
            this.followSpeed = followSpeed;
            enableHandle = true;
            isFollow = true;
        }

        /// <summary>
        /// 固定跟随相机
        /// </summary>
        /// <param name="target">跟随目标</param>
        /// <param name="distance">跟随距离</param>
        /// <param name="height">跟随高度</param>
        public void FollowFixed(Transform target, float distance, float height, float followSpeed = 5)
        {
            isFocusing = false;
            followTarget = target;
            followOffset = new Vector3(0, -height, distance);
            followDirection = followTarget.TransformDirection(followOffset);
            this.followSpeed = followSpeed;
            enableHandle = false;
            isFollow = true;
        }

        /// <summary>
        /// 退出跟随
        /// </summary>
        public void ExitFollow()
        {
            isFollow = false;
        }

        bool IsPointerOverUI()
        {
            PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventDataCurrentPosition, results);

            foreach (var result in results)
            {
                if (result.gameObject.layer == LayerMask.NameToLayer("UI")) // 仅当 UI 元素拦截时 return true
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 更新所有输入（键盘、鼠标）
        /// </summary>
        void UpdateInputs()
        {
            // 重置所有输入变量
            mouseRotationInput = Vector2.zero;
            mouseTranslationInput = Vector2.zero;
            inputVertical = 0f;
            inputHorizontal = 0f;
            inputUpAxis = 0f;
            inputScrollWheel = 0f;

            if (ignoreNextMouseInput)
            {
                ignoreNextMouseInput = false;
                // 但键盘输入依然有效
                return;
            }

            // 左键拖动产生平移输入（X:左右，Y:前后）
            if (Input.GetMouseButton(0))
            {
                mouseTranslationInput.x = -Input.GetAxis("Mouse X") * k_MouseSensitivityMultiplier;
                mouseTranslationInput.y = -Input.GetAxis("Mouse Y") * k_MouseSensitivityMultiplier;
            }

            // 右键拖动产生旋转输入
            if (Input.GetMouseButton(1))
            {
                mouseRotationInput.x = Input.GetAxis("Mouse X") * k_MouseSensitivityMultiplier;
                mouseRotationInput.y = Input.GetAxis("Mouse Y") * k_MouseSensitivityMultiplier;
                if (isShieldRotation)
                {
                    mouseRotationInput.x = 0;
                    mouseRotationInput.y = 0;
                }
            }

            // 键盘平移输入：W/S (前后)，A/D (左右)
            if (Input.GetKey(KeyCode.A)) inputHorizontal -= 1;
            if (Input.GetKey(KeyCode.D)) inputHorizontal += 1;
            if (Input.GetKey(KeyCode.W)) inputVertical += 1;
            if (Input.GetKey(KeyCode.S)) inputVertical -= 1;

            // 上下平移：Q/E
            if (Input.GetKey(KeyCode.Q)) inputUpAxis -= 1;
            if (Input.GetKey(KeyCode.E)) inputUpAxis += 1;

            // 中键拖动也用于上下平移
            if (Input.GetMouseButton(2))
            {
                inputUpAxis += Input.GetAxis("Mouse Y") * k_MouseSensitivityMultiplier;
            }

            // 鼠标滚轮输入
            inputScrollWheel = Input.GetAxis("Mouse ScrollWheel");
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetMouseButton(0))
            {
                inputScrollWheel -= Input.GetAxis("Mouse Y") * k_MouseSensitivityMultiplier;
            }
        }

        /// <summary>
        /// 使用右键输入更新旋转（添加阻尼效果）
        /// </summary>
        void UpdateRotation()
        {
            
            // 将鼠标旋转输入转换为期望的角速度（角度/秒）
            Vector2 desiredAngularVelocity = mouseRotationInput * m_LookSpeed;
            //desiredAngularVelocity.x *= 0.125f;
            // 计算旋转平滑因子（指数衰减）
            float rotSmoothFactor = 1 - Mathf.Exp(-rotationSmoothingFactor * Time.deltaTime);
            // 逐步将当前角速度平滑过渡到期望角速度
            rotVelocity = Vector2.Lerp(rotVelocity, desiredAngularVelocity, rotSmoothFactor);

            // 获取当前旋转角度，注意处理 pitch 的区间（-180~180）
            Vector3 currentEuler = transform.rotation.eulerAngles;
            float pitch = (currentEuler.x > 180f) ? currentEuler.x - 360f : currentEuler.x;
            float yaw = currentEuler.y;

            // 根据角速度积分更新角度
            // 注意：rotVelocity.y 控制俯仰（上下），方向与实际需求有关，如需反转可调整符号
            pitch -= rotVelocity.y * Time.deltaTime * 0.5f;
            yaw += rotVelocity.x * Time.deltaTime;

            // 限制俯仰角防止翻转
            pitch = Mathf.Clamp(pitch, -89.9f, 89.9f);

            // 生成目标旋转角度并赋值
            Quaternion targetRot = Quaternion.Euler(pitch, yaw, 0f);
            transform.rotation = targetRot;
        }

        /// <summary>
        /// 根据输入更新位置（含阻尼）
        /// </summary>
        void UpdateMovement()
        {
            // 先通过射线检测计算当前与地面的距离，从而获得高度因子
            ray.origin = new Vector3(transform.position.x, transform.position.y + 1000, transform.position.z);
            ray.direction = Vector3.down;
            float heightFactor = 0f;
            float height = 0;
            const float heightF = 0.2f;
            if (Physics.Raycast(ray, out raycastHit, 10000, 1 << 10))
            {
                currentDis = raycastHit.distance - 1000;
                height = currentDis;
                heightFactor = CalculateLerpFactor(currentDis);
            }
            else
            {
                height = transform.position.y;
                heightFactor = CalculateLerpFactor(transform.position.y);
            }

            // 根据高度因子计算基础移动速度和滚轮速度
            float baseMoveSpeed = math.abs(height * heightF);
            //baseMoveSpeed = math.clamp(baseMoveSpeed, m_MinMoveSpeed, m_MoveSpeed);
            baseMoveSpeed = Mathf.Lerp(m_MinMoveSpeed, m_MoveSpeed, heightFactor);
            //Debug.Log($"baseMoveSpeed:{baseMoveSpeed}");
            float scrollSpeed = math.abs(height * heightF);
            scrollSpeed = math.clamp(scrollSpeed, m_MinScrollSpeed, m_ScrollSpeed);
            //Debug.Log($"scrollSpeed:{scrollSpeed}");
            //float scrollSpeed = Mathf.Lerp(m_MinScrollSpeed, m_ScrollSpeed, heightFactor);
            float moveSpeed = baseMoveSpeed;
            // 键盘加速或减速
            if (Input.GetKey(KeyCode.LeftShift)) moveSpeed *= m_Turbo;
            if (Input.GetKey(KeyCode.LeftControl)) moveSpeed /= m_Turbo;

            // 计算平面上（XZ）的方向向量
            Vector3 rightXZ = new Vector3(transform.right.x, 0, transform.right.z).normalized;
            Vector3 forwardXZ = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;

            // 根据键盘和鼠标左键拖动，组合出期望的平移方向
            Vector3 desiredMove = Vector3.zero;
            desiredMove += rightXZ * inputHorizontal;
            desiredMove += forwardXZ * inputVertical;
            desiredMove += Vector3.up * inputUpAxis;
            desiredMove += rightXZ * mouseTranslationInput.x;
            desiredMove += forwardXZ * mouseTranslationInput.y;
            // 滚轮输入沿摄像机前方向前移
            desiredMove += transform.forward * inputScrollWheel * scrollSpeed;

            // 得到期望的瞬时速度（单位：米/秒）
            Vector3 desiredVelocity = desiredMove * moveSpeed;
            // 根据 movementSmoothingFactor 对当前移动速度做阻尼平滑
            float moveSmoothFactor = 1 - Mathf.Exp(-movementSmoothingFactor * Time.deltaTime);
            moveVelocity = Vector3.Lerp(moveVelocity, desiredVelocity, moveSmoothFactor);

            // 计算新的目标位置：基于 transform.position 加上速度积分
            Vector3 newPos = transform.position + moveVelocity * Time.deltaTime;

            if (isProtectWallWithSphereCast)
            {
                Vector3 desiredPosition = newPos;

                desiredPosition = HandleCollision(desiredPosition);

                transform.position = desiredPosition;


            }
            else
            {
                // 地面检测：确保新的 Y 坐标不低于地面 + mindis
                ray.origin = new Vector3(newPos.x, newPos.y + 1000, newPos.z);
                ray.direction = Vector3.down;
                if (Physics.Raycast(ray, out raycastHit, 10000, RaycastLayerMask))
                {
                    float minY = raycastHit.point.y + mindis;
                    if (newPos.y < minY)
                    {
                        newPos.y = minY;
                    }
                }
                transform.position = newPos;
            }

        }

        bool PerformCollisionCheck(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit)
        {
            // 使用球体射线检测
            return Physics.SphereCast(
                origin,
                collisionRadius,
                direction,
                out hit,
                maxDistance,
                RaycastLayerMask
            );
        }

        Vector3 HandleCollision(Vector3 desiredPosition)
        {
            // 计算从目标到相机的方向
            Vector3 direction = (desiredPosition - transform.position).normalized;
            float targetDistance = Vector3.Distance(transform.position, desiredPosition);

            // 执行碰撞检测
            RaycastHit hit;
            bool collisionOccurred = PerformCollisionCheck(
                transform.position,
                direction,
                targetDistance + 0.5f,
                out hit
            );

            // 处理碰撞结果
            if (collisionOccurred)
            {
                // 计算避免穿墙的位置
                float adjustedDistance = hit.distance - 0.3f;
                float currentDistance = adjustedDistance; //Mathf.Clamp(adjustedDistance, minDistance, maxDistance);

                // 更新期望位置
                desiredPosition = transform.position + direction * currentDistance;
            }
            return desiredPosition;
        }

        /// <summary>
        /// 根据与地面距离计算 Lerp 因子（0～1）
        /// </summary>
        /// <param name="distance">与地面的距离</param>
        /// <returns>线性因子</returns>
        float CalculateLerpFactor(float distance)
        {
            distance = math.abs(distance);
            if (distance <= mindis)
                return 0f;
            else if (distance >= maxdis)
                return 1f;
            else
            {
                float t = (distance - mindis) / (maxdis - mindis);
                return Mathf.Clamp01(t);
            }
        }

         /// <summary>
        ///  聚焦物体
        /// </summary>
        /// <param name="size">-1：计算物体的包围盒x计算屏幕占比， 不是-1 ：使用size计算屏幕占比</param>
        public void HandleFocusClick(float size = -1)
        {
            if (!isOn)
                return;
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, focusLayerMask))
            {
                GameObject clickedObject = hit.collider.gameObject;
                if (size == -1)
                {
                    FocusOnObject(clickedObject);
                    return;
                }
                FocusOnObject(clickedObject, size);
            }
        }

        /// <summary>
        ///  聚焦物体
        /// </summary>
        /// <param name="target"></param>
        /// <param name="size">-1：计算物体的包围盒x计算屏幕占比， 不是-1 ：使用size计算屏幕占比</param>
        public void HandleFocusClickTarget(GameObject target, float size = -1)
        {
            if (!isOn)
                return;
            if (size == -1)
            {
                FocusOnObject(target);
                return;
            }
            FocusOnObject(target, size);
        }
        public void FocusOnObject(GameObject targetObject, float objectSize)
        {
            focusedObject = targetObject;
            float distance = CalculateFocusDistance(objectSize, focusScreenWidthRatio);
            Vector3 objectCenter = targetObject.transform.position;
            Vector3 cameraForward = transform.forward;
            targetPosition = objectCenter - cameraForward * distance;
            if(Vector3.Distance(transform.position, targetPosition) <= eps)
            {
                return;
            }   
            moveVelocity = Vector3.zero;
            rotVelocity = Vector2.zero;
            ignoreNextMouseInput = true;
            isFocusing = true;
        }
        public void FocusOnObject(GameObject targetObject)
        {
            Bounds bounds = GetObjectBounds(targetObject);
            float objectWidth = bounds.size.x;
            FocusOnObject(targetObject, objectWidth * 2);
        }

        /// <summary>
        ///  聚焦物体
        /// </summary>
        /// <param name="target"></param>
        /// <param name="size">-1：计算物体的包围盒x计算屏幕占比， 不是-1 ：使用size计算屏幕占比</param>
        public float HandleFocusClickTargetDistance(GameObject target, float size = -1)
        {
            if (!isOn)
                return -1;
            if (size == -1)
            {
                return FocusOnObjectDistance(target);
            }
            return FocusOnObjectDistance(target, size);
        }
        public float FocusOnObjectDistance(GameObject targetObject, float objectSize)
        {
            focusedObject = targetObject;
            float distance = CalculateFocusDistance(objectSize, focusScreenWidthRatio);
            return distance;
        }
        public float FocusOnObjectDistance(GameObject targetObject)
        {
            Bounds bounds = GetObjectBounds(targetObject);
            float objectWidth = bounds.size.x;
            return FocusOnObjectDistance(targetObject, objectWidth * 2);
        }
        float CalculateFocusDistance(float objectWidth, float screenRatio)
        {
            float fov = mainCamera.fieldOfView;
            float aspect = mainCamera.aspect;
            float horizontalFOV = 2f * Mathf.Atan(Mathf.Tan(fov * Mathf.Deg2Rad / 2f) * aspect);
            float distance = (objectWidth / screenRatio) / (2f * Mathf.Tan(horizontalFOV / 2f));
            return distance;
        }

        Bounds GetObjectBounds(GameObject obj)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
                return renderer.bounds;
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                return bounds;
            }
            return new Bounds(obj.transform.position, Vector3.one);
        }

        void MoveCameraToTarget()
        {
            Vector3 newPos = transform.position;
            if (useSmoothFocus)
            {
                float smoothFactor = 1f - Mathf.Exp(-focusMoveSpeed * Time.deltaTime);
                newPos = Vector3.Lerp(transform.position, targetPosition, smoothFactor);
            }
            else
            {
                newPos = Vector3.MoveTowards(transform.position, targetPosition, Time.deltaTime * focusMoveSpeed);
            }

            if (isProtectWallWithSphereCast)
            {
                newPos = HandleCollision(newPos);
            }

            transform.position = newPos;

            if (Vector3.Distance(transform.position, targetPosition) <= eps)
            {
                // transform.position = targetPosition;
                isFocusing = false;
                moveVelocity = Vector3.zero;
                rotVelocity = Vector2.zero;
                ignoreNextMouseInput = true;
            }
        }

    }
}
