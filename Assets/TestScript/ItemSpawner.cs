using Mirror;
using UnityEngine;

public class ItemSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject itemPrefab; // Prefab đã gắn NetworkIdentity
    [SerializeField] private Transform spawnPoint;

    [Server]
    public void SpawnItem()
    {
        if (itemPrefab == null || spawnPoint == null) return;

        GameObject item = Instantiate(itemPrefab, spawnPoint.position, Quaternion.identity);
    
        NetworkServer.Spawn(item);
        Debug.Log("Spawned item at " + spawnPoint.position);
    }

    // Tạo nút trong Inspector (Right Click hoặc dấu 3 chấm trên component)
    [ContextMenu("Spawn Item (Server Only)")]
    private void SpawnItemFromInspector()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Chỉ bấm khi đang Play Mode!");
            return;
        }

        if (isServer)
            SpawnItem();
        else
            Debug.LogWarning("Chỉ server/host mới được spawn item!");
    }
}
