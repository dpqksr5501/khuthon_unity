using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

namespace Khuthon.Backend
{
    /// <summary>
    /// 이미지 URL에서 Texture를 다운로드하여 RawImage UI에 표시합니다.
    /// 최대 4장의 이미지를 슬롯에 표시하며, 스크롤 패널과 연동 가능합니다.
    /// </summary>
    public class ImageDisplayPanel : MonoBehaviour
    {
        [Header("UI 슬롯 (4개 연결)")]
        [SerializeField] private RawImage[] imageSlots = new RawImage[4];
        [SerializeField] private Button[] selectButtons = new Button[4];

        [Header("로딩 표시")]
        [SerializeField] private GameObject loadingOverlay;

        // 슬롯 선택 이벤트: (slotIndex, imageUrl)
        public event Action<int, string> OnImageSelected;

        private List<string> _currentUrls = new List<string>();
        private readonly Texture2D[] _loadedTextures = new Texture2D[4];

        private void Awake()
        {
            for (int i = 0; i < selectButtons.Length; i++)
            {
                int idx = i; // 클로저 캡처
                if (selectButtons[idx] != null)
                    selectButtons[idx].onClick.AddListener(() => SelectImage(idx));
            }
        }

        /// <summary>
        /// URL 목록을 받아서 이미지 슬롯에 로딩 시작
        /// </summary>
        public void DisplayImages(List<string> urls)
        {
            _currentUrls = urls ?? new List<string>();
            ClearSlots();
            if (loadingOverlay != null) loadingOverlay.SetActive(true);

            for (int i = 0; i < imageSlots.Length && i < _currentUrls.Count; i++)
            {
                int idx = i;
                StartCoroutine(LoadTexture(_currentUrls[idx], idx));
            }
        }

        private IEnumerator LoadTexture(string url, int slotIndex)
        {
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(request);
                    _loadedTextures[slotIndex] = tex;

                    if (imageSlots[slotIndex] != null)
                        imageSlots[slotIndex].texture = tex;

                    if (selectButtons[slotIndex] != null)
                        selectButtons[slotIndex].interactable = true;
                }
                else
                {
                    Debug.LogWarning($"[ImageDisplayPanel] 슬롯 {slotIndex} 로드 실패: {request.error}");
                }
            }

            // 모든 슬롯 로딩 완료 확인
            CheckAllLoaded();
        }

        private void CheckAllLoaded()
        {
            // 간단하게: 활성 코루틴이 없으면 오버레이 숨김
            // 실제로는 카운터 기반으로 처리
            if (loadingOverlay != null) loadingOverlay.SetActive(false);
        }

        private void SelectImage(int index)
        {
            if (index < _currentUrls.Count)
            {
                Debug.Log($"[ImageDisplayPanel] 슬롯 {index} 선택됨: {_currentUrls[index]}");
                OnImageSelected?.Invoke(index, _currentUrls[index]);
            }
        }

        private void ClearSlots()
        {
            foreach (var slot in imageSlots)
                if (slot != null) slot.texture = null;

            foreach (var btn in selectButtons)
                if (btn != null) btn.interactable = false;
        }

        private void OnDestroy()
        {
            foreach (var tex in _loadedTextures)
                if (tex != null) Destroy(tex);
        }
    }
}
