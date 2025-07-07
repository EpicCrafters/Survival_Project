using TMPro;
using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    private Player player;
    private InventoryManager inventoryManager;

    private IPickupAble currentPickup;               // Vật phẩm có thể nhặt được
    private Iinteractable currentInteractable;       // Đối tượng có thể tương tác (như cửa, NPC)

    [Header("Thiết lập tương tác")]
    [SerializeField] private Transform interactionRayOrigin;     // Gốc bắn ray (vị trí mắt hoặc camera)
    [SerializeField] private float interactDistance = 2f;        // Khoảng cách tương tác
    [SerializeField] private LayerMask interactableLayers;       // Layer để xác định vật thể có thể tương tác

    [Header("Input & UI")]
    [SerializeField] private GameInput gameInput;                // Script xử lý input
    [SerializeField] private HealthBarScreenUI healthBarUI;      // UI thanh máu cho vật thể tương tác (cây, đá)
    /*  [SerializeField] private Transform letterPopupPrefab; */       // Prefab hiển thị popup chữ cái (nếu có)

    //private LetterPopup currentLetterPopup;

    private Color rayColor;

    private bool isRock = false;
    private bool isTree = false;
    private bool isHoldingInteract = false;
    public bool isReadyToMine = false;
    public bool isReadyToPickup = false;

    private void Start()
    {
        rayColor = Color.green;



        inventoryManager = InventoryManager.instance;

        // Đăng ký sự kiện input từ người chơi
        gameInput.OnInteractStarted += OnInteractStarted;
        gameInput.OnInteractFinished += OnInteractFinished;
        gameInput.OnInteract += OnInteractPressed;
    }

    private void Update()
    {
        PerformRaycast(); // Mỗi frame kiểm tra vật thể trước mặt
    }

    // Xử lý khi nhấn nút tương tác 1 lần (click)
    private void OnInteractPressed(object sender, System.EventArgs e)
    {
        TryPickupCurrentItem();
    }

    // Xử lý khi bắt đầu giữ nút tương tác
    private void OnInteractStarted(object sender, System.EventArgs e)
    {
        if (isReadyToMine)
            isHoldingInteract = true;
    }

    // Xử lý khi nhả nút tương tác
    private void OnInteractFinished(object sender, System.EventArgs e)
    {
        isHoldingInteract = false;
    }

    // Hàm bắn ray kiểm tra các vật thể có thể tương tác
    private void PerformRaycast()
    {
        Ray ray = new Ray(interactionRayOrigin.position, interactionRayOrigin.forward);
        Debug.DrawRay(ray.origin, ray.direction * interactDistance, rayColor, 0.1f);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayers))
        {
            if (TrySetPickupable(hit)) return;       // vật phẩm có thể nhặt
            if (TrySetInteractable(hit)) return;     // đối tượng tương tác
            if (TrySetMineable(hit)) return;         // tài nguyên khai thác duoc
        }

        ClearInteractionState(); // Không trúng gì
    }

    // Nếu raycast trúng vật phẩm có thể nhặt
    private bool TrySetPickupable(RaycastHit hit)
    {
        if (hit.collider.TryGetComponent(out IPickupAble pickup))
        {

            rayColor = Color.blue;
            currentPickup = pickup;
            currentInteractable = null;
            isReadyToPickup = true;
            return true;
        }
        return false;
    }

    // Nếu raycast trúng đối tượng có thể tương tác
    private bool TrySetInteractable(RaycastHit hit)
    {
        if (hit.collider.TryGetComponent(out Iinteractable interactable))
        {
            rayColor = Color.yellow;
            currentInteractable = interactable;
            currentPickup = null;
            return true;
        }
        return false;
    }

    // Nếu raycast trúng đối tượng có thể khai thác (cây, đá,...)
    private bool TrySetMineable(RaycastHit hit)
    {
        if (hit.collider.TryGetComponent(out IMinenable minenable))
        {


            rayColor = Color.red;


            isReadyToMine = true;

            // Hiện thanh máu nếu có
            if (hit.collider.TryGetComponent(out MyTree tree))
                healthBarUI.SetTarget(tree.GetHealthSystem());
            else if (hit.collider.TryGetComponent(out MyRock rock))
                healthBarUI.SetTarget(rock.GetHealthSystem());

            // Gán loại tài nguyên (cây hay đá)
            switch (minenable.GetResourceType())
            {
                case ResourceType.Tree:
                    isTree = true;
                    break;
                case ResourceType.Rock:
                    isRock = true;
                    break;
            }

            return true;
        }
        return false;
    }

    // Xử lý logic nhặt vật phẩm
    private void TryPickupCurrentItem()
    {
        if (currentPickup == null) return;

        // Lấy ra script Item từ IPickupAble
        if (currentPickup is MonoBehaviour mb &&
            mb.TryGetComponent<Item>(out var itemComponent))
        {
            bool added = inventoryManager.AddItem(itemComponent.itemData);
            if (added)
            {
                currentPickup.Pickup(); // Gọi hàm Pickup (Destroy object)
                ClearInteractionState();
            }
            else
            {
                Debug.Log("Kho đầy!");
            }
        }
    }

    // Reset toàn bộ trạng thái tương tác
    private void ClearInteractionState()
    {
        isRock = false;
        isTree = false;
        isReadyToMine = false;
        isReadyToPickup = false;
        isHoldingInteract = false;
        currentPickup = null;
        currentInteractable = null;
        rayColor = Color.green;
        healthBarUI.ClearTarget();
    }

    // Các hàm public để kiểm tra trạng thái
    public bool IsMining() => isHoldingInteract;
    public bool IsTree() => isTree;
    public bool IsRock() => isRock;
}
