using UnityEngine;
using System.Diagnostics;
using UnityEditor;
using System.IO;
using System.Globalization;
using System;

public class TripoSRForUnity : MonoBehaviour
{
    [Header("General Settings")]
    [SerializeField, Tooltip("Path to your Python executable")]
    public string pythonPath = "python";
    
    [SerializeField, Tooltip("If true, automatically adds the generated mesh to the scene.")]
    private bool autoAddMesh = true;

    [SerializeField, Tooltip("If true, automatically adds MeshCollider & RigidBody.")]
    private bool autoAddPhysicsComponents = true;
    
    [SerializeField, Tooltip("If true, automatically rotates the mesh's parent GameObject to negate wrong rotations.")]
    private bool autoFixRotation = true;
    
    [ReadOnly, SerializeField, Tooltip("If true, moves and renames the output .obj file (based on the input image's filename)")]
    private bool moveAndRename = true;
    
    [SerializeField, Tooltip("If moveAndRename = true, specifies the relative path to some folder where the output .obj file will be moved to.")]
    private string moveAndRenamePath = "Models";
    
    [SerializeField, Tooltip("If true, TripoSR's run.py debug output is printed to Unity's console.")]
    private bool showDebugLogs = true;

    [SerializeField, Tooltip("Path to input image(s).")]
    private Texture2D[] images;
    
    [Header("TripoSR Parameters")]
    [ReadOnly, SerializeField, Tooltip("Device to use. Default: 'cuda:0'")]
    private string device = "cuda:0";

    [ReadOnly, SerializeField, Tooltip("Path to the pretrained model. Default: 'stabilityai/TripoSR'")]
    private string pretrainedModelNameOrPath = "stabilityai/TripoSR";

    [SerializeField, Tooltip("Evaluation chunk size. Default: 8192")]
    private int chunkSize = 8192;

    [SerializeField, Tooltip("Marching cubes grid resolution. Default: 256")]
    private int marchingCubesResolution = 256;

    [SerializeField, Tooltip("If true, background will not be removed. Default: false")]
    private bool noRemoveBg = false;

    [SerializeField, Tooltip("Foreground to image size ratio. Default: 0.85")]
    private float foregroundRatio = 0.85f;

    [ReadOnly, SerializeField, Tooltip("Output directory. Default: 'output/'")]
    private string outputDir = "output/";

    [ReadOnly, SerializeField, Tooltip("Mesh save format. Default: 'obj'")]
    private string modelSaveFormat = "obj";

    [ReadOnly, SerializeField, Tooltip("If true, saves a rendered video. Default: false")]
    private bool render = false;

    private Process pythonProcess;
    private bool isProcessRunning = false;

    public static event Action OnPythonProcessEnded;
    public static event Action<GameObject> OnModelInstantiated;

    public void RunTripoSR()
    {
        if (images == null || images.Length == 0)
        {
            UnityEngine.Debug.LogError("No images assigned to TripoSR.");
            return;
        }
        RunInternal(images);
    }

    public void RunTripoSRWithTexture(Texture2D tex)
    {
        RunInternal(new Texture2D[] { tex });
    }

    private void RunInternal(Texture2D[] inputImages)
    {
        this.images = inputImages; // 콜백(MoveAndRenameOutputFile)에서 사용할 수 있도록 저장

        if (isProcessRunning)
        {
            UnityEngine.Debug.Log("A TripoSR process is already running - quitting and replacing process.");

            if (pythonProcess is { HasExited: false })
            {
                pythonProcess.Kill();
                pythonProcess.Dispose();
            }

            pythonProcess = null;
            isProcessRunning = false;
        }
        
        string[] imagePaths = new string[inputImages.Length];
        for (int i = 0; i < inputImages.Length; i++)
        {
            string path = AssetDatabase.GetAssetPath(inputImages[i]);
            imagePaths[i] = Path.GetFullPath(path);
        }

        string args = $"\"{string.Join("\" \"", imagePaths)}\" --device {device} " +
                      $"--pretrained-model-name-or-path {pretrainedModelNameOrPath} " +
                      $"--chunk-size {chunkSize} --mc-resolution {marchingCubesResolution} " +
                      $"{(noRemoveBg ? "--no-remove-bg " : "")} " +
                      $"--foreground-ratio {foregroundRatio.ToString(CultureInfo.InvariantCulture)} --output-dir {Path.Combine(Application.dataPath, "TripoSR/" + outputDir)} " +
                      $"--model-save-format {((modelSaveFormat == "dae") ? "obj" : modelSaveFormat)} " +
                      $"{(render ? "--render" : "")}";

        ProcessStartInfo start = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = $"{Path.Combine(Application.dataPath, "TripoSR/run.py")} {args}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        pythonProcess = new Process {StartInfo = start};
        pythonProcess.StartInfo = start;
        pythonProcess.EnableRaisingEvents = true;
        pythonProcess.Exited += OnPythonProcessExited;

        pythonProcess.OutputDataReceived += (sender, e) => 
        {
            if (showDebugLogs && !string.IsNullOrEmpty(e.Data))
            {
                UnityEngine.Debug.Log(e.Data);
            }
        };

        pythonProcess.ErrorDataReceived += (sender, e) => 
        {
            if (showDebugLogs && !string.IsNullOrEmpty(e.Data))
            {
                UnityEngine.Debug.Log(e.Data);
            }
        };

        pythonProcess.Start();
        pythonProcess.BeginOutputReadLine();
        pythonProcess.BeginErrorReadLine();
        isProcessRunning = true;
    }

    private void OnPythonProcessExited(object sender, EventArgs e)
    {
        isProcessRunning = false;
        pythonProcess = null;
        
        if (moveAndRename) UnityEditor.EditorApplication.delayCall += MoveAndRenameOutputFile;
        else if (autoAddMesh) UnityEditor.EditorApplication.delayCall += () => AddMeshToScene(null);

        UnityEditor.EditorApplication.delayCall += () => OnPythonProcessEnded?.Invoke();
    }

    private void MoveAndRenameOutputFile()
    {
        string originalPath = Path.Combine(Application.dataPath, "TripoSR/" + outputDir + "0/mesh.obj");
        string modelsDirectory = "Assets/"+moveAndRenamePath;
        // 타임스탬프를 추가하여 파일 이름이 겹치지 않게 만듭니다.
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        // 이미지의 GetHashCode를 사용하여 고유하지만 일정한 이름 생성 (재로딩 시 캐시 확인용)
        string hash = Mathf.Abs(AssetDatabase.GetAssetPath(images[0]).GetHashCode()).ToString();
        string newFileName = "Model_" + hash + ".obj";
        string newAssetPath = Path.Combine(modelsDirectory, newFileName);
        string newPath = Path.Combine(Application.dataPath, newAssetPath.Substring("Assets/".Length));

        if (!Directory.Exists(modelsDirectory)) Directory.CreateDirectory(modelsDirectory);

        if (File.Exists(originalPath))
        {
            if (File.Exists(newPath)) 
            {
                File.Delete(newPath); // 만약 존재한다면 덮어쓰기 위해 삭제
            }
            
            File.Move(originalPath, newPath);
            AssetDatabase.Refresh();

            UnityEngine.Debug.Log($"Moved and renamed mesh to path: {newPath}");

            if (autoAddMesh) AddMeshToScene(newAssetPath);
        }
        else UnityEngine.Debug.Log($"File @ {originalPath} does not exist - cannot move and rename.");
    }

    private void AddMeshToScene(string path = null)
    {        
        string objPath = path ?? "Assets/TripoSR/" + outputDir + "0/mesh.obj";

        AssetDatabase.Refresh();
        AssetDatabase.ImportAsset(objPath, ImportAssetOptions.ForceUpdate);

        GameObject importedObj = AssetDatabase.LoadAssetAtPath<GameObject>(objPath);

        if (importedObj != null)
        {
            GameObject instantiatedObj = Instantiate(importedObj);
            instantiatedObj.name = importedObj.name;

            UnityEngine.Debug.Log("Instantiated GameObject prefab: " + instantiatedObj.name);

            if (autoFixRotation) instantiatedObj.transform.GetChild(0).rotation = Quaternion.Euler(new Vector3(-90f, -90f, 0f));

            if (autoAddPhysicsComponents) 
            {
                GameObject meshObj = instantiatedObj.transform.GetChild(0).gameObject;
                MeshCollider mc = meshObj.AddComponent<MeshCollider>();
                mc.convex = true;
                // Rigidbody는 배치가 완료된 후에 추가하는 것이 좋으므로 여기서는 생략하거나 비활성화 상태로 추가
                Rigidbody rb = meshObj.AddComponent<Rigidbody>();
                rb.isKinematic = true; // 배치 전까지는 물리 고정
            }

            OnModelInstantiated?.Invoke(instantiatedObj); // 생성된 오브젝트 알림
        }
        else UnityEngine.Debug.LogError("Failed to load the mesh at path: " + objPath);
    }


    void OnDisable() { if (pythonProcess != null && !pythonProcess.HasExited) pythonProcess.Kill(); }
}
