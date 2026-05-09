using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Khuthon.InGame
{
    /// <summary>
    /// UI Toolkit(UXML) 기반 검색 입력 UI.
    /// </summary>
    public class SearchInputUI : MonoBehaviour
    {
        [Header("UI Toolkit 설정")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private PlayerController playerController;

        // UI 요소
        private VisualElement _root;
        private DropdownField _yearDropdown;
        private DropdownField _categoryDropdown;
        private TextField _searchField;
        private Button _submitButton;
        private Button _cancelButton;

        public event Action<string, string, string> OnSearchSubmit;
        public event Action OnCancel;
        public bool IsOpen { get { return _root != null && _root.style.display == DisplayStyle.Flex; } }

        private void Awake()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();

            if (uiDocument == null || uiDocument.visualTreeAsset == null)
            {
                Debug.LogWarning("[SearchInputUI] UIDocument 또는 VisualTreeAsset이 누락되었습니다.");
                return;
            }

            _root = uiDocument.rootVisualElement;
            if (_root == null) return;
            
            // 요소 쿼리
            _yearDropdown = _root.Q<DropdownField>("year-dropdown");
            _categoryDropdown = _root.Q<DropdownField>("category-dropdown");
            _searchField = _root.Q<TextField>("search-field");
            _submitButton = _root.Q<Button>("submit-button");
            _cancelButton = _root.Q<Button>("cancel-button");

            // 드롭다운 옵션 초기화
            if (_yearDropdown != null)
            {
                var years = new List<string>();
                for (int i = 2026; i >= 2000; i--) years.Add(i.ToString());
                _yearDropdown.choices = years;
                _yearDropdown.value = "2024";
            }


            if (_categoryDropdown != null)
            {
                _categoryDropdown.choices = new List<string> { "인물", "물건", "밈", "음식", "사건" };
                _categoryDropdown.value = "물건";
            }

            // 이벤트 바인딩
            if (_submitButton != null) _submitButton.clicked += Submit;
            if (_cancelButton != null) _cancelButton.clicked += () => {
                OnCancel?.Invoke();
                Close();
            };

            SetPanelVisible(false);
        }

        private void Update()
        {
            // 현재 화면에 어떤 입력창(TextField)이라도 포커스가 가 있는지 확인 (Global Focus Check)
            var allDocs = FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude);
            foreach (var doc in allDocs)
            {
                var focused = doc.rootVisualElement?.panel?.focusController?.focusedElement;
                if (focused != null && (focused is TextField || focused.GetType().Name.Contains("TextInput"))) 
                    return; // 어떤 입력창이든 활성화되어 있다면 단축키 무시
            }

            // 새 Input System 방식 (Keyboard.current 사용)
            if (UnityEngine.InputSystem.Keyboard.current != null && 
                UnityEngine.InputSystem.Keyboard.current[UnityEngine.InputSystem.Key.T].wasPressedThisFrame)
            {
                Toggle();
            }
        }

        public void Open()
        {
            SetPanelVisible(true);
            
            // 1. 모든 게임 입력 시스템 정지
            var playerInput = FindAnyObjectByType<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null) playerInput.enabled = false;

            if (playerController != null)
                playerController.MovementLocked = true;

            // 2. StarterAssets 입력 초기화
            var starterInputs = FindAnyObjectByType<StarterAssets.StarterAssetsInputs>();
            if (starterInputs != null)
            {
                starterInputs.move = Vector2.zero;
                starterInputs.jump = false;
                starterInputs.sprint = false;
                starterInputs.cursorLocked = false;
                starterInputs.cursorInputForLook = false;
            }

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        public void Close()
        {
            SetPanelVisible(false);
            // 커서 및 입력 제어는 GameOrchestrator에서 일괄 관리하도록 위임합니다.
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        private void Submit()
        {
            string text = _searchField.value?.Trim() ?? "";
            if (string.IsNullOrEmpty(text)) return;

            string year = _yearDropdown.value;
            string category = _categoryDropdown.value;

            Debug.Log($"[SearchInputUI] UXML 제출: {year} {category} {text}");
            OnSearchSubmit?.Invoke(text, year, category);
            Close();
            _searchField.value = "";
        }

        private void SetPanelVisible(bool visible)
        {
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
