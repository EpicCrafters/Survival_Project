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
            AddItemServer(it.itemId.id, it.amount);
        }
    }

    // ===== SERVER API =====
    // ===== ADD ITEM =====

    [Server]
    private void AddItemServer(int itemId, int amount)
    {
        int maxStack = GetMaxStack(ItemDatabase.GetById(itemId));

        // ===== PHASE 1: GỘP VÀO STACK CŨ =====
        for (int i = 0; i < slots.Count && amount > 0; i++)
        {
            var s = slots[i];
            if (s.itemId == itemId && s.count < maxStack)
            {
                int canAdd = maxStack - s.count;
                int add = Mathf.Min(canAdd, amount);

                s.count += add;
                amount -= add;
                slots[i] = s;
            }
        }
        // ===== PHASE 2: TẠO SLOT MỚI =====
        for (int i = 0; i < slots.Count && amount > 0; i++)
        {
            if (slots[i].itemId == -1)
            {
                int put = Mathf.Min(amount, maxStack);
                slots[i] = new SlotState
                {
                    itemId = itemId,
                    count = put
                };
                amount -= put;
            }
        }
    }

    [Command]
    public void CmdAddItemWithStacking(int itemId, int amount)
    {
        // Debug.Log($"[SERVER] AddItemWithStacking {itemId} x{amount}");

        // First, try to find existing slots with the same item and stack
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].itemId == itemId)
            {
                // Found matching item - add to stack
                var slot = slots[i];
                slot.count += amount;
                slots[i] = slot;
                // Debug.Log($"[SERVER] Stacked {amount} to existing slot {i}. New count: {slot.count}");
                return;
            }
        }

        // No existing stack found - add to first empty slot
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].itemId == -1)
            {
                slots[i] = new SlotState
                {
                    itemId = itemId,
                    count = amount
                };
                //Debug.Log($"[SERVER] Added new item to slot {i}");
                return;
            }
        }

        // Debug.LogWarning($"[SERVER] Inventory full! Could not add item {itemId}");
    }
    [Command]
    public void CmdAddItem(int itemId, int amount)
    {
        AddItemServer(itemId, amount);
    }
    // ===== MOVE ITEM =====
    [Server]
    private void TryMoveServer(int from, int to)
    {
        if (from < 0 || from >= slots.Count || to < 0 || to >= slots.Count)
            return;

        var a = slots[from];
        var b = slots[to];

        // Slot nguồn trống → bỏ
        if (a.itemId < 0)
            return;

        // =========================
        // 1. Slot đích trống
        // =========================
        if (b.itemId < 0)
        {
            slots[to] = a;
            slots[from] = EmptySlot();
            return;
        }

        // =========================
        // 2. Cùng item → stack có giới hạn
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

            // ❗ KHÔNG ADD ĐƯỢC → KHÔNG ĐỔI GÌ
            if (canAdd <= 0)
                return;

            int move = Mathf.Min(canAdd, a.count);

            // Cập nhật slot đích
            b.count += move;
            slots[to] = b;

            // Cập nhật slot nguồn
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
        // 3. Khác item → swap
        // =========================
        slots[from] = b;
        slots[to] = a;
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

}
