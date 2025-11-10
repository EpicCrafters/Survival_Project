// Put this on a server-only bootstrap object
using System.Text;
using UnityEngine;
using Mirror;

public class ServerBootstrap : MonoBehaviour
{
    private void Start()
    {
        if (NetworkServer.active)
        {
            NetworkServer.RegisterHandler<EnvLoadedMessage>(OnServerEnvLoaded, false);
        }
    }

    private void OnServerEnvLoaded(NetworkConnection conn, EnvLoadedMessage msg)
    {
        var router = FindObjectOfType<ResourceManagerRouter>();
        if (router == null) return;

        // Build snapshot bytes (reuse same logic as router or call a router helper if you add one)
        var managers = ResourceManager.GetAllManagers();
        foreach (var rm in managers)
        {
            if (rm == null) continue;
            var snap = rm.core.GetSnapshot();
            string json = JsonUtility.ToJson(snap);
            byte[] payload = Encoding.UTF8.GetBytes(json);

            // Optional compression: payload = CompressBytes(payload) and pass compressed=true
            router.SendSnapshotChunked(conn as NetworkConnectionToClient, payload, compressed: false);
            return;
        }

        // no manager -> send empty payload
        router.SendSnapshotChunked(conn as NetworkConnectionToClient, new byte[0], compressed: false);
    }
}
