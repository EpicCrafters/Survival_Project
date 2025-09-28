using System;
using UnityEngine;

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

    // cached components
    private ResourceInstanceNet _resourceNet;
    private ResourceInstanceVisual _resourceVisual;

    // Awake: initialize health and hook death callback
    protected virtual void Awake()
    {
        // Avoid double-initialization if Awake is called again (editor scripts, etc.)
        if (healthSystem != null) return;

        InitializeHealth();

        if (healthSystem != null)
        {
            // subscribe to death event (ensure your HealthSystem exposes OnDead)
            healthSystem.OnDead += OnResourceDestroyed_Internal;
        }
        else
        {
            Debug.LogWarning($"{name}: HealthSystem null after InitializeHealth().");
        }

        ValidateComponents();

        // cache optional network/visual facades (may be null)
        _resourceNet = GetComponent<ResourceInstanceNet>();
        _resourceVisual = GetComponent<ResourceInstanceVisual>();
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

    /// <summary>
    /// Ask the authoritative manager to mark this resource destroyed and optionally replace it with a replacement prefab.
    /// Behavior:
    /// - If a ResourceManager exists:
    ///      - If we're a client, route via ResourceInstanceNet (ClientRequestChop) if present (server handles authority).
    ///      - Else (host/standalone): register a runtime destroyed replacement prefab with the manager (if provided),
    ///        then request state change via ResourceManager.RequestResourceStateChange(uniqueId, true).
    /// - Else fallback: local-only replacement + destroy (with logs).
    /// </summary>
    /// <param name="replacementPrefab">Optional non-networked visual prefab to use as the stump/replacement.</param>
    public void RequestDestroyAndReplace(GameObject replacementPrefab = null)
    {
        // Defensive early return with helpful log
        if (isBeingDestroyed || isDestroyed)
        {
            Debug.LogWarning($"{name}: RequestDestroyAndReplace skipped because isBeingDestroyed={isBeingDestroyed}, isDestroyed={isDestroyed}");
            return;
        }

        // Mark that we're in the destruction flow (prevents re-entry).
        isBeingDestroyed = true;

        // Ensure we have an id for persistent-managed resources; if missing, generate a temp id (helpful for runtime spawn cases)
        if (string.IsNullOrEmpty(uniqueId))
        {
            GenerateUniqueIdIfMissing();
            Debug.LogWarning($"{name}: UniqueId was empty when requesting destroy - generated temporary id '{uniqueId}'. In persistent flows prefer manager-assigned ids.");
        }

        // Try to find a ResourceManager (authoritative). Use FindFirstObjectByType for safety in different setups.
        var rm = FindFirstObjectByType<ResourceManager>();
        if (rm != null)
        {
            // If client role, route via network facade if available (to reach server)
            if (rm.role == ResourceManagerRole.Client)
            {
                if (_resourceNet != null)
                {
                    _resourceNet.ClientRequestChop(/*optimistic=*/ false);
                    return;
                }
                else
                {
                    Debug.LogWarning($"{name}: Client-side destroy requested but ResourceInstanceNet not present; cannot send request to server. Falling back to local-only behavior.");
                    // fall through to local fallback
                }
            }
            else
            {
                // Host or Standalone: register replacement (runtime only) then request authoritative state change
                if (replacementPrefab != null)
                {
                    Debug.Log($"[BaseResource] {name} is requesting ResourceManager to destroy UniqueId={uniqueId} (role={rm.role}).");

                    try { rm.SetDestroyedReplacementPrefab(uniqueId, replacementPrefab); }
                    catch (Exception ex) { Debug.LogWarning($"[BaseResource] Failed to SetDestroyedReplacementPrefab on ResourceManager: {ex}"); }
                }

                // Request authoritative change (ResourceManager will apply core update, visuals and broadcast/persist)
                try { rm.RequestResourceStateChange(uniqueId, true); }
                catch (Exception ex) { Debug.LogError($"[BaseResource] RequestResourceStateChange failed: {ex}"); }

                // Manager will handle visuals/persistence; do not destroy locally here.
                return;
            }
        }

        // Last-resort fallback: local replacement + destroy (no persistence)
        if (replacementPrefab != null)
        {
            var r = Instantiate(replacementPrefab, transform.position, transform.rotation, transform.parent);
            // if replacement has a resource/visual, try set ids to keep the scene tied to the same unique id
            var br = r.GetComponent<BaseResource>();
            if (br != null) br.SetUniqueId(uniqueId);

            var rv = r.GetComponent<ResourceInstanceVisual>() ?? r.GetComponentInChildren<ResourceInstanceVisual>();
            if (rv != null)
            {
                rv.SetUniqueId(uniqueId);
                rv.isChopped = true;
                rv.ApplyState(/*treatAsReplacement=*/ true);
            }
        }

        Debug.LogWarning($"{name}: RequestDestroyAndReplace performed local-only replacement (no persistence). UniqueId={uniqueId}");
        DestroyResource();
    }

    /// <summary>
    /// Ask authoritative manager to persist a state change (e.g. isChopped=true) without replacing the object.
    /// Preference order:
    /// - ResourceManager (network aware) via RequestResourceStateChange
    /// - Log and no-op
    /// </summary>
    /*public void RequestPersistState(bool newIsChopped)
    {
        if (string.IsNullOrEmpty(uniqueId))
        {
            Debug.LogWarning($"{name}: RequestPersistState called but UniqueId is empty.");
            return;
        }

        var rm = FindFirstObjectByType<ResourceManager>();
        if (rm != null)
        {
            try { rm.RequestResourceStateChange(uniqueId, newIsChopped); }
            catch (Exception ex) { Debug.LogError($"[BaseResource] RequestPersistState failed via ResourceManager: {ex}"); }
            return;
        }

        Debug.LogWarning($"{name}: RequestPersistState called but no ResourceManager found. No persistence will occur.");
    }*/

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

    #region Editor/Validation helpers (optional)

#if UNITY_EDITOR
    [ContextMenu("Generate UniqueId")]
    private void Editor_GenerateUniqueId()
    {
        GenerateUniqueIdIfMissing();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[BaseResource] Generated UniqueId: {uniqueId} for {name}");
    }
#endif

    #endregion
}
