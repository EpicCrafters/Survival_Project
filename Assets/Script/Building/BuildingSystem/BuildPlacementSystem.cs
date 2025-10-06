using UnityEngine;

public class BuildPlacementSystem : MonoBehaviour
{
    [SerializeField] private LayerMask buildLayer;
    [SerializeField] private float clusterSnapRadiusMultiplier = 1.25f;
    private BuildManager buildManager;
    private float rotY; // xoay theo Q/E
    private Transform previewAnchor;

    public void Initialize(BuildManager manager)
    {
        buildManager = manager;
    }

    public void ResetRotation() => rotY = 0f;

    public void UpdatePlacement(ItemData item)
    {
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

            bool canPlace = CheckCanPlace(snappedPos, finalRotY, item);

            buildManager.previewSystem.UpdatePreview(snappedPos, finalRotY, canPlace);

            if (Input.GetMouseButtonDown(0) && canPlace)
            {
                buildManager.ConfirmPlacement(snappedPos, finalRotY, item, previewAnchor);
            }
        }
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

            float offsetIntoCell = 0.3355f; // tỉ lệ ~0.35 khi  điều chỉnh nếu cần
            
            if (Mathf.Abs(fracX) > Mathf.Abs(fracZ))
            {
                float signX = Mathf.Sign(fracX);
                float x = origin.x + (ix + 0.5f * signX) * buildManager.cellWidth;
                float z = origin.z + iz * buildManager.cellWidth;
                outPosition = new Vector3(x, origin.y, z);
                outRotY = 0f;
                //outPosition.x -= signX * offsetIntoCell; // dịch vào tâm ô như cũ
            }
            else
            {
                float signZ = Mathf.Sign(fracZ);
                float z = origin.z + (iz + 0.5f * signZ) * buildManager.cellWidth;
                float x = origin.x + ix * buildManager.cellWidth;
                outPosition = new Vector3(x, origin.y, z);
                outRotY = 90f;
                //outPosition.z -= signZ * offsetIntoCell;
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
                // Công trình cũ chưa có BuildClusterRef: dùng chính transform của nó (sẽ mượt hơn khi đã có anchor)
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
        // Tạo ghost để kiểm tra collider
        GameObject ghost = Instantiate(obj.worldPrefab, pos, Quaternion.Euler(0, rotY, 0));
        ghost.transform.localScale = new Vector3(buildManager.cellWidth, buildManager.cellHeight, buildManager.cellWidth);

        bool isOccupied = false;
        Collider[] colliders = ghost.GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            collider.isTrigger = true;

            RaycastHit[] hits = Physics.BoxCastAll(
                collider.bounds.center,
                collider.bounds.extents * 0.4f,
                Vector3.up,
                Quaternion.identity,
                1,
                buildLayer);

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider != null &&
                    hit.collider.GetComponentInParent<BuildtObject>() != null &&
                    !obj.building.ignorObject.Contains(hit.collider.GetComponentInParent<BuildtObject>().objectType))
                {
                    isOccupied = true;
                    break;
                }
            }
            if (isOccupied) break;
        }

        Destroy(ghost);

        bool isPlatform =
            obj.building.partType == BuildingPartType.Foundation ||
            obj.building.partType == BuildingPartType.Floor;

        bool needAnchor = !isPlatform;
        bool blockedByNoAnchor = needAnchor && previewAnchor == null;

        return !isOccupied && !blockedByNoAnchor;
    }
    public void PlaceObject(Vector3 pos, float rotY, ItemData obj, PlayerHoldingItem playerHolding, Transform anchor)
    {
        GameObject newObj = Instantiate(obj.worldPrefab, pos, Quaternion.Euler(0, rotY, 0), BuildHierarchy.Root);

        newObj.transform.localScale = new Vector3(buildManager.cellWidth, buildManager.cellHeight, buildManager.cellWidth);

        // layer (vẫn giữ cảnh báo: groundMask chỉ 1 bit)
        int layerIndex = Mathf.FloorToInt(Mathf.Log(buildLayer.value, 2));
        newObj.layer = layerIndex;
        foreach (Transform child in newObj.transform) child.gameObject.layer = layerIndex;

        BuildtObject buildingObject = newObj.AddComponent<BuildtObject>();
        buildingObject.objectType = obj;
        // thêm GUID duy nhất khi tạo
        buildingObject.guid = System.Guid.NewGuid().ToString();

        var bsm = BuildingSaveManager.Instance;
        if (bsm != null)
        {
            bsm.AddOrUpdateRecord(buildingObject);
            Debug.Log("[PlaceObject] Đã đăng ký công trình vào BSM: " + buildingObject.guid);
        }

        var clusterRef = newObj.AddComponent<BuildClusterRef>();
        if (anchor != null) clusterRef.anchor = anchor;
        else
        {
            bool isPlatform = obj.building.partType == BuildingPartType.Foundation ||
                              obj.building.partType == BuildingPartType.Floor;
            clusterRef.anchor = isPlatform ? newObj.transform : null;
        }

        // notify save manager
        var buildt = newObj.GetComponent<BuildtObject>();
        if (buildt != null)
        {
            BuildingSaveManager.Instance?.AddOrUpdateRecord(buildt);
        }

        //if (playerHolding != null) playerHolding.OnPlaced();
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
}
public static class BuildHierarchy
{
    private static Transform _root;

    public static Transform Root
    {
        get
        {
            if (_root == null)
            {
                var existing = GameObject.Find("BuildRoot");
                if (existing != null) _root = existing.transform;
                else
                {
                    GameObject go = new GameObject("BuildRoot");
                    _root = go.transform;
                }
            }
            return _root;
        }
    }
}
