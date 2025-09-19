using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ResourceManagerOffline
/// - Keeps spawn records in memory and spawns them in the scene.
/// - DOES NOT save automatically on object destroy/chop when manualSaveOnly is true.
/// - Only writes to disk when SaveNow() is explicitly called (or SaveToJsonImmediate(force:true)).
/// - Minimal, defensive, drop-in replacement for the offline manager.
/// </summary>
[DisallowMultipleComponent]
public class ResourceManagerOffline : MonoBehaviour
{
    #region Inspector fields

    [Header("JSON Source (choose one)")]
    [Tooltip("Drag the exported expanded JSON file (TextAsset) here for convenience.")]
    public TextAsset spawnJsonAsset;

    [Tooltip("If not using TextAsset: path to file. If starts with 'Assets/' it will be resolved to project path.")]
    public string spawnJsonPath;

    [Header("Spawn / Parenting")]
    [Tooltip("Parent transform to attach spawned objects under (your landscape). If null, objects are root-level.")]
    public Transform spawnParent;

    [Tooltip("If JSON records were exported local to a chunk, set exporterReference to that chunk transform so we can convert to world on load.")]
    public Transform exporterReference;

    [Tooltip("If true, the records in JSON are LOCAL to the exporterReference (or original spawner). If false, they are world-space.")]
    public bool recordsAreLocalSpace = true;

    [Tooltip("If true, use a placeholder GameObject when a recorded prefab cannot be resolved.")]
    public bool spawnPlaceholderForMissingPrefabs = true;

    [Tooltip("If true the manager will clear existing children under spawnParent before loading.")]
    public bool clearSpawnParentOnLoad = true;

    [Header("Save Mode")]
    [Tooltip("If true, writing to disk only happens when SaveNow() is explicitly called (Save button).")]
    public bool manualSaveOnly = true;

    [Tooltip("If true, automatically save unsaved changes on quit (only honored when manualSaveOnly == false).")]
    public bool autoSaveOnQuit = false;

    [Tooltip("Enable periodic auto-save (only honored when manualSaveOnly == false).")]
    public bool enableAutoSave = false;

    [Tooltip("Interval in seconds between auto-save checks when enabled.")]
    public float autoSaveIntervalSeconds = 300f;

    [Header("Debounce / Legacy")]
    [Tooltip("If true, coalesce frequent saves and write after 'saveDebounceSeconds'. Useful when autosave is enabled.")]
    public bool useDebouncedSave = true;

    [Tooltip("Seconds to wait before writing a debounced save.")]
    public float saveDebounceSeconds = 0.5f;

    [Header("Behavior / Diagnostics")]
    public bool verboseLogs = true;

    [Header("Load Behavior")]
    [Tooltip("Auto-load JSON on Start()")]
    public bool autoLoad = true;

    #endregion

    #region Runtime state & events

    // Singleton-ish convenience (not strict enforced).
    public static ResourceManagerOffline Instance { get; private set; }

    [Serializable]
    public class SpawnRecord
    {
        public string uniqueId;
        public string prefabGuid;   // editor GUID (optional)
        public string prefabPath;   // asset path (optional)
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale = Vector3.one;
        public bool isChopped;
    }

    [Serializable]
    public class SpawnRecordCollection
    {
        public int schemaVersion = 1;
        public List<SpawnRecord> records = new List<SpawnRecord>();
    }

    // in-memory store
    public Dictionary<string, SpawnRecord> recordsById = new Dictionary<string, SpawnRecord>();

    // save coroutines & autosave coroutine
    private Coroutine saveCoroutine = null;
    private Coroutine autoSaveCoroutine = null;

    // dirty flag + event
    public bool hasUnsavedChanges { get; private set; } = false;
    public event Action<bool> OnDirtyStateChanged;

    // event hooks for external systems
    public event Action<SpawnRecord> OnRecordChanged;
    public event Action<GameObject, SpawnRecord> OnSpawned;

    #endregion

    #region Unity lifecycle

    void Awake()
    {
        // basic singleton handling (non-persistent)
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ResourceManagerOffline] Multiple instances detected; destroying duplicate.");
            Destroy(this);
            return;
        }
        Instance = this;

        if (verboseLogs) Debug.Log("[ResourceManagerOffline] Awake");
    }

    void Start()
    {
        if (autoLoad)
        {
            if (verboseLogs) Debug.Log("[ResourceManagerOffline] Loading JSON at Start.");
            LoadFromJson();
        }

        // Defensive: if manualSaveOnly is enabled, don't start autosave.
        if (enableAutoSave)
        {
            if (manualSaveOnly)
            {
                if (verboseLogs) Debug.Log("[ResourceManagerOffline] enableAutoSave requested but manualSaveOnly=true — autosave will not start.");
            }
            else
            {
                autoSaveCoroutine = StartCoroutine(AutoSaveCoroutine());
                if (verboseLogs) Debug.Log("[ResourceManagerOffline] AutoSave coroutine started.");
            }
        }

        // clear any pending save coroutine if in manual mode (defensive)
        if (manualSaveOnly && saveCoroutine != null)
        {
            StopCoroutine(saveCoroutine);
            saveCoroutine = null;
            if (verboseLogs) Debug.Log("[ResourceManagerOffline] Cancelled pending save coroutine due to manualSaveOnly=true.");
        }
    }

    void OnDestroy()
    {
        if (autoSaveCoroutine != null) StopCoroutine(autoSaveCoroutine);
        if (saveCoroutine != null) StopCoroutine(saveCoroutine);
    }

    void OnApplicationQuit()
    {
        if (manualSaveOnly)
        {
            if (verboseLogs) Debug.Log("[ResourceManagerOffline] ApplicationQuit: manualSaveOnly=true, skipping automatic quit save.");
            return;
        }

        if (hasUnsavedChanges && autoSaveOnQuit)
        {
            if (verboseLogs) Debug.Log("[ResourceManagerOffline] OnApplicationQuit - auto-saving unsaved changes.");
            SaveNow();
        }
    }

    #endregion

    #region Load

    [ContextMenu("LoadFromJson (ResourceManagerOffline)")]
    public void LoadFromJson()
    {
        string resolvedPath = ResolveFullPath();
        if (verboseLogs) Debug.Log($"[ResourceManagerOffline] ResolveFullPath -> {resolvedPath}");

        string json = null;
#if UNITY_EDITOR
        if (spawnJsonAsset != null)
        {
            json = spawnJsonAsset.text;
            if (verboseLogs) Debug.Log("[ResourceManagerOffline] Using spawnJsonAsset.text for load.");
        }
#endif

        if (string.IsNullOrEmpty(json))
        {
            if (!File.Exists(resolvedPath))
            {
                Debug.LogWarning($"[ResourceManagerOffline] Spawn JSON not found at: {resolvedPath}\nDrag file into spawnJsonAsset or set spawnJsonPath to the asset path (Assets/SpawnData/spawn_expanded.json).");
                return;
            }
            try { json = File.ReadAllText(resolvedPath); }
            catch (Exception ex) { Debug.LogError("[ResourceManagerOffline] Failed to read spawn JSON: " + ex.Message); return; }
        }

        var container = JsonUtility.FromJson<SpawnRecordCollection>(json);
        if (container == null || container.records == null || container.records.Count == 0)
        {
            if (!string.IsNullOrEmpty(json) && json.Contains("\"instances\"") && json.Contains("\"prefabs\""))
            {
                Debug.LogWarning("[ResourceManagerOffline] Detected compact grouped JSON (prefabs + instances). Re-export with expanded format (records array) or convert externally.");
            }
            else
            {
                Debug.LogWarning("[ResourceManagerOffline] JSON parsed but no records were found. Preview:\n" + (json.Length > 256 ? json.Substring(0, 256) + "..." : json));
            }
            return;
        }

        // clear existing children if requested
        if (spawnParent != null && clearSpawnParentOnLoad)
        {
            for (int i = spawnParent.childCount - 1; i >= 0; i--)
            {
                var child = spawnParent.GetChild(i).gameObject;
#if UNITY_EDITOR
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
#else
                Destroy(child);
#endif
            }
        }

        // populate in-memory map and spawn
        recordsById.Clear();
        int spawned = 0;
        foreach (var r in container.records)
        {
            if (string.IsNullOrEmpty(r.uniqueId))
            {
                Debug.LogWarning("[ResourceManagerOffline] Skipping record with missing uniqueId.");
                continue;
            }

            recordsById[r.uniqueId] = r;

            // attempt prefab resolution (editor-only GUID/path; in builds this will be null -> placeholder)
            GameObject prefab = ResolvePrefabFromRecord(r);

            // compute world transform
            Vector3 desiredWorldPos;
            Quaternion desiredWorldRot;
            Vector3 desiredWorldScale;

            if (recordsAreLocalSpace && exporterReference != null)
            {
                desiredWorldPos = exporterReference.TransformPoint(r.position);
                desiredWorldRot = exporterReference.rotation * r.rotation;
                desiredWorldScale = Vector3.Scale(exporterReference.lossyScale, r.scale);
            }
            else
            {
                desiredWorldPos = r.position;
                desiredWorldRot = r.rotation;
                desiredWorldScale = r.scale;
            }

            GameObject go = null;

            if (prefab != null)
            {
                go = Instantiate(prefab);
                ApplyWorldTransform(go.transform, desiredWorldPos, desiredWorldRot, desiredWorldScale, spawnParent);
            }
            else
            {
                if (spawnPlaceholderForMissingPrefabs)
                {
                    go = CreateMissingPrefabPlaceholder(r.uniqueId, desiredWorldPos, desiredWorldRot, desiredWorldScale);
                    ApplyWorldTransform(go.transform, desiredWorldPos, desiredWorldRot, desiredWorldScale, spawnParent);
                    if (verboseLogs) Debug.LogWarning($"[ResourceManagerOffline] Spawned placeholder for missing prefab for record {r.uniqueId}");
                }
                else
                {
                    if (verboseLogs) Debug.LogWarning($"[ResourceManagerOffline] Skipping spawn for record {r.uniqueId} because prefab wasn't found.");
                    continue;
                }
            }

            // attach runtime instance binding
            var inst = go.GetComponent<ResourceInstanceOffline>() ?? go.AddComponent<ResourceInstanceOffline>();
            inst.uniqueId = r.uniqueId;
            inst.isChopped = r.isChopped;
            inst.ApplyState();

            var br = go.GetComponent<BaseResource>();
            if (br != null) br.SetUniqueId(r.uniqueId);

            spawned++;
            OnSpawned?.Invoke(go, r);
        }

        Debug.Log($"[ResourceManagerOffline] Loaded {container.records.Count} records; spawned {spawned} objects. (resolvedPath={resolvedPath})");

        // loaded state is authoritative -> clear dirty
        SetDirty(false);
    }

    GameObject ResolvePrefabFromRecord(SpawnRecord r)
    {
#if UNITY_EDITOR
        string path = null;
        if (!string.IsNullOrEmpty(r.prefabGuid))
        {
            try { path = AssetDatabase.GUIDToAssetPath(r.prefabGuid); }
            catch { path = null; }
        }
        if (string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(r.prefabPath))
            path = r.prefabPath;

        if (!string.IsNullOrEmpty(path))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null && verboseLogs) Debug.LogWarning($"[ResourceManagerOffline] Could not load prefab at path '{path}' for record {r.uniqueId}");
            return prefab;
        }
#endif
        return null; // nothing found or in build
    }

    GameObject CreateMissingPrefabPlaceholder(string uniqueId, Vector3 worldPos, Quaternion worldRot, Vector3 worldScale)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = $"MISSING_PREFAB_{uniqueId}";
        cube.transform.position = worldPos;
        cube.transform.rotation = worldRot;
        cube.transform.localScale = worldScale;

        var col = cube.GetComponent<Collider>();
        if (col != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(col);
#else
            Destroy(col);
#endif
        }

        var inst = cube.AddComponent<ResourceInstanceOffline>();
        inst.uniqueId = uniqueId;
        inst.isChopped = false;
        inst.ApplyState();

        return cube;
    }

    #endregion

    #region Save / Persistence (manual-only friendly)

    /// <summary>
    /// Called by ResourceInstanceOffline (or other systems) to update state in memory only.
    /// This marks data dirty but does NOT persist to disk in manualSaveOnly mode.
    /// </summary>
    public void OnResourceStateChanged(string uniqueId, bool isChopped)
    {
        if (!recordsById.TryGetValue(uniqueId, out var rec))
        {
            Debug.LogWarning("[ResourceManagerOffline] state changed for unknown id: " + uniqueId);
            return;
        }

        rec.isChopped = isChopped;
        SetDirty(true);

        if (verboseLogs) Debug.Log($"[ResourceManagerOffline] Marked {uniqueId} dirty (isChopped={isChopped}).");

        // IMPORTANT: do NOT auto-save here in manual mode.
        // If manualSaveOnly==false and useDebouncedSave==true, ScheduleSave would allow autosave behavior,
        // but if manualSaveOnly==true we intentionally do nothing.
        if (!manualSaveOnly && useDebouncedSave)
        {
            ScheduleSave(saveDebounceSeconds);
        }

        OnRecordChanged?.Invoke(rec);
    }

    /// <summary>
    /// Immediate (atomic) save to disk. Guarded by manualSaveOnly unless 'force' is true.
    /// </summary>
    [ContextMenu("SaveToJsonImmediate (ResourceManagerOffline)")]
    public void SaveToJsonImmediate(bool force = false)
    {
        if (manualSaveOnly && !force)
        {
            if (verboseLogs) Debug.Log("[ResourceManagerOffline] Skipping SaveToJsonImmediate because manualSaveOnly=true and force=false.");
            return;
        }

        if (verboseLogs)
        {
            // small helpful trace so you can identify unexpected callers during testing
            Debug.Log($"[ResourceManagerOffline] SaveToJsonImmediate called (force={force}). Stack:\n{System.Environment.StackTrace}");
        }

        string resolvedPath = ResolveFullPath();
        var container = new SpawnRecordCollection { records = new List<SpawnRecord>(recordsById.Values) };
        string outJson = JsonUtility.ToJson(container, true);

        try
        {
            var dir = Path.GetDirectoryName(resolvedPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string tmp = resolvedPath + ".tmp";
            File.WriteAllText(tmp, outJson);

            // Replace the original atomically (best-effort)
            if (File.Exists(resolvedPath)) File.Delete(resolvedPath);
            File.Move(tmp, resolvedPath);

#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
            SetDirty(false);
            if (verboseLogs) Debug.Log("[ResourceManagerOffline] Saved spawn JSON to: " + resolvedPath);
        }
        catch (Exception ex)
        {
            Debug.LogError("[ResourceManagerOffline] Failed to save spawn JSON atomically: " + ex.Message);
            // Fallback: try non-atomic write
            try { File.WriteAllText(resolvedPath, outJson); SetDirty(false); }
            catch (Exception ex2) { Debug.LogError("[ResourceManagerOffline] Fallback write failed: " + ex2.Message); }
        }
    }

    /// <summary>
    /// Save now (explicit API typically called by UI Save button).
    /// This forces a save even if manualSaveOnly==true.
    /// </summary>
    public void SaveNow()
    {
        SaveToJsonImmediate(force: true);
    }

    /// <summary>
    /// Debounced save helper (disabled in manualSaveOnly mode).
    /// </summary>
    public void ScheduleSave(float delaySeconds = 0.5f)
    {
        if (manualSaveOnly)
        {
            if (verboseLogs) Debug.Log("[ResourceManagerOffline] ScheduleSave skipped because manualSaveOnly=true");
            return;
        }

        if (saveCoroutine != null) StopCoroutine(saveCoroutine);
        saveCoroutine = StartCoroutine(DelayedSaveCoroutine(delaySeconds));
    }

    IEnumerator DelayedSaveCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        SaveToJsonImmediate();
        saveCoroutine = null;
    }

    #endregion

    #region Replace / Destroy centralization

    /// <summary>
    /// Mark record destroyed (isChopped=true), update in-memory record, destroy currentInstance and spawn replacementPrefab at same transform.
    /// Does NOT save by default; accepts saveNow param to force save.
    /// </summary>
    public GameObject MarkResourceDestroyedAndReplace(string uniqueId, GameObject currentInstance, GameObject replacementPrefab = null, bool saveNow = false)
    {
        if (string.IsNullOrEmpty(uniqueId))
        {
            Debug.LogWarning("[ResourceManagerOffline] MarkResourceDestroyedAndReplace called with empty uniqueId.");
            return null;
        }

        if (!recordsById.TryGetValue(uniqueId, out var rec))
        {
            Debug.LogWarning($"[ResourceManagerOffline] No record found for {uniqueId}.");
            return null;
        }

        if (!rec.isChopped)
        {
            rec.isChopped = true;
            SetDirty(true);
            if (verboseLogs) Debug.Log($"[ResourceManagerOffline] Marked {uniqueId} chopped (dirty).");
            OnRecordChanged?.Invoke(rec);
        }
        else
        {
            if (verboseLogs) Debug.Log($"[ResourceManagerOffline] Resource {uniqueId} already marked chopped.");
        }

        GameObject spawned = null;

        if (currentInstance != null)
        {
            Transform parent = currentInstance.transform.parent;
            Vector3 pos = currentInstance.transform.position;
            Quaternion rot = currentInstance.transform.rotation;
            Vector3 localScale = currentInstance.transform.localScale;

#if UNITY_EDITOR
            if (Application.isPlaying) Destroy(currentInstance);
            else DestroyImmediate(currentInstance);
#else
            Destroy(currentInstance);
#endif

            if (replacementPrefab != null)
            {
                try
                {
                    spawned = Instantiate(replacementPrefab, pos, rot, parent);
                    spawned.transform.localScale = localScale;

                    var inst = spawned.GetComponent<ResourceInstanceOffline>() ?? spawned.AddComponent<ResourceInstanceOffline>();
                    inst.uniqueId = uniqueId;
                    inst.isChopped = true;
                    inst.ApplyState();

                    var br = spawned.GetComponent<BaseResource>();
                    if (br != null) br.SetUniqueId(uniqueId);

                    if (verboseLogs) Debug.Log($"[ResourceManagerOffline] Spawned replacement '{spawned.name}' for {uniqueId}.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ResourceManagerOffline] Exception while instantiating replacement prefab: {ex.Message}");
                    spawned = null;
                }
            }
            else
            {
                if (verboseLogs) Debug.Log($"[ResourceManagerOffline] No replacementPrefab provided for {uniqueId} (no stump will be spawned).");
            }
        }

        if (saveNow) SaveNow(); // explicit force allowed
        return spawned;
    }

    #endregion

    #region Helpers

    string ResolveFullPath()
    {
#if UNITY_EDITOR
        if (spawnJsonAsset != null)
        {
            string assetPath = AssetDatabase.GetAssetPath(spawnJsonAsset); // e.g. "Assets/SpawnData/spawn_expanded.json"
            if (!string.IsNullOrEmpty(assetPath))
            {
                string relative = assetPath.StartsWith("Assets/") ? assetPath.Substring("Assets/".Length) : assetPath;
                return Path.Combine(Application.dataPath, relative);
            }
        }
#endif
        if (!string.IsNullOrEmpty(spawnJsonPath))
        {
            if (spawnJsonPath.StartsWith("Assets/"))
            {
                string relative = spawnJsonPath.Substring("Assets/".Length);
                return Path.Combine(Application.dataPath, relative);
            }
            return spawnJsonPath;
        }
        return Path.Combine(Application.dataPath, "SpawnData", "spawn_expanded.json");
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
                // In manual mode we won't auto-save; cancel coroutine
                if (verboseLogs) Debug.Log("[ResourceManagerOffline] AutoSaveCoroutine exiting because manualSaveOnly=true.");
                yield break;
            }

            if (hasUnsavedChanges)
            {
                if (verboseLogs) Debug.Log("[ResourceManagerOffline] AutoSave triggered.");
                SaveNow();
            }
        }
    }

    // Robust transform applier (preserves desired world transform when parent is non-uniformly scaled).
    static void ApplyWorldTransform(Transform t, Vector3 worldPos, Quaternion worldRot, Vector3 worldScale, Transform parent)
    {
        if (parent == null)
        {
            t.SetParent(null);
            t.position = worldPos;
            t.rotation = worldRot;
            t.localScale = worldScale;
            return;
        }

        // Set parent first so local transforms are applied in parent's space
        t.SetParent(parent, false);
        // compute local position/rotation relative to parent
        t.localPosition = parent.worldToLocalMatrix.MultiplyPoint3x4(worldPos);
        t.localRotation = Quaternion.Inverse(parent.rotation) * worldRot;

        // compute localScale as elementwise division of desiredWorldScale by parent's lossyScale (guard zeros)
        Vector3 p = parent.lossyScale;
        t.localScale = new Vector3(
            Mathf.Approximately(p.x, 0f) ? worldScale.x : worldScale.x / p.x,
            Mathf.Approximately(p.y, 0f) ? worldScale.y : worldScale.y / p.y,
            Mathf.Approximately(p.z, 0f) ? worldScale.z : worldScale.z / p.z
        );
    }

    /// <summary>
    /// Return a snapshot container suitable for saving or inspection.
    /// </summary>
    public SpawnRecordCollection GetSpawnRecordContainer()
    {
        return new SpawnRecordCollection { records = new List<SpawnRecord>(recordsById.Values) };
    }

    #endregion
}
