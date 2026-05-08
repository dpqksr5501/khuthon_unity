using UnityEngine;
using UnityEngine.UIElements;
using Khuthon.Backend;
using Khuthon.InGame;
using System.Collections.Generic;

namespace Khuthon
{
    /// <summary>
    /// UI Toolkit(UXML) 기반 오브젝트 상호작용 매니저.
    /// </summary>
    public class ObjectInteractionManager : MonoBehaviour
    {
        [Header("상호작용 설정")]
        [SerializeField] private LayerMask objectLayer;
        [SerializeField] private float interactDistance = 10f;

        [Header("UI Toolkit 설정")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset recommendPopupAsset;
        
        private VisualElement _root;
        private VisualElement _recommendPopup;
        private Button _recommendButton;
        
        private GameObject _selectedObject;
        private Camera _mainCamera;

        private void Awake()
        {
            _mainCamera = Camera.main;
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            
            if (uiDocument != null)
            {
                _root = uiDocument.rootVisualElement;
            }
        }

        private void Update()
        {
            // 좌클릭 감지
            if (Input.GetMouseButtonDown(0))
            {
                HandleClick();
            }

            // UI 위치 업데이트 (오브젝트 따라다니기)
            if (_recommendPopup != null && _selectedObject != null)
            {
                UpdateUIPosition();
            }
        }

        private void HandleClick()
        {
            // UI를 클릭한 경우 무시 (UI Toolkit 요소 위에 있는지 확인)
            if (_root != null && _root.panel != null)
            {
                // UI Toolkit 좌표계는 Y축이 반대이므로 변환 필요할 수 있으나 Pick(Input.mousePosition)은 내부적으로 처리함
                var picked = _root.panel.Pick(Input.mousePosition);
                if (picked != null && picked != _root) return;
            }

            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, objectLayer))
            {
                var handle = hit.collider.GetComponentInParent<PlacedObjectHandle>();
                if (handle != null)
                {
                    SelectObject(handle.gameObject);
                }
                else
                {
                    DeselectObject();
                }
            }
            else
            {
                DeselectObject();
            }
        }

        private void SelectObject(GameObject obj)
        {
            if (_selectedObject == obj) return;
            
            DeselectObject();
            _selectedObject = obj;
            
            // 추천 버튼 UI 생성 (UXML 인스턴스화)
            if (recommendPopupAsset != null && _root != null)
            {
                _recommendPopup = recommendPopupAsset.Instantiate();
                _recommendButton = _recommendPopup.Q<Button>("recommend-button");
                
                var handle = obj.GetComponent<PlacedObjectHandle>();
                if (_recommendButton != null && handle != null)
                {
                    _recommendButton.clicked += () => RecommendObject(handle);
                }
                
                _root.Add(_recommendPopup);
            }

            Debug.Log($"[Interaction] 오브젝트 선택됨: {obj.name}");
        }

        private void DeselectObject()
        {
            if (_recommendPopup != null)
            {
                _recommendPopup.RemoveFromHierarchy();
                _recommendPopup = null;
            }
            _selectedObject = null;
        }

        private void UpdateUIPosition()
        {
            // 오브젝트의 월드 좌표를 패널(스크린) 좌표로 변환
            Vector3 worldPos = _selectedObject.transform.position + Vector3.up * 1.5f;
            Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, worldPos, _mainCamera);
            
            // UI 위치 설정 (중앙 정렬을 위해 너비/높이의 절반만큼 보정 가능)
            _recommendPopup.style.left = panelPos.x - 60; // width/2
            _recommendPopup.style.top = panelPos.y - 20;  // height/2
            
            // 카메라 뒤에 있는 경우 숨김 처리
            Vector3 screenPos = _mainCamera.WorldToViewportPoint(worldPos);
            _recommendPopup.style.display = (screenPos.z > 0) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RecommendObject(PlacedObjectHandle handle)
        {
            if (string.IsNullOrEmpty(handle.FirebaseKey))
            {
                Debug.LogWarning("[Interaction] Firebase Key가 없어 추천할 수 없습니다.");
                return;
            }

            string path = $"placements/{handle.UserId}/{handle.FirebaseKey}";
            FirebaseManager.Instance.ReadObject<PlacedObjectRecord>(path, (record, ok) => {
                if (ok)
                {
                    record.recommendCount++;
                    FirebaseManager.Instance.WriteObject(path, record, (success) => {
                        if (success) Debug.Log($"[Interaction] 추천 완료! 현재 추천 수: {record.recommendCount}");
                    });
                }
            });
        }
    }
}
