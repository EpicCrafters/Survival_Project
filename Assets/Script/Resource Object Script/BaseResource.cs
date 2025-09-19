using UnityEngine;

/// <summary>
/// BaseResource: abstract base for all world-placed resources (trees, rocks, etc.)
/// - Adds persistent uniqueId support (set by loader at spawn time)
/// - Provides safe damage / destroy lifecycle and helpers to request persistence/replace via ResourceManagerOffline
///
/// IMPORTANT: ResourceManagerOffline should still be the sole writer of JSON. Resources call into it to request changes.
/// </summary>
public abstract class BaseResource : MonoBehaviour, IDamageable
{
[Header("Base Resource Settings")]
[SerializeField] protected int baseHealth = 30;
[SerializeField] protected ResourceType resourceType;

[Header("Drop Settings")]
    [SerializeField] protected int minDropCount = 1;
    [SerializeField] protected int maxDropCount = 3;
    [SerializeField] protected float dropRadius = 0.2f;
    [SerializeField] protected float dropHeight = 0.1f;

    // runtime state
    protected HealthSystem healthSystem;
    protected bool isDestroyed = false;       // object already fully removed
    protected bool isBeingDestroyed = false;  // destruction process in progress

    // persistent unique id (assigned by loader / ResourceManager at spawn time)
    [SerializeField] private string uniqueId;
    public string UniqueId => uniqueId;
    public void SetUniqueId(string id) { uniqueId = id; }

    // Awake: initialize health and hook death callback
    protected virtual void Awake()
    {
        // Avoid double-initialization if Awake is called again (editor scripts, etc.)
        if (healthSystem != null) return;

        InitializeHealth();

        if (healthSystem != null)
        {
            // subscribe to death event (ensure your HealthSystem exposes OnDead)
            healthSystem.OnDead += OnResourceDestroyed;
        }
        else
        {
            Debug.LogWarning($"{name}: HealthSystem null after InitializeHealth().");
        }

        ValidateComponents();
    }

    // Ensure we clean up subscription to avoid stray references
    protected virtual void OnDestroy()
    {
        if (healthSystem != null)
            healthSystem.OnDead -= OnResourceDestroyed;
    }

    // --- Abstracts to implement in derived classes ---
    protected abstract void InitializeHealth();      // set up healthSystem
    protected abstract void OnResourceDestroyed();   // resource-specific destruction behavior
    protected abstract void ValidateComponents();    // check assigned prefabs, colliders, etc.
    public abstract ResourceType GetResourceType();

    // --- Damage API (from IDamageable) ---
    public virtual void Damage(int amount)
    {
        if (isDestroyed || isBeingDestroyed || healthSystem == null) return;
        healthSystem.Damage(amount);
        OnDamageReceived(amount);
    }

    /// <summary>
    /// Override to respond to damage events (e.g. play hit fx)
    /// </summary>
    protected virtual void OnDamageReceived(int amount)
    {
        if (healthSystem != null)
            Debug.Log($"{gameObject.name} took {amount} damage. Health: {healthSystem.GetHealth()}");
    }

    public virtual HealthSystem GetHealthSystem() => healthSystem;

    // --- Common helper for spawning item drops (non-persistent)
    protected void SpawnDrops(Transform prefab, int count, Vector3 basePosition)
    {
        if (prefab == null) return;

        for (int i = 0; i < count; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-dropRadius, dropRadius),
                dropHeight,
                Random.Range(-dropRadius, dropRadius)
            );

            Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            Instantiate(prefab, basePosition + offset, randomRotation);
        }
    }

    // --- Simple removal helper (local-only)
    // Use RequestDestroyAndReplace to update the authoritative manager + persist.
    protected virtual void DestroyResource()
    {
        if (isDestroyed) return;
        isDestroyed = true;
        Destroy(gameObject);
    }

    // --- Helpers to interact with ResourceManagerOffline (centralized persistence)
    /// <summary>
    /// Ask ResourceManagerOffline to mark this resource destroyed and optionally replace it with a replacement prefab.
    /// This method is safe if manager is missing: it will perform a local fallback destroy.
    /// </summary>
    /// <param name="replacementPrefab">replacement GameObject prefab (can be null)</param>
    public void RequestDestroyAndReplace(GameObject replacementPrefab = null)
    {
        if (isBeingDestroyed || isDestroyed) return;
        isBeingDestroyed = true;

        if (!string.IsNullOrEmpty(UniqueId) && ResourceManagerOffline.Instance != null)
        {
            // Centralized manager will update JSON, save, destroy and spawn replacement properly.
            ResourceManagerOffline.Instance.MarkResourceDestroyedAndReplace(UniqueId, this.gameObject, replacementPrefab);
        }
        else
        {
            // Fallback: update local scene only (no persistence)
            if (!string.IsNullOrEmpty(UniqueId) && ResourceManagerOffline.Instance == null)
                Debug.LogWarning($"{name}: ResourceManagerOffline instance not found - performing local destroy only for UniqueId={UniqueId}.");
            if (replacementPrefab != null)
            {
                var r = Instantiate(replacementPrefab, transform.position, transform.rotation, transform.parent);
                // try attach ids if possible
                var inst = r.GetComponent<ResourceInstanceOffline>() ?? r.AddComponent<ResourceInstanceOffline>();
                inst.uniqueId = UniqueId;
                inst.isChopped = true;
                inst.ApplyState();

                var br = r.GetComponent<BaseResource>();
                if (br != null) br.SetUniqueId(UniqueId);
            }

            DestroyResource();
        }
    }

    /// <summary>
    /// Ask ResourceManagerOffline to persist a state change (e.g. isChopped=true) without replacing the object.
    /// Manager handles updating in-memory record and saving JSON.
    /// </summary>
    public void RequestPersistState(bool isChopped)
    {
        if (string.IsNullOrEmpty(UniqueId))
        {
            Debug.LogWarning($"{name}: RequestPersistState called but UniqueId is empty.");
            return;
        }

        if (ResourceManagerOffline.Instance == null)
        {
            Debug.LogWarning($"{name}: RequestPersistState called but ResourceManagerOffline.Instance is null. No persistence will occur.");
            return;
        }

        ResourceManagerOffline.Instance.OnResourceStateChanged(UniqueId, isChopped);
    }

    // Expose status for other systems / debugging
    public bool IsDestroyed => isDestroyed;
    public bool IsBeingDestroyed => isBeingDestroyed;
}