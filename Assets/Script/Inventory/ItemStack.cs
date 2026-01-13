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

            switch (data.type)
            {
                case ItemType.Resource:
                    return data.resource != null && data.resource.stackable;

                case ItemType.BuildingPart:
                    return data.building != null && data.building.stackable;

                default:
                    return false;
            }
        }
    }
    public int MaxStack
    {
        get
        {
            if (data == null) return 1;

            switch (data.type)
            {
                case ItemType.Resource:
                    return data.resource != null ? data.resource.maxStack : 1;

                case ItemType.BuildingPart:
                    return data.building != null ? data.building.maxStack : 1;

                default:
                    return 1;
            }
        }
    }

    public bool CanAddMore => count < MaxStack;
}
