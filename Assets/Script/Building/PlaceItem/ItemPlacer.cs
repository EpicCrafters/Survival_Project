using UnityEngine;

public class ItemPlacer : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Material ghostMaterial;
    
    [SerializeField] private float maxPlaceDistance = 25f;
    

    private GameObject ghostObject;
    private ItemData placingItem;
    private PlayerHoldingItem playerHolding;

    [SerializeField] private LayerMask placeableLayers;

    private bool isPlacing;
    private float currentRotationY = 0f;

    public void StartPlacing(ItemData item, PlayerHoldingItem player)
    {
        placingItem = item;
        playerHolding = player;

        if (placingItem == null || placingItem.worldPrefab == null)
        {
            Debug.LogWarning("[ItemPlacer] No item to place.");
            return;
        }

        ghostObject = Instantiate(placingItem.worldPrefab);
        ApplyGhostMaterial(ghostObject);

        Rigidbody rb = ghostObject.GetComponent<Rigidbody>();
        if (rb == null) rb = ghostObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        foreach (var col in ghostObject.GetComponentsInChildren<Collider>())
        {
            col.enabled = true;
            col.isTrigger = true;
        }

        if (ghostObject.GetComponent<GhostValidator>() == null)
            ghostObject.AddComponent<GhostValidator>();

        currentRotationY = 0f;
        isPlacing = true;
    }

    void Update()
    {
        if (!isPlacing) return;

        UpdateGhostPosition();

        if (Input.GetKeyDown(KeyCode.R))
        {
            currentRotationY += 90f;
            ghostObject.transform.Rotate(Vector3.up, 90f);
        }

        if (Input.GetMouseButtonDown(0))
        {
            PlaceItem();
        }
    }

    void UpdateGhostPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, maxPlaceDistance, placeableLayers))
        {
            if (ghostObject != null && hit.collider.transform.IsChildOf(ghostObject.transform))
                return;

            Vector3 placePos = hit.point;

            float dist = Vector3.Distance(playerHolding.transform.position, placePos);
            if (dist > maxPlaceDistance)
                return;

            Renderer rend = ghostObject.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                float offsetY = rend.bounds.extents.y;
                placePos.y += offsetY;
            }

            ghostObject.transform.position = placePos;
            ghostObject.transform.rotation = Quaternion.Euler(0, currentRotationY, 0);
        }
    }


    void PlaceItem()
    {
        if (ghostObject == null) return;

        GhostValidator validator = ghostObject.GetComponent<GhostValidator>();
        if (validator != null && !validator.IsValid)
        {
            Debug.Log("[PlaceItem] Cannot place: ghost is colliding.");
            return;
        }

        Instantiate(placingItem.worldPrefab, ghostObject.transform.position, ghostObject.transform.rotation);
        Destroy(ghostObject);
        isPlacing = false;
        playerHolding.OnPlaced();
    }

    void ApplyGhostMaterial(GameObject obj)
    {
        foreach (var rend in obj.GetComponentsInChildren<Renderer>())
        {
            Material[] ghostMats = new Material[rend.materials.Length];
            for (int i = 0; i < ghostMats.Length; i++)
                ghostMats[i] = ghostMaterial;
            rend.materials = ghostMats;
        }
    }

    public void CancelPlacing()
    {
        if (ghostObject != null)
            Destroy(ghostObject);

        ghostObject = null;
        isPlacing = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawRay(Camera.main.transform.position, Camera.main.transform.forward * maxPlaceDistance);
    }

}