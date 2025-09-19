#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Optimized Editor-only UV-mask-based spawner for ANY BaseResource prefab.
/// Improvements over original:
/// - Caches mask pixels to avoid GetPixelBilinear() overhead.
/// - Uses barycentric interpolation of vertex normals (optional) to avoid physics raycasts.
/// - Spatial hash grid for O(1) approximate min-separation checks.
/// - Better color matching (Euclidean distance), configurable tolerance mode.
/// - Reduced Undo overhead: groups undo operations so Undo history is compact.
/// - Prefab and BaseResource caching.
/// - Minor cleanliness/robustness fixes.
///
/// Notes:
/// - Still Editor-only (uses PrefabUtility, AssetDatabase, Undo, etc.).
/// - Keep a runtime system if you want to reconstruct placements at runtime from records.
/// </summary>
[ExecuteInEditMode]
public class UVMapResourceSpawner : MonoBehaviour
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
        public string prefabGuid;     // Editor-only stable reference
        public ResourceType type;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }

    //[Header("Debug / Session Output (Editor)")]
    //public List<SpawnRecord> lastSessionRecords = new List<SpawnRecord>();

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

    #region Context Menu Commands

    [Header("Spawn Options (Editor)")]
    public bool alignToSurfaceNormal = false; // optional: rotate to face surface normal

    [ContextMenu("Spawn All Groups")]
    public void SpawnAllGroups()
    {
        if (!ValidateSetup()) return;

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

        Undo.SetCurrentGroupName("Spawn Resources");
        int undoGroup = Undo.GetCurrentGroup();

        //lastSessionRecords.Clear();

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
                        isValid = p.GetComponent<BaseResource>() != null;
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

                    // 8) Instantiate - use PrefabUtility to keep prefab link, then parent to this transform
                    var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    if (obj == null)
                    {
                        Debug.LogWarning("Prefab instantiation failed for one prefab in group " + group.resourceType);
                        continue;
                    }

                    // Parent and place
                    obj.transform.SetParent(this.transform, true);
                    obj.transform.position = worldPoint + sampleNormal * spawnHeightOffset;

                    // Rotation: optionally align to surface normal
                    if (alignToSurfaceNormal)
                    {
                        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, sampleNormal).normalized;
                        if (forward.sqrMagnitude < 1e-6f) forward = Vector3.Cross(sampleNormal, Vector3.right).normalized;
                        float yaw = Random.Range(minYaw, maxYaw);
                        Quaternion yawRot = Quaternion.AngleAxis(yaw, sampleNormal);
                        obj.transform.rotation = yawRot * Quaternion.LookRotation(forward, sampleNormal);
                    }
                    else
                    {
                        float yaw = Random.Range(minYaw, maxYaw);
                        obj.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    }

                    // Scale (with optional parent-scale compensation)
                    float rndScale = Random.Range(minScale, maxScale);
                    if (compensateParentScale)
                    {
                        Vector3 parentLossy = transform.lossyScale;
                        float fixX = parentLossy.x != 0f ? 1f / parentLossy.x : 1f;
                        float fixY = parentLossy.y != 0f ? 1f / parentLossy.y : 1f;
                        float fixZ = parentLossy.z != 0f ? 1f / parentLossy.z : 1f;
                        obj.transform.localScale = new Vector3(rndScale * fixX, rndScale * fixY, rndScale * fixZ);
                    }
                    else
                    {
                        obj.transform.localScale = Vector3.one * rndScale;
                    }

                    // Register create undo per-object (will be grouped)
                    Undo.RegisterCreatedObjectUndo(obj, "Spawn Resource");

                    // 9) Record (editor convenience, GUID is stable in editor)
                    string guid = AssetDatabase.AssetPathToGUID(
                        AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(obj))
                    );

                    /*lastSessionRecords.Add(new SpawnRecord
                    {
                        prefabGuid = guid,
                        type = group.resourceType,
                        position = obj.transform.position,
                        rotation = obj.transform.rotation,
                        scale = obj.transform.localScale
                    });*/

                    grid.Add(obj.transform.position);
                    placed++;
                }

                Debug.Log($"Group {group.resourceType}: placed {placed}/{group.spawnCount} after {attempts} attempts.", this);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Random.state = previousRandState; // restore RNG state

            // collapse undo group so all creations are one undo step
            Undo.CollapseUndoOperations(undoGroup);
        }

        //Debug.Log($"Spawn finished. Total new records: {lastSessionRecords.Count}", this);
    }

    [ContextMenu("Scan Existing (Children → Records)")]
    public void ScanExisting()
    {
        if (!ValidateSetup()) return;
        //lastSessionRecords.Clear();

        foreach (Transform child in transform)
        {
            var br = child.GetComponent<BaseResource>();
            if (br == null) continue;

            string guid = AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject))
            );

            /*lastSessionRecords.Add(new SpawnRecord
            {
                prefabGuid = guid,
                type = br.GetResourceType(),
                position = child.position,
                rotation = child.rotation,
                scale = child.localScale
            });*/
        }

        //Debug.Log($"Scanned {lastSessionRecords.Count} BaseResource children → records.", this);
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
    #endregion

    #region Helpers
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
        float x = u * (maskWidth - 1);
        float y = v * (maskHeight - 1);
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        int x1 = Mathf.Min(x0 + 1, maskWidth - 1);
        int y1 = Mathf.Min(y0 + 1, maskHeight - 1);
        float tx = x - x0;
        float ty = y - y0;

        Color c00 = maskPixels[y0 * maskWidth + x0];
        Color c10 = maskPixels[y0 * maskWidth + x1];
        Color c01 = maskPixels[y1 * maskWidth + x0];
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
        private Dictionary<long, List<Vector3>> buckets = new Dictionary<long, List<Vector3>>();

        public SpatialHashGrid(float minSeparation)
        {
            cellSize = Mathf.Max(0.0001f, minSeparation);
        }

        private long HashCoords(int x, int y, int z)
        {
            // pack small ints into a long
            unchecked
            {
                long lx = (long)x & 0xffffffffL;
                long ly = (long)y & 0xffffffffL;
                long lz = (long)z & 0xffffffffL;
                return (lx) | (ly << 21) | (lz << 42);
            }
        }

        private void GetCell(Vector3 p, out int cx, out int cy, out int cz)
        {
            cx = Mathf.FloorToInt(p.x / cellSize);
            cy = Mathf.FloorToInt(p.y / cellSize);
            cz = Mathf.FloorToInt(p.z / cellSize);
        }

        public bool IsTooClose(Vector3 p)
        {
            GetCell(p, out int cx, out int cy, out int cz);
            float sq = cellSize * cellSize;
            for (int z = cz - 1; z <= cz + 1; z++)
                for (int y = cy - 1; y <= cy + 1; y++)
                    for (int x = cx - 1; x <= cx + 1; x++)
                    {
                        long key = HashCoords(x, y, z);
                        if (!buckets.TryGetValue(key, out var list)) continue;
                        for (int i = 0; i < list.Count; i++)
                        {
                            if ((list[i] - p).sqrMagnitude < sq) return true;
                        }
                    }
            return false;
        }

        public void Add(Vector3 p)
        {
            GetCell(p, out int cx, out int cy, out int cz);
            long key = HashCoords(cx, cy, cz);
            if (!buckets.TryGetValue(key, out var list)) { list = new List<Vector3>(); buckets[key] = list; }
            list.Add(p);
        }
    }
    #endregion
}
#endif
