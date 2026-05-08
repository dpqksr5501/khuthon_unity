using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Khuthon.Backend
{
    /// <summary>
    /// Serper.dev API를 통해 실제 구글 이미지 검색 결과를 가져옵니다.
    /// 발급: https://serper.dev/ (회원가입 시 2,500회 무료)
    /// </summary>
    public class GoogleImageSearcher : MonoBehaviour
    {
        [Header("Serper.dev API Settings")]
        [Tooltip("https://serper.dev/ 에서 발급한 API Key를 입력하세요.")]
        [SerializeField] private string apiKey = "YOUR_SERPER_API_KEY";
        [SerializeField] private int imageCount = 4;

        public event Action<List<string>> OnImageUrlsFetched;
        public event Action<string> OnSearchError;

        private const string SERPER_URL = "https://google.serper.dev/images";

        public void SearchImages(string query)
        {
            if (string.IsNullOrEmpty(query)) return;
            StartCoroutine(FetchImagesCoroutine(query));
        }

        private IEnumerator FetchImagesCoroutine(string query)
        {
            // Serper는 POST 방식을 사용하며 JSON 바디를 요구합니다.
            string jsonBody = "{\"q\":\"" + query + "\", \"num\":" + (imageCount * 2) + "}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            using (UnityWebRequest request = new UnityWebRequest(SERPER_URL, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("X-API-KEY", apiKey);

                Debug.Log($"[GoogleImageSearcher] Serper Searching: {query}");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string responseBody = request.downloadHandler != null ? request.downloadHandler.text : "No response body";
                    Debug.LogError($"[GoogleImageSearcher] Error {request.responseCode}: {request.error}\nDetail: {responseBody}");
                    OnSearchError?.Invoke($"검색 오류: {request.responseCode}");
                    yield break;
                }

                ParseSerperResponse(request.downloadHandler.text);
            }
        }

        private void ParseSerperResponse(string json)
        {
            try
            {
                SerperResponse response = JsonUtility.FromJson<SerperResponse>(json);
                List<string> urls = new List<string>();

                if (response?.images != null)
                {
                    foreach (var img in response.images)
                    {
                        if (!string.IsNullOrEmpty(img.imageUrl))
                        {
                            urls.Add(img.imageUrl);
                        }
                        if (urls.Count >= imageCount) break;
                    }
                }

                Debug.Log($"[GoogleImageSearcher] Serper: {urls.Count}개 이미지 URL 수신.");
                OnImageUrlsFetched?.Invoke(urls);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GoogleImageSearcher] Parse Error: {ex.Message}");
                OnSearchError?.Invoke("응답 해석 실패");
            }
        }

        // ─── Serper JSON 모델 ──────────────────────────────────────────────
        [Serializable]
        private class SerperResponse
        {
            public List<SerperImage> images;
        }

        [Serializable]
        private class SerperImage
        {
            public string title;
            public string imageUrl;
            public int imageWidth;
            public int imageHeight;
        }
    }
}
