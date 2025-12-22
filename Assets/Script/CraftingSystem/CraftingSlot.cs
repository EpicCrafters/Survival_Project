using UnityEngine.EventSystems;
using UnityEngine;

public class CraftingSlot : SlotBase
{

    public int index;

    public override void OnDrop(PointerEventData eventData)
    {
        var obj = eventData.pointerDrag;
        if (obj == null) return;

        var item = obj.GetComponent<InventoryItem>();
        if (item == null) return;

        item.droppedOnSlot = true;

        item.transform.SetParent(transform, false);

        var rt = item.transform as RectTransform;
        rt.anchoredPosition = Vector2.zero;

        CraftingManager.Instance?.OnCraftingSlotChanged();
    }
}
