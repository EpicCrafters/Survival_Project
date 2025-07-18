using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public class PlayerInteract : MonoBehaviour
{
    private Player player;
    private InventoryManager inventoryManager;
    private PlayerHoldingItem playerHoldingItem;

    private IPickupAble currentPickup;
    private Iinteractable currentInteractable;

    [Header("Thiết lập tương tác")]
    [SerializeField] private Transform interactionRayOrigin;
    [SerializeField] private Transform startPosition;
    [SerializeField] private float rayCastHigh;
    [SerializeField] private float interactDistance = 2f;
    [SerializeField] private LayerMask interactableLayers;

    [Header("Input & UI")]
    [SerializeField] private GameInput gameInput;
    [SerializeField] private HealthBarScreenUI healthBarUI;

    private Color rayColor;
    private bool isRock = false;
    private bool isTree = false;
    private bool isHoldingInteract = false;

    public bool isReadyToMine = false;
    public bool isReadyToPickup = false;

    private void Start()
    {

        //Cursor.lockState = CursorLockMode.Locked;
        rayColor = Color.green;
        inventoryManager = InventoryManager.instance;
        playerHoldingItem = GetComponent<PlayerHoldingItem>();

        gameInput.OnInteractStarted += OnInteractStarted;
        gameInput.OnInteractFinished += OnInteractFinished;
        gameInput.OnInteract += OnInteractPressed;
    }

    private void Update()
    {
        PerformRaycast();
    }

    private void OnInteractPressed(object sender, System.EventArgs e)
    {
        TryPickupCurrentItem();
        if (currentInteractable != null)
        {

            Debug.Log("Interacting with: " + currentInteractable);
            currentInteractable.Interact();
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

    private void PerformRaycast()
    {
        Ray ray = new Ray(startPosition.position+Vector3.up* rayCastHigh, interactionRayOrigin.forward);
        Debug.DrawRay(ray.origin, ray.direction * interactDistance, rayColor, 0.1f);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayers))
        {
            if (TrySetPickupable(hit)) return;  // vật phẩm có thể lấy được
            if (TrySetInteractable(hit)) return; // vật phẩm có thể tương tác được
            if (TrySetMineable(hit)) return;   // vật phẩm có thể đào được
        }

        ClearInteractionState();
    }

    private bool TrySetPickupable(RaycastHit hit)
    {
        if (hit.collider.TryGetComponent(out IPickupAble pickup))
        {
            // Lấy object MonoBehaviour của pickup để so sánh
            MonoBehaviour pickupMB = pickup as MonoBehaviour;

            // Lấy item GameObject mà player đang cầm
            GameObject heldObject = null;
            if (playerHoldingItem != null)
                heldObject = playerHoldingItem.GetCurrentHeldObject();

            // Nếu vật thể trúng ray là đúng món đồ đang cầm thì không nhặt lại
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


    private bool TrySetInteractable(RaycastHit hit)
    {
        if (hit.collider.TryGetComponent(out Iinteractable interactable))
        {
            rayColor = Color.yellow;
            currentInteractable = interactable;
            currentPickup = null;
            if (interactable is CampFire campfire)
            {
                campfire.ShowUI();
                Debug.Log("Show");
            }
            return true;
        }
        return false;
    }

    private bool TrySetMineable(RaycastHit hit)
    {
        if (!hit.collider.TryGetComponent(out IMinenable minenable))
            return false;

        rayColor = Color.gray;
        isReadyToMine = false;

        //Lấy vật phẩm đang cầm trên tay
        ItemData heldItemData = null;
        if (playerHoldingItem != null && playerHoldingItem.IsHolding())
        {
            GameObject heldObject = playerHoldingItem.GetCurrentHeldObject();
            if (heldObject != null && heldObject.TryGetComponent<Item>(out var heldItem))
            {
                heldItemData = heldItem.itemData;
            }
        }

        //Kiểm tra có cầm đúng vật phẩm không
        if (heldItemData == null || heldItemData.type != ItemType.Tool)
        {
            Debug.Log("No valid tool held.");
            return false;
        }

        ToolType heldTool = heldItemData.tool.toolType;
        ResourceType resourceType = minenable.GetResourceType();

        bool valid = false;

        if (resourceType == ResourceType.Tree && heldTool == ToolType.Axe)
            valid = true;
        else if (resourceType == ResourceType.Rock && heldTool == ToolType.Pickaxe)
            valid = true;
       


        if (!valid)
        {
            Debug.Log("Wrong tool for this resource.");
            return false;
        }

        
        rayColor = Color.red;
        isReadyToMine = true;

        // Health UI
        if (hit.collider.TryGetComponent(out MyTree tree))
            healthBarUI.SetTarget(tree.GetHealthSystem());
        else if (hit.collider.TryGetComponent(out MyRock rock))
            healthBarUI.SetTarget(rock.GetHealthSystem());

        
        isTree = resourceType == ResourceType.Tree;
        isRock = resourceType == ResourceType.Rock;

        return true;
    }



    private void TryPickupCurrentItem()
    {
        if (currentPickup == null) return;

        if (currentPickup is MonoBehaviour mb && mb.TryGetComponent<Item>(out var itemComponent))
        {
            bool added = inventoryManager.AddItem(itemComponent.itemData);
            if (added)
            {
                currentPickup.Pickup();
                ClearInteractionState();
            }
            else
            {
                Debug.Log("Inventory is full!");
            }
        }
    }

    private void ClearInteractionState()//Reset trạng thái
    {
        isRock = false;
        isTree = false;
        isReadyToMine = false;
        isReadyToPickup = false;
        isHoldingInteract = false;


        rayColor = Color.green;
        healthBarUI.ClearTarget();


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
