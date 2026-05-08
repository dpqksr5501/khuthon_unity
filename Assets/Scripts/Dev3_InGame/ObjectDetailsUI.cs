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

        private void Awake()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            SetupUI();
            gameObject.SetActive(false);
        }

        private void SetupUI()
        {
            _root = uiDocument.rootVisualElement;
            _nameField = _root.Q<TextField>("name-field");
            _descField = _root.Q<TextField>("desc-field");
            _bgmButton = _root.Q<Button>("bgm-button");
            _bgmLabel = _root.Q<Label>("bgm-label");
            _confirmButton = _root.Q<Button>("confirm-button");
            _cancelButton = _root.Q<Button>("cancel-button");

            _bgmButton.clicked += SelectBgmFile;
            _confirmButton.clicked += () => OnConfirmed?.Invoke(_nameField.value, _descField.value, _selectedBgmPath);
            _cancelButton.clicked += () => OnCanceled?.Invoke();
        }

        private void SelectBgmFile()
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("배경음 선택", "", "mp3");
            if (!string.IsNullOrEmpty(path))
            {
                _selectedBgmPath = path;
                _bgmLabel.text = Path.GetFileName(path);
                _bgmLabel.style.color = new Color(0.4f, 0.4f, 1f, 1f); // Indigo 컬러
            }
#else
            Debug.LogWarning("BGM 파일 선택은 에디터 환경에서만 지원됩니다.");
#endif
        }

        public void Show(string defaultName = "")
        {
            gameObject.SetActive(true);
            _nameField.value = defaultName;
            _descField.value = "";
            _selectedBgmPath = "";
            _bgmLabel.text = "선택된 파일 없음";
            _bgmLabel.style.color = new Color(255, 255, 255, 0.4f);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
