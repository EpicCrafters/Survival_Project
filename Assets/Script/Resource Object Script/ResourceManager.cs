// ResourceManager.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Lightweight runtime snapshot of a resource (for network/page sending).
/// Purpose: small, stable JSON shape clients can consume. DOES NOT replace SpawnRecord.
/// </summary>
[Serializable]
public class ResourceState
{
    public string uniqueId;
    public string prefabGuid;      // optional, helps client resolve prefab if needed
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
    public bool isChopped;

    public ResourceState() { }

    // Construct from your authoritative SpawnRecord
    public ResourceState(SpawnRecord r)
    {
        if (r == null) return;
        uniqueId = r.uniqueId;
        prefabGuid = r.prefabGuid;
        position = r.position;
        rotation = r.rotation;
        scale = r.scale;
        isChopped = r.isChopped;
    }

    // Optional: convert back to SpawnRecord (fills minimal fields)
    public SpawnRecord ToSpawnRecord()
    {
        return new SpawnRecord
        {
            uniqueId = uniqueId,
            prefabGuid = prefabGuid,
            prefabPath = null,              // not included in snapshot by design
            sceneName = null,              // snapshot may omit scene/chunk; fill if needed
            groundId = null,
            position = position,
            rotation = rotation,
            scale = scale,
            isChopped = isChopped
        };
    }
}

/// <summary>
/// One page of resource snapshots. Serialize with JsonUtility and send via TargetRpc.
/// </summary>
[Serializable]
public class ResourcePage
{
    public int pageIndex;
    public bool isLast;
    public List<ResourceState> resources = new List<ResourceState>();
}


public enum ResourceManagerRole { Standalone, Host, Client }
public enum ResourceChangeSource { Local, Network, ClientRequest, Preload, Unknown }

public class ResourceStateChangeEvent
{
    public string uniqueId;
    public bool previousState;
    public bool newState;
    public GameObject instance;
    public ResourceChangeSource source;
    public DateTime timestamp;

    public ResourceStateChangeEvent(string uniqueId, bool previousState, bool newState, GameObject instance, ResourceChangeSource source)
    {
        this.uniqueId = uniqueId;
        this.previousState = previousState;
        this.newState = newState;
        this.instance = instance;
        this.source = source;
        this.timestamp = DateTime.UtcNow;
    }
}

[DisallowMultipleComponent]
public class ResourceManager : MonoBehaviour, ISaveable
{
    // NOTE: removed destructive singleton behavior to support multiple per-scene managers.
    // Use per-scene registry and lookup helpers instead.

    // --- Registry for multi-manager setups ---
    private static readonly Dictionary<int, ResourceManager> managersBySceneHandle = new Dictionary<int, ResourceManager>();
    private static readonly object managersBySceneLock = new object();

    // Map uniqueId -> owning ResourceManager (global index for routing network requests)
    private static readonly Dictionary<string, ResourceManager> managerByUniqueId = new Dictionary<string, ResourceManager>();
    private static readonly object managerByUniqueIdLock = new object();

    /// <summary>
    /// Public static accessor. Returns any existing manager (first found) or null.
    /// Kept for backwards compatibility; prefer manager lookup APIs below for correctness.
    /// </summary>
    public static ResourceManager Instance
    {
        get
        {
            // Try registry first
            lock (managersBySceneLock)
            {
                if (managersBySceneHandle.Count > 0)
                    return managersBySceneHandle.Values.FirstOrDefault();
            }

            // Fallback: try scene search
            var found = FindObjectOfType<ResourceManager>();
            if (found == null)
            {
                Debug.LogWarning("[ResourceManager] Instance accessed but no ResourceManager present in scene.");
            }
            return found;
        }
    }

    /// <summary>
    /// Ensure an instance exists. If none found, create a runtime one.
    /// Use only when you want an automatic manager (e.g. in small games / bootstrap).
    /// This will create a standalone manager in the current active scene.
    /// </summary>
    public static ResourceManager GetOrCreateInstance(bool makePersistent = true)
    {
        var inst = Instance;
        if (inst != null) return inst;

        lock (managersBySceneLock)
        {
            inst = FindObjectOfType<ResourceManager>();
            if (inst != null) return inst;

            var go = new GameObject("ResourceManager");
            if (makePersistent) DontDestroyOnLoad(go);
            inst = go.AddComponent<ResourceManager>();
            Debug.Log("[ResourceManager] Runtime-created ResourceManager instance.");
            return inst;
        }
    }

    // Expose helper to get manager by uniqueId
    public static ResourceManager GetManagerForUniqueId(string uniqueId)
    {
        if (string.IsNullOrEmpty(uniqueId)) return null;
        lock (managerByUniqueIdLock)
        {
            managerByUniqueId.TryGetValue(uniqueId, out var rm);
            return rm;
        }
    }

    public static ResourceManager GetManagerForGameObject(GameObject go)
    {
        if (go == null) return null;
        return GetManagerForScene(go.scene);
    }

    public static ResourceManager GetManagerForScene(Scene s)
    {
        lock (managersBySceneLock)
        {
            managersBySceneHandle.TryGetValue(s.handle, out var rm);
            return rm;
        }
    }

    public static IEnumerable<ResourceManager> GetAllManagers()
    {
        lock (managersBySceneLock)
        {
            return managersBySceneHandle.Values.ToList();
        }
    }

    // Add at top of ResourceManager
    [Header("Debug (temporary)")]
    public Material debugFallbackMaterial; // assign an Unlit/Color material compatible with your pipeline

    [Header("Role")]
    public ResourceManagerRole role = ResourceManagerRole.Standalone;

    [Header("JSON Source (Host/Standalone only)")]
    public TextAsset spawnJsonAsset;
    public string spawnJsonPath; // if starting with "Assets/" resolves to project path

    [Header("Spawn")]
    public Transform spawnParent;
    public Transform exporterReference;
    public bool recordsAreLocalSpace = true; // If true and exporterReference == null, records are assumed LOCAL to spawnParent
    public bool spawnPlaceholderForMissingPrefabs = true;
    public bool clearSpawnParentOnLoad = true; // optional

    [Header("Startup control")]
    [Tooltip("When true, Start() will not auto-load; call LoadSnapshotAndSpawnImmediate() manually (useful with HostBootstrap).")]
    public bool deferLoadUntilManualStart = false;

    [Header("Persistence / Network")]
    public bool manualSaveOnly = true;
    public bool enableAutoSave = false;
    public float autoSaveIntervalSeconds = 300f;

    [Header("Network Paging")]
    [Tooltip("How many resource states per snapshot page when sending initial state to a client.")]
    public int snapshotPageSize = 64;

    [Header("Client-side performance tuning")]
    [Tooltip("Max milliseconds per frame to spend instantiating objects from a large snapshot on clients (0 disables).")]
    public float spawnTimeBudgetMs = 8f;
    [Tooltip("When client record count > this, use time-budgeted spawning.")]
    public int batchSpawnThreshold = 200;

    [Header("Debug")]
    public bool verboseLogs = true;

    [Header("Runtime Prefab Registry (for builds)")]
    public List<GameObject> runtimePrefabs = new List<GameObject>();
    private Dictionary<string, GameObject> runtimePrefabMap;

    // Core + adapters
    public ResourceManagerCore core;
    IResourcePersistence persistence;
    INetworkAdapter network;

    // Live instance map (unique id -> GameObject)
    private readonly Dictionary<string, GameObject> instancesById = new Dictionary<string, GameObject>();

    // Map for runtime destroyed/stump replacement prefabs keyed by uniqueId.
    private readonly Dictionary<string, GameObject> destroyedReplacementPrefabMap = new Dictionary<string, GameObject>();
    private readonly object destroyedReplacementMapLock = new object();

    // dirty flag
    public bool hasUnsavedChanges { get; private set; } = false;
    public event Action<bool> OnDirtyStateChanged;

    // Events for unified state-change handling
    public event Action<ResourceStateChangeEvent> OnResourceStateChanged;
    // When client requests a change; network adapter should forward to host
    public event Action<string, bool> OnClientResourceChangeRequested;

    // internals
    private Coroutine autoSaveCoroutine = null;

    // main-thread marshalling
    private readonly Queue<Action> mainThreadQueue = new Queue<Action>();
    private int mainThreadId;

    // pending-spawn guard (avoid duplicate spawn attempts)
    private readonly HashSet<string> pendingSpawns = new HashSet<string>();
    private readonly object pendingSpawnsLock = new object();

    // preloading flag (skip per-instance Mirror-enqueue during host preload)
    private bool isPreloading = false;

    // guard to avoid registering runtime prefabs multiple times
    private bool runtimePrefabsRegisteredWithMirror = false;

    // small helpers
    void LogV(string s) { /*if (verboseLogs) Debug.Log(s);*/ }
    void LogW(string s) { /*if (verboseLogs) Debug.LogWarning(s);*/ }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnDomainReload() { /* no-op placeholder in this file */ }

    [Header("Prefab Registry (ScriptableObject)")]
    public ResourcePrefabDatabase resourcePrefabDatabase;

    #region Unity lifecycle

    void Awake()
    {
        // Register this manager for its scene and proceed.
        mainThreadId = Thread.CurrentThread.ManagedThreadId;
        core = new ResourceManagerCore();

        // Build runtime prefab map (use names/paths as keys if needed).
        runtimePrefabMap = new Dictionary<string, GameObject>();
        if (runtimePrefabs != null)
        {
            foreach (var p in runtimePrefabs)
            {
                if (p == null) continue;
                var key = p.name;
                if (!runtimePrefabMap.ContainsKey(key)) runtimePrefabMap[key] = p;
            }
        }

        // adapters
        if (network == null)
        {
            var found = GetComponents<MonoBehaviour>().OfType<INetworkAdapter>().FirstOrDefault();
            if (found != null) network = found;
        }
        if (network == null) network = new NoNetworkAdapter();

        if (persistence == null)
        {
            if (role == ResourceManagerRole.Client) persistence = new NullPersistence();
            else persistence = new LocalJsonPersistence(spawnJsonAsset, spawnJsonPath, verboseLogs);
        }

        // wire core events
        Debug.Log($"[ResourceManager] Awake instanceID={this.GetInstanceID()}");
        core.OnSpawnRequested += SpawnRecordVisual;
        core.OnRecordChangedRaw += (id, state) => SetDirty(true);

        // wire network events
        network.OnChangeReceived += HandleNetworkChange;
        network.OnSnapshotReceived += HandleSnapshotReceived;

        // register runtime prefabs with Mirror early (best-effort)
        if (useMirrorRegistrationRecommended()) TryRegisterRuntimePrefabsWithMirror();

        // register this manager instance for its scene
        try
        {
            int handle = this.gameObject.scene.handle;
            lock (managersBySceneLock)
            {
                managersBySceneHandle[handle] = this;
            }
            LogV($"[ResourceManager] Registered manager '{name}' for scene '{this.gameObject.scene.name}' (handle={handle}).");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ResourceManager] Failed to register manager for scene: {ex}");
        }
    }

    // Replace the existing void Start() with this coroutine Start()
    IEnumerator Start()
    {
        // Re-attempt Mirror registration in case Mirror wasn't ready in Awake.
        if (useMirrorRegistrationRecommended()) TryRegisterRuntimePrefabsWithMirror();

        // Small, deterministic wait loop to avoid startup races in builds.
        // Wait until either:
        //  - runtimePrefabMap has entries (we have runtime prefabs)
        //  - runtimePrefabsRegisteredWithMirror == true (registration finished)
        //  - or until Mirror registration is not recommended (no network support)
        // Timeout after a bounded number of frames to avoid hanging startup.
        int maxFramesToWait = 30; // ~0.5s at 60fps — adjust if you need shorter/longer
        int waited = 0;
        while (waited < maxFramesToWait)
        {
            bool havePrefabs = (runtimePrefabMap != null && runtimePrefabMap.Count > 0);
            bool registrationDone = runtimePrefabsRegisteredWithMirror; // internal guard set by TryRegisterRuntimePrefabsWithMirror
            bool skipMirror = !useMirrorRegistrationRecommended();

            if (havePrefabs || registrationDone || skipMirror) break;

            // re-try registration once per frame in case Mirror finishes up
            if (useMirrorRegistrationRecommended()) TryRegisterRuntimePrefabsWithMirror();

            waited++;
            yield return null;
        }

        if (waited >= maxFramesToWait)
        {
            Debug.LogWarning($"[ResourceManager] Start: waited {maxFramesToWait} frames for prefab/registration readiness and timed out. runtimePrefabMap.count={(runtimePrefabMap != null ? runtimePrefabMap.Count : 0)} runtimePrefabsRegisteredWithMirror={runtimePrefabsRegisteredWithMirror}");
        }
        else
        {
            Debug.Log($"[ResourceManager] Start: readiness achieved after {waited} frames. runtimePrefabMap.count={(runtimePrefabMap != null ? runtimePrefabMap.Count : 0)} runtimePrefabsRegisteredWithMirror={runtimePrefabsRegisteredWithMirror}");
        }

        // Now proceed with the existing load/spawn logic (unchanged, just delayed)
        if (!deferLoadUntilManualStart && (role == ResourceManagerRole.Standalone || role == ResourceManagerRole.Host))
        {
            try
            {
                var snapshot = persistence.Load();
                core.LoadSnapshot(snapshot);

                // Host/Standalone: spawn synchronously (fast local spawn)
                // Clients are throttled in HandleSnapshotReceived
                core.RequestSpawnAll();
                Debug.Log($"[RM Start BEFORE RequestSpawnAll] runtimePrefabs count={(runtimePrefabs != null ? runtimePrefabs.Count : 0)} runtimePrefabMap.count={(runtimePrefabMap != null ? runtimePrefabMap.Count : 0)} runtimePrefabsRegisteredWithMirror={runtimePrefabsRegisteredWithMirror} instanceID={this.GetInstanceID()}");

                // If host and Mirror active, spawn network Identities now (if any)
                if (role == ResourceManagerRole.Host && useMirrorRegistrationRecommended() && NetworkServer.active)
                {
                    // Spawn networked objects immediately (small batches internally if needed)
                    StartCoroutine(MirrorSpawnRegisteredInstancesCoroutine(50));
                }

                SetDirty(false);
                LogV($"[ResourceManager] Loaded snapshot with {core.recordsById.Count} records.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ResourceManager] Failed to load snapshot: {ex}");
                try { core.LoadSnapshot(new SpawnRecordCollection()); } catch { }
            }
        }
        else if (role == ResourceManagerRole.Client)
        {
            // ask host for snapshot (network adapter implementation must handle this)
            try
            {
                network.RequestSnapshot();
                LogV("[ResourceManager] Client requested snapshot from host.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ResourceManager] Error requesting snapshot from network adapter: {ex}");
            }
        }

        // auto-save coroutine (only if not manual-only and not client)
        if (enableAutoSave && !manualSaveOnly && role != ResourceManagerRole.Client)
            autoSaveCoroutine = StartCoroutine(AutoSaveCoroutine());
    }


    void Update()
    {
        // Drain main-thread queue (fast)
        Action[] actions = null;
        lock (mainThreadQueue)
        {
            if (mainThreadQueue.Count > 0)
            {
                actions = mainThreadQueue.ToArray();
                mainThreadQueue.Clear();
            }
        }
        if (actions != null)
        {
            foreach (var a in actions)
            {
                try { a?.Invoke(); } catch (Exception ex) { Debug.LogError($"[ResourceManager] Exception in queued action: {ex}"); }
            }
        }
    }

    void OnDestroy()
    {
        try { if (autoSaveCoroutine != null) StopCoroutine(autoSaveCoroutine); } catch { }
        try { network.OnChangeReceived -= HandleNetworkChange; network.OnSnapshotReceived -= HandleSnapshotReceived; } catch { }
        try { core.OnSpawnRequested -= SpawnRecordVisual; } catch { }

        // remove manager registry entry
        try
        {
            int handle = this.gameObject.scene.handle;
            lock (managersBySceneLock)
            {
                if (managersBySceneHandle.TryGetValue(handle, out var existing) && existing == this)
                    managersBySceneHandle.Remove(handle);
            }
        }
        catch { }

        // remove any uniqueId -> this mappings owned by this manager
        try
        {
            lock (managerByUniqueIdLock)
            {
                var keysToRemove = managerByUniqueId.Where(kv => kv.Value == this).Select(kv => kv.Key).ToList();
                foreach (var k in keysToRemove) managerByUniqueId.Remove(k);
            }
        }
        catch { }
    }

    #endregion

    #region Public API / helpers

    // Decide whether registration with Mirror is meaningful (Mirror present + we intend to use Mirror)
    private bool useMirrorRegistrationRecommended()
    {
        return true; // keep true for projects using Mirror; adapter may be NoNetworkAdapter for singleplayer
    }

    public void SetNetworkAdapter(INetworkAdapter adapter)
    {
        if (network != null)
        {
            try { network.OnChangeReceived -= HandleNetworkChange; network.OnSnapshotReceived -= HandleSnapshotReceived; } catch { }
        }
        network = adapter ?? new NoNetworkAdapter();
        try { network.OnChangeReceived += HandleNetworkChange; network.OnSnapshotReceived += HandleSnapshotReceived; } catch { }
        LogV("[ResourceManager] Network adapter set.");
    }

    /// <summary>
    /// Load the snapshot and spawn everything immediately (synchronous). Useful when host wants to preload world
    /// before starting the network host. After calling this, call NetworkManager.StartHost() and then MirrorSpawnRegisteredInstances().
    /// </summary>
    public void LoadSnapshotAndSpawnImmediate()
    {
        if (role == ResourceManagerRole.Client)
        {
            Debug.LogWarning("[ResourceManager] LoadSnapshotAndSpawnImmediate called in Client role — use only on Host/Standalone.");
            return;
        }

        isPreloading = true;
        try
        {
            var snapshot = persistence.Load();
            core.LoadSnapshot(snapshot);

            // selective clear (optional)
            if (clearSpawnParentOnLoad && spawnParent != null)
            {
                for (int i = spawnParent.childCount - 1; i >= 0; i--)
                {
                    var child = spawnParent.GetChild(i)?.gameObject;
                    if (child == null) continue;
                    var rv = child.GetComponent<ResourceInstanceVisual>() ?? child.GetComponentInChildren<ResourceInstanceVisual>();
                    if (rv != null && !string.IsNullOrEmpty(rv.uniqueId) && core.recordsById.ContainsKey(rv.uniqueId))
                        continue;
#if UNITY_EDITOR
                    if (Application.isPlaying) Destroy(child);
                    else DestroyImmediate(child);
#else
                    Destroy(child);
#endif
                }
            }

            // spawn synchronously (fast)
            core.RequestSpawnAll();

            SetDirty(false);
            LogV("[ResourceManager] LoadSnapshotAndSpawnImmediate: spawn complete.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ResourceManager] LoadSnapshotAndSpawnImmediate failed: {ex}");
        }
        finally
        {
            isPreloading = false;
        }
    }

    /// <summary>
    /// After preloading on host, call this once NetworkServer.active == true to NetworkServer.Spawn the pre-instantiated objects.
    /// This coroutine does small yields to avoid a long single-frame hitch.
    /// </summary>
    public IEnumerator MirrorSpawnRegisteredInstancesCoroutine(int yieldEvery = 50)
    {
        while (!NetworkServer.active)
            yield return null;

        int spawned = 0;
        foreach (var kv in instancesById)
        {
            var go = kv.Value;
            if (go == null) continue;
            var ni = go.GetComponent<NetworkIdentity>();
            if (ni != null)
            {
                try { NetworkServer.Spawn(go); }
                catch (Exception ex) { Debug.LogWarning($"[ResourceManager] Mirror spawn failed for {go.name}: {ex.Message}"); }
                spawned++;
                if (yieldEvery > 0 && spawned % yieldEvery == 0) yield return null;
            }
        }
    }

    public void MirrorSpawnRegisteredInstances(int yieldEvery = 50)
    {
        StartCoroutine(MirrorSpawnRegisteredInstancesCoroutine(yieldEvery));
    }

    #endregion

    #region Network / Snapshot handlers

    // main-thread marshalling helper: only enqueue if caller is off main thread
    private void EnqueueOrExecute(Action action)
    {
        if (action == null) return;
        if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
        {
            lock (mainThreadQueue) mainThreadQueue.Enqueue(action);
        }
        else
        {
            action();
        }
    }

    void HandleNetworkChange(string uniqueId, bool isChopped)
    {
        EnqueueOrExecute(() => HandleNetworkChangeMainThread(uniqueId, isChopped));
    }

    // Network-originated changes use the centralized apply path
    void HandleNetworkChangeMainThread(string uniqueId, bool isChopped)
    {
        ApplyResourceStateChange(uniqueId, isChopped, ResourceChangeSource.Network);
    }

    void HandleSnapshotReceived(SpawnRecordCollection container)
    {
        EnqueueOrExecute(() => HandleSnapshotReceivedMainThread(container));
    }

    void HandleSnapshotReceivedMainThread(SpawnRecordCollection container)
    {
        LogV("[ResourceManager] HandleSnapshotReceivedMainThread start");

        try
        {
            core.LoadSnapshot(container);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ResourceManager] Failed to load snapshot container: {ex}");
            try { core.LoadSnapshot(new SpawnRecordCollection()); } catch { }
        }

        // selective clear spawnParent if configured
        if (clearSpawnParentOnLoad && spawnParent != null && !(role == ResourceManagerRole.Client && true /*clients may rely on Mirror spawn*/))
        {
            for (int i = spawnParent.childCount - 1; i >= 0; i--)
            {
                var child = spawnParent.GetChild(i)?.gameObject;
                if (child == null) continue;
                var rv = child.GetComponent<ResourceInstanceVisual>() ?? child.GetComponentInChildren<ResourceInstanceVisual>();
                if (rv != null && !string.IsNullOrEmpty(rv.uniqueId) && core.recordsById.ContainsKey(rv.uniqueId))
                    continue;
#if UNITY_EDITOR
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
#else
                Destroy(child);
#endif
            }
        }

        // Clear instance registry for this snapshot (clients using Mirror spawn may not want this)
        if (!(role == ResourceManagerRole.Client))
            instancesById.Clear();

        // Spawn behavior:
        // - Clients: throttle if snapshot large (prevent freeze)
        // - Host/Standalone: spawn immediately (fast)
        if (role == ResourceManagerRole.Client && core.recordsById.Count > batchSpawnThreshold && spawnTimeBudgetMs > 0f)
        {
            LogV($"[ResourceManager] Client will spawn {core.recordsById.Count} records using time budget {spawnTimeBudgetMs}ms/frame.");
            StartCoroutine(SpawnRecordsTimed(core.recordsById.Values, spawnTimeBudgetMs));
        }
        else
        {
            core.RequestSpawnAll();
        }

        SetDirty(false);
        LogV($"[ResourceManager] Snapshot received (records={core.recordsById.Count})");
    }

    // --- ADDED: Server-side helper to send initial state pages to a client connection ---
    // server-side: call from relay or NetworkManager
    // server-side: call from relay or NetworkManager
    // inside ResourceManager class
    // Convenience overload: keep existing callers working
    public void SendInitialStateTo(NetworkConnectionToClient conn, NetworkSnapshotRelay relay, string filterSceneName = null)
    {
        if (conn == null || relay == null)
        {
            Debug.LogWarning("[ResourceManager] SendInitialStateTo called with null conn or relay.");
            return;
        }

        // Build DTO list from SpawnRecord collection, filter by sceneName if provided
        var list = new List<ResourceState>();
        try
        {
            foreach (var kv in core.recordsById)
            {
                var r = kv.Value;
                if (!string.IsNullOrEmpty(filterSceneName))
                {
                    if (!string.Equals(r.sceneName, filterSceneName, StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                list.Add(new ResourceState(r));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ResourceManager] Error building resource list for snapshot: {ex}");
            var emptyErr = new ResourcePage { pageIndex = 0, isLast = true, resources = new List<ResourceState>() };
            relay.Target_SendSnapshotPage(conn, JsonUtility.ToJson(emptyErr));
            relay.Target_FinishSnapshot(conn);
            return;
        }

        // === CHANGED SECTION: send everything in one JSON ===
        var fullPage = new ResourcePage
        {
            pageIndex = 0,
            isLast = true,
            resources = list
        };

        string json = JsonUtility.ToJson(fullPage);

        try
        {
            relay.Target_SendSnapshotPage(conn, json);
            relay.Target_FinishSnapshot(conn);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ResourceManager] Failed to send snapshot: {ex}");
        }
    }

    #endregion

    #region Save / Persistence

    public void SaveNow()
    {
        if (role == ResourceManagerRole.Client)
        {
            Debug.LogWarning("[ResourceManager] Client should not call SaveNow(): request host to save if you want persistence.");
            try { network.RequestHostSave(); } catch (Exception ex) { Debug.LogWarning($"[ResourceManager] RequestHostSave failed: {ex}"); }
            return;
        }

        var container = core.GetSnapshot();
        DrainMainThreadQueueImmediately();
        try
        {
            persistence.Save(container);
            SetDirty(false);
            LogV("[ResourceManager] SaveNow completed.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ResourceManager] SaveNow failed: {ex}");
        }
    }

    void SetDirty(bool dirty)
    {
        if (hasUnsavedChanges == dirty) return;
        hasUnsavedChanges = dirty;
        OnDirtyStateChanged?.Invoke(hasUnsavedChanges);
    }

    IEnumerator AutoSaveCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(autoSaveIntervalSeconds);
            if (manualSaveOnly)
            {
                if (verboseLogs) Debug.Log("[ResourceManager] AutoSaveCoroutine stopping because manualSaveOnly=true");
                yield break;
            }
            if (hasUnsavedChanges && role != ResourceManagerRole.Client)
            {
                if (verboseLogs) Debug.Log("[ResourceManager] AutoSave triggered");
                SaveNow();
            }
        }
    }

    #endregion

    #region Spawn helpers

    // Timed spawning for clients: spawn across multiple frames to avoid freeze
    private IEnumerator SpawnRecordsTimed(IEnumerable<SpawnRecord> records, float budgetMsPerFrame)
    {
        if (budgetMsPerFrame <= 0f)
        {
            foreach (var r in records) SpawnRecordVisual(r);
            yield break;
        }

        float budgetSec = budgetMsPerFrame / 1000f;
        float frameStart = Time.realtimeSinceStartup;
        int processed = 0;
        foreach (var r in records)
        {
            SpawnRecordVisual(r);
            processed++;
            if (Time.realtimeSinceStartup - frameStart > budgetSec)
            {
                LogV($"[ResourceManager] SpawnRecordsTimed yielding after {processed} items this frame.");
                yield return null;
                frameStart = Time.realtimeSinceStartup;
                processed = 0;
            }
        }
    }

    void SpawnRecordVisual(SpawnRecord r)
    {
        //Debug.Log($"[SpawnRecordVisual START] uniqueId={r.uniqueId} prefabPath='{r.prefabPath}'");
        if (r == null) return;

        // Prevent duplicate spawn attempts for same id
        lock (pendingSpawnsLock)
        {
            if (pendingSpawns.Contains(r.uniqueId)) return;
            pendingSpawns.Add(r.uniqueId);
        }

        try
        {
            // If client relies on Mirror spawn, do not instantiate local world objects here.
            if (role == ResourceManagerRole.Client && useMirrorRegistrationRecommended())
            {
                LogV($"[ResourceManager] Client suppressed SpawnRecordVisual for {r.uniqueId} (Mirror-managed).");
                return;
            }

            LogV($"[RM] SpawnRecordVisual: id={r?.uniqueId} spawnParent={(spawnParent ? spawnParent.name : "NULL")} " +
                $"spawnParentScene={(spawnParent ? spawnParent.gameObject.scene.name : "<null>")} resourceManagerScene={gameObject.scene.name}");

            GameObject prefab = ResolvePrefabFromRecord(r);
            if (prefab == null) { LogW($"Prefab missing for record {r.uniqueId}"); }
            else
            {
                LogV($"Prefab EXIST!!? {r.uniqueId}");
            }
            bool recordIsLocalToParent = recordsAreLocalSpace && exporterReference == null && spawnParent != null;
            bool recordIsLocalToExporter = recordsAreLocalSpace && exporterReference != null;

            Vector3 desiredWorldPos = Vector3.zero;
            Quaternion desiredWorldRot = Quaternion.identity;
            Vector3 desiredWorldScale = Vector3.one;

            if (recordIsLocalToExporter)
            {
                desiredWorldPos = exporterReference.TransformPoint(r.position);
                desiredWorldRot = exporterReference.rotation * r.rotation;
                desiredWorldScale = Vector3.Scale(exporterReference.lossyScale, r.scale);
            }
            else if (!recordsAreLocalSpace)
            {
                desiredWorldPos = r.position;
                desiredWorldRot = r.rotation;
                desiredWorldScale = r.scale;
            }

            GameObject go = null;
            if (prefab != null)
            {
                if (recordIsLocalToParent)
                {
                    go = Instantiate(prefab, spawnParent);
                    //DebugLogSpawnedObject(prefab, r, prefab == null);
                    go.transform.localPosition = r.position;
                    go.transform.localRotation = r.rotation;
                    go.transform.localScale = r.scale;
                }
                else
                {
                    go = Instantiate(prefab, desiredWorldPos, desiredWorldRot);
                    //DebugLogSpawnedObject(prefab, r, prefab == null);
                    go.transform.localScale = desiredWorldScale;
                    if (spawnParent != null)
                    {
                        SceneManager.MoveGameObjectToScene(go, spawnParent.gameObject.scene);
                        go.transform.SetParent(spawnParent, true);
                    }
                    else go.transform.SetParent(null, true);
                }
            }
            else if (spawnPlaceholderForMissingPrefabs)
            {
                if (recordIsLocalToParent)
                {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    //DebugLogSpawnedObject(prefab, r, prefab == null);
                    cube.name = $"MISSING_PREFAB_{r.uniqueId}";
                    cube.transform.SetParent(spawnParent, false);
                    cube.transform.localPosition = r.position;
                    cube.transform.localRotation = r.rotation;
                    cube.transform.localScale = r.scale;
                    var col = cube.GetComponent<Collider>();
                    if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
                    go = cube;
                }
                else
                {
                    go = CreateMissingPrefabPlaceholder(r.uniqueId, desiredWorldPos, desiredWorldRot, desiredWorldScale);
                    //DebugLogSpawnedObject(prefab, r, prefab == null);
                    if (spawnParent != null)
                    {
                        SceneManager.MoveGameObjectToScene(go, spawnParent.gameObject.scene);
                        go.transform.SetParent(spawnParent, true);
                    }
                    else go.transform.SetParent(null, true);
                }
            }

            if (go != null)
            {
                // Attach or update the visual script
                var visual = go.GetComponent<ResourceInstanceVisual>() ?? go.AddComponent<ResourceInstanceVisual>();
                visual.SetUniqueId(r.uniqueId);
                visual.isChopped = r.isChopped;

                // If a runtime destroyed replacement was registered for this uniqueId, apply it to the visual now
                GameObject replacementPrefab = null;
                lock (destroyedReplacementMapLock)
                {
                    destroyedReplacementPrefabMap.TryGetValue(r.uniqueId, out replacementPrefab);
                }
                if (replacementPrefab != null)
                {
                    try { visual.SetDestroyedReplacementPrefab(replacementPrefab); }
                    catch (Exception ex) { Debug.LogWarning($"[ResourceManager] Visual.SetDestroyedReplacementPrefab threw: {ex.Message}"); }
                }

                visual.ApplyState();

                // Register with ResourceManager so it can be tracked
                try { RegisterInstance(visual); }
                catch (Exception ex) { Debug.LogWarning($"[SpawnRecordVisual] Failed to register {r.uniqueId}: {ex}"); }

                // Legacy fallback for BaseResource if you still need it
                var br = go.GetComponent<BaseResource>();
                if (br != null) br.SetUniqueId(r.uniqueId);

                // Register instance for offline-to-network sync (host batch MirrorSpawn)
                RegisterInstance(r.uniqueId, go);

                // Mirror spawn on host: either spawn immediately if not preloading or skip and batch later
                if (role == ResourceManagerRole.Host && useMirrorRegistrationRecommended() && NetworkServer.active)
                {
                    if (isPreloading)
                    {
                        // skip, we'll call MirrorSpawnRegisteredInstancesCoroutine after preload finishes
                    }
                    else
                    {
                        var ni = go.GetComponent<NetworkIdentity>();
                        if (ni != null)
                        {
                            try { NetworkServer.Spawn(go); }
                            catch (Exception ex) { LogW($"NetworkServer.Spawn failed for {go.name}: {ex.Message}"); }
                        }
                    }
                }
            }
        }
        finally
        {
            lock (pendingSpawnsLock) pendingSpawns.Remove(r.uniqueId);
        }
    }

    protected virtual GameObject ResolvePrefabFromRecord(SpawnRecord r)
    {
        if (r == null) return null;
        // 1. ScriptableObject registry lookup
        if (resourcePrefabDatabase != null)
        {
            var prefab = resourcePrefabDatabase.GetPrefab(r.prefabPath);
            //prefab = null;
            if (prefab != null) { return prefab; }
            //else Debug.LogError("Prefab NULL from getting from prefabPath");
        }

/*#if UNITY_EDITOR
        // 2. Editor-only AssetDatabase lookup
        string path = null;
        if (!string.IsNullOrEmpty(r.prefabGuid))
        {
            try { path = AssetDatabase.GUIDToAssetPath(r.prefabGuid); } catch { path = null; }
        }
        if (string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(r.prefabPath)) path = r.prefabPath;
        if (!string.IsNullOrEmpty(path))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null && verboseLogs)
                Debug.LogWarning($"[ResourceManager] Could not load prefab at path '{path}' for record {r.uniqueId}");
            return prefab;
        }
#endif*/

        // 3. Runtime-prefabs map
        if (!string.IsNullOrEmpty(r.prefabPath) && runtimePrefabMap != null && runtimePrefabMap.TryGetValue(r.prefabPath, out var p1))
            return p1;
        if (!string.IsNullOrEmpty(r.prefabGuid) && runtimePrefabMap != null && runtimePrefabMap.TryGetValue(r.prefabGuid, out var p2))
            return p2;

        // 4. Resources folder fallback
        if (!string.IsNullOrEmpty(r.prefabPath))
        {
            var loaded = Resources.Load<GameObject>(r.prefabPath);
            if (loaded != null) return loaded;
        }

        // 5. Nothing found
        return null;
    }


    GameObject CreateMissingPrefabPlaceholder(string uniqueId, Vector3 worldPos, Quaternion worldRot, Vector3 worldScale)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = $"MISSING_PREFAB_{uniqueId}";
        cube.transform.position = worldPos;
        cube.transform.rotation = worldRot;
        cube.transform.localScale = worldScale;
        var col = cube.GetComponent<Collider>();
        if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
        return cube;
    }

    #endregion

    #region Instance registry & centralized change API

    /// <summary>
    /// Gameplay/UI should call this to request a change.
    /// On clients this raises OnClientResourceChangeRequested which network adapter should forward to host.
    /// On host/standalone this applies directly (and host will broadcast).
    /// Thread-safe entry.
    /// </summary>
    public void RequestResourceStateChange(string uniqueId, bool isChopped)
    {
        if (string.IsNullOrEmpty(uniqueId)) return;

        if (role == ResourceManagerRole.Client)
        {
            // Clients must ask host to change authoritative state
            LogV($"[ResourceManager] Client requests change: {uniqueId} -> {isChopped}");
            try { OnClientResourceChangeRequested?.Invoke(uniqueId, isChopped); }
            catch (Exception ex) { Debug.LogError($"[ResourceManager] Exception in OnClientResourceChangeRequested handlers: {ex}"); }
            return;
        }

        // Host/Standalone: apply immediately on main thread
        ApplyResourceStateChange(uniqueId, isChopped, ResourceChangeSource.Local);
    }

    /// <summary>
    /// Apply a resource state change (safe to call from any thread).
    /// Use source to indicate origin; host-local changes will be broadcasted.
    /// </summary>
    public void ApplyResourceStateChange(string uniqueId, bool isChopped, ResourceChangeSource source = ResourceChangeSource.Local)
    {
        EnqueueOrExecute(() => ApplyResourceStateChangeMainThread(uniqueId, isChopped, source));
    }

    private void ApplyResourceStateChangeMainThread(string uniqueId, bool isChopped, ResourceChangeSource source)
    {
        if (string.IsNullOrEmpty(uniqueId)) return;

        SpawnRecord rec = null;
        if (core.recordsById != null && core.recordsById.TryGetValue(uniqueId, out rec))
        {
            bool previousState = rec.isChopped;

            // Update core authoritative data depending on role
            if (role == ResourceManagerRole.Host)
            {
                core.ApplyLocalChange(uniqueId, isChopped);
                SetDirty(true);
            }
            else if (role == ResourceManagerRole.Client)
            {
                core.ApplyAuthorityChange(uniqueId, isChopped);
            }
            else // Standalone
            {
                core.ApplyLocalChange(uniqueId, isChopped);
                SetDirty(true);
            }

            // Update visual instance if present
            GameObject instanceGo = null;
            if (instancesById.TryGetValue(uniqueId, out instanceGo) && instanceGo != null)
            {
                var inst = instanceGo.GetComponent<ResourceInstanceVisual>() ?? instanceGo.GetComponentInChildren<ResourceInstanceVisual>();
                if (inst != null)
                {
                    inst.isChopped = isChopped;
                    inst.ApplyState();
                }
            }

            // Host-origin local changes should be broadcast to clients
            if (role == ResourceManagerRole.Host && source == ResourceChangeSource.Local)
            {
                try { network.BroadcastChange(uniqueId, isChopped); }
                catch (Exception ex) { LogW($"[ResourceManager] BroadcastChange failed for {uniqueId}: {ex.Message}"); }
            }

            // Fire unified event for other systems
            var evt = new ResourceStateChangeEvent(uniqueId, previousState, isChopped, instanceGo, source);
            try { OnResourceStateChanged?.Invoke(evt); }
            catch (Exception ex) { Debug.LogError($"[ResourceManager] Exception in OnResourceStateChanged handlers: {ex}"); }
        }
        else
        {
            LogW($"[ResourceManager] ApplyResourceStateChange: unknown id {uniqueId}");
        }
    }

    // Safer RegisterInstance that stores GameObject by unique id
    private void RegisterInstance(string uniqueId, GameObject go)
    {
        if (string.IsNullOrEmpty(uniqueId) || go == null) return;
        lock (instancesById)
        {
            if (instancesById.TryGetValue(uniqueId, out var existing) && existing != null && existing != go)
            {
                LogW($"[ResourceManager] RegisterInstance replacing existing instance for id {uniqueId} (old={existing.name}, new={go.name})");
            }
            instancesById[uniqueId] = go;
        }

        // register global ownership mapping so server can find the right manager by uniqueId
        lock (managerByUniqueIdLock)
        {
            if (managerByUniqueId.TryGetValue(uniqueId, out var existingManager) && existingManager != this)
            {
                LogW($"[ResourceManager] uniqueId {uniqueId} already registered to manager '{existingManager.name}'. Overwriting to '{this.name}'.");
            }
            managerByUniqueId[uniqueId] = this;
        }
    }

    // Overload used by SpawnRecordVisual (and existing call sites that pass ResourceInstanceVisual)
    public void RegisterInstance(ResourceInstanceVisual visual)
    {
        if (visual == null) return;
        if (string.IsNullOrEmpty(visual.uniqueId))
        {
            LogW("[ResourceManager] RegisterInstance(ResourceInstanceVisual) called with empty uniqueId.");
            return;
        }
        RegisterInstance(visual.uniqueId, visual.gameObject);
    }

    public void UnregisterInstance(string uniqueId)
    {
        if (string.IsNullOrEmpty(uniqueId)) return;
        lock (instancesById)
        {
            instancesById.Remove(uniqueId);
        }

        lock (managerByUniqueIdLock)
        {
            if (managerByUniqueId.TryGetValue(uniqueId, out var owner) && owner == this) managerByUniqueId.Remove(uniqueId);
        }
    }

    public void UnregisterInstance(ResourceInstanceVisual visual)
    {
        if (visual == null) return;
        UnregisterInstance(visual.uniqueId);
    }

    public bool TryGetInstance(string uniqueId, out GameObject inst)
    {
        inst = null;
        if (string.IsNullOrEmpty(uniqueId)) return false;
        return instancesById.TryGetValue(uniqueId, out inst) && inst != null;
    }

    /// <summary>
    /// Public API: set or clear a runtime destroyed/stump replacement prefab for a particular uniqueId.
    /// - If prefab is non-null, it will be stored and applied to the visual now (if present) and to any future spawn of that id.
    /// - If prefab is null, the stored override is removed.
    /// Thread-safe entry: will run on main thread.
    /// </summary>
    public void SetDestroyedReplacementPrefab(string uniqueId, GameObject prefab)
    {
        if (string.IsNullOrEmpty(uniqueId)) return;

        EnqueueOrExecute(() =>
        {
            lock (destroyedReplacementMapLock)
            {
                if (prefab == null)
                {
                    if (destroyedReplacementPrefabMap.ContainsKey(uniqueId))
                        destroyedReplacementPrefabMap.Remove(uniqueId);
                }
                else
                {
                    destroyedReplacementPrefabMap[uniqueId] = prefab;
                }
            }

            // If an instance is already present, apply immediately
            if (instancesById.TryGetValue(uniqueId, out var go) && go != null)
            {
                var vis = go.GetComponent<ResourceInstanceVisual>();
                if (vis != null)
                {
                    try { vis.SetDestroyedReplacementPrefab(prefab); }
                    catch (Exception ex) { Debug.LogWarning($"[ResourceManager] SetDestroyedReplacementPrefab failed on visual: {ex.Message}"); }
                }
            }
        });
    }

    #endregion

    #region Mirror prefab registration helper

    private void TryRegisterRuntimePrefabsWithMirror()
    {
        if (runtimePrefabsRegisteredWithMirror) return;
        if (runtimePrefabs == null || runtimePrefabs.Count == 0) { runtimePrefabsRegisteredWithMirror = true; return; }

        int registered = 0;
        foreach (var p in runtimePrefabs)
        {
            if (p == null) continue;
            try
            {
                var ni = p.GetComponent<NetworkIdentity>();
                if (ni == null) { LogW($"[ResourceManager] Prefab '{p.name}' has no NetworkIdentity; Mirror registration skipped."); continue; }
                NetworkClient.RegisterPrefab(p);
                registered++;
            }
            catch (Exception ex) { LogW($"[ResourceManager] Failed to register prefab '{p.name}' with Mirror: {ex.Message}"); }
        }
        runtimePrefabsRegisteredWithMirror = true;
        LogV($"[ResourceManager] Registered {registered} runtime prefabs with Mirror.");
    }

    #endregion

    private void DrainMainThreadQueueImmediately()
    {
        Action[] actions = null;
        lock (mainThreadQueue)
        {
            if (mainThreadQueue.Count > 0)
            {
                actions = mainThreadQueue.ToArray();
                mainThreadQueue.Clear();
            }
        }
        if (actions != null)
        {
            foreach (var a in actions)
            {
                try { a?.Invoke(); } catch (Exception ex) { Debug.LogError($"[ResourceManager] Exception in queued action while draining before save: {ex}"); }
            }
        }
    }

    // Replace or call this after you instantiate / create placeholder
    void DebugLogSpawnedObject(GameObject spawned, SpawnRecord record, bool createdAsPlaceholder)
    {
        if (spawned == null)
        {
            Debug.LogError($"ResourceManager DEBUG: Spawn returned NULL for record {record?.uniqueId}. createdAsPlaceholder={createdAsPlaceholder}");
            return;
        }

        // Basic identity
        string id = record != null ? record.uniqueId : "<no-record>";
        Debug.Log($"ResourceManager DEBUG: Spawned GameObject name='{spawned.name}' for record={id} placeholder={createdAsPlaceholder}");

        // Check if it's a primitive cube (fast heuristic)
        var mf = spawned.GetComponent<MeshFilter>();
        bool isPrimitiveCube = false;
        if (mf != null && mf.sharedMesh != null && mf.sharedMesh.name.ToLower().Contains("cube"))
        {
            isPrimitiveCube = true;
        }
        Debug.Log($" - Has MeshFilter: {(mf != null ? "yes" : "no")}, Mesh name: {(mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "<null>")}, primitiveCube={isPrimitiveCube}");

        // MeshRenderer / SkinnedMeshRenderer
        var mr = spawned.GetComponent<MeshRenderer>();
        var smr = spawned.GetComponent<UnityEngine.SkinnedMeshRenderer>();
        if (mr != null)
        {
            Debug.Log($" - MeshRenderer found. materials count = {mr.sharedMaterials?.Length ?? 0}");
            for (int i = 0; i < (mr.sharedMaterials?.Length ?? 0); ++i)
            {
                var mat = mr.sharedMaterials[i];
                Debug.Log($"   - Material[{i}] = {(mat != null ? mat.name : "<null>")}");
                if (mat != null && mat.shader != null)
                {
                    Debug.Log($"     - shader = {mat.shader.name}, isSupported = {mat.shader.isSupported}");
                }
                else Debug.Log($"     - shader = <null>");
            }
        }
        else if (smr != null)
        {
            Debug.Log($" - SkinnedMeshRenderer found. materials count = {smr.sharedMaterials?.Length ?? 0}");
            for (int i = 0; i < (smr.sharedMaterials?.Length ?? 0); ++i)
            {
                var mat = smr.sharedMaterials[i];
                Debug.Log($"   - Material[{i}] = {(mat != null ? mat.name : "<null>")}");
                if (mat != null && mat.shader != null)
                {
                    Debug.Log($"     - shader = {mat.shader.name}, isSupported = {mat.shader.isSupported}");
                }
                else Debug.Log($"     - shader = <null>");
            }
        }
        else
        {
            Debug.Log($" - No MeshRenderer / SkinnedMeshRenderer on root. Checking children...");
            foreach (var r in spawned.GetComponentsInChildren<Renderer>(true))
            {
                Debug.Log($"   Child renderer: name='{r.gameObject.name}' type={r.GetType().Name} materials={(r.sharedMaterials?.Length ?? 0)}");
                for (int i = 0; i < (r.sharedMaterials?.Length ?? 0); ++i)
                {
                    var mat = r.sharedMaterials[i];
                    Debug.Log($"     - Material[{i}] = {(mat != null ? mat.name : "<null>")}");
                    if (mat != null && mat.shader != null) Debug.Log($"       shader={mat.shader.name}, isSupported={mat.shader.isSupported}");
                    else Debug.Log($"       shader=<null>");
                }
            }
        }

        // If this *is* a primitive cube OR materials are null/unsupported, apply debugFallbackMaterial (to avoid magenta)
        bool materialsProblem = false;
        // consider materialsProblem true if any renderer has a null material or shader.isSupported == false
        foreach (var r in spawned.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0) { materialsProblem = true; break; }
            foreach (var m in mats)
            {
                if (m == null) { materialsProblem = true; break; }
                if (m.shader == null || !m.shader.isSupported) { materialsProblem = true; break; }
            }
            if (materialsProblem) break;
        }

        if (isPrimitiveCube || materialsProblem)
        {
            Debug.LogWarning($"ResourceManager DEBUG: Spawned object appears to be a placeholder or has material/shader problems. isCube={isPrimitiveCube} materialsProblem={materialsProblem}");
            if (debugFallbackMaterial != null)
            {
                foreach (var r in spawned.GetComponentsInChildren<Renderer>(true))
                {
                    // Use sharedMaterial so Unity includes the asset in builds and you don't create instance GC churn.
                    var arr = r.sharedMaterials;
                    for (int i = 0; i < arr.Length; ++i) arr[i] = debugFallbackMaterial;
                    r.sharedMaterials = arr;
                }
                Debug.Log("ResourceManager DEBUG: Applied debugFallbackMaterial to spawned object to avoid magenta.");
            }
        }

        // Extra: log active scene and ResourceManager instance id to ensure you are looking at the right manager
        Debug.Log($"ResourceManager DEBUG: activeScene='{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}' ResourceManager.instanceID={this.GetInstanceID()}");
    }

    public bool HasUnsavedChanges => hasUnsavedChanges;
    public string SaveableName => gameObject.name;
    public string SceneName => gameObject.scene.name;

    private void OnEnable()
    {
        SaveManager.Instance?.Register(this);
        // hoặc nếu bạn muốn đảm bảo register sau Awake của SaveManager, có thể Start() check
    }

    private void OnDisable()
    {
        SaveManager.Instance?.Unregister(this);
    }
}
