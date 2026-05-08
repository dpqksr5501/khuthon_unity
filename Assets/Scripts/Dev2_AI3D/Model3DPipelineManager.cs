using System;
using System.Collections;
using UnityEngine;
using Khuthon.AI3D;
using Khuthon.Backend;

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

        private void Awake()
        {
            // 자동 컴포넌트 연결
            if (tripo3DClient == null) tripo3DClient = FindObjectOfType<Tripo3DClient>();
            if (glbLoader == null) glbLoader = FindObjectOfType<GlbModelLoader>();
            if (localTripoSR == null) localTripoSR = FindObjectOfType<TripoSRForUnity>();

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
        public void RunPipeline(string imageUrl, Vector3? spawnPosition = null)
        {
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
                    localTripoSR.RunTripoSRWithTexture(savedTex);
                    // TripoSRForUnity는 작업 완료 후 자동으로 씬에 배치하도록 설정되어 있음
                    // 배치가 완료되면 이벤트를 발생시키기 위해 OnPythonProcessEnded 구독
                    TripoSRForUnity.OnPythonProcessEnded += OnLocalProcessEnded;
                }
                #else
                Debug.LogError("Local TripoSR only works in Unity Editor.");
                #endif
            }
        }

        private void OnLocalProcessEnded()
        {
            TripoSRForUnity.OnPythonProcessEnded -= OnLocalProcessEnded;
            Debug.Log("[Pipeline] 로컬 TripoSR 작업 완료");
            // TripoSRForUnity가 생성한 오브젝트를 찾아서 OnModelReady 호출해야 함
            // 하지만 TripoSRForUnity는 스스로 Instantiate 하므로, 
            // 여기서 별도의 작업을 하지 않아도 씬에는 나타남.
            // 필요하다면 가장 최근에 생성된 GeneratedModel을 찾아서 보낼 수 있음.
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
