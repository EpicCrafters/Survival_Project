[System.Serializable]
public class ItemStack
{
    public ItemData data;
    public int count;

    public ItemStack(ItemData data, int count = 1)
    {
        this.data = data;
        this.count = count;
    }

    public bool IsStackable
    {
        get
        {
            if (data == null) return false;

            return data.IsStackable();
        }
    }

    public int MaxStack
    {
        get
        {
            if (data == null) return 1;


            return data.GetMaxStack();
        }
    }

    public bool CanAddMore => count < MaxStack;
}