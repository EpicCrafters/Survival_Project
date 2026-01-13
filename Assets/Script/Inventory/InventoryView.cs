using System.Linq;
using Mirror;
using Unity.VisualScripting;
using UnityEngine;

public class InventoryView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject inventoryItemPrefab;
    [SerializeField] private RectTransform inventoryRoot;
    [SerializeField] private Canvas uiCanvas;
    private InventoryItem splitGhost;

    [Header("Slot Groups")]
    [SerializeField] public InventorySlot[] hotbarSlots;
    [SerializeField] private InventorySlot[] mainSlots;
    [SerializeField] public InventorySlot[] craftingSlots;
    public InventorySlot[] CraftingSlots => craftingSlots;
    public InventorySlot[] InventorySlots =>
        hotbarSlots.Concat(mainSlots).ToArray();

    private InventoryData inventoryData;

    public int TotalSlots =>
        hotbarSlots.Length + mainSlots.Length + craftingSlots.Length;
    private void Start()
    {
        int idx = 0;

        // Hotbar
        foreach (var slot in hotbarSlots)
            slot.index = idx++;

        // Inventory
        foreach (var slot in mainSlots)
            slot.index = idx++;

        // Crafting
        foreach (var slot in craftingSlots)
            slot.index = idx++;
    }
    private void Update()
    {
        if (splitGhost != null)
        {
            splitGhost.transform.position = Input.mousePosition;
        }
    }

    public void Bind(InventoryData data)
    {
        if (inventoryData != null)
            inventoryData.slots.Callback -= OnSlotChanged;

        inventoryData = data;

        if (inventoryData != null)
        {
            inventoryData.OnSplitBufferClient += HandleSplitBuffer;
            inventoryData.slots.Callback += OnSlotChanged;
            RedrawAll();
        }
    }

    private void OnDestroy()
    {
        if (inventoryData != null)
            inventoryData.slots.Callback -= OnSlotChanged;
    }

    private void OnSlotChanged(SyncList<InventoryData.SlotState>.Operation op,
                               int index,
                               InventoryData.SlotState oldItem,
                               InventoryData.SlotState newItem)
    {
        RedrawSlot(index);
        // Nếu slot này là crafting → update recipe
        if (IsCraftingSlot(index))
        {
            CraftingManager.Instance?.OnCraftingSlotChanged();
        }
    }
    bool IsCraftingSlot(int index)
    {
        foreach (var slot in craftingSlots)
        {
            if (slot.index == index)
                return true;
        }
        return false;
    }

    // ================= UI =================

    private void RedrawAll()
    {
        ClearAll();

        for (int i = 0; i < inventoryData.slots.Count; i++)
            RedrawSlot(i);
    }

    private void RedrawSlot(int index)
    {
        InventorySlot slot = GetSlotByIndex(index);
        if (slot == null) return;

        // clear old
        foreach (Transform c in slot.transform)
            Destroy(c.gameObject);

        var data = inventoryData.slots[index];
        if (data.itemId < 0 || data.count <= 0) return;

        GameObject go = Instantiate(inventoryItemPrefab, slot.transform);

        var ui = go.GetComponent<InventoryItem>();
        ui.Bind(new ItemStack(ItemDatabase.Get(data.itemId), data.count));
    }

    private void ClearAll()
    {
        foreach (var s in hotbarSlots)
            ClearSlot(s);
        foreach (var s in mainSlots)
            ClearSlot(s);
        foreach (var s in craftingSlots)
            ClearSlot(s);
    }

    private void ClearSlot(InventorySlot slot)
    {
        foreach (Transform c in slot.transform)
            Destroy(c.gameObject);
    }

    private InventorySlot GetSlotByIndex(int index)
    {
        if (index < hotbarSlots.Length)
            return hotbarSlots[index];

        index -= hotbarSlots.Length;

        if (index < mainSlots.Length)
            return mainSlots[index];

        index -= mainSlots.Length;

        if (index < craftingSlots.Length)
            return craftingSlots[index];

        return null;
    }
    public bool IsPointerInsideInventory()
    {
        //if (inventoryRoot == null || uiCanvas == null)
        //    return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            inventoryRoot,
            Input.mousePosition,
            uiCanvas.worldCamera
        );
    }
    private void HandleSplitBuffer(SplitBuffer buffer)
    {
        // XÓA ghost cũ nếu có
        if (splitGhost != null)
        {
            Destroy(splitGhost.gameObject);
            splitGhost = null;
        }

        if (!buffer.active)
            return;

        // TẠO ghost mới
        GameObject go = Instantiate(inventoryItemPrefab, uiCanvas.transform);
        splitGhost = go.GetComponent<InventoryItem>();

        ItemData data = ItemDatabase.Get(buffer.itemId);
        splitGhost.Bind(new ItemStack(data, buffer.count));
        RectTransform rt = splitGhost.GetComponent<RectTransform>();

        rt.SetParent(uiCanvas.transform, false); // false = không giữ world scale
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        rt.sizeDelta = new Vector2(64, 64); // hoặc size slot của bạn
        rt.localScale = Vector3.one;

        // Cấu hình ghost
        splitGhost.SetGhostVisual(); // bạn đã có sẵn
    }
    public bool HasActiveSplit()
    {
        return splitGhost != null;
    }

}
