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

        //Debug.Log($"[Core] LoadSnapshot: records incoming = {snapshot.records?.Count ?? 0}"); // << add

        foreach (var r in snapshot.records) recordsById[r.uniqueId] = r;

        //Debug.Log($"[Core] LoadSnapshot: recordsById now = {recordsById.Count}"); // << add
    }

    public void RequestSpawnAll()
    {
        //Debug.Log($"[Core] RequestSpawnAll: recordsById count = {recordsById.Count}"); // << add
        foreach (var r in recordsById.Values) OnSpawnRequested?.Invoke(r);
        Debug.Log("[Core] RequestSpawnAll: finished invoking OnSpawnRequested"); // optional
    }

    public bool TryGetRecord(string uniqueId, out SpawnRecord rec) => recordsById.TryGetValue(uniqueId, out rec);

    public void AddOrUpdateRecord(SpawnRecord r)
    {
        if (string.IsNullOrEmpty(r?.uniqueId)) return;
        recordsById[r.uniqueId] = r;
    }

    public void ApplyChange(string uniqueId, bool isChopped, int curHealth)
    {
        if (!recordsById.TryGetValue(uniqueId, out var rec))
        {
            Debug.LogWarning($"[Core] ApplyLocalChange: unknown id {uniqueId}");
            return;
        }
        rec.isChopped = isChopped;
        rec.curHealth = curHealth;
        OnRecordChanged?.Invoke(rec);
        OnRecordChangedRaw?.Invoke(uniqueId, isChopped);
    }
}
