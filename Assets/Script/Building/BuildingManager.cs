using UnityEngine;

public class BuildManager : MonoBehaviour
{
    public static BuildManager Instance { get; private set; }

    private GhostPlacer ghostPlacer;
    private BuildInputHandler inputHandler;

    private ItemData currentItem;
    private PlayerHoldingItem player;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        ghostPlacer = GetComponent<GhostPlacer>();
        inputHandler = GetComponent<BuildInputHandler>();
    }

    public void StartPlacing(ItemData item, PlayerHoldingItem holdingPlayer)
    {
        currentItem = item;
        player = holdingPlayer;

        ghostPlacer.BeginGhost(item);
        inputHandler.EnableInput(true);
    }

    public void ConfirmPlacement(Vector3 pos, Quaternion rot)
    {
        var validator = ghostPlacer.GetValidator();
        if (validator != null && !validator.IsValid)
        {
            Debug.Log("[BuildManager] Không thể đặt: Vị trí không hợp lệ.");
            return;
        }

        // instantiate thực tế
        var placed = Instantiate(currentItem.worldPrefab, pos, rot);
        // set layer tag cho placed object để nó trở thành chướng ngại cho các ghost khác
         SetLayerRecursive(placed, LayerMask.NameToLayer("PlacedBuild"));

        // nếu cần, toggle colliders to non-trigger (thường prefab đã setup sẵn)
        foreach (var c in placed.GetComponentsInChildren<Collider>()) c.isTrigger = false;

        player.OnPlaced();
        StopPlacing();
    }

    /// <summary>
    /// Đổi layer cho object và tất cả con của nó.
    /// </summary>
    public static void SetLayerRecursive(GameObject obj, int newLayer)
    {
        if (newLayer < 0 || newLayer > 31)
        {
            Debug.LogError("Layer index phải nằm trong khoảng [0...31]");
            return;
        }

        obj.layer = newLayer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, newLayer);
        }
    }


    public void StopPlacing()
    {
        inputHandler.EnableInput(false);
        ghostPlacer.ClearGhost();
        currentItem = null;
        player = null;
    }

    public ItemData GetCurrentItem() => currentItem;
}
