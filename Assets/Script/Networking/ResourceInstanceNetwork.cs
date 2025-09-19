using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

[DisallowMultipleComponent]
public class ResourceInstanceNetwork : NetworkBehaviour
{
    [Header("Network / Persistence (server authoritative)")]
    [SyncVar(hook = nameof(OnUniqueIdChanged))]
    public string uniqueId;

    // Server authoritative chopped flag. Server sets this; clients receive it via SyncVar.
    [SyncVar(hook = nameof(OnIsChoppedChanged))]
    public bool isChopped = false;

    [Header("Replacement (optional)")]
    [Tooltip("Prefab to instantiate on server when this resource is chopped (e.g. stump). Must be a spawnable prefab (NetworkIdentity).")]
    public GameObject destroyedReplacement;

    [Tooltip("If true, this prefab should be treated as a replacement (stump) by default.")]
    public bool treatAsReplacementPrefab = false;

    [Header("Debug")]
    public bool verboseLogs = false;

    // internal cached components (exclude replaced-child if any)
    private const string kReplacementChildName = "__destroyed_replacement";

    private List<Renderer> _cachedRenderers = new List<Renderer>();
    private bool[] _rendererInitialStates;
    private List<Collider> _cachedColliders = new List<Collider>();
    private bool[] _colliderInitialStates;
    private bool _cached = false;

    // scheduled coroutine handle
    private Coroutine _disableOriginalCollidersCoroutine;

    #region Unity lifecycle

    public override void OnStartServer()
    {
        base.OnStartServer();
        // nothing special yet
        if (verboseLogs) Debug.Log($"[ResourceInstanceNetwork][Server] OnStartServer {name} uniqueId={uniqueId} isChopped={isChopped}");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        CacheOriginalChildrenIfNeeded();

        // Apply initial visuals based on SyncVar value on clients
        bool treatAsReplacement = DetermineTreatAsReplacement();
        ApplyStateLocal(treatAsReplacement);

        if (verboseLogs) Debug.Log($"[ResourceInstanceNetwork][Client] OnStartClient {name} uniqueId={uniqueId} isChopped={isChopped} treatAsReplacement={treatAsReplacement}");
    }

    #endregion

    #region SyncVar hooks

    private void OnUniqueIdChanged(string oldId, string newId)
    {
        if (verboseLogs) Debug.Log($"[ResourceInstanceNetwork] UniqueId changed: {oldId} -> {newId} on {name}");
    }

    private void OnIsChoppedChanged(bool oldVal, bool newVal)
    {
        if (verboseLogs) Debug.Log($"[ResourceInstanceNetwork] isChopped SyncVar changed on {name}: {oldVal} -> {newVal}");
        bool treatAsReplacement = DetermineTreatAsReplacement();
        ApplyStateLocal(treatAsReplacement);

        // Server-side extra actions when resource becomes chopped
        if (isServer && newVal == true)
        {
            // The server should handle persistence and replacement spawning.
            // We encapsulate that in a server method.
            // NOTE: if you prefer centralized manager-based replacement, call ResourceManagerOffline.MarkResourceDestroyedAndReplace(...) here instead.
            Server_HandleChopped();
        }
    }

    #endregion

    #region Public / network API

    /// <summary>
    /// Client -> Server request: ask the server to mark this resource chopped.
    /// Server must validate (distance, permissions) before applying.
    /// requiresAuthority = false allows non-owner clients to request; do validation on server.
    /// </summary>
    [Command(requiresAuthority = false)]
    public void CmdRequestMarkChopped()
    {
        if (!isServer)
            return;

        // TODO: add server-side validation here (distance checks, tool checks, anti-cheat).
        if (verboseLogs) Debug.Log($"[ResourceInstanceNetwork][Server] CmdRequestMarkChopped received for {name} uniqueId={uniqueId} from connection {connectionToClient?.connectionId}");
        SetChoppedServer(true);
    }

    /// <summary>
    /// Server-only: set chopped state and persist + spawn replacement as appropriate.
    /// </summary>
    [Server]
    public void SetChoppedServer(bool chopped)
    {
        if (isChopped == chopped) return;

        isChopped = chopped; // SyncVar -> propagate to clients (OnIsChoppedChanged will run there)
        if (verboseLogs) Debug.Log($"[ResourceInstanceNetwork][Server] SetChoppedServer({chopped}) for {name} uniqueId={uniqueId}");

        // Persist server-side (ResourceManagerOffline should be server-only in your Mirror build)
        if (ResourceManagerOffline.Instance != null)
        {
            try
            {
                ResourceManagerOffline.Instance.OnResourceStateChanged(uniqueId, isChopped);
                if (verboseLogs) Debug.Log($"[ResourceInstanceNetwork][Server] Persisted state for {uniqueId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ResourceInstanceNetwork][Server] Error persisting state: {ex}");
            }
        }
    }

    #endregion

    #region Server chopped handling (spawn replacement, destroy original)

    // Called on server when this instance becomes chopped
    [Server]
    private void Server_HandleChopped()
    {
        // Determine if we should treat this instance purely as a replacement (i.e. keep visible),
        // or if we should create a replacement prefab and remove this instance.
        bool treatAsReplacement = DetermineTreatAsReplacement();

        if (treatAsReplacement)
        {
            // If this object is itself a replacement (stump loaded from DB/JSON), don't spawn another replacement.
            // But we should ensure colliders are enabled safely (we rely on ApplyStateLocal which schedules enabling)
            if (verboseLogs) Debug.Log($"[ResourceInstanceNetwork][Server] {name} is treated as replacement; no server spawn.");
            return;
        }

        // Not a replacement: spawn replacement prefab (server) and destroy this object (server authoritative)
        if (destroyedReplacement != null)
        {
            try
            {
                // Instantiate replacement server-side
                var repl = Instantiate(destroyedReplacement, transform.position, transform.rotation);

                // preserve localScale of original if possible
                repl.transform.localScale = transform.localScale;

                // If replacement has ResourceInstanceNetwork, configure it BEFORE spawning so SyncVars are ready.
                var replRi = repl.GetComponent<ResourceInstanceNetwork>();
                if (replRi != null)
                {
                    replRi.uniqueId = uniqueId;
                    // mark replacement as chopped (stump)
                    replRi.isChopped = true;
                    replRi.treatAsReplacementPrefab = true;

                    // Disable its colliders on the server immediately to avoid overlap impulses
                    replRi.CacheOriginalChildrenIfNeeded();
                    replRi.DisableAllCachedCollidersImmediate();
                }

                // Spawn via Mirror so all clients receive it
                NetworkServer.Spawn(repl);

                // Ask the replacement instance (on clients) to prepare colliders (disable now and enable after next FixedUpdate).
                if (replRi != null)
                {
                    // Rpc executed on replacement across clients (and runs on server instance as well).
                    replRi.RpcPrepareReplacementOnClients();
                }

                if (verboseLogs) Debug.Log($"[ResourceInstanceNetwork][Server] Spawned replacement '{repl.name}' for {uniqueId}");
                // Destroy this original networked object across server/clients
                NetworkServer.Destroy(this.gameObject);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ResourceInstanceNetwork][Server] Exception while spawning replacement: {ex}");
            }
        }
        else
        {
            // No replacement prefab assigned. We still persist the chopped state and can leave this object hidden or destroyed.
            // Here we destroy the object server-side to match offline behavior where original is removed.
            if (verboseLogs) Debug.Log($"[ResourceInstanceNetwork][Server] No destroyedReplacement set for {uniqueId} — destroying original.");
            NetworkServer.Destroy(this.gameObject);
        }
    }

    /// <summary>
    /// Called on replacement instance (server) to tell all clients to prepare replacement colliders:
    /// - ensure visuals enabled
    /// - disable colliders immediately on client
    /// - schedule enabling colliders after next FixedUpdate on client
    /// This Rpc targets the replacement instance itself (so RtP id is the replacement object's netId).
    /// </summary>
    [ClientRpc]
    private void RpcPrepareReplacementOnClients()
    {
        // This runs on clients and server (on server, colliders were already disabled above)
        CacheOriginalChildrenIfNeeded();
        EnsureOriginalRenderersEnabled();
        DisableAllCachedCollidersImmediate();
        StartCoroutine(EnableCollidersAfterFixedUpdate(gameObject));
        if (verboseLogs) Debug.Log($"[ResourceInstanceNetwork][ClientRpc] RpcPrepareReplacementOnClients executed on {name}");
    }

    #endregion

    #region Local visuals & colliders (client + server local application)

    /// <summary>
    /// Local-only application of visuals/collider toggles.
    /// Called from SyncVar hook (clients) and from OnStartClient.
    /// This intentionally does NOT try to spawn networked replacements locally.
    /// </summary>
    private void ApplyStateLocal(bool treatAsReplacement)
    {
        try
        {
            CacheOriginalChildrenIfNeeded();

            // If this instance is a replacement prefab flagged in the editor, honor that.
            if (!treatAsReplacement && treatAsReplacementPrefab)
                treatAsReplacement = true;

            // Auto-detect stump via MyTree if present
            if (!treatAsReplacement)
            {
                var mt = GetComponent<MyTree>();
                if (mt != null)
                {
                    try
                    {
                        if (mt.GetTreeType() == MyTree.TreeType.Stump)
                            treatAsReplacement = true;
                    }
                    catch { /* tolerate cross-assembly */ }
                }
            }

            if (isChopped)
            {
                if (treatAsReplacement)
                {
                    // Replacement (stump): visuals on immediately, colliders delayed next FixedUpdate
                    CancelScheduledColliderDisableIfAny();
                    EnsureOriginalRenderersEnabled();
                    DisableAllCachedCollidersImmediate();
                    StartCoroutine(EnableCollidersAfterFixedUpdate(gameObject));
                    RemoveReplacementChildIfExists(); // defensive
                    if (verboseLogs) Debug.Log($"[ResourceInstanceNetwork] ApplyStateLocal: {name} treated as REPLACEMENT -> visuals on, colliders will enable next FixedUpdate.");
                }
                else
                {
                    // This instance is the original that became chopped: hide visuals,
                    // schedule disabling colliders, and expect server to spawn replacement (networked).
                    DisableOriginalRenderersImmediate();
                    ScheduleDisableOriginalCollidersNextFixedUpdate();
                    // Do NOT create replacement child locally in networked mode (server spawns persistent replacement).
                    if (verboseLogs) Debug.Log($"[ResourceInstanceNetwork] ApplyStateLocal: {name} treated as CHOPPED -> visuals off (server should spawn replacement).");
                }
            }
            else
            {
                // Not chopped -> restore original cached states
                CancelScheduledColliderDisableIfAny();
                RestoreOriginalVisuals();
                RemoveReplacementChildIfExists();
                if (verboseLogs) Debug.Log($"[ResourceInstanceNetwork] ApplyStateLocal: {name} restored to UNCHOPPED.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ResourceInstanceNetwork] ApplyStateLocal exception on {name}: {ex}");
        }
    }

    #endregion

    #region Caching helpers (renderers/colliders)

    private void CacheOriginalChildrenIfNeeded()
    {
        if (_cached) return;

        _cachedRenderers.Clear();
        _cachedColliders.Clear();

        var allRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in allRenderers)
        {
            if (IsUnderReplacementChild(r.transform)) continue;
            _cachedRenderers.Add(r);
        }

        var allColliders = GetComponentsInChildren<Collider>(true);
        foreach (var c in allColliders)
        {
            if (IsUnderReplacementChild(c.transform)) continue;
            _cachedColliders.Add(c);
        }

        _rendererInitialStates = new bool[_cachedRenderers.Count];
        for (int i = 0; i < _cachedRenderers.Count; i++)
            _rendererInitialStates[i] = _cachedRenderers[i] != null ? _cachedRenderers[i].enabled : false;

        _colliderInitialStates = new bool[_cachedColliders.Count];
        for (int i = 0; i < _cachedColliders.Count; i++)
            _colliderInitialStates[i] = _cachedColliders[i] != null ? _cachedColliders[i].enabled : false;

        _cached = true;
    }

    private bool IsUnderReplacementChild(Transform t)
    {
        if (t == null) return false;
        var cur = t;
        while (cur != null && cur != transform)
        {
            if (string.Equals(cur.name, kReplacementChildName, StringComparison.Ordinal))
                return true;
            cur = cur.parent;
        }
        return false;
    }

    private void DisableOriginalRenderersImmediate()
    {
        for (int i = 0; i < _cachedRenderers.Count; i++)
        {
            var r = _cachedRenderers[i];
            if (r != null) r.enabled = false;
        }
    }

    private void EnsureOriginalRenderersEnabled()
    {
        for (int i = 0; i < _cachedRenderers.Count; i++)
        {
            var r = _cachedRenderers[i];
            if (r != null) r.enabled = true;
        }
    }

    private void RestoreOriginalVisuals()
    {
        for (int i = 0; i < _cachedRenderers.Count; i++)
        {
            var r = _cachedRenderers[i];
            if (r != null) r.enabled = _rendererInitialStates[i];
        }
        for (int i = 0; i < _cachedColliders.Count; i++)
        {
            var c = _cachedColliders[i];
            if (c != null) c.enabled = _colliderInitialStates[i];
        }
    }

    private void DisableAllCachedCollidersImmediate()
    {
        for (int i = 0; i < _cachedColliders.Count; i++)
        {
            var c = _cachedColliders[i];
            if (c != null) c.enabled = false;
        }
    }

    #endregion

    #region Scheduled collider toggles (coroutines)

    private void ScheduleDisableOriginalCollidersNextFixedUpdate()
    {
        if (!Application.isPlaying)
        {
            // immediate in editor for preview
            for (int i = 0; i < _cachedColliders.Count; i++)
            {
                var c = _cachedColliders[i];
                if (c != null) c.enabled = false;
            }
            return;
        }

        if (_disableOriginalCollidersCoroutine != null) return;
        _disableOriginalCollidersCoroutine = StartCoroutine(DisableOriginalCollidersAfterFixedUpdate());
    }

    private IEnumerator DisableOriginalCollidersAfterFixedUpdate()
    {
        yield return new WaitForFixedUpdate();

        for (int i = 0; i < _cachedColliders.Count; i++)
        {
            var c = _cachedColliders[i];
            if (c != null) c.enabled = false;
        }

        _disableOriginalCollidersCoroutine = null;
    }

    private void CancelScheduledColliderDisableIfAny()
    {
        if (_disableOriginalCollidersCoroutine != null)
        {
            StopCoroutine(_disableOriginalCollidersCoroutine);
            _disableOriginalCollidersCoroutine = null;
        }
    }

    /// <summary>
    /// Enable colliders on `root` after next FixedUpdate.
    /// Public so we can start this coroutine on replacement instance via Rpc or locally.
    /// </summary>
    public IEnumerator EnableCollidersAfterFixedUpdate(GameObject root)
    {
        if (!Application.isPlaying)
        {
            if (root != null)
            {
                foreach (var c in root.GetComponentsInChildren<Collider>(true))
                    if (c != null) c.enabled = true;
            }
            yield break;
        }

        yield return new WaitForFixedUpdate();

        if (root == null) yield break;

        Physics.SyncTransforms();

        var cols = root.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols)
        {
            if (c != null) c.enabled = true;
        }
    }

    #endregion

    #region Replacement child management (defensive - seldom used in networked flows)

    // In networked flow we expect server to spawn replacement networkedly; these helpers are kept defensive
    private void RemoveReplacementChildIfExists()
    {
        var existing = transform.Find(kReplacementChildName);
        if (existing == null) return;

        if (Application.isPlaying)
            Destroy(existing.gameObject);
        else
            DestroyImmediate(existing.gameObject);
    }

    #endregion

    #region Utilities

    private bool DetermineTreatAsReplacement()
    {
        if (treatAsReplacementPrefab) return true;
        var mt = GetComponent<MyTree>();
        if (mt != null)
        {
            try { return mt.GetTreeType() == MyTree.TreeType.Stump; }
            catch { return false; }
        }
        return false;
    }

    #endregion

    #region Editor/debug

#if UNITY_EDITOR
    [ContextMenu("Debug: Toggle Chopped (Editor)")]
    private void EditorToggleChopped()
    {
        isChopped = !isChopped;
        ApplyStateLocal(DetermineTreatAsReplacement());
    }
#endif

    [ContextMenu("Debug: RequestMarkChopped (Cmd)")]
    private void Debug_RequestMarkChopped()
    {
        if (isServer)
            SetChoppedServer(true);
        else if (isClient)
            CmdRequestMarkChopped();
    }

    #endregion
}
