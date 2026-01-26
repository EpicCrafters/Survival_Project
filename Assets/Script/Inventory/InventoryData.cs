using Mirror;
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class StarterItem
{
    public ItemData itemId;
    public int amount = 1;
}
[System.Serializable]
public struct SplitBuffer
{
    public bool active;
    public int itemId;
    public int count;
    public int sourceSlot;
}

public class InventoryData : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnSplitBufferChanged))]
    private SplitBuffer splitBuffer;

    [SerializeField] private List<StarterItem> starterItems = new();

    [System.Serializable]

    public struct SlotState
    {
        public int itemId;
        public int count;
    }

    public readonly SyncList<SlotState> slots = new SyncList<SlotState>();

    private int totalSlots;
    //public InventoryView inventoryView;
    public override void OnStartServer()
    {
        base.OnStartServer();

        // Khởi tạo slot rỗng
        slots.Clear();
        totalSlots = SystemManager.Instance.GetComponentInChildren<InventoryView>().TotalSlots;
        for (int i = 0; i < totalSlots; i++)
        {
            slots.Add(new SlotState { itemId = -1, count = 0 });
        }
        foreach (var it in starterItems)
        {
            ServerAddItem(it.itemId.id, it.amount);
        }
    }

    // ===== SERVER API =====
    // ===== ADD ITEM =====

    [Server]
    public int ServerAddItem(int itemId, int amount)
    {
        if (amount <= 0) return 0;

        ItemData itemData = ItemDatabase.GetById(itemId);
        if (itemData == null)
        {
            Debug.LogWarning($"[SERVER] ItemData not found for ID: {itemId}");
            return 0;
        }

        int maxStack = GetMaxStack(itemData);
        int remaining = amount;
        int totalAdded = 0;

        // ===== PHASE 1: Stack into existing slots =====
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            var slot = slots[i];
            if (slot.itemId == itemId && slot.count < maxStack)
            {
                int canAdd = maxStack - slot.count;
                int add = Mathf.Min(canAdd, remaining);

                slot.count += add;
                remaining -= add;
                totalAdded += add;
                slots[i] = slot;
            }
        }

        // ===== PHASE 2: Create new slots =====
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (slots[i].itemId == -1)
            {
                int put = Mathf.Min(remaining, maxStack);
                slots[i] = new SlotState
                {
                    itemId = itemId,
                    count = put
                };
                remaining -= put;
                totalAdded += put;
            }
        }

        if (remaining > 0)
        {
            Debug.LogWarning($"[SERVER] Inventory full - couldn't add {remaining} of item {itemId}");
        }

        return totalAdded;
    }

    [Command]
    public void CmdAddItemWithStacking(int itemId, int amount)
    {
        ServerAddItem(itemId, amount);
    }
    [Command]
    public void CmdAddItem(int itemId, int amount)
    {
        ServerAddItem(itemId, amount);
    }
    // ===== MOVE ITEM =====
    [Server]
    private bool TryMoveServer(int from, int to)
    {
        if (from < 0 || from >= slots.Count || to < 0 || to >= slots.Count)
            return false;

        var a = slots[from];
        var b = slots[to];

        if (a.itemId < 0)
            return false;

        // Slot đích trống
        if (b.itemId < 0)
        {
            slots[to] = a;
            slots[from] = EmptySlot();
            return true;
        }

        // Cùng item → stack
        if (a.itemId == b.itemId)
        {
            ItemData data = ItemDatabase.Get(a.itemId);
            int maxStack = GetMaxStack(data);

            int canAdd = maxStack - b.count;
            if (canAdd <= 0)
                return false;

            int move = Mathf.Min(canAdd, a.count);
            b.count += move;
            slots[to] = b;

            if (a.count > move)
            {
                a.count -= move;
                slots[from] = a;
            }
            else
            {
                slots[from] = EmptySlot();
            }
            return true;
        }

        // Khác item → swap
        slots[from] = b;
        slots[to] = a;
        return true;
    }

    private int GetMaxStack(ItemData data)
    {
        switch (data.type)
        {
            case ItemType.Resource:
                return data.resource.maxStack;

            case ItemType.BuildingPart:
                return data.building.maxStack;
            case ItemType.Consumable:
                return data.consumable.maxStack;
            default:
                return 1;
        }
    }

    [Command]
    public void CmdRequestMove(int fromIndex, int toIndex)
    {
        if (!IsValidIndex(fromIndex) || !IsValidIndex(toIndex))
            return;

        TryMoveServer(fromIndex, toIndex);
        //SystemManager.Instance.GetComponentInChildren<InventoryView>().RedrawAll();
    }
    private SlotState EmptySlot()
    {
        return new SlotState { itemId = -1, count = 0 };
    }

    private bool IsValidIndex(int i)
    {
        return i >= 0 && i < slots.Count;
    }
    // ===== DROP ITEM =====
    [Command]
    public void CmdRequestDrop(int fromIndex, int count)
    {
        if (!IsValidIndex(fromIndex) || count <= 0)
            return;

        var slot = slots[fromIndex];
        if (slot.itemId < 0 || slot.count <= 0)
            return;

        int dropCount = Mathf.Min(count, slot.count);

        // 1) Trừ item trong inventory (SERVER)
        slot.count -= dropCount;
        slots[fromIndex] = (slot.count > 0) ? slot : EmptySlot();
        Debug.Log("Drop item to world");
        // 2) Spawn world item (SERVER)
        var playerNet = GetComponent<PlayerNetwork>();
        if (playerNet != null)
            playerNet.SpawnWorldItem(slot.itemId, dropCount);
    }
    // ===== SPLIT ITEM =====
    [Command]
    public void CmdRequestSplitHalf(int fromSlot)
    {
        Debug.Log($"[SERVER] SplitHalf request slot {fromSlot}");

        if (!IsValidIndex(fromSlot))
        {
            Debug.Log("[SERVER] Invalid slot index");
            return;
        }

        if (splitBuffer.active)
        {
            Debug.Log("[SERVER] Split buffer already active");
            return;
        }

        var slot = slots[fromSlot];
        Debug.Log($"[SERVER] Slot item={slot.itemId} count={slot.count}");

        if (slot.itemId < 0 || slot.count <= 1)
        {
            Debug.Log("[SERVER] Slot not splittable");
            return;
        }

        int split = slot.count / 2;
        int remain = slot.count - split;

        if (split <= 0) return;

        // Trừ slot gốc
        slot.count = remain;
        slots[fromSlot] = slot;

        // Tạo buffer
        splitBuffer = new SplitBuffer
        {
            active = true,
            itemId = slot.itemId,
            count = split,
            sourceSlot = fromSlot
        };
    }
    [Command]
    public void CmdPlaceSplit(int toSlot)
    {
        if (!splitBuffer.active) return;
        if (!IsValidIndex(toSlot)) return;

        var dest = slots[toSlot];

        // Slot trống
        if (dest.itemId < 0)
        {
            slots[toSlot] = new SlotState
            {
                itemId = splitBuffer.itemId,
                count = splitBuffer.count
            };
            splitBuffer = default;
            return;
        }

        // Cùng item → stack (chưa xét max stack ở phase này)
        if (dest.itemId == splitBuffer.itemId)
        {
            dest.count += splitBuffer.count;
            slots[toSlot] = dest;
            splitBuffer = default;
        }
    }
    [Command]
    public void CmdCancelSplit()
    {
        if (!splitBuffer.active) return;

        var src = splitBuffer.sourceSlot;
        if (IsValidIndex(src))
        {
            var slot = slots[src];
            slot.count += splitBuffer.count;
            slots[src] = slot;
        }

        splitBuffer = default;
    }
    //==========REMOVE=========
    [Command]
    public void CmdConsumeFromSlot(int slotIndex, int amount)
    {
        if (!IsValidIndex(slotIndex) || amount <= 0)
            return;

        var slot = slots[slotIndex];
        if (slot.itemId < 0 || slot.count < amount)
            return;

        slot.count -= amount;
        slots[slotIndex] = slot.count > 0 ? slot : EmptySlot();
    }

    public event System.Action<SplitBuffer> OnSplitBufferClient;

    private void OnSplitBufferChanged(SplitBuffer oldVal, SplitBuffer newVal)
    {
        OnSplitBufferClient?.Invoke(newVal);
    }
    public SlotState GetSlot(int index)
    {
        if (index < 0 || index >= slots.Count)
            return default;

        return slots[index];
    }
    //===========RETURN ITEM=========
    [Command]
    public void CmdReturnCraftingItems()
    {
        var view = SystemManager.Instance.GetComponentInChildren<InventoryView>();
        if (view == null) return;

        ReturnCraftingItemsServer(
            view.CraftingSlots,
            view.InventorySlots
        );
    }
    [Server]
    public void ReturnCraftingItemsServer(
    InventorySlot[] craftingSlots,
    InventorySlot[] inventorySlots)
    {
        foreach (var craftSlotUI in craftingSlots)
        {
            int craftIndex = craftSlotUI.index;
            var craftSlot = GetSlot(craftIndex);

            if (craftSlot.itemId < 0 || craftSlot.count <= 0)
                continue;

            int remain = craftSlot.count;

            ItemData data = ItemDatabase.Get(craftSlot.itemId);
            int maxStack = GetMaxStack(data);

            // ===== PHASE 1: GỘP VÀO STACK CÙNG LOẠI =====
            foreach (var invSlotUI in inventorySlots)
            {
                if (remain <= 0) break;

                int invIndex = invSlotUI.index;
                var invSlot = GetSlot(invIndex);

                if (invSlot.itemId == craftSlot.itemId &&
                    invSlot.count < maxStack)
                {
                    int canAdd = maxStack - invSlot.count;
                    int add = Mathf.Min(canAdd, remain);

                    invSlot.count += add;
                    remain -= add;
                    slots[invIndex] = invSlot;
                }
            }

            // ===== PHASE 2: ĐẨY VÀO SLOT TRỐNG =====
            foreach (var invSlotUI in inventorySlots)
            {
                if (remain <= 0) break;

                int invIndex = invSlotUI.index;
                var invSlot = GetSlot(invIndex);

                if (invSlot.itemId < 0)
                {
                    int put = Mathf.Min(remain, maxStack);

                    slots[invIndex] = new SlotState
                    {
                        itemId = craftSlot.itemId,
                        count = put
                    };

                    remain -= put;
                }
            }

            // ===== PHASE 3: UPDATE CRAFTING SLOT =====
            if (remain > 0)
            {
                // Chưa trả hết → để lại crafting slot
                slots[craftIndex] = new SlotState
                {
                    itemId = craftSlot.itemId,
                    count = remain
                };
            }
            else
            {
                // Trả hết → clear crafting slot
                slots[craftIndex] = EmptySlot();
            }
        }
    }
    //===========Crating Item==========
    [SerializeField]
    private List<int> craftingSlotIndices = new();

    [Command]
    public void CmdCraft(int recipeIndex)
    {
        var craftingManager = CraftingManager.Instance;
        if (craftingManager == null) return;

        var recipes = craftingManager.recipes;
        if (recipeIndex < 0 || recipeIndex >= recipes.Length) return;

        var recipe = recipes[recipeIndex];

        if (!ServerCanCraft(recipe)) return;

        ServerConsumeIngredients(recipe);
        ServerAddResult(recipe);
    }
    [Server]
    bool ServerCanCraft(CraftingRecipe recipe)
    {
        foreach (var ing in recipe.ingredients)
        {
            if (CountItemInCraftingServer(ing.item) < ing.amount)
                return false;
        }
        return true;
    }

    [Server]
    void ServerConsumeIngredients(CraftingRecipe recipe)
    {
        foreach (var ing in recipe.ingredients)
        {
            int remain = ing.amount;

            foreach (int index in craftingSlotIndices)
            {
                if (remain <= 0) break;

                var slot = slots[index];
                if (slot.itemId != ing.item.id) continue;

                int take = Mathf.Min(slot.count, remain);
                slot.count -= take;
                remain -= take;

                slots[index] =
                    slot.count > 0 ? slot : EmptySlot();
            }
        }
    }
    [Server]
    void ServerAddResult(CraftingRecipe recipe)
    {
        ServerAddItem(recipe.result.id, recipe.resultAmount);
    }

    [Command]
    public void CmdRegisterCraftingSlots(int[] indices)
    {
        craftingSlotIndices.Clear();
        craftingSlotIndices.AddRange(indices);
    }
    [Server]
    int CountItemInCraftingServer(ItemData item)
    {
        int total = 0;

        foreach (int index in craftingSlotIndices)
        {
            var slot = slots[index];
            if (slot.itemId == item.id)
                total += slot.count;
        }

        return total;
    }

    // ==================== RESOURCE INTERACTION COMMANDS ====================

    [Command]
    public void CmdDamageBush(string bushId, int damage)
    {
        if (WorldResourceManager.Instance != null)
        {
            WorldResourceManager.Instance.CmdDamageResource(bushId, damage);
        }
    }
}
