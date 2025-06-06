using TMPro;
using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    private PlayerCarryItem playerCarryItem;
    private InventoryManager inventoryManager;

    [Header("Game Input")]
    [SerializeField] private GameInput gameInput; // Input hệ thống (nhận phím tương tác)

    private PickupableItem currentItem; // Item có thể nhặt được mà người chơi đang nhìn vào
    private Ray lastRay; // Tia raycast cuối cùng được bắn ra
    [SerializeField] private Transform letterPopupPrefab; // Prefab popup hiện chữ cái
    private LetterPopup currentLetterPopup;

    [Header("Interaction")]
    [SerializeField] private Transform interactionRayOrigin; // Vị trí bắt đầu raycast (thường là camera)
    [SerializeField] private float interactDistance = 2f; // Khoảng cách tối đa có thể tương tác
    [SerializeField] private ItemHolder itemHolder; // Nơi giữ item (trên tay)
    [SerializeField] private LayerMask interactLayer; // Layer chứa các đối tượng có thể tương tác
    [SerializeField] private TextMeshProUGUI objectText; // Text hiện tên object khi trỏ vào
    private Iinteractable currentInteractable; // Đối tượng tương tác (không phải item)

    [SerializeField] private GameObject UI; // UI hiện lên khi trỏ vào đối tượng

    [SerializeField] private bool isPointingSomething = false; // Có đang trỏ vào đối tượng nào không

    void Start()
    {
        inventoryManager = InventoryManager.instance;
        playerCarryItem = GetComponent<PlayerCarryItem>();
        gameInput.OnInteract += GameInput_OnInteract; // Lắng nghe sự kiện phím tương tác
        Hide(); // Ẩn UI lúc đầu
    }

    // Hàm xử lý khi người chơi nhấn nút tương tác
    private void GameInput_OnInteract(object sender, System.EventArgs e)
    {
        if (isPointingSomething)
        {
            if (currentItem != null)
            {
                if (currentItem.item.carryMode == CarryMode.Inventory)
                {
                    // Nếu là item bỏ vào inventory
                    inventoryManager.AddItem(currentItem.item);
                    Destroy(currentItem.gameObject); // Xoá item khỏi thế giới
                    Hide();
                    currentItem = null;
                }
                else if (currentItem.item.carryMode == CarryMode.Carryable)
                {
                    // Nếu là item có thể cầm trên tay
                    playerCarryItem.Carry(currentItem.gameObject);
                    currentItem = null;
                    Hide();
                }
                else
                {
                    Debug.Log("Inventory Full");
                }
            }
            else if (currentInteractable != null)
            {
                // Nếu đang tương tác với đối tượng khác
                currentInteractable.Interact();
            }
        }
    }

    void Update()
    {
        RaycastDetection(); // Gọi raycast để kiểm tra vật thể trước mặt mỗi frame
    }

    // Kiểm tra vật thể trước mặt bằng raycast
    private void RaycastDetection()
    {
        // Nếu đang giữ vật thì tự động thả ra
        if (itemHolder.HasItem())
        {
            itemHolder.DropCurrentItem();
            return;
        }

        Ray ray = new Ray(interactionRayOrigin.position, interactionRayOrigin.forward);
        lastRay = ray;

        // Kiểm tra tia va chạm với vật thể trong khoảng cách và layer hợp lệ
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactLayer))
        {
            // Kiểm tra xem đó có phải là item có thể nhặt không
            PickupableItem item = hit.collider.GetComponent<PickupableItem>();
            if (item && item.canBePicked)
            {
                currentItem = item;
                currentInteractable = null;
                objectText.text = item.gameObject.name;
                isPointingSomething = true;
                ShowLetterPopup(item.transform); // Hiện chữ trên đầu item
                Show(); // Hiện UI
                Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.green, 1f);
                return;
            }

            // Kiểm tra xem đó có phải là đối tượng có thể tương tác không
            Iinteractable interactable = hit.collider.GetComponent<Iinteractable>();
            if (interactable != null)
            {
                currentItem = null;
                currentInteractable = interactable;
                objectText.text = hit.collider.gameObject.name;
                ShowLetterPopup(hit.transform);
                isPointingSomething = true;
                Show();
                Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.yellow, 1f);
                return;
            }
        }

        // Nếu không trỏ vào gì cả
        HideLetterPopup();
        currentItem = null;
        currentInteractable = null;
        isPointingSomething = false;
        Hide();
        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.red, 1f);
    }

    // Hiện UI
    private void Show()
    {
        UI.SetActive(true);
    }

    // Ẩn UI
    private void Hide()
    {
        UI?.SetActive(false);
    }

    // Hiện popup chữ cái (ví dụ: E)
    private void ShowLetterPopup(Transform target)
    {
        if (currentLetterPopup != null) return;

        Transform popupTransform = Instantiate(letterPopupPrefab, target.position + Vector3.up * 2f, Quaternion.identity);
        currentLetterPopup = popupTransform.GetComponent<LetterPopup>();
        currentLetterPopup.Setup(target);
    }

    // Ẩn popup chữ cái
    private void HideLetterPopup()
    {
        if (currentLetterPopup != null)
        {
            Destroy(currentLetterPopup.gameObject);
            currentLetterPopup = null;
        }
    }
}
