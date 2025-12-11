using System;
using UnityEngine;
using Mirror;


#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// BaseResource: abstract base for all world-placed resources (trees, rocks, etc.)
/// - Adds persistent uniqueId support (set by loader at spawn time)
/// - Provides safe damage / destroy lifecycle and helpers to request persistence/replace via ResourceManager
///
/// IMPORTANT: Managers are the sole writers of persistent JSON. Resources call into managers to request changes.
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
    [SerializeField] protected bool isDestroyed = false;       // object already fully removed
    [SerializeField] protected bool isBeingDestroyed = false;  // destruction process in progress

    // persistent unique id (assigned by loader / ResourceManager at spawn time)
    [SerializeField] private string uniqueId;
    public string UniqueId => uniqueId;
    public void SetUniqueId(string id) { uniqueId = id; }

    [Tooltip("Maximum allowed chop distance from player's root (server-side validation).")]
    public float maxInteractDistance = 3.0f;

    // Awake: initialize health and hook death callback
    protected virtual void Awake()
    {
        // Avoid double-initialization if Awake is called again (editor scripts, etc.)
        if (healthSystem != null) return;

        InitializeHealth();

        if (healthSystem != null)
        {
            healthSystem.OnDead += OnResourceDestroyed_Internal;

            // Try to load health from record, but don't override if health system has better data
            if (!string.IsNullOrEmpty(uniqueId))
            {
                ResourceManager rm = ResourceManager.GetManagerForGameObject(this.gameObject);
                if (rm != null && rm.core.TryGetRecord(uniqueId, out var record))
                {
                    // Only set health from record if it makes sense
                    if (record.curHealth > 0 && record.curHealth <= healthSystem.GetHealth())
                    {
                        healthSystem.SetHealth(record.curHealth);
                    }
                    else if (record.curHealth <= 0)
                    {
                        // Record says it's dead/destroyed - mark accordingly
                        healthSystem.SetHealth(0);
                    }
                }
            }
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
        try
        {
            if (healthSystem != null)
                healthSystem.OnDead -= OnResourceDestroyed_Internal;
        }
        catch { /* best-effort cleanup */ }
    }

    // --- Abstracts to implement in derived classes ---
    protected abstract void InitializeHealth();      // set up healthSystem
    protected abstract void OnResourceDestroyed();   // resource-specific destruction behavior (override should call RequestDestroyAndReplace or RequestPersistState)
    protected abstract void ValidateComponents();    // check assigned prefabs, colliders, etc.
    public abstract ResourceType GetResourceType();

    // --- Damage API (from IDamageable) ---
    public virtual void Damage(int amount)
    {
        if (isDestroyed || isBeingDestroyed || healthSystem == null) return;
        healthSystem.Damage(amount);
        OnDamageReceived(amount);

        UpdateSpawnRecordHealth();
        UpdateReplacementHealth();
    }

    public virtual void Damage(int amount, HitInfo hit)
    {
        if (isDestroyed || isBeingDestroyed || healthSystem == null) return;
        healthSystem.Damage(amount);
        OnDamageReceived(amount);

        UpdateSpawnRecordHealth();
        UpdateReplacementHealth();

        if (hit.point != Vector3.zero)
        {
            Debug.Log($"{gameObject.name} was hit at {hit.point}, took {amount} damage.");
        }
    }

    public bool CanTriggerHitStop() => false; // Resources never trigger hit stop

    private void UpdateSpawnRecordHealth()
    {
        if (string.IsNullOrEmpty(uniqueId))
        {
            return;
        }

        int currentHealth = healthSystem?.GetHealth() ?? 0;

        ResourceManager rm = ResourceManager.GetManagerForGameObject(this.gameObject);
        if (rm != null)
        {
            rm.RequestResourceStateChange(uniqueId, IsBeingDestroyed, currentHealth);

            // Mark this object as having changed health
            rm.MarkHealthChanged(uniqueId);
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: No ResourceManager found for health update!");
        }
    }

    public bool IsDead() => isDestroyed || (healthSystem != null && healthSystem.GetHealth() <= 0);
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
                UnityEngine.Random.Range(-dropRadius, dropRadius),
                dropHeight,
                UnityEngine.Random.Range(-dropRadius, dropRadius)
            );

            Quaternion randomRotation = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360f), 0);
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

    // --- Helpers to interact with managers (networked / offline) ---

    /// <summary>
    /// INTERNAL: callback wrapped around your abstract OnResourceDestroyed so that derived
    /// classes can implement their custom behavior and then call RequestDestroyAndReplace(...) or RequestPersistState(...).
    /// This ensures the manager-driven flow is consistent and that we don't double-run logic accidentally.
    /// </summary>
    private void OnResourceDestroyed_Internal()
    {
        // Let derived type perform its destruction behavior (drop items, play VFX)
        try { OnResourceDestroyed(); }
        catch (Exception ex) { Debug.LogError($"[BaseResource] Exception in OnResourceDestroyed override for {name}: {ex}"); }
    }

    public void RequestDestroyAndReplace(GameObject replacementPrefab = null)
    {
        if (isBeingDestroyed || isDestroyed) return;

        isBeingDestroyed = true;

        if (string.IsNullOrEmpty(uniqueId))
        {
            GenerateUniqueIdIfMissing();
            Debug.LogWarning($"{name}: UniqueId was empty - generated '{uniqueId}'.");
        }

        // Fallback: local-only behavior
        ApplyLocalFallback(replacementPrefab);
    }

    private void ApplyLocalFallback(GameObject replacementPrefab)
    {
        if (replacementPrefab != null)
        {
            // Prefer using ResourceInstanceVisual on this GameObject to create/manage the replacement
            var localVis = GetComponent<ResourceInstanceVisual>();
            if (localVis != null)
            {
                try
                {
                    localVis.SetDestroyedReplacementPrefab(replacementPrefab);
                    localVis.SetUniqueId(uniqueId);
                    localVis.isChopped = true;
                    localVis.ApplyState(forceTreatAsReplacement: true);
                    Debug.Log($"[BaseResource] Used ResourceInstanceVisual to apply replacement for {name} (UniqueId={uniqueId}).");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[BaseResource] Failed to apply replacement via ResourceInstanceVisual: {ex.Message}. Falling back to instantiate.");
                }
            }
            /*
            // Final fallback: instantiate as a child named "__destroyed_replacement" so ResourceInstanceVisual / ResourceManager can detect and adopt it.
            var replacementTransform = Instantiate(replacementPrefab).transform;

            // Match world position & rotation first
            replacementTransform.SetPositionAndRotation(transform.position, transform.rotation);

            // Parent under this transform so visuals/reconciler can detect it consistently
            replacementTransform.SetParent(transform, worldPositionStays: true);

            // Explicit marker name for detection
            replacementTransform.name = "__destroyed_replacement";

            // Copy the world-scale-equivalent of the original
            try
            {
                Vector3 worldScale = transform.lossyScale;
                Vector3 parentScale = transform.parent ? transform.parent.lossyScale : Vector3.one;
                replacementTransform.localScale = new Vector3(
                    worldScale.x / parentScale.x,
                    worldScale.y / parentScale.y,
                    worldScale.z / parentScale.z
                );

                Debug.Log($"[BaseResource] Preserved world scale on replacement: {replacementTransform.localScale}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BaseResource] Failed to copy localScale to replacement: {ex.Message}");
            }

            var replacementGo = replacementTransform.gameObject;

            // assign uniqueId so loader recognizes this as the same record
            try
            {
                var vis = replacementGo.GetComponent<ResourceInstanceVisual>() ?? replacementGo.AddComponent<ResourceInstanceVisual>();
                vis.SetUniqueId(uniqueId);
                vis.isChopped = true;
                vis.ApplyState(forceTreatAsReplacement: true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BaseResource] Exception while assigning ResourceInstanceVisual on replacement: {ex}");
            }

            // transfer BaseResource id if replacement prefab has one
            var br = replacementGo.GetComponent<BaseResource>();
            if (br != null)
            {
                try
                {
                    br.SetUniqueId(uniqueId);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BaseResource] Exception calling SetUniqueId on replacement's BaseResource: {ex}");
                }
            }*/
        }

        /*Debug.LogWarning($"{name}: RequestDestroyAndReplace performed local-only replacement (no persistence). UniqueId={uniqueId}");
        DestroyResource();*/
    }
    private void UpdateReplacementHealth()
    {
        if (string.IsNullOrEmpty(uniqueId) || !uniqueId.Contains("_stump")) return;

        // Extract original uniqueId from stump id
        string originalId = uniqueId.Replace("_stump", "");

        ResourceManager rm = ResourceManager.GetManagerForGameObject(this.gameObject);
        if (rm != null && healthSystem != null)
        {
            rm.UpdateReplacementHealth(originalId, healthSystem.GetHealth());
        }
    }

    // Expose status for other systems / debugging
    public bool IsDestroyed => isDestroyed;
    public bool IsBeingDestroyed => isBeingDestroyed;

    // Utility: generate a GUID-based id for temporary runtime objects that need one
    protected void GenerateUniqueIdIfMissing()
    {
        if (string.IsNullOrEmpty(uniqueId))
        {
            uniqueId = Guid.NewGuid().ToString("N");
        }
    }
}