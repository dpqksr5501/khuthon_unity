using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Khuthon.InGame;
using System.Collections;
using StarterAssets;

namespace Khuthon
{
    [System.Serializable]
    public class IslandData
    {
        public string islandName;
        public Transform cameraAnchor; // 카메라가 이동할 위치와 회전값
        public string targetSceneName;
    }

    public class IslandPortal : MonoBehaviour
    {
        [Header("섬 설정")]
        [SerializeField] private List<IslandData> islands = new List<IslandData>();
        [SerializeField] private float transitionSpeed = 5f;

        [Header("참조")]
        [SerializeField] private Camera portalCamera; // 포탈 진입 시 활성화할 카메라 (선택 사항)
        
        private PlayerController _customPlayerController;
        private ThirdPersonController _starterThirdPersonController;
        private StarterAssetsInputs _starterInputs;
        private Camera _mainCamera;
        private MonoBehaviour _cinemachineBrain;

        private bool _isPortalActive = false;
        private int _currentIndex = 0;
        private Coroutine _transitionCoroutine;

        private void Awake()
        {
            if (portalCamera != null) portalCamera.gameObject.SetActive(false);
            _mainCamera = Camera.main;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isPortalActive) return;

            Debug.Log($"[Portal] 충돌 감지됨: {other.name} (Tag: {other.tag})");

            if (other.CompareTag("Player"))
            {
                _customPlayerController = other.GetComponentInParent<PlayerController>();
                _starterThirdPersonController = other.GetComponentInParent<ThirdPersonController>();
                _starterInputs = other.GetComponentInParent<StarterAssetsInputs>();

                if (_customPlayerController != null || _starterThirdPersonController != null)
                {
                    Debug.Log("[Portal] 플레이어 감지 완료. 모드 전환 시작.");
                    EnterPortalMode();
                }
                else
                {
                    Debug.LogWarning("[Portal] 'Player' 태그는 있으나 컨트롤러 컴포넌트를 찾을 수 없습니다.");
                }
            }
        }

        private void EnterPortalMode()
        {
            _isPortalActive = true;
            
            // 시네머신 브레인 비활성화 (수동 카메라 제어를 위해)
            if (_mainCamera != null)
            {
                _cinemachineBrain = _mainCamera.GetComponent("CinemachineBrain") as MonoBehaviour;
                if (_cinemachineBrain != null)
                {
                    _cinemachineBrain.enabled = false;
                    Debug.Log("[Portal] CinemachineBrain 비활성화됨.");
                }
            }

            if (_customPlayerController != null) _customPlayerController.MovementLocked = true;
            if (_starterThirdPersonController != null) _starterThirdPersonController.enabled = false;
            
            if (_starterInputs != null)
            {
                _starterInputs.move = Vector2.zero;
                _starterInputs.look = Vector2.zero;
                _starterInputs.cursorLocked = false;
                _starterInputs.cursorInputForLook = false;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (portalCamera != null)
            {
                portalCamera.gameObject.SetActive(true);
            }

            UpdateCameraView();
            Debug.Log("[Portal] 포탈 모드 진입 완료.");
        }

        private void Update()
        {
            if (!_isPortalActive) return;

            HandleSelectionInput();
            HandleConfirmationInput();
            HandleExitInput();
        }

        private void HandleSelectionInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool leftPressed = keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame;
            bool rightPressed = keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame;

            if (leftPressed || rightPressed)
            {
                if (islands.Count <= 1)
                {
                    Debug.LogWarning("[Portal] 전환할 섬이 부족합니다.");
                    return;
                }

                if (leftPressed)
                {
                    _currentIndex = (_currentIndex - 1 + islands.Count) % islands.Count;
                    Debug.Log($"[Portal] 이전 섬 선택: 인덱스 {_currentIndex}");
                }
                else
                {
                    _currentIndex = (_currentIndex + 1) % islands.Count;
                    Debug.Log($"[Portal] 다음 섬 선택: 인덱스 {_currentIndex}");
                }
                UpdateCameraView();
            }
        }

        private void HandleConfirmationInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                if (islands.Count > 0 && _currentIndex < islands.Count)
                {
                    string sceneName = islands[_currentIndex].targetSceneName;
                    if (!string.IsNullOrEmpty(sceneName))
                    {
                        Debug.Log($"[Portal] {sceneName} 씬으로 이동합니다.");
                        SceneManager.LoadScene(sceneName);
                    }
                }
            }
        }

        private void HandleExitInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                ExitPortalMode();
            }
        }

        private void ExitPortalMode()
        {
            _isPortalActive = false;
            
            if (_cinemachineBrain != null) _cinemachineBrain.enabled = true;

            if (_customPlayerController != null) _customPlayerController.MovementLocked = false;
            if (_starterThirdPersonController != null) _starterThirdPersonController.enabled = true;

            if (_starterInputs != null)
            {
                _starterInputs.cursorLocked = true;
                _starterInputs.cursorInputForLook = true;
            }

            if (portalCamera != null) portalCamera.gameObject.SetActive(false);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void UpdateCameraView()
        {
            if (islands.Count == 0) return;

            Transform target = islands[_currentIndex].cameraAnchor;
            if (target == null)
            {
                Debug.LogWarning($"[Portal] 인덱스 {_currentIndex}의 CameraAnchor가 없습니다.");
                return;
            }

            if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = StartCoroutine(TransitionCamera(target));
        }

        private IEnumerator TransitionCamera(Transform target)
        {
            Transform camTransform = portalCamera != null ? portalCamera.transform : _mainCamera.transform;
            
            Debug.Log($"[Portal] 카메라 이동 시작: {camTransform.name} -> {target.name}");

            float elapsed = 0;
            Vector3 startPos = camTransform.position;
            Quaternion startRot = camTransform.rotation;

            while (elapsed < 1.0f)
            {
                elapsed += Time.deltaTime * transitionSpeed;
                camTransform.position = Vector3.Lerp(startPos, target.position, elapsed);
                camTransform.rotation = Quaternion.Slerp(startRot, target.rotation, elapsed);
                yield return null;
            }

            camTransform.position = target.position;
            camTransform.rotation = target.rotation;
        }
    }
}
