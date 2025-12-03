using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Collections;
using UnityEngine;
using Mirror;
using System.Linq;

/// <summary>
/// Central server-side router for ResourceManager operations.
/// - Place exactly one instance in the bootstrap/persistence scene (with NetworkIdentity).
/// - Clients call CmdRequestChange / CmdRequestSnapshot / CmdRequestHostSave.
/// - Server applies changes to the appropriate ResourceManager(s) and replies to clients via TargetRpc / ClientRpc.
/// - This version sends snapshots in byte[] chunks to avoid Mirror's string size limit,
///   and delays sends when connection isn't ready yet.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class ResourceManagerRouter : NetworkBehaviour
{
    // SAFE chunk size in bytes (lowered to be safer for testing).
    private const int SAFE_MAX_CHUNK_SIZE = 50000; // 50 KB

    // snapshot id generator
    private int nextSnapshotId = 1;

    // ================= Commands (Client -> Server) =================

    [Command(requiresAuthority = false)]
    public void CmdRequestSnapshot(NetworkConnectionToClient sender = null)
    {
        if (!isServer) return;

        try
        {
            var managers = ResourceManager.GetAllManagers();
            foreach (var rm in managers)
            {
                if (rm == null) continue;
                var snap = rm.core.GetSnapshot();
                string json = JsonUtility.ToJson(snap);
                byte[] payload = Encoding.UTF8.GetBytes(json);
                Debug.Log($"[Router] CmdRequestSnapshot: built payload len={payload.Length} for conn={sender?.connectionId}");
                SendSnapshotChunked(sender, payload, compressed: false);
                return;
            }
            Debug.Log("[Router] CmdRequestSnapshot: no manager snapshot found, sending empty payload.");
            SendSnapshotChunked(sender, new byte[0], compressed: false);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Router] CmdRequestSnapshot failed: {ex}");
            SendSnapshotChunked(sender, new byte[0], compressed: false);
        }
    }

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

    // ================= Server -> Client replies / broadcasts =================

    // Keep old small-payload TargetReceiveSnapshot for compatibility
    [TargetRpc]
    public void TargetReceiveSnapshot(NetworkConnection target, string json)
    {
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

    // CHUNKED TargetRpc: send a payload chunk to the target client.
    [TargetRpc]
    private void TargetReceiveSnapshotChunk(NetworkConnection target, int snapshotId, int index, int totalChunks, byte[] chunk, bool compressed)
    {
        // Runs on the client; forward to the local singleton to reassemble/process
        ClientSnapshotReceiver.Instance?.OnReceiveChunk(snapshotId, index, totalChunks, chunk, compressed);
    }

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

    // ===================== Chunk sending helpers =====================

    /// <summary>
    /// Splits payload into SAFE_MAX_CHUNK_SIZE blocks and sends each to the given client connection.
    /// If connection isn't ready yet, queue the send via DelaySendWhenReady coroutine.
    /// </summary>
    public void SendSnapshotChunked(NetworkConnectionToClient conn, byte[] payload, bool compressed)
    {
        if (!isServer) return;
        if (conn == null) return;

        // ensure payload not null
        if (payload == null) payload = new byte[0];

        // If connection is not ready yet, delay sending until it's ready (avoid early RPCs)
        if (!conn.isReady)
        {
            Debug.LogWarning($"[Router] Conn {conn.connectionId} not ready. Scheduling delayed snapshot send (len={payload.Length}).");
            StartCoroutine(DelaySendWhenReady(conn, payload, compressed));
            return;
        }

        int snapshotId = System.Threading.Interlocked.Increment(ref nextSnapshotId);

        int maxChunk = SAFE_MAX_CHUNK_SIZE;
        int totalChunks = (payload.Length + maxChunk - 1) / Math.Max(1, maxChunk);
        if (totalChunks == 0) totalChunks = 1;

        for (int i = 0; i < totalChunks; ++i)
        {
            int offset = i * maxChunk;
            int len = Math.Min(maxChunk, Math.Max(0, payload.Length - offset));
            byte[] chunk = new byte[len];
            if (len > 0) Array.Copy(payload, offset, chunk, 0, len);

            try
            {
                Debug.Log($"[Router] Sending snapshot chunk to conn={conn.connectionId} snapshotId={snapshotId} {i + 1}/{totalChunks} len={chunk.Length} compressed={compressed}");
                TargetReceiveSnapshotChunk(conn, snapshotId, i, totalChunks, chunk, compressed);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Router] Exception sending chunk {i}/{totalChunks} to conn {conn.connectionId}: {ex}");
            }
        }
    }

    private IEnumerator DelaySendWhenReady(NetworkConnectionToClient conn, byte[] payload, bool compressed)
    {
        float timeout = 10f;
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            if (conn == null)
            {
                Debug.LogWarning($"[Router] DelaySendWhenReady: conn became null, aborting.");
                yield break;
            }
            if (conn.isReady) break;
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (conn == null || !conn.isReady)
        {
            Debug.LogWarning($"[Router] Connection {conn?.connectionId ?? -1} not ready after wait; aborting snapshot send.");
            yield break;
        }
        // send now
        SendSnapshotChunked(conn, payload, compressed);
    }

    // OPTIONAL: compression helper (server-side) if you want to compress before sending.
    private static byte[] CompressBytes(byte[] data)
    {
        if (data == null || data.Length == 0) return data;
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
