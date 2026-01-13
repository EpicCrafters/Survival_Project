using Mirror;
using TMPro;
using UnityEngine;

// Updated PlayerInteract to support NetworkedChoppable health bars
public class PlayerInteract : NetworkBehaviour
{
    private InventoryManager inventoryManager;
    private PlayerHoldingItem playerHoldingItem;

    private IPickupAble currentPickup;
    private Iinteractable currentInteractable;

    [Header("Thiết lập raycast")]
    [SerializeField] private Transform interactionRayOrigin;
    [SerializeField] private Transform rayStartPoint;
    public float rayHeight = 1.0f;
    [SerializeField] private float cameraRayDistance = 5f;
    [SerializeField] private float miningRay = 3f;
    [SerializeField] private float interactRay = 3f;
    [SerializeField] private LayerMask interactableLayers;
    private float currentRayDistance;

    [Header("Input & UI")]
    [SerializeField] private GameInput gameInput;
    [SerializeField] private UIManager uiManager;

    private Color rayColor = Color.green;
    private bool isRock = false;
    private bool isTree = false;
    private bool isHoldingInteract = false;

    public bool isReadyToMine = false;
    public bool isReadyToPickup = false;

    // Cache for current mineable target
    private IMinenable currentMineable;
    private HealthSystem currentHealthSystem;

    private void Start()
    {
        inventoryManager = InventoryManager.instance;
        playerHoldingItem = GetComponent<PlayerHoldingItem>();

        if (interactionRayOrigin == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
                interactionRayOrigin = cam.transform;
            else
                Debug.LogError("No Main Camera found! Please tag your camera as MainCamera.");
        }

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

    public void SetUpGameInput(GameInput gameinput)
    {
        gameInput = gameinput;
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        PerformRaycast();

        // Update health bar for any mineable being targeted
        if (isReadyToMine && currentHealthSystem != null)
        {
            uiManager.healthBar.Update(currentHealthSystem);
        }
    }

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

    private void PerformRaycast()
    {
        if (interactionRayOrigin == null || rayStartPoint == null) return;

        Ray camRay = new Ray(interactionRayOrigin.position, interactionRayOrigin.forward);
        Debug.DrawRay(camRay.origin, camRay.direction * cameraRayDistance, Color.red, 0.1f);

        currentRayDistance = interactRay;

        if (Physics.Raycast(camRay, out RaycastHit camHit, cameraRayDistance, interactableLayers))
        {
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

            if (interactable is CampFire campfire)
                campfire.ShowUI();

            return true;
        }
        return false;
    }

    private bool TrySetMineable(RaycastHit hit)
    {
        if (!hit.collider.TryGetComponent(out IMinenable minenable)) return false;

        rayColor = Color.red;
        isReadyToMine = false;

        ItemData heldItemData = null;

        if (playerHoldingItem != null)
        {
            GameObject heldObject = playerHoldingItem.GetCurrentHeldObject();

            if (heldObject != null)
            {
                if (heldObject.TryGetComponent<ItemHeld>(out var held))
                    heldItemData = held.itemData;
                else if (heldObject.TryGetComponent<Item>(out var item))
                    heldItemData = item.itemData;
            }
            else if (playerHoldingItem.ItemData != null)
            {
                heldItemData = playerHoldingItem.ItemData;
            }
        }

        if (heldItemData == null) return false;
        if (heldItemData.type != ItemType.Tool) return false;

        ToolType heldTool = heldItemData.tool.toolType;
        ResourceType resourceType = minenable.GetResourceType();

        bool valid = (resourceType == ResourceType.Tree && heldTool == ToolType.Axe) ||
                     (resourceType == ResourceType.Rock && heldTool == ToolType.Pickaxe);

        if (!valid) return false;

        isReadyToMine = true;
        isTree = resourceType == ResourceType.Tree;
        isRock = resourceType == ResourceType.Rock;

        currentMineable = minenable;
        currentHealthSystem = null;

        // ===== TRY TO GET HEALTH SYSTEM FROM DIFFERENT TYPES =====

        // 1. Try BaseResource (MyTree, MyRock)
        if (hit.collider.TryGetComponent<BaseResource>(out var baseResource))
        {
            currentHealthSystem = baseResource.GetHealthSystem();
            if (currentHealthSystem != null)
            {
                uiManager.healthBar.SetTarget(currentHealthSystem);
                uiManager.ShowInteractUI();
                uiManager.ChangeInteractText("E: Mine");
                return true;
            }
        }

        // 2. Try NetworkedChoppable (MyLog, MyHalfLog, MyStump)
        if (hit.collider.TryGetComponent<NetworkedChoppable>(out var networkedChoppable))
        {
            currentHealthSystem = networkedChoppable.GetHealthSystem();
            if (currentHealthSystem != null)
            {
                uiManager.healthBar.SetTarget(currentHealthSystem);
                uiManager.ShowInteractUI();
                uiManager.ChangeInteractText("E: Chop");
                return true;
            }
        }

        // 3. Try getting health system directly from parent
        var parentMineable = hit.collider.GetComponentInParent<IMinenable>();
        if (parentMineable != null)
        {
            // Try BaseResource in parent
            if (hit.collider.transform.parent?.TryGetComponent<BaseResource>(out var parentBase) == true)
            {
                currentHealthSystem = parentBase.GetHealthSystem();
            }
            // Try NetworkedChoppable in parent
            else if (hit.collider.transform.parent?.TryGetComponent<NetworkedChoppable>(out var parentNetworked) == true)
            {
                currentHealthSystem = parentNetworked.GetHealthSystem();
            }

            if (currentHealthSystem != null)
            {
                uiManager.healthBar.SetTarget(currentHealthSystem);
                uiManager.ShowInteractUI();
                uiManager.ChangeInteractText("E: Mine");
                return true;
            }
        }

        // Fallback: show mine text without health bar
        uiManager.ShowInteractUI();
        uiManager.ChangeInteractText("E: Mine");
        Debug.LogWarning($"[PlayerInteract] Could not find HealthSystem for {hit.collider.name}");

        return true;
    }

    private void TryPickupCurrentItem()
    {
        if (currentPickup == null) return;

        if (currentPickup is MonoBehaviour mb && mb.TryGetComponent<Item>(out var itemComponent))
        {
            bool added = inventoryManager?.AddItem(itemComponent.itemData) ?? false;
            if (added)
            {
                CmdPickupItem(itemComponent.netIdentity);
                ClearInteractionState();
            }
            else
            {
                Debug.Log("Inventory đầy!");
            }
        }
        else
        {
            Debug.LogWarning("[TryPickupCurrentItem] currentPickup has no Item component.");
        }
    }

    [Command]
    private void CmdPickupItem(NetworkIdentity itemNetId)
    {
        if (itemNetId != null && itemNetId.TryGetComponent<IPickupAble>(out var pickup))
        {
            pickup.Pickup(connectionToClient.identity);
        }
    }

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
        currentMineable = null;
        currentHealthSystem = null;

        if (currentInteractable is CampFire campfire)
            campfire.HideUI();

        currentInteractable = null;
    }

    public bool IsMining() => isHoldingInteract;
    public bool IsTree() => isTree;
    public bool IsRock() => isRock;
}