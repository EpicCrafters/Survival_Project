// MirrorNetworkAdapter.cs
using System;
using System.Collections;
using UnityEngine;
using Mirror;

[RequireComponent(typeof(ResourceManager))]
public class MirrorNetworkAdapter : NetworkBehaviour, INetworkAdapter
{
    public event Action<string, bool> OnChangeReceived;
    public event Action<SpawnRecordCollection> OnSnapshotReceived;

    ResourceManager rm;

    public bool IsHost => NetworkServer.active;
    public bool IsClient => NetworkClient.isConnected;

    void Awake()
    {
        rm = GetComponent<ResourceManager>();
        if (rm == null) Debug.LogError("[MirrorNetworkAdapter] ResourceManager not found on GameObject.");

        // Prefer explicit API on ResourceManager
        try
        {
            rm.SetNetworkAdapter(this);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[MirrorNetworkAdapter] Failed to SetNetworkAdapter: " + ex.Message);
            // Fallback: set internal field via reflection if absolutely necessary (not recommended)
        }
    }

    // -------------------------
    // Snapshot logic
    // -------------------------
    public void RequestSnapshot()
    {
        // If client, ask server (Cmd). If host/standalone, invoke locally.
        if (IsClient && !IsHost)
        {
            if (NetworkClient.isConnected) CmdRequestSnapshot();
            else StartCoroutine(WaitForConnectionThenRequestSnapshot());
            return;
        }

        // Host / standalone - give local snapshot
        var snap = rm != null ? rm.core.GetSnapshot() : null;
        OnSnapshotReceived?.Invoke(snap);
    }

    IEnumerator WaitForConnectionThenRequestSnapshot()
    {
        float timeout = 10f; float timer = 0f;
        while (!NetworkClient.isConnected && timer < timeout)
        {
            yield return null; timer += Time.deltaTime;
        }
        if (NetworkClient.isConnected) CmdRequestSnapshot();
        else Debug.LogWarning("[MirrorNetworkAdapter] Timeout waiting for connection to request snapshot.");
    }

    [Command(requiresAuthority = false)]
    void CmdRequestSnapshot(NetworkConnectionToClient sender = null)
    {
        if (!isServer) return;
        var snap = rm != null ? rm.core.GetSnapshot() : null;
        string json = JsonUtility.ToJson(snap);
        TargetReceiveSnapshot(sender, json);
    }

    [TargetRpc]
    void TargetReceiveSnapshot(NetworkConnection target, string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            OnSnapshotReceived?.Invoke(null);
            return;
        }
        var snap = JsonUtility.FromJson<SpawnRecordCollection>(json);
        OnSnapshotReceived?.Invoke(snap);
    }

    public void SendSnapshot(SpawnRecordCollection container)
    {
        // Host broadcast snapshot to all clients
        string json = JsonUtility.ToJson(container);
        if (isServer)
            RpcReceiveSnapshotAll(json);
        else
            OnSnapshotReceived?.Invoke(container);
    }

    [ClientRpc]
    void RpcReceiveSnapshotAll(string json)
    {
        // Host should not re-apply its own RPC
        if (isServer) return;
        var snap = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<SpawnRecordCollection>(json);
        OnSnapshotReceived?.Invoke(snap);
    }

    // -------------------------
    // Change (client -> server; server -> clients)
    // -------------------------
    public void RequestChange(string uniqueId, bool isChopped)
    {
        if (IsClient && !IsHost)
        {
            if (NetworkClient.isConnected) CmdRequestChange(uniqueId, isChopped);
            else StartCoroutine(WaitAndCmdRequestChange(uniqueId, isChopped));
            return;
        }

        // host/local
        OnChangeReceived?.Invoke(uniqueId, isChopped);
    }

    IEnumerator WaitAndCmdRequestChange(string id, bool chopped)
    {
        float timeout = 10f; float timer = 0f;
        while (!NetworkClient.isConnected && timer < timeout)
        {
            yield return null; timer += Time.deltaTime;
        }
        if (NetworkClient.isConnected) CmdRequestChange(id, chopped);
        else Debug.LogWarning("[MirrorNetworkAdapter] Timeout waiting for connection to request change.");
    }

    [Command(requiresAuthority = false)]
    void CmdRequestChange(string uniqueId, bool isChopped, NetworkConnectionToClient sender = null)
    {
        if (!isServer) return;
        // Notify the ResourceManager by raising the event (ResourceManager subscribed)
        OnChangeReceived?.Invoke(uniqueId, isChopped);

        // As a guard if ResourceManager wasn't wired for some reason:
        if (rm != null)
        {
            var method = typeof(ResourceManager).GetMethod("HandleNetworkChange",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (method != null) method.Invoke(rm, new object[] { uniqueId, isChopped });
        }
    }

    public void BroadcastChange(string uniqueId, bool isChopped)
    {
        if (isServer) RpcReceiveChange(uniqueId, isChopped);
        else OnChangeReceived?.Invoke(uniqueId, isChopped);
    }

    [ClientRpc]
    void RpcReceiveChange(string uniqueId, bool isChopped)
    {
        // Avoid host double-apply
        if (isServer) return;
        OnChangeReceived?.Invoke(uniqueId, isChopped);
    }

    // -------------------------
    // Request host save
    // -------------------------
    public void RequestHostSave()
    {
        if (IsClient && !IsHost)
        {
            if (NetworkClient.isConnected) CmdRequestHostSave();
            else StartCoroutine(WaitAndCmdRequestHostSave());
            return;
        }

        // host or standalone
        rm?.SaveNow();
    }

    IEnumerator WaitAndCmdRequestHostSave()
    {
        float timeout = 10f; float timer = 0f;
        while (!NetworkClient.isConnected && timer < timeout)
        {
            yield return null; timer += Time.deltaTime;
        }
        if (NetworkClient.isConnected) CmdRequestHostSave();
        else Debug.LogWarning("[MirrorNetworkAdapter] Timeout waiting for connection to request host save.");
    }

    [Command(requiresAuthority = false)]
    void CmdRequestHostSave()
    {
        if (!isServer) return;
        rm?.SaveNow();
    }
}
