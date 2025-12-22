// UVDecorationSpawner.cs - Modified to work with GameObject prefabs
using UnityEngine;
using System.Collections.Generic;

public class UVDecorationSpawner : MonoBehaviour
{
    [System.Serializable]
    public class DecorationGroup
    {
        [Header("Mask Settings")]
        public Color maskColor = Color.green;
        [Range(0f, 1f)] public float colorTolerance = 0.1f;
        public bool useEuclideanColorTolerance = true;

        [Header("Spawn Settings")]
        public GameObject[] prefabs;
        public float density = 0.1f; // Objects per square unit
        public float minScale = 0.8f;
        public float maxScale = 1.2f;
        public float minYaw = 0f;
        public float maxYaw = 360f;

        [Header("Placement Filters")]
        [Range(0f, 90f)] public float maxSlope = 60f;
        public float minSeparation = 0f; // Keep objects apart
        public float yOffset = 0f; // Adjust height above ground

        [Header("Performance")]
        public int maxAttemptsMultiplier = 50;
    }

    [Header("UV Mask")]
    public Texture2D spawnMask;

    [Header("Terrain Prefab")]
    public GameObject terrainPrefab; // Drag terrain GameObject prefab here
    public Vector3 terrainPosition = Vector3.zero;
    public Vector3 terrainRotation = Vector3.zero;
    public Vector3 terrainScale = Vector3.one;

    [Header("Debug")]
    public bool spawnTerrainInstance = false; // Spawn a visible instance of the terrain for debugging
    private GameObject terrainInstance;

    [Header("Raycast Settings")]
    public bool usePhysicsRaycast = false; // Set to false for prefab-only mode
    public LayerMask groundLayer;
    public float raycastDownDistance = 100f;
    public float rayStartAbove = 50f;
    public bool alignToSurfaceNormal = false;

    [Header("General Settings")]
    public bool spawnOnStart = true;
    public int seed = 12345;
    public float spawnHeightOffset = 0.05f;

    [Header("Decoration Groups")]
    public DecorationGroup[] decorationGroups;

    // Cached mesh data
    private Mesh terrainMesh;
    private Vector3[] vertices;
    private int[] triangles;
    private Vector2[] uvs;
    private Vector3[] normals;

    private List<GameObject> spawnedDecorations = new List<GameObject>();
    private Color[] maskPixels;
    private int maskWidth, maskHeight;

    void Start()
    {
        if (spawnOnStart)
        {
            GenerateDecorations();
        }
    }

    void OnDestroy()
    {
        // Clean up terrain instance if it exists
        if (terrainInstance != null)
        {
            Destroy(terrainInstance);
        }
    }

    public void GenerateDecorations()
    {
        Random.InitState(seed);
        ClearDecorations();

        if (!ValidateSetup())
        {
            Debug.LogError("UVDecorationSpawner: Setup validation failed!");
            return;
        }

        PrepareMaskCache();
        ExtractMeshData();

        // Create transform matrix for the terrain
        Matrix4x4 terrainTransform = Matrix4x4.TRS(terrainPosition, Quaternion.Euler(terrainRotation), terrainScale);
        Matrix4x4 normalMatrix = terrainTransform.inverse.transpose;

        // Optionally spawn a visible instance of the terrain for debugging
        if (spawnTerrainInstance && terrainInstance == null)
        {
            terrainInstance = Instantiate(terrainPrefab, transform);
            terrainInstance.transform.position = terrainPosition;
            terrainInstance.transform.rotation = Quaternion.Euler(terrainRotation);
            terrainInstance.transform.localScale = terrainScale;
        }

        var cdfData = BuildTriangleCDF(vertices, triangles);

        foreach (var group in decorationGroups)
        {
            if (group.prefabs == null || group.prefabs.Length == 0)
            {
                Debug.LogWarning($"Group with color {group.maskColor} has no prefabs assigned!");
                continue;
            }

            float meshArea = CalculateMeshArea(vertices, triangles);
            int targetCount = Mathf.RoundToInt(meshArea * group.density);

            SpawnGroup(group, targetCount, cdfData, terrainTransform, normalMatrix);
        }

        Debug.Log($"UVDecorationSpawner: Spawned {spawnedDecorations.Count} decorations");
    }

    void ExtractMeshData()
    {
        if (terrainPrefab == null) return;

        // Try to get mesh from MeshFilter
        MeshFilter meshFilter = terrainPrefab.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            terrainMesh = meshFilter.sharedMesh;
        }
        else
        {
            // Try to get mesh from SkinnedMeshRenderer
            SkinnedMeshRenderer skinnedRenderer = terrainPrefab.GetComponent<SkinnedMeshRenderer>();
            if (skinnedRenderer != null && skinnedRenderer.sharedMesh != null)
            {
                terrainMesh = skinnedRenderer.sharedMesh;
            }
            else
            {
                Debug.LogError("Terrain prefab has no MeshFilter or SkinnedMeshRenderer with a mesh!");
                return;
            }
        }

        vertices = terrainMesh.vertices;
        triangles = terrainMesh.triangles;
        uvs = terrainMesh.uv;
        normals = terrainMesh.normals;
    }

    void SpawnGroup(DecorationGroup group, int targetCount, TriangleCDF cdfData,
                   Matrix4x4 terrainTransform, Matrix4x4 normalMatrix)
    {
        int spawned = 0;
        int attempts = 0;
        int maxAttempts = targetCount * group.maxAttemptsMultiplier;

        SpatialHashGrid grid = new SpatialHashGrid(group.minSeparation);

        while (spawned < targetCount && attempts < maxAttempts)
        {
            attempts++;

            // 1. Sample random triangle
            int triIndex = SampleTriangleIndex(cdfData);
            int i0 = triangles[triIndex * 3];
            int i1 = triangles[triIndex * 3 + 1];
            int i2 = triangles[triIndex * 3 + 2];

            // 2. Random barycentric coordinates
            float r1 = Random.value;
            float r2 = Random.value;
            float sqrtR1 = Mathf.Sqrt(r1);
            float b0 = 1f - sqrtR1;
            float b1 = sqrtR1 * (1f - r2);
            float b2 = sqrtR1 * r2;

            // 3. Interpolate position and UV
            Vector3 localPos = b0 * vertices[i0] + b1 * vertices[i1] + b2 * vertices[i2];
            Vector2 uv = b0 * uvs[i0] + b1 * uvs[i1] + b2 * uvs[i2];

            // 4. Sample mask color at UV
            uv.x = Mathf.Repeat(uv.x, 1f);
            uv.y = Mathf.Repeat(uv.y, 1f);
            Color maskColor = SampleMaskBilinear(uv.x, uv.y);

            // 5. Check if color matches group
            if (!IsColorMatch(maskColor, group.maskColor, group.colorTolerance, group.useEuclideanColorTolerance))
                continue;

            // 6. Transform local position to world space using the terrain transform
            Vector3 worldPos = terrainTransform.MultiplyPoint3x4(localPos);

            // 7. Calculate ground position and normal
            Vector3 groundPos;
            Vector3 groundNormal;

            if (usePhysicsRaycast)
            {
                // Optional: Raycast into the scene
                Vector3 rayStart = worldPos + Vector3.up * rayStartAbove;
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, raycastDownDistance, groundLayer))
                {
                    groundPos = hit.point;
                    groundNormal = hit.normal;
                }
                else
                {
                    // Fall back to mesh surface
                    groundPos = worldPos;
                    groundNormal = CalculateInterpolatedNormal(b0, b1, b2, i0, i1, i2, normals, normalMatrix);
                }
            }
            else
            {
                // Use mesh surface directly
                groundPos = worldPos;
                groundNormal = CalculateInterpolatedNormal(b0, b1, b2, i0, i1, i2, normals, normalMatrix);
            }

            // 8. Slope check
            float slope = Vector3.Angle(groundNormal, Vector3.up);
            if (slope > group.maxSlope)
                continue;

            // 9. Minimum separation check
            if (group.minSeparation > 0 && grid.IsTooClose(groundPos))
                continue;

            // 10. Choose random prefab
            GameObject prefab = group.prefabs[Random.Range(0, group.prefabs.Length)];
            if (prefab == null)
                continue;

            // 11. Calculate spawn position
            Vector3 spawnPos = groundPos + groundNormal * (spawnHeightOffset + group.yOffset);

            // 12. Calculate rotation
            Quaternion rotation;
            if (alignToSurfaceNormal)
            {
                Vector3 forward = Vector3.Cross(groundNormal, Vector3.right).normalized;
                if (forward.sqrMagnitude < 0.01f)
                    forward = Vector3.Cross(groundNormal, Vector3.forward).normalized;

                float yaw = Random.Range(group.minYaw, group.maxYaw);
                rotation = Quaternion.AngleAxis(yaw, groundNormal) * Quaternion.LookRotation(forward, groundNormal);
            }
            else
            {
                rotation = Quaternion.Euler(0, Random.Range(group.minYaw, group.maxYaw), 0);
            }

            // 13. Calculate scale
            float scale = Random.Range(group.minScale, group.maxScale);

            // 14. Spawn the decoration
            GameObject decoration = Instantiate(prefab, spawnPos, rotation, transform);
            decoration.transform.localScale = Vector3.one * scale;

            spawnedDecorations.Add(decoration);
            grid.Add(groundPos);
            spawned++;
        }

        if (spawned < targetCount)
        {
            Debug.LogWarning($"Group {group.maskColor}: Only spawned {spawned}/{targetCount} after {attempts} attempts");
        }
        else
        {
            Debug.Log($"Group {group.maskColor}: Successfully spawned {spawned} objects");
        }
    }

    Vector3 CalculateInterpolatedNormal(float b0, float b1, float b2, int i0, int i1, int i2,
                                        Vector3[] normals, Matrix4x4 normalMatrix)
    {
        if (normals != null && normals.Length > i2)
        {
            Vector3 localNormal = (b0 * normals[i0] + b1 * normals[i1] + b2 * normals[i2]).normalized;
            return normalMatrix.MultiplyVector(localNormal).normalized;
        }
        return Vector3.up;
    }

    public void ClearDecorations()
    {
        foreach (GameObject deco in spawnedDecorations)
        {
            if (deco != null) Destroy(deco);
        }
        spawnedDecorations.Clear();

        // Optionally clear the terrain instance
        if (terrainInstance != null && !spawnTerrainInstance)
        {
            Destroy(terrainInstance);
            terrainInstance = null;
        }
    }

    #region Helper Methods

    bool ValidateSetup()
    {
        if (spawnMask == null)
        {
            Debug.LogError("No spawn mask assigned!");
            return false;
        }

        if (!spawnMask.isReadable)
        {
            Debug.LogError($"Spawn mask '{spawnMask.name}' is not Read/Write enabled!");
            return false;
        }

        if (terrainPrefab == null)
        {
            Debug.LogError("No terrain prefab assigned!");
            return false;
        }

        // Check if prefab has mesh data
        MeshFilter meshFilter = terrainPrefab.GetComponent<MeshFilter>();
        SkinnedMeshRenderer skinnedRenderer = terrainPrefab.GetComponent<SkinnedMeshRenderer>();

        Mesh testMesh = null;
        if (meshFilter != null) testMesh = meshFilter.sharedMesh;
        else if (skinnedRenderer != null) testMesh = skinnedRenderer.sharedMesh;

        if (testMesh == null)
        {
            Debug.LogError("Terrain prefab has no mesh!");
            return false;
        }

        if (testMesh.uv == null || testMesh.uv.Length == 0)
        {
            Debug.LogError("Terrain mesh has no UV coordinates!");
            return false;
        }

        return true;
    }

    void PrepareMaskCache()
    {
        maskWidth = spawnMask.width;
        maskHeight = spawnMask.height;
        maskPixels = spawnMask.GetPixels();
    }

    Color SampleMaskBilinear(float u, float v)
    {
        if (maskPixels == null || maskWidth == 0 || maskHeight == 0)
        {
            PrepareMaskCache();
            if (maskPixels == null) return Color.black;
        }

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

    bool IsColorMatch(Color a, Color b, float tolerance, bool useEuclidean)
    {
        if (useEuclidean)
        {
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            return (dr * dr + dg * dg + db * db) <= tolerance * tolerance;
        }
        else
        {
            return Mathf.Abs(a.r - b.r) <= tolerance &&
                   Mathf.Abs(a.g - b.g) <= tolerance &&
                   Mathf.Abs(a.b - b.b) <= tolerance;
        }
    }

    struct TriangleCDF
    {
        public float[] cdf;
        public int triangleCount;
    }

    TriangleCDF BuildTriangleCDF(Vector3[] vertices, int[] triangles)
    {
        int triCount = triangles.Length / 3;
        float totalArea = 0;
        float[] areas = new float[triCount];

        for (int i = 0; i < triCount; i++)
        {
            int i0 = triangles[i * 3];
            int i1 = triangles[i * 3 + 1];
            int i2 = triangles[i * 3 + 2];

            Vector3 v0 = vertices[i0];
            Vector3 v1 = vertices[i1];
            Vector3 v2 = vertices[i2];

            float area = Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
            areas[i] = area;
            totalArea += area;
        }

        float[] cdf = new float[triCount];
        float accum = 0;
        for (int i = 0; i < triCount; i++)
        {
            accum += areas[i] / totalArea;
            cdf[i] = accum;
        }

        return new TriangleCDF { cdf = cdf, triangleCount = triCount };
    }

    int SampleTriangleIndex(TriangleCDF data)
    {
        float r = Random.value;
        int left = 0, right = data.triangleCount - 1;

        while (left < right)
        {
            int mid = (left + right) / 2;
            if (r <= data.cdf[mid])
                right = mid;
            else
                left = mid + 1;
        }

        return left;
    }

    float CalculateMeshArea(Vector3[] vertices, int[] triangles)
    {
        float area = 0;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v0 = vertices[triangles[i]];
            Vector3 v1 = vertices[triangles[i + 1]];
            Vector3 v2 = vertices[triangles[i + 2]];

            area += Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
        }
        return area;
    }

    class SpatialHashGrid
    {
        private float cellSize;
        private float minDistSqr;
        private Dictionary<Vector2Int, List<Vector3>> cells = new Dictionary<Vector2Int, List<Vector3>>();

        public SpatialHashGrid(float minSeparation)
        {
            this.cellSize = Mathf.Max(0.1f, minSeparation);
            this.minDistSqr = minSeparation * minSeparation;
        }

        public bool IsTooClose(Vector3 point)
        {
            if (minDistSqr <= 0) return false;

            Vector2Int cell = GetCell(point);
            float sqrDist;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    Vector2Int neighbor = new Vector2Int(cell.x + dx, cell.y + dz);
                    if (cells.TryGetValue(neighbor, out var points))
                    {
                        foreach (var p in points)
                        {
                            sqrDist = (p.x - point.x) * (p.x - point.x) +
                                      (p.z - point.z) * (p.z - point.z);
                            if (sqrDist < minDistSqr)
                                return true;
                        }
                    }
                }
            }
            return false;
        }

        public void Add(Vector3 point)
        {
            if (minDistSqr <= 0) return;

            Vector2Int cell = GetCell(point);
            if (!cells.ContainsKey(cell))
                cells[cell] = new List<Vector3>();

            cells[cell].Add(point);
        }

        private Vector2Int GetCell(Vector3 point)
        {
            return new Vector2Int(
                Mathf.FloorToInt(point.x / cellSize),
                Mathf.FloorToInt(point.z / cellSize)
            );
        }
    }

    #endregion

    #region Editor Tools

    [ContextMenu("Generate Decorations")]
    void GenerateInEditor()
    {
        GenerateDecorations();
    }

    [ContextMenu("Clear Decorations")]
    void ClearInEditor()
    {
        ClearDecorations();
    }

    #endregion
}