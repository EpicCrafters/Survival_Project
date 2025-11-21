// BuildPlacementSystem.cs (Mirror-friendly)
// Keep placement logic local for preview; provide ServerPlaceObject to be called only on server.
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class BuildPlacementSystem : MonoBehaviour
{
    [SerializeField] private LayerMask buildLayer;
    [SerializeField] private float clusterSnapRadiusMultiplier = 1.25f;
    private BuildManager buildManager;
    private float rotY; // xoay theo Q/E
    private Transform previewAnchor;

    // Ghost caching to avoid allocations every frame
    private GameObject ghostCache;
    private GameObject ghostCachePrefab;

    // Tuning: khi nâng foundation lần đầu, thêm một ít để tránh clipping
    [Tooltip("Số mét nâng thêm sau khi đã tính để tránh cắt mặt đất")]
    [SerializeField] private float foundationExtraLift = 0.02f;

    [Tooltip("Giới hạn nâng tối đa")]
    [SerializeField] private float foundationMaxLift = 1.0f;

    public void Initialize(BuildManager manager)
    {
        buildManager = manager;
    }

    public void ResetRotation() => rotY = 0f;

    public void UpdatePlacement(ItemData item)
    {
        if (Camera.main == null) return; // sanity

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 30, item.building.groundMask))
        {
            if (Input.GetKeyDown(KeyCode.Q)) rotY -= 90;
            else if (Input.GetKeyDown(KeyCode.E)) rotY += 90;

            Transform anchor;
            bool hasAnchor = TryFindNearestAnchor(hit.point, out anchor);
            previewAnchor = hasAnchor ? anchor : null;

            ComputeSnappedTransform(hit.point, item, previewAnchor,
                                    out Vector3 snappedPos, out float computedRotY);

            float finalRotY = computedRotY + rotY;

            // --- CHỈNH: nếu là foundation và KHÔNG có anchor thì adjust height trước khi preview ---
            if (item.building.partType == BuildingPartType.Foundation && previewAnchor == null)
            {
                // truyền rotation để tính góc-corners chính xác
                snappedPos = AdjustFoundationHeight(snappedPos, finalRotY, item, foundationExtraLift, foundationMaxLift);
            }

            bool canPlace = CheckCanPlace(snappedPos, finalRotY, item);

            // Now show preview at corrected height
            buildManager.previewSystem.UpdatePreview(snappedPos, finalRotY, canPlace);

            if (Input.GetMouseButtonDown(0) && canPlace)
            {
                // Keep existing signature: ConfirmPlacement in BuildManager will
                // call CmdRequestPlace on client (or directly call ServerPlaceObject on host)
                buildManager.ConfirmPlacement(snappedPos, finalRotY, item, previewAnchor);
            }
        }
    }

    private Vector3 AdjustFoundationHeight(Vector3 basePos, float rotY, ItemData item, float extraLift, float maxLift)
    {
        // compute half extents in world XZ using the cell width
        float half = buildManager.cellWidth * 0.5f;

        // corner offsets in local (before rotation)
        Vector3[] localCorners = new Vector3[]
        {
            new Vector3(-half, 0f, -half),
            new Vector3(-half, 0f, +half),
            new Vector3(+half, 0f, -half),
            new Vector3(+half, 0f, +half)
        };

        // rotation to apply to local corners
        Quaternion rot = Quaternion.Euler(0f, rotY, 0f);

        float lowestGroundY = float.MaxValue;
        bool foundAny = false;

        // raycast down from slightly above each corner and get the lowest hit
        foreach (var lc in localCorners)
        {
            Vector3 worldCorner = basePos + rot * lc;

            // raycast from a bit above corner to ensure hitting slope
            Vector3 rayStart = worldCorner + Vector3.up * (buildManager.cellHeight + 0.5f);
            float rayLen = buildManager.cellHeight + 5f; // reasonable depth

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, rayLen, item.building.groundMask))
            {
                lowestGroundY = Mathf.Min(lowestGroundY, hit.point.y);
                foundAny = true;
            }
            else
            {
                // if no ground under corner, optionally treat as "very low"
                // here we skip it; if *none* corners hit ground, we won't lift.
            }
        }

        if (!foundAny)
        {
            // no ground detected under corners -> return original
            return basePos;
        }


        float foundationHalfHeight = buildManager.cellHeight * 0.3f;
        float foundationBottomY = basePos.y - foundationHalfHeight;

        float neededLift = 0f;
        // only lift when really needed (foundation bottom is under ground)
        if (foundationBottomY < lowestGroundY)
        {
            float rawLift = lowestGroundY - foundationBottomY;

            // giảm độ nhạy nâng: chỉ nâng 30% của mức cần thiết
            float adjustedLift = rawLift * 0.2f;

            // clamp để tránh bay
            adjustedLift = Mathf.Clamp(adjustedLift, 0f, maxLift);

            // thêm uplift nhỏ để tránh clipping
            adjustedLift += extraLift;

            basePos.y += adjustedLift;
        }
        else
        {
            // nếu không cần nâng -> hạ nhẹ xuống để không trông bay
            basePos.y -= extraLift * 0.5f;
        }

        return basePos;
    }

    private void ComputeSnappedTransform(Vector3 rawPosition, ItemData obj, Transform anchor, out Vector3 outPosition, out float outRotY)
    {
        outRotY = 0f;
        outPosition = rawPosition;

        if (anchor == null) return;

        Vector3 origin = anchor.position;

        float localX = (rawPosition.x - origin.x) / buildManager.cellWidth;
        float localZ = (rawPosition.z - origin.z) / buildManager.cellWidth;
        float localY = (rawPosition.y - origin.y) / buildManager.cellHeight;

        int ix = Mathf.RoundToInt(localX);
        int iz = Mathf.RoundToInt(localZ);
        int iy = Mathf.RoundToInt(localY);

        Vector3 cellCenter = new Vector3(origin.x + ix * buildManager.cellWidth,
                                         origin.y + iy * buildManager.cellHeight,
                                         origin.z + iz * buildManager.cellWidth);

        if (obj.building.snapToGridEdge)
        {
            float fracX = localX - ix;
            float fracZ = localZ - iz;

            float offsetIntoCell = 0.3355f; // tỉ lệ ~0.35 khi điều chỉnh nếu cần

            if (Mathf.Abs(fracX) > Mathf.Abs(fracZ))
            {
                float signX = Mathf.Sign(fracX);
                float x = origin.x + (ix + 0.5f * signX) * buildManager.cellWidth;
                float z = origin.z + iz * buildManager.cellWidth;
                outPosition = new Vector3(x, origin.y, z);
                outRotY = 0f;
            }
            else
            {
                float signZ = Mathf.Sign(fracZ);
                float z = origin.z + (iz + 0.5f * signZ) * buildManager.cellWidth;
                float x = origin.x + ix * buildManager.cellWidth;
                outPosition = new Vector3(x, origin.y, z);
                outRotY = 90f;
            }
        }
        else
        {
            outPosition = cellCenter;
            outRotY = 0f;
        }

        int targetLayer = iy + obj.building.verticalOffset;
        outPosition.y = origin.y + targetLayer * buildManager.cellHeight;
    }

    // tìm anchor gần nhất (ưu tiên BuildClusterRef, fallback BuildtObject cũ)
    private bool TryFindNearestAnchor(Vector3 pos, out Transform anchor)
    {
        anchor = null;
        float minSqr = float.MaxValue;

        float radius = buildManager.cellWidth * Mathf.Max(0.1f, clusterSnapRadiusMultiplier);
        Collider[] cols = Physics.OverlapSphere(pos, radius, buildLayer);

        foreach (var c in cols)
        {
            Transform candidate = null;

            var refComp = c.GetComponentInParent<BuildClusterRef>();
            if (refComp != null && refComp.anchor != null)
            {
                candidate = refComp.anchor;
            }
            else
            {
                var old = c.GetComponentInParent<BuildtObject>();
                if (old != null) candidate = old.transform;
            }

            if (candidate == null) continue;

            float d = (candidate.position - pos).sqrMagnitude;
            if (d < minSqr)
            {
                minSqr = d;
                anchor = candidate;
            }
        }

        return anchor != null;
    }

    private bool CheckCanPlace(Vector3 pos, float rotY, ItemData obj)
    {
        if (obj == null || obj.worldPrefab == null) return false;

        // Ensure we have a cached ghost of the same prefab
        if (ghostCache == null || ghostCachePrefab != obj.worldPrefab)
        {
            if (ghostCache != null) Destroy(ghostCache);
            ghostCache = Instantiate(obj.worldPrefab);
            ghostCachePrefab = obj.worldPrefab;

            // Hide renderers and make colliders triggers so they don't interfere
            foreach (var r in ghostCache.GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var c in ghostCache.GetComponentsInChildren<Collider>()) c.isTrigger = true;
        }

        ghostCache.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, rotY, 0));
        ghostCache.transform.localScale = new Vector3(buildManager.cellWidth, buildManager.cellHeight, buildManager.cellWidth);

        bool isOccupied = false;
        Collider[] colliders = ghostCache.GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            // Use OverlapBox with the collider's world bounds to detect any build objects
            Vector3 center = collider.bounds.center;
            Vector3 halfExtents = collider.bounds.extents * 0.4f; // adjust tolerance if needed
            Quaternion orientation = ghostCache.transform.rotation;

            Collider[] hits = Physics.OverlapBox(center, halfExtents, orientation, buildLayer);
            foreach (var hit in hits)
            {
                var existing = hit.GetComponentInParent<BuildtObject>();
                if (existing != null && !obj.building.ignorObject.Contains(existing.objectType))
                {
                    isOccupied = true;
                    break;
                }
            }
            if (isOccupied) break;
        }

        bool isPlatform =
            obj.building.partType == BuildingPartType.Foundation ||
            obj.building.partType == BuildingPartType.Floor;

        bool needAnchor = !isPlatform;
        bool blockedByNoAnchor = needAnchor && previewAnchor == null;

        return !isOccupied && !blockedByNoAnchor;
    }

    // ---------------------------
    // Server-side spawn / destroy
    // These must be called on the server (isServer == true)
    // ---------------------------

    // Called by BuildManager on server (or by Cmd handler)
    public void ServerPlaceObject(Vector3 pos, float rotY, ItemData obj, PlayerHoldingItem playerHolding, Transform anchor, BuildManager callerManager)
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning("[ServerPlaceObject] attempted to call ServerPlaceObject while not on server.");
            return;
        }

        if (obj == null || obj.worldPrefab == null)
        {
            Debug.LogWarning("[ServerPlaceObject] invalid item.");
            return;
        }

        // Instantiate WITHOUT parent (Mirror sẽ không giữ parent nếu bạn spawn với parent)
        GameObject newObj = Instantiate(obj.worldPrefab, pos, Quaternion.Euler(0, rotY, 0));
        // set scale BEFORE spawn (scale sẽ sync qua clients nếu NetworkTransform/transform sync được)
        newObj.transform.localScale = new Vector3(buildManager.cellWidth, buildManager.cellHeight, buildManager.cellWidth);

        // layer - set trên server so clients nhìn đúng khi OnStartClient chạy (client cũng sẽ reset layer trong OnStartClient)
        int layerIndex = GetBuildLayerIndex();
        SetLayerRecursively(newObj, layerIndex);

        // Add BuildtObject and set metadata (server authoritative)
        BuildtObject buildingObject = newObj.GetComponent<BuildtObject>();
        if (buildingObject == null) buildingObject = newObj.AddComponent<BuildtObject>();
        buildingObject.objectType = obj;
        buildingObject.guid = System.Guid.NewGuid().ToString();

        // cluster ref
        var clusterRef = newObj.GetComponent<BuildClusterRef>();
        if (clusterRef == null) clusterRef = newObj.AddComponent<BuildClusterRef>();
        if (anchor != null) clusterRef.anchor = anchor;
        else
        {
            bool isPlatform = obj.building.partType == BuildingPartType.Foundation ||
                              obj.building.partType == BuildingPartType.Floor;
            clusterRef.anchor = isPlatform ? newObj.transform : null;
        }

        // Register in save manager (server-side)
        BuildingSaveManager.Instance?.AddOrUpdateRecord(buildingObject);

        // Finally spawn via Mirror so all clients see it
        NetworkServer.Spawn(newObj);

        // AFTER spawn: set parent on server for server-side scene organization
        // Note: parent is NOT guaranteed to propagate to clients, so client-side script must also parent.
        if (BuildHierarchy.Root != null)
            newObj.transform.SetParent(BuildHierarchy.Root, true);
    }


    public void ServerDestroyBuild(BuildtObject target)
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning("[ServerDestroyBuild] not server.");
            return;
        }
        if (target == null)
        {
            Debug.LogWarning("[ServerDestroyBuild] target null.");
            return;
        }

        // Remove record from save
        BuildingSaveManager.Instance?.RemoveRecord(target.guid);

        // Mirror destroy
        NetworkIdentity nid = target.GetComponent<NetworkIdentity>();
        if (nid != null)
        {
            NetworkServer.Destroy(nid.gameObject);
        }
        else
        {
            // Fallback: destroy normally (won't sync to clients)
            Destroy(target.gameObject);
        }
    }

    public int GetBuildLayerIndex()
    {
        int mask = buildLayer.value;
        for (int i = 0; i < 32; i++)
        {
            if ((mask & (1 << i)) != 0) return i;
        }
        // fallback: layer 0 (Default)
        return 0;
    }

    private void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        foreach (Transform t in go.transform) SetLayerRecursively(t.gameObject, layer);
    }

    private void OnDisable()
    {
        if (ghostCache != null) Destroy(ghostCache);
        ghostCache = null;
        ghostCachePrefab = null;
    }
}
