using TMPro;
using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    private InventoryManager inventoryManager;
    private PickupableItem currentItem;            // Vật phẩm có thể nhặt được
    private Iinteractable currentInteractable;     // Đối tượng có thể tương tác



    [SerializeField] private bool isHoldingInteract = false;
    [SerializeField] private bool isReadyToMind = false;


    [Header("Game Input")]
    [SerializeField] private GameInput gameInput;  // Script xử lý input

    [Header("Interaction")]
    [SerializeField] private Transform interactionRayOrigin; // Gốc bắn tia ray 
    [SerializeField] private float interactDistance = 2f;    // Khoảng cách tương tác
    [SerializeField] private ItemHolder itemHolder;          // Vị trí giữ vật phẩm (trên tay)
    [SerializeField] private Transform letterPopupPrefab;    // Prefab popup chữ 
    /* [SerializeField] private TextMeshProUGUI objectText; */    // Text hiển thị tên vật thể
    /*[SerializeField] private GameObject UI;  */                // Giao diện hiện khi trỏ vào vật thể

    private LetterPopup currentLetterPopup;        // Hiện popup chữ cái
    private bool isPointingSomething = false;      // Cờ kiểm tra có đang trỏ vào vật thể không

    private void Start()
    {
        inventoryManager = InventoryManager.instance;
        gameInput.OnInteractStarted += GameInput_OnInteractStarted;
        gameInput.OnInteractFinished += GameInput_OnInteractFinished;
        gameInput.OnInteract += GameInput_OnInteract; // Đăng ký sự kiện nhấn nút tương tác
        /*  HideUI();*/ // Ẩn UI ban đầu
    }

    private void GameInput_OnInteractFinished(object sender, System.EventArgs e)
    {

        isHoldingInteract = false;
    }

    private void GameInput_OnInteractStarted(object sender, System.EventArgs e)
    {
        if (isReadyToMind)
        {
            isHoldingInteract = true;
        }
    }

    private void Update()
    {
        PerformRaycast(); // Gọi raycast mỗi frame để kiểm tra vật thể trước mặt
    }

    // Xử lý khi người chơi nhấn nút tương tác
    private void GameInput_OnInteract(object sender, System.EventArgs e)
    {
        //if (!isPointingSomething) return;

        //if (currentItem != null)
        //{
        //    // Nếu là vật phẩm có thể nhặt và cho vào inventory
        //    if (currentItem.item.carryMode == CarryMode.Inventory)
        //    {
        //        inventoryManager.AddItem(currentItem.item); // Thêm vào kho
        //        Destroy(currentItem.gameObject);            // Xoá khỏi thế giới
        //        ClearTarget(); // Reset lại trạng thái trỏ
        //    }
        //    else
        //    {
        //        Debug.Log("Inventory Full"); // Nếu không thể nhặt
        //    }
        //}
        if (currentInteractable != null)
        {
            // Nếu là đối tượng có thể tương tác
            currentInteractable.Interact();
        }
    }

    // Hàm raycast để kiểm tra vật thể trước mặt
    private void PerformRaycast()
    {
        // Nếu đang cầm vật phẩm => thả ra
        if (itemHolder.HasItem())
        {
            itemHolder.DropCurrentItem();
            return;
        }

        Ray ray = new Ray(interactionRayOrigin.position, interactionRayOrigin.forward);
        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.green, 0.1f);
        // Thực hiện raycast
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {

            // Nếu trúng vật phẩm có thể nhặt
            if (hit.collider.TryGetComponent(out PickupableItem item) && item.canBePicked)
            {
                currentItem = item;
                currentInteractable = null;
                /* SetUI(item.transform, item.name);*/ // Hiện UI và popup
                return;
            }

            // Nếu trúng đối tượng có thể tương tác 
            if (hit.collider.TryGetComponent(out Iinteractable interactable))
            {
                currentItem = null;
                currentInteractable = interactable;
                //SetUI(hit.transform, hit.collider.name);
                return;
            }

            // Nếu trúng đối tượng khai thác được 
            if (hit.collider.TryGetComponent(out IMinenable minenable))
            {
                isReadyToMind = true;
                Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.red, 0.1f);
                //SetUI(hit.transform, hit.collider.name);
                Debug.Log("Trúng đối tượng tài nguyên: " + hit.collider.name);

                switch (minenable.GetResourceType())
                {
                    case ResourceType.Tree:
                        Debug.Log("→ Cây");
                        break;
                    case ResourceType.Rock:
                        Debug.Log("→ Đá");
                        break;

                }
                return;
            }
        }

        // Không trỏ vào gì
        ClearTarget();
    }

    // Hiển thị UI và popup chữ
    //private void SetUI(Transform target, string displayName)
    //{
    //    objectText.text = displayName;
    //    ShowLetterPopup(target);
    //    //ShowUI();
    //    isPointingSomething = true;
    //}

    // Xoá thông tin đối tượng hiện tại và ẩn UI
    private void ClearTarget()
    {
        isReadyToMind = false;
        currentItem = null;
        currentInteractable = null;
        if (!isReadyToMind)
        {
            isHoldingInteract = false;
        }
        isPointingSomething = false;
        //HideLetterPopup();
        //HideUI();
    }

    //private void ShowUI()
    //{
    //    if (UI != null) UI.SetActive(true);
    //}

    //private void HideUI()
    //{
    //    if (UI != null) UI.SetActive(false);
    //}

    // Hiện popup chữ cái 
    //private void ShowLetterPopup(Transform target)
    //{
    //    if (currentLetterPopup != null) return;

    //    Transform popup = Instantiate(letterPopupPrefab, target.position + Vector3.up * 2f, Quaternion.identity);
    //    currentLetterPopup = popup.GetComponent<LetterPopup>();
    //    currentLetterPopup.Setup(target);
    //}

    //// Xoá popup chữ cái
    //private void HideLetterPopup()
    //{
    //    if (currentLetterPopup != null)
    //    {
    //        Destroy(currentLetterPopup.gameObject);
    //        currentLetterPopup = null;
    //    }
    //}

    public bool isMining()
    {
        return isHoldingInteract;
    }
}
