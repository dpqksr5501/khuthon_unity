using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Khuthon.AI3D;
using Khuthon.Backend;

namespace Khuthon.InGame
{
    /// <summary>
    /// Raycast 기반 하우징 배치 시스템.
    /// </summary>
    public class HousingPlacementSystem : MonoBehaviour
    {
        [Header("Raycast 설정")]
        [SerializeField] private LayerMask placementLayerMask = ~0;
        [SerializeField] private LayerMask blockingLayerMask;
        [SerializeField] private float maxRayDistance = 50f;

        [Header("시각적 피드백")]
        [SerializeField] private Color validColor = new Color(0f, 1f, 0f, 0.5f);
        [SerializeField] private Color invalidColor = new Color(1, 0, 0, 0.5f);

        [Header("그리드 스냅 (옵션)")]
        [SerializeField] private bool useGridSnap = true;
        [SerializeField] private float gridSize = 1f;

        [Header("Firebase 저장")]
        [SerializeField] private bool saveToFirebase = true;
        [SerializeField] private string userId = "player_1";

        [Header("오디오 효과음")]
        [SerializeField] private AudioClip placementConfirmSFX;
        private AudioSource _audioSource;

        public event Action<GameObject, Vector3> OnObjectPlaced;
        public event Action OnPlacementCancelled;
        public event Action OnPlacementsLoaded;

        public bool IsPlacingObject { get; private set; }

        private GameObject _previewObject;
        private string _pendingModelUrl;
        private string _pendingTitle;
        private string _pendingDescription;
        private string _pendingBgmPath;
        private string _currentPeriod;
        private Camera _camera;

        private void Awake()
        {
            _camera = Camera.main;
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        }

        private void Update()
        {
            if (!IsPlacingObject || _previewObject == null) return;

            UpdatePreviewPosition();

            var mouse = UnityEngine.InputSystem.Mouse.current;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;

            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame && !IsPointerOverUI())
                ConfirmPlacement();

            if (mouse.rightButton.wasPressedThisFrame || 
                (keyboard != null && keyboard[UnityEngine.InputSystem.Key.Escape].wasPressedThisFrame))
                CancelPlacement();
        }

        public void StartPlacement(GameObject modelObject, string modelUrl = "", string period = "", string title = "", string desc = "", string bgm = "")
        {
            if (IsPlacingObject) CancelPlacement();

            _pendingModelUrl = modelUrl;
            _pendingTitle = title;
            _pendingDescription = desc;
            _pendingBgmPath = bgm;
            _currentPeriod = period;
            _previewObject = modelObject;

            ApplyPreviewMaterial(_previewObject, validColor);

            IsPlacingObject = true;

            var starterInputs = FindAnyObjectByType<StarterAssets.StarterAssetsInputs>();
            if (starterInputs != null)
            {
                starterInputs.cursorLocked = false;
                starterInputs.cursorInputForLook = false;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log($"[Housing] 배치 모드 시작 (Period: {period})");
        }

        private void EndPlacement()
        {
            IsPlacingObject = false;

            var starterInputs = FindAnyObjectByType<StarterAssets.StarterAssetsInputs>();
            if (starterInputs != null)
            {
                starterInputs.cursorLocked = true;
                starterInputs.cursorInputForLook = true;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void UpdatePreviewPosition()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;

            Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hitInfo, maxRayDistance, placementLayerMask))
            {
                Vector3 pos = hitInfo.point;
                if (useGridSnap) pos = SnapToGrid(pos);
                _previewObject.transform.position = pos;

                bool isBlocked = CheckOverlap(_previewObject);
                ApplyPreviewMaterial(_previewObject, isBlocked ? invalidColor : validColor);
            }
        }

        private void ConfirmPlacement()
        {
            if (_previewObject == null) return;

            Vector3 finalPos = _previewObject.transform.position;
            RestoreOriginalMaterial(_previewObject);

            Rigidbody rb = _previewObject.GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }

            var handle = _previewObject.AddComponent<PlacedObjectHandle>();
            handle.ModelUrl = _pendingModelUrl;
            handle.UserId = userId;
            handle.ObjectName = _pendingTitle;
            handle.Description = _pendingDescription;
            handle.BgmPath = _pendingBgmPath;

            if (saveToFirebase && FirebaseManager.Instance != null)
            {
                var record = new PlacedObjectRecord
                {
                    userId = userId,
                    period = _currentPeriod,
                    sceneName = SceneManager.GetActiveScene().name,
                    objectName = _pendingTitle,
                    description = _pendingDescription,
                    bgmPath = _pendingBgmPath,
                    modelUrl = _pendingModelUrl,
                    posX = finalPos.x,
                    posY = finalPos.y,
                    posZ = finalPos.z,
                    recommendCount = 0,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                FirebaseManager.Instance.PushData($"placements/{userId}", JsonUtility.ToJson(record),
                    (key, ok) => {
                        if (ok) {
                            handle.FirebaseKey = key;
                            Debug.Log($"[Housing] Firebase 저장 완료: {key}");
                        }
                    });
            }

            // 배치 완료 효과음 재생
            if (placementConfirmSFX != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(placementConfirmSFX);
            }

            OnObjectPlaced?.Invoke(_previewObject, finalPos);
            _previewObject = null;
            _pendingModelUrl = "";
            _currentPeriod = "";
            
            EndPlacement();
        }

        private void CancelPlacement()
        {
            if (_previewObject != null)
            {
                Destroy(_previewObject);
                _previewObject = null;
            }
            _pendingModelUrl = "";
            OnPlacementCancelled?.Invoke();
            EndPlacement();
        }

        private Vector3 SnapToGrid(Vector3 pos)
        {
            return new Vector3(Mathf.Round(pos.x / gridSize) * gridSize, pos.y, Mathf.Round(pos.z / gridSize) * gridSize);
        }

        private bool CheckOverlap(GameObject obj)
        {
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
                    mat.SetFloat("_Surface", 1f);
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
                    mat.SetFloat("_Surface", 0f);
                    mat.renderQueue = -1;
                }
            }
        }

        private bool IsPointerOverUI()
        {
            return UnityEngine.EventSystems.EventSystem.current != null &&
                   UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        }

        // ─── 로딩 로직 ─────────────────────────────────────────────────────────
        public void LoadAllPlacements(Model3DPipelineManager pipelineManager)
        {
            if (FirebaseManager.Instance == null) return;

            Debug.Log($"[Housing] {userId}의 배치 데이터 로딩 중...");
            FirebaseManager.Instance.ReadDictionary($"placements/{userId}", (json, ok) =>
            {
                if (ok && !string.IsNullOrEmpty(json) && json != "null" && json != "{}")
                {
                    var records = ParseFirebaseDictionary(json);
                    string currentScene = SceneManager.GetActiveScene().name;
                    foreach (var record in records)
                    {
                        if (string.IsNullOrEmpty(record.modelUrl))
                        {
                            Debug.LogWarning($"[Housing] URL이 없는 레코드 발견(구버전 데이터), 스킵합니다. (ID: {record.firebaseKey})");
                            continue;
                        }

                        // 씬 이름이 일치하는 경우에만 소환 (단, 기존 데이터 호환을 위해 sceneName이 비어있으면 일단 통과)
                        if (!string.IsNullOrEmpty(record.sceneName) && record.sceneName != currentScene)
                        {
                            continue;
                        }

                        StartCoroutine(SpawnSavedObject(record, pipelineManager));
                    }
                    OnPlacementsLoaded?.Invoke();
                }
            });
        }

        private IEnumerator SpawnSavedObject(PlacedObjectRecord record, Model3DPipelineManager pipelineManager)
        {
            Vector3 pos = new Vector3(record.posX, record.posY, record.posZ);
            
            // 모델 생성 요청 시 콜백 등록 (isAutoPlacement = true로 설정하여 다시 배치 모드가 뜨지 않게 함)
            pipelineManager.RunPipeline(record.modelUrl, pos, (model) => {
                if (model != null)
                {
                    var handle = model.GetComponent<PlacedObjectHandle>();
                    if (handle == null) handle = model.AddComponent<PlacedObjectHandle>();
                    
                    handle.FirebaseKey = record.firebaseKey;
                    handle.UserId = record.userId;
                    handle.ModelUrl = record.modelUrl;
                    
                    // 불러올 때 즉시 추천수에 맞춰 크기 조정
                    handle.UpdateScale(record.recommendCount);
                    
                    Debug.Log($"[Housing] 불러온 오브젝트 설정 완료: {record.firebaseKey} (추천수: {record.recommendCount})");
                }
            }, true);
            
            yield return null;
        }

        private List<PlacedObjectRecord> ParseFirebaseDictionary(string json)
        {
            var list = new List<PlacedObjectRecord>();
            if (string.IsNullOrEmpty(json) || json == "null" || json == "{}") return list;

            try 
            {
                // 1. 전체 문자열에서 개별 레코드들( { "ID" : { ... } } )을 찾아내기 위해 
                // " : { " 패턴을 기준으로 분리 시도
                int searchIndex = 0;
                while (true)
                {
                    // "key":{ 를 찾음
                    int colonIndex = json.IndexOf(":{", searchIndex);
                    if (colonIndex == -1) break;

                    // 키 추출 (따옴표 사이의 값)
                    int keyEnd = json.LastIndexOf("\"", colonIndex - 1);
                    int keyStart = json.LastIndexOf("\"", keyEnd - 1) + 1;
                    string key = json.Substring(keyStart, keyEnd - keyStart);

                    // 값(객체) 추출: 중괄호 쌍 맞추기
                    int objectStart = colonIndex + 1;
                    int braceCount = 0;
                    int objectEnd = -1;

                    for (int i = objectStart; i < json.Length; i++)
                    {
                        if (json[i] == '{') braceCount++;
                        else if (json[i] == '}') braceCount--;

                        if (braceCount == 0)
                        {
                            objectEnd = i;
                            break;
                        }
                    }

                    if (objectEnd != -1)
                    {
                        string objectJson = json.Substring(objectStart, (objectEnd - objectStart) + 1);
                        var record = JsonUtility.FromJson<PlacedObjectRecord>(objectJson);
                        if (record != null)
                        {
                            record.firebaseKey = key;
                            list.Add(record);
                        }
                        searchIndex = objectEnd + 1;
                    }
                    else
                    {
                        break;
                    }
                }
            } 
            catch (Exception ex) 
            {
                Debug.LogError($"[Housing] 파싱 최종 실패: {ex.Message}");
            }
            return list;
        }
    }

    public class PlacedObjectHandle : MonoBehaviour
    {
        public string ModelUrl { get; set; }
        public string UserId { get; set; }
        public string FirebaseKey { get; set; }
        public string ObjectName { get; set; }
        public string Description { get; set; }
        public string BgmPath { get; set; }

        private AudioSource _audioSource;
        private AudioClip _cachedClip;

        private void Awake()
        {
            // 3D 사운드 설정을 위한 AudioSource 추가
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1.0f; // 100% 3D 사운드
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.minDistance = 1f;
            _audioSource.maxDistance = 15f; // 15미터 밖에서는 안 들림
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
        }

        public void UpdateScale(int count)
        {
            float scaleFactor = 1.0f + (count * 0.1f);
            transform.localScale = Vector3.one * scaleFactor;
            Debug.Log($"[Housing] 오브젝트 크기 업데이트: {scaleFactor}x (추천수: {count})");
        }

        public void PlayBGM()
        {
            if (string.IsNullOrEmpty(BgmPath)) return;

            if (_cachedClip != null)
            {
                _audioSource.clip = _cachedClip;
                _audioSource.Play();
            }
            else
            {
                StartCoroutine(LoadAndPlayAudio());
            }
        }

        public void StopBGM()
        {
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }

        private System.Collections.IEnumerator LoadAndPlayAudio()
        {
            // 로컬 파일 경로를 URI 형식으로 변환 (Windows 대응)
            string uri = "file://" + BgmPath.Replace("\\", "/");
            
            using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.MPEG))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    _cachedClip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
                    _audioSource.clip = _cachedClip;
                    _audioSource.Play();
                    Debug.Log($"[Audio] BGM 재생 시작: {BgmPath}");
                }
                else
                {
                    Debug.LogError($"[Audio] BGM 로드 실패: {www.error} (Path: {uri})");
                }
            }
        }
    }
}
