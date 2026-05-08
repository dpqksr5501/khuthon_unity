using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.IO;

namespace Khuthon.InGame
{
    public class ObjectDetailsUI : MonoBehaviour
    {
        [Header("UI Toolkit 설정")]
        [SerializeField] private UIDocument uiDocument;
        
        private VisualElement _root;
        private TextField _nameField;
        private TextField _descField;
        private Button _bgmButton;
        private Label _bgmLabel;
        private Button _confirmButton;
        private Button _cancelButton;

        private string _selectedBgmPath = "";
        
        public event Action<string, string, string> OnConfirmed; // Name, Description, BgmPath
        public event Action OnCanceled;

        private void OnEnable()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (uiDocument != null && uiDocument.rootVisualElement != null)
            {
                SetupUI();
            }
        }

        private void SetupUI()
        {
            _root = uiDocument.rootVisualElement;
            
            // 기존에 연결된 이벤트가 있다면 해제 (중복 방지)
            if (_bgmButton != null) _bgmButton.clicked -= SelectBgmFile;

            _nameField = _root.Q<TextField>("name-field");
            _descField = _root.Q<TextField>("desc-field");
            _bgmButton = _root.Q<Button>("bgm-button");
            _bgmLabel = _root.Q<Label>("bgm-label");
            _confirmButton = _root.Q<Button>("confirm-button");
            _cancelButton = _root.Q<Button>("cancel-button");

            if (_bgmButton != null) _bgmButton.clicked += SelectBgmFile;
            if (_confirmButton != null) _confirmButton.clicked += () => OnConfirmed?.Invoke(_nameField.value, _descField.value, _selectedBgmPath);
            if (_cancelButton != null) _cancelButton.clicked += () => OnCanceled?.Invoke();
        }

        private void SelectBgmFile()
        {
            Debug.Log("[ObjectDetailsUI] BGM 파일 선택 버튼 클릭됨");
#if UNITY_EDITOR
            // 유니티 에디터의 파일 선택창을 띄웁니다.
            string path = UnityEditor.EditorUtility.OpenFilePanel("배경음 선택 (.mp3)", "", "mp3");
            
            if (!string.IsNullOrEmpty(path))
            {
                Debug.Log($"[ObjectDetailsUI] 파일 선택 완료: {path}");
                _selectedBgmPath = path;
                _bgmLabel.text = Path.GetFileName(path);
                _bgmLabel.style.color = new Color(0.4f, 0.4f, 1f, 1f); 
            }
            else
            {
                Debug.Log("[ObjectDetailsUI] 파일 선택이 취소되었습니다.");
            }
#else
            Debug.LogWarning("BGM 파일 선택은 유니티 에디터 실행 중일 때만 가능합니다.");
#endif
        }

        private void Start()
        {
            // 시작할 때는 숨김 처리 (rootVisualElement를 통해)
            Hide();
        }

        public void Show(string defaultName = "")
        {
            if (_root == null) SetupUI();
            if (_root != null) _root.style.display = DisplayStyle.Flex;
            
            _nameField.value = defaultName;
            _descField.value = "";
            _selectedBgmPath = "";
            _bgmLabel.text = "선택된 파일 없음";
            _bgmLabel.style.color = new Color(255, 255, 255, 0.4f);

            // 1. 모든 게임 입력 시스템 정지 (가장 확실한 방법)
            var playerInput = FindAnyObjectByType<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null) playerInput.enabled = false;

            var starterInputs = FindAnyObjectByType<StarterAssets.StarterAssetsInputs>();
            if (starterInputs != null)
            {
                starterInputs.move = Vector2.zero;
                starterInputs.jump = false;
                starterInputs.sprint = false;
                starterInputs.cursorLocked = false;
                starterInputs.cursorInputForLook = false;
            }

            // 2. 커서 강제 해제
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            Debug.Log("[ObjectDetailsUI] 상세 정보창 활성화 및 입력 시스템 정지");
        }

        public void Hide()
        {
            if (_root == null) SetupUI();
            if (_root != null) _root.style.display = DisplayStyle.None;

            // 1. 게임 입력 시스템 복구
            var playerInput = FindAnyObjectByType<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null) playerInput.enabled = true;

            var starterInputs = FindAnyObjectByType<StarterAssets.StarterAssetsInputs>();
            if (starterInputs != null)
            {
                starterInputs.cursorLocked = true;
                starterInputs.cursorInputForLook = true;
            }

            // 2. 커서 다시 잠금
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;

            Debug.Log("[ObjectDetailsUI] 상세 정보창 비활성화 및 입력 시스템 복구");
        }
    }
}
