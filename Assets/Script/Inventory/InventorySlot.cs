using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour, IDropHandler
{
    public Image image;
    public Color selectedColor, notSelectedColor;

    private void Awake()
    {
        Deselect();
    }

    public void Select()
    {
        image.color = selectedColor;
    }

    public void Deselect()
    {
        image.color = notSelectedColor;
    }

    public void OnDrop(PointerEventData eventData)
    {
        GameObject dropped = eventData.pointerDrag;
        InventoryItem droppedItem = dropped.GetComponent<InventoryItem>();

        // Item already in this slot?
        InventoryItem existingItem = GetComponentInChildren<InventoryItem>();

        if (existingItem == null)
        {
            // Slot is empty → just assign
            droppedItem.parentAfterDrag = transform;
        }
        else if (existingItem.item == droppedItem.item && droppedItem.item.stackable)
        {
            // Same item & stackable → merge
            int total = existingItem.count + droppedItem.count;
            int maxStack = droppedItem.item.maxStack;

            if (total <= maxStack)
            {
                existingItem.count = total;
                existingItem.RefreshCount();
                Destroy(droppedItem.gameObject);
            }
            else
            {
                int remaining = total - maxStack;
                existingItem.count = maxStack;
                existingItem.RefreshCount();

                droppedItem.count = remaining;
                droppedItem.RefreshCount();

                // Stay in old slot
                droppedItem.transform.SetParent(droppedItem.parentAfterDrag);
            }
        }
        else
        {
            // Occupied by different item → swap
            droppedItem.transform.SetParent(droppedItem.parentAfterDrag);
        }
    }
}
