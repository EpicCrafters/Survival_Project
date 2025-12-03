// MirrorNetworkAdapter_Router.cs
using System;
using System.Collections;
using UnityEngine;
using Mirror;

/// <summary>
/// INetworkAdapter implementation backed by a single ResourceManagerRouter NetworkBehaviour.
/// - Attach to the same GameObject as ResourceManager (no NetworkIdentity required).
/// - Finds ResourceManagerRouter in the scene and forwards client->server requests to it.
/// - Subscribes to NetworkMessageBus to receive server->client RPCs.
/// </summary>
[RequireComponent(typeof(ResourceManager))]
public class MirrorNetworkAdapter_Router : MonoBehaviour, INetworkAdapter
{
    // INetworkAdapter properties
    public bool IsHost => NetworkServer.active;
    public bool IsClient => NetworkClient.isConnected;

    // Events required by the interface
    public event Action<string, bool, int> OnChangeReceived;
    public event Action<SpawnRecordCollection> OnSnapshotReceived;

    private ResourceManager rm;
    private ResourceManagerRouter router;

    void Awake()
    {
        rm = GetComponent<ResourceManager>();
        if (rm == null) Debug.LogError("[MirrorNetworkAdapter] ResourceManager not found on GameObject.");

        // find the router (should live in bootstrap/persistence scene)
        router = FindObjectOfType<ResourceManagerRouter>();
        if (router == null)
            Debug.LogWarning("[MirrorNetworkAdapter] ResourceManagerRouter not found in scene. Commands will fail until router is present.");

        // register adapter with the resource manager
        try { rm.SetNetworkAdapter(this); }
        catch (Exception ex) { Debug.LogWarning("[MirrorNetworkAdapter] SetNetworkAdapter failed: " + ex.Message); }

        // subscribe to global bus for incoming RPCs
        NetworkMessageBus.OnSnapshotReceived += HandleBusSnapshot;
    }

    void OnDestroy()
    {
        NetworkMessageBus.OnSnapshotReceived -= HandleBusSnapshot;
    }

    // ---------------- INetworkAdapter API ----------------

    /// <summary>
    /// Client asks host for a snapshot (or host/local returns).
    /// </summary>
    public void RequestSnapshot()
    {
        // If host/standalone, supply snapshot locally
        if (NetworkServer.active && !NetworkClient.isConnected)
        {
            var snap = rm != null ? rm.core.GetSnapshot() : null;
            OnSnapshotReceived?.Invoke(snap);
            return;
        }

        // Client -> ask router (Command)
        if (router != null && NetworkClient.isConnected)
        {
            try { router.CmdRequestSnapshot(); }
            catch (Exception ex) { Debug.LogWarning($"[MirrorNetworkAdapter] CmdRequestSnapshot failed call: {ex}"); }
            return;
        }

        // Fallback: no router or offline
        Debug.LogWarning("[MirrorNetworkAdapter] RequestSnapshot: router not available or not connected.");
        OnSnapshotReceived?.Invoke(null);
    }

    /// <summary>
    /// Host sends snapshot to clients (or broadcast).
    /// </summary>
    public void SendSnapshot(SpawnRecordCollection container)
    {
        if (NetworkServer.active)
        {
            if (router != null) router.BroadcastSnapshotToClients(container);
            else Debug.LogWarning("[MirrorNetworkAdapter] SendSnapshot: router not found on server.");
        }
        else
        {
            Debug.LogWarning("[MirrorNetworkAdapter] SendSnapshot called on client - ignored.");
        }
    }

    /// <summary>
    /// Request host to save all managers (single RPC).
    /// </summary>
    public void RequestHostSave()
    {
        if (NetworkClient.isConnected && router != null)
        {
            try { router.CmdRequestHostSave(); }
            catch (Exception ex) { Debug.LogWarning($"[MirrorNetworkAdapter] CmdRequestHostSave call failed: {ex}"); }
            return;
        }

        if (NetworkServer.active)
        {
            foreach (var m in ResourceManager.GetAllManagers())
            {
                try { m.SaveNow(); }
                catch (Exception ex) { Debug.LogError($"[MirrorNetworkAdapter] Local SaveNow failed for {m?.name}: {ex}"); }
            }
            return;
        }

        Debug.LogWarning("[MirrorNetworkAdapter] RequestHostSave: no network router or server.");
    }

    // ---------------- Bus handlers (router -> client via NetworkMessageBus) ----------------

    private void HandleBusSnapshot(SpawnRecordCollection snap)
    {
        try { OnSnapshotReceived?.Invoke(snap); } catch (Exception ex) { Debug.LogError($"[MirrorNetworkAdapter] Bus snapshot handler threw: {ex}"); }
    }
}
