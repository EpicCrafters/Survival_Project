using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Sub-mesh configuration for multi-material instancing
/// </summary>
[System.Serializable]
public class InstancedSubMesh
{
    public int subMeshIndex = 0;
    public Material material;
}

/// <summary>
/// Configuration for a type of resource (tree, bush, rock, etc)
/// </summary>
[System.Serializable]
public class ResourceTypeConfig
{
    public string typeName = "Tree";
    [Tooltip("Prefab GUID or path pattern to match")]
    public string prefabIdentifier;

    [Header("Instancing")]
    public Mesh instancedMesh;
    [Tooltip("Single material for simple objects")]
    public Material instancedMaterial;
    [Tooltip("Multiple materials for complex objects (leaves + trunk). Leave empty to use single material.")]
    public InstancedSubMesh[] subMeshes;

    [Header("Interactive")]
    public GameObject interactivePrefab;

    public bool HasMultipleMaterials => subMeshes != null && subMeshes.Length > 0;
}

/// <summary>
/// GPU instancing for distant resources, individual interactive resources when close.
/// Works with ResourceManagerRouter for multiplayer support.
/// </summary>
public class TreeLODSystem : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public ResourceManagerRouter router;

    [Header("LOD Settings")]
    [Tooltip("Distance where resources become interactive")]
    public float interactiveDistance = 40f;
    [Tooltip("Distance where resources deactivate (should be > interactive distance)")]
    public float deactivateDistance = 50f;
    [Tooltip("Max resources to activate per frame (spread load)")]
    public int maxActivationsPerFrame = 5;

    [Header("Resource Type Definitions")]
    public ResourceTypeConfig[] resourceTypes;

    [Header("Spatial Optimization")]
    [Tooltip("Grid cell size for spatial partitioning")]
    public float spatialGridCellSize = 100f;
    public bool useOctree = false;

    [Header("Debug")]
    public bool showDebugGizmos = false;
    public bool verboseLogs = false;

    // Map: resourceType -> (uniqueId -> GameObject)
    private Dictionary<string, Dictionary<string, GameObject>> activeResourcesByType =
        new Dictionary<string, Dictionary<string, GameObject>>();

    // Active interactive resources (uniqueId -> GameObject)
    private Dictionary<string, GameObject> allActiveResources = new Dictionary<string, GameObject>();

    // All resource managers in the scene
    private List<ResourceManager> resourceManagers = new List<ResourceManager>();

    // Spatial grid for efficient distance checks (uniqueId -> position)
    private Dictionary<Vector2Int, List<ResourceEntry>> spatialGrid =
        new Dictionary<Vector2Int, List<ResourceEntry>>();

    // Helper class to store resource data
    private class ResourceEntry
    {
        public string uniqueId;
        public Vector3 position;
        public ResourceManager manager;
        public string resourceType;
    }

    // Instance rendering data per resource type
    private Dictionary<string, List<Matrix4x4>> instanceMatricesByType =
        new Dictionary<string, List<Matrix4x4>>();

    // Batch processing
    private Queue<string> activationQueue = new Queue<string>();
    private Queue<string> deactivationQueue = new Queue<string>();

    void Start()
    {
        if (player == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        if (router == null)
            router = FindObjectOfType<ResourceManagerRouter>();

        // Find all ResourceManagers in the scene
        resourceManagers = ResourceManager.GetAllManagers().ToList();

        if (resourceManagers.Count == 0)
        {
            Debug.LogError("[TreeLODSystem] No ResourceManagers found in scene!");
            enabled = false;
            return;
        }

        // Initialize resource type dictionaries
        foreach (var config in resourceTypes)
        {
            activeResourcesByType[config.typeName] = new Dictionary<string, GameObject>();
            instanceMatricesByType[config.typeName] = new List<Matrix4x4>();
        }

        // Build spatial grid from all managers
        BuildSpatialGrid();

        // Subscribe to resource state changes from all managers
        foreach (var manager in resourceManagers)
        {
            if (manager != null)
                manager.OnResourceStateChanged += OnResourceStateChanged;
        }

        if (verboseLogs)
        {
            int totalResources = resourceManagers.Sum(m => m.core.recordsById.Count);
            Debug.Log($"[TreeLODSystem] Initialized with {resourceManagers.Count} managers, {totalResources} total resources");
        }
    }

    void OnDestroy()
    {
        foreach (var manager in resourceManagers)
        {
            if (manager != null)
                manager.OnResourceStateChanged -= OnResourceStateChanged;
        }
    }

    void Update()
    {
        if (player == null) return;

        UpdateResourceStates();
        ProcessActivationQueues();
        RenderInstancedResources();
    }

    /// <summary>
    /// Build spatial grid for efficient neighbor queries from ALL managers
    /// </summary>
    void BuildSpatialGrid()
    {
        spatialGrid.Clear();

        foreach (var manager in resourceManagers)
        {
            if (manager == null) continue;

            foreach (var kvp in manager.core.recordsById)
            {
                if (kvp.Value.isChopped) continue;

                string resourceType = DetermineResourceType(kvp.Value);

                var entry = new ResourceEntry
                {
                    uniqueId = kvp.Key,
                    position = kvp.Value.position,
                    manager = manager,
                    resourceType = resourceType
                };

                Vector2Int cell = GetGridCell(kvp.Value.position);
                if (!spatialGrid.ContainsKey(cell))
                    spatialGrid[cell] = new List<ResourceEntry>();

                spatialGrid[cell].Add(entry);
            }
        }

        if (verboseLogs)
        {
            int totalEntries = spatialGrid.Values.Sum(list => list.Count);
            Debug.Log($"[TreeLODSystem] Built spatial grid with {spatialGrid.Count} cells, {totalEntries} resources");
        }
    }

    /// <summary>
    /// Determine resource type from spawn record (match by prefab path/guid)
    /// </summary>
    string DetermineResourceType(SpawnRecord record)
    {
        foreach (var config in resourceTypes)
        {
            if (!string.IsNullOrEmpty(record.prefabPath) &&
                record.prefabPath.Contains(config.prefabIdentifier))
                return config.typeName;

            if (!string.IsNullOrEmpty(record.prefabGuid) &&
                record.prefabGuid == config.prefabIdentifier)
                return config.typeName;
        }

        return "Unknown";
    }

    /// <summary>
    /// Get resource type config by name
    /// </summary>
    ResourceTypeConfig GetResourceConfig(string typeName)
    {
        foreach (var config in resourceTypes)
        {
            if (config.typeName == typeName)
                return config;
        }
        return null;
    }

    Vector2Int GetGridCell(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / spatialGridCellSize),
            Mathf.FloorToInt(worldPos.z / spatialGridCellSize)
        );
    }

    /// <summary>
    /// Get nearby resource entries efficiently using spatial grid
    /// </summary>
    IEnumerable<ResourceEntry> GetNearbyResources(Vector3 position, float radius)
    {
        Vector2Int centerCell = GetGridCell(position);
        int cellRadius = Mathf.CeilToInt(radius / spatialGridCellSize);

        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int z = -cellRadius; z <= cellRadius; z++)
            {
                Vector2Int cell = new Vector2Int(centerCell.x + x, centerCell.y + z);
                if (spatialGrid.TryGetValue(cell, out var entries))
                {
                    foreach (var entry in entries)
                        yield return entry;
                }
            }
        }
    }

    /// <summary>
    /// Check distances and queue activation/deactivation
    /// </summary>
    void UpdateResourceStates()
    {
        if (player == null) return;

        Vector3 playerPos = player.position;

        // Only check nearby resources using spatial grid
        foreach (var entry in GetNearbyResources(playerPos, deactivateDistance))
        {
            if (entry.manager == null ||
                !entry.manager.core.recordsById.TryGetValue(entry.uniqueId, out var record))
                continue;

            if (record.isChopped) continue;

            float distSqr = (record.position - playerPos).sqrMagnitude;
            float dist = Mathf.Sqrt(distSqr);

            bool isActive = allActiveResources.ContainsKey(entry.uniqueId);

            // Activate close resources
            if (dist < interactiveDistance && !isActive)
            {
                if (!activationQueue.Contains(entry.uniqueId))
                    activationQueue.Enqueue(entry.uniqueId);
            }
            // Deactivate far resources (with hysteresis)
            else if (dist > deactivateDistance && isActive)
            {
                if (!deactivationQueue.Contains(entry.uniqueId))
                    deactivationQueue.Enqueue(entry.uniqueId);
            }
        }
    }

    /// <summary>
    /// Process activation/deactivation queues with per-frame budget
    /// </summary>
    void ProcessActivationQueues()
    {
        int processed = 0;

        // Deactivations first (cheaper)
        while (deactivationQueue.Count > 0 && processed < maxActivationsPerFrame * 2)
        {
            string id = deactivationQueue.Dequeue();
            DeactivateResource(id);
            processed++;
        }

        // Activations
        processed = 0;
        while (activationQueue.Count > 0 && processed < maxActivationsPerFrame)
        {
            string id = activationQueue.Dequeue();
            ActivateResource(id);
            processed++;
        }
    }

    /// <summary>
    /// Spawn interactive resource instance
    /// </summary>
    void ActivateResource(string uniqueId)
    {
        if (allActiveResources.ContainsKey(uniqueId)) return;

        // Find which manager owns this resource
        ResourceManager owningManager = ResourceManager.GetManagerForUniqueId(uniqueId);
        if (owningManager == null) return;

        if (!owningManager.core.recordsById.TryGetValue(uniqueId, out var record))
            return;

        string resourceType = DetermineResourceType(record);
        ResourceTypeConfig config = GetResourceConfig(resourceType);

        if (config == null || config.interactivePrefab == null)
        {
            if (verboseLogs)
                Debug.LogWarning($"[TreeLODSystem] No config or prefab for resource type '{resourceType}'");
            return;
        }

        GameObject resource = Instantiate(
            config.interactivePrefab,
            record.position,
            record.rotation,
            transform
        );

        resource.transform.localScale = record.scale;

        // Set up the resource's unique ID so it works with ResourceManager
        var visual = resource.GetComponent<ResourceInstanceVisual>();
        if (visual != null)
        {
            visual.SetUniqueId(uniqueId);
            visual.isChopped = record.isChopped;
            visual.ApplyState();
        }

        var baseResource = resource.GetComponent<BaseResource>();
        if (baseResource != null)
        {
            baseResource.SetUniqueId(uniqueId);

            var healthSystem = baseResource.GetHealthSystem();
            if (healthSystem != null)
                healthSystem.SetHealth(record.curHealth);
        }

        // Register with the owning ResourceManager
        if (visual != null)
            owningManager.RegisterInstance(visual);

        allActiveResources[uniqueId] = resource;

        if (!activeResourcesByType[resourceType].ContainsKey(uniqueId))
            activeResourcesByType[resourceType][uniqueId] = resource;

        if (verboseLogs)
            Debug.Log($"[TreeLODSystem] Activated {resourceType} {uniqueId} at {record.position}");
    }

    /// <summary>
    /// Destroy interactive resource, return to instanced rendering
    /// </summary>
    void DeactivateResource(string uniqueId)
    {
        if (!allActiveResources.TryGetValue(uniqueId, out var resource))
            return;

        if (resource != null)
        {
            // Sync current health back to core before destroying
            var baseResource = resource.GetComponent<BaseResource>();
            if (baseResource != null)
            {
                var healthSystem = baseResource.GetHealthSystem();
                ResourceManager owningManager = ResourceManager.GetManagerForUniqueId(uniqueId);

                if (healthSystem != null && owningManager != null &&
                    owningManager.core.recordsById.TryGetValue(uniqueId, out var record))
                {
                    record.curHealth = healthSystem.GetHealth();
                }
            }

            Destroy(resource);
        }

        allActiveResources.Remove(uniqueId);

        // Remove from type-specific dictionary
        foreach (var typeDict in activeResourcesByType.Values)
        {
            if (typeDict.ContainsKey(uniqueId))
            {
                typeDict.Remove(uniqueId);
                break;
            }
        }

        if (verboseLogs)
            Debug.Log($"[TreeLODSystem] Deactivated resource {uniqueId}");
    }

    /// <summary>
    /// Render all non-active, non-chopped resources as GPU instances
    /// </summary>
    void RenderInstancedResources()
    {
        Vector3 playerPos = player != null ? player.position : Vector3.zero;

        // Clear all instance lists
        foreach (var list in instanceMatricesByType.Values)
            list.Clear();

        // Collect matrices for each resource type from all managers
        foreach (var manager in resourceManagers)
        {
            if (manager == null) continue;

            foreach (var kvp in manager.core.recordsById)
            {
                string id = kvp.Key;
                SpawnRecord record = kvp.Value;

                // Skip if chopped or currently active
                if (record.isChopped || allActiveResources.ContainsKey(id))
                    continue;

                // Only instance if outside interactive distance
                float distSqr = (record.position - playerPos).sqrMagnitude;
                if (distSqr >= interactiveDistance * interactiveDistance)
                {
                    string resourceType = DetermineResourceType(record);

                    if (!instanceMatricesByType.ContainsKey(resourceType))
                        instanceMatricesByType[resourceType] = new List<Matrix4x4>();

                    Matrix4x4 matrix = Matrix4x4.TRS(
                        record.position,
                        record.rotation,
                        record.scale
                    );

                    instanceMatricesByType[resourceType].Add(matrix);
                }
            }
        }

        // Render each resource type
        foreach (var config in resourceTypes)
        {
            if (config.instancedMesh == null)
                continue;

            if (!instanceMatricesByType.TryGetValue(config.typeName, out var matrices))
                continue;

            if (matrices.Count == 0)
                continue;

            // Check if this resource has multiple materials (like tree with leaves + trunk)
            if (config.HasMultipleMaterials)
            {
                RenderMultiMaterialInstances(config, matrices);
            }
            else
            {
                // Simple single-material rendering
                if (config.instancedMaterial == null)
                    continue;

                RenderSingleMaterialInstances(config.instancedMesh, config.instancedMaterial, matrices);
            }
        }
    }

    /// <summary>
    /// Render instances with a single material (bushes, rocks, simple objects)
    /// </summary>
    void RenderSingleMaterialInstances(Mesh mesh, Material material, List<Matrix4x4> matrices)
    {
        int batchSize = 1023;
        for (int i = 0; i < matrices.Count; i += batchSize)
        {
            int count = Mathf.Min(batchSize, matrices.Count - i);
            Graphics.DrawMeshInstanced(
                mesh,
                0, // submesh index
                material,
                matrices.GetRange(i, count),
                null,
                UnityEngine.Rendering.ShadowCastingMode.On,
                true
            );
        }
    }

    /// <summary>
    /// Render instances with multiple materials (trees with leaves + trunk)
    /// Each submesh is rendered separately with its own material
    /// </summary>
    void RenderMultiMaterialInstances(ResourceTypeConfig config, List<Matrix4x4> matrices)
    {
        int batchSize = 1023;

        // Render each submesh with its material
        foreach (var subMesh in config.subMeshes)
        {
            if (subMesh.material == null)
            {
                if (verboseLogs)
                    Debug.LogWarning($"[TreeLODSystem] SubMesh {subMesh.subMeshIndex} for {config.typeName} has no material!");
                continue;
            }

            // Render in batches
            for (int i = 0; i < matrices.Count; i += batchSize)
            {
                int count = Mathf.Min(batchSize, matrices.Count - i);

                try
                {
                    Graphics.DrawMeshInstanced(
                        config.instancedMesh,
                        subMesh.subMeshIndex,
                        subMesh.material,
                        matrices.GetRange(i, count),
                        null,
                        UnityEngine.Rendering.ShadowCastingMode.On,
                        true
                    );
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[TreeLODSystem] Failed to render submesh {subMesh.subMeshIndex} for {config.typeName}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Handle resource state changes (chopping, damage, etc)
    /// </summary>
    void OnResourceStateChanged(ResourceStateChangeEvent evt)
    {
        if (evt == null) return;

        // If resource was chopped, deactivate it immediately
        if (evt.newState && allActiveResources.ContainsKey(evt.uniqueId))
        {
            DeactivateResource(evt.uniqueId);
        }

        // Update spatial grid if needed
        if (evt.newState != evt.previousState)
        {
            ResourceManager manager = ResourceManager.GetManagerForUniqueId(evt.uniqueId);
            if (manager != null && manager.core.recordsById.TryGetValue(evt.uniqueId, out var record))
            {
                Vector2Int cell = GetGridCell(record.position);
                if (spatialGrid.TryGetValue(cell, out var cellResources))
                {
                    if (evt.newState) // chopped
                    {
                        cellResources.RemoveAll(e => e.uniqueId == evt.uniqueId);
                    }
                    else
                    {
                        // Re-add if not already present
                        if (!cellResources.Any(e => e.uniqueId == evt.uniqueId))
                        {
                            cellResources.Add(new ResourceEntry
                            {
                                uniqueId = evt.uniqueId,
                                position = record.position,
                                manager = manager,
                                resourceType = DetermineResourceType(record)
                            });
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Force rebuild spatial grid (call after loading new areas)
    /// </summary>
    public void RebuildSpatialGrid()
    {
        // Refresh manager list
        resourceManagers = ResourceManager.GetAllManagers().ToList();
        BuildSpatialGrid();
    }

    /// <summary>
    /// Get stats for debugging
    /// </summary>
    public void LogStats()
    {
        int totalInstanced = instanceMatricesByType.Values.Sum(list => list.Count);
        Debug.Log($"[TreeLODSystem] Stats:\n" +
                  $"- Active Resources: {allActiveResources.Count}\n" +
                  $"- Instanced Resources: {totalInstanced}\n" +
                  $"- Spatial Grid Cells: {spatialGrid.Count}\n" +
                  $"- Activation Queue: {activationQueue.Count}\n" +
                  $"- Deactivation Queue: {deactivationQueue.Count}");

        foreach (var config in resourceTypes)
        {
            int active = activeResourcesByType.ContainsKey(config.typeName) ?
                activeResourcesByType[config.typeName].Count : 0;
            int instanced = instanceMatricesByType.ContainsKey(config.typeName) ?
                instanceMatricesByType[config.typeName].Count : 0;
            Debug.Log($"  {config.typeName}: {active} active, {instanced} instanced");
        }
    }

    void OnDrawGizmos()
    {
        if (!showDebugGizmos || player == null) return;

        // Draw interaction radius
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(player.position, interactiveDistance);

        // Draw deactivation radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(player.position, deactivateDistance);

        // Draw active resources
        Gizmos.color = Color.blue;
        foreach (var kvp in allActiveResources)
        {
            if (kvp.Value != null)
                Gizmos.DrawWireCube(kvp.Value.transform.position, Vector3.one * 2f);
        }
    }
}