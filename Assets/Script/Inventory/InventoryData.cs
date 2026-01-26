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

    public override void OnStartServer()
    {
        base.OnStartServer();

        // Initialize empty slots
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

    // ==================== CENTRALIZED ADD ITEM SYSTEM ====================

    /// <summary>
    /// MAIN SERVER METHOD - All item additions go through here
    /// Returns the number of items successfully added
    /// </summary>
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

    /// <summary>
    /// Check if inventory has space for items
    /// </summary>
    [Server]
    public bool CanAddItem(int itemId, int amount)
    {
        if (amount <= 0) return true;

        ItemData itemData = ItemDatabase.GetById(itemId);
        if (itemData == null) return false;

        int maxStack = GetMaxStack(itemData);
        int remaining = amount;

        // Check existing stacks
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            var slot = slots[i];
            if (slot.itemId == itemId && slot.count < maxStack)
            {
                int canAdd = maxStack - slot.count;
                remaining -= Mathf.Min(canAdd, remaining);
            }
        }

        // Check empty slots
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (slots[i].itemId == -1)
            {
                remaining -= Mathf.Min(remaining, maxStack);
            }
        }

        return remaining <= 0;
    }

    /// <summary>
    /// Helper to get max stack size for any item type
    /// </summary>
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

    // ==================== COMMAND WRAPPERS ====================

    [Command]
    public void CmdAddItem(int itemId, int amount)
    {
        ServerAddItem(itemId, amount);
    }

    [Command]
    public void CmdAddItemWithStacking(int itemId, int amount)
    {
        ServerAddItem(itemId, amount);
    }

    // ===== MOVE ITEM =====
    [Server]
    private void TryMoveServer(int from, int to)
    {
        if (from < 0 || from >= slots.Count || to < 0 || to >= slots.Count)
            return;

        var a = slots[from];
        var b = slots[to];

        // Source slot empty → abort
        if (a.itemId < 0)
            return;

        // =========================
        // 1. Destination slot empty
        // =========================
        if (b.itemId < 0)
        {
            slots[to] = a;
            slots[from] = EmptySlot();
            return;
        }

        // =========================
        // 2. Same item → stack with limit
        // =========================
        if (a.itemId == b.itemId)
        {
            ItemData data = ItemDatabase.Get(a.itemId);
            if (data == null)
                return;

            int maxStack = GetMaxStack(data);
            if (maxStack <= 0)
                return;

            int canAdd = maxStack - b.count;

            // Cannot add → do nothing
            if (canAdd <= 0)
                return;

            int move = Mathf.Min(canAdd, a.count);

            // Update destination slot
            b.count += move;
            slots[to] = b;

            // Update source slot
            if (a.count > move)
            {
                a.count -= move;
                slots[from] = a;
            }
            else
            {
                slots[from] = EmptySlot();
            }

            return;
        }

        // =========================
        // 3. Different item → swap
        // =========================
        slots[from] = b;
        slots[to] = a;
    }

    [Command]
    public void CmdRequestMove(int fromIndex, int toIndex)
    {
        if (!IsValidIndex(fromIndex) || !IsValidIndex(toIndex))
            return;

        TryMoveServer(fromIndex, toIndex);
        SystemManager.Instance.GetComponentInChildren<InventoryView>().RedrawAll();
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

        // 1) Remove item from inventory (SERVER)
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

        // Reduce source slot
        slot.count = remain;
        slots[fromSlot] = slot;

        // Create buffer
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

        // Empty slot
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

        // Same item → stack
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

    // ===== REMOVE =====
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