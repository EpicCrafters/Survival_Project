using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager instance;

    public int IndexSlotBar = 0;

    [Header("Starter Items")]
    [SerializeField] private List<StarterItem> starterItems = new List<StarterItem>();

    [Header("UI References")]
    [SerializeField] private InventorySlot[] hotbarSlots;
    [SerializeField] private InventorySlot[] mainInventorySlots;
    [SerializeField] private GameObject inventoryItemPrefab;
    [SerializeField] private GameInput gameInput;

    [Header("Player Reference")]
    [SerializeField] private PlayerHoldingItem playerHolding;

    private Dictionary<ItemData, int> itemCounts = new Dictionary<ItemData, int>();
    private int selectedHotbarIndex = -1;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        ChangeHotbarSlot(IndexSlotBar);

        // Add starter items
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

    public bool AddItem(ItemData itemData)
    {
        if (itemCounts.ContainsKey(itemData))
            itemCounts[itemData]++;
        else
            itemCounts[itemData] = 1;

        if (TryStackItem(itemData, hotbarSlots) || TryStackItem(itemData, mainInventorySlots))
            return true;

        if (TryAddNewItem(itemData, hotbarSlots) || TryAddNewItem(itemData, mainInventorySlots))
            return true;

        return false;
    }

    private bool TryStackItem(ItemData item, InventorySlot[] slots)
    {
        foreach (var slot in slots)
        {
            InventoryItem itemInSlot = slot.GetComponentInChildren<InventoryItem>();
            if (itemInSlot == null || itemInSlot.item != item) continue;

            bool canStack = false;
            int maxStack = 1;

            // Check stackable types in order of priority
            if (item.type == ItemType.Consumable && item.consumable != null)
            {
                // Consumables are always stackable (food, potions, etc.)
                canStack = true;
                maxStack = 99; // Default max stack for consumables
            }
            else if (item.type == ItemType.Resource && item.resource != null && item.resource.stackable)
            {
                canStack = true;
                maxStack = item.resource.maxStack;
            }
            else if (item.type == ItemType.BuildingPart && item.building != null && item.building.stackable)
            {
                canStack = true;
                maxStack = item.building.maxStack;
            }

            if (canStack && itemInSlot.count < maxStack)
            {
                itemInSlot.count++;
                itemInSlot.RefreshCount();
                return true;
            }
        }
        return false;
    }

    private bool TryAddNewItem(ItemData item, InventorySlot[] slots)
    {
        foreach (var slot in slots)
        {
            if (slot.GetComponentInChildren<InventoryItem>() == null)
            {
                SpawnNewItem(item, slot);
                return true;
            }
        }
        return false;
    }

    private void SpawnNewItem(ItemData item, InventorySlot slot)
    {
        GameObject newItemGO = Instantiate(inventoryItemPrefab, slot.transform);
        InventoryItem inventoryItem = newItemGO.GetComponent<InventoryItem>();
        inventoryItem.InitialiseItem(item);

        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            if (slot == hotbarSlots[i] && i == selectedHotbarIndex)
            {
                if (playerHolding != null)
                {
                    playerHolding.HoldingItem(item);
                }
                break;
            }
        }
    }

    public void ChangeHotbarSlot(int index, bool forceRefresh = false)
    {
        if (hotbarSlots == null || index < 0 || index >= hotbarSlots.Length)
            return;

        if (selectedHotbarIndex != index)
        {
            if (selectedHotbarIndex >= 0 && selectedHotbarIndex < hotbarSlots.Length)
                hotbarSlots[selectedHotbarIndex].Deselect();

            hotbarSlots[index].Select();
            selectedHotbarIndex = index;
        }

        InventoryItem selectedItem = hotbarSlots[index].GetComponentInChildren<InventoryItem>();
        if (playerHolding != null)
        {
            if (selectedItem != null)
            {
                playerHolding.HoldingItem(selectedItem.item);
            }
            else
            {
                playerHolding.Clear();
            }
        }
    }

    private void ScrollSlot(int direction)
    {
        if (hotbarSlots == null || hotbarSlots.Length == 0)
            return;

        int newSlot = (selectedHotbarIndex + direction + hotbarSlots.Length) % hotbarSlots.Length;
        IndexSlotBar = newSlot;
        ChangeHotbarSlot(IndexSlotBar);
    }

    private void HandleScroll(object sender, float scrollValue)
    {
        if (scrollValue > 0)
        {
            ScrollSlot(-1);
        }
        else if (scrollValue < 0)
        {
            ScrollSlot(1);
        }
    }

    private void HandleNumberKey(object sender, int index)
    {
        if (index >= 0 && index < hotbarSlots.Length)
        {
            IndexSlotBar = index;
            ChangeHotbarSlot(IndexSlotBar, true);
        }
    }

    public void SortItems()
    {
        List<ItemData> allItems = new List<ItemData>();

        CollectItems(hotbarSlots, allItems);
        CollectItems(mainInventorySlots, allItems);

        ClearSlots(hotbarSlots);
        ClearSlots(mainInventorySlots);

        foreach (var item in allItems)
            AddItem(item);
    }

    private void CollectItems(InventorySlot[] slots, List<ItemData> list)
    {
        foreach (var slot in slots)
        {
            InventoryItem itemInSlot = slot.GetComponentInChildren<InventoryItem>();
            if (itemInSlot != null)
            {
                for (int i = 0; i < itemInSlot.count; i++)
                    list.Add(itemInSlot.item);
                Destroy(itemInSlot.gameObject);
            }
        }
    }

    private void ClearSlots(InventorySlot[] slots)
    {
        foreach (var slot in slots)
        {
            InventoryItem item = slot.GetComponentInChildren<InventoryItem>();
            if (item != null)
                Destroy(item.gameObject);
        }
    }

    public void OnItemDropped(InventorySlot fromSlot, InventorySlot toSlot)
    {
        InventoryItem fromItem = fromSlot.GetComponentInChildren<InventoryItem>();
        InventoryItem toItem = toSlot.GetComponentInChildren<InventoryItem>();

        if (fromItem == null) return;

        if (toItem == null)
        {
            fromItem.transform.SetParent(toSlot.transform);
        }
        else if (fromItem.item == toItem.item && fromItem.item.resource.stackable)
        {
            int transferable = Mathf.Min(fromItem.count, toItem.item.resource.maxStack - toItem.count);
            fromItem.count -= transferable;
            toItem.count += transferable;
            toItem.RefreshCount();

            if (fromItem.count <= 0)
                Destroy(fromItem.gameObject);
            else
                fromItem.RefreshCount();
        }
        else
        {
            Transform temp = toItem.transform;
            toItem.transform.SetParent(fromSlot.transform);
            fromItem.transform.SetParent(toSlot.transform);
        }
    }

    public void SpawnSplitItem(ItemData item, int amount)
    {
        if (!TrySpawn(item, amount, mainInventorySlots))
            TrySpawn(item, amount, hotbarSlots);
    }

    public ItemData GetSelectedItem(bool fromInventory)
    {
        InventorySlot[] source = fromInventory ? mainInventorySlots : hotbarSlots;
        int index = fromInventory ? 0 : selectedHotbarIndex;

        if (index >= 0 && index < source.Length)
        {
            InventoryItem itemUI = source[index].GetComponentInChildren<InventoryItem>();
            return itemUI != null ? itemUI.item : null;
        }

        return null;
    }

    private bool TrySpawn(ItemData item, int amount, InventorySlot[] slots)
    {
        foreach (var slot in slots)
        {
            if (slot.GetComponentInChildren<InventoryItem>() == null)
            {
                GameObject newItemGO = Instantiate(inventoryItemPrefab, slot.transform);
                InventoryItem inventoryItem = newItemGO.GetComponent<InventoryItem>();
                inventoryItem.InitialiseItem(item);
                inventoryItem.count = amount;
                inventoryItem.RefreshCount();
                return true;
            }
        }
        return false;
    }

    public bool RemoveItem(ItemData itemData, int amount)
    {
        if (!itemCounts.ContainsKey(itemData) || itemCounts[itemData] < amount)
            return false;

        itemCounts[itemData] -= amount;
        if (itemCounts[itemData] <= 0)
            itemCounts.Remove(itemData);

        UpdateInventoryUIAfterRemove(itemData, amount);
        return true;
    }

    // Add this method to your InventoryManager class
    // Replace the existing UpdateInventoryUIAfterRemove method with this one

    private void UpdateInventoryUIAfterRemove(ItemData itemData, int amount)
    {
        int remaining = amount;

        // ✅ PRIORITY 1: Remove from the CURRENTLY SELECTED HOTBAR SLOT first
        if (selectedHotbarIndex >= 0 && selectedHotbarIndex < hotbarSlots.Length)
        {
            InventorySlot selectedSlot = hotbarSlots[selectedHotbarIndex];
            InventoryItem itemUI = selectedSlot.GetComponentInChildren<InventoryItem>();

            if (itemUI != null && itemUI.item == itemData)
            {
                int removeCount = Mathf.Min(itemUI.count, remaining);
                itemUI.count -= removeCount;
                remaining -= removeCount;

                if (itemUI.count <= 0)
                {
                    Destroy(itemUI.gameObject);
                    if (playerHolding != null)
                    {
                        playerHolding.Clear();
                    }
                }
                else
                {
                    itemUI.RefreshCount();
                    // Update held item visual to show new count
                    if (playerHolding != null)
                    {
                        playerHolding.RefreshHoldingItem(itemData, itemUI.count);
                    }
                }

                if (remaining <= 0)
                    return; // Done removing
            }
        }

        // ✅ PRIORITY 2: Remove from other hotbar slots (if still needed)
        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            if (i == selectedHotbarIndex) continue; // Skip selected slot, already handled

            InventoryItem itemUI = hotbarSlots[i].GetComponentInChildren<InventoryItem>();
            if (itemUI != null && itemUI.item == itemData)
            {
                int removeCount = Mathf.Min(itemUI.count, remaining);
                itemUI.count -= removeCount;
                remaining -= removeCount;

                if (itemUI.count <= 0)
                {
                    Destroy(itemUI.gameObject);
                }
                else
                {
                    itemUI.RefreshCount();
                }

                if (remaining <= 0)
                    return; // Done removing
            }
        }

        // ✅ PRIORITY 3: Remove from main inventory (if still needed)
        if (remaining > 0)
        {
            foreach (var slot in mainInventorySlots)
            {
                InventoryItem itemUI = slot.GetComponentInChildren<InventoryItem>();
                if (itemUI != null && itemUI.item == itemData)
                {
                    int removeCount = Mathf.Min(itemUI.count, remaining);
                    itemUI.count -= removeCount;
                    remaining -= removeCount;

                    if (itemUI.count <= 0)
                        Destroy(itemUI.gameObject);
                    else
                        itemUI.RefreshCount();

                    if (remaining <= 0)
                        return; // Done removing
                }
            }
        }
    }

    public int GetItemCount(ItemData itemData)
    {
        if (itemCounts.TryGetValue(itemData, out int count))
        {
            return count;
        }
        return 0;
    }
}

// ==========================================
//  STARTER ITEM DATA STRUCTURE
// ==========================================
[System.Serializable]
public class StarterItem
{
    public ItemData item;
    public int amount = 1;
}