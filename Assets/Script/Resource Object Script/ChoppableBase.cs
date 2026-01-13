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

        ResourceManager rm = ResourceManager.GetManagerForGameObject(this.gameObject);
        if (!string.IsNullOrEmpty(UniqueId))
        {
            rm?.RequestResourceStateChange(UniqueId, true, 0);
        }

        if (!NetworkServer.active)
        {
            DebugLog("OnResourceDestroyed called on client, skipping");
            return;
        }

        DebugLog($"Starting destruction for {gameObject.name}");

        if (string.IsNullOrEmpty(UniqueId))
        {
            GenerateUniqueIdIfMissing();
        }

        SpawnChopResults();

        GameObject replacementPrefab = GetReplacementPrefab();
        if (rm != null && replacementPrefab != null)
        {
            RequestDestroyAndReplace(replacementPrefab);
        }
        else
        {
            isBeingDestroyed = true;
            DestroyResource();
        }
    }

    protected abstract void SpawnChopResults();
    protected abstract GameObject GetReplacementPrefab();

    protected void SpawnNetworkedObject(Transform prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null || !NetworkServer.active) return;

        var obj = Instantiate(prefab, position, rotation);

        var rb = obj.GetComponent<Rigidbody>();
        if (rb == null) rb = obj.gameObject.AddComponent<Rigidbody>();

        rb.AddForce(Vector3.up * UnityEngine.Random.Range(0.3f, 1f) +
                   UnityEngine.Random.insideUnitSphere * UnityEngine.Random.Range(0.1f, 0.8f),
                   ForceMode.Impulse);
        rb.AddTorque(UnityEngine.Random.insideUnitSphere * 0.5f, ForceMode.Impulse);

        NetworkServer.Spawn(obj.gameObject);
        DebugLog($"Spawned networked object: {prefab.name}");
    }

    protected override void ValidateComponents() { }
    public override ResourceType GetResourceType() => ResourceType.Tree;

    protected void DebugLog(string msg)
    {
        if (debugMode) Debug.Log($"[{GetType().Name}][{name}] {msg}");
    }
}
