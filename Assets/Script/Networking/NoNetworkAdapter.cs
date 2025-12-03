// NoNetworkAdapter.cs
using System;
public class NoNetworkAdapter : INetworkAdapter
{
    public bool IsHost => true;   // treating standalone as authoritative
    public bool IsClient => false;

    public event Action<string, bool, int> OnChangeReceived;
    public event Action<SpawnRecordCollection> OnSnapshotReceived;

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
