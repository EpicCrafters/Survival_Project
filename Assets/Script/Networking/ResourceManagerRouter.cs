using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// Central server-side router for ResourceManager operations.
/// - Place exactly one instance in the bootstrap/persistence scene (with NetworkIdentity).
/// - Clients call CmdRequestChange / CmdRequestSnapshot / CmdRequestHostSave.
/// - Server applies changes to the appropriate ResourceManager(s) and replies to clients via TargetRpc / ClientRpc.
/// - RpcReceiveChange / RpcReceiveSnapshot broadcast to clients and will use NetworkMessageBus to deliver to local adapters.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class ResourceManagerRouter : NetworkBehaviour
{
    // ========== Client -> Server Commands ==========

    // Client requests server snapshot for a particular manager scene or "global" (could be extended with scene id)
    // sender is the client connection who called the cmd; we'll send the snapshot back via TargetReceiveSnapshot.
    [Command(requiresAuthority = false)]
    public void CmdRequestSnapshot(NetworkConnectionToClient sender = null)
    {
        if (!isServer) return;

        // Example: combine snapshots from all managers or choose a specific one.
        // Here we return a combined snapshot per-manager by serializing each manager's snapshot as JSON array (simple approach).
        try
        {
            var managers = ResourceManager.GetAllManagers();
            // For simplicity return the first non-null manager snapshot (you can aggregate as needed).
            foreach (var rm in managers)
            {
                if (rm == null) continue;
                var snap = rm.core.GetSnapshot();
                string json = JsonUtility.ToJson(snap);
                TargetReceiveSnapshot(sender, json);
                return;
            }
            // no manager available
            TargetReceiveSnapshot(sender, "");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Router] CmdRequestSnapshot failed: {ex}");
            TargetReceiveSnapshot(sender, "");
        }
    }

    // Client requests a state change for a uniqueId (e.g. chop)
    [Command(requiresAuthority = false)]
    public void CmdRequestChange(string uniqueId, bool isChopped, NetworkConnectionToClient sender = null)
    {
        if (!isServer) return;

        if (string.IsNullOrEmpty(uniqueId)) return;

        // Find owning manager and apply change server-side (authoritative)
        var rm = ResourceManager.GetManagerForUniqueId(uniqueId);
        if (rm != null)
        {
            // Optional: basic server-side validation could be performed here (distance, rate limit, etc.)
            try
            {
                rm.RequestResourceStateChange(uniqueId, isChopped);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Router] Failed to apply change to manager '{rm.name}' for id={uniqueId}: {ex}");
            }
        }
        else
        {
            Debug.LogWarning($"[Router] CmdRequestChange: no manager found for uniqueId {uniqueId}");
        }
    }

    // Client requests host to save all managers (single RPC)
    [Command(requiresAuthority = false)]
    public void CmdRequestHostSave(NetworkConnectionToClient sender = null)
    {
        if (!isServer) return;

        foreach (var rm in ResourceManager.GetAllManagers())
        {
            try { rm.SaveNow(); }
            catch (Exception ex) { Debug.LogError($"[Router] CmdRequestHostSave SaveNow failed for {rm?.name}: {ex}"); }
        }
    }

    // ========== Server -> Client replies / broadcasts ==========

    // Send a snapshot JSON to a particular client
    [TargetRpc]
    public void TargetReceiveSnapshot(NetworkConnection target, string json)
    {
        // Will run on the targeted client.
        // Use NetworkMessageBus to notify local adapter(s).
        if (string.IsNullOrEmpty(json))
        {
            NetworkMessageBus.RaiseSnapshotReceived(null);
            return;
        }
        try
        {
            var snap = JsonUtility.FromJson<SpawnRecordCollection>(json);
            NetworkMessageBus.RaiseSnapshotReceived(snap);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Router] TargetReceiveSnapshot JSON->object failed: {ex}");
            NetworkMessageBus.RaiseSnapshotReceived(null);
        }
    }

    // Broadcast a snapshot to all clients (server->all)
    [ClientRpc]
    public void RpcReceiveSnapshotAll(string json)
    {
        if (isServer) return; // host shouldn't re-apply
        if (string.IsNullOrEmpty(json)) { NetworkMessageBus.RaiseSnapshotReceived(null); return; }
        try
        {
            var snap = JsonUtility.FromJson<SpawnRecordCollection>(json);
            NetworkMessageBus.RaiseSnapshotReceived(snap);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Router] RpcReceiveSnapshotAll JSON->obj failed: {ex}");
            NetworkMessageBus.RaiseSnapshotReceived(null);
        }
    }

    // Broadcast a single change to all clients (uniqueId, isChopped)
    [ClientRpc]
    public void RpcReceiveChange(string uniqueId, bool isChopped)
    {
        if (isServer) return;
        NetworkMessageBus.RaiseChangeReceived(uniqueId, isChopped);
    }

    // Convenience server-side helper for host code to call to broadcast a change to clients
    // Call this from server/host code when you want to broadcast an update
    public void BroadcastChangeToClients(string uniqueId, bool isChopped)
    {
        if (!isServer) return;
        try { RpcReceiveChange(uniqueId, isChopped); }
        catch (Exception ex) { Debug.LogWarning($"[Router] BroadcastChangeToClients failed: {ex}"); }
    }

    // Convenience server-side helper to broadcast a snapshot to all clients
    public void BroadcastSnapshotToClients(SpawnRecordCollection container)
    {
        if (!isServer) return;
        try
        {
            string json = JsonUtility.ToJson(container);
            RpcReceiveSnapshotAll(json);
        }
        catch (Exception ex) { Debug.LogWarning($"[Router] BroadcastSnapshotToClients failed: {ex}"); }
    }
}
