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
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            validDrag = false;
            return;
        }

        validDrag = true;

        originSlot = GetComponentInParent<InventorySlot>();
        parentAfterDrag = transform.parent;
        droppedOnSlot = false;

        Canvas canvas = GetComponentInParent<Canvas>();
        transform.SetParent(canvas.transform, true);
        transform.SetAsLastSibling();

        canvasGroup.blocksRaycasts = false;
        image.raycastTarget = false;

        InventoryManager.instance.isDraggingItem = true;
    }
    public void OnDrag(PointerEventData eventData)
    {
        if (!validDrag) return;
        transform.position = eventData.position;
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!validDrag) return;

        validDrag = false;

        canvasGroup.blocksRaycasts = true;
        image.raycastTarget = true;

        InventoryManager.instance.EndDrag(this);
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
        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        InventorySlot slot = GetComponentInParent<InventorySlot>();
        if (slot == null) return;

        var inv = InventoryManager.instance;

        if (!inv.splitState.active)
            inv.StartSplit(slot.index);
        else if (inv.splitState.sourceSlot == slot.index)
            inv.IncreaseSplit(slot.index);
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
