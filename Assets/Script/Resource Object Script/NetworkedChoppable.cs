using Mirror;
using System;
using UnityEngine;

// =============================================
// NETWORKED BASE - For Log, HalfLog, Stump
// =============================================
public abstract class NetworkedChoppable : NetworkBehaviour, IDamageable, IMinenable
{
    [Header("Base Settings")]
    [SerializeField] protected bool debugMode = true;

    protected HealthSystem healthSystem;
    protected bool isDestroyed = false;
    protected bool isBeingDestroyed = false;

    [SerializeField] private string uniqueId;
    public string UniqueId => uniqueId;
    public void SetUniqueId(string id) { uniqueId = id; }

    protected virtual void Awake()
    {
        if (healthSystem != null) return;

        healthSystem = new HealthSystem(GetHealthAmount());
        if (healthSystem != null)
        {
            healthSystem.OnDead += OnResourceDestroyed_Internal;
        }
    }

    protected virtual void OnDestroy()
    {
        try
        {
            if (healthSystem != null)
                healthSystem.OnDead -= OnResourceDestroyed_Internal;
        }
        catch { }
    }

    protected abstract int GetHealthAmount();

    // IDamageable implementation
    public virtual void Damage(int amount)
    {
        if (isDestroyed || isBeingDestroyed || healthSystem == null) return;

        // Only server processes damage
        if (!isServer) return;

        healthSystem.Damage(amount);
        DebugLog($"Took {amount} damage. Health: {healthSystem.GetHealth()}");
    }

    public virtual void Damage(int amount, HitInfo hit)
    {
        Damage(amount);
    }

    public bool CanTriggerHitStop() => false;
    public bool IsDead() => isDestroyed || (healthSystem != null && healthSystem.GetHealth() <= 0);
    public virtual HealthSystem GetHealthSystem() => healthSystem;

    private void OnResourceDestroyed_Internal()
    {
        if (isBeingDestroyed || isDestroyed) return;

        // Only server handles destruction
        if (!isServer)
        {
            DebugLog("OnResourceDestroyed called on client, skipping");
            return;
        }

        isBeingDestroyed = true;
        DebugLog("Starting destruction");

        try
        {
            OnResourceDestroyed();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception in OnResourceDestroyed: {ex}");
        }

        // Destroy on server (Mirror will sync to clients)
        NetworkServer.Destroy(gameObject);
    }

    protected abstract void OnResourceDestroyed();

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

    // IMinenable implementation
    public ResourceType GetResourceType() => ResourceType.Tree;

    protected void DebugLog(string msg)
    {
        if (debugMode) Debug.Log($"[{GetType().Name}][{name}] {msg}");
    }
}