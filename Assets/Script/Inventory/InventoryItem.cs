using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class InventoryItem : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerClickHandler

{
    // ===== DATA (read-only from outside) =====
    public ItemData ItemData { get; private set; }
    private ItemStack boundStack;
    // ===== UI =====
    [SerializeField] private Image image;
    [SerializeField] private TextMeshProUGUI countText;

    // ===== Drag =====
    [HideInInspector] public InventorySlot originSlot;
    [HideInInspector] public bool droppedOnSlot;
    private PointerEventData.InputButton dragButton;

    private CanvasGroup canvasGroup;
    private Transform parentAfterDrag;
    private bool validDrag;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    // -------------------------------------------------
    // UI BIND (BẮT BUỘC)
    // -------------------------------------------------
    public void Bind(ItemStack stack)
    {
        if (stack == null) return;

        boundStack = stack;
        ItemData = stack.data;

        image.sprite = stack.data.image;
        countText.text = stack.count.ToString();
        countText.gameObject.SetActive(stack.count > 1);
    }

    // -------------------------------------------------
    // DRAG
    // -------------------------------------------------
    public void OnBeginDrag(PointerEventData eventData)
    {
        //  CHẶN CHUỘT PHẢI
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            validDrag = false;
            return;
        }

        dragButton = eventData.button;
        validDrag = true;

        originSlot = GetComponentInParent<InventorySlot>();

        Canvas canvas = GetComponentInParent<Canvas>();
        transform.SetParent(canvas.transform, true);
        transform.SetAsLastSibling();

        canvasGroup.blocksRaycasts = false;
        image.raycastTarget = false;
    }
    public void OnDrag(PointerEventData eventData)
    {
        if (!validDrag) return;
        transform.position = eventData.position;
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!validDrag)
            return;

        validDrag = false;
        canvasGroup.blocksRaycasts = true;
        image.raycastTarget = true;

        // Nếu thả ra ngoài inventory drop
        var view = SystemManager.Instance.GetComponentInChildren<InventoryView>();
        if (view != null && !view.IsPointerInsideInventory())
        {
            var input = SystemManager.Instance.GetComponentInChildren<InventoryInput>();
            if (input != null && originSlot != null)
            {
                int fromIndex = originSlot.index;
                int count = GetCount(); // hoặc 1 nếu muốn drop từng cái
                input.RequestDrop(fromIndex, count);
            }
        }

        //  Luôn hủy UI drag cũ
        Destroy(gameObject);
    }

    public void SetAlpha(float a)
    {
        var c = image.color;
        c.a = a;
        image.color = c;
    }
    public void SetGhostVisual()
    {
        if (image != null)
        {
            image.enabled = true;
            image.color = new Color(1f, 1f, 1f, 0.5f);
            image.raycastTarget = false;
        }

        var cg = GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.blocksRaycasts = false;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {      
        InventorySlot slot = GetComponentInParent<InventorySlot>();
        if (slot == null)
        {
            Debug.Log("slot Item NULL");
            return;
        }
        Debug.Log("slot item NO NULL");
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            Debug.Log("RIGHT CLICK DETECTED");

            var sm = SystemManager.Instance;
            Debug.Log($"SystemManager = {sm}");

            var input = sm != null ? sm.GetComponentInChildren<InventoryInput>() : null;
            Debug.Log($"InventoryInput = {input}");

            if (input == null)
            {
                Debug.LogError("InventoryInput NOT FOUND or INACTIVE");
                return;
            }

            input.RequestSplitHalf(slot.index);
        }
    }

    public int GetSlotIndex()
    {
        InventorySlot slot = GetComponentInParent<InventorySlot>();
        return slot != null ? slot.index : -1;
    }
    public int GetCount()
    {
        return boundStack != null ? boundStack.count : 1;
    }

}
