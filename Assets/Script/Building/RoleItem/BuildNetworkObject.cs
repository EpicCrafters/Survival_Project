using Mirror;
using UnityEngine;

public class BuildNetworkObject : NetworkBehaviour
{
    // OnStartClient chạy trên mọi client khi object spawn
    public override void OnStartClient()
    {
        base.OnStartClient();

        // 1) Set đúng layer của BuildObject
        var placement = FindObjectOfType<BuildPlacementSystem>();
        if (placement != null)
        {
            int buildLayer = placement.GetBuildLayerIndex();
            SetLayerRecursively(gameObject, buildLayer);
        }

        // 2) Parent vào BuildHierarchy.Root
        if (BuildHierarchy.Root != null)
            transform.SetParent(BuildHierarchy.Root, true);

        // --------------------------------------------------
        // 3) ĐẢM BẢO LUÔN CÓ BuildtObject
        // --------------------------------------------------
        var bo = GetComponent<BuildtObject>();
        if (bo == null)
            bo = gameObject.AddComponent<BuildtObject>();

        // --------------------------------------------------
        // 4) ĐẢM BẢO LUÔN CÓ BuildClusterRef
        // --------------------------------------------------
        var cref = GetComponent<BuildClusterRef>();
        if (cref == null)
            cref = gameObject.AddComponent<BuildClusterRef>();

        // --------------------------------------------------
        // 5) KHÔI PHỤC ANCHOR: nếu anchor rỗng thì anchor = chính nó
        //    (áp dụng cho Foundation và Floor)
        // --------------------------------------------------
        if (cref.anchor == null)
        {
            var item = bo.objectType;
            if (item != null)
            {
                bool isPlatform =
                    item.building.partType == BuildingPartType.Foundation ||
                    item.building.partType == BuildingPartType.Floor;

                if (isPlatform)
                    cref.anchor = transform;
            }
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