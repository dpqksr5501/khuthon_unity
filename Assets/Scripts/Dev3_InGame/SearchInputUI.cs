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
        [SerializeField] private KeyCode toggleKey = KeyCode.T;

        // UI 요소
        private VisualElement _root;
        private DropdownField _yearDropdown;
        private DropdownField _categoryDropdown;
        private TextField _searchField;
        private Button _submitButton;
        private Button _cancelButton;

        public event Action<string, string, string> OnSearchSubmit;
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (playerController == null) playerController = FindObjectOfType<PlayerController>();

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
                _yearDropdown.choices = new List<string> { "2020", "2021", "2022", "2023", "2024", "2025", "2026" };
                _yearDropdown.value = "2024";
            }


            if (_categoryDropdown != null)
            {
                _categoryDropdown.choices = new List<string> { "인물", "물건", "밈", "음식", "사건" };
                _categoryDropdown.value = "물건";
            }

            // 이벤트 바인딩
            if (_submitButton != null) _submitButton.clicked += Submit;
            if (_cancelButton != null) _cancelButton.clicked += Close;

            SetPanelVisible(false);
        }

        private void Update()
        {
            // TextField가 포커스된 상태라면 단축키 입력을 막음
            if (_searchField != null && _searchField.focusController.focusedElement == _searchField)
                return;

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
            if (playerController != null)
                playerController.MovementLocked = true;

            // StarterAssets 가 있다면 커서 잠금 해제
            var starterInputs = FindObjectOfType<StarterAssets.StarterAssetsInputs>();
            if (starterInputs != null)
            {
                starterInputs.cursorLocked = false;
                starterInputs.cursorInputForLook = false;
            }

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        public void Close()
        {
            SetPanelVisible(false);
            if (playerController != null)
                playerController.MovementLocked = false;

            // StarterAssets 복구
            var starterInputs = FindObjectOfType<StarterAssets.StarterAssetsInputs>();
            if (starterInputs != null)
            {
                starterInputs.cursorLocked = true;
                starterInputs.cursorInputForLook = true;
            }

            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
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
            IsOpen = visible;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
