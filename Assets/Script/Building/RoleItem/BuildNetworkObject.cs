using Mirror;
using UnityEngine;

public class BuildNetworkObject : NetworkBehaviour
{
    public override void OnStartClient()
    {
        base.OnStartClient();

        // 1) Set đúng layer
        var placement = FindObjectOfType<BuildPlacementSystem>();
        if (placement != null)
        {
            int buildLayer = placement.GetBuildLayerIndex();
            SetLayerRecursively(gameObject, buildLayer);
        }

        // 2) Parent vào BuildHierarchy.Root
        if (BuildHierarchy.Root != null)
            transform.SetParent(BuildHierarchy.Root, true);

        // 3) Đảm bảo có BuildtObject
        var bo = GetComponent<BuildtObject>();
        if (bo == null)
            bo = gameObject.AddComponent<BuildtObject>();

        // 4) Đảm bảo có BuildClusterRef
        var cref = GetComponent<BuildClusterRef>();
        if (cref == null)
            cref = gameObject.AddComponent<BuildClusterRef>();

        // ------------------------------------
        // 5) GÁN LẠI ANCHOR ĐÚNG 100%
        //    Tuyệt đối không tin giá trị có sẵn từ prefab.
        // ------------------------------------

        if (bo.objectType != null)
        {
            bool isPlatform =
                bo.objectType.building.partType == BuildingPartType.Foundation ||
                bo.objectType.building.partType == BuildingPartType.Floor;

            if (isPlatform)
            {
                // Bắt buộc anchor = chính object
                cref.anchor = transform;
            }
            else
            {
                // Non-platform không ép lại anchor nếu đã được server gửi xuống đúng
                // Nhưng nếu prefab để sai -> xóa luôn
                if (cref.anchor == null || cref.anchor == transform.root)
                {
                    // Không nên để anchor rác = Player hoặc Prefab Root
                    cref.anchor = null;
                }
            }
        }
        else
        {
            // fallback cực an toàn: nếu không biết loại, gán anchor = chính object
            cref.anchor = transform;
        }
    }

    private void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        foreach (Transform t in go.transform)
            SetLayerRecursively(t.gameObject, layer);
    }
}
