using UnityEngine;

public class DefaultPlacementRole : ScriptableObject, IBuildPlacementRole
{
    public virtual void ComputePreview(
     Vector3 hitPoint,
     float rotY,
     Transform anchor,
     BuildManager manager,
     ItemData item,
     out Vector3 outPos,
     out float outRotY,
     out bool canPlace)
    {
        // Nếu có anchor → snap theo grid
        if (anchor != null)
        {
            ComputeSnappedWithEdgeSupport(hitPoint, rotY, anchor, manager, item,
                out outPos, out outRotY);
        }
        else
        {
            outPos = hitPoint;
            outRotY = rotY;
        }

        // Q/E xoay thêm
        outRotY += rotY;

        // Check
        canPlace = CheckOccupy(outPos, outRotY, item, manager);
    }


    public virtual bool ValidatePlacement(
        Vector3 pos,
        float rotY,
        Transform anchor,
        ItemData item,
        BuildManager manager)
    {
        return true;
    }

    public virtual void OnServerPlaced(GameObject spawnedObj,
        Transform anchor,
        ItemData item,
        BuildManager manager)
    {
    }


    // ================
    // Helper methods
    // ================

    protected void ComputeSnappedTransformLikeSystem(
        Vector3 raw, float rotY, Transform anchor,
        BuildManager manager, ItemData obj,
        out Vector3 outPos, out float outRotY)
    {
        outRotY = 0f;
        outPos = raw;

        if (anchor == null) return;

        Vector3 origin = anchor.position;

        float localX = (raw.x - origin.x) / manager.cellWidth;
        float localZ = (raw.z - origin.z) / manager.cellWidth;
        float localY = (raw.y - origin.y) / manager.cellHeight;

        int ix = Mathf.RoundToInt(localX);
        int iz = Mathf.RoundToInt(localZ);
        int iy = Mathf.RoundToInt(localY);

        Vector3 cellCenter = new Vector3(
            origin.x + ix * manager.cellWidth,
            origin.y + iy * manager.cellHeight,
            origin.z + iz * manager.cellWidth
        );

        outPos = cellCenter;
        outRotY = 0;

        int targetLayer = iy + obj.building.verticalOffset;
        outPos.y = origin.y + targetLayer * manager.cellHeight;
    }

    // inside your role class (DefaultPlacementRole or FoundationPlacementRole)
    protected bool CheckOccupy(Vector3 pos, float rotY, ItemData item, BuildManager manager)
    {
        // safety
        if (item == null || item.worldPrefab == null || manager == null) return true;

        // Try get placement system and valid build layer index
        var placement = manager.placementSystem;
        if (placement == null)
        {
            Debug.LogWarning("[CheckOccupy] placementSystem null on manager - assuming occupied=false");
            return true; // conservative: allow placing (or change to false if you prefer)
        }

        int buildLayerIndex = placement.GetBuildLayerIndex();
        if (buildLayerIndex < 0 || buildLayerIndex > 31)
        {
            Debug.LogWarning($"[CheckOccupy] invalid buildLayerIndex={buildLayerIndex}");
            return true;
        }

        int buildLayerMask = 1 << buildLayerIndex;

        // Instantiate a temporary ghost (hidden)
        GameObject ghost = GameObject.Instantiate(item.worldPrefab);
        ghost.hideFlags = HideFlags.HideInHierarchy;

        // Make renderers invisible and colliders triggers so they don't block physics checks
        foreach (var r in ghost.GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach (var c in ghost.GetComponentsInChildren<Collider>()) c.isTrigger = true;

        ghost.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, rotY, 0f));
        ghost.transform.localScale = new Vector3(manager.cellWidth, manager.cellHeight, manager.cellWidth);

        // Ensure physics transforms are up-to-date before reading bounds
        Physics.SyncTransforms();

        bool occupied = false;

        // Tolerance multiplier: tăng một chút để giảm false-negative (miss)
        const float halfExtentsMultiplier = 0.9f;

        // Use QueryTriggerInteraction.Ignore so ghost's trigger colliders are not returned
        var qtr = QueryTriggerInteraction.Ignore;

        foreach (var col in ghost.GetComponentsInChildren<Collider>())
        {
            // compute world center & half extents
            Vector3 center = col.bounds.center;
            Vector3 halfExtents = col.bounds.extents * halfExtentsMultiplier;

            // debug (uncomment khi cần)
            // Debug.Log($"[CheckOccupy] OverlapBox center={center} half={halfExtents} layerMask={buildLayerMask}");

            // Overlap against build layer only, ignore triggers (so ghost triggers won't show)
            Collider[] hits = Physics.OverlapBox(center, halfExtents, ghost.transform.rotation, buildLayerMask, qtr);

            // If nothing hit, continue to next collider
            if (hits == null || hits.Length == 0) continue;

            foreach (var h in hits)
            {
                // ignore collisions with the ghost itself (safety)
                if (h.transform.root == ghost.transform) continue;

                // try find BuildtObject on the hit
                var existing = h.GetComponentInParent<BuildtObject>();
                if (existing == null) continue;

                // respect ignore list (if configured)
                if (item.building.ignorObject != null && item.building.ignorObject.Contains(existing.objectType))
                    continue;

                // found blocking existing build object
                occupied = true;
                break;
            }

            if (occupied) break;
        }

        // cleanup ghost
        // use DestroyImmediate in editor to remove instantly; Destroy during play is fine
#if UNITY_EDITOR
        GameObject.DestroyImmediate(ghost);
#else
    GameObject.Destroy(ghost);
#endif

        return !occupied;
    }

    protected void ComputeSnappedWithEdgeSupport(
    Vector3 raw,
    float rotY,
    Transform anchor,
    BuildManager manager,
    ItemData obj,
    out Vector3 outPosition,
    out float outRotY)
    {
        outRotY = 0f;
        outPosition = raw;

        if (anchor == null) return;

        Vector3 origin = anchor.position;

        float localX = (raw.x - origin.x) / manager.cellWidth;
        float localZ = (raw.z - origin.z) / manager.cellWidth;
        float localY = (raw.y - origin.y) / manager.cellHeight;

        int ix = Mathf.RoundToInt(localX);
        int iz = Mathf.RoundToInt(localZ);
        int iy = Mathf.RoundToInt(localY);

        Vector3 cellCenter = new Vector3(
            origin.x + ix * manager.cellWidth,
            origin.y + iy * manager.cellHeight,
            origin.z + iz * manager.cellWidth
        );

        // Nếu không snap cạnh → dùng center
        if (!obj.building.snapToGridEdge)
        {
            outPosition = cellCenter;
            outRotY = 0f;

            // apply vertical offset
            int targetLayer = iy + obj.building.verticalOffset;
            outPosition.y = origin.y + targetLayer * manager.cellHeight;
            return;
        }

        // --------------------------
        // SNAP EDGE LOGIC (original)
        // --------------------------
        float fracX = localX - ix;
        float fracZ = localZ - iz;

        if (Mathf.Abs(fracX) > Mathf.Abs(fracZ))
        {
            float signX = Mathf.Sign(fracX);
            float x = origin.x + (ix + 0.5f * signX) * manager.cellWidth;
            float z = origin.z + iz * manager.cellWidth;
            outPosition = new Vector3(x, cellCenter.y, z);
            outRotY = 0f;
        }
        else
        {
            float signZ = Mathf.Sign(fracZ);
            float z = origin.z + (iz + 0.5f * signZ) * manager.cellWidth;
            float x = origin.x + ix * manager.cellWidth;
            outPosition = new Vector3(x, cellCenter.y, z);
            outRotY = 90f;
        }

        // Vertical offset
        int targetLayer2 = iy + obj.building.verticalOffset;
        outPosition.y = origin.y + targetLayer2 * manager.cellHeight;
    }

}
