using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace Khuthon.UI
{
    public class TitleScreenController : MonoBehaviour
    {
        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            // Find buttons
            var playBtn = root.Q<Button>("play-button");
            var settingsBtn = root.Q<Button>("settings-button");
            var exitBtn = root.Q<Button>("exit-button");

            // Attach click events
            if (playBtn != null)
            {
                playBtn.clicked += () => {
                    Debug.Log("Starting Game...");
                    // Assuming 'MainScene' is the name of your game scene
                    // You can change this to any scene you want to load
                    SceneManager.LoadScene("MAP"); 
                };
            }

            if (settingsBtn != null)
            {
                settingsBtn.clicked += () => {
                    Debug.Log("Settings Opened");
                };
            }

            if (exitBtn != null)
            {
                exitBtn.clicked += () => {
                    Debug.Log("Exiting Game...");
                    #if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
                    #else
                    Application.Quit();
                    #endif
                };
            }

            // Enable mouse cursor
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }
    }
}
