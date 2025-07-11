using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlot : SlotBase
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

    // KHÔNG cần override OnDrop nếu không thêm logic riêng
    // Nếu cần thêm: override rồi gọi base.OnDrop(eventData)
}
