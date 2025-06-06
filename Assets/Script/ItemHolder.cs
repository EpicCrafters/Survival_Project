using UnityEngine;

public class ItemHolder : MonoBehaviour
{
    public Transform holdPoint;
    private PickupableItem currentItem;

    public void HoldItem(PickupableItem item)
    {
        currentItem = item;
        item.PickUp(holdPoint);
    }

    public void DropCurrentItem()
    {
        if (currentItem == null) return;

        currentItem.Drop();
        currentItem = null;
    }

    public bool HasItem()
    {
        return currentItem != null;
    }

    void Update()
    {
       
    }
}
