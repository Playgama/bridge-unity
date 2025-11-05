using UnityEngine;
using UnityEngine.SceneManagement;
using System.Runtime.InteropServices;
using System.Collections;

public class PlaygamaBridgeBootstrap
{
    private static bool initialized = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (initialized) return;
        initialized = true;

        #if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log("PlaygamaBridge Bootstrap: Checking bridge status...");
        
        if (IsBridgeInitialized())
        {
            Debug.Log("✅ Bridge already initialized by index.html - doing nothing");
            return;
        }
        
        Debug.Log("⚠️ Bridge not initialized - creating initialization scene");
        CreateInitializationScene();
        #else
        Debug.Log("PlaygamaBridge: Not WebGL, skipping");
        #endif
    }

    [DllImport("__Internal")]
    private static extern bool CheckBridgeInitialized();

    private static bool IsBridgeInitialized()
    {
        try
        {
            return CheckBridgeInitialized();
        }
        catch
        {
            return false;
        }
    }

    private static void CreateInitializationScene()
    {
        // Get the first scene from build settings (the scene that would have loaded)
        string originalFirstScene = GetFirstSceneName();
        
        // Create temporary initialization scene
        Scene initScene = SceneManager.CreateScene("_BridgeInitialization");
        SceneManager.SetActiveScene(initScene);
        
        // Create GameObject with initializer
        GameObject bridgeObject = new GameObject("PlaygamaBridge");
        var initializer = bridgeObject.AddComponent<PlaygamaBridgeInitializer>();
        initializer.SetNextScene(originalFirstScene);
        
        Debug.Log($"Initialization scene created, will load '{originalFirstScene}' after bridge ready");
    }
    
    private static string GetFirstSceneName()
    {
        if (SceneManager.sceneCountInBuildSettings > 0)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(0);
            return System.IO.Path.GetFileNameWithoutExtension(scenePath);
        }
        
        Debug.LogError("No scenes in build settings!");
        return "";
    }
}

public class PlaygamaBridgeInitializer : MonoBehaviour
{
    [DllImport("__Internal")]
    private static extern void PlaygamaBridgeInitialize();
    
    private string nextSceneName = "";
    private bool bridgeReady = false;
    private float waitTimer = 0f;
    private float maxWaitTime = 10f;
    
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        gameObject.name = "PlaygamaBridge";
    }
    
    void Start()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        StartCoroutine(InitializeAndLoadNextScene());
        #else
        LoadNextScene();
        #endif
    }
    
    public void SetNextScene(string sceneName)
    {
        nextSceneName = sceneName;
    }
    
    private IEnumerator InitializeAndLoadNextScene()
    {
        Debug.Log("Initializing Playgama Bridge from Unity...");
        
        yield return null;
        
        try
        {
            PlaygamaBridgeInitialize();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize bridge: {e.Message}");
            LoadNextScene();
            yield break;
        }
        
        // Wait for OnBridgeReady callback
        while (!bridgeReady && waitTimer < maxWaitTime)
        {
            waitTimer += Time.deltaTime;
            yield return null;
        }
        
        if (bridgeReady)
        {
            Debug.Log($"✅ Playgama Bridge initialized successfully in {waitTimer:F2}s");
        }
        else
        {
            Debug.LogError($"⏱️ Bridge initialization timeout after {maxWaitTime}s");
        }
        
        LoadNextScene();
    }
    
    void OnBridgeReady(string success)
    {
        if (success == "true")
        {
            bridgeReady = true;
            Debug.Log("Bridge ready callback received!");
        }
    }
    
    private void LoadNextScene()
    {
        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogError("No next scene specified!");
            return;
        }
        
        Debug.Log($"Loading scene: {nextSceneName}");
        Destroy(gameObject);
        SceneManager.LoadScene(nextSceneName);
    }
}
