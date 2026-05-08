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
    /// </summary>
    public class ImageSelectionUI : MonoBehaviour
    {
        [Header("UI Toolkit 설정")]
        [SerializeField] private UIDocument uiDocument;

        private VisualElement _root;
        private VisualElement[] _slots = new VisualElement[4];
        private VisualElement[] _previews = new VisualElement[4];
        private Button _confirmButton;
        private Button _cancelButton;

        public event Action<int, string> OnImageConfirmed;
        public event Action OnCancelled;

        private List<string> _urls = new List<string>();
        private int _selectedIndex = -1;
        private readonly Texture2D[] _textures = new Texture2D[4];

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

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                _slots[i] = _root.Q<VisualElement>($"slot-{i}");
                _previews[i] = _root.Q<VisualElement>($"img-{i}");
                
                // 요소가 존재할 때만 이벤트 등록
                if (_slots[idx] != null)
                {
                    _slots[idx].RegisterCallback<ClickEvent>(evt => SelectSlot(idx));
                }
                else
                {
                    Debug.LogWarning($"[ImageSelectionUI] UXML에서 'slot-{idx}'를 찾을 수 없습니다.");
                }
            }

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
            ResetSlots();
            SetVisible(true);

            for (int i = 0; i < 4; i++)
            {
                if (i < _urls.Count)
                    StartCoroutine(LoadImage(i, _urls[i]));
                else
                    _slots[i].style.display = DisplayStyle.None;
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
                    _textures[index] = tex;
                    _previews[index].style.backgroundImage = new StyleBackground(tex);
                }
            }
        }

        private void SelectSlot(int index)
        {
            _selectedIndex = index;
            for (int i = 0; i < 4; i++)
            {
                if (i == index)
                    _slots[i].AddToClassList("selected");
                else
                    _slots[i].RemoveFromClassList("selected");
            }
            _confirmButton.SetEnabled(true);
        }

        private void Confirm()
        {
            if (_selectedIndex >= 0 && _selectedIndex < _urls.Count)
            {
                OnImageConfirmed?.Invoke(_selectedIndex, _urls[_selectedIndex]);
                SetVisible(false);
            }
        }

        private void Cancel()
        {
            OnCancelled?.Invoke();
            SetVisible(false);
        }

        private void ResetSlots()
        {
            for (int i = 0; i < 4; i++)
            {
                _slots[i].style.display = DisplayStyle.Flex;
                _slots[i].RemoveFromClassList("selected");
                _previews[i].style.backgroundImage = null;
            }
            _confirmButton.SetEnabled(false);
        }

        private void SetVisible(bool visible)
        {
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
        }

        private void OnDestroy()
        {
            foreach (var tex in _textures)
                if (tex != null) Destroy(tex);
        }
    }
}
