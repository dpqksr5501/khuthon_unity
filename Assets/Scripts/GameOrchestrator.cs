using System.Collections.Generic;
using UnityEngine;
using Khuthon.Backend;
using Khuthon.AI3D;
using Khuthon.InGame;

namespace Khuthon
{
    /// <summary>
    /// 전체 게임 플로우 오케스트레이터.
    /// Dev1 → Dev2 → Dev3 파이프라인을 연결합니다.
    ///
    /// 플로우:
    /// [T키] SearchInputUI 열림
    ///   → 검색어 입력 → GoogleImageSearcher.SearchImages()
    ///   → 이미지 4장 URL 반환 → ImageSelectionUI.Show()
    ///   → 이미지 선택 확인 → Model3DPipelineManager.RunPipeline()
    ///   → GLB 로드 완료 → HousingPlacementSystem.StartPlacement()
    ///   → 좌클릭으로 배치 → Firebase 저장
    /// </summary>
    public class GameOrchestrator : MonoBehaviour
    {
        [Header("Dev1 - 백엔드")]
        [SerializeField] private GoogleImageSearcher googleSearcher;
        [SerializeField] private FirebaseManager firebaseManager;

        [Header("Dev2 - AI/3D")]
        [SerializeField] private Model3DPipelineManager pipelineManager;

        [Header("Dev3 - 인게임")]
        [SerializeField] private SearchInputUI searchInputUI;
        [SerializeField] private ImageSelectionUI imageSelectionUI;
        [SerializeField] private HousingPlacementSystem housingSystem;
        [SerializeField] private PlayerController playerController;

        [Header("상태 표시 UI (옵션)")]
        [SerializeField] private UnityEngine.UI.Text statusText;

        private string _lastPeriod;

        private void Awake()
        {
            // 자동 연결 (씬에 하나씩만 있는 경우)
            if (googleSearcher == null) googleSearcher = FindObjectOfType<GoogleImageSearcher>();
            if (pipelineManager == null) pipelineManager = FindObjectOfType<Model3DPipelineManager>();
            if (searchInputUI == null) searchInputUI = FindObjectOfType<SearchInputUI>();
            if (imageSelectionUI == null) imageSelectionUI = FindObjectOfType<ImageSelectionUI>();
            if (housingSystem == null) housingSystem = FindObjectOfType<HousingPlacementSystem>();
            if (playerController == null) playerController = FindObjectOfType<PlayerController>();
        }

        private void Start()
        {
            // ── 이벤트 연결 ──────────────────────────────────────────────────────

            // 1) 검색어 입력 완료 → 이미지 검색
            if (searchInputUI != null)
                searchInputUI.OnSearchSubmit += OnSearchSubmitted;

            // 2) 이미지 검색 완료 → 선택 UI 표시
            if (googleSearcher != null)
            {
                googleSearcher.OnImageUrlsFetched += OnImagesFetched;
                googleSearcher.OnSearchError += err => SetStatus($"검색 오류: {err}");
            }

            // 3) 이미지 선택 확인 → 3D 생성 파이프라인
            if (imageSelectionUI != null)
            {
                imageSelectionUI.OnImageConfirmed += OnImageConfirmed;
                imageSelectionUI.OnCancelled += () => SetStatus("선택 취소됨");
            }

            // 4) 3D 모델 준비 → 배치 모드
            if (pipelineManager != null)
            {
                pipelineManager.OnModelReady += OnModelReady;
                pipelineManager.OnPipelineError += err => SetStatus($"3D 생성 오류: {err}");
            }

            // 5) 배치 완료
            if (housingSystem != null)
                housingSystem.OnObjectPlaced += (obj, pos) =>
                    SetStatus($"'{obj.name}' 배치 완료: {pos}");

            SetStatus("T 키를 눌러 검색 시작");
        }

        // ─── 이벤트 핸들러 ────────────────────────────────────────────────────────

        private void OnSearchSubmitted(string query, string year, string category)
        {
            _lastPeriod = $"{year}";
            // 예: "2024 유행한 인물 차은우"
            string fullQuery = $"{year} 유행한 {category} {query}";
            SetStatus($"'{fullQuery}' 검색 중...");
            googleSearcher?.SearchImages(fullQuery);
        }

        private void OnImagesFetched(List<string> urls)
        {
            SetStatus($"이미지 {urls.Count}개 검색 완료. 선택하세요.");
            imageSelectionUI?.Show(urls);
        }

        private void OnImageConfirmed(int index, string imageUrl)
        {
            SetStatus("3D 모델 생성 중... (최대 3분 소요)");
            if (playerController != null) playerController.MovementLocked = true;
            pipelineManager?.RunPipeline(imageUrl);
        }

        private void OnModelReady(GameObject model)
        {
            SetStatus("3D 모델 로드 완료! 클릭으로 배치하세요. (우클릭/ESC = 취소)");
            if (playerController != null) playerController.MovementLocked = false;
            housingSystem?.StartPlacement(model, "", _lastPeriod);
        }

        private void SetStatus(string message)
        {
            Debug.Log($"[GameOrchestrator] {message}");
            if (statusText != null) statusText.text = message;
        }
    }
}
