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

        // QUAN TRỌNG: báo cho item biết là đã drop vào slot
        item.droppedOnSlot = true;

        var input = SystemManager.Instance.GetComponentInChildren<InventoryInput>();
        if (input == null) return;

        input.RequestMove(from.index, to.index);
    }
}
