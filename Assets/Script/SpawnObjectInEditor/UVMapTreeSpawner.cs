#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[ExecuteInEditMode]
public class UVMapTreeSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public Texture2D spawnMask;                 // Painted UV map
    public Color targetColor = Color.green;     // Mask color to allow
    [Range(0f, 1f)] public float colorTolerance = 0.1f; // per-channel tolerance
    public GameObject[] treePrefabs;
    public int spawnCount = 100;

    [Tooltip("Random scale applied to spawned trees (before compensating parent scale).")]
    public float minScale = 0.9f;
    public float maxScale = 1.1f;

    [Tooltip("Lift trees slightly to avoid clipping.")]
    public float spawnHeightOffset = 0.05f;

    [Header("Raycast")]
    [Tooltip("Layer of THIS chunk's MeshCollider only. Keeps rays from hitting neighbors.")]
    public LayerMask chunkLayer;
    [Tooltip("How high above the sampled surface point to start the downward ray.")]
    public float rayStartAbove = 5f;
    [Tooltip("How far downward to cast the ray.")]
    public float raycastDownDistance = 200f;

    [Header("Advanced")]
    [Tooltip("Max attempts = spawnCount * maxAttemptsMultiplier.")]
    public int maxAttemptsMultiplier = 50;
    [Tooltip("Compensate for parent scaling so trees look correct in world space.")]
    public bool compensateParentScale = true;

    [Header("Runtime/Editor Tree Data")]
    public List<TreeData> treeDatas = new List<TreeData>();

    private Renderer rend;
    private MeshCollider meshCol;

    [System.Serializable]
    public class TreeData
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public int prefabIndex; // Index in treePrefabs array
        public bool isCut = false;
    }

    void OnValidate()
    {
        rend = GetComponentInChildren<Renderer>();
        meshCol = GetComponentInChildren<MeshCollider>();

        if (!rend || !meshCol)
            Debug.LogWarning($"{name}: Missing Renderer or MeshCollider on this chunk!");

        if (spawnMask != null && !spawnMask.isReadable)
            Debug.LogError($"Spawn mask '{spawnMask.name}' is not Read/Write enabled. Enable Read/Write in its import settings.");
    }

    [ContextMenu("Scan Existing Trees")]
    void ScanExistingTrees()
    {
        treeDatas.Clear();

        foreach (Transform child in transform)
        {
            // Try to find prefab index by matching name start
            int prefabIndex = -1;
            for (int i = 0; i < treePrefabs.Length; i++)
            {
                if (child.name.StartsWith(treePrefabs[i].name))
                {
                    prefabIndex = i;
                    break;
                }
            }

            TreeData data = new TreeData()
            {
                position = child.position,
                rotation = child.rotation,
                scale = child.localScale,
                prefabIndex = prefabIndex
            };

            treeDatas.Add(data);
        }

        Debug.Log($"Scanned {treeDatas.Count} trees.");
    }

    [ContextMenu("Spawn Trees From Data")]
    void SpawnFromData()
    {
        foreach (var data in treeDatas)
        {
            if (data.prefabIndex < 0 || data.prefabIndex >= treePrefabs.Length) continue;
            var prefab = treePrefabs[data.prefabIndex];
            var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, this.transform);
            tree.transform.position = data.position;
            tree.transform.rotation = data.rotation;
            tree.transform.localScale = data.scale;
        }
    }

    [ContextMenu("Spawn Trees")]
    void SpawnTrees()
    {
        if (!ValidateSetup()) return;

        var mesh = meshCol.sharedMesh;
        var tris = mesh.triangles;
        var verts = mesh.vertices;
        var uvs = mesh.uv;

        if (uvs == null || uvs.Length != verts.Length)
        {
            Debug.LogError("Mesh has no valid UV0 for mask sampling.");
            return;
        }

        // Precompute triangle areas for area-weighted sampling (uniform over surface)
        var triAreas = new float[tris.Length / 3];
        float totalArea = 0f;
        for (int t = 0; t < triAreas.Length; t++)
        {
            int i0 = tris[t * 3 + 0];
            int i1 = tris[t * 3 + 1];
            int i2 = tris[t * 3 + 2];

            Vector3 v0 = verts[i0];
            Vector3 v1 = verts[i1];
            Vector3 v2 = verts[i2];

            float area = Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
            triAreas[t] = area;
            totalArea += area;
        }

        // Cumulative distribution for weighted random
        var cdf = new float[triAreas.Length];
        float accum = 0f;
        for (int t = 0; t < triAreas.Length; t++)
        {
            accum += triAreas[t] / totalArea;
            cdf[t] = accum;
        }

        int placed = 0;
        int attempts = 0;
        int maxAttempts = Mathf.Max(spawnCount * Mathf.Max(1, maxAttemptsMultiplier), spawnCount);

        Undo.RegisterFullObjectHierarchyUndo(gameObject, "Spawn Trees");

        while (placed < spawnCount && attempts < maxAttempts)
        {
            attempts++;

            // 1) Pick a triangle by area-weighted random
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

            // 2) Random barycentric point (uniform over triangle)
            float r1 = Random.value;
            float r2 = Random.value;
            float sqrtR1 = Mathf.Sqrt(r1);
            float b0 = 1f - sqrtR1;
            float b1 = sqrtR1 * (1f - r2);
            float b2 = sqrtR1 * r2;

            Vector3 localPoint = b0 * v0 + b1 * v1 + b2 * v2;
            Vector2 uv = b0 * uv0 + b1 * uv1 + b2 * uv2;

            // 3) Check mask color at UV
            Color px = spawnMask.GetPixelBilinear(uv.x, uv.y);
            if (!IsColorMatch(px, targetColor, colorTolerance)) continue;

            // 4) Convert to world and do a short, guaranteed downward ray to snap to surface
            Vector3 worldSurfaceApprox = meshCol.transform.TransformPoint(localPoint);
            Vector3 rayOrigin = worldSurfaceApprox + Vector3.up * rayStartAbove;

            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDownDistance, chunkLayer)) continue;
            if (hit.collider != meshCol) continue;

            // 5) Spawn
            GameObject prefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
            var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, this.transform);

            // Position & rotation
            tree.transform.position = hit.point + Vector3.up * spawnHeightOffset;
            tree.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            // Scale (with optional parent-scale compensation)
            float rndScale = Random.Range(minScale, maxScale);
            if (compensateParentScale)
            {
                Vector3 parentLossy = transform.lossyScale;
                float fixX = parentLossy.x != 0 ? 1f / parentLossy.x : 1f;
                float fixY = parentLossy.y != 0 ? 1f / parentLossy.y : 1f;
                float fixZ = parentLossy.z != 0 ? 1f / parentLossy.z : 1f;
                tree.transform.localScale = new Vector3(rndScale * fixX, rndScale * fixY, rndScale * fixZ);
            }
            else
            {
                tree.transform.localScale = Vector3.one * rndScale;
            }

            Undo.RegisterCreatedObjectUndo(tree, "Spawn Tree");

            // Save tree data
            TreeData newData = new TreeData()
            {
                position = tree.transform.position,
                rotation = tree.transform.rotation,
                scale = tree.transform.localScale,
                prefabIndex = System.Array.IndexOf(treePrefabs, prefab),
                isCut = false
            };
            treeDatas.Add(newData);

            placed++;
        }

        Debug.Log($"Tree spawn finished: placed {placed}/{spawnCount} after {attempts} attempts. " +
                  $"(Mask: {(spawnMask ? spawnMask.name : "null")}, Chunk: {name})", this);
    }

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
        if (treePrefabs == null || treePrefabs.Length == 0)
        {
            Debug.LogError("No tree prefabs assigned.", this);
            return false;
        }
        if (rend == null || meshCol == null)
        {
            Debug.LogError("Chunk requires a Renderer and a MeshCollider in children.", this);
            return false;
        }
        if (((1 << meshCol.gameObject.layer) & chunkLayer) == 0)
        {
            Debug.LogWarning($"MeshCollider '{meshCol.name}' is on layer {LayerMask.LayerToName(meshCol.gameObject.layer)} " +
                             $"but chunkLayer mask does not include it. Rays may not hit.", this);
        }
        return true;
    }

    static int SampleTriangleIndex(float[] cdf)
    {
        float r = Random.value;
        int lo = 0, hi = cdf.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (r <= cdf[mid]) hi = mid; else lo = mid + 1;
        }
        return lo;
    }

    bool IsColorMatch(Color a, Color b, float tol)
    {
        return Mathf.Abs(a.r - b.r) <= tol &&
               Mathf.Abs(a.g - b.g) <= tol &&
               Mathf.Abs(a.b - b.b) <= tol;
    }
}
#endif
