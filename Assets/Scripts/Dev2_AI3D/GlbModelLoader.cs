using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

// GLTFast 패키지가 설치되어 있어야 합니다:
// Unity Package Manager → "Add package by name" → com.unity.cloud.gltfast
using GLTFast;

namespace Khuthon.AI3D
{
    /// <summary>
    /// GLB URL 또는 로컬 경로에서 3D 모델을 런타임에 로드합니다.
    /// GLTFast(com.unity.cloud.gltfast) 패키지를 사용합니다.
    /// </summary>
    public class GlbModelLoader : MonoBehaviour
    {
        [Header("로드 설정")]
        [Tooltip("생성된 모델의 부모 Transform (null이면 씬 루트)")]
        [SerializeField] private Transform modelParent;
        [Tooltip("로드 후 적용할 초기 스케일")]
        [SerializeField] private Vector3 initialScale = Vector3.one;
        [Tooltip("로드 중 표시할 로딩 오브젝트 (스피너 등)")]
        [SerializeField] private GameObject loadingIndicator;

        public event Action<GameObject> OnModelLoaded;
        public event Action<string> OnLoadError;

        private GameObject _currentModel;

        public static GlbModelLoader Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>
        /// HTTP URL에서 GLB를 다운로드하고 씬에 배치합니다.
        /// </summary>
        public void LoadFromUrl(string glbUrl, Vector3? spawnPosition = null)
        {
            StartCoroutine(DownloadAndLoadCoroutine(glbUrl, spawnPosition));
        }

        /// <summary>
        /// 로컬 파일 경로에서 GLB를 로드합니다. (file:// prefix 자동 처리)
        /// </summary>
        public void LoadFromLocalPath(string filePath, Vector3? spawnPosition = null)
        {
            string url = filePath.StartsWith("file://") ? filePath : "file://" + filePath;
            StartCoroutine(DownloadAndLoadCoroutine(url, spawnPosition));
        }

        /// <summary>
        /// 현재 로드된 모델을 제거합니다.
        /// </summary>
        public void UnloadCurrentModel()
        {
            if (_currentModel != null)
            {
                Destroy(_currentModel);
                _currentModel = null;
            }
        }

        private IEnumerator DownloadAndLoadCoroutine(string url, Vector3? spawnPos)
        {
            SetLoading(true);
            Debug.Log($"[GlbModelLoader] 다운로드 시작: {url}");

            // GLB 바이트 다운로드
            byte[] glbBytes = null;
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    string err = $"GLB 다운로드 실패: {req.error}";
                    Debug.LogError($"[GlbModelLoader] {err}");
                    OnLoadError?.Invoke(err);
                    SetLoading(false);
                    yield break;
                }
                glbBytes = req.downloadHandler.data;
                Debug.Log($"[GlbModelLoader] 다운로드 완료: {glbBytes.Length} bytes");
            }

            // GLTFast로 파싱 및 인스턴스화
            yield return StartCoroutine(InstantiateGlb(glbBytes, spawnPos));
            SetLoading(false);
        }

        private IEnumerator InstantiateGlb(byte[] glbBytes, Vector3? spawnPos)
        {
            var gltf = new GltfImport();
            bool success = false;

            // 비동기 로드 (async/await → coroutine 래핑)
            var loadTask = gltf.LoadGltfBinary(glbBytes);
            while (!loadTask.IsCompleted) yield return null;
            success = loadTask.Result;

            if (!success)
            {
                string err = "GLTFast: GLB 파싱 실패";
                Debug.LogError($"[GlbModelLoader] {err}");
                OnLoadError?.Invoke(err);
                yield break;
            }

            // 기존 모델 제거
            UnloadCurrentModel();

            // 새 GameObject 생성 후 GLTFast 인스턴스화
            _currentModel = new GameObject("GeneratedModel");
            if (modelParent != null) _currentModel.transform.SetParent(modelParent);
            _currentModel.transform.localScale = initialScale;
            if (spawnPos.HasValue) _currentModel.transform.position = spawnPos.Value;

            var instantiateTask = gltf.InstantiateMainSceneAsync(_currentModel.transform);
            while (!instantiateTask.IsCompleted) yield return null;

            if (instantiateTask.Result)
            {
                Debug.Log($"[GlbModelLoader] 모델 로드 성공: {_currentModel.name}");
                OnModelLoaded?.Invoke(_currentModel);
            }
            else
            {
                string err = "GLTFast: 인스턴스화 실패";
                Debug.LogError($"[GlbModelLoader] {err}");
                OnLoadError?.Invoke(err);
                Destroy(_currentModel);
                _currentModel = null;
            }
        }

        private void SetLoading(bool active)
        {
            if (loadingIndicator != null)
                loadingIndicator.SetActive(active);
        }
    }
}
