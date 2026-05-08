using System;
using System.Collections;
using UnityEngine;
using Khuthon.AI3D;
using Khuthon.Backend;
using System.IO;
using System.Collections.Generic;

namespace Khuthon.AI3D
{
    /// <summary>
    /// Dev2 전체 파이프라인 통합 매니저.
    /// 1) 이미지 URL 수신 → Tripo3D API 요청 → GLB 다운로드 → GLTFast 로드
    /// 2) 더미 이미지 URL로 R&D 테스트 가능
    /// </summary>
    public class Model3DPipelineManager : MonoBehaviour
    {
        [Header("의존 컴포넌트")]
        [SerializeField] private Tripo3DClient tripo3DClient;
        [SerializeField] private GlbModelLoader glbLoader;
        
        [Header("Local TripoSR (Alternative)")]
        [SerializeField] private bool useLocalTripoSR = true;
        [SerializeField] private TripoSRForUnity localTripoSR;
        [SerializeField] private string localInputSavePath = "Assets/TripoSR/input_runtime.png";

        [Header("R&D 테스트용 더미 데이터")]
        [SerializeField] private bool useDummyOnStart = false;
        [Tooltip("더미 이미지 URL (공개 JPEG 링크)")]
        [SerializeField] private string dummyImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/4/47/PNG_transparency_demonstration_1.png/280px-PNG_transparency_demonstration_1.png";
        [Tooltip("테스트용 더미 GLB URL (Khronos 샘플)")]
        [SerializeField] private string dummyGlbUrl = "https://github.com/KhronosGroup/glTF-Sample-Assets/raw/main/Models/Box/glTF-Binary/Box.glb";
        [SerializeField] private bool skipApiUseDummyGlb = false;

        public event Action<GameObject> OnModelReady;
        public event Action<string> OnPipelineError;
        
        private Vector3? _pendingSpawnPosition;
        private Action<GameObject> _onCurrentModelReady;
        private bool _isAutoPlacement;

        private void Awake()
        {
            // 자동 컴포넌트 연결
            if (tripo3DClient == null) tripo3DClient = FindAnyObjectByType<Tripo3DClient>();
            if (glbLoader == null) glbLoader = FindAnyObjectByType<GlbModelLoader>();
            if (localTripoSR == null) localTripoSR = FindAnyObjectByType<TripoSRForUnity>();

            if (glbLoader != null)
            {
                glbLoader.OnModelLoaded += model => OnModelReady?.Invoke(model);
                glbLoader.OnLoadError += err => OnPipelineError?.Invoke(err);
            }
        }

        private void Start()
        {
            if (useDummyOnStart)
                StartDummyTest();
        }

        /// <summary>
        /// 이미지 URL을 받아 전체 파이프라인 실행
        /// </summary>
        public void RunPipeline(string imageUrl, Vector3? spawnPosition = null, Action<GameObject> onModelReady = null, bool isAutoPlacement = false)
        {
            _onCurrentModelReady = onModelReady;
            _pendingSpawnPosition = spawnPosition;
            _isAutoPlacement = isAutoPlacement;

            // 0. 캐시 확인 (이미 생성된 파일이 있는지)
            string cachedAssetPath = GetCachedModelPath(imageUrl);
            if (System.IO.File.Exists(Path.Combine(Application.dataPath, cachedAssetPath.Substring("Assets/".Length))))
            {
                Debug.Log($"[Pipeline] 캐시된 모델 발견: {cachedAssetPath}");
                LoadLocalModel(cachedAssetPath);
                return;
            }

            if (skipApiUseDummyGlb)
            {
                Debug.Log("[Pipeline] API 스킵 → 더미 GLB 직접 로드");
                glbLoader?.LoadFromUrl(dummyGlbUrl, spawnPosition);
                return;
            }

            Debug.Log($"[Pipeline] 파이프라인 시작: {imageUrl}");

            if (useLocalTripoSR)
            {
                StartCoroutine(RunLocalPipeline(imageUrl, spawnPosition));
                return;
            }

            tripo3DClient?.GenerateFromImageUrl(imageUrl,
                glbUrl =>
                {
                    Debug.Log($"[Pipeline] GLB 생성 완료: {glbUrl}");
                    glbLoader?.LoadFromUrl(glbUrl, spawnPosition);
                },
                err =>
                {
                    Debug.LogError($"[Pipeline] Tripo3D 오류: {err}");
                    OnPipelineError?.Invoke(err);
                }
            );
        }

        /// <summary>
        /// 텍스트 프롬프트로 파이프라인 실행
        /// </summary>
        public void RunPipelineFromText(string prompt, Vector3? spawnPosition = null)
        {
            tripo3DClient?.GenerateFromText(prompt,
                glbUrl => glbLoader?.LoadFromUrl(glbUrl, spawnPosition),
                err => OnPipelineError?.Invoke(err)
            );
        }

        private IEnumerator RunLocalPipeline(string imageUrl, Vector3? spawnPosition)
        {
            Debug.Log("[Pipeline] 로컬 TripoSR 실행 중...");
            
            // 1. 이미지 다운로드
            using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(imageUrl))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    OnPipelineError?.Invoke($"이미지 다운로드 실패: {req.error}");
                    yield break;
                }

                Texture2D tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                
                // 2. 파일로 저장 (TripoSRForUnity는 AssetDatabase 기반이므로 파일이 필요함)
                byte[] bytes = tex.EncodeToPNG();
                System.IO.File.WriteAllBytes(localInputSavePath, bytes);
                
                #if UNITY_EDITOR
                // 임포터가 비결정적 생성을 피하도록 미리 머티리얼 에셋 생성
                string urpMaterialPath = "Assets/Materials/URPVertexColorMaterial.mat";
                if (UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(urpMaterialPath) == null)
                {
                    Shader urpShader = Shader.Find("Custom/URPVertexColor");
                    if (urpShader != null)
                    {
                        if (!System.IO.Directory.Exists("Assets/Materials")) System.IO.Directory.CreateDirectory("Assets/Materials");
                        Material newMat = new Material(urpShader);
                        UnityEditor.AssetDatabase.CreateAsset(newMat, urpMaterialPath);
                        UnityEditor.AssetDatabase.SaveAssets();
                    }
                }

                UnityEditor.AssetDatabase.Refresh();
                Texture2D savedTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(localInputSavePath);
                
                if (localTripoSR != null)
                {
                    // URL 해시를 파일명으로 지정하여 중복 생성 방지 및 캐싱 연동
                    localTripoSR.customOutputName = "Model_" + Mathf.Abs(imageUrl.GetHashCode()).ToString();
                    localTripoSR.RunTripoSRWithTexture(savedTex);
                    TripoSRForUnity.OnPythonProcessEnded += OnLocalProcessEnded;
                    // 생성된 오브젝트를 위치시키기 위해 구독
                    TripoSRForUnity.OnModelInstantiated += HandleModelInstantiated;
                    
                    // 현재 파이프라인에서 사용할 스폰 위치 저장
                    _pendingSpawnPosition = spawnPosition;
                }
                #else
                Debug.LogError("Local TripoSR only works in Unity Editor.");
                #endif
            }
        }

        private string GetCachedModelPath(string url)
        {
            // URL을 기반으로 고유한 파일명 생성 (MD5 등 사용 가능하지만 여기서는 간단히 GetHashCode)
            // 실제 프로젝트에서는 더 안전한 해시 함수 권장
            string hash = Mathf.Abs(url.GetHashCode()).ToString();
            return $"Assets/Models/Model_{hash}.obj";
        }

        private void LoadLocalModel(string assetPath)
        {
            #if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
            GameObject importedObj = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (importedObj != null)
            {
                GameObject instance = Instantiate(importedObj);
                instance.name = Path.GetFileNameWithoutExtension(assetPath);
                
                // 1. 회전 보정
                if (instance.transform.childCount > 0)
                {
                    Transform meshChild = instance.transform.GetChild(0);
                    meshChild.rotation = Quaternion.Euler(-90f, -90f, 0f);

                    // 2. 물리 구성 (InteractionManager가 인식할 수 있게 콜라이더 추가)
                    MeshCollider mc = meshChild.gameObject.AddComponent<MeshCollider>();
                    mc.convex = true;

                    Rigidbody rb = meshChild.gameObject.AddComponent<Rigidbody>();
                    rb.isKinematic = true; 
                    
                    // 3. 레이어 설정 (오브젝트 레이어로 설정되어야 인식됨)
                    // (가정: 6번 레이어가 오브젝트 레이어라면)
                    // instance.layer = 6; 
                }

                HandleModelInstantiated(instance);
            }
            else
            {
                Debug.LogError($"[Pipeline] 캐시 로드 실패: {assetPath}");
            }
            #endif
        }

        private void HandleModelInstantiated(GameObject model)
        {
            TripoSRForUnity.OnModelInstantiated -= HandleModelInstantiated;

            if (_pendingSpawnPosition.HasValue)
            {
                model.transform.position = _pendingSpawnPosition.Value;
                Debug.Log($"[Pipeline] 모델을 지정된 위치로 이동: {_pendingSpawnPosition.Value}");
            }
            
            // 콜백 실행
            _onCurrentModelReady?.Invoke(model);
            _onCurrentModelReady = null;

            // Housing 시스템 등이 감지할 수 있도록 전역 이벤트 발생
            // 자동 배치인 경우 GameOrchestrator가 StartPlacement를 호출하지 않도록 주의 필요
            if (!_isAutoPlacement)
                OnModelReady?.Invoke(model);
        }

        private void OnLocalProcessEnded()
        {
            TripoSRForUnity.OnPythonProcessEnded -= OnLocalProcessEnded;
            Debug.Log("[Pipeline] 로컬 TripoSR 작업 완료");
        }

        /// <summary>
        /// R&D 더미 테스트: 더미 GLB를 씬 원점에 즉시 로드
        /// </summary>
        [ContextMenu("Run Dummy Test")]
        public void StartDummyTest()
        {
            Debug.Log("[Pipeline] 더미 테스트 시작");
            if (skipApiUseDummyGlb)
                glbLoader?.LoadFromUrl(dummyGlbUrl, Vector3.zero);
            else
                RunPipeline(dummyImageUrl, Vector3.zero);
        }
    }
}
