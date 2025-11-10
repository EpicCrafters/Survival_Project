using Mirror;
using UnityEngine;

public class NetworkSnapshotRelay : NetworkBehaviour
{
    // Client -> Server: request snapshot for a specific scene/chunk (no authority required)
    [Command(requiresAuthority = false)]
    public void Cmd_RequestSnapshotFromClient(string sceneName, NetworkConnectionToClient sender = null)
    {
        if (!NetworkServer.active) return;
        var rm = ResourceManager.GetManagerForScene(gameObject.scene) ?? ResourceManager.Instance;
        if (rm == null)
        {
            Debug.LogError("[NetworkSnapshotRelay] No ResourceManager found to handle snapshot request.");
            return;
        }

        // SceneName may be null/empty to request full world (not recommended)
        rm.SendInitialStateTo(sender, this, filterSceneName: sceneName);
    }

    [TargetRpc]
    public void Target_SendSnapshotPage(NetworkConnection target, string pageJson)
    {
        ClientResourceVisuals.Instance?.ApplyResourcePageJson(pageJson);
    }

    [TargetRpc]
    public void Target_FinishSnapshot(NetworkConnection target)
    {
        ClientResourceVisuals.Instance?.OnInitialSnapshotComplete();
    }
}
