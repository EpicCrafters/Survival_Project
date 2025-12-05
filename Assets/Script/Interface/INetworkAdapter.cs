// INetworkAdapter.cs
using System;

public interface INetworkAdapter
{
    bool IsHost { get; }
    bool IsClient { get; }

    /// <summary>Request a full snapshot (client asks host) / host sends until someone responds.</summary>
    void RequestSnapshot();

    /// <summary>Host sends a snapshot to a client (or broadcast)</summary>
    void SendSnapshot(SpawnRecordCollection container);

    event Action<string, bool, int> OnChangeReceived; // invoked on both host (when a client request arrives) and clients (when host broadcasts)
    event Action<SpawnRecordCollection> OnSnapshotReceived;

    // Optional: Request host to save (if you want clients to ask host to persist)
    void RequestHostSave();
}
