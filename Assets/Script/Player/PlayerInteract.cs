using TMPro;
using UnityEngine;

// Quản lý tương tác player với vật phẩm, công trình, cây/đá
public class PlayerInteract : MonoBehaviour
{
    private InventoryManager inventoryManager;
    private PlayerHoldingItem playerHoldingItem;

    private IPickupAble currentPickup;
    private Iinteractable currentInteractable;

    [Header("Thiết lập raycast")]
    [SerializeField] private Transform interactionRayOrigin; // Hướng nhìn raycast
    [SerializeField] private Transform rayStartPoint;       // Điểm bắt đầu ray
    public float rayHeight = 1.0f;                           // Chiều cao ray so với player
    [SerializeField] private float cameraRayDistance = 5f;   // Ray từ camera
    [SerializeField] private float miningRay = 3f;          // Khoảng cách mining
    [SerializeField] private float interactRay = 3f;        // Khoảng cách interact
    [SerializeField] private LayerMask interactableLayers;  // Layer các object tương tác
    private float currentRayDistance;

    [Header("Input & UI")]
    [SerializeField] private GameInput gameInput;
    [SerializeField] private UIManager uiManager;

    private Color rayColor = Color.green;                  // Màu ray debug
    private bool isRock = false;
    private bool isTree = false;
    private bool isHoldingInteract = false;

    public bool isReadyToMine = false;
    public bool isReadyToPickup = false;

    private void Start()
    {
        inventoryManager = InventoryManager.instance;
        playerHoldingItem = GetComponent<PlayerHoldingItem>();

        // Đăng ký sự kiện input
        if (gameInput != null)
        {
            gameInput.OnInteractStarted += OnInteractStarted;
            gameInput.OnInteractFinished += OnInteractFinished;
            gameInput.OnInteract += OnInteractPressed;
        }

        // Tạo layer mask
        int pickupableLayer = LayerMask.NameToLayer("Pickupable");
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        int mineableLayer = LayerMask.NameToLayer("Mineable");
        int bothLayer = LayerMask.NameToLayer("PickupableAndMineable");

        interactableLayers = (1 << pickupableLayer) | (1 << interactableLayer) |
                             (1 << mineableLayer) | (1 << bothLayer);
        currentRayDistance = interactRay;
    }

    private void Update()
    {
        PerformRaycast();

        // Cập nhật fill nếu đang mining
        if (isReadyToMine && (isTree || isRock))
        {
            if (currentInteractable is MyTree tree)
                uiManager.healthBar.Update(tree.GetHealthSystem());
            else if (currentInteractable is MyRock rock)
                uiManager.healthBar.Update(rock.GetHealthSystem());
        }
    }

    // -------------------------
    // Xử lý nhấn nút tương tác
    // -------------------------
    private void OnInteractPressed(object sender, System.EventArgs e)
    {
        if (isReadyToPickup) TryPickupCurrentItem();

        if (currentInteractable != null)
        {
            currentInteractable.Interact();
            Debug.Log("Interacting with: " + currentInteractable);
        }
    }

    private void OnInteractStarted(object sender, System.EventArgs e)
    {
        if (isReadyToMine)
            isHoldingInteract = true;
    }

    private void OnInteractFinished(object sender, System.EventArgs e)
    {
        isHoldingInteract = false;
    }

    // -------------------------
    // Raycast để kiểm tra object phía trước
    // -------------------------
    private void PerformRaycast()
    {
        if (interactionRayOrigin == null || rayStartPoint == null) return;

        Ray camRay = new Ray(interactionRayOrigin.position, interactionRayOrigin.forward);
        Debug.DrawRay(camRay.origin, camRay.direction * cameraRayDistance, Color.red, 0.1f);

        // Set default ray distance
        currentRayDistance = interactRay;

        if (Physics.Raycast(camRay, out RaycastHit camHit, cameraRayDistance, interactableLayers))
        {
            // Check if camera hit a mineable object first to set correct ray distance
            if (camHit.collider.TryGetComponent(out IMinenable minenable))
            {
                currentRayDistance = miningRay;
            }

            Vector3 fromPlayer = rayStartPoint.position + Vector3.up * rayHeight;
            Vector3 direction = (camHit.point - fromPlayer).normalized;

            Ray ray = new Ray(fromPlayer, direction);
            Debug.DrawRay(ray.origin, direction * currentRayDistance, rayColor, 0.1f);

            if (Physics.Raycast(ray, out RaycastHit hit, currentRayDistance, interactableLayers))
            {
                if (TrySetMineable(hit)) return;
                if (TrySetPickupable(hit)) return;
                if (TrySetInteractable(hit)) return;
            }
        }

        ClearInteractionState();
    }

    // -------------------------
    // Kiểm tra nhặt vật phẩm
    // -------------------------
    private bool TrySetPickupable(RaycastHit hit)
    {
        if (hit.collider.TryGetComponent(out IPickupAble pickup))
        {
            currentPickup = pickup;
            currentInteractable = null;
            isReadyToPickup = true;

            rayColor = Color.blue;
            uiManager.ShowInteractUI();
            uiManager.ChangeInteractText("E: Pick Up");
            return true;
        }
        return false;
    }

    // -------------------------
    // Kiểm tra tương tác object
    // -------------------------
    private bool TrySetInteractable(RaycastHit hit)
    {
        if (hit.collider.TryGetComponent(out Iinteractable interactable))
        {
            currentInteractable = interactable;
            currentPickup = null;

            rayColor = Color.yellow;
            uiManager.ShowInteractUI();
            uiManager.ChangeInteractText("E: Interact");

            if (interactable is CampFire campfire)
                campfire.ShowUI();

            return true;
        }
        return false;
    }

    // -------------------------
    // Kiểm tra mining (cây/đá)
    // -------------------------
    private bool TrySetMineable(RaycastHit hit)
    {
        if (!hit.collider.TryGetComponent(out IMinenable minenable)) return false;

        rayColor = Color.red;
        isReadyToMine = false;

        ItemData heldItemData = null;
        if (playerHoldingItem != null && playerHoldingItem.IsHolding())
        {
            GameObject heldObject = playerHoldingItem.GetCurrentHeldObject();
            if (heldObject != null && heldObject.TryGetComponent<Item>(out var heldItem))
                heldItemData = heldItem.itemData;
        }

        if (heldItemData == null || heldItemData.type != ItemType.Tool)
            return false;

        ToolType heldTool = heldItemData.tool.toolType;
        ResourceType resourceType = minenable.GetResourceType();

        bool valid = (resourceType == ResourceType.Tree && heldTool == ToolType.Axe) ||
                     (resourceType == ResourceType.Rock && heldTool == ToolType.Pickaxe);
        if (!valid) return false;

        isReadyToMine = true;

        isTree = resourceType == ResourceType.Tree;
        isRock = resourceType == ResourceType.Rock;

        // Hiển thị health bar screen UI
        if (isTree && hit.collider.TryGetComponent(out MyTree tree))
            uiManager.healthBar.SetTarget(tree.GetHealthSystem());
        else if (isRock && hit.collider.TryGetComponent(out MyRock rock))
            uiManager.healthBar.SetTarget(rock.GetHealthSystem());

        uiManager.ShowInteractUI();
        uiManager.ChangeInteractText("E: Mine");

        return true;
    }

    // -------------------------
    // Nhặt vật phẩm
    // -------------------------
    private void TryPickupCurrentItem()
    {
        if (currentPickup == null) return;

        if (currentPickup is MonoBehaviour mb && mb.TryGetComponent<Item>(out var itemComponent))
        {
            bool added = inventoryManager?.AddItem(itemComponent.itemData) ?? false;
            if (added)
            {
                currentPickup.Pickup();
                ClearInteractionState();
            }
            else
            {
                Debug.Log("Inventory đầy!");
            }
        }
    }

    // -------------------------
    // Reset trạng thái tương tác
    // -------------------------
    private void ClearInteractionState()
    {
        uiManager.HideInteractUI();
        uiManager.healthBar.ClearTarget();

        currentRayDistance = interactRay;
        isRock = false;
        isTree = false;
        isReadyToMine = false;
        isReadyToPickup = false;
        isHoldingInteract = false;

        rayColor = Color.green;
        currentPickup = null;

        if (currentInteractable is CampFire campfire)
            campfire.HideUI();

        currentInteractable = null;
    }

    // -------------------------
    // Getter trạng thái
    // -------------------------
    public bool IsMining() => isHoldingInteract;
    public bool IsTree() => isTree;
    public bool IsRock() => isRock;
}