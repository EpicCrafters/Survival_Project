using Mirror;
using System;
using UnityEngine;

// =============================================
// BASE CLASS - Non-networked (for Trees)
// =============================================
public abstract class ChoppableBase : BaseResource, IMinenable
{
    [Header("Debug")]
    [SerializeField] protected bool debugMode = true;

    [Header("Destroy Effect Spawn Position")]
    [SerializeField] public Transform effectPosition;

    protected override void InitializeHealth()
    {
        healthSystem = new HealthSystem(GetHealthAmount());
        resourceType = ResourceType.Tree;
    }

    protected abstract int GetHealthAmount();

    protected override void OnResourceDestroyed()
    {
        if (isBeingDestroyed || isDestroyed)
        {
            Debug.LogWarning($"{gameObject.name}: Already being destroyed!");
            return;
        }

        // Update ResourceManager state
        ResourceManager rm = ResourceManager.GetManagerForGameObject(this.gameObject);
        if (!string.IsNullOrEmpty(UniqueId))
        {
            rm?.RequestResourceStateChange(UniqueId, true, 0);
        }

        if (!NetworkServer.active)
        {
            DebugLog("OnResourceDestroyed called on client, skipping server logic");
            return;
        }

        DebugLog($"Server: Starting destruction for {gameObject.name}");

        if (string.IsNullOrEmpty(UniqueId))
        {
            GenerateUniqueIdIfMissing();
        }

        // Server spawns networked results (log)
        SpawnChopResults();

        // ✅ Notify all clients to destroy their local copy
        if (WorldResourceManager.Instance != null)
        {
            WorldResourceManager.Instance.RpcDestroyTree(UniqueId);
        }

        // Server destroys its own copy
        isBeingDestroyed = true;

        // Spawn local stump for host
        SpawnLocalStump();

        DestroyResource();
    }

    protected abstract void SpawnChopResults();
    protected abstract GameObject GetReplacementPrefab();
    public void ClientSideDestroy()
    {
        if (isBeingDestroyed || isDestroyed)
        {
            DebugLog("Already being destroyed, skipping");
            return;
        }

        isBeingDestroyed = true;

        DebugLog("Client-side destruction triggered");

        // Spawn local stump (non-networked, visual only)
        SpawnLocalStump();

        // Destroy this tree GameObject
        DestroyResource();
    }

    /// <summary>
    /// Spawn stump locally without networking - override in derived classes
    /// </summary>
    protected virtual void SpawnLocalStump()
    {
        // Default: do nothing
        // MyTree will override this to spawn the stump prefab
    }

  
   protected void SpawnNetworkedObject(Transform prefab, Vector3 position, Quaternion rotation)
{
    if (prefab == null || !NetworkServer.active)
    {
        Debug.LogWarning($"[SpawnNetworkedObject] Skipped - Server:{NetworkServer.active}, Prefab:{prefab != null}");
        return;
    }

    var obj = Instantiate(prefab, position, rotation);
    var rb = obj.GetComponent<Rigidbody>();
    if (rb == null) rb = obj.gameObject.AddComponent<Rigidbody>();
    
    rb.AddForce(Vector3.up * UnityEngine.Random.Range(0.3f, 1f) +
               UnityEngine.Random.insideUnitSphere * UnityEngine.Random.Range(0.1f, 0.8f),
               ForceMode.Impulse);
    rb.AddTorque(UnityEngine.Random.insideUnitSphere * 0.5f, ForceMode.Impulse);
    
    NetworkServer.Spawn(obj.gameObject);
    Debug.Log($"[SERVER] ✅ Spawned networked object: {prefab.name}");
}

    protected override void ValidateComponents() { }
    public override ResourceType GetResourceType() => ResourceType.Tree;

    protected void DebugLog(string msg)
    {
        if (debugMode) Debug.Log($"[{GetType().Name}][{name}] {msg}");
    }
}
