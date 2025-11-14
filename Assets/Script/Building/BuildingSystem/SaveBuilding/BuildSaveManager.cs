using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

//[DefaultExecutionOrder(-50)] // cố gắng chạy trước nhiều hệ thống khác
public class BuildingSaveManager : MonoBehaviour, ISaveable
{
    public static BuildingSaveManager Instance { get; private set; }

    [Header("Persistence")]
    public string fileName = "buildings.json"; // will be written to Application.persistentDataPath
    public bool verbose = true;

    [Header("Prefab resolution")]
    // Populate this with all building ItemData used for placing buildings (inspectors)
    public List<ItemData> itemCatalog = new List<ItemData>();

    // runtime store
    private readonly Dictionary<string, BuildRecord> recordsByGuid = new Dictionary<string, BuildRecord>();
    private bool hasUnsavedChanges = false;

    public bool HasUnsavedChanges => hasUnsavedChanges;
    public string SaveableName => gameObject.name;
    public string SceneName => gameObject.scene.name;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        Debug.Log("[BSM] OnEnable called, trying Register with SaveManager");
        SaveManager.Instance?.Register(this);
        // Load on enable (or could be Start)
        LoadNow();
    }

    private void OnDisable()
    {
        Debug.Log("[BSM] OnDisable called, Unregister");
        SaveManager.Instance?.Unregister(this);
    }

    #region Public API used by placement/destroy systems
    public void AddOrUpdateRecord(BuildtObject b)
    {
        if (b == null) return;
        var rec = BuildRecordFromObject(b);
        recordsByGuid[rec.guid] = rec;
        SetDirty(true);
        //if (verbose) Debug.Log($"[BSM] Add/Update record {rec.guid} ({rec.itemId})");
    }

    public void RemoveRecord(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return;
        if (recordsByGuid.Remove(guid))
        {
            SetDirty(true);
            if (verbose) Debug.Log($"[BSM] Removed record {guid}");
        }
    }

    public BuildRecordCollection GetSnapshot()
    {
        var col = new BuildRecordCollection();
        col.version = 1;
        col.records.AddRange(recordsByGuid.Values);
        return col;
    }
    #endregion

    #region Save / Load
    public void SaveNow()
    {
        var col = GetSnapshot();
        string json = JsonUtility.ToJson(col, true);
        string path = Path.Combine(Application.persistentDataPath, fileName);
        try
        {
            File.WriteAllText(path, json);
            SetDirty(false);
            if (verbose) Debug.Log($"[BSM] Saved {col.records.Count} build records to {path}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[BSM] Save failed: {ex}");
        }
    }

    public void LoadNow()
    {
        string path = Path.Combine(Application.persistentDataPath, fileName);
        if (!File.Exists(path))
        {
            if (verbose) Debug.Log($"[BSM] No build save file found at {path}");
            // Also attempt to load from StreamingAssets or other source if you want
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            var col = JsonUtility.FromJson<BuildRecordCollection>(json);
            if (col == null || col.records == null) { if (verbose) Debug.LogWarning("[BSM] Empty/invalid save file."); return; }

            // Clear any existing in-scene build root children (optional; depends on design).
            // For safety, we will not auto-delete in-scene objects; instead we spawn alongside.
            recordsByGuid.Clear();

            // Spawn all, but defer anchor linking until all spawned.
            var spawnedMap = new Dictionary<string, BuildtObject>();

            foreach (var rec in col.records)
            {
                var prefab = ResolvePrefabForRecord(rec);
                GameObject go;
                if (prefab != null)
                {
                    go = Instantiate(prefab, rec.position.ToVector3(), rec.rotation.ToQuaternion(), BuildHierarchy.Root);
                    go.transform.localScale = rec.scale.ToVector3();
                    ApplyBuildLayer(go);
                }
                else
                {
                    // fallback placeholder
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.SetParent(BuildHierarchy.Root, false);
                    go.name = $"MISSING_PREFAB_{rec.itemId}_{rec.guid}";
                    go.transform.position = rec.position.ToVector3();
                    go.transform.rotation = rec.rotation.ToQuaternion();
                    go.transform.localScale = rec.scale.ToVector3();
                    var colComp = go.GetComponent<Collider>();
                    if (colComp != null) DestroyImmediate(colComp);
                }

                // ensure BuildtObject component and assign fields
                var bo = go.GetComponent<BuildtObject>() ?? go.AddComponent<BuildtObject>();
                bo.objectType = FindItemDataById(rec.itemId); // may be null
                bo.guid = rec.guid;

                // ensure BuildClusterRef exists
                var cref = go.GetComponent<BuildClusterRef>() ?? go.AddComponent<BuildClusterRef>();
                // anchor will be resolved later
                cref.anchor = null;

                spawnedMap[rec.guid] = bo;
                recordsByGuid[rec.guid] = rec;
            }

            // resolve anchors
            foreach (var rec in col.records)
            {
                if (string.IsNullOrEmpty(rec.anchorGuid)) continue;
                if (!spawnedMap.TryGetValue(rec.guid, out var bo)) continue;
                var cref = bo.GetComponent<BuildClusterRef>();
                if (rec.anchorGuid == bo.guid)
                {
                    // anchor is itself (common for platforms)
                    cref.anchor = bo.transform;
                }
                else if (spawnedMap.TryGetValue(rec.anchorGuid, out var anchorObj))
                {
                    cref.anchor = anchorObj.transform;
                }
                else
                {
                    // fallback: nearest anchor (very simple)
                    cref.anchor = FindNearestAnchorFor(bo.transform.position);
                    if (verbose) Debug.LogWarning($"[BSM] Could not find anchor {rec.anchorGuid} for {rec.guid}. Fallback nearest: {cref.anchor?.name ?? "null"}");
                }
            }

            SetDirty(false);
            if (verbose) Debug.Log($"[BSM] Loaded {col.records.Count} build records from {path}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[BSM] Load failed: {ex}");
        }
    }
    #endregion

    #region Utilities
    private void SetDirty(bool d) => hasUnsavedChanges = d;

    private BuildRecord BuildRecordFromObject(BuildtObject b)
    {
        var rec = new BuildRecord();
        rec.guid = b.guid;
        //rec.itemId = b.objectType != null ? b.objectType.building.id : (b.objectType?.worldPrefab?.name ?? "unknown");
        rec.prefabName = b.objectType?.worldPrefab?.name ?? "unknown";
        rec.position = new SerializableVector3(b.transform.position);
        rec.rotation = new SerializableQuaternion(b.transform.rotation);
        rec.scale = new SerializableVector3(b.transform.localScale);

        var cref = b.GetComponent<BuildClusterRef>();
        if (cref != null && cref.anchor != null)
        {
            var anchorBo = cref.anchor.GetComponentInParent<BuildtObject>();
            if (anchorBo != null) rec.anchorGuid = anchorBo.guid;
            else rec.anchorGuid = null;
        }
        else rec.anchorGuid = null;

        rec.sceneName = b.gameObject.scene.name;
        return rec;
    }

    private ItemData FindItemDataById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var it in itemCatalog)
        {
            if (it == null) continue;
            //if (it.building.id == id || it.name == id) return it;
        }
        return null;
    }

    private GameObject ResolvePrefabForRecord(BuildRecord rec)
    {
        // Prefer lookup by itemCatalog via itemId
        var it = FindItemDataById(rec.itemId);
        if (it != null && it.worldPrefab != null) return it.worldPrefab;
        // fallback: try Resources load by prefabName
        if (!string.IsNullOrEmpty(rec.prefabName))
        {
            var r = Resources.Load<GameObject>(rec.prefabName);
            if (r != null) return r;
        }
        return null;
    }

    private Transform FindNearestAnchorFor(Vector3 pos)
    {
        float radius = 2.0f; // you may tune or compute from cell size
        var cols = Physics.OverlapSphere(pos, radius);
        Transform best = null;
        float min = float.MaxValue;
        foreach (var c in cols)
        {
            var bo = c.GetComponentInParent<BuildtObject>();
            if (bo == null) continue;
            float d = (bo.transform.position - pos).sqrMagnitude;
            if (d < min) { min = d; best = bo.transform; }
        }
        return best;
    }
    private void ApplyBuildLayer(GameObject go)
    {
        var placement = FindObjectOfType<BuildPlacementSystem>();
        if (placement == null) return;
        int layerIndex = placement.GetBuildLayerIndex();
        go.layer = layerIndex;
        foreach (Transform child in go.transform)
            child.gameObject.layer = layerIndex;
    }

    #endregion
}
