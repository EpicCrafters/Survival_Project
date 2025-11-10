using System;
using System.Text;
using UnityEngine;
using Mirror;

public class CustomNetworkManager : NetworkManager
{
    public override void OnStartServer()
    {
        base.OnStartServer();

        // Ensure clean registration
        NetworkServer.UnregisterHandler<EnvLoadedMessage>();
        NetworkServer.RegisterHandler<EnvLoadedMessage>(OnServerEnvLoaded, false);

        var t = typeof(EnvLoadedMessage);
        Debug.Log($"[NetworkManager] Registered EnvLoadedMessage handler in OnStartServer. Type={t.FullName} Assembly={t.Assembly.FullName}");
    }

    public override void OnStopServer()
    {
        // Unregister to avoid duplicate registrations if server restarts in same process
        try
        {
            NetworkServer.UnregisterHandler<EnvLoadedMessage>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] Unregister handler exception: {ex}");
        }

        base.OnStopServer();
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);
        // Do NOT send full environment snapshots here.
        // Client should send EnvLoadedMessage when it's ready and you'll respond there.
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);
    }

    private void OnServerEnvLoaded(NetworkConnectionToClient conn, EnvLoadedMessage msg)
    {
        // Defensive handler: catch exceptions and avoid throwing out of the handler.
        try
        {
            if (conn == null)
            {
                Debug.LogWarning("[NetworkManager] OnServerEnvLoaded: conn is null");
                return;
            }

            Debug.Log($"[NetworkManager] OnServerEnvLoaded invoked for conn {conn.connectionId} at {DateTime.UtcNow:O}");

            // Example: find the router and ask it to send a chunked snapshot.
            var router = FindObjectOfType<ResourceManagerRouter>();
            if (router != null)
            {
                // Build a snapshot payload. Keep this lightweight here or call a router helper that does it.
                // Example payload builder (adapt to your code):
                byte[] payload = BuildSnapshotBytesFor(conn);
                // Send chunked (router.SendSnapshotChunked is server-side)
                router.SendSnapshotChunked(conn, payload, compressed: false);
                Debug.Log($"[NetworkManager] Triggered chunked snapshot send to conn {conn.connectionId} (payload len={payload?.Length ?? 0})");
            }
            else
            {
                Debug.LogWarning("[NetworkManager] OnServerEnvLoaded: ResourceManagerRouter not found.");
            }
        }
        catch (Exception ex)
        {
            // Important: do not let exceptions escape the handler; Mirror treats that as a fatal invoke failure.
            Debug.LogError($"[NetworkManager] Exception in OnServerEnvLoaded handler: {ex}");
        }
    }

    // Example helper: build snapshot bytes (adapt to your ResourceManager API)
    private byte[] BuildSnapshotBytesFor(NetworkConnectionToClient conn)
    {
        try
        {
            var managers = ResourceManager.GetAllManagers();
            foreach (var rm in managers)
            {
                if (rm == null) continue;
                var snap = rm.core.GetSnapshot(); // your existing API
                string json = JsonUtility.ToJson(snap);
                return Encoding.UTF8.GetBytes(json);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkManager] BuildSnapshotBytesFor failed: {ex}");
        }
        return new byte[0];
    }
}
