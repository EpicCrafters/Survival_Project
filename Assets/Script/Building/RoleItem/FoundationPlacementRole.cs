using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable Objects/PlacementRole/Foundation")]
public class FoundationPlacementRole : DefaultPlacementRole
{
    private float extraLift = 0.02f;
    private float maxLift = 1f;

    public override void ComputePreview(
        Vector3 hitPoint,
        float rotY,
        Transform anchor,
        BuildManager manager,
        ItemData item,
        out Vector3 outPos,
        out float outRotY,
        out bool canPlace)
    {
        //Debug.Log($"[{GetType().Name}] ComputePreview CALLED");

        // 1) Nếu có anchor → snap theo anchor (giống ComputeSnappedTransform)
        if (anchor != null)
        {
            ComputeSnappedTransformLikeSystem(hitPoint, rotY, anchor, manager, item, out outPos, out outRotY);
        }
        else
        {
            // Không có anchor → đặt trực tiếp theo hit
            outPos = hitPoint;
            outRotY = rotY;
        }

        // 2) Nếu KHÔNG có anchor → điều chỉnh độ cao (AdjustFoundationHeight)
        if (anchor == null)
            outPos = AdjustFoundationHeight(outPos, outRotY, item, manager);

        // 3) CheckCanPlace
        canPlace = CheckOccupy(outPos, outRotY, item, manager);
    }

    public override void OnServerPlaced(GameObject spawnedObj,
        Transform anchor,
        ItemData item,
        BuildManager manager)
    {
        // Nếu foundation cần xử lý AutoFoundation (spawn pillar)
        // Hệ thống của bạn có AutoFoundation riêng, nên ta không cần gì thêm
        // Nhưng nếu muốn gọi lại, bạn có thể enable component ở đây.
    }


    // ==========================
    // --- COPY LOGIC TỪ SYSTEM ---
    // ==========================

    private void ComputeSnappedTransformLikeSystem(
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
        outRotY = 0f;

        int targetLayer = iy + obj.building.verticalOffset;
        outPos.y = origin.y + targetLayer * manager.cellHeight;
    }


    private Vector3 AdjustFoundationHeight(Vector3 basePos, float rotY, ItemData item, BuildManager manager)
    {
        float half = manager.cellWidth * 0.5f;

        Vector3[] localCorners =
        {
            new Vector3(-half, 0f, -half),
            new Vector3(-half, 0f, +half),
            new Vector3(+half, 0f, -half),
            new Vector3(+half, 0f, +half)
        };

        Quaternion rot = Quaternion.Euler(0f, rotY, 0f);

        float lowestGroundY = float.MaxValue;
        bool foundAny = false;

        foreach (var lc in localCorners)
        {
            Vector3 worldCorner = basePos + rot * lc;

            Vector3 rayStart = worldCorner + Vector3.up * (manager.cellHeight + 0.5f);
            float rayLen = manager.cellHeight + 5f;

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, rayLen, item.building.groundMask))
            {
                lowestGroundY = Mathf.Min(lowestGroundY, hit.point.y);
                foundAny = true;
            }
        }

        if (!foundAny)
            return basePos;

        float halfHeight = manager.cellHeight * 0.3f;
        float foundationBottomY = basePos.y - halfHeight;

        if (foundationBottomY < lowestGroundY)
        {
            float rawLift = lowestGroundY - foundationBottomY;
            float adjusted = rawLift * 0.2f;
            adjusted = Mathf.Clamp(adjusted, 0f, maxLift);
            adjusted += extraLift;
            basePos.y += adjusted;
        }
        else
        {
            basePos.y -= extraLift * 0.5f;
        }

        return basePos;
    }
}
