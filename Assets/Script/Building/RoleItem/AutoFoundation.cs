using UnityEngine;
using Mirror;

public class AutoFoundation : NetworkBehaviour
{
    [SerializeField] private Transform[] cornerPoints;
    [SerializeField] private GameObject pillarPrefab;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float maxPillarLength = 3f;
    [SerializeField] private float minPillarLength = 0.3f;

    public override void OnStartServer()
    {
        // chỉ server mới sinh cột
        foreach (Transform corner in cornerPoints)
        {
            Vector3 start = corner.position;
            if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, maxPillarLength, groundMask))
            {
                float dist = Vector3.Distance(start, hit.point);
                if (dist < minPillarLength) continue;

                GameObject pillar = Instantiate(pillarPrefab, transform);
                AdjustPillarHeight(pillar, start, hit.point, dist);

                NetworkServer.Spawn(pillar); // Sync qua client
            }
        }
    }

    private void AdjustPillarHeight(GameObject pillar, Vector3 top, Vector3 bottom, float dist)
    {
        // Tính lại scale dựa trên mesh bounds như phần 2 bạn đã làm
        var mr = pillar.GetComponentInChildren<MeshRenderer>();
        float originalHeight = mr != null ? mr.bounds.size.y : 1f;

        float scaleY = dist / originalHeight;
        var s = pillar.transform.localScale;
        s.y *= scaleY;
        pillar.transform.localScale = s;

        // đặt đáy ở mặt đất
        float worldHalfHeight = (originalHeight * scaleY) * 0.5f;
        pillar.transform.position = bottom + Vector3.up * worldHalfHeight;
    }
}
