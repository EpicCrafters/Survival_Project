// BuildPlacementSystem.cs (Mirror-friendly)
// Keep placement logic local for preview; provide ServerPlaceObject to be called only on server.
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class BuildPlacementSystem : MonoBehaviour
{
    [SerializeField] public LayerMask buildLayer;
    [SerializeField] private float clusterSnapRadiusMultiplier = 1.25f;
    private BuildManager buildManager;
    private float rotY; // xoay theo Q/E
    private Transform previewAnchor;

    private IBuildPlacementRole defaultRole;
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
    private void Awake()
    {
        defaultRole = ScriptableObject.CreateInstance<DefaultPlacementRole>();
    }

    public void ResetRotation() => rotY = 0f;

    public void UpdatePlacement(ItemData item)
    {
        if (Camera.main == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        IBuildPlacementRole role = GetRole(item);
        //Debug.Log($"Role = {role.GetType().Name} | Item = {item.itemName}");

        if (Physics.Raycast(ray, out RaycastHit hit, 30, item.building.groundMask))
        {
            if (Input.GetKeyDown(KeyCode.Q)) rotY -= 90;
            else if (Input.GetKeyDown(KeyCode.E)) rotY += 90;

            Transform anchor;
            bool hasAnchor = TryFindNearestAnchor(hit.point, out anchor);
            previewAnchor = hasAnchor ? anchor : null;

            // =============================
            //     CALL ROLE PREVIEW
            // =============================
            role.ComputePreview(
                hit.point,
                rotY,
                previewAnchor,
                buildManager,
                item,
                out Vector3 rPos,
                out float rRotY,
                out bool rCanPlace
            );

            // ALWAYS UPDATE PREVIEW HERE
            buildManager.previewSystem.UpdatePreview(rPos, rRotY, rCanPlace);

            if (Input.GetMouseButtonDown(0) && rCanPlace)
            {
                buildManager.ConfirmPlacement(rPos, rRotY, item, previewAnchor);
            }
        }
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
        var role = GetRole(obj);
        // Instantiate WITHOUT parent (Mirror sẽ không giữ parent nếu bạn spawn với parent)
        GameObject newObj = Instantiate(obj.worldPrefab, pos, Quaternion.Euler(0, rotY, 0));
        // set scale BEFORE spawn (scale sẽ sync qua clients nếu NetworkTransform/transform sync được)
        newObj.transform.localScale = new Vector3(buildManager.cellWidth, buildManager.cellHeight, buildManager.cellWidth);

        // layer - set trên server so clients nhìn đúng khi OnStartClient chạy (client cũng sẽ reset layer trong OnStartClient)
        int layerIndex = GetBuildLayerIndex();

        // Nếu object này là cửa → set lại layer Interactable
        if (obj.building.partType == BuildingPartType.Door)
        {
            int interactLayer = LayerMask.NameToLayer("Interactable");
            SetLayerRecursively(newObj, interactLayer);
        }
        else
        {
            SetLayerRecursively(newObj, layerIndex);
        }


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
        // Attempt to find socket by proximity (snap-accurate method)
        DoorFrameSocket nearest = null;
        float bestDist = 0.3f;      // snap tolerance (0.2–0.3m là chuẩn)
        float dist;

        foreach (DoorFrameSocket ds in FindObjectsOfType<DoorFrameSocket>())
        {
            dist = Vector3.Distance(ds.snapPoint.position, pos);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = ds;
            }
        }

        if (nearest != null)
        {
            if (nearest.occupied)
            {
                //Debug.LogWarning("[Server] Socket occupied → destroying door.");
                NetworkServer.Destroy(newObj);
                return;
            }

            nearest.occupied = true;

            newObj.transform.SetParent(nearest.snapPoint);
            newObj.transform.localPosition = Vector3.zero;
            newObj.transform.localRotation = Quaternion.identity;

            //Debug.Log($"[Server] Door snapped to {nearest.name} (distance={bestDist})");
        }
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
    //GetAndSetLayer================================================
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
    //================================================================
    private IBuildPlacementRole GetRole(ItemData item)
    {
        if (item == null || item.building == null || item.building.placementRole == null)
            return defaultRole;

        var role = item.building.placementRole as IBuildPlacementRole;
        if (role == null)
            return defaultRole;

        return role;
    }

}
