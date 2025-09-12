using UnityEngine;

public class BuildManager : MonoBehaviour
{
    [Header("Sub-systems")]
    public BuildPreviewSystem previewSystem;
    public BuildPlacementSystem placementSystem;
    public BuildDestroySystem destroySystem;
    [SerializeField] public float cellHeight = 9.0f;
    [SerializeField] public float cellWidth = 9.0f;
    private ItemData currentItem;
    private PlayerHoldingItem playerHolding;

    private void Awake()
    {
        // Get các subsystem từ Player (hoặc child object)
        if (previewSystem == null) previewSystem = GetComponent<BuildPreviewSystem>();
        if (placementSystem == null) placementSystem = GetComponent<BuildPlacementSystem>();
        if (destroySystem == null) destroySystem = GetComponent<BuildDestroySystem>();
    }

    private void Update()
    {
        if (currentItem == null) return;

        if (currentItem.type == ItemType.Tool)
        {
            destroySystem.UpdateDestroy(currentItem);
        }
        else
        {
            placementSystem.UpdatePlacement(currentItem);
        }
    }

    public void SetCurrentItem(ItemData item, PlayerHoldingItem player)
    {
        currentItem = item;
        playerHolding = player;

        previewSystem.ClearPreview();

        if (item != null && item.type == ItemType.BuildingPart)
        {
            previewSystem.StartPreview(item);
        }
    }

    public void EndVisualisingObject()
    {
        previewSystem.ClearPreview();
    }

    public void ConfirmPlacement(Vector3 pos, float rotY, ItemData obj, Transform previewAnchor)
    {
        placementSystem.PlaceObject(pos, rotY, obj, playerHolding);
    }
}
