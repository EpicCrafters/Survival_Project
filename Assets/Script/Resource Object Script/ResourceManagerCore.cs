// ResourceManagerCore.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure core: in-memory model & events. No IO, no networking.
/// </summary>
public class ResourceManagerCore
{
    public readonly Dictionary<string, SpawnRecord> recordsById = new Dictionary<string, SpawnRecord>();

    // Events
    public event Action<SpawnRecord> OnRecordChanged;      // record mutated locally/authoritatively
    public event Action<string, bool> OnRecordChangedRaw;  // id + state
    public event Action<SpawnRecord> OnSpawnRequested;     // ask facade to spawn visual

    public SpawnRecordCollection GetSnapshot()
    {
        return new SpawnRecordCollection { records = new List<SpawnRecord>(recordsById.Values) };
    }

    public void LoadSnapshot(SpawnRecordCollection snapshot)
    {
        recordsById.Clear();
        if (snapshot == null) return;
        foreach (var r in snapshot.records) recordsById[r.uniqueId] = r;
    }

    public void RequestSpawnAll()
    {
        foreach (var r in recordsById.Values) OnSpawnRequested?.Invoke(r);
    }

    public bool TryGetRecord(string uniqueId, out SpawnRecord rec) => recordsById.TryGetValue(uniqueId, out rec);

    public void AddOrUpdateRecord(SpawnRecord r)
    {
        if (string.IsNullOrEmpty(r?.uniqueId)) return;
        recordsById[r.uniqueId] = r;
    }

    public void ApplyLocalChange(string uniqueId, bool isChopped)
    {
        if (!recordsById.TryGetValue(uniqueId, out var rec))
        {
            Debug.LogWarning($"[Core] ApplyLocalChange: unknown id {uniqueId}");
            return;
        }
        rec.isChopped = isChopped;
        OnRecordChanged?.Invoke(rec);
        OnRecordChangedRaw?.Invoke(uniqueId, isChopped);
    }

    /// <summary>
    /// Apply authority change (used by clients when host broadcasts).
    /// Behavior mirrors ApplyLocalChange.
    /// </summary>
    public void ApplyAuthorityChange(string uniqueId, bool isChopped)
    {
        if (!recordsById.TryGetValue(uniqueId, out var rec))
        {
            Debug.LogWarning($"[Core] ApplyAuthorityChange: unknown id {uniqueId}");
            return;
        }
        rec.isChopped = isChopped;
        OnRecordChanged?.Invoke(rec);
        OnRecordChangedRaw?.Invoke(uniqueId, isChopped);
    }
}
