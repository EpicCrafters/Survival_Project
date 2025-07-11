using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class InventoryItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public ItemData item;

    [Header("UI")]
    public Image image;
    public TextMeshProUGUI countText;

    [SerializeField] public int count = 1;
    [HideInInspector] public Transform parentAfterDrag;


    [Header("Drag")]
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    // STATIC SPLIT STATE
    public bool isSplitClone = false;   // Đánh dấu đây là clone split
    public static GameObject splitClone;
    public static InventoryItem splitFromSlot;
    public static int splitQuantity = 0;
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) //add canvas group
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

    }
    // Khi click chuột phải lên item
    public void OnPointerClick(PointerEventData eventData)
    {
        // Chỉ xử lý khi click chuột phải và còn ít nhất 2 để tách
        if (eventData.button == PointerEventData.InputButton.Right && count > 1)
        {
            // Nếu đây là clone thì không cho tách tiếp
            if (isSplitClone) return;
            // Nếu chưa có clone tách thì tạo mới
            if (splitClone == null)
            {
                // Ghi nhớ slot gốc đang tách
                splitFromSlot = this;
                // Bắt đầu tách 1 đơn vị
                splitQuantity = 1;
                // Trừ slot gốc đi 1
                count--;
                RefreshCount();
                // Tạo clone từ chính object này
                splitClone = Instantiate(gameObject, transform.root);
                
                InventoryItem cloneItem = splitClone.GetComponent<InventoryItem>();

                cloneItem.isSplitClone = true;
                // Trường hợp clone mất sprite do `Awake` thì fix lại:
                cloneItem.item = item;

                cloneItem.image.sprite =item.image;

                cloneItem.count = splitQuantity;        // Gán số lượng clone
                cloneItem.RefreshCount();               // Làm mới text số

                RectTransform cloneRT = splitClone.GetComponent<RectTransform>();
                RectTransform sourceRT = GetComponent<RectTransform>();
                cloneRT.anchorMin = sourceRT.anchorMin;
                cloneRT.anchorMax = sourceRT.anchorMax;
                cloneRT.pivot = sourceRT.pivot;
                cloneRT.sizeDelta = sourceRT.sizeDelta;
                cloneRT.localScale = Vector3.one;

                splitClone.transform.SetAsLastSibling();

                // Làm mờ clone và cho phép raycast xuyên qua
                var cg = splitClone.GetComponent<CanvasGroup>();
                cg.alpha = 0.6f;
                cg.blocksRaycasts = false;
                
                Debug.Log($"[Split] Bắt đầu tách {item.itemName} x{splitQuantity}");
            }
            else if (splitFromSlot == this)
            {
                // Nếu đang tách tiếp cùng slot gốc thì tăng số lượng tách
                if (count > 0)
                {
                    splitQuantity++;  // Tăng số tách
                    count--;          // Slot gốc trừ tiếp
                    RefreshCount();   // Cập nhật slot gốc
                    InventoryItem cloneItem = splitClone.GetComponent<InventoryItem>();
                    cloneItem.count = splitQuantity;    // Update số lượng clone
                    cloneItem.RefreshCount();
                    Debug.Log($"[Split] Tăng số tách lên: {splitQuantity}");
                }
            }
        }
    }


    public void InitialiseItem(ItemData newItem)
    {
        item = newItem;
        image.sprite = newItem.image;
        RefreshCount();
    }

    public void RefreshCount()
    {
        countText.text = count.ToString();
        bool textActive = count > 1;
        countText.gameObject.SetActive(textActive);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (splitClone != null) return; // Không kéo item gốc khi đang tách
        //Debug.Log("Begin drag");
        parentAfterDrag = transform.parent;
        transform.SetParent(transform.root);
        transform.SetAsLastSibling();
        image.raycastTarget = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (splitClone != null) return; // Không kéo item gốc khi đang tách
        //Debug.Log("Dragging");
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (splitClone != null) return; // Không kéo item gốc khi đang tách
        //Debug.Log("End drag");
        transform.SetParent(parentAfterDrag);
        InventoryManager.instance.ChangeHotbarSlot(InventoryManager.instance.IndexSlotBar);
        image.raycastTarget = true;
    }

    private void Update()
    {
        if (splitClone != null)
        {
            // Clone đi theo chuột
            splitClone.transform.position = Input.mousePosition;
            //Debug.Log($"[DEBUG] Mouse: {Input.mousePosition} | Clone: {splitClone.transform.position}");

            // Chuột trái đặt item clone
            if (Input.GetMouseButtonDown(0)) TryPlaceSplit();

            // Chuột phải ngoài slot gốc => huỷ tách
            if (Input.GetMouseButtonDown(1))
            {
                PointerEventData pointerData = new PointerEventData(EventSystem.current);
                pointerData.position = Input.mousePosition;

                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);

                bool hoveringOriginalSlot = false;

                foreach (var result in results)
                {
                    InventoryItem item = result.gameObject.GetComponentInParent<InventoryItem>();
                    if (item == splitFromSlot)
                    {
                        hoveringOriginalSlot = true;
                        break;
                    }
                }

                if (!hoveringOriginalSlot)
                {
                    CancelSplit();
                }
            }
        }
    }
    private void TryPlaceSplit()
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            InventorySlot slot = result.gameObject.GetComponentInParent<InventorySlot>();

            if (slot != null)
            {
                InventoryItem itemInSlot = slot.GetComponentInChildren<InventoryItem>();

                if (itemInSlot == null)
                {
                    // Slot rỗng => Đặt clone vào
                    GameObject newItem = Instantiate(gameObject, slot.transform);
                    InventoryItem newInvItem = newItem.GetComponent<InventoryItem>();
                    newInvItem.InitialiseItem(splitFromSlot.item);
                    newInvItem.count = splitQuantity;
                    newInvItem.RefreshCount();
                }
                else if (itemInSlot.item == splitFromSlot.item && itemInSlot.item.resource.stackable)
                {
                    // Slot cùng loại, stack được => Gộp stack
                    int maxStack = itemInSlot.item.resource.maxStack;
                    int total = itemInSlot.count + splitQuantity;

                    if (total <= maxStack)
                    {
                        itemInSlot.count = total;
                        itemInSlot.RefreshCount();
                    }
                    else
                    {
                        int leftover = total - maxStack;
                        itemInSlot.count = maxStack;
                        itemInSlot.RefreshCount();

                        splitFromSlot.count += leftover; // Dư trả lại slot gốc
                        splitFromSlot.RefreshCount();
                    }
                }
                else
                {
                    Debug.Log("[Split] Slot khác loại, không đặt.");
                    return;
                }

                // Huỷ clone sau khi đặt thành công
                Destroy(splitClone);
                splitClone = null;
                splitFromSlot = null;
                splitQuantity = 0;

                Debug.Log("[Split] Đặt xong");
                return;
            }
        }
    }

    // Huỷ tách trả lại số slot gốc
    private void CancelSplit()
    {
        splitFromSlot.count += splitQuantity; // Trả lại số lượng
        splitFromSlot.RefreshCount();

        Destroy(splitClone);
        splitClone = null;
        splitFromSlot = null;
        splitQuantity = 0;

        Debug.Log("[Split] Hủy tách");
    }
}
