#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;
using static Unity.Burst.Intrinsics.X86; // (kept from your original file)

/// <summary>
/// UV-mask-based spawner for ANY BaseResource prefab.
/// Now supports two modes:
///  - Instantiate prefabs into hierarchy (original behavior).
///  - Export compact grouped placement data to JSON (new feature).
/// 
/// NOTE: This file is editor-only (wrapped in UNITY_EDITOR).
/// </summary>
[ExecuteInEditMode]
public class UVMapResourceDataGenerator : MonoBehaviour
{
    [Header("Mask (UV0)")]
    public Texture2D spawnMask;

    [Header("Raycast")]
    [Tooltip("Layer of THIS chunk's MeshCollider only. Keeps rays from hitting neighbors.")]
    public LayerMask chunkLayer;
    [Tooltip("How high above the sampled surface point to start the downward ray (used only if physicsRaycast==true).")]
    public float rayStartAbove = 5f;
    [Tooltip("How far downward to cast the ray (used only if physicsRaycast==true).")]
    public float raycastDownDistance = 200f;

    [Header("Mode")]
    [Tooltip("If true, use Physics.Raycast to find surface points. If false, interpolate vertex positions/normals (much faster but assumes MeshCollider uses the same mesh and transform).")]
    public bool usePhysicsRaycast = false;

    [Header("Common Spawn Tweaks")]
    [Tooltip("Lift slightly to avoid z-fighting/clipping.")]
    public float spawnHeightOffset = 0.05f;
    [Tooltip("If true, compensate parent's lossy scale so spawned objects look correct in world space.")]
    public bool compensateParentScale = true;
    [Tooltip("Optional deterministic random seed for reproducible results.")]
    public bool useDeterministicSeed = false;
    public int randomSeed = 12345;

    [System.Serializable]
    public class ResourceGroup
    {
        public ResourceType resourceType;                    // Your existing enum
        [Tooltip("Prefabs to pick from (must contain a BaseResource component).")]
        public GameObject[] prefabs;
        [Tooltip("Which mask color means this group can spawn.")]
        public Color maskColor = Color.green;
        [Tooltip("Per-channel tolerance (legacy) or Euclidean tolerance depending on 'useEuclideanColorTolerance'.")]
        [Range(0f, 1f)] public float colorTolerance = 0.1f; // per-channel or euclidean depending on mode
        [Tooltip("If true interpret colorTolerance as Euclidean distance in RGB space (recommended).")]
        public bool useEuclideanColorTolerance = true;
        [Tooltip("How many instances to try to place for this group.")]
        public int spawnCount = 100;

        [Header("Per-Group Randomization")]
        public float minScale = 0.9f;
        public float maxScale = 1.1f;
        public float minYaw = 0f;
        public float maxYaw = 360f;

        [Header("Placement Filters")]
        [Tooltip("Reject points steeper than this (0=flat, 90=vertical).")]
        [Range(0f, 90f)] public float maxSlope = 60f;
        [Tooltip("Keep at least this distance from other placed instances in this session.")]
        public float minSeparation = 0f;

        [Header("Attempts")]
        [Tooltip("Max attempts = spawnCount * maxAttemptsMultiplier (per group).")]
        public int maxAttemptsMultiplier = 50;
    }

    [Header("Groups (color → prefab set)")]
    public ResourceGroup[] resourceGroups;

    [Header("Runtime/Editor References")]
    public Renderer rend;         // child renderer of this chunk
    public MeshCollider meshCol;  // child mesh collider of this chunk

    // Lightweight record kept locally for debugging/bridge steps. Do NOT rely on indices for persistence.
    [System.Serializable]
    public class SpawnRecord
    {
        public string uniqueId;     // stable per-instance id
        public string prefabGuid;   // Editor-only stable reference
        public string prefabPath;   // asset path (helpful for editor-side rehydration)
        public ResourceType type;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        // runtime state (your simple boolean)
        public bool isChopped = false;
    }

    [System.Serializable]
    public class SpawnRecordCollection
    {
        public List<SpawnRecord> records = new List<SpawnRecord>();
    }

    [Header("Export Options")]
    [Tooltip("If true, do NOT instantiate. Generate placement records and export to JSON instead.")]
    public bool emitJsonInsteadOfInstantiating = false;
    [Tooltip("Default filename suggested in Save dialog")]
    public string exportFileName = "spawn_data.json";
    [Tooltip("Record transforms relative to this.transform (recommended). If false, records are world-space.")]
    public bool recordLocalSpace = true;

    [Header("Compact Export Options")]
    [Tooltip("When true, write compact grouped JSON (recommended for large counts).")]
    public bool useCompactGroupedExport = true;
    [Tooltip("Number of decimal places for floats in compact export (smaller => less precision).")]
    [Range(0, 9)] public int floatPrecision = 3;

    [Header("Spawn Options (Editor)")]
    public bool alignToSurfaceNormal = false; // optional: rotate to face surface normal

    [Header("Debug / Session Output (Editor)")]
    public List<SpawnRecord> lastSessionRecords = new List<SpawnRecord>();

    // Internal caches used per-run
    private Color[] maskPixels = null;
    private int maskWidth = 0, maskHeight = 0;

    void OnValidate()
    {
        if (!rend) rend = GetComponentInChildren<Renderer>();
        if (!meshCol) meshCol = GetComponentInChildren<MeshCollider>();

        if (!rend || !meshCol)
            Debug.LogWarning($"{name}: Missing Renderer or MeshCollider on this chunk!");

        if (spawnMask && !spawnMask.isReadable)
            Debug.LogError($"Spawn mask '{spawnMask.name}' is not Read/Write enabled. Enable Read/Write in its import settings.");
    }

    [ContextMenu("Spawn All Groups")]
    public void SpawnAllGroups()
    {
        if (!ValidateSetup()) return;

        lastSessionRecords.Clear();

        // Save & restore Unity RNG state so we don't affect other editor tools
        var previousRandState = Random.state;
        if (useDeterministicSeed) Random.InitState(randomSeed);

        var mesh = meshCol.sharedMesh;
        var tris = mesh.triangles;
        var verts = mesh.vertices;
        var uvs = mesh.uv;
        var normals = mesh.normals;

        if (uvs == null || uvs.Length != verts.Length)
        {
            Debug.LogError("Mesh has no valid UV0 for mask sampling.");
            Random.state = previousRandState;
            return;
        }

        var cdf = BuildTriangleCDF(verts, tris);

        // Cache mask pixels for fast sampling
        PrepareMaskCache();

        if (!emitJsonInsteadOfInstantiating)
            Undo.SetCurrentGroupName("Spawn Resources");
        int undoGroup = Undo.GetCurrentGroup();

        // spatial grid caches for min separation per-group
        var placedPositionsByGroup = new Dictionary<ResourceGroup, SpatialHashGrid>();

        // Prefab -> has BaseResource cache
        var prefabValidCache = new Dictionary<GameObject, bool>();

        int totalGroups = resourceGroups != null ? resourceGroups.Length : 0;
        int groupIndex = 0;

        try
        {
            foreach (var group in resourceGroups)
            {
                groupIndex++;
                if (group == null) continue;
                if (group.prefabs == null || group.prefabs.Length == 0)
                {
                    Debug.LogWarning($"Group {group.resourceType} has no prefabs assigned.");
                    continue;
                }

                // Validate BaseResource presence quickly using cache
                bool anyValid = false;
                foreach (var p in group.prefabs)
                {
                    if (!p) continue;
                    if (!prefabValidCache.TryGetValue(p, out bool isValid))
                    {
                        //isValid = p.GetComponent<BaseResource>() != null;
                        isValid = true;
                        prefabValidCache[p] = isValid;
                    }
                    if (isValid) { anyValid = true; break; }
                }
                if (!anyValid)
                {
                    Debug.LogWarning($"Group {group.resourceType} has no prefabs with BaseResource component.");
                    continue;
                }

                int placed = 0;
                int attempts = 0;
                int maxAttempts = Mathf.Max(group.spawnCount * Mathf.Max(1, group.maxAttemptsMultiplier), group.spawnCount);
                if (!placedPositionsByGroup.ContainsKey(group)) placedPositionsByGroup[group] = new SpatialHashGrid(group.minSeparation);
                var grid = placedPositionsByGroup[group];

                // quick per-group caches
                Color targetColor = group.maskColor;
                float tol = Mathf.Clamp01(group.colorTolerance);
                float minSep = group.minSeparation;
                int prefabCount = group.prefabs.Length;
                float minScale = group.minScale;
                float maxScale = group.maxScale;
                float minYaw = group.minYaw;
                float maxYaw = group.maxYaw;
                float maxSlope = group.maxSlope;
                bool useEuclidean = group.useEuclideanColorTolerance;

                while (placed < group.spawnCount && attempts < maxAttempts)
                {
                    attempts++;

                    // reduce progress bar calls to once per 128 attempts
                    if ((attempts & 127) == 0)
                    {
                        float progress = (groupIndex - 1 + (float)attempts / Mathf.Max(1, maxAttempts)) / Mathf.Max(1, totalGroups);
                        if (EditorUtility.DisplayCancelableProgressBar("Spawning resources", $"Group {group.resourceType} ({groupIndex}/{totalGroups}) - attempt {attempts}/{maxAttempts}", progress))
                        {
                            Debug.Log("Spawn cancelled by user.", this);
                            break;
                        }
                    }

                    // 1) Pick a triangle by CDF
                    int triIndex = SampleTriangleIndex(cdf);
                    int i0 = tris[triIndex * 3 + 0];
                    int i1 = tris[triIndex * 3 + 1];
                    int i2 = tris[triIndex * 3 + 2];

                    Vector3 v0 = verts[i0];
                    Vector3 v1 = verts[i1];
                    Vector3 v2 = verts[i2];
                    Vector2 uv0 = uvs[i0];
                    Vector2 uv1 = uvs[i1];
                    Vector2 uv2 = uvs[i2];

                    // 2) Uniform random barycentric point
                    float r1 = Random.value;
                    float r2 = Random.value;
                    float sqrtR1 = Mathf.Sqrt(r1);
                    float b0 = 1f - sqrtR1;
                    float b1 = sqrtR1 * (1f - r2);
                    float b2 = sqrtR1 * r2;

                    Vector3 localPoint = b0 * v0 + b1 * v1 + b2 * v2;
                    Vector2 uv = b0 * uv0 + b1 * uv1 + b2 * uv2;

                    // 3) Mask color check - bilinear sample using cached pixels
                    uv.x = Mathf.Repeat(uv.x, 1f);
                    uv.y = Mathf.Repeat(uv.y, 1f);
                    Color px = SampleMaskBilinear(uv.x, uv.y);
                    if (!IsColorMatch(px, targetColor, tol, useEuclidean))
                        continue;

                    // 4) Convert to world and find surface normal/point
                    Vector3 worldPoint = meshCol.transform.TransformPoint(localPoint);
                    Vector3 sampleNormal;

                    if (usePhysicsRaycast)
                    {
                        Vector3 rayOrigin = worldPoint + Vector3.up * rayStartAbove;
                        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDownDistance, chunkLayer, QueryTriggerInteraction.Ignore))
                            continue;
                        if (hit.collider != meshCol)
                            continue; // ensure we hit THIS chunk only
                        sampleNormal = hit.normal;
                        worldPoint = hit.point; // trust physics height
                    }
                    else
                    {
                        // interpolate vertex normals (smooth normal) to compute slope
                        if (normals != null && normals.Length == verts.Length)
                        {
                            Vector3 n0 = normals[i0];
                            Vector3 n1 = normals[i1];
                            Vector3 n2 = normals[i2];
                            Vector3 localNormal = (b0 * n0 + b1 * n1 + b2 * n2).normalized;
                            sampleNormal = meshCol.transform.TransformDirection(localNormal).normalized;
                        }
                        else
                        {
                            // fall back to triangle normal if vertex normals missing
                            Vector3 triNormal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                            sampleNormal = meshCol.transform.TransformDirection(triNormal).normalized;
                        }
                    }

                    // 5) Slope filter
                    float slopeDeg = Vector3.Angle(sampleNormal, Vector3.up);
                    if (slopeDeg > maxSlope)
                        continue;

                    // 6) Min separation (spatial hash grid)
                    if (minSep > 0f)
                    {
                        if (grid.IsTooClose(worldPoint)) continue;
                    }

                    // 7) Choose prefab (ensure BaseResource) with caching
                    GameObject prefab = null;
                    for (int tries = 0; tries < prefabCount && prefab == null; tries++)
                    {
                        var pick = group.prefabs[Random.Range(0, prefabCount)];
                        if (!pick) continue;
                        if (!prefabValidCache.TryGetValue(pick, out bool isValid))
                        {
                            isValid = pick.GetComponent<BaseResource>() != null;
                            prefabValidCache[pick] = isValid;
                        }
                        if (isValid) prefab = pick;
                    }
                    if (!prefab) continue;

                    // 8) Compute placement transform (but do NOT instantiate if exporting)
                    Vector3 worldPlacedPos = worldPoint + sampleNormal * spawnHeightOffset;

                    // Rotation: optionally align to surface normal
                    Quaternion worldRotation;
                    if (alignToSurfaceNormal)
                    {
                        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, sampleNormal).normalized;
                        if (forward.sqrMagnitude < 1e-6f) forward = Vector3.Cross(sampleNormal, Vector3.right).normalized;
                        float yaw = Random.Range(minYaw, maxYaw);
                        Quaternion yawRot = Quaternion.AngleAxis(yaw, sampleNormal);
                        worldRotation = yawRot * Quaternion.LookRotation(forward, sampleNormal);
                    }
                    else
                    {
                        float yaw = Random.Range(minYaw, maxYaw);
                        worldRotation = Quaternion.Euler(0f, yaw, 0f);
                    }

                    // Scale (with optional parent-scale compensation)
                    float rndScale = Random.Range(minScale, maxScale);
                    Vector3 localScaleVec;
                    if (compensateParentScale)
                    {
                        Vector3 parentLossy = transform.lossyScale;
                        float fixX = parentLossy.x != 0f ? 1f / parentLossy.x : 1f;
                        float fixY = parentLossy.y != 0f ? 1f / parentLossy.y : 1f;
                        float fixZ = parentLossy.z != 0f ? 1f / parentLossy.z : 1f;
                        localScaleVec = new Vector3(rndScale * fixX, rndScale * fixY, rndScale * fixZ);
                    }
                    else
                    {
                        localScaleVec = Vector3.one * rndScale;
                    }

                    if (emitJsonInsteadOfInstantiating)
                    {
                        // get prefab GUID and path
                        string prefabPath = AssetDatabase.GetAssetPath(prefab);
                        string guid = AssetDatabase.AssetPathToGUID(prefabPath);

                        // compute stored position & rotation (local or world as requested)
                        Vector3 storedPos = recordLocalSpace ? transform.InverseTransformPoint(worldPlacedPos) : worldPlacedPos;
                        Quaternion storedRot = recordLocalSpace ? Quaternion.Inverse(transform.rotation) * worldRotation : worldRotation;

                        // generate unique id using inspector floatPrecision (digits)
                        int precDigits = Mathf.Clamp(floatPrecision, 0, 9);
                        string uid = GenerateUniqueId(guid, storedPos, precDigits);

                        lastSessionRecords.Add(new SpawnRecord
                        {
                            uniqueId = uid,
                            prefabGuid = guid,
                            prefabPath = prefabPath,
                            type = group.resourceType,
                            position = storedPos,
                            rotation = storedRot,
                            scale = localScaleVec,
                            isChopped = false
                        });

                        grid.Add(worldPlacedPos);
                        placed++;
                        continue; // move to next placement
                    }
                    else
                    {
                        // Instantiate into hierarchy (original behavior)
                        var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                        if (obj == null)
                        {
                            Debug.LogWarning("Prefab instantiation failed for one prefab in group " + group.resourceType);
                            continue;
                        }

                        obj.transform.SetParent(this.transform, true);
                        obj.transform.position = worldPlacedPos;
                        obj.transform.rotation = worldRotation;
                        obj.transform.localScale = localScaleVec;

                        // Register create undo per-object (will be grouped)
                        Undo.RegisterCreatedObjectUndo(obj, "Spawn Resource");

                        // 9) Record (editor convenience, GUID is stable in editor)
                        string instPrefabPath = AssetDatabase.GetAssetPath(
                            PrefabUtility.GetCorrespondingObjectFromSource(obj)
                        );
                        string instGuid = AssetDatabase.AssetPathToGUID(instPrefabPath);

                        // compute stored position & rotation (local or world as requested)
                        Vector3 storedPos2 = recordLocalSpace ? transform.InverseTransformPoint(obj.transform.position) : obj.transform.position;
                        Quaternion storedRot2 = recordLocalSpace ? Quaternion.Inverse(transform.rotation) * obj.transform.rotation : obj.transform.rotation;

                        // generate uid
                        int precDigits2 = Mathf.Clamp(floatPrecision, 0, 9);
                        string uid2 = GenerateUniqueId(instGuid, storedPos2, precDigits2);

                        // Attempt to determine resource type from instantiated object (if it has BaseResource)
                        var br = obj.GetComponent<BaseResource>();

                        lastSessionRecords.Add(new SpawnRecord
                        {
                            uniqueId = uid2,
                            prefabGuid = instGuid,
                            prefabPath = instPrefabPath,
                            type = br != null ? br.GetResourceType() : group.resourceType,
                            position = storedPos2,
                            rotation = storedRot2,
                            scale = obj.transform.localScale,
                            isChopped = false
                        });

                        grid.Add(obj.transform.position);
                        placed++;
                    }
                }

                Debug.Log($"Group {group.resourceType}: placed {placed}/{group.spawnCount} after {attempts} attempts.", this);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Random.state = previousRandState; // restore RNG state

            // collapse undo group so all creations are one undo step
            if (!emitJsonInsteadOfInstantiating)
                Undo.CollapseUndoOperations(undoGroup);
        }

        // Export JSON if requested
        if (emitJsonInsteadOfInstantiating)
        {
            if (lastSessionRecords.Count == 0)
            {
                Debug.Log("No spawn records were generated.");
            }
            else
            {
                string file = EditorUtility.SaveFilePanel("Save spawn JSON", Application.dataPath, exportFileName, "json");
                if (!string.IsNullOrEmpty(file))
                {
                    if (useCompactGroupedExport)
                        SaveCompactGroupedJson(file, lastSessionRecords, recordLocalSpace, floatPrecision);
                    else
                    {
                        var container = new SpawnRecordCollection { records = lastSessionRecords };
                        File.WriteAllText(file, JsonUtility.ToJson(container, false), Encoding.UTF8);
                    }

                    AssetDatabase.Refresh();
                    Debug.Log($"Exported {lastSessionRecords.Count} spawn records to:\n{file}", this);
                }
                else
                {
                    Debug.Log("Export cancelled.");
                }
            }
        }
    }

    [ContextMenu("Scan Existing (Children → Records)")]
    public void ScanExisting()
    {
        if (!ValidateSetup()) return;
        lastSessionRecords.Clear();

        foreach (Transform child in transform)
        {
            var br = child.GetComponent<BaseResource>();
            if (br == null) continue;

            string prefabPath = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject));
            string guid = AssetDatabase.AssetPathToGUID(prefabPath);

            // generate unique id using child's local position
            string uidExisting = GenerateUniqueId(guid, child.localPosition, Mathf.Clamp(floatPrecision, 0, 9));

            lastSessionRecords.Add(new SpawnRecord
            {
                uniqueId = uidExisting,
                prefabGuid = guid,
                prefabPath = prefabPath,
                type = br.GetResourceType(),
                position = child.localPosition,
                rotation = child.localRotation,
                scale = child.localScale,
                isChopped = false
            });
        }

        Debug.Log($"Scanned {lastSessionRecords.Count} BaseResource children → records.", this);
    }

    [ContextMenu("Clear All Spawned Children")]
    public void ClearAllChildren()
    {
        var toDelete = new List<GameObject>();
        foreach (Transform child in transform)
        {
            toDelete.Add(child.gameObject);
        }

        if (toDelete.Count == 0)
        {
            Debug.Log("No children to clear.");
            return;
        }

        Undo.SetCurrentGroupName("Clear Spawned Children");
        int g = Undo.GetCurrentGroup();
        foreach (var go in toDelete)
        {
            Undo.DestroyObjectImmediate(go);
        }
        Undo.CollapseUndoOperations(g);
    }

    // --- Compact grouped exporter ---
    private void SaveCompactGroupedJson(string path, List<SpawnRecord> records, bool recordedLocalSpace, int precision)
    {
        if (records == null || records.Count == 0) return;

        int prec = Mathf.Clamp(precision, 0, 9);
        string fmt = "F" + prec;
        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder(records.Count * 80);

        // Build prefab table: guid -> index
        var prefabIndexByGuid = new Dictionary<string, int>();
        var prefabGuidList = new List<string>();
        var prefabPathList = new List<string>();
        var prefabTypeList = new List<int>();

        foreach (var r in records)
        {
            string g = r.prefabGuid ?? "";
            if (!prefabIndexByGuid.ContainsKey(g))
            {
                int idx = prefabGuidList.Count;
                prefabIndexByGuid[g] = idx;
                prefabGuidList.Add(g);
                prefabPathList.Add(r.prefabPath ?? "");
                prefabTypeList.Add((int)r.type);
            }
        }

        // header / meta
        sb.Append("{\"meta\":{");
        sb.Append("\"v\":1,");
        sb.Append("\"local\":");
        sb.Append(recordedLocalSpace ? "true" : "false");
        sb.Append(",\"prec\":");
        sb.Append(prec);
        sb.Append("},");

        // prefabs array: [ [guid, path, type], ... ]
        sb.Append("\"prefabs\":[");
        for (int i = 0; i < prefabGuidList.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('[');
            sb.Append('\"').Append(EscapeJsonString(prefabGuidList[i])).Append("\",");
            sb.Append('\"').Append(EscapeJsonString(prefabPathList[i])).Append("\",");
            sb.Append(prefabTypeList[i]);
            sb.Append(']');
        }
        sb.Append("],");

        // instances array: [ [prefabIndex, px,py,pz, qx,qy,qz,qw, sx,sy,sz], ... ]
        sb.Append("\"instances\":[");
        for (int i = 0; i < records.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var r = records[i];

            int pidx;
            if (!prefabIndexByGuid.TryGetValue(r.prefabGuid ?? "", out pidx)) pidx = 0;

            sb.Append('[');
            // prefab index
            sb.Append(pidx).Append(',');

            // position
            sb.Append(r.position.x.ToString(fmt, ci)).Append(',');
            sb.Append(r.position.y.ToString(fmt, ci)).Append(',');
            sb.Append(r.position.z.ToString(fmt, ci)).Append(',');

            // rotation quaternion
            sb.Append(r.rotation.x.ToString(fmt, ci)).Append(',');
            sb.Append(r.rotation.y.ToString(fmt, ci)).Append(',');
            sb.Append(r.rotation.z.ToString(fmt, ci)).Append(',');
            sb.Append(r.rotation.w.ToString(fmt, ci)).Append(',');

            // scale
            sb.Append(r.scale.x.ToString(fmt, ci)).Append(',');
            sb.Append(r.scale.y.ToString(fmt, ci)).Append(',');
            sb.Append(r.scale.z.ToString(fmt, ci));

            sb.Append(']');
        }
        sb.Append("]}");

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    // small JSON string escaper for paths/GUIDs
    private string EscapeJsonString(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    // deterministic id based on prefab GUID + quantized position
    private string GenerateUniqueId(string prefabGuid, Vector3 pos, int precisionDigits = 3)
    {
        // precisionDigits: 3 => quantize to 10^3 (e.g. positions * 1000)
        int quant = 1;
        for (int i = 0; i < precisionDigits; i++) quant *= 10;
        long x = Mathf.RoundToInt(pos.x * quant);
        long y = Mathf.RoundToInt(pos.y * quant);
        long z = Mathf.RoundToInt(pos.z * quant);
        return $"{prefabGuid}_{x}_{y}_{z}";
    }

    // --- Helpers ---
    bool ValidateSetup()
    {
        if (spawnMask == null)
        {
            Debug.LogError("Missing spawn mask.", this);
            return false;
        }
        if (!spawnMask.isReadable)
        {
            Debug.LogError($"Spawn mask '{spawnMask.name}' is not readable. Enable Read/Write in import settings.", this);
            return false;
        }
        if (rend == null || meshCol == null)
        {
            Debug.LogError("Chunk requires a Renderer and a MeshCollider in children.", this);
            return false;
        }
        if (((1 << meshCol.gameObject.layer) & chunkLayer) == 0)
        {
            Debug.LogWarning($"MeshCollider '{meshCol.name}' is on layer {LayerMask.LayerToName(meshCol.gameObject.layer)} but chunkLayer mask does not include it. Rays may not hit.", this);
        }
        var mesh = meshCol.sharedMesh;
        if (mesh == null || mesh.uv == null || mesh.uv.Length != mesh.vertexCount)
        {
            Debug.LogError("MeshCollider's mesh has invalid UV0 for sampling.", this);
            return false;
        }
        return true;
    }

    struct TriCDF { public float[] cdf; public int triCount; }

    TriCDF BuildTriangleCDF(Vector3[] verts, int[] tris)
    {
        int tCount = tris.Length / 3;
        float totalArea = 0f;
        float[] areas = new float[tCount];

        for (int t = 0; t < tCount; t++)
        {
            int i0 = tris[t * 3 + 0];
            int i1 = tris[t * 3 + 1];
            int i2 = tris[t * 3 + 2];
            Vector3 v0 = verts[i0];
            Vector3 v1 = verts[i1];
            Vector3 v2 = verts[i2];
            float area = Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
            areas[t] = area;
            totalArea += area;
        }

        float[] cdf = new float[tCount];
        float accum = 0f;
        for (int t = 0; t < tCount; t++)
        {
            accum += (totalArea > 0f ? areas[t] / totalArea : 0f);
            cdf[t] = accum;
        }
        if (tCount > 0) cdf[tCount - 1] = 1f;

        return new TriCDF { cdf = cdf, triCount = tCount };
    }

    int SampleTriangleIndex(TriCDF data)
    {
        float r = Random.value;
        int lo = 0, hi = data.triCount - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (r <= data.cdf[mid]) hi = mid;
            else lo = mid + 1;
        }
        return lo;
    }

    void PrepareMaskCache()
    {
        if (spawnMask == null) { maskPixels = null; maskWidth = maskHeight = 0; return; }
        if (maskPixels != null && maskWidth == spawnMask.width && maskHeight == spawnMask.height) return; // already cached
        try
        {
            maskWidth = spawnMask.width;
            maskHeight = spawnMask.height;
            maskPixels = spawnMask.GetPixels();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to cache spawn mask pixels: {ex.Message}");
            maskPixels = null;
            maskWidth = maskHeight = 0;
        }
    }

    Color SampleMaskBilinear(float u, float v)
    {
        if (maskPixels == null || maskWidth == 0 || maskHeight == 0)
            return spawnMask != null ? spawnMask.GetPixelBilinear(u, v) : Color.black;

        // convert to pixel space (0..w-1, 0..h-1)
        float fx = u * (maskWidth - 1);
        float fy = v * (maskHeight - 1);
        int x = Mathf.FloorToInt(fx);
        int y = Mathf.FloorToInt(fy);
        int x1 = Mathf.Clamp(x + 1, 0, maskWidth - 1);
        int y1 = Mathf.Clamp(y + 1, 0, maskHeight - 1);
        float tx = fx - x;
        float ty = fy - y;

        Color c00 = maskPixels[y * maskWidth + x];
        Color c10 = maskPixels[y * maskWidth + x1];
        Color c01 = maskPixels[y1 * maskWidth + x];
        Color c11 = maskPixels[y1 * maskWidth + x1];

        Color cx0 = Color.Lerp(c00, c10, tx);
        Color cx1 = Color.Lerp(c01, c11, tx);
        return Color.Lerp(cx0, cx1, ty);
    }

    bool IsColorMatch(Color a, Color b, float tol, bool useEuclidean)
    {
        if (useEuclidean)
        {
            // Euclidean distance in RGB
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            return (dr * dr + dg * dg + db * db) <= tol * tol;
        }
        else
        {
            // per-channel tolerance
            return Mathf.Abs(a.r - b.r) <= tol && Mathf.Abs(a.g - b.g) <= tol && Mathf.Abs(a.b - b.b) <= tol;
        }
    }

    // Spatial hash grid for approximate min-separation checks (fast). Uses cell size = minSeparation.
    class SpatialHashGrid
    {
        private float cellSize;
        private float minDist;
        private Dictionary<Vector2Int, List<Vector3>> cells = new Dictionary<Vector2Int, List<Vector3>>();

        public SpatialHashGrid(float minSeparation)
        {
            this.minDist = minSeparation;
            this.cellSize = Mathf.Max(0.01f, minSeparation);
        }

        public bool IsTooClose(Vector3 p)
        {
            Vector2Int cell = Hash(p);
            float sq = minDist * minDist;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    Vector2Int n = new Vector2Int(cell.x + dx, cell.y + dz);
                    if (!cells.TryGetValue(n, out var list)) continue;
                    for (int i = 0; i < list.Count; i++)
                    {
                        if ((list[i] - p).sqrMagnitude < sq) return true;
                    }
                }
            return false;
        }

        public void Add(Vector3 p)
        {
            Vector2Int c = Hash(p);
            if (!cells.TryGetValue(c, out var list)) { list = new List<Vector3>(); cells[c] = list; }
            list.Add(p);
        }

        private Vector2Int Hash(Vector3 p)
        {
            return new Vector2Int(Mathf.FloorToInt(p.x / cellSize), Mathf.FloorToInt(p.z / cellSize));
        }
    }
}
#endif
