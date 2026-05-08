using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using Khuthon.Backend;
using Khuthon.AI3D;
using Khuthon.InGame;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;

namespace Khuthon.Editor
{
    public class PlaygroundSetup : EditorWindow
    {
        [MenuItem("Khuthon/Setup Playground Scene")]
        public static void SetupScene()
        {
            // 1. 새 씬 생성
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            newScene.name = "Playground";

            // 2. 바닥 생성
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(50, 1, 50);
            floor.transform.position = new Vector3(0, -0.5f, 0);
            floor.layer = LayerMask.NameToLayer("Default"); // 필요시 Ground 레이어 설정

            // 3. GameManager 구성
            GameObject gm = new GameObject("GameManager");
            var searcher = gm.AddComponent<GoogleImageSearcher>();
            var firebase = gm.AddComponent<FirebaseManager>();
            var tripo = gm.AddComponent<Tripo3DClient>();
            var localTripo = gm.AddComponent<TripoSRForUnity>();
            var loader = gm.AddComponent<GlbModelLoader>();
            var pipeline = gm.AddComponent<Model3DPipelineManager>();
            var housing = gm.AddComponent<HousingPlacementSystem>();
            var orchestrator = gm.AddComponent<GameOrchestrator>();

            // Pipeline 설정
            var pipelineSo = new SerializedObject(pipeline);
            pipelineSo.FindProperty("localTripoSR").objectReferenceValue = localTripo;
            pipelineSo.ApplyModifiedProperties();

            // 4. 플레이어 구성
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0, 1, 0);
            var cc = player.AddComponent<CharacterController>();
            var pc = player.AddComponent<PlayerController>();
            
            // 메인 카메라를 플레이어 자식으로 (간단한 구현)
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.transform.SetParent(player.transform);
                mainCam.transform.localPosition = new Vector3(0, 1, -5);
                mainCam.transform.localRotation = Quaternion.Euler(10, 0, 0);
            }

            // 5. UI 구성 (UI Toolkit - UIDocument)
            GameObject uiObj = new GameObject("UI_Toolkit");
            var uiDoc = uiObj.AddComponent<UIDocument>();
            uiObj.AddComponent<UnityEngine.UIElements.PanelEventHandler>();
            uiObj.AddComponent<UnityEngine.UIElements.PanelRaycaster>();

            // GameManager에 UIDocument 연결
            var searchUI = gm.AddComponent<SearchInputUI>();
            var selectionUI = gm.AddComponent<ImageSelectionUI>();
            
            // 6. Orchestrator 연결 (가능한 것들)
            var so = new SerializedObject(orchestrator);
            so.FindProperty("googleSearcher").objectReferenceValue = searcher;
            so.FindProperty("firebaseManager").objectReferenceValue = firebase;
            so.FindProperty("pipelineManager").objectReferenceValue = pipeline;
            so.FindProperty("searchInputUI").objectReferenceValue = searchUI;
            so.FindProperty("imageSelectionUI").objectReferenceValue = selectionUI; // 새로 추가된 연결
            so.FindProperty("housingSystem").objectReferenceValue = housing;
            so.FindProperty("playerController").objectReferenceValue = pc;
            so.ApplyModifiedProperties();

            Debug.Log("[PlaygroundSetup] UI Toolkit 기반 오브젝트 구성 완료. UIDocument의 Source Asset에 .uxml 파일을 수동으로 연결해주세요.");

            EditorSceneManager.SaveScene(newScene, "Assets/Scenes/Playground.unity");
            Debug.Log("[PlaygroundSetup] 'Assets/Scenes/Playground.unity'가 생성되었습니다.");
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            return obj;
        }
    }
}
