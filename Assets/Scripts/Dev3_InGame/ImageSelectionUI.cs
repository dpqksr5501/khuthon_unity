using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Networking;

namespace Khuthon.InGame
{
    /// <summary>
    /// UI Toolkit(UXML) 기반 이미지 선택 UI.
    /// 동적으로 슬롯을 생성하여 4개 이상의 이미지(예: 15개)를 지원합니다.
    /// </summary>
    public class ImageSelectionUI : MonoBehaviour
    {
        [Header("UI Toolkit 설정")]
        [SerializeField] private UIDocument uiDocument;

        private VisualElement _root;
        private VisualElement _grid;
        private List<VisualElement> _slots = new List<VisualElement>();
        private List<VisualElement> _previews = new List<VisualElement>();
        private Button _confirmButton;
        private Button _cancelButton;

        public event Action<int, string> OnImageConfirmed;
        public event Action OnCancelled;

        private List<string> _urls = new List<string>();
        private int _selectedIndex = -1;
        private List<Texture2D> _textures = new List<Texture2D>();

        private void Awake()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            
            if (uiDocument == null || uiDocument.visualTreeAsset == null)
            {
                Debug.LogWarning("[ImageSelectionUI] UIDocument 또는 VisualTreeAsset(UXML)이 연결되지 않았습니다.");
                return;
            }

            _root = uiDocument.rootVisualElement;
            if (_root == null) return;

            _grid = _root.Q<VisualElement>("image-grid");
            _confirmButton = _root.Q<Button>("confirm-button");
            _cancelButton = _root.Q<Button>("cancel-button");

            if (_confirmButton != null) _confirmButton.clicked += Confirm;
            if (_cancelButton != null) _cancelButton.clicked += Cancel;

            SetVisible(false);
        }

        public void Show(List<string> imageUrls)
        {
            _urls = imageUrls ?? new List<string>();
            _selectedIndex = -1;
            
            ClearExistingSlots();
            CreateDynamicSlots(_urls.Count);
            
            SetVisible(true);

            for (int i = 0; i < _urls.Count; i++)
            {
                StartCoroutine(LoadImage(i, _urls[i]));
            }
        }

        private void ClearExistingSlots()
        {
            if (_grid != null) _grid.Clear();
            _slots.Clear();
            _previews.Clear();
            
            foreach (var tex in _textures)
                if (tex != null) Destroy(tex);
            _textures.Clear();
        }

        private void CreateDynamicSlots(int count)
        {
            if (_grid == null) return;

            for (int i = 0; i < count; i++)
            {
                int idx = i;
                
                // 슬롯 생성
                var slot = new VisualElement();
                slot.AddToClassList("image-slot");
                slot.RegisterCallback<ClickEvent>(evt => SelectSlot(idx));
                
                // 이미지 프리뷰 생성
                var preview = new VisualElement();
                preview.AddToClassList("image-preview");
                slot.Add(preview);
                
                // 선택 오버레이 생성
                var overlay = new VisualElement();
                overlay.AddToClassList("selection-overlay");
                slot.Add(overlay);

                _grid.Add(slot);
                _slots.Add(slot);
                _previews.Add(preview);
                _textures.Add(null);
            }
        }

        private IEnumerator LoadImage(int index, string url)
        {
            using (var req = UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(req);
                    if (index < _textures.Count)
                    {
                        _textures[index] = tex;
                        _previews[index].style.backgroundImage = new StyleBackground(tex);
                    }
                }
            }
        }

        private void SelectSlot(int index)
        {
            _selectedIndex = index;
            ResetSlotsStyle();

            if (index >= 0 && index < _slots.Count)
            {
                _slots[index].AddToClassList("selected");
                Debug.Log($"[ImageSelectionUI] {index}번 슬롯 선택됨");
            }
        }

        private void ResetSlotsStyle()
        {
            foreach (var slot in _slots)
            {
                slot.RemoveFromClassList("selected");
            }
        }

        private void Confirm()
        {
            if (_selectedIndex >= 0 && _selectedIndex < _urls.Count)
            {
                OnImageConfirmed?.Invoke(_selectedIndex, _urls[_selectedIndex]);
                SetVisible(false);
            }
            else
            {
                Debug.LogWarning("[ImageSelectionUI] 선택된 이미지가 없습니다.");
            }
        }

        private void Cancel()
        {
            OnCancelled?.Invoke();
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            if (_root != null)
                _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            
            var starterInputs = FindObjectOfType<StarterAssets.StarterAssetsInputs>();

            if (visible)
            {
                if (starterInputs != null)
                {
                    starterInputs.cursorLocked = false;
                    starterInputs.cursorInputForLook = false;
                }
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
            else
            {
                if (starterInputs != null)
                {
                    starterInputs.cursorLocked = true;
                    starterInputs.cursorInputForLook = true;
                }
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }
        }

        private void OnDestroy()
        {
            ClearExistingSlots();
        }
    }
}
