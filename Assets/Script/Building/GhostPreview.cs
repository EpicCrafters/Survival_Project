using UnityEngine;

public class GhostPreview : MonoBehaviour
{
    public LayerMask blockingLayers;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;
    public float extraTolerance = 0.01f;

    public bool IsValid { get; private set; } = true;

    private Vector3 localCenter;
    private Vector3 localExtents;

    private void Awake()
    {
        CacheLocalBounds();
    }

    private void CacheLocalBounds()
    {
        var cols = GetComponentsInChildren<Collider>();
        if (cols.Length == 0)
        {
            localCenter = Vector3.zero;
            localExtents = Vector3.one * 0.1f;
            return;
        }

        Bounds combined = cols[0].bounds;
        for (int i = 1; i < cols.Length; i++)
            combined.Encapsulate(cols[i].bounds);

        // Đưa về local space
        localCenter = transform.InverseTransformPoint(combined.center);
        localExtents = combined.extents;
    }

    public void Revalidate()
    {
        Vector3 worldCenter = transform.TransformPoint(localCenter);
        Vector3 worldExtents = localExtents + Vector3.one * extraTolerance;

        var hits = Physics.OverlapBox(worldCenter, worldExtents, transform.rotation, blockingLayers, triggerInteraction);

        int count = 0;
        foreach (var c in hits)
        {
            if (c.transform.IsChildOf(transform)) continue;
            count++;
        }

        IsValid = (count == 0);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsValid ? new Color(0, 1, 0, 0.25f) : new Color(1, 0, 0, 0.25f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(localCenter, localExtents * 2 + Vector3.one * 0.01f);
    }
}
