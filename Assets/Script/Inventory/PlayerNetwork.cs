using Mirror;
using UnityEngine;

public class PlayerNetwork : NetworkBehaviour
{
    [SerializeField] private GameObject worldItemBundlePrefab;

    [Server]
    public void SpawnWorldItem(int itemId, int count)
    {
        ItemData data = ItemDatabase.Get(itemId);
        if (data == null || count <= 0) return;

        Vector3 pos = GetDropPosition();

        GameObject go = Instantiate(worldItemBundlePrefab, pos, Quaternion.identity);
        var bundle = go.GetComponent<WorldItemBundle>();
        bundle.Init(data, count, Vector3.zero);

        NetworkServer.Spawn(go);
    }
    private Vector3 GetDropPosition()
    {
        Vector3 forward = transform.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 target = origin + forward * 1.5f;

        // Raycast xuống đất
        if (Physics.Raycast(target, Vector3.down, out RaycastHit hit, 5f))
        {
            return hit.point + Vector3.up * 0.1f; // nhích lên tránh xuyên đất
        }

        // Fallback nếu không hit
        return transform.position + forward * 1.2f + Vector3.up * 0.5f;
    }

}
