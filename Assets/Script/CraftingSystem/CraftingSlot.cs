using UnityEngine;
using UnityEngine.EventSystems;

public class CraftingSlot : SlotBase
{
    public override void OnDrop(PointerEventData eventData)
    {
        base.OnDrop(eventData); // Gọi xử lý drop của InventorySlot

        CraftingManager.Instance.UpdateAvailableRecipes();
    }
}
