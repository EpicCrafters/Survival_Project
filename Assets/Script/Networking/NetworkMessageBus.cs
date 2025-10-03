using System;
using UnityEngine;

/// <summary>
/// Lightweight static message bus — clients subscribe to these events to get snapshot/change messages
/// delivered by the ResourceManagerRouter RPCs. This decouples router RPC handlers from per-scene adapters.
/// </summary>
public static class NetworkMessageBus
{
    public static event Action<SpawnRecordCollection> OnSnapshotReceived;
    public static event Action<string, bool> OnChangeReceived;

    public static void RaiseSnapshotReceived(SpawnRecordCollection container)
    {
        try { OnSnapshotReceived?.Invoke(container); }
        catch (Exception ex) { Debug.LogError($"[NetworkMessageBus] Snapshot handler threw: {ex}"); }
    }

    public static void RaiseChangeReceived(string uniqueId, bool isChopped)
    {
        try { OnChangeReceived?.Invoke(uniqueId, isChopped); }
        catch (Exception ex) { Debug.LogError($"[NetworkMessageBus] Change handler threw: {ex}"); }
    }
}
