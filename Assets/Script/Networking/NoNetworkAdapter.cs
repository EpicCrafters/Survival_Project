// NoNetworkAdapter.cs
using System;
public class NoNetworkAdapter : INetworkAdapter
{
    public bool IsHost => true;   // treating standalone as authoritative
    public bool IsClient => false;

    public event Action<string, bool> OnChangeReceived;
    public event Action<SpawnRecordCollection> OnSnapshotReceived;

    public void RequestChange(string uniqueId, bool isChopped)
    {
        // No network: immediately invoke as if host received it (useful for Standalone)
        OnChangeReceived?.Invoke(uniqueId, isChopped);
    }

    public void BroadcastChange(string uniqueId, bool isChopped)
    {
        // No actual clients; call OnChangeReceived for local replica (if any listeners)
        OnChangeReceived?.Invoke(uniqueId, isChopped);
    }

    public void RequestSnapshot()
    {
        // nothing to do
    }

    public void SendSnapshot(SpawnRecordCollection container)
    {
        // nothing to do
    }

    public void RequestHostSave()
    {
        // nothing to do
    }
}
