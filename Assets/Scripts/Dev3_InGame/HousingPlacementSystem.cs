using System;
using UnityEngine;
using Khuthon.AI3D;
using Khuthon.Backend;

namespace Khuthon.InGame
{
    /// <summary>
    /// Raycast 기반 하우징 배치 시스템.
    /// - 생성된 GLB 모델을 "배치 모드"로 마우스 커서 위치에 미리 보기
    /// - 좌클릭으로 배치 확정, 우클릭 또는 ESC로 취소
    /// - 배치된 오브젝트 Firebase에 저장 (FirebaseManager 연동)
    /// - 배치된 오브젝트 나중에 Gizmo 클릭으로 선택/이동 가능 (간단 버전)
    /// </summary>
    public class HousingPlacementSystem : MonoBehaviour
    {
        [Header("Raycast 설정")]
        [Tooltip("배치 가능한 레이어 마스크 (Floor, Ground 등)")]
        [SerializeField] private LayerMask placementLayerMask = ~0;
        [Tooltip("배치 불가 레이어 (이미 배치된 오브젝트 등)")]
        [SerializeField] private LayerMask blockingLayerMask;
        [SerializeField] private float maxRayDistance = 50f;

        [Header("시각적 피드백")]
        [Tooltip("배치 가능 시 색상")]
        [SerializeField] private Color validColor = new Color(0f, 1f, 0f, 0.5f);
        [Tooltip("배치 불가 시 색상")]
        [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.5f);

        [Header("그리드 스냅 (옵션)")]
        [SerializeField] private bool useGridSnap = true;
        [SerializeField] private float gridSize = 1f;

        [Header("Firebase 저장")]
        [SerializeField] private bool saveToFirebase = true;
        [SerializeField] private string userId = "player_001";

        // 배치 완료 이벤트: (배치된 GameObject, 위치)
        public event Action<GameObject, Vector3> OnObjectPlaced;
        public event Action OnPlacementCancelled;

        public bool IsPlacingObject { get; private set; }

        private GameObject _previewObject;
        private string _pendingModelUrl;
        private string _currentPeriod;
        private Camera _camera;

        private void Awake()
        {
            _camera = Camera.main;
        }

        private void Update()
        {
            if (!IsPlacingObject || _previewObject == null) return;

            UpdatePreviewPosition();

            var mouse = UnityEngine.InputSystem.Mouse.current;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;

            if (mouse == null) return;

            // 좌클릭 → 배치 확정 (wasPressedThisFrame)
            if (mouse.leftButton.wasPressedThisFrame && !IsPointerOverUI())
                ConfirmPlacement();

            // 우클릭 또는 ESC → 취소
            if (mouse.rightButton.wasPressedThisFrame || 
                (keyboard != null && keyboard[UnityEngine.InputSystem.Key.Escape].wasPressedThisFrame))
                CancelPlacement();
        }

        /// <summary>
        /// 배치 모드 시작. 로드된 모델 오브젝트를 미리보기로 사용합니다.
        /// </summary>
        public void StartPlacement(GameObject modelObject, string modelUrl = "", string period = "")
        {
            if (IsPlacingObject) CancelPlacement();

            _pendingModelUrl = modelUrl;
            _currentPeriod = period;
            _previewObject = modelObject;

            // 반투명 미리보기 설정
            ApplyPreviewMaterial(_previewObject, validColor);

            IsPlacingObject = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log($"[Housing] 배치 모드 시작 (Period: {period})");
        }

        private void UpdatePreviewPosition()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;

            Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, maxRayDistance, placementLayerMask);

            if (hit)
            {
                Vector3 pos = hitInfo.point;
                if (useGridSnap) pos = SnapToGrid(pos);
                _previewObject.transform.position = pos;

                // 겹침 체크
                bool isBlocked = CheckOverlap(_previewObject);
                ApplyPreviewMaterial(_previewObject, isBlocked ? invalidColor : validColor);
            }
        }

        private void ConfirmPlacement()
        {
            if (_previewObject == null) return;

            Vector3 finalPos = _previewObject.transform.position;

            // 미리보기 재질 → 실제 재질 복원
            RestoreOriginalMaterial(_previewObject);

            // 이동/선택 가능 컴포넌트 추가
            var placer = _previewObject.AddComponent<PlacedObjectHandle>();
            placer.ModelUrl = _pendingModelUrl;

            Debug.Log($"[Housing] 배치 확정: {finalPos}");

            // Firebase 저장
            if (saveToFirebase && FirebaseManager.Instance != null)
            {
                var record = new PlacedObjectRecord
                {
                    userId = userId,
                    period = _currentPeriod,
                    objectName = _previewObject.name,
                    modelUrl = _pendingModelUrl,
                    posX = finalPos.x,
                    posY = finalPos.y,
                    posZ = finalPos.z,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                FirebaseManager.Instance.PushData($"placements/{userId}", JsonUtility.ToJson(record),
                    (key, ok) => Debug.Log(ok ? $"[Housing] Firebase 저장 완료: {key}" : "[Housing] Firebase 저장 실패"));
            }

            OnObjectPlaced?.Invoke(_previewObject, finalPos);

            _previewObject = null;
            _pendingModelUrl = "";
            _currentPeriod = "";
            IsPlacingObject = false;

            // 커서 재잠금 (플레이어 컨트롤러가 처리하도록)
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void CancelPlacement()
        {
            if (_previewObject != null)
            {
                Destroy(_previewObject);
                _previewObject = null;
            }
            IsPlacingObject = false;
            _pendingModelUrl = "";
            OnPlacementCancelled?.Invoke();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Debug.Log("[Housing] 배치 취소");
        }

        private Vector3 SnapToGrid(Vector3 pos)
        {
            return new Vector3(
                Mathf.Round(pos.x / gridSize) * gridSize,
                pos.y,
                Mathf.Round(pos.z / gridSize) * gridSize
            );
        }

        private bool CheckOverlap(GameObject obj)
        {
            // 오브젝트의 모든 콜라이더 기준 겹침 확인
            var colliders = obj.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                Bounds b = col.bounds;
                Collider[] hits = Physics.OverlapBox(b.center, b.extents * 0.9f, Quaternion.identity, blockingLayerMask);
                if (hits.Length > 0) return true;
            }
            return false;
        }

        private void ApplyPreviewMaterial(GameObject obj, Color color)
        {
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.materials)
                {
                    mat.color = color;
                    // URP의 경우 투명 렌더링 활성화
                    mat.SetFloat("_Surface", 1f);      // Transparent
                    mat.renderQueue = 3000;
                }
            }
        }

        private void RestoreOriginalMaterial(GameObject obj)
        {
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.materials)
                {
                    mat.color = Color.white;
                    mat.SetFloat("_Surface", 0f);      // Opaque
                    mat.renderQueue = -1;
                }
            }
        }

        private bool IsPointerOverUI()
        {
            return UnityEngine.EventSystems.EventSystem.current != null &&
                   UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        }
    }

    /// <summary>
    /// 배치된 오브젝트에 붙는 핸들 컴포넌트.
    /// 추후 이동/삭제 기능 확장 가능.
    /// </summary>
    public class PlacedObjectHandle : MonoBehaviour
    {
        public string ModelUrl { get; set; }

        // TODO: 클릭 선택 후 드래그로 재배치 기능 추가 예정
    }
}
