// ResourceInstanceNet.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

[RequireComponent(typeof(ResourceInstanceVisual))]
public class ResourceInstanceNet : NetworkBehaviour
{
    [Tooltip("Unique id for this resource (should be assigned by ResourceManager on spawn).")]
    [SyncVar(hook = nameof(OnUniqueIdChanged))]
    public string uniqueId;

    [Tooltip("Synchronized chopped state (replicated to clients).")]
    [SyncVar(hook = nameof(OnIsChoppedChanged))]
    public bool isChoppedSync;

    [Tooltip("Maximum allowed chop distance from player's root (server-side validation).")]
    public float maxChopDistance = 3.0f;

    // Reference to the visual component
    private ResourceInstanceVisual visual;

    // Cached manager reference for fallback / subscription
    private ResourceManager _rm;

    // track whether we've subscribed to manager events so we can unsubscribe cleanly
    private bool _subscribedToManager = false;

    void Awake()
    {
        visual = GetComponent<ResourceInstanceVisual>();
        if (visual == null) Debug.LogError("[ResourceInstanceNet] ResourceInstanceVisual missing on the same GameObject.");
    }

    void Start()
    {
        // Ensure visual has the id if it exists
        if (visual != null && !string.IsNullOrEmpty(uniqueId))
            visual.SetUniqueId(uniqueId);

        // Try to find ResourceManager and wire up registration + subscriptions
        TryFindAndWireResourceManager();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Ensure visual has the authoritative id (SyncVar hook may have already run before this)
        if (visual != null && !string.IsNullOrEmpty(uniqueId))
            visual.SetUniqueId(uniqueId);

        // Apply the current chop state to visual (client-side)
        if (visual != null)
            visual.SetChoppedLocal(isChoppedSync);

        // Try wiring manager again if not done in Start (covers ordering edge-cases)
        TryFindAndWireResourceManager();
    }

    void OnDestroy()
    {
        UnsubscribeFromManager();
    }

    void OnUniqueIdChanged(string oldId, string newId)
    {
        uniqueId = newId;
        if (visual != null)
        {
            visual.SetUniqueId(newId);
        }

        // If manager exists, register now (covers cases where SyncVar arrives before manager init)
        if (_rm == null) TryFindAndWireResourceManager();
        else
        {
            try
            {
                if (visual != null && _rm != null)
                    visual.RegisterWithManagerImmediate(_rm);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ResourceInstanceNet] RegisterWithManagerImmediate failed in OnUniqueIdChanged: {ex}");
            }
        }
    }

    void OnIsChoppedChanged(bool oldVal, bool newVal)
    {
        isChoppedSync = newVal;
        if (visual != null)
        {
            // treatAsReplacement=false here; visuals are authoritative via ResourceManager state
            visual.SetChoppedLocal(newVal);
        }
    }

    /// <summary>
    /// Called by client-side code (player input/UI) to request a chop.
    /// optimistic=true will apply visual feedback immediately on the client (local-only) while the request is in-flight.
    /// </summary>
    public void ClientRequestChop(bool optimistic = false)
    {
        // Immediate local feedback if requested (client-side only)
        if (optimistic && visual != null)
            visual.SetChoppedLocal(true);

        // Prefer Mirror Command path if this NetworkBehaviour is active as a client and object is spawned
        if (isClient && NetworkClient.active)
        {
            // CmdRequestChop will run on the server
            CmdRequestChop();
            return;
        }

        // Fallback: use ResourceManager's public API directly (useful when not using Mirror-spawn)
        if (_rm == null) _rm = UnityEngine.Object.FindObjectOfType<ResourceManager>();
        if (_rm != null)
        {
            // On clients, this will raise OnClientResourceChangeRequested (network adapter should forward to host).
            _rm.RequestResourceStateChange(uniqueId, true);
        }
        else
        {
            Debug.LogWarning("[ResourceInstanceNet] No ResourceManager found for fallback path when requesting chop.");
        }
    }

    /// <summary>
    /// Mirror Command: executed on the server when a client requests a chop.
    /// requiresAuthority=false so non-owner clients may call this (common for world objects).
    /// </summary>
    [Command(requiresAuthority = false)]
    void CmdRequestChop(NetworkConnectionToClient sender = null)
    {
        // Server-side handler: validate and then ask the authoritative manager to apply the change.
        if (_rm == null) _rm = UnityEngine.Object.FindObjectOfType<ResourceManager>();
        if (_rm == null)
        {
            Debug.LogWarning("[ResourceInstanceNet] CmdRequestChop: ResourceManager not found on server.");
            return;
        }

        // ensure record exists
        if (_rm.core == null || !_rm.core.recordsById.TryGetValue(uniqueId, out var rec))
        {
            Debug.LogWarning($"[ResourceInstanceNet] CmdRequestChop: server record unknown: {uniqueId}");
            return;
        }

        try
        {
            // the SpawnRecord has a public 'isChopped' field — read it directly
            if (rec != null && rec.isChopped)
            {
                // already chopped on server
                return;
            }
        }
        catch
        {
            // best-effort; continue if we can't read it
        }

        // check distance if possible (validate player is close enough)
        if (sender != null && sender.identity != null && sender.identity.gameObject != null)
        {
            var playerObj = sender.identity.gameObject;
            Vector3 playerPos = playerObj.transform.position;
            Vector3 resourcePos = visual != null ? visual.transform.position : Vector3.zero;
            float dist = Vector3.Distance(playerPos, resourcePos);
            if (dist > maxChopDistance)
            {
                Debug.Log($"[ResourceInstanceNet] CmdRequestChop: player too far ({dist:F2}m) to chop resource {uniqueId}");
                return;
            }
        }

        // Passed basic validation: ask ResourceManager to apply authoritative change.
        // RequestResourceStateChange will call the manager's host apply path and broadcast accordingly.
        try
        {
            _rm.RequestResourceStateChange(uniqueId, true);

            // Also set server SyncVar immediately for this server instance so clients are updated without delay.
            // ResourceManager.OnResourceStateChanged handler (if present) may also update this, but set here as well.
            SetServerIsChopped(true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ResourceInstanceNet] CmdRequestChop: failed to request change on ResourceManager: {ex}");
        }
    }

    #region Server helpers

    /// <summary>
    /// Called by ResourceManager on the server when spawning to set initial SyncVar values before NetworkServer.Spawn.
    /// Best-effort; setting SyncVars on the server instance ensures clients receive initial values on spawn.
    /// </summary>
    public void SetServerInitialState(string id, bool isChopped)
    {
        // Setting SyncVars directly on server instance is acceptable; Mirror will include them in the spawn message.
        uniqueId = id;
        isChoppedSync = isChopped;
    }

    /// <summary>
    /// Best-effort helper used by ResourceManager when the authoritative state changes on the host.
    /// Setting this on the server instance updates the SyncVar and replicates to clients.
    /// </summary>
    public void SetServerIsChopped(bool isChopped)
    {
        if (!isServer) return;
        isChoppedSync = isChopped;
    }

    #endregion

    #region ResourceManager wiring

    private void TryFindAndWireResourceManager()
    {
        if (_rm == null)
            _rm = UnityEngine.Object.FindObjectOfType<ResourceManager>();

        if (_rm != null)
        {
            // Register visual with manager so manager's instancesById includes this GameObject
            try
            {
                if (visual != null)
                {
                    visual.RegisterWithManagerImmediate(_rm);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ResourceInstanceNet] RegisterWithManagerImmediate failed: {ex}");
            }

            // Subscribe to manager state-changed events so we can react (update visuals and server SyncVar)
            if (!_subscribedToManager)
            {
                try
                {
                    _rm.OnResourceStateChanged += HandleManagerResourceStateChanged;
                    _subscribedToManager = true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ResourceInstanceNet] Failed to subscribe to ResourceManager.OnResourceStateChanged: {ex}");
                }
            }
        }
    }

    private void UnsubscribeFromManager()
    {
        if (_rm != null && _subscribedToManager)
        {
            try
            {
                _rm.OnResourceStateChanged -= HandleManagerResourceStateChanged;
            }
            catch { /* best-effort */ }
            _subscribedToManager = false;
        }
    }

    private void HandleManagerResourceStateChanged(ResourceStateChangeEvent evt)
    {
        if (evt == null) return;
        if (evt.uniqueId != uniqueId) return;

        // Update local visual immediately (client/server)
        if (visual != null)
        {
            try { visual.SetChoppedLocal(evt.newState); }
            catch { /* ignore visual errors */ }
        }

        // If we're the server instance, ensure the SyncVar is also set (replicate to clients)
        if (isServer)
        {
            try { SetServerIsChopped(evt.newState); }
            catch { /* ignore */ }
        }
    }

    #endregion

    #region Editor/debug helpers

    [ContextMenu("Debug: ClientRequestChop (optimistic)")]
    private void Debug_ClientRequestChop()
    {
        ClientRequestChop(true);
    }

    #endregion
}
