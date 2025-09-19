// ResourceManager.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum ResourceManagerRole { Standalone, Host, Client }

[DisallowMultipleComponent]
public class ResourceManager : MonoBehaviour
{
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
    //public bool clearSpawnParentOnLoad = true;

    [Header("Persistence / Network")]
    public bool manualSaveOnly = true;
    public bool enableAutoSave = false; // only honored when manualSaveOnly == false
    public float autoSaveIntervalSeconds = 300f;

    [Header("Debug")]
    public bool verboseLogs = true;

    // Core + adapters
    public ResourceManagerCore core;
    IResourcePersistence persistence;
    INetworkAdapter network;

    // dirty flag
    public bool hasUnsavedChanges { get; private set; } = false;
    public event Action<bool> OnDirtyStateChanged;

    // internals
    private Coroutine autoSaveCoroutine = null;

    void Awake()
    {
        core = new ResourceManagerCore();

        // default adaptors if not configured externally
        if (network == null) network = new NoNetworkAdapter();
        if (persistence == null)
        {
            if (role == ResourceManagerRole.Client) persistence = new NullPersistence();
            else persistence = new LocalJsonPersistence(spawnJsonAsset, spawnJsonPath, verboseLogs);
        }

        // wire core events
        core.OnSpawnRequested += SpawnRecordVisual;
        core.OnRecordChangedRaw += (id, state) => { SetDirty(true); };

        // wire network events
        network.OnChangeReceived += HandleNetworkChange;
        network.OnSnapshotReceived += HandleSnapshotReceived;
    }

    void Start()
    {
        // auto-load for Host/Standalone: persistence loads snapshot and we spawn
        if (role == ResourceManagerRole.Standalone || role == ResourceManagerRole.Host)
        {
            var snapshot = persistence.Load();
            core.LoadSnapshot(snapshot);
            core.RequestSpawnAll();
            SetDirty(false);
            if (verboseLogs) Debug.Log($"[ResourceManager] Loaded snapshot with {core.recordsById.Count} records.");

            Debug.Log($"Snapshot loaded. Count = {snapshot?.records?.Count ?? 0}");
        }
        else // client
        {
            // ask host for snapshot (network adapter implementation must handle this)
            network.RequestSnapshot();
            if (verboseLogs) Debug.Log("[ResourceManager] Client requested snapshot from host.");
        }

        // auto-save coroutine only if allowed
        if (enableAutoSave && !manualSaveOnly && role != ResourceManagerRole.Client)
            autoSaveCoroutine = StartCoroutine(AutoSaveCoroutine());
    }

    void OnDestroy()
    {
        if (autoSaveCoroutine != null) StopCoroutine(autoSaveCoroutine);
    }

    #region Public API called by gameplay / ResourceInstanceOffline

    public void OnResourceStateChanged(string uniqueId, bool isChopped)
    {
        if (role == ResourceManagerRole.Client)
        {
            if (verboseLogs) Debug.Log($"[ResourceManager] Client requests change {uniqueId} -> {isChopped}");
            network.RequestChange(uniqueId, isChopped);
            return;
        }

        // Host or Standalone: apply locally and broadcast if host
        core.ApplyLocalChange(uniqueId, isChopped);

        if (role == ResourceManagerRole.Host)
        {
            network.BroadcastChange(uniqueId, isChopped);
            // host will persist when SaveNow is called
        }
    }

    public GameObject MarkResourceDestroyedAndReplace(string uniqueId, GameObject currentInstance, GameObject replacementPrefab = null, bool saveNow = false)
    {
        if (role == ResourceManagerRole.Client)
        {
            // clients request host to perform destruction
            network.RequestChange(uniqueId, true);
            // optimistic client-side replacement can be implemented by caller if desired; we skip it here
            return null;
        }

        if (!core.recordsById.TryGetValue(uniqueId, out var rec))
        {
            Debug.LogWarning($"[ResourceManager] MarkResourceDestroyedAndReplace: unknown id {uniqueId}");
            return null;
        }

        if (!rec.isChopped)
        {
            rec.isChopped = true;
            SetDirty(true);
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
                spawned = Instantiate(replacementPrefab, pos, rot, parent);
                spawned.transform.localScale = localScale;

                var inst = spawned.GetComponent<ResourceInstanceOffline>() ?? spawned.AddComponent<ResourceInstanceOffline>();
                inst.uniqueId = uniqueId;
                inst.isChopped = true;
                inst.ApplyState();

                var br = spawned.GetComponent<BaseResource>();
                if (br != null) br.SetUniqueId(uniqueId);
            }
        }

        if (role == ResourceManagerRole.Host)
            network.BroadcastChange(uniqueId, true);

        if (saveNow && role != ResourceManagerRole.Client)
            SaveNow();

        return spawned;
    }

    #endregion

    #region Network / Snapshot handlers

    void HandleNetworkChange(string uniqueId, bool isChopped)
    {
        if (role == ResourceManagerRole.Host)
        {
            if (!core.recordsById.ContainsKey(uniqueId))
            {
                Debug.LogWarning($"[ResourceManager] Host received change for unknown id {uniqueId}");
                return;
            }
            core.ApplyLocalChange(uniqueId, isChopped);
            SetDirty(true);
            network.BroadcastChange(uniqueId, isChopped);
        }
        else if (role == ResourceManagerRole.Client)
        {
            core.ApplyAuthorityChange(uniqueId, isChopped);
        }
        else
        {
            core.ApplyLocalChange(uniqueId, isChopped);
        }
    }

    void HandleSnapshotReceived(SpawnRecordCollection container)
    {
        core.LoadSnapshot(container);
        core.RequestSpawnAll();
        SetDirty(false);
        if (verboseLogs) Debug.Log($"[ResourceManager] Snapshot received (records={core.recordsById.Count})");
    }

    #endregion

    #region Save / Persistence

    public void SaveNow()
    {
        if (role == ResourceManagerRole.Client)
        {
            Debug.LogWarning("[ResourceManager] Client should not call SaveNow(): request host to save if you want persistence.");
            network.RequestHostSave();
            return;
        }

        var container = core.GetSnapshot();
        persistence.Save(container);
        SetDirty(false);
        if (verboseLogs) Debug.Log("[ResourceManager] SaveNow completed.");
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

    #region Spawn helpers (uses Editor AssetDatabase when available)

    void SpawnRecordVisual(SpawnRecord r)
    {
        Debug.Log($"[RM] SpawnRecordVisual: id={r?.uniqueId} spawnParent={(spawnParent ? spawnParent.name : "NULL")} " +
          $"spawnParentScene={(spawnParent ? spawnParent.gameObject.scene.name : "<null>")} " +
          $"resourceManagerScene={gameObject.scene.name}");

        if (r == null) return;
        GameObject prefab = ResolvePrefabFromRecord(r);

        if (prefab == null) Debug.LogWarning($"Prefab missing for record {r.uniqueId}");

        // Determine whether the record should be treated as:
        // A) local-space relative to spawnParent (recordsAreLocalSpace==true && exporterReference==null)
        // B) local-space relative to exporterReference (recordsAreLocalSpace==true && exporterReference!=null)
        // C) world-space (recordsAreLocalSpace==false)
        bool recordIsLocalToParent = recordsAreLocalSpace && exporterReference == null && spawnParent != null;
        bool recordIsLocalToExporter = recordsAreLocalSpace && exporterReference != null;

        // computed world TRS used when we treat the record as world-space (case C) or exporter-reference-local (case B)
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
            // record already in world space
            desiredWorldPos = r.position;
            desiredWorldRot = r.rotation;
            desiredWorldScale = r.scale;
        }
        // else if recordIsLocalToParent: we'll use r.position/rotation/scale as *local* values (do not convert to world here)

        GameObject go = null;

        if (prefab != null)
        {
            if (recordIsLocalToParent)
            {
                // JSON stores transform *local to spawnParent*. Parent first, then assign local TRS.
                // Instantiate with parent so Awake/OnEnable will see correct hierarchy (avoid temporary wrong parenting)
                go = Instantiate(prefab, spawnParent);
                // now assign local TRS from JSON
                go.transform.localPosition = r.position;
                go.transform.localRotation = r.rotation;
                go.transform.localScale = r.scale;

                if (verboseLogs) Debug.Log($"[RM] Spawned '{go.name}' as LOCAL-to-parent (localPos={r.position}, localRot={r.rotation.eulerAngles}, localScale={r.scale}) under '{spawnParent.name}'");
            }
            else
            {
                // Treat record as world-space (either exported from exporterReference or given as absolute world TRS)
                go = Instantiate(prefab, desiredWorldPos, desiredWorldRot);

                // set world scale before parenting (so world appearance matches JSON)
                go.transform.localScale = desiredWorldScale;

                if (spawnParent != null)
                {
                    // Move to parent's scene explicitly (optional)
                    SceneManager.MoveGameObjectToScene(go, spawnParent.gameObject.scene);

                    // parent while preserving world transform
                    go.transform.SetParent(spawnParent, true);
                }
                else
                {
                    // ensure it's root in its scene
                    go.transform.SetParent(null, true);
                }

                if (verboseLogs) Debug.Log($"[RM] Spawned '{go.name}' at world TRS pos={desiredWorldPos}, rot={desiredWorldRot.eulerAngles}, scl={desiredWorldScale}");
            }
        }
        else if (spawnPlaceholderForMissingPrefabs)
        {
            if (recordIsLocalToParent)
            {
                // create placeholder as child and assign local TRS
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"MISSING_PREFAB_{r.uniqueId}";
                cube.transform.SetParent(spawnParent, false); // set local TRS
                cube.transform.localPosition = r.position;
                cube.transform.localRotation = r.rotation;
                cube.transform.localScale = r.scale;

                var col = cube.GetComponent<Collider>();
                if (col != null)
                {
#if UNITY_EDITOR
                    DestroyImmediate(col);
#else
                    Destroy(col);
#endif
                }

                go = cube;

                if (verboseLogs) Debug.LogWarning($"[ResourceManager] Spawned placeholder (local) for missing prefab for record {r.uniqueId}");
            }
            else
            {
                // world-case placeholder
                go = CreateMissingPrefabPlaceholder(r.uniqueId, desiredWorldPos, desiredWorldRot, desiredWorldScale);

                if (spawnParent != null)
                {
                    SceneManager.MoveGameObjectToScene(go, spawnParent.gameObject.scene);
                    go.transform.SetParent(spawnParent, true);
                }
                else
                {
                    go.transform.SetParent(null, true);
                }

                if (verboseLogs) Debug.LogWarning($"[ResourceManager] Spawned placeholder (world) for missing prefab for record {r.uniqueId}");
            }
        }

        if (go != null)
        {
            var inst = go.GetComponent<ResourceInstanceOffline>() ?? go.AddComponent<ResourceInstanceOffline>();
            inst.uniqueId = r.uniqueId;
            inst.isChopped = r.isChopped;
            inst.ApplyState();

            var br = go.GetComponent<BaseResource>();
            if (br != null) br.SetUniqueId(r.uniqueId);
        }
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
            if (prefab == null && verboseLogs) Debug.LogWarning($"[ResourceManager] Could not load prefab at path '{path}' for record {r.uniqueId}");
            return prefab;
        }
#endif
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
        if (col != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(col);
#else
            Destroy(col);
#endif
        }
        return cube;
    }

    #endregion
}
