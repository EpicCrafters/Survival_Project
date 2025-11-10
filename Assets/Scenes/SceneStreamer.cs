using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

// SceneStreamer: stream scenes additively based on players' proximity,
// and freeze/unfreeze players while loading without requiring a Player.Instance.
// New: option to wait until "ground" scenes are loaded before starting.
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

    [Header("Player detection / freeze")]
    [Tooltip("Tags to consider players. If empty, will check objects with name containing 'player'.")]
    public string[] playerTags = new string[] { "Player" };
    [Tooltip("Component type names to disable when freezing. Examples: 'PlayerMovement', 'FirstPersonController'")]
    public string[] movementComponentNames = new string[] { "PlayerMovement", "FirstPersonController", "ThirdPersonController" };

    [Header("Ground / startup")]
    [Tooltip("If true, the streamer will wait until the ground scenes listed below are loaded before starting streaming logic.")]
    public bool waitForGroundScenes = true;
    [Tooltip("List the scene names that define the world's ground/environment. The streamer waits until all of these scenes are loaded.")]
    public string[] groundSceneNames = new string[0];

    private Transform[] players;
    private HashSet<string> loadingScenes = new HashSet<string>();
    private Dictionary<Transform, PlayerFreezeState> freezeStates = new Dictionary<Transform, PlayerFreezeState>();

    // Coroutines references (so we can stop them individually)
    private Coroutine checkScenesCoroutine;
    private Coroutine waitForGroundCoroutine;

    // Ensure we only start the check loop once when ready
    private bool streamingStarted = false;

    void Awake()
    {
        // Subscribe so we can re-check when scenes get loaded at runtime
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    void Start()
    {
        RefreshPlayers();

        if (players == null || players.Length == 0)
        {
            Debug.LogWarning("[SceneStreamer] No players found at Start(). Will continue and try to find players on the fly.");
        }

        // If any of the configured scenes are already loaded, unload them so streamer controls them.
        // (Optional: leave this out if you want to keep currently loaded scenes)
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

        if (waitForGroundScenes && groundSceneNames != null && groundSceneNames.Length > 0)
        {
            waitForGroundCoroutine = StartCoroutine(WaitForGroundScenesThenStart());
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

    // Public method host/spawner can call once world is ready
    public void StartStreaming()
    {
        if (streamingStarted) return;
        streamingStarted = true;
        if (checkScenesCoroutine != null) StopCoroutine(checkScenesCoroutine);
        checkScenesCoroutine = StartCoroutine(CheckScenes());
    }

    public void StopStreaming()
    {
        streamingStarted = false;
        if (checkScenesCoroutine != null)
        {
            StopCoroutine(checkScenesCoroutine);
            checkScenesCoroutine = null;
        }
    }

    IEnumerator WaitForGroundScenesThenStart()
    {
        while (!AreAllGroundScenesLoaded())
        {
            yield return new WaitForSeconds(checkInterval);
        }

        Debug.Log("[SceneStreamer] All ground scenes loaded. Starting streamer.");
        waitForGroundCoroutine = null;
        StartStreaming();
    }

    bool AreAllGroundScenesLoaded()
    {
        if (groundSceneNames == null || groundSceneNames.Length == 0) return true;

        foreach (var name in groundSceneNames)
        {
            if (string.IsNullOrEmpty(name)) continue;
            var scene = SceneManager.GetSceneByName(name);
            if (!scene.isLoaded) return false;
        }
        return true;
    }

    // Called whenever a scene is loaded — refresh players and re-check ground scenes if necessary
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshPlayers();

        if (!streamingStarted && waitForGroundScenes)
        {
            if (AreAllGroundScenesLoaded())
            {
                Debug.Log("[SceneStreamer] Detected all ground scenes loaded via sceneLoaded event.");
                StartStreaming();
            }
        }
    }

    void OnSceneUnloaded(Scene scene)
    {
        // If a ground scene got unloaded and we require them, stop streaming to be safe.
        if (waitForGroundScenes && groundSceneNames != null && groundSceneNames.Contains(scene.name))
        {
            Debug.LogWarning($"[SceneStreamer] Ground scene '{scene.name}' was unloaded. Stopping streamer until ground is restored.");
            // Stop only our coroutines
            if (checkScenesCoroutine != null) { StopCoroutine(checkScenesCoroutine); checkScenesCoroutine = null; }
            if (waitForGroundCoroutine != null) { StopCoroutine(waitForGroundCoroutine); waitForGroundCoroutine = null; }
            streamingStarted = false;
            waitForGroundCoroutine = StartCoroutine(WaitForGroundScenesThenStart());
        }

        // Keep players list clean
        if (players != null && players.Length > 0)
        {
            var remaining = players.Where(p => p != null && p.gameObject.scene.isLoaded).ToArray();
            players = remaining;
        }

        // Remove destroyed transforms from freezeStates
        var toRemove = freezeStates.Keys.Where(t => t == null).ToList();
        foreach (var t in toRemove) freezeStates.Remove(t);
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
        }
    }

    public void UnregisterPlayer(Transform playerTransform)
    {
        if (playerTransform == null || players == null) return;
        players = players.Where(p => p != playerTransform).ToArray();
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
        while (true)
        {
            // Ensure players list remains valid (people may spawn/destroy)
            if (players == null || players.Length == 0)
                RefreshPlayers();

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

                    // Freeze players externally (only once for overlapping loads)
                    FreezePlayers(true);

                    loadingScenes.Add(sceneName);

                    var loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                    loadOp.completed += _ =>
                    {
                        // Remove from loading set
                        loadingScenes.Remove(sceneName);

                        // Only unfreeze when no other scenes are left loading
                        if (loadingScenes.Count == 0)
                        {
                            FreezePlayers(false);
                        }

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

    // Freeze/unfreeze implementation without modifying player's own script.
    void FreezePlayers(bool freeze)
    {
        if (players == null || players.Length == 0) return;

        foreach (var t in players)
        {
            if (t == null) continue;

            if (freeze)
            {
                if (freezeStates.ContainsKey(t)) continue; // already frozen

                var state = new PlayerFreezeState();

                // 1) Try to disable named movement components (by inspector-configured names)
                if (movementComponentNames != null)
                {
                    foreach (var compName in movementComponentNames)
                    {
                        if (string.IsNullOrEmpty(compName)) continue;
                        var comp = GetComponentByName(t, compName);
                        if (comp != null)
                        {
                            state.RegisterComponent(comp);
                            TryDisableComponent(comp);
                        }
                    }
                }

                // 2) Disable Behaviour-derived components that commonly control movement (safe generic check)
                var behaviours = t.GetComponents<Behaviour>();
                foreach (var b in behaviours)
                {
                    string n = b.GetType().Name.ToLower();
                    if (n.Contains("movement") || n.Contains("controller") || n.Contains("motor") || n.Contains("input"))
                    {
                        state.RegisterComponent(b);
                        b.enabled = false;
                    }
                }

                // 3) CharacterController
                var cc = t.GetComponent<CharacterController>();
                if (cc != null)
                {
                    state.characterControllerEnabled = cc.enabled;
                    cc.enabled = false;
                    state.hasCharacterController = true;
                }

                // 4) Rigidbody (3D)
                var rb = t.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    state.hasRigidbody = true;
                    state.rbIsKinematic = rb.isKinematic;
                    state.rbVelocity = rb.linearVelocity;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true; // stop physics
                }

                // 5) Rigidbody2D
                var rb2 = t.GetComponent<Rigidbody2D>();
                if (rb2 != null)
                {
                    state.hasRigidbody2D = true;
                    state.rb2dIsKinematic = rb2.isKinematic;
                    state.rb2dVelocity = rb2.linearVelocity;
                    rb2.linearVelocity = Vector2.zero;
                    rb2.angularVelocity = 0f;
                    rb2.isKinematic = true;
                }

                // 6) NavMeshAgent (if used)
#if ENABLE_NAVMESH
                var nav = t.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (nav != null)
                {
                    state.hasNavMeshAgent = true;
                    state.navAgentEnabled = nav.enabled;
                    state.navAgentIsStopped = nav.isStopped;
                    nav.isStopped = true;
                    nav.enabled = false;
                }
#endif

                freezeStates[t] = state;
            }
            else
            {
                // Unfreeze: restore state if we stored it
                if (!freezeStates.TryGetValue(t, out var state)) continue;

                // Restore registered Behaviour components
                foreach (var kv in state.componentEnabledStates)
                {
                    var comp = kv.Key;
                    bool wasEnabled = kv.Value;
                    if (comp == null) continue;
                    if (comp is Behaviour b)
                    {
                        b.enabled = wasEnabled;
                    }
                }

                // CharacterController
                if (state.hasCharacterController)
                {
                    var cc = t.GetComponent<CharacterController>();
                    if (cc != null)
                    {
                        cc.enabled = state.characterControllerEnabled;
                    }
                }

                // Rigidbody
                if (state.hasRigidbody)
                {
                    var rb = t.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = state.rbIsKinematic;
                        rb.linearVelocity = state.rbVelocity;
                    }
                }

                // Rigidbody2D
                if (state.hasRigidbody2D)
                {
                    var rb2 = t.GetComponent<Rigidbody2D>();
                    if (rb2 != null)
                    {
                        rb2.isKinematic = state.rb2dIsKinematic;
                        rb2.linearVelocity = state.rb2dVelocity;
                    }
                }

#if ENABLE_NAVMESH
                if (state.hasNavMeshAgent)
                {
                    var nav = t.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (nav != null)
                    {
                        nav.enabled = state.navAgentEnabled;
                        nav.isStopped = state.navAgentIsStopped;
                    }
                }
#endif

                freezeStates.Remove(t);
            }
        }
    }

    // Try to disable a component generically (behaviour or other)
    void TryDisableComponent(Component comp)
    {
        if (comp == null) return;
        if (comp is Behaviour b)
        {
            b.enabled = false;
            return;
        }

        // For other components that don't expose enabled, nothing safe we can do generically.
        // But we still registered them so we can attempt to restore if needed.
    }

    // Helper: get component by type name (safer than direct string GetComponent in many cases)
    Component GetComponentByName(Transform t, string typeName)
    {
        if (t == null || string.IsNullOrEmpty(typeName)) return null;

        // Try to resolve full type (works if user provided assembly-qualified or namespace-qualified names)
        var type = Type.GetType(typeName);
        if (type != null)
        {
            return t.GetComponent(type);
        }

        // Fallback: search behaviours for matching type name (case-insensitive)
        var allComps = t.GetComponents<Component>();
        foreach (var c in allComps)
        {
            if (c == null) continue;
            if (c.GetType().Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
                return c;
            // also check full name
            if (c.GetType().FullName != null && c.GetType().FullName.EndsWith("." + typeName, StringComparison.OrdinalIgnoreCase))
                return c;
        }

        return null;
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

    // Data class to capture previous states to restore later
    private class PlayerFreezeState
    {
        // store Component -> wasEnabled (for Behaviour components)
        public Dictionary<Component, bool> componentEnabledStates = new Dictionary<Component, bool>();

        public void RegisterComponent(Component comp)
        {
            if (comp == null) return;
            if (componentEnabledStates.ContainsKey(comp)) return;

            if (comp is Behaviour b)
                componentEnabledStates[comp] = b.enabled;
            else
                componentEnabledStates[comp] = false; // for non-Behaviour, we can't track enabled - default false
        }

        // CharacterController
        public bool hasCharacterController = false;
        public bool characterControllerEnabled = false;

        // Rigidbody (3D)
        public bool hasRigidbody = false;
        public bool rbIsKinematic = false;
        public Vector3 rbVelocity = Vector3.zero;

        // Rigidbody2D
        public bool hasRigidbody2D = false;
        public bool rb2dIsKinematic = false;
        public Vector2 rb2dVelocity = Vector2.zero;

#if ENABLE_NAVMESH
        // NavMeshAgent
        public bool hasNavMeshAgent = false;
        public bool navAgentEnabled = false;
        public bool navAgentIsStopped = false;
#endif
    }
}
