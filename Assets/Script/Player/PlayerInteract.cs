using TMPro;
using Unity.Hierarchy;
using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    private Player player;
    private InventoryManager inventoryManager;
    private PlayerHoldingItem playerHoldingItem;

    private IPickupAble currentPickup;
    private Iinteractable currentInteractable;

    [Header("Thiết lập tương tác")]
    [SerializeField] private Transform interactionRayOrigin;   // Hướng của tia tương tác (camera hoặc điểm nhìn)
    [SerializeField] private Transform rayStartPoint;          // Vị trí bắt đầu raycast
    public float rayHeight;                                    // Chiều cao thêm vào ray
    [SerializeField] private float interactDistance = 2f;      // Khoảng cách có thể tương tác
    [SerializeField] private LayerMask interactableLayers;     // Layer các đối tượng có thể tương tác

    [Header("Input & UI")]
    [SerializeField] private GameInput gameInput;
    [SerializeField] private HealthBarScreenUI healthBarUI;

    private Color rayColor;
    private bool isRock = false;               // Có phải đang đào đá không
    private bool isTree = false;               // Có phải đang chặt cây không
    private bool isHoldingInteract = false;    // Người chơi đang giữ nút tương tác

    public bool isReadyToMine = false;         // Sẵn sàng để đào
    public bool isReadyToPickup = false;       // Sẵn sàng để nhặt

    private void Start()
    {
        rayColor = Color.green;
        inventoryManager = InventoryManager.instance;
        playerHoldingItem = GetComponent<PlayerHoldingItem>();

        // Lấy các layer đã gán trong Unity
        int pickupableLayer = LayerMask.NameToLayer("Pickupable");
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        int mineableLayer = LayerMask.NameToLayer("Mineable");
        int bothLayer = LayerMask.NameToLayer("PickupableAndMineable");

        // Kết hợp các layer lại thành một layer mask
        interactableLayers = (1 << pickupableLayer) | (1 << interactableLayer) | (1 << mineableLayer | 1 << bothLayer);

        // Đăng ký sự kiện từ input
        gameInput.OnInteractStarted += OnInteractStarted;
        gameInput.OnInteractFinished += OnInteractFinished;
        gameInput.OnInteract += OnInteractPressed;
    }

    private void Update()
    {
        PerformRaycast(); // Kiểm tra đối tượng phía trước mỗi frame


    }

    // Xử lý khi nhấn giữ nút tương tác
    private void OnInteractPressed(object sender, System.EventArgs e)
    {

        if (isReadyToPickup)
        {
            TryPickupCurrentItem(); // Thử nhặt vật phẩm
        }

        if (currentInteractable != null)
        {
            currentInteractable.Interact();
            Debug.Log("Interacting with: " + currentInteractable);
        }
    }

    // Khi bắt đầu giữ nút tương tác
    private void OnInteractStarted(object sender, System.EventArgs e)
    {
        if (isReadyToMine)
            isHoldingInteract = true;
    }

    // Khi thả nút tương tác
    private void OnInteractFinished(object sender, System.EventArgs e)
    {
        isHoldingInteract = false;
    }

    // Bắn ray để kiểm tra các vật thể phía trước
    private void PerformRaycast()
    {
        //Bắn ray từ camera 
        Ray camRay = new Ray(interactionRayOrigin.position, interactionRayOrigin.forward);
        Debug.DrawRay(camRay.origin, camRay.direction * interactDistance, Color.red, 0.1f);
        if (Physics.Raycast(camRay, out RaycastHit camHit, interactDistance, interactableLayers))
        {

            // Tính vị trí bắt đầu của ray từ player 
            Vector3 fromPlayer = rayStartPoint.position + Vector3.up * rayHeight;

            // Tính hướng từ vị trí player đến điểm mà camera đang nhìn đến
            // Công thức Vector Hướng: w=t-s
            //w: là hướng đi
            //t: là vị trí điểm đến 
            //s: là vị trí băt đầu 
            Vector3 direction = (camHit.point - fromPlayer).normalized;

            // Tạo ray từ player 
            Ray ray = new Ray(fromPlayer, direction);

            // Vẽ ray 
            Debug.DrawRay(ray.origin, direction * interactDistance, rayColor, 0.1f);

            // Kiểm tra xem ray từ player có va vào vật thể nào không
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayers))
            {
                // vật phẩm có thể đào
                if (TrySetMineable(hit)) return;

                // vật phẩm có thể nhặt
                if (TrySetPickupable(hit)) return;

                // vật phẩm có thể tương tác
                if (TrySetInteractable(hit)) return;
            }
        }

        // Nếu không va trúng gì cả reset
        ClearInteractionState();
    }

    // Xử lý vật phẩm có thể nhặt
    private bool TrySetPickupable(RaycastHit hit)
    {
        if (hit.collider.TryGetComponent(out IPickupAble pickup))
        {
            MonoBehaviour pickupMB = pickup as MonoBehaviour;

            GameObject heldObject = null;
            if (playerHoldingItem != null)
                heldObject = playerHoldingItem.GetCurrentHeldObject();

            // Không cho nhặt lại món đồ đang cầm
            if (heldObject != null && pickupMB != null && pickupMB.gameObject == heldObject)
            {
                return false;
            }

            rayColor = Color.blue;
            currentPickup = pickup;
            currentInteractable = null;
            isReadyToPickup = true;
            return true;
        }
        return false;
    }

    // Xử lý vật phẩm có thể tương tác
    private bool TrySetInteractable(RaycastHit hit)
    {
        if (hit.collider.TryGetComponent(out Iinteractable interactable))
        {
            rayColor = Color.yellow;
            currentInteractable = interactable;
            currentPickup = null;

            if (interactable is CampFire campfire)
            {
                campfire.ShowUI(); // Hiển thị UI lửa trại
                //Debug.Log("Show");
            }
            return true;
        }
        return false;
    }

    // Xử lý vật phẩm có thể đào
    private bool TrySetMineable(RaycastHit hit)
    {
        if (!hit.collider.TryGetComponent(out IMinenable minenable))
            return false;

        rayColor = Color.gray;
        isReadyToMine = false;

        // Lấy vật phẩm đang cầm trên tay
        ItemData heldItemData = null;
        if (playerHoldingItem != null && playerHoldingItem.IsHolding())
        {
            GameObject heldObject = playerHoldingItem.GetCurrentHeldObject();
            if (heldObject != null && heldObject.TryGetComponent<Item>(out var heldItem))
            {
                heldItemData = heldItem.itemData;
            }
        }

        // Kiểm tra có cầm công cụ hợp lệ 
        if (heldItemData == null || heldItemData.type != ItemType.Tool)
        {
            //Debug.Log("No tool");
            return false;
        }

        ToolType heldTool = heldItemData.tool.toolType;
        ResourceType resourceType = minenable.GetResourceType();

        bool valid = false;

        if (resourceType == ResourceType.Tree && heldTool == ToolType.Axe)
            valid = true;
        else if (resourceType == ResourceType.Rock && heldTool == ToolType.Pickaxe)
            valid = true;



        rayColor = Color.red;
        isReadyToMine = true;

        // Gán thanh máu cho vật phẩm tương ứng
        if (hit.collider.TryGetComponent(out MyTree tree))
            healthBarUI.SetTarget(tree.GetHealthSystem());
        else if (hit.collider.TryGetComponent(out MyRock rock))
            healthBarUI.SetTarget(rock.GetHealthSystem());

        isTree = resourceType == ResourceType.Tree;
        isRock = resourceType == ResourceType.Rock;

        return true;
    }

    //nhặt vật phẩm 
    private void TryPickupCurrentItem()
    {
        if (currentPickup == null) return;

        if (currentPickup is MonoBehaviour mb && mb.TryGetComponent<Item>(out var itemComponent))
        {
            bool added = inventoryManager.AddItem(itemComponent.itemData);
            if (added)
            {
                currentPickup.Pickup(); // Gọi hàm nhặt
                ClearInteractionState();
            }
            else
            {
                Debug.Log("Inventory is full!");
            }
        }
    }

    // Reset tất cả trạng thái tương tác
    private void ClearInteractionState()
    {
        isRock = false;
        isTree = false;
        isReadyToMine = false;
        isReadyToPickup = false;
        isHoldingInteract = false;

        rayColor = Color.green;
        healthBarUI.ClearTarget();

        // Ẩn UI lửa trại
        if (currentInteractable is CampFire campfire)
        {
            campfire.HideUI();
        }

        currentPickup = null;
        currentInteractable = null;
    }


    public bool IsMining() => isHoldingInteract;
    public bool IsTree() => isTree;
    public bool IsRock() => isRock;
}
