using System;
using System.Text;
using UnityEngine;
using Mirror;
using System.IO.Compression;
using System.IO;

/// <summary>
/// Merged NetworkManager:
/// - custom spawn point behavior on player add
/// - registers EnvLoadedMessage handler on server start (no race)
/// - defensive OnServerEnvLoaded that forwards to ResourceManagerRouter.SendSnapshotChunked
/// - unregisters handler on server stop
/// </summary>
public class MyNetworkManager : NetworkManager
{
    [Header("Custom Spawn Point")]
    public Transform spawnPoint;

    // Chunking will be handled by ResourceManagerRouter; we only build payload here
    public override void OnStartServer()
    {
        base.OnStartServer();

        try
        {
            NetworkServer.UnregisterHandler<EnvLoadedMessage>();
            NetworkServer.RegisterHandler<EnvLoadedMessage>(OnServerEnvLoaded, false);

            var t = typeof(EnvLoadedMessage);
            Debug.Log($"[MyNetworkManager] Registered EnvLoadedMessage handler in OnStartServer. Type={t.FullName} Assembly={t.Assembly.FullName}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MyNetworkManager] Failed to register EnvLoadedMessage handler: {ex}");
        }
    }

    public override void OnStopServer()
    {
        try
        {
            NetworkServer.UnregisterHandler<EnvLoadedMessage>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MyNetworkManager] Unregister handler exception: {ex}");
        }

        base.OnStopServer();
    }

    // Keep your custom spawn logic
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        Debug.Log($"[MyNetworkManager] Spawning player for connection {conn.connectionId}");

        Vector3 pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        GameObject player = Instantiate(playerPrefab, pos, rot);
        NetworkServer.AddPlayerForConnection(conn, player);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        Debug.Log($"[MyNetworkManager] OnServerDisconnect conn {conn?.connectionId}");
        base.OnServerDisconnect(conn);
    }

    // Defensive handler for EnvLoadedMessage from client
    private void OnServerEnvLoaded(NetworkConnectionToClient conn, EnvLoadedMessage msg)
    {
        try
        {
            if (conn == null)
            {
                Debug.LogWarning("[MyNetworkManager] OnServerEnvLoaded: conn is null");
                return;
            }

            Debug.Log($"[MyNetworkManager] OnServerEnvLoaded invoked for conn {conn.connectionId} at {DateTime.UtcNow:O}");

            // Forward to router in a safe way
            var router = FindObjectOfType<ResourceManagerRouter>();
            if (router != null)
            {
                // Build snapshot bytes (adapt if your ResourceManager API differs)
                byte[] payload = BuildSnapshotBytesFor(conn);
                router.SendSnapshotChunked(conn, payload, compressed: false);
                Debug.Log($"[MyNetworkManager] Triggered chunked snapshot send to conn {conn.connectionId} (payload len={payload?.Length ?? 0})");
            }
            else
            {
                Debug.LogWarning("[MyNetworkManager] ResourceManagerRouter not found; skipping snapshot send.");
            }
        }
        catch (Exception ex)
        {
            // VERY important: do not let exceptions escape this handler
            Debug.LogError($"[MyNetworkManager] Exception in OnServerEnvLoaded handler: {ex}");
        }
    }

    // Example helper to create snapshot bytes; replace with your actual snapshot creation logic.
    private byte[] BuildSnapshotBytesFor(NetworkConnectionToClient conn)
    {
        try
        {
            var managers = ResourceManager.GetAllManagers();
            foreach (var rm in managers)
            {
                if (rm == null) continue;
                var snap = rm.core.GetSnapshot();
                string json = JsonUtility.ToJson(snap);
                byte[] uncompressed = Encoding.UTF8.GetBytes(json);

                // Compress the data
                byte[] compressed = CompressBytes(uncompressed);

                Debug.Log($"[MyNetworkManager] Compressed snapshot: {uncompressed.Length} → {compressed.Length} bytes ({(float)compressed.Length / uncompressed.Length * 100:F1}%)");

                return compressed;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MyNetworkManager] BuildSnapshotBytesFor failed: {ex}");
        }
        return new byte[0];
    }

    private static byte[] CompressBytes(byte[] data)
    {
        using (var ms = new MemoryStream())
        {
            using (var gzip = new GZipStream(ms, CompressionMode.Compress))
            {
                gzip.Write(data, 0, data.Length);
            }
            return ms.ToArray();
        }
    }
}