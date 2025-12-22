public class InventorySplitState
{
    public int sourceSlot = -1;   // slot gốc
    public ItemStack buffer;      // item đã tách ra

    public bool active => buffer != null;

    public void Reset()
    {
        sourceSlot = -1;
        buffer = null;
    }
}
