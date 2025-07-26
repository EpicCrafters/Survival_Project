using UnityEngine;

public class ItemPlacer : MonoBehaviour
{
    [SerializeField] private Material ghostMaterial;

    private GameObject ghostObject;
    private ItemData placingItem;
    private PlayerHoldingItem playerHolding;

    private bool isPlacing;

    public void StartPlacing(ItemData item, PlayerHoldingItem player)
    {
        placingItem = item;
        playerHolding = player;

        if (placingItem == null || placingItem.worldPrefab == null)
        {
            Debug.LogWarning("No item to place.");
            return;
        }

        ghostObject = Instantiate(placingItem.worldPrefab);
        ApplyGhostMaterial(ghostObject);

        // Tắt collider ghost
        Collider[] colliders = ghostObject.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }

        isPlacing = true;
    }

    void Update()
    {
        if (!isPlacing) return;

        UpdateGhostPosition();
        DebugPlacementRay();
        if (Input.GetMouseButtonDown(0))
        {
            PlaceItem();
        }
    }

    void UpdateGhostPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 50f)) // bỏ LayerMask
        {
            // Bỏ qua chính ghost
            if (ghostObject != null && hit.collider.transform.IsChildOf(ghostObject.transform))
            {
                Debug.Log("[Ghost] Ray hit ghost, skip");
                return;
            }

            
            if (!hit.collider.CompareTag("Finish"))
            {
                Debug.Log($"[Ghost] Hit {hit.collider.name} but not Ground");
                return;
            }

            Vector3 placePos = hit.point;

            // Offset cho prefab nổi lên mặt đất
            Renderer rend = ghostObject.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                float offsetY = rend.bounds.extents.y;
                placePos.y += offsetY;
                Debug.Log($"[Ghost] OffsetY: {offsetY}");
            }

            ghostObject.transform.position = placePos;
            ghostObject.transform.rotation = Quaternion.identity;

            Debug.Log($"[Ghost] Final Pos: {ghostObject.transform.position}");
        }
        else
        {
            Debug.Log("[Ghost] Ray missed any collider");
        }
    }

    void PlaceItem()
    {
        if (ghostObject == null)
        {
            Debug.LogWarning("Ghost is null, cannot place");
            return;
        }

        Vector3 placePos = ghostObject.transform.position;
        Quaternion placeRot = ghostObject.transform.rotation;

        if (placePos == Vector3.zero)
        {
            Debug.LogWarning("Ghost position is zero! Cancel placing.");
            return;
        }

        Instantiate(placingItem.worldPrefab, placePos, placeRot);

        Destroy(ghostObject);
        isPlacing = false;

        Debug.Log($"Placed: {placingItem.itemName} at {placePos}");

        playerHolding.OnPlaced();
    }

    private void DebugPlacementRay()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        Debug.DrawRay(ray.origin, ray.direction * 100f, Color.cyan);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            Debug.Log($"[Ray] Hit: {hit.collider.name} at {hit.point} (Tag: {hit.collider.tag})");
            Debug.DrawLine(ray.origin, hit.point, Color.green);
            Debug.DrawRay(hit.point, Vector3.up * 0.5f, Color.red);
        }
        else
        {
            Debug.Log($"[Ray] Missed");
        }
    }

    void ApplyGhostMaterial(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var rend in renderers)
        {
            rend.material = ghostMaterial;
        }
    }




    //Them
    public void CancelPlacing()
    {
        if (ghostObject != null)
        {
            Destroy(ghostObject);
            ghostObject = null;
        }
        isPlacing = false;
    }

}
