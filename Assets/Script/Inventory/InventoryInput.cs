using Mirror;
using UnityEngine;

public class InventoryInput : MonoBehaviour
{

    private InventoryData inventoryData;
    private PlayerHoldingItem holding;
    [SerializeField] private InventorySlot[] hotbarSlotsInput;
    [Header("Input")]
    [SerializeField] private GameInput gameInput;
    private int selectedHotbarIndex = 0;
    //public int IndexSlotBar = 0;
    public int SelectedHotbarIndex => selectedHotbarIndex;

    public void Bind(InventoryData data, PlayerHoldingItem holdingItem)
    {
        inventoryData = data;
        holding = holdingItem;
        hotbarSlotsInput = GetComponent<InventoryView>().hotbarSlots;
        ChangeHotbarSlot(selectedHotbarIndex);
        inventoryData.slots.Callback += OnInventoryChanged;
        //UpdateHolding();
    }
    //==========PlayerHolding=========
    private void UpdateHoldingFromData()
    {
        if (inventoryData == null || holding == null)
            return;

        var slot = inventoryData.GetSlot(selectedHotbarIndex);

        if (slot.itemId < 0 || slot.count <= 0)
        {
            holding.ClearHeldItem();
            return;
        }

        ItemData data = ItemDatabase.Get(slot.itemId);
        holding.RefreshHoldingItem(data, slot.count);
    }
    public void ChangeHotbarSlot(int index, bool force = false)
    {
        if (index < 0 || index >= hotbarSlotsInput.Length)
            return;

        if (selectedHotbarIndex != index || force)
        {
            if (selectedHotbarIndex >= 0)
                hotbarSlotsInput[selectedHotbarIndex].Deselect();

            hotbarSlotsInput[index].Select();
            selectedHotbarIndex = index;

            //  đổi hotbar → hỏi server slot
            UpdateHoldingFromData();
        }
    }
    public void SetGameInput(GameInput input)
    {
        if (gameInput != null)
        {
            gameInput.OnScroll -= HandleScroll;
            gameInput.OnNumberKeyPressed -= HandleNumberKey;
        }

        gameInput = input;

        if (gameInput != null)
        {
            gameInput.OnScroll += HandleScroll;
            gameInput.OnNumberKeyPressed += HandleNumberKey;
        }
    }
    private void HandleScroll(object sender, float scrollValue)
    {
        if (scrollValue > 0)
        {
            ScrollSlot(-1); // Cuộn lên
        }
        else if (scrollValue < 0)
        {
            ScrollSlot(1); // Cuộn xuống
        }
    }

    // Xử lý khi nhấn phím số 1–9 để chọn hotbar
    private void HandleNumberKey(object sender, int index)
    {
        if (index >= 0 && index < hotbarSlotsInput.Length)
        {
            ChangeHotbarSlot(index, true);
        }
    }

    private void ScrollSlot(int direction)
    {
        if (hotbarSlotsInput == null || hotbarSlotsInput.Length == 0)
            return;

        int newSlot = (selectedHotbarIndex + direction + hotbarSlotsInput.Length)
                      % hotbarSlotsInput.Length;

        ChangeHotbarSlot(newSlot);
    }
    private void OnInventoryChanged(
    SyncList<InventoryData.SlotState>.Operation op,
    int index,
    InventoryData.SlotState oldItem,
    InventoryData.SlotState newItem)
    {
        //  CHỈ quan tâm slot đang được chọn
        if (index != selectedHotbarIndex)
            return;

        UpdateHoldingFromData();
    }


    //=====Request==========
    public bool RequestTryAddItem(int id, int amount)
    {
        if (inventoryData == null)
            return false;

        inventoryData.CmdAddItem(id, amount);
        return true;
    }
    public void RequestMove(int from, int to)
    {
        if (inventoryData == null)
            return;

        inventoryData.CmdRequestMove(from, to);
    }
    public void RequestDrop(int fromIndex, int count)
    {
        if (inventoryData == null) return;
        if (count <= 0) return;

        inventoryData.CmdRequestDrop(fromIndex, count);
    }
    public void RequestSplitHalf(int fromSlot)
    {
        Debug.Log("Move to REQUEST SPLIT");
        inventoryData?.CmdRequestSplitHalf(fromSlot);
    }

    public void RequestPlaceSplit(int toSlot)
    {
        inventoryData?.CmdPlaceSplit(toSlot);
    }

    public void RequestCancelSplit()
    {
        inventoryData?.CmdCancelSplit();
    }
    public void RequestConsumeHeldItem(int amount)
    {
        if (inventoryData == null) return;

        inventoryData.CmdConsumeFromSlot(selectedHotbarIndex, amount);
    }
    public void RequestConsumeFromSlot(int slotIndex, int amount)
    {
        if (inventoryData == null)
        {
            Debug.LogWarning("[InventoryInput] InventoryData is null");
            return;
        }

        // TẠM THỜI: client-authoritative
        for (int i = 0; i < amount; i++)
        {
            var slot = inventoryData.GetSlot(slotIndex);
            if (slot.itemId < 0 || slot.count <= 0)
                return;

            slot.count--;
            inventoryData.slots[slotIndex] = slot.count > 0
                ? slot
                : new InventoryData.SlotState { itemId = -1, count = 0 };
        }
    }
}
