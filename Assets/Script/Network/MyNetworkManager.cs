using UnityEngine;
using Mirror;

public class MyNetworkManager : NetworkManager
{
    [Header("Custom Spawn Point")]
    public Transform spawnPoint;

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        Debug.Log("Spawning player for connection " + conn.connectionId);

        Vector3 pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        GameObject player = Instantiate(playerPrefab, pos, rot);
        NetworkServer.AddPlayerForConnection(conn, player);
    }
}
