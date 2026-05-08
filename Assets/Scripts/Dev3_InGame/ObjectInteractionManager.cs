using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Khuthon.Backend;
using Khuthon.InGame;
using System.Collections.Generic;

namespace Khuthon
{
    /// <summary>
    /// 플레이어가 바라보는 오브젝트를 감지하고 'G' 키로 추천 기능을 수행합니다.
    /// </summary>
    public class ObjectInteractionManager : MonoBehaviour
    {
        [Header("상호작용 설정")]
        [SerializeField] private LayerMask objectLayer;
        [SerializeField] private float interactDistance = 4f;

        [Header("UI Toolkit 설정")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset recommendPopupAsset;
        
        private VisualElement _root;
        private VisualElement _recommendPopup;
        private Label _countLabel;
        
        private GameObject _focusedObject;
        private PlacedObjectHandle _focusedHandle;
        private Camera _mainCamera;

        private void Awake()
        {
            _mainCamera = Camera.main;
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            
            if (uiDocument != null)
            {
                _root = uiDocument.rootVisualElement;
                // UIDocument에 기본적으로 붙어있는 UI가 있다면 제거 (인스펙터에서 Source Asset이 설정된 경우 대비)
                _root.Clear();
            }
        }

        private void Update()
        {
            HandleDetection();
            HandleInput();

            if (_recommendPopup != null && _focusedObject != null)
            {
                UpdateUIPosition();
            }
        }

        private List<PlacedObjectHandle> _nearbyObjects = new List<PlacedObjectHandle>();

        private void OnTriggerEnter(Collider other)
        {
            var handle = other.GetComponentInParent<PlacedObjectHandle>();
            if (handle != null && !_nearbyObjects.Contains(handle))
            {
                _nearbyObjects.Add(handle);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var handle = other.GetComponentInParent<PlacedObjectHandle>();
            if (handle != null && _nearbyObjects.Contains(handle))
            {
                _nearbyObjects.Remove(handle);
                if (_focusedHandle == handle)
                {
                    UnfocusObject();
                }
            }
        }

        private void HandleDetection()
        {
            if (_nearbyObjects.Count == 0)
            {
                if (_focusedObject != null) UnfocusObject();
                return;
            }

            // 리스트에서 가장 가까운 오브젝트 찾기
            PlacedObjectHandle closestHandle = null;
            float minDistance = float.MaxValue;

            for (int i = _nearbyObjects.Count - 1; i >= 0; i--)
            {
                var handle = _nearbyObjects[i];
                if (handle == null) // 파괴된 오브젝트 예외 처리
                {
                    _nearbyObjects.RemoveAt(i);
                    continue;
                }

                float dist = Vector3.Distance(transform.position, handle.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestHandle = handle;
                }
            }

            // 가장 가까운 오브젝트 포커스
            if (closestHandle != null)
            {
                if (_focusedObject != closestHandle.gameObject)
                {
                    FocusObject(closestHandle);
                }
            }
            else if (_focusedObject != null)
            {
                UnfocusObject();
            }
        }

        private void HandleInput()
        {
            if (_focusedHandle == null) return;

            // 'G' 키 감지
            if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
            {
                RecommendObject(_focusedHandle);
            }
        }

        private void FocusObject(PlacedObjectHandle handle)
        {
            if (handle == null || _focusedObject == handle.gameObject) return;
            
            // 기존 팝업이 있다면 확실히 제거
            UnfocusObject();

            _focusedObject = handle.gameObject;
            _focusedHandle = handle;

            if (recommendPopupAsset != null && _root != null)
            {
                // 생성 전 한 번 더 전체 자식 검사 (무한 생성 방지)
                _root.Clear(); 
                
                _recommendPopup = recommendPopupAsset.Instantiate();
                _countLabel = _recommendPopup.Q<Label>("count-label");
                _root.Add(_recommendPopup);
                
                RefreshRecommendCount(handle);
                SetHighlight(handle.gameObject, true);
            }
        }

        private void UnfocusObject()
        {
            // _root 아래의 모든 요소를 날려서 확실하게 정리
            if (_root != null)
            {
                _root.Clear();
            }

            if (_focusedObject != null)
            {
                SetHighlight(_focusedObject, false);
            }

            _recommendPopup = null;
            _focusedObject = null;
            _focusedHandle = null;
        }

        private void UpdateUIPosition()
        {
            if (_root == null || _root.panel == null || _mainCamera == null || _focusedObject == null) return;

            Vector3 worldPos = _focusedObject.transform.position + Vector3.up * 1.2f;

            // 카메라 뒤 체크 (NaN 방지)
            Vector3 screenPos = _mainCamera.WorldToViewportPoint(worldPos);
            if (screenPos.z <= 0)
            {
                if (_recommendPopup != null) _recommendPopup.style.display = DisplayStyle.None;
                return;
            }

            // 오브젝트 머리 위에 UI 배치
            Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, worldPos, _mainCamera);
            
            if (_recommendPopup != null)
            {
                _recommendPopup.style.display = DisplayStyle.Flex;
                _recommendPopup.style.left = panelPos.x - 90; // width/2
                _recommendPopup.style.top = panelPos.y - 120; // height
            }
        }

        private void RefreshRecommendCount(PlacedObjectHandle handle)
        {
            if (string.IsNullOrEmpty(handle.FirebaseKey)) return;

            string path = $"placements/{handle.UserId}/{handle.FirebaseKey}";
            FirebaseManager.Instance.ReadObject<PlacedObjectRecord>(path, (record, ok) => {
                if (ok && _countLabel != null)
                {
                    _countLabel.text = $"추천 수: {record.recommendCount}";
                }
            });
        }

        private void RecommendObject(PlacedObjectHandle handle)
        {
            if (string.IsNullOrEmpty(handle.FirebaseKey)) return;

            string path = $"placements/{handle.UserId}/{handle.FirebaseKey}";
            FirebaseManager.Instance.ReadObject<PlacedObjectRecord>(path, (record, ok) => {
                if (ok)
                {
                    record.recommendCount++;
                    FirebaseManager.Instance.WriteObject(path, record, (success) => {
                        if (success)
                        {
                            Debug.Log($"[Interaction] 추천 완료! 현재 추천 수: {record.recommendCount}");
                            if (_countLabel != null) _countLabel.text = $"추천 수: {record.recommendCount}";
                        }
                    });
                }
            });
        }

        private void SetHighlight(GameObject obj, bool highlight)
        {
            // 간단한 하이라이트 효과: Material 색상 조절
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.materials)
                {
                    if (highlight)
                        mat.EnableKeyword("_EMISSION");
                    else
                        mat.DisableKeyword("_EMISSION");
                }
            }
        }
    }
}
