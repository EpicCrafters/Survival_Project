using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

/// <summary>
/// Client-side singleton that reassembles chunked snapshots sent by ResourceManagerRouter.
/// Attach to a persistent client GameObject in your bootstrap scene so it's available before RPCs arrive.
/// </summary>
public class ClientSnapshotReceiver : MonoBehaviour
{
    public static ClientSnapshotReceiver Instance { get; private set; }

    class SnapshotBuffer
    {
        public int totalChunks;
        public int receivedCount;
        public byte[][] chunks;
        public DateTime firstReceivedAt;
        public bool compressed;
    }

    private Dictionary<int, SnapshotBuffer> buffers = new Dictionary<int, SnapshotBuffer>();
    private const float BUFFER_TIMEOUT = 30f; // seconds

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        Instance = this;
    }

    private void Update()
    {
        // cleanup timed-out buffers
        var now = DateTime.UtcNow;
        var toRemove = new List<int>();
        foreach (var kv in buffers)
        {
            if ((now - kv.Value.firstReceivedAt).TotalSeconds > BUFFER_TIMEOUT)
                toRemove.Add(kv.Key);
        }
        foreach (var id in toRemove) buffers.Remove(id);
    }

    // Called by Router.TargetReceiveSnapshotChunk via forwarding
    public void OnReceiveChunk(int snapshotId, int index, int totalChunks, byte[] chunk, bool compressed)
    {
        try
        {
            // validation
            if (index < 0 || totalChunks <= 0)
            {
                Debug.LogWarning($"ClientSnapshotReceiver: invalid chunk parameters snapshotId={snapshotId} index={index} totalChunks={totalChunks}");
                return;
            }

            if (!buffers.TryGetValue(snapshotId, out var buf))
            {
                buf = new SnapshotBuffer
                {
                    totalChunks = totalChunks,
                    receivedCount = 0,
                    chunks = new byte[totalChunks][],
                    firstReceivedAt = DateTime.UtcNow,
                    compressed = compressed
                };
                buffers[snapshotId] = buf;
                Debug.Log($"ClientSnapshotReceiver: created buffer for snapshotId={snapshotId} totalChunks={totalChunks} compressed={compressed}");
            }

            if (index >= buf.totalChunks) return;

            if (buf.chunks[index] == null)
            {
                buf.chunks[index] = chunk ?? new byte[0];
                buf.receivedCount++;
                Debug.Log($"ClientSnapshotReceiver: received chunk {index + 1}/{buf.totalChunks} for snapshotId={snapshotId} len={buf.chunks[index].Length}");
            }
            else
            {
                Debug.Log($"ClientSnapshotReceiver: duplicate chunk {index} for snapshotId={snapshotId} ignored.");
            }

            if (buf.receivedCount == buf.totalChunks)
            {
                // reassemble
                int totalBytes = 0;
                for (int i = 0; i < buf.totalChunks; ++i) totalBytes += (buf.chunks[i]?.Length ?? 0);
                var all = new byte[totalBytes];
                int pos = 0;
                for (int i = 0; i < buf.totalChunks; ++i)
                {
                    var c = buf.chunks[i];
                    if (c != null && c.Length > 0)
                    {
                        Array.Copy(c, 0, all, pos, c.Length);
                        pos += c.Length;
                    }
                }

                if (buf.compressed)
                {
                    try { all = DecompressBytes(all); }
                    catch (Exception dex)
                    {
                        Debug.LogError($"ClientSnapshotReceiver: Decompress failed for snapshot {snapshotId}: {dex}");
                        buffers.Remove(snapshotId);
                        return;
                    }
                }

                string json = Encoding.UTF8.GetString(all);
                Debug.Log($"ClientSnapshotReceiver: snapshot {snapshotId} assembled jsonLen={json?.Length ?? 0}");

                try
                {
                    if (string.IsNullOrEmpty(json))
                    {
                        NetworkMessageBus.RaiseSnapshotReceived(null);
                    }
                    else
                    {
                        var snap = JsonUtility.FromJson<SpawnRecordCollection>(json);
                        NetworkMessageBus.RaiseSnapshotReceived(snap);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"ClientSnapshotReceiver: ProcessSnapshotJson error: {ex}");
                    NetworkMessageBus.RaiseSnapshotReceived(null);
                }

                buffers.Remove(snapshotId);
            }
        }
        catch (Exception outer)
        {
            Debug.LogError($"ClientSnapshotReceiver: OnReceiveChunk exception snapshotId={snapshotId} index={index}: {outer}");
        }
    }

    private static byte[] DecompressBytes(byte[] compressed)
    {
        if (compressed == null || compressed.Length == 0) return compressed;
        using (var input = new MemoryStream(compressed))
        using (var gzip = new GZipStream(input, CompressionMode.Decompress))
        using (var output = new MemoryStream())
        {
            gzip.CopyTo(output);
            return output.ToArray();
        }
    }
}
