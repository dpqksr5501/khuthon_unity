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

        [Header("UI Toolkit 설정")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset recommendPopupAsset;
        [SerializeField] private AudioClip recommendSFX;
        
        private AudioSource _audioSource;
        private VisualElement _root;
        private VisualElement _recommendPopup;
        private Label _yearLabel;
        private Label _titleLabel;
        private Label _descriptionLabel;
        private Label _countLabel;
        
        private GameObject _focusedObject;
        private PlacedObjectHandle _focusedHandle;
        private Camera _mainCamera;

        // 아웃라인 프로퍼티 캐싱
        private static readonly int ShowOutlineId = Shader.PropertyToID("_ShowOutline");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

        private void Awake()
        {
            _mainCamera = Camera.main;
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            
            if (uiDocument != null)
            {
                // 다른 UI보다 앞에 보이도록 정렬 순서 상향
                uiDocument.sortingOrder = 100;
                _root = uiDocument.rootVisualElement;
                
                // 루트가 화면을 꽉 채우도록 설정 (절대 좌표 HUD를 위해 필수)
                _root.style.flexGrow = 1;
                _root.style.width = Length.Percent(100);
                _root.style.height = Length.Percent(100);
                
                _root.Clear();
            }
        }

        private void Update()
        {
            HandleDetection();
            HandleInput();
            // HUD가 고정 위치이므로 UpdateUIPosition() 호출 제거
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

            // 현재 UI 포커스가 입력창에 있다면 G 키 입력 무시
            var focusedElement = _root?.panel?.focusController?.focusedElement;
            if (focusedElement != null && (focusedElement is TextField || focusedElement.GetType().Name.Contains("TextInput"))) return;

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
                // 팝업 컨테이너가 전체 화면을 차지하도록 설정 (우측 하단 배치를 위해 필수)
                _recommendPopup.style.flexGrow = 1;
                _recommendPopup.style.width = Length.Percent(100);
                _recommendPopup.style.height = Length.Percent(100);
                _recommendPopup.style.position = Position.Absolute;

                _yearLabel = _recommendPopup.Q<Label>("year-label");
                _titleLabel = _recommendPopup.Q<Label>("title-label");
                _descriptionLabel = _recommendPopup.Q<Label>("description-label");
                _countLabel = _recommendPopup.Q<Label>("count-label");
                _root.Add(_recommendPopup);
                
                RefreshRecommendCount(handle);
                SetHighlight(handle.gameObject, true);
                
                // BGM 재생 시작
                handle.PlayBGM();
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
                // BGM 정지
                if (_focusedHandle != null) _focusedHandle.StopBGM();
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
            // 먼저 인스펙터에 설정된 로컬 정보로 표시
            if (_yearLabel != null) _yearLabel.text = $"{handle.Period}년";
            if (_titleLabel != null) _titleLabel.text = $"{{ {handle.ObjectName} }}";
            if (_descriptionLabel != null) _descriptionLabel.text = handle.Description;
            if (_countLabel != null) _countLabel.text = "공감수 | 0";

            if (string.IsNullOrEmpty(handle.FirebaseKey)) return;

            string path = $"placements/{handle.UserId}/{handle.FirebaseKey}";
            FirebaseManager.Instance.ReadObject<PlacedObjectRecord>(path, (record, ok) => {
                if (ok && record != null)
                {
                    if (_yearLabel != null) _yearLabel.text = $"{record.period}년";
                    if (_titleLabel != null) _titleLabel.text = $"{{ {record.objectName} }}";
                    if (_descriptionLabel != null) _descriptionLabel.text = record.description;
                    if (_countLabel != null) _countLabel.text = $"공감수 | {record.recommendCount}";
                    handle.UpdateScale(record.recommendCount);
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
                            if (_countLabel != null) _countLabel.text = $"공감수 | {record.recommendCount}";
                            handle.UpdateScale(record.recommendCount); // 크기 즉시 반영

                            // 공감 완료 효과음 재생
                            if (recommendSFX != null && _audioSource != null)
                            {
                                _audioSource.PlayOneShot(recommendSFX);
                            }
                        }
                    });
                }
            });
        }

        private void SetHighlight(GameObject obj, bool highlight)
        {
            if (obj == null) return;

            // 쉐이더의 _ShowOutline 토글 (1 = 켬, 0 = 끔)
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.materials)
                {
                    if (mat.HasProperty(ShowOutlineId))
                    {
                        mat.SetFloat(ShowOutlineId, highlight ? 1f : 0f);
                        if (highlight)
                        {
                            mat.SetColor(OutlineColorId, new Color(0.4f, 0.4f, 1f, 1f)); // Indigo 연보라빛
                        }
                    }
                }
            }
        }
    }
}
