using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
[System.Serializable]
public class StarterItem
{
    public ItemData item;
    public int amount = 1;
}

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager instance;

    public InventoryController controller;
    public InventorySplitState splitState = new InventorySplitState();
    public bool isDraggingItem;
    public bool pendingRedraw;

    [Header("UI Prefabs")]
    public InventoryItem ghostUI;
    private InventoryItem ghostInstance;
    public GameObject inventoryItemPrefab;
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private RectTransform inventoryRoot;

    [Header("Slot Groups")]
    [SerializeField] private InventorySlot[] hotbarSlots;
    [SerializeField] private InventorySlot[] mainInventorySlots;
    [SerializeField] private InventorySlot[] craftingSlots;
    public IReadOnlyList<InventorySlot> CraftingSlots => craftingSlots;


    [Header("Input")]
    [SerializeField] private GameInput gameInput;

    //Replace Add Item Data old
    [Header("Starter Items")]
    [SerializeField] private List<StarterItem> starterItems = new List<StarterItem>();

    private int selectedHotbarIndex = -1;
    public int IndexSlotBar = 0;

    [SerializeField] private PlayerHoldingItem playerHolding;

    // Optional counter map
    private Dictionary<ItemData, int> itemCounts = new Dictionary<ItemData, int>();

    private void Awake()
    {
        instance = this;

        int totalSlots =
        hotbarSlots.Length +
        mainInventorySlots.Length +
        craftingSlots.Length;

        controller = new InventoryController(totalSlots);

    }

    private void Start()
    {
        int idx = 0;

        // Hotbar
        foreach (var slot in hotbarSlots)
            slot.index = idx++;

        // Inventory
        foreach (var slot in mainInventorySlots)
            slot.index = idx++;

        // Crafting
        foreach (var slot in craftingSlots)
            slot.index = idx++;

        foreach (StarterItem starterItem in starterItems)
        {
            if (starterItem.item != null)
            {
                for (int i = 0; i < starterItem.amount; i++)
                {
                    AddItem(starterItem.item);
                }
            }
        }
        ChangeHotbarSlot(IndexSlotBar);
    }

    private void Update()
    {
        UpdateSplitGhost();
    }

    // ---------------------------
    // ADD ITEM
    // ---------------------------
    public bool AddItem(ItemData itemData)
    {
        bool added = controller.AddItem(itemData);

        if (added)
        {
            if (!itemCounts.ContainsKey(itemData)) itemCounts[itemData] = 0;
            itemCounts[itemData]++;

            RedrawUI();
        }
        return added;
    }

    // ---------------------------
    // REMOVE ITEM
    // ---------------------------
    public bool RemoveItem(ItemData itemData, int amount)
    {
        int totalHave = 0;

        for (int i = 0; i < controller.slots.Count; i++)
        {
            var s = controller.slots[i];
            if (s != null && s.data == itemData)
                totalHave += s.count;
        }

        if (totalHave < amount) return false;

        int remain = amount;

        for (int i = 0; i < controller.slots.Count && remain > 0; i++)
        {
            var s = controller.slots[i];
            if (s != null && s.data == itemData)
            {
                int take = Mathf.Min(s.count, remain);
                s.count -= take;
                remain -= take;
                if (s.count <= 0) controller.slots[i] = null;
            }
        }

        if (itemCounts.ContainsKey(itemData))
        {
            itemCounts[itemData] -= amount;
            if (itemCounts[itemData] <= 0)
                itemCounts.Remove(itemData);
        }

        RedrawUI();
        return true;
    }

    // ---------------------------
    // HOTBAR SELECTION
    // ---------------------------
    public void ChangeHotbarSlot(int index, bool force = false)
    {
        if (index < 0 || index >= hotbarSlots.Length)
            return;

        if (selectedHotbarIndex != index)
        {
            if (selectedHotbarIndex >= 0)
                hotbarSlots[selectedHotbarIndex].Deselect();

            hotbarSlots[index].Select();
            selectedHotbarIndex = index;
        }

        InventoryItem itUI = hotbarSlots[index].GetComponentInChildren<InventoryItem>();

        if (playerHolding != null)
        {
            if (itUI != null) playerHolding.HoldingItem(itUI.ItemData);

            else playerHolding.Clear();
        }
    }

    // ---------------------------
    // DRAG & DROP CALLBACK
    // ---------------------------
    public void OnItemDropped(InventorySlot fromSlot, InventorySlot toSlot)
    {
        int from = fromSlot.index;
        int to = toSlot.index;

        bool ok = controller.MoveOrStack(from, to);

        if (ok)
        {
            if (IsCraftingSlot(fromSlot) || IsCraftingSlot(toSlot))
            {
                CraftingManager.Instance.OnCraftingSlotChanged();
            }
            pendingRedraw = true;
        }

    }
    private bool IsCraftingSlot(InventorySlot slot)
    {
        return craftingSlots.Contains(slot);
    }

    // ---------------------------
    // REDRAW UI
    // ---------------------------
    public void RedrawUI()
    {
        if (isDraggingItem) return;

        ClearAllUI();

        int idx = 0;
        foreach (var s in hotbarSlots)
        {
            CreateItemUI(idx, s);
            idx++;
        }
        foreach (var s in mainInventorySlots)
        {
            CreateItemUI(idx, s);
            idx++;
        }
        foreach (var s in craftingSlots)
        {
            CreateItemUI(idx, s);
            idx++;
        }
    }

    private void ClearAllUI()
    {
        foreach (var s in hotbarSlots)
        {
            var items = s.GetComponentsInChildren<InventoryItem>();
            foreach (var it in items)
                Destroy(it.gameObject);
        }

        foreach (var s in mainInventorySlots)
        {
            var items = s.GetComponentsInChildren<InventoryItem>();
            foreach (var it in items)
                Destroy(it.gameObject);
        }
        foreach (var s in craftingSlots)
        {
            var items = s.GetComponentsInChildren<InventoryItem>();
            foreach (var it in items)
                Destroy(it.gameObject);
        }
    }
    private void CreateItemUI(int index, InventorySlot slot)
    {
        ItemStack st = controller.GetSlot(index);
        if (st == null) return;

        GameObject go = Instantiate(inventoryItemPrefab, slot.transform);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localPosition = Vector3.zero;

        InventoryItem ui = go.GetComponent<InventoryItem>();
        ui.Bind(st);
    }

    // ---------------------------
    // SPLIT LOGIC
    // ---------------------------
    public void StartSplit(int slotIndex)
    {
        var s = controller.GetSlot(slotIndex);
        if (s == null || s.count <= 1) return;

        if (controller.Split(slotIndex, 1, out ItemStack newStack))
        {
            splitState.sourceSlot = slotIndex;
            splitState.buffer = newStack;
            RedrawUI();
            SetAllItemRaycast(false);
        }
    }

    public void IncreaseSplit(int slotIndex)
    {
        if (!splitState.active) return;
        if (splitState.sourceSlot != slotIndex) return;

        var slot = controller.GetSlot(slotIndex);
        if (slot == null || slot.count <= 0) return;

        if (controller.Split(slotIndex, 1, out ItemStack more))
        {
            splitState.buffer.count += more.count;

            RedrawUI();
            SetAllItemRaycast(false);
        }
    }
    private void UpdateSplitGhost()
    {
        if (!splitState.active)
        {
            if (ghostInstance != null)
                Destroy(ghostInstance.gameObject);

            return;
        }

        if (ghostUI == null)
        {
            Debug.LogError("ghostUI chưa được gán trong InventoryManager");
            return;
        }

        if (ghostInstance == null)
        {
            ghostInstance = Instantiate(ghostUI, uiCanvas.transform);
            ghostInstance.SetGhostVisual();

            //  FIX SCALE + SIZE
            RectTransform ghostRT = ghostInstance.GetComponent<RectTransform>();

            ghostRT.anchorMin = new Vector2(0.5f, 0.5f);
            ghostRT.anchorMax = new Vector2(0.5f, 0.5f);
            ghostRT.pivot = new Vector2(0.5f, 0.5f);

            ghostRT.sizeDelta = new Vector2(64, 64); // hoặc size slot của bạn
            ghostRT.localScale = Vector3.one;
        }

        if (splitState.buffer == null)
            return;

        ghostInstance.transform.position = Input.mousePosition;
        ghostInstance.Bind(splitState.buffer);
    }
    public void PlaceSplit(int targetSlot)
    {

        if (!splitState.active) return;

        var buffer = splitState.buffer;

        // Stack / place từng phần
        controller.PlaceStackPartial(targetSlot, buffer);

        // Nếu còn dư → trả về inventory
        if (buffer.count > 0)
        {
            controller.AddStack(buffer);
        }

        splitState.Reset();

        if (ghostInstance != null)
            Destroy(ghostInstance.gameObject);

        SetAllItemRaycast(true);
        RedrawUI();

        var slot = GetSlotByIndex(targetSlot);
        if (IsCraftingSlot(slot))
            CraftingManager.Instance.OnCraftingSlotChanged();
    }

    public void CancelSplit()
    {
        if (!splitState.active)
        {
            Debug.Log("khong chay splitStateAtive");
            return;
        }
        Debug.Log("da chay splitActive");
        int src = splitState.sourceSlot;
        ItemStack buffer = splitState.buffer;

        bool returned = false;

        // 1 Ưu tiên trả về slot gốc
        if (src >= 0)
        {
            returned = controller.PlaceStackPartial(src, buffer);
        }

        // 2️ Nếu slot gốc không nhận hết → trả phần còn lại về inventory
        if (buffer.count > 0)
        {
            controller.AddStack(buffer);
        }

        splitState.Reset();

        if (ghostInstance != null)
            Destroy(ghostInstance.gameObject);

        SetAllItemRaycast(true);
        RedrawUI();
    }
    public void EndDrag(InventoryItem item)
    {
        if (item == null) return;

        isDraggingItem = false;

        bool droppedInInventory = IsPointerInsideInventory();

        if (!item.droppedOnSlot && !droppedInInventory)
        {
            //  DROP RA WORLD
            DropItemToWorld(item);
            Destroy(item.gameObject);
            return;
        }

        // --- logic cũ ---
        if (item.originSlot == null)
        {
            Destroy(item.gameObject);
            return;
        }

        if (item.droppedOnSlot)
        {
            if (pendingRedraw)
            {
                pendingRedraw = false;
                RedrawUI();
            }

            Destroy(item.gameObject);
        }
        else
        {
            item.transform.SetParent(item.originSlot.transform, true);
            item.transform.localPosition = Vector3.zero;
        }

        if (pendingRedraw)
        {
            pendingRedraw = false;
            RedrawUI();
        }
    }

    private void SetAllItemRaycast(bool enable)
    {
        int count = 0;

        foreach (var slot in hotbarSlots)
        {
            if (slot == null) continue;

            var items = slot.GetComponentsInChildren<InventoryItem>(true);
            foreach (var it in items)
            {
                var img = it.GetComponentInChildren<Image>(true);
                if (img != null)
                {
                    img.raycastTarget = enable;
                    count++;
                }
            }
        }

        foreach (var slot in mainInventorySlots)
        {
            if (slot == null) continue;

            var items = slot.GetComponentsInChildren<InventoryItem>(true);
            foreach (var it in items)
            {
                var img = it.GetComponentInChildren<Image>(true);
                if (img != null)
                {
                    img.raycastTarget = enable;
                    count++;
                }
            }
        }

        //Debug.Log($"[SetAllItemRaycast] enable={enable}, affected {count} InventoryItem");
    }
    //refund item
    public void OnInventoryClosed()
    {
        Debug.Log("vao dc ham close");
        // Hủy split nếu đang split
        if (splitState.active)
        {
            Debug.Log("hoan tra item");
            CancelSplit();
        }
        // Hủy drag nếu đang drag
        isDraggingItem = false;
    }
    //check inventoryUI
    public bool IsPointerInsideInventory()
    {
        return RectTransformUtility.RectangleContainsScreenPoint(
            inventoryRoot,
            Input.mousePosition,
            uiCanvas.worldCamera
        );
    }
    private void DropItemToWorld(InventoryItem item)
    {
        int fromIndex = item.originSlot.index;
        ItemStack stack = controller.GetSlot(fromIndex);

        if (stack == null) return;

        // Lấy 1 item hoặc cả stack (tùy design)
        int dropCount = 1;

        stack.count -= dropCount;
        if (stack.count <= 0)
            controller.slots[fromIndex] = null;

        // Spawn world item
        SpawnWorldItem(stack.data, dropCount);

        RedrawUI();
    }
    private void SpawnWorldItem(ItemData data, int count)
    {
        if (data.worldPrefab == null) return;

        Vector3 pos = GetPlayerDropPosition();

        GameObject go = Instantiate(data.worldPrefab, pos, Quaternion.identity);

        //var pickup = go.GetComponent<WorldItemPickup>();
        //if (pickup != null)
        //    pickup.Init(data, count);
    }

    private Vector3 GetPlayerDropPosition()
    {
        return Camera.main.transform.position
             + Camera.main.transform.forward * 2f;
    }

    // ---------------------------
    // COUNTER
    // ---------------------------
    public int GetItemCount(ItemData itemData)
    {
        if (itemCounts.TryGetValue(itemData, out int value))
            return value;

        return 0;
    }
    public void SetPlayerHolding(PlayerHoldingItem holding)
    {
        playerHolding = holding;
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
        if (index >= 0 && index < hotbarSlots.Length)
        {
            IndexSlotBar = index;
            ChangeHotbarSlot(IndexSlotBar, true); // force refresh
        }
    }
    // Cuộn qua các ô hotbar bằng chuột
    private void ScrollSlot(int direction)
    {
        if (hotbarSlots == null || hotbarSlots.Length == 0)
            return;

        int newSlot = (selectedHotbarIndex + direction + hotbarSlots.Length) % hotbarSlots.Length;
        IndexSlotBar = newSlot;
        ChangeHotbarSlot(IndexSlotBar);
    }
    public InventorySlot GetSlotByIndex(int index)
    {
        if (index < 0) return null;

        if (index < hotbarSlots.Length)
            return hotbarSlots[index];

        index -= hotbarSlots.Length;

        if (index < mainInventorySlots.Length)
            return mainInventorySlots[index];

        index -= mainInventorySlots.Length;

        if (index < craftingSlots.Length)
            return craftingSlots[index];

        return null;
    }

}
