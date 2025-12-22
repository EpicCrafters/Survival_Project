using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

// SceneStreamer: stream scenes additively based on players' proximity
public class SceneStreamer : MonoBehaviour
{
    [Header("Scene config")]
    public SceneConfig sceneConfig;

    [Header("Distances (units)")]
    public float loadDistance = 100f;
    public float unloadDistance = 120f;

    [Header("Checks")]
    public float checkInterval = 0.5f; // Time between checks

    [Header("Gizmos")]
    public Color boundColor = Color.yellow;
    public Color loadColor = Color.green;
    public Color unloadColor = Color.red;
    public bool drawLabels = true;

    [Header("Player detection")]
    [Tooltip("Tags to consider players. If empty, will check objects with name containing 'player'.")]
    public string[] playerTags = new string[] { "Player" };

    [Header("Startup Configuration")]
    [Tooltip("Wait for all scenes in sceneConfig to be available before starting streaming logic")]
    public bool waitForAllScenes = true;

    [Header("Automatic Player Detection")]
    [Tooltip("Automatically search for players in newly loaded scenes")]
    public bool autoDetectNewPlayers = true;
    [Tooltip("Player prefab name to search for (case insensitive)")]
    public string playerPrefabName = "Player";

    private Transform[] players;
    private HashSet<string> loadingScenes = new HashSet<string>();

    // Coroutines references (so we can stop them individually)
    private Coroutine checkScenesCoroutine;
    private Coroutine startupCoroutine;

    // Ensure we only start the check loop once when ready
    private bool streamingStarted = false;
    private bool allScenesAvailable = false;

    void Awake()
    {
        // Subscribe so we can re-check when scenes get loaded at runtime
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    void Start()
    {
        // Initialize with empty players array - they'll be spawned later
        players = new Transform[0];

        // If any of the configured scenes are already loaded, unload them so streamer controls them.
        if (sceneConfig != null && sceneConfig.scenes != null)
        {
            foreach (var scene in sceneConfig.scenes)
            {
                if (string.IsNullOrEmpty(scene.sceneName)) continue;
                var s = SceneManager.GetSceneByName(scene.sceneName);
                if (s.isLoaded)
                {
                    SceneManager.UnloadSceneAsync(scene.sceneName);
                }
            }
        }

        // Startup sequence
        if (waitForAllScenes)
        {
            startupCoroutine = StartCoroutine(WaitForAllScenesThenStart());
        }
        else
        {
            StartStreaming();
        }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    IEnumerator WaitForAllScenesThenStart()
    {
        Debug.Log("[SceneStreamer] Waiting for all scenes to be available...");

        while (!AreAllScenesInConfigAvailable())
        {
            yield return new WaitForSeconds(checkInterval);
        }

        allScenesAvailable = true;
        Debug.Log("[SceneStreamer] All scenes in config are available.");

        // Now proceed with normal startup
        StartStreaming();
    }

    bool AreAllScenesInConfigAvailable()
    {
        if (sceneConfig == null || sceneConfig.scenes == null)
            return true;

        // Check if all scenes in the config exist in the build settings
        foreach (var sceneData in sceneConfig.scenes)
        {
            if (string.IsNullOrEmpty(sceneData.sceneName))
                continue;

            // Check if scene exists in build settings
            if (!DoesSceneExistInBuildSettings(sceneData.sceneName))
            {
                return false;
            }
        }

        return true;
    }

#if UNITY_EDITOR
    bool DoesSceneExistInBuildSettings(string sceneName)
    {
        foreach (var scene in EditorBuildSettings.scenes)
        {
            string scenePath = scene.path;
            string sceneFileName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (sceneFileName == sceneName)
            {
                return true;
            }
        }
        return false;
    }
#else
    bool DoesSceneExistInBuildSettings(string sceneName)
    {
        // At runtime, we can only check if the scene is in the build settings
        // by trying to get its build index
        return SceneUtility.GetBuildIndexByScenePath(sceneName) >= 0;
    }
#endif

    // Public method host/spawner can call once world is ready
    public void StartStreaming()
    {
        if (streamingStarted) return;
        streamingStarted = true;
        if (checkScenesCoroutine != null) StopCoroutine(checkScenesCoroutine);
        checkScenesCoroutine = StartCoroutine(CheckScenes());
        Debug.Log("[SceneStreamer] Streaming started.");
    }

    public void StopStreaming()
    {
        streamingStarted = false;
        if (checkScenesCoroutine != null)
        {
            StopCoroutine(checkScenesCoroutine);
            checkScenesCoroutine = null;
        }
        Debug.Log("[SceneStreamer] Streaming stopped.");
    }

    // Called whenever a scene is loaded — refresh players
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshPlayers();

        // Also search for player objects in the newly loaded scene
        if (autoDetectNewPlayers)
        {
            FindPlayersInScene(scene);
        }
    }

    void FindPlayersInScene(Scene scene)
    {
        var rootObjects = scene.GetRootGameObjects();
        foreach (var obj in rootObjects)
        {
            // Check by name
            if (obj.name.Contains(playerPrefabName, StringComparison.OrdinalIgnoreCase))
            {
                RegisterPlayer(obj.transform);
                Debug.Log($"[SceneStreamer] Auto-detected player: {obj.name}");
            }

            // Also check children
            foreach (Transform child in obj.transform)
            {
                if (child.name.Contains(playerPrefabName, StringComparison.OrdinalIgnoreCase))
                {
                    RegisterPlayer(child);
                    Debug.Log($"[SceneStreamer] Auto-detected player: {child.name}");
                }
            }
        }
    }

    void OnSceneUnloaded(Scene scene)
    {
        // Keep players list clean
        if (players != null && players.Length > 0)
        {
            var remaining = players.Where(p => p != null && p.gameObject.scene.isLoaded).ToArray();
            players = remaining;
        }
    }

    // Public API for spawn code to register player transforms directly (recommended)
    public void RegisterPlayer(Transform playerTransform)
    {
        if (playerTransform == null) return;
        var list = players != null ? players.ToList() : new List<Transform>();
        if (!list.Contains(playerTransform))
        {
            list.Add(playerTransform);
            players = list.ToArray();
            Debug.Log($"[SceneStreamer] Registered player: {playerTransform.name}");
        }
    }

    public void UnregisterPlayer(Transform playerTransform)
    {
        if (playerTransform == null || players == null) return;
        players = players.Where(p => p != playerTransform).ToArray();
        Debug.Log($"[SceneStreamer] Unregistered player: {playerTransform.name}");
    }

    // Discover players by tag or name (call after scenes load)
    void RefreshPlayers()
    {
        var found = new List<Transform>();

        if (playerTags != null && playerTags.Length > 0)
        {
            foreach (var tag in playerTags)
            {
                if (string.IsNullOrEmpty(tag)) continue;
                try
                {
                    var gos = GameObject.FindGameObjectsWithTag(tag);
                    foreach (var go in gos) found.Add(go.transform);
                }
                catch { /* tag might not exist - ignore */ }
            }
        }

        if (found.Count == 0)
        {
            var all = FindObjectsOfType<Transform>();
            foreach (var t in all)
            {
                // fallback: restrict to roots to avoid matching bones/children accidentally
                if (t.parent != null) continue;
                if (t.name.ToLower().Contains("player"))
                    found.Add(t);
            }
        }

        players = found.Distinct().ToArray();
    }

    IEnumerator CheckScenes()
    {
        // Wait until all scenes in config exist if we're configured to wait
        if (waitForAllScenes && !allScenesAvailable)
        {
            Debug.Log("[SceneStreamer] Waiting for all scenes to be available before streaming...");
            while (!AreAllScenesInConfigAvailable())
            {
                yield return new WaitForSeconds(checkInterval);
            }
            allScenesAvailable = true;
        }

        while (true)
        {
            // Always try to find players if we don't have any
            if (players == null || players.Length == 0)
            {
                RefreshPlayers();

                // If still no players, wait and continue
                if (players == null || players.Length == 0)
                {
                    yield return new WaitForSeconds(checkInterval);
                    continue;
                }
            }

            if (sceneConfig == null || sceneConfig.scenes == null)
            {
                yield return new WaitForSeconds(checkInterval);
                continue;
            }

            // Group bounds by scene name to support multiple bounds per scene.
            var groupedScenes = sceneConfig.scenes
                .Where(s => !string.IsNullOrEmpty(s.sceneName))
                .GroupBy(s => s.sceneName)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var kvp in groupedScenes)
            {
                string sceneName = kvp.Key;
                List<SceneData> sceneBoundsList = kvp.Value;

                // Defensive: skip if no players known right now
                if (players == null || players.Length == 0)
                {
                    // If we don't know players, skip load/unload decisions for safety.
                    continue;
                }

                bool anyPlayerNear = players.Any(p =>
                    p != null &&
                    sceneBoundsList.Any(bound =>
                        bound.sceneBounds.Contains(p.position) ||
                        Vector3.Distance(bound.sceneBounds.ClosestPoint(p.position), p.position) <= loadDistance));

                bool allPlayersFar = players.All(p =>
                    p == null ||
                    sceneBoundsList.All(bound =>
                        Vector3.Distance(bound.sceneBounds.ClosestPoint(p.position), p.position) > unloadDistance));

                Scene sceneObj = SceneManager.GetSceneByName(sceneName);

                if (anyPlayerNear && !sceneObj.isLoaded && !loadingScenes.Contains(sceneName))
                {
                    Debug.Log($"[SceneStreamer] Loading scene: {sceneName}");

                    loadingScenes.Add(sceneName);

                    var loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                    loadOp.completed += _ =>
                    {
                        // Remove from loading set
                        loadingScenes.Remove(sceneName);

                        // Refresh players in case the newly-loaded scene spawns player objects
                        RefreshPlayers();
                    };
                }
                else if (allPlayersFar && sceneObj.isLoaded)
                {
                    Debug.Log($"[SceneStreamer] Unloading scene: {sceneName}");
                    SceneManager.UnloadSceneAsync(sceneName);
                }
            }

            yield return new WaitForSeconds(checkInterval);
        }
    }

    void OnDrawGizmos()
    {
        if (sceneConfig == null || sceneConfig.scenes == null) return;

        foreach (var scene in sceneConfig.scenes)
        {
            Vector3 center = scene.sceneBounds.center;
            Vector3 size = scene.sceneBounds.size;

            // Draw the base scene bounds
            Gizmos.color = boundColor;
            Gizmos.DrawWireCube(center, size);

            // Draw load distance bounds 
            Gizmos.color = loadColor;
            Gizmos.DrawWireCube(center, size + Vector3.one * loadDistance * 2f);

            // Draw unload distance bounds 
            Gizmos.color = unloadColor;
            Gizmos.DrawWireCube(center, size + Vector3.one * unloadDistance * 2f);

#if UNITY_EDITOR
            if (drawLabels)
            {
                Handles.Label(center + Vector3.up * 2f, scene.sceneName);
            }
#endif
        }
    }
}