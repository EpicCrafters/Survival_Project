using UnityEngine;

public class GhostPlacer : MonoBehaviour
{
    private GameObject ghost;
    private ItemData currentItem;
    private float currentRotation = 0f;

    [SerializeField] private Vector3 gridSize = new Vector3(1f, 1f, 1f); // allow Y snap step
    [SerializeField] private LayerMask blockingLayers;
    [SerializeField] private float verticalSnapEpsilon = 0.01f;

    private GhostPreview validator;
    private float verticalOffset = 0f; // additional Y offset (for multi-floor)

    public GhostPreview GetValidator() => validator;

    public void BeginGhost(ItemData itemToPlace)
    {
        currentItem = itemToPlace;
        ghost = Instantiate(itemToPlace.worldPrefab);
        ApplyGhostMaterial(ghost, itemToPlace.building.ghostMaterial_Valid);

        // make ghost colliders triggers so it doesn't interact physically while previewing
        foreach (var col in ghost.GetComponentsInChildren<Collider>())
            col.isTrigger = true;

        if (!ghost.TryGetComponent(out validator))
            validator = ghost.AddComponent<GhostPreview>();

        validator.blockingLayers = blockingLayers;
        validator.triggerInteraction = QueryTriggerInteraction.Collide;
    }

    public void ClearGhost()
    {
        if (ghost != null) Destroy(ghost);
        ghost = null;
        validator = null;
        currentItem = null;
        verticalOffset = 0f;
    }

    // NEW: accept RaycastHit (so we know hit.point.normal etc if needed)
    public void UpdateGhost(RaycastHit hit)
    {
        if (ghost == null) return;

        Vector3 basePos = hit.point;
        Vector3 snapped = SnapToGrid(basePos, gridSize);

        ghost.transform.rotation = Quaternion.Euler(0, currentRotation, 0);
        ghost.transform.position = snapped;

        // Calculate bounds and lift so bottom aligns with hit point (avoid sinking)
        Bounds b = CalculateCombinedBounds(ghost);
        float minY = b.min.y;
        float desiredMinY = basePos.y;
        float deltaY = desiredMinY - minY;
        ghost.transform.position += Vector3.up * (deltaY + verticalSnapEpsilon + verticalOffset);

        // revalidate and recolor
        if (validator != null) validator.Revalidate();
        UpdateGhostColor();
    }

    public void Rotate(float angle) => currentRotation += angle;

    public void ChangeVerticalLevel(float delta)
    {
        verticalOffset += delta;
        if (gridSize.y > 0.0001f)
            verticalOffset = Mathf.Round(verticalOffset / gridSize.y) * gridSize.y;
    }

    public Vector3 GetPosition() => ghost != null ? ghost.transform.position : Vector3.zero;
    public Quaternion GetRotation() => ghost != null ? ghost.transform.rotation : Quaternion.identity;

    private void UpdateGhostColor()
    {
        if (validator == null || currentItem == null) return;

        var mats = validator.IsValid ? currentItem.building.ghostMaterial_Valid : currentItem.building.ghostMaterial_Invalid;

        foreach (var rend in ghost.GetComponentsInChildren<Renderer>())
        {
            Material[] matArray = new Material[rend.materials.Length];
            for (int i = 0; i < matArray.Length; i++) matArray[i] = mats;
            rend.materials = matArray;
        }
    }

    private Bounds CalculateCombinedBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }

    private Vector3 SnapToGrid(Vector3 position, Vector3 grid)
    {
        float x = Mathf.Round(position.x / grid.x) * grid.x;
        float y = Mathf.Round(position.y / grid.y) * grid.y;
        float z = Mathf.Round(position.z / grid.z) * grid.z;
        return new Vector3(x, y, z);
    }

    private void ApplyGhostMaterial(GameObject obj, Material mat)
    {
        if (mat == null) return;
        foreach (var rend in obj.GetComponentsInChildren<Renderer>())
        {
            Material[] ghostMats = new Material[rend.materials.Length];
            for (int i = 0; i < ghostMats.Length; i++) ghostMats[i] = mat;
            rend.materials = ghostMats;
        }
    }
}
