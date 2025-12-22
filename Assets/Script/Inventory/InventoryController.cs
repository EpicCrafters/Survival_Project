using System.Collections.Generic;
using UnityEngine;

public class InventoryController
{
    // Mỗi phần tử có thể null hoặc ItemStack
    public List<ItemStack> slots;

    public InventoryController(int size)
    {
        // Khởi tạo list với 'size' phần tử null để truy cập theo index an toàn
        slots = new List<ItemStack>(size);
        for (int i = 0; i < size; i++)
            slots.Add(null);
    }

    public ItemStack GetSlot(int index)
    {
        if (index < 0 || index >= slots.Count) return null;
        return slots[index];
    }

    public bool AddItem(ItemData data)
    {
        // 1) Thử gộp trước
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s != null && s.data == data && s.IsStackable && s.CanAddMore)
            {
                s.count++;
                return true;
            }
        }

        // 2) Nếu không gộp, thêm vào ô trống
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = new ItemStack(data, 1);
                return true;
            }
        }

        return false;
    }

    // Thêm: cho phép thêm nguyên ItemStack (dùng khi return/cancel split)
    // Trả về true nếu toàn bộ 'stack' đã được đặt; nếu không có chỗ, có thể đặt 1 phần (vẫn trả về false)
    public bool AddStack(ItemStack stack)
    {
        if (stack == null || stack.count <= 0 || stack.data == null) return false;

        // 1) Gộp vào các ô cùng loại trước
        for (int i = 0; i < slots.Count && stack.count > 0; i++)
        {
            var s = slots[i];
            if (s != null && s.data == stack.data && s.IsStackable && s.CanAddMore)
            {
                int can = s.MaxStack - s.count;
                int move = Mathf.Min(can, stack.count);
                s.count += move;
                stack.count -= move;
            }
        }

        // 2) Đặt vào ô trống
        for (int i = 0; i < slots.Count && stack.count > 0; i++)
        {
            if (slots[i] == null)
            {
                int place = stack.IsStackable ? Mathf.Min(stack.count, stack.MaxStack) : 1;
                slots[i] = new ItemStack(stack.data, place);
                stack.count -= place;
            }
        }

        return stack.count <= 0;
    }

    // PlaceStackFully: đặt nguyên stack vào slot 'to' chỉ khi slot trống hoặc đủ chỗ để gộp hoàn toàn
    public bool PlaceStackFully(int to, ItemStack stack)
    {
        if (to < 0 || to >= slots.Count) return false;
        if (stack == null || stack.count <= 0 || stack.data == null) return false;

        var dest = slots[to];

        if (dest == null)
        {
            // đặt trực tiếp
            slots[to] = new ItemStack(stack.data, stack.count);
            return true;
        }

        // cùng loại và gộp được
        if (dest.data == stack.data && dest.IsStackable)
        {
            if (dest.count + stack.count <= dest.MaxStack)
            {
                dest.count += stack.count;
                return true;
            }
            else
            {
                // không đủ chỗ để đặt toàn bộ
                return false;
            }
        }

        // khác loại -> không thể đặt
        return false;
    }

    public void SwapSlots(int a, int b)
    {
        if (a < 0 || a >= slots.Count || b < 0 || b >= slots.Count) return;
        var temp = slots[a];
        slots[a] = slots[b];
        slots[b] = temp;
    }

    public bool Split(int index, int amount, out ItemStack newStack)
    {
        newStack = null;
        if (index < 0 || index >= slots.Count) return false;

        var slot = slots[index];

        if (slot == null || slot.count <= amount) return false;

        slot.count -= amount;
        newStack = new ItemStack(slot.data, amount);
        return true;
    }

    public bool MoveOrStack(int from, int to)
    {
        if (from < 0 || to < 0 || from >= slots.Count || to >= slots.Count)
            return false;

        var a = slots[from]; // item gốc
        var b = slots[to];   // item ở ô đích

        // Không có gì để di chuyển
        if (a == null) return false;

        // 1) Slot đích trống → chuyển thẳng
        if (b == null)
        {
            slots[to] = a;
            slots[from] = null;
            return true;
        }

        // 2) Cùng loại → stack
        if (a.data == b.data && a.IsStackable)
        {
            int canMove = b.MaxStack - b.count;

            if (canMove <= 0)
                return false;

            int moveAmount = Mathf.Min(a.count, canMove);

            b.count += moveAmount;
            a.count -= moveAmount;

            if (a.count <= 0)
                slots[from] = null;

            return true;
        }

        // 3) Khác loại → swap
        slots[from] = b;
        slots[to] = a;

        return true;
    }
    public bool PlaceStackPartial(int to, ItemStack stack)
    {
        if (to < 0 || to >= slots.Count) return false;
        if (stack == null || stack.count <= 0 || stack.data == null) return false;

        var dest = slots[to];

        // Slot trống
        if (dest == null)
        {
            slots[to] = new ItemStack(stack.data, stack.count);
            stack.count = 0;
            return true;
        }

        // Cùng loại → stack từng phần
        if (dest.data == stack.data && dest.IsStackable)
        {
            int canMove = dest.MaxStack - dest.count;
            if (canMove <= 0) return false;

            int move = Mathf.Min(canMove, stack.count);
            dest.count += move;
            stack.count -= move;

            return move > 0;
        }

        return false;
    }

}
