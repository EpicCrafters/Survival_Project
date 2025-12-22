using UnityEngine;
using UnityEngine.EventSystems;

public class SlotBase : MonoBehaviour, IDropHandler
{
    public virtual void OnDrop(PointerEventData eventData)
    {

        var obj = eventData.pointerDrag;
        if (obj == null) return;

        var item = obj.GetComponent<InventoryItem>();
        if (item == null) return;

        var from = item.originSlot;
        var to = GetComponent<InventorySlot>();

        if (from == null || to == null) return;

        item.droppedOnSlot = true;
        InventoryManager.instance.OnItemDropped(from, to);
    }

}
