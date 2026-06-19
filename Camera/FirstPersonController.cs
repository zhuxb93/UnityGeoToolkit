using UnityEngine;

namespace GeoToolkit
{
    using UnityEngine;

    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("移动参数")]
        public float movementSpeed = 4.0f;
        public float jumpSpeed = 3.5f;
        public float runMultiplier = 2.0f;
        public float gravity = -9.81f;

        [Header("鼠标参数")]
        public float mouseSensitivity = 100f;

        private CharacterController characterController;
        private Vector3 velocity;
        private float xAxisClamp;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            xAxisClamp = 0.0f;
        }

        private void Update()
        {
            HandleMovement();
            HandleMouseLook();
        }

        private void HandleMovement()
        {
            if (characterController.isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }

            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");

            Vector3 movement = transform.right * x + transform.forward * z;

            // 基础移动
            characterController.Move(movement * movementSpeed * Time.deltaTime);

            // 跑步
            if (Input.GetKey(KeyCode.LeftShift))
            {
                characterController.Move(movement * movementSpeed * (runMultiplier - 1f) * Time.deltaTime);
            }

            // 跳跃
            if (Input.GetButton("Jump") && characterController.isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpSpeed * -2f * gravity);
            }

            // 重力
            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);
        }

        private void HandleMouseLook()
        {
            // 只有按住鼠标右键时才旋转
            if (Input.GetMouseButton(1))
            {
                float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
                float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

                xAxisClamp += mouseY;

                if (xAxisClamp > 90.0f)
                {
                    xAxisClamp = 90.0f;
                    mouseY = 0.0f;
                    ClampXAxisRotationToValue(270.0f);
                }
                else if (xAxisClamp < -90.0f)
                {
                    xAxisClamp = -90.0f;
                    mouseY = 0.0f;
                    ClampXAxisRotationToValue(90.0f);
                }

                // 上下旋转（绕X轴）
                transform.Rotate(Vector3.left * mouseY);

                // 左右旋转（绕Y轴）
                transform.Rotate(Vector3.up * mouseX, Space.World);

                // 强制 Z 轴为 0
                Vector3 bodyEuler = transform.eulerAngles;
                bodyEuler.z = 0;
                transform.eulerAngles = bodyEuler;
            }
        }

        private void ClampXAxisRotationToValue(float value)
        {
            Vector3 eulerRotation = transform.eulerAngles;
            eulerRotation.x = value;
            transform.eulerAngles = eulerRotation;
        }
    }

}


