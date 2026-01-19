using Mirror;
using TMPro;
using UnityEngine;
using static InventoryData;
using static UnityEngine.Rendering.VirtualTexturing.Debugging;

// Quản lý tương tác player với vật phẩm, công trình, cây/đá
public class PlayerInteract : NetworkBehaviour
{

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

        playerHoldingItem = GetComponent<PlayerHoldingItem>();
        if (interactionRayOrigin == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
                interactionRayOrigin = cam.transform;
            else
                Debug.LogError("No Main Camera found! Please tag your camera as MainCamera.");
        }
        // Auto-find GameInput
        if (gameInput == null)
            gameInput = FindObjectOfType<GameInput>();

        if (gameInput != null)
        {
            gameInput.OnInteractStarted += OnInteractStarted;
            gameInput.OnInteractFinished += OnInteractFinished;
            gameInput.OnInteract += OnInteractPressed;
        }
        else
        {
            Debug.LogError("GameInput not found in scene!");
        }


        if (uiManager == null)
            uiManager = FindObjectOfType<UIManager>();

        if (uiManager == null)
            Debug.LogError("UIManager not found in scene!");




        int pickupableLayer = LayerMask.NameToLayer("Pickupable");
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        int mineableLayer = LayerMask.NameToLayer("Mineable");
        int bothLayer = LayerMask.NameToLayer("PickupableAndMineable");

        interactableLayers = (1 << pickupableLayer) | (1 << interactableLayer) |
                             (1 << mineableLayer) | (1 << bothLayer);

        currentRayDistance = interactRay;
    }


    public void UpdatePlayerInteract(float deltaTime)
    {

        if (!isLocalPlayer) return;

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


    // Xử lý nhấn nút tương tác

    private void OnInteractPressed(object sender, System.EventArgs e)
    {
        if (isReadyToPickup) TryPickupCurrentItem();

        if (currentInteractable != null)
        {
            currentInteractable.Interact(playerHoldingItem);
            Debug.Log("Interacting with: " + currentInteractable);
        }
    }

    private void OnInteractStarted(object sender, System.EventArgs e)
    {
        if (isReadyToMine)
            isHoldingInteract = true;

        if (TryGetComponent<PlayerAnimator>(out var animator))
        {
            if (animator.isLocalPlayer)
                animator.CmdSetMining(true);
        }
    }

    private void OnInteractFinished(object sender, System.EventArgs e)
    {
        isHoldingInteract = false;

        if (TryGetComponent<PlayerAnimator>(out var animator))
        {
            if (animator.isLocalPlayer)
                animator.CmdSetMining(false);
        }
    }


    // Raycast để kiểm tra object phía trước

    private void PerformRaycast()
    {
        if (interactionRayOrigin == null || rayStartPoint == null) return;

        Ray camRay = new Ray(interactionRayOrigin.position, interactionRayOrigin.forward);
        Debug.DrawRay(camRay.origin, camRay.direction * cameraRayDistance, Color.red, 0.1f);

        // Set default ray distance
        currentRayDistance = interactRay;

        if (Physics.Raycast(camRay, out RaycastHit camHit, cameraRayDistance, interactableLayers))
        {
            //Debug.Log("<color=yellow>CamRay hit: </color>" + camHit.collider.name);

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


    // Kiểm tra nhặt vật phẩm
    private bool TrySetPickupable(RaycastHit hit)
    {
        if (hit.collider.TryGetComponent(out WorldItemBundle bundle))
        {
            currentPickup = bundle;
            currentInteractable = null;
            isReadyToPickup = true;

            uiManager.ShowInteractUI();
            uiManager.ChangeInteractText(
                "E: Nhặt",
                bundle.GetItemData().itemName,
                bundle.GetItemData().image,
                bundle.count
            );

            return true;
        }
        else if (hit.collider.TryGetComponent(out IPickupAble pickup))
        {
            currentPickup = pickup;
            currentInteractable = null;
            isReadyToPickup = true;
            uiManager.ShowInteractUI();
            uiManager.ChangeInteractText(
                "E: Nhặt",
                null,
                null,
                0
            );
            return true;
        }
        return false;
    }

    // Kiểm tra tương tác object
    private bool TrySetInteractable(RaycastHit hit)
    {
        if (hit.collider.TryGetComponent(out Iinteractable interactable))
        {

            currentInteractable = interactable;
            currentPickup = null;

            rayColor = Color.yellow;

            string text = "Interact";
            if (interactable is IHasCustomText custom)
                text = custom.GetInteractText();

            uiManager.ShowInteractUI();
            uiManager.ChangeInteractText("E: " + text);

            if (interactable is IHasUI hasUI)
                hasUI.ShowUI();

            return true;
        }
        return false;
    }


    // Kiểm tra mining (cây/đá)

    // Replace your TrySetMineable method with this version
    // This fixes the tool detection by getting ItemData from PlayerHoldingItem.ItemData

    private bool TrySetMineable(RaycastHit hit)
    {
        if (!hit.collider.TryGetComponent(out IMinenable minenable)) return false;

        rayColor = Color.red;
        isReadyToMine = false;

        // Get ItemData directly from PlayerHoldingItem
        ItemData heldItemData = null;
        if (playerHoldingItem != null && playerHoldingItem.IsHolding())
        {
            heldItemData = playerHoldingItem.ItemData;

            if (heldItemData == null)
            {
                GameObject heldObject = playerHoldingItem.GetCurrentHeldObject();
                if (heldObject != null && heldObject.TryGetComponent<Item>(out var heldItem))
                    heldItemData = heldItem.itemData;
            }
        }

        // Early return if no tool held
        if (heldItemData == null || heldItemData.type != ItemType.Tool)
            return false;

        ToolType heldTool = heldItemData.tool.toolType;
        ResourceType resourceType = minenable.GetResourceType();

        bool valid = (resourceType == ResourceType.Tree && heldTool == ToolType.Axe) ||
                     (resourceType == ResourceType.Rock && heldTool == ToolType.Pickaxe);

        if (!valid) return false;

        // Tool is correct - enable mining
        isReadyToMine = true;
        currentInteractable = minenable as Iinteractable;

        isTree = resourceType == ResourceType.Tree;
        isRock = resourceType == ResourceType.Rock;

        // **FIX: Display health bar for ALL choppable/mineable objects**
        // Check both base class types
        HealthSystem healthSystem = null;

        // Try NetworkedChoppable first (Log, HalfLog, Stump)
        if (hit.collider.TryGetComponent<NetworkedChoppable>(out var networkedChoppable))
        {
            healthSystem = networkedChoppable.GetHealthSystem();
        }
        // Then try ChoppableBase (Tree)
        else if (hit.collider.TryGetComponent<ChoppableBase>(out var choppableBase))
        {
            healthSystem = choppableBase.GetHealthSystem();
        }
        // Fallback to MyRock for rocks
        else if (isRock && hit.collider.TryGetComponent(out MyRock rock))
        {
            healthSystem = rock.GetHealthSystem();
        }

        // Set the health bar if we found a health system
        if (healthSystem != null)
        {
            uiManager.healthBar.SetTarget(healthSystem);
        }

        uiManager.ShowInteractUI();
        uiManager.ChangeInteractText("E: Mine");

        return true;
    }

    // Nhặt vật phẩm
    private void TryPickupCurrentItem()
    {
        if (currentPickup == null) return;

        if (currentPickup is NetworkBehaviour nb)
        {

            CmdRequestPickup(nb.netIdentity);
            ClearInteractionState();
        }
    }
    [Command]
    private void CmdRequestPickup(NetworkIdentity itemNetId)
    {
        Debug.Log("pickUp Item");
        if (itemNetId == null) return;
        Debug.Log("itemId is null");
        var invData = GetComponentInChildren<InventoryData>();
        if (invData == null) return;

        int itemId = -1;
        int count = 0;

        // Ưu tiên bundle
        if (itemNetId.TryGetComponent<WorldItemBundle>(out var bundle))
        {
            itemId = bundle.GetItemData().id;
            count = bundle.count;
        }
        // Resource / stick / ore...
        else if (itemNetId.TryGetComponent<Item>(out var pickup))
        {
            Debug.Log("Tim duoc item");
            itemId = pickup.itemData.id;
            count = 1;
        }
        // Có thể thêm ELSE IF cho loại khác
        else
        {
            return; // không phải item nhặt được
        }

        if (itemId < 0 || count <= 0)
            return;

        invData.CmdAddItem(itemId, count);
        NetworkServer.Destroy(itemNetId.gameObject);
    }

    // Reset trạng thái tương tác

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

        if (currentInteractable is IHasUI hasUI)
            hasUI.HideUI();

        currentInteractable = null;
    }


    // Getter trạng thái

    public bool IsMining() => isHoldingInteract;
    public bool IsTree() => isTree;
    public bool IsRock() => isRock;
}


