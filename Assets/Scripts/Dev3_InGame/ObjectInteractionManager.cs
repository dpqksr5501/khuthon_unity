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

        private void HandleDetection()
        {
            // 플레이어 주변 일정 거리(interactDistance) 내의 오브젝트 탐색
            Collider[] colliders = Physics.OverlapSphere(transform.position, interactDistance, objectLayer);
            
            PlacedObjectHandle closestHandle = null;
            float minDistance = float.MaxValue;

            foreach (var col in colliders)
            {
                var handle = col.GetComponentInParent<PlacedObjectHandle>();
                if (handle != null)
                {
                    float dist = Vector3.Distance(transform.position, handle.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closestHandle = handle;
                    }
                }
            }

            // 가장 가까운 오브젝트가 있다면 포커스
            if (closestHandle != null)
            {
                if (_focusedObject != closestHandle.gameObject)
                {
                    FocusObject(closestHandle);
                }
            }
            else
            {
                // 주변에 아무것도 없을 때
                if (_focusedObject != null)
                {
                    UnfocusObject();
                }
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
            _focusedObject = handle.gameObject;
            _focusedHandle = handle;
            
            // UI 생성
            if (recommendPopupAsset != null && _root != null)
            {
                _recommendPopup = recommendPopupAsset.Instantiate();
                _countLabel = _recommendPopup.Q<Label>("count-label");
                _root.Add(_recommendPopup);
                
                RefreshRecommendCount(handle);
            }

            // TODO: 아웃라인 효과 추가 (Material 변경 등)
            SetHighlight(handle.gameObject, true);
        }

        private void UnfocusObject()
        {
            if (_recommendPopup != null)
            {
                _recommendPopup.RemoveFromHierarchy();
                _recommendPopup = null;
            }

            if (_focusedObject != null)
            {
                SetHighlight(_focusedObject, false);
            }

            _focusedObject = null;
            _focusedHandle = null;
        }

        private void UpdateUIPosition()
        {
            if (_root == null || _root.panel == null || _mainCamera == null) return;

            // 오브젝트 머리 위에 UI 배치
            Vector3 worldPos = _focusedObject.transform.position + Vector3.up * 1.2f;
            Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, worldPos, _mainCamera);
            
            _recommendPopup.style.left = panelPos.x - 90; // width/2
            _recommendPopup.style.top = panelPos.y - 60;  // height/2
            
            // 카메라 뒤 체크
            Vector3 screenPos = _mainCamera.WorldToViewportPoint(worldPos);
            _recommendPopup.style.display = (screenPos.z > 0) ? DisplayStyle.Flex : DisplayStyle.None;
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
