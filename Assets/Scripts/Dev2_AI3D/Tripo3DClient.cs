using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Khuthon.AI3D
{
    /// <summary>
    /// Tripo3D API v2 연동 스크립트.
    /// 이미지 URL(또는 Base64)을 입력으로 3D 모델(.glb) 생성을 요청하고,
    /// 완료된 GLB 다운로드 URL을 콜백으로 반환합니다.
    /// </summary>
    public class Tripo3DClient : MonoBehaviour
    {
        [Header("Tripo3D API")]
        [Tooltip("https://platform.tripo3d.ai 에서 발급한 API Key")]
        [SerializeField] private string apiKey = "YOUR_TRIPO3D_API_KEY";

        private const string API_BASE = "https://api.tripo3d.ai/v2/openapi";
        private const float POLL_INTERVAL = 3f;   // 태스크 상태 폴링 간격 (초)
        private const float TIMEOUT = 300f;        // 최대 대기 시간 (초)

        public static Tripo3DClient Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ─── 공개 API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 이미지 URL 리스트로 3D 모델 생성 요청
        /// </summary>
        public void GenerateFromImageUrl(string imageUrl, Action<string> onGlbUrl, Action<string> onError)
        {
            StartCoroutine(GenerateCoroutine(imageUrl, onGlbUrl, onError));
        }

        /// <summary>
        /// 텍스트 프롬프트로 3D 모델 생성 요청
        /// </summary>
        public void GenerateFromText(string prompt, Action<string> onGlbUrl, Action<string> onError)
        {
            StartCoroutine(GenerateTextCoroutine(prompt, onGlbUrl, onError));
        }

        // ─── 이미지 → 3D ──────────────────────────────────────────────────────────
        private IEnumerator GenerateCoroutine(string imageUrl, Action<string> onGlbUrl, Action<string> onError)
        {
            // Step 1: 이미지 URL 업로드하여 file_token 획득
            Debug.Log($"[Tripo3D] Uploading image: {imageUrl}");
            string fileToken = null;
            yield return StartCoroutine(UploadImageFromUrl(imageUrl, token => fileToken = token, onError));
            if (string.IsNullOrEmpty(fileToken)) yield break;

            // Step 2: 3D 생성 태스크 생성
            string taskJson = $"{{\"type\":\"image_to_model\",\"file\":{{\"type\":\"jpg\",\"file_token\":\"{fileToken}\"}}}}";
            yield return StartCoroutine(CreateAndPollTask(taskJson, onGlbUrl, onError));
        }

        private IEnumerator UploadImageFromUrl(string imageUrl, Action<string> onToken, Action<string> onError)
        {
            // 이미지 바이트 다운로드
            using (var imgReq = UnityWebRequest.Get(imageUrl))
            {
                yield return imgReq.SendWebRequest();
                if (imgReq.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"이미지 다운로드 실패: {imgReq.error}");
                    yield break;
                }

                byte[] imgBytes = imgReq.downloadHandler.data;
                // Multipart 업로드
                WWWForm form = new WWWForm();
                form.AddBinaryData("file", imgBytes, "image.jpg", "image/jpeg");

                using (var uploadReq = UnityWebRequest.Post($"{API_BASE}/upload", form))
                {
                    uploadReq.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                    yield return uploadReq.SendWebRequest();

                    if (uploadReq.result != UnityWebRequest.Result.Success)
                    {
                        onError?.Invoke($"업로드 실패: {uploadReq.error} | {uploadReq.downloadHandler.text}");
                        yield break;
                    }

                    var resp = JsonUtility.FromJson<UploadResponse>(uploadReq.downloadHandler.text);
                    onToken?.Invoke(resp?.data?.image_token);
                }
            }
        }

        // ─── 텍스트 → 3D ──────────────────────────────────────────────────────────
        private IEnumerator GenerateTextCoroutine(string prompt, Action<string> onGlbUrl, Action<string> onError)
        {
            string taskJson = $"{{\"type\":\"text_to_model\",\"prompt\":\"{EscapeJson(prompt)}\"}}";
            yield return StartCoroutine(CreateAndPollTask(taskJson, onGlbUrl, onError));
        }

        // ─── 공통: 태스크 생성 + 폴링 ────────────────────────────────────────────
        private IEnumerator CreateAndPollTask(string taskJson, Action<string> onGlbUrl, Action<string> onError)
        {
            // Task 생성
            string taskId = null;
            yield return StartCoroutine(PostTask(taskJson, id => taskId = id, onError));
            if (string.IsNullOrEmpty(taskId)) yield break;

            Debug.Log($"[Tripo3D] Task created: {taskId}");

            // 완료까지 폴링
            float elapsed = 0f;
            while (elapsed < TIMEOUT)
            {
                yield return new WaitForSeconds(POLL_INTERVAL);
                elapsed += POLL_INTERVAL;

                TaskStatusResponse status = null;
                yield return StartCoroutine(GetTaskStatus(taskId, s => status = s, onError));

                if (status == null) continue;

                Debug.Log($"[Tripo3D] Task {taskId} status: {status.data?.status}");

                switch (status.data?.status)
                {
                    case "success":
                        string glbUrl = status.data?.output?.model;
                        if (!string.IsNullOrEmpty(glbUrl))
                        {
                            Debug.Log($"[Tripo3D] GLB URL: {glbUrl}");
                            onGlbUrl?.Invoke(glbUrl);
                        }
                        else onError?.Invoke("GLB URL이 응답에 없습니다.");
                        yield break;

                    case "failed":
                    case "cancelled":
                        onError?.Invoke($"Task {status.data.status}: {taskId}");
                        yield break;
                }
                // "queued", "running" 등은 계속 폴링
            }
            onError?.Invoke($"Timeout: Task {taskId} did not complete in {TIMEOUT}s");
        }

        private IEnumerator PostTask(string json, Action<string> onId, Action<string> onError)
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            using (var req = new UnityWebRequest($"{API_BASE}/task", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Task 생성 실패: {req.error} | {req.downloadHandler.text}");
                    yield break;
                }
                var resp = JsonUtility.FromJson<CreateTaskResponse>(req.downloadHandler.text);
                onId?.Invoke(resp?.data?.task_id);
            }
        }

        private IEnumerator GetTaskStatus(string taskId, Action<TaskStatusResponse> onStatus, Action<string> onError)
        {
            using (var req = UnityWebRequest.Get($"{API_BASE}/task/{taskId}"))
            {
                req.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Tripo3D] 상태 조회 실패: {req.error}");
                    onStatus?.Invoke(null);
                    yield break;
                }
                onStatus?.Invoke(JsonUtility.FromJson<TaskStatusResponse>(req.downloadHandler.text));
            }
        }

        private static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

        // ─── JSON 응답 모델 ───────────────────────────────────────────────────────
        [Serializable] private class UploadResponse { public UploadData data; }
        [Serializable] private class UploadData { public string image_token; }

        [Serializable] private class CreateTaskResponse { public CreateTaskData data; }
        [Serializable] private class CreateTaskData { public string task_id; }

        [Serializable] private class TaskStatusResponse { public TaskData data; }
        [Serializable] private class TaskData
        {
            public string task_id;
            public string status;   // queued / running / success / failed / cancelled
            public TaskOutput output;
        }
        [Serializable] private class TaskOutput { public string model; }   // GLB URL
    }
}
