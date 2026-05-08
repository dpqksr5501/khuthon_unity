using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Khuthon.Backend
{
    /// <summary>
    /// Firebase Realtime Database REST API를 사용한 간단한 CRUD 래퍼.
    /// Firebase SDK 없이 UnityWebRequest로 동작합니다.
    /// Inspector에서 DatabaseUrl을 설정하세요.
    /// (예: https://YOUR-PROJECT-default-rtdb.firebaseio.com)
    /// </summary>
    public class FirebaseManager : MonoBehaviour
    {
        [Header("Firebase 설정")]
        [Tooltip("Firebase 콘솔 → Realtime Database → 데이터 탭의 URL")]
        [SerializeField] private string databaseUrl = "https://YOUR-PROJECT-default-rtdb.firebaseio.com";
        [Tooltip("Firebase Auth 토큰 (보안 규칙이 있을 경우)")]
        [SerializeField] private string authToken = "";

        public static FirebaseManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ─── 쓰기 (PUT = 덮어쓰기) ────────────────────────────────────────────────
        /// <summary>
        /// path에 JSON 문자열을 PUT 합니다. (경로 없으면 새로 생성)
        /// </summary>
        public void WriteData(string path, string jsonData, Action<bool> onComplete = null)
        {
            StartCoroutine(PutCoroutine(path, jsonData, onComplete));
        }

        /// <summary>
        /// 오브젝트를 자동으로 JSON 직렬화하여 씁니다.
        /// </summary>
        public void WriteObject<T>(string path, T data, Action<bool> onComplete = null)
        {
            string json = JsonUtility.ToJson(data);
            WriteData(path, json, onComplete);
        }

        // ─── 추가 (POST = 자동 key 생성) ─────────────────────────────────────────
        public void PushData(string path, string jsonData, Action<string, bool> onComplete = null)
        {
            StartCoroutine(PostCoroutine(path, jsonData, onComplete));
        }

        // ─── 읽기 (GET) ───────────────────────────────────────────────────────────
        public void ReadData(string path, Action<string, bool> onComplete)
        {
            StartCoroutine(GetCoroutine(path, onComplete));
        }

        public void ReadObject<T>(string path, Action<T, bool> onComplete)
        {
            ReadData(path, (json, ok) =>
            {
                if (ok && !string.IsNullOrEmpty(json) && json != "null")
                {
                    try { onComplete?.Invoke(JsonUtility.FromJson<T>(json), true); }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[FirebaseManager] 역직렬화 실패: {ex.Message}");
                        onComplete?.Invoke(default, false);
                    }
                }
                else onComplete?.Invoke(default, false);
            });
        }

        /// <summary>
        /// 여러 개의 오브젝트 딕셔너리(JSON Object)를 읽어옵니다.
        /// </summary>
        public void ReadDictionary(string path, Action<string, bool> onComplete)
        {
            ReadData(path, onComplete);
        }

        // ─── 삭제 (DELETE) ────────────────────────────────────────────────────────
        public void DeleteData(string path, Action<bool> onComplete = null)
        {
            StartCoroutine(DeleteCoroutine(path, onComplete));
        }

        // ─── 내부 코루틴 ─────────────────────────────────────────────────────────
        private string BuildUrl(string path)
        {
            string authSuffix = string.IsNullOrEmpty(authToken) ? "" : $"?auth={authToken}";
            return $"{databaseUrl.TrimEnd('/')}/{path.TrimStart('/')}.json{authSuffix}";
        }

        private IEnumerator PutCoroutine(string path, string json, Action<bool> cb)
        {
            string url = BuildUrl(path);
            byte[] body = Encoding.UTF8.GetBytes(json);
            using (var req = new UnityWebRequest(url, "PUT"))
            {
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();
                bool ok = req.result == UnityWebRequest.Result.Success;
                if (!ok) Debug.LogError($"[Firebase PUT] {req.error} | {req.downloadHandler.text}");
                cb?.Invoke(ok);
            }
        }

        private IEnumerator PostCoroutine(string path, string json, Action<string, bool> cb)
        {
            string url = BuildUrl(path);
            byte[] body = Encoding.UTF8.GetBytes(json);
            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                yield return req.SendWebRequest();
                bool ok = req.result == UnityWebRequest.Result.Success;
                string key = "";
                if (ok)
                {
                    // Firebase POST 응답: {"name":"-NxxXXX"}
                    var resp = JsonUtility.FromJson<PostResponse>(req.downloadHandler.text);
                    key = resp?.name ?? "";
                }
                else Debug.LogError($"[Firebase POST] {req.error}");
                cb?.Invoke(key, ok);
            }
        }

        private IEnumerator GetCoroutine(string path, Action<string, bool> cb)
        {
            string url = BuildUrl(path);
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                bool ok = req.result == UnityWebRequest.Result.Success;
                if (!ok) Debug.LogError($"[Firebase GET] {req.error}");
                cb?.Invoke(ok ? req.downloadHandler.text : null, ok);
            }
        }

        private IEnumerator DeleteCoroutine(string path, Action<bool> cb)
        {
            string url = BuildUrl(path);
            using (var req = UnityWebRequest.Delete(url))
            {
                yield return req.SendWebRequest();
                bool ok = req.result == UnityWebRequest.Result.Success;
                if (!ok) Debug.LogError($"[Firebase DELETE] {req.error}");
                cb?.Invoke(ok);
            }
        }

        [Serializable] private class PostResponse { public string name; }
    }

    // ─── 게임 데이터 예시 모델 ────────────────────────────────────────────────────
    [Serializable]
    public class PlacedObjectRecord
    {
        public string firebaseKey; // Firebase에서 생성된 키 저장용
        public string userId;
        public string period; // 예: "2024_1분기"
        public string sceneName; // 현재 씬 이름 추가
        public string objectName;
        public string description; // 작품 설명 추가
        public string bgmPath;     // BGM 파일 경로 추가
        public string modelUrl;
        public float posX, posY, posZ;
        public int recommendCount; // 추천 수 추가
        public long timestamp;
    }
}
