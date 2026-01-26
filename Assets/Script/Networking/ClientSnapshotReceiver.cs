using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

/// <summary>
/// Client-side singleton that reassembles chunked snapshots sent by ResourceManagerRouter.
/// NOW WITH PROGRESS TRACKING for UI feedback
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
    private const float BUFFER_TIMEOUT = 120f; // Increased from 30s to 120s

    // PUBLIC PROGRESS TRACKING - for UI to display
    public int CurrentSnapshotId { get; private set; } = -1;
    public int ReceivedChunks { get; private set; } = 0;
    public int TotalChunks { get; private set; } = 0;
    public float ProgressPercent => TotalChunks > 0 ? (float)ReceivedChunks / TotalChunks * 100f : 0f;
    public bool IsReceiving => CurrentSnapshotId >= 0 && ReceivedChunks < TotalChunks;
    public DateTime LastChunkReceived { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // cleanup timed-out buffers
        var now = DateTime.UtcNow;
        var toRemove = new List<int>();
        foreach (var kv in buffers)
        {
            var elapsed = (now - kv.Value.firstReceivedAt).TotalSeconds;
            if (elapsed > BUFFER_TIMEOUT)
            {
                Debug.LogWarning($"[ClientReceiver] Buffer {kv.Key} timed out after {elapsed:F1}s");
                toRemove.Add(kv.Key);
            }
        }
        foreach (var id in toRemove)
        {
            buffers.Remove(id);
            if (id == CurrentSnapshotId)
            {
                CurrentSnapshotId = -1;
                ReceivedChunks = 0;
                TotalChunks = 0;
            }
        }

        // Warn if receiving stalled
        if (IsReceiving && (DateTime.UtcNow - LastChunkReceived).TotalSeconds > 5f)
        {
            Debug.LogWarning($"[ClientReceiver] No chunks for 5s! Progress: {ProgressPercent:F1}% ({ReceivedChunks}/{TotalChunks})");
        }
    }

    // Called by Router.TargetReceiveSnapshotChunk via forwarding
    public void OnReceiveChunk(int snapshotId, int index, int totalChunks, byte[] chunk, bool compressed)
    {
        try
        {
            LastChunkReceived = DateTime.UtcNow;

            // validation
            if (index < 0 || totalChunks <= 0)
            {
                Debug.LogWarning($"[ClientReceiver] Invalid chunk: snapshotId={snapshotId} index={index} totalChunks={totalChunks}");
                return;
            }

            if (!buffers.TryGetValue(snapshotId, out var buf))
            {
                // New snapshot started
                buf = new SnapshotBuffer
                {
                    totalChunks = totalChunks,
                    receivedCount = 0,
                    chunks = new byte[totalChunks][],
                    firstReceivedAt = DateTime.UtcNow,
                    compressed = compressed
                };
                buffers[snapshotId] = buf;

                // Update progress tracking
                CurrentSnapshotId = snapshotId;
                TotalChunks = totalChunks;
                ReceivedChunks = 0;

                Debug.Log($"[ClientReceiver] Starting snapshot {snapshotId}: {totalChunks} chunks, compressed={compressed}");
            }

            if (index >= buf.totalChunks)
            {
                Debug.LogWarning($"[ClientReceiver] Chunk index {index} out of range (max {buf.totalChunks})");
                return;
            }

            if (buf.chunks[index] == null)
            {
                buf.chunks[index] = chunk ?? new byte[0];
                buf.receivedCount++;
                ReceivedChunks = buf.receivedCount;

                // Log progress every 50 chunks or at completion
                if (buf.receivedCount % 50 == 0 || buf.receivedCount == buf.totalChunks)
                {
                    Debug.Log($"[ClientReceiver] Progress: {ProgressPercent:F1}% ({ReceivedChunks}/{TotalChunks} chunks)");
                }
            }
            else
            {
                Debug.Log($"[ClientReceiver] Duplicate chunk {index} ignored");
            }

            // All chunks received?
            if (buf.receivedCount == buf.totalChunks)
            {
                Debug.Log($"[ClientReceiver] All chunks received! Assembling {snapshotId}...");
                AssembleSnapshot(snapshotId, buf);
            }
        }
        catch (Exception outer)
        {
            Debug.LogError($"[ClientReceiver] OnReceiveChunk exception snapshotId={snapshotId} index={index}: {outer}");
        }
    }

    private void AssembleSnapshot(int snapshotId, SnapshotBuffer buf)
    {
        try
        {
            // Calculate total size
            int totalBytes = 0;
            for (int i = 0; i < buf.totalChunks; ++i)
                totalBytes += (buf.chunks[i]?.Length ?? 0);

            // Reassemble
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

            Debug.Log($"[ClientReceiver] Assembled {totalBytes} bytes");

            // Decompress if needed
            if (buf.compressed)
            {
                try
                {
                    Debug.Log($"[ClientReceiver] Decompressing...");
                    all = DecompressBytes(all);
                    Debug.Log($"[ClientReceiver] Decompressed to {all.Length} bytes");
                }
                catch (Exception dex)
                {
                    Debug.LogError($"[ClientReceiver] Decompression failed: {dex}");
                    buffers.Remove(snapshotId);
                    NetworkMessageBus.RaiseSnapshotReceived(null);
                    ResetProgress();
                    return;
                }
            }

            // Parse JSON
            string json = Encoding.UTF8.GetString(all);
            Debug.Log($"[ClientReceiver] Parsing JSON ({json?.Length ?? 0} chars)...");

            try
            {
                if (string.IsNullOrEmpty(json))
                {
                    Debug.LogWarning($"[ClientReceiver] Empty snapshot JSON");
                    NetworkMessageBus.RaiseSnapshotReceived(null);
                }
                else
                {
                    var snap = JsonUtility.FromJson<SpawnRecordCollection>(json);
                    Debug.Log($"[ClientReceiver] ✓ Snapshot {snapshotId} complete! {snap?.records?.Count ?? 0} records");
                    NetworkMessageBus.RaiseSnapshotReceived(snap);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClientReceiver] JSON parsing failed: {ex}");
                NetworkMessageBus.RaiseSnapshotReceived(null);
            }

            // Cleanup
            buffers.Remove(snapshotId);
            ResetProgress();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ClientReceiver] AssembleSnapshot failed: {ex}");
            NetworkMessageBus.RaiseSnapshotReceived(null);
            buffers.Remove(snapshotId);
            ResetProgress();
        }
    }

    private void ResetProgress()
    {
        CurrentSnapshotId = -1;
        ReceivedChunks = 0;
        TotalChunks = 0;
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