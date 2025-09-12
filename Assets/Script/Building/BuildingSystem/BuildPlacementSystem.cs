using UnityEngine;

public class BuildPlacementSystem : MonoBehaviour
{
    [SerializeField] private LayerMask buildLayer;
    [SerializeField] private float clusterSnapRadiusMultiplier = 1.25f;
    [SerializeField] private BuildManager buildManager;
    private float rotY; // xoay theo Q/E
    private Transform previewAnchor;

    private void Awake()
    {
        if (buildManager == null) buildManager = GetComponent<BuildManager>();
    }

    public void UpdatePlacement(ItemData item)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 30, item.building.groundMask))
        {
            // Xoay thủ công bằng Q/E
            if (Input.GetKeyDown(KeyCode.Q)) rotY -= 90;
            else if (Input.GetKeyDown(KeyCode.E)) rotY += 90;

            // Anchor gần nhất
            Transform anchor;
            bool hasAnchor = TryFindNearestAnchor(hit.point, out anchor);
            previewAnchor = hasAnchor ? anchor : null;

            // Tính toán snapped position và rotation
            ComputeSnappedTransform(hit.point, item, previewAnchor,
                                    out Vector3 snappedPos, out float computedRotY);

            float finalRotY = computedRotY + rotY;

            // Kiểm tra có thể đặt không
            bool canPlace = CheckCanPlace(snappedPos, finalRotY, item);

            // Cập nhật ghost preview
            buildManager.previewSystem.UpdatePreview(snappedPos, finalRotY, canPlace);

            // Nếu bấm chuột trái thì confirm đặt
            if (Input.GetMouseButtonDown(0) && canPlace)
            {
                buildManager.ConfirmPlacement(snappedPos, finalRotY, item, previewAnchor);
            }
        }
    }

    private void ComputeSnappedTransform(Vector3 rawPos, ItemData obj, Transform anchor,
                                         out Vector3 outPos, out float outRotY)
    {
        outRotY = 0f;
        outPos = rawPos;

        if (anchor == null)
        {
            return; // không có anchor → tự do
        }

        Vector3 origin = anchor.position;

        // Tính theo lưới
        float localX = (rawPos.x - origin.x) / buildManager.cellWidth;
        float localZ = (rawPos.z - origin.z) / buildManager.cellWidth;
        float localY = (rawPos.y - origin.y) / buildManager.cellHeight;

        int ix = Mathf.RoundToInt(localX);
        int iz = Mathf.RoundToInt(localZ);
        int iy = Mathf.RoundToInt(localY);

        Vector3 cellCenter = new Vector3(
            origin.x + ix * buildManager.cellWidth,
            origin.y + iy * buildManager.cellHeight,
            origin.z + iz * buildManager.cellWidth
        );

        float fracX = localX - ix;
        float fracZ = localZ - iz;

        if (obj.building.snapToGridEdge)
        {
            if (Mathf.Abs(fracX) > Mathf.Abs(fracZ))
            {
                float signX = Mathf.Sign(fracX);
                float x = origin.x + (ix + 0.5f * signX) * buildManager.cellWidth;
                float z = origin.z + iz * buildManager.cellWidth;

                outPos = new Vector3(x, origin.y, z);
                outRotY = 0f;
                outPos.x -= signX * 0.35f;
            }
            else
            {
                float signZ = Mathf.Sign(fracZ);
                float z = origin.z + (iz + 0.5f * signZ) * buildManager.cellWidth;
                float x = origin.x + ix * buildManager.cellWidth;

                outPos = new Vector3(x, origin.y, z);
                outRotY = -90f;
                outPos.z -= signZ * 0.35f;
            }
        }
        else
        {
            outPos = cellCenter;
            outRotY = 0f;
        }

        int targetLayer = iy + obj.building.verticalOffset;
        outPos.y = origin.y + targetLayer * buildManager.cellHeight;
    }

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
    public void PlaceObject(Vector3 pos, float rotY, ItemData obj, PlayerHoldingItem playerHolding)
    {
        // Spawn prefab
        GameObject newObj = GameObject.Instantiate(obj.worldPrefab, pos, Quaternion.Euler(0, rotY, 0));
        newObj.transform.localScale = new Vector3(buildManager.cellWidth, buildManager.cellHeight, buildManager.cellWidth);

        // Đặt layer xây dựng (nếu cần)
        int layerIndex = Mathf.FloorToInt(Mathf.Log(obj.building.groundMask.value, 2));
        newObj.layer = layerIndex;
        foreach (Transform child in newObj.transform)
        {
            child.gameObject.layer = layerIndex;
        }

        // Gắn BuildtObject
        BuildtObject buildingObject = newObj.AddComponent<BuildtObject>();
        buildingObject.objectType = obj;

        // Gắn BuildClusterRef (anchor logic giống như bạn có trong code cũ)
        var clusterRef = newObj.AddComponent<BuildClusterRef>();
        clusterRef.anchor = null; // TODO: truyền vào anchor nếu bạn muốn giữ snapping cụm

        // Thông báo lại cho PlayerHoldingItem
        if (playerHolding != null)
        {
            playerHolding.OnPlaced();
        }
    }
}
