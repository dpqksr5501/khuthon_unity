using UnityEngine;
using UnityEngine.InputSystem;

namespace Khuthon.InGame
{
    /// <summary>
    /// StarterAssets ThirdPersonController와 함께 쓸 수 있는 플레이어 이동 컨트롤러.
    /// 새 Input System 기반. 텍스트 입력 UI 열림 시 이동/카메라 잠금 지원.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("이동 설정")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float runSpeed = 10f;
        [SerializeField] private float jumpHeight = 1.5f;
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float rotationSmoothTime = 0.12f;

        [Header("카메라")]
        [SerializeField] private Transform cameraTransform;

        [Header("입력 액션 (Input System)")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string moveActionName = "Player/Move";
        [SerializeField] private string jumpActionName = "Player/Jump";
        [SerializeField] private string sprintActionName = "Player/Sprint";

        // 이동 잠금 (UI 열림 시)
        public bool MovementLocked { get; set; } = false;

        private CharacterController _controller;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;

        private Vector3 _velocity;
        private float _rotationVelocity;
        private bool _isGrounded;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            if (inputActions != null)
            {
                _moveAction = inputActions.FindAction(moveActionName);
                _jumpAction = inputActions.FindAction(jumpActionName);
                _sprintAction = inputActions.FindAction(sprintActionName);
            }
        }

        private void OnEnable()
        {
            _moveAction?.Enable();
            _jumpAction?.Enable();
            _sprintAction?.Enable();
        }

        private void OnDisable()
        {
            _moveAction?.Disable();
            _jumpAction?.Disable();
            _sprintAction?.Disable();
        }

        private void Update()
        {
            CheckGrounded();
            HandleGravity();

            if (MovementLocked) return;

            HandleMove();
            HandleJump();
        }

        private void CheckGrounded()
        {
            _isGrounded = _controller.isGrounded;
            if (_isGrounded && _velocity.y < 0f)
                _velocity.y = -2f;
        }

        private void HandleGravity()
        {
            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        private void HandleMove()
        {
            Vector2 input = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            bool isSprinting = _sprintAction?.IsPressed() ?? false;
            float speed = isSprinting ? runSpeed : walkSpeed;

            if (input.magnitude < 0.1f) return;

            // 카메라 방향 기준으로 이동 방향 계산
            float targetAngle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg
                                + (cameraTransform != null ? cameraTransform.eulerAngles.y : 0f);

            float smoothAngle = Mathf.SmoothDampAngle(
                transform.eulerAngles.y, targetAngle, ref _rotationVelocity, rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            _controller.Move(moveDir.normalized * speed * Time.deltaTime);
        }

        private void HandleJump()
        {
            if (_jumpAction != null && _jumpAction.WasPressedThisFrame() && _isGrounded)
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }
}
